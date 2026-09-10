# ServerGuard Geliştirme Günlüğü

Her prompt sonunda ne yapıldığı, neden yapıldığı ve hangi kavramların öğrenildiği burada tutulur.

---

## Prompt 1 — Solution iskeleti (2026-09-02)

### Ne istendi
ServerGuard adında bir .NET solution; içinde Agent (Worker Service), Api (Web API) ve Shared (Class Library) projeleri. Shared içinde üç DTO record'u. README'de mimari özet.

### Ne yapıldı
- `ServerGuard.sln` ve `src/` altında üç proje oluşturuldu (.NET 9). Tüm projelerde `<Nullable>enable</Nullable>` açık.
- Referanslar kuruldu: `Agent → Shared`, `Api → Shared`. Agent ile Api birbirini görmez.
- `ServerGuard.Shared` içinde `sealed record` olarak `ServerMetricDto`, `SecurityEventDto`, `TrafficLogDto` ve `SecurityEventType` enum'ı tanımlandı. Zaman alanları için `DateTimeOffset` seçildi (farklı zaman dilimindeki sunucular karışmasın).
- `ApiRoutes` sabit sınıfı Shared'a kondu: endpoint yolları tek yerden yönetilir, magic string yok.
- Api'de şablondan gelen weather örneği silindi. `/health` endpoint'i ve `GlobalExceptionHandler` (`IExceptionHandler`) eklendi.
- Agent'ta `Worker` sınıfı `PeriodicTimer` ile çalışıyor; aralık `appsettings.json` → `Agent:CollectionInterval` üzerinden `AgentOptions` ile okunuyor. `stoppingToken` bekleme noktasına geçiriliyor.
- `README.md` ve `.gitignore` eklendi.

### Doğrulama
- `dotnet build`: 0 uyarı, 0 hata.
- `GET /health` → 200 "Healthy".

### Öğrenilen kavramlar
- **Solution / çoklu proje**: her projenin tek bir sorumluluğu ve kendi deploy birimi var.
- **Shared contract**: Agent ve Api'nin aynı veri şeklini konuşmasını sağlayan ortak kütüphane.
- **DTO ve `record`**: immutability ve value equality; veri taşıyıcıda davranış yok.
- **Bağımlılık yönü**: `Agent → Shared ← Api`. Agent asla Api'ye referans vermez.
- **Options pattern**: sayısal ayarların koda gömülmek yerine konfigürasyondan okunması.

---

## Prompt 2 — EF Core + MSSQL, POST /api/metrics (2026-09-02)

### Ne istendi
Api'de EF Core ile MSSQL bağlantısı (`EnableRetryOnFailure`), `ServerMetric` entity'si, FluentValidation ile doğrulanan `POST /api/metrics`, repository pattern, her yerde `CancellationToken`, migration oluşturup uygulama, Serilog'a yazan global exception handler. Hata 400, başarı 201.

