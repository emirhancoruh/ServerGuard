# Yayınlama Rehberi

ServerGuard'ı **10 numaralı sunucuya** (API + panel + agent) ve **11 numaralı sunucuya** (yalnızca
agent) kurmak için adım adım yol.

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

---

## 0. Ön koşullar

Sunucu 10'da:

| Gereksinim | Not |
|---|---|
| **ASP.NET Core 9 Hosting Bundle** | IIS'in .NET uygulamasını çalıştırabilmesi için zorunlu. Kurulumdan sonra `iisreset`. |
| **IIS** | Zaten kurulu. |
| **SQL Server** | Mevcut örnek kullanılabilir. |
| **PowerShell 5.1+** | Kurulum betikleri için. |

Sunucu 11'de:

| Gereksinim | Not |
|---|---|
| **.NET 9 Runtime** | Agent framework'e bağımlı yayınlanır. |

---

## 1. Sırları üretin

Sırlar **hiçbir zaman** dosyaya veya depoya yazılmaz. Üretmek için yardımcı araç kullanılır.

Geliştirme makinenizde:

```bash
dotnet run --project src/ServerGuard.Tools -- hash-password --user admin
```

Parola sorulur (ekrana yazılmaz) ve size bir özet verir. Bu özeti not alın.

Her sunucu için ayrı bir agent anahtarı üretin — biri sızarsa yalnızca o iptal edilir:

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name SERVER10 --index 0
```

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name SERVER11 --index 1
```

Bir de token imza anahtarı gerekir. Herhangi bir `new-key` çıktısındaki anahtar kullanılabilir:

```bash
dotnet run --project src/ServerGuard.Tools -- new-key --name JWT
```

> Bu değerleri parola yöneticisine kaydedin. İmza anahtarı değiştirilirse **tüm panel oturumları
> düşer** — acil durumda erişimi kesmenin yolu da budur.

---

## 2. Paketi hazırlayın

```bash
powershell -ExecutionPolicy Bypass -File .\deploy\Yayinla.ps1
```

Betik sırasıyla: Angular panelini derler, API'nin `wwwroot`'una kopyalar, üç projeyi de
`publish/` altına yayınlar ve **paket içinde sır kalmadığını doğrular**. Bir `appsettings.json`
içinde dolu bir sır bulursa işlem durur.

Çıktı:

```
publish\api     → IIS'e kopyalanacak (panel dahil)
publish\agent   → her sunucuya kopyalanacak
publish\tools   → doğrulama aracı
```

---

## 3. Veritabanı

Sunucu 10'da veritabanını oluşturun veya güncelleyin. Geliştirme makinenizden, sunucunun SQL'ine
bağlanan bir connection string ile:

```bash
dotnet ef database update --project src/ServerGuard.Api --connection "Server=SUNUCU10;Database=ServerGuard;..."
```

> **SQL hesabı `sa` olmamalıdır.** ServerGuard için ayrı bir kullanıcı açın ve yalnızca
> `ServerGuard` veritabanında `db_datareader` + `db_datawriter` verin. Şema değişikliği gerektiren
> `dotnet ef database update` işlemini yetkili bir hesapla, uygulamanın günlük çalışmasını kısıtlı
> hesapla yapın.

---

## 4. IIS sitesi (sunucu 10)

### 4.1 Dosyaları kopyalayın

`publish\api` içeriğini örneğin `C:\inetpub\ServerGuard` altına kopyalayın.

### 4.2 Uygulama havuzu

IIS Yönetimi → Uygulama Havuzları → yeni havuz:

| Ayar | Değer |
|---|---|
| .NET CLR sürümü | **Yönetilen kod yok** |
| Yönetilen ardışık düzen | Tümleşik |
| Kimlik | `ApplicationPoolIdentity` (varsayılan) |

### 4.3 Site

Yeni site: fiziksel yol `C:\inetpub\ServerGuard`, oluşturduğunuz havuz, bağlama portu (ör. 443 veya 8443).

