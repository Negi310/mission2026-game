[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildPath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceCommit,
    [string]$Repository='Negi310/mission2026-game'
)
$ErrorActionPreference='Stop'
$project=(git rev-parse --show-toplevel).Trim()
$gitDirectory=(git rev-parse --absolute-git-dir).Trim()
$site=(Resolve-Path -LiteralPath $BuildPath).Path
if(!(Test-Path -LiteralPath (Join-Path $site 'index.html')) -or !(Test-Path -LiteralPath (Join-Path $site 'Build'))) {
    throw '完成したWebビルドのフォルダを指定してください。'
}
$files=Get-ChildItem -LiteralPath $site -Recurse -File
if($files | Where-Object {$_.Length -ge 100MB -or $_.Name -match 'DoNotShip'}) {
    throw '公開対象に大きすぎるファイルか開発専用ファイルがあります。'
}
$encoding=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $site '.nojekyll'),'',$encoding)
# Pages needs the binary payload itself, rather than a Git LFS pointer.
[IO.File]::WriteAllText((Join-Path $site '.gitattributes'),"* -filter -text`n",$encoding)
[IO.File]::WriteAllText((Join-Path $site 'build-info.json'),(@{
    sourceCommit=$SourceCommit; platform='WebGL'; publishedAt=[DateTime]::UtcNow.ToString('o')
}|ConvertTo-Json),$encoding)
$oldIndex=$env:GIT_INDEX_FILE
$env:GIT_INDEX_FILE=Join-Path $project ('Builds/pages-'+[Guid]::NewGuid().ToString('N')+'.index')
try {
    $remote=git ls-remote origin refs/heads/gh-pages
    $parent=$null
    if($remote) {
        $parent=($remote -split '\s+')[0]
        git fetch origin gh-pages
        if($LASTEXITCODE -ne 0){throw '既存の公開ブランチを取得できません。'}
    }
    Push-Location $site
    try {
        git --git-dir $gitDirectory --work-tree $site read-tree --empty
        git --git-dir $gitDirectory --work-tree $site add -f --all
        if($LASTEXITCODE -ne 0){throw '公開ファイルを準備できません。'}
        foreach($entry in (git --git-dir $gitDirectory ls-files -s)) {
            if($entry -match '^\d+ ([0-9a-f]{40}) 0\t(.*)$') {
                $blob=$Matches[1];$relative=$Matches[2]
                $bytes=git --git-dir $gitDirectory cat-file -s $blob
                if([long]$bytes -ne (Get-Item -LiteralPath (Join-Path $site $relative)).Length) {
                    throw "公開ファイルが実体と一致しません: $relative"
                }
            }
        }
        $tree=git --git-dir $gitDirectory write-tree
        $arguments=@('--git-dir',$gitDirectory,'commit-tree',$tree)
        if($parent){$arguments+=@('-p',$parent)}
        $commit="Publish Tank Town Web build $SourceCommit" | git @arguments
        if($LASTEXITCODE -ne 0){throw '公開コミットを作成できません。'}
        git --git-dir $gitDirectory push origin "${commit}:refs/heads/gh-pages"
        if($LASTEXITCODE -ne 0){throw 'Pagesへのpushに失敗しました。'}
    } finally {Pop-Location}
    $config=Join-Path $project 'Builds/pages-config.json'
    @{build_type='legacy';source=@{branch='gh-pages';path='/'}}|ConvertTo-Json|Set-Content -LiteralPath $config -Encoding utf8
    gh api "repos/$Repository/pages" -X PUT --input $config
    if($LASTEXITCODE -ne 0){throw 'Pagesの公開設定に失敗しました。'}
    gh api "repos/$Repository/pages/builds" -X POST
    if($LASTEXITCODE -ne 0){throw 'Pagesの配信開始リクエストに失敗しました。'}
} finally {$env:GIT_INDEX_FILE=$oldIndex}
