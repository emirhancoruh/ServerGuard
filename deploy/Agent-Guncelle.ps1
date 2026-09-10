<#
.SYNOPSIS
    ServerGuard Agent'i yerinde gunceller.

.DESCRIPTION
    Servisi durdurur, yeni dosyalari kopyalar ve servisi yeniden baslatir.

    Iki dosya korunur:
      * traffic-offset.json - log okuma konumu. Silinirse agent gunun tum IIS
        logunu bastan okur ve mukerrer kayit olusur.
      * appsettings.json    - sunucuya ozel ayarlar (ServerName, ApiKey, log yollari).
        Uzerine yazilsaydi her guncellemede ayarlar kaybolurdu.

    Yeni surumde zorunlu hale gelen ayarlar -ApiKey ve -LogRoot parametreleriyle
    mevcut dosyaya eklenebilir; dosyanin geri kalani oldugu gibi kalir.

.PARAMETER InstallPath
    Agent'in kurulu oldugu klasor.

.PARAMETER ApiKey
    API'nin bu agent icin tanidigi anahtar. Verilirse mevcut appsettings.json
    icine yazilir. 'ServerGuard.Tools new-key' ile uretilir.

.PARAMETER LogRoot
    Cok siteli IIS sunucularinda tum site log klasorlerini barindiran kok dizin
    (ornek: C:\inetpub\logs\LogFiles). Verilirse altindaki tum siteler izlenir.

.EXAMPLE
    .\Guncelle.ps1
    .\Guncelle.ps1 -ApiKey "..." -LogRoot "C:\inetpub\logs\LogFiles"
    .\Guncelle.ps1 -InstallPath "D:\ServerGuard\Agent"
#>
[CmdletBinding()]
param(
    [string]$InstallPath = 'C:\ServerGuard\Agent',
    [string]$ServiceName = 'ServerGuard.Agent',
    [string]$ApiKey,
    [string]$LogRoot
)

$ErrorActionPreference = 'Stop'

$sourcePath      = $PSScriptRoot
$offsetFileName  = 'traffic-offset.json'
$settingsName    = 'appsettings.json'
$preservedFiles  = @('Guncelle.ps1', $offsetFileName, $settingsName)

function Write-Step { param([string]$Text) Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "    $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "    $Text" -ForegroundColor Yellow }

# --- Yonetici kontrolu ---------------------------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
           ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    throw 'Bu betik yonetici olarak calistirilmalidir (servis durdurma/baslatma yetkisi gerekir).'
}

# --- On kontroller -------------------------------------------------------
Write-Step 'On kontroller'

if (-not (Test-Path (Join-Path $sourcePath 'ServerGuard.Agent.exe'))) {
    throw "Yeni dosyalar bulunamadi: $sourcePath icinde ServerGuard.Agent.exe yok."
}

if (-not (Test-Path $InstallPath)) {
    throw "Kurulum klasoru bulunamadi: $InstallPath. -InstallPath ile dogru yolu verin."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $service) {
    throw "Servis bulunamadi: $ServiceName. Once 'sc.exe create' ile kurulmali."
}

$settingsPath = Join-Path $InstallPath $settingsName
if (-not (Test-Path $settingsPath)) {
    throw "Ayar dosyasi bulunamadi: $settingsPath. Once temiz kurulum yapin."
}

Write-Ok "Kaynak    : $sourcePath"
Write-Ok "Hedef     : $InstallPath"
Write-Ok "Servis    : $ServiceName ($($service.Status))"

# --- Yeni ayarlarin hazirlanmasi -----------------------------------------
# Ayarlar servis durdurulmadan once hazirlanir ve dogrulanir: gecersiz bir
# duzenleme yuzunden servis durup bir daha acilamaz duruma gelmemeli.
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settingsChanged = $false

if ($ApiKey) {
    if (-not $settings.Agent) { throw "Ayar dosyasinda 'Agent' bolumu yok: $settingsPath" }
    $settings.Agent | Add-Member -NotePropertyName 'ApiKey' -NotePropertyValue $ApiKey -Force
    $settingsChanged = $true
    Write-Ok 'ApiKey ayarlanacak.'
}

