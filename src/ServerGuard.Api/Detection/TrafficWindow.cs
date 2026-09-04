namespace ServerGuard.Api.Detection;

/// <summary>
/// Tek bir IP için pencere içindeki istek sayısını tutan, sabit bellekli ve thread-safe sayaç.
/// </summary>
/// <remarks>
/// Brute-force tespitindeki <see cref="FailureWindow"/> her denemenin zamanını saklar; eşik küçük
/// (5) olduğu için bu ucuzdur. Trafikte eşik yüzlerce isteğe çıkar ve her isteğin zamanını saklamak
/// IP başına kilobaytlarca yer tutardı. Bu yüzden pencere dilimlere bölünür ve her dilimde yalnızca
/// bir sayaç tutulur: bellek dilim sayısı kadar sabittir, sayım dilim çözünürlüğü kadar hassastır.
/// </remarks>
public sealed class TrafficWindow
{
    private readonly long[] _slotEpochs;
    private readonly int[] _slotCounts;
    private readonly long _bucketTicks;
    private readonly Lock _gate = new();

    private long _newestEpoch = long.MinValue;
    private DateTimeOffset _lastSeenAt;
    private DateTimeOffset _lastAlertAt = DateTimeOffset.MinValue;

    public TrafficWindow(TimeSpan window, int bucketCount, DateTimeOffset createdAt)
    {
        // Dilim süresi hiçbir zaman sıfır olmamalı; aksi halde dilim hesabı bölme hatası verir.
        _bucketTicks = Math.Max(1, window.Ticks / bucketCount);
        _slotEpochs = new long[bucketCount];
        _slotCounts = new int[bucketCount];
        Array.Fill(_slotEpochs, long.MinValue);
        _lastSeenAt = createdAt;
    }

    public DateTimeOffset LastSeenAt
    {
        get
        {
            lock (_gate)
            {
                return _lastSeenAt;
            }
        }
    }

    /// <summary>
    /// İsteği sayar ve eşiğin aşılıp aşılmadığını atomik olarak döner.
    /// Alarm üretildikten sonra <paramref name="alertCooldown"/> süresince aynı IP için
    /// yeniden alarm üretilmez; sayım kesintisiz sürer.
    /// </summary>
    /// <param name="occurredAt">
    /// İsteğin gerçekte yapıldığı an (IIS log damgası). Sayım penceresi bu zamana göre işler:
    /// IIS logları toplu yazdığından bir dakikalık trafik tek seferde gelebilir; geliş anına göre
    /// saymak bu isteklerin hepsini aynı ana yığar ve yanlış alarm üretirdi.
    /// </param>
    /// <param name="observedAt">
    /// Kaydın işlendiği an (duvar saati). Yalnızca "bu IP en son ne zaman görüldü" bilgisi için
    /// kullanılır; boşta kalan sayaçların temizlenmesi buna dayanır. Geçmişe dönük log okunurken
    /// olay zamanı çok eski olabilir, ama sayaç o an kullanılmaktadır.
    /// </param>
    public bool TryRegisterRequest(
        DateTimeOffset occurredAt,
        DateTimeOffset observedAt,
        int requestThreshold,
        TimeSpan alertCooldown,
        out int requestsInWindow)
    {
        lock (_gate)
        {
            if (observedAt > _lastSeenAt)
            {
                _lastSeenAt = observedAt;
            }

            Count(occurredAt);
            requestsInWindow = SumWindow();

            if (requestsInWindow <= requestThreshold)
            {
                return false;
            }

            if (occurredAt - _lastAlertAt < alertCooldown)
            {
                return false;
            }

            _lastAlertAt = occurredAt;
            return true;
        }
    }

    private void Count(DateTimeOffset occurredAt)
    {
        var epoch = occurredAt.UtcTicks / _bucketTicks;
        var slot = (int)Math.Abs(epoch % _slotEpochs.Length);

        if (epoch > _newestEpoch)
        {
            _newestEpoch = epoch;
        }

        if (_slotEpochs[slot] == epoch)
        {
            _slotCounts[slot]++;
            return;
        }

        if (epoch > _slotEpochs[slot])
        {
            // Dilim devredildi: eski sayım düşer, yenisi başlar.
            _slotEpochs[slot] = epoch;
            _slotCounts[slot] = 1;
            return;
        }

        // IIS logları toplu yazıldığından kayıtlar sırasız gelebilir. Dilimi çoktan devredilmiş
        // bir kayıt sayılmaz; sayılsaydı güncel dilimin sayacı bozulurdu.
    }

    private int SumWindow()
    {
        var total = 0;

        for (var index = 0; index < _slotEpochs.Length; index++)
        {
            var age = _newestEpoch - _slotEpochs[index];

            if (age >= 0 && age < _slotEpochs.Length)
            {
                total += _slotCounts[index];
            }
        }

        return total;
    }
}
