using Microsoft.Extensions.Options;
using Serilog;
using ServerGuard.Agent.Configuration;
using ServerGuard.Agent.Metrics;
using ServerGuard.Agent.Security;
using ServerGuard.Agent.Traffic;
using ServerGuard.Agent.Transport;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

const string WindowsServiceName = "ServerGuard.Agent";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = WindowsServiceName);

// Hizmet olarak calisirken konsol ciktisi hicbir yere gitmez. Varsayilan saglayicilar
// birakilirsa ayni kayit hem Olay Gunlugu'ne hem Serilog'a yazilir; tek kaynak birakilir.
builder.Logging.ClearProviders();
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.WriteToRollingFile(builder.Environment.ContentRootPath));

builder.Services.AddValidatedOptions<AgentOptions>(builder.Configuration, AgentOptions.SectionName);
builder.Services.AddValidatedOptions<MetricsOptions>(builder.Configuration, MetricsOptions.SectionName);
builder.Services.AddValidatedOptions<SecurityEventOptions>(builder.Configuration, SecurityEventOptions.SectionName);
builder.Services.AddValidatedOptions<TrafficOptions>(builder.Configuration, TrafficOptions.SectionName);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBackendHttpClient();
builder.Services.AddSingleton<IBackendSender, HttpBackendSender>();

builder.Services.AddBackendDispatcher<ServerMetricDto>(
    ApiRoutes.Metrics,
    services => services.GetRequiredService<IOptions<MetricsOptions>>().Value.QueueCapacity);

builder.Services.AddBackendDispatcher<SecurityEventDto>(
    ApiRoutes.SecurityEvents,
    services => services.GetRequiredService<IOptions<SecurityEventOptions>>().Value.QueueCapacity);

builder.Services.AddBackendDispatcher<TrafficLogDto>(
    ApiRoutes.TrafficLogs,
    services => services.GetRequiredService<IOptions<TrafficOptions>>().Value.QueueCapacity);

builder.Services.AddSingleton<ISystemMetricsReader, PerformanceCounterMetricsReader>();
builder.Services.AddSingleton<SecurityEventParser>();
builder.Services.AddSingleton<W3CLogParser>();
builder.Services.AddSingleton<TrafficLogFileReader>();
builder.Services.AddSingleton<LogOffsetStore>();
builder.Services.AddSingleton<LogDirectoryResolver>();

builder.Services.AddHostedService<MetricCollectorWorker>();
builder.Services.AddHostedService<SecurityEventWorker>();
builder.Services.AddHostedService<TrafficLogWorker>();

var host = builder.Build();
host.Run();
