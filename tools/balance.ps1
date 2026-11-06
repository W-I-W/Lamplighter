# Reproducible headless balance checks. No game parameters or user saves are changed.
# Parse wrapper options explicitly: PowerShell's parameter abbreviations would
# otherwise consume CLI options such as --n, --out or --verbose.
$Game = $false; $SkipCompilation = $false; $TimeoutSeconds = 900; $Rest = @()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$previousLocation = Get-Location
$savedEnv = @{}
foreach ($key in @('LAMP_AUTOTEST','LAMP_STAGE','LAMP_LOAD','LAMP_LANG','LAMP_BALANCE','LAMP_NET')) { $savedEnv[$key] = [Environment]::GetEnvironmentVariable($key) }

function NativeArgument([string]$value) {
    if ($value.Length -gt 0 -and $value -notmatch '[\s"]') { return $value }
    return '"' + [regex]::Replace([regex]::Replace($value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}
function Run-Godot([string[]]$nativeArgs, [string]$logPath) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $script:godotBin
    $info.WorkingDirectory = $root
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $allArgs = @('--headless','--path',$root,'--log-file',$logPath) + $nativeArgs
    $info.Arguments = ($allArgs | ForEach-Object { NativeArgument $_ }) -join ' '
    $balanceProc = New-Object System.Diagnostics.Process
    $balanceProc.StartInfo = $info
    try {
        [void]$balanceProc.Start()
        $stdout = $balanceProc.StandardOutput.ReadToEndAsync()
        $stderr = $balanceProc.StandardError.ReadToEndAsync()
        if (-not $balanceProc.WaitForExit($TimeoutSeconds * 1000)) { $balanceProc.Kill(); throw "Godot timed out after $TimeoutSeconds seconds. Log: $logPath" }
        $output = $stdout.Result + $stderr.Result
        if (Test-Path -LiteralPath $logPath) { $output = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8 }
        $output -split "`n" | Where-Object { $_ -match '^\[balance\]|^\[validate\]|\[check\] (FAIL|SUMMARY)|EXCEPTION' } | ForEach-Object { Write-Host $_ }
        return [pscustomobject]@{ Code = $balanceProc.ExitCode; Text = $output }
    }
    finally { $balanceProc.Dispose() }
}
try {
    for ($argIndex = 0; $argIndex -lt $args.Count; $argIndex++) {
        switch ($args[$argIndex]) {
            '-Game' { $Game = $true }
            '-SkipCompilation' { $SkipCompilation = $true }
            '-TimeoutSeconds' {
                $argIndex++
                if ($argIndex -ge $args.Count -or -not [int]::TryParse($args[$argIndex], [ref]$TimeoutSeconds)) { throw '-TimeoutSeconds requires an integer.' }
            }
            default { $Rest += [string]$args[$argIndex] }
        }
    }
    Set-Location -LiteralPath $root
    $script:godotBin = $env:GODOT
    if (-not $script:godotBin) { $script:godotBin = 'E:\Program Files (x86)\Steam\steamapps\common\Godot Engine\godot.windows.opt.tools.64.exe' }
    if (-not (Test-Path -LiteralPath $script:godotBin)) { throw 'Set GODOT to the Godot .NET executable.' }
    if ($TimeoutSeconds -lt 1 -or $TimeoutSeconds -gt 86400) { throw 'TimeoutSeconds must be 1..86400.' }
    if (-not $Rest -or $Rest.Count -eq 0) { $Rest = @('check') }
    if (-not $SkipCompilation) {
        dotnet build (Join-Path $root 'Lamplighter.csproj') --no-restore --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'Build failed; simulation was not started.' }
    }
    foreach ($key in $savedEnv.Keys) { [Environment]::SetEnvironmentVariable($key, '') }
    $runDir = Join-Path $root ('balance/out/_cli/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runDir -Force | Out-Null
    if ($Rest[0] -eq 'check' -or ($Game -and $Rest[0] -eq 'validate')) {
        $stages = if ($Rest[0] -eq 'check') { @('balance','balancecontract') } else { @('balance') }
        foreach ($stage in $stages) {
            $testDir = Join-Path $runDir $stage; New-Item -ItemType Directory -Path $testDir -Force | Out-Null
            $env:LAMP_AUTOTEST = $testDir; $env:LAMP_STAGE = $stage
            $stageLog = Join-Path $runDir "$stage.log"
            Write-Host "[balance] Game verification: $stage"
            $result = Run-Godot @('--quit-after','10000') $stageLog
            if ($result.Code -ne 0 -or $result.Text -notmatch 'SUMMARY failures=0' -or $result.Text -match '\[check\] FAIL|EXCEPTION|SCRIPT ERROR') { throw "Game verification failed ($stage). Log: $stageLog" }
        }
        $env:LAMP_AUTOTEST = ''; $env:LAMP_STAGE = ''
        if ($Rest[0] -eq 'check') { $Rest += @('--contract-log', (Join-Path $runDir 'balancecontract.log')) }
    }
    $result = Run-Godot (@('--','balance') + $Rest) (Join-Path $runDir 'command.log')
    Write-Host "[balance] Logs: $runDir"
    $exitCode = $result.Code
}
catch { Write-Host "[balance] ERROR: $_"; $exitCode = 2 }
finally {
    foreach ($key in $savedEnv.Keys) { [Environment]::SetEnvironmentVariable($key, $savedEnv[$key]) }
    Set-Location -LiteralPath $previousLocation.Path
}
exit $exitCode
