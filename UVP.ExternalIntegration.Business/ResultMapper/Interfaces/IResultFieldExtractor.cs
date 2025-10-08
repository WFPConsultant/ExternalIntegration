using Newtonsoft.Json.Linq;
using UVP.ExternalIntegration.Business.ResultMapper.DTOs;

namespace UVP.ExternalIntegration.Business.ResultMapper.Interfaces
{
    public interface IResultFieldExtractor
    {
        ResultMappingFields ExtractResponseFields(string response);
        ResultMappingFields ExtractResponseFields(string response, string systemCode);
        string? TryGetStringFromJsonAnyDepth(JToken root, params string[] keys);
        int TryGetIntFromJsonAnyDepth(JToken root, string key);
    }
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