### 4.4 Klasör izinleri

Uygulama kendi log'unu `logs` klasörüne yazar. Havuz kimliğine yazma yetkisi verin:

```bash
icacls "C:\inetpub\ServerGuard\logs" /grant "IIS AppPool\ServerGuard:(OI)(CI)M" /T
```

> Klasör yoksa önce oluşturun. Bu yetki verilmezse uygulama çalışır ama **hiçbir log tutulmaz** —
> sorun çıktığında elinizde kayıt olmaz.

### 4.5 Sırları verin

Sırlar `web.config` içindeki `<environmentVariables>` bölümüne yazılır. `C:\inetpub\ServerGuard\web.config`
dosyasını açın ve `<aspNetCore ...>` etiketini şu hale getirin:

```xml
<aspNetCore processPath="dotnet" arguments=".\ServerGuard.Api.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ConnectionStrings__ServerGuard" value="Server=...;Database=ServerGuard;User Id=serverguard;Password=...;TrustServerCertificate=True;" />
    <environmentVariable name="Security__Jwt__SigningKey" value="ADIM-1-DEKI-IMZA-ANAHTARI" />
    <environmentVariable name="Security__Panel__Users__0__UserName" value="admin" />
    <environmentVariable name="Security__Panel__Users__0__PasswordHash" value="pbkdf2-sha256$..." />
    <environmentVariable name="Security__Ingest__ApiKeys__0__Name" value="SERVER10" />
    <environmentVariable name="Security__Ingest__ApiKeys__0__Key" value="SERVER10-ANAHTARI" />
    <environmentVariable name="Security__Ingest__ApiKeys__1__Name" value="SERVER11" />
    <environmentVariable name="Security__Ingest__ApiKeys__1__Key" value="SERVER11-ANAHTARI" />
  </environmentVariables>
</aspNetCore>
```

Opsiyonel bildirim ve itibar servisi için:

```xml
<environmentVariable name="Notifications__Telegram__BotToken" value="..." />
<environmentVariable name="Notifications__Telegram__ChatId" value="..." />
<environmentVariable name="Detection__IpReputation__ApiKey" value="..." />
```

> **Bu dosya artık sır içerir.** NTFS izinlerini daraltın: yalnızca Administrators ve uygulama
> havuzu kimliği okuyabilsin. Dosyayı depoya, e-postaya veya sohbete koymayın.

> API, sırlar eksikken **açılmaz** ve hangi ortam değişkeninin eksik olduğunu tek tek yazar.
> Bu bilinçlidir: eksik yapılandırmayla yarı çalışan bir izleme sistemi, hiç açılmayandan tehlikelidir.

### 4.6 Siteyi başlatın ve doğrulayın

```bash
curl -k https://SUNUCU10:PORT/health
```

`Healthy` dönmeli. Dönmüyorsa `C:\inetpub\ServerGuard\logs` altındaki log dosyasına bakın.

---

## 5. HTTPS

İç ağda bile paneli HTTPS'e almanız önerilir: oturum token'ı ve alarm içerikleri düz metin akmasın.

1. Sunucu 10 için bir sertifika edinin (iç CA veya ticari) ve IIS bağlamasına ekleyin.
2. Sertifika **agent'ların çalıştığı makinelerde de güvenilir** olmalıdır. Aksi halde agent
   bağlanamaz.
3. Sertifika hazır olduğunda HTTPS'i zorunlu kılın:

```xml
<environmentVariable name="Security__RequireHttps" value="true" />
```

Bu ayar hem HTTP→HTTPS yönlendirmesini hem de HSTS başlığını açar.

> **Sertifika hazır değilken bu ayarı açmayın.** HSTS tarayıcıya "bu adrese bir daha asla HTTP ile
> gitme" der; geri alması zordur ve agent'lar güvenilmeyen sertifika yüzünden bağlantı kuramaz.
> Varsayılan `false` olması bu yüzdendir.

---

## 6. Agent kurulumu

