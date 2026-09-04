using System.ComponentModel.DataAnnotations;
using ServerGuard.Agent.Configuration;

namespace ServerGuard.Agent.Traffic;

public sealed class TrafficOptions
{
    public const string SectionName = "Agent:Traffic";

    private const string MinInterval = "00:00:01";
    private const string MaxInterval = "00:05:00";
    private const int LinesPerCycleLowerBound = 10;
    private const int LinesPerCycleUpperBound = 50_000;

    /// <summary>IIS bulunmayan makinelerde kapatılabilir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>IIS'in W3C log dosyalarını yazdığı klasör (tek site).</summary>
    public string LogDirectory { get; set; } = @"C:\inetpub\logs\LogFiles\W3SVC1";

    /// <summary>Klasördeki log dosyalarını eşleyen desen; en yeni dosya izlenir.</summary>
    public string FilePattern { get; set; } = "u_ex*.log";

    /// <summary>
    /// Son okunan konumun yazıldığı dosya. Göreli verilirse uygulamanın içerik köküne göre çözülür.
    /// </summary>
    public string OffsetFilePath { get; set; } = "traffic-offset.json";

    /// <summary>
    /// FileSystemWatcher olayları kaçabildiğinden düzenli aralıkla da kontrol edilir.
    /// </summary>
    [Range(typeof(TimeSpan), MinInterval, MaxInterval)]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Tek bir turda işlenecek en fazla satır; geri kalanı sonraki tura kalır.</summary>
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
