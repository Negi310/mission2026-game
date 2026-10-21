[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Script,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\output\blender'),

    [string]$BlenderPath = $env:BLENDER_EXE,

    [int]$TimeoutSeconds = 300,

    [string[]]$ScriptArguments = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (!$BlenderPath) {
    $candidates = @(
        'D:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
        'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
    )
    $BlenderPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (!$BlenderPath) {
        throw 'blender.exeが見つかりません。-BlenderPathか環境変数BLENDER_EXEで指定してください。'
    }
}

$scriptPath = (Resolve-Path -LiteralPath $Script).Path
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$logPath = Join-Path $outputRoot ([System.IO.Path]::GetFileNameWithoutExtension($scriptPath) + '.log')

# blender.exeは標準出力をパイプでつなぐと終了しないことがあるため、ファイルへリダイレクトして待つ
$arguments = @('--background', '--factory-startup', '--python-exit-code', '1', '--python', "`"$scriptPath`"", '--', '--out', "`"$outputRoot`"") +
    ($ScriptArguments | ForEach-Object { "`"$_`"" })
$process = Start-Process -FilePath $BlenderPath -ArgumentList $arguments -NoNewWindow -PassThru `
    -RedirectStandardOutput $logPath -RedirectStandardError "$logPath.err"

if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill()
    throw "Blenderが${TimeoutSeconds}秒以内に終了しませんでした。ログ: $logPath"
}

Get-Content -LiteralPath $logPath, "$logPath.err" | Where-Object { $_ -match '^\[bkit\]|Error|Traceback|^\s+File ' }
if ($process.ExitCode -ne 0) {
    throw "Blenderがエラー終了しました (exit $($process.ExitCode))。ログ: $logPath"
}