Her iki sunucuda da aynı adımlar; yalnızca `ServerName` ve `ApiKey` farklıdır.

### 6.1 Kopyalayın

`publish\agent` içeriğini `C:\ServerGuard\Agent` altına kopyalayın.

### 6.2 Ayarlayın

`C:\ServerGuard\Agent\appsettings.json`:

```json
{
  "Agent": {
    "ServerName": "SERVER10",
    "ApiBaseUrl": "https://sunucu10:8443",
    "ApiKey": "O-SUNUCUYA-AIT-ANAHTAR",
    "Traffic": {
      "LogRoot": "C:\\inetpub\\logs\\LogFiles"
    }
  }
}
```

> `LogRoot` verildiğinde altındaki **tüm** `W3SVC*` klasörleri izlenir; 14 sitenin hepsi tek ayarla
> kapsanır ve sonradan açılan siteler `DirectoryRescanInterval` (varsayılan 10 dk) içinde
> kendiliğinden yakalanır. Tek site izlemek isterseniz `LogRoot`'u boş bırakıp `LogDirectory`
> kullanın.

> `ApiKey` boşsa agent **hiç açılmaz** ve sebebini log'a yazar. Anahtarsız bir agent zaten tek bir
> kaydı bile teslim edemez; sessizce çalışıp veri kaybetmesindense açılışta durması yeğdir.

### 6.3 Hizmet olarak kurun

```bash
sc.exe create ServerGuard.Agent binPath= "C:\ServerGuard\Agent\ServerGuard.Agent.exe" start= auto
```

```bash
sc.exe start ServerGuard.Agent
```

Ayrıntılı adımlar, yetkiler ve sorun giderme: [AGENT-KURULUM.md](AGENT-KURULUM.md)

### 6.4 Mevcut bir agent'ı güncelleme

Zaten kurulu bir agent varsa (ör. YLNSERVER) paketi kopyalayıp güncelleme betiğini çalıştırın.
Betik `appsettings.json` ve okuma konumunu **korur**, yalnızca program dosyalarını değiştirir:

```bash
powershell -ExecutionPolicy Bypass -File C:\ServerGuard\Yeni\Guncelle.ps1 -ApiKey "ANAHTAR" -LogRoot "C:\inetpub\logs\LogFiles"
```

---

## 7. Güvenlik duvarı

Agent'ların API'ye ulaşabilmesi için sunucu 10'da ilgili portu açın. Kaynağı **yalnızca kendi
sunucularınızla** sınırlayın:

```bash
netsh advfirewall firewall add rule name="ServerGuard API" dir=in action=allow protocol=TCP localport=8443 remoteip=10.0.0.0/24
```

> Windows, bir programa ilk kez bağlantı geldiğinde kendiliğinden "program tabanlı" bir kural
> oluşturabilir ve bu kural tüm profilleri kapsayabilir. `wf.msc` içinden gereksiz kuralları
> temizleyin; aksi halde port sandığınızdan geniş bir kitleye açık olur.

---

## 8. Kurulumu doğrulayın

`publish\tools` klasörünü sunucu 10'a (veya kendi makinenize) kopyalayın ve dışarıdan kontrol edin:

```bash
ServerGuard.Tools.exe check --url https://sunucu10:8443 --user admin --ingest-key "SERVER10-ANAHTARI"
```

Parola sorulmaz; ortam değişkeninden okunur:

```bash
set SERVERGUARD_PASSWORD=parolaniz
```

Araç sırasıyla şunları doğrular:

