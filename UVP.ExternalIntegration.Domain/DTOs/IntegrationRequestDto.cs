namespace UVP.ExternalIntegration.Domain.DTOs
{
    public class IntegrationRequestDto
    {
        public long DoaCandidateId { get; set; }
        public long CandidateId { get; set; }
        public string IntegrationType { get; set; } = string.Empty;
        public string? IntegrationOperation { get; set; }
    }
}
