namespace UVP.ExternalIntegration.Domain.DTOs
{
    public class IntegrationRequestDto
    {
        public int DoaCandidateId { get; set; }
        public int CandidateId { get; set; }
        public string IntegrationType { get; set; } = string.Empty;
        public string? IntegrationOperation { get; set; }
    }
}
