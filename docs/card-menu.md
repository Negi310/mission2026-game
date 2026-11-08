# カードメニューとWeb公開

タイトル・遊び方・リザルト・記録・一時停止は共通の回転カードUIです。
中央のカードをボタン（砲撃・Space/J/K・Enter）で決定します。
左右のハンドル、A/D、左右矢印、ゲームパッドの左スティックで選択します。
画面の左右の矢印とカードはマウスでも操作できます。

ハンドル入力は中央付近±0.24で再受付し、±0.48を超えると1枚移動します。
倒したままなら0.55秒後から0.32秒おきに移動します。メニューを開いた直後は一度中央へ戻します。
調整画面・接続診断が開いている間はメニューの選択と決定を停止します。

Web版はUnity 6000.3.19f1のWeb Build Supportを追加後、隔離したプロジェクトで以下を実行します。

    Unity.exe -batchmode -projectPath <project> -executeMethod TankTown.Editor.TankTownSetup.BuildWeb -quit -logFile <log>

出力先はBuilds/Web。GitHub Pagesで特別な圧縮ヘッダーを必要としない非圧縮形式です。
公開用テンプレートはAssets/WebGLTemplates/TankTown/index.htmlです。
PCのChrome/Edgeで「ハンドル接続」からBLE UART機器を選択できます。
接続はブラウザーの許可が必要で、自動接続ではありません。実機のブラウザー接続は未検証です。

日本語フォントはNoto Sans JP（SIL Open Font License）。フォント本体とライセンスをResources/Fontsに同梱しています。
出典: https://github.com/notofonts/noto-cjk
