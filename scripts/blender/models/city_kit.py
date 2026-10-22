"""ビル群の街 (通常の街・大きな街) の部品一式。ゲームは TownModelKit で読み込む。

部品は空オブジェクト "<部品名>" の下の "<部品名>|<オブジェクト名>" (1マテリアルずつ) で、
メートル単位・Unity座標で作る。基準点 (原点) は部品ごとに決めてあり、ゲーム側と合わせている。

- 窓・店先: 窓ガラスの中心、壁の外面 (Z+が外)。標準の窓ガラスは高層2.0×1.6・集合住宅2.9×1.6・商店2.5×1.6
- バルコニー: 外壁パネルの下端中央、壁の外面。標準の外壁パネル幅9.12
- 屋上の飾り: 屋上部材 (高層の頂部4.32×3×4.32・屋上設備6×1.2×6・切妻屋根の板17.9×0.3×9.5) の中心
- 街路・公園の小物: 各小物のルートの位置 (多くは地面、箱・ベンチ・燃料は中心)
- 木の葉: 縦横奥行き1に収めた形 (ゲーム側で 3.25×3.05×3.25 に拡大)

    pwsh -File scripts/blender/Invoke-Blender.ps1 -Script scripts/blender/models/city_kit.py -ScriptArguments --assets
"""

import math
import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

ASSET_PATH = os.path.join("Assets", "TankTown", "Resources", "Models", "CityKit.fbx")


def palette():
    """マテリアル名はゲーム側の対応表 (TownModelKit) のキー。色はプレビュー用の目安。"""
    m = bkit.material
    return {
        "glass": m("CityGlass", (0.04, 0.34, 0.59), metallic=0.2, roughness=0.15),
        "frame": m("CityFrame", (0.85, 0.91, 1.0)),
        "cream": m("CityCream", (1.0, 0.94, 0.77)),
        "dark": m("CityDark", (0.055, 0.09, 0.16), metallic=0.4, roughness=0.45),
        "paint": m("CityPaint", (0.1, 0.75, 1.0)),
        "lamp": m("CityLamp", (1.0, 0.85, 0.54), emission=2.0),
        "yellow": m("CityYellow", (1.0, 0.83, 0.07)),
        "wood": m("CityWood", (0.3, 0.19, 0.105), roughness=0.8),
        "leaf": m("CityLeaf", (0.12, 0.84, 0.42), roughness=0.8),
        "green": m("CityGreen", (0.12, 0.84, 0.42), roughness=0.8),
        "red": m("CityRed", (1.0, 0.11, 0.025)),
        "white": m("CityWhite", (0.97, 0.99, 1.0)),
        "rock": m("CityRock", (0.76, 0.57, 0.35), roughness=0.8),
        "lime": m("CityLime", (0.64, 1.0, 0.04)),
        "sand": m("CitySand", (1.0, 0.81, 0.45), roughness=0.9),
        "island": m("CityIsland", (0.15, 0.73, 0.48), roughness=0.8),
        "cloud": m("CityCloud", (0.97, 0.99, 1.0), roughness=0.9),
    }


# ---------------------------------------------------------------- 建物の窓・外壁

def window_frame(mb, p, width, height, mullions=(), transom=None, sill=True, head=True, depth=0.2):
    """窓ガラスの周りの額縁・方立て・無目・水切り・庇。原点は窓ガラス中心の壁面。"""
    f = p["frame"]
    border = 0.12
    mb.box((width, height, 0.04), (0, 0, 0.03), p["glass"])
    for side in (-1, 1):
        mb.box((border, height + 2 * border, depth), (side * (width + border) / 2, 0, depth / 2), f)
        mb.box((width, border, depth), (0, side * (height + border) / 2, depth / 2), f)
    for x in mullions:
        mb.box((0.07, height, 0.12), (x, 0, 0.08), f)
    if transom is not None:
        mb.box((width, 0.07, 0.12), (0, transom, 0.08), f)
    if sill:
        mb.box((width + 0.4, 0.1, 0.32), (0, -height / 2 - border - 0.03, 0.16), f, bevel=0.025, segments=1)
    if head:
        mb.box((width + 0.36, 0.08, 0.26), (0, height / 2 + border + 0.04, 0.13), f, bevel=0.02, segments=1)


def tower_window(p):
    mb = bkit.MeshBuilder()
    # 細かく分けると遠目にうるさいので、どの窓も縦の方立て1本 (2枚) まで
    window_frame(mb, p, 2.0, 1.6, mullions=(0.0,), sill=False)
    return mb, {"CityGlass": "Window", "CityFrame": "Deep window frame"}


def apartment_window(p):
    mb = bkit.MeshBuilder()
    window_frame(mb, p, 2.9, 1.6, mullions=(0.0,))
    # 両側の雨戸 (クリーム) と、その上下の小さな金具
    for side in (-1, 1):
        x = side * (2.9 / 2 + 0.12 + 0.3)
        mb.box((0.5, 1.74, 0.07), (x, 0, 0.05), p["cream"], bevel=0.02, segments=1)
        for y in (-0.28, 0.28):
            mb.box((0.42, 0.05, 0.03), (x, y, 0.1), p["frame"])
    return mb, {"CityGlass": "Window", "CityFrame": "Deep window frame", "CityCream": "Window shutters"}


