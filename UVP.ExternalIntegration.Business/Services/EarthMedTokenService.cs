using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Domain.Configuration;

namespace UVP.ExternalIntegration.Business.Services
{
    public class EarthMedTokenService : IEarthMedTokenService
    {
        private readonly IHttpConnectorService _httpConnector;
        private readonly IMemoryCache _cache;
        private readonly EarthMedConfiguration _config;
        private readonly ILogger _logger = Log.ForContext<EarthMedTokenService>();
        private const string TokenCacheKey = "EARTHMED_ACCESS_TOKEN";
        private static readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);

        public EarthMedTokenService(
            IHttpConnectorService httpConnector,
            IMemoryCache cache,
            IOptions<EarthMedConfiguration> config)
        {
            _httpConnector = httpConnector;
            _cache = cache;
            _config = config.Value;

            ValidateConfiguration();
        }
        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_config.TokenUrl))
                throw new InvalidOperationException("EarthMed:TokenUrl configuration is missing");

            if (string.IsNullOrWhiteSpace(_config.ClientId))
                throw new InvalidOperationException("EarthMed:ClientId configuration is missing");

            if (string.IsNullOrWhiteSpace(_config.ClientSecret))
                throw new InvalidOperationException("EarthMed:ClientSecret configuration is missing");

            if (string.IsNullOrWhiteSpace(_config.Scope))
                throw new InvalidOperationException("EarthMed:Scope configuration is missing");

            _logger.Information("[EARTHMED] Token service configured with TokenUrl: {TokenUrl}", _config.TokenUrl);
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            // Step 1: Try to get from cache first
            if (_cache.TryGetValue(TokenCacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
            {
                _logger.Debug("[EARTHMED] Using cached access token");
                return cachedToken;
            }

            // Step 2: Cache miss - request new token (with lock to prevent concurrent requests)
            await _tokenLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock (another thread might have refreshed it)
                if (_cache.TryGetValue(TokenCacheKey, out cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
                {
                    _logger.Debug("[EARTHMED] Token was refreshed by another thread, using cached token");
                    return cachedToken;
                }

                return await RequestNewTokenAsync();
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        /// <summary>
        /// Public method to invalidate token when it results in unauthorized/bad request
        /// </summary>
        public void InvalidateToken()
        {
            _cache.Remove(TokenCacheKey);
            _logger.Warning("[EARTHMED] Access token invalidated from cache");
        }

        private async Task<string?> RequestNewTokenAsync()
        {
            _logger.Information("[EARTHMED] Requesting new access token from {TokenUrl}", _config.TokenUrl);

            var payload = $"grant_type=client_credentials" +
                         $"&client_id={_config.ClientId}" +
                         $"&client_secret={_config.ClientSecret}" +
                         $"&scope={_config.Scope}";

            var request = new HttpRequestDto
            {
                Url = _config.TokenUrl,
                Method = "POST",
                Payload = payload,
                TimeoutSeconds = 30
                // Headers removed - auto-detected as form-urlencoded by HttpConnectorService
            };

            var response = await _httpConnector.SendRequestAsync(request);

            if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
            {
                _logger.Error("[EARTHMED] Failed to obtain access token. Status: {StatusCode}, Body: {Body}",
                    response.StatusCode, response.Body ?? "empty");
                return null;
            }

            try
            {
                var tokenResponse = JsonConvert.DeserializeObject<EarthMedTokenResponse>(response.Body);
                if (tokenResponse?.AccessToken == null)
                {
                    _logger.Error("[EARTHMED] Token response did not contain access_token. Response: {Response}",
                        response.Body);
                    return null;
                }

                // Cache for specified duration minus buffer (default: 3540 seconds = 59 minutes)
                var expiresIn = tokenResponse.ExpiresIn > 0 ? tokenResponse.ExpiresIn : 3599;
                var cacheExpiration = TimeSpan.FromSeconds(expiresIn - _config.TokenCacheExpirationBuffer);

                _cache.Set(TokenCacheKey, tokenResponse.AccessToken, cacheExpiration);
                _logger.Information("[EARTHMED] Access token cached for {Seconds} seconds (expires in {Minutes} minutes)",
                    cacheExpiration.TotalSeconds, Math.Round(cacheExpiration.TotalMinutes, 1));

                return tokenResponse.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[EARTHMED] Error parsing token response. Body: {Body}", response.Body);
                return null;
            }
        }

        private class EarthMedTokenResponse
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



