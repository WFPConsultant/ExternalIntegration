using Microsoft.Extensions.DependencyInjection;
using Serilog;
using UVP.ExternalIntegration.Business.ResultMapper.Handlers;

namespace UVP.ExternalIntegration.Business.ResultMapper.Interfaces
{
    public interface IResultMappingHandlerFactory
    {
        IResultMappingSystemHandler? GetHandler(string systemCode);
        IEnumerable<IResultMappingSystemHandler> GetAllHandlers();
    }

    public class IntegrationSystemHandlerFactory : IResultMappingHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _handlerTypes;
        private readonly ILogger _logger = Log.ForContext<IntegrationSystemHandlerFactory>();

        public IntegrationSystemHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _handlerTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "CMTS", typeof(CmtsResultMappingHandler) },
                { "EARTHMED", typeof(EarthMedResultMappingHandler) }
            };
        }

        public IResultMappingSystemHandler? GetHandler(string systemCode)
        {
            if (string.IsNullOrWhiteSpace(systemCode))
            {
                _logger.Warning("GetHandler called with null or empty systemCode");
                return null;
            }

            var normalizedCode = systemCode.Trim().ToUpperInvariant();
            if (!_handlerTypes.TryGetValue(normalizedCode, out var handlerType))
            {
                _logger.Warning("No handler found for system code: {SystemCode}", systemCode);
                return null;
            }

            try
            {
                var handler = (IResultMappingSystemHandler)_serviceProvider.GetRequiredService(handlerType);
                _logger.Debug("Retrieved handler for system: {SystemCode}", systemCode);
                return handler;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating handler for system code: {SystemCode}", systemCode);
                return null;
            }
        }

        public IEnumerable<IResultMappingSystemHandler> GetAllHandlers()
        {
            var handlers = new List<IResultMappingSystemHandler>();
            foreach (var handlerType in _handlerTypes.Values)
            {
                try
                {
                    var handler = (IResultMappingSystemHandler)_serviceProvider.GetRequiredService(handlerType);
                    handlers.Add(handler);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error creating handler of type: {HandlerType}", handlerType.Name);
                }
            }
            return handlers;
        }
    }
}
