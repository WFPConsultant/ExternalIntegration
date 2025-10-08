using Serilog;
using System.Dynamic;
using System.Text.Json;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Domain.Enums;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class ModelLoaderService : IModelLoaderService
    {
        private readonly IIntegrationInvocationRepository _invocationRepo;
        private readonly IIntegrationEndpointRepository _endpointRepo;
        private readonly IGenericRepository<IntegrationInvocationLog> _invocationLogRepo;
        private readonly IGenericRepository<DoaCandidate> _doaCandidateRepo;
        private readonly IGenericRepository<Candidate> _candidateRepo;
        private readonly IGenericRepository<DoaCandidateClearances> _clearancesRepo;
        private readonly IGenericRepository<DoaCandidateClearancesOneHR> _clearancesOneHRRepo;
        private readonly IGenericRepository<Doa> _doaRepo;
        private readonly IGenericRepository<User> _userRepo;
        private readonly IKeyMappingProvider _keyMappingProvider;
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
            IGenericRepository<User> userRepo,
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
            _userRepo = userRepo;
            _keyMappingProvider = keyMappingProvider;
        }

        public async Task<object> LoadModelDataAsync(string uvpDataModel, long integrationInvocationId)
        {
            _logger.Information("Loading model data for invocation {InvocationId}, models: {UVPDataModel}",
                integrationInvocationId, uvpDataModel);

            var invocation = await _invocationRepo.GetByIdAsync(integrationInvocationId)
                            ?? throw new InvalidOperationException($"Invocation {integrationInvocationId} not found.");

            var endpoint = await _endpointRepo.GetActiveEndpointAsync(
                               invocation.IntegrationType,
                               invocation.IntegrationOperation)
                           ?? throw new InvalidOperationException(
                               $"No active endpoint for {invocation.IntegrationType}/{invocation.IntegrationOperation}");

            var keyMap = _keyMappingProvider.GetKeyMap(endpoint);

            var firstRequestLog = await GetFirstRequestLogAsync(integrationInvocationId);

            var keyBag = BuildKeyBagFromPayload(invocation, firstRequestLog.RequestPayload!, keyMap);

            var (doaCandidateId, candidateId) = await ResolveIdsAsync(keyBag, invocation.IntegrationType);

            return await BuildModelBundleAsync(uvpDataModel, doaCandidateId, candidateId);
        }

        public async Task<object> LoadModelDataAsync(string uvpDataModel, IntegrationRequestDto bootstrapRequest)
        {
            var (doaCandidateId, candidateId) = await ResolveIdsFromBootstrapAsync(bootstrapRequest);

            return await BuildModelBundleAsync(uvpDataModel, doaCandidateId, candidateId);
        }

        private async Task<IntegrationInvocationLog> GetFirstRequestLogAsync(long invocationId)
        {
            var allLogs = await _invocationLogRepo.FindAsync(l => l.IntegrationInvocationId == invocationId);
            var firstRequestLog = allLogs
                .Where(l => !string.IsNullOrWhiteSpace(l.RequestPayload))
                .OrderBy(l => l.CreatedOn)
                .FirstOrDefault();

            if (firstRequestLog == null || string.IsNullOrWhiteSpace(firstRequestLog.RequestPayload))
                throw new InvalidOperationException($"No RequestPayload found for invocation {invocationId}");

            return firstRequestLog;
        }

        private Dictionary<string, object> BuildKeyBagFromPayload(
            IntegrationInvocation invocation,
            string requestPayload,
            IDictionary<string, string> keyMap)
        {
            var keyBag = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "IntegrationType", invocation.IntegrationType ?? string.Empty },
                { "IntegrationOperation", invocation.IntegrationOperation ?? string.Empty }
            };

            try
            {
                using var doc = JsonDocument.Parse(requestPayload);
                CollectUsefulKeys(doc.RootElement, keyBag);

                // Apply key mapping
                foreach (var kvp in keyMap)
                {
                    var extKey = kvp.Key;
                    var intKey = kvp.Value;

                    if (BagHasKey(keyBag, intKey)) continue;

                    if (TryGetFromBag(keyBag, extKey, out var val))
                    {
                        keyBag[intKey] = val!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to parse RequestPayload for invocation {InvocationId}",
                    invocation.IntegrationInvocationId);
            }

            return keyBag;
        }

        private async Task<(long doaCandidateId, long candidateId)> ResolveIdsAsync(
            Dictionary<string, object> keyBag,
            string integrationType)
        {
            // Check for integration-specific ID extraction patterns
            if (integrationType.Equals(IntegrationType.EARTHMED.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return await ExtractIdsFromReferenceNumberAsync(keyBag);
            }

            // Default extraction for CMTS and others
            var doaCandidateId = TryGetIntFromBag(keyBag, "externalBatchId");
            var candidateId = TryGetIntFromBag(keyBag, "externalRequestId");

            // Fallback: try to find by ID field
            if (doaCandidateId == 0 && candidateId == 0)
            {
                Guid.TryParse(TryGetStringFromBag(keyBag, "id"), out var id);
                if (id != Guid.Empty)
                {
                    (doaCandidateId, candidateId) = await ResolveFromOneHRByIdAsync(id);
                }
            }

            // Cross-reference using OneHR table
            return await CrossReferenceIdsAsync(doaCandidateId, candidateId);
        }

        private async Task<(long doaCandidateId, long candidateId)> ExtractIdsFromReferenceNumberAsync(
            Dictionary<string, object> keyBag)
        {
            if (!TryGetFromBag(keyBag, "ReferenceNumber", out var refNumberObj) || refNumberObj == null)
            {
                _logger.Warning("[EARTHMED] ReferenceNumber not found in payload");
                return (0L, 0L);
            }

            var referenceNumber = refNumberObj.ToString();
            if (string.IsNullOrWhiteSpace(referenceNumber) || !referenceNumber.Contains("_"))
            {
                _logger.Warning("[EARTHMED] Invalid ReferenceNumber format: {ReferenceNumber}", referenceNumber);
                return (0L, 0L);
            }

            var parts = referenceNumber.Split('_');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var doaId) ||
                !int.TryParse(parts[1], out var doaCandidateId))
            {
                _logger.Warning("[EARTHMED] Failed to parse ReferenceNumber: {ReferenceNumber}", referenceNumber);
                return (0L, 0L);
            }

            var doaCandidate = (await _doaCandidateRepo.FindAsync(x =>
                x.DoaId == doaId && x.Id == doaCandidateId))
                .FirstOrDefault();

            if (doaCandidate == null)
            {
                _logger.Warning("[EARTHMED] DoaCandidate not found for DoaId={DoaId}, DoaCandidateId={DoaCandidateId}",
                    doaId, doaCandidateId);
                return (0L, 0L);
            }

            _logger.Information("[EARTHMED] Extracted: DoaId={DoaId}, DoaCandidateId={DoaCandidateId}, CandidateId={CandidateId}",
                doaId, doaCandidateId, doaCandidate.CandidateId);

            return (doaCandidateId, doaCandidate.CandidateId);
        }

        private async Task<(long doaCandidateId, long candidateId)> ResolveFromOneHRByIdAsync(Guid id)
        {
            var oneHr = (await _clearancesOneHRRepo.FindAsync(x => x.DoaCandidateClearanceId == id.ToString()))
                .FirstOrDefault();

            if (oneHr == null)
            {
                oneHr = (await _clearancesOneHRRepo.FindAsync(x => x.RVCaseId == id.ToString()))
                    .FirstOrDefault();
            }

            return oneHr != null ? ((long)oneHr.DoaCandidateId, (long)oneHr.CandidateId) : (0, 0);
        }

        private async Task<(long doaCandidateId, long candidateId)> CrossReferenceIdsAsync(
            long doaCandidateId,
            long candidateId)
        {
            if (doaCandidateId != 0 && candidateId != 0)
                return (doaCandidateId, candidateId);

            if (doaCandidateId == 0 && candidateId != 0)
            {
                var oneHr = (await _clearancesOneHRRepo.FindAsync(x => x.CandidateId == candidateId))
                    .FirstOrDefault();
                if (oneHr != null) doaCandidateId = oneHr.DoaCandidateId;
            }
            else if (candidateId == 0 && doaCandidateId != 0)
            {
                var oneHr = (await _clearancesOneHRRepo.FindAsync(x => x.DoaCandidateId == doaCandidateId))
                    .FirstOrDefault();
                if (oneHr != null) candidateId = oneHr.CandidateId;
            }

            if (doaCandidateId == 0 || candidateId == 0)
            {
                throw new InvalidOperationException(
                    "Could not determine DoaCandidateId/CandidateId. Ensure keyMap is correct.");
            }

            return (doaCandidateId, candidateId);
        }

        private async Task<(long doaCandidateId, long candidateId)> ResolveIdsFromBootstrapAsync(
            IntegrationRequestDto bootstrapRequest)
        {
            return await CrossReferenceIdsAsync(
                bootstrapRequest.DoaCandidateId,
                bootstrapRequest.CandidateId);
        }

        private async Task<object> BuildModelBundleAsync(string uvpDataModel, long doaCandidateId, long candidateId)
        {
            var modelNames = uvpDataModel.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));

            dynamic result = new ExpandoObject();
            var modelDict = (IDictionary<string, object>)result;

            foreach (var name in modelNames)
            {
                var modelData = await LoadSpecificModelAsync(name, doaCandidateId, candidateId);

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

        private async Task<object?> LoadSpecificModelAsync(string modelName, long doaCandidateId, long candidateId)
        {
            return modelName switch
            {
                "DoaCandidate" => await _doaCandidateRepo.GetByIdAsync(doaCandidateId),

                "Candidate" => await _candidateRepo.GetByIdAsync(candidateId),

                "DoaCandidateClearancesOneHR" => (await _clearancesOneHRRepo.FindAsync(d =>
                    d.DoaCandidateId == doaCandidateId && d.CandidateId == candidateId))
                    .FirstOrDefault(),

                "DoaCandidateClearances" => (await _clearancesRepo.FindAsync(d =>
                    d.DoaCandidateId == doaCandidateId))
                    .FirstOrDefault(),

                "Doa" => await LoadDoaModelAsync(doaCandidateId),

                "User" => await LoadUserModelAsync(candidateId),

                _ => null
            };
        }

        private async Task<object?> LoadDoaModelAsync(long doaCandidateId)
        {
            var doaCandidateEntity = await _doaCandidateRepo.GetByIdAsync(doaCandidateId);
            if (doaCandidateEntity == null) return null;

            var doaEntity = await _doaRepo.GetByIdAsync(doaCandidateEntity.DoaId);
            if (doaEntity == null) return null;

            return new
            {
                doaEntity.Id,
                doaEntity.Name,
                doaEntity.OrganizationMission,
                doaEntity.DutyStationCode,
                doaEntity.DutyStationDescription,
                doaEntity.StartDate,
                doaEntity.ExpectedEndDate
            };
        }

        private async Task<object?> LoadUserModelAsync(long candidateId)
        {
            var candidateEntity = await _candidateRepo.GetByIdAsync(candidateId);
            if (candidateEntity == null) return null;

            var userEntity = await _userRepo.GetByIdAsync(candidateEntity.UserId);
            if (userEntity == null) return null;

            return new
            {
                userEntity.Id,
                userEntity.FirstName,
                MiddleName = userEntity.MiddleName ?? string.Empty,
                userEntity.LastName,
                Gender = userEntity.Gender ?? string.Empty,
                BirthDate = userEntity.DateOfBirth,
                EmailAddress = userEntity.PersonalEmail ?? string.Empty,
                NationalityCode = userEntity.NationalityISOCode ?? string.Empty
            };
        }

        // Helper methods
        private static bool BagHasKey(IDictionary<string, object> bag, string key)
            => bag.ContainsKey(key) || bag.Keys.Any(k => k.EndsWith("." + key, StringComparison.OrdinalIgnoreCase));

        private static bool TryGetFromBag(IDictionary<string, object> bag, string key, out object? value)
        {
            if (bag.TryGetValue(key, out value))
                return true;

            var match = bag.FirstOrDefault(kv => kv.Key.EndsWith("." + key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key))
            {
                value = match.Value;
                return true;
            }

            value = null;
            return false;
        }

        private static long TryGetIntFromBag(IDictionary<string, object> bag, string key)
        {
            //if (TryGetFromBag(bag, key, out var v))
            //{
            //    if (v is int i) return i;
            //    if (int.TryParse(v?.ToString(), out var n)) return n;
            //}
            //return 0;
            if (TryGetFromBag(bag, key, out var v))
            {
                if (v is int i) return i;
                if (v is long l) return l;  // Handle long values
                if (long.TryParse(v?.ToString(), out var n)) return n;  // Changed to long.TryParse
            }
            return 0L;  // Use 0L for long literal
        }

        private static string? TryGetStringFromBag(IDictionary<string, object> bag, string key)
        {
            return TryGetFromBag(bag, key, out var v) ? v?.ToString() : null;
        }

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

                        // Collect IDs and special fields
                        if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("indexNo", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("IndexNumber", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("ReferenceNumber", StringComparison.OrdinalIgnoreCase))
                        {
                            Add(name, prop.Value);
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
    }
}