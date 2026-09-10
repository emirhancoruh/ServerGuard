<#
.SYNOPSIS
    ServerGuard'i yayina hazirlar: panel, API, agent ve yardimci arac.

.DESCRIPTION
    Angular panelini derleyip API'nin wwwroot'una kopyalar, ardindan uc projeyi de
    publish/ altina yayinlar. Panel API ile ayni kaynaktan servis edildigi icin
    kurulumda tek IIS sitesi, tek sertifika yeterlidir ve CORS'a gerek kalmaz.

    Paketleme oncesinde appsettings.json dosyalarinda sir birakilip birakilmadigi
    kontrol edilir; bulunursa islem durur. Sirlar yalnizca sunucudaki ortam
    degiskenlerinde bulunmalidir.

.PARAMETER OutputPath
    Yayin ciktilarinin yazilacagi klasor.

.PARAMETER SkipWeb
    Angular derlemesini atlar. Yalnizca backend degistiyse zaman kazandirir.

.EXAMPLE
    .\deploy\Yayinla.ps1
    .\deploy\Yayinla.ps1 -SkipWeb
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$SkipWeb
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $repositoryRoot 'publish' }

$apiProject    = Join-Path $repositoryRoot 'src\ServerGuard.Api\ServerGuard.Api.csproj'
$agentProject  = Join-Path $repositoryRoot 'src\ServerGuard.Agent\ServerGuard.Agent.csproj'
$toolsProject  = Join-Path $repositoryRoot 'src\ServerGuard.Tools\ServerGuard.Tools.csproj'
$testProject   = Join-Path $repositoryRoot 'tests\ServerGuard.UnitTests\ServerGuard.UnitTests.csproj'
$webPath       = Join-Path $repositoryRoot 'src\ServerGuard.Web'
$webRoot       = Join-Path $repositoryRoot 'src\ServerGuard.Api\wwwroot'
$updateScript  = Join-Path $PSScriptRoot 'Agent-Guncelle.ps1'

$apiOutput   = Join-Path $OutputPath 'api'
$agentOutput = Join-Path $OutputPath 'agent'
$toolsOutput = Join-Path $OutputPath 'tools'

