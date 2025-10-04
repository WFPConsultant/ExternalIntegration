using Hangfire.Dashboard;

namespace UVP.ExternalIntegration.ApiHost.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            // In production, implement proper authorization
            // For development, allow all
            return true;
        }
    }
}
