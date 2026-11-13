# カードメニューとWeb公開

タイトル・リザルト・一時停止の操作ボタンを、中央が大きくなる横スライド式に揃えています。ボタンは傾けません。スコアのボード・ランキング一覧・遊び方は通常表示です。
中央のカードをボタン（砲撃・Space/J/K・Enter）で決定します。
左右のハンドル、A/D、左右矢印、ゲームパッドの左スティックで選択します。
画面の左右の矢印とカードはマウスでも操作できます。

ハンドル入力は中央付近±0.32で再受付し、±0.72を超えると1枚移動します。
倒したままなら0.65秒後から0.40秒おきに移動します。メニューを開いた直後は一度中央へ戻します。
調整画面・接続診断が開いている間はメニューの選択と決定を停止します。

リザルトは表示後1.35秒、かつ最後の決定入力や押下状態から0.65秒以上落ち着いてから決定可能になります。砲撃を連打し続けたり押しっぱなしにしても再挑戦しません。解除後の新しい入力で選びます。マウスのボタンも同じ保護を使います。
一時停止・タイトル・リザルトの補助的な説明文と常設の操作案内は省き、操作方法は「あそびかた」へまとめています。

Web版はUnity 6000.3.19f1のWeb Build Supportを追加後、隔離したプロジェクトで以下を実行します。

    Unity.exe -batchmode -projectPath <project> -executeMethod TankTown.Editor.TankTownSetup.BuildWeb -quit -logFile <log>

出力先はBuilds/Web。gzip圧縮とUnityの展開フォールバックを使い、GitHub Pagesで特別な圧縮ヘッダーを必要としない形式です。
公開用テンプレートはAssets/WebGLTemplates/TankTown/index.htmlです。
PCのChrome/Edgeで「ハンドル接続」からBLE UART機器を選択できます。
日本語は同梱のNoto Sans JP、Web版の英字見出し・数値はUnity内蔵フォントを使い、OS依存のフォント取得や文字の欠けを避けます。
接続はブラウザーの許可が必要で、自動接続ではありません。実機のブラウザー接続は未検証です。

日本語フォントはNoto Sans JP（SIL Open Font License）。フォント本体とライセンスをResources/Fontsに同梱しています。
出典: https://github.com/notofonts/noto-cjk

ローカルのHTTPサーバーで読み込み・操作を確認した後、公開するソースのコミットを固定し、以下でPages用ブランチへ公開できます。

    ./scripts/Publish-TankTownPages.ps1 -BuildPath '<project>/Builds/Web' -SourceCommit '<40文字のコミットID>'

公開先: https://negi310.github.io/mission2026-game/
圧縮と展開フォールバックの説明: https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-deploying.html
