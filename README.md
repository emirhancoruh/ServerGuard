# ServerGuard

Kendi sunucularımızı izlemek için geliştirilen monitoring sistemi: CPU/RAM yükü, HTTP trafiği,
başarısız/başarılı oturum açma girişimleri (saldırı tespiti) ve sunucu erişilebilirliği.


## Mimari

```
┌──────────────────┐  X-ServerGuard-Key  ┌──────────────────┐      ┌──────────┐
│ ServerGuard.Agent│ ──────────────────► │  ServerGuard.Api │ ───► │  MSSQL   │
│ (her sunucuda)   │                     │ (merkezi backend)│      └──────────┘
└────────┬─────────┘                     └───┬────────┬─────┘
         │                                   │        │ SignalR (canlı yayın)
         └──────► ServerGuard.Shared ◄───────┘        ▼
                  (DTO / sözleşmeler)          ┌──────────────────┐
                                    Bearer JWT │ ServerGuard.Web  │
                                    ◄───────── │ (Angular panel)  │
                                               └──────────────────┘
```

Agent'lar API anahtarıyla **yazar**, panel oturum token'ıyla **okur**. İki yol ayrıdır: agent
anahtarı hiçbir sorgu ucunu açmaz, panel token'ı hiçbir veri yazamaz.

| Proje | Tür | Sorumluluk |
|---|---|---|
| `ServerGuard.Agent` | Worker Service | Sunucu üzerinde çalışır; metrik, güvenlik olayı ve trafik verisini toplayıp Api'ye gönderir. |
| `ServerGuard.Api` | ASP.NET Core Web API | Agent'lardan veri alır, EF Core ile MSSQL'e yazar, SignalR ile panele canlı yayınlar. |
| `ServerGuard.Shared` | Class Library | Agent ile Api arasında paylaşılan `record` DTO'lar, enum'lar, endpoint sözleşmesi (`ApiRoutes`) ve doğrulama sınırları (`MetricConstraints`). |
| `ServerGuard.Web` | Angular 21 | Monitoring paneli. Hub'a bağlanır, her sunucu için CPU/RAM gauge'larını canlı günceller. |
| `ServerGuard.Tools` | Konsol uygulaması | Kurulum sırlarını üretir ve çalışan bir API'yi dışarıdan doğrular. Sistemin çalışması için gerekli değildir. |

### Kapsam: neyi izliyoruz, neyi izlemiyoruz

Beklenti kurmak için açıkça yazmak gerekir. Agent kurulu her sunucuda **toplananlar**:

| Veri | Kapsam |
|---|---|
| CPU / RAM / en dolu diskin boş alanı | Makinenin tamamı — servis bazında değil |
| Başarılı ve başarısız oturum açma (4624/4625) | Makinenin tamamı |
| HTTP trafiği: istek yolu, durum kodu, yanıt süresi, istemci IP | **Tüm IIS siteleri** (`Agent:Traffic:LogRoot` dolduğunda) |

**Toplanmayanlar** — bunları bu sistemden beklemeyin:

| Eksik | Sonucu |
|---|---|
| İsteğin hangi IIS **sitesine** ait olduğu | Siteler yalnızca istek yoluna göre ayrışır; aynı yolu kullanan iki site tek satırda birleşir |
| Servislerin kendi uygulama log'ları | HTTP 500 sayısı görünür, ama hatanın sebebi görünmez |
| Uygulama havuzu / Windows servis durumu | "Havuz durdu" bilgisi yok; yalnızca trafiğin kesilmesinden dolaylı anlaşılır |
| Süreç bazında CPU/RAM | "Hangi servis CPU yiyor" sorusunun cevabı yok |
| SQL Server metrikleri | Yok |

IIS trafiğinin toplanabilmesi için her sitenin log biçimi **W3C** olmalı ve `time-taken` alanı
seçili olmalıdır; biri eksikse o sitenin satırları atlanır. Kurulum sırasında doğrulama adımı için
bkz. [docs/YAYINLAMA.md](docs/YAYINLAMA.md).

### Bağımlılık yönü

`Agent → Shared ← Api`. Agent ve Api birbirine referans vermez; yalnızca Shared üzerinden aynı sözleşmeyi konuşur.

### Api katmanları

```
Controller  →  IValidator (FluentValidation)  →  IRepository  →  DbContext  →  MSSQL
                                                     ▲
                                        controller DbContext'i görmez
```

