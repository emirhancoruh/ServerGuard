# Yayınlama Rehberi — Adım Adım

ServerGuard'ı **10 numaralı sunucuya** (backend + panel) ve **11 numaralı sunucuya**
(yalnızca agent) kurmak için sırayla uygulanacak adımlar.

```
Sunucu 10                                          Sunucu 11
├── IIS sitesi: ServerGuardClient  (panel, 8090)   └── ServerGuard.Agent
│        │  tarayıcıdan                                    │  (Windows hizmeti)
│        ▼                                                 │
├── IIS sitesi: ServerGuard        (backend, 8091) ◄───────┘
│                                     X-ServerGuard-Key
├── SQL Server (ServerGuard veritabanı)
└── ServerGuard.Agent (Windows hizmeti)
```

Panel ve backend **ayrı sitelerdir**. Panel, API'nin adresini çalışma zamanında
`config.json`'dan okur; adres değiştiğinde sunucuda tek satır düzenlenir, yeniden derleme
gerekmez.

**Her adımın sonunda bir doğrulama var.** Doğrulama geçmeden bir sonraki adıma geçmeyin;
sorunun hangi adımda çıktığını bilmek, sonunda hepsini birden aramaktan çok daha kolaydır.

Toplam süre: yaklaşık 1,5 saat.

---

## Adım 0 — Elinizde ne olmalı

- [ ] Sunucu 10 ve 11'de **yönetici** yetkisi
- [ ] **İki boş port** — örnek: panel `8090`, backend `8091`
- [ ] SQL Server örneğinin adı ve orada kullanıcı açma yetkisi
- [ ] Parola yöneticisi (üreteceğiniz sırları oraya kaydedeceksiniz)

Portların boş olduğunu doğrulayın:

```bash
%windir%\system32\inetsrv\appcmd list sites
```

```bash
netstat -ano | findstr LISTENING | findstr ":8090 :8091"
```

İkincisi **hiçbir şey dönmemeli**.

---

## Adım 1 — Hosting Bundle (sunucu 10)

IIS, .NET uygulamasını tek başına çalıştıramaz; **ASP.NET Core Module** gerekir.

**Doğrulama:**

```bash
dotnet --list-runtimes
```

Çıktıda **`Microsoft.AspNetCore.App 9.0.x`** olmalı. Yoksa
[ASP.NET Core 9 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0) kurup
`iisreset` yapın.

```bash
%windir%\system32\inetsrv\appcmd list config -section:system.webServer/globalModules | findstr AspNetCore
```

`AspNetCoreModuleV2` satırı dönmeli.

> **URL Rewrite modülü** de gerekir — panel sitesindeki derin bağlantılar (`/reports`) onunla
> çalışır. Sunucunuzda ARR/Server Farms varsa zaten kuruludur.

---

## Adım 2 — Veritabanı ve SQL kullanıcısı

**`sa` kullanmayın.** Bağlantı dizesi bir gün sızarsa kayıp bu veritabanıyla sınırlı kalsın.

1. SSMS'te çalıştırın (parolayı kendiniz belirleyin):

```sql
CREATE DATABASE ServerGuard;
GO
CREATE LOGIN serverguard WITH PASSWORD = 'BURAYA-GUCLU-BIR-PAROLA';
GO
USE ServerGuard;
CREATE USER serverguard FOR LOGIN serverguard;
ALTER ROLE db_datareader ADD MEMBER serverguard;
ALTER ROLE db_datawriter ADD MEMBER serverguard;
GO
```

2. Şemayı oluşturun. Geliştirme makinenizde betiği üretin:

```bash
dotnet ef migrations script --project src/ServerGuard.Api --idempotent --output serverguard-sema.sql
```

Üretilen dosyayı SSMS'te `ServerGuard` veritabanı seçiliyken çalıştırın.

> `--idempotent`, "zaten uygulanmışları atla" biçiminde üretir; aynı dosyayı iki kez
> çalıştırmak zarar vermez. Sonraki sürümlerde de aynı yöntem kullanılır.

