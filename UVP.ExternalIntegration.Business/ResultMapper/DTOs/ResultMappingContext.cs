namespace UVP.ExternalIntegration.Business.ResultMapper.DTOs
{
    public class ResultMappingContext
    {
        public int DoaCandidateId { get; set; }
        public int CandidateId { get; set; }
        public string IntegrationType { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public long IntegrationInvocationId { get; set; }
    }
}
