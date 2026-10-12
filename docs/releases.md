# Windows版の配布

[最新リリース](https://github.com/Negi310/mission2026-game/releases/latest)の`TankTown-vX.Y.Z-Windows-x64.zip`をダウンロードし、すべて展開して`TankTown/TankTown.exe`を起動します。Unityは不要です。Source codeのZIPはUnityプロジェクトであり、ゲームの実行ファイルは含みません。

exeと同梱フォルダを一緒に保持してください。更新時は新しいZIPを別フォルダに展開します。記録はWindowsユーザーのローカルデータへ保存されるため、同じPC・同じユーザーならランキングを引き継ぎます。ZIP形式なのでインストーラー・自動更新機能はありません。対応はWindows x64です。

## 配布ビルドの作成

Unity **6000.3.19f1**とWindows Build Supportを使います。Editorを閉じた検証用コピーで、対象コミットとゲーム本体の一致を確認してから実行してください。`ProjectSettings/ProjectSettings.asset`の`bundleVersion`を配布するバージョンに設定します。開発用の`BuildWindows`とは別に、`BuildWindowsRelease`はDevelopmentフラグなしでビルドし、バージョン・コミット・Unity版を`build-info.json`へ記録します。

```powershell
$env:TANKTOWN_BUILD_COMMIT = git rev-parse HEAD
& 'D:/Unity/Editor/6000.3.19f1/Editor/Unity.exe' -batchmode -projectPath '<検証用プロジェクト>' -executeMethod TankTown.Editor.TankTownSetup.BuildWindowsRelease -quit -logFile '<ビルドログ>'
# Unityの終了コード・ログ・ビルド結果を確認してから梱包する。
./scripts/Package-TankTownRelease.ps1 -BuildPath '<検証用プロジェクト>/Builds/TankTown-Release' -OutputDirectory './Builds/Releases'
```

スクリプトは起動に必要なexe・DLL・データ・ランタイムのみをコピーし、日本語の遊び方を加えてZIPを作ります。Burstの`DoNotShip`フォルダ、画面キャプチャ、ログ、開発用バックアップは梱包しません。SHA-256も出力します。同じバージョンの既存ZIPは上書きしません。梱包時の作業フォルダは出力先に残します。

## 検証と公開

作成したZIPを新しいフォルダへ展開し、全ファイルの一致と起動を確認します。ゲーム本体のPlayMode・Windows実画面の検証結果も確認してください。配布版には開発用の`--tank-town-*-smoke`診断は含まれません。

対象の変更をmainへ統合した後、公開対象コミットを固定してリリースを作成します。以下はバージョン0.1.0の例です。`release-notes.md`にはダウンロードするZIP名、起動手順、内容、対応OS、検証結果を記載します。

```powershell
gh release create v0.1.0 './Builds/Releases/TankTown-v0.1.0-Windows-x64.zip' './Builds/Releases/TankTown-v0.1.0-Windows-x64.zip.sha256' --repo Negi310/mission2026-game --target '<公開対象のmainコミット>' --title 'Tank Town v0.1.0 — Windows版' --notes-file './Builds/Releases/release-notes.md' --latest
```

ビルドはローカルで実行します。GitHub ActionsによるUnityビルド、自動更新・署名付きインストーラーは今回の配布には含めません。
