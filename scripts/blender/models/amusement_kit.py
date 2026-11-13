"""遊園地の遊具と小物の部品一式。ゲームは TownModelKit で Models/AmusementKit を読み込む。

部品は空オブジェクト "<部品名>" の下の "<部品名>|<オブジェクト名>" (1マテリアルずつ) で、
メートル単位・Unity座標で作る。基準点 (原点) はゲーム側 (TankTownAmusementWorld) の
物理部材 (箱の当たり判定) の中心に合わせてある。

- 骨組み: "Tube" は直径1・長さ1、"Truss 3/6/12" は断面1×1・長さ1 (Y方向)。ゲーム側で (太さ, 長さ, 太さ) に伸ばす
- 観覧車の外周は長さ4 (Y方向)、スポークは長さ1、コースターの線路は長さ3 (Z方向が進行方向、Yが線路の上) で作り、
  ゲーム側で長さだけ合わせる
- 屋根の布 (メリーゴーラウンド・テント) は、ゲーム側の傾けた板の当たり判定の向きで作る
- ParkTint は部材ごとの色、ParkAccent は乗り物ごとの色、ParkPaint は報酬の色に塗り替える

    pwsh -File scripts/blender/Invoke-Blender.ps1 -Script scripts/blender/models/amusement_kit.py -ScriptArguments --assets
"""

import math
import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

ASSET_PATH = os.path.join("Assets", "TankTown", "Resources", "Models", "AmusementKit.fbx")

# メリーゴーラウンド (1区画、幅20) とサーカステント (2区画) の寸法。ゲーム側と同じ値
CAROUSEL_RADIUS = 9.4
TENT_RADIUS, TENT_APEX, TENT_EAVE = 10.5, 15.5, 6.5


def palette():
    """マテリアル名はゲーム側の対応表のキー。色はプレビュー用の目安。"""
    m = bkit.material
    return {
        "tint": m("ParkTint", (1.0, 0.77, 0.025), metallic=0.3, roughness=0.35),
        "accent": m("ParkAccent", (0.015, 0.68, 1.0), roughness=0.3),
        "paint": m("ParkPaint", (1.0, 0.045, 0.5), roughness=0.35),
        "red": m("ParkRed", (1.0, 0.13, 0.045), metallic=0.3, roughness=0.35),
        "yellow": m("ParkYellow", (1.0, 0.77, 0.025), metallic=0.3, roughness=0.35),
        "blue": m("ParkBlue", (0.015, 0.68, 1.0), metallic=0.3, roughness=0.35),
        "pink": m("ParkPink", (1.0, 0.045, 0.5), roughness=0.4),
        "white": m("ParkWhite", (1.0, 0.96, 0.81), metallic=0.2, roughness=0.4),
        "dark": m("ParkDark", (0.055, 0.09, 0.16), metallic=0.4, roughness=0.45),
        "glass": m("ParkGlass", (0.04, 0.34, 0.59), metallic=0.2, roughness=0.15),
        "lamp": m("ParkLamp", (1.0, 0.85, 0.54), emission=2.0),
        "leaf": m("ParkLeaf", (0.12, 0.84, 0.42), roughness=0.8),
        "wood": m("ParkWood", (0.62, 0.36, 0.18), roughness=0.7),
        "lime": m("ParkLime", (0.64, 1.0, 0.04)),
    }


def star(outer, inner=None, points=5):
    """正面から見た星形 (上向き)。"""
    inner = inner or outer * 0.45
    return [((outer if k % 2 == 0 else inner) * math.cos(math.radians(90 + k * 180 / points)),
             (outer if k % 2 == 0 else inner) * math.sin(math.radians(90 + k * 180 / points))) for k in range(points * 2)]


def scallops(width, top, bottom, count, depth, steps=4):
    """正面から見た、下の縁が波形の帯 (日よけや屋根の垂れ幕)。"""
    outline = [(-width / 2, top)]
    for k in range(count):
        x0 = -width / 2 + width * k / count
        for s in range(steps + 1):
            t = s / steps
            outline.append((x0 + width / count * t, bottom - depth * math.sin(math.pi * t)))
    outline.append((width / 2, top))
    return outline


def circle(radius, y, steps):
    return [(math.cos(2 * math.pi * k / steps) * radius, y, math.sin(2 * math.pi * k / steps) * radius) for k in range(steps)]


def rounded_rect(width, height, radius, steps=4):
    points = []
    for cx, cy, start in ((width / 2 - radius, height / 2 - radius, 0), (-width / 2 + radius, height / 2 - radius, 90),
                          (-width / 2 + radius, -height / 2 + radius, 180), (width / 2 - radius, -height / 2 + radius, 270)):
        for i in range(steps + 1):
            a = math.radians(start + 90 * i / steps)
            points.append((cx + radius * math.cos(a), cy + radius * math.sin(a)))
    return points


# ---------------------------------------------------------------- 骨組み (ゲーム側で伸ばす)

