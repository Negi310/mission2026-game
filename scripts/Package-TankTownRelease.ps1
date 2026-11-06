[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

$buildRoot = (Resolve-Path -LiteralPath $BuildPath).Path
$infoPath = Join-Path $buildRoot 'build-info.json'
$info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
if ($info.developmentBuild -ne $false -or $info.platform -ne 'Windows-x64' -or
    $info.version -notmatch '^\d+\.\d+\.\d+$' -or $info.sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw '配布用Windowsビルドのbuild-info.jsonが必要です。'
}

foreach ($required in @('TankTown.exe', 'UnityPlayer.dll', 'TankTown_Data', 'MonoBleedingEdge')) {
    if (!(Test-Path -LiteralPath (Join-Path $buildRoot $required))) {
        throw "必要な配布ファイルがありません: $required"
    }
}

$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$packageName = "TankTown-v$($info.version)-Windows-x64"
$archivePath = Join-Path $outputRoot "$packageName.zip"
$checksumPath = "$archivePath.sha256"
if ((Test-Path -LiteralPath $archivePath) -or (Test-Path -LiteralPath $checksumPath)) {
    throw "同じバージョンの配布物が既に存在します: $archivePath"
}

$stagingRoot = Join-Path $outputRoot (".staging-" + [Guid]::NewGuid().ToString('N'))
$gameRoot = Join-Path $stagingRoot 'TankTown'
[System.IO.Directory]::CreateDirectory($gameRoot) | Out-Null
$allowed = @('TankTown.exe', 'UnityPlayer.dll', 'TankTown_Data', 'MonoBleedingEdge',
    'D3D12', 'UnityCrashHandler64.exe', 'WinPixEventRuntime.dll', 'build-info.json', 'microbit-controller.json')
foreach ($name in $allowed) {
    $sourcePath = Join-Path $buildRoot $name
    if (Test-Path -LiteralPath $sourcePath) {
        Copy-Item -LiteralPath $sourcePath -Destination $gameRoot -Recurse
    }
}

foreach ($relative in @('docs/microbit-controller.md', 'hardware/microbit/main.py')) {
    $sourcePath = Join-Path $buildRoot $relative
    if (!(Test-Path -LiteralPath $sourcePath)) { throw "コントローラーの配布ファイルがありません: $relative" }
    $destination = Join-Path $gameRoot $relative
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $sourcePath -Destination $destination
}
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'microbit-controller.json'))) {
    throw 'コントローラー設定ファイルがありません。'
}

$instructions = @"
TANK TOWN v$($info.version) / Windows 64bit

遊び方
1. ZIPを右クリックして「すべて展開」してください。
2. TankTownフォルダ内のTankTown.exeを起動します。Unityは不要です。
3. タイトルで街を選び、SpaceまたはEnterで開始します。
   1: いつもの街 / 2: 大きな街 / 3: お菓子の街 / 4: 遊園地（試作）

前進: 自動
左右のハンドル: A / D、または左右矢印
砲撃・所持しているレーザーや巨大爆弾を使用: Space / J / K
一時停止: Esc
詳しい操作: タイトルの「あそびかた」
リザルト: Spaceでもういちど / Tabで記録を見る / Escで記録を閉じる
終了: Alt+F4（標準は全画面表示）

Bluetoothハンドル（Windowsにペアリング済みのBLE UARTが対象）
1. WindowsのBluetooth設定からコントローラーをペアリングします。
2. コントローラー側で「角度,ピエゾ値」と改行を続けて送信してください。
3. ゲームは自動でBLE UARTを探し、受信するとハンドルと砲撃に反映します。
4. 感度・閾値・接続先は同じフォルダのmicrobit-controller.jsonを編集して再起動します。
   初期値: 角度の送信値±1023で最大旋回、砲撃350以上、再受付180以下。
詳しくはdocs/microbit-controller.mdを参照してください。
Bluetooth機器での通信と送信コードのUUID適合は未確認です。
既存の送信プログラムはそのまま使ってください。同梱main.pyは旧USB方式専用です。

TankTown_Data、UnityPlayer.dll、MonoBleedingEdgeなどは起動に必要です。
exeだけ取り出さず、同梱フォルダごと保持してください。
更新は新しいZIPを別フォルダへ展開してください。
ランキングは同じPC・Windowsユーザーなら引き継ぎます。

最新バージョン: https://github.com/Negi310/mission2026-game/releases/latest
ソース: $($info.sourceCommit)
Unity: $($info.unityVersion)
"@
$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $gameRoot '遊び方.txt'), $instructions, $encoding)
[System.IO.Compression.ZipFile]::CreateFromDirectory($stagingRoot, $archivePath,
    [System.IO.Compression.CompressionLevel]::Optimal, $false)
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($checksumPath, "$archiveHash  $packageName.zip`n", $encoding)

# 展開された各ファイルは、後の起動検証に使えるよう残す。
[pscustomobject]@{
    Archive = $archivePath
    Checksum = $checksumPath
    StagingDirectory = $stagingRoot
    Version = $info.version
    SourceCommit = $info.sourceCommit
    Bytes = (Get-Item -LiteralPath $archivePath).Length
    SHA256 = $archiveHash
}