def shop_window(p):
    mb = bkit.MeshBuilder()
    window_frame(mb, p, 2.5, 1.6)
    # 窓辺の花台と植え込み
    mb.box((2.6, 0.3, 0.34), (0, -1.0, 0.32), p["dark"], bevel=0.03, segments=1)
    for i in range(6):
        x = -1.05 + i * 0.42
        mb.sphere(0.22, (x, -0.8, 0.32 + (i % 2) * 0.04), p["green"], segments=8)
    return mb, {"CityGlass": "Window", "CityFrame": "Deep window frame", "CityDark": "Window planter",
                "CityGreen": "Window greenery"}


def shop_storefront(p):
    """商店1階の大きなショーウインドウと、しま模様の日よけ。原点は標準の窓ガラスの中心。"""
    mb = bkit.MeshBuilder()
    width, top, bottom = 2.9, 1.05, -1.2
    height = top - bottom
    middle = (top + bottom) / 2
    mb.box((width, height, 0.04), (0, middle, 0.03), p["glass"])
    f = p["frame"]
    for side in (-1, 1):
        mb.box((0.14, height + 0.14, 0.2), (side * (width + 0.14) / 2, middle + 0.07, 0.1), f)
    mb.box((width + 0.28, 0.14, 0.2), (0, top + 0.07, 0.1), f)
    mb.box((width + 0.28, 0.32, 0.16), (0, bottom - 0.16, 0.08), p["dark"], bevel=0.02, segments=1)
    # 日よけ: 横から見て前下がりの板と前垂れ。色はクリームと建物の色の交互
    root, depth = top + 0.55, 0.95
    outline = [(0.0, root), (depth, root - 0.38), (depth, root - 0.62), (depth - 0.06, root - 0.62),
               (depth - 0.06, root - 0.44), (0.0, root - 0.08)]
    stripes = 6
    stripe = (width + 0.4) / stripes
    for i in range(stripes):
        x = -(width + 0.4) / 2 + stripe * (i + 0.5)
        mb.prism(outline, stripe, (x, 0, 0), p["paint"] if i % 2 == 0 else p["cream"])
    return mb, {"CityGlass": "Window", "CityFrame": "Deep window frame", "CityDark": "Storefront base",
                "CityPaint": "Awning stripes", "CityCream": "Awning light stripes"}


def apartment_balcony(p):
    """集合住宅のバルコニー。原点は外壁パネルの下端中央の壁面。

    手すり壁は建物の色にして、正面から見たときも報酬の色が読めるようにする。
    """
    mb = bkit.MeshBuilder()
    width, depth = 7.4, 1.05
    mb.box((width, 0.18, depth), (0, 0.09, depth / 2), p["frame"], bevel=0.03, segments=1)
    mb.box((width, 0.88, 0.08), (0, 0.18 + 0.44, depth - 0.04), p["paint"])
    for side in (-1, 1):
        mb.box((0.08, 0.88, depth - 0.08), (side * (width / 2 - 0.04), 0.62, (depth - 0.08) / 2), p["paint"])
    mb.box((width + 0.1, 0.08, 0.16), (0, 1.1, depth - 0.04), p["cream"], bevel=0.02, segments=1)
    for side in (-1, 1):
        mb.box((0.1, 0.08, depth), (side * (width / 2 + 0.0), 1.1, depth / 2), p["cream"])
    return mb, {"CityFrame": "Balcony slab", "CityPaint": "Balcony parapet", "CityCream": "Balcony rail"}


# ---------------------------------------------------------------- 屋上

def tower_crown_trim(p):
    """高層ビルの頂部 (4.32×3×4.32、色は建物の色) に付けるガラス帯・笠木・尖塔・灯。"""
    mb = bkit.MeshBuilder()
    half = 4.32 / 2
    for yaw in (0, 90, 180, 270):
        with mb.at(yaw=yaw):
            mb.box((4.32 - 0.7, 0.75, 0.05), (0, 0.45, half + 0.02), p["glass"])
            mb.box((4.32 - 0.5, 0.08, 0.12), (0, 0.04, half + 0.05), p["frame"])
    mb.box((4.32 + 0.4, 0.24, 4.32 + 0.4), (0, 1.62, 0), p["frame"], bevel=0.05, segments=1)
    mb.box((2.2, 0.3, 2.2), (0, 1.89, 0), p["frame"], bevel=0.04, segments=1)
    mb.cylinder(0.34, 0.5, (0, 2.29, 0), p["dark"], segments=12)
    mb.cylinder(0.1, 2.9, (0, 3.99, 0), p["frame"], segments=8)
    mb.sphere(0.24, (0, 5.5, 0), p["lamp"], segments=10)
    return mb, {"CityGlass": "Crown glass", "CityFrame": "Crown cap", "CityDark": "Crown mast", "CityLamp": "Crown beacon"}


