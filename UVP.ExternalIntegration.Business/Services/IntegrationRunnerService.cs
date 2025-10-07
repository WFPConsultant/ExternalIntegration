using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class IntegrationRunnerService : IIntegrationRunnerService
    {
        private readonly IIntegrationInvocationRepository _invocationRepo;
        private readonly IGenericRepository<IntegrationInvocationLog> _invocationLogRepo;
        private readonly IIntegrationEndpointRepository _endpointRepo;
        private readonly IRenderingEngineService _renderingEngine;
        private readonly IHttpConnectorService _httpConnector;
        private readonly IResultMapperService _resultMapper;
        private readonly IModelLoaderService _modelLoader;
        private readonly IEarthMedTokenService _tokenService;
        private readonly ILogger _logger = Log.ForContext<IntegrationRunnerService>();

        public IntegrationRunnerService(
            IIntegrationInvocationRepository invocationRepo,
            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
            IIntegrationEndpointRepository endpointRepo,
            IRenderingEngineService renderingEngine,
            IHttpConnectorService httpConnector,
            IResultMapperService resultMapper,
            IModelLoaderService modelLoader,
            IEarthMedTokenService tokenService)
        {
            _invocationRepo = invocationRepo;
            _invocationLogRepo = invocationLogRepo;
            _endpointRepo = endpointRepo;
            _renderingEngine = renderingEngine;
            _httpConnector = httpConnector;
            _resultMapper = resultMapper;
            _modelLoader = modelLoader;
            _tokenService = tokenService;
        }

        // Queued / retry entry (no DTO): rebuild from first request log row.
        public Task ProcessInvocationAsync(long invocationId)
            => ExecuteIntegrationAsync(invocationId, bootstrapRequest: null);

        // First-run entry (from Swagger): we have the DTO for composing & logging the first request.
        public Task ProcessInvocationAsync(long invocationId, IntegrationRequestDto bootstrapRequest)
            => ExecuteIntegrationAsync(invocationId, bootstrapRequest);

        private async Task<bool> ExecuteIntegrationAsync(long invocationId, IntegrationRequestDto? bootstrapRequest)
        {
            var invocation = await _invocationRepo.GetByIdAsync(invocationId);
            if (invocation == null)
            {
                _logger.Warning("Invocation {InvocationId} not found", invocationId);
                return false;
            }

            IntegrationEndpointConfiguration? endpoint = null;

            try
            {
                _logger.Information("Executing invocation {InvocationId} for {IntegrationType}/{Operation}",
                    invocationId, invocation.IntegrationType, invocation.IntegrationOperation);

                // Mark in-progress and increment attempt
                invocation.IntegrationStatus = IntegrationStatus.IN_PROGRESS.ToString();
                invocation.AttemptCount++; // first execution becomes 1, retries increment further
                invocation.UpdatedOn = DateTime.UtcNow;
                invocation.UpdatedUser = "System";
                await _invocationRepo.UpdateAsync(invocation);
                await _invocationRepo.SaveChangesAsync();

                // Endpoint metadata (IsActive filtered inside repo)
                endpoint = await _endpointRepo.GetActiveEndpointAsync(
                               invocation.IntegrationType,
                               invocation.IntegrationOperation)
                           ?? throw new InvalidOperationException(
                               $"No active endpoint found for {invocation.IntegrationType}/{invocation.IntegrationOperation}");

                // === Build model ===
                object model;
                if (bootstrapRequest != null)
                {
                    // initial run uses bootstrap DTO
                    model = await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, bootstrapRequest);
                }
                else
                {
                    // retries/queued runs rebuild from first request log row
                    model = await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, invocation.IntegrationInvocationId);
                }

                // Build URL (supports {id} path param)
                var url = BuildUrlWithPathParameters(endpoint, model);

                // Prepare payload for POST/PUT also GET (FOR CMTS GET does not have body but we are keeping it to retrieve DoaCandidateId and CandidateId)
                string? payload = null;
                var methodUpper = endpoint.HttpMethod?.ToUpperInvariant();
                if ((methodUpper == "POST" || methodUpper == "PUT" || methodUpper == "GET") && !string.IsNullOrEmpty(endpoint.PayloadModelMapper))
                {
                    payload = await _renderingEngine.RenderPayloadAsync(endpoint.PayloadModelMapper, model);                    
                }

                // Logging sequence
                var existingLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
                var nextSequence = existingLogs.Any() ? existingLogs.Max(l => l.LogSequence) + 1 : 1;

                // Log request row
                var requestLog = new IntegrationInvocationLog
                {
                    IntegrationInvocationId = invocationId,
                    RequestPayload = payload,
                    ResponsePayload = null,
                    IntegrationStatus = IntegrationStatus.IN_PROGRESS.ToString(),
                    RequestSentOn = DateTime.UtcNow,
                    ResponseReceivedOn = null,
                    LogSequence = nextSequence,
                    CreatedOn = DateTime.UtcNow,
                    CreatedUser = "System"
                };
                await _invocationLogRepo.AddAsync(requestLog);
                await _invocationLogRepo.SaveChangesAsync();

                // ====================================================================
                // EARTHMED ENHANCEMENT: Prepare headers with Authorization token
                // ====================================================================
                var headers = new Dictionary<string, string>();

                // Add Authorization header for EARTHMED
                if (invocation.IntegrationType.Equals(IntegrationType.EARTHMED.ToString(), StringComparison.OrdinalIgnoreCase)) //EARTHMED
                {
                    var token = await _tokenService.GetAccessTokenAsync();
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        _logger.Error("[EARTHMED] Failed to obtain access token for invocation {InvocationId}", invocationId);
                        throw new InvalidOperationException("Failed to obtain EARTHMED access token");
                    }
                    headers["Authorization"] = $"Bearer {token}";
                    _logger.Debug("[EARTHMED] Added Bearer token to request for invocation {InvocationId}", invocationId);
                }

                // Build HTTP request with headers
                var httpRequest = new HttpRequestDto
                {
                    Url = url,
                    Method = endpoint.HttpMethod ?? "POST",
                    Payload = payload,
                    TimeoutSeconds = endpoint.TimeoutSeconds,
                    Headers = headers.Count > 0 ? headers : null
                };

                // Send HTTP (no internal retry; interval-based retries happen at job level)
                var response = await _httpConnector.SendRequestAsync(httpRequest, retryPolicy: null);
                // ====================================================================


                // ====================================================================
                // EARTHMED TOKEN INVALIDATION: Handle 401/400 by invalidating token
                // ====================================================================
                if (invocation.IntegrationType.Equals(IntegrationType.EARTHMED.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    // Check for 401 Unauthorized or specific token-related errors in a 400 Bad Request response
                    if (response.StatusCode == 401 || (response.StatusCode == 400 && IsTokenError(response.Body)))
                    {
                        _logger.Warning("[EARTHMED] Received {StatusCode} response, invalidating cached token for retry",
                            response.StatusCode);
                        // Invalidate the cached token
                        _tokenService.InvalidateToken();
                        // The retry mechanism will automatically fetch a fresh token on the next attempt
                    }
                }
                //=====================================================================

                // Compute retry budget and whether this is the last transaction
                var attemptedRetries = Math.Max(0, invocation.AttemptCount - 1);
                var isLastTransaction = !endpoint.Retrigger || attemptedRetries >= endpoint.RetriggerCount;

                // Log response row
                var responseLog = new IntegrationInvocationLog
                {
                    IntegrationInvocationId = invocationId,
                    RequestPayload = null,
                    ResponsePayload = response.Body,
                    ResponseStatusCode = response.StatusCode,
                    IntegrationStatus = response.IsSuccess
                        ? IntegrationStatus.SUCCESS.ToString()
                        : (isLastTransaction
                            ? IntegrationStatus.PERMANENTLY_FAILED.ToString()
                            : IntegrationStatus.FAILED.ToString()),
                    RequestSentOn = null,
                    ResponseReceivedOn = DateTime.UtcNow,
                    ResponseTimeMs = response.ResponseTimeMs,
                    ErrorDetails = response.StatusCode != 200
                                    ? BuildErrorDetails(response.StatusCode, response.Body, response.ErrorMessage)
                                    : null,
                    LogSequence = nextSequence + 1,
                    CreatedOn = DateTime.UtcNow,
                    CreatedUser = "System"
                };
                await _invocationLogRepo.AddAsync(responseLog);
                await _invocationLogRepo.SaveChangesAsync();

                if (response.IsSuccess)
                {
                    invocation.IntegrationStatus = IntegrationStatus.SUCCESS.ToString();

                    if (!string.IsNullOrWhiteSpace(response.Body))
                    {
                        await _resultMapper.ProcessResponseAsync(invocation, response.Body, invocation.IntegrationType);
                    }
                    else
                    {
                        // Handle "success with no body" by operation/type
                        await _resultMapper.ProcessResponseAsync(invocation, null, invocation.IntegrationType);
                    }

                    _logger.Information("Invocation {InvocationId} completed successfully", invocationId);
                }
                else
                {
                    if (endpoint.Retrigger && !isLastTransaction)
                    {
                        invocation.IntegrationStatus = IntegrationStatus.RETRY.ToString();
                        var minutes = Math.Max(1, endpoint.RetriggerInterval);
                        invocation.NextRetryTime = DateTime.UtcNow.AddMinutes(minutes);

                        _logger.Warning("Invocation {InvocationId} scheduled to retry at {RetryTime} (attempt {Attempt}/{Max})",
                            invocationId, invocation.NextRetryTime, attemptedRetries + 1, endpoint.RetriggerCount);
                    }
                    else
                    {
                        invocation.IntegrationStatus = IntegrationStatus.PERMANENTLY_FAILED.ToString();
                        invocation.NextRetryTime = null;

                        _logger.Error("Invocation {InvocationId} permanently failed after {Retries} retries",
                            invocationId, attemptedRetries);
                    }
                }

                invocation.UpdatedOn = DateTime.UtcNow;
                invocation.UpdatedUser = "System";
                await _invocationRepo.UpdateAsync(invocation);
                await _invocationRepo.SaveChangesAsync();

                return response.IsSuccess;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing invocation {InvocationId}", invocationId);

                // Write a FAILED error log row for traceability
                var existingLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
                var nextSequence = existingLogs.Any() ? existingLogs.Max(l => l.LogSequence) + 1 : 1;

                var errorLog = new IntegrationInvocationLog
                {
                    IntegrationInvocationId = invocationId,
                    RequestPayload = null,
                    ResponsePayload = null,
                    IntegrationStatus = IntegrationStatus.FAILED.ToString(),
                    ErrorDetails = ex.ToString(),
                    LogSequence = nextSequence,
                    CreatedOn = DateTime.UtcNow,
                    CreatedUser = "System",
                    RequestSentOn = DateTime.UtcNow
                };
                await _invocationLogRepo.AddAsync(errorLog);
                await _invocationLogRepo.SaveChangesAsync();

                // Decide RETRY vs PERMANENTLY_FAILED based on endpoint retry budget
                try
                {
                    endpoint ??= await _endpointRepo.GetActiveEndpointAsync(
                        invocation.IntegrationType, invocation.IntegrationOperation);

                    var attemptedRetries = Math.Max(0, invocation.AttemptCount - 1);
                    var hasBudget = endpoint != null && endpoint.Retrigger && attemptedRetries < endpoint.RetriggerCount;

                    if (hasBudget)
                    {
                        invocation.IntegrationStatus = IntegrationStatus.RETRY.ToString();
                        var minutes = Math.Max(1, endpoint!.RetriggerInterval);
                        invocation.NextRetryTime = DateTime.UtcNow.AddMinutes(minutes);

                        _logger.Warning("Invocation {InvocationId} (exception) scheduled to retry at {RetryTime} (attempt {Attempt}/{Max})",
                            invocationId, invocation.NextRetryTime, attemptedRetries + 1, endpoint!.RetriggerCount);
                    }
                    else
                    {
                        invocation.IntegrationStatus = IntegrationStatus.PERMANENTLY_FAILED.ToString();
                        invocation.NextRetryTime = null;

                        _logger.Error("Invocation {InvocationId} permanently failed due to exception; no retry budget left.", invocationId);
                    }

                    invocation.UpdatedOn = DateTime.UtcNow;
                    invocation.UpdatedUser = "System";
                    await _invocationRepo.UpdateAsync(invocation);
                    await _invocationRepo.SaveChangesAsync();
                }
                catch (Exception inner)
                {
                    _logger.Error(inner, "Failed to set terminal state after exception for invocation {InvocationId}", invocationId);
                }

                return false;
            }
        }

        // Helper method to check if the error is token-related
        private bool IsTokenError(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            // Check the response content for specific token-related error messages
            var bodyLower = responseBody.ToLower();
            return bodyLower.Contains("invalid_token") ||
                   bodyLower.Contains("token_expired") ||
                   bodyLower.Contains("expired_token") ||
                   bodyLower.Contains("unauthorized") ||
                   bodyLower.Contains("access_denied") ||
                   bodyLower.Contains("authentication");
        }

        private static string BuildErrorDetails(int statusCode, string? responseBody, string? errorMessage)
        {
            const int MaxLen = 4000;
            string Truncate(string? s) => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= MaxLen ? s : s[..MaxLen]);

            var parts = new List<string> { $"HTTP {statusCode}" };
            var err = Truncate(errorMessage);
            if (!string.IsNullOrEmpty(err)) parts.Add($"Error: {err}");
            var body = Truncate(responseBody);
            if (!string.IsNullOrEmpty(body)) parts.Add($"Body: {body}");
            return string.Join(" | ", parts);
        }

        private string BuildUrlWithPathParameters(IntegrationEndpointConfiguration endpoint, object model)
        {
            var baseUrl = endpoint.BaseUrl.TrimEnd('/');
            var pathTemplate = endpoint.PathTemplate.TrimStart('/');

            if (pathTemplate.Contains("{id}"))
            {
                string? paramValue = null;

                switch (endpoint.IntegrationOperation)
                {
                    case "GET_CLEARANCE_STATUS":
                        paramValue = GetPropertyValue(model, "DoaCandidateClearancesOneHR", "DoaCandidateClearanceId");
                        break;
                    case "SET_STATUS_DELIVERED":
                    case "ACKNOWLEDGE_RESPONSE":
                        paramValue = GetPropertyValue(model, "DoaCandidateClearancesOneHR", "RVCaseId");
                        break;
                }

                if (!string.IsNullOrEmpty(paramValue))
                    pathTemplate = pathTemplate.Replace("{id}", paramValue);
            }

            return $"{baseUrl}/{pathTemplate}";
        }

        private string? GetPropertyValue(object model, string entityName, string propertyName)
        {
            try
            {
                if (model is System.Collections.Generic.IDictionary<string, object> dict && dict.ContainsKey(entityName))
                {
                    var entity = dict[entityName];
                    if (entity != null)
                    {
                        var prop = entity.GetType().GetProperty(propertyName);
                        if (prop != null)
                        {
                            var value = prop.GetValue(entity);
                            return value?.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error getting property {Property} from {Entity}", propertyName, entityName);
            }

            return null;
        }
    }
}

//using Serilog;
//using UVP.ExternalIntegration.Business.Interfaces;
//using UVP.ExternalIntegration.Domain.DTOs;
//using UVP.ExternalIntegration.Domain.Entities;
//using UVP.ExternalIntegration.Domain.Enums;
//using UVP.ExternalIntegration.Repository.Interfaces;

//namespace UVP.ExternalIntegration.Business.Services
//{
//    public class IntegrationRunnerService : IIntegrationRunnerService
//    {
//        private readonly IIntegrationInvocationRepository _invocationRepo;
//        private readonly IGenericRepository<IntegrationInvocationLog> _invocationLogRepo;
//        private readonly IIntegrationEndpointRepository _endpointRepo;
//        private readonly IRenderingEngineService _renderingEngine;
//        private readonly IHttpConnectorService _httpConnector;
//        private readonly IResultMapperService _resultMapper;
//        private readonly IModelLoaderService _modelLoader;
//        private readonly IEarthMedTokenService _tokenService;
//        private readonly ILogger _logger = Log.ForContext<IntegrationRunnerService>();

//        public IntegrationRunnerService(
//            IIntegrationInvocationRepository invocationRepo,
//            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
//            IIntegrationEndpointRepository endpointRepo,
//            IRenderingEngineService renderingEngine,
//            IHttpConnectorService httpConnector,
//            IResultMapperService resultMapper,
//            IModelLoaderService modelLoader,
//             IEarthMedTokenService tokenService)
//        {
//            _invocationRepo = invocationRepo;
//            _invocationLogRepo = invocationLogRepo;
//            _endpointRepo = endpointRepo;
//            _renderingEngine = renderingEngine;
//            _httpConnector = httpConnector;
//            _resultMapper = resultMapper;
//            _modelLoader = modelLoader;
//            _tokenService = tokenService;
//        }

//        // Queued / retry entry (no DTO): rebuild from first request log row.
//        public Task ProcessInvocationAsync(long invocationId)
//            => ExecuteIntegrationAsync(invocationId, bootstrapRequest: null);

//        // First-run entry (from Swagger): we have the DTO for composing & logging the first request.
//        public Task ProcessInvocationAsync(long invocationId, IntegrationRequestDto bootstrapRequest)
//            => ExecuteIntegrationAsync(invocationId, bootstrapRequest);

//        private async Task<bool> ExecuteIntegrationAsync(long invocationId, IntegrationRequestDto? bootstrapRequest)
//        {
//            var invocation = await _invocationRepo.GetByIdAsync(invocationId);
//            if (invocation == null)
//            {
//                _logger.Warning("Invocation {InvocationId} not found", invocationId);
//                return false;
//            }

//            IntegrationEndpointConfiguration? endpoint = null;

//            try
//            {
//                _logger.Information("Executing invocation {InvocationId} for {IntegrationType}/{Operation}",
//                    invocationId, invocation.IntegrationType, invocation.IntegrationOperation);

//                // Mark in-progress and increment attempt
//                invocation.IntegrationStatus = IntegrationStatus.IN_PROGRESS.ToString();
//                invocation.AttemptCount++; // first execution becomes 1, retries increment further
//                invocation.UpdatedOn = DateTime.UtcNow;
//                invocation.UpdatedUser = "System";
//                await _invocationRepo.UpdateAsync(invocation);
//                await _invocationRepo.SaveChangesAsync();

//                // Endpoint metadata (IsActive filtered inside repo)
//                endpoint = await _endpointRepo.GetActiveEndpointAsync(
//                               invocation.IntegrationType,
//                               invocation.IntegrationOperation)
//                           ?? throw new InvalidOperationException(
//                               $"No active endpoint found for {invocation.IntegrationType}/{invocation.IntegrationOperation}");

//                // === Build model ===
//                object model;
//                if (bootstrapRequest != null)
//                {
//                    // initial run uses bootstrap DTO
//                    model = await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, bootstrapRequest);
//                }
//                else
//                {
//                    // retries/queued runs rebuild from first request log row
//                    model = await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, invocation.IntegrationInvocationId);
//                }

//                // Build URL (supports {id} path param)
//                var url = BuildUrlWithPathParameters(endpoint, model);

//                // Prepare payload for POST/PUT also GET (FOR CMTS GET does not have body but we are keeping it to retrieve DoaCandidateId and CandidateId)
//                string? payload = null;
//                var methodUpper = endpoint.HttpMethod?.ToUpperInvariant();
//                if ((methodUpper == "POST" || methodUpper == "PUT"  || methodUpper == "GET") && !string.IsNullOrEmpty(endpoint.PayloadModelMapper))
//                {
//                    payload = await _renderingEngine.RenderPayloadAsync(endpoint.PayloadModelMapper, model);
//                }

//                // Logging sequence
//                var existingLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
//                var nextSequence = existingLogs.Any() ? existingLogs.Max(l => l.LogSequence) + 1 : 1;

//                // Log request row
//                var requestLog = new IntegrationInvocationLog
//                {
//                    IntegrationInvocationId = invocationId,
//                    RequestPayload = payload,
//                    ResponsePayload = null,
//                    IntegrationStatus = IntegrationStatus.IN_PROGRESS.ToString(),
//                    RequestSentOn = DateTime.UtcNow,
//                    ResponseReceivedOn = null,
//                    LogSequence = nextSequence,
//                    CreatedOn = DateTime.UtcNow,
//                    CreatedUser = "System"
//                };
//                await _invocationLogRepo.AddAsync(requestLog);
//                await _invocationLogRepo.SaveChangesAsync();

//                // Send HTTP (no internal retry; interval-based retries happen at job level)                
//                var response = await _httpConnector.SendRequestAsync(
//                    url, endpoint.HttpMethod ?? "POST", payload, endpoint.TimeoutSeconds, retryPolicy: null);

//                // Compute retry budget and whether this is the last transaction
//                var attemptedRetries = Math.Max(0, invocation.AttemptCount - 1);
//                var isLastTransaction = !endpoint.Retrigger || attemptedRetries >= endpoint.RetriggerCount;

//                // Log response row
//                var responseLog = new IntegrationInvocationLog
//                {
//                    IntegrationInvocationId = invocationId,
//                    RequestPayload = null,
//                    ResponsePayload = response.Body,
//                    ResponseStatusCode = response.StatusCode,
//                    IntegrationStatus = response.IsSuccess
//                        ? IntegrationStatus.SUCCESS.ToString()
//                        : (isLastTransaction
//                            ? IntegrationStatus.PERMANENTLY_FAILED.ToString()
//                            : IntegrationStatus.FAILED.ToString()),
//                    RequestSentOn = null,
//                    ResponseReceivedOn = DateTime.UtcNow,
//                    ResponseTimeMs = response.ResponseTimeMs,
//                    ErrorDetails = response.StatusCode != 200
//                                    ? BuildErrorDetails(response.StatusCode, response.Body, response.ErrorMessage)
//                                    : null,
//                    LogSequence = nextSequence + 1,
//                    CreatedOn = DateTime.UtcNow,
//                    CreatedUser = "System"
//                };
//                await _invocationLogRepo.AddAsync(responseLog);
//                await _invocationLogRepo.SaveChangesAsync();

//                if (response.IsSuccess)
//                {
//                    invocation.IntegrationStatus = IntegrationStatus.SUCCESS.ToString();

//                    if (!string.IsNullOrWhiteSpace(response.Body))
//                    {
//                        await _resultMapper.ProcessResponseAsync(invocation, response.Body, invocation.IntegrationType);
//                    }
//                    else
//                    {
//                        // Handle "success with no body" by operation/type
//                        await _resultMapper.ProcessResponseAsync(invocation, null, invocation.IntegrationType);
//                    }

//                    _logger.Information("Invocation {InvocationId} completed successfully", invocationId);
//                }
//                else
//                {
//                    if (endpoint.Retrigger && !isLastTransaction)
//                    {
//                        invocation.IntegrationStatus = IntegrationStatus.RETRY.ToString();
//                        var minutes = Math.Max(1, endpoint.RetriggerInterval);
//                        invocation.NextRetryTime = DateTime.UtcNow.AddMinutes(minutes);

//                        _logger.Warning("Invocation {InvocationId} scheduled to retry at {RetryTime} (attempt {Attempt}/{Max})",
//                            invocationId, invocation.NextRetryTime, attemptedRetries + 1, endpoint.RetriggerCount);
//                    }
//                    else
//                    {
//                        invocation.IntegrationStatus = IntegrationStatus.PERMANENTLY_FAILED.ToString();
//                        invocation.NextRetryTime = null;

//                        _logger.Error("Invocation {InvocationId} permanently failed after {Retries} retries",
//                            invocationId, attemptedRetries);
//                    }
//                }

//                invocation.UpdatedOn = DateTime.UtcNow;
//                invocation.UpdatedUser = "System";
//                await _invocationRepo.UpdateAsync(invocation);
//                await _invocationRepo.SaveChangesAsync();

//                return response.IsSuccess;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error(ex, "Error executing invocation {InvocationId}", invocationId);

//                // Write a FAILED error log row for traceability
//                var existingLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
//                var nextSequence = existingLogs.Any() ? existingLogs.Max(l => l.LogSequence) + 1 : 1;

//                var errorLog = new IntegrationInvocationLog
//                {
//                    IntegrationInvocationId = invocationId,
//                    RequestPayload = null,
//                    ResponsePayload = null,
//                    IntegrationStatus = IntegrationStatus.FAILED.ToString(),
//                    ErrorDetails = ex.ToString(),
//                    LogSequence = nextSequence,
//                    CreatedOn = DateTime.UtcNow,
//                    CreatedUser = "System",
//                    RequestSentOn = DateTime.UtcNow
//                };
//                await _invocationLogRepo.AddAsync(errorLog);
//                await _invocationLogRepo.SaveChangesAsync();

//                // Decide RETRY vs PERMANENTLY_FAILED based on endpoint retry budget
//                try
//                {
//                    endpoint ??= await _endpointRepo.GetActiveEndpointAsync(
//                        invocation.IntegrationType, invocation.IntegrationOperation);

//                    var attemptedRetries = Math.Max(0, invocation.AttemptCount - 1);
//                    var hasBudget = endpoint != null && endpoint.Retrigger && attemptedRetries < endpoint.RetriggerCount;

//                    if (hasBudget)
//                    {
//                        invocation.IntegrationStatus = IntegrationStatus.RETRY.ToString();
//                        var minutes = Math.Max(1, endpoint!.RetriggerInterval);
//                        invocation.NextRetryTime = DateTime.UtcNow.AddMinutes(minutes);

//                        _logger.Warning("Invocation {InvocationId} (exception) scheduled to retry at {RetryTime} (attempt {Attempt}/{Max})",
//                            invocationId, invocation.NextRetryTime, attemptedRetries + 1, endpoint!.RetriggerCount);
//                    }
//                    else
//                    {
//                        invocation.IntegrationStatus = IntegrationStatus.PERMANENTLY_FAILED.ToString();
//                        invocation.NextRetryTime = null;

//                        _logger.Error("Invocation {InvocationId} permanently failed due to exception; no retry budget left.", invocationId);
//                    }

//                    invocation.UpdatedOn = DateTime.UtcNow;
//                    invocation.UpdatedUser = "System";
//                    await _invocationRepo.UpdateAsync(invocation);
//                    await _invocationRepo.SaveChangesAsync();
//                }
//                catch (Exception inner)
//                {
//                    _logger.Error(inner, "Failed to set terminal state after exception for invocation {InvocationId}", invocationId);
//                }

//                return false;
//            }
//        }

//        private static string BuildErrorDetails(int statusCode, string? responseBody, string? errorMessage)
//        {
//            const int MaxLen = 4000;
//            string Truncate(string? s) => string.IsNullOrWhiteSpace(s) ? "" : (s.Length <= MaxLen ? s : s[..MaxLen]);

//            var parts = new List<string> { $"HTTP {statusCode}" };
//            var err = Truncate(errorMessage);
//            if (!string.IsNullOrEmpty(err)) parts.Add($"Error: {err}");
//            var body = Truncate(responseBody);
//            if (!string.IsNullOrEmpty(body)) parts.Add($"Body: {body}");
//            return string.Join(" | ", parts);
//        }

//        private string BuildUrlWithPathParameters(IntegrationEndpointConfiguration endpoint, object model)
//        {
//            var baseUrl = endpoint.BaseUrl.TrimEnd('/');
//            var pathTemplate = endpoint.PathTemplate.TrimStart('/');

//            if (pathTemplate.Contains("{id}"))
//            {
//                string? paramValue = null;

//                switch (endpoint.IntegrationOperation)
//                {
//                    case "GET_CLEARANCE_STATUS":
//                        paramValue = GetPropertyValue(model, "DoaCandidateClearancesOneHR", "DoaCandidateClearanceId");
//                        break;
//                    case "SET_STATUS_DELIVERED":
//                    case "ACKNOWLEDGE_RESPONSE":
//                        paramValue = GetPropertyValue(model, "DoaCandidateClearancesOneHR", "RVCaseId");
//                        break;
//                }

//                if (!string.IsNullOrEmpty(paramValue))
//                    pathTemplate = pathTemplate.Replace("{id}", paramValue);
//            }

//            return $"{baseUrl}/{pathTemplate}";
//        }

//        private string? GetPropertyValue(object model, string entityName, string propertyName)
//        {
//            try
//            {
//                if (model is System.Collections.Generic.IDictionary<string, object> dict && dict.ContainsKey(entityName))
//                {
//                    var entity = dict[entityName];
//                    if (entity != null)
//                    {
//                        var prop = entity.GetType().GetProperty(propertyName);
//                        if (prop != null)
//                        {
//                            var value = prop.GetValue(entity);
//                            return value?.ToString();
//                        }
//                    }
//                }
//            }
//            catch (Exception ex)
//            {
//                _logger.Warning(ex, "Error getting property {Property} from {Entity}", propertyName, entityName);
//            }

//            return null;
//        }
//    }
//}
