using Microsoft.EntityFrameworkCore;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Context;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Repository.Repositories
{
    public class IntegrationInvocationLogRepository : GenericRepository<IntegrationInvocationLog>, IIntegrationInvocationLogRepository
    {
        public IntegrationInvocationLogRepository(UVPDbContext db) : base(db) { }

        public async Task<IntegrationInvocationLog?> GetFirstRequestLogAsync(long invocationId, CancellationToken ct = default)
        {
            return await _dbSet
                .Where(x => x.IntegrationInvocationId == invocationId && x.RequestPayload != null)
                .OrderBy(x => x.CreatedOn)
                .FirstOrDefaultAsync(ct);
        }
    }
}
