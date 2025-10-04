using Microsoft.EntityFrameworkCore;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Context;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Repository.Repositories
{
    public class IntegrationEndpointRepository : GenericRepository<IntegrationEndpointConfiguration>, IIntegrationEndpointRepository
    {
        public IntegrationEndpointRepository(UVPDbContext context) : base(context)
        {
        }

        public async Task<IntegrationEndpointConfiguration?> GetActiveEndpointAsync(string integrationType, string operation)
        {
            return await _dbSet
                .Where(e => e.IntegrationType == integrationType
                    && e.IntegrationOperation == operation
                    && e.IsActive)
                .FirstOrDefaultAsync();
        }
    }
}
