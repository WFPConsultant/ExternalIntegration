using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IEarthMedTokenService
    {
        //Task<string?> GetAccessTokenAsync();
        Task<string?> GetAccessTokenAsync();
        void InvalidateToken();
    }
}
