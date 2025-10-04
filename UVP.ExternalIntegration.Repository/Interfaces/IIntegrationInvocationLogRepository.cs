using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Repository.Interfaces
{
    public interface IIntegrationInvocationLogRepository : IGenericRepository<IntegrationInvocationLog>
    {
        /// <summary>
        /// Returns the FIRST log row for the invocation where RequestPayload IS NOT NULL.
        /// This is the bootstrap request row.
        /// </summary>
        Task<IntegrationInvocationLog?> GetFirstRequestLogAsync(long invocationId, CancellationToken ct = default);
    }
}
