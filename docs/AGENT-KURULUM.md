# ServerGuard.Agent — Sunucuya Kurulum

Bu doküman, agent'ı yeni bir sunucuya (ör. ikinci sunucunuza) Windows Service olarak kurmayı anlatır.

Agent her sunucuda **bağımsız** çalışır ve topladığı veriyi merkezî `ServerGuard.Api`'ye gönderir.
Sunucular birbirini tanımaz; panelde ayrışmaları yalnızca `ServerName` alanına dayanır.

---

## 1. Değiştirilmesi gereken ayarlar

Yayınlanan klasördeki `appsettings.json` dosyasında **her sunucuda farklı olması gereken** üç değer vardır:

| Ayar | Ne yapmalı | Örnek |
|---|---|---|
| `Agent:ServerName` | **Her sunucuda benzersiz olmalı.** Panelde bu isimle görünür. | `"web-01"` / `"db-01"` |
| `Agent:ApiBaseUrl` | Merkezî Api'nin adresi. Api başka bir makinedeyse `localhost` **olmaz**. | `"http://10.0.0.5:5190"` |
| `Agent:Traffic:LogDirectory` | O sunucudaki IIS sitesinin log klasörü. Site kimliği farklıysa `W3SVC` numarası da farklıdır. | `"C:\\inetpub\\logs\\LogFiles\\W3SVC2"` |

Geri kalan ayarlar (toplama aralıkları, kuyruk kapasiteleri, eşikler) genellikle olduğu gibi bırakılabilir.

### İkinci sunucu için örnek `appsettings.json`

```json
{
  "Agent": {
    "ServerName": "db-01",
    "ApiBaseUrl": "http://10.0.0.5:5190",
    "Metrics": {
      "Enabled": true,
      "CollectionInterval": "00:00:10",
      "QueueCapacity": 1000
    },
    "SecurityEvents": {
      "Enabled": true,
      "FlushInterval": "00:00:05",
      "QueueCapacity": 5000
    },
    "Traffic": {
      "Enabled": true,
      "LogDirectory": "C:\\inetpub\\logs\\LogFiles\\W3SVC1",
      "FilePattern": "u_ex*.log",
      "OffsetFilePath": "traffic-offset.json",
      "PollInterval": "00:00:02",
      "MaxLinesPerCycle": 2000,
      "QueueCapacity": 5000,
      "ReadExistingFileOnFirstRun": true
    }
  }
}
```

> **`ServerName` boş bırakılırsa** makine adı kullanılır. İki sunucunun makine adı zaten farklıysa bu
> yeterlidir; ama panelde okunaklı isimler (`web-01`, `db-01`) görmek için açıkça yazmanız önerilir.

> **`ReadExistingFileOnFirstRun`**: ilk çalıştırmada o günün IIS logu **baştan** okunur. Yoğun bir
> sitede bu, kuruluma tek seferlik büyük bir yük bindirir ve geçmiş trafik için de anomali alarmı
> üretebilir. İstemiyorsanız `false` yapın.

---

## 2. Yayınlama (publish)

Geliştirme makinesinde:

```bash
dotnet publish src/ServerGuard.Agent -c Release -o publish/agent
```

`publish/agent` klasörünü hedef sunucuya kopyalayın, örneğin `C:\ServerGuard\Agent` altına.
Kopyaladıktan sonra oradaki `appsettings.json` dosyasını 1. adımdaki gibi düzenleyin.

Hedef sunucuda **.NET 9 Runtime** kurulu olmalıdır. Kurulu değilse ya runtime kurun ya da
kendi kendine yeten bir paket üretin:

```bash
dotnet publish src/ServerGuard.Agent -c Release -r win-x64 --self-contained true -o publish/agent
```

---

## 3. Windows Service olarak kaydetme

Hedef sunucuda **yönetici** PowerShell açın.

```bash
sc.exe create ServerGuard.Agent binPath= "C:\ServerGuard\Agent\ServerGuard.Agent.exe" start= auto DisplayName= "ServerGuard Agent"
```

> `sc.exe` sözdizimi kritiktir: `binPath=` ile değer arasında **boşluk vardır**, eşittirden önce
> boşluk **yoktur**. Yol boşluk içeriyorsa tırnak içine alın.

Açıklama eklemek (isteğe bağlı):

```bash
sc.exe description ServerGuard.Agent "Sunucu metriklerini, guvenlik olaylarini ve IIS trafigini ServerGuard.Api'ye gonderir."
```

Servis çökerse otomatik yeniden başlaması için (önerilir):

```bash
sc.exe failure ServerGuard.Agent reset= 86400 actions= restart/5000/restart/10000/restart/30000
```

Başlatma:

