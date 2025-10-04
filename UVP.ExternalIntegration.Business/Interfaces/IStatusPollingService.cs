using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IStatusPollingService
    {
        /// <summary>
        /// Scan in-flight clearance rows and enqueue GET_CLEARANCE_STATUS invocations where due.
        /// Returns true if executed without unhandled errors.
        /// </summary>
        Task<bool> ProcessOpenClearancesAsync();
        Task<bool> ProcessAcknowledgeAsync();
    }
}
