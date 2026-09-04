namespace ServerGuard.Api.Detection;

/// <summary>
/// Tek bir IP için son <c>threshold</c> başarısız giriş denemesinin zamanını tutan,
/// sabit boyutlu ve thread-safe kayan pencere.
/// </summary>
/// <remarks>
/// Yalnızca "eşik kadar denemenin hepsi pencere içinde mi?" sorusunu yanıtlamak yeterli olduğundan
/// tüm zaman damgalarını biriktirmek gerekmez: en son <c>threshold</c> tanesi saklanır.
/// Böylece IP başına bellek kullanımı sabittir ve saldırı ne kadar uzun sürerse sürsün büyümez.
/// </remarks>
public sealed class FailureWindow(int threshold)
{
    private readonly DateTimeOffset[] _attempts = new DateTimeOffset[threshold];
    private readonly Lock _gate = new();

    private int _writeIndex;
    private int _recordedCount;

    /// <summary>
    /// Yeni bir başarısız denemeyi kaydeder ve eşiğin aşılıp aşılmadığını atomik olarak döner.
    /// Eşik aşıldığında pencere sıfırlanır; böylece süren bir saldırı her denemede yeni alarm üretmez,
    /// bir sonraki alarm için eşik kadar yeni deneme gerekir.
    /// </summary>
    public bool TryRegisterFailure(DateTimeOffset occurredAt, TimeSpan window, out int attemptsInWindow)
    {
        lock (_gate)
        {
            _attempts[_writeIndex] = occurredAt;
            _writeIndex = (_writeIndex + 1) % _attempts.Length;

            if (_recordedCount < _attempts.Length)
            {
                _recordedCount++;
            }

            attemptsInWindow = CountWithin(occurredAt - window);

            if (attemptsInWindow < _attempts.Length)
            {
                return false;
            }

            Reset();
            return true;
        }
    }

    private int CountWithin(DateTimeOffset cutoff)
    {
        var count = 0;

        for (var index = 0; index < _recordedCount; index++)
        {
            if (_attempts[index] >= cutoff)
            {
                count++;
            }
        }

        return count;
    }

    private void Reset()
    {
        Array.Clear(_attempts);
        _writeIndex = 0;
        _recordedCount = 0;
    }
}
