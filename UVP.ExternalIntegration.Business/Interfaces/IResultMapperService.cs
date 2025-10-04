using System.Threading.Tasks;
using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IResultMapperService
    {
        /// <summary>
        /// Map an external response body back into UVP domain state for the given invocation.
        /// Should not rely on DoaCandidateId/ReferenceId stored on the invocation row.
        /// </summary>
        Task ProcessResponseAsync(IntegrationInvocation invocation, string responseBody, string integrationType);
    }
}

//using UVP.ExternalIntegration.Domain.Entities;

//namespace UVP.ExternalIntegration.Business.Interfaces
//{
//    public interface IResultMapperService
//    {
//        Task ProcessResponseAsync(IntegrationInvocation invocation, string response, string integrationType);
//        Task<bool> MapResponseToEntitiesAsync(string integrationType, string operation, string response);
//    }
//}
