using ServerGuard.Agent.Queue;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Transport;

/// <summary>
/// Bir kayıt türünü backend'e güvenilir biçimde ulaştırır: kuyruğa alır, sırayla gönderir,
/// backend erişilemezse kayıtları kuyrukta bekletir. Kuyruk dolarsa en eski kayıt loglanarak atılır.
/// </summary>
public sealed class BackendDispatcher<T> where T : class, IServerPayload
{
    private readonly string _route;
    private readonly int _capacity;
    private readonly IBackendSender _sender;
    private readonly ILogger<BackendDispatcher<T>> _logger;
    private readonly PendingQueue<T> _queue;

    public BackendDispatcher(
        string route,
        int queueCapacity,
        IBackendSender sender,
        ILogger<BackendDispatcher<T>> logger)
    {
        _route = route;
        _capacity = queueCapacity;
        _sender = sender;
        _logger = logger;
        _queue = new PendingQueue<T>(queueCapacity, OnDropped);
    }

    public int PendingCount => _queue.Count;

    public void Enqueue(T payload) => _queue.Enqueue(payload);

    /// <summary>
    /// Kuyruğu backend'e boşaltır. Backend erişilemez duruma gelirse kalan kayıtlar korunur
    /// ve bir sonraki çağrıda kaldığı yerden devam edilir.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _queue.TryPeek(out var payload))
        {
            var result = await _sender.SendAsync(_route, payload, cancellationToken);

            if (result == SendResult.Unavailable)
            {
                _logger.LogWarning(
                    "Backend unavailable for {Route}; {Count} record(s) kept in local queue.",
                    _route,
                    _queue.Count);
                return;
            }

            _queue.TryDequeue(out _);
        }
    }

    private void OnDropped(T dropped) =>
        _logger.LogWarning(
            "Pending queue full for {Route} (Capacity={Capacity}); oldest record dropped. " +
            "Server={ServerName} Timestamp={Timestamp}",
            _route,
            _capacity,
            dropped.ServerName,
            dropped.Timestamp);
}