def roof_utility_top(p):
    """屋上設備の台 (6×1.2×6、黒) の上に載せる室外機と給水タンク。"""
    mb = bkit.MeshBuilder()
    top = 0.6
    for i, x in enumerate((-1.6, 0.0)):
        mb.box((1.3, 0.75, 1.0), (x, top + 0.375, 1.6), p["frame"], bevel=0.04, segments=1)
        mb.cylinder(0.33, 0.04, (x, top + 0.77, 1.6), p["dark"], segments=14)
        mb.box((0.12, 0.6, 0.12), (x + 0.5, top + 0.3, 1.0), p["dark"])
    mb.cylinder(0.07, 3.4, (-0.8, top + 0.16, 0.95), p["dark"], axis="x", segments=8)
    tank = (1.5, -1.4)
    for sx in (-1, 1):
        for sz in (-1, 1):
            mb.box((0.12, 1.1, 0.12), (tank[0] + sx * 0.55, top + 0.55, tank[1] + sz * 0.55), p["dark"])
    mb.box((1.5, 0.1, 1.5), (tank[0], top + 1.15, tank[1]), p["dark"])
    mb.cylinder(0.82, 1.25, (tank[0], top + 1.82, tank[1]), p["frame"], segments=20)
    mb.cylinder(0.86, 0.4, (tank[0], top + 2.64, tank[1]), p["cream"], segments=20, radius_end=0.15)
    return mb, {"CityFrame": "Roof machines", "CityDark": "Roof pipes", "CityCream": "Water tank roof"}


def shop_roof_tiles(p):
    """切妻屋根の板 (17.9×0.3×9.5) の上面に並べる瓦の筋。"""
    mb = bkit.MeshBuilder()
    for k in range(9):
        z = -4.2 + k * 1.05
        mb.box((17.9, 0.07, 0.16), (0, 0.185, z), p["frame"])
    mb.box((18.1, 0.12, 0.3), (0, 0.15, 4.75), p["cream"], bevel=0.03, segments=1)
    return mb, {"CityFrame": "Roof tile ridges", "CityCream": "Roof eave"}


# ---------------------------------------------------------------- 街路・公園の小物

def street_lamp(p):
    """街灯。原点は地面。従来の柱 (高さ2.8) と灯 (高さ2.7、0.9角) の当たり判定に収まる。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.24, 0.3, (0, 0.15, 0), p["dark"], segments=12)
    mb.cylinder(0.085, 2.3, (0, 1.4, 0), p["dark"], segments=10, radius_end=0.065)
    mb.cylinder(0.13, 0.1, (0, 0.85, 0), p["cream"], segments=12)
    mb.cylinder(0.12, 0.14, (0, 2.5, 0), p["dark"], segments=12)
    mb.cylinder(0.2, 0.06, (0, 2.6, 0), p["cream"], segments=14)
    mb.sphere(0.27, (0, 2.85, 0), p["lamp"], segments=12)
    mb.cylinder(0.34, 0.2, (0, 3.13, 0), p["cream"], segments=14, radius_end=0.06)
    return mb, {"CityDark": "Post", "CityCream": "Lamp trim", "CityLamp": "Top"}


def rounded_rect(width, height, radius, steps=4):
    points = []
    for cx, cy, start in ((width / 2 - radius, height / 2 - radius, 0), (-width / 2 + radius, height / 2 - radius, 90),
                          (-width / 2 + radius, -height / 2 + radius, 180), (width / 2 - radius, -height / 2 + radius, 270)):
        for i in range(steps + 1):
            a = math.radians(start + 90 * i / steps)
            points.append((cx + radius * math.cos(a), cy + radius * math.sin(a)))
    return points


def street_sign(p):
    """道路標識。原点は地面。看板は高さ2.7、1.8×0.9。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.07, 2.3, (0, 1.15, 0), p["dark"], segments=10)
    mb.cylinder(0.09, 0.08, (0, 2.3, 0), p["dark"], segments=10)
    mb.prism(rounded_rect(1.8, 0.9, 0.16), 0.08, (0, 2.7, 0), p["yellow"], axis="z")
    mb.prism(rounded_rect(1.66, 0.76, 0.1), 0.02, (0, 2.7, 0.05), p["cream"], axis="z")
    mb.prism(rounded_rect(1.52, 0.62, 0.07), 0.02, (0, 2.7, 0.065), p["yellow"], axis="z")
    # 右向きの矢印
    arrow = [(-0.5, -0.07), (0.15, -0.07), (0.15, -0.2), (0.5, 0.0), (0.15, 0.2), (0.15, 0.07), (-0.5, 0.07)]
    mb.prism(arrow, 0.02, (0, 2.7, 0.08), p["dark"], axis="z")
    for y in (2.45, 2.95):
        mb.box((0.24, 0.08, 0.1), (0, y, -0.06), p["dark"])
    return mb, {"CityDark": "Post", "CityYellow": "Sign board", "CityCream": "Sign border"}