### Ne yapıldı
**Paketler (Api):** `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (9.0.19), `FluentValidation.DependencyInjectionExtensions` (12.1.1), `Serilog.AspNetCore` (10.0.0).

**Shared:**
- `ApiRoutes.Metrics = "/api/metrics"` eklendi.
- `MetricConstraints` eklendi: `MinPercent`, `MaxPercent`, `ServerNameMaxLength`. Validator ve DB kolon uzunluğu aynı sabiti kullanır; Agent de ileride buradan okuyacak.

**Api — veri katmanı (`Data/`):**
- `Entities/ServerMetric`: DTO alanları + `Id` (long) + `CreatedAt`. `required init` özellikleriyle eksik alan derleme zamanında yakalanır.
- `Configurations/ServerMetricConfiguration`: `IEntityTypeConfiguration` ile max length ve `(ServerName, Timestamp)` index'i. Entity sınıfı EF attribute'larından temiz kalır.
- `ServerGuardDbContext`: `ApplyConfigurationsFromAssembly` ile tüm konfigürasyonları otomatik yükler.
- `PersistenceExtensions.AddPersistence`: connection string'i okur (yoksa açıklayıcı hata ile fail-fast), `UseSqlServer` + `EnableRetryOnFailure(5 deneme, 30 sn max)`, repository kaydı, `/health`'e DbContext health check.
- `Migrations/20260902080745_InitialCreate` oluşturuldu ve `ServerGuard` veritabanına uygulandı.

**Api — uygulama katmanı:**
- `Repositories/IServerMetricRepository` + `ServerMetricRepository`: controller DbContext'i görmez, yalnızca soyutlamaya bağımlıdır (DIP).
- `Validation/ServerMetricDtoValidator`: ServerName boş olamaz ve max uzunluk; CPU ve RAM 0-100 arası.
- `Mapping/ServerMetricMapper.ToEntity`: DTO → entity dönüşümü tek yerde.
- `Contracts/CreatedMetricResponse`: 201 gövdesinde dönen `{ id }`.
- `Controllers/MetricsController`: validator → geçersizse `ValidationProblem` (400); geçerliyse `TimeProvider` ile `CreatedAt` set edilip repository'ye yazılır, 201 döner. Tüm async zincir `CancellationToken` taşır.

**Loglama ve hata yönetimi:**
- Serilog `appsettings.json`'dan yapılandırılır (Console sink, `FromLogContext`), `UseSerilogRequestLogging` ile her istek tek satır structured log.
- `GlobalExceptionHandler` `ILogger` üzerinden Serilog'a `TraceId` ve `Path` alanlarıyla yazar; kullanıcıya yalnızca kısa `ProblemDetails` döner, stack trace sızmaz.

**Secret yönetimi:**
- Connection string koda/appsettings'e yazılmadı. `dotnet user-secrets` ile `ConnectionStrings:ServerGuard` olarak saklandı. `appsettings.json`'da boş placeholder var; yoksa uygulama açılışta anlamlı hata verir.

### Doğrulama
| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| `dotnet ef database update` | `ServerGuard` DB ve `ServerMetrics` tablosu oluştu |
| `POST /api/metrics` geçerli gövde | 201, `{"id":1}` |
| `POST /api/metrics` boş ServerName + CPU 150 | 400, alan bazlı hata listesi |
| `GET /health` (DB check dahil) | 200 Healthy |
| SQL sorgusu | 1 satır, `CreatedAt` UTC olarak yazılmış |

### Öğrenilen kavramlar
- **Entity vs DTO**: DTO taşıma sözleşmesi, entity kalıcılık modeli. `Id` ve `CreatedAt` gibi DB'ye özel alanlar DTO'ya sızmaz.
- **Repository pattern**: veri erişimi soyutlanır; controller "nasıl saklandığını" bilmez, test edilebilirlik artar.
- **Connection resiliency**: `EnableRetryOnFailure` geçici ağ/DB kopmalarında otomatik yeniden dener.
- **Migration**: şema değişiklikleri kod olarak versiyonlanır, `database update` ile uygulanır.
- **FluentValidation**: kuralları ayrı sınıfta, akıcı sözdizimiyle; hata mesajları alan bazlı 400 ProblemDetails olarak döner.
- **Structured logging**: `{TraceId}` gibi alanlar düz metin değil, sorgulanabilir property olarak loglanır.
- **`TimeProvider`**: `DateTime.UtcNow` yerine enjekte edilen soyutlama; testte zaman sabitlenebilir.
- **Fail-fast konfigürasyon**: eksik secret ilk istekte değil, uygulama açılırken yakalanır.

### Notlar / dikkat
- `dotnet ef` komutları user-secrets'ı okuyabilsin diye `ASPNETCORE_ENVIRONMENT=Development` ile çalıştırılmalı.
- Yerel SQL Server'ın sertifikası self-signed olduğundan connection string'de `TrustServerCertificate=True` var. Production'da gerçek sertifika kullanılmalı.

---

## Prompt 3 — Agent: metrik toplama, resilience, yerel kuyruk, Windows Service (2026-09-03)

### Ne istendi
`MetricCollectorWorker : BackgroundService`: her 10 saniyede PerformanceCounter ile CPU/RAM oku (IDisposable doğru yönetilsin), `ServerMetricDto` oluştur, `IHttpClientFactory` named client + `Microsoft.Extensions.Http.Resilience` (retry + timeout + circuit breaker) ile POST et. Backend'e ulaşılamazsa veriyi kaybetme: sınırlı kuyrukta biriktir, bağlantı gelince gönder, sınır aşılırsa en eskiyi at ve logla. Sunucu adı ve backend URL appsettings'ten. Windows Service olarak çalışsın.

### Ne yapıldı
**Proje ayarları:** Agent `net9.0-windows` hedefine alındı (PerformanceCounter ve Windows Service yalnızca Windows'ta çalışır; platform analizörü uyarıları böylece kalkar). Paketler: `Microsoft.Extensions.Http.Resilience` 9.10.0, `Microsoft.Extensions.Hosting.WindowsServices` 9.0.19, `System.Diagnostics.PerformanceCounter` 9.0.19, `Microsoft.Extensions.Options.DataAnnotations` 9.0.19. Eski `Worker.cs` silindi.

**Configuration/AgentOptions:** `ServerName` (boşsa `Environment.MachineName`), `BackendBaseUrl` (`[Required]`), `CollectionInterval` (1 sn - 1 saat), `PendingQueueCapacity` (1 - 100.000). `ValidateDataAnnotations().ValidateOnStart()` ile hatalı config uygulamayı açılışta düşürür.

**Metrics/:**
- `ISystemMetricsReader : IDisposable` + `SystemMetricSnapshot` record.
- `PerformanceCounterMetricsReader`: `Processor\% Processor Time\_Total` ve `Memory\Available Bytes` sayaçları. RAM yüzdesi fiziksel bellekten hesaplanır (`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`). CPU sayacı ilk okumada 0 döndüğü için constructor'da ısındırılır. Singleton kayıtlı; host kapanırken DI container `Dispose` eder.

**Queue/PendingMetricQueue:** `System.Threading.Channels` bounded channel, `FullMode = DropOldest`. Kapasite dolunca `itemDropped` callback'i en eski kaydı `Warning` seviyesinde loglar (sunucu adı + timestamp). `TryPeek` / `TryDequeue` ile "gönder, başarılıysa çıkar" akışı; başarısızsa kayıt kuyrukta kalır.

**Transport/:**
- `BackendHttpClient.AddBackendHttpClient()`: named client `"Backend"`, `BaseAddress` options'tan. `AddStandardResilienceHandler` ile retry (3 deneme, 2 sn üstel backoff + jitter), attempt timeout 10 sn, toplam timeout 60 sn, circuit breaker (30 sn örnekleme, %50 hata oranı, min 5 istek, 15 sn açık kalma). Tüm sayılar sabit olarak sınıfın başında.
- `IMetricSender` / `HttpMetricSender`: sonucu üçe ayırır. `Sent` (2xx), `Rejected` (4xx: veri geçersiz, tekrar denemek anlamsız, hata loglanır ve kayıt atılır), `Unavailable` (5xx, ağ hatası, `BrokenCircuitException`, `TimeoutRejectedException`: kayıt kuyrukta kalır).

**MetricCollectorWorker:** `PeriodicTimer(interval, TimeProvider)` ile tick; `WaitForNextTickAsync(stoppingToken)` tek bekleme noktası ve token alır. Her tick: oku → DTO → kuyruğa ekle → kuyruğu boşalt. Döngü içi hatalar yakalanıp loglanır, servis düşmez (.NET 8+ `BackgroundService` unhandled exception'da host'u kapatır). Kapanışta kuyrukta kaç kayıt kaldığı loglanır.

**Program.cs:** `AddWindowsService(ServiceName = "ServerGuard.Agent")`. `Host.CreateApplicationBuilder` ile `IHostBuilder.UseWindowsService()`'in karşılığı budur; içerik kökünü ve EventLog provider'ını da ayarlar.

### Doğrulama
| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| PerformanceCounter Türkçe Windows'ta | İngilizce sayaç adlarıyla çalıştı, CPU %6-20, RAM %80 okundu |
| API kapalı, Agent açık (45 sn) | 3 retry → `HttpRequestException` → "1 metric kept in local queue"; circuit breaker açıldı → `BrokenCircuitException` ile hızlı hata → kuyruk 2, 3 |
| API sonradan açıldı | Kuyruktaki kayıtlar sırayla gönderildi, DB'ye 5 yeni satır yazıldı (Id 3-7) |
| Config doğrulama | `BackendBaseUrl` yoksa uygulama açılışta hata verir |

### Öğrenilen kavramlar
- **BackgroundService yaşam döngüsü**: `stoppingToken` her bekleme noktasına geçer; `OperationCanceledException` normal kapanıştır.
- **IDisposable ve DI**: singleton kaydedilen `IDisposable` nesneleri container kapanışta serbest bırakır, elle `Dispose` çağırmaya gerek yoktur.
- **Named HttpClient + IHttpClientFactory**: socket tükenmesini önler, policy'ler client adına bağlanır.
- **Resilience pipeline sırası**: TotalTimeout → Retry → CircuitBreaker → AttemptTimeout. Circuit açıkken istekler ağa çıkmadan hızlı reddedilir, backend'e yük binmez.
- **Bounded queue / backpressure**: sınırsız bellek büyümesi yerine bilinçli, loglanan veri kaybı.
- **Geçici vs kalıcı hata ayrımı**: 4xx tekrar denenmez, 5xx/ağ hatası denenir. Bu ayrım olmadan geçersiz bir kayıt kuyruğu sonsuza dek tıkar.
- **Options validation**: yanlış config'in ilk istekte değil açılışta patlaması.

### Notlar / dikkat
- Windows Service kurulumu için `dotnet publish` + `sc.exe create` adımları README'de.
- Kuyruk bellek içidir; Agent yeniden başlarsa bekleyen kayıtlar kaybolur. Kalıcı (disk) kuyruk ileride ihtiyaç olursa eklenir.
- Polly retry logları `Warning` seviyesinde görünür; production'da `Logging:LogLevel:Polly` = `Error` yapılabilir.

---

## Prompt 4 — SignalR canlı yayın ve Angular dashboard (2026-09-03)

### Ne istendi
Api'ye `MonitoringHub : Hub`; `/api/metrics` her kayıtta `ReceiveMetric` event'i ile yayın yapsın. Bir istemciye gönderim hata verirse izole edilsin, diğerlerini engellemesin. Angular'da `@microsoft/signalr` ile `withAutomaticReconnect()` kullanan servis, veriler Observable ile component'e aksın. Dashboard'da her sunucu için CPU ve RAM DevExtreme gauge ile canlı gösterilsin.

### Ne yapıldı

**Backend (ServerGuard.Api):**
- `Hubs/IMonitoringClient`: sunucudan istemciye giden event sözleşmesi (`ReceiveMetric`). `Hub<IMonitoringClient>` sayesinde event adı magic string değil, derleyici kontrollü metot.
- `Hubs/MonitoringHub`: bağlantı açılış/kapanışını `Debug` seviyesinde loglar.
- `Realtime/IMetricBroadcaster` + `SignalRMetricBroadcaster`: yayın soyutlaması. Controller `IHubContext`'i doğrudan görmez (DIP).
- `Realtime/RealtimeExtensions`: `AddRealtime()` ve `MapRealtimeHubs()` ile Program.cs sade kalır. Hub yolu `ApiRoutes.MonitoringHub` sabitinden.
- `Configuration/WebClientCors`: tarayıcıdan erişim için CORS. İzinli origin'ler `Cors:AllowedOrigins` bölümünden okunur, koda gömülmez. SignalR için `AllowCredentials()` şart.
- `MetricsController`: kayıt başarılı olduktan sonra `BroadcastAsync` çağrılır.
- `ApiRoutes`'a `MonitoringHub = "/hubs/monitoring"` eklendi.

**Hata izolasyonu:** İki katmanlı. SignalR altyapısı `Clients.All` çağrısında her bağlantıya bağımsız gönderim yapar; kopmuş bir istemci diğerlerinin yayınını engellemez ve o bağlantı sessizce düşürülür. Ek olarak `SignalRMetricBroadcaster` içindeki try/catch, hub altyapısında beklenmedik bir hata çıkarsa bunun HTTP isteğini düşürmesini engeller. Metrik zaten veritabanına yazılmıştır, yayın hatası kaydı geçersiz kılmaz; durum `Warning` olarak loglanır.

**Frontend (ServerGuard.Web, yeni Angular 21 projesi):**
- `core/api-routes.ts`: hub yolu ve event adı tek yerde (backend `ApiRoutes` karşılığı).
- `core/models/`: `ServerMetric` (DTO karşılığı), `ConnectionState`, `ServerStatus` + `toServerStatus` dönüştürücü.
- `core/services/monitoring-hub.service.ts`:
  - `withAutomaticReconnect([0, 2s, 5s, 10s, 30s])` ile kopan bağlantı sessizce yeniden denenir.
  - Gelen metrikler `Subject` üzerinden `metrics$` Observable'ı olarak yayınlanır.
  - Bağlantı durumu `signal` olarak dışa verilir; şablonda rozet olarak gösterilir.
  - **Önemli ayrım:** `withAutomaticReconnect` yalnızca *kurulmuş* bir bağlantı koptuğunda çalışır, ilk `start()` başarısızlığını kapsamaz. Bu yüzden ilk bağlantı için 5 sn'de bir tekrar deneyen ayrı bir mekanizma yazıldı. Böylece backend Angular'dan sonra açılsa bile UI kendini toparlar.
- `dashboard/`: `metrics$` aboneliği `takeUntilDestroyed` ile otomatik temizlenir. Veri sunucu adına göre `Map`'te tutulur, aynı sunucudan gelen yeni metrik öncekinin üzerine yazar. `computed` ile alfabetik sıralı liste üretilir.
- Gauge'lar `dx-circular-gauge`: 0-100 ölçek, üç renk bölgesi (0-70 yeşil, 70-90 turuncu, 90-100 kırmızı), 400 ms animasyon ile yumuşak geçiş. Eşikler component'te sabit olarak tanımlı.
- `angular.json`: DevExtreme teması (`dx.light.css`), production için environment dosya değişimi, bundle bütçesi DevExtreme'e göre yükseltildi, CommonJS uyarıları için `allowedCommonJsDependencies`.

### Doğrulama
| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| `ng build` | Başarılı, 1.92 MB bundle |
| Hub negotiate endpoint | 200 |
| Agent → Api → tarayıcı | Gauge'lar 10 sn'de bir canlı güncellendi (CPU 5.1 → 6.9 → 10.7) |
| Gauge iğne konumu | CPU %12.4 → -116°, RAM %95.9 → +118°; değerle orantılı |
| **API kapatıldı** | Rozet "Yeniden bağlanıyor" oldu, son veriler ekranda kaldı, UI donmadı |
| **API geri açıldı** | Sayfa yenilenmeden "Canlı"ya döndü, yeni veri aktı |
| İki tarayıcı sekmesi | İkisi de aynı anda aynı veriyi aldı (13:39:09) |
| İkinci sunucu (`db-01`) POST | Yeni kart anında eklendi, alfabetik sıralandı |

### Öğrenilen kavramlar
- **SignalR ve WebSocket**: sunucunun istemciyi beklemeden veri itmesi (push). Polling'e göre hem gecikme hem yük avantajı.
- **Strongly-typed Hub**: `Hub<TClient>` ile event adları derleyici kontrolünde.
- **`IHubContext`**: hub dışından (controller, servis) yayın yapmanın yolu.
- **CORS ve credentials**: tarayıcı güvenlik modeli; SignalR için origin'in açıkça izinli olması gerekir, joker karakter yetmez.
- **Observable vs Signal**: akış (zaman içinde gelen olaylar) için Observable, anlık durum (bağlantı durumu, sunucu listesi) için signal.
- **`takeUntilDestroyed`**: component yok olunca aboneliğin otomatik kapanması; memory leak önlenir.
- **Optimistic UI**: bağlantı koptuğunda ekranı boşaltmak yerine son bilinen veriyi göstermek, durumu rozetle belirtmek.

### Notlar / dikkat
- **DevExtreme lisansı ticaridir.** Şu an deneme sürümü çalışıyor ve sayfanın üstünde turuncu bir lisans bandı görünüyor. Üretim için lisans satın alınmalı; alınmayacaksa gauge'lar ücretsiz bir kütüphaneyle (ör. ngx-charts, ECharts) değiştirilmeli.
- Node 22.14 kurulu olduğu için Angular 22 yerine Angular 21 kullanıldı (CLI 22 en az Node 22.22 istiyor).
- Dashboard sunucu listesini yalnızca canlı yayından kurar; sayfa yenilenince geçmiş veri gelmez. Başlangıç verisi için `GET /api/metrics/latest` gibi bir endpoint sonraki adımda eklenebilir.
- Tarayıcı koyu tema kullandığında sayfa okunmuyordu; `styles.scss` içinde `color-scheme: light` ve açık arka planla sabitlendi.

---

## Prompt 5 — Windows güvenlik olayları: başarısız/başarılı giriş tespiti (2026-09-04)

### Ne istendi
Agent'a `SecurityEventWorker : BackgroundService`. `EventLogWatcher` ile Security kanalını dinle, yalnızca Event ID 4625 ve 4624'ü yakala, watcher'ı `IDisposable` olarak doğru yönet. Olay XML'inden kaynak IP ve kullanıcı adını çıkar. Bir olayın ayrıştırılması başarısız olursa yalnızca o olayı logla ve atla, watcher durmasın. Olay işleme bloklamasın. `SecurityEventDto` oluşturup backend'e gönder. Gereken minimum yetkiyi (Event Log Readers) README'de belirt, Administrator zorunlu kılma.

### Ne yapıldı

**Taşıma katmanı genelleştirildi (refactor).** Artık iki farklı kayıt türü aynı yolu kullanıyor. Kopyala-yapıştır yerine tek bir mekanizma:
- `PendingMetricQueue` → `PendingQueue<T>` (generic bounded kuyruk).
- `IMetricSender`/`HttpMetricSender` → `IBackendSender`/`HttpBackendSender` (rota parametre olarak alınır).
- Yeni `BackendDispatcher<T>`: "bir kayıt türünü backend'e güvenilir ulaştır" sorumluluğunu tek yerde toplar (kuyruğa al, sırayla gönder, erişilemezse beklet, kuyruk dolarsa en eskiyi loglayarak at).
- `Shared/IServerPayload`: `ServerName` + `Timestamp`. Dispatcher, kaydın türünü bilmeden anlamlı log yazabiliyor. Üç DTO da bu arayüzü uyguluyor.

**Konfigürasyon alt bölümlere ayrıldı.** `Agent:Metrics` ve `Agent:SecurityEvents` ayrı sınıflara bağlanıyor. Her worker yalnızca kendi ayarını görüyor (ISP). `ValidateDataAnnotations()` iç içe nesnelere inmediği için her bölüm ayrı ayrı bağlanıp doğrulanıyor — `AddValidatedOptions` yardımcısı bu tekrarı önlüyor.

**`Security/WindowsSecurityEventIds`:** 4625/4624 sabitleri ve XPath sorgusu. Filtreleme işletim sistemi tarafında yapılıyor, ilgisiz olaylar sürece hiç gelmiyor.

**`Security/SecurityEventParser`:** Olay XML'ini `SecurityEventDto`'ya çevirir. Namespace'e duyarlı `XDocument` ayrıştırma. `TryParse` deseni: hiçbir durumda exception fırlatmaz, `false` döner ve sebebi loglar. Normalleştirmeler: IP `-` veya boşsa `local`, kullanıcı adı boşsa `unknown`, uzun değerler sınırlara göre kırpılır, geçersiz zaman damgasında fallback kullanılır. XML'den bağımsız test edilebilsin diye `EventRecord` değil `string` alır.

**`Security/SecurityEventWorker`:**
- `EventLogWatcher` başlatılır; olaylar geldikçe `EventRecordWritten` tetiklenir.
- **Bloklamayan işleme:** handler yalnızca ayrıştırıp kuyruğa bırakır (`TryWrite`, asla beklemez). HTTP gönderimi ayrı bir `PeriodicTimer` döngüsünde yapılır. Böylece watcher thread'i ağ beklemez.
- **Hata izolasyonu:** handler'ın tamamı try/catch içinde; tek bir olayın hatası dinlemeyi durdurmaz. `EventRecord` `using` ile serbest bırakılır.
- **Kaynak yönetimi:** `StopWatcher()` event aboneliğini kaldırır, `Enabled = false` yapar ve `Dispose` eder. Hem `finally` bloğunda hem `Dispose()` override'ında çağrılır, iki kez çağrılmaya karşı güvenli.
- **Yetki hatası:** Windows erişimi reddettiğinde bu, `Enabled = true` anında değil, callback'e gelen `args.EventException` ile **asenkron** olarak bildiriliyor. Bu durumda Windows aboneliği zaten kapatıyor. Toplayıcı çalışıyormuş gibi görünmek yerine açıklayıcı bir hata yazıp temiz kapanıyor; `CancellationTokenSource` ile yalnızca kendi döngüsü sonlanıyor, agent'ın geri kalanı çalışmaya devam ediyor.

**Backend tarafı** (olayların gidebileceği bir yer olmadan iş eksik kalırdı): `SecurityEvent` entity, `SecurityEventConfiguration`, repository, `SecurityEventDtoValidator`, `POST /api/security-events` ve `AddSecurityEvents` migration'ı. Entity'de enum **metin olarak** saklanıyor (`HasConversion<string>`), sorgular okunur kalıyor. Index `(ServerName, EventType, SourceIp, Timestamp)` — brute-force sorgusunun tam olarak ihtiyaç duyduğu sıra.

**Yayın katmanı:** `IMetricBroadcaster` → `IMonitoringBroadcaster` (iki metot), hub'a `ReceiveSecurityEvent` eklendi.

**Yol boyunca bulunan hata:** Enum'lar JSON'da varsayılan olarak **sayı** serileşiyordu. `"eventType": "FailedLogin"` gönderimi 400 dönüyor, panele `0`/`1` gidiyordu. `Shared/JsonDefaults` ile ortak sözleşme tanımlandı; Api (controller + SignalR protokolü) ve Agent aynı ayarı kullanıyor. Artık ad da sayı da kabul ediliyor, dışarı hep ad yazılıyor.

### Doğrulama

Parser 11 senaryoyla test edildi (geçici bir konsol projesiyle):

| Girdi | Sonuç |
|---|---|
| 4625, uzak IP | `FailedLogin`, ip=203.0.113.44 |
| 4624, IP = `-` | `SuccessfulLogin`, ip=`local` |
| IPv6 + `DOMAIN\user` | Scope ve ters bölü korundu |
| Kullanıcı adı boş | `unknown` |
| EventData hiç yok | Varsayılanlarla ayrıştırıldı |
| Geçersiz XML | Atlandı, uyarı yazıldı |
| System bölümü yok | Atlandı |
| EventID metin (`abc`) | Atlandı |
| İlgisiz EventID (4634) | Atlandı |
| Geçersiz zaman damgası | Fallback zaman kullanıldı |
| 400 karakterlik kullanıcı adı | 256'ya kırpıldı |

**Sonuç: 7 ayrıştırıldı, 4 atlandı, hiç exception sızmadı.**

Uçtan uca:

| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| Migration | `SecurityEvents` tablosu oluştu |
| Agent'ın gerçek gönderim yolu (parser → dispatcher → HTTP → Api) | 5 olay, hepsi 201, kuyrukta 0 kaldı |
| `POST` enum adıyla / sayıyla | İkisi de 201 |
| `POST` geçersiz enum adı / boş alanlar | 400, alan bazlı hatalar |
| DB kaydı | Enum metin olarak yazıldı |
| Brute-force sorgusu | Aynı IP'den 4 başarısız giriş gruplandı |
| **Yetkisiz makinede agent** | Açıklayıcı tek hata satırı, güvenlik toplayıcısı temiz durdu |
| **Aynı anda metrik toplama** | Kesintisiz devam etti, agent ayakta kaldı |

### Öğrenilen kavramlar
- **`EventLogWatcher` push modeli**: log'u sürekli sorgulamak (polling) yerine Windows'un olay geldiğinde haber vermesi.
- **XPath ile kaynakta filtreleme**: 4624/4625 dışındaki olaylar sürece hiç ulaşmaz; işlemci ve bellek boşa harcanmaz.
- **Callback'te bloklamama**: olay işleyicisi kütüphanenin thread'inde çalışır. Orada ağ beklemek olay birikmesine yol açar; doğru desen "hızlıca kuyruğa bırak, ayrı döngüde işle".
- **`TryParse` deseni**: beklenen başarısızlığı exception yerine `bool` ile bildirmek. Bozuk veri normal bir durumdur, istisnai değil.
- **Asenkron hata bildirimi**: bazı Windows API'lerinde yetki hatası çağrı anında değil, ilk kullanımda callback üzerinden gelir. Yalnızca constructor'ı try/catch'e almak yetmez.
- **En az ayrıcalık (least privilege)**: Administrator yerine yalnızca `Event Log Readers`. Agent ele geçirilse bile saldırganın eline geçen yetki sınırlıdır.
- **Kısmi çalışma (graceful degradation)**: bir yetenek kullanılamadığında tüm servisi düşürmek yerine yalnızca o parçayı kapatmak.
- **JSON'da enum sözleşmesi**: sayı olarak taşımak, enum'a ortadan yeni değer eklendiğinde eski kayıtların anlamını değiştirir. Ad olarak taşımak buna dayanıklıdır.
- **Generic ile tekrar önleme**: ikinci kullanım ortaya çıktığında soyutlamak — daha erken yapmak spekülasyon, daha geç yapmak kopya kod olurdu.

### Notlar / dikkat
- **4624 gürültülüdür.** Windows'ta `SYSTEM`, `LOCAL SERVICE` ve makine hesapları (`MAKINE$`) sürekli başarılı oturum açar. İstendiği gibi tüm 4624'ler toplanıyor; ileride bu hesaplar filtrelenebilir veya yalnızca uzaktan (LogonType 3/10) oturumlar tutulabilir.
- Kuyruk bellek içidir; agent yeniden başlarsa bekleyen olaylar kaybolur.
- Bu adımda yalnızca **veri toplandı**. "Şu IP'den 5 dakikada 10 başarısız giriş → alarm" kuralı henüz yok; DB index'i bu sorguya hazır.
- Panel (`ServerGuard.Web`) `ReceiveSecurityEvent` yayınını henüz dinlemiyor.
- Geliştirme makinesinde `Event Log Readers` grubu boş ve oturum yönetici değil; bu yüzden gerçek `EventLogWatcher` teslimatı test edilemedi. Yetki verildikten sonra doğrulanmalı.

---

## Prompt 6 — Brute-force tespiti ve ilk gerçek alarm (2026-09-04)

### Ne istendi
Api'de `BruteForceDetectionService`. `FailedLogin` tipindeki olaylar geldiğinde aynı `SourceIp`'den son 5 dakikada 5+ başarısız giriş varsa `SecurityAlert` üret (Severity: High, Type: BruteForceAttempt). `IMemoryCache` ile IP bazlı sliding window sayaç; her sayaca `SlidingExpiration` tanımlı, süresiz büyümesin. Sayaç artırma concurrent isteklerde thread-safe (atomik) olsun. Alarm oluşunca DB'ye kaydet ve `MonitoringHub` üzerinden `ReceiveAlert` ile yayınla.

### Ne yapıldı

**Shared:** `AlertSeverity` (Low/Medium/High/Critical), `AlertType` (BruteForceAttempt), `SecurityAlertDto` (`IServerPayload` uygular) ve `AlertConstraints`.

**`Detection/FailureWindow` — kayan pencerenin çekirdeği.**
Burada bilinçli bir tasarım kararı var. Düz bir sayaç + `SlidingExpiration`, "son 5 dakikada 5 deneme" demek **değildir**: 4 dakikada bir deneme yapan bir saldırganda giriş hiç zaman aşımına uğramaz ve sayaç 20 dakika sonra yanlışlıkla eşiğe ulaşır. Bunun yerine son `threshold` denemenin **zaman damgası** sabit boyutlu bir halka tamponda tutuluyor: eşik kadar denemenin en eskisi de pencere içindeyse alarm gerçektir. IP başına bellek sabittir, saldırı ne kadar sürerse sürsün büyümez.

Thread-safety `Lock` (.NET 9) ile sağlanıyor: kayıt ekleme, pencere içi sayım ve sıfırlama tek bir atomik blokta. Eşik aşılınca pencere sıfırlanıyor; böylece süren bir saldırı her denemede yeni alarm üretmiyor, bir sonraki alarm için eşik kadar yeni deneme gerekiyor.

**`Detection/MemoryCacheFailureWindowStore` — pencerelerin saklandığı yer.**
İki ayrı sınır var:
- `SlidingExpiration = Window` → pencere süresince sessiz kalan IP bellekten düşer.
- `SizeLimit = TrackedIpLimit` (varsayılan 50.000) → sahte IP'lerle sel yapan bir saldırgan, süre dolmadan bile belleği şişiremez; sınır aşılınca en az kullanılan pencereler tahliye edilir.

Tespit kendi `MemoryCache` örneğini kullanıyor: uygulamanın diğer cache kullanımları tespit sayaçlarını tahliye edemesin, tespit de onları tahliye edemesin.

Ayrıca `IMemoryCache`'in get-or-create adımı **atomik değildir**. Kilitsiz bırakılsaydı iki eşzamanlı istek ayrı pencere oluşturup birinin saydığı denemeler kaybolurdu. Bu yüzden oluşturma bir kilitle serileştiriliyor — kilit yalnızca ilk oluşturmada işletiliyor, her istekte değil (çift kontrollü kilitleme).

**`Detection/BruteForceDetectionService`:** `FailedLogin` dışındaki olayları ve kural kapalıysa her şeyi atlıyor. Eşik aşılınca `SecurityAlertDto` üretip repository ile kaydediyor, `Warning` seviyesinde loglayıp yayınlıyor. Tüm gövde try/catch içinde: **tespitte bir hata olsa bile olayın kaydı bozulmaz**, çünkü olay zaten veritabanına yazılmıştır.

**Ayarlar** `Detection:BruteForce` bölümünden okunuyor (`Enabled`, `FailureThreshold`, `Window`, `TrackedIpLimit`), açılışta doğrulanıyor. Hiçbir eşik koda gömülü değil.

**Kalıcılık:** `SecurityAlert` entity, konfigürasyon (enum'lar metin olarak, index `(ServerName, Timestamp)`), repository, mapper ve `AddSecurityAlerts` migration'ı.

**Yayın:** `IMonitoringClient`'a `ReceiveAlert`, broadcaster'a `BroadcastAlertAsync` eklendi.

### Doğrulama

| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| Migration | `SecurityAlerts` tablosu oluştu |
| 4 başarısız giriş (eşik altı) | Alarm yok |
| 5. deneme | 1 alarm, Severity=High, Type=BruteForceAttempt |
| Alarm sonrası 3 deneme daha | Yeni alarm yok (pencere sıfırlandı) |
| Farklı IP | Kendi sayacını tuttu, ayrı alarm |
| Farklı sunucu, aynı IP | Ayrı sayaç |
| 10 başarılı giriş | Sayılmadı, alarm yok |
| **5 deneme 2'şer dakika arayla (20 dk)** | **Alarm yok** — pencereye sığmadı |
| **Aynı IP 5 denemeyi 1 dakikaya sıkıştırdı** | Alarm üretildi |
| **100 paralel istek, eşik 5** | **Tam 20 alarm** (100/5) — hiç kayıp artırım yok |
| Gerçek SignalR istemcisi | `ReceiveAlert` alındı, `alertType`/`severity` ad olarak geldi |

Alarmın istemciye ulaşan hali:

```json
{
  "serverName": "web-01",
  "alertType": "BruteForceAttempt",
  "severity": "High",
  "sourceIp": "198.51.100.251",
  "failedAttemptCount": 5,
  "description": "198.51.100.251 adresinden son 5 dakika içinde 5 başarısız giriş denemesi yapıldı."
}
```

### Öğrenilen kavramlar
- **Sliding window vs sliding expiration**: ikisi aynı şey değil. Expiration "ne zaman unutulur"u, window "hangi aralık sayılır"ı belirler. Doğru sonuç için zaman damgası tutmak gerekir.
- **Sabit bellekli sayım**: "eşiğe ulaşıldı mı?" sorusunu yanıtlamak için tüm geçmişi tutmaya gerek yok, son N damga yeterli.
- **Atomiklik**: `Interlocked` tek bir sayı için yeterli olur; birden fazla alanı birlikte güncellemek (tampon + indeks + sayaç) kilit gerektirir.
- **Çift kontrollü kilitleme**: pahalı olan yalnızca ilk oluşturma; kilidi sıcak yola koymamak.
- **`IMemoryCache` atomik değildir**: `GetOrCreate` yarış koşuluna açıktır, eşzamanlılık gerektiren sayaçlarda dikkat ister.
- **Eviction policy**: sadece zaman aşımı yetmez; girdi sayısını da sınırlamak, saldırganın belleği tüketmesini engeller.
- **Alarm yorgunluğu (alert fatigue)**: eşik aşıldıktan sonra her olayda alarm üretmek gürültü yaratır; pencereyi sıfırlamak basit ve etkili bir bastırma yöntemidir.
- **Yan etkinin izolasyonu**: tespit, olayın kaydından sonra ve kendi try/catch'i içinde çalışır — ek yetenek, ana akışı riske atmaz.

### Notlar / dikkat
- Sayaçlar **bellek içidir**. Api yeniden başlarsa pencereler sıfırlanır ve birden fazla Api örneği çalıştırılırsa her biri kendi sayacını tutar. Ölçeklenirken paylaşılan bir sayaç (Redis) veya veritabanı sorgusu gerekir.
- Pencere, olayın **kendi zaman damgasını** kullanır (Api'ye varış zamanını değil). Agent'ın saati kaymışsa tespit etkilenir; sunucularda saat senkronizasyonu önemlidir.
- Şu an tek kural var. Yeni kurallar (port taraması, olağan dışı trafik) eklendiğinde `SecurityEventsController` içinde tek tek çağırmak yerine bir analiz zinciri kurmak gerekecek.
- Panel (`ServerGuard.Web`) `ReceiveAlert` yayınını henüz dinlemiyor; alarmlar şimdilik yalnızca veritabanında ve log'da görünüyor.

---

## Prompt 7 — Alarm listesi ve sayfalanmış sorgu (2026-09-04)

### Ne istendi
Angular panele alarm listesi component'i: `ReceiveAlert` event'lerini try/catch ile dinle (bozuk veri UI'ı çökertmesin), en yeni üstte göster, her satırda zaman/sunucu/tip/açıklama/severity (renk kodlu: High=kırmızı, Medium=turuncu). Ayrıca `GET /api/alerts?serverName=&from=&to=` endpoint'i, sayfalama destekli — sınırsız sonuç kümesi dönmesin.

### Ne yapıldı

**Backend:**
- `Shared/PaginationConstraints`: `MinPage`, `MinPageSize`, `MaxPageSize` (100), `DefaultPageSize` (25). Sınırlar tek yerde, hem doğrulayıcı hem istemci aynı sözleşmeyi görüyor.
- `Shared/Dtos/PagedResult<T>`: `Items`, `Page`, `PageSize`, `TotalCount` + hesaplanan `TotalPages` ve `HasNextPage`.
- `Contracts/AlertQuery`: `serverName`, `from`, `to`, `page`, `pageSize`. Hepsi isteğe bağlı, sayfa boyutu verilmezse varsayılan.
- `AlertQueryValidator`: sayfa >= 1, sayfa boyutu 1-100 arası, `from <= to`, sunucu adı uzunluğu. **Sayfa boyutu üst sınırı sessizce kırpılmıyor, 400 dönüyor** — istemci ne istediğini bilerek düzeltsin.
- `SecurityAlertRepository.QueryAsync`: `AsNoTracking()`, filtreler LINQ ile (parametreli sorgu, string birleştirme yok), `Timestamp` azalan + `Id` azalan sıralama (zaman damgaları eşitse sıralama kararlı kalsın), `Skip/Take` ile sayfalama. Projeksiyon doğrudan DTO'ya yapılıyor, gereksiz kolon çekilmiyor.
- `AlertsController`: `GET /api/alerts`.

**Sözleşme değişikliği:** `SecurityAlertDto`'ya `Id` eklendi. Tespit servisi artık alarmı **önce kaydediyor**, sonra kalıcı Id'yi taşıyan DTO'yu yayınlıyor. Böylece canlı yayından gelen alarmla geçmiş sorgusundan gelen aynı kayıt eşlenebiliyor — panelde çift satır oluşmuyor.

**Frontend:**
- `core/models/security-alert.ts`: `SecurityAlert` arayüzü ve `toSecurityAlert(raw: unknown)` **tip koruyucusu**. Zorunlu alanlar (id sayı, serverName/severity metin, ayrıştırılabilir timestamp) yoksa `null` döner; isteğe bağlı alanlar eksikse anlamlı varsayılanlarla doldurulur.
- `MonitoringHubService`: `alerts$` akışı eklendi. Handler `try/catch` içinde; doğrulamadan geçmeyen kayıt konsola uyarı yazıp atlanıyor, akışa hiç girmiyor. **Bozuk bir kayıt ne aboneliği ne hub bağlantısını ne de UI'ı düşürüyor.**
- `AlertApiService`: `GET /api/alerts` çağrısı; dönen listedeki bozuk kayıtlar da aynı koruyucudan geçirilip eleniyor, sağlamlar gösterilmeye devam ediyor.
- `alerts/AlertList` component'i: açılışta ilk sayfayı çekiyor, canlı alarmları başa ekliyor. `merge` fonksiyonu Id'ye göre tekilleştirip zamana göre sıralıyor ve listeyi `MAX_VISIBLE_ALERTS` (200) ile sınırlıyor — uzun süre açık kalan sekmede bellek büyümesin. Geçmiş yüklenemezse bilgilendirme gösteriliyor ama **canlı akış çalışmaya devam ediyor**.
- Severity renkleri: Critical koyu kırmızı, High kırmızı, Medium turuncu, Low gri. Tablo kendi `overflow-x` kabında; sayfa yatay kaymıyor.
- `provideHttpClient(withFetch())` eklendi (proje şablonunda yoktu).

### Doğrulama

Endpoint:

| Test | Sonuç |
|---|---|
| Varsayılan çağrı | `page=1 pageSize=25 total=3 totalPages=1 hasNext=false` |
| `pageSize=2&page=1` | 2 kayıt, `hasNext=true` |
| `pageSize=2&page=2` | 1 kayıt, `hasNext=false` |
| `serverName=web-01` / olmayan sunucu | 3 kayıt / 0 kayıt |
| Tarih aralığı (bugün / 2020) | 3 kayıt / 0 kayıt |
| **`pageSize=5000`** | **400** — "1 ve 100 arasında olmalı" |
| `page=0` | 400 |
| `from > to` | 400 |

Panel (tarayıcıda):

| Test | Sonuç |
|---|---|
| Açılışta geçmiş yükleme | 3 alarm listelendi, en yeni üstte |
| **Sayfa açıkken yeni alarm** | Listenin başına eklendi, sayaç 3→4, sayfa yenilenmeden |
| Renk kodlaması | Critical/High kırmızı tonları, Medium turuncu, Low gri |
| Konsol | Hata yok |
| Gauge'lar + alarm listesi birlikte | İkisi de canlı çalıştı |

Bozuk veri koruması (14 vitest testi, hepsi geçti): `null`, `undefined`, metin, sayı, dizi, boş nesne, eksik `id`, metin `id`, eksik `serverName`/`severity`/`timestamp`, ayrıştırılamayan tarih → hepsi `null` döndü. Eksik isteğe bağlı alanlar varsayılanlarla dolduruldu.

### Öğrenilen kavramlar
- **Sayfalama neden zorunlu**: filtresiz bir `GET`, tablo büyüdükçe hem veritabanını hem belleği hem ağı tüketir. Üst sınır sunucu tarafında zorlanmalı; istemcinin iyi niyetine güvenilmez.
- **Kararlı sıralama**: yalnızca `Timestamp`'e göre sıralamak, eşit damgalarda sayfalar arası kayıt tekrarına/atlanmasına yol açar. İkincil anahtar (`Id`) bunu önler.
- **Tip koruyucusu (type guard)**: TypeScript'in tipleri derleme zamanındadır; ağdan gelen veri `unknown`'dır. Çalışma zamanında doğrulanmadan güvenmek, sözleşme değişince UI'ı çökertir.
- **Kısmi başarısızlık**: geçmiş yüklenemese bile canlı akış çalışır; bozuk bir kayıt atlanır, sağlamlar gösterilir. "Ya hep ya hiç" yerine mümkün olanı göstermek.
- **İstemci tarafında sınırlama**: canlı akışla beslenen liste doğal olarak sınırsız büyür; görünen kayıt sayısını sınırlamak gerekir (sunucudaki kuyruk sınırının UI karşılığı).
- **Id ile tekilleştirme**: aynı kaydın iki yoldan (canlı + geçmiş) gelmesi normaldir; kalıcı kimlik olmadan tekilleştirme kırılgan olur.
- **`AsNoTracking()`**: salt okunur sorgularda EF'in değişiklik takibi gereksiz maliyettir.

### Notlar / dikkat
- Panelde şu an yalnızca ilk sayfa gösteriliyor; `serverName`/`from`/`to` filtreleri ve "sonraki sayfa" düğmesi endpoint'te hazır ama arayüze bağlanmadı.
- Liste 200 kayıtla sınırlı; daha eskisini görmek için sorgu arayüzü gerekecek.
- Alarm zamanları tarayıcının yerel saatinde gösteriliyor (veritabanında UTC).
- Medium/Low/Critical renkleri, tespit servisi şu an yalnızca `High` ürettiği için doğrudan veritabanına örnek kayıt eklenerek doğrulandı; test kayıtları sonrasında silindi.

---

## Prompt 8 — IIS trafik loglarının takibi (log tailing) (2026-09-04)

### Ne istendi
Agent'a `TrafficLogWorker : BackgroundService`. IIS'in W3C Extended Log dosyasını (yol appsettings'ten) `FileSystemWatcher` ile izle, yalnızca yeni satırları oku. Son okunan konumu yerel bir dosyaya yaz — yeniden başlatmada kaldığı yerden devam etsin, veri kaybetmesin, mükerrer kayıt oluşturmasın. Günlük rotasyonu tespit et. Dosya kilitliyse `FileShare.ReadWrite` ile aç, açamazsan bekle ve tekrar dene, worker'ı çökertme. Her satırdan istemci IP, path, status ve süreyi çıkar; beklenmedik biçimde satırı atla ve logla. `TrafficLogDto` üretip backend'e gönder.

### Ne yapıldı

**`Traffic/W3CFieldMap` — alan sırası sabit değildir.**
W3C formatında sütun sırasını dosyanın başındaki `#Fields:` satırı belirler ve IIS yapılandırması değişirse dosyanın **ortasında** yeni bir yönerge yazılabilir. Bu yüzden sütun indeksleri sabit kodlanmadı; eşleme yönergeden okunuyor ve okuma sırasında güncellenebiliyor.

