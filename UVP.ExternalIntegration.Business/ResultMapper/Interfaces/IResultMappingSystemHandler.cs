using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;
using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Business.ResultMapper.Interfaces
{
    public interface IResultMappingSystemHandler
    {
        string SystemCode { get; }
        Task<bool> HandleCreateClearanceRequestAsync(ResultMappingContext context, ResultMappingFields fields);
        Task<bool> HandleAcknowledgeResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord);
        Task<bool> HandleStatusResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord);
    }
}
