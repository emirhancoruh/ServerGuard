# Yayınlama Rehberi — Adım Adım

ServerGuard'ı **10 numaralı sunucuya** (API + panel + agent) ve **11 numaralı sunucuya**
(yalnızca agent) kurmak için sırayla uygulanacak adımlar.

```
Sunucu 10                                   Sunucu 11
├── IIS sitesi: ServerGuard                 └── ServerGuard.Agent (Windows hizmeti)
│   ├── ServerGuard.Api                            │
│   └── wwwroot/  (Angular panel)                  │
├── SQL Server (ServerGuard veritabanı)            │
└── ServerGuard.Agent (Windows hizmeti)            │
        │                                          │
        └──────────► API ◄────────────────────────┘
                 (X-ServerGuard-Key)
```

Panel API ile **aynı kaynaktan** servis edilir. Bu bilinçli bir tercihtir: tek site, tek sertifika,
CORS ayarı yok ve oturum token'ı başka bir kaynağa hiç gitmez.

**Her adımın sonunda bir doğrulama var.** Doğrulama geçmeden bir sonraki adıma geçmeyin; sorunun
hangi adımda çıktığını bilmek, sonunda hepsini birden aramaktan çok daha kolaydır.

Toplam süre: yaklaşık 1–1,5 saat.

---

## Adım 0 — Elinizde ne olmalı

Başlamadan önce hazırlayın:

- [ ] Sunucu 10 ve 11'de **yönetici** yetkisi
- [ ] Sunucu 10 için kullanılacak **port** (örnek: `8443`) — mevcut 14 site ile çakışmamalı
- [ ] SQL Server örneğinin adı ve orada yeni kullanıcı açma yetkisi
- [ ] Parola yöneticisi (üreteceğiniz sırları oraya kaydedeceksiniz)

**Sunucu 10'da gerekenler:**

| Gereksinim | Kontrol |
|---|---|
| IIS | Zaten kurulu |
| **ASP.NET Core 9 Hosting Bundle** | Adım 1'de kurulacak |
| SQL Server | Mevcut örnek kullanılabilir |
| PowerShell 5.1+ | Windows Server ile geliyor |

**Sunucu 11'de gereken:** .NET 9 Runtime.

---

## Adım 1 — Hosting Bundle kurulumu (sunucu 10)

IIS, .NET uygulamasını tek başına çalıştıramaz; **ASP.NET Core Module** gerekir ve o da Hosting
Bundle ile gelir. Bu adım atlanırsa site açılır ama her istek `500.19` veya `500.31` döner.