**`Traffic/W3CLogParser`:** `TryParse` deseni. Zorunlu alanlar (`c-ip`, `cs-uri-stem`, `sc-status`, `time-taken`) eksik, `-` veya sayıya çevrilemiyorsa satır atlanıp loglanıyor. Zaman damgası `date` + `time` sütunlarından UTC olarak okunuyor, okunamazsa çağıranın verdiği zamana düşülüyor.

**`Traffic/TrafficLogFileReader` — yarım satır sorunu.**
IIS satırı yazarken okuma yapılırsa yarım satır yakalanabilir. Okuyucu, tampondaki **son satır sonu karakterine kadar** olan kısmı işliyor ve konumu yarım satırın başında bırakıyor. Satır tamamlandığında baştan ve eksiksiz okunuyor. Satır sonunda bölmek UTF-8 için güvenli, çünkü devam baytları hiçbir zaman `0x0A` olmuyor.

Dosya `FileShare.ReadWrite | FileShare.Delete` ile açılıyor (Delete, rotasyon sırasındaki yeniden adlandırma için). `IOException`/`UnauthorizedAccessException` yakalanıp uyarı olarak loglanıyor ve bir sonraki turda tekrar deneniyor.

**`Traffic/LogOffsetStore`:** Konum `{ FileName, Offset }` olarak JSON'a yazılıyor. Yazma **atomik**: önce geçici dosyaya yazılıp sonra taşınıyor, böylece yazma sırasında süreç düşerse mevcut konum dosyası bozulmuyor. Bozuk bir konum dosyası okumayı engellemiyor, baştan başlanıyor.

