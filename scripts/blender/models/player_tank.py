"""プレイヤー戦車の新モデル。

寸法・回転軸・部品名は TankTownWorld.CreateTank に合わせている。
- 原点は車体中心、接地面は Y=-0.8 (当たり判定 2.6×1.6×3.4 の底)
- "Turret pivot" (0, 0.68, -0.15) で砲塔が回り、"Fixed cannon pivot" で砲身が上下する
- "Shot origin" は砲身の軸から前へ2.8

ゲームへ入れるときは `-ScriptArguments --assets` を付けて実行し、
Assets/TankTown/Resources/Models/PlayerTank.fbx も書き出す。
"""

import math
import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

TURRET_PIVOT = (0.0, 0.68, -0.15)
CANNON_PIVOT = (0.0, 0.72, 0.40)  # 砲塔の回転軸から (0, 0.04, 0.55)
SHOT_DISTANCE = 2.8


def palette():
    return {
        "lime": bkit.material("TankLime", (0.64, 1.0, 0.04), roughness=0.45),
        "cream": bkit.material("TankCream", (1.0, 0.94, 0.77), roughness=0.5),
        "dark": bkit.material("TankDark", (0.055, 0.09, 0.16), metallic=0.4, roughness=0.45),
        "rubber": bkit.material("TankRubber", (0.12, 0.16, 0.19), roughness=0.85),
        "yellow": bkit.material("TankReflector", (1.0, 0.83, 0.07), roughness=0.3, emission=0.6),
        "lamp": bkit.material("TankLamp", (1.0, 0.85, 0.54), roughness=0.2, emission=2.0),
    }


def track_outline(half_length, radius, center_y, pitch=0.27, tooth=0.06, step=0.05):
    """履帯を横から見た輪郭 (Z, Y)。外周に一定間隔で滑り止めの山をつける。"""
    perimeter = 4 * half_length + 2 * math.pi * radius
    halves = max(2, round(perimeter / (pitch / 2)))
    halves += halves % 2
    segment = perimeter / halves

    def at(s):
        straight = 2 * half_length
        arc = math.pi * radius
        if s < straight:  # 下の直線 (後→前)
            return (-half_length + s, center_y - radius), (0.0, -1.0)
        s -= straight
        if s < arc:  # 前の円弧 (下→上)
            angle = -math.pi / 2 + s / radius
            return (half_length + radius * math.cos(angle), center_y + radius * math.sin(angle)), (math.cos(angle), math.sin(angle))
        s -= arc
        if s < straight:  # 上の直線 (前→後)
            return (half_length - s, center_y + radius), (0.0, 1.0)
        s -= straight
        angle = math.pi / 2 + s / radius  # 後ろの円弧 (上→下)
        return (-half_length + radius * math.cos(angle), center_y + radius * math.sin(angle)), (math.cos(angle), math.sin(angle))

    points = []
    for k in range(halves):
        offset = tooth if k % 2 == 0 else 0.0
        samples = max(1, math.ceil(segment / step))
        for i in range(samples + 1):
            (z, y), (nz, ny) = at(min(k * segment + segment * i / samples, perimeter - 1e-6))
            points.append((z + nz * offset, y + ny * offset))
    return points


def build_tracks(mb, p):
    """履帯: 滑り止めの山がついた輪の外形と、外側の転輪・起動輪・誘導輪。

    車体と別メッシュにして、ブルドーザー時の車体の色替えや履帯だけの演出に使えるようにする。
    """
    lime, cream, dark = p["lime"], p["cream"], p["dark"]
    for side in (-1, 1):
        mb.prism(track_outline(1.33, 0.39, -0.35), 0.62, (side * 1.2, 0, 0), p["rubber"])
        wheel_x = side * 1.545
        for z in (-0.96, -0.48, 0.0, 0.48, 0.96):
            mb.cylinder(0.28, 0.06, (wheel_x, -0.42, z), cream, axis="x")
            mb.cylinder(0.11, 0.05, (wheel_x + side * 0.05, -0.42, z), lime, axis="x")
        mb.cylinder(0.31, 0.07, (wheel_x, -0.33, 1.33), dark, axis="x", segments=12)
        mb.cylinder(0.12, 0.06, (wheel_x + side * 0.05, -0.33, 1.33), cream, axis="x")
        mb.cylinder(0.27, 0.06, (wheel_x, -0.33, -1.33), cream, axis="x")
        mb.cylinder(0.1, 0.05, (wheel_x + side * 0.05, -0.33, -1.33), lime, axis="x")


def build_hull(mb, p):
    lime, cream, dark = p["lime"], p["cream"], p["dark"]
    # 下部車体（左右の履帯の間）と、前面が傾斜した上部車体
    mb.prism([(-1.45, -0.58), (1.35, -0.58), (1.72, -0.05), (-1.62, -0.05)], 1.76, mat=lime, bevel=0.04)
    mb.prism([(-1.62, -0.05), (1.72, -0.05), (1.72, 0.14), (1.05, 0.5), (-1.5, 0.5), (-1.62, 0.38)], 2.1, mat=lime, bevel=0.05)

    for side in (-1, 1):
        x = side * 1.2
        # フェンダーと前後の泥よけ
        mb.box((0.68, 0.06, 3.3), (x, 0.19, 0), lime, bevel=0.02)
        mb.box((0.68, 0.06, 0.4), (x, 0.105, 1.83), lime, bevel=0.02, pitch=25)
        mb.box((0.68, 0.06, 0.4), (x, 0.105, -1.83), lime, bevel=0.02, pitch=-25)

        # 前照灯、反射板、牽引フック、工具箱、側面の帯
        mb.cylinder(0.11, 0.12, (x, 0.33, 1.5), dark, axis="z")
        mb.cylinder(0.085, 0.02, (x, 0.33, 1.565), p["lamp"], axis="z")
        mb.box((0.26, 0.12, 0.05), (side * 0.83, 0.2, -1.645), p["yellow"])
        mb.box((0.14, 0.12, 0.14), (side * 0.55, 0.02, 1.76), dark, bevel=0.02)
        mb.box((0.48, 0.2, 0.8), (x, 0.32, -0.9), dark, bevel=0.03)
        mb.box((0.02, 0.09, 2.2), (side * 1.06, 0.36, -0.15), cream)

        # 排気管
        mb.cylinder(0.075, 0.18, (side * 0.62, 0.28, -1.68), dark, axis="z", segments=16)

    # 後部のエンジンデッキと吸気グリル
    mb.box((1.5, 0.08, 0.8), (0, 0.54, -1.0), lime, bevel=0.02)
    for i in range(6):
        mb.box((1.3, 0.03, 0.06), (0, 0.595, -0.7 - i * 0.12), dark)


