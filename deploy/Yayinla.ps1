<#
.SYNOPSIS
    ServerGuard'i yayina hazirlar: backend, panel, agent ve yardimci arac.

.DESCRIPTION
    Dort paket uretir ve her birini ayri bir zip olarak sikistirir:

      publish\api    -> ServerGuard        (IIS sitesi, backend)
      publish\web    -> ServerGuardClient  (IIS sitesi, Angular panel)
      publish\agent  -> Windows hizmeti    (her izlenen sunucuya)
      publish\tools  -> sir uretme ve saglik dogrulama araci

    Panel ve backend AYRI sitelerde yayinlanir. Panel, API'nin adresini calisma
    zamaninda config.json'dan okur; adres degistiginde sunucuda tek satir duzenlenir,
    yeniden derleme gerekmez.

    Paketleme oncesinde appsettings.json dosyalarinda sir birakilip birakilmadigi
    kontrol edilir; bulunursa islem durur. Sirlar yalnizca sunucudaki ortam
    degiskenlerinde bulunmalidir.

.PARAMETER OutputPath
    Yayin ciktilarinin yazilacagi klasor.

.PARAMETER SkipWeb
    Angular derlemesini atlar. Yalnizca backend degistiyse zaman kazandirir.

.PARAMETER SkipTests
    Birim testlerini atlar. Yalnizca hizli bir deneme paketi icin; yayina cikarken kullanmayin.

.EXAMPLE
    .\deploy\Yayinla.ps1
    .\deploy\Yayinla.ps1 -SkipWeb
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [switch]$SkipWeb,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $repositoryRoot 'publish' }

$apiProject   = Join-Path $repositoryRoot 'src\ServerGuard.Api\ServerGuard.Api.csproj'
$agentProject = Join-Path $repositoryRoot 'src\ServerGuard.Agent\ServerGuard.Agent.csproj'
$toolsProject = Join-Path $repositoryRoot 'src\ServerGuard.Tools\ServerGuard.Tools.csproj'
$testProject  = Join-Path $repositoryRoot 'tests\ServerGuard.UnitTests\ServerGuard.UnitTests.csproj'
$webPath      = Join-Path $repositoryRoot 'src\ServerGuard.Web'
$apiWebRoot   = Join-Path $repositoryRoot 'src\ServerGuard.Api\wwwroot'
$updateScript = Join-Path $PSScriptRoot 'Agent-Guncelle.ps1'
$panelConfig  = Join-Path $PSScriptRoot 'panel-web.config'

$apiOutput   = Join-Path $OutputPath 'api'
$webOutput   = Join-Path $OutputPath 'web'
$agentOutput = Join-Path $OutputPath 'agent'
$toolsOutput = Join-Path $OutputPath 'tools'

