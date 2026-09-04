namespace ServerGuard.Agent.Security;

/// <summary>
/// Dinlenen Windows Security kanalı olay kimlikleri.
/// </summary>
public static class WindowsSecurityEventIds
{
    public const int FailedLogin = 4625;
    public const int SuccessfulLogin = 4624;

    /// <summary>Yalnızca ilgilenilen olayları getiren XPath sorgusu; filtreleme işletim sistemi tarafında yapılır.</summary>
    public static readonly string LogonEventsXPath =
        $"*[System[(EventID={FailedLogin} or EventID={SuccessfulLogin})]]";
}
