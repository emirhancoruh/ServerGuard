using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Tools.Probing;

/// <summary>
/// Çalışan bir ServerGuard API'sine dışarıdan istek atarak sağlığını ve güvenlik
/// kurallarının gerçekten uygulandığını doğrular.
/// </summary>
/// <remarks>
/// <para>
/// Kontroller yalnızca "cevap veriyor mu" sorusunu değil, "kapılar kapalı mı" sorusunu da
/// yanıtlar: token'sız okuma ve anahtarsız yazma denemelerinin <b>reddedilmesi</b> beklenir.
/// Böylece yetkilendirme yanlışlıkla kaldırılırsa bu araç bunu hemen bildirir.
/// </para>
/// <para>
/// Hiçbir kontrol veritabanına kalıcı kayıt yazmaz. Agent anahtarı, kasıtlı olarak geçersiz
/// bir gövdeyle denenir: anahtar geçerliyse doğrulama hatası (400), geçersizse yetki
/// hatası (401) döner. İki durum birbirinden ayırt edilebilir ve veri kirlenmez.
/// </para>
/// </remarks>
public sealed class ApiProbe(HttpClient httpClient, ProbeSettings settings)
{
    private const string NegotiatePath = $"{ApiRoutes.MonitoringHub}/negotiate?negotiateVersion=1";
    private const string HealthyBody = "Healthy";

    /// <summary>
    /// Hatalı parola denemesinde kullanılan, hiçbir zaman doğru olamayacak değer.
    /// Biçimsel doğrulamadan geçecek kadar uzun tutulur; aksi halde 401 yerine 400 dönerdi.
    /// </summary>
    private const string DeliberatelyWrongPassword = "ServerGuardProbeInvalidPassword";

    /// <summary>Bu oranın üzerindeki 5xx yüzdesi uyarı üretir.</summary>
    private const double ErrorRateWarningPercent = 1;

    private static readonly string[] RequiredSecurityHeaders =
    [
        "X-Content-Type-Options",
        "X-Frame-Options",
        "Referrer-Policy",
        "Content-Security-Policy"
    ];

    public async Task<IReadOnlyList<ProbeStepResult>> RunAsync(CancellationToken cancellationToken)
    {
        List<ProbeStepResult> results =
        [
            await CheckLivenessAsync(cancellationToken),
            await CheckReadinessAsync(cancellationToken),
            await CheckSecurityHeadersAsync(cancellationToken),
            await CheckAnonymousReadIsBlockedAsync(cancellationToken),
            await CheckAnonymousWriteIsBlockedAsync(cancellationToken),
            await CheckAnonymousHubIsBlockedAsync(cancellationToken),
            await CheckIngestKeyAsync(cancellationToken)
        ];

        results.AddRange(await RunPanelChecksAsync(cancellationToken));

        return results;
    }

    // --- Erisilebilirlik ----------------------------------------------------

    private Task<ProbeStepResult> CheckLivenessAsync(CancellationToken cancellationToken)
    {
        const string Name = "API ayakta mi";

        return SafeAsync(Name, async () =>
        {
            using var response = await httpClient.GetAsync(ApiRoutes.Health, cancellationToken);
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            return response.IsSuccessStatusCode && body == HealthyBody
                ? ProbeStepResult.Pass(Name, $"{ApiRoutes.Health} -> {body}")
                : ProbeStepResult.Fail(Name, $"{ApiRoutes.Health} -> {(int)response.StatusCode} {body}");
        });
    }

    private Task<ProbeStepResult> CheckReadinessAsync(CancellationToken cancellationToken)
    {
        const string Name = "Veritabani erisilebilir mi";

        return SafeAsync(Name, async () =>
        {
            using var response = await httpClient.GetAsync(ApiRoutes.HealthReady, cancellationToken);
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            return response.IsSuccessStatusCode
                ? ProbeStepResult.Pass(Name, $"{ApiRoutes.HealthReady} -> {body}")
                : ProbeStepResult.Fail(
                    Name,
                    $"{ApiRoutes.HealthReady} -> {(int)response.StatusCode} {body}. Baglanti dizesini ve SQL Server'i kontrol edin.");
        });
    }

    private Task<ProbeStepResult> CheckSecurityHeadersAsync(CancellationToken cancellationToken)
    {
        const string Name = "Guvenlik header'lari";

        return SafeAsync(Name, async () =>
        {
            using var response = await httpClient.GetAsync(ApiRoutes.Health, cancellationToken);

            var missing = RequiredSecurityHeaders
                .Where(header => !response.Headers.Contains(header))
                .ToArray();

            return missing.Length == 0
                ? ProbeStepResult.Pass(Name, string.Join(", ", RequiredSecurityHeaders))
                : ProbeStepResult.Fail(Name, $"Eksik: {string.Join(", ", missing)}");
        });
    }

    // --- Kapali kapi kontrolleri --------------------------------------------