function Write-Step { param([string]$Text) Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "    $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "    $Text" -ForegroundColor Yellow }

# Yayin klasoru her seferinde sifirdan olusturulur. Onceki bir yayindan kalan DLL,
# yeni surumun bagimliliklariyla catisip uygulamanin acilmamasina yol acabilir;
# 'dotnet publish' hedef klasoru kendiliginden temizlemez.
function Reset-Directory {
    param([string]$Path)

    if (Test-Path $Path) { Remove-Item -Path $Path -Recurse -Force }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

# --- Sir sizintisi kontrolu ----------------------------------------------
# Bu kontrol paketleme oncesinde calisir: bir sir yanlislikla appsettings.json'a
# yazildiysa, sunucuya tasinmadan once burada yakalanir.
function Assert-NoSecrets {
    param([string]$SettingsPath)

    if (-not (Test-Path $SettingsPath)) { return }

    $settings = Get-Content $SettingsPath -Raw | ConvertFrom-Json

    $checks = @(
        @{ Path = 'ConnectionStrings.ServerGuard';   Value = $settings.ConnectionStrings.ServerGuard },
        @{ Path = 'Security.Jwt.SigningKey';         Value = $settings.Security.Jwt.SigningKey },
        @{ Path = 'Detection.IpReputation.ApiKey';   Value = $settings.Detection.IpReputation.ApiKey },
        @{ Path = 'Notifications.Telegram.BotToken'; Value = $settings.Notifications.Telegram.BotToken },
        @{ Path = 'Notifications.Telegram.ChatId';   Value = $settings.Notifications.Telegram.ChatId },
        @{ Path = 'Agent.ApiKey';                    Value = $settings.Agent.ApiKey }
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

function New-Package {
    param([string]$SourceDirectory, [string]$ZipPath)

    if (Test-Path $ZipPath) { Remove-Item -Path $ZipPath -Force }
    Compress-Archive -Path (Join-Path $SourceDirectory '*') -DestinationPath $ZipPath -CompressionLevel Optimal

    $sizeMb = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
    Write-Ok "$(Split-Path $ZipPath -Leaf) ($sizeMb MB)"
}

Write-Step 'Sir sizintisi kontrolu'
Assert-NoSecrets (Join-Path $repositoryRoot 'src\ServerGuard.Api\appsettings.json')
Assert-NoSecrets (Join-Path $repositoryRoot 'src\ServerGuard.Agent\appsettings.json')
Write-Ok 'appsettings.json dosyalarinda sir yok.'

# --- Testler --------------------------------------------------------------
# Testler paketlemeden once calisir: basarisiz bir testle uretilen paket sunucuya gitmemelidir.
if ($SkipTests) {
    Write-Step 'Birim testleri atlandi (-SkipTests)'
    Write-Warn 'Yayina cikarken testleri atlamayin.'
}
else {
    Write-Step 'Birim testleri calistiriliyor'
    & dotnet test $testProject -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Birim testleri basarisiz oldu (cikis kodu $LASTEXITCODE); paketleme durduruldu." }
    Write-Ok 'Tum testler gecti.'
}

# --- Backend --------------------------------------------------------------
# Panel ayri bir sitede yayinlandigi icin API paketinde panel dosyalari bulunmaz. Onceki
# kurulumdan kalmis bir panel kopyasi pakete sizip eski surumu tasimamalidir.
#
# Klasorun kendisi silinmez, yalnizca icerigi bosaltilir: wwwroot hic yoksa tasarim zamani
# araclari ('dotnet ef migrations script' gibi) uygulamayi ayaga kaldiramaz ve sema betigi
# uretilemez. Yayinlanan paket bundan etkilenmez.
Write-Step 'API wwwroot bosaltiliyor (panel ayri sitede)'
Reset-Directory $apiWebRoot
Write-Ok 'Bos.'

Write-Step 'Backend yayinlaniyor'
Reset-Directory $apiOutput
& dotnet publish $apiProject -c Release -o $apiOutput --nologo
if ($LASTEXITCODE -ne 0) { throw "Backend yayinlanamadi (cikis kodu $LASTEXITCODE)." }
Write-Ok $apiOutput

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

    # Angular surumune gore cikti ya dogrudan dist kokunde ya da browser alt klasorunde olur.
    $distPath = Join-Path $webPath 'dist\server-guard-web\browser'
    if (-not (Test-Path (Join-Path $distPath 'index.html'))) {
        $distPath = Join-Path $webPath 'dist\server-guard-web'
    }
    if (-not (Test-Path (Join-Path $distPath 'index.html'))) {
        throw "Panel ciktisi bulunamadi: $distPath"
    }

    Write-Step 'Panel paketleniyor'
    Reset-Directory $webOutput
    Copy-Item (Join-Path $distPath '*') $webOutput -Recurse -Force

    if (-not (Test-Path $panelConfig)) { throw "Panel web.config sablonu bulunamadi: $panelConfig" }
    Copy-Item $panelConfig (Join-Path $webOutput 'web.config') -Force

    if (-not (Test-Path (Join-Path $webOutput 'config.json'))) {
        throw 'Panel paketinde config.json yok; API adresi calisma zamaninda okunamaz.'
    }

    Write-Ok $webOutput
}

# --- Agent ve arac --------------------------------------------------------
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

# --- Pakette sir ve artik kalmadigini dogrula -----------------------------
Write-Step 'Paket icerigi dogrulaniyor'
Assert-NoSecrets (Join-Path $apiOutput 'appsettings.json')
Assert-NoSecrets (Join-Path $agentOutput 'appsettings.json')

foreach ($stray in @(
    (Join-Path $agentOutput 'traffic-offset.json'),
    (Join-Path $apiOutput 'logs'),
    (Join-Path $agentOutput 'logs'))) {
    if (Test-Path $stray) {
        Remove-Item -Path $stray -Recurse -Force
        Write-Warn "Yerel artik pakette bulundu ve silindi: $(Split-Path $stray -Leaf)"
    }
}

# Bos bir wwwroot klasoru sorun degil; icinde panel dosyasi olmasi sorundur.
$packagedWebRoot = Join-Path $apiOutput 'wwwroot'
if (Test-Path $packagedWebRoot) {
    $leakedPanel = @(Get-ChildItem $packagedWebRoot -Recurse -File)
    if ($leakedPanel.Count -gt 0) {
        throw "Backend paketinde $($leakedPanel.Count) panel dosyasi var; panel ayri sitede yayinlanmali."
    }
}
Write-Ok 'Paket temiz.'

# --- Zip ------------------------------------------------------------------
Write-Step 'Zip dosyalari olusturuluyor'
New-Package $apiOutput   (Join-Path $OutputPath 'ServerGuard-Backend.zip')
if (-not $SkipWeb) { New-Package $webOutput (Join-Path $OutputPath 'ServerGuard-Panel.zip') }
New-Package $agentOutput (Join-Path $OutputPath 'ServerGuard-Agent.zip')
New-Package $toolsOutput (Join-Path $OutputPath 'ServerGuard-Tools.zip')

# --- Sonraki adimlar ------------------------------------------------------
Write-Host "`nYayin hazir." -ForegroundColor Green
Write-Host "  Backend : ServerGuard-Backend.zip  -> IIS sitesi 'ServerGuard'       (havuz: ServerGuard)"
Write-Host "  Panel   : ServerGuard-Panel.zip    -> IIS sitesi 'ServerGuardClient' (havuz: ServerGuardClient)"
Write-Host "  Agent   : ServerGuard-Agent.zip    -> izlenen her sunucuya Windows hizmeti"
Write-Host "  Arac    : ServerGuard-Tools.zip    -> sir uretme ve saglik dogrulama"
Write-Host "`nSonraki adimlar icin docs\YAYINLAMA.md dosyasina bakin." -ForegroundColor Gray
