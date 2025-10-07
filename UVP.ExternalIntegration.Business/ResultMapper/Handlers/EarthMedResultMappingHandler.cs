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

                _logger.Information("[EARTHMED] Processing {Count} status results from response", responseDto.Result.Count);

                int successCount = 0;
                int skippedCount = 0;

                // Process each result in the response
                foreach (var statusResult in responseDto.Result)
                {
                    try
                    {
                        // IndexNumber in the response = CandidateId in our system
                        if (string.IsNullOrWhiteSpace(statusResult.IndexNumber))
                        {
                            _logger.Warning("[EARTHMED] Skipping result with missing IndexNumber. Id={Id}", statusResult.Id);
                            skippedCount++;
                            continue;
                        }

                        // Parse IndexNumber as CandidateId
                        if (!int.TryParse(statusResult.IndexNumber, out var candidateId))
                        {
                            _logger.Warning("[EARTHMED] Invalid IndexNumber format: {IndexNumber}. Skipping.", statusResult.IndexNumber);
                            skippedCount++;
                            continue;
                        }

                        // Find the corresponding OneHR record by CandidateId where IsCompleted = false
                        var oneHr = (await _clearancesOneHRRepo.FindAsync(o =>
                            o.CandidateId == Convert.ToInt32(statusResult.IndexNumber) &&
                            !o.IsCompleted))
                            .OrderByDescending(o => o.RequestedDate)
                            .FirstOrDefault();

                        if (oneHr == null)
                        {
                            _logger.Debug("[EARTHMED] No incomplete OneHR record found for CandidateId={CandidateId} (IndexNumber={IndexNumber}). Skipping.",
                                candidateId, statusResult.IndexNumber);
                            skippedCount++;
                            continue;
                        }

                        // Verify the clearance is still in CLEARANCE_REQUESTED status
                        var clearance = (await _clearancesRepo.FindAsync(c =>
                            c.DoaCandidateId == oneHr.DoaCandidateId &&
                            c.RecruitmentClearanceCode == SystemCode &&
                            c.StatusCode == "CLEARANCE_REQUESTED"))
                            .OrderByDescending(c => c.RequestedDate)
                            .FirstOrDefault();

                        if (clearance == null)
                        {
                            _logger.Debug("[EARTHMED] No clearance with CLEARANCE_REQUESTED status found for DoaCandidateId={DoaCandidateId}. Skipping.",
                                oneHr.DoaCandidateId);
                            skippedCount++;
                            continue;
                        }

                        _logger.Information("[EARTHMED] CYCLE 2 - GET_STATUS: Processing Id={Id}, IndexNumber={IndexNumber}, CandidateId={CandidateId}, DoaCandidateId={DoaCandidateId}, Status={Status}",
                            statusResult.Id, statusResult.IndexNumber, candidateId, oneHr.DoaCandidateId, statusResult.ClearanceStatus);

                        // Update OneHR record - mark as completed
                        oneHr.RVCaseId = statusResult.Id.ToString();
                        oneHr.IsCompleted = true;
                        oneHr.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;

                        await _clearancesOneHRRepo.UpdateAsync(oneHr);
                        await _clearancesOneHRRepo.SaveChangesAsync();

                        _logger.Information("[EARTHMED] OneHR record updated. CandidateId={CandidateId}, DoaCandidateId={DoaCandidateId}, RVCaseId={RVCaseId}, IsCompleted=true",
                            candidateId, oneHr.DoaCandidateId, oneHr.RVCaseId);

                        // Update clearance record - mark as DELIVERED and Complete (EARTHMED only has 2 cycles)
                        clearance.StatusCode = "DELIVERED";
                        clearance.Outcome = "Complete";
                        clearance.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;
                        clearance.LinkDetailRemarks = AppendToRemarks(clearance.LinkDetailRemarks,
                            $"ResponseId={statusResult.Id};Status={statusResult.ClearanceStatus};IndexNumber={statusResult.IndexNumber}");
                        clearance.UpdatedDate = DateTime.UtcNow;

                        await _clearancesRepo.UpdateAsync(clearance);
                        await _clearancesRepo.SaveChangesAsync();

                        _logger.Information("[EARTHMED] Clearance updated to DELIVERED. DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                            oneHr.DoaCandidateId, candidateId);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[EARTHMED] Error processing status result for IndexNumber={IndexNumber}, Id={Id}",
                            statusResult.IndexNumber, statusResult.Id);
                        // Continue processing other results
                    }
                }

                _logger.Information("[EARTHMED] CYCLE 2 Complete: Processed {SuccessCount} records successfully, {SkippedCount} skipped",
                    successCount, skippedCount);

                // Return true if at least one record was processed successfully
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error in HandleStatusResponseAsync");
                return false;
            }
        }
        //public override async Task<bool> HandleStatusResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        //{
        //    try
        //    {
        //        var responseDto = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(context.Response);
        //        if (responseDto == null || !responseDto.IsSuccess || responseDto.Result == null || !responseDto.Result.Any())
        //        {
        //            _logger.Warning("[EARTHMED] GET_CLEARANCE_STATUS: No results or IsSuccess=false");
        //            return false;
        //        }

        //        var statusResult = responseDto.Result.FirstOrDefault(r =>
        //            r.IndexNumber == oneHrRecord.DoaCandidateClearanceId ||
        //            r.Id.ToString() == oneHrRecord.DoaCandidateClearanceId);

        //        if (statusResult == null)
        //        {
        //            _logger.Warning("[EARTHMED] No matching status record found for Id/IndexNumber={Id}",
        //                oneHrRecord.DoaCandidateClearanceId);
        //            return false;
        //        }

        //        _logger.Information("[EARTHMED] CYCLE 2 - GET_STATUS: Id={Id}, Status={Status}, IndexNumber={IndexNumber}",
        //            statusResult.Id, statusResult.ClearanceStatus, statusResult.IndexNumber);

        //        // Update OneHR record - mark as completed
        //        oneHrRecord.RVCaseId = statusResult.Id.ToString();
        //        oneHrRecord.IsCompleted = true;
        //        oneHrRecord.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;

        //        await _clearancesOneHRRepo.UpdateAsync(oneHrRecord);
        //        await _clearancesOneHRRepo.SaveChangesAsync();

        //        _logger.Information("[EARTHMED] OneHR record updated. RVCaseId={RVCaseId}, IsCompleted=true",
        //            oneHrRecord.RVCaseId);

        //        // Update summary record - mark as DELIVERED and Complete (EARTHMED only has 2 cycles)
        //        var summary = (await _clearancesRepo.FindAsync(
        //            c => c.DoaCandidateId == oneHrRecord.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
        //            .OrderByDescending(c => c.RequestedDate)
        //            .FirstOrDefault();

        //        if (summary != null)
        //        {
        //            summary.StatusCode = "DELIVERED";  // EARTHMED: Direct to DELIVERED (no ACK cycle)
        //            summary.Outcome = "Complete";
        //            summary.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;
        //            summary.LinkDetailRemarks = AppendToRemarks(summary.LinkDetailRemarks,
        //                $"ResponseId={statusResult.Id};Status={statusResult.ClearanceStatus}");
        //            summary.UpdatedDate = DateTime.UtcNow;

        //            await _clearancesRepo.UpdateAsync(summary);
        //            await _clearancesRepo.SaveChangesAsync();

        //            _logger.Information("[EARTHMED] CYCLE 2 Complete: StatusCode=DELIVERED, Outcome=Complete (No ACK needed)");
        //        }

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error(ex, "[EARTHMED] Error in HandleStatusResponseAsync");
        //        return false;
        //    }
        //    //try
        //    //{
        //    //    var responseDto = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(context.Response);
        //    //    if (responseDto == null || !responseDto.IsSuccess || responseDto.Result == null || !responseDto.Result.Any())
        //    //    {
        //    //        _logger.Warning("[EARTHMED] GET_CLEARANCE_STATUS: No results or IsSuccess=false");
        //    //        return false;
        //    //    }

        //    //    var statusResult = responseDto.Result.FirstOrDefault(r =>
        //    //        r.IndexNumber == oneHrRecord.DoaCandidateClearanceId ||
        //    //        r.Id.ToString() == oneHrRecord.DoaCandidateClearanceId);

        //    //    if (statusResult == null)
        //    //    {
        //    //        _logger.Warning("[EARTHMED] No matching status record found for Id/IndexNumber={Id}",
        //    //            oneHrRecord.DoaCandidateClearanceId);
        //    //        return false;
        //    //    }

        //    //    _logger.Information("[EARTHMED] STATUS: Id={Id}, Status={Status}, IndexNumber={IndexNumber}",
        //    //        statusResult.Id, statusResult.ClearanceStatus, statusResult.IndexNumber);

        //    //    oneHrRecord.RVCaseId = statusResult.Id.ToString();
        //    //    oneHrRecord.IsCompleted = true;
        //    //    oneHrRecord.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;

        //    //    await _clearancesOneHRRepo.UpdateAsync(oneHrRecord);
        //    //    await _clearancesOneHRRepo.SaveChangesAsync();

        //    //    _logger.Information("[EARTHMED] OneHR record updated. RVCaseId={RVCaseId}, IsCompleted=true",
        //    //        oneHrRecord.RVCaseId);

        //    //    var summary = (await _clearancesRepo.FindAsync(
        //    //        c => c.DoaCandidateId == oneHrRecord.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
        //    //        .OrderByDescending(c => c.RequestedDate)
        //    //        .FirstOrDefault();

        //    //    if (summary != null)
        //    //    {
        //    //        summary.StatusCode = "CLEARED";
        //    //        summary.Outcome = "Complete";
        //    //        summary.CompletionDate = statusResult.ClearanceDate ?? DateTime.UtcNow;
        //    //        summary.LinkDetailRemarks = AppendToRemarks(summary.LinkDetailRemarks,
        //    //            $"ResponseId={statusResult.Id};Status={statusResult.ClearanceStatus}");
        //    //        summary.UpdatedDate = DateTime.UtcNow;

        //    //        await _clearancesRepo.UpdateAsync(summary);
        //    //        await _clearancesRepo.SaveChangesAsync();

        //    //        _logger.Information("[EARTHMED] Summary record updated with StatusCode=CLEARED");
        //    //    }

        //    //    return true;
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //    _logger.Error(ex, "[EARTHMED] Error in HandleStatusResponseAsync");
        //    //    return false;
        //    //}
        //}

        public override async Task<bool> HandleAcknowledgeResponseAsync(ResultMappingContext context, ResultMappingFields fields, DoaCandidateClearancesOneHR oneHrRecord)
        {
            try
            {
                _logger.Warning("[EARTHMED] ACKNOWLEDGE_RESPONSE called - EARTHMED only has 2 cycles, this should not happen");

                // EARTHMED doesn't have a 3rd cycle - everything is completed in HandleStatusResponseAsync
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error in HandleAcknowledgeResponseAsync");
                return false;
            }
            //try
            //{
            //    _logger.Information("[EARTHMED] CYCLE 3 - ACKNOWLEDGE");

            //    var clearances = (await _clearancesRepo.FindAsync(
            //        c => c.DoaCandidateId == context.DoaCandidateId && c.RecruitmentClearanceCode == SystemCode))
            //        .FirstOrDefault();

            //    if (clearances != null)
            //    {
            //        clearances.StatusCode = "DELIVERED";
            //        clearances.UpdatedDate = DateTime.UtcNow;
            //        clearances.AdditionalRemarks = AppendToRemarks(clearances.AdditionalRemarks,
            //            "Acknowledgement posted");
            //        await _clearancesRepo.UpdateAsync(clearances);
            //        await _clearancesRepo.SaveChangesAsync();

            //        _logger.Information("[EARTHMED] CYCLE 3 Complete: StatusCode=DELIVERED");
            //    }

            //    return true;
            //}
            //catch (Exception ex)
            //{
            //    _logger.Error(ex, "[EARTHMED] Error in HandleAcknowledgeResponseAsync");
            //    return false;
            //}
        }
    }
}