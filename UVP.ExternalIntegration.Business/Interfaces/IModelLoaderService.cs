using UVP.ExternalIntegration.Domain.DTOs;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IModelLoaderService
    {
        /// <summary>
        /// Used on retries/queued runs: rebuild from the FIRST request log row (RequestPayload != null).
        /// </summary>
        Task<object> LoadModelDataAsync(string uvpDataModel, long integrationInvocationId);

        /// <summary>
        /// Used on the very first execution to build the model directly from the caller's DTO,
        /// before any logs exist. This lets the runner render and log the initial request payload.
        /// </summary>
        Task<object> LoadModelDataAsync(string uvpDataModel, IntegrationRequestDto bootstrapRequest);
    }
}
