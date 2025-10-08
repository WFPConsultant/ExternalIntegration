using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class ResultMapperService : IResultMapperService
    {
        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _clearancesOneHRRepo;
        private readonly IGenericRepository<IntegrationInvocationLog> _invocationLogRepo;
        private readonly IResultMappingHandlerFactory _handlerFactory;
        private readonly IResultFieldExtractor _fieldExtractor;
        private readonly IGenericRepository<DoaCandidate> _doaCandidateRepo;
        private readonly ILogger _logger = Log.ForContext<ResultMapperService>();

        public ResultMapperService(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo,
            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
            IResultMappingHandlerFactory handlerFactory,
            IResultFieldExtractor fieldExtractor,
            IGenericRepository<DoaCandidate> doaCandidateRepo)
        {
            _clearancesRepo = clearancesRepo;
            _clearancesOneHRRepo = clearancesOneHRRepo;
            _invocationLogRepo = invocationLogRepo;
            _handlerFactory = handlerFactory;
            _fieldExtractor = fieldExtractor;
            _doaCandidateRepo = doaCandidateRepo;
        }

        public async Task ProcessResponseAsync(IntegrationInvocation invocation, string response, string integrationType)
        {
            try
            {
                _logger.Information("Processing response for {IntegrationType}/{Operation}", integrationType, invocation.IntegrationOperation);

                var handler = _handlerFactory.GetHandler(integrationType);
                if (handler == null)
                {
                    _logger.Warning("Unknown integration type: {Type}", integrationType);
                    return;
                }

                // Special handling for STATUS calls
                if (string.Equals(invocation.IntegrationOperation, IntegrationOperation.GET_CLEARANCE_STATUS.ToString(), StringComparison.OrdinalIgnoreCase) || string.Equals(invocation.IntegrationOperation, IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    var success = await ProcessStatusForSystemAsync(handler, invocation, response);
                    if (!success)
                        _logger.Warning("Failed to map status response for invocation {InvocationId}", invocation.IntegrationInvocationId);
                    return;
                }

                // Default path (CREATE / ACK etc.)
                var (doaCandidateId, candidateId) = await ResolveIdsFromFirstRequestAsync(invocation);

                var context = new ResultMappingContext
                {
                    DoaCandidateId = doaCandidateId,
                    CandidateId = candidateId,
                    IntegrationType = integrationType,
                    Operation = invocation.IntegrationOperation,
                    Response = response,
                    IntegrationInvocationId = invocation.IntegrationInvocationId
                };

                var ok = await ProcessOperationAsync(handler, context);

                if (!ok)
                {
                    _logger.Warning("Failed to map response for invocation {InvocationId}", invocation.IntegrationInvocationId);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing response for invocation {InvocationId}", invocation.IntegrationInvocationId);
                throw;
            }
        }

        private async Task<bool> ProcessOperationAsync(IResultMappingSystemHandler handler, ResultMappingContext context)
        {
            switch (context.Operation?.Trim().ToUpperInvariant())
            {
                case "CREATE_CLEARANCE_REQUEST":
                    var fields = _fieldExtractor.ExtractResponseFields(context.Response, handler.SystemCode);
                    return await handler.HandleCreateClearanceRequestAsync(context, fields);

                //case "ACKNOWLEDGE_RESPONSE":
                //    return await handler.HandleAcknowledgeResponseAsync(context);

                default:
                    _logger.Warning("Unknown operation for {System}: {Operation}", handler.SystemCode, context.Operation);
                    return false;
            }
        }

        private async Task<bool> ProcessStatusForSystemAsync(IResultMappingSystemHandler handler, IntegrationInvocation invocation, string response)
        {
            var systemCode = handler.SystemCode;

            // Find clearanceId from the CURRENT (status) request payload
            var latestReqPayload = await GetLatestRequestPayloadAsync(invocation.IntegrationInvocationId);
            if (latestReqPayload == null)
            {
                _logger.Warning("[{System}] No latest REQUEST payload found for status invocation {Id}", systemCode, invocation.IntegrationInvocationId);
                return false;
            }

            // EARTHMED-specific logic: Process all results in the response
            if (systemCode.Equals(IntegrationType.EARTHMED.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("[{System}] Processing EARTHMED status response with multiple results", systemCode);

                // For EARTHMED, we process all results in the response
                // The handler will match each result against DoaCandidateClearancesOneHR by IndexNumber (CandidateId)

                // Create a minimal context - the handler will process all results
                var earthMedContext = new ResultMappingContext
                {
                    DoaCandidateId = 0, // Will be determined per result in the handler
                    CandidateId = 0,    // Will be determined per result in the handler
                    IntegrationType = systemCode,
                    Operation = IntegrationOperation.GET_CLEARANCE_STATUS.ToString(),
                    Response = response,
                    IntegrationInvocationId = invocation.IntegrationInvocationId
                };

                // Pass null for fields and oneHrRecord - EARTHMED handler processes all results internally
                return await handler.HandleStatusResponseAsync(earthMedContext, null, null);
            }

            // CMTS and other systems - existing logic
            DoaCandidateClearancesOneHR? oneHr = null;

            var clearanceId_rvcaseId = _fieldExtractor.TryGetStringFromJsonAnyDepth(latestReqPayload,
                "id", "clearanceId", "clearanceRequestId", "requestId", "data.id", "payload.id");

            if (string.IsNullOrWhiteSpace(clearanceId_rvcaseId))
            {
                _logger.Warning("[{System}] Could not extract clearanceId from status request payload. Invocation {Id}",
                    systemCode, invocation.IntegrationInvocationId);
                return false;
            }

            // Locate OneHR link row by clearanceId
            oneHr = (await _clearancesOneHRRepo.FindAsync(o => o.DoaCandidateClearanceId == clearanceId_rvcaseId))
                        .OrderByDescending(o => o.RequestedDate)
                        .FirstOrDefault();

            if (oneHr == null)
            {
                oneHr = (await _clearancesOneHRRepo.FindAsync(o => o.RVCaseId == clearanceId_rvcaseId))
                        .OrderByDescending(o => o.RequestedDate)
                        .FirstOrDefault();
            }

            if (oneHr == null)
            {
                _logger.Warning("[{System}] OneHR row not found for clearanceId={ClearanceId}",
                    systemCode, clearanceId_rvcaseId);
                return false;
            }

            // Use system-specific field extraction for status responses
            var fields = _fieldExtractor.ExtractResponseFields(response, systemCode);

            var context = new ResultMappingContext
            {
                DoaCandidateId = oneHr.DoaCandidateId,
                CandidateId = oneHr.CandidateId,
                IntegrationType = systemCode,
                Operation = response == null
                    ? IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString()
                    : IntegrationOperation.GET_CLEARANCE_STATUS.ToString(),
                Response = response,
                IntegrationInvocationId = invocation.IntegrationInvocationId
            };

            return response == null
                    ? await handler.HandleAcknowledgeResponseAsync(context, fields, oneHr)
                    : await handler.HandleStatusResponseAsync(context, fields, oneHr);
        }
      
        private async Task<(long doaCandidateId, long candidateId)> ResolveIdsFromFirstRequestAsync(IntegrationInvocation invocation)
        {
            var firstRequestLog = (await _invocationLogRepo.FindAsync(l =>
                    l.IntegrationInvocationId == invocation.IntegrationInvocationId))
                .OrderBy(l => l.LogSequence)
                .FirstOrDefault();

            if (firstRequestLog != null && !string.IsNullOrWhiteSpace(firstRequestLog.RequestPayload))
            {
                try
                {
                    var payload = JToken.Parse(firstRequestLog.RequestPayload);
                    var doaCanId = _fieldExtractor.TryGetIntFromJsonAnyDepth(payload, "externalBatchId");
                    var candId = _fieldExtractor.TryGetIntFromJsonAnyDepth(payload, "externalRequestId");

                    if (doaCanId > 0 && candId > 0)
                        return (doaCanId, candId);


                    // For EARTHMED: extract from ReferenceNumber (format: DoaId_DoaCandidateId)
                    var referenceNumber = _fieldExtractor.TryGetStringFromJsonAnyDepth(payload, "ReferenceNumber");
                    if (!string.IsNullOrWhiteSpace(referenceNumber) && referenceNumber.Contains("_"))
                    {
                        var parts = referenceNumber.Split('_');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out var doaId) &&
                            int.TryParse(parts[1], out var doaCandidateId))
                        {

                            // For EARTHMED, we need to get CandidateId from DoaCandidateClearancesOneHR

                            var candidate = (await _doaCandidateRepo.FindAsync(x => x.DoaId == doaId))
                            .FirstOrDefault();

                            if (candidate != null)
                            {
                                _logger.Information("[EARTHMED] Resolved from ReferenceNumber: DoaId={DoaId}, DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                                    doaId, doaCandidateId, candidate.CandidateId);
                                return (doaCandidateId, candidate.CandidateId);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Could not parse first request payload for invocation {Id}", invocation.IntegrationInvocationId);
                }
            }

            throw new InvalidOperationException(
                $"Could not determine DoaCandidateId/CandidateId for invocation {invocation.IntegrationInvocationId}. " +
                $"Ensure the first REQUEST log contains externalBatchId/externalRequestId.");
        }

        private async Task<JToken?> GetLatestRequestPayloadAsync(long invocationId)
        {
            var latestRequest = (await _invocationLogRepo.FindAsync(l =>
                    l.IntegrationInvocationId == invocationId))
                .OrderBy(l => l.LogSequence)
                .FirstOrDefault();

            if (latestRequest == null || string.IsNullOrWhiteSpace(latestRequest.RequestPayload))
                return null;

            try
            {
                return JToken.Parse(latestRequest.RequestPayload);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error parsing latest request payload for invocation {Id}", invocationId);
                return null;
            }
        }
    }
}
