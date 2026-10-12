# Windows配布版 v0.1.0

2026-10-12。mainへ統合された通常・大型・お菓子の3ステージと、直近の外壁支持修正を含むWindows x64版です。Unity 6000.3.19f1の別コピーで、`BuildWindowsRelease`を実行しました。ゲームの挙動は変更していません。

ビルド元コミットは`7800017e982eb2184b953c2db3e706a1f30ed53f`です。公開タグに至る追加変更は起動案内と検証文書です。240ファイルを比較し、テクスチャ・音声・ゲームコード・シーン・設定が揃うことを確認しました。Unityによる保存の差はYAMLの行末空白と、共有マテリアルへ保存されたシェーダー既定値11項目だけです。

- Windows x64の配布ビルド成功、Unityの終了コード0。`BuildOptions.None`で作成し、`build-info.json`にも`developmentBuild: false`を記録しました。
- `Package-TankTownRelease.ps1`で起動に必要なファイルと日本語の遊び方だけを梱包しました。`DoNotShip`・ログ・画面キャプチャは含めません。開発用の画面診断クラスがRuntime DLLから除外されたことも確認しました。
- ZIPを新しいフォルダへ展開し、172ファイルすべてのSHA-256とファイル数が梱包前と一致しました。
- 展開先のexeを非対話モードで20秒起動しました。既定のD3D12で初期化し、`stage=CandyTown`・`seconds=60`の生成ログと、例外・シェーダーエラーがないことを確認しました。確認後は検証側からプロセスを停止しました。この確認は手動プレイ全体の代わりにはなりません。
- 梱包スクリプトが同じバージョンのZIPを上書きせず、Developmentビルドを拒否することを確認しました。
- ゲーム本体は直前のPlayMode 15/15、Windowsの20画面、Chromeでの比較表示検証を引き継ぎます。[外壁支持修正の検証](candy-wall-support-validation.md)。今回は全PlayModeテストの再実行は行っていません。

配布ZIPは`TankTown-v0.1.0-Windows-x64.zip`、50,655,318 bytesです。

```text
SHA-256: 807d299812342b89bf2080dd8f9aced8e307f37bc3c6ec1d699fedd3f3b1f5ce
```

ローカルの記録は`Logs/windows-release-v0.1.0-build.log`、`source-check.json`、`package.json`、`validation.json`、`package-guards.json`、`player.log`（後5件も同じ`windows-release-v0.1.0-`接頭辞）です。配布ZIPと`.sha256`は`Builds/Releases`に保存しています。

GitHubリポジトリは非公開のため、リリースのダウンロードにはアクセス権のあるアカウントが必要です。
