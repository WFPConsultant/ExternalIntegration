using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Repository.Interfaces
{
    public interface IIntegrationInvocationRepository : IGenericRepository<IntegrationInvocation>
    {
        Task<IEnumerable<IntegrationInvocation>> GetPendingInvocationsAsync();
        Task<List<IntegrationInvocation>> GetRetryableInvocationsAsync(DateTime utcNow, int take = 200, CancellationToken ct = default);
    }
}
