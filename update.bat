<# :
@echo off
rem  Aufbau: Diese Datei ist gleichzeitig Batch- und PowerShell-Skript.
rem  Die erste Zeile ist fuer cmd ein harmloses Sprungziel, fuer PowerShell der Beginn
rem  eines Kommentarblocks, der an der Zeile vor dem PowerShell-Teil endet. cmd fuehrt also
rem  nur diesen oberen Teil aus, der die Datei an PowerShell uebergibt; PowerShell
rem  ueberspringt ihn und fuehrt den Rest aus. Deshalb hier keine spitzen Klammern verwenden.
rem ---------------------------------------------------------------------------
rem  DSR Tracker - Update
rem  Laedt die neueste DSRTracker.exe von der offiziellen GitHub-Release-Seite,
rem  prueft Pruefsumme (SHA256) und - falls die GitHub CLI installiert ist -
rem  den signierten Herkunftsnachweis, und ersetzt erst dann die alte exe.
rem  Der Tracker selbst baut keine Internetverbindung auf; das macht nur
rem  dieses Skript, und nur wenn du es startest.
rem ---------------------------------------------------------------------------
setlocal
set "DSR_DIR=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& ([ScriptBlock]::Create((Get-Content -LiteralPath '%~f0' -Raw -Encoding UTF8)))" %*
set "RC=%ERRORLEVEL%"
echo.
echo Druecke eine beliebige Taste, um das Fenster zu schliessen ...
pause >nul
exit /b %RC%
#>

# ------------------------------- PowerShell-Teil -------------------------------
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Repo    = 'spidy666/DSRtracker'
$ExeName = 'DSRTracker.exe'
$Force   = $args -contains '--force'

function Info($t) { Write-Host $t }
function Ok($t)   { Write-Host $t -ForegroundColor Green }
function Warn($t) { Write-Host $t -ForegroundColor Yellow }
function Step($n, $t) { Write-Host ''; Write-Host "[$n/6] $t" -ForegroundColor Cyan }
function Fail($t) {
    Write-Host $t -ForegroundColor Red
    Write-Host ''
    Write-Host ' Update abgebrochen. Deine bisherige exe wurde NICHT veraendert.' -ForegroundColor Red
    exit 1
}

