using Newtonsoft.Json;

namespace UVP.ExternalIntegration.Domain.DTOs.EarthMed
{
    public class EarthMedGetStatusResponseDto
    {
        [JsonProperty("IsSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("TotalRecordsRemaining")]
        public int TotalRecordsRemaining { get; set; }

        [JsonProperty("Result")]
        public List<EarthMedStatusResultDto>? Result { get; set; }
    }

    public class EarthMedStatusResultDto
    {
        [JsonProperty("Id")]
        public int Id { get; set; }

        [JsonProperty("IndexNumber")]
        public string? IndexNumber { get; set; }

        [JsonProperty("FirstName")]
        public string? FirstName { get; set; }

        [JsonProperty("LastName")]
        public string? LastName { get; set; }

        [JsonProperty("ReferenceNumber")]
        public string? ReferenceNumber { get; set; }

        [JsonProperty("SequenceNumber")]
        public string? SequenceNumber { get; set; }

        [JsonProperty("ClearanceType")]
        public string? ClearanceType { get; set; }

        [JsonProperty("ClearanceStatus")]
        public string? ClearanceStatus { get; set; }

        [JsonProperty("ClearanceDate")]
        public DateTime? ClearanceDate { get; set; }

        [JsonProperty("RequestDate")]
        public DateTime? RequestDate { get; set; }

        [JsonProperty("StartDate")]
        public DateTime? StartDate { get; set; }

        [JsonProperty("EndDate")]
        public DateTime? EndDate { get; set; }
    }
}
