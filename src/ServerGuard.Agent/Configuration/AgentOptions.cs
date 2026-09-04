using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Agent.Configuration;

/// <summary>
/// Tüm toplayıcıların paylaştığı temel ayarlar. Toplayıcıya özel ayarlar
/// <see cref="MetricsOptions"/> ve <see cref="SecurityEventOptions"/> içindedir.
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    private string? _serverName;

    /// <summary>Boş bırakılırsa makine adı kullanılır.</summary>
    public string ServerName
    {
        get => string.IsNullOrWhiteSpace(_serverName) ? Environment.MachineName : _serverName;
        set => _serverName = value;
    }

    [Required]
    public Uri? ApiBaseUrl { get; set; }
}