> "Build failed" diyorsa API'niz o sırada çalışıyor ve derleme çıktısını kilitliyordur.
> API'yi durdurun ya da komuta `--no-build` ekleyin.

**Doğrulama:** SSMS'te şu 5 tablo görünmeli: `ServerMetrics`, `SecurityEvents`,
`SecurityAlerts`, `TrafficLogs`, `__EFMigrationsHistory`.

> Uygulamanın günlük çalışması için `db_datareader` + `db_datawriter` yeterlidir; uygulama
> hiçbir zaman tablo yaratmaz.

---

## Adım 3 — Sırları üretin

Geliştirme makinenizde veya sunucudaki `Tools` klasöründe. **Hiçbirini dosyaya yazmayın**,
parola yöneticisine kaydedin.

```bash
ServerGuard.Tools.exe hash-password --user admin
```

```bash
ServerGuard.Tools.exe new-key --name SERVER10 --index 0
```

```bash
ServerGuard.Tools.exe new-key --name SERVER11 --index 1
```

```bash
ServerGuard.Tools.exe new-key --name JWT
```

**Doğrulama:** Elinizde 4 değer olmalı — 1 parola özeti (`pbkdf2-sha256$...`), 2 agent
anahtarı, 1 imza anahtarı.

> Her sunucuya **ayrı anahtar**: biri sızarsa yalnızca o iptal edilir.

> İmza anahtarı değiştirilirse **tüm panel oturumları düşer**. Acil durumda erişimi kesmenin
> yolu da budur.

---

## Adım 4 — Paketleri hazırlayın

Geliştirme makinenizde:

```bash
powershell -ExecutionPolicy Bypass -File .\deploy\Yayinla.ps1
```

Betik: sır sızıntısı kontrolü → birim testleri → backend publish → Angular build → dört zip.

