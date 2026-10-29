# Lanther balance simulator CLI.
#   powershell -File tools\balance.ps1 simulate --n 2000 --seed 1 --name base
#   powershell -File tools\balance.ps1 analyze --n 2000 --name analysis
#   powershell -File tools\balance.ps1 optimize --iters 12 --train 150 --valid 600 --name opt1
#   powershell -File tools\balance.ps1 compare --a base --b opt1 --n 2000
#   powershell -File tools\balance.ps1 report --from base/base
#   powershell -File tools\balance.ps1 validate           (model checks; add -Game to also run the real-game parity stage)
#   powershell -File tools\balance.ps1 apply --overrides opt1 --confirm
# Output: balance\out\<name>\ (JSON, CSV, Markdown); proposals: balance\proposals\<name>.json. Stop a long run: create balance\STOP.
param([switch]$Game, [Parameter(ValueFromRemainingArguments = $true)] $Rest)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$Godot = $env:GODOT
if (-not $Godot) { $Godot = 'E:\Program Files (x86)\Steam\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe' }
if (-not (Test-Path $Godot)) { throw "Godot not found: $Godot (set GODOT)" }
$line = ($Rest -join ' ')
$env:LAMP_AUTOTEST = ''; $env:LAMP_STAGE = ''; $env:LAMP_LOAD = ''
$out = cmd /c "`"$Godot`" --headless --path . -- balance $line 2>&1" | Out-String
Write-Host $out
$code = $LASTEXITCODE
if ($Game -and $Rest -contains 'validate') {
    $tmp = Join-Path $env:TEMP 'lamp_check_balance'; New-Item -ItemType Directory -Force $tmp | Out-Null
    $env:LAMP_AUTOTEST = $tmp; $env:LAMP_STAGE = 'balance'; $env:LAMP_LANG = ''
    $g = cmd /c "`"$Godot`" --path . --resolution 960x540 2>&1" | Out-String
    $g -split "`n" | Where-Object { $_ -match '\[check\]|EXCEPTION' } | ForEach-Object { Write-Host $_ }
    $env:LAMP_AUTOTEST = ''; $env:LAMP_STAGE = ''
    if (($g -notmatch 'SUMMARY failures=0') -or ($g -match '\[check\] FAIL') -or ($g -match 'EXCEPTION')) { $code = 1 }
}
exit $code