**`Traffic/TrafficLogWorker` — en önemli tasarım kararı.**
Konum, satırlar **backend'e ulaştıktan sonra** kalıcı hale getiriliyor. Backend erişilemezken konum ilerlemiyor ve yeni satır okunmuyor; log dosyasının kendisi tampon görevi görüyor. Bu, bellek kuyruğuna güvenmekten çok daha sağlam: IIS logları zaten diskte duruyor.

Sonuç olarak teslimat garantisi **"en az bir kez"**: süreç, tam teslimat ile konumun diske yazılması arasındaki kısa aralıkta düşerse birkaç satır tekrar okunabilir. Kayıp yerine tekrarı seçmek monitoring için doğru tercih.

`FileSystemWatcher` tek başına güvenilmez (olay kaçırabilir, hata verebilir). Bu yüzden watcher yalnızca döngüyü **erken uyandıran** bir sinyal; ayrıca yoklama aralığı var. Watcher hata verirse yoklama sayesinde akış durmuyor.

Rotasyon: en yeni dosya ada göre bulunuyor. Yeni dosya oluştuğunda hemen geçilmiyor — **önce eski dosyada okunacak satır kalmadığı doğrulanıyor**, böylece gün devrinde son istekler kaybolmuyor.

**Backend:** `TrafficLog` entity, konfigürasyon, repository, `TrafficLogDtoValidator`, `POST /api/traffic`, hub'a `ReceiveTrafficLog` ve `AddTrafficLogs` migration'ı. İki index var: `(ServerName, Timestamp)` ve `(ClientIp, Timestamp)` — ikincisi anormal patern analizinin ihtiyaç duyacağı sıra.

**Shared:** `NetworkConstraints.IpAddressMaxLength` eklendi; `SecurityEventConstraints` ve `TrafficConstraints` aynı değeri buradan alıyor, IP uzunluğu iki yerde tekrarlanmıyor.

### Doğrulama

Gerçek bir IIS kurulumu olmadığı için gerçekçi bir W3C log dosyası üretilip agent ona yönlendirildi.