1. [dotnet.microsoft.com/download/dotnet/9.0](https://dotnet.microsoft.com/download/dotnet/9.0)
   adresinden **ASP.NET Core Runtime → Hosting Bundle**'ı indirin.
2. Kurun.
3. Kurulum bitince IIS'i yeniden başlatın:

```bash
iisreset
```

**Doğrulama:**

```bash
dotnet --list-runtimes
```

Çıktıda hem `Microsoft.AspNetCore.App 9.0.x` hem `Microsoft.NETCore.App 9.0.x` görünmeli.

```bash
%windir%\system32\inetsrv\appcmd list config -section:system.webServer/globalModules | findstr AspNetCore
```

`AspNetCoreModuleV2` satırı dönmeli. Boş dönerse Hosting Bundle kurulmamış demektir; `iisreset`
yapıp tekrar bakın.

---

## Adım 2 — Veritabanı ve SQL kullanıcısı

**`sa` kullanmayın.** ServerGuard'a ayrı, sınırlı bir hesap açın: bir gün bağlantı dizesi sızarsa
kayıp yalnızca bu veritabanıyla sınırlı kalır.

1. SQL Server Management Studio'da yeni sorgu açın ve çalıştırın (parolayı kendiniz belirleyin):

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

2. Şemayı oluşturun. Geliştirme makinenizde SQL betiğini üretin:

```bash
dotnet ef migrations script --project src/ServerGuard.Api --idempotent --output serverguard-sema.sql
```

Üretilen `serverguard-sema.sql` dosyasını SSMS'te `ServerGuard` veritabanı seçiliyken çalıştırın.

> `--idempotent`, betiği "zaten uygulanmışları atla" biçiminde üretir; aynı dosyayı iki kez
> çalıştırmak zarar vermez. Sonraki sürümlerde de aynı yöntem kullanılır.

> Komut "Build failed" diyorsa API'niz o sırada çalışıyor ve derleme çıktısını kilitliyordur.
> API'yi durdurun ya da komuta `--no-build` ekleyin.

Geliştirme makineniz sunucunun SQL'ine doğrudan bağlanabiliyorsa betik üretmeden de yapabilirsiniz:

```bash
dotnet ef database update --project src/ServerGuard.Api --connection "Server=SUNUCU10;Database=ServerGuard;User Id=SEMA-YETKILI-HESAP;Password=...;TrustServerCertificate=True;"
```

**Doğrulama:** SSMS'te `ServerGuard` veritabanı altında şu 5 tablo görünmeli:
`ServerMetrics`, `SecurityEvents`, `SecurityAlerts`, `TrafficLogs`, `__EFMigrationsHistory`.

> Uygulamanın günlük çalışması için `db_datareader` + `db_datawriter` yeterlidir; şema
> değişikliğini ayrıca siz uygularsınız. Uygulamanın kendisi hiçbir zaman tablo yaratmaz.

---

## Adım 3 — Sırları üretin

Geliştirme makinenizde çalıştırın. **Hiçbirini dosyaya yazmayın**, parola yöneticisine kaydedin.

1. Panel kullanıcısı için parola özeti (parola ekrana yazılmaz):

```bash
dotnet run --project src/ServerGuard.Tools -- hash-password --user admin
```

2. Sunucu 10'un agent anahtarı:

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name SERVER10 --index 0
```

3. Sunucu 11'in agent anahtarı:

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name SERVER11 --index 1
```

4. Token imza anahtarı (bir `new-key` çıktısı daha):

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name JWT
```

**Doğrulama:** Elinizde 4 değer olmalı — 1 parola özeti (`pbkdf2-sha256$...` ile başlar),
2 agent anahtarı, 1 imza anahtarı.

> Her sunucuya **ayrı anahtar** verilmesinin sebebi: biri sızarsa yalnızca o iptal edilir, diğer
> sunucu veri göndermeyi sürdürür.

> İmza anahtarı değiştirilirse **tüm panel oturumları düşer**. Acil durumda erişimi kesmenin
> yolu da budur.

---

## Adım 4 — Paketi hazırlayın

Geliştirme makinenizde:

```bash
powershell -ExecutionPolicy Bypass -File .\deploy\Yayinla.ps1
```

Betik sırasıyla: `appsettings.json` dosyalarında sır kalmadığını doğrular, birim testlerini
çalıştırır, Angular panelini derleyip API'nin `wwwroot`'una kopyalar, üç projeyi de yayınlar
ve paketi son kez denetler.

**Doğrulama:** "Yayin hazir." satırını görün. Üç klasör oluşmuş olmalı:

```
publish\api     → IIS'e kopyalanacak (panel dahil)
publish\agent   → her iki sunucuya kopyalanacak
publish\tools   → doğrulama aracı
```

`publish\api\wwwroot\index.html` dosyasının var olduğunu kontrol edin — yoksa panel servis edilmez,
API salt veri servisi olarak çalışır.

---

## Adım 5 — Dosyaları sunucu 10'a kopyalayın

1. Sunucu 10'da klasörü oluşturun:

```bash
mkdir C:\inetpub\ServerGuard
```

2. `publish\api` **içeriğini** (klasörün kendisini değil) buraya kopyalayın.

**Doğrulama:** `C:\inetpub\ServerGuard\ServerGuard.Api.dll`, `web.config` ve `wwwroot\index.html`
yerinde olmalı.

---

## Adım 6 — Uygulama havuzu oluşturun

Yönetici komut isteminde:

```bash
%windir%\system32\inetsrv\appcmd add apppool /name:ServerGuard /managedRuntimeVersion:"" /managedPipelineMode:Integrated
```

> `managedRuntimeVersion:""` = **"Yönetilen kod yok"**. .NET Core/9 uygulamaları IIS'in .NET
> Framework çalışma zamanını kullanmaz; bu değer boş bırakılmazsa uygulama açılmaz.

Ardından izleme uygulamasına özgü dört ayar:

```bash
%windir%\system32\inetsrv\appcmd set apppool /apppool.name:ServerGuard /processModel.idleTimeout:00:00:00
```

```bash
%windir%\system32\inetsrv\appcmd set apppool /apppool.name:ServerGuard /startMode:AlwaysRunning
```

```bash
%windir%\system32\inetsrv\appcmd set apppool /apppool.name:ServerGuard /processModel.loadUserProfile:true
```

```bash
%windir%\system32\inetsrv\appcmd set apppool /apppool.name:ServerGuard /recycling.periodicRestart.time:00:00:00
```

Ne işe yaradıkları:

| Ayar | Sebep |
|---|---|
| `idleTimeout:00:00:00` | IIS boşta kalan havuzu 20 dakikada kapatır. Kapanınca veri temizleme ve alarm bildirimi gibi arka plan işleri durur. İzleme sistemi hiç uyumamalıdır. |
| `startMode:AlwaysRunning` | Sunucu yeniden başladığında ilk isteği beklemeden ayağa kalkar. |
| `loadUserProfile:true` | ASP.NET Core'un anahtar deposu için gerekir; kapalıyken açılışta uyarı üretir. |
| `periodicRestart.time:00:00:00` | Varsayılan 29 saatte bir geri dönüşüm, panelin canlı bağlantısını gelişigüzel bir saatte koparır. |

İsterseniz geri dönüşümü tamamen kapatmak yerine sabit bir saate alın:

```bash
%windir%\system32\inetsrv\appcmd set apppool /apppool.name:ServerGuard /+recycling.periodicRestart.schedule.[value='03:00:00']
```

**Doğrulama:**

```bash
%windir%\system32\inetsrv\appcmd list apppool ServerGuard /text:*
```

`state:Started`, `managedRuntimeVersion:` (boş) ve `idleTimeout:00:00:00` görünmeli.

---

## Adım 7 — Siteyi oluşturun

```bash
%windir%\system32\inetsrv\appcmd add site /name:ServerGuard /bindings:http/*:8443: /physicalPath:"C:\inetpub\ServerGuard"
```

```bash
%windir%\system32\inetsrv\appcmd set app /app.name:"ServerGuard/" /applicationPool:ServerGuard
```

> IIS Yönetimi'nden yapmak isterseniz: **Siteler → sağ tık → Web Sitesi Ekle**. Site adı
> `ServerGuard`, fiziksel yol `C:\inetpub\ServerGuard`, uygulama havuzu `ServerGuard`, port `8443`.

**Doğrulama:**

```bash
%windir%\system32\inetsrv\appcmd list site ServerGuard
```

`state:Started` ve `bindings:http/*:8443:` görünmeli.

---

## Adım 8 — Klasör izinleri

Uygulama kendi log'unu `logs` klasörüne yazar. **Bu izin verilmezse uygulama çalışır ama hiçbir
log tutulmaz** — sorun çıktığında elinizde kayıt olmaz.

```bash
mkdir C:\inetpub\ServerGuard\logs
```

```bash
icacls "C:\inetpub\ServerGuard\logs" /grant "IIS AppPool\ServerGuard:(OI)(CI)M" /T
```

**Doğrulama:**

```bash
icacls "C:\inetpub\ServerGuard\logs"
```

Çıktıda `IIS AppPool\ServerGuard:(OI)(CI)(M)` satırı görünmeli.

---

## Adım 9 — Sırları web.config'e girin

`C:\inetpub\ServerGuard\web.config` dosyasını Not Defteri'nde açın. `<aspNetCore ... />` satırını
bulun ve şu hale getirin (kendi kapanışını `/>` ile yapan tek satırdan, açılış-kapanış çiftine
dönüştüğüne dikkat edin):

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
  </environmentVariables>
</aspNetCore>
```

İsteğe bağlı — Telegram bildirimi ve IP itibar sorgusu kullanacaksanız aynı bloğa ekleyin:

```xml
<environmentVariable name="Notifications__Telegram__BotToken" value="..." />
<environmentVariable name="Notifications__Telegram__ChatId" value="..." />
<environmentVariable name="Detection__IpReputation__ApiKey" value="..." />
```

Dosyayı kaydedin, sonra erişimini daraltın:

```bash
icacls "C:\inetpub\ServerGuard\web.config" /inheritance:r /grant "Administrators:(R,W)" /grant "SYSTEM:(R,W)" /grant "IIS AppPool\ServerGuard:(R)"
```

> **Bu dosya artık sır içeriyor.** Depoya, e-postaya veya sohbete koymayın. Yedek alırken de
> aynı özenle davranın.

**Doğrulama:** Dosyayı tarayıcıya sürükleyip bırakın; XML hatasız açılmalı. Açılmıyorsa bir etiket
kapanmamıştır.

---

## Adım 10 — Siteyi başlatın

```bash
%windir%\system32\inetsrv\appcmd start site /site.name:ServerGuard
```

**Doğrulama:**

```bash
curl.exe http://localhost:8443/health
```

`Healthy` dönmeli.

```bash
curl.exe http://localhost:8443/health/ready
```

Bu da `Healthy` dönmeli — veritabanı bağlantısının çalıştığını kanıtlar.

Tarayıcıdan `http://SUNUCU10:8443` açın: **giriş ekranı** gelmeli. Adım 3'teki kullanıcı adı ve
parolayla girin.

**Bir şey dönmüyorsa** `C:\inetpub\ServerGuard\logs\api-YYYYMMDD.log` dosyasına bakın. API eksik
yapılandırmayla **bilerek açılmaz** ve hangi ortam değişkeninin eksik olduğunu tek tek yazar.

---

## Adım 11 — Güvenlik duvarı

Agent'ların API'ye ulaşabilmesi için portu açın. Kaynağı **kendi ağınızla sınırlayın**:

```bash
netsh advfirewall firewall add rule name="ServerGuard API" dir=in action=allow protocol=TCP localport=8443 remoteip=10.0.0.0/24
```

> `remoteip` değerini kendi ağınıza göre yazın. Sınırsız bırakırsanız port, olması gerekenden
> geniş bir kitleye açılır.

**Doğrulama:** Sunucu 11'den:

```bash
curl.exe http://SUNUCU10:8443/health
```

`Healthy` dönmeli.

> Windows, bir programa ilk bağlantı geldiğinde kendiliğinden "program tabanlı" bir kural
> oluşturabilir ve bu kural tüm profilleri kapsayabilir. `wf.msc` açıp gereksiz kuralları temizleyin.

---

## Adım 12 — IIS log ayarlarını doğrulayın (trafik toplamanın şartı)

**Bu adımı atlamayın.** Agent, IIS'in yazdığı W3C log dosyalarını okur. Bir sitenin ayarları
uymuyorsa **o sitenin trafiği hiç görünmez** ve bunu ancak panelde eksikliği fark ederek anlarsınız.

Sunucu 10 ve 11'de yönetici PowerShell'de çalıştırın:

```powershell
Import-Module WebAdministration; Get-ChildItem IIS:\Sites | Select-Object Name, Id, @{n='Klasor';e={$_.logFile.directory}}, @{n='Bicim';e={$_.logFile.logFormat}}, @{n='Alanlar';e={$_.logFile.logExtFileFlags}} | Format-List
```

> `Import-Module WebAdministration` hata veriyorsa **IIS Yönetim Betikleri ve Araçları** özelliği
> kurulu değildir: Sunucu Yöneticisi → Rol ve Özellik Ekle → Web Sunucusu (IIS) → Yönetim Araçları →
> **IIS Yönetim Betikleri ve Araçları**. Aynı bilgileri IIS Yönetimi'nden site site de görebilirsiniz
> (site → **Logging**).

Her site için üç şeyi kontrol edin:

| Kontrol | Olması gereken | Değilse |
|---|---|---|
| **Biçim** | `W3C` | IIS/NCSA/Custom biçimlerinde `#Fields:` satırı yoktur; agent o dosyadaki satırları atlar. IIS Yönetimi → site → **Logging** → Format: **W3C** |
| **Alanlar** | En az `Date, Time, ClientIP, UriStem, HttpStatus, TimeTaken` | Eksikse: Logging → **Select Fields…** → eksikleri işaretleyin. `time-taken` yoksa o sitenin **tüm** satırları atlanır |
| **Klasör** | Hepsi aynı kök altında (örn. `%SystemDrive%\inetpub\logs\LogFiles`) | Farklı bir yola bakan site varsa ya oraya alın ya da agent ayarında `LogDirectories` ile ayrıca listeleyin |

Değişiklik yaptıysanız o sitenin uygulama havuzunu geri dönüştürün ki yeni ayarla yazmaya başlasın.

**Doğrulama:** Klasördeki bugünün dosyasını açın:

```powershell
Get-Content "C:\inetpub\logs\LogFiles\W3SVC1\u_ex*.log" -Tail 3
```

Başta `#Fields: ...` satırını içeren bir dosya ve içinde `time-taken` sütunu görmelisiniz.

> IIS logları HTTP.SYS tarafından tamponlanır; kayıtlar diske en geç ~1 dakika içinde yazılır.
> Panelde anlık değil, yaklaşık bir dakikalık gecikmeyle görünmeleri normaldir.

---

## Adım 13 — Agent kurulumu (sunucu 10)

1. `publish\agent` içeriğini `C:\ServerGuard\Agent` altına kopyalayın.

2. `C:\ServerGuard\Agent\appsettings.json` dosyasında dört değeri düzenleyin:

```json
{
  "Agent": {
    "ServerName": "SERVER10",
    "ApiBaseUrl": "http://localhost:8443",
    "ApiKey": "SERVER10-ANAHTARI",
    "Traffic": {
      "LogRoot": "C:\\inetpub\\logs\\LogFiles"
    }
  }
}
```

> `LogRoot` **dolu olmalı.** Boş bırakılırsa yalnızca tek bir klasör izlenir ve 14 sitenin
> 13'ünün trafiği hiç toplanmaz. Dolduğunda altındaki tüm `W3SVC*` klasörleri izlenir; sonradan
> açtığınız siteler de 10 dakika içinde kendiliğinden yakalanır.

> `ServerName` **her sunucuda benzersiz** olmalı; panelde ayrım buna dayanır.

3. Hizmeti kurun (yönetici komut istemi):

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

```bash
sc.exe query ServerGuard.Agent
```

`STATE : 4 RUNNING` görmelisiniz. Sonra log'a bakın:

```powershell
Get-Content "C:\ServerGuard\Agent\logs\agent-*.log" -Tail 20
```

Şu satırlar olmalı:

- `Metric collector started. Server=SERVER10 ...`
- `Traffic log watcher started. ... Directories=N` → **N, site sayınız kadar olmalı**
- `Watching IIS log directory. Directory=...` → her site için bir satır

Servis hemen duruyorsa sebep log'un ilk satırlarındadır. En sık sebep: `ApiKey` boş
(bu sürümde anahtarsız agent bilerek açılmaz).

---

## Adım 14 — Agent kurulumu (sunucu 11)

Adım 13'ün aynısı, iki fark var:

```json
{
  "Agent": {
    "ServerName": "SERVER11",
    "ApiBaseUrl": "http://SUNUCU10:8443",
    "ApiKey": "SERVER11-ANAHTARI",
    "Traffic": {
      "LogRoot": "C:\\inetpub\\logs\\LogFiles"
    }
  }
}
```

`ApiBaseUrl` artık `localhost` **değil**, sunucu 10'un adı. Adım 12'yi bu sunucuda da uygulayın.

**Doğrulama:** Aynı log kontrolleri; ayrıca `Backend unreachable` satırı **olmamalı**. Varsa
güvenlik duvarı veya adres yanlıştır.

---

## Adım 15 — Kurulumu baştan sona doğrulayın

`publish\tools` klasörünü sunucu 10'a kopyalayın (örn. `C:\ServerGuard\Tools`) ve çalıştırın:

```bash
set SERVERGUARD_PASSWORD=panel-parolaniz
```

```bash
C:\ServerGuard\Tools\ServerGuard.Tools.exe check --url http://localhost:8443 --user admin --ingest-key "SERVER10-ANAHTARI"
```

Araç 12 kontrol yapar. **Hepsi `OK` olmalı**, `HATA` olan hiçbir satır kalmamalı:

| Kontrol | Ne kanıtlar |
|---|---|
| API ayakta mı | Süreç çalışıyor |
| Veritabanı erişilebilir mi | Bağlantı dizesi doğru |
| Güvenlik header'ları | Tarayıcı savunmaları devrede |
| Token'sız okuma engelleniyor mu | Yetkilendirme gerçekten açık |
| Anahtarsız yazma engelleniyor mu | Sahte veri gönderilemiyor |
| Token'sız canlı bağlantı engelleniyor mu | Hub dışarıya açık değil |
| Agent anahtarı geçerli mi | Agent'lar veri gönderebilecek |
| Hatalı parola reddediliyor mu | Parola doğrulaması çalışıyor |
| Panel oturumu açılabiliyor mu | Kullanıcı tanımı doğru |
| Token kabul ediliyor mu | Oturum akışı sağlam |
| Sunucu durumları | **Her iki sunucu da `Online` olmalı** |
| Trafik ve hata oranı | 5xx oranı ve alarm sayısı |

Sonra panelden gözle doğrulayın — `http://SUNUCU10:8443`:

- [ ] Giriş yapabiliyorum
- [ ] Sunucu kartlarında **iki sunucu** da çevrimiçi
- [ ] CPU / RAM / Disk çubukları dolu
- [ ] Trafik grafiğinde veri var (1–2 dakika bekleyin, IIS tamponu var)
- [ ] Servis sağlığı tablosunda servisleriniz listeleniyor
- [ ] Sağ üstte "Canlı" yazıyor (SignalR bağlı)
- [ ] Çıkış yapınca giriş ekranına dönüyorum

---

## Adım 16 — HTTPS (sertifika hazır olunca)

İç ağda bile önerilir: oturum token'ı ve alarm içerikleri düz metin akmasın.

1. Sunucu 10 için sertifika edinin (iç CA veya ticari) ve IIS bağlamasına ekleyin:
   IIS Yönetimi → site → **Bağlamalar** → Ekle → tür `https`, port `8443`, sertifikayı seçin.
2. Sertifikanın **agent'ların çalıştığı makinelerde de güvenilir** olduğunu doğrulayın. Sunucu
   11'den `curl.exe https://SUNUCU10:8443/health` sertifika hatası vermemeli.
3. Agent'ların `ApiBaseUrl` değerini `https://...` yapın ve hizmetleri yeniden başlatın.
4. Son olarak HTTPS'i zorunlu kılın — `web.config` içine ekleyin:

```xml
<environmentVariable name="Security__RequireHttps" value="true" />
```

> **Bu ayarı sertifika hazır olmadan açmayın.** HSTS tarayıcıya "bu adrese bir daha asla HTTP ile
> gitme" der; geri alması zordur ve agent'lar güvenilmeyen sertifika yüzünden bağlanamaz.
> Varsayılanının `false` olması bu yüzdendir.

---

## Adım 17 — Kurulum sonrası

- [ ] `ServerGuard.Tools check` görevini Görev Zamanlayıcı'ya günlük ekleyin:

```bash
schtasks /create /tn "ServerGuard Kontrol" /tr "C:\ServerGuard\Tools\ServerGuard.Tools.exe check --url http://localhost:8443" /sc daily /st 08:00 /ru SYSTEM
```

- [ ] DevExtreme lisansını uygulayın (panelin üstündeki deneme bandı kalkmalı)
- [ ] Veri saklama sürelerini gözden geçirin (aşağıda)
- [ ] `web.config` yedeğini parola yöneticisiyle aynı özende saklayın

---

## Yeni bir IIS sitesi eklerseniz

ServerGuard tarafında **hiçbir şey yapmanız gerekmez.** `LogRoot` dolu olduğu için yeni sitenin
log klasörü en geç 10 dakika içinde kendiliğinden bulunur ve izlenmeye başlar.

Yalnızca yeni sitenin log ayarlarının Adım 12'deki üç şartı sağladığından emin olun: biçim `W3C`,
`time-taken` alanı seçili, klasör `LogRoot` altında.

### Panelde siteler nasıl ayrışır

**Site adı kaydedilmiyor.** Trafik kayıtlarında sunucu adı, istek yolu, durum kodu ve yanıt süresi
var; hangi IIS sitesinden geldiği yok. Panelin "Servis sağlığı" tablosu, servis adını **istek
yolunun ilk iki segmentinden** türetir:

```
/services/kanban/Synchronize/Webhook  →  /services/kanban
/api/musteri/liste                    →  /api/musteri
```

Bunun pratik sonucu: yolları farklı olan siteler panelde doğal olarak ayrışır, **aynı yolu kullanan
iki site ise tek satırda birleşir**. Örneğin iki ayrı sitede de `/api/health` varsa, ikisinin
istekleri aynı servis gibi görünür.

Sitelerin panelde ayrı ayrı görünmesi gerekiyorsa bu bir geliştirme konusudur — trafik kaydına site
kimliği eklenmesi gerekir. Şimdilik yolları çakışmayan kurulumlarda sorun çıkarmaz.

---

## Bakım

### Veri saklama

Tablolar sınırsız büyümez; süresi dolan kayıtlar 6 saatte bir partiler hâlinde silinir.

| Anahtar (`Maintenance:Retention`) | Varsayılan |
|---|---|
| `ServerMetrics` | 30 gün |
| `TrafficLogs` | 30 gün |
| `SecurityEvents` | 90 gün |
| `SecurityAlerts` | 365 gün |

Değiştirmek için `web.config`'e ekleyin, örneğin trafiği 14 güne çekmek:

```xml
<environmentVariable name="Maintenance__Retention__TrafficLogs" value="14.00:00:00" />
```

> Kapatılırsa (`Maintenance__Retention__Enabled=false`) disk dolana kadar veri birikir ve SQL
> Server durduğunda izleme sisteminin kendisi çöker. Kapatacaksanız disk kullanımını ayrıca izleyin.

### Log dosyaları

| Bileşen | Yer | Saklama |
|---|---|---|
| API | `C:\inetpub\ServerGuard\logs\api-YYYYMMDD.log` | 30 dosya, dosya başına en fazla 50 MB |
| Agent | `C:\ServerGuard\Agent\logs\agent-YYYYMMDD.log` | 14 dosya, dosya başına en fazla 20 MB |

### Yeni sürüme geçiş

1. `deploy\Yayinla.ps1` çalıştırın.
2. Yeni migration varsa Adım 2'deki `dotnet ef database update` komutunu tekrar uygulayın.
3. Siteyi durdurun, `publish\api` içeriğini kopyalayın — **`web.config` dosyasının üzerine
   yazmayın**, sırlar orada. Siteyi başlatın.
4. Agent'ları güncelleyin (ayarları ve okuma konumunu korur):

```bash
powershell -ExecutionPolicy Bypass -File C:\ServerGuard\Yeni\Guncelle.ps1
```

5. `ServerGuard.Tools check` ile doğrulayın.

---

## Sık karşılaşılan sorunlar

| Belirti | Sebep |
|---|---|
| `500.19` / `500.31` | Hosting Bundle kurulu değil veya `iisreset` yapılmadı (Adım 1) |
| Site açılıyor, `/health` boş dönüyor | Uygulama havuzunun .NET CLR sürümü "Yönetilen kod yok" değil (Adım 6) |
| Açılışta `logs` klasörüne hiçbir şey yazılmıyor | Havuz kimliğine yazma yetkisi verilmedi (Adım 8) |
| Log'da eksik ortam değişkeni listesi | `web.config` düzenlemesi eksik (Adım 9); listedeki adları birebir kullanın |
| `/health` Healthy, `/health/ready` değil | SQL bağlantısı kurulamıyor: kullanıcı, parola veya sunucu adı yanlış |
| Panel açılıyor ama giriş kabul edilmiyor | Parola özeti eksik/yanlış kopyalanmış. `$` işaretleri dahil tamamı yapıştırılmalı |
| Agent servisi hemen duruyor | `ApiKey` boş veya bir ayar aralık dışında; sebep agent log'unun ilk satırlarında |
| Agent log'unda `Backend rejected the agent API key` | Anahtar API'deki listede yok. Kayıtlar kuyrukta bekler, düzeltince gönderilir |
| Panelde sunucu var, trafik yok | Adım 12 atlanmış: site biçimi W3C değil veya `time-taken` seçili değil |
| Bazı siteler panelde yok | `LogRoot` boş bırakılmış veya o sitenin log klasörü başka bir yolda |
| Trafik 1–2 dakika gecikmeli | Normal. IIS log tamponu (HTTP.SYS) periyodik boşalır |