def steel_tube(p):
    """直径1・長さ1の鋼管。スポーク・柱・手すりに使う。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.5, 1.0, mat=p["tint"], segments=10)
    return mb, {"ParkTint": "Steel tube"}


def truss(p, bays):
    """断面1×1・長さ1の角形トラス。4本の弦材と、各面のジグザグの斜材。"""
    mb = bkit.MeshBuilder()
    c = 0.42
    for x in (-c, c):
        for z in (-c, c):
            mb.cylinder(0.075, 1.0, (x, 0, z), p["tint"], segments=6)
    corners = [(c, -c), (c, c), (-c, c), (-c, -c)]
    sides = [(corners[k], corners[(k + 1) % 4]) for k in range(4)]
    step = 1.0 / bays
    for k in range(bays):
        y0, y1 = -0.5 + k * step, -0.5 + (k + 1) * step
        for a, b in sides:
            if k % 2:
                a, b = b, a
            mb.tube([(a[0], y0, a[1]), (b[0], y1, b[1])], 0.035, mat=p["tint"], sides=4)
    for y in (-0.47, 0.47):
        for a, b in sides:
            mb.tube([(a[0], y, a[1]), (b[0], y, b[1])], 0.04, mat=p["tint"], sides=4)
    return mb, {"ParkTint": "Lattice"}


# ---------------------------------------------------------------- 観覧車

def wheel_rim(p):
    """観覧車の外周の1区間 (長さ4、Y方向)。前後2本のレール (Z±1.3) と、外側 (-X) の電球。"""
    mb = bkit.MeshBuilder()
    for z in (-1.3, 1.3):
        mb.cylinder(0.17, 4.0, (0, 0, z), p["tint"], segments=8)
        for y in (-1.0, 1.0):
            mb.sphere(0.15, (-0.26, y, z), p["lamp"], segments=6)
    mb.cylinder(0.08, 2.6, (0, 0, 0), p["white"], axis="z", segments=6)
    mb.tube([(0, -1.95, -1.3), (0, 0, 1.3), (0, 1.95, -1.3)], 0.05, mat=p["white"], sides=4)
    return mb, {"ParkTint": "Wheel rim rails", "ParkWhite": "Wheel rim ties", "ParkLamp": "Wheel rim bulbs"}


def twin_spoke(p):
    """前後の面に1本ずつ並ぶスポーク (長さ1、Y方向)。間にゴンドラが通る。"""
    mb = bkit.MeshBuilder()
    for z in (-1.25, 1.25):
        mb.cylinder(0.1, 1.0, (0, 0, z), p["tint"], segments=6)
    return mb, {"ParkTint": "Wheel spokes"}


def ferris_gondola(p):
    """観覧車のゴンドラ。原点は箱 (2.4×2.0×2.0) の中心で、吊り棒は上1.75の軸まで。"""
    mb = bkit.MeshBuilder()
    acc = p["accent"]
    mb.box((2.2, 0.85, 1.8), (0, -0.55, 0), acc, bevel=0.28)
    mb.box((2.26, 0.12, 1.86), (0, -0.1, 0), p["yellow"], bevel=0.05, segments=1)
    mb.box((2.06, 0.62, 1.66), (0, 0.27, 0), p["glass"], bevel=0.12)
    for x in (-0.98, 0.98):
        for z in (-0.78, 0.78):
            mb.box((0.14, 0.66, 0.14), (x, 0.27, z), p["white"])
    mb.box((2.36, 0.34, 1.96), (0, 0.72, 0), p["white"], bevel=0.16)
    mb.lathe([(0.95, 0.86), (0.62, 1.04), (0.0, 1.12)], mat=acc, segments=12)
    mb.cylinder(0.07, 0.72, (0, 1.4, 0), p["dark"], segments=6)
    mb.cylinder(0.1, 2.7, (0, 1.75, 0), p["dark"], axis="z", segments=6)
    return mb, {"ParkAccent": "Gondola body", "ParkYellow": "Gondola trim", "ParkGlass": "Gondola windows",
                "ParkWhite": "Gondola roof", "ParkDark": "Gondola hanger"}


def ferris_hub(p):
    """観覧車の軸。赤い胴、前後の星の飾り、A字の脚の先まで届く車軸。原点は軸の中心。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(1.55, 2.4, mat=p["red"], axis="z", segments=20)
    for side in (-1, 1):
        mb.cylinder(1.9, 0.22, (0, 0, side * 1.3), p["yellow"], axis="z", segments=20)
        mb.prism(star(1.4), 0.16, (0, 0, side * 1.48), p["yellow"], axis="z")
        mb.sphere(0.42, (0, 0, side * 1.6), p["white"], segments=10)
    mb.cylinder(0.32, 4.6, mat=p["dark"], axis="z", segments=10)
    return mb, {"ParkRed": "Ferris drum", "ParkYellow": "Ferris star", "ParkWhite": "Ferris cap", "ParkDark": "Ferris axle"}


def ferris_deck(p):
    """観覧車の乗り場。原点は台 (7×0.9×4.2) の中心。両端に階段、奥に手すり。"""
    mb = bkit.MeshBuilder()
    mb.box((7.0, 0.9, 4.2), (0, 0, 0), p["blue"], bevel=0.06, segments=1)
    mb.box((7.2, 0.12, 4.4), (0, 0.5, 0), p["white"], bevel=0.03, segments=1)
    for side in (-1, 1):
        for k in range(3):
            h = 0.9 - (k + 1) * 0.3
            mb.box((0.45, h, 2.0), (side * (3.72 + k * 0.45), -0.45 + h / 2, 0), p["white"])
    for x in (-3.3, -1.1, 1.1, 3.3):
        mb.cylinder(0.06, 1.0, (x, 1.05, 2.0), p["dark"], segments=6)
    mb.cylinder(0.07, 6.8, (0, 1.55, 2.0), p["yellow"], axis="x", segments=6)
    return mb, {"ParkBlue": "Deck base", "ParkWhite": "Deck floor and steps", "ParkDark": "Deck posts", "ParkYellow": "Deck rail"}


# ---------------------------------------------------------------- ジェットコースター

def coaster_track(p):
    """線路の1区間 (長さ3、Z方向が進行方向、Yが線路の上)。2本のレール、背骨の管、枕木。"""
    mb = bkit.MeshBuilder()
    length = 3.0
    for x in (-0.6, 0.6):
        mb.cylinder(0.12, length, (x, 0.12, 0), p["tint"], axis="z", segments=8)
    mb.cylinder(0.22, length, (0, -0.3, 0), p["tint"], axis="z", segments=8)
    for z in (-0.75, 0.75):
        mb.box((1.42, 0.1, 0.16), (0, 0.02, z), p["white"])
        for x in (-0.55, 0.55):
            mb.tube([(x, 0.0, z), (0, -0.3, z)], 0.05, mat=p["white"], sides=4)
    return mb, {"ParkTint": "Coaster rails", "ParkWhite": "Coaster ties"}


def coaster_car(p, front):
    """コースターの車両 (前がZ+)。原点は箱 (1.7×1.1×2.6) の中心。先頭車は顔と前照灯付き。"""
    mb = bkit.MeshBuilder()
    acc = p["accent"]
    mb.box((1.62, 0.55, 2.2), (0, -0.12, -0.05), acc, bevel=0.18)
    mb.box((1.66, 0.1, 2.0), (0, 0.02, -0.05), p["yellow"], bevel=0.03, segments=1)
    mb.box((1.3, 0.2, 1.7), (0, 0.18, -0.08), p["dark"], bevel=0.06, segments=1)
    for z in (0.15, -0.75):
        mb.box((1.36, 0.72, 0.18), (0, 0.42, z - 0.2), acc, bevel=0.07)
        for x in (-0.33, 0.33):
            bar = [(x - 0.2, 0.3, z + 0.18), (x - 0.2, 0.66, z), (x + 0.2, 0.66, z), (x + 0.2, 0.3, z + 0.18)]
            mb.tube(bar, 0.04, mat=p["yellow"], sides=5)
    mb.box((1.2, 0.25, 1.7), (0, -0.48, -0.05), p["dark"])
    for x in (-0.62, 0.62):
        for z in (-0.6, 0.5):
            mb.cylinder(0.15, 0.12, (x, -0.5, z), p["dark"], axis="x", segments=8)
    if front:
        mb.box((1.5, 0.52, 0.7), (0, -0.08, 1.1), acc, bevel=0.24)
        for x in (-0.46, 0.46):
            mb.sphere(0.13, (x, -0.02, 1.43), p["lamp"], segments=8)
        with mb.at((0, 0.2, 1.18), pitch=-60):
            mb.prism(star(0.32), 0.05, mat=p["yellow"], axis="z")
    return mb, {"ParkAccent": "Coaster car body", "ParkYellow": "Coaster car trim", "ParkDark": "Coaster car seats and wheels",
                "ParkLamp": "Coaster headlights"}


