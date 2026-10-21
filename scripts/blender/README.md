# Blenderモデル生成スクリプト

Blender 5.2 をバックグラウンドで動かし、Pythonスクリプトから3Dモデルを作る仕組みです。
GUIを開かずに、Unity向けFBX・`.blend`・確認用PNGを書き出します。

## 使い方

```powershell
pwsh -File .\scripts\blender\Invoke-Blender.ps1 -Script .\scripts\blender\models\supply_crate.py
```

Windows PowerShell 5 (`powershell`) は既定の実行ポリシーでスクリプトを止めるため、PowerShell 7 (`pwsh`) で実行します。

- 出力先は既定で `output/blender/`（gitには入りません）。`-OutputDirectory` で変更できます。
- Blenderの場所は `D:\Program Files\Blender Foundation\Blender 5.2\blender.exe` を自動で探します。別の場所なら `-BlenderPath` か環境変数 `BLENDER_EXE` で指定します。
- 実行ログは出力先の `<スクリプト名>.log` に残ります。

## モデルスクリプトの書き方

`models/` にスクリプトを置き、`bkit.py` の関数を使います（例: `models/supply_crate.py`）。

| 関数 | 内容 |
| --- | --- |
| `reset_scene()` | 空のシーンから始める |
| `material(name, color, ...)` | PBRマテリアルを作る |
| `box(...)` / `cylinder(...)` | 基本形状を置く（メートル単位、Z上） |
| `join(name, objects)` | モディファイアを適用して1メッシュにまとめる |
| `set_origin_to_bottom(obj)` | 原点を底面中心にする（Unityで地面に置きやすい） |
| `export_fbx(path, objects)` | Unity向けFBX（Y上・スケール1）を書き出す |
| `render_preview(path)` | 形の確認用PNGを描画する |

Unityで使うときは、書き出したFBXを `Assets/` 以下にコピーします。
