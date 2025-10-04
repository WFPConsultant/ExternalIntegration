using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Repository.Interfaces
{
    public interface IIntegrationEndpointRepository : IGenericRepository<IntegrationEndpointConfiguration>
    {
        Task<IntegrationEndpointConfiguration?> GetActiveEndpointAsync(string integrationType, string operation);
    }
}