def tree_trunk(p):
    """木の幹と枝。原点は地面。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.26, 3.3, (0, 1.65, 0), p["wood"], segments=10, radius_end=0.15)
    for yaw, pitch, length, height in ((20, 50, 1.2, 1.9), (150, 45, 1.0, 2.1), (260, 55, 1.1, 1.7)):
        with mb.at((0, height, 0), yaw=yaw, pitch=-pitch):
            mb.cylinder(0.11, length, (0, 0, length / 2), p["wood"], axis="z", segments=8, radius_end=0.06)
    return mb, {"CityWood": "Tree trunk and branches"}


def tree_canopy(p, variant):
    """木の葉 (縦横奥行き1に正規化)。0は丸い広葉樹、1は円すいの針葉樹、2は細長い木。"""
    mb = bkit.MeshBuilder()
    leaf = p["leaf"]
    if variant == 0:
        mb.sphere(0.34, (0, 0.1, 0), leaf, segments=12)
        for i in range(6):
            a = math.radians(i * 60 + 15)
            mb.sphere(0.25 + (i % 2) * 0.03, (math.cos(a) * 0.24, -0.05 + (i % 3) * 0.05, math.sin(a) * 0.24), leaf, segments=10)
    elif variant == 1:
        for k, (radius, y) in enumerate(((0.5, -0.32), (0.4, -0.05), (0.28, 0.2))):
            mb.cylinder(radius, 0.42, (0, y, 0), leaf, segments=10, radius_end=0.04, yaw=k * 17)
    else:
        for k, (r, y, x) in enumerate(((0.3, -0.25, 0.03), (0.32, 0.0, -0.04), (0.27, 0.22, 0.02), (0.2, 0.38, 0.0))):
            mb.sphere(r, (x, y, -x), leaf, segments=10)
    return mb, {"CityLeaf": "Tree crown"}


def crate(p):
    """押しつぶせる木箱 (1.2角)。原点は箱の中心。"""
    mb = bkit.MeshBuilder()
    mb.box((1.1, 1.1, 1.1), (0, 0, 0), p["lime"])
    e = 0.12
    for a in (-1, 1):
        for b in (-1, 1):
            mb.box((1.2, e, e), (0, a * (0.6 - e / 2), b * (0.6 - e / 2)), p["cream"], bevel=0.02, segments=1)
            mb.box((e, 1.2 - 2 * e, e), (a * (0.6 - e / 2), 0, b * (0.6 - e / 2)), p["cream"], bevel=0.02, segments=1)
            mb.box((e, e, 1.2 - 2 * e), (a * (0.6 - e / 2), b * (0.6 - e / 2), 0), p["cream"], bevel=0.02, segments=1)
    # 前後左右の面の斜め板
    for yaw in (0, 90, 180, 270):
        with mb.at(yaw=yaw):
            mb.box((1.25, 0.1, 0.05), (0, 0, 0.565), p["cream"], roll=45)
    for a in (-1, 1):
        for b in (-1, 1):
            for c in (-1, 1):
                mb.box((0.16, 0.16, 0.16), (a * 0.58, b * 0.58, c * 0.58), p["dark"])
    return mb, {"CityLime": "Crate body", "CityCream": "Crate frame", "CityDark": "Crate corners"}


def bench(p):
    """公園のベンチ (2.6×0.9×0.8)。原点は箱の中心で、背もたれは +Z 側。"""
    mb = bkit.MeshBuilder()
    seat = -0.02
    for k in range(3):
        mb.box((2.4, 0.06, 0.14), (0, seat, -0.22 + k * 0.17), p["yellow"], bevel=0.02, segments=1)
    for k, y in enumerate((0.2, 0.36)):
        mb.box((2.4, 0.12, 0.05), (0, y, 0.3 + k * 0.03), p["yellow"], bevel=0.015, segments=1, pitch=-12)
    for side in (-1, 1):
        x = side * 1.12
        mb.box((0.08, 0.43, 0.08), (x, -0.235, -0.24), p["dark"])
        mb.box((0.08, 0.85, 0.08), (x, -0.025, 0.28), p["dark"], pitch=-8)
        mb.box((0.08, 0.06, 0.62), (x, 0.17, 0.0), p["dark"])
        mb.box((0.08, 0.06, 0.6), (x, seat - 0.06, 0.02), p["dark"])
    return mb, {"CityYellow": "Bench slats", "CityDark": "Bench frame"}


def fountain(p):
    """広場の噴水。原点は地面の中心。当たり判定は直径4・高さ1の円柱。"""
    mb = bkit.MeshBuilder()
    stone, water = p["cream"], p["glass"]
    mb.cylinder(2.0, 0.55, (0, 0.275, 0), stone, segments=32)
    mb.cylinder(1.86, 0.04, (0, 0.6, 0), water, segments=32)
    # 水面を囲む縁は、輪に並べた石で作る (円柱の上面と水面が重ならないように)
    count = 28
    for i in range(count):
        with mb.at(yaw=i * 360 / count):
            mb.box((2 * math.pi * 2.0 / count + 0.02, 0.3, 0.26), (0, 0.65, 1.93), stone)
    mb.cylinder(0.42, 1.05, (0, 1.12, 0), stone, segments=16)
    mb.cylinder(0.3, 0.32, (0, 1.72, 0), stone, segments=20, radius_end=0.95)
    mb.cylinder(0.84, 0.04, (0, 1.9, 0), water, segments=24)
    mb.cylinder(0.09, 0.5, (0, 2.1, 0), water, segments=10)
    mb.sphere(0.16, (0, 2.4, 0), water, segments=10)
    return mb, {"CityCream": "Fountain stone", "CityGlass": "Fountain water"}


def red_barrel(p):
    """爆発するドラム缶。原点は中心 (当たり判定1.3×1.9×1.3)。最初の部品を本体にする。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.62, 1.84, (0, 0, 0), p["red"], segments=20)
    for y in (-0.92, 0.92):
        mb.cylinder(0.65, 0.07, (0, y, 0), p["dark"], segments=20)
    for y in (-0.32, 0.32):
        mb.cylinder(0.635, 0.05, (0, y, 0), p["dark"], segments=20)
    for y in (-0.6, 0.6):
        mb.cylinder(0.632, 0.2, (0, y, 0), p["yellow"], segments=20)
    mb.cylinder(0.08, 0.06, (0.3, 0.95, 0.1), p["dark"], segments=8)
    mb.box((0.42, 0.42, 0.04), (0, 0, -0.63), p["cream"], roll=45)
    mb.box((0.07, 0.2, 0.02), (0, 0.04, -0.655), p["dark"])
    mb.box((0.07, 0.06, 0.02), (0, -0.12, -0.655), p["dark"])
    return mb, {"CityRed": "Barrel body", "CityDark": "Barrel rims", "CityYellow": "Warning band", "CityCream": "Warning mark"}


