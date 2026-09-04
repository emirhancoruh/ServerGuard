namespace ServerGuard.Agent.Metrics;

/// <summary>
/// İşletim sisteminden anlık CPU/RAM kullanımını okur. Uygulama kapanırken DI container tarafından dispose edilir.
/// </summary>
public interface ISystemMetricsReader : IDisposable
{
    SystemMetricSnapshot Read();
}