# Yayin klasoru her seferinde sifirdan olusturulur. Onceki bir yayindan kalan DLL,
# yeni surumun bagimliliklariyla catisip uygulamanin acilmamasina yol acabilir;
# 'dotnet publish' hedef klasoru kendiliginden temizlemez.
function Reset-Directory {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Write-Step { param([string]$Text) Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "    $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "    $Text" -ForegroundColor Yellow }

# --- Sir sizintisi kontrolu ----------------------------------------------
# Bu kontrol paketleme oncesinde calisir: bir sir yanlislikla appsettings.json'a
# yazildiysa, sunucuya tasinmadan once burada yakalanir.
function Assert-NoSecrets {
    param([string]$SettingsPath)

    if (-not (Test-Path $SettingsPath)) { return }

    $settings = Get-Content $SettingsPath -Raw | ConvertFrom-Json

    $checks = @(
        @{ Path = 'ConnectionStrings.ServerGuard';      Value = $settings.ConnectionStrings.ServerGuard },
        @{ Path = 'Security.Jwt.SigningKey';            Value = $settings.Security.Jwt.SigningKey },
        @{ Path = 'Detection.IpReputation.ApiKey';      Value = $settings.Detection.IpReputation.ApiKey },
        @{ Path = 'Notifications.Telegram.BotToken';    Value = $settings.Notifications.Telegram.BotToken },
        @{ Path = 'Notifications.Telegram.ChatId';      Value = $settings.Notifications.Telegram.ChatId },
        @{ Path = 'Agent.ApiKey';                       Value = $settings.Agent.ApiKey }
    )

    foreach ($check in $checks) {
        if (-not [string]::IsNullOrWhiteSpace($check.Value)) {
            throw "Sir sizintisi: $SettingsPath icindeki '$($check.Path)' dolu. Degeri silin; sirlar ortam degiskeninde tutulur."
        }
    }

    if ($settings.Security -and $settings.Security.Panel -and $settings.Security.Panel.Users.Count -gt 0) {
        throw "Sir sizintisi: $SettingsPath icinde Security:Panel:Users dolu. Kullanicilar ortam degiskeniyle verilmelidir."
    }

    if ($settings.Security -and $settings.Security.Ingest -and $settings.Security.Ingest.ApiKeys.Count -gt 0) {
        throw "Sir sizintisi: $SettingsPath icinde Security:Ingest:ApiKeys dolu. Anahtarlar ortam degiskeniyle verilmelidir."
    }
}

Write-Step 'Sir sizintisi kontrolu'
Assert-NoSecrets (Join-Path $repositoryRoot 'src\ServerGuard.Api\appsettings.json')
Assert-NoSecrets (Join-Path $repositoryRoot 'src\ServerGuard.Agent\appsettings.json')
Write-Ok 'appsettings.json dosyalarinda sir yok.'

# --- Testler --------------------------------------------------------------
# Testler paketlemeden once calisir: basarisiz bir testle uretilen paket sunucuya gitmemelidir.
Write-Step 'Birim testleri calistiriliyor'
& dotnet test $testProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Birim testleri basarisiz oldu (cikis kodu $LASTEXITCODE); paketleme durduruldu." }
Write-Ok 'Tum testler gecti.'

# --- Panel ----------------------------------------------------------------
if ($SkipWeb) {
    Write-Step 'Panel derlemesi atlandi (-SkipWeb)'
}
else {
    Write-Step 'Panel derleniyor (Angular, production)'

    Push-Location $webPath
    try {
        & npm.cmd run build -- --configuration production
        if ($LASTEXITCODE -ne 0) { throw "Angular derlemesi basarisiz oldu (cikis kodu $LASTEXITCODE)." }
    }
    finally {
        Pop-Location
    }

    $distPath = Join-Path $webPath 'dist\server-guard-web\browser'
    if (-not (Test-Path $distPath)) {
        $distPath = Join-Path $webPath 'dist\server-guard-web'
    }
    if (-not (Test-Path (Join-Path $distPath 'index.html'))) {
        throw "Panel ciktisi bulunamadi: $distPath"
    }

    Write-Step 'Panel API wwwroot altina kopyalaniyor'
    if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Copy-Item (Join-Path $distPath '*') $webRoot -Recurse -Force
    Write-Ok $webRoot
}

# --- Backend --------------------------------------------------------------
Write-Step 'API yayinlaniyor'
Reset-Directory $apiOutput
& dotnet publish $apiProject -c Release -o $apiOutput --nologo
if ($LASTEXITCODE -ne 0) { throw "API yayinlanamadi (cikis kodu $LASTEXITCODE)." }
Write-Ok $apiOutput

Write-Step 'Agent yayinlaniyor'
Reset-Directory $agentOutput
& dotnet publish $agentProject -c Release -o $agentOutput --nologo
if ($LASTEXITCODE -ne 0) { throw "Agent yayinlanamadi (cikis kodu $LASTEXITCODE)." }

if (Test-Path $updateScript) {
    Copy-Item $updateScript (Join-Path $agentOutput 'Guncelle.ps1') -Force
    Write-Ok 'Guncelle.ps1 pakete eklendi.'
}
Write-Ok $agentOutput

Write-Step 'Yardimci arac yayinlaniyor'
Reset-Directory $toolsOutput
& dotnet publish $toolsProject -c Release -o $toolsOutput --nologo
if ($LASTEXITCODE -ne 0) { throw "Arac yayinlanamadi (cikis kodu $LASTEXITCODE)." }
Write-Ok $toolsOutput

# --- Pakette sir kalmadigini dogrula --------------------------------------
Write-Step 'Paket icerigi dogrulaniyor'
Assert-NoSecrets (Join-Path $apiOutput 'appsettings.json')
Assert-NoSecrets (Join-Path $agentOutput 'appsettings.json')

$leakedOffset = Join-Path $agentOutput 'traffic-offset.json'
if (Test-Path $leakedOffset) {
    Remove-Item $leakedOffset -Force
    Write-Warn 'traffic-offset.json pakette bulundu ve silindi.'
}
Write-Ok 'Paket temiz.'

# --- Sonraki adimlar ------------------------------------------------------
Write-Host "`nYayin hazir." -ForegroundColor Green
Write-Host "  API + panel : $apiOutput"
Write-Host "  Agent       : $agentOutput"
Write-Host "  Arac        : $toolsOutput"
Write-Host "`nSonraki adimlar icin docs\YAYINLAMA.md dosyasina bakin." -ForegroundColor Gray
