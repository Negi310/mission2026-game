"""動作確認用のサンプル: 金属枠つきの補給木箱 (1m角)。"""

import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

args = bkit.script_args()
bkit.reset_scene()

wood = bkit.material("CrateWood", (0.62, 0.42, 0.22), roughness=0.8)
metal = bkit.material("CrateMetal", (0.32, 0.35, 0.37), metallic=0.9, roughness=0.4)

mb = bkit.MeshBuilder()
mb.box((0.96, 0.96, 0.96), (0, 0.5, 0), wood, bevel=0.01)
# 縦の角材と上下の帯
for x in (-0.48, 0.48):
    for z in (-0.48, 0.48):
        mb.box((0.08, 1.0, 0.08), (x, 0.5, z), metal, bevel=0.005)
for y in (0.04, 0.96):
    for side in (-0.48, 0.48):
        mb.box((1.0, 0.08, 0.08), (0, y, side), metal)
        mb.box((0.08, 0.08, 1.0), (side, y, 0), metal)
crate = mb.build("SupplyCrate")

out = args["out"]
bkit.export_fbx(os.path.join(out, "SupplyCrate.fbx"), crate)
bkit.save_blend(os.path.join(out, "SupplyCrate.blend"))
bkit.render_preview(os.path.join(out, "SupplyCrate.png"))
