using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.ResultMapper.Services
{
    public abstract class BaseResultMappingHandler : IResultMappingSystemHandler
    {
        protected readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        protected readonly IGenericRepository<DoaCandidateClearancesOneHR> _clearancesOneHRRepo;
        protected readonly ILogger _logger;

        protected BaseResultMappingHandler(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo)
        {
            _clearancesRepo = clearancesRepo;
            _clearancesOneHRRepo = clearancesOneHRRepo;
            _logger = Log.ForContext(GetType());
        }

        public abstract string SystemCode { get; }

        public virtual async Task<bool> HandleCreateClearanceRequestAsync(ResultMappingContext context, ResultMappingFields fields)
        {
            if (string.IsNullOrWhiteSpace(fields.RequestId))
            {
                _logger.Error("{System} create response missing request id. Raw={Raw}",
                    SystemCode, Truncate(context.Response, 500));
                return false;
            }

            _logger.Information("[{System}] CYCLE 1 - CREATE: RequestId={RequestId} Status={Status}",
                SystemCode, fields.RequestId, fields.StatusLabel ?? "n/a");

            var oneHr = new DoaCandidateClearancesOneHR
            {
                DoaCandidateId = context.DoaCandidateId,
                CandidateId = context.CandidateId,
                DoaCandidateClearanceId = fields.RequestId,
                RequestedDate = DateTime.UtcNow,
                IsCompleted = false,
                Retry = 0
            };
            await _clearancesOneHRRepo.AddAsync(oneHr);
            await _clearancesOneHRRepo.SaveChangesAsync();

            var clearance = new DoaCandidateClearances
            {
                DoaCandidateId = context.DoaCandidateId,
                RecruitmentClearanceCode = SystemCode,
                RequestedDate = DateTime.UtcNow,
                StatusCode = "CLEARANCE_REQUESTED",
                LinkDetailRemarks = $"clearanceRequestId={fields.RequestId}",
                UpdatedDate = DateTime.UtcNow
            };
            await _clearancesRepo.AddAsync(clearance);
            await _clearancesRepo.SaveChangesAsync();

            _logger.Information("[{System}] CYCLE 1 Complete: StatusCode=CLEARANCE_REQUESTED", SystemCode);
            return true;
        }

        public virtual async Task<bool> HandleAcknowledgeResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        {
            _logger.Information("CYCLE 3 - ACKNOWLEDGE [{System}]", SystemCode);

            var clearances = (await _clearancesRepo.FindAsync(
                c => c.DoaCandidateId == context.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
                .FirstOrDefault();

            if (clearances != null)
            {
                clearances.StatusCode = "DELIVERED";
                clearances.UpdatedDate = DateTime.UtcNow;
                clearances.AdditionalRemarks = AppendToRemarks(clearances.AdditionalRemarks, "Acknowledgement posted");
                await _clearancesRepo.UpdateAsync(clearances);
                await _clearancesRepo.SaveChangesAsync();

                _logger.Information("CYCLE 3 Complete: StatusCode=DELIVERED for {System}", SystemCode);
            }

            return true;
        }
        public virtual async Task<bool> HandleStatusResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR? oneHrRecord)
        {
            // Null check for oneHrRecord - required for EARTHMED which processes multiple results
            if (oneHrRecord == null)
            {
                _logger.Warning("[{System}] HandleStatusResponseAsync called with null oneHrRecord", SystemCode);
                return false;
            }

            _logger.Information("[{System}] STATUS: Doa={DoaId}, Cand={CandId}, ClearanceId={ClearanceId}, RespId={RespId}, Status={Status}",
                SystemCode, oneHrRecord.DoaCandidateId, oneHrRecord.CandidateId, oneHrRecord.DoaCandidateClearanceId,
                fields.ResponseId ?? "n/a", fields.StatusLabel ?? fields.StatusCode?.ToString() ?? "n/a");

            // Log the original RVCaseId for comparison
            var originalRVCaseId = oneHrRecord.RVCaseId;

            // Update OneHR record with RVCaseId from ResponseId
            if (!string.IsNullOrWhiteSpace(fields.ResponseId))
            {
                oneHrRecord.RVCaseId = fields.ResponseId;
                _logger.Information("[{System}] Setting RVCaseId: '{OldValue}' -> '{NewValue}'",
                    SystemCode, originalRVCaseId ?? "null", fields.ResponseId);
            }
            else
            {
                _logger.Warning("[{System}] ResponseId is null/empty - RVCaseId will not be updated. Current RVCaseId: '{CurrentValue}'",
                    SystemCode, originalRVCaseId ?? "null");

                // Log the raw response for debugging
                _logger.Warning("[{System}] Raw response for debugging: {Response}",
                    SystemCode, context.Response.Length > 2000 ? context.Response.Substring(0, 2000) + "..." : context.Response);
            }

            oneHrRecord.IsCompleted = true;
            oneHrRecord.CompletionDate = fields.StatusDate ?? DateTime.UtcNow;

            try
            {
                await _clearancesOneHRRepo.UpdateAsync(oneHrRecord);
                await _clearancesOneHRRepo.SaveChangesAsync();

                _logger.Information("[{System}] OneHR record updated successfully. Final RVCaseId: '{RVCaseId}', IsCompleted: {IsCompleted}",
                    SystemCode, oneHrRecord.RVCaseId ?? "null", oneHrRecord.IsCompleted);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{System}] Failed to update OneHR record for DoaCandidateId: {DoaId}",
                    SystemCode, oneHrRecord.DoaCandidateId);
                throw;
            }

            // Update summary row
            var summary = (await _clearancesRepo.FindAsync(
                c => c.DoaCandidateId == oneHrRecord.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
                .OrderByDescending(c => c.RequestedDate)
                .FirstOrDefault();

            if (summary != null)
            {
                summary.StatusCode = "CLEARED";
                summary.Outcome = "Complete";
                summary.CompletionDate = fields.StatusDate ?? DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(fields.ResponseId))
                    summary.LinkDetailRemarks = AppendToRemarks(summary.LinkDetailRemarks, $"clearanceResponseId={fields.ResponseId}");
                summary.UpdatedDate = DateTime.UtcNow;

                await _clearancesRepo.UpdateAsync(summary);
                await _clearancesRepo.SaveChangesAsync();

                _logger.Information("[{System}] Summary record updated with StatusCode=CLEARED", SystemCode);

            }
            else
            {
                _logger.Warning("[{System}] Summary row not found for DoaCandidateId={DoaId}", SystemCode, oneHrRecord.DoaCandidateId);
            }

            return true;
        }
        //public virtual async Task<bool> HandleStatusResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        //{
        //    _logger.Information("[{System}] STATUS: Doa={DoaId}, Cand={CandId}, ClearanceId={ClearanceId}, RespId={RespId}, Status={Status}",
        //        SystemCode, oneHrRecord.DoaCandidateId, oneHrRecord.CandidateId, oneHrRecord.DoaCandidateClearanceId,
        //        fields.ResponseId ?? "n/a", fields.StatusLabel ?? fields.StatusCode?.ToString() ?? "n/a");

        //    // Log the original RVCaseId for comparison
        //    var originalRVCaseId = oneHrRecord.RVCaseId;
        //    //if(originalRVCaseId!=null)
        //    //    fields.ResponseId = originalRVCaseId;

        //    // Update OneHR record with RVCaseId from ResponseId
        //    if (!string.IsNullOrWhiteSpace(fields.ResponseId))
        //    {
        //        oneHrRecord.RVCaseId = fields.ResponseId;
        //        _logger.Information("[{System}] Setting RVCaseId: '{OldValue}' -> '{NewValue}'",
        //            SystemCode, originalRVCaseId ?? "null", fields.ResponseId);
        //    }
        //    else
        //    {
        //        _logger.Warning("[{System}] ResponseId is null/empty - RVCaseId will not be updated. Current RVCaseId: '{CurrentValue}'",
        //            SystemCode, originalRVCaseId ?? "null");

        //        // Log the raw response for debugging
        //        _logger.Warning("[{System}] Raw response for debugging: {Response}",
        //            SystemCode, context.Response.Length > 2000 ? context.Response.Substring(0, 2000) + "..." : context.Response);
        //    }

        //    oneHrRecord.IsCompleted = true;
        //    oneHrRecord.CompletionDate = fields.StatusDate ?? DateTime.UtcNow;

        //    try
        //    {
        //        await _clearancesOneHRRepo.UpdateAsync(oneHrRecord);
        //        await _clearancesOneHRRepo.SaveChangesAsync();

        //        _logger.Information("[{System}] OneHR record updated successfully. Final RVCaseId: '{RVCaseId}', IsCompleted: {IsCompleted}",
        //            SystemCode, oneHrRecord.RVCaseId ?? "null", oneHrRecord.IsCompleted);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "[{System}] Failed to update OneHR record for DoaCandidateId: {DoaId}",
        //            SystemCode, oneHrRecord.DoaCandidateId);
        //        throw;
        //    }

        //    // Update summary row
        //    var summary = (await _clearancesRepo.FindAsync(
        //        c => c.DoaCandidateId == oneHrRecord.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
        //        .OrderByDescending(c => c.RequestedDate)
        //        .FirstOrDefault();

        //    if (summary != null)
        //    {
        //        summary.StatusCode = "CLEARED";
        //        summary.Outcome = "Complete";
        //        summary.CompletionDate = fields.StatusDate ?? DateTime.UtcNow;
        //        if (!string.IsNullOrWhiteSpace(fields.ResponseId))
        //            summary.LinkDetailRemarks = AppendToRemarks(summary.LinkDetailRemarks, $"clearanceResponseId={fields.ResponseId}");
        //        summary.UpdatedDate = DateTime.UtcNow;

        //        await _clearancesRepo.UpdateAsync(summary);
        //        await _clearancesRepo.SaveChangesAsync();

        //        _logger.Information("[{System}] Summary record updated with StatusCode=CLEARED", SystemCode);

        //    }
        //    else
        //    {
        //        _logger.Warning("[{System}] Summary row not found for DoaCandidateId={DoaId}", SystemCode, oneHrRecord.DoaCandidateId);
        //    }

        //    return true;
        //}

        protected string AppendToRemarks(string? existingRemarks, string newRemark)
        {
            if (string.IsNullOrWhiteSpace(existingRemarks)) return newRemark;
            return $"{existingRemarks.TrimEnd(';')};{newRemark}";
        }

        protected static string Truncate(string? s, int max)
            => string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s[..max];
    }
}
