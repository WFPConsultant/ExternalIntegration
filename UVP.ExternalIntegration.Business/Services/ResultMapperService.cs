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
        private readonly ILogger _logger = Log.ForContext<ResultMapperService>();

        public ResultMapperService(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo,
            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
            IResultMappingHandlerFactory handlerFactory,
            IResultFieldExtractor fieldExtractor)
        {
            _clearancesRepo = clearancesRepo;
            _clearancesOneHRRepo = clearancesOneHRRepo;
            _invocationLogRepo = invocationLogRepo;
            _handlerFactory = handlerFactory;
            _fieldExtractor = fieldExtractor;
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

            var clearanceId_rvcaseId = _fieldExtractor.TryGetStringFromJsonAnyDepth(latestReqPayload,
                "id", "clearanceId", "clearanceRequestId", "requestId", "data.id", "payload.id");

            if (string.IsNullOrWhiteSpace(clearanceId_rvcaseId))
            {
                _logger.Warning("[{System}] Could not extract clearanceId from status request payload. Invocation {Id}", systemCode, invocation.IntegrationInvocationId);
                return false;
            }

            // Locate OneHR link row by clearanceId
            var oneHr = (await _clearancesOneHRRepo.FindAsync(o => o.DoaCandidateClearanceId == clearanceId_rvcaseId))
                        .OrderByDescending(o => o.RequestedDate)
                        .FirstOrDefault();

            if (oneHr == null)
            {
                oneHr = (await _clearancesOneHRRepo.FindAsync(o => o.RVCaseId == clearanceId_rvcaseId))
                        .OrderByDescending(o => o.RequestedDate)
                        .FirstOrDefault();
            }
            if (oneHr == null) //if oneHr still null, log and exit
            {
                _logger.Warning("[{System}] OneHR row not found for clearanceId={ClearanceId}", systemCode, clearanceId_rvcaseId);
                return false;
            }

            // Use system-specific field extraction for status responses
            var fields = _fieldExtractor.ExtractResponseFields(response, systemCode);

            var context = new ResultMappingContext
            {
                DoaCandidateId = oneHr.DoaCandidateId,
                CandidateId = oneHr.CandidateId,
                IntegrationType = systemCode,
                Operation = response == null ? IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString() : IntegrationOperation.GET_CLEARANCE_STATUS.ToString(),//"GET_CLEARANCE_STATUS",
                Response = response,
                IntegrationInvocationId = invocation.IntegrationInvocationId
            };

            //return await handler.HandleStatusResponseAsync(context, fields, oneHr);
            return response == null
                    ? await handler.HandleAcknowledgeResponseAsync(context, fields, oneHr)
                    : await handler.HandleStatusResponseAsync(context, fields, oneHr);
        }

        private async Task<(int doaCandidateId, int candidateId)> ResolveIdsFromFirstRequestAsync(IntegrationInvocation invocation)
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
