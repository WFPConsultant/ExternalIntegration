using UVP.ExternalIntegration.Domain.DTOs;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IIntegrationRunnerService
    {
        /// <summary>
        /// Standard entry point (used for queued & retry runs).
        /// The runner must reconstruct context from logs and metadata.
        /// </summary>
        Task ProcessInvocationAsync(long invocationId);

        /// <summary>
        /// Bootstrap entry point for the FIRST execution, when we still have the caller's DTO.
        /// Use this to build the initial model & write the first RequestPayload log row.
        /// </summary>
        Task ProcessInvocationAsync(long invocationId, IntegrationRequestDto bootstrapRequest);
    }
}
