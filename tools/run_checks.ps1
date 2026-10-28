# Lanther automated checks.
# Usage:  powershell -File tools\run_checks.ps1   (env GODOT or -Godot may point to godot*.exe)
param([string]$Godot = $env:GODOT, [switch]$NetOnly, [string]$NetCase = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
if (-not $Godot) { $Godot = 'E:\Program Files (x86)\Steam\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe' }
if (-not (Test-Path $Godot)) { throw "Godot not found: $Godot (set GODOT or -Godot)" }

# Два процесса игры в одной сети (ENet, localhost): проверка игровой сетевой архитектуры. Это НЕ проверка Steam P2P.
function Run-NetPair($name, $port, $expect, $clientEnv = @{}) {
    $out = @{}
    foreach ($role in 'host', 'join') {
        $dir = Join-Path $env:TEMP "lamp_check_net$role"; New-Item -ItemType Directory -Force $dir | Out-Null; Get-ChildItem $dir -File | Remove-Item -Force
        $env:LAMP_AUTOTEST = $dir; $env:LAMP_STAGE = $(if ($role -eq 'host') { 'nethost' } else { 'netjoin' }); $env:LAMP_LANG = ''; $env:LAMP_LOAD = ''
        $env:LAMP_NET = $(if ($role -eq 'host') { 'host' } else { "join:127.0.0.1:$port" }); $env:LAMP_NET_PORT = "$port"; $env:LAMP_NET_EXPECT = $expect; $env:LAMP_NET_FAKEVER = ''
        if ($role -eq 'join') { foreach ($k in $clientEnv.Keys) { Set-Item "env:$k" $clientEnv[$k] } }
        $log = Join-Path $env:TEMP "lamp_net_$role.log"
        $out[$role] = Start-Process -FilePath $Godot -ArgumentList '--path', '.', '--resolution', '960x540' -PassThru -NoNewWindow -RedirectStandardOutput $log
        $out["$role.log"] = $log
        if ($role -eq 'host') { Start-Sleep -Seconds 6 }
    }
    $deadline = (Get-Date).AddSeconds(100)
    while ((-not $out['host'].HasExited -or -not $out['join'].HasExited) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    foreach ($role in 'host', 'join') { if (-not $out[$role].HasExited) { $out[$role].Kill() } }
    $why = @(); $ok = $true
    foreach ($role in 'host', 'join') {
        $text = Get-Content $out["$role.log"] -Raw
        if ($text -notmatch 'SUMMARY failures=0') { $ok = $false; $why += "${role}: no clean summary" }
        if ($text -match '\[check\] FAIL') { $ok = $false; $why += "${role}: failed check" }
        if ($text -match 'EXCEPTION') { $ok = $false; $why += "${role}: exception" }
        if ($text -notmatch 'NET DONE') { $ok = $false; $why += "${role}: not finished" }
    }
    $script:results += [pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Notes = ($why -join '; ') }
    $env:LAMP_NET = ''; $env:LAMP_NET_PORT = ''; $env:LAMP_NET_EXPECT = ''; $env:LAMP_NET_FAKEVER = ''
}
if ($NetOnly) {
    $script:results = @()
    if ($NetCase -ne 'reject') { Run-NetPair 'net (ENet, 2 processes): handshake, seed, positions, leave' 24871 '' @{} }
    if ($NetCase -ne 'pair') { Run-NetPair 'net (ENet): incompatible version is rejected' 24872 'reject' @{ LAMP_NET_FAKEVER = '9.9.9' } }
    $script:results | Format-Table -AutoSize
    if ($script:results | Where-Object Result -eq 'FAIL') { exit 1 } else { Write-Host 'NET CHECKS PASSED'; exit 0 }
}

Write-Host '== dotnet build (Debug + ExportRelease)'
dotnet build Lamplighter.csproj --nologo -v q | Out-Null; if ($LASTEXITCODE) { throw 'Debug build failed' }
dotnet build Lamplighter.csproj -c ExportRelease --nologo -v q | Out-Null; if ($LASTEXITCODE) { throw 'ExportRelease build failed' }
Write-Host 'build OK'

$script:results = @()
function Run-Stage($name, $stage, $expect, $extraEnv = @{}, $reject = @()) {
    $dirName = if ($stage -eq 'reload') { 'final' } else { $stage }
    $dir = Join-Path $env:TEMP "lamp_check_$dirName"
    New-Item -ItemType Directory -Force $dir | Out-Null
    if ($stage -ne 'reload' -and $stage -ne 'interactload') { Get-ChildItem $dir -File | Remove-Item -Force }
    $env:LAMP_AUTOTEST = $dir; $env:LAMP_STAGE = $stage; $env:LAMP_LANG = ''; $env:LAMP_LOAD = ''
    foreach ($k in $extraEnv.Keys) { Set-Item "env:$k" $extraEnv[$k] }
    $out = cmd /c "`"$Godot`" --path . --resolution 1280x720 2>&1" | Out-String
    $ok = $true; $why = @()
    foreach ($e in $expect) { if ($out -notmatch [regex]::Escape($e)) { $ok = $false; $why += "missing: $e" } }
    foreach ($r in $reject) { if ($out -match $r) { $ok = $false; $why += "found: $r" } }
    if ($out -match 'EXCEPTION') { $ok = $false; $why += 'EXCEPTION' }
    $script:results += [pscustomobject]@{ Check = $name; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Notes = ($why -join '; ') }
    $env:LAMP_LANG = ''; $env:LAMP_LOAD = ''; $env:LAMP_NET = ''
}

Run-Stage 'world audit: 40 seeds, bosses, pois, blueprints, pacing' 'world' @('WORLD AUDIT PASSED')
Run-Stage 'ice monastery and winter warden' 'ice' @('ice ancients reachable=4 unreachable=0', 'boss4 defeated=True', 'prismTech=True')
Run-Stage 'heart, source, finale' 'heart' @('source defeated=True', 'finale started stage=1', 'after finale stage=0 finished=True')
Run-Stage 'endgame: rifts, legends, decor' 'end' @('after clear: on=False', 'legends=31', 'decor=3')
Run-Stage 'menu, slots, backup, rebind' 'menu' @('save to slot1 exists=True valid=True', 'main valid=False -> would load backup=True', 'after resave main valid=True', 'translate(G)=E')
Run-Stage 'localization sweep (en)' 'loc' @('SCRIPT DONE') @{ LAMP_LANG = 'en' } @('loc-missing')
Run-Stage 'perf stress (chunks, 30 enemies)' 'perf' @('PERF: chunks=') @{} @('fps=(?:[0-9]|[1-5][0-9]) ')
Run-Stage 'final checks: chunks, regrowth, death, save' 'final' @('SUMMARY failures=0') @{} @('\[check\] FAIL')
Run-Stage 'graphics: all sprites and effect primitives load from PNG' 'texcheck' @('SUMMARY failures=0') @{} @('\[check\] FAIL')
Run-Stage 'interaction: selection, hysteresis, mouse, E/U, build mode, removal' 'interact' @('SUMMARY failures=0') @{} @('\[check\] FAIL')
$tmpI = Join-Path $env:TEMP 'lamp_check_interactload'; New-Item -ItemType Directory -Force $tmpI | Out-Null
Copy-Item (Join-Path (Join-Path $root 'testsaves') 'mid\save.json') $tmpI -Force
Run-Stage 'interaction after loading a save' 'interactload' @('SUMMARY failures=0') @{ LAMP_LOAD = '1'; LAMP_DIRNAME = 'interactload' } @('\[check\] FAIL')
$tmpU = Join-Path $env:TEMP 'lamp_check_ui'; New-Item -ItemType Directory -Force $tmpU | Out-Null
Copy-Item (Join-Path (Join-Path $root 'testsaves') 'mid\save.json') $tmpU -Force
Run-Stage 'ui: HUD, build menu, inventory, all windows (screenshots)' 'ui' @('UI SCREENSHOTS DONE') @{ LAMP_LOAD = '1' } @('\[check\] FAIL', 'ArgumentException', 'SCRIPT ERROR')
Run-Stage 'fx: enemy attack stages, hit feedback, deaths (screenshots)' 'fx' @('FX SCREENSHOTS DONE') @{} @('\[check\] FAIL', 'ArgumentException', 'SCRIPT ERROR')
Run-Stage 'items: inventory, hotbar, tools in combat, crafting groups, build mode' 'items' @('ITEMS DONE', 'SUMMARY failures=0') @{} @('\[check\] FAIL', 'ArgumentException')
Run-Stage 'trees: crowns, collisions, trunk over player, chopping, bushes' 'trees' @('TREES DONE', 'SUMMARY failures=0') @{} @('\[check\] FAIL', 'ArgumentException')
Run-Stage 'upgrades: categories open by discovery, window states, save and old saves' 'upgrades' @('UPGRADES DONE', 'SUMMARY failures=0') @{} @('\[check\] FAIL', 'ArgumentException')
Run-Stage 'steam host (real Steam lobby, 1 client; skipped without Steam/GodotSteam)' 'netsteam' @('NETSTEAM DONE', 'SUMMARY failures=0') @{ LAMP_NET = 'hoststeam' } @('\[check\] FAIL')
Run-Stage 'net menu pages (screenshots, en localization)' 'netmenu' @('NETMENU DONE') @{ LAMP_LANG = 'en' } @('loc-missing')
Run-Stage 'economy audit (trade loops, sinks)' 'econ' @('ECON AUDIT PASSED')
Run-Stage 'combat audit (weapons, dead effects)' 'combat' @('COMBAT AUDIT PASSED')
Run-Stage 'soak: 100 s, all biomes, leaks/fps' 'soak' @('SUMMARY failures=0') @{} @('\[check\] FAIL')
foreach ($k in 'early','mid','late','post') {
    $tmp = Join-Path $env:TEMP "lamp_check_save_$k"; New-Item -ItemType Directory -Force $tmp | Out-Null
    Copy-Item (Join-Path (Join-Path $root "testsaves") (Join-Path $k "save.json")) $tmp -Force
    $env:LAMP_AUTOTEST = $tmp; $env:LAMP_STAGE = 'savesmoke'; $env:LAMP_LOAD = '1'; $env:LAMP_LANG = ''
    $out = cmd /c "`"$Godot`" --path . --resolution 1280x720 2>&1" | Out-String
    $ok = ($out -match 'SUMMARY failures=0') -and ($out -notmatch '\[check\] FAIL') -and ($out -notmatch 'EXCEPTION')
    $script:results += [pscustomobject]@{ Check = "test save loads and runs: $k"; Result = $(if ($ok) { 'PASS' } else { 'FAIL' }); Notes = '' }
    $env:LAMP_LOAD = ''
}
Run-NetPair 'net (ENet, 2 processes): handshake, seed, positions, leave' 24871 '' @{}
Run-NetPair 'net (ENet): incompatible version is rejected' 24872 'reject' @{ LAMP_NET_FAKEVER = '9.9.9' }
Run-Stage 'reload finished save' 'reload' @('SUMMARY failures=0') @{ LAMP_LOAD = '1' } @('\[check\] FAIL')

$script:results | Format-Table -AutoSize
if ($script:results | Where-Object Result -eq 'FAIL') { exit 1 } else { Write-Host 'ALL CHECKS PASSED' }
