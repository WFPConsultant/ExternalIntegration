namespace UVP.ExternalIntegration.Business.ResultMapper.DTOs
{
    public class ResultMappingFields
    {
        public string? RequestId { get; set; }
        public string? ResponseId { get; set; }
        public int? StatusCode { get; set; }
        public string? StatusLabel { get; set; }
        public DateTime? StatusDate { get; set; }
        public string? Outcome { get; set; }
    }
}