def fuel_tank(p):
    """爆発する燃料タンク (横置き)。原点は中心 (当たり判定2.2×2.2×2.8)。最初の部品を本体にする。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(1.0, 2.2, (0, 0.05, 0), p["red"], axis="z", segments=24)
    for side in (-1, 1):
        mb.cylinder(1.0, 0.2, (0, 0.05, side * 1.2), p["red"], axis="z", segments=24, radius_end=0.62,
                    yaw=0 if side > 0 else 180)
    for z in (-0.8, 0.8):
        mb.box((1.7, 0.45, 0.3), (0, -0.87, z), p["dark"], bevel=0.03, segments=1)
    mb.cylinder(1.03, 0.26, (0, 0.05, 0), p["yellow"], axis="z", segments=24)
    mb.cylinder(0.16, 0.25, (0, 1.12, 0.6), p["dark"], segments=10)
    mb.cylinder(0.24, 0.06, (0, 1.25, 0.6), p["dark"], segments=12)
    mb.box((0.6, 0.6, 0.04), (0, 0.05, -1.33), p["cream"], roll=45)
    mb.box((0.09, 0.28, 0.02), (0, 0.1, -1.36), p["dark"])
    mb.box((0.09, 0.08, 0.02), (0, -0.12, -1.36), p["dark"])
    return mb, {"CityRed": "Tank body", "CityDark": "Tank fittings", "CityYellow": "Warning band", "CityCream": "Warning mark"}


def guardrail(p):
    """海岸のガードレール (長さ7.7)。原点はルート (地面から0.6)。"""
    mb = bkit.MeshBuilder()
    # 波形の板: 横から見た輪郭 (Z前, Y上) をX方向へ押し出す
    w = [(0.0, 0.43), (0.09, 0.36), (0.09, 0.3), (0.0, 0.25), (0.0, 0.19), (0.09, 0.14), (0.09, 0.08), (0.0, 0.01),
         (0.04, 0.01), (0.13, 0.08), (0.13, 0.14), (0.04, 0.19), (0.04, 0.25), (0.13, 0.3), (0.13, 0.36), (0.04, 0.43)]
    mb.prism([(z - 0.12, y) for z, y in w], 7.7, (0, 0.0, 0), p["white"], axis="x")
    for end in (-1, 1):
        x = end * 3.4
        mb.box((0.16, 1.1, 0.16), (x, -0.05, -0.12), p["rock"], bevel=0.02, segments=1)
        mb.box((0.18, 0.06, 0.18), (x, 0.52, -0.12), p["dark"])
        mb.box((0.12, 0.1, 0.03), (x, 0.22, 0.03), p["yellow"])
    return mb, {"CityWhite": "Coastal rail", "CityRock": "Rail post", "CityDark": "Rail caps", "CityYellow": "Rail reflector"}


def power_pole(p):
    """電柱。原点は地面 (当たり判定0.45×6.4×0.45)。"""
    mb = bkit.MeshBuilder()
    mb.cylinder(0.21, 6.4, (0, 3.2, 0), p["cream"], segments=12, radius_end=0.15)
    mb.box((2.8, 0.18, 0.18), (0, 5.7, 0), p["dark"], bevel=0.02, segments=1)
    for side in (-1, 1):
        mb.box((0.07, 0.9, 0.07), (side * 0.38, 5.32, 0), p["dark"], roll=side * 52)
    for i in (-1, 0, 1):
        x = i * 1.1
        for k in range(3):
            mb.cylinder(0.13 - k * 0.015, 0.1, (x, 5.86 + k * 0.12, 0), p["glass"], segments=10)
    mb.cylinder(0.3, 0.7, (0, 4.5, 0.42), p["dark"], segments=14)
    mb.box((0.12, 0.5, 0.2), (0, 4.5, 0.18), p["dark"])
    for k in range(5):
        mb.box((0.3, 0.04, 0.04), (0, 1.8 + k * 0.45, 0), p["dark"], yaw=90 * (k % 2))
    return mb, {"CityCream": "Power pole post", "CityDark": "Power cross arm", "CityGlass": "Insulator"}


# ---------------------------------------------------------------- 道路

def lane_dash(p):
    """車線の破線1本 (0.32×2.1、両端が丸い)。原点は破線の中心の路面。上向きの面だけ。"""
    mb = bkit.MeshBuilder()
    mb.decal(rounded_rect(0.32, 2.1, 0.15, steps=3), mat=p["cream"])
    return mb, {"CityCream": "Lane dash"}


def crosswalk(p):
    """横断歩道。原点は帯の中心の路面で、道路はZ方向に延びる (幅16)。上向きの面だけ。"""
    mb = bkit.MeshBuilder()
    for k in range(11):
        x = -7.0 + k * 1.4
        mb.decal(rounded_rect(0.55, 2.6, 0.06, steps=1), (x, 0, 0), p["cream"])
    return mb, {"CityCream": "Crosswalk stripes"}


def manhole(p):
    """マンホールのふた。原点は中心の路面。上向きの面だけ。"""
    mb = bkit.MeshBuilder()
    ring = [(math.cos(math.radians(a)) * 0.55, math.sin(math.radians(a)) * 0.55) for a in range(0, 360, 20)]
    mb.decal(ring, mat=p["frame"])
    inner = [(math.cos(math.radians(a)) * 0.46, math.sin(math.radians(a)) * 0.46) for a in range(0, 360, 20)]
    mb.decal(inner, (0, 0.004, 0), p["dark"])
    for k in range(-2, 3):
        mb.decal(rounded_rect(0.07, 0.62 - abs(k) * 0.12, 0.02, steps=1), (k * 0.16, 0.008, 0), p["frame"])
    return mb, {"CityFrame": "Manhole rim", "CityDark": "Manhole cover"}


def traffic_signal(p):
    """信号機。原点は柱の根元。従来どおり、灯は (-1.4, 3.6, 0) の箱 (前面 -Z) と (0, 3.6, -1.4) の箱 (前面 -X) に
    ゲームが球を3つずつ置くので、灯の位置 (高さ 4.12 / 3.60 / 3.08) を空けてひさしだけ付ける。"""
    mb = bkit.MeshBuilder()
    dark, cream = p["dark"], p["cream"]
    mb.cylinder(0.3, 0.25, (0, 0.125, 0), dark, segments=14)
    mb.cylinder(0.16, 4.15, (0, 2.32, 0), dark, segments=12, radius_end=0.13)
    mb.sphere(0.15, (0, 4.4, 0), dark, segments=10)
    # 腕はどちらもローカルの -X 方向。1つ目はそのまま (灯は -Z 向き)、2つ目は -90度回して腕を -Z 方向にする。
    # 回すと -Z 向きの灯は +X 向きになってしまうので、2つ目はローカルで +Z 向き (回した後に -X 向き) に作る
    for yaw, face in ((0, -1), (-90, 1)):
        with mb.at(yaw=yaw):
            mb.cylinder(0.075, 1.2, (-0.68, 3.95, 0), cream, axis="x", segments=10)
            mb.cylinder(0.04, 1.1, (-0.5, 3.6, 0), cream, axis="x", segments=8, roll=-38)
            mb.box((0.62, 1.68, 0.5), (-1.4, 3.6, 0.03 * face), dark, bevel=0.06, segments=1)
            mb.box((0.96, 2.0, 0.06), (-1.4, 3.6, -0.25 * face), p["yellow"], bevel=0.03, segments=1)
            for i in range(3):
                y = 4.12 - i * 0.52
                mb.box((0.46, 0.04, 0.34), (-1.4, y + 0.23, 0.44 * face), dark, pitch=12 * face)
                for side in (-1, 1):
                    mb.box((0.04, 0.24, 0.3), (-1.4 + side * 0.22, y + 0.11, 0.43 * face), dark)
    return mb, {"CityDark": "Signal post and housings", "CityCream": "Signal arms", "CityYellow": "Signal backplates"}


# ---------------------------------------------------------------- 雲・遠景

def cloud(p, variant):
    """もこもこした雲。原点は雲の中心 (高さ48〜64に置く)。下ほど青みがかった陰。"""
    mb = bkit.MeshBuilder()
    c = p["cloud"]
    lobes = {
        0: [((-12, 0.5, 0), 6.0), ((-5, 2.5, 1), 8.0), ((3, 3.5, -1), 9.0), ((11, 1.5, 1), 6.5), ((1, 1.5, 4), 6.0), ((-2, 1, -4), 5.5)],
        1: [((-16, 0, 0), 4.5), ((-9, 1.5, 1), 6.0), ((-1, 2, 0), 6.5), ((7, 1.5, -1), 5.5), ((14, 0.5, 0), 4.5)],
        2: [((-6, 1, 0), 6.0), ((0, 5, 0), 7.5), ((6, 2, 1), 6.0), ((1, 1.5, 4), 5.0), ((0, 9, -1), 5.0)],
    }[variant]
    for center, radius in lobes:
        mb.sphere(radius, center, c, segments=14)
    width = {0: 16, 1: 19, 2: 11}[variant]
    mb.sphere(1, (0, -1.6, 0), c, segments=16, scale=(width, 3.2, 6.5))
    return mb, {"CityCloud": "Cloud puffs"}


def cloud_shade(x, y, z):
    # 下から見上げると光の当たらない面になるので、頂点色の陰は控えめにする
    v = 0.9 + min(1.0, max(0.0, (y + 5) / 13)) * 0.12
    return (v * 0.94, v * 0.97, min(1.0, v))


def island(p, variant):
    """遠くの島。原点は海面の中心。0は丘と林、1は岩山と灯台、2は小さな村。"""
    mb = bkit.MeshBuilder()
    sand, green, rock, leaf = p["sand"], p["island"], p["rock"], p["leaf"]
    radius = {0: 34, 1: 26, 2: 30}[variant]
    mb.cylinder(radius, 3.0, (0, -0.5, 0), sand, segments=28, radius_end=radius - 4)
    rng = [0.37, 0.81, 0.13, 0.59, 0.94, 0.25, 0.68, 0.46]

    def tree(x, z, height):
        mb.cylinder(0.35, height * 0.4, (x, 1 + height * 0.2, z), p["wood"], segments=6)
        mb.sphere(height * 0.32, (x, 1 + height * 0.62, z), leaf, segments=8, scale=(1, 1.2, 1))

    if variant == 0:
        for (x, z), size in (((-8, 4), (20, 12, 16)), ((10, -6), (16, 9, 14)), ((2, 13), (12, 7, 10))):
            mb.sphere(1, (x, 0.5, z), green, segments=16, scale=size)
        for k in range(8):
            a = math.radians(k * 45 + 10)
            tree(math.cos(a) * (radius - 9), math.sin(a) * (radius - 9), 5 + rng[k] * 3)
    elif variant == 1:
        for (x, y, z), size in (((0, 1, 0), (16, 13, 14)), ((-9, 0, 6), (9, 8, 8)), ((8, 0, -7), (8, 6, 7))):
            mb.sphere(1, (x, y, z), rock, segments=10, scale=size)
        mb.sphere(1, (0, 9, 0), green, segments=14, scale=(12, 6, 11))
        # 灯台: 白と赤のしま、展望台、灯、赤い屋根
        base = 13.5
        mb.cylinder(2.2, 12, (0, base + 6, 0), p["white"], segments=16, radius_end=1.6)
        for k in range(3):
            y = base + 2 + k * 4
            r = 2.2 - (y - base) / 12 * 0.6 + 0.05
            mb.cylinder(r, 1.6, (0, y, 0), p["red"], segments=16, radius_end=r - 0.08)
        mb.cylinder(2.3, 0.4, (0, base + 12.2, 0), p["dark"], segments=16)
        mb.cylinder(1.1, 1.6, (0, base + 13.2, 0), p["lamp"], segments=12)
        mb.cylinder(1.5, 1.4, (0, base + 14.7, 0), p["red"], segments=12, radius_end=0.15)
    else:
        mb.sphere(1, (0, 0.8, 0), green, segments=16, scale=(22, 6, 20))
        for k, (x, z) in enumerate(((-8, -5), (-2, -9), (5, -6), (-6, 4), (4, 3))):
            h = 3 + rng[k] * 1.5
            mb.box((4, h, 3.5), (x, 4 + h / 2, z), p["cream"], bevel=0.2, segments=1)
            roof = [(-2.2, 0), (2.2, 0), (0, 1.8)]
            mb.prism(roof, 4.3, (x, 4 + h, z), p["red"], axis="z")
        for k in range(5):
            a = math.radians(k * 72 + 30)
            tree(math.cos(a) * (radius - 8), math.sin(a) * (radius - 8), 5 + rng[k] * 2)
        mb.box((3, 0.4, 12), (radius - 4, 0.8, 0), p["wood"])
    for k in range(5):
        a = math.radians(k * 73 + variant * 20)
        r = radius - 2 + rng[k] * 3
        mb.sphere(1, (math.cos(a) * r, 0.3, math.sin(a) * r), rock, segments=6, scale=(2.2, 1.6, 2.0))
    return mb, {"CitySand": "Island beach", "CityIsland": "Island hills", "CityRock": "Island rocks",
                "CityLeaf": "Island trees", "CityWood": "Island wood", "CityWhite": "Lighthouse",
                "CityRed": "Island red roofs", "CityDark": "Lighthouse gallery", "CityLamp": "Lighthouse lamp",
                "CityCream": "Island houses"}


def sailboat(p):
    """遠くの海の帆船 (長さ約8)。原点は海面の中心で、船首は +Z。"""
    mb = bkit.MeshBuilder()
    hull = [(-3.6, 0.9), (3.4, 0.9), (4.2, 1.5), (3.0, -0.4), (-3.2, -0.4), (-3.9, 1.3)]
    mb.prism(hull, 2.4, mat=p["white"], bevel=0.08, segments=1)
    mb.prism([(-3.65, 0.55), (3.6, 0.55), (3.85, 0.85), (-3.8, 0.85)], 2.46, mat=p["red"])
    mb.box((1.6, 0.8, 2.0), (0, 1.3, -1.0), p["white"], bevel=0.1, segments=1)
    mb.cylinder(0.1, 8.0, (0, 4.9, 0.4), p["dark"], segments=8)
    mb.prism([(0.6, 1.6), (0.6, 8.4), (-3.2, 1.8)], 0.06, mat=p["cream"])
    mb.prism([(0.9, 2.0), (0.9, 7.6), (3.6, 1.6)], 0.06, mat=p["cream"])
    return mb, {"CityWhite": "Boat hull", "CityRed": "Boat stripe", "CityDark": "Boat mast", "CityCream": "Boat sails"}


PIECES = [
    ("Tower window", tower_window), ("Apartment window", apartment_window), ("Shop window", shop_window),
    ("Shop storefront", shop_storefront), ("Apartment balcony", apartment_balcony),
    ("Tower crown trim", tower_crown_trim), ("Roof utility top", roof_utility_top), ("Shop roof tiles", shop_roof_tiles),
    ("Street lamp", street_lamp), ("Street sign", street_sign), ("Tree trunk", tree_trunk),
    ("Crate", crate), ("Bench", bench), ("Fountain", fountain), ("Red barrel", red_barrel), ("Fuel tank", fuel_tank),
    ("Guardrail", guardrail), ("Power pole", power_pole),
    ("Lane dash", lane_dash), ("Crosswalk", crosswalk), ("Manhole", manhole), ("Traffic signal", traffic_signal),
    ("Sailboat", sailboat),
]


def canopy_shade(x, y, z):
    # 従来の葉と同じく、下ほど暗い陰影 (正規化した座標で -0.5〜0.5)
    return 0.84 + min(1.0, max(0.0, y + 0.45)) * 0.24 + math.sin(x * 9 + z * 7) * 0.04


def build_kit():
    p = palette()
    root = bkit.empty("City kit")
    groups = {}
    for name, builder in PIECES:
        mb, roles = builder(p)
        groups[name] = mb.build_split(name, parent=root, roles=roles)
    for variant in range(3):
        mb, roles = tree_canopy(p, variant)
        name = f"Tree canopy {variant}"
        groups[name] = mb.build_split(name, parent=root, roles=roles, normalize=True, shade=canopy_shade)
        mb, roles = cloud(p, variant)
        groups[f"Cloud {variant}"] = mb.build_split(f"Cloud {variant}", parent=root, roles=roles, shade=cloud_shade)
        mb, roles = island(p, variant)
        groups[f"Island {variant}"] = mb.build_split(f"Island {variant}", parent=root, roles=roles)
    return root, groups


def move(group, position, scale=(1, 1, 1)):
    """部品の空オブジェクトを、親 (City kit) から見たUnity座標の位置へ動かす。"""
    group.location = bkit.to_fbx(position)
    group.scale = scale


def duplicate(group):
    copy = group.copy()
    bkit.bpy.context.scene.collection.objects.link(copy)
    copy.parent = group.parent
    for child in group.children:
        part = child.copy()
        bkit.bpy.context.scene.collection.objects.link(part)
        part.parent = copy
    return copy


def layout_preview(groups):
    """書き出し後に、確認用に部品を並べる (書き出したFBXには影響しない)。"""
    rows = [
        (["Tower window", "Apartment window", "Shop window", "Shop storefront"], 4.6, 0.0, 1.6),
        (["Apartment balcony", "Tower crown trim", "Roof utility top"], 8.5, -9.0, 0.6),
        (["Street lamp", "Street sign", "Crate", "Bench", "Fountain", "Red barrel", "Fuel tank", "Power pole"], 4.2, -19.0, 0.0),
    ]
    for names, spacing, z, lift in rows:
        for c, name in enumerate(names):
            move(groups[name], ((c - (len(names) - 1) / 2) * spacing, lift, z))
    for variant in range(3):
        x = (variant - 2) * 4.5
        move(groups[f"Tree canopy {variant}"], (x, 3.05, -28.0), (3.25, 3.05, 3.25))
        move(groups["Tree trunk"] if variant == 0 else duplicate(groups["Tree trunk"]), (x, 0, -28.0))
    move(groups["Guardrail"], (7.0, 0.6, -28.0))
    move(groups["Shop roof tiles"], (0, 0, -40.0), (0.5, 1, 0.5))
    move(groups["Traffic signal"], (14.0, 0, -19.0))
    move(groups["Crosswalk"], (0, 0.02, -48.0), (0.5, 1, 0.5))
    move(groups["Lane dash"], (5.0, 0.02, -48.0))
    move(groups["Manhole"], (-6.0, 0.02, -48.0))
    move(groups["Sailboat"], (14.0, 0, -36.0), (0.6, 0.6, 0.6))
    for variant in range(3):
        move(groups[f"Cloud {variant}"], ((variant - 1) * 14.0, 14.0, 6.0), (0.3, 0.3, 0.3))
        move(groups[f"Island {variant}"], ((variant - 1) * 22.0, 0, 28.0), (0.3, 0.3, 0.3))


if __name__ == "__main__":
    args = bkit.script_args()
    bkit.reset_scene()
    root, groups = build_kit()
    out = args["out"]
    bkit.export_fbx(os.path.join(out, "CityKit.fbx"), root)
    if args.get("assets"):
        repo = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
        bkit.export_fbx(os.path.join(repo, ASSET_PATH), root)
    bkit.save_blend(os.path.join(out, "CityKit.blend"))
    layout_preview(groups)
    bkit.render_preview(os.path.join(out, "CityKit.png"), direction=(0.35, 0.75, -1.0), distance=2.0, lens=35,
                        resolution=(1600, 1000), floor_color=(0.85, 0.82, 0.74))