def coaster_station(p):
    """大型コースターの駅。原点は台 (20×1×5) の中心で、線路は -Z 側を通る。しま模様の屋根と星の看板。"""
    mb = bkit.MeshBuilder()
    mb.box((20.0, 1.0, 5.0), (0, 0, 0), p["blue"], bevel=0.06, segments=1)
    mb.box((20.2, 0.14, 5.2), (0, 0.55, 0), p["white"], bevel=0.03, segments=1)
    for x in (-9.2, -4.6, 0.0, 4.6, 9.2):
        for z in (-2.1, 2.1):
            mb.cylinder(0.14, 4.4, (x, 2.8, z), p["white"], segments=8)
    for k in range(10):
        x0 = -10.5 + k * 2.1
        grid = [[(x0, 5.3, -3.4), (x0 + 2.1, 5.3, -3.4)], [(x0, 6.2, 0.0), (x0 + 2.1, 6.2, 0.0)], [(x0, 5.3, 3.0), (x0 + 2.1, 5.3, 3.0)]]
        mb.shell(grid, 0.14, mat=p["tint"] if k % 2 == 0 else p["white"], smooth=False)
    mb.prism(scallops(21.0, 5.25, 4.9, 10, 0.35), 0.06, (0, 0, -3.42), p["yellow"], axis="z")
    mb.prism(rounded_rect(6.0, 1.6, 0.4), 0.25, (0, 7.2, -0.2), p["white"], axis="z")
    mb.prism(star(0.7), 0.1, (0, 7.2, -0.38), p["paint"], axis="z")
    for x in (-2.0, 2.0):
        mb.prism(star(0.4), 0.1, (x, 7.2, -0.38), p["yellow"], axis="z")
    return mb, {"ParkBlue": "Station platform", "ParkWhite": "Station posts and stripes", "ParkTint": "Station roof stripes",
                "ParkYellow": "Station valance", "ParkPaint": "Station star"}


# ---------------------------------------------------------------- メリーゴーラウンド

def canopy_height(r):
    return 8.3 - 3.3 * (r / 9.6) ** 1.3


def carousel_canopy(p):
    """30度分の屋根 (12枚で一周)。半分ずつ赤と白のしま、金の垂れ幕と電球。原点はゲーム側の屋根板の中心。"""
    mb = bkit.MeshBuilder()
    rows = [0.7 + (9.6 - 0.7) * k / 7 for k in range(8)]

    def wedge(a0, a1):
        cols = [a0 + (a1 - a0) * k / 3 for k in range(4)]
        return [[(math.sin(math.radians(a)) * r, canopy_height(r), math.cos(math.radians(a)) * r) for a in cols] for r in rows]

    mb.shell(wedge(-15, 0), 0.12, mat=p["tint"])
    mb.shell(wedge(0, 15), 0.12, mat=p["white"])
    chord = 2 * 9.6 * math.sin(math.radians(15))
    depth = 9.6 * math.cos(math.radians(15))
    mb.prism(scallops(chord, 5.0, 4.55, 3, 0.3), 0.08, (0, 0, depth), p["yellow"], axis="z")
    for k in range(3):
        mb.sphere(0.15, (-chord / 2 + chord * (k + 0.5) / 3, 4.2, depth + 0.06), p["lamp"], segments=6)
    for x in (-chord / 2, chord / 2):
        mb.sphere(0.17, (x, 5.05, depth + 0.06), p["lamp"], segments=6)
    roles = {"ParkTint": "Canopy stripe", "ParkWhite": "Canopy white stripe", "ParkYellow": "Canopy valance", "ParkLamp": "Canopy bulbs"}
    return mb, roles, (0, 6.2, CAROUSEL_RADIUS / 2)


def carousel_horse(p):
    """メリーゴーラウンドの馬 (前がZ+)。原点は胴の中心で、金の柱は床から屋根の下まで。"""
    mb = bkit.MeshBuilder()
    body = p["white"]
    mb.sphere(0.5, (0, 0, 0), body, segments=14, scale=(0.62, 0.74, 1.5))
    mb.tube([(0, 0.12, 0.5), (0, 0.45, 0.72), (0, 0.72, 0.84)], 0.19, mat=body, sides=8, radius_at=lambda i, t: 1 - 0.25 * t)
    with mb.at((0, 0.78, 1.02), pitch=38):
        mb.sphere(0.5, (0, 0, 0), body, segments=10, scale=(0.36, 0.38, 0.78))
    for x in (-0.09, 0.09):
        mb.cylinder(0.05, 0.2, (x, 1.0, 0.8), body, segments=5, radius_end=0.01)
    for x in (-0.17, 0.17):
        mb.tube([(x, -0.15, 0.48), (x, -0.42, 0.75), (x, -0.62, 0.6)], 0.075, mat=body, sides=6)
        mb.tube([(x, -0.18, -0.5), (x, -0.62, -0.62), (x, -1.0, -0.52)], 0.075, mat=body, sides=6)
        mb.sphere(0.09, (x, -0.66, 0.58), p["dark"], segments=6)
        mb.sphere(0.09, (x, -1.04, -0.5), p["dark"], segments=6)
    mb.tube([(0, 0.12, -0.72), (0, 0.0, -0.98), (0, -0.4, -1.08)], 0.11, mat=p["yellow"], sides=6, radius_at=lambda i, t: 1 - 0.5 * t)
    mb.tube([(0, 0.38, 0.48), (0, 0.74, 0.7), (0, 0.98, 0.9)], 0.085, mat=p["yellow"], sides=6)
    mb.box((0.68, 0.12, 0.52), (0, 0.37, -0.06), p["accent"], bevel=0.05)
    mb.box((0.74, 0.34, 0.6), (0, 0.18, -0.06), p["accent"], bevel=0.08)
    mb.cylinder(0.07, 5.8, (0, 1.7, 0), p["yellow"], segments=8)
    return mb, {"ParkWhite": "Horse body", "ParkYellow": "Horse mane and pole", "ParkAccent": "Horse saddle", "ParkDark": "Horse hooves"}


