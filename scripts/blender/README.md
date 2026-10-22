# Blenderモデル生成スクリプト

Blender 5.2 をバックグラウンドで動かし、Pythonスクリプトから3Dモデルを作る仕組みです。
GUIを開かずに、Unity向けFBX・`.blend`・確認用PNGを書き出します。

## 使い方

```powershell
pwsh -File .\scripts\blender\Invoke-Blender.ps1 -Script .\scripts\blender\models\player_tank.py
```

Windows PowerShell 5 (`powershell`) は既定の実行ポリシーでスクリプトを止めるため、PowerShell 7 (`pwsh`) で実行します。

- 出力先は既定で `output/blender/`（gitには入りません）。`-OutputDirectory` で変更できます。
- Blenderの場所は `D:\Program Files\Blender Foundation\Blender 5.2\blender.exe` を自動で探します。別の場所なら `-BlenderPath` か環境変数 `BLENDER_EXE` で指定します。
- 実行ログは出力先の `<スクリプト名>.log` に残ります。

## モデル

| スクリプト | 内容 | 出力 |
| --- | --- | --- |
| `models/player_tank.py` | プレイヤー戦車。寸法・回転軸・部品名は `TankTownWorld.CreateTank` と同じ | `PlayerTank.fbx`、前後のプレビュー |
| `models/city_kit.py` | 通常の街・大きな街の部品一式。窓・店先・バルコニー・屋上の飾り、街灯・標識・木・木箱・ベンチ・噴水・ドラム缶・燃料タンク・ガードレール・電柱・信号機、横断歩道・マンホール・車線の破線、雲・遠くの島・帆船 | `CityKit.fbx`、部品一覧のプレビュー |
| `models/city_buildings.py` | ビル群の街の高層ビル・集合住宅・商店を1棟ずつ作った試作 (ゲームでは未使用) | `CityTower.fbx` など3つ、街並みのプレビュー |
| `models/supply_crate.py` | 動作確認用の木箱 | `SupplyCrate.fbx` |

戦車は `Tank visual` の下に `Hull`、`Tracks`、`Turret pivot` → `Turret`、`Fixed cannon pivot` → `Fixed cannon`、`Shot origin` を持ちます。

### 戦車をゲームへ入れる

```powershell
pwsh -File .\scripts\blender\Invoke-Blender.ps1 -Script .\scripts\blender\models\player_tank.py -ScriptArguments --assets
```

`--assets` を付けると `Assets/TankTown/Resources/Models/PlayerTank.fbx` も書き出します。ゲームは `TankTownWorld.CreateTank` でこれを読み込み、FBXのマテリアルを名前 (`TankLime` など) で共通のトイ風マテリアルへ置き換えます。設定の `importedTankModel` をオフにするか、FBXが無い場合は従来の箱の戦車になります。部品名と回転軸を変えるとゲーム側の参照が切れるため、`TankTownTankModelTests` で確認してください。
建物は部材ごと (`Pillar L<階> S<面>`、`Beam`、`Floor slab`、`Facade panel`、屋根) の別オブジェクトで、原点は底面中心、入口は -Z 側です。

### 街の部品一式をゲームへ入れる

```powershell
pwsh -File .\scripts\blender\Invoke-Blender.ps1 -Script .\scripts\blender\models\city_kit.py -ScriptArguments --assets
```

`Assets/TankTown/Resources/Models/CityKit.fbx` を書き出します。各部品は空オブジェクト `<部品名>` の下に、1マテリアルずつのメッシュ `<部品名>|<オブジェクト名>` を並べた形です。ゲームの `TownModelKit` がこれを読み、壊せる部材や小物の子として置きます。

- 建物の部材は従来どおり箱のままで、当たり判定・破壊・分裂・遠景の簡略化は変わりません。窓などの飾りだけを部品に置き換えます。
- 建物はマテリアルごとにまとめて描くため、部品は1メッシュ1マテリアルにしています。まとめる処理でメッシュを読むので、FBXの読み込み設定でRead/Writeを有効にしています (`CityKit.fbx.meta`)。
- 部品の基準点と標準の寸法は `city_kit.py` の先頭に書いてあり、ゲーム側 (`TownFacadeDetails.cs` など) と合わせています。変えるときは両方を直し、`TankTownCityKitTests` で確認してください。
- 設定の `importedCityModels` をオフにすると従来の箱の飾りに戻ります。お菓子の街・遊園地では使いません。

## モデルスクリプトの書き方

座標・寸法・回転は **Unityと同じ** (X右, Y上, Z前, メートル, 度) で書きます。Blender内部の向きとFBXの変換は `bkit.py` が受け持ちます。

| 関数 | 内容 |
| --- | --- |
| `reset_scene()` | 空のシーンから始める |
| `material(name, color, ...)` | マテリアルを作る。色はUnityのインスペクターと同じsRGB |
| `empty(name, position, parent)` | 回転軸などの空オブジェクト |
| `MeshBuilder()` | 部品をまとめて1メッシュにする。`box` / `cylinder` / `sphere` / `prism` (輪郭の押し出し)、`at(...)` でローカル座標 |
| `MeshBuilder.build(name, origin, parent)` | メッシュにする。`origin` が回転軸 (原点) |
| `MeshBuilder.build_split(piece, ...)` | ゲームの部品用に、マテリアルごとの別メッシュへ分ける。`normalize` で縦横奥行き1に収め、`shade` で頂点色を付ける |
| `place(root, position, yaw)` | プレビュー用にルートを動かす (FBXには影響しない) |
| `export_fbx(path, root)` | root以下をUnity向けFBX (Y上・スケール1) で書き出す |
| `render_preview(path, direction, ...)` | 形の確認用PNGを描画する |

FBXの「Apply Transform (bake_space_transform)」は2階層以上の子の位置を誤るため使っていません。メッシュと子の位置をFBXの座標系で持ち、ルートだけBlender用に回しています。

Unityで使うときは、書き出したFBXを `Assets/` 以下にコピーします。
