namespace ServerGuard.Api.Security;

/// <summary>Agent API anahtarını doğrular.</summary>
public interface IIngestKeyValidator
{
    /// <summary>
    /// Anahtar tanımlıysa ona karşılık gelen agent adını döndürür, değilse <c>null</c>.
    /// </summary>
    string? ResolveAgentName(string presentedKey);
}
