using ServerGuard.Shared;

namespace ServerGuard.Agent.Transport;

public static class DispatcherRegistration
{
    /// <summary>
    /// Bir kayıt türü için backend dispatcher'ı kaydeder. Rota ve kuyruk kapasitesi
    /// türe özel olduğundan kayıt açıkça yapılır.
    /// </summary>
    public static IServiceCollection AddBackendDispatcher<T>(
        this IServiceCollection services,
        string route,
        Func<IServiceProvider, int> queueCapacityResolver)
        where T : class, IServerPayload
    {
        services.AddSingleton(serviceProvider => new BackendDispatcher<T>(
            route,
            queueCapacityResolver(serviceProvider),
            serviceProvider.GetRequiredService<IBackendSender>(),
            serviceProvider.GetRequiredService<ILogger<BackendDispatcher<T>>>()));

        return services;
    }
}
