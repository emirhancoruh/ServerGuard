using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.Extensions.Options;
using Serilog;
using ServerGuard.Api.Configuration;
using ServerGuard.Api.Data;
using ServerGuard.Api.Detection;
using ServerGuard.Api.ErrorHandling;
using ServerGuard.Api.Hosting;
using ServerGuard.Api.Maintenance;
using ServerGuard.Api.Monitoring;
using ServerGuard.Api.Notifications;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Reputation;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Api.Validation;
using ServerGuard.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.WriteToRollingFile(builder.Environment.ContentRootPath));

builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Limits.MaxRequestBodySize = RequestLimits.MaxRequestBodyBytes);

builder.Services.Configure<Microsoft.AspNetCore.Builder.IISServerOptions>(iis =>
    iis.MaxRequestBodySize = RequestLimits.MaxRequestBodyBytes);

builder.Services.AddOpenApi();
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<ServerMetricDtoValidator>();

builder.Services.Configure<SecurityHeaderOptions>(
    builder.Configuration.GetSection(SecurityHeaderOptions.SectionName));
builder.Services.AddApiSecurity(builder.Configuration, builder.Environment);
builder.Services.AddApiRateLimiting(builder.Configuration);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMaintenance(builder.Configuration);
builder.Services.AddMonitoring(builder.Configuration);
builder.Services.AddIpReputation(builder.Configuration);
builder.Services.AddAlertNotifications(builder.Configuration);
builder.Services.AddDetection(builder.Configuration);
builder.Services.AddRealtime();
builder.Services.AddWebClientCors(builder.Configuration, builder.Environment);

var app = builder.Build();

var security = app.Services.GetRequiredService<IOptions<SecurityOptions>>().Value;

// Panel ayrı bir sitede yayınlandığında CORS zorunlu hale gelir ve yanlış yazılmış bir
// origin tarayıcıda opak bir hataya dönüşür. Uygulanan liste açılışta yazılır; panel
// boş görünüyorsa ilk bakılacak yer burasıdır.
LogAllowedOrigins(app, builder.Configuration);

static void LogAllowedOrigins(WebApplication app, IConfiguration configuration)
{
    var configured = configuration.GetSection(WebClientCors.AllowedOriginsKey).Get<string[]>() ?? [];
    var applied = WebClientCors.ResolveOrigins(configuration, app.Environment);
    var dropped = configured.Except(applied, StringComparer.OrdinalIgnoreCase).ToArray();

    // Elenen origin'i yazmamak, "her şey doğru görünüyor ama panel çalışmıyor" durumunu
    // yaratır; hangi değerin neden düştüğü açıkça söylenir.
    if (dropped.Length > 0)
    {
        app.Logger.LogWarning(
            "CORS origins ignored in Production because they are loopback addresses: {Origins}. " +
            "Use the address the panel is actually opened with (server name or IP), not localhost.",
            string.Join(", ", dropped));
    }

    if (applied.Length == 0)
    {
        app.Logger.LogWarning(
            "No CORS origin is allowed ({Key} is empty). A panel served from another site will be " +
            "blocked by the browser.",
            WebClientCors.AllowedOriginsKey);

        return;
    }

    app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", applied));
}

// Güvenlik header'ları boru hattının en başında eklenir; böylece statik dosyalar ve
// hata yanıtları dahil hiçbir yanıt onlarsız çıkmaz.
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS zorunluluğu ayarla açılır. Geçerli bir sertifika kurulmadan açılırsa aynı ağdaki
// agent'lar güvenilmeyen sertifika nedeniyle bağlanamaz; bu yüzden varsayılan kapalıdır.
if (security.RequireHttps)
{
    app.UseHttpsRedirection();
}

// Panel API ile aynı kaynaktan servis edilirse CORS'a hiç gerek kalmaz ve tek sertifika yeter.
app.UseSinglePageApp();

app.UseCors(WebClientCors.PolicyName);

app.UseAuthentication();
app.UseApiRateLimiting();
app.UseAuthorization();

app.MapControllers();
app.MapRealtimeHubs();
app.MapHealthEndpoints();
app.MapSinglePageAppFallback();

app.Run();
