using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class IntegrationOrchestrationService : IIntegrationOrchestrationService
    {
        private readonly IInvocationManagerService _invocationManager;
        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _clearancesOneHRRepo;
        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        private readonly ILogger _logger = Log.ForContext<IntegrationOrchestrationService>();

        public IntegrationOrchestrationService(
            IInvocationManagerService invocationManager,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo,
            IGenericRepository<DoaCandidateClearances> clearancesRepo)
        {
            _invocationManager = invocationManager;
            _clearancesOneHRRepo = clearancesOneHRRepo;
            _clearancesRepo = clearancesRepo;
        }

        public async Task<bool> ExecuteFullClearanceCycleAsync(int doaCandidateId, int candidateId, string integrationType)
        {
            try
            {
                _logger.Information("Starting full clearance cycle for DoaCandidate: {DoaCandidateId}, Candidate: {CandidateId}, Type: {Type}",
                    doaCandidateId, candidateId, integrationType);

                // Cycle 1: Create Clearance Request
                var createRequest = new IntegrationRequestDto
                {
                    DoaCandidateId = doaCandidateId,
                    CandidateId = candidateId,
                    IntegrationType = integrationType,
                    IntegrationOperation = IntegrationOperation.CREATE_CLEARANCE_REQUEST.ToString()
                };

                var invocationId = await _invocationManager.CreateInvocationAsync(createRequest);
                _logger.Information("Created clearance request invocation: {InvocationId}", invocationId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing full clearance cycle");
                return false;
            }
        }

        public async Task<bool> CheckAndProgressClearanceAsync(int doaCandidateId, int candidateId, string integrationType)
        {
            try
            {
                // Check if we have a clearance request ID (Cycle 1 complete)
                var clearanceOneHR = (await _clearancesOneHRRepo.FindAsync(
                    c => c.DoaCandidateId == doaCandidateId && c.CandidateId == candidateId))
                    .FirstOrDefault();

                if (clearanceOneHR == null)
                {
                    _logger.Warning("No clearance record found for DoaCandidate: {DoaCandidateId}", doaCandidateId);
                    return false;
                }

                var clearance = (await _clearancesRepo.FindAsync(
                    c => c.DoaCandidateId == doaCandidateId && c.RecruitmentClearanceCode == integrationType))
                    .FirstOrDefault();

                if (clearance == null)
                {
                    _logger.Warning("No clearance summary found for DoaCandidate: {DoaCandidateId}", doaCandidateId);
                    return false;
                }

                // If we have a clearance request ID but not completed, check status (Cycle 2)
                if (!string.IsNullOrEmpty(clearanceOneHR.DoaCandidateClearanceId) && !clearanceOneHR.IsCompleted)
                {
                    _logger.Information("Checking clearance status for request: {ClearanceRequestId}",
                        clearanceOneHR.DoaCandidateClearanceId);

                    var statusRequest = new IntegrationRequestDto
                    {
                        DoaCandidateId = doaCandidateId,
                        CandidateId = candidateId,
                        IntegrationType = integrationType,
                        IntegrationOperation = IntegrationOperation.GET_CLEARANCE_STATUS.ToString()
                    };

                    await _invocationManager.CreateInvocationAsync(statusRequest);
                    return true;
                }

                // If we have a response ID but not acknowledged, send acknowledgment (Cycle 3)
                if (!string.IsNullOrEmpty(clearanceOneHR.RVCaseId) && clearance.StatusCode != "DELIVERED")
                {
                    _logger.Information("Sending acknowledgment for response: {ResponseId}", clearanceOneHR.RVCaseId);

                    var ackRequest = new IntegrationRequestDto
                    {
                        DoaCandidateId = doaCandidateId,
                        CandidateId = candidateId,
                        IntegrationType = integrationType,
                        IntegrationOperation = IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString()
                    };

                    await _invocationManager.CreateInvocationAsync(ackRequest);
                    return true;
                }

                _logger.Information("Clearance cycle complete for DoaCandidate: {DoaCandidateId}", doaCandidateId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking and progressing clearance");
                return false;
            }
        }
    }
}