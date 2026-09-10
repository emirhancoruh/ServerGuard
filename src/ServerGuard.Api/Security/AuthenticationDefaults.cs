namespace ServerGuard.Api.Security;

/// <summary>Kimlik doğrulama şeması, politika ve claim adları. Magic string tek yerde.</summary>
public static class AuthenticationDefaults
{
    /// <summary>Agent'ların kullandığı API anahtarı şeması.</summary>
    public const string ApiKeyScheme = "ApiKey";

    /// <summary>Panelin kullandığı JWT şeması.</summary>
    public const string PanelScheme = "PanelBearer";

    /// <summary>Agent kimliğinin taşındığı claim; log ve denetim için kullanılır.</summary>
    public const string AgentNameClaimType = "serverguard:agent";
}

/// <summary>Endpoint'lerin bağlandığı yetkilendirme politikaları.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Agent'ın veri yazabildiği endpoint'ler (metrik, güvenlik olayı, trafik).</summary>
    public const string Ingest = "Ingest";

    /// <summary>Panelin veri okuduğu endpoint'ler ve SignalR hub'ı.</summary>
    public const string Panel = "Panel";
}