def carousel_column(p):
    """中心の柱。鏡の板と金の輪、屋根の頂の飾りと旗。原点はゲーム側の柱 (1.5×7.2×1.5、中心の高さ3.85)。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.72, 6.9, (0, 3.95, 0), p["red"], segments=16)
    for k in range(8):
        with mb.at(yaw=k * 45 + 22.5):
            mb.box((0.5, 2.6, 0.06), (0, 3.6, 0.72), p["glass"])
    for y in (0.75, 2.1, 5.1, 7.3):
        mb.cylinder(0.84, 0.22, (0, y, 0), p["yellow"], segments=16)
    mb.cylinder(0.75, 0.4, (0, 8.25, 0), p["yellow"], segments=12)
    mb.sphere(0.45, (0, 8.8, 0), p["yellow"], segments=10)
    mb.cylinder(0.05, 1.6, (0, 9.8, 0), p["dark"], segments=5)
    mb.prism([(0, 0), (1.1, 0.3), (0, 0.6)], 0.04, (0.02, 10.0, 0), p["paint"], axis="z")
    return mb, {"ParkRed": "Carousel column", "ParkGlass": "Carousel mirrors", "ParkYellow": "Carousel gold rings",
                "ParkDark": "Carousel spire", "ParkPaint": "Carousel flag"}, (0, 3.85, 0)


# ---------------------------------------------------------------- 落下タワー

def drop_tower_crown(p):
    """落下タワーの頂部。八角形の帯、四方の星の看板、電球、尖った屋根。原点は箱 (6×2.5×6) の中心。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(3.0, 2.2, mat=p["blue"], segments=8)
    mb.cylinder(3.15, 0.25, (0, 1.15, 0), p["yellow"], segments=8)
    mb.cylinder(3.15, 0.25, (0, -1.15, 0), p["yellow"], segments=8)
    mb.cylinder(3.0, 2.6, (0, 2.55, 0), p["white"], segments=8, radius_end=0.25)
    mb.cylinder(0.1, 2.2, (0, 4.9, 0), p["dark"], segments=5)
    mb.prism(star(0.55), 0.12, (0, 6.1, 0), p["yellow"], axis="z")
    for k in range(4):
        with mb.at(yaw=k * 90 + 22.5):
            mb.prism(star(1.0), 0.2, (0, 0.05, 2.95), p["yellow"], axis="z")
    for k in range(16):
        a = math.radians(k * 22.5)
        for y in (-0.85, 0.95):
            mb.sphere(0.14, (math.sin(a) * 3.05, y, math.cos(a) * 3.05), p["lamp"], segments=6)
    return mb, {"ParkBlue": "Crown band", "ParkYellow": "Crown stars", "ParkWhite": "Crown roof", "ParkDark": "Crown spire",
                "ParkLamp": "Crown bulbs"}


def drop_tower_ring(p):
    """落下タワーの座席の輪。柱を囲むドーナツ形と、外向きの12席。原点は箱 (7.4×0.7×7.4) の中心。"""
    mb = bkit.MeshBuilder()
    mb.tube(circle(2.6, 0, 24), 0.42, mat=p["yellow"], sides=8, closed=True)
    for k in range(12):
        with mb.at(yaw=k * 30):
            mb.box((0.72, 0.12, 0.6), (0, 0.0, 3.25), p["accent"], bevel=0.04, segments=1)
            mb.box((0.72, 1.0, 0.14), (0, 0.5, 2.98), p["accent"], bevel=0.05)
            mb.tube([(-0.28, 1.0, 3.0), (-0.28, 0.85, 3.4), (-0.2, 0.25, 3.45)], 0.05, mat=p["dark"], sides=5)
            mb.tube([(0.28, 1.0, 3.0), (0.28, 0.85, 3.4), (0.2, 0.25, 3.45)], 0.05, mat=p["dark"], sides=5)
    return mb, {"ParkYellow": "Drop ring", "ParkAccent": "Drop seats", "ParkDark": "Drop harnesses"}


# ---------------------------------------------------------------- 海賊船・コーヒーカップ

def pirate_ship(p):
    """揺れる海賊船の船体 (長さ11、X方向)。原点は箱 (11×2.8×3.8) の中心。腕は船の中央の上へ付く。"""
    mb = bkit.MeshBuilder()
    bottom = [(-5.6, 1.4), (-5.0, 0.4), (-4.0, -0.55), (-2.5, -1.1), (0, -1.3), (2.5, -1.1), (4.0, -0.55), (5.0, 0.4), (5.6, 1.4)]
    top = [(4.8, 1.0), (3.0, 0.6), (0, 0.5), (-3.0, 0.6), (-4.8, 1.0)]
    mb.prism(bottom + top, 3.4, mat=p["wood"], axis="z")
    edge = [(-5.5, 1.38), (-4.8, 1.02), (-3.0, 0.62), (0, 0.52), (3.0, 0.62), (4.8, 1.02), (5.5, 1.38)]
    for z in (-1.76, 1.76):
        mb.tube([(x, y, z) for x, y in edge], 0.11, mat=p["yellow"], sides=6)
        mb.tube([(x * 0.97, y - 0.55, z) for x, y in edge], 0.15, mat=p["red"], sides=6)
        mb.prism(star(0.85), 0.1, (0, -0.35, z), p["paint"], axis="z")
    for k in range(6):
        x = -3.75 + k * 1.5
        mb.box((0.5, 0.12, 3.0), (x, 0.72, 0), p["blue"], bevel=0.03, segments=1)
        mb.box((0.12, 0.55, 3.0), (x - 0.25, 1.0, 0), p["blue"], bevel=0.03, segments=1)
    for side in (-1, 1):
        mb.sphere(0.42, (side * 5.75, 1.75, 0), p["yellow"], segments=10)
        mb.cylinder(0.3 if side > 0 else 0.05, 0.7, (side * 6.05, 1.75, 0), p["yellow"], axis="x", segments=8,
                    radius_end=0.05 if side > 0 else 0.3)
        mb.cylinder(0.05, 2.0, (side * 5.3, 2.4, 0), p["dark"], segments=5)
        mb.prism([(0, 0), (-side * 1.1, 0.3), (0, 0.6)], 0.04, (side * 5.3, 2.8, 0), p["paint"], axis="z")
    return mb, {"ParkWood": "Ship hull", "ParkYellow": "Ship trim", "ParkRed": "Ship stripe", "ParkPaint": "Ship stars and flags",
                "ParkBlue": "Ship benches", "ParkDark": "Ship flagpoles"}


def teacup(p):
    """コーヒーカップ。原点は箱 (3.4×1.6×3.4) の中心。受け皿、金の縁、取っ手、水玉、中央のハンドル。"""
    mb = bkit.MeshBuilder()
    acc = p["accent"]
    cup = [(0.0, -0.55), (1.0, -0.55), (1.42, -0.15), (1.6, 0.45), (1.52, 0.52), (1.4, 0.0), (1.0, -0.35), (0.0, -0.35)]
    mb.lathe(cup, mat=acc, segments=20)
    mb.tube(circle(1.56, 0.52, 20), 0.07, mat=p["yellow"], sides=6, closed=True)
    mb.lathe([(0.0, -0.8), (1.85, -0.72), (1.95, -0.62), (1.0, -0.58), (0.0, -0.6)], mat=p["white"], segments=20)
    mb.tube([(1.5, 0.3, 0), (2.0, 0.25, 0), (2.05, -0.05, 0), (1.45, -0.22, 0)], 0.1, mat=acc, sides=6)
    for k in range(8):
        with mb.at((0, 0.12, 0), yaw=k * 45 + 22.5):
            mb.cylinder(0.17, 0.06, (0, 0, 1.52), p["white"], axis="z", segments=8)
    mb.cylinder(0.12, 0.6, (0, -0.1, 0), p["dark"], segments=6)
    mb.tube(circle(0.42, 0.22, 12), 0.05, mat=p["dark"], sides=5, closed=True)
    return mb, {"ParkAccent": "Teacup body", "ParkYellow": "Teacup rim", "ParkWhite": "Teacup saucer and dots", "ParkDark": "Teacup wheel"}


