namespace UVP.ExternalIntegration.Business.ResultMapper.DTOs
{
    public class ResultMappingContext
    {
        public long DoaCandidateId { get; set; }
        public long CandidateId { get; set; }
        public string IntegrationType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public long IntegrationInvocationId { get; set; }
    }
}
