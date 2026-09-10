using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Throttling;

/// <summary>
/// Hız sınırlamasının kayıt noktası.
/// </summary>
/// <remarks>
/// Sınırlar istemci başına ayrılır (partition): kimliği doğrulanmış istekler agent adına veya
/// kullanıcı adına, doğrulanmamış istekler uzak IP adresine göre. Böylece bir agent'ın veya
/// bir kullanıcının aşırı isteği diğerlerini etkilemez.
/// </remarks>
public static class ThrottlingExtensions
{
    private const string UnknownPartitionKey = "unknown";

    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            var options = configuration
                .GetSection(RateLimitOptions.SectionName)
                .Get<RateLimitOptions>() ?? new RateLimitOptions();

            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.OnRejected = (context, cancellationToken) =>
            {
                // İstemcinin ne zaman tekrar deneyebileceğini bilmesi, kör yeniden denemeyi önler.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                return ValueTask.CompletedTask;
            };

            AddFixedWindowPolicy(limiter, RateLimitPolicies.Ingest, options, options.IngestPermitLimit);
            AddFixedWindowPolicy(limiter, RateLimitPolicies.Panel, options, options.PanelPermitLimit);
            AddFixedWindowPolicy(limiter, RateLimitPolicies.Login, options, options.LoginPermitLimit);
        });

        return services;
    }

    /// <summary>
    /// Sınırlama kapatıldıysa devreye alınmaz; böylece "Enabled=false" ayarı gerçekten
    /// hiçbir maliyet üretmez.
    /// </summary>
    public static IApplicationBuilder UseApiRateLimiting(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<RateLimitOptions>>().Value;

        if (!options.Enabled)
        {
            app.Logger.LogWarning(
                "Rate limiting is disabled by configuration ({SectionName}:Enabled=false).",
                RateLimitOptions.SectionName);

            return app;
        }

        return app.UseRateLimiter();
    }

    private static void AddFixedWindowPolicy(
        RateLimiterOptions limiter,
        string policyName,
        RateLimitOptions options,
        int permitLimit)
    {
        limiter.AddPolicy(policyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            BuildPartitionKey(policyName, httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = options.Window,
                QueueLimit = options.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    }

    /// <summary>
    /// Politika adı anahtara dahil edilir; aynı istemcinin farklı politikalardaki sayaçları
    /// birbirine karışmaz.
    /// </summary>
    private static string BuildPartitionKey(string policyName, HttpContext httpContext)
    {
        var identity = httpContext.User.Identity;

        var client = identity is { IsAuthenticated: true, Name.Length: > 0 }
            ? identity.Name
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownPartitionKey;

        return $"{policyName}:{client}";
    }
}
