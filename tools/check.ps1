# Единая точка входа проверок проекта.
#   powershell -File tools\check.ps1 lint        # секунды: статический линтер (Godot не нужен)
#   powershell -File tools\check.ps1 quick       # lint + сборка + проверка соответствия баланс-модели игре
#   powershell -File tools\check.ps1 balance     # quick + симуляция баланса (--n 200) + шлюз против эталона
#   powershell -File tools\check.ps1 full        # quick + весь tools\run_checks.ps1 (долго, ~десятки минут)
#   powershell -File tools\check.ps1 baseline    # сохранить последний прогон как balance\baseline.json
# Параметры: -N 200 (кампаний на профиль), -Seed 20261106, -Strict (предупреждения линтера = ошибки)
param(
    [Parameter(Position = 0)][ValidateSet('lint', 'quick', 'balance', 'full', 'baseline')][string]$Mode = 'quick',
    [int]$N = 200, [int]$Seed = 20261106, [switch]$Strict
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$py = (Get-Command python -ErrorAction SilentlyContinue)
if (-not $py) { throw 'Нужен python в PATH (stdlib достаточно).' }
$steps = @()
function Step($name, [scriptblock]$body) {
    Write-Host "`n== $name"
    $t = Get-Date
    & $body | Out-Host
    $code = $LASTEXITCODE
    $script:steps += [pscustomobject]@{ Step = $name; Result = $(if ($code -eq 0) { 'PASS' } else { "FAIL($code)" }); Sec = [int]((Get-Date) - $t).TotalSeconds }
    return $code
}
function Finish {
    Write-Host ''
    $steps | Format-Table -AutoSize
    if ($steps | Where-Object { $_.Result -ne 'PASS' }) { Write-Host 'ПРОВЕРКА НЕ ПРОЙДЕНА'; exit 1 }
    Write-Host 'ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ'; exit 0
}

$lintArgs = @(); if ($Strict) { $lintArgs += '--strict' }
if ($Mode -eq 'baseline') { python tools\balance_gate.py save; exit $LASTEXITCODE }

if ((Step 'lint' { python tools\lint.py @lintArgs }) -ne 0 -and $Mode -eq 'lint') { Finish }
if ($Mode -eq 'lint') { Finish }

if ((Step 'dotnet build' { dotnet build Lamplighter.csproj --nologo -v q }) -ne 0) { Finish }
if ((Step 'balance: parity with the real Game' { powershell -NoProfile -ExecutionPolicy Bypass -File tools\balance.ps1 -SkipCompilation validate --game }) -ne 0) { Finish }

if ($Mode -eq 'balance') {
    $name = 'gate'
    Step "balance check (n=$N, seed=$Seed)" { powershell -NoProfile -ExecutionPolicy Bypass -File tools\balance.ps1 -SkipCompilation check --n $N --bench-k $N --validation-n 4 --seed $Seed --threads 6 --name $name } | Out-Null
    if (Test-Path 'balance\baseline.json') {
        Step 'balance gate vs baseline' { python tools\balance_gate.py diff --run "balance/out/$name" } | Out-Null
    } else {
        Write-Host 'Нет balance\baseline.json — шлюз пропущен. Создайте: tools\check.ps1 baseline'
    }
}
if ($Mode -eq 'full') {
    Step 'run_checks.ps1 (все стадии настоящей игры)' { powershell -NoProfile -ExecutionPolicy Bypass -File tools\run_checks.ps1 } | Out-Null
}
Finish
