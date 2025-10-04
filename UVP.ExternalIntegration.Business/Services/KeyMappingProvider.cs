using System.Text.Json;
using System.Text.RegularExpressions;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Business.Services
{
    /// <summary>
    /// Provides a mapping from external payload keys (vendor) to internal keys.
    /// Resolution order:
    ///   1) If endpoint.PayloadModelMapper has a JSON { "keyMap": { ext: int, ... } } -> use it.
    ///   2) If PayloadModelMapper is a JSON template, invert fields where value is a single placeholder,
    ///      e.g.  "externalRequestId": "{{ CandidateId }}", which yields ext->int = externalRequestId->CandidateId.
    ///   3) If it is a raw (non-JSON) template, regex-scan for the same pattern and invert.
    ///
    /// If all fail, returns an empty mapping (no hardcoded defaults).
    /// </summary>
    public class KeyMappingProvider : IKeyMappingProvider
    {
        // Mustache-like placeholders: {{ Key }}, ${Key}, {Key}
        private static readonly Regex PlaceholderRegex =
            new(@"^\s*(?:\{\{\s*(?<k>[A-Za-z0-9_.]+)\s*\}\}|\$\{\s*(?<k>[A-Za-z0-9_.]+)\s*\}|\{\s*(?<k>[A-Za-z0-9_.]+)\s*\})\s*$",
                RegexOptions.Compiled);

        // For raw-text templates, match "externalKey": "placeholder"
        private static readonly Regex RawJsonLineRegex =
            new("\"(?<ext>[^\"]+)\"\\s*:\\s*\"(?<ph>(?:\\{\\{\\s*[A-Za-z0-9_.]+\\s*\\}\\}|\\$\\{\\s*[A-Za-z0-9_.]+\\s*\\}|\\{\\s*[A-Za-z0-9_.]+\\s*\\}))\"",
                RegexOptions.Compiled);

        public IDictionary<string, string> GetKeyMap(IntegrationEndpointConfiguration endpoint)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (endpoint == null || string.IsNullOrWhiteSpace(endpoint.PayloadModelMapper))
                return map;

            var text = endpoint.PayloadModelMapper.Trim();

            // 1) Try explicit JSON with "keyMap"
            if (TryReadExplicitKeyMap(text, out var explicitMap))
                return explicitMap;

            // 2) Try to parse as JSON template and invert simple placeholders
            if (TryInvertJsonTemplate(text, out var invertedFromJson))
                return invertedFromJson;

            // 3) Fallback: raw-text template scan (still invert)
            if (TryInvertRawTemplate(text, out var invertedFromRaw))
                return invertedFromRaw;

            // No mapping discovered — return empty map (no hardcoded defaults)
            return map;
        }

        private static bool TryReadExplicitKeyMap(string text, out IDictionary<string, string> result)
        {
            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("keyMap", out var km) &&
                    km.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in km.EnumerateObject())
                    {
                        if (p.Value.ValueKind == JsonValueKind.String)
                            result[p.Name] = p.Value.GetString()!;
                    }
                    return result.Count > 0;
                }
            }
            catch
            {
                // Not a JSON with keyMap — ignore
            }
            return false;
        }

        private static bool TryInvertJsonTemplate(string text, out IDictionary<string, string> result)
        {
            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(text);
                InvertJsonObject(doc.RootElement, result, parentPath: null);
                return result.Count > 0;
            }
            catch
            {
                // Not valid JSON — fall through
                return false;
            }
        }

        private static void InvertJsonObject(JsonElement el, IDictionary<string, string> map, string? parentPath)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in el.EnumerateObject())
                {
                    var name = prop.Name; // external key
                    var full = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}.{name}";
                    var val = prop.Value;

                    // Only invert when the value is a single placeholder (no concatenation, no complex expression)
                    if (val.ValueKind == JsonValueKind.String)
                    {
                        var m = PlaceholderRegex.Match(val.GetString() ?? string.Empty);
                        if (m.Success)
                        {
                            var internalKey = m.Groups["k"].Value; // e.g., CandidateId
                            // store both plain and namespaced external keys as needed by loaders
                            if (!map.ContainsKey(name)) map[name] = internalKey;
                            if (!map.ContainsKey(full)) map[full] = internalKey;
                            continue;
                        }
                    }

                    // Recurse into objects/arrays
                    if (val.ValueKind == JsonValueKind.Object || val.ValueKind == JsonValueKind.Array)
                        InvertJsonObject(val, map, full);
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                    InvertJsonObject(item, map, parentPath);
            }
        }

        private static bool TryInvertRawTemplate(string text, out IDictionary<string, string> result)
        {
            result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (Match m in RawJsonLineRegex.Matches(text))
                {
                    var ext = m.Groups["ext"].Value;
                    var ph = m.Groups["ph"].Value;
                    var pm = PlaceholderRegex.Match(ph);
                    if (pm.Success)
                    {
                        var internalKey = pm.Groups["k"].Value;
                        if (!result.ContainsKey(ext)) result[ext] = internalKey;
                    }
                }
                return result.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
