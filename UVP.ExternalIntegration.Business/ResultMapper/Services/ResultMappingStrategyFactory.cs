using Microsoft.Extensions.DependencyInjection;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.Strategies;

namespace UVP.ExternalIntegration.Business.ResultMapper.Services
{
    /// <summary>
    /// Factory that resolves result mapping strategies from DI container based on integration type
    /// </summary>
    public class ResultMappingStrategyFactory : IResultMappingStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _strategyTypes;
        private readonly ILogger _logger = Log.ForContext<ResultMappingStrategyFactory>();

        public ResultMappingStrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;

            // Map integration types to their strategy implementations
            // This is the ONLY place you need to register a new integration
            _strategyTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "CMTS", typeof(CmtsResultMappingStrategy) },
                { "EARTHMED", typeof(EarthMedResultMappingStrategy) }
                // Add new integrations here:
                // { "NEWSYSTEM", typeof(NewSystemResultMappingStrategy) }
            };

            _logger.Information("ResultMappingStrategyFactory initialized with {Count} strategies: {Types}",
                _strategyTypes.Count,
                string.Join(", ", _strategyTypes.Keys));
        }

        public IResultMappingStrategy? GetStrategy(string integrationType)
        {
            if (string.IsNullOrWhiteSpace(integrationType))
            {
                _logger.Warning("GetStrategy called with null or empty integrationType");
                return null;
            }

            var normalizedType = integrationType.Trim().ToUpperInvariant();

            if (!_strategyTypes.TryGetValue(normalizedType, out var strategyType))
            {
                _logger.Warning("No strategy registered for integration type: {IntegrationType}. Available types: {Available}",
                    integrationType,
                    string.Join(", ", _strategyTypes.Keys));
                return null;
            }

            try
            {
                var strategy = (IResultMappingStrategy)_serviceProvider.GetRequiredService(strategyType);
                _logger.Debug("Retrieved strategy for integration type: {IntegrationType} -> {StrategyType}",
                    integrationType,
                    strategyType.Name);
                return strategy;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating strategy for integration type: {IntegrationType}", integrationType);
                return null;
            }
        }

        public IEnumerable<IResultMappingStrategy> GetAllStrategies()
        {
            var strategies = new List<IResultMappingStrategy>();

            foreach (var kvp in _strategyTypes)
            {
                try
                {
                    var strategy = (IResultMappingStrategy)_serviceProvider.GetRequiredService(kvp.Value);
                    strategies.Add(strategy);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error creating strategy for type: {StrategyType}", kvp.Value.Name);
                }
            }

            return strategies;
        }
    }
}