def teapot(p):
    """コーヒーカップの中心のティーポット。原点は箱 (3.2×4.4×3.2) の中心。"""
    mb = bkit.MeshBuilder()
    mb.lathe([(0.0, -2.2), (1.2, -2.2), (1.6, -1.4), (1.75, -0.5), (1.5, 0.4), (1.0, 0.8), (0.0, 0.8)], mat=p["pink"], segments=20)
    mb.lathe([(0.0, 0.8), (1.05, 0.8), (0.9, 1.2), (0.4, 1.45), (0.0, 1.5)], mat=p["white"], segments=16)
    mb.sphere(0.3, (0, 1.75, 0), p["yellow"], segments=8)
    mb.tube(circle(1.66, -1.4, 20), 0.08, mat=p["yellow"], sides=6, closed=True)
    mb.tube([(1.45, -0.8, 0), (2.2, -0.2, 0), (2.6, 0.6, 0), (2.9, 0.9, 0)], 0.3, mat=p["pink"], sides=8, radius_at=lambda i, t: 1 - 0.45 * t)
    mb.tube([(-1.5, 0.2, 0), (-2.3, 0.1, 0), (-2.4, -0.8, 0), (-1.6, -1.2, 0)], 0.17, mat=p["yellow"], sides=6)
    return mb, {"ParkPink": "Teapot body", "ParkWhite": "Teapot lid", "ParkYellow": "Teapot gold"}


# ---------------------------------------------------------------- サーカステント

def tent_roof_height(r):
    return TENT_EAVE + (TENT_APEX - TENT_EAVE) * (1 - r / TENT_RADIUS) ** 1.15


TENT_PITCH = math.degrees(math.atan2(TENT_APEX - TENT_EAVE, TENT_RADIUS))
TENT_SLAB = (0.0, (TENT_APEX + TENT_EAVE) / 2, TENT_RADIUS / 2)


def tent_slab(mb):
    """テントの屋根板の当たり判定 (傾けた板) の向きで作るための座標変換。"""
    return mb.at(pitch=-TENT_PITCH)


def tent_roof_wedge(p):
    """テントの両端の円すいの屋根の30度分。半分ずつ2色のしまと、縁の金の垂れ幕。"""
    mb = bkit.MeshBuilder()
    rows = [0.35 + (TENT_RADIUS - 0.35) * k / 8 for k in range(9)]

    def wedge(a0, a1):
        cols = [a0 + (a1 - a0) * k / 3 for k in range(4)]
        return [[(math.sin(math.radians(a)) * r, tent_roof_height(r), math.cos(math.radians(a)) * r) for a in cols] for r in rows]

    chord = 2 * TENT_RADIUS * math.sin(math.radians(15))
    depth = TENT_RADIUS * math.cos(math.radians(15))
    with tent_slab(mb), mb.at(tuple(-v for v in TENT_SLAB)):
        mb.shell(wedge(-15, 0), 0.14, mat=p["tint"])
        mb.shell(wedge(0, 15), 0.14, mat=p["white"])
        mb.prism(scallops(chord, TENT_EAVE + 0.05, TENT_EAVE - 0.45, 3, 0.35), 0.08, (0, 0, depth), p["yellow"], axis="z")
    return mb, {"ParkTint": "Tent stripe", "ParkWhite": "Tent white stripe", "ParkYellow": "Tent valance"}


def tent_roof_side(p):
    """2本の柱の間の屋根の1/4 (長さ13)。棟から軒へ下る2色のしまと金の垂れ幕。"""
    mb = bkit.MeshBuilder()
    rows = [TENT_RADIUS * k / 8 for k in range(9)]
    with tent_slab(mb), mb.at(tuple(-v for v in TENT_SLAB)):
        for k in range(5):
            x0 = -6.5 + k * 2.6
            grid = [[(x0, tent_roof_height(z), z), (x0 + 2.6, tent_roof_height(z), z)] for z in rows]
            mb.shell(grid, 0.14, mat=p["tint"] if k % 2 == 0 else p["white"])
        mb.prism(scallops(13.0, TENT_EAVE + 0.05, TENT_EAVE - 0.45, 5, 0.35), 0.08, (0, 0, TENT_RADIUS), p["yellow"], axis="z")
    return mb, {"ParkTint": "Tent stripe", "ParkWhite": "Tent white stripe", "ParkYellow": "Tent valance"}


def tent_wall_arc(p):
    """テントの両端の丸い壁の30度分。原点は壁の板の中心 (半径の弦の位置、高さ3.25)。"""
    mb = bkit.MeshBuilder()
    width = 2 * TENT_RADIUS * math.sin(math.radians(7.5))
    for k, yaw in enumerate((-7.5, 7.5)):
        with mb.at(yaw=yaw):
            mb.box((width + 0.04, TENT_EAVE, 0.2), (0, TENT_EAVE / 2, TENT_RADIUS * math.cos(math.radians(7.5))),
                   p["tint"] if k == 0 else p["white"])
            mb.box((width + 0.08, 0.35, 0.28), (0, TENT_EAVE - 0.3, TENT_RADIUS * math.cos(math.radians(7.5))), p["yellow"])
    return mb, {"ParkTint": "Tent wall stripe", "ParkWhite": "Tent wall white stripe", "ParkYellow": "Tent wall band"}, \
        (0, TENT_EAVE / 2, TENT_RADIUS * math.cos(math.radians(15)))


def tent_wall(p):
    """2本の柱の間のまっすぐな壁 (長さ13)。原点は壁の中心。"""
    mb = bkit.MeshBuilder()
    for k in range(5):
        mb.box((2.62, TENT_EAVE, 0.2), (-5.2 + k * 2.6, 0, 0), p["tint"] if k % 2 == 0 else p["white"])
    mb.box((13.1, 0.35, 0.28), (0, TENT_EAVE / 2 - 0.3, 0), p["yellow"])
    return mb, {"ParkTint": "Tent wall stripe", "ParkWhite": "Tent wall white stripe", "ParkYellow": "Tent wall band"}


