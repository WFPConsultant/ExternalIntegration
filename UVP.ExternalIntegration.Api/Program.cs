using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using UVP.ExternalIntegration.ApiHost.Extensions;
using UVP.ExternalIntegration.ApiHost.Filters;
using UVP.ExternalIntegration.ApiHost.Middleware;
using UVP.ExternalIntegration.Business.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.Handlers;
using UVP.ExternalIntegration.Business.ResultMapper.Interfaces;
using UVP.ExternalIntegration.Business.ResultMapper.Services;
using UVP.ExternalIntegration.Business.Services;
using UVP.ExternalIntegration.Domain.Configuration;
using UVP.ExternalIntegration.Domain.Entities;
using UVP.ExternalIntegration.Repository.Context;
using UVP.ExternalIntegration.Repository.Interfaces;
using UVP.ExternalIntegration.Repository.Repositories;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/uvp-integration-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting UVP External Integration API");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "UVP External Integration API", Version = "v1" });
    });

    // Add Database Context
    builder.Services.AddDbContext<UVPDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    
     

    // =====================================
    // Repository Pattern Registrations
    // =====================================

    Log.Information("Registering repositories...");

    // Add Integration Services (repositories and business services)
    builder.Services.AddIntegrationServices();

    // Generic Repository
    //builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

    // Specific Repositories
    //builder.Services.AddScoped<IIntegrationEndpointRepository, IntegrationEndpointRepository>();
    builder.Services.AddScoped<IIntegrationInvocationRepository, IntegrationInvocationRepository>();

    // Additional Generic Repositories for specific entities
    builder.Services.AddScoped<IGenericRepository<IntegrationInvocation>, GenericRepository<IntegrationInvocation>>();
    builder.Services.AddScoped<IGenericRepository<IntegrationInvocationLog>, GenericRepository<IntegrationInvocationLog>>();
    builder.Services.AddScoped<IGenericRepository<IntegrationEndpointConfiguration>, GenericRepository<IntegrationEndpointConfiguration>>();
    builder.Services.AddScoped<IGenericRepository<DoaCandidate>, GenericRepository<DoaCandidate>>();
    builder.Services.AddScoped<IGenericRepository<Candidate>, GenericRepository<Candidate>>();
    builder.Services.AddScoped<IGenericRepository<DoaCandidateClearances>, GenericRepository<DoaCandidateClearances>>();
    builder.Services.AddScoped<IGenericRepository<DoaCandidateClearancesOneHR>, GenericRepository<DoaCandidateClearancesOneHR>>();
    builder.Services.AddScoped<IGenericRepository<Doa>, GenericRepository<Doa>>();
    builder.Services.AddScoped<IStatusPollingService, StatusPollingService>();
    builder.Services.AddScoped<IGenericRepository<User>, GenericRepository<User>>();

    builder.Services.AddScoped<IModelLoaderService, ModelLoaderService>();

    // =====================================
    // Business Services Registrations
    // =====================================

    Log.Information("Registering business services...");

    // Core Integration Services
    builder.Services.AddScoped<IInvocationManagerService, InvocationManagerService>();
    builder.Services.AddScoped<IIntegrationRunnerService, IntegrationRunnerService>();
    builder.Services.AddScoped<IRenderingEngineService, RenderingEngineService>();
    builder.Services.AddScoped<IResultMapperService, ResultMapperService>();
    builder.Services.AddScoped<IIntegrationOrchestrationService, IntegrationOrchestrationService>();

    builder.Services.AddScoped<IKeyMappingProvider, KeyMappingProvider>();


    // Register field extractor
    builder.Services.AddScoped<IResultFieldExtractor, ResultFieldExtractor>();

    // Register all system handlers
    builder.Services.AddScoped<CmtsResultMappingHandler>();
    builder.Services.AddScoped<EarthMedResultMappingHandler>();

    // Register factory
    builder.Services.AddScoped<IResultMappingHandlerFactory, IntegrationSystemHandlerFactory>();

    // Register main service
    builder.Services.AddScoped<IResultMapperService, ResultMapperService>();
    // Add Memory Cache for token caching
    builder.Services.AddMemoryCache();

    // Add EARTHMED Token Service
    builder.Services.AddScoped<IEarthMedTokenService, EarthMedTokenService>();
    // Configure EarthMed settings
    builder.Services.Configure<EarthMedConfiguration>(
        builder.Configuration.GetSection("EarthMed"));


    // HTTP Client Service with Polly
    builder.Services.AddHttpClient<IHttpConnectorService, HttpConnectorService>(client =>
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", "UVP-Integration-Client/1.0");
        client.Timeout = TimeSpan.FromSeconds(100); // Default timeout, will be overridden per request
    });

    
  
    // Configure Hangfire
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSerilogLogProvider()
        .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

    builder.Services.AddHangfireServer();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        //app.UseSwaggerUI();
        app.UseSwaggerUI(c =>
        {
            c.DefaultModelsExpandDepth(-1);
        });
    }

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    app.UseHttpsRedirection();
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseAuthorization();

    // Configure Hangfire Dashboard
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });

    RecurringJob.AddOrUpdate<IInvocationManagerService>(
    "process-pending-invocations",
    service => service.ProcessPendingInvocationsAsync(),
    "0 */5 * * * *"); // at second 0, every 5 minutes

    //RecurringJob.AddOrUpdate<IInvocationManagerService>(
    //    "process-retryable-invocations",
    //    service => service.ProcessRetryableInvocationsAsync(),
    //    "0 */5 * * * *");  // at second 0, every 5 minutes
    RecurringJob.AddOrUpdate<IInvocationManagerService>(
   "process-retryable-invocations",
   service => service.ProcessRetryableInvocationsAsync(),
   "0 */5 * * * *");
    RecurringJob.AddOrUpdate<IStatusPollingService>(
        "uvp-poll-clearances",
        service => service.ProcessOpenClearancesAsync(),
        "0 */2 * * * *");
    RecurringJob.AddOrUpdate<IStatusPollingService>(
        "uvp-poll-acks",
        service => service.ProcessAcknowledgeAsync(),
        "0 */5 * * * *"); // every 5 minutes at second 15 (staggered)
                          // at second 0, every 5 minutes
                          // at second 0, every 5 minutes

    app.MapControllers();

    Log.Information("Application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}