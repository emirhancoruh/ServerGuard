using System.ComponentModel.DataAnnotations;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Configuration;

/// <summary>
/// Tüm toplayıcıların paylaştığı temel ayarlar. Toplayıcıya özel ayarlar
/// <see cref="MetricsOptions"/> ve <see cref="SecurityEventOptions"/> içindedir.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    private const string MissingApiKeyMessage =
        "Agent:ApiKey tanimli degil. API tarafindaki Security:Ingest:ApiKeys listesinde bulunan bir anahtar girin " +
        "('ServerGuard.Tools new-key' ile uretilir). Anahtar olmadan gonderilen her kayit reddedilir.";

    private const string ShortApiKeyMessage =
        "Agent:ApiKey cok kisa. 'ServerGuard.Tools new-key' ile uretilmis bir anahtar kullanin.";

    private string? _serverName;

    /// <summary>Boş bırakılırsa makine adı kullanılır.</summary>
    public string ServerName
    {
        get => string.IsNullOrWhiteSpace(_serverName) ? Environment.MachineName : _serverName;
        set => _serverName = value;
    }

    [Required]
    public Uri? ApiBaseUrl { get; set; }

    /// <summary>
    /// API'nin bu agent'ı tanıması için gereken anahtar.
    /// </summary>
    /// <remarks>
    /// Eksikse agent hiç açılmaz. Bu bilinçli bir tercihtir: anahtarsız bir agent hiçbir kaydı
    /// teslim edemez, yalnızca sessizce reddedilir. Açılışta durmak, sorunu haftalar sonra
    /// "veri neden gelmiyor?" olarak keşfetmekten iyidir.
    /// </remarks>
    [Required(ErrorMessage = MissingApiKeyMessage)]
    [MinLength(AuthConstraints.MinimumSecretLength, ErrorMessage = ShortApiKeyMessage)]
    public string ApiKey { get; set; } = string.Empty;
}
