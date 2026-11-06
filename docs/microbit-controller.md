# micro:bit ハンドル（Windows / Bluetooth・USB）

現在の受信形式は **`角度,ピエゾ値\n`** です。送信側で既にこの形式を出している場合、そのコードを使ってください。旧USB用 `hardware/microbit/main.py` に書き換える必要はありません（これはBluetooth非対応の別方式です）。

## Bluetoothで接続する

1. PCの「設定 → Bluetoothとデバイス」でBluetoothをオンにします。PCはBLE対応のBluetoothアダプターが必要です。
2. コントローラーの送信プログラムを起動し、必要ならそのファームウェアの手順でペアリング可能な状態にします。
3. Windowsの「デバイスの追加 → Bluetooth」からmicro:bitをペアリングします。今回のBLE受信処理はWindowsに登録されたGATTサービスを列挙する方式です。ゲーム内のペアリング画面はありません。
4. スマホのUART確認アプリなど、コントローラーへ接続している別アプリは切断します。
5. 対応ビルドの `TankTown.exe` を起動します。標準設定 `transport: auto` はBLE UARTを探し、見つからなければ従来のUSBも探します。Bluetoothだけに限定するには設定を `bluetooth` にするか、起動引数 `--microbit-bluetooth` を付けます。
6. Unity Console／Player.logに `MICROBIT CONNECTED: Input System wheel` が出たら、左右へ傾けて旋回、ピエゾを叩いて砲撃を試します。タイトル・リザルトでも砲撃入力で開始／再挑戦できます。

**現在のBluetooth送信方式・実機との接続は未確認です。** BLE UART（micro:bit／Nordic系）の標準サービスを初期値として実装しています。別のサービスなら送信コードに合わせてUUIDを変更してください。Bluetoothという名称だけではBLE UARTかSPPかを判別できません。

