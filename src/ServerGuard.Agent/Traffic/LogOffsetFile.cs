namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Konum dosyasının içeriği: izlenen her klasör için okuma konumu.
/// </summary>
/// <remarks>
/// Anahtar, klasörün tam yoludur. Windows'ta yollar büyük/küçük harf duyarsız olduğundan
/// karşılaştırma da öyle yapılır; aksi halde ayardaki yazım değiştiğinde aynı klasör
/// ikinci kez baştan okunurdu.
/// </remarks>
public sealed class LogOffsetFile
{
    public Dictionary<string, LogOffset> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
