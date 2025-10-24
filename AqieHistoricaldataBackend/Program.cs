using Amazon.Extensions.NETCore.Setup;
using Amazon.S3;
using AqieHistoricaldataBackend.Atomfeed.Endpoints;
using AqieHistoricaldataBackend.Atomfeed.Services;
using AqieHistoricaldataBackend.Config;
using AqieHistoricaldataBackend.Example.Endpoints;
using AqieHistoricaldataBackend.Example.Services;
using AqieHistoricaldataBackend.Utils;
using AqieHistoricaldataBackend.Utils.Http;
using AqieHistoricaldataBackend.Utils.Logging;
using AqieHistoricaldataBackend.Utils.Mongo;
using Elastic.CommonSchema;
using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Net.Http.Headers;
using Serilog;
using System.Diagnostics.CodeAnalysis;


var app = CreateWebApplication(args);
await app.RunAsync();
return;

[ExcludeFromCodeCoverage]
static WebApplication CreateWebApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    ConfigureBuilder(builder);

    var app = builder.Build();
    return SetupApplication(app);
}

[ExcludeFromCodeCoverage]
static void ConfigureBuilder(WebApplicationBuilder builder)
{
    builder.Configuration.AddEnvironmentVariables();

    // Load certificates into Trust Store - Note must happen before Mongo and Http client connections.
    builder.Services.AddCustomTrustStore();
    
    // Configure logging to use the CDP Platform standards.
    builder.Services.AddHttpContextAccessor();
    builder.Host.UseSerilog(CdpLogging.Configuration);
    
    // Default HTTP Client
    //builder.Services
    //    .AddHttpClient("DefaultClient")
    //    .AddHeaderPropagation();

    // Proxy HTTP Client
    builder.Services.AddTransient<ProxyHttpMessageHandler>();
    builder.Services
        .AddHttpClient("proxy")
        .ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>();

    builder.Services.AddHttpClient("Atomfeed", httpClient =>
    {
        httpClient.BaseAddress = new Uri("https://uk-air.defra.gov.uk/");
    
    }).ConfigurePrimaryHttpMessageHandler<ProxyHttpMessageHandler>(); ;
    
   

    // Propagate trace header.
    builder.Services.AddHeaderPropagation(options =>
    {
        var traceHeader = builder.Configuration.GetValue<string>("TraceHeader");
        if (!string.IsNullOrWhiteSpace(traceHeader))
        {
            options.Headers.Add(traceHeader);
        }
    });

    // Add Hangfire services.
    builder.Services.AddHangfire(config => config.UseMemoryStorage());
    builder.Services.AddHangfireServer();
    // Register AWS SDK services
    builder.Services.AddAWSService<IAmazonS3>();
    if(builder.Environment.IsDevelopment())
    {
        builder.Services.AddDefaultAWSOptions(new AWSOptions { Region = Amazon.RegionEndpoint.EUWest2 });
    }

    // Set up the MongoDB client. Config and credentials are injected automatically at runtime.
    builder.Services.Configure<MongoConfig>(builder.Configuration.GetSection("Mongo"));
    builder.Services.AddSingleton<IMongoDbClientFactory, MongoDbClientFactory>();
    
    builder.Services.AddHealthChecks();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    
    // Set up the endpoints and their dependencies
    builder.Services.AddSingleton<IExamplePersistence, ExamplePersistence>();
    builder.Services.AddSingleton<IAtomHistoryService, AtomHistoryService>();
    builder.Services.AddSingleton<IAtomHourlyFetchService, AtomHourlyFetchService>();
    builder.Services.AddSingleton<IAtomDailyFetchService, AtomDailyFetchService>();
    builder.Services.AddSingleton<IAtomAnnualFetchService, AtomAnnualFetchService>();
    builder.Services.AddSingleton<IAWSS3BucketService, AWSS3BucketService>();
    builder.Services.AddSingleton<IS3TransferUtility, S3TransferUtility>();    
    builder.Services.AddSingleton<IHourlyAtomFeedExportCSV, HourlyAtomFeedExportCSV>();
    builder.Services.AddSingleton<IDailyAtomFeedExportCSV, DailyAtomFeedExportCSV>();
    builder.Services.AddSingleton<IAnnualAtomFeedExportCSV, AnnualAtomFeedExportCSV>();
    builder.Services.AddSingleton<IAWSPreSignedURLService, AWSPreSignedURLService>();
    builder.Services.AddSingleton<IHistoryexceedenceService, HistoryexceedenceService>();
    builder.Services.AddSingleton<IAtomDataSelectionService, AtomDataSelectionService>();
    builder.Services.AddSingleton<IAtomDataSelectionStationService, AtomDataSelectionStationService>();
    builder.Services.AddSingleton<IAtomDataSelectionStationBoundryService, AtomDataSelectionStationBoundryService>();
    builder.Services.AddSingleton<IAtomDataSelectionHourlyFetchService, AtomDataSelectionHourlyFetchService>();
    builder.Services.AddSingleton<IDataSelectionHourlyAtomFeedExportCSV, DataSelectionHourlyAtomFeedExportCSV>();

}

[ExcludeFromCodeCoverage]
static WebApplication SetupApplication(WebApplication app)
{
    app.UseHeaderPropagation();
    app.UseRouting();
    app.MapHealthChecks("/health");

    // Example module, remove before deploying!
    app.UseExampleEndpoints();
    app.UseServiceAtomHistoryEndpoints();
    app.UseHangfireDashboard("/hangfire");
    return app;
}
