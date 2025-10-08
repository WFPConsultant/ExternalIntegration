using Newtonsoft.Json.Linq;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;

namespace UVP.ExternalIntegration.Business.ResultMapper.Services
{
    public class ResultFieldExtractor : IResultFieldExtractor
    {
        private readonly ILogger _logger = Log.ForContext<ResultFieldExtractor>();

        public ResultMappingFields ExtractResponseFields(string response)
        {
            return ExtractResponseFieldsInternal(response, null);
        }

        public ResultMappingFields ExtractResponseFields(string response, string systemCode)
        {
            return ExtractResponseFieldsInternal(response, systemCode);
        }

        private ResultMappingFields ExtractResponseFieldsInternal(string response, string? systemCode)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                _logger.Warning("ExtractResponseFields: Empty or null response provided");
                return new ResultMappingFields();
            }

            try
            {
                var jo = JToken.Parse(response);

                _logger.Debug("Extracting fields from response for system {System}: {Response}",
                    systemCode ?? "Unknown",
                    response.Length > 1000 ? response.Substring(0, 1000) + "..." : response);

                // Try standard field extraction first
                string? reqId = FirstNonEmpty(
                    jo.SelectToken("clearanceRequestId")?.ToString(),
                    jo.SelectToken("requestId")?.ToString(),
                    jo.SelectToken("data.requestId")?.ToString(),
                    jo.SelectToken("payload.requestId")?.ToString());

                string? respId = FirstNonEmpty(
                    jo.SelectToken("clearanceResponseId")?.ToString(),
                    jo.SelectToken("responseId")?.ToString(),
                    jo.SelectToken("data.responseId")?.ToString(),
                    jo.SelectToken("payload.responseId")?.ToString(),
                    jo.SelectToken("resultId")?.ToString(),
                    jo.SelectToken("id")?.ToString(),
                    jo.SelectToken("caseId")?.ToString(),
                    jo.SelectToken("rvCaseId")?.ToString(),
                    jo.SelectToken("RVCaseId")?.ToString(),
                    jo.SelectToken("data.caseId")?.ToString(),
                    jo.SelectToken("payload.caseId")?.ToString());

                // If standard extraction failed, try system-specific extraction
                if (string.IsNullOrWhiteSpace(respId) && !string.IsNullOrWhiteSpace(systemCode))
                {
                    respId = ExtractSystemSpecificResponseId(jo, systemCode);
                }

                int? statusCode = null;
                var statusToken = jo.SelectToken("status") ?? jo.SelectToken("data.status") ?? jo.SelectToken("payload.status");
                if (statusToken != null && statusToken.Type == JTokenType.Integer)
                    statusCode = statusToken.Value<int>();

                string? statusLabel = FirstNonEmpty(
                    statusToken?.Type == JTokenType.String ? statusToken?.ToString() : null,
                    jo.SelectToken("statusText")?.ToString(),
                    jo.SelectToken("state")?.ToString(),
                    jo.SelectToken("decision")?.ToString());

                DateTime? statusDate = TryParseDate(
                    FirstNonEmpty(
                        jo.SelectToken("statusDate")?.ToString(),
                        jo.SelectToken("decisionDate")?.ToString(),
                        jo.SelectToken("completedOn")?.ToString(),
                        jo.SelectToken("data.statusDate")?.ToString(),
                        jo.SelectToken("payload.statusDate")?.ToString()));

                string? outcome = FirstNonEmpty(
                    jo.SelectToken("outcome")?.ToString(),
                    jo.SelectToken("decision")?.ToString(),
                    jo.SelectToken("result")?.ToString());

                var extractedFields = new ResultMappingFields
                {
                    RequestId = reqId,
                    ResponseId = respId,
                    StatusCode = statusCode,
                    StatusLabel = statusLabel,
                    StatusDate = statusDate,
                    Outcome = outcome
                };

                _logger.Information("Extracted fields for {System} - RequestId: {RequestId}, ResponseId: {ResponseId}, StatusCode: {StatusCode}, StatusLabel: {StatusLabel}",
                    systemCode ?? "Unknown",
                    extractedFields.RequestId ?? "null",
                    extractedFields.ResponseId ?? "null",
                    extractedFields.StatusCode?.ToString() ?? "null",
                    extractedFields.StatusLabel ?? "null");

                if (string.IsNullOrWhiteSpace(extractedFields.ResponseId))
                {
                    var availableTokens = GetAllTokenPaths(jo);
                    _logger.Warning("ResponseId extraction failed for {System}. Available tokens: {Tokens}",
                        systemCode ?? "Unknown", string.Join(", ", availableTokens.Take(20)));
                }

                return extractedFields;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error extracting fields from response for system {System}: {Response}",
                    systemCode ?? "Unknown",
                    response.Length > 500 ? response.Substring(0, 500) + "..." : response);
                return new ResultMappingFields();
            }
        }

        private string? ExtractSystemSpecificResponseId(JToken jo, string systemCode)
        {
            return systemCode.ToUpperInvariant() switch
            {
                "CMTS" => FirstNonEmpty(
                    jo.SelectToken("ClearanceRequestId")?.ToString(),
                    jo.SelectToken("clearanceRequestId")?.ToString(),
                    jo.SelectToken("requestId")?.ToString(),
                    jo.SelectToken("id")?.ToString()),

                "CDS" => FirstNonEmpty(
                    jo.SelectToken("cdsResponseId")?.ToString(),
                    jo.SelectToken("cds_response_id")?.ToString(),
                    jo.SelectToken("responseId")?.ToString(),
                    jo.SelectToken("response_id")?.ToString(),
                    jo.SelectToken("id")?.ToString()),

                "CLEARCHECK" => FirstNonEmpty(
                    jo.SelectToken("clearCheckId")?.ToString(),
                    jo.SelectToken("checkId")?.ToString(),
                    jo.SelectToken("responseId")?.ToString(),
                    jo.SelectToken("id")?.ToString()),

                "EARTHMED" => FirstNonEmpty(
                    jo.SelectToken("earthmedCaseId")?.ToString(),
                    jo.SelectToken("caseId")?.ToString(),
                    jo.SelectToken("medicalId")?.ToString(),
                    jo.SelectToken("responseId")?.ToString(),
                    jo.SelectToken("id")?.ToString()),

                _ => null
            };
        }

        private List<string> GetAllTokenPaths(JToken token, string currentPath = "")
        {
            var paths = new List<string>();

            if (token.Type == JTokenType.Property)
            {
                var prop = (JProperty)token;
                var newPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                paths.Add(newPath);
                paths.AddRange(GetAllTokenPaths(prop.Value, newPath));
            }
            else if (token.Type == JTokenType.Object)
            {
                foreach (var child in token.Children())
                {
                    paths.AddRange(GetAllTokenPaths(child, currentPath));
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                for (int i = 0; i < Math.Min(token.Count(), 3); i++)
                {
                    paths.AddRange(GetAllTokenPaths(token[i], $"{currentPath}[{i}]"));
                }
            }

            return paths;
        }

        public string? TryGetStringFromJsonAnyDepth(JToken root, params string[] keys)
        {
            foreach (var k in keys)
            {
                var t = root.SelectTokens($"$..{k}").FirstOrDefault();
                if (t != null)
                {
                    var s = t.Type == JTokenType.String ? t.Value<string>() : t.ToString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        _logger.Debug("Found value for key '{Key}': {Value}", k, s);
                        return s;
                    }
                }
            }
            _logger.Debug("No value found for keys: {Keys}", string.Join(", ", keys));
            return null;
        }

        public int TryGetIntFromJsonAnyDepth(JToken root, string key)
        {
            var token = root.SelectTokens($"$..{key}").FirstOrDefault();
            if (token == null)
            {
                _logger.Debug("No token found for key: {Key}", key);
                return 0;
            }

            if (token.Type == JTokenType.Integer) return token.Value<int>();

            var s = token.ToString();
            var result = int.TryParse(s, out var v) ? v : 0;
            _logger.Debug("Extracted int value for key '{Key}': {Value}", key, result);
            return result;
        }

        private static string? FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        private static DateTime? TryParseDate(string? s)
            => DateTime.TryParse(s, out var d) ? d : null;
       
    }
}
