namespace ServerGuard.Agent.Traffic;

/// <summary>
/// W3C Extended Log Format alan adları. IIS bu adları dosyanın başındaki
/// "#Fields:" satırında bildirir; sıraları yapılandırmaya göre değişebilir.
/// </summary>
public static class W3CFields
{
    public const string Date = "date";
    public const string Time = "time";
    public const string ClientIp = "c-ip";
    public const string UriStem = "cs-uri-stem";
    public const string Status = "sc-status";
    public const string TimeTaken = "time-taken";

    /// <summary>Değeri olmayan alanlar için IIS'in yazdığı işaret.</summary>
    public const string NotApplicable = "-";
}