| Test | Sonuç |
|---|---|
| `dotnet build` | 0 uyarı, 0 hata |
| 9 satırlık dosya (3'ü bozuk) | 6 kayıt yazıldı, 3 satır atlanıp loglandı |
| Bozuk satır türleri | Alan sayısı eksik / `time-taken=abc` / `c-ip=-` → hepsi atlandı |
| Zaman damgası | Log'dan okundu (`08:00:01`), "şimdi" kullanılmadı |
| **Yarım satır (newline yok)** | **Okunmadı**, konum ilerlemedi |
| Satır tamamlanınca | Eksiksiz okundu, 3 yeni kayıt |
| **Agent kapalıyken 2 satır eklendi, yeniden başlatıldı** | **11 kayıt: mükerrer yok, kayıp yok** |
| **Rotasyon** (eski dosyaya son satır + yeni dosya) | Önce eski dosyanın son satırı okundu, sonra geçildi |
| **Yeni dosyada farklı alan sırası** (15 → 7 sütun) | Parser uyum sağladı, doğru sütunlardan okudu |
| **Dosya `FileShare.None` ile 9 sn kilitlendi** | Uyarı loglandı, **agent ayakta kaldı**, kilit kalkınca satır okundu |
| **Backend 30 sn kapalı, 3 satır eklendi** | Konum **ilerlemedi** (340'ta kaldı), uyarı loglandı |
| Backend geri geldi | 3 satır **tam birer kez** ulaştı, konum ilerledi |
| Endpoint doğrulama | `statusCode=999`, `responseTimeMs=-5`, boş IP → 400 |
| **IIS olmayan makine** | Açıklayıcı uyarı, toplayıcı durdu, **agent çalışmaya devam etti** |

### Öğrenilen kavramlar
- **Log tailing**: dosyayı baştan okumak yerine konumdan devam etmek. Tüm log toplama araçlarının (Filebeat, Fluentd, Promtail) temel çalışma biçimi.
- **Yarım satır problemi**: yazılmakta olan bir dosyayı okurken satır bütünlüğünü korumak. Satır sonunu görmeden işlememek gerekir.
- **Checkpoint / offset kalıcılığı**: "nereye kadar işledim" bilgisini dayanıklı tutmak. Atomik yazma olmadan checkpoint'in kendisi bozulabilir.
- **Teslimat garantileri**: "en fazla bir kez" (kayıp riski) ile "en az bir kez" (tekrar riski) arasındaki seçim. Dağıtık sistemlerde tam olarak bir kez, koordinasyon olmadan mümkün değildir.
- **Geri baskı (backpressure)**: alıcı yetişemiyorsa kaynaktan okumayı durdurmak. Kuyruğu şişirmek yerine veriyi kaynağında (log dosyasında) bırakmak.
- **`FileShare`**: bir dosyayı başka bir süreç yazarken okuyabilmek. Paylaşım bayrakları uyuşmazsa açma başarısız olur.
- **`FileSystemWatcher` güvenilmezdir**: tampon taşarsa olay kaçar. Üretimde her zaman yoklama ile birlikte kullanılmalı.
- **Kendini tanımlayan format**: W3C'de sütunları verinin kendisi bildirir. Sabit indeks varsaymak, yapılandırma değişince sessizce yanlış veri üretir.

### Notlar / dikkat
- Şu an **tek klasör** (tek IIS sitesi) izleniyor. Birden fazla site için (`W3SVC1`, `W3SVC2`) çoklu izleyici gerekir.
- IIS logları varsayılan olarak **periyodik** yazar (tampon boşaltma). Anlık takip için IIS'te log flush süresi kısaltılmalı, aksi halde veriler dakikalar gecikebilir.
- `ReadExistingFileOnFirstRun` varsayılan `true`: ilk çalıştırmada günün log dosyası baştan okunur. Yoğun bir sitede bu, kuruluma tek seferlik büyük bir yük bindirir; `false` yapılabilir.
- Trafik verisi hacimlidir. Saklama/özetleme politikası kurulmadan üretimde uzun süre çalıştırılmamalı.
- Panel (`ServerGuard.Web`) `ReceiveTrafficLog` yayınını henüz dinlemiyor.
- Bu adımda yalnızca veri toplandı; "anormal patern" tespiti (aynı IP'den aşırı istek, 5xx patlaması) henüz yok. `(ClientIp, Timestamp)` index'i bu sorguya hazır.

---

## Prompt 9 — Trafik anomali kuralı (2026-09-04)

### Ne istendi
Api'ye `TrafficAnomalyDetectionService`. Gelen trafik kayıtlarını IP bazında grupla: aynı IP'den son 1 dakikada 100'den fazla istek geldiyse `SecurityAlert` üret (Type: TrafficAnomaly, Severity: Medium). Eşikler appsettings'ten okunsun, hardcode edilmesin. Belirli süredir görülmeyen IP'leri periyodik temizleyen bir mekanizma ekle. Koda, bunun gerçek bir DDoS koruması olmadığını açıklayan yorum ekle.

### Ne yapıldı

**Sözleşme düzeltmesi.** `SecurityAlertDto.FailedAttemptCount` → `ObservedCount`. İkinci alarm tipi geldiğinde bu ad yanlış olurdu: trafik anomalisinde sayılan şey başarısız giriş değil, istek. Anlamı alarm tipine göre değişiyor ve bu XML yorumunda açıklandı. EF `RenameColumn` üretti, mevcut veri korundu. `AlertType` enum'ına `TrafficAnomaly` eklendi.

**`Detection/TrafficWindow` — neden farklı bir yapı.**
Brute-force'taki `FailureWindow` her denemenin zaman damgasını saklıyor; eşik 5 olduğu için ucuz. Trafikte eşik 100'e çıkıyor ve her isteğin damgasını saklamak IP başına ~1,6 KB eder; 100.000 IP'de 160 MB. Bu yüzden pencere **dilimlere bölündü** (varsayılan 6 dilim × 10 saniye) ve her dilimde yalnızca bir sayaç tutuluyor. Bellek IP başına sabit ve küçük, sayım dilim çözünürlüğü kadar hassas — "100'den fazla mı?" sorusu için fazlasıyla yeterli.

Dilim devri mutlak "epoch" numarasıyla yapılıyor. IIS logları toplu yazdığından kayıtlar sırasız gelebiliyor; dilimi çoktan devredilmiş bir kayıt sayılmıyor, çünkü sayılsaydı güncel dilimin sayacı bozulurdu.

**İki farklı zaman kavramı.** Test sırasında ortaya çıkan önemli bir ayrım:
- **Sayım** olayın gerçekleştiği ana (IIS log damgası) göre yapılıyor. IIS bir dakikalık trafiği tek seferde yazdığından, geliş anına göre saymak bu isteklerin hepsini aynı ana yığar ve yanlış alarm üretirdi.
- **"En son ne zaman görüldü"** ise duvar saatine göre tutuluyor; temizlik buna dayanıyor. Geçmişe dönük log okunurken olay zamanı çok eski olabilir ama sayaç o an kullanılmaktadır.

**`Detection/TrafficWindowStore`:** `ConcurrentDictionary`. Brute-force'taki `IMemoryCache`'ten farklı olarak `GetOrAdd`, sözlükte gerçekten duran örneği döndürüyor; bu yüzden sayım kaybı olmuyor ve ek kilit gerekmiyor. Ayrıca `TrackedIpLimit` sert üst sınırı var: temizlik taramaları arasında sahte IP seli belleği dolduramaz.

**`Detection/TrafficWindowCleanupService`:** `BackgroundService`, `CleanupInterval` sıklığında tarayıp `IdleRetention` süresidir görülmeyen sayaçları siliyor. Kaç kayıt silindiği ve kaç kaldığı loglanıyor. Tek bir taramanın hatası servisi düşürmüyor.

**Alarm bastırma.** `AlertCooldown` (varsayılan 5 dk): bir IP için alarm üretildikten sonra o süre boyunca yeni alarm üretilmiyor, ama sayım kesintisiz sürüyor. Bu olmadan saniyede yüzlerce istek gönderen bir kaynak, saniyede onlarca alarm üretirdi.

**DDoS açıklaması.** `TrafficAnomalyDetectionService` üzerine kapsamlı bir `<remarks>` yazıldı: bu katmanın istekleri engellemediği, yavaşlatmadığı, hız sınırlaması uygulamadığı; loglar okunduktan **sonra**, yani istekler çoktan işlendikten sonra çalıştığı; gerçek korumanın güvenlik duvarı, reverse proxy/CDN hız sınırlama, IIS Dynamic IP Restrictions veya sağlayıcı seviyesinde DDoS azaltma ile yapılması gerektiği açıkça belirtildi.

### Doğrulama

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| Migration (`RenameColumn`) | Sütun yeniden adlandırıldı, veri korundu |
| **100 eşzamanlı istek** (eşik 100) | Alarm yok — kural "daha fazla" |
| 101. istek | 1 alarm, `TrafficAnomaly` / `Medium` / `ObservedCount=101` |
| **Cooldown içinde 200 istek daha** | Yeni alarm yok |
| Farklı IP (50 istek) | Kendi sayacı, alarm yok |
| Aynı IP farklı sunucu (101 istek) | Ayrı sayaç, ayrı alarm |
| **101 istek 2 dakikaya yayılmış** | **Alarm yok** — pencereye sığmıyor |
| Aynı IP 101 isteği bir dakikaya sıkıştırdı | Alarm üretildi |
| **Boşta kalan sayaç temizliği** | `Removed=1 Remaining=0` |
| Brute-force kuralı (yeniden adlandırma sonrası) | Çalışmaya devam etti |
| `GET /api/alerts` | İki alarm tipi birlikte listelendi |

**Test sırasında ortaya çıkan bulgu:** İlk temizlik denemesinde `IdleRetention` için 20 saniye verdim; `[Range]` alt sınırı 1 dakika olduğu için **API açılışta düştü**. Bu bir hata değil, `ValidateOnStart` doğrulamasının çalıştığının kanıtı: hatalı konfigürasyon ilk istekte değil, hemen fark edildi. Test betiğine sağlık kontrolü eklendi; aksi halde `curl -s` hataları yutup testler yanlışlıkla "geçti" görünüyordu.

### Öğrenilen kavramlar
- **Dilimli (bucketed) sayaç**: yüksek hacimde "son X sürede kaç olay" sorusunu sabit bellekle yanıtlamak. Hassasiyetten biraz feragat edip belleği kurtarmak.
- **Aynı soruya farklı çözüm**: brute-force'ta damga listesi, trafikte dilimli sayaç. Doğru veri yapısı, ölçeğe göre değişir; körü körüne yeniden kullanım her zaman doğru değildir.
- **Olay zamanı (event time) vs işleme zamanı (processing time)**: akış işlemede temel ayrım. Toplu/gecikmeli gelen verilerde hangisinin kullanılacağı sonucu tamamen değiştirir.
- **`ConcurrentDictionary.GetOrAdd` vs `IMemoryCache.GetOrCreate`**: ilki sözlükteki gerçek örneği döndürür (güvenli), ikincisi yarış koşuluna açıktır.
- **Alarm bastırma (cooldown)**: tespit ile bildirim ayrı şeylerdir. Sürekli bir olay için sürekli alarm üretmek, alarmı işe yaramaz hale getirir.
- **İki katmanlı bellek koruması**: zaman aşımı (boşta kalanı sil) + sert sınır (sayıyı kısıtla). Yalnızca biri yeterli değildir.
- **Gözlem ≠ koruma**: monitoring bir olayı görünür kılar, engellemez. Bu ayrımı kod içinde açıkça yazmak, sistemin yanlış bir güvenlik hissi vermesini önler.

### Notlar / dikkat
- **Bu bir DDoS koruması değildir.** Gerçek koruma sunucuya ulaşmadan önceki katmanlarda yapılmalıdır.
- Sayaçlar bellek içidir: Api yeniden başlarsa sıfırlanır; birden fazla Api örneğinde her biri kendi sayacını tutar.
- IIS logları gecikmeli yazıldığından alarm, olaydan dakikalar sonra üretilebilir.
- `ReadExistingFileOnFirstRun=true` ile ilk kurulumda geçmiş loglar okunursa, geçmişteki yoğun dönemler için de alarm üretilir. Bu istenmiyorsa ayar `false` yapılmalı.
- Eşik değerleri ortamınıza göre ayarlanmalı: 100 istek/dakika bazı sağlık kontrolü (health check) veya izleme araçları için normal olabilir. Yanlış pozitifleri azaltmak için bilinen IP'lerin muaf tutulması (allow list) ileride eklenebilir.
- Panel `TrafficAnomaly` alarmlarını mevcut listede gösteriyor (Medium = turuncu), ayrı bir trafik ekranı henüz yok.

---

## Prompt 10 — Trafik ekranı: zaman serisi grafiği ve top IP tablosu (2026-09-04)

### Ne istendi
Panele son 1 saatteki istek sayısını zaman bazlı gösteren bir çizgi grafik (DevExtreme Chart), yükleme ve hata durumları için UI state'i. Ayrıca `GET /api/traffic/top-ips?serverName=&minutes=` endpoint'i ile en çok istek atan ilk 10 IP tablosu. Grafik SignalR ile canlı güncellensin.

### Ne yapıldı

**Kapsam notu:** Prompt yalnızca `top-ips` endpoint'ini adlandırıyor, ama "son 1 saatin" grafiği açılışta geçmiş veri gerektiriyor; canlı akışla beslenen bir grafik boş başlardı. Bu yüzden `GET /api/traffic/timeline` de eklendi.

**Backend:**
- `GET /api/traffic/timeline?serverName=&minutes=&bucketSeconds=` — aralığı eşit dilimlere bölüp her dilimdeki istek sayısını döner.
- `GET /api/traffic/top-ips?serverName=&minutes=&take=` — en çok istek gönderen adresler.
- Gruplama **veritabanında** yapılıyor (`EF.Functions.DateDiffSecond` ile dilim indeksi); tüm satırlar belleğe çekilmiyor.
- **Boş dilimler sıfırla dolduruluyor.** Aksi halde çizgi grafik, istek gelmeyen aralıkları atlayarak yanıltıcı bir süreklilik gösterirdi.
- Sıralamada ikincil anahtar var (`ClientIp`), eşit sayıda isteği olan adreslerde sıra kararlı kalıyor.
- `TrafficQueryConstraints` ile sınırlar: `minutes` 1-1440, `bucketSeconds` 10-3600, `take` 1-100. Aşılırsa 400 dönüyor.

**Frontend:**
- `core/models/traffic.ts`: `TrafficLog`, `TrafficTimelinePoint`, `TopClientIp` ve üçü için tip koruyucuları. Bozuk kayıt hem hub'dan hem API'den geldiğinde eleniyor.
- `MonitoringHubService`: `trafficLogs$` akışı eklendi. Alarm ve trafik için ayrı ayrı yazılmış olan doğrulama/try-catch kodu tek bir generic `emit` metoduna toplandı.
- `TrafficApiService`: iki sorgu, dönen listeler koruyucudan geçiriliyor.
- `traffic/TrafficChart`: `dx-chart` çizgi grafiği. Üç durum — `loading` / `error` (yeniden dene düğmesiyle) / `ready`. Canlı gelen her istek, ait olduğu dilimin sayacını artırıyor; dilim serinin sonundan yeniyse aradaki boşluklar sıfırla doldurulup pencere kaydırılıyor, böylece grafik hep son 60 dakikayı gösteriyor ve sınırsız büyümüyor. Serinin gerisinde kalan çok eski kayıtlar grafiği değiştirmiyor.
- `traffic/TopIpsTable`: aynı üç durum. Satır içi oran çubuğu ile dağılım görsel olarak okunuyor.
- Her iki bileşen de dashboard'a eklendi; dar ekranda alt alta geçen bir grid.

### Doğrulama

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| 400 kayıtlık test verisi, `GET /timeline` | 60 nokta, 58 dolu dilim, 400 isteğin tamamı sayıldı |
| Boş dilimler | Sıfırla dolduruldu, seride kopukluk yok |
| `GET /top-ips` | 10 satır, doğru sıralama |
| `serverName` filtresi | Yalnızca o sunucunun kayıtları |
| `minutes=5` (dar aralık) | Sayılar buna göre düştü |
| `bucketSeconds=300` | 10 dakikalık aralık 2 noktaya bölündü |
| Sınırlar: `take=5000`, `minutes=99999`, `bucketSeconds=1` | Üçü de 400 |
| Tarayıcı: grafik + tablo | İkisi de render oldu |
| **Canlı güncelleme** | 25 istek gönderildi; grafik sayfa yenilenmeden güncellendi, Y ekseni 8 → 25 ölçeklendi |
| **Hata durumu** (API kapalı) | İki bölümde de hata kutusu ve "Yeniden dene" düğmesi |
| **Yeniden dene düğmesi** (API geri gelince) | Hata temizlendi, grafik ve 10 satırlık tablo yüklendi |

**Yol boyunca çıkan iki hata:**
1. `GET /top-ips` 500 döndü. Sebep: EF, record'a projeksiyon yaptıktan **sonra** o record'un alanlarına göre sıralamayı SQL'e çeviremiyor. Sıralama anonim tip üzerinden SQL'de yapılıp DTO'ya sonradan dönüştürülerek çözüldü. Global exception handler bu sırada beklendiği gibi davrandı: kullanıcıya güvenli mesaj, detay log'a.
2. Panel ilk açılışta her şeyi hata durumunda gösterdi. Sebep kod değil konfigürasyon: `environment.ts` HTTPS 7066'yı gösteriyordu, API ise yalnızca HTTP 5190'da çalışıyordu. API iki portta birden başlatılınca düzeldi. Bu, hata UI'ının kendiliğinden bir testi oldu.

### Öğrenilen kavramlar
- **Zaman serisi kovalama (time bucketing)**: ham kayıtları eşit dilimlere indirgeyerek grafiğe uygun hale getirmek. Veritabanında yapıldığında ağ ve bellek maliyeti düşer.
- **Boş dilimlerin önemi**: eksik nokta ile sıfır değerli nokta farklıdır. Eksik bırakmak, grafikte olmayan bir sürekliliği varmış gibi gösterir.
- **EF Core çeviri sınırları**: her LINQ ifadesi SQL'e çevrilemez. Projeksiyon sonrası sıralama tipik bir tuzaktır; anonim tip kullanıp dönüşümü belleğe bırakmak standart çözümdür.
- **UI durum makinesi**: `loading` / `ready` / `error` üçlüsü. Hata durumunda kullanıcıya bir çıkış yolu (yeniden dene) sunmak, ekranı ölü bırakmamak.
- **Canlı + geçmiş birleşimi**: geçmiş HTTP ile bir kez çekilir, üzerine canlı akış işlenir. İkisinin aynı dilimlemeyi kullanması şart, aksi halde sayılar tutmaz.
- **Kayan pencere UI tarafında**: canlı akışla beslenen bir grafik, eski noktalar atılmazsa sınırsız büyür.

### Notlar / dikkat
- **Top IP tablosu canlı güncellenmiyor**, açılışta bir kez yükleniyor (prompt yalnızca grafik için canlı güncelleme istiyordu). Periyodik yenileme veya bir "yenile" düğmesi eklenebilir.
- Grafik ve tablo şu an **tüm sunucuları birlikte** gösteriyor. `serverName` filtresi endpoint'lerde hazır ama arayüze bağlanmadı; iki sunuculu kurulumda sunucu seçici eklemek faydalı olacak.
- Grafik yalnızca toplam istek sayısını gösteriyor. Hata oranını (4xx/5xx) ayrı bir seri olarak eklemek, "servislerde sorun var mı" sorusuna doğrudan yanıt verirdi.
- Zaman aralığı (60 dakika) ve dilim boyu (60 saniye) şimdilik sabit; arayüzden seçilebilir hale getirilebilir.
- Bundle boyutu 2,56 MB'a çıktı (DevExtreme Chart eklendi). Üretimde yalnızca kullanılan DevExtreme modüllerini almak için ayrı bir optimizasyon gerekebilir.

---

## Prompt 11 — İkinci sunucu entegrasyonu (2026-09-04)

### Ne istendi
Agent'ta `ServerName` ve `ApiBaseUrl`'in yapılandırılabilir olduğunu doğrula. İkinci sunucuya kurulum için, `appsettings.json`'da nelerin değişeceğini ve `sc create` ile Windows Service kaydını anlatan bir README yaz. Backend ve panelde `ServerName`'in her yerde doğru kullanıldığından ve birden fazla sunucuyu ayırt ettiğinden emin ol. Panele sunucu seçici (dropdown) ekle, seçilen sunucuya göre tüm ekranlar filtrelensin.

### Denetim sonucu

Önce mevcut durum tarandı:

| Kontrol | Sonuç |
|---|---|
| Agent'ta `ServerName` kullanımı | Üç toplayıcı da `_agentOptions.ServerName` kullanıyor, tutarlı |
| Agent'ta Api adresi | Yapılandırılabilir, `[Required]` ile açılışta doğrulanıyor |
| Entity'lerde `ServerName` | Dördünde de var, index'lerde ilk sütun |
| Tespit kuralları | İkisi de sunucu + IP çiftiyle anahtarlanıyor, sunucular karışmıyor |
| `/api/alerts`, `/api/traffic/*` | `serverName` filtresi zaten mevcut |
| **Sunucu listesi endpoint'i** | **Yok** — dropdown'ı besleyecek kaynak eksikti |

### Ne yapıldı

**Adlandırma:** `Agent:BackendBaseUrl` → `Agent:ApiBaseUrl`. Hem prompt'taki isimlendirmeye hem web istemcisindeki `apiBaseUrl` değişkenine uyuyor; aynı kavram artık iki projede aynı adla anılıyor.

**`GET /api/servers`:** Veri göndermiş sunucuları son görülme zamanlarıyla döner. Ayrı bir sunucu kayıt tablosu tutulmuyor, liste gelen kayıtlardan türetiliyor. Hem metrik hem trafik taranıyor: bir sunucuda metrik toplama kapatılmış olsa bile trafik gönderiyorsa listede görünür. Sorgu `sinceHours` (varsayılan 24, en fazla 720) ile sınırlı — bu olmadan sorgu tüm tabloyu tarardı.

**`ServerSelectionService`:** Seçili sunucuyu tutan tek bir signal. `null` = tüm sunucular. Bileşenler hem geçmiş sorgularında (parametre olarak) hem canlı akışta (`matches()` ile) bunu kullanıyor.

**`ServerSelector` bileşeni:** Listeyi API'den çeker, kendi loading/error durumu var. Seçili sunucu listeden kaybolursa otomatik olarak "tümü"ne döner — ekran boş kalmasın.

**Dört bileşen de seçime bağlandı:**
- **Sunucu kartları (gauge'lar):** `computed` ile filtreleniyor.
- **Trafik grafiği:** seçim değişince `effect` ile seri yeniden yükleniyor; canlı gelen kayıtlar da filtreleniyor.
- **Top IP tablosu:** aynı şekilde yeniden yükleniyor.
- **Alarm listesi:** seçim değişince liste sıfırlanıp yeniden çekiliyor; canlı alarmlar filtreleniyor.

**`docs/AGENT-KURULUM.md`:** İkinci sunucu kurulumu için ayrı doküman — değişmesi gereken üç ayar, örnek `appsettings.json`, publish, `sc.exe create` (sözdizimindeki boşluk tuzağı dahil), servis çökmesinde otomatik yeniden başlatma, en az ayrıcalık yetkileri, ağ/güvenlik duvarı, kurulum sonrası doğrulama adımları ve sık karşılaşılan sorunlar tablosu.

### Doğrulama

İki sunuculuk (`web-01`, `db-01`) veri üretilip test edildi: 300 trafik kaydı (her sunucuda 150), iki metrik, iki alarm.

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` | 0 hata, 0 uyarı |
| `GET /api/servers` | İki sunucu da son görülme zamanlarıyla döndü |
| `sinceHours=99999` | 400 |
| `top-ips` — tümü / web-01 / db-01 | Sırasıyla karışık / yalnızca `203.0.113.x` / yalnızca `198.51.100.x` |
| `alerts` — tümü / web-01 / db-01 | 2 / 1 / 1 alarm, doğru sunucular |
| `timeline` toplam istek — tümü / web-01 / db-01 | **300 / 150 / 150** |
| Panelde dropdown | "Tüm sunucular", "db-01", "web-01" |
| db-01 seçimi | Top IP'ler `198.51.100.x`'e döndü, alarm sayacı 1, grafik yeniden ölçeklendi |
| web-01 seçimi | Top IP'ler `203.0.113.x`, yalnızca web-01 alarmı |
| "Tüm sunucular"a dönüş | 10 IP, iki alarm |
| **Canlı metrik, db-01 seçiliyken** | İki sunucuya da gönderildi, **yalnızca db-01 kartı göründü** |
| **Canlı trafik — seçili olmayan sunucu** | web-01 için 40 istek gönderildi, **grafik değişmedi** |
| **Canlı trafik — seçili sunucu** | db-01 için 40 istek, **grafik 3 → 40 ölçeklendi** |

### Öğrenilen kavramlar
- **Kiracı/kapsam ayrımı (scoping)**: tek bir alan (`ServerName`) sistemin her katmanında tutarlı taşındığında, çok kaynaklı bir sistem tek kaynaklı gibi yönetilebilir hale gelir.
- **Türetilmiş liste vs kayıt tablosu**: sunucu listesini veriden türetmek ekstra tablo ve senkronizasyon derdi olmadan çalışır; karşılığında sorgu maliyeti ve "hiç veri göndermemiş sunucu görünmez" kısıtı gelir.
- **Paylaşılan UI durumu**: seçim gibi ekranlar arası ortak durum tek bir serviste tutulur; her bileşenin kendi kopyasını tutması tutarsızlığa yol açar.
- **Signal + `effect`**: bir sinyal değiştiğinde veri yeniden çekmenin bildirimsel yolu. Manuel abonelik yönetimi gerekmiyor.
- **Filtrenin iki yerde uygulanması**: geçmiş veriyi sunucu filtreler, canlı akışı istemci filtreler. İkisi tutarlı olmazsa ekranda seçili olmayan sunucunun verisi belirir.
- **Konfigürasyonla ayrışma**: aynı binary'nin farklı sunucularda farklı kimlikle çalışması. Kod değişmez, yalnızca ayar dosyası değişir.

### Notlar / dikkat
- **Sunucu kartları yalnızca canlı akıştan doluyor.** Panel açıldığında bir sunucu henüz metrik göndermediyse kartı görünmez (en fazla toplama aralığı kadar, varsayılan 10 sn). Geçmişten son metrikleri çeken bir endpoint bunu çözerdi.
- **Sunucu listesi açılışta bir kez çekiliyor.** Panel açıkken yeni bir sunucu eklenirse listede görünmesi için sayfa yenilenmeli.
- Bir sunucu tamamen sustuğunda listede `sinceHours` süresince kalmaya devam eder. "Sunucu çöktü" tespiti (son görülme zamanına bakıp uyarı üretmek) henüz yok; `lastSeenAt` alanı bu iş için hazır.
- `ServerName` karşılaştırmaları veritabanında SQL Server'ın varsayılan harf duyarsız sıralamasına, bellekte ise `OrdinalIgnoreCase`'e dayanıyor. `web-01` ve `WEB-01` aynı sunucu sayılır.

---

## Prompt 12 — IP itibar sorgusu (AbuseIPDB) (2026-09-07)

### Ne istendi
`IpReputationService`. Trafik anomalisi şüpheli bir IP tespit ettiğinde AbuseIPDB'ye sorgu at (API anahtarı user-secrets/ortam değişkeninden, asla koda gömülmesin). Bu çağrıya da retry + timeout + circuit breaker uygula. AbuseIPDB yanıt vermez veya yavaş olursa **alarm üretimini bloklamasın**; itibar bilgisi eksik gelirse alarm yine de oluşsun. Skoru alarm kaydına işle. Aynı IP'yi son 1 saat içinde tekrar sorgulama (IMemoryCache, expiration tanımlı).

### Ne yapıldı

**Sözleşme:** `SecurityAlertDto` ve `SecurityAlert` entity'sine `AbuseConfidenceScore` (`int?`) eklendi. Alan **nullable**: "skor yok" ile "skor sıfır" farklı anlamlar taşır. Sıfır, AbuseIPDB'nin o adres hakkında olumsuz kaydı olmadığını söyler; null ise bilgi hiç alınamadı demektir.

**`Reputation/AbuseIpDbClient`:** Named HttpClient + resilience. Süreler bilinçli olarak kısa: attempt 2 sn, **toplam 5 sn**, 2 retry, circuit breaker (30 sn örnekleme, %50 hata oranı, 1 dk açık kalma). `TotalRequestTimeout`, alarm üretiminin bekleyebileceği **en uzun süreyi** belirler. API anahtarı yalnızca istek başlığına konur, hiçbir log satırına yazılmaz.

**`Reputation/AbuseIpDbReputationService`:** En iyi çaba (best effort) ilkesi — skor gelirse alarmı zenginleştirir, gelmezse hiçbir şeyi bozmaz. Hiçbir hata dışarı sızmaz, dönüş en kötü ihtimalle `null`.

Üç ayrı korumaya sahip:
- **Anahtar yoksa** servis sessizce devre dışı kalır ve açılışta bir bilgilendirme yazar. Sistem itibar bilgisi olmadan çalışmayı sürdürür; bu bonus bir yetenektir, zorunluluk değil.
- **Genel internete ait olmayan adresler sorgulanmaz.** Özel ağ (10.x, 172.16-31.x, 192.168.x), loopback, link-local, CGNAT ve IPv6 karşılıkları elenir. AbuseIPDB bunlar için anlamlı sonuç dönmez; sorgulamak günlük kotayı boşa harcar.
- **429 (rate limit) ayrı ele alınır** ve açıklayıcı bir uyarı yazılır.

**Önbellek:** Kendi `MemoryCache` örneği, `SizeLimit` ile sınırlı. **Mutlak süre** (`AbsoluteExpirationRelativeToNow`) kullanılır, kayan süre değil — bu bilinçli bir tercih: skor belirli bir ana ait bir olgudur, kayan süre sık görülen bir adres için eskimiş skoru süresiz taze tutardı. Yalnızca başarılı sorgular önbelleğe alınır; başarısızlar alınsaydı geçici bir kesinti bir saat boyunca skorsuz kalmaya yol açardı.

**Bağlantı:** `TrafficAnomalyDetectionService`, alarmı oluşturmadan önce skoru sorar. Skor geldiyse hem `AbuseConfidenceScore` kolonuna hem de açıklama metnine işlenir; böylece mevcut panelde ek bir UI değişikliği olmadan görünür.

### Doğrulama

Gerçek AbuseIPDB anahtarı olmadığı için başarı yolu, AbuseIPDB'nin `/check` yanıtını taklit eden ve gelen istekleri sayan yerel bir test sunucusuyla doğrulandı.

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| Migration | `AbuseConfidenceScore` kolonu nullable olarak eklendi |
| **Anahtar tanımsız** | Bilgilendirme loglandı, alarm üretildi, skor `NULL` |
| **İtibar servisi ULAŞILAMIYOR** | **Alarm üretildi (201), skor `NULL`, süre 5224 ms** — `TotalRequestTimeout` sınırında |
| **Devre kesici** | Ardışık hatalardan sonra devre açıldı, süre **5224 ms → ~100 ms**'ye düştü, 7 alarmın hepsi oluştu |
| Başarılı sorgu | Skor 100 ve 42 doğru okundu, kolona ve açıklamaya işlendi |
| **Önbellek** | ~102 alarm üretildi, dış servise yalnızca **2 istek** gitti |
| **Özel ağ adresi** (192.168.1.50) | Hiç sorgulanmadı (test sunucusunun sayacında yok), skor `NULL` |

**Önbellek testindeki 2 istek üzerine:** Testte alarm cooldown'ı sıfırlanarak 101 eşzamanlı istek gönderildi; iki iş parçacığı önbellek dolmadan aynı anda ıskaladı ("cache stampede"). Üretimde varsayılan 5 dakikalık cooldown bu durumu pratikte imkânsız kılıyor. Tek istek garantisi için anahtar bazlı kilit (single-flight) eklenebilirdi; 102 yerine 2 istek kota koruması için fazlasıyla yeterli olduğundan bu karmaşıklık eklenmedi.

### Öğrenilen kavramlar
- **En iyi çaba (best effort) entegrasyonu**: dış servis bir *zenginleştirme*dir, bağımlılık değil. Ana akış onsuz da doğru sonuç üretebilmelidir.
- **Zaman bütçesi**: "bloklamasın" mutlak bir ifade değildir; pratikte "sınırlı sürede pes etsin" demektir. `TotalRequestTimeout` bu sınırı tek yerden garanti eder.
- **Circuit breaker'ın asıl değeri**: ilk hata 5 saniyeye mal olur, sonrakiler 100 ms'ye. Dış servis çöktüğünde sistem her seferinde aynı bedeli ödemez.
- **Nullable'ın anlamı**: "veri yok" ile "değer sıfır" ayrımı. Sıfırla doldurmak, bilgi eksikliğini olumlu bir bulguymuş gibi gösterir.
- **Mutlak vs kayan süre**: önbellekte hangisinin seçileceği verinin doğasına bağlıdır. Olgular (skor) mutlak, oturum/etkinlik verisi kayan süre ister.
- **Kota bilinci**: ücretsiz tier'larda her çağrı sayılır. Önbellek ve gereksiz sorguların elenmesi (özel IP'ler) işlevsel değil, ekonomik gerekliliktir.
- **Sır yönetimi**: anahtar yalnızca istek başlığında yaşar; konfigürasyonda boş placeholder durur, log'a hiç yazılmaz.

### Notlar / dikkat
- **Gerçek anahtarla henüz denenmedi.** AbuseIPDB'den ücretsiz anahtar alıp `dotnet user-secrets set "Detection:IpReputation:ApiKey" "..."` ile tanımlanmalı. Anahtar olmadan sistem sorunsuz çalışır, yalnızca skor alanı boş kalır.
- Ücretsiz tier günde 1.000 sorgu verir. Önbellek ve özel IP elemesi sayesinde bu limit normal kullanımda sorun olmaz, ama çok sayıda farklı IP'den alarm üreten bir ortamda kota tükenebilir.
- **Yalnızca trafik anomalisi kuralına bağlandı** (prompt'ta böyle istendi). Brute-force alarmlarına eklemek tek satırlık bir değişiklik; başarısız giriş denemelerinde de kaynak IP'nin itibarı bilinmek istenirse yapılabilir.
- Skor alarm kaydında ve açıklama metninde görünüyor; alarm tablosunda ayrı bir sütun olarak gösterilmiyor.
- İtibar sorgusu alarm üretimini **en fazla 5 saniye** geciktirebilir. Bu süre yalnızca önbellekte olmayan bir adres için ve yalnızca dış servis yavaşsa yaşanır. Sıfır gecikme isteniyorsa alarm önce skorsuz kaydedilip arka planda zenginleştirilmelidir; bu iki DB yazımı ve ek karmaşıklık demektir.

---

## Prompt 13 — Telegram bildirimi (2026-09-07)

### Ne istendi
Severity'si High olan her yeni alarm için Telegram Bot API üzerinden (token sır deposundan) önceden tanımlı bir sohbete bildirim gönderen `TelegramNotificationService`. Gönderimi alarm kaydedilme akışına **event-based** bağla, servisi controller'a gömme. Telegram gönderimi başarısız olursa alarmın DB'ye kaydedilmesini etkilemesin; hata sadece loglansın, kayıt geri alınmasın.

### Ne yapıldı

**Olay tabanlı ayrıştırma.** Prompt 12'de itibar sorgusu ana akışta bekletiliyordu (en fazla 5 sn). Burada bu tuzağa düşmemek için gerçek bir ayrıştırma yapıldı:

```
Tespit kuralı → IAlertEventPublisher.Publish()   [senkron, mikrosaniye]
                        ↓
              sınırlı Channel<SecurityAlertDto>
                        ↓
        AlertNotificationWorker : BackgroundService
                        ↓
              IAlertNotifier → TelegramNotificationService
```

`Publish` yalnızca kuyruğa yazıp döner. Gerçek gönderim ayrı bir arka plan servisinde yapılır; dış servisin yavaşlığı isteği **hiç** bekletmez. Kuyruk sınırlıdır (`DropOldest`), dolarsa en eski bildirim loglanarak atılır.

**`AlertRaiser` (refactor).** Her tespit kuralı aynı üç adımı (kaydet, panele yayınla, bildirim kuyruğuna bırak) tekrarlıyordu. Üçüncü adım eklenince bu tekrar riskli hale geldi: yeni bir kural bir adımı unutabilirdi. Adımlar `IAlertRaiser` arkasına toplandı; kurallar artık yalnızca "şu alarmı üret" diyor.

**`IAlertNotifier`.** Worker, kayıtlı tüm bildiricileri dolaşır. E-posta veya webhook eklemek için tek bir DI kaydı yeterli; worker değişmez (OCP).

**Severity eşiği yapılandırılabilir.** Prompt "High" diyor, ama `MinimumSeverity` olarak modellendi (varsayılan `High`). Böylece High **ve** Critical bildirilir. Sabit "yalnızca High" yazılsaydı, ileride Critical üreten bir kural eklendiğinde en ciddi alarmlar sessizce atlanırdı.

**Güvenlik — token sızıntısı.** Telegram token'ı URL yolunda taşınır (`/bot<token>/sendMessage`). HttpClient'ın varsayılan günlükleyicisi istek URI'sini yazdığından **token log dosyalarına sızardı.** Bu istemcide `RemoveAllLoggers()` ile varsayılan günlükleme kapatıldı; gönderim sonucu URI içermeyen kendi log satırlarımızla raporlanıyor. Ek olarak Serilog'da `System.Net.Http.HttpClient` ve `Polly` seviyeleri `Warning`'e çekildi.

**Mesaj biçimi.** HTML parse mode kullanıldığı için dışarıdan gelen değerler (sunucu adı, IP, açıklama) kaçırılıyor; aksi halde `<` içeren bir açıklama mesajı bozardı.

### Doğrulama

Telegram Bot API'sini taklit eden, gelen mesajları ve token'ı kaydeden, istenirse yavaşlayan/hata dönen bir test sunucusu yazıldı.

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| High alarm (brute-force) | Bildirim gönderildi, mesaj doğru biçimlendi |
| **Telegram 30 saniye askıda** | **Alarm isteği 114 ms sürdü** — hiç beklemedi |
| **Telegram 500 hatası** | İstek 112 ms, **alarm DB'de kaldı** (kayıt geri alınmadı) |
| **Medium alarm** (trafik anomalisi) | Bildirim gönderilmedi, sayaç değişmedi |
| **Token log'da geçiyor mu** | **0 kez** — sızıntı yok |
| Token Telegram'a ulaştı mı | Evet (URL'de gitmesi gerekiyor) |

**Yol boyunca bulunan gerçek hata:** İlk denemede gönderim `NotSupportedException: The 'bot123456' scheme is not supported` ile düştü. Sebebi: Telegram token'ı `<bot_id>:<secret>` biçiminde olduğu için **iki nokta içerir**; göreli yol baştaki eğik çizgi olmadan verildiğinde URI ayrıştırıcısı `bot123456`'yı bir şema sanıyor. Yol kök göreli (`/bot...`) hale getirilerek düzeltildi. Bu hata gerçek bir token'la kesinlikle patlardı; test sunucusuna gerçekçi biçimde (iki noktalı) bir token verildiği için yakalandı.

### Öğrenilen kavramlar
- **Olay tabanlı ayrıştırma (event-based decoupling)**: "yan etkiyi ana akıştan ayır" ilkesinin somut hali. Kuyruğa yazmak mikrosaniye sürer; ne kadar yavaş olursa olsun dış servis çağıranı bekletemez.
- **Sınırlı kanal (bounded channel)**: üretici tüketiciden hızlıysa ne olacağına önceden karar vermek. Sınırsız kuyruk, gecikmiş bir çöküştür.
- **Zaman bütçesinin yer değiştirmesi**: bildirim arka planda olduğu için retry/timeout değerleri cömert tutulabildi (toplam 45 sn). Kimseyi bekletmiyorsak ısrarla denemek mantıklıdır — Prompt 12'deki 5 saniyelik cimri bütçenin tam tersi, ve sebebi aynı: kimin beklediği.
- **Sır sızıntısının beklenmedik yolu**: sır yalnızca konfigürasyonda değil, **URL'de de** olabilir. Kütüphanelerin varsayılan günlüklemesi bunu farkında olmadan diske yazar.
- **URI ayrıştırma tuzağı**: göreli bir yolun ilk segmentinde iki nokta varsa şema olarak yorumlanır. Token, kimlik veya zaman içeren yollarda sık rastlanan bir hata.
- **Yapılandırılabilir eşik vs sabit koşul**: "yalnızca High" yazmak bugün doğru, yarın sessiz bir hata. Minimum seviye modellemek her iki durumu da karşılar.

### Notlar / dikkat
- **Gerçek bot ile henüz denenmedi.** BotFather'dan bot oluşturup token ve chat kimliği alınmalı: `dotnet user-secrets set "Notifications:Telegram:BotToken" "..."` ve `"Notifications:Telegram:ChatId" "..."`. Tanımlanmazsa sistem sorunsuz çalışır, yalnızca bildirim gönderilmez.
- Telegram sohbet başına saniyede ~1 mesaj sınırı uygular. Çok sayıda High alarm aynı anda üretilirse 429 alınabilir; bu durumda bildirim düşer (loglanır), alarm kaydı etkilenmez. Yoğun ortamlarda alarmları gruplayıp tek mesajda göndermek gerekebilir.
- Kuyruk bellek içidir: Api yeniden başlarsa gönderilmemiş bildirimler kaybolur. Alarmın kendisi veritabanındadır, yalnızca bildirim kaybolur.
- Şu an tek kanal var. E-posta/webhook eklemek `IAlertNotifier` uygulayıp DI'a kaydetmekten ibaret.

---

## Prompt 14 — Geçmiş raporlama ve CSV dışa aktarma (2026-09-07)

### Ne istendi
`GET /api/reports/summary?serverName=&from=&to=`: belirtilen aralıkta toplam istek, ortalama CPU/RAM ve tipe göre alarm kırılımı. Tarih aralığına makul bir üst sınır (ör. 90 gün) koy ki büyük veri setlerinde sorgu veritabanını kilitleyip sistemi yavaşlatmasın. Panele basit bir rapor ekranı ekle, veriyi tablo halinde göster, CSV dışa aktarma düğmesi koy.

### Ne yapıldı

**Endpoint.** Üç toplama sorgusu: trafik sayımı, metrik ortalamaları, tipe göre alarm kırılımı. Hepsi veritabanında yapılıyor, hiçbir satır belleğe çekilmiyor.

**Veritabanını koruma — üç katman:**
- **90 günlük üst sınır** (istendiği gibi). Aşılırsa 400 dönüyor, sessizce kırpılmıyor.
- **30 saniyelik komut zaman aşımı.** Beklenmedik biçimde uzayan bir rapor sorgusu, veri yazan agent'ları süresiz bekletmek yerine iptal ediliyor.
- **`AsNoTracking()`** — salt okunur sorguda değişiklik takibi gereksiz maliyet.

**Ortalamalar nullable.** Aralıkta hiç ölçüm yoksa `null` dönüyor, sıfır değil. Prompt 12'deki `AbuseConfidenceScore` kararıyla aynı gerekçe: "ölçüm yok" ile "ortalama sıfır" farklı şeyler; sıfırla doldurmak, veri yokluğunu bir bulguymuş gibi gösterir. Ekranda "Ölçüm yok" yazıyor. Ayrıca `MetricSampleCount` dönülüyor: ortalamanın kaç ölçüme dayandığını bilmek güvenilirliğin göstergesi.

Boş küme için `AverageAsync` sorun çıkarır; onun yerine tek gruba indirgeyen bir projeksiyon kullanıldı. Hiç satır yoksa grup oluşmuyor ve sonuç `null` geliyor.

**Rapor ekranı.** Panel artık iki sekmeli: **Panel** ve **Rapor**. Rapor ekranında tarih aralığı seçicileri, sunucu seçici (mevcut `ServerSelectionService` yeniden kullanıldı), tablo ve CSV düğmesi var. Aralık sınırı **istemcide de** kontrol ediliyor: 90 günü aşan veya ters bir aralıkta düğme pasifleşiyor ve sebep yazılıyor — kullanıcı hatayı sunucuya gidip dönmeden görüyor.

**CSV dışa aktarma.** Tablo satırları ve CSV **aynı kaynaktan** (`rows` computed) üretiliyor; ekranda görünen ile dosyaya yazılan birbirinden ayrışamaz.

İki pratik ayrıntı:
- **UTF-8 BOM** eklendi. Excel bu işaret olmadan dosyayı yerel kod sayfasıyla açar ve Türkçe karakterler bozulur.
- **Noktalı virgül ayırıcı.** Excel'in Türkçe yerel ayarında virgül ondalık ayırıcıdır; virgülle ayrılmış dosya tek sütunda açılır.
- Alanlar RFC 4180'e göre kaçırılıyor (ayırıcı/tırnak/satır sonu içeren değerler tırnaklanıyor, içteki tırnaklar ikileniyor).

### Doğrulama

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| Varsayılan çağrı (7 gün) | 500 istek, CPU 40, RAM 60, 500 ölçüm, 4 alarm (2+2 kırılım) |
| `serverName=web-01` | 250 istek, CPU **20**, RAM **40**, 3 alarm — ekilen veriyle birebir |
| `serverName=db-01` | 250 istek, CPU **60**, RAM **80**, 1 alarm |
| **Veri olmayan aralık (2020)** | İstek 0, ortalamalar **null** (sıfır değil), ölçüm 0 |
| **120 günlük aralık** | 400 — "Tarih aralığı en fazla 90 gün olabilir." |
| **`from > to`** | 400 |
| Tam 90 gün | Geçti (sınır dahil) |
| Ekranda sunucu filtresi | db-01 seçilince tablo 250/60/80'e döndü |
| **CSV içeriği** | Dosya adı, MIME tipi, noktalı virgül ayırıcı ve satırlar doğru |
| **CSV baytları** | İlk üç bayt `EF BB BF` — BOM doğru |
| **Arayüz sınırı: 120 gün** | Hata mesajı çıktı, düğme pasifleşti |
| **Arayüz: ters aralık** | Hata mesajı çıktı, düğme pasifleşti |
| Arayüz: geçerli 37 gün | Hata yok, düğme aktif |
| Sekme geçişi Panel ↔ Rapor | Yönlendirme ve aktif sekme doğru |
| Konsol | Hata yok |

**Test sırasında düzeltilen bir yanılgı:** CSV'de BOM'u `Blob.text()` ile kontrol ettim ve "yok" sonucu aldım. Sebep koddaki bir eksiklik değil: `Blob.text()` spec gereği baştaki BOM'u kırpıyor. Baytlara doğrudan bakınca `EF BB BF` göründü. Yanlış bir test, doğru koda "hatalı" dedirtebiliyor.

### Öğrenilen kavramlar
- **Toplama sorgularının maliyeti**: `COUNT`/`AVG` tüm aralığı taramak zorundadır. Sayfalamayla sınırlanamaz; tek koruma aralığın kendisini sınırlamaktır.
- **Komut zaman aşımı**: sorgu süresini sınırlamak, kilitlerin ne kadar tutulacağını da sınırlar. Yazma yapan sistemlerde okuma sorgusunun süresi başkasının problemi olur.
- **Boş kümenin ortalaması yoktur**: `AVG` boş kümede `NULL` döner. Bunu sıfıra çevirmek veriyi çarpıtır.
- **Örnek sayısını da vermek**: "%40 ortalama" tek başına eksik bilgidir; 3 ölçüme mi 3000 ölçüme mi dayandığı kararı değiştirir.
- **Çift taraflı doğrulama**: aynı kural hem istemcide (hızlı geri bildirim) hem sunucuda (asıl koruma) uygulanır. İstemci doğrulaması bir kolaylıktır, güvenlik sınırı değildir.
- **Tek kaynaktan üretim**: ekrandaki tablo ile CSV aynı diziden türetildiğinde "ekranda başka, dosyada başka" hatası yapısal olarak imkânsız hale gelir.
- **CSV'nin yerelleşmesi**: BOM ve ayırıcı seçimi teknik değil kullanıcı sorunudur. Excel'de bozuk açılan bir dosya, üretilmemiş sayılır.

### Notlar / dikkat
- Rapor **anlık hesaplanıyor**, önbelleklenmiyor. Tablolar büyüdükçe 90 günlük bir sorgu yavaşlayabilir; o noktada günlük özetleri önceden hesaplayıp saklamak (rollup tablosu) gerekir.
- Özet şu an **sabit alanlar** içeriyor. Sunucu bazında kırılım, saatlik dağılım veya en çok alarm üreten IP'ler gibi ayrıntılar yok.
- CSV **tarayıcıda** üretiliyor; ekranda görünen özetin birebir kopyası. Ham kayıtları (tüm trafik satırları) dışa aktarmak isteniyorsa bu, sunucu tarafında akış (streaming) gerektirir — tarayıcıda üretilemez.
- Tarih seçicileri **yerel gün** sınırlarını kullanıyor: başlangıç 00:00:00, bitiş 23:59:59.999 olarak UTC'ye çevriliyor. Sunucuların farklı zaman diliminde olduğu bir kurulumda bu ayrıntı gözden geçirilmeli.

---

## Prompt 15 — SIEM/izleme eksiklerinin tamamlanması (2026-09-10)

### Neden

Panel ilk kez gerçek bir sunucudan (`YLNSERVER`, 14 IIS mikroservisi) veri almaya başladıktan sonra
elimizdeki veriye bakıldığında ciddi bir boşluk ortaya çıktı:

| Saat | İstek | 500 hatası | Oran |
|---|---|---|---|
| 08:00 | 27 | 12 | %44 |
| **11:00** | **92** | **76** | **%83** |

93 hatanın tamamı tek bir endpoint'ten geliyordu (`/services/kanban/Synchronize/Webhook`,
ortalama 4,3 sn, en yavaş 22 sn). **Panel bunların hiçbirini göstermiyordu**; ekranda sadece
yeşil bir toplam trafik çizgisi vardı.

Eksiklik veri toplamada değil sunumdaydı: `StatusCode` ve `ResponseTimeMs` zaten toplanıyor,
ekranda hiç kullanılmıyordu.

### Ne yapıldı

**1. Sunucu ayakta mı (en kritik eksik).** Bir agent susarsa kartı eski veriyle ekranda durmaya
devam ediyordu. `ServerHealthStatus` (Online / Stale / Offline) eklendi; karar son görülme
zamanından **sunucu tarafında** verilir — istemcilerin saatleri kaymış olabilir ve aynı sunucu
iki panelde farklı görünmemelidir. Eşikler `Monitoring:ServerHealth` altında yapılandırılır
(varsayılan 60 sn gecikme, 3 dk erişilemez).

**2. Hata oranı ve gecikme.** `TrafficTimelinePointDto` artık 2xx/4xx/5xx sayaçlarını ayrı taşıyor;
grafik yığılmış alan grafiğine dönüştü. Toplam istek eğrisi tek başına yanıltıcıdır: hepsi hata
dönen bir servis, sağlıklı bir servisle aynı görünür.

**3. Servis bazında sağlık.** `GET /api/traffic/services` istek yolunun ilk iki segmentinden servis
adı türetir (`/services/kanban/Sync/Webhook` → `/services/kanban`) ve istek sayısı, 4xx/5xx, hata
oranı, ortalama/maksimum yanıt süresi döner. En çok 5xx dönen servis en üstte. 14 mikroservisli bir
kurulumda "hangisi bozuldu" sorusunun cevabı budur.

**4. Genel durum özeti.** `GET /api/overview` sunucu durumlarını, trafik sayaçlarını ve alarmları
tek özete indirger. Panelin en üstünde tek cümlelik bir durum çubuğu olarak gösterilir; en kötü
sinyal kazanır. Bu hesap istemciye bırakılsaydı her panel kendi yorumunu üretirdi.

**5. Disk alanı.** Sunucu çökmelerinin en yaygın sebeplerinden biri. Agent artık en dolu sabit
diskin boş alan yüzdesini gönderiyor — ortalama değil **en kötü** disk, çünkü sunucuyu durduran
odur. Alan **nullable**: bu ölçümü göndermeyen eski agent'lar çalışmaya devam eder, panelde
yalnızca "—" görünür.

**6. Bilgi yoğunluğu.** Büyük DevExtreme gauge'lar, iki sayı için çok yer kaplıyordu ve çevrimdışı
durumu ifade edemiyordu. Yerlerine kompakt sunucu kartları geldi: durum noktası, "4 dk önce",
CPU/RAM/disk için satır içi çubuklar. Erişilemeyen sunucu soluk ve kırmızı kenarlıklı görünür.

Renk hiçbir yerde tek başına anlam taşımaz; nokta, kenarlık ve metinle birlikte kullanılır.

### Doğrulama

| Test | Sonuç |
|---|---|
| `dotnet build` / `ng build` / 14 vitest | Hepsi geçti, 0 uyarı |
| Migration (`AddDiskFreePercent`) | Nullable kolon eklendi, mevcut veri korundu |
| `GET /api/servers` | Durum, son görülme saniyesi, CPU/RAM/disk döndü |
| `GET /api/overview` | 72 istek, %0 hata, ort 511 ms, 2 yüksek öncelikli alarm |
| `GET /api/traffic/services` | kanban 65 istek, token ort **5683 ms**, licence ort **3090 ms** |
| **Çökmüş sunucu (10 dk sessiz)** | **Offline**, durum çubuğu kırmızıya döndü, kart soluklaştı |
| **Gecikmiş sunucu (90 sn)** | **Stale**, turuncu |
| Çevrimiçi sunucu (30 sn) | **Online**, yeşil |
| Disk %3 ve %8 | Kırmızı; %62 normal renk |
| Eski agent (disk göndermeyen) | Panelde "—", hata yok |

Servis tablosu ilk açılışta işe yarar bir şey gösterdi: `/services/token` ortalama 5,7 saniye,
`/services/licence` 3,1 saniye. Bunlar daha önce görünmüyordu.

### Öğrenilen kavramlar
- **Veri toplamak ile göstermek ayrı işlerdir.** Toplanan ama gösterilmeyen alan, yokmuş gibidir.
  Hata oranı ve gecikme aylardır kaydediliyordu ve kimse göremiyordu.
- **Verinin yokluğu da bir sinyaldir.** Canlı akış "sunucu çöktü" diyemez; çünkü çöken sunucu
  hiçbir şey göndermez. Bunu ancak periyodik bir kontrol fark eder.
- **Yorumu sunucuda yapmak.** Durum kararı istemcide verilseydi, saat farkları yüzünden aynı sistem
  iki ekranda farklı görünebilirdi.
- **Nullable alan ile sürüm uyumu.** Yeni bir metrik eklemek, sahadaki eski agent'ları bozmamalı.
  `null` "bu agent göndermiyor" demektir ve panelde dürüstçe "—" olarak gösterilir.
- **En kötü değeri raporlamak.** Disklerin ortalaması anlamsızdır; sunucuyu durduran, dolan diskdir.
- **İzleme ekranında yoğunluk.** Süslü gösterge yerine piksel başına daha çok sinyal; ve her
  durumun (çevrimdışı, veri yok, yükleniyor) görsel bir karşılığı olması.

### Notlar / dikkat
- **Sahadaki agent güncellenmeli.** `YLNSERVER`'daki agent disk ölçümü göndermiyor; yeni paket
  kopyalanıp servis yeniden başlatılana kadar disk sütunu boş kalacak.
- Servis adı, istek yolunun ilk **iki** segmentinden türetiliyor. Farklı bir yol düzeni kullanan
  ortamlarda `MonitoringConstraints.ServiceNameSegmentCount` ayarlanmalı.
- Gecikme ölçütü olarak ortalama ve maksimum kullanılıyor. p95/p99 daha iyi olurdu ama EF Core
  `PERCENTILE_CONT` çeviremiyor; ham SQL gerekirdi.
- **Alarm durumu (yeni/görüldü/çözüldü) hâlâ yok.** Gerçek bir SIEM'de alarmların yaşam döngüsü
  vardır; bizimkiler tek seferlik. Sıradaki en değerli eksik bu.
- Servis satırına tıklayıp detaya inme (drill-down) yok.
- Uygulama havuzu (app pool) durumu ve Windows servis durumu toplanmıyor.

---

## Prompt 16 — Üretime hazırlık: kimlik doğrulama, dayanıklılık ve doğrulama aracı (2026-09-10)

### Ne istendi

> "10 yıllık senior bir developersin. Bu geliştirmelerin hepsini sonrasında hata yaşamayacağımız
> şekilde düzenle ve en güvenli hale getir. Proje prod ortamına çıkacak hale gelsin. Eksik kalmasın.
> Projeye harici bir test servisi de yazabilirsin ileride bir sorun var mı diye ona istek atarak
> anlayabiliriz."

### Neden

Yayına geçmeden önce yapılan incelemede üç sınıf eksik vardı. İlki güvenlikti ama diğer ikisi
**sistemin ayakta kalmasıyla** ilgiliydi ve en az onun kadar kritikti:

| # | Bulgu | Sonucu |
|---|---|---|
| 1 | Hiçbir uçta kimlik doğrulama yok | Ağdaki herkes alarmları okuyabilir, sahte veri yazabilir |
| 2 | Log yalnızca konsola yazıyor | IIS altında konsol hiçbir yere gitmez; sorun anında hiç kayıt olmaz |
| 3 | Veri temizleme yok | Disk dolunca SQL durur, **izleme sisteminin kendisi çöker** |
| 4 | Hız sınırlama yok | Bozuk bir döngü veya kötü niyetli istek API'yi ve veritabanını tüketebilir |
| 5 | CORS `localhost:4200`'e sabit | Yayında panel çalışmaz |
| 6 | HTTPS kararı verilmemiş | Sertifikasız açılırsa agent'lar bağlanamaz |

Ölçülen büyüme, 3. maddenin ne kadar somut olduğunu gösteriyordu: **tek** sunucudan 3 günde 2310
metrik, 1910 güvenlik olayı, 1308 trafik kaydı.

### Ne yapıldı

**1. İki ayrı yetki yolu.** Agent'lar `X-ServerGuard-Key` header'ıyla yalnızca **yazar**, panel
`Bearer` token'ıyla yalnızca **okur**. Ayrım bilinçli: agent anahtarı sızsa alarmlar okunamaz,
panel token'ı sızsa sahte metrik yazılamaz. Her endpoint bir politikaya bağlandı
(`Ingest` / `Panel`); açık kalan tek uçlar `/health`, `/health/ready` ve `/api/auth/login`.

SignalR hub'ı da aynı yetkiyi ister. Tarayıcı WebSocket el sıkışmasında header gönderemediği için
token sorgu parametresiyle taşınır; bu kabul **yalnızca hub yoluna** tanındı, aksi halde token'lar
erişim log'larına ve tarayıcı geçmişine sızardı.

**2. Panel girişi.** Kullanıcı adı + parola, karşılığında 8 saatlik JWT. Parolalar PBKDF2-SHA256
(210.000 yineleme) özeti olarak saklanır, düz metin hiçbir yerde yok. Kaba kuvvete karşı üç katman:
hesap kilidi (5 denemede 15 dk), IP başına dakikada 10 deneme, ve pahalı özet.

Var olmayan kullanıcı için de aynı hesaplama yapılır ve aynı yanıt döner — hangi kullanıcı adlarının
geçerli olduğu yanıt süresinden anlaşılamasın diye.

**3. Açılışta fail-fast.** Production'da eksik güvenlik yapılandırmasıyla API **açılmaz** ve hangi
ortam değişkeninin eksik olduğunu tek tek yazar. Geliştirmede açılır ama her eksiği uyarır; depoyu
yeni klonlayan biri projeyi çalıştırabilsin diye.

Agent tarafında da aynısı: `Agent:ApiKey` boşsa agent hiç açılmaz. Anahtarsız bir agent tek bir
kaydı bile teslim edemez; sessizce çalışıp veri kaybetmesindense açılışta durup sebebini yazması yeğdir.

**4. Dosyaya log.** API ve agent artık kendi klasörlerindeki `logs` dizinine günlük döndürülen
dosya yazar. Yol **mutlak** olarak, uygulamanın kendi klasörüne göre hesaplanır: Serilog göreli
yolları sürecin çalışma dizinine göre çözer ve bu dizin IIS altında ya da Windows hizmetinde
(`C:\Windows\System32`) beklenmedik bir yer olabilir.

**5. Veri saklama.** Süresi dolan kayıtlar 6 saatte bir, 5.000'lik partiler hâlinde silinir; tek bir
uzun DELETE tabloyu kilitlemez. Süreler tablo bazında ayrı: metrik 30 gün, trafik 30 gün, olay 90
gün, alarm 365 gün.

Silme, agent'ın gönderdiği `Timestamp` yerine sunucunun yazdığı `CreatedAt` alanına göre yapılır —
saati yanlış bir agent kayıtlarını kalıcı hale getiremesin diye. Bunun için dört tabloya `CreatedAt`
indeksi eklendi (`AddRetentionIndexes`); indekssiz her tur tablonun tamamını tarardı.

**6. Hız sınırlama.** İstemci başına ayrılmış sayaçlar: kimliği doğrulanmış istekler agent/kullanıcı
adına, doğrulanmamış istekler kaynak IP'ye göre. Sınıra takılan istek `429` ve `Retry-After` alır;
agent bunu **geçici** sayar, kaydı atmaz, kuyrukta tutar.

**7. Tarayıcı savunmaları ve HTTPS.** Her yanıta CSP, `X-Frame-Options`, `X-Content-Type-Options`,
`Referrer-Policy`, `Permissions-Policy` eklenir. `Security:RequireHttps` açıkken HTTPS yönlendirmesi
ve HSTS devreye girer — **varsayılan kapalı**, çünkü sertifika hazır olmadan açılırsa agent'lar
güvenilmeyen sertifika yüzünden bağlanamaz ve HSTS geri alınması zor bir iz bırakır.

**8. Panel API ile aynı kaynaktan.** Angular çıktısı API'nin `wwwroot`'una kopyalanıyor. Tek IIS
sitesi, tek sertifika, CORS'a hiç gerek yok ve token başka bir kaynağa gitmiyor. CORS yalnızca
`ng serve` için duruyor; production'da loopback adresleri listeden sessizce eleniyor.

**9. Çok siteli IIS trafik toplama.** Agent tek bir klasör izliyordu; sahada 14 site vardı, yani
trafiğin çoğu görünmüyordu. `LogRoot` verildiğinde altındaki tüm `W3SVC*` klasörleri izleniyor ve
sonradan açılan siteler 10 dakika içinde yakalanıyor. Her klasörün okuma konumu ayrı tutuluyor;
eski tek klasörlü konum dosyası açılışta otomatik dönüştürülüyor.

Kuyruk klasörler arasında paylaşıldığından bir turda klasörler **sırayla** işleniyor: bir klasörün
kayıtları teslim edilmeden diğerine geçilmiyor. Bu kural olmadan, teslim edilemeyen kayıtlar başka
bir klasörün konumunu ilerletebilir ve o klasörün satırları kaybolabilirdi.

**10. Harici doğrulama aracı (`ServerGuard.Tools`).** Çalışan bir kurulumu dışarıdan kontrol eder.
Yalnızca "cevap veriyor mu" değil, **"kapılar kapalı mı"** sorusunu da yanıtlar: token'sız okuma ve
anahtarsız yazma denemelerinin *reddedilmesi* beklenir. Yetkilendirme yanlışlıkla kaldırılırsa araç
bunu hemen bildirir.

Hiçbir kontrol veritabanına kayıt yazmaz. Agent anahtarı kasıtlı olarak geçersiz bir gövdeyle
denenir: anahtar geçerliyse doğrulama hatası (400), geçersizse yetki hatası (401) döner. İki durum
ayırt edilir ve veri kirlenmez.

Araç ayrıca kurulum sırlarını üretir (`hash-password`, `new-key`); parola ekrana yazılmadan sorulur.

**11. Yayınlama betiği.** `deploy\Yayinla.ps1` paneli derler, `wwwroot`'a kopyalar, üç projeyi
yayınlar ve **paket içinde sır kalmadığını doğrular** — bir `appsettings.json` içinde dolu bir sır
bulursa işlem durur. Bu kontrol daha önce elle yakalanan bir hatanın (düz metin `sa` parolası)
tekrarını engeller.

**12. Agent güncelleme betiği düzeltildi.** Eski betik `appsettings.json` dosyasının **üzerine
yazıyordu**: her güncellemede sunucuya özel ayarlar kayboluyordu. Yeni betik ayar dosyasını ve okuma
konumunu korur; yeni zorunlu ayarları (`-ApiKey`, `-LogRoot`) mevcut dosyaya ekler ve yazdığı JSON'u
servis başlamadan önce doğrular.

### Doğrulama

Her adım gerçek verilerle sınandı; hiçbiri "derleniyor, herhalde çalışır" diye bırakılmadı.

| Test | Sonuç |
|---|---|
| `dotnet build` (4 proje) / `ng build` | 0 hata, 0 uyarı |
| Token'sız `GET /api/servers` | **401** |
| Anahtarsız `POST /api/metrics` | **401** |
| Token'sız hub `negotiate` | **401** |
| Doğru anahtarla ingest | **400** (gövde doğrulaması) → anahtar kabul edildi |
| Hatalı parola × 5 | 5. denemede **hesap kilitlendi**, sonrakiler parola hiç kontrol edilmeden reddedildi |
| Hatalı parola × 10+ | **429** (IP başına hız sınırı) |
| Panel: giriş → panel → çıkış | Çalıştı; çıkışta hub kapandı, oturum silindi |
| Panel: geçersiz token ile açılış | Otomatik `/login?returnUrl=/` yönlendirmesi |
| Retention: 12.000 süresi dolmuş satır | 3 partide **silindi**, gerçek veri (2310+1308+1910) **korundu** |
| Production'da sırsız açılış | **Açılmadı**; eksik 3 ortam değişkenini tek tek yazdı |
| Agent: `ApiKey` boş | **Açılmadı**, ne yapılacağını yazdı |
| Agent: yanlış anahtar | Kayıt **atılmadı**, kuyrukta tutuldu; dakikada bir açıklayıcı hata |
| Çok siteli trafik (3 klasör × 10 satır) | 3 klasör de toplandı, konum dosyasında 3 ayrı kayıt |
| Aynı dosyaya 5 satır eklendi | Yalnızca yeni 5 satır okundu; **mükerrer yok** (15 satır = 15 tekil) |
| Eski tek klasörlü konum dosyası | Dönüştürüldü; W3SVC1 kaldığı yerden, diğerleri baştan |
| Backend kapalıyken agent | Hiçbir konum ilerlemedi; API dönünce **45 satırın tamamı bir kez** yazıldı |
| Yayınlanmış paket + Production | Panel `wwwroot`'tan servis edildi, giriş ve canlı akış çalıştı |
| Yayınlanmış agent → yayınlanmış API | Metrikler ulaştı, agent log dosyası oluştu |
| BOM'lu `appsettings.json` (Guncelle.ps1'in yazdığı biçim) | Sorunsuz okundu |
| `ServerGuard.Tools check` | **10 başarılı, 0 başarısız**, 2 uyarı (YLNSERVER çevrimdışı) |

### Yol boyunca bulunan gerçek hatalar

Bunlar planlanan iş değildi; test ederken ortaya çıktı.

**1. Production'da panel adresi bozuktu.** `environment.production.ts` içinde `apiBaseUrl: '/'`
yazıyordu. Yollar zaten `/api/...` ile başladığından sonuç `//api/servers` oluyor ve tarayıcı bunu
**`api` adlı başka bir sunucu** sanıyor. Değer boş dizeye çekildi. Bu hata yalnızca yayında ortaya
çıkardı; geliştirmede tam adres kullanıldığı için hiç görünmüyordu.

**2. CSP, DevExtreme temasını tamamen devre dışı bırakıyordu.** Angular'ın `inlineCritical`
eniyilemesi stil dosyasını `<link media="print" onload="this.media='all'">` ile bağlıyor. CSP satır
içi olay işleyicilerini engellediği için `onload` hiç çalışmıyor ve **694 KB'lık global stil
uygulanmadan kalıyordu**. Tarayıcıda `dx-widget` sınıfının hesaplanan yazı tipine bakınca görüldü.
Çözüm: production yapılandırmasında `inlineCritical: false`. Yayınlanan paketle yeniden doğrulandı.

**3. Yayınlanan agent hiç açılmıyordu.** Serilog paketleri 10.x sürümünden geldiği için derlenen
kod `Microsoft.Extensions.Hosting.Abstractions` **10.0.0**'ı istiyordu; pakete kopyalanan dosya ise
projenin geri kalanıyla uyumlu **9.0.19**'du. Sonuç: `FileNotFoundException`, açılışta.

`dotnet build` ve `dotnet run` bunu göstermiyordu — yalnızca **yayınlanmış paket** çalıştırıldığında
ortaya çıkıyordu. Serilog paketleri .NET 9 ile aynı sürüm hattına (9.0.0) çekildi.

**4. Yayın klasörü kendiliğinden temizlenmiyor.** `dotnet publish -o`, hedef klasördeki eski
dosyaları silmez. Önceki yayından kalan 10.0.0 sürümlü bir DLL, sürüm düşürüldükten sonra bile
pakette kalmaya devam etti ve hatayı sürdürdü. `Yayinla.ps1` artık her yayında hedef klasörü
sıfırlıyor — eski bir sürümün artığının sunucuya taşınması, bulunması en zor hatalardandır.

**5. Agent güncelleme betiği ayarları siliyordu** (yukarıda madde 12).

**6. `MaxTrackedDirectories` isim çakışması** — sabit ve özellik aynı adı taşıyordu; derleme hatası,
hemen düzeltildi.

### Öğrenilen kavramlar
- **Kimlik doğrulama tek bir kapı değildir.** Yazma ve okuma ayrı yetkiler ister; ikisini tek
  anahtara bağlamak, anahtarlardan biri sızdığında kaybı ikiye katlar.
- **Fail-fast, sessiz çalışmaktan iyidir.** Eksik yapılandırmayla yarı çalışan bir izleme sistemi,
  hiç açılmayandan tehlikelidir: "veri neden gelmiyor?" sorusu haftalar sonra sorulur.
- **Geçici hata ile kalıcı hatayı ayırmak.** 401 "veri bozuk" demek değil, "yapılandırma yanlış"
  demektir. Kaydı atmak veriyi kalıcı olarak kaybettirir; kuyrukta tutmak, anahtar düzeltilince
  birikmiş kayıtları kurtarır.
- **Sunucu saatine güvenmek.** Saklama süresi agent'ın gönderdiği zaman damgasına dayansaydı, saati
  ileri alınmış bir agent kayıtlarını sonsuza kadar yaşatabilirdi.
- **Göreli yol, ortam değişince kayar.** Log yolu mutlak olmalı; izleme aracının kendi log'unun
  nerede olduğu belirsiz olamaz.
- **Güvenlik önlemi başka bir şeyi bozabilir.** CSP doğru bir önlemdi ama panelin stilini kapattı.
  Önlem eklemek yetmez, eklendikten sonra sistemin hâlâ çalıştığı **görülmelidir**.
- **Test aracı, olumsuz durumu da sınamalıdır.** "200 döndü" yetmez; "401 dönmesi gerekiyordu ve
  döndü" asıl kanıttır.
- **Derlenen kod ile yayınlanan paket aynı şey değildir.** `dotnet run` çalışıyor diye paket de
  çalışacak diye bir kural yok; paket ayrı bir bağımlılık çözümlemesiyle oluşur ve **ayrıca**
  denenmelidir.
- **Paket klasörü birikimlidir.** Temizlenmeyen bir çıktı klasörü, eski sürümün dosyalarını
  sessizce yeni pakete taşır.
- **Paylaşılan kaynak, sıra kuralı gerektirir.** Çoklu klasör izlemede tek kuyruk paylaşıldığından,
  bir klasörün teslim edilmemiş kayıtları başka bir klasörün konumunu ilerletmemelidir.

### Notlar / dikkat
- **DevExtreme lisansı hâlâ uygulanmadı**; panelin üstünde deneme bandı görünüyor. Yayından önce
  lisans anahtarı girilmelidir.
- **Sahadaki agent güncellenmeli.** `YLNSERVER` hâlâ eski sürüm; yeni pakette `Guncelle.ps1`
  ayarları koruyarak günceller ve `-ApiKey` ile anahtarı ekler.
- Token iptali yok. Bir kullanıcının erişimini anında kesmek için imza anahtarı değiştirilir; bu
  tüm oturumları düşürür. Küçük bir ekip için kabul edilebilir, kullanıcı sayısı artarsa gözden
  geçirilmeli.
- Panel token'ı `localStorage`'da tutuluyor. XSS durumunda okunabilir; CSP satır içi script'i
  engelleyerek bu riski azaltır ama sıfırlamaz.
- Hız sınırı sayaçları bellek içidir. Tek örnekte doğru çalışır; API çoğaltılırsa paylaşılan bir
  sayaç (ör. Redis) gerekir.
- **Alarm yaşam döngüsü (yeni/görüldü/çözüldü) hâlâ yok** — Prompt 15'ten devreden en değerli eksik.
- Servis satırına tıklayıp detaya inme (drill-down) ve uygulama havuzu durumu toplama hâlâ yok.
