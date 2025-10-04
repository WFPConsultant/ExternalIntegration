using Newtonsoft.Json;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;
using UVP.ExternalIntegration.Business.ResultMapper.Services;
using UVP.ExternalIntegration.Domain.DTOs.EarthMed;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.ResultMapper.Handlers
{
    public class EarthMedResultMappingHandler : BaseResultMappingHandler
    {
        public EarthMedResultMappingHandler(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo)
            : base(clearancesRepo, clearancesOneHRRepo)
        {
        }

        public override string SystemCode => "EARTHMED";

        public override async Task<bool> HandleCreateClearanceRequestAsync(ResultMappingContext context, ResultMappingFields fields)
        {
            try
            {
                var responseDto = JsonConvert.DeserializeObject<EarthMedCreateClearanceResponseDto>(context.Response);
                if (responseDto == null || !responseDto.IsSuccess || responseDto.Result == null)
                {
                    _logger.Error("[EARTHMED] Invalid create clearance response: IsSuccess={IsSuccess}",
                        responseDto?.IsSuccess ?? false);
                    return false;
                }

                var result = responseDto.Result;
                var idString = result.Id.ToString();
                var indexNumber = result.IndexNumber ?? string.Empty;

                _logger.Information("[EARTHMED] CYCLE 1 - CREATE: Id={Id}, IndexNumber={IndexNumber}, IsSuccess={IsSuccess}",
                    result.Id, indexNumber, responseDto.IsSuccess);

                var clearanceOneHR = new DoaCandidateClearancesOneHR
                {
                    DoaCandidateId = context.DoaCandidateId,
                    CandidateId = context.CandidateId,
                    DoaCandidateClearanceId = idString,
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
                    LinkDetailRemarks = $"Id={idString};IndexNumber={indexNumber}",
                    UpdatedDate = DateTime.UtcNow
                };
                await _clearancesRepo.AddAsync(clearances);
                await _clearancesRepo.SaveChangesAsync();

                _logger.Information("[EARTHMED] CYCLE 1 Complete: Persisted with StatusCode=CLEARANCE_REQUESTED");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error in HandleCreateClearanceRequestAsync");
                return false;
            }
        }

        public override async Task<bool> HandleStatusResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        {
            try
            {
                var responseDto = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(context.Response);
                if (responseDto == null || !responseDto.IsSuccess || responseDto.Result == null || !responseDto.Result.Any())
                {
                    _logger.Warning("[EARTHMED] GET_CLEARANCE_STATUS: No results or IsSuccess=false");
                    return false;
                }

                var statusResult = responseDto.Result.FirstOrDefault(r =>
                    r.IndexNumber == oneHrRecord.DoaCandidateClearanceId ||
                    r.Id.ToString() == oneHrRecord.DoaCandidateClearanceId);

                if (statusResult == null)
                {
                    _logger.Warning("[EARTHMED] No matching status record found for Id/IndexNumber={Id}",
                        oneHrRecord.DoaCandidateClearanceId);
                    return false;
                }

                _logger.Information("[EARTHMED] STATUS: Id={Id}, Status={Status}, IndexNumber={IndexNumber}",
                    statusResult.Id, statusResult.ClearanceStatus, statusResult.IndexNumber);

                oneHrRecord.RVCaseId = statusResult.Id.ToString();
                oneHrRecord.IsCompleted = true;
                oneHrRecord.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;

                await _clearancesOneHRRepo.UpdateAsync(oneHrRecord);
                await _clearancesOneHRRepo.SaveChangesAsync();

                _logger.Information("[EARTHMED] OneHR record updated. RVCaseId={RVCaseId}, IsCompleted=true",
                    oneHrRecord.RVCaseId);

                var summary = (await _clearancesRepo.FindAsync(
                    c => c.DoaCandidateId == oneHrRecord.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
                    .OrderByDescending(c => c.RequestedDate)
                    .FirstOrDefault();

                if (summary != null)
                {
                    summary.StatusCode = "CLEARED";
                    summary.Outcome = "Complete";
                    summary.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;
                    summary.LinkDetailRemarks = AppendToRemarks(summary.LinkDetailRemarks,
                        $"ResponseId={statusResult.Id};Status={statusResult.ClearanceStatus}");
                    summary.UpdatedDate = DateTime.UtcNow;

                    await _clearancesRepo.UpdateAsync(summary);
                    await _clearancesRepo.SaveChangesAsync();

                    _logger.Information("[EARTHMED] Summary record updated with StatusCode=CLEARED");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error in HandleStatusResponseAsync");
                return false;
            }
        }

        public override async Task<bool> HandleAcknowledgeResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        {
            try
            {
                _logger.Information("[EARTHMED] CYCLE 3 - ACKNOWLEDGE");

                var clearances = (await _clearancesRepo.FindAsync(
                    c => c.DoaCandidateId == context.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
                    .FirstOrDefault();

                if (clearances != null)
                {
                    clearances.StatusCode = "DELIVERED";
                    clearances.UpdatedDate = DateTime.UtcNow;
                    clearances.AdditionalRemarks = AppendToRemarks(clearances.AdditionalRemarks,
                        "Acknowledgement posted");
                    await _clearancesRepo.UpdateAsync(clearances);
                    await _clearancesRepo.SaveChangesAsync();

                    _logger.Information("[EARTHMED] CYCLE 3 Complete: StatusCode=DELIVERED");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error in HandleAcknowledgeResponseAsync");
                return false;
            }
        }
    }
}