# Windows版の全画面起動

2026-10-12。Windows版を、モニターの解像度に合わせた枠なし全画面で起動するように変更しました。プロジェクトとビルド処理の設定を`FullScreenWindow`に統一し、通常起動時に一度だけデスクトップの解像度を適用します。以前のウィンドウ表示設定が残っていても全画面になります。Alt+Enterの切り替えを許可しています。Unity Editorとバッチモードでは起動時の解像度変更を行いません。

- Unity 6000.3.19f1でWindows x64 Development Buildが成功しました（`Logs/fullscreen-build.log`）。変更した3つのC#ファイルは、検証プロジェクトとレビュー用ブランチでSHA256が一致しています。
- 実プレイヤーをバッチモードを使わず、`-screen-fullscreen 0 -screen-width 960 -screen-height 540`付きで起動しました。Unityは`mode=FullScreenWindow, size=1920x1080, desktop=1920x1080, batch=False`を記録しています（`Logs/fullscreen-player.log`）。
- Win32のウィンドウ検査で、ウィンドウとモニターの範囲がともに`(0,0)-(1920,1080)`、描画領域が1920×1080、タイトルバーとサイズ変更枠がないことを確認しました（`Logs/fullscreen-native-window.json`）。全画面の確認は、画面キャプチャの大きさだけで判断していません。
- 同じ実ビルドの遊園地診断が終了コード0で完了しました。タイトルとプレイ画面を含む14枚を保存し、7種類の建造物、通常砲撃の部分破壊、支柱を失った場合の倒壊、報酬エリアの生成を確認しています。
- 1920×1080の実プレイ画像で、破壊度・残り時間・ミニマップ・下部のアイテムUIの収まりを確認しました。Chromeでは今回のタイトル・プレイ・1280×720画像を読み込み、画像の寸法、1920・1280・720・390px幅での閲覧、原寸リンクとエラーがないことを確認しました（`Logs/fullscreen-browser.log`）。ブラウザ検証の対象はUnityの実画面画像です。

ローカルの`Builds/TankTown/TankTown.exe`と`Builds/TankTown-Amusement/TankTown.exe`を更新しました。GitHubの既存v0.1.0配布物は変更していません。
