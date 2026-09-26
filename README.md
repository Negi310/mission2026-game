# TANK / TOWN

子供向けの三人称戦車アクションのUnityプロトタイプ。3分間で箱庭の街を破壊し、街全体のブロック数に対する破壊済みブロックの割合を競います。

![ゲーム画面](docs/images/tank-town-gameplay.png)

## 起動

Unity **6000.3.19f1** で開き、メニュー **Tank Town → Create or open playable scene** を選び、Playを押してください。開始画面の「あそぶ」またはEnterでスタートします。専用シーンは `Assets/TankTown/Scenes/TankTown.unity` です。街と戦車はPlay時に生成します。

## 操作

|操作|試作用の入力|
|---|---|
|前進|自動|
|左右旋回|A / D、← / →、ゲームパッド左スティック|
|左圧センサー相当|Space / J、ゲームパッドLボタン|
|右圧センサー相当|K、ゲームパッドRボタン|
|開始・再挑戦・再開|Enter、ゲームパッドStart、画面ボタン|
|一時停止|Esc。アプリがフォーカスを失った場合も停止|

砲塔は車体の正面に固定。小物は体当たりで破壊できます。大きな建物への強い衝突や街の外への移動で戦車が爆発し、安全地点へ戻ります。暫定で3秒のペナルティと1.2秒の復帰待ち時間があります。待ち時間も制限時間に含まれます。

## 実装範囲

- シード固定のカラフルな街、16棟のブロック建築、街灯・看板・木・箱。
- 砲撃地点の範囲破壊。下のブロックを失った同じ柱の上部もRigidbodyになり、衝撃と重力で崩落。
- 同じブロックは1回だけ加点。破片が消えても街全体の分母は変えません。
- 破片は暫定180個・9秒まで。破片と戦車は衝突せず、地面・未破壊建物には衝突します。
- 安全地点の履歴、復帰直後の衝突猶予、停止・タイムアップ・結果・再挑戦。
- 日本語HUD、三人称追従カメラ、簡単な発射・爆発音。

## 調整と実機接続

`Assets/TankTown/Resources/TankTownSettings.asset` で制限時間、速度、旋回速度、射撃間隔、爆風、復帰、圧センサー閾値、破片上限を変更できます。射撃間隔0.35秒は試作用の値です。

外部入力アダプターから `TankInput` に、Unityのメインスレッドで以下を渡してください。

```csharp
input.useHardware = true;
input.SetWheel(normalizedSteering); // -1: 左、0: 中央、+1: 右
input.SetPressure(0, leftPressure); // 0〜1
input.SetPressure(1, rightPressure);
// デバイス側で打撃を検出済みなら input.PulsePad(0 または 1)
```

センサーは押し込み閾値で1回発火し、解除閾値まで戻ると再発火できます。左右の信号は `PadPressed(int)` で区別していますが、試作では両方とも同じ砲撃につないでいます。左右の最終的な役割は未確定です。USB/シリアル/HIDの通信部分、キャリブレーション、実機での操作感は機材仕様確定後に追加・検証します。

パワーアップ、成長システム、イージーモードは未実装です。今回その仕様を固定していません。破壊は柱単位の支持判定で、隣接する梁の荷重計算や建築全体の構造解析ではありません。

## 検証・ビルド

Unity Test RunnerのPlayModeで `TankTown.Tests` を実行します。自動前進・旋回、砲弾命中、体当たり、支持喪失、重複加点防止、左右圧入力、射撃間隔、復帰、時間切れ、停止、再挑戦を検証します。

初回検証ではPlayMode 7件が成功しています。検証環境と制限は [検証記録](docs/validation.md) を参照してください。

Windows Development BuildはUnityを閉じた検証コピーで以下のメソッドをバッチ実行できます。

```text
Unity.exe -batchmode -projectPath <project> -executeMethod TankTown.Editor.TankTownSetup.BuildWindows -quit -logFile <log>
```

出力先は `Builds/TankTown/TankTown.exe`。初回起動時のパッケージ解決にはネットワークが必要です。

Development Buildを `TankTown.exe -batchmode -force-d3d11 --tank-town-smoke` で起動すると、開始・破壊・復帰・結果の描画検証を行い、実行ファイル横の `Captures` に4枚の画像を保存して終了します。

ネイティブUnityゲームのため、ブラウザ上のUI検証は対象外です。Unity PlayModeとWindows実行画面で検証します。