micro:bitのUARTはBluetooth Low EnergyのGATTサービスであり、WindowsのCOMポートやHIDゲームコントローラーとは別の方式です。[micro:bit公式案内](https://support.microbit.org/support/solutions/articles/19000062330-using-the-micro-bit-bluetooth-low-energy-uart-serial-over-bluetooth-)。

## 受信と入力への変換

送信例（括弧・角括弧は付けません）：

```text
0,0
-30,512
1023,980
```

各行の末尾に本物の改行LFを付けます。文字としてのバックスラッシュ＋nではありません。CRLF、コンマ前後の空白、小数の角度にも対応します。角度の送信値-1023〜1023、ピエゾ0〜1023の整数を受け付けます。範囲外、NaN、Infinity、不正な行、64文字超の行は無視します。BLEの複数通知に分割された行は改行まで結合し、1通知に複数行あってもすべて解析します。

- 角度の送信値は中立・最大旋回値・遊びを反映して-1〜1へ正規化します。初期値は中立0、±1023で最大旋回、±34は遊びです。入力範囲は利用者の訂正に合わせて変更しています。
- ピエゾは初期値350以上の立ち上がりで1発。180以下まで戻ると再受付します。叩いた後の振動による再発火を抑える最短間隔は0.12秒です。これはゲーム内の砲撃クールタイムと別です。
- 初接続・再接続時の高いピエゾ値で誤射せず、一度低い値へ戻ってから受け付けます。高いまま保持しても連射しません。
- 画面更新の間に届いた低→高→低の短いピークも判定します。同じ画面更新内の複数の砲撃は1発にまとめます。
- 有効な行が0.35秒止まると入力を解除し、キーボード・ゲームパッドへ戻ります。再接続は自動で試みます。フォーカスが外れている間の砲撃は破棄します。

## 閾値・感度を調整する

`TankTown.exe` と同じフォルダーの **`microbit-controller.json`** を編集してゲームを再起動します。Unity Editorではプロジェクト直下の同名ファイルを読みます。起動引数 `--microbit-config "C:/path/settings.json"` で別の設定も選べます。設定ファイルがない場合は同じ初期値を使います。

|設定|初期値|意味|
|---|---|---|
|`transport`|`auto`|`bluetooth`はBLE UART、`serial`は指定COM、`usb`はUSB、`off`は無効|
|`serialPort`|空欄|COM方式のとき `COM5` など。空欄ではmicro:bit USBを探索|
|`bluetoothDevice`|空欄|複数台ある場合、接続する機器のBluetoothアドレスで絞る。例 `AA:BB:CC:DD:EE:FF`。機器名ではない|
|`serviceUuid`|`6e400001-b5a3-f393-e0a9-e50e24dcca9e`|micro:bit／Nordic UARTサービス|
|`receiveUuid`|空欄|空欄ではUARTサービスの通知・indication可能な特性を選択。独自UUIDも指定可能|
|`angleCenter`|`0`|中立の送信値|
|`fullSteerAngle`|`1023`|中立から最大旋回までの送信値。少ない傾きで曲げるなら512など|
|`deadzoneDegrees`|`34`|中立付近の遊び。キー名は旧来のまま、送信値と同じ単位で指定|
|`invertSteering`|`false`|左右を反転|
|`piezoThreshold`|`350`|砲撃する値|
|`piezoRelease`|`180`|再受付するために戻る値。砲撃閾値より小さくする|
|`piezoCooldownSeconds`|`0.12`|振動による連発を防ぐ最短間隔|
|`timeoutSeconds`|`0.35`|有効な行を受信しなくなって入力を切る秒数|

初期の閾値は仮の値です。次の生値表示で「触っていないとき」「弱く叩いたとき」「強く叩いたとき」を確認し、触っていない値より十分上、軽く叩いたピークより下に `piezoThreshold` を設定します。通常の静止値が180より高い機材では `piezoRelease` も上げてください。

設定の省略項目には初期値を使います。不正な設定の場合は警告を出してコントローラー入力を無効にし、キーボード操作を継続できます。

## Unity Input Systemで確認する

経路は「Bluetooth GATT／COM → 改行まで受信 → コンマで解析 → 感度・閾値判定 → MicrobitWheel → TankInput」です。受信は背景処理、Input Systemへのイベント発行はメインスレッドで行います。

UnityでPlayし、`Window > Analysis > Input Debugger > Devices > micro:bit Wheel` を開きます。**有効なデータを受信している間だけ**表示されます。

|値|範囲|Binding|
|---|---|---|
|旋回|補正済み-1〜1|`<MicrobitWheel>/steering`|
|砲撃|閾値で生成した押下・解放|`<MicrobitWheel>/horn`|
|角度の生値|-1023〜1023|`<MicrobitWheel>/angle`|
|ピエゾの生値|整数0〜1023|`<MicrobitWheel>/piezo`|

旋回はValue/Axis、砲撃はButton、ピエゾの確認はPassThrough/Integerなどで利用できます。ゲームはInput SystemのDynamic Updateを使用します。Windowsの `joy.cpl` には表示しません。

## BluetoothでCOMポートが作られる機材の場合

Bluetooth SPP／RFCOMMでWindowsにCOMポートが割り当てられる機材なら、Windowsでペアリング後、デバイスマネージャーなどで**送信ポート**を確認します。設定を `transport: serial`、`serialPort: COM5` などにするか、次のように起動します。BLE UARTをCOM方式で開くことはできません。

```powershell
.\TankTown.exe --microbit-port COM5
```

シリアル設定は115200bps、8N1。同じ `角度,ピエゾ値\n` を処理します。左右反転は `--microbit-invert`、接続を無効にするには `--microbit-off` が使えます。

## 従来のUSB方式を使う場合

旧 `hardware/microbit/main.py` は、Aでデジタルクラクションの接続ピンを選び、Bで中立を決めるUSB専用ファームウェアです。旧形式 `TT1,<旋回-1000〜1000>,<累計ボタン0〜65535>\n` も引き続き受信できます。この形式ではピエゾ閾値設定を使いません。USB上で新しい2値の形式を送って試すこともできます。

## 確認範囲

Bluetooth受信・CSV解析・閾値判定はv0.4.0からの対応です。配布ZIPに設定JSONと本手順、旧USB専用ファームウェアを同梱します。物理コントローラーのペアリング、サービスUUID、角度の符号、ピエゾの実測による閾値調整は実機での確認が必要です。

WindowsのBLE処理は標準APIを使い、外部ブリッジやPython実行環境を必要としません。[GATT通知](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothleapis/nf-bluetoothleapis-bluetoothgattregisterevent)、[通知購読](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothleapis/nf-bluetoothleapis-bluetoothgattsetdescriptorvalue)。

## 検証記録（2026-11-06）

Unity 6000.3.19f1の分離コピーでPlayMode 40件成功。2値CSV、小数・空白・CRLF、範囲外データ拒否、分割通知と複数行、短いピエゾピーク、ヒステリシス、クールダウン、切断／再接続、受信キュー上限、角度補正、Input ActionsとTankInputへの砲撃・旋回、生値の伝達、WindowsのBLEサービス列挙と未接続時の失敗、既存のラウンド遷移を確認しました。記録は `Logs/microbit-bluetooth-final-tests.xml` と対応ログです。

Windows Development Build成功。`Builds/TankTown-Bluetooth/TankTown.exe` に設定ファイルとこの手順を同梱しています。Bluetoothのみの探索を有効にした実行版で、タイトルの4解像度・ホバー、遊び方、カウントダウン、砲撃、リザルト、Space再挑戦が成功し、終了コード0でした。記録は `Logs/microbit-bluetooth-build.log`、`microbit-bluetooth-player.log`、`microbit-bluetooth-smoke-status.json`。BLEで実機データを受信した試験ではありません。この記録はDevelopment版の検証です。配布用ビルドの最終結果はv0.4.0のリリース本文に記載します。

入力範囲の訂正後、送信値±1023を最大旋回として受理し、±528.5で半分の旋回量、±34以内で中立になることを確認しました。追加した端点・中間値を含むPlayMode 43件が成功しています。記録は `Logs/release-v040-range-tests.xml` と対応ログです。ピエゾの範囲0〜1023と閾値は変更していません。
