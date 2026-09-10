using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ServerGuard.Shared;

namespace ServerGuard.Api.Hosting;

/// <summary>
/// İki ayrı sağlık ucu tanımlar.
/// </summary>
/// <remarks>
/// <para>
/// <c>/health</c> yalnızca sürecin ayakta olduğunu söyler ve hiçbir dış bağımlılığa
/// dokunmaz. IIS ve dış izleme bu ucu sık aralıklarla çağırdığından, her çağrıda
/// veritabanına gitmek gereksiz yük olurdu.
/// </para>
/// <para>
/// <c>/health/ready</c> veritabanı erişimini de dener; API'nin gerçekten iş görebilir
/// durumda olduğunu bu uç söyler.
/// </para>
/// <para>
/// İki uç da kimlik doğrulaması istemez; yalnızca "Healthy"/"Unhealthy" döner,
/// içeriden bilgi sızdırmaz.
/// </para>
/// </remarks>
public static class HealthEndpointExtensions
{
    /// <summary>Dış bağımlılıkları da kontrol eden sağlık kontrollerinin etiketi.</summary>
    public const string ReadinessTag = "ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(ApiRoutes.Health, new HealthCheckOptions
        {
            Predicate = _ => false
        });

        endpoints.MapHealthChecks(ApiRoutes.HealthReady, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadinessTag)
        });

        return endpoints;
    }
}
