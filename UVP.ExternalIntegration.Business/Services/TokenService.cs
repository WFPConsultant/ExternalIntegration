using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.Configuration;

namespace UVP.ExternalIntegration.Business.Services
{
    /// <summary>
    /// Generic token service supporting multiple OAuth-based integrations
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IHttpConnectorService _httpConnector;
        private readonly IMemoryCache _cache;
        private readonly OAuthConfiguration _config;
        private readonly ILogger _logger = Log.ForContext<TokenService>();
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        public TokenService(
            IHttpConnectorService httpConnector,
            IMemoryCache cache,
            IOptions<OAuthConfiguration> config)
        {
            _httpConnector = httpConnector;
            _cache = cache;
            _config = config.Value;
        }

        public async Task<string?> GetAccessTokenAsync(string integrationType)
        {
            var integrationKey = integrationType.ToUpperInvariant();

            if (!_config.Integrations.ContainsKey(integrationKey))
            {
                _logger.Warning("[{IntegrationType}] OAuth configuration not found", integrationType);
                return null;
            }

            var settings = _config.Integrations[integrationKey];

            // Check if authentication is required
            if (!settings.RequiresAuthentication)
            {
                return null;
            }

            var cacheKey = $"ACCESS_TOKEN_{integrationKey}";

            // Try cache first
            if (_cache.TryGetValue(cacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                _logger.Debug("[{IntegrationType}] Using cached access token", integrationType);
                return cachedToken;
            }

            // Cache miss - request new token with lock
            await _tokenLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_cache.TryGetValue(cacheKey, out cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
                {
                    _logger.Debug("[{IntegrationType}] Token refreshed by another thread", integrationType);
                    return cachedToken;
                }

                return await RequestNewTokenAsync(integrationType, settings, cacheKey);
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public void InvalidateToken(string integrationType)
        {
            var cacheKey = $"ACCESS_TOKEN_{integrationType.ToUpperInvariant()}";
            _cache.Remove(cacheKey);
            _logger.Warning("[{IntegrationType}] Access token invalidated from cache", integrationType);
        }

        private async Task<string?> RequestNewTokenAsync(string integrationType, OAuthSettings settings, string cacheKey)
        {
            _logger.Information("[{IntegrationType}] Requesting new access token from {TokenUrl}",
                integrationType, settings.TokenUrl);

            var payload = $"grant_type=client_credentials" +
                         $"&client_id={settings.ClientId}" +
                         $"&client_secret={settings.ClientSecret}" +
                         $"&scope={settings.Scope}";

            var request = new HttpRequestDto
            {
                Url = settings.TokenUrl,
                Method = "POST",
                Payload = payload,
                TimeoutSeconds = 30
            };

            var response = await _httpConnector.SendRequestAsync(request);

            if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
            {
                _logger.Error("[{IntegrationType}] Failed to obtain access token. Status: {StatusCode}, Body: {Body}",
                    integrationType, response.StatusCode, response.Body ?? "empty");
                return null;
            }

            try
            {
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(response.Body);
                if (tokenResponse?.AccessToken == null)
                {
                    _logger.Error("[{IntegrationType}] Token response missing access_token. Response: {Response}",
                        integrationType, response.Body);
                    return null;
                }

                // Cache token with expiration buffer
                var expiresIn = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3599;
                var cacheExpiration = TimeSpan.FromSeconds(expiresIn - settings.TokenCacheExpirationBuffer);

                _cache.Set(cacheKey, tokenResponse.AccessToken, cacheExpiration);

                _logger.Information("[{IntegrationType}] Access token cached for {Seconds}s (expires in {Minutes}min)",
                    integrationType, cacheExpiration.TotalSeconds, Math.Round(cacheExpiration.TotalMinutes, 1));

                return tokenResponse.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[{IntegrationType}] Error parsing token response. Body: {Body}",
                    integrationType, response.Body);
                return null;
            }
        }

        private class TokenResponse
        {
            [JsonProperty("access_token")]
            public string? AccessToken { get; set; }

            [JsonProperty("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty("token_type")]
            public string? TokenType { get; set; }
        }
    }
}
