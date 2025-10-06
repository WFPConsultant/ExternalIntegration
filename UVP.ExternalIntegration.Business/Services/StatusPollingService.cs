using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    /// <summary>
    /// Background polling job (no schema change version).
    /// Rule: keep polling while DoaCandidateClearances.StatusCode != 'DELIVERED'
    /// and IntegrationInvocation.AttemptCount < IntegrationEndpointConfiguration.RetriggerCount.
    /// </summary>
    public class StatusPollingService : IStatusPollingService
    {
        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _oneHrRepo;
        private readonly IGenericRepository<IntegrationInvocationLog> _logRepo;
        private readonly IIntegrationInvocationRepository _invocationRepo;
        private readonly IIntegrationEndpointRepository _endpointRepo;
        private readonly IInvocationManagerService _invocationManager;
        private readonly IGenericRepository<DoaCandidate> _doaCandidateRepo;
        private readonly ILogger _logger = Log.ForContext<StatusPollingService>();

        public StatusPollingService(
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> oneHrRepo,
            IGenericRepository<IntegrationInvocationLog> logRepo,
            IIntegrationInvocationRepository invocationRepo,
            IIntegrationEndpointRepository endpointRepo,
            IInvocationManagerService invocationManager,
            IGenericRepository<DoaCandidate> doaCandidateRepo)
        {
            _clearancesRepo = clearancesRepo;
            _oneHrRepo = oneHrRepo;
            _logRepo = logRepo;
            _invocationRepo = invocationRepo;
            _endpointRepo = endpointRepo;
            _invocationManager = invocationManager;
            _doaCandidateRepo = doaCandidateRepo;
        }

        public async Task<bool> ProcessOpenClearancesAsync()
        {
            try
            {
                // 1) Pick all non-delivered clearances
                var open = (await _clearancesRepo.FindAsync(c => c.StatusCode != "DELIVERED" && c.StatusCode == "CLEARANCE_REQUESTED"))
                           .OrderByDescending(c => c.UpdatedDate)
                           .ToList();

                foreach (var clearance in open)
                {
                    var doaId = clearance.DoaCandidateId;

                    // 2) Get CandidateId & clearance request id from OneHR row if present
                    var oneHr = (await _oneHrRepo.FindAsync(x => x.DoaCandidateId == doaId))
                                .OrderByDescending(x => x.RequestedDate).FirstOrDefault();

                    if (oneHr == null || string.IsNullOrWhiteSpace(oneHr.DoaCandidateClearanceId))
                    {
                        _logger.Information("Skip polling DoaCandidateId={DoaCandidateId} (no OneHR row / no RequestId yet)", doaId);
                        continue;
                    }

                    var candidateId = oneHr.CandidateId;
                    var integrationType = clearance.RecruitmentClearanceCode;
                    var operation = IntegrationOperation.GET_CLEARANCE_STATUS.ToString();

                    // 3) Endpoint & retrigger policy
                    var endpoint = await _endpointRepo.GetActiveEndpointAsync(integrationType, operation);
                    if (endpoint == null || !endpoint.Retrigger || endpoint.RetriggerCount <= 0)
                    {
                        _logger.Information("[{Type}] No retrigger policy for status. Skipping.", integrationType);
                        continue;
                    }

                    // 4) Try to find an existing invocation for this (type+op) and this candidate ids
                    var invocations = await _invocationRepo.FindAsync(i =>
                        i.IntegrationType == integrationType &&
                        i.IntegrationOperation == operation);

                    IntegrationInvocation? matched = null;
                    foreach (var inv in invocations.OrderByDescending(i => i.UpdatedOn ?? i.CreatedOn))
                    {
                        var logs = await _logRepo.FindAsync(l => l.IntegrationInvocationId == inv.IntegrationInvocationId);
                        var first = logs.OrderBy(l => l.LogSequence).FirstOrDefault();
                        if (first?.RequestPayload == null) continue;

                        // Check if this invocation matches the current clearance
                        bool isMatch = await IsInvocationMatchAsync(first.RequestPayload, doaId, candidateId, integrationType);

                        if (isMatch)
                        {
                            matched = inv;
                            break;
                        }
                    }

                    if (matched == null)
                    {
                        // No previous status invocation: create the first one
                        var req = new IntegrationRequestDto
                        {
                            DoaCandidateId = doaId,
                            CandidateId = candidateId,
                            IntegrationType = integrationType,
                            IntegrationOperation = operation
                        };

                        _logger.Information("[{Type}] Creating first GET_CLEARANCE_STATUS for DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                            integrationType, doaId, candidateId);

                        await _invocationManager.CreateInvocationAsync(req);
                        continue;
                    }

                    // 5) Enforce AttemptCount vs RetriggerCount
                    if (matched.AttemptCount >= endpoint.RetriggerCount)
                    {
                        _logger.Information("[{Type}] AttemptCount {Attempt} reached RetriggerCount {Limit} for invocation {Id}. Skipping.",
                            integrationType, matched.AttemptCount, endpoint.RetriggerCount, matched.IntegrationInvocationId);
                        continue;
                    }

                    // 6) Retrigger same invocation: set to PENDING and increment AttemptCount
                    matched.AttemptCount += 1;
                    matched.IntegrationStatus = IntegrationStatus.PENDING.ToString();
                    matched.UpdatedOn = DateTime.UtcNow;
                    matched.UpdatedUser = "System";
                    await _invocationRepo.UpdateAsync(matched);
                    await _invocationRepo.SaveChangesAsync();

                    _logger.Information("[{Type}] Re-queuing GET_CLEARANCE_STATUS invocation {Id} (attempt {Attempt}/{Limit})",
                        integrationType, matched.IntegrationInvocationId, matched.AttemptCount, endpoint.RetriggerCount);

                    // Run it now (consistent with ProcessPendingInvocations behavior)
                    await _invocationManager.ProcessPendingInvocationsAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in ProcessOpenClearancesAsync (no-schema)");
                return false;
            }
        }

        public async Task<bool> ProcessAcknowledgeAsync()
        {
            try
            {
                // Pull all clearances that are not yet delivered
                var nonDelivered = (await _clearancesRepo.FindAsync(c => c.StatusCode != "DELIVERED" && c.StatusCode == "CLEARED"))
                                   .OrderByDescending(c => c.UpdatedDate)
                                   .ToList();

                foreach (var clearance in nonDelivered)
                {
                    var doaId = clearance.DoaCandidateId;

                    // Must have an OneHR row with RVCaseId to ACK
                    var oneHr = (await _oneHrRepo.FindAsync(x => x.DoaCandidateId == doaId))
                                .OrderByDescending(x => x.RequestedDate).FirstOrDefault();

                    if (oneHr == null || string.IsNullOrWhiteSpace(oneHr.RVCaseId))
                    {
                        _logger.Information("Skip ACK for DoaCandidateId={DoaCandidateId} (no RVCaseId yet)", doaId);
                        continue;
                    }

                    var candidateId = oneHr.CandidateId;
                    var integrationType = clearance.RecruitmentClearanceCode;
                    var operation = IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString();

                    // Endpoint policy for ACK
                    var endpoint = await _endpointRepo.GetActiveEndpointAsync(integrationType, operation);
                    if (endpoint == null || !endpoint.Retrigger || endpoint.RetriggerCount <= 0)
                    {
                        _logger.Information("[{Type}] No retrigger policy for ACKNOWLEDGE_RESPONSE. Skipping.", integrationType);
                        continue;
                    }

                    // Find existing ACK invocation for this candidate-type
                    var invocations = await _invocationRepo.FindAsync(i =>
                        i.IntegrationType == integrationType &&
                        i.IntegrationOperation == operation);

                    IntegrationInvocation? matched = null;
                    foreach (var inv in invocations.OrderByDescending(i => i.UpdatedOn ?? i.CreatedOn))
                    {
                        var logs = await _logRepo.FindAsync(l => l.IntegrationInvocationId == inv.IntegrationInvocationId);
                        var first = logs.OrderBy(l => l.LogSequence).FirstOrDefault();
                        if (first?.RequestPayload == null) continue;

                        // Match by RVCaseId and candidate identifiers
                        bool isMatch = await IsAckInvocationMatchAsync(first.RequestPayload, oneHr.RVCaseId, doaId, candidateId, integrationType);

                        if (isMatch)
                        {
                            matched = inv;
                            break;
                        }
                    }

                    if (matched == null)
                    {
                        // Create first ACK invocation
                        var req = new IntegrationRequestDto
                        {
                            DoaCandidateId = doaId,
                            CandidateId = candidateId,
                            IntegrationType = integrationType,
                            IntegrationOperation = operation
                        };

                        _logger.Information("[{Type}] Creating first ACKNOWLEDGE_RESPONSE for DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}, RVCaseId={RVCaseId}",
                            integrationType, doaId, candidateId, oneHr.RVCaseId);

                        await _invocationManager.CreateInvocationAsync(req);
                        continue;
                    }

                    // Enforce AttemptCount cap
                    if (matched.AttemptCount >= endpoint.RetriggerCount)
                    {
                        _logger.Information("[{Type}] ACK attempts {Attempt} reached RetriggerCount {Limit} for invocation {Id}. Skipping.",
                            integrationType, matched.AttemptCount, endpoint.RetriggerCount, matched.IntegrationInvocationId);
                        continue;
                    }

                    // Retrigger same invocation
                    matched.AttemptCount += 1;
                    matched.IntegrationStatus = IntegrationStatus.PENDING.ToString();
                    matched.UpdatedOn = DateTime.UtcNow;
                    matched.UpdatedUser = "System";
                    await _invocationRepo.UpdateAsync(matched);
                    await _invocationRepo.SaveChangesAsync();

                    _logger.Information("[{Type}] Re-queuing ACKNOWLEDGE_RESPONSE invocation {Id} (attempt {Attempt}/{Limit})",
                        integrationType, matched.IntegrationInvocationId, matched.AttemptCount, endpoint.RetriggerCount);

                    await _invocationManager.ProcessPendingInvocationsAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in ProcessAcknowledgeAsync (no-schema)");
                return false;
            }
        }

        /// <summary>
        /// Check if an invocation's request payload matches the current clearance.
        /// Handles both CMTS (externalBatchId/externalRequestId) and EARTHMED (ReferenceNumber) formats.
        /// </summary>
        private async Task<bool> IsInvocationMatchAsync(string requestPayload, int doaCandidateId, int candidateId, string integrationType)
        {
            try
            {
                // EARTHMED: Match by ReferenceNumber (DoaId_DoaCandidateId format)
                if (integrationType.Equals("EARTHMED", StringComparison.OrdinalIgnoreCase))
                {
                    return await IsEarthMedPayloadMatchAsync(requestPayload, doaCandidateId, candidateId);
                }

                // CMTS and others: Match by direct ID presence in payload
                return requestPayload.Contains(doaCandidateId.ToString()) &&
                       requestPayload.Contains(candidateId.ToString());
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking invocation match for payload");
                return false;
            }
        }

        /// <summary>
        /// Check if an EARTHMED payload matches the given DoaCandidateId and CandidateId.
        /// Uses ReferenceNumber (DoaId_DoaCandidateId) and IndexNumber for matching.
        /// </summary>
        private async Task<bool> IsEarthMedPayloadMatchAsync(string requestPayload, int doaCandidateId, int candidateId)
        {
            try
            {
                using var doc = JsonDocument.Parse(requestPayload);
                var root = doc.RootElement;

                // Extract ReferenceNumber from payload
                string? referenceNumber = null;
                if (root.TryGetProperty("ReferenceNumber", out var refNumProp))
                {
                    referenceNumber = refNumProp.GetString();
                }

                if (string.IsNullOrWhiteSpace(referenceNumber) || !referenceNumber.Contains("_"))
                {
                    _logger.Debug("[EARTHMED] ReferenceNumber not found or invalid format in payload");
                    return false;
                }

                // Parse ReferenceNumber: DoaId_DoaCandidateId
                var parts = referenceNumber.Split('_');
                if (parts.Length != 2)
                {
                    _logger.Debug("[EARTHMED] ReferenceNumber does not have 2 parts: {ReferenceNumber}", referenceNumber);
                    return false;
                }

                if (!int.TryParse(parts[0], out var payloadDoaId) ||
                    !int.TryParse(parts[1], out var payloadDoaCandidateId))
                {
                    _logger.Debug("[EARTHMED] Failed to parse DoaId/DoaCandidateId from ReferenceNumber: {ReferenceNumber}", referenceNumber);
                    return false;
                }

                // Match DoaCandidateId from ReferenceNumber
                if (payloadDoaCandidateId != doaCandidateId)
                {
                    return false;
                }

                // Verify CandidateId by querying DoaCandidate table
                var doaCandidate = (await _doaCandidateRepo.FindAsync(x =>
                    x.DoaId == payloadDoaId &&
                    x.Id == payloadDoaCandidateId))
                    .FirstOrDefault();

                if (doaCandidate == null)
                {
                    _logger.Debug("[EARTHMED] DoaCandidate not found for DoaId={DoaId}, DoaCandidateId={DoaCandidateId}",
                        payloadDoaId, payloadDoaCandidateId);
                    return false;
                }

                // Match CandidateId
                bool isMatch = doaCandidate.CandidateId == candidateId;

                _logger.Debug("[EARTHMED] Payload match result: {IsMatch} (DoaCandidateId: {PayloadId} vs {CurrentId}, CandidateId: {PayloadCandId} vs {CurrentCandId})",
                    isMatch, payloadDoaCandidateId, doaCandidateId, doaCandidate.CandidateId, candidateId);

                return isMatch;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[EARTHMED] Error parsing payload for match check");
                return false;
            }
        }

        /// <summary>
        /// Check if an acknowledgment invocation matches the current clearance.
        /// Handles both CMTS and EARTHMED formats.
        /// </summary>
        private async Task<bool> IsAckInvocationMatchAsync(string requestPayload, string rvCaseId, int doaCandidateId, int candidateId, string integrationType)
        {
            try
            {
                // All types should contain RVCaseId in ACK payload
                if (!requestPayload.Contains(rvCaseId))
                {
                    return false;
                }

                // EARTHMED: Additional matching using ReferenceNumber
                if (integrationType.Equals("EARTHMED", StringComparison.OrdinalIgnoreCase))
                {
                    return await IsEarthMedPayloadMatchAsync(requestPayload, doaCandidateId, candidateId);
                }

                // CMTS and others: Match by RVCaseId and direct ID presence
                return requestPayload.Contains(doaCandidateId.ToString()) &&
                       requestPayload.Contains(candidateId.ToString());
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error checking ACK invocation match for payload");
                return false;
            }
        }
    }
}

//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using UVP.ExternalIntegration.Business.Interfaces;
//using UVP.ExternalIntegration.Domain.DTOs;
//using UVP.ExternalIntegration.Domain.Entities;
//using UVP.ExternalIntegration.Domain.Enums;
//using UVP.ExternalIntegration.Repository.Interfaces;

//namespace UVP.ExternalIntegration.Business.Services
//{
//    /// <summary>
//    /// Background polling job (no schema change version).
//    /// Rule: keep polling while DoaCandidateClearances.StatusCode != 'DELIVERED'
//    /// and IntegrationInvocation.AttemptCount < IntegrationEndpointConfiguration.RetriggerCount.
//    /// </summary>
//    public class StatusPollingService : IStatusPollingService
//    {
//        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
//        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _oneHrRepo;
//        private readonly IGenericRepository<IntegrationInvocationLog> _logRepo;
//        private readonly IIntegrationInvocationRepository _invocationRepo;
//        private readonly IIntegrationEndpointRepository _endpointRepo;
//        private readonly IInvocationManagerService _invocationManager;
//        private readonly ILogger _logger = Log.ForContext<StatusPollingService>();

//        public StatusPollingService(
//            IGenericRepository<DoaCandidateClearances> clearancesRepo,
//            IGenericRepository<DoaCandidateClearancesOneHR> oneHrRepo,
//            IGenericRepository<IntegrationInvocationLog> logRepo,
//            IIntegrationInvocationRepository invocationRepo,
//            IIntegrationEndpointRepository endpointRepo,
//            IInvocationManagerService invocationManager)
//        {
//            _clearancesRepo = clearancesRepo;
//            _oneHrRepo = oneHrRepo;
//            _logRepo = logRepo;
//            _invocationRepo = invocationRepo;
//            _endpointRepo = endpointRepo;
//            _invocationManager = invocationManager;
//        }

//        public async Task<bool> ProcessOpenClearancesAsync()
//        {
//            try
//            {
//                // 1) Pick all non-delivered clearances
//                var open = (await _clearancesRepo.FindAsync(c => c.StatusCode != "DELIVERED" && c.StatusCode == "CLEARANCE_REQUESTED"))
//                           .OrderByDescending(c => c.UpdatedDate)
//                           .ToList();

//                foreach (var clearance in open)
//                {
//                    var doaId = clearance.DoaCandidateId;

//                    // 2) Get CandidateId & clearance request id from OneHR row if present
//                    var oneHr = (await _oneHrRepo.FindAsync(x => x.DoaCandidateId == doaId))
//                                .OrderByDescending(x => x.RequestedDate).FirstOrDefault();

//                    if (oneHr == null || string.IsNullOrWhiteSpace(oneHr.DoaCandidateClearanceId))
//                    {
//                        _logger.Information("Skip polling DoaCandidateId={DoaCandidateId} (no OneHR row / no RequestId yet)", doaId);
//                        continue;
//                    }

//                    var candidateId = oneHr.CandidateId;
//                    var integrationType = clearance.RecruitmentClearanceCode;
//                    var operation = IntegrationOperation.GET_CLEARANCE_STATUS.ToString();

//                    // 3) Endpoint & retrigger policy
//                    var endpoint = await _endpointRepo.GetActiveEndpointAsync(integrationType, operation);
//                    if (endpoint == null || !endpoint.Retrigger || endpoint.RetriggerCount <= 0)
//                    {
//                        _logger.Information("[{Type}] No retrigger policy for status. Skipping.", integrationType);
//                        continue;
//                    }

//                    // 4) Try to find an existing invocation for this (type+op) and this candidate ids
//                    var invocations = await _invocationRepo.FindAsync(i =>
//                        i.IntegrationType == integrationType &&
//                        i.IntegrationOperation == operation);

//                    IntegrationInvocation? matched = null;
//                    foreach (var inv in invocations.OrderByDescending(i => i.UpdatedOn ?? i.CreatedOn))
//                    {
//                        var logs = await _logRepo.FindAsync(l => l.IntegrationInvocationId == inv.IntegrationInvocationId);
//                        var first = logs.OrderBy(l => l.LogSequence).FirstOrDefault();
//                        if (first?.RequestPayload == null) continue;

//                        // naive match: payload contains both ids (as numbers). Your payload uses externalBatchId/externalRequestId.
//                        if (first.RequestPayload.Contains(doaId.ToString()) && first.RequestPayload.Contains(candidateId.ToString()))
//                        {
//                            matched = inv;
//                            break;
//                        }
//                    }

//                    if (matched == null)
//                    {
//                        // No previous status invocation: create the first one
//                        var req = new IntegrationRequestDto
//                        {
//                            DoaCandidateId = doaId,
//                            CandidateId = candidateId,
//                            IntegrationType = integrationType,
//                            IntegrationOperation = operation
//                        };

//                        _logger.Information("[{Type}] Creating first GET_CLEARANCE_STATUS for DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
//                            integrationType, doaId, candidateId);

//                        await _invocationManager.CreateInvocationAsync(req);
//                        continue;
//                    }

//                    // 5) Enforce AttemptCount vs RetriggerCount
//                    if (matched.AttemptCount >= endpoint.RetriggerCount)
//                    {
//                        _logger.Information("[{Type}] AttemptCount {Attempt} reached RetriggerCount {Limit} for invocation {Id}. Skipping.",
//                            integrationType, matched.AttemptCount, endpoint.RetriggerCount, matched.IntegrationInvocationId);
//                        continue;
//                    }

//                    // 6) Retrigger same invocation: set to PENDING and increment AttemptCount
//                    matched.AttemptCount += 1;
//                    matched.IntegrationStatus = IntegrationStatus.PENDING.ToString();
//                    matched.UpdatedOn = DateTime.UtcNow;
//                    matched.UpdatedUser = "System";
//                    await _invocationRepo.UpdateAsync(matched);
//                    await _invocationRepo.SaveChangesAsync();

//                    _logger.Information("[{Type}] Re-queuing GET_CLEARANCE_STATUS invocation {Id} (attempt {Attempt}/{Limit})",
//                        integrationType, matched.IntegrationInvocationId, matched.AttemptCount, endpoint.RetriggerCount);

//                    // Run it now (consistent with ProcessPendingInvocations behavior)
//                    await _invocationManager.ProcessPendingInvocationsAsync();
//                }

//                return true;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error(ex, "Error in ProcessOpenClearancesAsync (no-schema)");
//                return false;
//            }
//        }

//        public async Task<bool> ProcessAcknowledgeAsync()
//        {
//            try
//            {
//                // Pull all clearances that are not yet delivered
//                var nonDelivered = (await _clearancesRepo.FindAsync(c => c.StatusCode != "DELIVERED" && c.StatusCode == "CLEARED"))
//                                   .OrderByDescending(c => c.UpdatedDate)
//                                   .ToList();

//                foreach (var clearance in nonDelivered)
//                {
//                    var doaId = clearance.DoaCandidateId;

//                    // Must have an OneHR row with RVCaseId to ACK
//                    var oneHr = (await _oneHrRepo.FindAsync(x => x.DoaCandidateId == doaId))
//                                .OrderByDescending(x => x.RequestedDate).FirstOrDefault();

//                    if (oneHr == null || string.IsNullOrWhiteSpace(oneHr.RVCaseId))
//                    {
//                        _logger.Information("Skip ACK for DoaCandidateId={DoaCandidateId} (no RVCaseId yet)", doaId);
//                        continue;
//                    }

//                    var candidateId = oneHr.CandidateId;
//                    var integrationType = clearance.RecruitmentClearanceCode;
//                    var operation = IntegrationOperation.ACKNOWLEDGE_RESPONSE.ToString();

//                    // Endpoint policy for ACK
//                    var endpoint = await _endpointRepo.GetActiveEndpointAsync(integrationType, operation);
//                    if (endpoint == null || !endpoint.Retrigger || endpoint.RetriggerCount <= 0)
//                    {
//                        _logger.Information("[{Type}] No retrigger policy for ACKNOWLEDGE_RESPONSE. Skipping.", integrationType);
//                        continue;
//                    }

//                    // Find existing ACK invocation for this candidate-type
//                    var invocations = await _invocationRepo.FindAsync(i =>
//                        i.IntegrationType == integrationType &&
//                        i.IntegrationOperation == operation);

//                    IntegrationInvocation? matched = null;
//                    foreach (var inv in invocations.OrderByDescending(i => i.UpdatedOn ?? i.CreatedOn))
//                    {
//                        var logs = await _logRepo.FindAsync(l => l.IntegrationInvocationId == inv.IntegrationInvocationId);
//                        var first = logs.OrderBy(l => l.LogSequence).FirstOrDefault();
//                        if (first?.RequestPayload == null) continue;

//                        // Match by RVCaseId and ids if present
//                        if (first.RequestPayload.Contains(oneHr.RVCaseId) &&
//                            first.RequestPayload.Contains(doaId.ToString()) &&
//                            first.RequestPayload.Contains(candidateId.ToString()))
//                        {
//                            matched = inv;
//                            break;
//                        }
//                    }

//                    if (matched == null)
//                    {
//                        // Create first ACK invocation
//                        var req = new IntegrationRequestDto
//                        {
//                            DoaCandidateId = doaId,
//                            CandidateId = candidateId,
//                            IntegrationType = integrationType,
//                            IntegrationOperation = operation
//                        };

//                        _logger.Information("[{Type}] Creating first ACKNOWLEDGE_RESPONSE for DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}, RVCaseId={RVCaseId}",
//                            integrationType, doaId, candidateId, oneHr.RVCaseId);

//                        await _invocationManager.CreateInvocationAsync(req);
//                        continue;
//                    }

//                    // Enforce AttemptCount cap
//                    if (matched.AttemptCount >= endpoint.RetriggerCount)
//                    {
//                        _logger.Information("[{Type}] ACK attempts {Attempt} reached RetriggerCount {Limit} for invocation {Id}. Skipping.",
//                            integrationType, matched.AttemptCount, endpoint.RetriggerCount, matched.IntegrationInvocationId);
//                        continue;
//                    }

//                    // Retrigger same invocation
//                    matched.AttemptCount += 1;
//                    matched.IntegrationStatus = IntegrationStatus.PENDING.ToString();
//                    matched.UpdatedOn = DateTime.UtcNow;
//                    matched.UpdatedUser = "System";
//                    await _invocationRepo.UpdateAsync(matched);
//                    await _invocationRepo.SaveChangesAsync();

//                    _logger.Information("[{Type}] Re-queuing ACKNOWLEDGE_RESPONSE invocation {Id} (attempt {Attempt}/{Limit})",
//                        integrationType, matched.IntegrationInvocationId, matched.AttemptCount, endpoint.RetriggerCount);

//                    await _invocationManager.ProcessPendingInvocationsAsync();
//                }

//                return true;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error(ex, "Error in ProcessAcknowledgeAsync (no-schema)");
//                return false;
//            }
//        }
//    }
//}
