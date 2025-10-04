using Newtonsoft.Json;

namespace UVP.ExternalIntegration.Domain.DTOs
{
    public class ClearanceResponseDto
    {
        [JsonProperty("clearanceRequestId")]
        public string? ClearanceRequestId { get; set; }

        [JsonProperty("clearanceResponseId")]
        public string? ClearanceResponseId { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("statusDate")]
        public DateTime? StatusDate { get; set; }
    }
}
