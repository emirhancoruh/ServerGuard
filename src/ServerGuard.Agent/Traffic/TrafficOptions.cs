using System.ComponentModel.DataAnnotations;
using ServerGuard.Agent.Configuration;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// IIS trafik log toplayıcısının ayarları.
/// </summary>
/// <remarks>
/// İzlenecek klasörler üç yoldan biriyle belirlenir; sırayla denenir:
/// <list type="number">
/// <item><see cref="LogRoot"/> verilirse altındaki <see cref="DirectoryPattern"/> ile eşleşen
/// tüm site klasörleri otomatik bulunur. Çok siteli sunucularda önerilen yol budur;
/// yeni bir site eklendiğinde ayar değiştirmeye gerek kalmaz.</item>
/// <item><see cref="LogDirectories"/> verilirse yalnızca o klasörler izlenir.</item>
/// <item>Hiçbiri verilmezse <see cref="LogDirectory"/> kullanılır (tek site, eski davranış).</item>
/// </list>
/// </remarks>
public sealed class TrafficOptions
{
    public const string SectionName = "Agent:Traffic";

    private const string MinInterval = "00:00:01";
    private const string MaxInterval = "00:05:00";
    private const string MinRescanInterval = "00:01:00";
    private const string MaxRescanInterval = "1.00:00:00";
    private const int LinesPerCycleLowerBound = 10;
    private const int LinesPerCycleUpperBound = 50_000;
    private const int TrackedDirectoryLowerBound = 1;
    private const int TrackedDirectoryUpperBound = 200;

    /// <summary>IIS bulunmayan makinelerde kapatılabilir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// IIS'in tüm site log klasörlerini barındıran kök dizin (örn. <c>C:\inetpub\logs\LogFiles</c>).
    /// Verilirse altındaki site klasörleri kendiliğinden bulunur.
    /// </summary>
    public string LogRoot { get; set; } = string.Empty;

    /// <summary><see cref="LogRoot"/> altında site klasörlerini eşleyen desen.</summary>
    public string DirectoryPattern { get; set; } = "W3SVC*";

    /// <summary>Açıkça izlenecek klasörler. <see cref="LogRoot"/> verilmediğinde kullanılır.</summary>
    public IReadOnlyList<string> LogDirectories { get; set; } = [];

    /// <summary>Tek siteli kurulumlar için geriye dönük uyumluluk ayarı.</summary>
    public string LogDirectory { get; set; } = @"C:\inetpub\logs\LogFiles\W3SVC1";

    /// <summary>Klasördeki log dosyalarını eşleyen desen; her klasörde en yeni dosya izlenir.</summary>
    public string FilePattern { get; set; } = "u_ex*.log";

    /// <summary>
    /// Son okunan konumların yazıldığı dosya. Göreli verilirse uygulamanın içerik köküne göre çözülür.
    /// </summary>
    public string OffsetFilePath { get; set; } = "traffic-offset.json";

    /// <summary>
    /// FileSystemWatcher olayları kaçabildiğinden düzenli aralıkla da kontrol edilir.
    /// </summary>
    [Range(typeof(TimeSpan), MinInterval, MaxInterval)]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Yeni açılan sitelerin yakalanması için klasör listesinin yeniden taranma aralığı.
    /// Yalnızca <see cref="LogRoot"/> kullanıldığında anlamlıdır.
    /// </summary>
    [Range(typeof(TimeSpan), MinRescanInterval, MaxRescanInterval)]
    public TimeSpan DirectoryRescanInterval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// İzlenecek en fazla klasör. Yanlış bir kök dizin verildiğinde yüzlerce klasör için
    /// dosya izleyici açılmasını engeller.
    /// </summary>
    [Range(TrackedDirectoryLowerBound, TrackedDirectoryUpperBound)]
    public int MaxTrackedDirectories { get; set; } = 50;

    /// <summary>Tek bir turda <b>klasör başına</b> işlenecek en fazla satır.</summary>
    [Range(LinesPerCycleLowerBound, LinesPerCycleUpperBound)]
    public int MaxLinesPerCycle { get; set; } = 2_000;

    /// <summary>Backend erişilemediğinde yerelde tutulacak en fazla kayıt sayısı.</summary>
    [Range(QueueCapacityLimits.Min, QueueCapacityLimits.Max)]
    public int QueueCapacity { get; set; } = 5_000;

    /// <summary>
    /// İlk çalıştırmada mevcut dosyanın başından mı okunsun, yoksa yalnızca yeni satırlar mı?
    /// Varsayılan olarak baştan okunur; böylece panel açılır açılmaz veri görünür.
    /// </summary>
    public bool ReadExistingFileOnFirstRun { get; set; } = true;
}