**Doğrulama:** `publish\` altında dört zip oluşmalı:

| Zip | Nereye |
|---|---|
| `ServerGuard-Backend.zip` | Sunucu 10 → `C:\inetpub\wwwroot\ServerGuard` |
| `ServerGuard-Panel.zip` | Sunucu 10 → `C:\inetpub\wwwroot\ServerGuardClient` |
| `ServerGuard-Tools.zip` | Sunucu 10 → `C:\ServerGuard\Tools` |
| `ServerGuard-Agent.zip` | Sunucu 10 ve 11 → `C:\ServerGuard\Agent` |

---

## Adım 5 — Dosyaları yerleştirin (sunucu 10)

Zip'leri sunucuya alın. Her birine **sağ tık → Özellikler → "Unblock"** işaretleyin.

> Başka makineden gelen dosyalar engellenmiş işaretlenir; kaldırılmazsa içindeki DLL'ler
> bazı durumlarda yüklenmez.

Sonra her zip'i kendi klasörüne ayıklayın:

| Zip içeriği | Hedef klasör |
|---|---|
| `ServerGuard-Backend.zip` | `C:\inetpub\wwwroot\ServerGuard` |
| `ServerGuard-Panel.zip` | `C:\inetpub\wwwroot\ServerGuardClient` |
| `ServerGuard-Tools.zip` | `C:\ServerGuard\Tools` |

**Doğrulama:**

| Bulunmalı | Yol |
|---|---|
| Backend | `C:\inetpub\wwwroot\ServerGuard\ServerGuard.Api.dll` ve `web.config` |
| Panel | `C:\inetpub\wwwroot\ServerGuardClient\index.html`, `config.json` ve `web.config` |
| Araç | `C:\ServerGuard\Tools\ServerGuard.Tools.exe` |

> Backend klasöründe `wwwroot` **olmamalıdır** — panel ayrı sitede.

---

# Backend sitesi

## Adım 6 — Backend uygulama havuzu

IIS Yönetimi → **Uygulama Havuzları** → sağ tık → **Uygulama Havuzu Ekle**:

| Alan | Değer |
|---|---|
| **Name** | `ServerGuard` |
| **.NET CLR version** | ⚠️ **No Managed Code** |
| **Managed pipeline mode** | `Integrated` |

> `.NET CLR version` varsayılan `v4.0.30319` gelir ve **yanlıştır**. .NET 9 uygulamaları
> IIS'in .NET Framework çalışma zamanını kullanmaz; değiştirilmezse her istek `500.30` döner.

Sonra havuza sağ tık → **Advanced Settings** → dört değer:

| Bölüm | Ayar | Değer | Neden |
|---|---|---|---|
| General | **Start Mode** | `AlwaysRunning` | Sunucu açılışında ilk isteği beklemeden ayağa kalksın |
| Process Model | **Idle Time-out (minutes)** | `0` | IIS boştaki havuzu 20 dk'da kapatır; kapanınca veri temizleme ve alarm bildirimi durur |
| Process Model | **Load User Profile** | `True` | ASP.NET Core anahtar deposu için gerekir |
| Recycling | **Regular Time Interval (minutes)** | `0` | Varsayılan 29 saatlik geri dönüşüm, canlı bağlantıyı rastgele bir saatte koparır |

**Identity** `ApplicationPoolIdentity` kalsın.

**Doğrulama:** Havuz listesinde `ServerGuard` satırının `.NET CLR V…` sütunu
**"No Managed Code"** göstermeli.

---

## Adım 7 — Backend sitesi

IIS Yönetimi → **Siteler** → sağ tık → **Web Sitesi Ekle**:

| Alan | Değer |
|---|---|
| **Site name** | `ServerGuard` |
| **Application pool** | **Select…** → `ServerGuard` |
| **Physical path** | `C:\inetpub\wwwroot\ServerGuard` |
| **Type / IP / Port** | `http` / `All Unassigned` / `8091` |
| **Host name** | boş |

> `Application pool` alanını değiştirmeyi atlamayın; `DefaultAppPool` kalırsa `500.30` alırsınız.

**Doğrulama:**

```bash
%windir%\system32\inetsrv\appcmd list site ServerGuard
```

`bindings:http/*:8091:,state:Started` görünmeli.

---

## Adım 8 — Backend log klasörü ve izin

`C:\inetpub\wwwroot\ServerGuard` içinde **`logs`** klasörü oluşturun. Sağ tık → **Özellikler →
Güvenlik → Düzenle → Ekle** → **Konumlar…**'dan **bu bilgisayarı** seçin → nesne adına
`IIS AppPool\ServerGuard` yazın → **Adları Denetle** → **Tamam** → **Değiştir (Modify)**
işaretleyin → Uygula.

> Bu izin verilmezse uygulama çalışır ama **hiçbir log tutmaz** — sorun çıktığında elinizde
> kayıt olmaz, ki bir sonraki adımda tam olarak o log'a bakacağız.

---

## Adım 9 — Backend sırları (web.config)

`C:\inetpub\wwwroot\ServerGuard\web.config` dosyasını açın. Tek satırlık `<aspNetCore ... />`
etiketini açılış-kapanış çiftine dönüştürüp içine `<environmentVariables>` ekleyin:

```xml
<aspNetCore processPath="dotnet" arguments=".\ServerGuard.Api.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ConnectionStrings__ServerGuard" value="Server=SUNUCU10;Database=ServerGuard;User Id=serverguard;Password=ADIM-2-DEKI-PAROLA;TrustServerCertificate=True;" />
    <environmentVariable name="Security__Jwt__SigningKey" value="ADIM-3-TEKI-IMZA-ANAHTARI" />
    <environmentVariable name="Security__Panel__Users__0__UserName" value="admin" />
    <environmentVariable name="Security__Panel__Users__0__PasswordHash" value="pbkdf2-sha256$..." />
    <environmentVariable name="Security__Ingest__ApiKeys__0__Name" value="SERVER10" />
    <environmentVariable name="Security__Ingest__ApiKeys__0__Key" value="SERVER10-ANAHTARI" />
    <environmentVariable name="Security__Ingest__ApiKeys__1__Name" value="SERVER11" />
    <environmentVariable name="Security__Ingest__ApiKeys__1__Key" value="SERVER11-ANAHTARI" />
    <environmentVariable name="Cors__AllowedOrigins__0" value="http://SUNUCU10:8090" />
  </environmentVariables>
</aspNetCore>
```

> ⚠️ **`Cors__AllowedOrigins__0` panel ayrı sitede olduğu için zorunludur.** Buraya panelin
> tarayıcıda göründüğü adres birebir yazılır: şema + sunucu adı + port. `http://sunucu10:8090`
> ile `http://10.0.0.10:8090` **farklı origin'lerdir**; paneli hangi adresle açacaksanız onu
> yazın. Birden fazla adres kullanacaksanız `__1`, `__2` diye ekleyin.

> Production'da `localhost` origin'leri bilerek yok sayılır. Panele sunucunun üstünden
> `localhost` ile bakarsanız API çağrıları engellenir — sunucu adıyla açın.

İsteğe bağlı:

```xml
<environmentVariable name="Notifications__Telegram__BotToken" value="..." />
<environmentVariable name="Notifications__Telegram__ChatId" value="..." />
<environmentVariable name="Detection__IpReputation__ApiKey" value="..." />
```

Kaydedin, sonra erişimini daraltın (yönetici cmd):

```bash
icacls "C:\inetpub\wwwroot\ServerGuard\web.config" /inheritance:r /grant "Administrators:(R,W)" /grant "SYSTEM:(R,W)" /grant "IIS AppPool\ServerGuard:(R)"
```

> **Bu dosya artık sır içeriyor.** Depoya, e-postaya veya sohbete koymayın.

---

## Adım 10 — Backend'i doğrulayın

```bash
curl.exe http://localhost:8091/health
```

`Healthy` dönmeli.

```bash
curl.exe http://localhost:8091/health/ready
```

Bu da `Healthy` dönmeli — veritabanı bağlantısının çalıştığını kanıtlar.

Log'a bakın — `C:\inetpub\wwwroot\ServerGuard\logs\api-YYYYMMDD.log` içinde şu satırlar olmalı:

```
Security configuration is complete. PanelUsers=1 IngestKeys=2 RequireHttps=False
CORS allowed origins: http://SUNUCU10:8090
Data retention started. ...
```

> **`CORS allowed origins`** satırı panelin adresini göstermiyorsa panel API'ye ulaşamaz.
> Adım 9'a dönün.

> API eksik yapılandırmayla **bilerek açılmaz** ve hangi ortam değişkeninin eksik olduğunu
> tek tek yazar. `500.30` alıyorsanız cevap bu dosyadadır.

---

# Panel sitesi

## Adım 11 — Panel uygulama havuzu

IIS Yönetimi → **Uygulama Havuzları** → **Uygulama Havuzu Ekle**:

| Alan | Değer |
|---|---|
| **Name** | `ServerGuardClient` |
| **.NET CLR version** | **No Managed Code** |
| **Managed pipeline mode** | `Integrated` |

> Panel statik dosyalardan ibarettir; içinde .NET çalışmaz. Ayrı havuz olması, panelde bir
> sorun çıktığında backend'in ve veri toplamanın etkilenmemesini sağlar.

Gelişmiş ayar gerekmez; varsayılanlar yeterli.

---

## Adım 12 — Panel sitesi

**Siteler → Web Sitesi Ekle**:

| Alan | Değer |
|---|---|
| **Site name** | `ServerGuardClient` |
| **Application pool** | **Select…** → `ServerGuardClient` |
| **Physical path** | `C:\inetpub\wwwroot\ServerGuardClient` |
| **Type / IP / Port** | `http` / `All Unassigned` / `8090` |
| **Host name** | boş |

**Doğrulama:**

```bash
curl.exe -o NUL -w "%{http_code}\n" http://localhost:8090/
```

`200` dönmeli. Dönmüyorsa `500.19` ihtimali yüksektir — `web.config`'deki bir bölüm IIS'te
kilitli olabilir; hata sayfası hangi satır olduğunu yazar.

---

## Adım 13 — Panelin API adresi ve CSP

İki dosyada birer satır. **İkisi de API'nin adresini gösterir; biri eksik kalırsa panel boş
görünür.**

**1. `C:\inetpub\wwwroot\ServerGuardClient\config.json`**

```json
{
  "apiBaseUrl": "http://SUNUCU10:8091"
}
```

> Sondaki eğik çizgi olmadan. Panel bu adresi her açılışta okur; ileride HTTPS'e geçtiğinizde
> **yalnızca bu satırı** değiştirmeniz yeter, yeniden derleme gerekmez.

**2. `C:\inetpub\wwwroot\ServerGuardClient\web.config`** — `Content-Security-Policy` satırındaki
`connect-src` bölümü:

```
connect-src 'self' http://SUNUCU10:8091 ws://SUNUCU10:8091;
```

> Hem `http` hem `ws` yazılır: SignalR canlı bağlantısı WebSocket kullanır. HTTPS'e
> geçildiğinde ikisi de `https` / `wss` olur.

**Doğrulama:**

```bash
curl.exe -s -D - -o NUL http://localhost:8090/ | findstr /i "content-security"
```

Çıktıdaki `connect-src` API adresini içermeli.

```bash
curl.exe -o NUL -w "%{http_code}\n" http://localhost:8090/reports
```

`200` dönmeli — derin bağlantı `index.html`'e düşüyor demektir. `404` dönerse URL Rewrite
modülü kurulu değildir.

---

## Adım 14 — Paneli açın

Başka bir makineden (VPN üzerinden) **`http://SUNUCU10:8090`** açın.

> Sunucunun üstünden `localhost` ile değil, **sunucu adıyla** açın. `Cors__AllowedOrigins`
> içinde hangi adres yazıyorsa panelin adres çubuğunda da o olmalıdır.

- [ ] Giriş ekranı geliyor
- [ ] Adım 3'teki kullanıcı adı ve parolayla giriş yapılıyor
- [ ] Giriş sonrası panel açılıyor, sağ üstte **"Canlı"** yazıyor

**Panel boş geliyorsa** F12 → **Console**'a bakın:

| Konsoldaki hata | Sebep |
|---|---|
| `blocked by CORS policy` | Adım 9'daki `Cors__AllowedOrigins__0` panelin adresiyle birebir aynı değil |
| `violates ... Content-Security-Policy` | Adım 13'teki `connect-src` eksik veya yanlış |
| `config.json okunamadı` | Dosya yerinde değil ya da JSON bozuk |
| `ERR_CONNECTION_REFUSED` | Backend sitesi çalışmıyor; Adım 10'a dönün |

---

## Adım 15 — Güvenlik duvarı

Agent'ların **backend**'e ulaşabilmesi için 8091'i açın. Kaynağı kendi ağınızla sınırlayın:

```bash
netsh advfirewall firewall add rule name="ServerGuard API" dir=in action=allow protocol=TCP localport=8091 remoteip=10.0.0.0/24
```

Paneli VPN üzerinden açacaksanız 8090 için de benzer bir kural gerekir.

**Doğrulama:** Sunucu 11'den `curl.exe http://SUNUCU10:8091/health` → `Healthy`.

> Windows, bir programa ilk bağlantı geldiğinde kendiliğinden "program tabanlı" bir kural
> oluşturabilir ve bu tüm profilleri kapsayabilir. `wf.msc` açıp gereksiz kuralları temizleyin.

---

# Agent kurulumu

## Adım 16 — IIS log ayarlarını doğrulayın (trafik toplamanın şartı)

**Bu adımı atlamayın.** Agent, IIS'in yazdığı W3C log dosyalarını okur. Bir sitenin ayarları
uymuyorsa **o sitenin trafiği hiç görünmez**.

Sunucu 10 ve 11'de yönetici PowerShell'de:

```powershell
Import-Module WebAdministration; Get-ChildItem IIS:\Sites | Select-Object Name, Id, @{n='Klasor';e={$_.logFile.directory}}, @{n='Bicim';e={$_.logFile.logFormat}}, @{n='Alanlar';e={$_.logFile.logExtFileFlags}} | Format-List
```

> `Import-Module WebAdministration` hata veriyorsa **IIS Yönetim Betikleri ve Araçları**
> özelliği kurulu değildir. Aynı bilgilere IIS Yönetimi'nden site → **Logging** ile de
> bakabilirsiniz.

Her site için üç şey:

| Kontrol | Olması gereken | Değilse |
|---|---|---|
| **Biçim** | `W3C` | IIS/NCSA/Custom biçimlerinde `#Fields:` satırı yoktur; agent o satırları atlar. Logging → Format: **W3C** |
| **Alanlar** | En az `Date, Time, ClientIP, UriStem, HttpStatus, TimeTaken` | Logging → **Select Fields…**. `TimeTaken` yoksa o sitenin **tüm** satırları atlanır |
| **Klasör** | Hepsi aynı kök altında (örn. `%SystemDrive%\inetpub\logs\LogFiles`) | Farklı yoldaki site için agent ayarında `LogDirectories` kullanın |

---

## Adım 17 — Agent (sunucu 10)

1. `ServerGuard-Agent.zip` içeriğini `C:\ServerGuard\Agent` altına ayıklayın.

2. `C:\ServerGuard\Agent\appsettings.json`:

```json
{
  "Agent": {
    "ServerName": "SERVER10",
    "ApiBaseUrl": "http://localhost:8091",
    "ApiKey": "SERVER10-ANAHTARI",
    "Traffic": {
      "LogRoot": "C:\\inetpub\\logs\\LogFiles"
    }
  }
}
```

> `LogRoot` **dolu olmalı.** Boş bırakılırsa yalnızca tek klasör izlenir ve diğer sitelerin
> trafiği hiç toplanmaz. Dolduğunda altındaki tüm `W3SVC*` klasörleri izlenir; sonradan
> açtığınız siteler 10 dakika içinde kendiliğinden yakalanır.

> `ApiKey` boşsa agent **hiç açılmaz** ve sebebini log'a yazar. Anahtarsız bir agent tek bir
> kaydı bile teslim edemez; sessizce çalışıp veri kaybetmesindense açılışta durması yeğdir.

3. Hizmeti kurun (yönetici cmd):

```bash
sc.exe create ServerGuard.Agent binPath= "C:\ServerGuard\Agent\ServerGuard.Agent.exe" start= auto DisplayName= "ServerGuard Agent"
```

```bash
sc.exe failure ServerGuard.Agent reset= 86400 actions= restart/5000/restart/10000/restart/30000
```

```bash
sc.exe start ServerGuard.Agent
```

> `sc.exe` sözdizimi katıdır: `binPath=` ile değer arasında **boşluk vardır**, eşittirden önce
> boşluk **yoktur**.

**Doğrulama:**

```powershell
Get-Content "C:\ServerGuard\Agent\logs\agent-*.log" -Tail 20
```

Olması gerekenler:

- `Metric collector started. Server=SERVER10 ...`
- `Traffic log watcher started. ... Directories=N` → **N, site sayınız kadar**
- `Watching IIS log directory. Directory=...` → her site için bir satır

---

## Adım 18 — Agent (sunucu 11)

Aynısı, iki fark:

```json
{
  "Agent": {
    "ServerName": "SERVER11",
    "ApiBaseUrl": "http://SUNUCU10:8091",
    "ApiKey": "SERVER11-ANAHTARI",
    "Traffic": {
      "LogRoot": "C:\\inetpub\\logs\\LogFiles"
    }
  }
}
```

Adım 16'yı bu sunucuda da uygulayın.

**Doğrulama:** Log'da `Backend unreachable` satırı **olmamalı**. Varsa güvenlik duvarı veya
adres yanlıştır.

---

## Adım 19 — Kurulumu baştan sona doğrulayın

```bash
set SERVERGUARD_PASSWORD=panel-parolaniz
```

```bash
C:\ServerGuard\Tools\ServerGuard.Tools.exe check --url http://localhost:8091 --user admin --ingest-key "SERVER10-ANAHTARI"
```

12 kontrolün **hepsi `OK`** olmalı:

| Kontrol | Ne kanıtlar |
|---|---|
| API ayakta mı / Veritabanı erişilebilir mi | Süreç ve bağlantı sağlam |
| Güvenlik header'ları | Tarayıcı savunmaları devrede |
| Token'sız okuma / anahtarsız yazma / token'sız canlı bağlantı engelleniyor mu | Yetkilendirme **gerçekten** açık |
| Agent anahtarı geçerli mi | Agent'lar veri gönderebilecek |
| Hatalı parola reddediliyor mu | Parola doğrulaması çalışıyor |
| Panel oturumu / token | Kullanıcı tanımı doğru |
| Sunucu durumları | **Her iki sunucu da `Online`** |
| Trafik ve hata oranı | 5xx oranı ve alarm sayısı |

Panelden gözle:

- [ ] İki sunucu da çevrimiçi
- [ ] CPU / RAM / Disk dolu
- [ ] Trafik grafiğinde veri var (1–2 dk bekleyin, IIS tamponu var)
- [ ] Servis sağlığı tablosunda servisleriniz listeleniyor
- [ ] Sağ üstte "Canlı"
- [ ] Çıkış yapınca giriş ekranına dönüyor

---

## Adım 20 — HTTPS (sertifika hazır olunca)

İlk aşamada VPN + giriş ile devam edebilirsiniz. Sertifika hazır olduğunda:

1. İki siteye de `https` bağlaması ekleyin (IIS → site → **Bağlamalar**).
2. Sertifikanın **agent'ların çalıştığı makinelerde de güvenilir** olduğunu doğrulayın:
   sunucu 11'den `curl.exe https://SUNUCU10:8091/health` sertifika hatası vermemeli.
3. **Üç yeri** güncelleyin:

| Dosya | Değişiklik |
|---|---|
| Panel `config.json` | `"apiBaseUrl": "https://SUNUCU10:8443"` |
| Panel `web.config` | `connect-src 'self' https://SUNUCU10:8443 wss://SUNUCU10:8443` |
| Backend `web.config` | `Cors__AllowedOrigins__0` → panelin yeni `https` adresi |

4. Agent'ların `ApiBaseUrl` değerini `https://...` yapıp hizmetleri yeniden başlatın.
5. Son olarak backend `web.config`'e ekleyin:

```xml
<environmentVariable name="Security__RequireHttps" value="true" />
```

> **Sertifika hazır değilken bu ayarı açmayın.** HSTS tarayıcıya "bu adrese bir daha asla HTTP
> ile gitme" der; geri alması zordur ve agent'lar güvenilmeyen sertifika yüzünden bağlanamaz.

---

## Yeni bir IIS sitesi eklerseniz

ServerGuard tarafında **hiçbir şey yapmanız gerekmez.** `LogRoot` dolu olduğu için yeni
sitenin log klasörü en geç 10 dakika içinde bulunur ve izlenmeye başlar. Yalnızca Adım 16'daki
üç şartı sağladığından emin olun.

### Panelde siteler nasıl ayrışır

**Site adı kaydedilmiyor.** Trafik kayıtlarında sunucu adı, istek yolu, durum kodu ve yanıt
süresi var; hangi IIS sitesinden geldiği yok. "Servis sağlığı" tablosu servis adını **istek
yolunun ilk iki segmentinden** türetir:

```
/services/kanban/Synchronize/Webhook  →  /services/kanban
/api/musteri/liste                    →  /api/musteri
```

Yolları farklı olan siteler doğal olarak ayrışır; **aynı yolu kullanan iki site tek satırda
birleşir**. Sitelerin ayrı ayrı görünmesi gerekiyorsa trafik kaydına site kimliği eklenmelidir
— bu bir geliştirme konusudur.

---

## Bakım

### Güncelleme

| | Backend | Panel |
|---|---|---|
| 1 | Siteyi durdurun (DLL'ler kilitli) | Gerekmez |
| 2 | Dosyaları kopyalayın, **`web.config` hariç** | Dosyaları kopyalayın, **`web.config` ve `config.json` hariç** |
| 3 | Yeni migration varsa uygulayın | — |
| 4 | Siteyi başlatın | — |
| 5 | `ServerGuard.Tools check` | Panelde Ctrl+F5 |

> `dotnet publish` her seferinde temiz bir `web.config` üretir; sunucudaki dosyanın üzerine
> yazarsanız **tüm ayarlarınız silinir**. Panel tarafında `config.json` için de aynı şey geçerli.

Agent güncellemesi ayarları koruyarak yapılır:

```bash
powershell -ExecutionPolicy Bypass -File C:\ServerGuard\Yeni\Guncelle.ps1
```

### Veri saklama

| Anahtar (`Maintenance:Retention`) | Varsayılan |
|---|---|
| `ServerMetrics` | 30 gün |
| `TrafficLogs` | 30 gün |
| `SecurityEvents` | 90 gün |
| `SecurityAlerts` | 365 gün |

Değiştirmek için backend `web.config`'e ekleyin:

```xml
<environmentVariable name="Maintenance__Retention__TrafficLogs" value="14.00:00:00" />
```

> Kapatılırsa disk dolana kadar veri birikir ve SQL Server durduğunda izleme sisteminin
> kendisi çöker.

### Log dosyaları

| Bileşen | Yer | Saklama |
|---|---|---|
| Backend | `C:\inetpub\wwwroot\ServerGuard\logs\api-YYYYMMDD.log` | 30 dosya, en fazla 50 MB |
| Agent | `C:\ServerGuard\Agent\logs\agent-YYYYMMDD.log` | 14 dosya, en fazla 20 MB |

---

## Sık karşılaşılan sorunlar

| Belirti | Sebep |
|---|---|
| `500.19` (backend) | `web.config` düzenlemesinde XML hatası; hata sayfası satırı yazar |
| `500.19` (panel) | `web.config`'deki bir bölüm IIS'te kilitli veya URL Rewrite kurulu değil |
| `500.30` | Havuzun CLR sürümü "No Managed Code" değil, ya da eksik ortam değişkeni. Cevap `logs\api-*.log` dosyasında |
| `500.31` | `Microsoft.AspNetCore.App 9.0.x` kurulu değil |
| `/health` Healthy, `/health/ready` değil | SQL bağlantısı kurulamıyor |
| Panel boş, konsolda CORS hatası | `Cors__AllowedOrigins__0` panelin adresiyle birebir aynı değil |
| Panel boş, konsolda CSP hatası | Panel `web.config`'deki `connect-src` eksik |
| `/reports` 404 | URL Rewrite modülü kurulu değil |
| Giriş kabul edilmiyor | Parola özeti eksik kopyalanmış; `$` işaretleri dahil tamamı yapıştırılmalı |
| Agent hemen duruyor | `ApiKey` boş veya ayar aralık dışında; sebep agent log'unun ilk satırlarında |
| Agent log'unda `Backend rejected the agent API key` | Anahtar API'deki listede yok. Kayıtlar kuyrukta bekler, düzeltince gönderilir |
| Panelde sunucu var, trafik yok | Adım 16 atlanmış: biçim W3C değil veya `TimeTaken` seçili değil |
| Bazı siteler panelde yok | `LogRoot` boş bırakılmış |
| Trafik 1–2 dk gecikmeli | Normal. IIS log tamponu (HTTP.SYS) periyodik boşalır |
