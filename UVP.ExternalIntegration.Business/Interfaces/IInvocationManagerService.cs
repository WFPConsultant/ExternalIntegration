using UVP.ExternalIntegration.Domain.DTOs;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IInvocationManagerService
    {
        Task<long> CreateInvocationAsync(long doaCandidateId, long candidateId, string integrationType);
        Task<long> CreateInvocationAsync(IntegrationRequestDto request);
        Task<bool> ProcessPendingInvocationsAsync();
        Task<bool> ProcessRetryableInvocationsAsync();
    }
}