| Kontrol | Ne kanıtlar |
|---|---|
| API ayakta mı | Süreç çalışıyor |
| Veritabanı erişilebilir mi | Connection string ve SQL doğru |
| Güvenlik header'ları | Tarayıcı savunmaları devrede |
| Token'sız okuma engelleniyor mu | Yetkilendirme **gerçekten** açık |
| Anahtarsız yazma engelleniyor mu | Sahte veri gönderilemiyor |
| Token'sız canlı bağlantı engelleniyor mu | Hub dışarıya açık değil |
| Agent anahtarı geçerli mi | Agent'lar veri gönderebilecek |
| Hatalı parola reddediliyor mu | Parola doğrulaması çalışıyor |
| Panel oturumu açılabiliyor mu | Kullanıcı tanımı doğru |
| Sunucu durumları | Hangi sunucudan veri geliyor |
| Trafik ve hata oranı | 5xx oranı ve alarm sayısı |

Başarısız kontrol varsa çıkış kodu `1` olur. Görev Zamanlayıcı'ya günlük bir görev olarak eklerseniz,
bir şey bozulduğunda görev başarısız görünür:

```bash
schtasks /create /tn "ServerGuard Kontrol" /tr "C:\ServerGuard\Tools\ServerGuard.Tools.exe check --url https://sunucu10:8443" /sc daily /st 08:00 /ru SYSTEM
```

> Hiçbir kontrol veritabanına kayıt yazmaz. Agent anahtarı kasıtlı olarak geçersiz bir gövdeyle
> denenir: anahtar geçerliyse doğrulama hatası (400), geçersizse yetki hatası (401) döner. İki
> durum ayırt edilir ve veri kirlenmez.

---

## 9. Kurulum sonrası kontrol listesi

- [ ] `web.config` NTFS izinleri daraltıldı
- [ ] `logs` klasörüne yazma yetkisi verildi ve dosya oluştu
- [ ] SQL hesabı `sa` değil, yalnızca `ServerGuard` veritabanında yetkili
- [ ] Güvenlik duvarı kuralı yalnızca kendi ağınızı kapsıyor
- [ ] `ServerGuard.Tools check` tüm kontrolleri geçiyor
- [ ] Her iki sunucu da panelde **Çevrimiçi** görünüyor
- [ ] Panele giriş çalışıyor, çıkış yapınca oturum kapanıyor
- [ ] Sertifika kurulduysa `Security__RequireHttps=true` yapıldı
- [ ] DevExtreme lisansı uygulandı (panelin üstündeki deneme bandı kalktı)
- [ ] Veri saklama süreleri gözden geçirildi (`Maintenance:Retention`)

---

## 10. Bakım

### Veri saklama

Tablolar sınırsız büyümez; süresi dolan kayıtlar 6 saatte bir partiler hâlinde silinir.

| Anahtar (`Maintenance:Retention`) | Varsayılan |
|---|---|
| `ServerMetrics` | 30 gün |
| `TrafficLogs` | 30 gün |
| `SecurityEvents` | 90 gün |
| `SecurityAlerts` | 365 gün |

Değiştirmek için ortam değişkeni ekleyin, örneğin trafiği 14 güne çekmek:

```xml
<environmentVariable name="Maintenance__Retention__TrafficLogs" value="14.00:00:00" />
```

> Bu ayar kapatılırsa (`Maintenance__Retention__Enabled=false`) disk dolana kadar veri birikir ve
> SQL Server durduğunda izleme sisteminin kendisi çöker. Kapatacaksanız disk kullanımını ayrıca izleyin.

### Log dosyaları

| Bileşen | Yer | Saklama |
|---|---|---|
| API | `<site>\logs\api-YYYYMMDD.log` | 30 dosya, dosya başına en fazla 50 MB |
| Agent | `C:\ServerGuard\Agent\logs\agent-YYYYMMDD.log` | 14 dosya, dosya başına en fazla 20 MB |

### Yeni sürüme geçiş

1. `deploy\Yayinla.ps1` çalıştırın.
2. Yeni migration varsa `dotnet ef database update` uygulayın.
3. IIS sitesini durdurun, `publish\api` içeriğini kopyalayın (`web.config` dosyasının üzerine
   yazmayın — sırlar orada), siteyi başlatın.
4. Agent'ları `Guncelle.ps1` ile güncelleyin.
5. `ServerGuard.Tools check` ile doğrulayın.
