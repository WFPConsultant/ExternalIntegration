using Newtonsoft.Json;

namespace UVP.ExternalIntegration.Domain.DTOs
{
    public class ClearanceRequestDto
    {
        [JsonProperty("ClearanceRequest")]
        public ClearanceRequest? ClearanceRequest { get; set; }
    }

    public class ClearanceRequest
    {
        [JsonProperty("externalRequestId")]
        public string? ExternalRequestId { get; set; }

        [JsonProperty("externalBatchId")]
        public string? ExternalBatchId { get; set; }

        [JsonProperty("lastName")]
        public string? LastName { get; set; }

        [JsonProperty("middleName")]
        public string? MiddleName { get; set; }

        [JsonProperty("firstName")]
        public string? FirstName { get; set; }

        [JsonProperty("indexNo")]
        public string? IndexNo { get; set; }

        [JsonProperty("gender")]
        public string? Gender { get; set; }

        [JsonProperty("countryOfBirth")]
        public string? CountryOfBirth { get; set; }

        [JsonProperty("countryOfBirthISOCode")]
        public string? CountryOfBirthISOCode { get; set; }

        [JsonProperty("nationality")]
        public string? Nationality { get; set; }

        [JsonProperty("nationalityISOCode")]
        public string? NationalityISOCode { get; set; }

        [JsonProperty("dateOfBirth")]
        public string? DateOfBirth { get; set; }

        [JsonProperty("department")]
        public string? Department { get; set; }

        [JsonProperty("requestorName")]
        public string? RequestorName { get; set; }

        [JsonProperty("requestorEmail")]
        public string? RequestorEmail { get; set; }
    }
}