def build_turret(mb, p):
    lime, cream, dark = p["lime"], p["cream"], p["dark"]
    # 前すぼまりの砲塔本体
    outline = [(-0.5, 0.72), (0.5, 0.72), (0.8, 0.25), (0.8, -0.85), (0.62, -1.1), (-0.62, -1.1), (-0.8, -0.85), (-0.8, 0.25)]
    mb.prism(outline, 0.5, (0, 0.75, 0), lime, axis="y", bevel=0.07)
    for side in (-1, 1):
        mb.box((0.02, 0.08, 1.0), (side * 0.81, 0.82, -0.35), cream)
        # 発煙弾発射機
        for y in (0.82, 0.94):
            mb.cylinder(0.055, 0.25, (side * 0.88, y, 0.12), dark, axis="z", segments=12, pitch=-20)

    # 車長用キューポラとハッチ、装填手ハッチ、ペリスコープ
    mb.cylinder(0.28, 0.16, (0.32, 1.08, -0.45), lime, segments=28)
    mb.cylinder(0.25, 0.05, (0.32, 1.185, -0.45), cream, segments=28)
    mb.box((0.25, 0.04, 0.04), (0.32, 1.225, -0.45), dark)
    mb.box((0.45, 0.04, 0.5), (-0.35, 1.02, -0.5), cream, bevel=0.015)
    for x in (-0.15, 0.15):
        mb.box((0.2, 0.1, 0.1), (x, 1.04, 0.42), dark, bevel=0.015)

    # 後部の物入れとアンテナ
    mb.box((1.3, 0.28, 0.35), (0, 0.78, -1.27), dark, bevel=0.03)
    mb.cylinder(0.06, 0.08, (-0.55, 1.04, -0.85), dark, segments=12)
    mb.cylinder(0.018, 1.2, (-0.55, 1.64, -0.85), dark, segments=8)
    mb.sphere(0.06, (-0.55, 2.26, -0.85), p["yellow"])


def build_cannon(mb, p):
    lime, cream, dark = p["lime"], p["cream"], p["dark"]
    z0 = CANNON_PIVOT[2]
    y = CANNON_PIVOT[1]
    mb.box((0.75, 0.42, 0.3), (0, y, 0.82), lime, bevel=0.08)  # 防盾
    mb.cylinder(0.17, 0.5, (0, y, 1.2), cream, axis="z")
    mb.cylinder(0.125, 1.3, (0, y, 2.1), cream, axis="z")
    mb.cylinder(0.175, 0.3, (0, y, 2.0), cream, axis="z")  # 排煙器
    mb.cylinder(0.19, 0.3, (0, y, z0 + 2.48), dark, axis="z", segments=8)  # 砲口制退器


def build_tank():
    """戦車を組み立て、ルート ("Tank visual") を返す。"""
    p = palette()
    root = bkit.empty("Tank visual")

    mb = bkit.MeshBuilder()
    build_hull(mb, p)
    mb.build("Hull", parent=root)
    mb = bkit.MeshBuilder()
    build_tracks(mb, p)
    mb.build("Tracks", parent=root)

    turret_pivot = bkit.empty("Turret pivot", TURRET_PIVOT, root)
    mb = bkit.MeshBuilder()
    build_turret(mb, p)
    mb.build("Turret", TURRET_PIVOT, turret_pivot)

    cannon_pivot = bkit.empty("Fixed cannon pivot", CANNON_PIVOT, turret_pivot)
    mb = bkit.MeshBuilder()
    build_cannon(mb, p)
    mb.build("Fixed cannon", CANNON_PIVOT, cannon_pivot)
    bkit.empty("Shot origin", (CANNON_PIVOT[0], CANNON_PIVOT[1], CANNON_PIVOT[2] + SHOT_DISTANCE), cannon_pivot)
    return root


if __name__ == "__main__":
    args = bkit.script_args()
    bkit.reset_scene()
    tank = build_tank()
    out = args["out"]
    bkit.export_fbx(os.path.join(out, "PlayerTank.fbx"), tank)
    if args.get("assets"):
        # ゲームが Resources.Load("Models/PlayerTank") で読む場所
        repo = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
        bkit.export_fbx(os.path.join(repo, "Assets", "TankTown", "Resources", "Models", "PlayerTank.fbx"), tank)
    bkit.save_blend(os.path.join(out, "PlayerTank.blend"))
    bkit.render_preview(os.path.join(out, "PlayerTank_front.png"), direction=(1.0, 0.75, 1.25))
    bkit.render_preview(os.path.join(out, "PlayerTank_rear.png"), direction=(-1.1, 0.9, -1.0))
