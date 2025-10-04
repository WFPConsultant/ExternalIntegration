using Hangfire;
using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    /// <summary>
    /// IMPORTANT: Add this overload to IIntegrationRunnerService to keep initial (Swagger) flow intact:
    /// Task ProcessInvocationAsync(long integrationInvocationId, IntegrationRequestDto bootstrapRequest);
    /// Existing method ProcessInvocationAsync(long) remains for retries / queued runs.
    /// </summary>
    public class InvocationManagerService : IInvocationManagerService
    {
        private readonly IIntegrationInvocationRepository _invocationRepo;
        private readonly IIntegrationRunnerService _integrationRunner;
        private readonly ILogger _logger = Log.ForContext<InvocationManagerService>();
        private readonly IBackgroundJobClient _backgroundJobs;

        public InvocationManagerService(
            IIntegrationInvocationRepository invocationRepo,
            IBackgroundJobClient backgroundJobs,
            IIntegrationRunnerService integrationRunner)
        {
            _invocationRepo = invocationRepo;
            _backgroundJobs = backgroundJobs;
            _integrationRunner = integrationRunner;
        }

        /// <summary>
        /// Convenience overload kept for Swagger or any caller that has the two IDs handy.
        /// NOTE: We DO NOT persist these IDs on the invocation row anymore.
        /// They are only used to build/log the first RequestPayload during the initial run.
        /// </summary>
        public async Task<long> CreateInvocationAsync(int doaCandidateId, int candidateId, string integrationType)
        {
            var request = new IntegrationRequestDto
            {
                DoaCandidateId = doaCandidateId,
                CandidateId = candidateId,
                IntegrationType = integrationType,
                IntegrationOperation = IntegrationOperation.CREATE_CLEARANCE_REQUEST.ToString()
            };

            return await CreateInvocationAsync(request);
        }

        /// <summary>
        /// Create a minimal Invocation row (no DoaCandidateId/ReferenceId/ExternalReferenceId).
        /// Immediately process the invocation ONCE with the provided bootstrap request,
        /// so the runner can produce and log the initial RequestPayload (first log row).
        /// </summary>
        public async Task<long> CreateInvocationAsync(IntegrationRequestDto request)
        {
            try
            {
                var invocation = new IntegrationInvocation
                {
                    // NO: DoaCandidateId / ReferenceId / ExternalReferenceId — removed by design
                    IntegrationType = request.IntegrationType,
                    IntegrationOperation = request.IntegrationOperation ?? IntegrationOperation.CREATE_CLEARANCE_REQUEST.ToString(),
                    IntegrationStatus = IntegrationStatus.PENDING.ToString(),
                    AttemptCount = 0,
                    IsActive = true,
                    CreatedOn = DateTime.UtcNow,
                    CreatedUser = "System",
                    UpdatedOn = DateTime.UtcNow,
                    UpdatedUser = "System"
                };

                await _invocationRepo.AddAsync(invocation);
                await _invocationRepo.SaveChangesAsync();

                _logger.Information("Created invocation {InvocationId} for {IntegrationType}/{Operation}",
                    invocation.IntegrationInvocationId, invocation.IntegrationType, invocation.IntegrationOperation);

                // IMPORTANT:
                // For the very first execution we pass the bootstrap request DTO
                // so the runner can compose and LOG the first request payload.
                await _integrationRunner.ProcessInvocationAsync(invocation.IntegrationInvocationId, request);

                return invocation.IntegrationInvocationId;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating invocation");
                throw;
            }
        }

        public async Task<bool> ProcessPendingInvocationsAsync()
        {
            try
            {
                var pendingInvocations = await _invocationRepo.GetPendingInvocationsAsync();

                foreach (var invocation in pendingInvocations)
                {
                    _logger.Information("Processing pending invocation {InvocationId}", invocation.IntegrationInvocationId);
                    // For queued/pending runs we don’t have a bootstrap DTO; the runner should
                    // load/resolve from logs if needed.
                    await _integrationRunner.ProcessInvocationAsync(invocation.IntegrationInvocationId);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing pending invocations");
                return false;
            }
        }

        /// <summary>
        /// Hangfire job: pick due RETRY rows and re-queue them.
        /// Retried runs reconstruct context solely from the first RequestPayload log row.
        /// </summary>
        public async Task<bool> ProcessRetryableInvocationsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                var retryableInvocations =
                    await _invocationRepo.GetRetryableInvocationsAsync(now, take: 200);

                foreach (var invocation in retryableInvocations)
                {
                    // Flip to PENDING and clear schedule marker to avoid double-processing
                    invocation.IntegrationStatus = IntegrationStatus.PENDING.ToString();
                    invocation.NextRetryTime = null;
                    invocation.UpdatedOn = now;
                    invocation.UpdatedUser = "System";

                    await _invocationRepo.UpdateAsync(invocation);
                    await _invocationRepo.SaveChangesAsync();

                    _logger.Information("Retrying invocation {InvocationId}", invocation.IntegrationInvocationId);

                    // Enqueue the standard runner entry (no DTO) — it will rebuild from log payload.
                    _backgroundJobs.Enqueue<IIntegrationRunnerService>(r =>
                        r.ProcessInvocationAsync(invocation.IntegrationInvocationId));
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing retryable invocations");
                return false;
            }
        }
    }
}
