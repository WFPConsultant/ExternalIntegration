using Newtonsoft.Json;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;
using UVP.ExternalIntegration.Business.ResultMapper.Services;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.ResultMapper.Handlers
{
    public class CmtsResultMappingHandler : BaseResultMappingHandler
    {
        public CmtsResultMappingHandler(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo)
            : base(clearancesRepo, clearancesOneHRRepo)
        {
        }

        public override string SystemCode => "CMTS";

        public override async Task<bool> HandleCreateClearanceRequestAsync(ResultMappingContext context, ResultMappingFields fields)
        {
            try
            {
                // CMTS has special handling for create response using ClearanceResponseDto
                var responseDto = JsonConvert.DeserializeObject<ClearanceResponseDto>(context.Response);
                if (responseDto == null || string.IsNullOrEmpty(responseDto.ClearanceRequestId))
                {
                    _logger.Error("Invalid CMTS create clearance response");
                    return false;
                }

                _logger.Information("CYCLE 1 - CREATE: ClearanceRequestId={RequestId}, Status={Status}",
                    responseDto.ClearanceRequestId, responseDto.Status);

                var clearanceOneHR = new DoaCandidateClearancesOneHR
                {
                    DoaCandidateId = context.DoaCandidateId,
                    CandidateId = context.CandidateId,
                    DoaCandidateClearanceId = responseDto.ClearanceRequestId,
                    RequestedDate = DateTime.UtcNow,
                    IsCompleted = false,
                    Retry = 0
                };
                await _clearancesOneHRRepo.AddAsync(clearanceOneHR);
                await _clearancesOneHRRepo.SaveChangesAsync();

                var clearances = new DoaCandidateClearances
                {
                    DoaCandidateId = context.DoaCandidateId,
                    RecruitmentClearanceCode = SystemCode,
                    RequestedDate = DateTime.UtcNow,
                    StatusCode = "CLEARANCE_REQUESTED",
                    LinkDetailRemarks = $"clearanceRequestId={responseDto.ClearanceRequestId}",
                    UpdatedDate = DateTime.UtcNow
                };
                await _clearancesRepo.AddAsync(clearances);
                await _clearancesRepo.SaveChangesAsync();

                _logger.Information("CYCLE 1 Complete: Persisted with StatusCode=CLEARANCE_REQUESTED");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in CMTS HandleCreateClearanceRequestAsync");
                return false;
            }
        }
    } 
}
