using Newtonsoft.Json.Linq;
using Scriban;
using Scriban.Runtime;
using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class RenderingEngineService : IRenderingEngineService
    {
        private readonly ILogger _logger = Log.ForContext<RenderingEngineService>();

        public async Task<string> RenderPayloadAsync(string templateJson, object model)
        {
            try
            {
                _logger.Debug("Rendering payload template");

                if (templateJson is null) throw new ArgumentNullException(nameof(templateJson));
                if (model is null) throw new ArgumentNullException(nameof(model));

                // Build Scriban context
                var ctx = new TemplateContext
                {
                    // Keep C# property casing (Candidate.FirstName not candidate.firstname)
                    MemberRenamer = member => member.Name,
                    // Be tolerant if something is missing/null (renders empty string)
                    StrictVariables = false
                };

                var globals = new ScriptObject();

                // If model is ExpandoObject from ModelLoaderService, it's IDictionary<string, object>
                if (model is IDictionary<string, object> expando)
                {
                    foreach (var kvp in expando)
                    {
                        // Expose Candidate / DoaCandidate directly in the template
                        globals[kvp.Key] = kvp.Value;
                        _logger.Debug("Added {Key} to template globals", kvp.Key);
                    }
                }
                else
                {
                    // Regular POCO: expose its members as a single root object
                    // (access in template as {{ Model.Property }})
                    globals["Model"] = model;
                }

                ctx.PushGlobal(globals);

                // Parse + render the WHOLE JSON as a single template
                var template = Template.Parse(templateJson);
                if (template.HasErrors)
                {
                    var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
                    throw new InvalidOperationException($"Template parse error(s): {errors}");
                }

                var rendered = template.Render(ctx);

                // Validate we produced valid JSON
                try
                {
                    JToken.Parse(rendered); // throws if invalid
                }
                catch (Exception jsonEx)
                {
                    _logger.Error("Rendered output is not valid JSON: {Output}", rendered);
                    throw new InvalidOperationException("Template rendered invalid JSON", jsonEx);
                }

                _logger.Debug("Successfully rendered payload: {Rendered}", rendered);
                return await Task.FromResult(rendered);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error rendering payload");
                throw;
            }
        }

        public async Task<string> RenderUrlAsync(string baseUrl, string pathTemplate, object model)
        {
            try
            {
                // Simple URL combination - path parameters are handled in IntegrationRunnerService
                var url = $"{baseUrl.TrimEnd('/')}/{pathTemplate.TrimStart('/')}";
                return await Task.FromResult(url);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error rendering URL");
                throw;
            }
        }

        public async Task<string> RenderPathParametersAsync(string pathTemplate, object model)
        {
            try
            {
                // This method can be used if we need to render path parameters with templates
                // Currently not used as we handle path parameters directly in IntegrationRunnerService
                if (string.IsNullOrEmpty(pathTemplate) || !pathTemplate.Contains("{{"))
                {
                    return await Task.FromResult(pathTemplate);
                }

                var ctx = new TemplateContext
                {
                    MemberRenamer = member => member.Name,
                    StrictVariables = false
                };

                var globals = new ScriptObject();

                if (model is IDictionary<string, object> expando)
                {
                    foreach (var kvp in expando)
                    {
                        globals[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    globals["Model"] = model;
                }

                ctx.PushGlobal(globals);

                var template = Template.Parse(pathTemplate);
                if (template.HasErrors)
                {
                    _logger.Warning("Path template has errors: {Errors}",
                        string.Join("; ", template.Messages));
                    return pathTemplate;
                }

                return await Task.FromResult(template.Render(ctx));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error rendering path parameters");
                return pathTemplate;
            }
        }
    }
}