```bash
sc.exe start ServerGuard.Agent
```

Durum kontrolü:

```bash
sc.exe query ServerGuard.Agent
```

### Kaldırma

```bash
sc.exe stop ServerGuard.Agent
```

```bash
sc.exe delete ServerGuard.Agent
```

---

## 4. Yetkiler (en az ayrıcalık)

Servis varsayılan olarak `LocalSystem` hesabıyla çalışır. **En az ayrıcalık** prensibi için ayrı bir
düşük yetkili hesap kullanmanız önerilir:

```bash
sc.exe config ServerGuard.Agent obj= ".\ServerGuardSvc" password= "..."
```

Bu hesabın ihtiyaç duyduğu yetkiler:

| İhtiyaç | Gereken yetki |
|---|---|
| Windows Security kanalını okumak (4624/4625) | Yerel **Event Log Readers** grubu üyeliği |
| IIS log klasörünü okumak | O klasörde **okuma** izni |
| Kendi klasörüne konum dosyası yazmak | Kurulum klasöründe **yazma** izni |

**Administrator yetkisi gerekmez.**

Event Log Readers grubuna ekleme (grup adı Windows'un diline göre değişir, bu yüzden SID kullanılır):

```bash
Add-LocalGroupMember -SID 'S-1-5-32-573' -Member 'MAKINE\ServerGuardSvc'
```

Doğrulama:

```bash
Get-LocalGroupMember -SID 'S-1-5-32-573'
```

> Grup üyeliği oturum açma anında değerlendirilir: değişiklikten sonra **servisi yeniden başlatın**.

Yetki verilmezse agent çökmez; güvenlik olayı toplayıcısı açıklayıcı bir hata yazıp durur,
metrik ve trafik toplama çalışmaya devam eder.

---

## 5. Ağ

Agent'tan Api'ye giden trafiğin açık olması gerekir:

- Api sunucusunda gelen bağlantı için **güvenlik duvarı kuralı** (varsayılan port `5190`, HTTPS için `7066`).
- Agent sunucusundan test:

```bash
curl http://10.0.0.5:5190/health
```

`Healthy` dönmüyorsa agent veri gönderemez. Bu durumda agent veriyi kuyrukta biriktirir, bağlantı
gelince gönderir; ama uzun kesintide kuyruk dolar ve en eski kayıtlar (loglanarak) atılır.

---

## 6. Kurulum sonrası doğrulama

1. **Servis çalışıyor mu?**

```bash
sc.exe query ServerGuard.Agent
```

`STATE : 4 RUNNING` görmelisiniz.

2. **Loglar ne diyor?** Agent, Windows Service olarak çalışırken Event Viewer → Windows Logs →
   Application altına yazar. Şu satırları arayın:
   - `Metric collector started. Server=...`
   - `Security event watcher started.` (yetki yoksa bunun yerine Event Log Readers uyarısı)
   - `Traffic log watcher started.` (IIS yoksa yol uyarısı)

3. **Api sunucusunda sunucu göründü mü?**

```bash
curl "http://10.0.0.5:5190/api/servers"
```

Yeni `ServerName` listede olmalı.

4. **Panelde:** sağ üstteki **Sunucu** listesinde yeni sunucu belirmeli. Seçtiğinizde tüm ekranlar
   (CPU/RAM kartları, trafik grafiği, top IP tablosu, alarm listesi) yalnızca o sunucuyu göstermeli.

> Panel açıkken yeni bir sunucu eklendiyse listede görünmesi için sayfayı yenileyin; sunucu listesi
> açılışta bir kez çekilir.

---

## 7. Sık karşılaşılan sorunlar

| Belirti | Olası sebep |
|---|---|
| Servis başlıyor sonra hemen duruyor | `appsettings.json` hatalı: `ApiBaseUrl` eksik veya bir ayar izin verilen aralığın dışında. Konfigürasyon açılışta doğrulanır ve hata Event Viewer'a yazılır. |
| Panelde sunucu görünmüyor | Agent Api'ye ulaşamıyor (ağ/güvenlik duvarı) ya da `ApiBaseUrl` yanlış. |
| İki sunucu panelde tek satır gibi görünüyor | İkisinde de `ServerName` aynı. Her sunucuda benzersiz olmalı. |
| Güvenlik olayları gelmiyor | Servis hesabı Event Log Readers grubunda değil. |
| Trafik verisi gelmiyor | `LogDirectory` yanlış, ya da IIS logları henüz diske yazılmadı (IIS tamponu periyodik boşaltır). |
| Trafik verisi çok gecikmeli geliyor | IIS log tampon boşaltma süresi uzun. IIS Yönetimi → Logging → log dosyası ayarlarından kısaltın. |