if ($LogRoot) {
    if (-not $settings.Agent.Traffic) { throw "Ayar dosyasinda 'Agent:Traffic' bolumu yok: $settingsPath" }
    $settings.Agent.Traffic | Add-Member -NotePropertyName 'LogRoot' -NotePropertyValue $LogRoot -Force
    $settingsChanged = $true
    Write-Ok "LogRoot ayarlanacak: $LogRoot"
}

$existingApiKey = $null
if ($settings.Agent) { $existingApiKey = $settings.Agent.ApiKey }

if ([string]::IsNullOrWhiteSpace($existingApiKey)) {
    throw @'
Agent:ApiKey tanimli degil. Bu surumde anahtarsiz agent acilmaz.
Anahtari API sunucusunda 'ServerGuard.Tools new-key' ile uretip
bu betigi -ApiKey "<anahtar>" parametresiyle yeniden calistirin.
'@
}

# --- Servisi durdur ------------------------------------------------------
Write-Step 'Servis durduruluyor'

if ($service.Status -ne 'Stopped') {
    Stop-Service -Name $ServiceName -Force
    $service.WaitForStatus('Stopped', (New-TimeSpan -Seconds 30))
    Write-Ok 'Durduruldu.'
}
else {
    Write-Ok 'Zaten duruyordu.'
}

# --- Yedekle -------------------------------------------------------------
Write-Step 'Mevcut durum yedekleniyor'

$offsetPath = Join-Path $InstallPath $offsetFileName
$backupRoot = Join-Path $InstallPath ('yedek-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))

New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

if (Test-Path $offsetPath) {
    Copy-Item $offsetPath (Join-Path $backupRoot $offsetFileName) -Force
    Write-Ok "Okuma konumu yedeklendi: $backupRoot\$offsetFileName"
}
else {
    Write-Warn 'Konum dosyasi yok; agent ilk calismasinda bastan okuyacak.'
}

Copy-Item $settingsPath (Join-Path $backupRoot $settingsName) -Force
Write-Ok "Ayarlar yedeklendi: $backupRoot\$settingsName"

# --- Yeni dosyalari kopyala ---------------------------------------------
Write-Step 'Yeni dosyalar kopyalaniyor'

$filesToCopy = Get-ChildItem -Path $sourcePath -File |
    Where-Object { $preservedFiles -notcontains $_.Name }

foreach ($file in $filesToCopy) {
    Copy-Item $file.FullName (Join-Path $InstallPath $file.Name) -Force
}

Write-Ok "$($filesToCopy.Count) dosya kopyalandi."
Write-Ok 'appsettings.json ve traffic-offset.json korundu.'

# --- Ayarlari yaz --------------------------------------------------------
if ($settingsChanged) {
    Write-Step 'Ayarlar guncelleniyor'
    $settings | ConvertTo-Json -Depth 20 | Set-Content $settingsPath -Encoding utf8

    # Yazilan dosyanin okunabilirligi hemen dogrulanir; bozuk JSON ile servis acilmaz.
    $null = Get-Content $settingsPath -Raw | ConvertFrom-Json
    Write-Ok 'Ayar dosyasi guncellendi ve dogrulandi.'
}

# --- Servisi baslat ------------------------------------------------------
Write-Step 'Servis baslatiliyor'

Start-Service -Name $ServiceName
(Get-Service -Name $ServiceName).WaitForStatus('Running', (New-TimeSpan -Seconds 30))

Write-Ok 'Calisiyor.'

# --- Dogrulama -----------------------------------------------------------
Write-Step 'Dogrulama'

Start-Sleep -Seconds 5
$final = Get-Service -Name $ServiceName

if ($final.Status -eq 'Running') {
    Write-Ok "Servis durumu: $($final.Status)"
    Write-Host "`nGuncelleme tamamlandi." -ForegroundColor Green
    Write-Host "Agent log'u: $InstallPath\logs" -ForegroundColor Gray
    Write-Host "Sorun olursa yedek burada: $backupRoot" -ForegroundColor Gray
}
else {
    Write-Warn "Servis beklenen durumda degil: $($final.Status)"
    Write-Warn 'Hatayi gormek icin konsoldan calistirin:'
    Write-Warn "  $InstallPath\ServerGuard.Agent.exe"
    Write-Warn "Geri donmek icin yedek: $backupRoot"
}
