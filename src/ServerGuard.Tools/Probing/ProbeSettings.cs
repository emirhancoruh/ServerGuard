namespace ServerGuard.Tools.Probing;

/// <summary>
/// Kontrolün nereye ve hangi kimlik bilgileriyle bağlanacağı.
/// </summary>
/// <param name="BaseAddress">API'nin kök adresi.</param>
/// <param name="UserName">Panel kullanıcısı. Verilmezse oturum gerektiren kontroller atlanır.</param>
/// <param name="Password">Panel parolası.</param>
/// <param name="IngestApiKey">Agent anahtarı. Verilmezse ingest kontrolü atlanır.</param>
/// <param name="AllowUntrustedCertificate">
/// Geliştirme sertifikasıyla test ederken gerekir. Production kontrolünde kullanılmamalıdır;
/// sertifika sorunu tam da bulunması gereken sorunlardan biridir.
/// </param>
/// <param name="Timeout">Tek bir isteğin aşamayacağı süre.</param>
public sealed record ProbeSettings(
    Uri BaseAddress,
    string? UserName,
    string? Password,
    string? IngestApiKey,
    bool AllowUntrustedCertificate,
    TimeSpan Timeout);
