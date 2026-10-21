"""動作確認用のサンプル: 金属枠つきの補給木箱 (1m角)。"""

import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

args = bkit.script_args()
bkit.reset_scene()

wood = bkit.material("CrateWood", (0.45, 0.28, 0.13), roughness=0.8)
metal = bkit.material("CrateMetal", (0.25, 0.27, 0.28), metallic=0.9, roughness=0.4)

parts = [bkit.box("Body", (0.96, 0.96, 0.96), (0, 0, 0.5), wood, bevel=0.01)]
# 縦の角材と上下の帯
for x in (-0.48, 0.48):
    for y in (-0.48, 0.48):
        parts.append(bkit.box("Post", (0.08, 0.08, 1.0), (x, y, 0.5), metal, bevel=0.005))
for z in (0.04, 0.96):
    parts.append(bkit.box("BandX", (1.0, 0.08, 0.08), (0, -0.48, z), metal))
    parts.append(bkit.box("BandX", (1.0, 0.08, 0.08), (0, 0.48, z), metal))
    parts.append(bkit.box("BandY", (0.08, 1.0, 0.08), (-0.48, 0, z), metal))
    parts.append(bkit.box("BandY", (0.08, 1.0, 0.08), (0.48, 0, z), metal))

crate = bkit.join("SupplyCrate", parts)
bkit.set_origin_to_bottom(crate)

out = args["out"]
bkit.export_fbx(os.path.join(out, "SupplyCrate.fbx"), [crate])
bkit.save_blend(os.path.join(out, "SupplyCrate.blend"))
bkit.render_preview(os.path.join(out, "SupplyCrate.png"))
