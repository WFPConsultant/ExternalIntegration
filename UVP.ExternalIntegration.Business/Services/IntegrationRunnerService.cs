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
        private readonly ITokenService _tokenService;
        private readonly ILogger _logger = Log.ForContext<IntegrationRunnerService>();

        public IntegrationRunnerService(
            IIntegrationInvocationRepository invocationRepo,
            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
            IIntegrationEndpointRepository endpointRepo,
            IRenderingEngineService renderingEngine,
            IHttpConnectorService httpConnector,
            IResultMapperService resultMapper,
            IModelLoaderService modelLoader,
            ITokenService tokenService)
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

        public Task ProcessInvocationAsync(long invocationId)
            => ExecuteIntegrationAsync(invocationId, bootstrapRequest: null);

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
                invocation.AttemptCount++;
                invocation.UpdatedOn = DateTime.UtcNow;
                invocation.UpdatedUser = "System";
                await _invocationRepo.UpdateAsync(invocation);
                await _invocationRepo.SaveChangesAsync();

                // Get endpoint configuration
                endpoint = await _endpointRepo.GetActiveEndpointAsync(
                               invocation.IntegrationType,
                               invocation.IntegrationOperation)
                           ?? throw new InvalidOperationException(
                               $"No active endpoint found for {invocation.IntegrationType}/{invocation.IntegrationOperation}");

                // Build model
                object model = bootstrapRequest != null
                    ? await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, bootstrapRequest)
                    : await _modelLoader.LoadModelDataAsync(endpoint.UVPDataModel, invocation.IntegrationInvocationId);

                // Build URL with path parameters
                var url = BuildUrlWithPathParameters(endpoint, model);

                // Prepare payload for POST/PUT/GET operations
                string? payload = null;
                var methodUpper = endpoint.HttpMethod?.ToUpperInvariant();
                if ((methodUpper == "POST" || methodUpper == "PUT" || methodUpper == "GET")
                    && !string.IsNullOrEmpty(endpoint.PayloadModelMapper))
                {
                    payload = await _renderingEngine.RenderPayloadAsync(endpoint.PayloadModelMapper, model);
                }

                // Log request
                var nextSequence = await GetNextLogSequenceAsync(invocationId);
                await LogRequestAsync(invocationId, payload, nextSequence);

                // Prepare headers with authentication if required
                var headers = await BuildRequestHeadersAsync(invocation.IntegrationType);

                // Build HTTP request
                var httpRequest = new HttpRequestDto
                {
                    Url = url,
                    Method = endpoint.HttpMethod ?? "POST",
                    Payload = payload,
                    TimeoutSeconds = endpoint.TimeoutSeconds,
                    Headers = headers
                };

                // Send HTTP request
                var response = await _httpConnector.SendRequestAsync(httpRequest, retryPolicy: null);

                // Handle token-related errors for OAuth integrations
                await HandleTokenErrorsAsync(invocation.IntegrationType, response);

                // Determine retry eligibility
                var attemptedRetries = Math.Max(0, invocation.AttemptCount - 1);
                var isLastTransaction = !endpoint.Retrigger || attemptedRetries >= endpoint.RetriggerCount;

                // Log response
                await LogResponseAsync(invocationId, response, nextSequence + 1, isLastTransaction);

                // Process response or handle failure
                if (response.IsSuccess)
                {
                    await HandleSuccessResponseAsync(invocation, response);
                }
                else
                {
                    await HandleFailedResponseAsync(invocation, endpoint, isLastTransaction, attemptedRetries);
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
                await HandleExecutionExceptionAsync(invocationId, invocation, endpoint, ex);
                return false;
            }
        }

        private async Task<Dictionary<string, string>?> BuildRequestHeadersAsync(string integrationType)
        {
            var token = await _tokenService.GetAccessTokenAsync(integrationType);

            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            _logger.Debug("[{IntegrationType}] Added Bearer token to request", integrationType);

            return new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            };
        }

        private async Task HandleTokenErrorsAsync(string integrationType, HttpResponseDto response)
        {
            // Check for 401 Unauthorized or token-related errors in 400 Bad Request
            if (response.StatusCode == 401 || (response.StatusCode == 400 && IsTokenError(response.Body)))
            {
                _logger.Warning("[{IntegrationType}] Received {StatusCode} response, invalidating cached token",
                    integrationType, response.StatusCode);
                _tokenService.InvalidateToken(integrationType);
            }
        }

        private bool IsTokenError(string? responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            var bodyLower = responseBody.ToLower();
            return bodyLower.Contains("invalid_token") ||
                   bodyLower.Contains("token_expired") ||
                   bodyLower.Contains("expired_token") ||
                   bodyLower.Contains("unauthorized") ||
                   bodyLower.Contains("access_denied") ||
                   bodyLower.Contains("authentication");
        }

        private async Task<int> GetNextLogSequenceAsync(long invocationId)
        {
            var existingLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
            return existingLogs.Any() ? existingLogs.Max(l => l.LogSequence) + 1 : 1;
        }

        private async Task LogRequestAsync(long invocationId, string? payload, int sequence)
        {
            var requestLog = new IntegrationInvocationLog
            {
                IntegrationInvocationId = invocationId,
                RequestPayload = payload,
                ResponsePayload = null,
                IntegrationStatus = IntegrationStatus.IN_PROGRESS.ToString(),
                RequestSentOn = DateTime.UtcNow,
                ResponseReceivedOn = null,
                LogSequence = sequence,
                CreatedOn = DateTime.UtcNow,
                CreatedUser = "System"
            };
            await _invocationLogRepo.AddAsync(requestLog);
            await _invocationLogRepo.SaveChangesAsync();
        }

        private async Task LogResponseAsync(long invocationId, HttpResponseDto response, int sequence, bool isLastTransaction)
        {
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
                LogSequence = sequence,
                CreatedOn = DateTime.UtcNow,
                CreatedUser = "System"
            };
            await _invocationLogRepo.AddAsync(responseLog);
            await _invocationLogRepo.SaveChangesAsync();
        }

        private async Task HandleSuccessResponseAsync(IntegrationInvocation invocation, HttpResponseDto response)
        {
            invocation.IntegrationStatus = IntegrationStatus.SUCCESS.ToString();

            if (!string.IsNullOrWhiteSpace(response.Body))
            {
                await _resultMapper.ProcessResponseAsync(invocation, response.Body, invocation.IntegrationType);
            }
            else
            {
                await _resultMapper.ProcessResponseAsync(invocation, null, invocation.IntegrationType);
            }

            _logger.Information("Invocation {InvocationId} completed successfully", invocation.IntegrationInvocationId);
        }

        private async Task HandleFailedResponseAsync(
            IntegrationInvocation invocation,
            IntegrationEndpointConfiguration endpoint,
            bool isLastTransaction,
            int attemptedRetries)
        {
            if (endpoint.Retrigger && !isLastTransaction)
            {
                invocation.IntegrationStatus = IntegrationStatus.RETRY.ToString();
                var minutes = Math.Max(1, endpoint.RetriggerInterval);
                invocation.NextRetryTime = DateTime.UtcNow.AddMinutes(minutes);

                _logger.Warning("Invocation {InvocationId} scheduled to retry at {RetryTime} (attempt {Attempt}/{Max})",
                    invocation.IntegrationInvocationId, invocation.NextRetryTime, attemptedRetries + 1, endpoint.RetriggerCount);
            }
            else
            {
                invocation.IntegrationStatus = IntegrationStatus.PERMANENTLY_FAILED.ToString();
                invocation.NextRetryTime = null;

                _logger.Error("Invocation {InvocationId} permanently failed after {Retries} retries",
                    invocation.IntegrationInvocationId, attemptedRetries);
            }
        }

        private async Task HandleExecutionExceptionAsync(
            long invocationId,
            IntegrationInvocation invocation,
            IntegrationEndpointConfiguration? endpoint,
            Exception ex)
        {
            // Log error
            var nextSequence = await GetNextLogSequenceAsync(invocationId);
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

            // Determine retry status
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

                    _logger.Error("Invocation {InvocationId} permanently failed due to exception", invocationId);
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