    private Task<ProbeStepResult> CheckAnonymousReadIsBlockedAsync(CancellationToken cancellationToken) =>
        ExpectUnauthorizedAsync(
            "Token'siz okuma engelleniyor mu",
            () => new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Servers),
            cancellationToken);

    private Task<ProbeStepResult> CheckAnonymousWriteIsBlockedAsync(CancellationToken cancellationToken) =>
        ExpectUnauthorizedAsync(
            "Anahtarsiz yazma engelleniyor mu",
            () => new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Metrics)
            {
                Content = JsonContent.Create(BuildInvalidMetric(), options: JsonDefaults.Options)
            },
            cancellationToken);

    private Task<ProbeStepResult> CheckAnonymousHubIsBlockedAsync(CancellationToken cancellationToken) =>
        ExpectUnauthorizedAsync(
            "Token'siz canli baglanti engelleniyor mu",
            () => new HttpRequestMessage(HttpMethod.Post, NegotiatePath),
            cancellationToken);

    private Task<ProbeStepResult> ExpectUnauthorizedAsync(
        string name,
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken) =>
        SafeAsync(name, async () =>
        {
            using var request = requestFactory();
            using var response = await httpClient.SendAsync(request, cancellationToken);

            return response.StatusCode is HttpStatusCode.Unauthorized
                ? ProbeStepResult.Pass(name, "401 Unauthorized")
                : ProbeStepResult.Fail(
                    name,
                    $"Beklenen 401, gelen {(int)response.StatusCode}. Yetkilendirme devre disi kalmis olabilir.");
        });

    // --- Agent anahtari -----------------------------------------------------

    private Task<ProbeStepResult> CheckIngestKeyAsync(CancellationToken cancellationToken)
    {
        const string Name = "Agent anahtari gecerli mi";

        if (string.IsNullOrWhiteSpace(settings.IngestApiKey))
        {
            return Task.FromResult(ProbeStepResult.Skip(Name, "--ingest-key verilmedi."));
        }

        return SafeAsync(Name, async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Metrics)
            {
                Content = JsonContent.Create(BuildInvalidMetric(), options: JsonDefaults.Options)
            };

            request.Headers.Add(AuthConstraints.ApiKeyHeaderName, settings.IngestApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            return response.StatusCode switch
            {
                // Anahtar kabul edildi, govde dogrulamadan gecemedi: beklenen ve istenen sonuc.
                HttpStatusCode.BadRequest => ProbeStepResult.Pass(Name, "Anahtar kabul edildi (400 dogrulama hatasi)."),
                HttpStatusCode.Unauthorized => ProbeStepResult.Fail(
                    Name,
                    "401 Unauthorized. Anahtar API'deki Security:Ingest:ApiKeys listesinde yok."),
                HttpStatusCode.Created => ProbeStepResult.Fail(
                    Name,
                    "Gecersiz govde 201 ile kabul edildi; dogrulama calismiyor."),
                _ => ProbeStepResult.Fail(Name, $"Beklenmeyen yanit: {(int)response.StatusCode}.")
            };
        });
    }

    /// <summary>
    /// Kasıtlı olarak doğrulamadan geçemeyecek bir metrik. Sunucu adı boş olduğundan
    /// hiçbir koşulda veritabanına yazılmaz.
    /// </summary>
    private static ServerMetricDto BuildInvalidMetric() =>
        new(string.Empty, 0, 0, DateTimeOffset.UtcNow);

    // --- Panel oturumu ------------------------------------------------------

    private async Task<IReadOnlyList<ProbeStepResult>> RunPanelChecksAsync(CancellationToken cancellationToken)
    {
        const string LoginName = "Panel oturumu acilabiliyor mu";

        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            return [ProbeStepResult.Skip(LoginName, "--user / --password verilmedi.")];
        }

        List<ProbeStepResult> results = [await CheckWrongPasswordIsRejectedAsync(cancellationToken)];

        var (loginResult, token) = await LoginAsync(LoginName, cancellationToken);
        results.Add(loginResult);

        if (token is null)
        {
            results.Add(ProbeStepResult.Skip("Sunucu durumlari", "Oturum acilamadi."));
            return results;
        }

        results.Add(await CheckCurrentUserAsync(token, cancellationToken));
        results.Add(await CheckServersAsync(token, cancellationToken));
        results.Add(await CheckOverviewAsync(token, cancellationToken));

        return results;
    }

    private Task<ProbeStepResult> CheckWrongPasswordIsRejectedAsync(CancellationToken cancellationToken)
    {
        const string Name = "Hatali parola reddediliyor mu";

        return SafeAsync(Name, async () =>
        {
            var request = new LoginRequestDto(settings.UserName!, DeliberatelyWrongPassword);

            using var response = await httpClient.PostAsJsonAsync(
                ApiRoutes.Login,
                request,
                JsonDefaults.Options,
                cancellationToken);

            return response.StatusCode is HttpStatusCode.Unauthorized
                ? ProbeStepResult.Pass(Name, "401 Unauthorized")
                : ProbeStepResult.Fail(Name, $"Beklenen 401, gelen {(int)response.StatusCode}.");
        });
    }

    private async Task<(ProbeStepResult Result, string? Token)> LoginAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new LoginRequestDto(settings.UserName!, settings.Password!);

            using var response = await httpClient.PostAsJsonAsync(
                ApiRoutes.Login,
                request,
                JsonDefaults.Options,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return (ProbeStepResult.Fail(name, $"{(int)response.StatusCode} dondu."), null);
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>(
                JsonDefaults.Options,
                cancellationToken);

            if (login is null || string.IsNullOrWhiteSpace(login.AccessToken))
            {
                return (ProbeStepResult.Fail(name, "Yanit token icermiyor."), null);
            }

            return (
                ProbeStepResult.Pass(name, $"Kullanici={login.UserName} Bitis={login.ExpiresAt:u}"),
                login.AccessToken);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return (ProbeStepResult.Fail(name, Describe(exception)), null);
        }
    }

    private Task<ProbeStepResult> CheckCurrentUserAsync(string token, CancellationToken cancellationToken)
    {
        const string Name = "Token kabul ediliyor mu";

        return SafeAsync(Name, async () =>
        {
            using var request = BuildAuthorizedRequest(HttpMethod.Get, ApiRoutes.CurrentUser, token);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ProbeStepResult.Fail(Name, $"{(int)response.StatusCode} dondu.");
            }

            var user = await response.Content.ReadFromJsonAsync<AuthenticatedUserDto>(
                JsonDefaults.Options,
                cancellationToken);

            return user is null
                ? ProbeStepResult.Fail(Name, "Yanit okunamadi.")
                : ProbeStepResult.Pass(Name, $"Kullanici={user.UserName}");
        });
    }

    private Task<ProbeStepResult> CheckServersAsync(string token, CancellationToken cancellationToken)
    {
        const string Name = "Sunucu durumlari";

        return SafeAsync(Name, async () =>
        {
            using var request = BuildAuthorizedRequest(HttpMethod.Get, ApiRoutes.Servers, token);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ProbeStepResult.Fail(Name, $"{(int)response.StatusCode} dondu.");
            }

            var servers = await response.Content.ReadFromJsonAsync<IReadOnlyList<ServerSummaryDto>>(
                JsonDefaults.Options,
                cancellationToken) ?? [];

            if (servers.Count == 0)
            {
                return ProbeStepResult.Warn(Name, "Hic sunucu kaydi yok; agent'lar veri gonderiyor mu?");
            }

            var summary = string.Join(
                ", ",
                servers.Select(server => $"{server.ServerName}={server.Status} ({server.SecondsSinceLastSeen}sn)"));

            var allOnline = servers.All(server => server.Status == ServerHealthStatus.Online);

            return allOnline
                ? ProbeStepResult.Pass(Name, summary)
                : ProbeStepResult.Warn(Name, summary);
        });
    }

    private Task<ProbeStepResult> CheckOverviewAsync(string token, CancellationToken cancellationToken)
    {
        const string Name = "Trafik ve hata orani";

        return SafeAsync(Name, async () =>
        {
            using var request = BuildAuthorizedRequest(HttpMethod.Get, ApiRoutes.Overview, token);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ProbeStepResult.Fail(Name, $"{(int)response.StatusCode} dondu.");
            }

            var overview = await response.Content.ReadFromJsonAsync<MonitoringOverviewDto>(
                JsonDefaults.Options,
                cancellationToken);

            if (overview is null)
            {
                return ProbeStepResult.Fail(Name, "Yanit okunamadi.");
            }

            var detail =
                $"Istek={overview.TotalRequestCount} 5xx={overview.ServerErrorCount} " +
                $"HataOrani=%{overview.ErrorRatePercent:F2} Alarm={overview.ActiveAlertCount} " +
                $"CevrimdisiSunucu={overview.OfflineServers}";

            return overview.ErrorRatePercent >= ErrorRateWarningPercent || overview.OfflineServers > 0
                ? ProbeStepResult.Warn(Name, detail)
                : ProbeStepResult.Pass(Name, detail);
        });
    }

    private static HttpRequestMessage BuildAuthorizedRequest(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    // --- Ortak hata sarmalayici ---------------------------------------------

    /// <summary>
    /// Bir kontrolün ağ hatası nedeniyle patlaması, diğer kontrollerin çalışmasını
    /// engellememelidir; hata da bir sonuçtur.
    /// </summary>
    private static async Task<ProbeStepResult> SafeAsync(string name, Func<Task<ProbeStepResult>> check)
    {
        try
        {
            return await check();
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return ProbeStepResult.Fail(name, Describe(exception));
        }
    }

    private static bool IsExpected(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException;

    private static string Describe(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";
}
