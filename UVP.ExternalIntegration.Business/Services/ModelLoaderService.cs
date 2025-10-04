using Serilog;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.DTOs.EarthMed;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class ModelLoaderService : IModelLoaderService
    {
        private readonly IIntegrationInvocationRepository _invocationRepo;
        private readonly IIntegrationEndpointRepository _endpointRepo;                 // to get keyMap from endpoint
        private readonly IGenericRepository<IntegrationInvocationLog> _invocationLogRepo;

        private readonly IGenericRepository<DoaCandidate> _doaCandidateRepo;
        private readonly IGenericRepository<Candidate> _candidateRepo;
        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _clearancesOneHRRepo;

        private readonly IKeyMappingProvider _keyMappingProvider;
        private readonly IGenericRepository<Doa> _doaRepo;
        private readonly ILogger _logger = Log.ForContext<ModelLoaderService>();

        public ModelLoaderService(
            IIntegrationInvocationRepository invocationRepo,
            IIntegrationEndpointRepository endpointRepo,
            IGenericRepository<IntegrationInvocationLog> invocationLogRepo,
            IGenericRepository<DoaCandidate> doaCandidateRepo,
            IGenericRepository<Candidate> candidateRepo,
            IGenericRepository<DoaCandidateClearances> clearancesRepo,
            IGenericRepository<DoaCandidateClearancesOneHR> clearancesOneHRRepo,
            IGenericRepository<Doa> doaRepo,
            IKeyMappingProvider keyMappingProvider)
        {
            _invocationRepo = invocationRepo;
            _endpointRepo = endpointRepo;
            _invocationLogRepo = invocationLogRepo;

            _doaCandidateRepo = doaCandidateRepo;
            _candidateRepo = candidateRepo;
            _clearancesRepo = clearancesRepo;
            _clearancesOneHRRepo = clearancesOneHRRepo;
            _doaRepo = doaRepo;
            _keyMappingProvider = keyMappingProvider;
        }

        /// <summary>
        /// RETRY/queued execution: reconstruct IDs/context from the FIRST request log row (RequestPayload not null).
        /// </summary>
        public async Task<object> LoadModelDataAsync(string uvpDataModel, long integrationInvocationId)
        {
            _logger.Information("Loading model data for {InvocationId}, models: {UVPDataModel}",
                integrationInvocationId, uvpDataModel);

            var invocation = await _invocationRepo.GetByIdAsync(integrationInvocationId)
                            ?? throw new InvalidOperationException($"Invocation {integrationInvocationId} not found.");

            // Resolve endpoint (for key-map)
            var endpoint = await _endpointRepo.GetActiveEndpointAsync(
                               invocation.IntegrationType,
                               invocation.IntegrationOperation)
                           ?? throw new InvalidOperationException(
                               $"No active endpoint for {invocation.IntegrationType}/{invocation.IntegrationOperation}");

            var keyMap = _keyMappingProvider.GetKeyMap(endpoint); // external -> internal

            // FIRST request log row (payload present)
            var allLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == integrationInvocationId);
            var firstRequestLog = allLogs
                .Where(l => !string.IsNullOrWhiteSpace(l.RequestPayload))
                .OrderBy(l => l.CreatedOn)
                .FirstOrDefault();

            if (firstRequestLog == null || string.IsNullOrWhiteSpace(firstRequestLog.RequestPayload))
                throw new InvalidOperationException($"No RequestPayload found for invocation {integrationInvocationId} (expected in first log row).");

            // Build a generic KeyBag from the bootstrap payload
            var keyBag = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "IntegrationType", invocation.IntegrationType ?? string.Empty },
                { "IntegrationOperation", invocation.IntegrationOperation ?? string.Empty }
            };

            try
            {
                using var doc = JsonDocument.Parse(firstRequestLog.RequestPayload!);
                CollectUsefulKeys(doc.RootElement, keyBag);

                // Apply endpoint-specific (or default) mapping: ext -> int
                foreach (var kvp in keyMap)
                {
                    var extKey = kvp.Key;   // e.g., externalRequestId
                    var intKey = kvp.Value; // e.g., CandidateId

                    if (BagHasKey(keyBag, intKey)) continue;

                    if (TryGetFromBag(keyBag, extKey, out var val))
                    {
                        keyBag[intKey] = val!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse RequestPayload for {InvocationId}", integrationInvocationId);
            }

            // Pull normalized ids out of keyBag (tolerant: checks namespaced keys too)
            int doaCandidateId = TryGetIntFromBag(keyBag, "externalBatchId");//DoaCandidateId
            int candidateId = TryGetIntFromBag(keyBag, "externalRequestId");//CandidateId

            //for GET CMTS Clearance Status
            Guid id =Guid.Empty;
            if (keyBag.TryGetValue("id", out var idObj) && idObj != null)
            {
                Guid.TryParse(idObj.ToString(), out id);
            }

            // If one of the IDs is missing, try to infer using OneHR cross table
            if (doaCandidateId == 0 && candidateId == 0)
            {
                var oneHrByCandidate = (await _clearancesOneHRRepo.FindAsync(x => x.DoaCandidateClearanceId == Convert.ToString(id))).FirstOrDefault();
                if (oneHrByCandidate == null)//here oneHrByCandidate means we have to query with RVCaseId because Its the PUT request for Acknowledgement
                {
                    oneHrByCandidate = (await _clearancesOneHRRepo.FindAsync(x => x.RVCaseId == Convert.ToString(id))).FirstOrDefault();
                }
                if (oneHrByCandidate != null)
                {
                    doaCandidateId = oneHrByCandidate.DoaCandidateId;
                    candidateId = oneHrByCandidate.CandidateId;
                }
            }

            if (doaCandidateId == 0 && candidateId != 0)
            {
                var oneHrByCandidate = (await _clearancesOneHRRepo.FindAsync(x => x.CandidateId == candidateId)).FirstOrDefault();
                if (oneHrByCandidate != null) doaCandidateId = oneHrByCandidate.DoaCandidateId;
            }
            if (candidateId == 0 && doaCandidateId != 0)
            {
                var oneHrByDoa = (await _clearancesOneHRRepo.FindAsync(x => x.DoaCandidateId == doaCandidateId)).FirstOrDefault();
                if (oneHrByDoa != null) candidateId = oneHrByDoa.CandidateId;
            }

            if(doaCandidateId == 0 || candidateId == 0)
            {
                throw new InvalidOperationException(
                    $"Could not determine DoaCandidateId/CandidateId for invocation {integrationInvocationId}. " +
                    $"Ensure keyMap is correct and payload/logs contain mapped keys.");
            }

            // Build the dynamic bundle requested by rendering engine
            var modelNames = uvpDataModel.Split(',')
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s));

            dynamic result = new ExpandoObject();
            var modelDict = (IDictionary<string, object>)result;

            foreach (var name in modelNames)
            {
                object? modelData = null;

                switch (name)
                {
                    case "DoaCandidate":
                        modelData = await _doaCandidateRepo.GetByIdAsync(doaCandidateId);
                        break;

                    case "Candidate":
                        modelData = await _candidateRepo.GetByIdAsync(candidateId);
                        break;

                    case "DoaCandidateClearancesOneHR":
                        var oneHr = await _clearancesOneHRRepo.FindAsync(d => d.DoaCandidateId == doaCandidateId && d.CandidateId == candidateId);
                        modelData = oneHr.FirstOrDefault();
                        break;

                    case "DoaCandidateClearances":
                        var clearances = await _clearancesRepo.FindAsync(d => d.DoaCandidateId == doaCandidateId && d.RecruitmentClearanceCode == "CMTS");
                        modelData = clearances.FirstOrDefault();
                        break;

                    case "Doa":
                        var doaEntity = await _doaRepo.GetByIdAsync(doaCandidateId);
                        if (doaEntity != null)
                        {
                            modelData = new DoaIntegrationModel
                            {
                                Id = doaEntity.Id,
                                Name = doaEntity.Name,
                                OrganizationMission = doaEntity.OrganizationMission,
                                DutyStationCode = doaEntity.DutyStationCode,
                                DutyStationDescription = doaEntity.DutyStationDescription,
                                StartDate = doaEntity.StartDate,
                                ExpectedEndDate = doaEntity.ExpectedEndDate
                            };
                        }
                        break;

                    case "User":
                        var userEntity = await _candidateRepo.GetByIdAsync(candidateId);
                        if (userEntity != null)
                        {
                            modelData = new UserIntegrationModel
                            {
                                Id = userEntity.Id,
                                FirstName = userEntity.FirstName,
                                MiddleName = userEntity.MiddleName ?? string.Empty,
                                LastName = userEntity.LastName,
                                Gender = userEntity.Gender ?? string.Empty,
                                BirthDate = userEntity.DateOfBirth,
                                EmailAddress = string.Empty, // Not in Candidate entity
                                NationalityCode = userEntity.NationalityISOCode ?? string.Empty
                            };
                        }
                        break;

                    //case "DoaCandidate":
                    //    var doaCandidateEntity = await _doaCandidateRepo.GetByIdAsync(doaCandidateId);
                    //    if (doaCandidateEntity != null)
                    //    {
                    //        modelData = new DoaCandidateIntegrationModel
                    //        {
                    //            Id = doaCandidateEntity.Id,
                    //            ReferenceNumber = string.Empty, // Add to DoaCandidate entity if needed
                    //            SequenceNumber = string.Empty,  // Add to DoaCandidate entity if needed
                    //            ClearanceType = string.Empty,   // Add to DoaCandidate entity if needed
                    //            RequestStatus = doaCandidateEntity.Status ?? string.Empty,
                    //            RequestDate = doaCandidateEntity.CreatedDate,
                    //            StartDate = doaCandidateEntity.CreatedDate,
                    //            EndDate = doaCandidateEntity.UpdatedDate,
                    //            DoaId = doaCandidateId,
                    //            CandidateId = candidateId
                    //        };
                    //    }
                    //    break;

                    default:
                        _logger.Warning("Unknown model name: {Model}", name);
                        break;
                }

                if (modelData != null)
                {
                    modelDict[name] = modelData;
                    _logger.Debug("Loaded model {Model}", name);
                }
                else
                {
                    _logger.Warning("No data found for {Model} with DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                        name, doaCandidateId, candidateId);
                }
            }

            return result;
        }

        // -------- tolerant bag helpers --------

        private static bool BagHasKey(IDictionary<string, object> bag, string key)
            => bag.ContainsKey(key) || bag.Keys.Any(k => k.EndsWith("." + key, StringComparison.OrdinalIgnoreCase));

        private static bool TryGetFromBag(IDictionary<string, object> bag, string key, out object? value)
        {
            if (bag.TryGetValue(key, out value))
                return true;

            // allow namespaced keys like "ClearanceRequest.CandidateId"
            var match = bag.FirstOrDefault(kv => kv.Key.EndsWith("." + key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key))
            {
                value = match.Value;
                return true;
            }

            value = null;
            return false;
        }

        private static int TryGetIntFromBag(IDictionary<string, object> bag, string key)
        {
            if (TryGetFromBag(bag, key, out var v))
            {
                if (v is int i) return i;
                if (int.TryParse(v?.ToString(), out var n)) return n;
            }
            return 0;
        }

        /// <summary>
        /// Recursively collect useful keys:
        ///  - any "*Id" (number or numeric string) -> stores both raw and namespaced (prefix.key)
        ///  - "indexNo" (frequent business identifier)
        /// </summary>
        private static void CollectUsefulKeys(JsonElement element, IDictionary<string, object> bag, string? prefix = null)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var name = prop.Name;
                        var full = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";

                        void Add(string k, JsonElement v)
                        {
                            if (bag.ContainsKey(k)) return;

                            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
                                bag[k] = n;
                            else if (v.ValueKind == JsonValueKind.String)
                            {
                                var s = v.GetString()!;
                                if (int.TryParse(s, out var ns)) bag[k] = ns; else bag[k] = s;
                            }
                        }

                        if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                        {
                            Add(name, prop.Value);
                            Add(full, prop.Value);
                        }
                        if (name.Equals("indexNo", StringComparison.OrdinalIgnoreCase))
                        {
                            Add("IndexNo", prop.Value);
                            Add(full, prop.Value);
                        }

                        CollectUsefulKeys(prop.Value, bag, full);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectUsefulKeys(item, bag, prefix);
                    break;
            }
        }

        public async Task<object> LoadModelDataAsync(string uvpDataModel, IntegrationRequestDto bootstrapRequest)
        {
            // 1) Resolve IDs from the incoming DTO (first-run only; we do NOT persist these on the invocation row)
            var doaCandidateId = bootstrapRequest.DoaCandidateId;
            var candidateId = bootstrapRequest.CandidateId;

            // 2) If one side is missing, try to infer via OneHR cross table
            if (doaCandidateId == 0 && candidateId != 0)
            {
                var link = (await _clearancesOneHRRepo.FindAsync(x => x.CandidateId == candidateId)).FirstOrDefault();
                if (link != null) doaCandidateId = link.DoaCandidateId;
            }
            if (candidateId == 0 && doaCandidateId != 0)
            {
                var link = (await _clearancesOneHRRepo.FindAsync(x => x.DoaCandidateId == doaCandidateId)).FirstOrDefault();
                if (link != null) candidateId = link.CandidateId;
            }

            if (doaCandidateId == 0 && candidateId == 0)
                throw new InvalidOperationException("Bootstrap request did not contain resolvable DoaCandidateId/CandidateId.");

            // 3) Build the dynamic model bundle requested by the endpoint (comma-separated list)
            var modelNames = uvpDataModel.Split(',')
                                         .Select(s => s.Trim())
                                         .Where(s => !string.IsNullOrEmpty(s));

            dynamic result = new ExpandoObject();
            var modelDict = (IDictionary<string, object>)result;

            foreach (var name in modelNames)
            {
                object? modelData = null;

                switch (name)
                {
                    case "DoaCandidate":
                        modelData = await _doaCandidateRepo.GetByIdAsync(doaCandidateId);
                        break;

                    case "Candidate":
                        modelData = await _candidateRepo.GetByIdAsync(candidateId);
                        break;

                    case "DoaCandidateClearancesOneHR":
                        var oneHr = await _clearancesOneHRRepo.FindAsync(d =>
                            d.DoaCandidateId == doaCandidateId && d.CandidateId == candidateId);
                        modelData = oneHr.FirstOrDefault();
                        break;

                    case "DoaCandidateClearances":
                        var clearances = await _clearancesRepo.FindAsync(d =>
                            d.DoaCandidateId == doaCandidateId && d.RecruitmentClearanceCode == "CMTS");
                        modelData = clearances.FirstOrDefault();
                        break;

                    case "Doa":
                        var doaEntity = await _doaRepo.GetByIdAsync(doaCandidateId);
                        if (doaEntity != null)
                        {
                            modelData = new DoaIntegrationModel
                            {
                                Id = doaEntity.Id,
                                Name = doaEntity.Name,
                                OrganizationMission = doaEntity.OrganizationMission,
                                DutyStationCode = doaEntity.DutyStationCode,
                                DutyStationDescription = doaEntity.DutyStationDescription,
                                StartDate = doaEntity.StartDate,
                                ExpectedEndDate = doaEntity.ExpectedEndDate
                            };
                        }
                        break;

                    case "User":
                        var userEntity = await _candidateRepo.GetByIdAsync(candidateId);
                        if (userEntity != null)
                        {
                            modelData = new UserIntegrationModel
                            {
                                Id = userEntity.Id,
                                FirstName = userEntity.FirstName,
                                MiddleName = userEntity.MiddleName ?? string.Empty,
                                LastName = userEntity.LastName,
                                Gender = userEntity.Gender ?? string.Empty,
                                BirthDate = userEntity.DateOfBirth,
                                EmailAddress = string.Empty, // Not in Candidate entity
                                NationalityCode = userEntity.NationalityISOCode ?? string.Empty
                            };
                        }
                        break;

                    //case "DoaCandidate":
                    //    var doaCandidateEntity = await _doaCandidateRepo.GetByIdAsync(doaCandidateId);
                    //    if (doaCandidateEntity != null)
                    //    {
                    //        modelData = new DoaCandidateIntegrationModel
                    //        {
                    //            Id = doaCandidateEntity.Id,
                    //            ReferenceNumber = string.Empty, // Add to DoaCandidate entity if needed
                    //            SequenceNumber = string.Empty,  // Add to DoaCandidate entity if needed
                    //            ClearanceType = string.Empty,   // Add to DoaCandidate entity if needed
                    //            RequestStatus = doaCandidateEntity.Status ?? string.Empty,
                    //            RequestDate = doaCandidateEntity.CreatedDate,
                    //            StartDate = doaCandidateEntity.CreatedDate,
                    //            EndDate = doaCandidateEntity.UpdatedDate,
                    //            DoaId = doaCandidateId,
                    //            CandidateId = candidateId
                    //        };
                    //    }
                    //    break;


                    default:
                        _logger.Warning("Unknown model name: {Model}", name);
                        break;
                }

                if (modelData != null)
                {
                    modelDict[name] = modelData;
                    _logger.Debug("Loaded model {Model}", name);
                }
                else
                {
                    _logger.Warning("No data found for {Model} with DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                        name, doaCandidateId, candidateId);
                }
            }

            return result;
        }
    }
}