# Zielordner: im Quellcode-Ordner (neben DSRTracker.csproj) -> dist\, sonst der Ordner des Skripts
$Here = $env:DSR_DIR
if (-not $Here) { $Here = (Get-Location).Path }
$Here = $Here.TrimEnd('\')
if (Test-Path (Join-Path $Here 'DSRTracker.csproj')) { $Target = Join-Path $Here 'dist' } else { $Target = $Here }
$TargetExe = Join-Path $Target $ExeName

Info '================================================'
Info ' DSR Tracker - Update'
Info '================================================'
Info ' Dieses Skript'
Info '  1. fragt bei GitHub nach der neuesten Version,'
Info '  2. laedt DSRTracker.exe von der offiziellen Release-Seite,'
Info '  3. prueft die Pruefsumme (SHA256),'
Info '  4. prueft den Herkunftsnachweis (falls GitHub CLI installiert),'
Info '  5. prueft, dass der Tracker nicht gerade laeuft,'
Info '  6. ersetzt die alte exe (die alte bleibt als .bak erhalten).'
Info ' Bei jedem Fehler wird abgebrochen und nichts veraendert.'
Info ''
Info " Ziel: $TargetExe"

# 1) Neuesten Release abfragen
Step 1 'Frage den neuesten Release bei GitHub ab ...'
$Headers = @{ 'User-Agent' = 'DSRTracker-Update'; 'Accept' = 'application/vnd.github+json' }
try {
    $Release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $Headers -UseBasicParsing
} catch {
    Fail " GitHub nicht erreichbar: $($_.Exception.Message)"
}
$Tag = $Release.tag_name
$ExeAsset = $Release.assets | Where-Object { $_.name -eq $ExeName } | Select-Object -First 1
$ShaAsset = $Release.assets | Where-Object { $_.name -eq "$ExeName.sha256" } | Select-Object -First 1
if (-not $ExeAsset -or -not $ShaAsset) { Fail " Release $Tag enthaelt keine $ExeName mit Pruefsumme. Abbruch." }
Info " Neueste Version: $Tag"

# Schon aktuell?
if ((Test-Path $TargetExe) -and -not $Force) {
    $Local = (Get-Item $TargetExe).VersionInfo.ProductVersion
    if ($Local) {
        $LocalShort = ($Local -split '\+')[0]
        if ($LocalShort -eq $Tag -or $LocalShort -eq "$Tag.0") {
            Ok " Deine exe ist bereits auf Version $Tag. Nichts zu tun."
            Info ' (Mit "update.bat --force" trotzdem neu laden.)'
            exit 0
        }
        Info " Installierte Version: $LocalShort"
    }
}

# 2) Herunterladen in einen eigenen Temp-Ordner
$Tmp = Join-Path $env:TEMP ("DSRTracker-update-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $Tmp | Out-Null
$NewExe = Join-Path $Tmp $ExeName
$ShaFile = Join-Path $Tmp "$ExeName.sha256"
try {
    Step 2 "Lade $ExeName ($([Math]::Round($ExeAsset.size / 1MB, 1)) MB) von der Release-Seite ..."
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $ExeAsset.browser_download_url -OutFile $NewExe -Headers @{ 'User-Agent' = 'DSRTracker-Update' } -UseBasicParsing
    Invoke-WebRequest -Uri $ShaAsset.browser_download_url -OutFile $ShaFile -Headers @{ 'User-Agent' = 'DSRTracker-Update' } -UseBasicParsing

    Ok ' Download abgeschlossen.'

    # 3) Pruefsumme
    Step 3 'Pruefe die Pruefsumme (SHA256) ...'
    $Expected = ((Get-Content -LiteralPath $ShaFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    $Actual = (Get-FileHash -LiteralPath $NewExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($Expected -notmatch '^[0-9a-f]{64}$') { Fail ' Pruefsummen-Datei ist ungueltig. Abbruch.' }
    if ($Actual -ne $Expected) { Fail " Pruefsumme stimmt NICHT (erwartet $Expected, erhalten $Actual). Datei wird verworfen." }
    # GitHub nennt die Pruefsumme der hochgeladenen Datei zusaetzlich selbst
    if ($ExeAsset.digest -and ($ExeAsset.digest -replace '^sha256:', '').ToLowerInvariant() -ne $Actual) {
        Fail ' Pruefsumme weicht von der Angabe der Release-Seite ab. Datei wird verworfen.'
    }
    Ok " Pruefsumme OK: $Actual"

    # 4) Herkunftsnachweis (nur mit installierter GitHub CLI)
    $Gh = Get-Command gh -ErrorAction SilentlyContinue
    Step 4 'Pruefe den Herkunftsnachweis ...'
    if ($Gh) {
        & gh attestation verify $NewExe --repo $Repo | Out-Host
        if ($LASTEXITCODE -ne 0) { Fail ' Herkunftsnachweis konnte NICHT bestaetigt werden. Datei wird verworfen.' }
        Ok ' Herkunftsnachweis OK: gebaut von GitHub Actions in diesem Repo.'
    } else {
        Warn ' GitHub CLI nicht installiert - Herkunftsnachweis wird uebersprungen.'
        Warn ' (Optional: "winget install --id GitHub.cli", danach "gh auth login".)'
    }

    # 5) Laeuft der Tracker noch?
    Step 5 'Pruefe, ob der Tracker gerade laeuft ...'
    $Running = Get-Process -Name 'DSRTracker' -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -eq $TargetExe } catch { $false }
    }
    if ($Running) { Fail ' Der Tracker laeuft noch. Bitte das Tracker-Fenster schliessen und update.bat erneut starten.' }
    Ok ' Tracker laeuft nicht.'

    # 6) Ersetzen (alte exe als Sicherung behalten)
    Step 6 'Ersetze die exe ...'
    if (-not (Test-Path $Target)) { New-Item -ItemType Directory -Path $Target | Out-Null }
    if (Test-Path $TargetExe) {
        Copy-Item -LiteralPath $TargetExe -Destination "$TargetExe.bak" -Force
        Info " Alte exe gesichert: $ExeName.bak"
    }
    Copy-Item -LiteralPath $NewExe -Destination $TargetExe -Force
    Info ''
    Ok '================================================'
    Ok " Update erfolgreich: $ExeName ist jetzt Version $Tag."
    Ok '================================================'
    Info " Gespeichert in: $Target"
    Info ' Du kannst den Tracker jetzt wie gewohnt starten.'
}
finally {
    Remove-Item -LiteralPath $Tmp -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
