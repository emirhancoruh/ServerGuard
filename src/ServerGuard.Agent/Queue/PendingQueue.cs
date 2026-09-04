using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace ServerGuard.Agent.Queue;

/// <summary>
/// Gönderilemeyen kayıtları sınırlı kapasiteli bir kuyrukta tutar.
/// Kapasite dolunca en eski kayıt atılır ve <paramref name="onDropped"/> ile bildirilir;
/// veri asla sessizce kaybolmaz.
/// </summary>
public sealed class PendingQueue<T>(int capacity, Action<T> onDropped)
    where T : class
{
    private readonly Channel<T> _channel = Channel.CreateBounded(
        new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        },
        onDropped);

    public int Count => _channel.Reader.Count;

    public void Enqueue(T item) => _channel.Writer.TryWrite(item);

    public bool TryPeek([NotNullWhen(true)] out T? item) => _channel.Reader.TryPeek(out item);

    public bool TryDequeue([NotNullWhen(true)] out T? item) => _channel.Reader.TryRead(out item);
}
