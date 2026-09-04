namespace ServerGuard.Shared;

/// <summary>
/// Alarm alanlarının ortak sınırları.
/// </summary>
public static class AlertConstraints
{
    public const int DescriptionMaxLength = 512;

    /// <summary>Enum adlarının sığacağı uzunluk; sayı yerine metin saklamak sorguları okunur kılar.</summary>
    public const int EnumNameMaxLength = 32;
}
