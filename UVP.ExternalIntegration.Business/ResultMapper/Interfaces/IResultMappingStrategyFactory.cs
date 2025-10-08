namespace UVP.ExternalIntegration.Business.ResultMapper.Interfaces
{
    /// <summary>
    /// Factory interface for creating result mapping strategies based on integration type
    /// </summary>
    public interface IResultMappingStrategyFactory
    {
        /// <summary>
        /// Gets the appropriate result mapping strategy for the given integration type
        /// </summary>
        /// <param name="integrationType">The integration system identifier (e.g., "CMTS", "EARTHMED")</param>
        /// <returns>Strategy instance or null if not found</returns>
        IResultMappingStrategy? GetStrategy(string integrationType);

        /// <summary>
        /// Gets all registered strategies (useful for diagnostics/testing)
        /// </summary>
        /// <returns>Collection of all available strategies</returns>
        IEnumerable<IResultMappingStrategy> GetAllStrategies();
    }
}
