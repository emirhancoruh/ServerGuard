\*\*Genel prensipler\*\*

\- SOLID'e tam uyum: her sınıf tek sorumluluk (SRP), somut sınıflara değil soyutlamalara bağımlılık (DIP), genişlemeye açık/değişikliğe kapalı tasarım (OCP).

\- Clean Code: anlamlı isimlendirme, kısa ve tek işi yapan metotlar, magic number/string yok (configuration/constants'a taşı), gereksiz yorum yerine kendini açıklayan kod.

\- Nullable reference types tüm projelerde açık (`<Nullable>enable</Nullable>`).

\- DTO'lar `record` olarak tanımlanır (immutability, value equality).

\- Modern C#/.NET (C# 12/13, .NET 9/10) syntax'ı: primary constructor, collection expressions, pattern matching.



\*\*Stabilite ve hata yönetimi (zorunlu)\*\*

\- Global exception handling: API'de `IExceptionHandler` ile merkezi hata yakalama — hiçbir endpoint unhandled exception ile stack trace sızdırmasın, kullanıcıya güvenli kısa mesaj, detay sadece log'a.

\- Her async metot `CancellationToken` alır ve aşağı taşır; `BackgroundService`'lerde `stoppingToken` tüm bekleme noktalarına (`Task.Delay` dahil) geçirilir.

\- Agent → Backend HTTP çağrılarında `Microsoft.Extensions.Http.Resilience` ile retry + timeout + circuit breaker.

\- `EventLogWatcher`, `FileSystemWatcher`, `PerformanceCounter` gibi kaynaklar `IDisposable`/`using` ile düzgün serbest bırakılır — memory leak yok.

\- `BackgroundService` singleton'dır; içinde `DbContext` gibi scoped servis kullanılacaksa `IServiceScopeFactory` ile her iterasyonda yeni scope açılır.

\- `IMemoryCache`/sliding window sayaçlarında mutlaka expiration/eviction policy tanımlı olur — sınırsız büyüyen koleksiyon yok.

\- Blocking call yasak: `.Result`, `.Wait()` yok — async all the way.

\- Dış çağrılarda süresiz bekleme yok, her operasyonun makul bir timeout'u var.



\*\*Veri güvenliği / sızıntı önleme\*\*

\- Secret'lar (API key, connection string, bot token) koda gömülmez; geliştirmede `dotnet user-secrets`, production'da environment variable/secret store.

\- Log'lara şifre, API key, tam token yazılmaz.

\- Dışarıdan gelen her veri (agent'tan, API'den) FluentValidation/DataAnnotations ile doğrulanmadan işlenmez.

\- EF Core'da parametreli sorgular; hiçbir yerde raw SQL string concatenation yok. `EnableRetryOnFailure` ile geçici bağlantı kopmalarına dayanıklılık.



\*\*Gözlemlenebilirlik\*\*

\- Structured logging (Serilog önerilir), düz string yerine yapılandırılmış alanlar (ServerName, CorrelationId).

\- `/health` endpoint'i (ASP.NET Core Health Checks).



\---



