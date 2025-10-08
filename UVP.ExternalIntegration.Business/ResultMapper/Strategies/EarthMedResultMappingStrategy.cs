using Newtonsoft.Json;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Domain.DTOs.EarthMed;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Interfaces;

namespace UVP.ExternalIntegration.Business.ResultMapper.Strategies
{
    public class EarthMedResultMappingStrategy : IResultMappingStrategy
    {
        private readonly ILogger _logger = Log.ForContext<EarthMedResultMappingStrategy>();
        private readonly IGenericRepository<DoaCandidate> _doaCandidateRepo;

        public EarthMedResultMappingStrategy(IGenericRepository<DoaCandidate> doaCandidateRepo)
        {
            _doaCandidateRepo = doaCandidateRepo;
        }

        public string SystemCode => "EARTHMED";
        public bool RequiresAcknowledgeCycle => false;
        public int ClearanceCycleCount => 2;

        public string? ExtractRequestId(string responseBody)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<EarthMedCreateClearanceResponseDto>(responseBody);
                return response?.Result?.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[{System}] Failed to extract request ID", SystemCode);
                return null;
            }
        }

        public string? ExtractResponseId(string responseBody)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(responseBody);
                var firstResult = response?.Result?.FirstOrDefault();
                return firstResult?.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[{System}] Failed to extract response ID", SystemCode);
                return null;
            }
        }

        public bool IsMultiResultStatusResponse(string responseBody)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(responseBody);
                return response?.Result?.Count > 1;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<StatusResultItem>> ExtractStatusResultsAsync(string responseBody)
        {
            var results = new List<StatusResultItem>();

            try
            {
                var response = JsonConvert.DeserializeObject<EarthMedGetStatusResponseDto>(responseBody);
                if (response?.Result == null || !response.Result.Any())
                {
                    return results;
                }

                foreach (var result in response.Result)
                {
                    if (string.IsNullOrWhiteSpace(result.IndexNumber) ||
                        !int.TryParse(result.IndexNumber, out var candidateId))
                    {
                        _logger.Warning("[{System}] Invalid IndexNumber: {IndexNumber}", SystemCode, result.IndexNumber);
                        continue;
                    }

                    // Extract DoaId and DoaCandidateId from ReferenceNumber
                    var (doaId, doaCandidateId) = ParseReferenceNumber(result.ReferenceNumber);

                    if (doaCandidateId == 0)
                    {
                        _logger.Warning("[{System}] Failed to parse ReferenceNumber: {RefNum}", SystemCode, result.ReferenceNumber);
                        continue;
                    }

                    results.Add(new StatusResultItem
                    {
                        Identifier = result.Id.ToString(),
                        CandidateId = candidateId,
                        DoaCandidateId = doaCandidateId,
                        Status = result.ClearanceStatus,
                        StatusDate = result.ClearanceDate,
                        AdditionalFields = new Dictionary<string, string>
                        {
                            ["IndexNumber"] = result.IndexNumber,
                            ["ReferenceNumber"] = result.ReferenceNumber ?? string.Empty
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{System}] Failed to extract status results", SystemCode);
            }

            return results;
        }

        public string GetStatusCompletionCode() => "DELIVERED";

        private (long doaId, long doaCandidateId) ParseReferenceNumber(string? referenceNumber)
        {
            if (string.IsNullOrWhiteSpace(referenceNumber) || !referenceNumber.Contains("_"))
            {
                return (0, 0);
            }

            var parts = referenceNumber.Split('_');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var doaCandidateId) ||
                !int.TryParse(parts[1], out var doaId))
            {
                return (0, 0);
            }

            return (doaId, doaCandidateId);
        }
    }
}
