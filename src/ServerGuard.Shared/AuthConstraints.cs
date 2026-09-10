namespace ServerGuard.Shared;

/// <summary>
/// Kimlik doğrulama sözleşmesinin Agent, Api ve web istemcisi tarafından paylaşılan sabitleri.
/// </summary>
public static class AuthConstraints
{
    /// <summary>Agent'ın ingest isteklerinde anahtarı taşıdığı header.</summary>
    public const string ApiKeyHeaderName = "X-ServerGuard-Key";

    /// <summary>
    /// SignalR WebSocket taşımasında tarayıcı özel header gönderemediğinden token
    /// bu sorgu parametresiyle iletilir. Yalnızca hub yolunda kabul edilir.
    /// </summary>
    public const string AccessTokenQueryParameter = "access_token";

    /// <summary>Anahtar ve parolalarda kabul edilen en kısa uzunluk.</summary>
    public const int MinimumSecretLength = 16;

    public const int MinimumUserNameLength = 3;
    public const int MaximumUserNameLength = 64;

    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 256;
}
