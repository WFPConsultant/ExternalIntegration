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
}
