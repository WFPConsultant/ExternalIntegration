namespace UVP.ExternalIntegration.Domain.Configuration
{
    /// <summary>
    /// Generic OAuth configuration for integration systems
    /// Supports multiple integration types through dictionary-based configuration
    /// </summary>
    public class OAuthConfiguration
    {
        public Dictionary<string, OAuthSettings> Integrations { get; set; } = new();
    }

    public class OAuthSettings
    {
        public string TokenUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int TokenCacheExpirationBuffer { get; set; } = 60;
        public bool RequiresAuthentication { get; set; } = false;
    }
}
