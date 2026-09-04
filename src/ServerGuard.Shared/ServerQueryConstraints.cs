namespace ServerGuard.Shared;

/// <summary>Sunucu listesi sorgusunun sınırları.</summary>
public static class ServerQueryConstraints
{
    public const int MinSinceHours = 1;

    /// <summary>30 gün; daha geniş bir tarama tablonun tamamını okumaya yaklaşır.</summary>
    public const int MaxSinceHours = 720;

    public const int DefaultSinceHours = 24;
}
