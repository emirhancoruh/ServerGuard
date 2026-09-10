namespace ServerGuard.Api.Hosting;

/// <summary>
/// Angular panelini API ile aynı kaynaktan servis eder.
/// </summary>
/// <remarks>
/// <para>
/// Panel <c>wwwroot</c> altına kopyalandığında devreye girer. Aynı kaynaktan servis etmek
/// üretimde en sade ve en güvenli kurulumdur: tek IIS sitesi, tek sertifika, CORS'a hiç
/// gerek yok ve token başka bir kaynağa gönderilmez.
/// </para>
/// <para>
/// Panel kopyalanmamışsa hiçbir şey yapılmaz; API salt veri servisi olarak çalışmayı sürdürür.
/// </para>
/// </remarks>
public static class SinglePageAppExtensions
{
    private const string EntryFileName = "index.html";

    /// <summary>Bilinmeyen API yollarının HTML'e düşmesini engelleyen yakalayıcı desen.</summary>
    private const string UnknownApiPattern = "/api/{**path}";

    public static WebApplication UseSinglePageApp(this WebApplication app)
    {
        if (!HasPanel(app.Environment))
        {
            app.Logger.LogInformation(
                "Single page app not found under wwwroot; the API serves data only. " +
                "Copy the Angular build output there to serve the panel from the same origin.");

            return app;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.Logger.LogInformation("Serving the panel from wwwroot on the same origin as the API.");

        return app;
    }

    /// <summary>
    /// Angular yönlendirmesi istemci tarafında olduğundan, bilinmeyen yollar
    /// <c>index.html</c>'e düşürülür.
    /// </summary>
    /// <remarks>
    /// Yönlendirme somut yolları yakalayıcı desenlere tercih ettiğinden, tanımlı controller
    /// uçları bundan etkilenmez. Tanımsız bir <c>/api/...</c> yolu ise HTML yerine 404 döner;
    /// aksi halde istemci hatalı bir adresi başarılı sanabilirdi.
    /// </remarks>
    public static WebApplication MapSinglePageAppFallback(this WebApplication app)
    {
        if (!HasPanel(app.Environment))
        {
            return app;
        }

        app.Map(UnknownApiPattern, () => Results.NotFound());
        app.MapFallbackToFile(EntryFileName);

        return app;
    }

    private static bool HasPanel(IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo(EntryFileName).Exists;
}
