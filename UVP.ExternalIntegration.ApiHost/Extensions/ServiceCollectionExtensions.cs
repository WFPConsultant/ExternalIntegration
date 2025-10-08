using Microsoft.Extensions.DependencyInjection;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.Handlers;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.Services;
using UVP.ExternalIntegration.Business.Services;
using UVP.ExternalIntegration.Repository.Interfaces;
using UVP.ExternalIntegration.Repository.Repositories;

namespace UVP.ExternalIntegration.ApiHost.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddIntegrationServices(this IServiceCollection services)
        {
            //// Register repositories
            //services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            //services.AddScoped<IIntegrationEndpointRepository, IntegrationEndpointRepository>();
            //services.AddScoped<IIntegrationInvocationRepository, IntegrationInvocationRepository>();

            //// Register services
            //services.AddScoped<IInvocationManagerService, InvocationManagerService>();
            //services.AddScoped<IRenderingEngineService, RenderingEngineService>();
            //services.AddScoped<IIntegrationRunnerService, IntegrationRunnerService>();
            //services.AddScoped<IResultMapperService, ResultMapperService>();
            //// Add ModelLoaderService
            //services.AddScoped<IModelLoaderService, ModelLoaderService>();

            //// Register HTTP client
            //services.AddHttpClient<IHttpConnectorService, HttpConnectorService>();

            //services.AddScoped<IKeyMappingProvider, KeyMappingProvider>();


            //// Register field extractor
            //services.AddScoped<IResultFieldExtractor, ResultFieldExtractor>();

            //// Register all system handlers
            //services.AddScoped<CmtsResultMappingHandler>();
            //services.AddScoped<EarthMedResultMappingHandler>();

            //// Register factory
            //services.AddScoped<IResultMappingHandlerFactory, IntegrationSystemHandlerFactory>();

            //// Register main service
            //services.AddScoped<IResultMapperService, ResultMapperService>();

            //services.AddScoped<IStatusPollingService, StatusPollingService>();
            //services.AddMemoryCache();
            //services.AddScoped<IEarthMedTokenService, EarthMedTokenService>();
            //return services;

            // =====================================
            // Repository Pattern
            // =====================================
            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IIntegrationEndpointRepository, IntegrationEndpointRepository>();
            services.AddScoped<IIntegrationInvocationRepository, IntegrationInvocationRepository>();

            // =====================================
            // Core Integration Services
            // =====================================
            services.AddScoped<IInvocationManagerService, InvocationManagerService>();
            services.AddScoped<IRenderingEngineService, RenderingEngineService>();
            services.AddScoped<IIntegrationRunnerService, IntegrationRunnerService>();
            services.AddScoped<IModelLoaderService, ModelLoaderService>();
            services.AddScoped<IIntegrationOrchestrationService, IntegrationOrchestrationService>();
            services.AddScoped<IStatusPollingService, StatusPollingService>();

            // =====================================
            // Result Mapping Services
            // =====================================
            services.AddScoped<IResultMapperService, ResultMapperService>();
            services.AddScoped<IResultFieldExtractor, ResultFieldExtractor>();
            services.AddScoped<IKeyMappingProvider, KeyMappingProvider>();

            // =====================================
            // Integration-Specific Handlers
            // =====================================
            services.AddScoped<CmtsResultMappingHandler>();
            services.AddScoped<EarthMedResultMappingHandler>();
            services.AddScoped<IResultMappingHandlerFactory, IntegrationSystemHandlerFactory>();

            // =====================================
            // HTTP and Authentication Services
            // =====================================
            services.AddHttpClient<IHttpConnectorService, HttpConnectorService>();
            services.AddMemoryCache();
            services.AddScoped<ITokenService, TokenService>();

            return services;
        }
    }
}