def tent_entrance(p):
    """テントの入口 (前がZ+)。原点は箱 (5.6×6×2) の中心。カーテンと星の看板。"""
    mb = bkit.MeshBuilder()
    mb.box((3.8, 4.6, 0.1), (0, -0.7, -0.6), p["dark"])
    for x in (-2.45, 2.45):
        mb.cylinder(0.22, 5.6, (x, -0.2, 0.2), p["white"], segments=10)
        mb.sphere(0.32, (x, 2.75, 0.2), p["yellow"], segments=8)
        side = 1 if x > 0 else -1
        mb.prism([(side * 1.9, 1.9), (side * 0.9, 1.9), (side * 1.6, -0.6), (side * 2.2, -3.0), (side * 2.4, -3.0), (side * 2.4, 1.9)],
                 0.12, (0, 0, 0.0), p["red"], axis="z")
    arch = [(-2.7, 1.8), (2.7, 1.8), (2.7, 2.3)] + [(2.6 * math.cos(math.radians(a)), 2.3 + 0.9 * math.sin(math.radians(a))) for a in range(10, 171, 20)] + [(-2.7, 2.3)]
    mb.prism(arch, 0.3, (0, 0, 0.2), p["yellow"], axis="z")
    mb.prism(star(0.8), 0.14, (0, 2.6, 0.42), p["paint"], axis="z")
    return mb, {"ParkDark": "Tent doorway", "ParkWhite": "Tent door posts", "ParkYellow": "Tent arch", "ParkRed": "Tent curtains",
                "ParkPaint": "Tent star"}


def tent_pole_top(p):
    """テントの柱の頂の玉と旗。原点は柱の上端。"""
    mb = bkit.MeshBuilder()
    mb.sphere(0.5, (0, 0.35, 0), p["yellow"], segments=10)
    mb.cylinder(0.06, 2.6, (0, 2.0, 0), p["dark"], segments=5)
    mb.prism([(0, 0), (2.0, 0.45), (0, 0.9)], 0.05, (0.04, 2.3, 0), p["paint"], axis="z")
    return mb, {"ParkYellow": "Tent pole ball", "ParkDark": "Tent flagpole", "ParkPaint": "Tent flag"}


# ---------------------------------------------------------------- 入口ゲート・売店

def gate_tower(p):
    """入口ゲートの塔。原点は柱の上端 (地面から8.8)。下へ8.8の塔、王冠の帯、玉ねぎ形の屋根と旗。"""
    mb = bkit.MeshBuilder()
    mb.box((2.5, 8.8, 2.5), (0, -4.4, 0), p["tint"], bevel=0.12)
    for z in (-1.27, 1.27):
        mb.prism([(0.5, -0.9)] + [(0.5 * math.cos(math.radians(a)), 0.5 * math.sin(math.radians(a))) for a in range(0, 181, 30)] + [(-0.5, -0.9)],
                 0.06, (0, -2.6, z), p["glass"], axis="z")
        mb.prism(star(0.55), 0.06, (0, -5.4, z * 1.02), p["yellow"], axis="z")
    mb.box((3.0, 0.5, 3.0), (0, 0.1, 0), p["yellow"], bevel=0.08)
    mb.lathe([(1.25, 0.35), (1.55, 0.9), (1.25, 1.7), (0.55, 2.3), (0.0, 2.85)], mat=p["tint"], segments=12)
    mb.cylinder(0.05, 1.8, (0, 3.5, 0), p["dark"], segments=5)
    mb.prism([(0, 0), (1.4, 0.35), (0, 0.7)], 0.04, (0.03, 3.8, 0), p["paint"], axis="z")
    return mb, {"ParkTint": "Gate tower", "ParkGlass": "Gate windows", "ParkYellow": "Gate crown", "ParkDark": "Gate flagpole",
                "ParkPaint": "Gate flag"}


def gate_star(p):
    """入口のアーチの頂の大きな星と電球。原点は星の中心。"""
    mb = bkit.MeshBuilder()
    outline = star(2.0)
    mb.prism(outline, 0.35, mat=p["yellow"], axis="z")
    mb.prism(star(1.45), 0.08, (0, 0, -0.2), p["white"], axis="z")
    for x, y in outline:
        mb.sphere(0.16, (x * 0.93, y * 0.93, -0.22), p["lamp"], segments=6)
    return mb, {"ParkYellow": "Gate star", "ParkWhite": "Gate star face", "ParkLamp": "Gate star bulbs"}


def booth_awning(p):
    """売店の窓口の日よけ (標準の幅6、ゲーム側で横に伸ばす)。原点は付け根の中央 (壁の外面)、前は -Z。"""
    mb = bkit.MeshBuilder()
    for k in range(6):
        x0 = -3.0 + k
        grid = [[(x0, 0.0, 0.0), (x0 + 1.0, 0.0, 0.0)], [(x0, -0.3, -0.6), (x0 + 1.0, -0.3, -0.6)], [(x0, -0.75, -1.05), (x0 + 1.0, -0.75, -1.05)]]
        mb.shell(grid, 0.06, mat=p["paint"] if k % 2 == 0 else p["white"])
    mb.prism(scallops(6.0, -0.72, -0.95, 6, 0.25), 0.05, (0, 0, -1.07), p["yellow"], axis="z")
    return mb, {"ParkPaint": "Awning stripes", "ParkWhite": "Awning white stripes", "ParkYellow": "Awning fringe"}


def ticket_sign(p):
    """チケット売り場の看板 (3.6×1.5のチケットの形)。原点は看板の中心、前は -Z。"""
    mb = bkit.MeshBuilder()
    outline = []
    w, h = 1.8, 0.75
    outline += [(-w, -h), (w, -h)]
    outline += [(w - 0.3 * math.cos(math.radians(a)), 0.3 * math.sin(math.radians(a))) for a in range(-90, 91, 30)]
    outline += [(w, h), (-w, h)]
    outline += [(-w + 0.3 * math.cos(math.radians(a)), 0.3 * math.sin(math.radians(a))) for a in range(90, -91, -30)]
    mb.prism(outline, 0.22, mat=p["paint"], axis="z")
    mb.prism(rounded_rect(2.6, 1.0, 0.12), 0.24, (0, 0, 0), p["white"], axis="z")
    mb.prism(star(0.42), 0.26, (0, 0, 0), p["yellow"], axis="z")
    for x in (-0.9, 0.9):
        mb.prism(star(0.2), 0.26, (x, 0, 0), p["paint"], axis="z")
    return mb, {"ParkPaint": "Ticket sign", "ParkWhite": "Ticket face", "ParkYellow": "Ticket star"}


def burger_sign(p):
    """軽食売り場の看板のハンバーガー。原点は中心、前は -Z。"""
    mb = bkit.MeshBuilder()
    mb.sphere(1.0, (0, 0.38, 0), p["yellow"], segments=14, scale=(1.3, 0.62, 0.42))
    for k in range(7):
        mb.sphere(0.07, (-0.75 + k * 0.25, 0.62 + 0.12 * math.sin(k), -0.36), p["white"], segments=5)
    mb.prism([(x * 0.14 - 1.4, 0.06 + 0.07 * math.sin(x * 1.3)) for x in range(21)] + [(1.4, -0.08), (-1.4, -0.08)], 0.7, (0, 0, 0), p["leaf"], axis="z")
    mb.prism([(-1.3, 0.02), (1.3, 0.02), (1.3, -0.12), (0.7, -0.12), (0.55, -0.35), (0.4, -0.12), (-0.5, -0.12), (-0.65, -0.3), (-0.8, -0.12), (-1.3, -0.12)],
             0.72, (0, -0.02, 0), p["white"], axis="z")
    mb.box((2.5, 0.36, 0.64), (0, -0.33, 0), p["wood"], bevel=0.14)
    mb.box((2.5, 0.34, 0.6), (0, -0.7, 0), p["yellow"], bevel=0.15)
    return mb, {"ParkYellow": "Burger buns", "ParkWhite": "Burger cheese and seeds", "ParkLeaf": "Burger lettuce", "ParkWood": "Burger patty"}


