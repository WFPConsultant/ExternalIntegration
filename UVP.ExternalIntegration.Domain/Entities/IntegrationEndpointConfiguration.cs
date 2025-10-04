namespace UVP.ExternalIntegration.Domain.Entities
{
    public class IntegrationEndpointConfiguration
    {
        public int IntegrationEndpointId { get; set; }
        public string IntegrationType { get; set; } = string.Empty;
        public string IntegrationOperation { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string PathTemplate { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; }
        public int MaxAttempts { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedUser { get; set; } = string.Empty;
        public DateTime UpdatedOn { get; set; }
        public string UpdatedUser { get; set; } = string.Empty;
        public string? SamplePayload { get; set; }
        public string? SampleResponse { get; set; }
        public string UVPDataModel { get; set; } = string.Empty;
        public string PayloadModelMapper { get; set; } = string.Empty;
        public bool Retrigger { get; set; }
        public int RetriggerCount { get; set; }
        public int RetriggerInterval { get; set; }
    }
}