- **Global hata yönetimi:** `IExceptionHandler` ile tüm unhandled exception'lar yakalanır, detay Serilog'a, kullanıcıya kısa `ProblemDetails`.
- **Dayanıklılık:** `EnableRetryOnFailure` ile geçici DB kopmalarında otomatik yeniden deneme.
- **Health:** `GET /health` yalnızca sürecin ayakta olduğunu söyler; `GET /health/ready` veritabanına da dokunur.
- **Yetkilendirme:** Her uç bir politikaya bağlıdır — `Ingest` (agent anahtarı) veya `Panel` (oturum token'ı).
- **Hız sınırlama:** İstemci başına ayrılmış sayaçlar; bir agent'ın veya kullanıcının aşırı isteği diğerlerini etkilemez.
- **Loglama:** Konsol ve `logs/` klasöründe günlük döndürülen dosya. IIS altında konsol çıktısı hiçbir yere gitmediğinden dosya zorunludur.

### Endpoint'ler

| Method | Yol | Yetki | Açıklama | Yanıt |
|---|---|---|---|---|
| `GET` | `/health` | açık | Sürecin ayakta olduğunu söyler; dış bağımlılığa dokunmaz | 200 Healthy |
| `GET` | `/health/ready` | açık | Veritabanı erişimini de dener | 200 Healthy / 503 |
| `POST` | `/api/auth/login` | açık | Kimlik doğrular, kısa ömürlü token döner | 200 token / 401 / 400 |
| `GET` | `/api/auth/me` | Panel | Geçerli token'ın sahibini döner | 200 kullanıcı / 401 |
| `GET` | `/api/servers` | Panel | Sunucuları durumu (Online/Stale/Offline), son görülme ve CPU/RAM/disk ile döner | 200 dizi / 400 doğrulama hatası |
| `GET` | `/api/overview` | Panel | Tek bakışta durum özeti: sunucular, hata oranı, gecikme, alarmlar | 200 özet / 400 doğrulama hatası |
| `POST` | `/api/metrics` | Ingest | `ServerMetricDto` kaydeder ve panele yayınlar | 201 `{ id }` / 400 doğrulama hatası |
| `POST` | `/api/security-events` | Ingest | `SecurityEventDto` kaydeder ve panele yayınlar | 201 `{ id }` / 400 doğrulama hatası |
| `GET` | `/api/alerts` | Panel | Alarmları filtreleyip sayfalayarak döner | 200 `PagedResult` / 400 doğrulama hatası |
| `POST` | `/api/traffic` | Ingest | `TrafficLogDto` kaydeder ve panele yayınlar | 201 `{ id }` / 400 doğrulama hatası |
| `GET` | `/api/traffic/timeline` | Panel | İstek sayısını zaman dilimlerine bölerek döner | 200 dizi / 400 doğrulama hatası |
| `GET` | `/api/traffic/top-ips` | Panel | En çok istek gönderen adresler | 200 dizi / 400 doğrulama hatası |
| `GET` | `/api/traffic/services` | Panel | Servis bazında istek, 4xx/5xx, hata oranı ve yanıt süresi | 200 dizi / 400 doğrulama hatası |
| `GET` | `/api/reports/summary` | Panel | Tarih aralığının özeti (istek, ortalama CPU/RAM, alarm kırılımı) | 200 özet / 400 doğrulama hatası |
| `WS` | `/hubs/monitoring` | Panel | SignalR hub. `ReceiveMetric`, `ReceiveSecurityEvent` ve `ReceiveAlert` event'lerini yayınlar | — |

Enum'lar JSON'da adlarıyla taşınır (`"eventType": "FailedLogin"`); sayısal değerler de kabul edilir.

## Kimlik doğrulama ve yetkilendirme

Hiçbir uç açık değildir. İki ayrı yol vardır ve birbirinin yerine geçemez:

| Yol | Kim kullanır | Nasıl taşınır | Ne yapabilir |
|---|---|---|---|
| **Ingest** | Agent | `X-ServerGuard-Key` header'ı | Yalnızca veri yazar |
| **Panel** | Web paneli / kullanıcı | `Authorization: Bearer <token>` | Yalnızca veri okur |

Bu ayrım bilinçlidir: agent anahtarı sızsa bile alarmlarınız okunamaz, panel token'ı sızsa bile
sahte metrik yazılamaz.

Yalnızca `GET /health`, `GET /health/ready` ve `POST /api/auth/login` kimlik doğrulaması istemez.

### Agent anahtarları

Her sunucuya ayrı anahtar verilir; biri sızarsa yalnızca o iptal edilir. Anahtarlar sabit zamanlı
karşılaştırılır ve **hiçbir zaman loglanmaz** — reddedilen istekte yalnızca kaynak adres ve yol yazılır.

Üretmek için:

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name SERVER10
```

| Anahtar (`Security:Ingest`) | Açıklama |
|---|---|
| `ApiKeys:N:Name` | Log'larda görünen tanımlayıcı ad (anahtarın kendisi değil) |
| `ApiKeys:N:Key` | Anahtar — **yalnızca sır deposundan** |

Agent tarafında karşılığı `Agent:ApiKey` ayarıdır. **Anahtar tanımlı değilse agent hiç açılmaz**:
anahtarsız bir agent tek bir kaydı bile teslim edemez, sessizce çalışıp veri kaybetmesindense
açılışta durup sebebini yazması yeğdir.

### Panel oturumu

Kullanıcı adı ve parola ile giriş yapılır, karşılığında kısa ömürlü (varsayılan 8 saat) bir JWT döner.
Parolalar **hiçbir yerde düz metin tutulmaz**; yapılandırmaya yalnızca PBKDF2-SHA256 özeti yazılır.

```bash
dotnet run --project src/ServerGuard.Tools -- hash-password --user admin
```

| Anahtar (`Security:Panel`) | Açıklama | Varsayılan |
|---|---|---|
| `Users:N:UserName` | Giriş adı | — |
| `Users:N:PasswordHash` | PBKDF2 özeti — **yalnızca sır deposundan** | — |
| `MaxFailedAttempts` | Bu sayıda başarısız denemeden sonra hesap kilitlenir | `5` |
| `LockoutDuration` | Kilit süresi | `00:15:00` |
| `TrackedAttemptLimit` | Aynı anda izlenecek en fazla başarısız giriş kaydı | `10000` |

Kaba kuvvete karşı üç katman vardır:

1. **Hesap kilidi** — art arda başarısız denemeden sonra hesap geçici olarak kilitlenir.
2. **IP başına hız sınırı** — dakikada en fazla `RateLimiting:LoginPermitLimit` deneme.
3. **Pahalı özet** — PBKDF2 210.000 yineleme; her deneme ölçülebilir bir maliyet taşır.

Var olmayan bir kullanıcı için de aynı hesaplama yapılır ve aynı yanıt döner; hangi kullanıcı
adlarının geçerli olduğu yanıt süresinden veya mesajından anlaşılamaz.

### Token imzası

| Anahtar (`Security:Jwt`) | Açıklama | Varsayılan |
|---|---|---|
| `SigningKey` | HMAC-SHA256 imza anahtarı, en az 32 karakter — **yalnızca sır deposundan** | — |
| `Issuer` / `Audience` | Token'ın kime ait olduğu | `ServerGuard` / `ServerGuard.Panel` |
| `AccessTokenLifetime` | Token ömrü | `08:00:00` |

Token sunucuda saklanmaz; doğrulama tamamen imzaya dayanır. Bu nedenle tek tek iptal edilemez,
ömrü kısa tutulur. **Tüm oturumları anında düşürmek için imza anahtarını değiştirip API'yi yeniden
başlatın** — acil durumda erişimi kesmenin yolu budur.

> Panel token'ı tarayıcıda `localStorage` içinde tutulur; sayfa yenilendiğinde yeniden giriş
> istenmez. Süresi dolmuş oturum hiç kullanılmaz, sunucu token'ı reddederse panel kendiliğinden
> giriş ekranına döner.

### Yapılandırma eksikse ne olur

Production ortamında eksik güvenlik yapılandırmasıyla **API açılmaz** ve hangi ortam değişkeninin
eksik olduğunu tek tek yazar. Geliştirmede açılır ama her eksiği uyarı olarak loglar; depoyu yeni
klonlayan biri projeyi çalıştırabilsin diye.

### Tarayıcı savunmaları

Her yanıta şu header'lar eklenir: `Content-Security-Policy`, `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`. `Security:RequireHttps` açıkken
`Strict-Transport-Security` de eklenir.

| Anahtar (`Security`) | Açıklama | Varsayılan |
|---|---|---|
| `RequireHttps` | HTTP'yi HTTPS'e yönlendir ve HSTS gönder | `false` |
| `Headers:ContentSecurityPolicy` | İçerik güvenlik politikası | aynı kaynak, satır içi script yok |
| `Headers:HstsMaxAge` | HSTS süresi | `365.00:00:00` |

> `RequireHttps` varsayılan olarak **kapalıdır**. Geçerli bir sertifika kurulmadan açılırsa
> agent'lar güvenilmeyen sertifika yüzünden bağlanamaz ve HSTS geri alınması zor bir iz bırakır.
> Sertifika hazır olduğunda açın.

---

## Hız sınırlama

Sınırlar istemci başına ayrılır: kimliği doğrulanmış istekler agent veya kullanıcı adına,
doğrulanmamış istekler kaynak IP'ye göre sayılır. Böylece bir agent'ın aşırı isteği diğer
sunucuların verisini kesmez.

| Anahtar (`RateLimiting`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Sınırlamayı aç/kapat | `true` |
| `Window` | Sayaçların sıfırlandığı pencere | `00:01:00` |
| `IngestPermitLimit` | Bir agent'ın pencere başına gönderebileceği kayıt | `3000` |
| `PanelPermitLimit` | Bir oturumun pencere başına yapabileceği sorgu | `600` |
| `LoginPermitLimit` | Bir IP'nin pencere başına deneyebileceği giriş | `10` |
| `QueueLimit` | Sınır aşılınca beklemeye alınacak istek | `0` |

Sınıra takılan istek `429` ve `Retry-After` header'ı alır. Agent bunu geçici bir durum sayar:
kaydı **atmaz**, kuyrukta tutar ve sonra tekrar dener.

> Bu bir DDoS koruması değildir. Amaç, tek bir istemcinin API'yi ve veritabanını tüketmesini
> engellemektir.

---

## Veri saklama

İzleme sistemi kendi diskini doldurup çökmemelidir. Süresi dolan kayıtlar arka planda, küçük
partiler hâlinde silinir; tek bir uzun DELETE tabloyu kilitlemez.

| Anahtar (`Maintenance:Retention`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Temizliği aç/kapat | `true` |
| `RunInterval` | Temizliğin sıklığı | `06:00:00` |
| `InitialDelay` | Açılıştan sonra ilk turu bekleme süresi | `00:02:00` |
| `BatchSize` | Tek DELETE ifadesinde silinecek en fazla satır | `5000` |
| `ServerMetrics` | Metrik saklama süresi | `30.00:00:00` |
| `TrafficLogs` | Trafik kaydı saklama süresi | `30.00:00:00` |
| `SecurityEvents` | Güvenlik olayı saklama süresi | `90.00:00:00` |
| `SecurityAlerts` | Alarm saklama süresi | `365.00:00:00` |

Süreler tablo bazında ayrıdır çünkü değerleri farklıdır: metrik verisi hızla değerini yitirir,
güvenlik alarmları adli inceleme için uzun süre gerekir.

Silme, agent'ın gönderdiği `Timestamp` alanına göre değil, sunucunun yazdığı `CreatedAt` alanına
göre yapılır: saati yanlış ayarlanmış veya kötü niyetli bir agent, gelecekteki bir zaman damgası
göndererek kayıtlarını kalıcı hale getiremesin diye.

---

## Loglama

| Bileşen | Yer | Saklama |
|---|---|---|
| API | `<uygulama klasörü>/logs/api-YYYYMMDD.log` | 30 dosya, dosya başına en fazla 50 MB |
| Agent | `<agent klasörü>/logs/agent-YYYYMMDD.log` | 14 dosya, dosya başına en fazla 20 MB |

Dosya yolu **mutlak** olarak, uygulamanın kendi klasörüne göre hesaplanır. Göreli bir yol IIS
altında veya Windows hizmeti olarak çalışırken beklenmedik bir klasöre düşerdi; izleme aracının
kendi log'unun nerede olduğu belirsiz olamaz.

> IIS uygulama havuzu kimliğine `logs` klasörü için yazma yetkisi verilmelidir. Verilmezse uygulama
> çalışır ama hiçbir log tutulmaz. Bkz. [docs/YAYINLAMA.md](docs/YAYINLAMA.md).

Log'lara parola, API anahtarı veya token yazılmaz.

---

## Doğrulama aracı (ServerGuard.Tools)

Çalışan bir kurulumu **dışarıdan** kontrol eder: yalnızca "cevap veriyor mu" değil, "kapılar kapalı
mı" sorusunu da yanıtlar. Yetkilendirme yanlışlıkla kaldırılırsa bu araç hemen bildirir.

```bash
ServerGuard.Tools.exe check --url https://sunucu10:8443 --user admin --ingest-key "..."
```

| Komut | İşi |
|---|---|
| `check` | Sağlık ve güvenlik kurallarını doğrular; başarısız kontrol varsa çıkış kodu `1` |
| `hash-password` | Panel kullanıcısı için PBKDF2 özeti üretir |
| `new-key` | Agent için rastgele API anahtarı üretir |

Parola ve anahtar komut satırı yerine `SERVERGUARD_PASSWORD` / `SERVERGUARD_INGEST_KEY` ortam
değişkenlerinden de okunabilir; böylece kabuk geçmişinde ve süreç listesinde görünmezler.

Hiçbir kontrol veritabanına kayıt yazmaz. Görev Zamanlayıcı'ya günlük görev olarak eklenebilir.

## Saldırı ve anomali tespiti

İki kural çalışır. Her ikisi de alarmı veritabanına yazar ve hub üzerinden `ReceiveAlert` ile yayınlar.

| Kural | Tetikleyici | Alarm |
|---|---|---|
| Brute-force | Aynı sunucu + IP'den pencere içinde eşik kadar **başarısız giriş** | `BruteForceAttempt` / `High` |
| Trafik anomalisi | Aynı sunucu + IP'den pencere içinde eşikten fazla **istek** | `TrafficAnomaly` / `Medium` |

> ### Bu bir DDoS koruması değildir
>
> Trafik anomali kuralı yalnızca **gözlem** yapar. IIS logları okunduktan sonra, yani istekler sunucuya
> çoktan ulaşıp işlendikten sonra çalışır; hiçbir isteği engellemez, yavaşlatmaz, hız sınırlaması
> uygulamaz veya IP yasaklamaz. Gerçek koruma trafiğin sunucuya ulaşmadan önceki katmanlarında yapılır:
> güvenlik duvarı, reverse proxy/CDN üzerinde hız sınırlama, IIS Dynamic IP Restrictions veya sağlayıcı
> seviyesinde DDoS azaltma. Buradaki amaç, olağan dışı paterni fark edilebilir kılmaktır.

### Brute-force kuralı

Kaydedilen her başarısız giriş, **aynı sunucu + aynı kaynak IP** için sayılır.

Sayaçlar bellekte tutulur ve iki sınırla korunur: pencere boyunca sessiz kalan IP'ler `SlidingExpiration`
ile düşer, izlenen IP sayısı `TrackedIpLimit` ile sınırlanır. Alarm üretildikten sonra pencere sıfırlanır;
süren bir saldırı her denemede yeni alarm üretmez.

| Anahtar (`Detection:BruteForce`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Kuralı aç/kapat | `true` |
| `FailureThreshold` | Alarm için gereken başarısız giriş sayısı | `5` |
| `Window` | Denemelerin sayıldığı aralık | `00:05:00` |
| `TrackedIpLimit` | Aynı anda izlenebilecek en fazla IP | `50000` |

### Trafik anomali kuralı

Her trafik kaydı IP bazında sayılır. Eşik aşılınca alarm üretilir ve `AlertCooldown` süresince aynı IP
için yeni alarm üretilmez — süren bir tarama saniyede onlarca alarm üretmesin diye. Sayım, isteğin
**gerçekte yapıldığı ana** (IIS log damgası) göre yapılır; IIS logları toplu yazdığından geliş anına
göre saymak yanlış alarm üretirdi.

Bellek iki şekilde korunur: `IdleRetention` süresidir görülmeyen sayaçlar periyodik taramayla silinir
(`CleanupInterval`), ayrıca izlenen IP sayısı `TrackedIpLimit` ile sınırlıdır.

| Anahtar (`Detection:TrafficAnomaly`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Kuralı aç/kapat | `true` |
| `RequestThreshold` | Bu sayının üzerine çıkılırsa alarm | `100` |
| `Window` | İsteklerin sayıldığı aralık | `00:01:00` |
| `AlertCooldown` | Aynı IP için alarmlar arası en az süre | `00:05:00` |
| `BucketCount` | Pencerenin dilim sayısı (hassasiyet/bellek dengesi) | `6` |
| `IdleRetention` | Bu süredir görülmeyen sayaç silinir | `01:00:00` |
| `CleanupInterval` | Temizlik taramasının sıklığı | `00:05:00` |
| `TrackedIpLimit` | Aynı anda izlenebilecek en fazla IP | `100000` |

> Eşikleri ortamınıza göre ayarlayın: dakikada 100 istek, bazı sağlık kontrolü veya izleme araçları
> için normal olabilir.

> Sayaçlar bellek içidir: Api yeniden başlarsa sıfırlanır ve birden fazla Api örneğinde her biri
> kendi sayacını tutar. Yatay ölçeklemede paylaşılan bir sayaç (ör. Redis) gerekir.

## Sunucu sağlık durumu

Bir agent susarsa panel bunu fark eder. Durum, son veri geldiği andan hesaplanır ve **sunucu
tarafında** belirlenir; istemcilerin saatleri kaymış olsa da aynı sistem her ekranda aynı görünür.

| Durum | Anlamı |
|---|---|
| `Online` | Beklenen aralıkta veri geliyor |
| `Stale` | Veri gecikti; ağ sorunu veya agent yavaşlaması olabilir |
| `Offline` | Uzun süredir veri yok; sunucu veya agent muhtemelen çalışmıyor |

| Anahtar (`Monitoring:ServerHealth`) | Açıklama | Varsayılan |
|---|---|---|
| `StaleAfter` | Bu süre veri gelmezse "gecikti" | `00:01:00` |
| `OfflineAfter` | Bu süre veri gelmezse "erişilemiyor" | `00:03:00` |

> Eşikler agent'ın toplama aralığına göre ayarlanmalıdır; tek bir kaçırılmış gönderim yanlış
> alarm üretmesin diye varsayılanlar aralığın epeyce üzerindedir.

### IP itibar sorgusu (AbuseIPDB) — opsiyonel

Trafik anomalisi alarmı üretilirken kaynak IP, [AbuseIPDB](https://www.abuseipdb.com/)'de sorgulanır
ve 0-100 arası kötüye kullanım skoru alarma işlenir.

Bu bir **zenginleştirmedir, bağımlılık değil**: AbuseIPDB yanıt vermezse, yavaşsa veya anahtar
tanımlı değilse **alarm yine de üretilir**, yalnızca skor alanı boş kalır. Bekleme süresi en fazla
5 saniyedir (`TotalRequestTimeout`); devre kesici açıldığında bu süre ~100 ms'ye düşer.

Kota koruması iki katmanlıdır: aynı adres `CacheDuration` süresince tekrar sorgulanmaz ve özel ağ
(10.x, 172.16-31.x, 192.168.x), loopback, link-local, CGNAT adresleri hiç sorgulanmaz.

**API anahtarı asla appsettings.json'a yazılmaz.** Geliştirmede:

```bash
dotnet user-secrets set "Detection:IpReputation:ApiKey" "ANAHTARINIZ" --project src/ServerGuard.Api
```

Production'da `Detection__IpReputation__ApiKey` ortam değişkeni kullanılır.

| Anahtar (`Detection:IpReputation`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Sorguyu aç/kapat | `true` |
| `ApiKey` | AbuseIPDB anahtarı — **yalnızca sır deposundan** | — |
| `BaseAddress` | Servis adresi | `https://api.abuseipdb.com/api/v2/` |
| `MaxAgeInDays` | Kaç günlük rapor geçmişi dikkate alınsın | `90` |
| `CacheDuration` | Aynı adresin tekrar sorgulanmayacağı süre | `01:00:00` |
| `CachedIpLimit` | Önbellekte tutulacak en fazla adres | `10000` |

> Ücretsiz tier günde 1.000 sorgu verir. Anahtar tanımlamazsanız sistem sorunsuz çalışır,
> yalnızca skor alanı boş kalır.

## Alarm bildirimleri (Telegram) — opsiyonel

Önem derecesi `MinimumSeverity` (varsayılan `High`) ve üzerindeki alarmlar Telegram'a bildirilir.

Bildirim **ana akıştan tamamen ayrıdır**: alarm üretildiğinde yalnızca bir kuyruğa bırakılır,
gönderimi ayrı bir arka plan servisi yapar. Telegram yavaş olsa, hata dönse veya hiç yanıt
vermese bile **alarmı kaydeden istek beklemez ve kayıt geri alınmaz**; hata yalnızca loglanır.

```
Tespit kuralı → kuyruk (sınırlı) → arka plan servisi → Telegram
```

**Bot token'ı ve chat kimliği asla appsettings.json'a yazılmaz.** [BotFather](https://t.me/botfather)
ile bot oluşturduktan sonra:

```bash
dotnet user-secrets set "Notifications:Telegram:BotToken" "TOKENINIZ" --project src/ServerGuard.Api
```

```bash
dotnet user-secrets set "Notifications:Telegram:ChatId" "SOHBET_KIMLIGI" --project src/ServerGuard.Api
```

Production'da `Notifications__Telegram__BotToken` ve `Notifications__Telegram__ChatId` ortam
değişkenleri kullanılır.

| Anahtar (`Notifications:Telegram`) | Açıklama | Varsayılan |
|---|---|---|
| `Enabled` | Bildirimi aç/kapat | `true` |
| `BotToken` | Bot token'ı — **yalnızca sır deposundan** | — |
| `ChatId` | Hedef sohbet kimliği — **yalnızca sır deposundan** | — |
| `MinimumSeverity` | Bu seviyeden itibaren bildirilir | `High` |
| `QueueCapacity` | Telegram erişilemezken bekletilecek en fazla bildirim | `500` |

> Telegram, bot token'ını URL yolunda taşır. Token'ın log dosyalarına sızmaması için bu
> istemcide HttpClient'ın varsayılan istek günlüğü kapatılmıştır.

> Token tanımlamazsanız sistem sorunsuz çalışır, yalnızca bildirim gönderilmez.

### Alarm sorgulama

```bash
curl "http://localhost:5190/api/alerts?serverName=web-01&from=2026-09-01T00:00:00Z&pageSize=50&page=1"
```

| Parametre | Açıklama | Varsayılan |
|---|---|---|
| `serverName` | Tek sunucuya filtrele | tümü |
| `from` / `to` | Zaman aralığı (ISO 8601) | sınırsız |
| `page` | Sayfa numarası (1'den başlar) | `1` |
| `pageSize` | Sayfa boyutu, en fazla 100 | `25` |

Yanıt `items`, `page`, `pageSize`, `totalCount`, `totalPages` ve `hasNextPage` alanlarını içerir.
Sayfa boyutu üst sınırı aşarsa istek sessizce kırpılmaz, **400** döner.

### Trafik sorgulama

```bash
curl "http://localhost:5190/api/traffic/timeline?serverName=web-01&minutes=60&bucketSeconds=60"
```

```bash
curl "http://localhost:5190/api/traffic/top-ips?serverName=web-01&minutes=60&take=10"
```

| Parametre | Açıklama | Varsayılan | Sınır |
|---|---|---|---|
| `serverName` | Tek sunucuya filtrele | tümü | — |
| `minutes` | Şu andan geriye kaç dakika | `60` | 1-1440 |
| `bucketSeconds` | Zaman serisinde dilim boyu | `60` | 10-3600 |
| `take` | Kaç adres döneceği (top-ips) | `10` | 1-100 |

Zaman serisinde istek gelmeyen dilimler de sıfır sayacıyla döner; grafikte kopukluk oluşmaz.

### Rapor

```bash
curl "http://localhost:5190/api/reports/summary?serverName=web-01&from=2026-08-01T00:00:00Z&to=2026-09-01T00:00:00Z"
```

Aralıktaki toplam istek sayısını, ortalama CPU/RAM değerlerini ve tipe göre alarm kırılımını döner.

| Parametre | Açıklama | Varsayılan | Sınır |
|---|---|---|---|
| `serverName` | Tek sunucuya filtrele | tümü | — |
| `from` | Başlangıç (ISO 8601) | `to` - 7 gün | — |
| `to` | Bitiş (ISO 8601) | şu an | — |

> **Tarih aralığı en fazla 90 gündür.** Rapor sorguları toplama yapar ve tüm aralığı taramak
> zorundadır; sınır, tek bir isteğin veritabanını uzun süre meşgul edip veri yazan agent'ları
> bekletmesini engeller. Aşılırsa istek sessizce kırpılmaz, **400** döner. Ayrıca sorgulara
> 30 saniyelik komut zaman aşımı uygulanır.

> Aralıkta hiç metrik toplanmadıysa ortalamalar `null` döner, sıfır değil. Yanıt ayrıca
> `metricSampleCount` içerir: ortalamanın kaç ölçüme dayandığını bilmek güvenilirliğin göstergesidir.

## Panel ve API ayrı sitelerde

Panel (`ServerGuardClient`) ve backend (`ServerGuard`) ayrı IIS siteleri olarak yayınlanır.
Panel statik dosyalardan ibarettir; kendi uygulama havuzunda çalışır ve backend'i hiç
etkilemez. Bunun pratik karşılığı: **panel güncellemesi backend'i yeniden başlatmaz** —
canlı bağlantılar kopmaz, arka plan işleri (veri temizleme, alarm bildirimi) kesintiye uğramaz.

Ayrı origin olduğu için üç ayar birbirini tutmalıdır:

| Nerede | Ne | Neyi bozar |
|---|---|---|
| Backend `web.config` | `Cors__AllowedOrigins__0` = panelin adresi | Eksikse tarayıcı her isteği engeller |
| Panel `config.json` | `apiBaseUrl` = API'nin adresi | Eksikse panel kendi kendine istek atar |
| Panel `web.config` | CSP `connect-src` = API'nin `http` ve `ws` adresi | Eksikse tarayıcı bağlantıyı reddeder |

API adresi **derlemeye gömülmez**; panel her açılışta `config.json`'dan okur. HTTPS'e geçişte
veya sunucu adı değiştiğinde panel yeniden derlenmez, sunucuda tek satır düzenlenir.

Uygulanan CORS listesi açılışta log'a yazılır (`CORS allowed origins: ...`); panel boş
görünüyorsa ilk bakılacak yer orasıdır. Production'da loopback adresleri listeden elenir ve
elenenler ayrıca uyarı olarak yazılır — geliştirme adresi sunucuya taşınırsa geliştirici
makinesinde açılmış bir sayfa canlı veriye erişebilirdi.

> Geliştirmede `ng serve` kullanılırken `config.json` boş bırakılır ve adres
> `environment.ts`'ten gelir; `Cors:AllowedOrigins` varsayılanı `http://localhost:4200`'dür.

### Paylaşılan DTO'lar

| DTO | Alanlar |
|---|---|
| `ServerMetricDto` | ServerName, CpuUsagePercent, RamUsagePercent, Timestamp |
| `SecurityEventDto` | ServerName, EventType (`FailedLogin`, `SuccessfulLogin`), SourceIp, Username, Timestamp |
| `TrafficLogDto` | ServerName, ClientIp, RequestPath, StatusCode, ResponseTimeMs, Timestamp |

## Kurulum

> **Sunucuya kurulum yapacaksanız** adım adım rehber ayrı bir dosyadadır:
> **[docs/YAYINLAMA.md](docs/YAYINLAMA.md)** — IIS sitesi, sırlar, sertifika, güvenlik duvarı ve
> kurulum sonrası doğrulama. Aşağıdaki adımlar geliştirme makinesi içindir.

### 1. Connection string (secret olarak)

Connection string koda yazılmaz. Geliştirmede user-secrets kullanılır:

```bash
dotnet user-secrets set "ConnectionStrings:ServerGuard" "Server=localhost;Database=ServerGuard;Integrated Security=True;TrustServerCertificate=True;" --project src/ServerGuard.Api
```

Production'da aynı anahtar environment variable olarak verilir: `ConnectionStrings__ServerGuard`.

### 2. Güvenlik sırları (secret olarak)

Panele giriş yapabilmek ve agent'ın veri gönderebilmesi için en az bir kullanıcı ve bir agent
anahtarı tanımlanmalıdır. Değerleri yardımcı araç üretir:

```bash
dotnet run --project src/ServerGuard.Tools -- hash-password --user admin
```

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name LOCALDEV
```

```bash
dotnet run --project src/ServerGuard.Tools -- new-signing-key
```

Çıkan değerleri user-secrets'a yazın:

```bash
dotnet user-secrets set "Security:Panel:Users:0:UserName" "admin" --project src/ServerGuard.Api
```

```bash
dotnet user-secrets set "Security:Panel:Users:0:PasswordHash" "URETILEN_OZET" --project src/ServerGuard.Api
```

```bash
dotnet user-secrets set "Security:Ingest:ApiKeys:0:Name" "LOCALDEV" --project src/ServerGuard.Api
```

```bash
dotnet user-secrets set "Security:Ingest:ApiKeys:0:Key" "URETILEN_ANAHTAR" --project src/ServerGuard.Api
```

Aynı anahtarı agent tarafında `Agent:ApiKey` olarak tanımlayın.

> Geliştirmede JWT imza anahtarı verilmezse süreç ömrü boyunca geçerli rastgele bir anahtar
> üretilir; API her yeniden başladığında yeniden giriş gerekir. Production'da anahtar zorunludur.

### 3. Veritabanını oluştur

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/ServerGuard.Api
```

### 4. Çalıştır

```bash
dotnet run --project src/ServerGuard.Api
```

```bash
dotnet run --project src/ServerGuard.Agent
```

## Agent

Agent her `Agent:CollectionInterval` (varsayılan 10 sn) sürede bir PerformanceCounter ile CPU ve RAM yüzdesini okur,
`ServerMetricDto` olarak `POST /api/metrics`'e gönderir.

```
PerformanceCounter → ServerMetricDto → PendingMetricQueue (bounded) → HttpMetricSender → Api
                                              ▲                            │
                                              └── backend erişilemezse ────┘
```

- **Resilience:** named `HttpClient` üzerinde retry (3 deneme, üstel backoff) + attempt/total timeout + circuit breaker.
- **Veri kaybı yok:** backend erişilemezse metrikler sınırlı kapasiteli yerel kuyrukta bekler, bağlantı gelince sırayla gönderilir.
  Kuyruk dolarsa en eski kayıt atılır ve bu `Warning` olarak loglanır.
- **4xx / 5xx ayrımı:** backend kaydı reddederse (4xx) tekrar denenmez ve hata loglanır; 5xx ve ağ hatalarında kayıt kuyrukta kalır.

### Güvenlik olayları (başarısız / başarılı giriş)

Agent, Windows **Security** kanalını `EventLogWatcher` ile dinler ve yalnızca şu olayları yakalar:

| Event ID | Anlamı | `SecurityEventDto.EventType` |
|---|---|---|
| 4625 | Başarısız oturum açma | `FailedLogin` |
| 4624 | Başarılı oturum açma | `SuccessfulLogin` |

Her olayın XML'inden kaynak IP (`IpAddress`) ve kullanıcı adı (`TargetUserName`) çıkarılır.
Yerel ve servis oturumlarında Windows IP alanına `-` yazar; bu değer `local` olarak normalleştirilir.
Bozuk veya eksik bir olay yalnızca loglanıp atlanır, dinleme durmaz.

### Gereken yetki: en az ayrıcalık

Security kanalını okumak için **Administrator yetkisi gerekmez.** Yeterli olan en düşük yetki,
agent'ın çalıştığı hesabın yerel **Event Log Readers** grubunda olmasıdır.

Yönetici bir PowerShell'de (SID kullanmak Windows'un dil ayarından bağımsızdır):

```bash
Add-LocalGroupMember -SID 'S-1-5-32-573' -Member 'MAKINE\ServisHesabi'
```

Doğrulamak için:

```bash
Get-LocalGroupMember -SID 'S-1-5-32-573'
```

Grup üyeliği oturum açma anında değerlendirilir: hesabın oturumunu kapatıp açın; agent Windows
Service olarak çalışıyorsa servisi yeniden başlatın.

Yetki verilmezse ne olur? Agent çökmez. Güvenlik toplayıcısı aşağıdaki gibi tek bir hata satırı
yazıp temiz şekilde durur, **metrik toplama etkilenmeden çalışmaya devam eder**:

```
Windows, Security kanalını okumayı reddetti. Agent'ın çalıştığı hesabı yerel
'Event Log Readers' grubuna ekleyin veya Agent:SecurityEvents:Enabled ayarını false yapın.
```

Güvenlik olayı toplamayacaksanız `Agent:SecurityEvents:Enabled` değerini `false` yapın; bu durumda
uyarı da yazılmaz.

### IIS trafik logları

Agent, IIS'in W3C Extended Log dosyasını takip eder (log tailing): yalnızca yeni eklenen satırları
okur, her satırdan istemci IP, istek yolu, HTTP status ve yanıt süresini çıkarır.
**Servislerinize hiç dokunmadan** çalışır, IIS'in zaten yazdığı logları okur.

Sağlamlık özellikleri:

- **Çok siteli sunucu.** `LogRoot` verildiğinde altındaki tüm `W3SVC*` klasörleri izlenir; sonradan
  açılan siteler `DirectoryRescanInterval` içinde kendiliğinden yakalanır. Her klasörün okuma konumu
  ayrı tutulur. Bir turda klasörler **sırayla** işlenir: bir klasörün kayıtları teslim edilmeden
  diğerine geçilmez, böylece teslim edilemeyen kayıtlar başka bir klasörün konumunu ilerletemez.
- **Konum kalıcı.** Son okunan konumlar `OffsetFilePath` dosyasına atomik olarak yazılır; agent yeniden
  başlarsa her klasör kaldığı yerden devam eder. Eski sürümden gelen tek klasörlü konum dosyası
  otomatik dönüştürülür, mükerrer kayıt oluşmaz.
- **Konum ancak veri ulaşınca ilerler.** Backend erişilemezken konum sabit kalır ve yeni satır
  okunmaz — log dosyası tampon görevi görür, veri kaybolmaz.
- **Yarım satır okunmaz.** IIS satırı yazarken yakalanırsa satır tamamlanana kadar beklenir.
- **Rotasyon.** Yeni günün dosyasına geçmeden önce eski dosyanın son satırları okunur.
- **Kilitli dosya.** `FileShare.ReadWrite` ile açılır; açılamazsa beklenip tekrar denenir, worker durmaz.
- **Bozuk satır.** Eksik/okunamayan alanı olan satır loglanıp atlanır, akış durmaz.
- **Alan sırası dinamik.** Sütunlar dosyadaki `#Fields:` yönergesinden okunur; IIS log yapılandırması
  değişse bile doğru sütunlar alınır.

> IIS logları varsayılan olarak periyodik yazar. Anlık takip istiyorsanız IIS Yönetimi'nden
> log tampon boşaltma süresini kısaltın, aksi halde kayıtlar dakikalarca gecikebilir.

IIS kurulu olmayan bir makinede toplayıcı açıklayıcı bir uyarı yazıp durur; agent'ın geri kalanı
(metrik, güvenlik olayları) çalışmaya devam eder.

### Agent ayarları (`src/ServerGuard.Agent/appsettings.json`)

| Anahtar | Açıklama | Varsayılan |
|---|---|---|
| `Agent:ServerName` | Kayıtlarda görünecek sunucu adı, **her sunucuda benzersiz**. Boşsa makine adı. | makine adı |
| `Agent:ApiBaseUrl` | Api adresi (zorunlu) | `http://localhost:5190` |
| `Agent:ApiKey` | API'nin bu agent'ı tanıdığı anahtar (**zorunlu**; boşsa agent açılmaz) | — |
| `Agent:Metrics:Enabled` | Metrik toplamayı aç/kapat | `true` |
| `Agent:Metrics:CollectionInterval` | Toplama aralığı | `00:00:10` |
| `Agent:Metrics:QueueCapacity` | Metrik kuyruğu kapasitesi | `1000` |
| `Agent:SecurityEvents:Enabled` | Güvenlik olayı toplamayı aç/kapat | `true` |
| `Agent:SecurityEvents:FlushInterval` | Biriken olayların gönderilme sıklığı | `00:00:05` |
| `Agent:SecurityEvents:QueueCapacity` | Olay kuyruğu kapasitesi | `5000` |
| `Agent:Traffic:Enabled` | IIS trafik toplamayı aç/kapat | `true` |
| `Agent:Traffic:LogRoot` | Tüm site log klasörlerini barındıran kök dizin; altındaki siteler kendiliğinden bulunur | boş |
| `Agent:Traffic:DirectoryPattern` | `LogRoot` altında site klasörlerini eşleyen desen | `W3SVC*` |
| `Agent:Traffic:LogDirectories` | Açıkça izlenecek klasör listesi (`LogRoot` boşken) | `[]` |
| `Agent:Traffic:LogDirectory` | Tek siteli kurulum için tekil klasör | `C:\inetpub\logs\LogFiles\W3SVC1` |
| `Agent:Traffic:FilePattern` | Log dosyası deseni; her klasörde en yenisi izlenir | `u_ex*.log` |
| `Agent:Traffic:OffsetFilePath` | Okuma konumlarının saklandığı dosya | `traffic-offset.json` |
| `Agent:Traffic:PollInterval` | Yoklama aralığı (watcher'a ek güvence) | `00:00:02` |
| `Agent:Traffic:DirectoryRescanInterval` | Yeni site klasörlerinin aranma sıklığı | `00:10:00` |
| `Agent:Traffic:MaxTrackedDirectories` | İzlenecek en fazla klasör | `50` |
| `Agent:Traffic:MaxLinesPerCycle` | Tek turda **klasör başına** işlenecek en fazla satır | `2000` |
| `Agent:Traffic:QueueCapacity` | Trafik kuyruğu kapasitesi | `5000` |
| `Agent:Traffic:ReadExistingFileOnFirstRun` | İlk açılışta mevcut dosyayı baştan oku | `true` |

### Windows Service olarak kurulum

Adım adım kurulum, yetkiler ve sorun giderme için: **[docs/AGENT-KURULUM.md](docs/AGENT-KURULUM.md)**

Özet:

```bash
powershell -ExecutionPolicy Bypass -File .\deploy\Yayinla.ps1
```

```bash
sc.exe create ServerGuard.Agent binPath= "C:\ServerGuard\Agent\ServerGuard.Agent.exe" start= auto
```

```bash
sc.exe start ServerGuard.Agent
```

`publish\agent` içeriğini sunucudaki `C:\ServerGuard\Agent` klasörüne kopyalayın, `appsettings.json`
içinde `ServerName`, `ApiBaseUrl` ve `ApiKey` değerlerini doldurun, sonra hizmeti kurun.

Kaldırmak için `sc.exe stop ServerGuard.Agent` ve `sc.exe delete ServerGuard.Agent`.
Servis logları `C:\ServerGuard\Agent\logs` altındadır.

Kurulu bir agent'ı güncellemek için pakete eklenen `Guncelle.ps1` kullanılır; betik `appsettings.json`
ve okuma konumunu koruyarak yalnızca program dosyalarını değiştirir.

## Birden fazla sunucu

Her sunucuya kendi agent'ı kurulur; hepsi aynı merkezî Api'ye veri gönderir. Sunucular birbirini
tanımaz, panelde ayrışmaları yalnızca `Agent:ServerName` alanına dayanır — bu değer **her sunucuda
benzersiz olmalıdır**.

Panelin sağ üstündeki **Sunucu** listesinden bir sunucu seçildiğinde tüm ekranlar (CPU/RAM kartları,
trafik grafiği, top IP tablosu, alarm listesi) yalnızca o sunucuyu gösterir. Seçim hem geçmiş
sorgularına hem de canlı akışa uygulanır.

Liste `GET /api/servers` ile doldurulur; kaynak, son 24 saatte metrik veya trafik göndermiş
sunuculardır (`sinceHours` ile değiştirilebilir, en fazla 720).

Yeni bir sunucu eklemek için: [docs/AGENT-KURULUM.md](docs/AGENT-KURULUM.md)

## Web paneli

Angular paneli hub'a bağlanır ve şunları gösterir:

- **Durum çubuğu** — tek cümlelik genel durum ve KPI'lar (istek/dk, hata oranı, ortalama ve en
  yavaş yanıt, alarm sayısı). En kötü sinyal kazanır; renk sistemin sağlığını okumadan gösterir.
- **Sunucu kartları** — durum noktası (çevrimiçi / veri gecikti / erişilemiyor), son görülme zamanı,
  CPU/RAM/disk çubukları. **Erişilemeyen sunucu soluk ve kırmızı kenarlıklı görünür**; agent
  susarsa panel bunu fark eder.
- **Servis sağlığı** — servis bazında istek, 4xx/5xx, hata oranı ve yanıt süresi. En çok 5xx dönen
  servis en üstte; hangi mikroservisin bozulduğu buradan okunur.
- **Trafik grafiği** — 2xx / 4xx / 5xx yığılmış alan grafiği. Toplam istek eğrisi tek başına
  yanıltıcıdır: hepsi hata dönen bir servis, sağlıklı olanla aynı görünür.
- **En çok istek atan IP'ler** — ilk 10 adres, oran çubuklarıyla.
- **Güvenlik alarmları** — en yeni üstte, önem derecesine göre renk kodlu.

Panel iki sekmelidir: **Panel** (canlı izleme) ve **Rapor** (geçmiş özet).

### Rapor ekranı

Tarih aralığı ve sunucu seçilip özet tablo halinde görüntülenir, **CSV olarak indirilebilir**.
Aralık sınırı istemcide de kontrol edilir: 90 günü aşan veya ters bir aralıkta düğme pasifleşir
ve sebep gösterilir.

> CSV dosyası UTF-8 BOM ile ve noktalı virgül ayırıcıyla üretilir; Excel'in Türkçe yerel
> ayarında sütunlar doğru ayrışsın ve Türkçe karakterler bozulmasın diye.
Açılışta geçmiş alarmlar `GET /api/alerts` ile yüklenir; sonrasında yeni alarmlar hub üzerinden
listenin başına eklenir. Hub'dan bozuk bir kayıt gelirse yalnızca o kayıt atlanır, liste çalışmaya devam eder.
Bağlantı koparsa ekran donmaz: son bilinen değerler kalır, rozet "Yeniden bağlanıyor" olur ve
bağlantı geri geldiğinde sayfa yenilenmeden akış devam eder.

```bash
npm start --prefix src/ServerGuard.Web
```

Panel `http://localhost:4200` adresinde açılır. Api adresi `src/ServerGuard.Web/src/environments/environment.ts` içindedir.

> **Lisans notu:** Gauge'lar DevExtreme ile çizilir ve DevExtreme ticari lisanslıdır. Şu an deneme
> sürümü çalıştığı için panelin üstünde bir lisans bandı görünür. Üretim kullanımı için lisans
> alınmalı veya gauge'lar ücretsiz bir kütüphaneyle değiştirilmelidir.

## Testler

| Katman | Komut | Kapsam |
|---|---|---|
| Backend | `dotnet test tests/ServerGuard.UnitTests` | Parola özetleme, sabit zamanlı karşılaştırma, agent anahtarı doğrulama, yapılandırma denetimi, sunucu sağlık eşikleri |
| Panel | `npm test --prefix src/ServerGuard.Web` | Ağdan gelen kayıtların çalışma zamanı doğrulaması |
| Uçtan uca | `ServerGuard.Tools check --url ...` | Çalışan bir kurulumun sağlığı ve güvenlik kuralları |

Birim testleri güvenliğin karar verdiği yerlere odaklanır: bir parola özetinin bozuk gelmesi
istisna değil "eşleşmedi" üretmeli, bilinmeyen bir agent anahtarı hiçbir koşulda kabul edilmemeli,
eksik yapılandırma production'da açılışı durdurmalıdır.

### Sıralama

En kolay çalıştırma sırası: Api → Web → Agent. Panel Api'den önce açılırsa da sorun olmaz,
bağlantıyı kurana kadar tekrar dener.