# ---------------------------------------------------------------- 小物

def balloon_lamp(p):
    """風船を付けた街灯。原点は地面。従来の柱 (0.5×5.6×0.5) と同じ高さ。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.36, 0.4, (0, 0.2, 0), p["dark"], segments=10)
    mb.cylinder(0.12, 5.0, (0, 2.9, 0), p["dark"], segments=8, radius_end=0.08)
    mb.cylinder(0.18, 0.2, (0, 1.2, 0), p["yellow"], segments=10)
    mb.lathe([(0.12, 5.35), (0.32, 5.55), (0.34, 5.95), (0.0, 6.0)], mat=p["lamp"], segments=8)
    mb.cylinder(0.42, 0.25, (0, 6.05, 0), p["dark"], segments=8, radius_end=0.08)
    mb.cylinder(0.05, 1.3, (0.6, 4.4, 0), p["dark"], axis="x", segments=5)
    mb.prism(scallops(1.6, 0.0, -2.1, 2, 0.25), 0.06, (1.05, 4.35, 0), p["pink"], axis="z")
    for k, (x, y, mat) in enumerate(((-0.95, 6.9, "yellow"), (0.1, 7.5, "blue"), (1.05, 6.95, "pink"))):
        mb.sphere(0.85, (x, y, 0.15 * (k - 1)), p[mat], segments=12, scale=(1, 1.15, 1))
        mb.tube([(x * 0.85, y - 0.95, 0.12 * (k - 1)), (x * 0.3, 5.6, 0), (0, 5.2, 0)], 0.02, mat=p["dark"], sides=3)
    return mb, {"ParkDark": "Lamp post", "ParkYellow": "Balloon yellow and trim", "ParkLamp": "Lamp glow", "ParkPink": "Balloon pink and banner",
                "ParkBlue": "Balloon blue"}


def planter_tree(p):
    """四角い植木鉢の丸い木。原点は鉢 (3×1×3) の中心。"""
    mb = bkit.MeshBuilder()
    mb.box((3.0, 1.0, 3.0), (0, 0, 0), p["lime"], bevel=0.12)
    mb.box((3.2, 0.2, 3.2), (0, 0.5, 0), p["white"], bevel=0.06, segments=1)
    mb.cylinder(0.24, 2.4, (0, 1.6, 0), p["wood"], segments=8, radius_end=0.17)
    mb.sphere(1.9, (0, 3.5, 0), p["leaf"], segments=14)
    mb.sphere(1.25, (0, 5.25, 0), p["leaf"], segments=12)
    return mb, {"ParkLime": "Planter", "ParkWhite": "Planter rim", "ParkWood": "Tree trunk", "ParkLeaf": "Round park foliage"}


def park_fence(p):
    """低い柵の1区間 (長さ5.6)。原点は上の横木の中心 (地面から1.1)。"""
    mb = bkit.MeshBuilder()
    for x in (-2.8, -0.93, 0.93, 2.8):
        mb.cylinder(0.08, 1.15, (x, -0.52, 0), p["dark"], segments=6)
        mb.sphere(0.13, (x, 0.12, 0), p["yellow"], segments=8)
    mb.cylinder(0.07, 5.6, (0, 0, 0), p["dark"], axis="x", segments=6)
    mb.cylinder(0.06, 5.6, (0, -0.55, 0), p["dark"], axis="x", segments=6)
    for k in range(12):
        x = -2.55 + k * 0.465
        if min(abs(x - c) for c in (-2.8, -0.93, 0.93, 2.8)) < 0.15:
            continue
        mb.cylinder(0.035, 0.6, (x, -0.28, 0), p["dark"], segments=4)
    return mb, {"ParkDark": "Fence rails", "ParkYellow": "Fence finials"}


PIECES = [
    ("Tube", steel_tube), ("Truss 3", lambda p: truss(p, 3)), ("Truss 6", lambda p: truss(p, 6)), ("Truss 12", lambda p: truss(p, 12)),
    ("Wheel rim", wheel_rim), ("Wheel spoke", twin_spoke), ("Ferris gondola", ferris_gondola), ("Ferris hub", ferris_hub),
    ("Ferris deck", ferris_deck),
    ("Coaster track", coaster_track), ("Coaster car front", lambda p: coaster_car(p, True)), ("Coaster car", lambda p: coaster_car(p, False)),
    ("Coaster station", coaster_station),
    ("Carousel canopy", carousel_canopy), ("Carousel horse", carousel_horse), ("Carousel column", carousel_column),
    ("Drop tower crown", drop_tower_crown), ("Drop tower ring", drop_tower_ring),
    ("Pirate ship", pirate_ship), ("Teacup", teacup), ("Teapot", teapot),
    ("Tent roof wedge", tent_roof_wedge), ("Tent roof side", tent_roof_side), ("Tent wall arc", tent_wall_arc), ("Tent wall", tent_wall),
    ("Tent entrance", tent_entrance), ("Tent pole top", tent_pole_top),
    ("Gate tower", gate_tower), ("Gate star", gate_star), ("Booth awning", booth_awning), ("Ticket sign", ticket_sign),
    ("Burger sign", burger_sign),
    ("Balloon lamp", balloon_lamp), ("Planter tree", planter_tree), ("Park fence", park_fence),
]


def build_kit():
    p = palette()
    root = bkit.empty("Amusement kit")
    groups = {}
    for name, builder in PIECES:
        result = builder(p)
        mb, roles = result[0], result[1]
        origin = result[2] if len(result) > 2 else (0, 0, 0)
        groups[name] = mb.build_split(name, origin=origin, parent=root, roles=roles)
    return root, groups


def move(group, position, scale=(1, 1, 1), yaw=0.0, pitch=0.0):
    """部品の空オブジェクトを、親から見たUnity座標の位置へ動かす (確認用)。"""
    group.location = bkit.to_fbx(position)
    group.scale = scale
    # FBX座標 (X反転) では、UnityのY回転は逆向き、X回転はそのまま。XYZ順でUnityと同じ X→Y の順に回る
    group.rotation_euler = (math.radians(pitch), -math.radians(yaw), 0)


def duplicate(group):
    copy = group.copy()
    bkit.bpy.context.scene.collection.objects.link(copy)
    copy.parent = group.parent
    for child in group.children:
        part = child.copy()
        bkit.bpy.context.scene.collection.objects.link(part)
        part.parent = copy
    return copy


def preview_only(path, groups, **kwargs):
    """groups の部品だけを描画する (ほかは一時的に隠す)。"""
    shown = {o for group in groups for o in group.children}
    hidden = [o for o in bkit.bpy.context.scene.objects if o.type == "MESH" and o not in shown]
    for o in hidden:
        o.hide_render = True
    bkit.render_preview(path, focus=list(shown), **kwargs)
    for o in hidden:
        o.hide_render = False


def assemble_carousel(groups, at):
    """確認用に、ゲームと同じ並べ方でメリーゴーラウンドを組む。"""
    x0, z0 = at
    parts = []
    for k in range(12):
        a = k * 30
        group = groups["Carousel canopy"] if k == 0 else duplicate(groups["Carousel canopy"])
        r = CAROUSEL_RADIUS / 2
        move(group, (x0 + math.sin(math.radians(a)) * r, 6.2, z0 + math.cos(math.radians(a)) * r), yaw=a)
        parts.append(group)
    for k in range(6):
        a = k * 60 + 15
        group = groups["Carousel horse"] if k == 0 else duplicate(groups["Carousel horse"])
        r = CAROUSEL_RADIUS * 0.62
        move(group, (x0 + math.sin(math.radians(a)) * r, 1.75 + 0.25 * (k % 2), z0 + math.cos(math.radians(a)) * r), yaw=a + 90)
        parts.append(group)
    move(groups["Carousel column"], (x0, 3.85, z0))
    return parts + [groups["Carousel column"]]


def assemble_tent(groups, at):
    x0, z0 = at
    parts = []
    first = {"Tent roof wedge": True, "Tent roof side": True, "Tent wall arc": True, "Tent wall": True}

    def take(name):
        if first[name]:
            first[name] = False
            return groups[name]
        return duplicate(groups[name])

    c = TENT_SLAB
    for side in (-1, 1):
        for k in range(6):
            a = 15 + k * 30 if side > 0 else 195 + k * 30
            g = take("Tent roof wedge")
            d = (math.sin(math.radians(a)), math.cos(math.radians(a)))
            move(g, (x0 + side * 13 + d[0] * c[2], c[1], z0 + d[1] * c[2]), yaw=a, pitch=TENT_PITCH)
            parts.append(g)
            g = take("Tent wall arc")
            r = TENT_RADIUS * math.cos(math.radians(15))
            move(g, (x0 + side * 13 + d[0] * r, TENT_EAVE / 2, z0 + d[1] * r), yaw=a)
            parts.append(g)
        for zs in (-1, 1):
            g = take("Tent roof side")
            move(g, (x0 + side * 6.5, c[1], z0 + zs * c[2]), yaw=0 if zs > 0 else 180, pitch=TENT_PITCH)
            parts.append(g)
            g = take("Tent wall")
            move(g, (x0 + side * 6.5, TENT_EAVE / 2, z0 + zs * TENT_RADIUS), yaw=0 if zs > 0 else 180)
            parts.append(g)
        g = groups["Tent pole top"] if side < 0 else duplicate(groups["Tent pole top"])
        move(g, (x0 + side * 13, 17.0, z0))
        parts.append(g)
    move(groups["Tent entrance"], (x0, 3.0, z0 - TENT_RADIUS - 1.0), yaw=180)
    return parts + [groups["Tent entrance"]]


def layout_preview(groups):
    """書き出し後に、確認用に部品を並べる (書き出したFBXには影響しない)。"""
    rides = [
        ("Ferris gondola", (0, 2, 0)), ("Ferris hub", (5, 2, 0)), ("Ferris deck", (12, 0.45, 0)),
        ("Coaster car front", (-6, 0.6, 2.0)), ("Coaster car", (-6, 0.6, -0.8)), ("Coaster track", (-6, 0.0, 0.0)),
        ("Wheel rim", (0, 6, -4)), ("Truss 6", (4, 4, -4)), ("Teacup", (-10, 0.8, -6)), ("Teapot", (-15, 2.2, -6)),
        ("Pirate ship", (6, 1.5, -10)), ("Drop tower ring", (-6, 0.5, -12)), ("Drop tower crown", (-14, 1.3, -14)),
    ]
    for name, at in rides:
        move(groups[name], at)
    move(groups["Coaster track"], (-6, 0, 0.6), scale=(1, 1, 1))
    move(groups["Wheel rim"], (0, 6, -4), scale=(1, 1, 1))
    move(groups["Truss 6"], (4, 4, -4), scale=(1.0, 6.0, 1.0))
    props = [("Balloon lamp", (0, 0, 0)), ("Planter tree", (5, 0.5, 0)), ("Park fence", (10, 1.1, 0)), ("Gate tower", (-6, 8.8, 0)),
             ("Gate star", (-11, 3, 0)), ("Booth awning", (16, 3, 0)), ("Ticket sign", (16, 5, 0)), ("Burger sign", (21, 4, 0)),
             ("Coaster station", (6, 0.5, 12))]
    for name, at in props:
        move(groups[name], (at[0] + 40, at[1], at[2]))


if __name__ == "__main__":
    args = bkit.script_args()
    bkit.reset_scene()
    root, groups = build_kit()
    out = args["out"]
    bkit.export_fbx(os.path.join(out, "AmusementKit.fbx"), root)
    if args.get("assets"):
        repo = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
        bkit.export_fbx(os.path.join(repo, ASSET_PATH), root)
    bkit.save_blend(os.path.join(out, "AmusementKit.blend"))
    layout_preview(groups)
    ride_names = ["Ferris gondola", "Ferris hub", "Ferris deck", "Coaster car front", "Coaster car", "Coaster track", "Wheel rim",
                  "Truss 6", "Teacup", "Teapot", "Pirate ship", "Drop tower ring", "Drop tower crown"]
    preview_only(os.path.join(out, "AmusementRides.png"), [groups[n] for n in ride_names], direction=(0.55, 0.5, -1.0),
                 distance=1.7, lens=35, resolution=(1600, 1000), floor_color=(1.0, 0.94, 0.8))
    prop_names = ["Balloon lamp", "Planter tree", "Park fence", "Gate tower", "Gate star", "Booth awning", "Ticket sign", "Burger sign",
                  "Coaster station"]
    preview_only(os.path.join(out, "AmusementProps.png"), [groups[n] for n in prop_names], direction=(0.4, 0.45, -1.0),
                 distance=1.6, lens=35, resolution=(1600, 900), floor_color=(1.0, 0.94, 0.8))
    carousel = assemble_carousel(groups, (-60, 0))
    preview_only(os.path.join(out, "AmusementCarousel.png"), carousel, direction=(0.5, 0.45, -1.0), distance=1.8, lens=35,
                 resolution=(1400, 1000), floor_color=(1.0, 0.94, 0.8))
    tent = assemble_tent(groups, (-120, 0))
    preview_only(os.path.join(out, "AmusementTent.png"), tent, direction=(0.45, 0.4, -1.0), distance=1.6, lens=35,
                 resolution=(1600, 900), floor_color=(1.0, 0.94, 0.8))
