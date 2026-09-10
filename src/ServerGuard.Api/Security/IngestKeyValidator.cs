using Microsoft.Extensions.Options;
using ServerGuard.Shared.Security;

namespace ServerGuard.Api.Security;

/// <summary>
/// Yapılandırmadaki agent anahtarlarını sabit zamanlı karşılaştırmayla doğrular.
/// </summary>
/// <remarks>
/// Tüm anahtarlar her istekte baştan sona denenir; eşleşme bulunsa bile döngü kısa devre
/// yapmaz. Böylece yanıt süresi, kaçıncı anahtarın eşleştiğini ele vermez.
/// </remarks>
public sealed class IngestKeyValidator(IOptions<SecurityOptions> options) : IIngestKeyValidator
{
    private readonly IReadOnlyList<IngestKeyOptions> _apiKeys = options.Value.Ingest.ApiKeys;

    public string? ResolveAgentName(string presentedKey)
    {
        if (string.IsNullOrEmpty(presentedKey))
        {
            return null;
        }

        string? matchedName = null;

        foreach (var apiKey in _apiKeys)
        {
            if (SecretComparer.Equals(apiKey.Key, presentedKey))
            {
                matchedName = apiKey.Name;
            }
        }

        return matchedName;
    }
}
