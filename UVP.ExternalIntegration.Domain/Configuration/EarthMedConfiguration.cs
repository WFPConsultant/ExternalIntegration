using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UVP.ExternalIntegration.Domain.Configuration
{
    public class EarthMedConfiguration
    {
        public string TokenUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int TokenCacheExpirationBuffer { get; set; } = 60; // seconds to subtract from actual expiration
    }
}
