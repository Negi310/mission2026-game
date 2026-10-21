"""ビル群の街の建物の新モデル (高層ビル・集合住宅・商店)。

寸法は TankTownWorld.CreateBuilding / TownLayout に合わせている。
- 階高3.6、角の柱1.8、幅は高層14.4・集合住宅20・商店17.6
- 原点は建物の底面中心。正面 (入口) は -Z を向く
- 部材はゲームと同じ単位 (階ごとの柱・梁・床・外壁パネル) で別オブジェクトにしてあり、
  名前は "<部材名> L<階> S<面>" の形
"""

import math
import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
sys.path.insert(0, HERE)
import bkit  # noqa: E402

FLOOR = 3.6
PILLAR = 1.8
BAND = 0.45  # 各階の上の梁 (外から見える帯)
WALL = 0.28
SIDES = (180, 90, 0, 270)  # TankTownWorld と同じ面の順 (0が正面 -Z)

TOWER, APARTMENT, SHOP = 0, 1, 2


def palette():
    return {
        "frame": bkit.material("CityFrame", (0.85, 0.91, 1.0), roughness=0.6),
        "cream": bkit.material("CityCream", (1.0, 0.94, 0.77), roughness=0.6),
        "glass": bkit.material("CityGlass", (0.04, 0.34, 0.59), metallic=0.2, roughness=0.15),
        "door": bkit.material("CityDoorGlass", (0.03, 0.12, 0.22), metallic=0.2, roughness=0.15),
        "dark": bkit.material("CityDark", (0.055, 0.09, 0.16), metallic=0.4, roughness=0.45),
        "grass": bkit.material("CityPlanter", (0.12, 0.84, 0.42), roughness=0.8),
        "yellow": bkit.material("CityHandle", (1.0, 0.83, 0.07), roughness=0.3),
        "beacon": bkit.material("CityBeacon", (1.0, 0.15, 0.1), emission=3.0),
    }


def facade_paint(name, color):
    return bkit.material(f"Facade{name}", color, roughness=0.55)


def window_wall(mb, p, facade, width, height, count, window_width, sill, head, door=None, planters=False):
    """外壁パネル1枚。窓の開口を柱型・腰壁・まぐさで囲み、ガラスを奥に入れる。

    ローカル座標: X=壁の幅方向 (中心0)、Y=0が床、Z=+WALL/2 が外面。door は扉にする窓の番号。
    """
    pier = (width - count * window_width) / (count + 1)
    top = height - head
    mb.box((width, head, WALL), (0, height - head / 2, 0), facade)
    for k in range(count + 1):
        mb.box((pier, top, WALL), (-width / 2 + pier / 2 + k * (pier + window_width), top / 2, 0), facade)

    for k in range(count):
        x = -width / 2 + pier * (k + 1) + window_width * (k + 0.5)
        is_door = k == door
        bottom = 0.0 if is_door else sill
        opening = top - bottom
        middle = (top + bottom) / 2
        if not is_door:
            mb.box((window_width, sill, WALL), (x, sill / 2, 0), facade)
        mb.box((window_width, opening, 0.05), (x, middle, -WALL / 2 + 0.06), p["door"] if is_door else p["glass"])
        # 内側の額縁
        trim_z = -WALL / 2 + 0.1
        for side in (-1, 1):
            mb.box((0.07, opening, 0.12), (x + side * (window_width / 2 - 0.035), middle, trim_z), p["frame"])
        mb.box((window_width, 0.07, 0.12), (x, top - 0.035, trim_z), p["frame"])
        if is_door:
            mb.box((0.06, opening - 0.07, 0.08), (x, middle - 0.035, trim_z + 0.02), p["frame"])
            for side in (-1, 1):
                mb.box((0.05, 0.4, 0.06), (x + side * 0.15, 1.05, trim_z + 0.08), p["yellow"])
            mb.box((window_width + 0.5, 0.12, 0.6), (x, 0.06, WALL / 2 + 0.3), p["frame"])
            continue
        mb.box((window_width, 0.07, 0.12), (x, bottom + 0.035, trim_z), p["frame"])
        if window_width > 1.3:
            mb.box((0.06, opening, 0.08), (x, middle, trim_z + 0.02), p["frame"])
        if opening > 1.6:
            mb.box((window_width, 0.06, 0.08), (x, bottom + opening * 0.72, trim_z + 0.02), p["frame"])
        mb.box((window_width + 0.24, 0.09, 0.22), (x, sill - 0.045, WALL / 2 + 0.07), p["frame"], bevel=0.02)
        if planters:
            mb.box((window_width, 0.28, 0.32), (x, sill - 0.24, WALL / 2 + 0.2), p["dark"], bevel=0.02)
            mb.box((window_width - 0.1, 0.12, 0.24), (x, sill - 0.06, WALL / 2 + 0.2), p["grass"], bevel=0.04)


def balcony(mb, p, facade, width):
    """外壁パネルの前に張り出すバルコニー (パネルのローカル座標)。"""
    depth = 1.0
    z = WALL / 2 + depth / 2
    mb.box((width, 0.16, depth), (0, 0.08, z), p["frame"], bevel=0.02)
    mb.box((width, 0.85, 0.08), (0, 0.16 + 0.425, z + depth / 2 - 0.04), p["cream"])
    for side in (-1, 1):
        mb.box((0.08, 0.85, depth - 0.08), (side * (width / 2 - 0.04), 0.585, z - 0.04), p["cream"])
    mb.box((width + 0.06, 0.07, 0.14), (0, 1.045, z + depth / 2 - 0.04), facade, bevel=0.02)


def awning(mb, p, facade, width, y, depth=1.3):
    """商店のしま模様の日よけ (パネルのローカル座標、y は付け根の高さ)。"""
    z0 = WALL / 2
    outline = [(z0, y), (z0 + depth, y - 0.4), (z0 + depth, y - 0.72), (z0 + depth - 0.07, y - 0.72),
               (z0 + depth - 0.07, y - 0.47), (z0, y - 0.1)]
    stripes = max(2, round(width / 0.6))
    stripe = width / stripes
    for i in range(stripes):
        mb.prism(outline, stripe, (-width / 2 + stripe * (i + 0.5), 0, 0), facade if i % 2 == 0 else p["cream"])


def building(style, floors, width, facade_color, name):
    """建物を組み立て、ルート (底面中心) を返す。"""
    p = palette()
    facade = facade_paint(name, facade_color)
    roof_paint = facade_paint(f"{name}Roof", tuple(c * 0.62 for c in facade_color))
    root = bkit.empty({TOWER: "Tower frame", APARTMENT: "Apartment frame", SHOP: "Shop frame"}[style] + f" {name}")
    half = width / 2
    span = width - 2 * PILLAR
    panel_width = span / 2 - 0.04
    panel_height = FLOOR - BAND
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))

    for level in range(floors):
        y = level * FLOOR
        top_floor = level == floors - 1

        for i, (sx, sz) in enumerate(corners):
            mb = bkit.MeshBuilder()
            mb.box((PILLAR, FLOOR, PILLAR), (sx * (half - PILLAR / 2), y + FLOOR / 2, sz * (half - PILLAR / 2)), p["frame"], bevel=0.05)
            if level == 0:
                mb.box((PILLAR + 0.16, 0.45, PILLAR + 0.16), (sx * (half - PILLAR / 2), 0.225, sz * (half - PILLAR / 2)), p["dark"], bevel=0.03)
            mb.build(f"Pillar L{level} S{i}", parent=root)

        for i, yaw in enumerate(SIDES):
            # 階の上の梁: 外面を柱と揃えた帯と、その下の細い水切り
            mb = bkit.MeshBuilder()
            with mb.at(yaw=yaw):
                mb.box((span, BAND, 0.6), (0, y + FLOOR - BAND / 2, half - 0.3), p["frame"])
                mb.box((span, 0.08, 0.14), (0, y + FLOOR - BAND - 0.04, half + 0.05), p["frame"])
            mb.build(f"Beam L{level} S{i}", parent=root)

        inner = half - 0.6
        for i, (sx, sz) in enumerate(corners):
            mb = bkit.MeshBuilder()
            mb.box((inner, 0.24, inner), (sx * inner / 2, y + FLOOR - 0.12, sz * inner / 2), p["cream"] if top_floor else p["frame"])
            mb.build(f"Floor slab L{level} S{i}", parent=root)

        for i, yaw in enumerate(SIDES):
            for panel in (-1, 1):
                mb = bkit.MeshBuilder()
                front = i == 0
                with mb.at(position=(0, y, 0), yaw=yaw), mb.at(position=(panel * span / 4, 0, half - 0.2)):
                    # 正面1階の、中央寄りの窓を入口にする
                    entrance = level == 0 and front
                    if style == TOWER:
                        count = 3
                        door = (count - 1 if panel < 0 else 0) if entrance else None
                        window_wall(mb, p, facade, panel_width, panel_height, count, panel_width / 4.6, 0.75, 0.45, door)
                    elif style == APARTMENT:
                        door = (1 if panel < 0 else 0) if entrance else None
                        window_wall(mb, p, facade, panel_width, panel_height, 2, 2.3, 0.95, 0.4, door)
                        if level > 0 and i in (0, 2):
                            balcony(mb, p, facade, panel_width - 0.6)
                    elif level == 0 and entrance:
                        door = 1 if panel < 0 else 0
                        window_wall(mb, p, facade, panel_width, panel_height, 2, (panel_width - 1.5) / 2, 0.35, 0.95, door)
                        awning(mb, p, facade, panel_width - 1.0, panel_height - 0.95)
                    elif level == 0:
                        window_wall(mb, p, facade, panel_width, panel_height, 1, panel_width - 1.2, 0.35, 0.95)
                        awning(mb, p, facade, panel_width - 1.0, panel_height - 0.95)
                    else:
                        window_wall(mb, p, facade, panel_width, panel_height, 2, 1.7, 1.0, 0.45, planters=True)
                if entrance and panel < 0:
                    entrance_details(mb, p, facade, style, half)
                mb.build(f"Facade panel L{level} S{i} P{'L' if panel < 0 else 'R'}", parent=root)

    roof(style, floors, width, facade, roof_paint, p, root)
    return root


def entrance_details(mb, p, facade, style, half):
    """正面1階の入口まわり (ひさし・看板)。正面の左パネルに含める。"""
    with mb.at(yaw=180):
        if style == SHOP:
            mb.box((half * 1.1 + 0.3, 1.0, 0.16), (0, FLOOR - BAND - 0.62, half + 0.02), facade, bevel=0.03)
            mb.box((half * 1.1, 0.8, 0.2), (0, FLOOR - BAND - 0.62, half + 0.06), p["cream"], bevel=0.03)
            for k in range(-2, 3):
                mb.box((1.2, 0.26, 0.06), (k * 1.6, FLOOR - BAND - 0.62, half + 0.17), p["dark"])
        else:
            canopy = 5.0 if style == TOWER else 3.4
            mb.box((canopy, 0.2, 1.6), (0, 2.95, half + 0.7), p["frame"], bevel=0.04)
            mb.box((canopy + 0.1, 0.08, 0.1), (0, 2.86, half + 1.5), facade)
            for side in (-1, 1):
                mb.cylinder(0.07, 2.85, (side * (canopy / 2 - 0.25), 1.425, half + 1.35), p["dark"], segments=12)


def roof(style, floors, width, facade, roof_paint, p, root):
    top = floors * FLOOR
    half = width / 2
    mb = bkit.MeshBuilder()
    if style == SHOP:
        # 切妻屋根: 妻壁 (丸窓つき)、軒の出た屋根板、瓦の筋、棟、煙突
        slope = math.radians(22)
        thickness = 0.25
        eave = half + 0.45
        eave_y = top + thickness - (eave - half) * math.tan(slope)
        rise = eave * math.tan(slope)
        ridge_y = eave_y + rise
        inner_ridge = ridge_y - thickness / math.cos(slope)
        mb.prism([(-half, top), (half, top), (0, inner_ridge)], width, mat=p["cream"])
        mb.prism([(-eave, eave_y - thickness), (0, inner_ridge), (eave, eave_y - thickness),
                  (eave, eave_y), (0, ridge_y), (-eave, eave_y)], width + 0.5, mat=roof_paint, bevel=0.03)
        for side in (-1, 1):
            window_y = top + (inner_ridge - top) * 0.38
            mb.cylinder(0.75, 0.08, (side * half, window_y, 0), p["frame"], axis="x", segments=28)
            mb.cylinder(0.6, 0.1, (side * half, window_y, 0), p["glass"], axis="x", segments=28)
            mb.box((0.12, 1.2, 0.07), (side * (half + 0.03), window_y, 0), p["frame"])
            for k in range(1, 7):
                t = k / 7
                mb.box((width + 0.5, 0.06, 0.14), (0, eave_y + rise * t + 0.03, side * eave * (1 - t)), facade,
                       pitch=side * math.degrees(slope))
        mb.box((width + 0.6, 0.22, 0.42), (0, ridge_y + 0.04, 0), p["cream"], bevel=0.05)
        chimney_y = eave_y + rise * 0.55
        mb.box((1.1, 2.6, 1.1), (half * 0.45, chimney_y + 0.9, half * 0.3), p["cream"], bevel=0.04)
        mb.box((1.3, 0.18, 1.3), (half * 0.45, chimney_y + 2.25, half * 0.3), p["dark"], bevel=0.03)
        mb.build(f"Shop gable roof L{floors - 1}", parent=root)
        return

    # 陸屋根のパラペット
    for i, yaw in enumerate(SIDES):
        length = width if i % 2 == 0 else width - 0.6
        with mb.at(yaw=yaw):
            mb.box((length, 0.6, 0.3), (0, top + 0.3, half - 0.15), p["frame"])
            mb.box((length + 0.1 if i % 2 == 0 else length, 0.1, 0.42), (0, top + 0.65, half - 0.15), facade, bevel=0.02)

    if style == TOWER:
        # 段状の頂部、ガラスの帯、尖塔と航空障害灯
        mb.box((width * 0.5, 1.4, width * 0.5), (0, top + 0.7, 0), p["frame"], bevel=0.06)
        crown = width * 0.36
        mb.box((crown, 1.8, crown), (0, top + 2.3, 0), facade, bevel=0.06)
        for yaw in SIDES:
            with mb.at(yaw=yaw):
                mb.box((crown - 0.6, 0.7, 0.06), (0, top + 2.3, crown / 2 + 0.01), p["glass"])
        mb.box((crown + 0.3, 0.22, crown + 0.3), (0, top + 3.31, 0), p["frame"], bevel=0.04)
        mb.cylinder(0.32, 0.5, (0, top + 3.67, 0), p["dark"], segments=16)
        mb.cylinder(0.12, 3.0, (0, top + 5.4, 0), p["frame"], segments=12)
        mb.sphere(0.28, (0, top + 7.0, 0), p["beacon"])
        name = "Tower crown"
    else:
        # 塔屋、給水タンク、室外機
        mb.box((3.2, 2.6, 3.2), (half * 0.42, top + 1.3, half * 0.38), p["cream"], bevel=0.05)
        mb.box((1.1, 2.0, 0.06), (half * 0.42, top + 1.0, half * 0.38 - 1.62), p["door"])
        mb.box((3.5, 0.18, 3.5), (half * 0.42, top + 2.69, half * 0.38), facade, bevel=0.03)
        tank = (-half * 0.45, -half * 0.3)
        for sx in (-1, 1):
            for sz in (-1, 1):
                mb.box((0.15, 1.3, 0.15), (tank[0] + sx * 0.7, top + 0.65, tank[1] + sz * 0.7), p["dark"])
        mb.box((2.0, 0.12, 2.0), (tank[0], top + 1.36, tank[1]), p["dark"])
        mb.cylinder(1.1, 1.7, (tank[0], top + 2.27, tank[1]), p["frame"], segments=32)
        mb.cylinder(1.15, 0.5, (tank[0], top + 3.37, tank[1]), facade, segments=32, radius_end=0.2)
        for k in range(3):
            x = -half * 0.3 + k * 1.5
            mb.box((1.2, 0.8, 0.9), (x, top + 0.4, half * 0.55), p["frame"], bevel=0.04)
            mb.cylinder(0.3, 0.05, (x, top + 0.82, half * 0.55), p["dark"], segments=20)
        name = "Roof utility"
    mb.build(f"{name} L{floors - 1}", parent=root)


SAMPLES = (
    ("Tower", TOWER, 7, 14.4, (1.0, 0.68, 0.04)),
    ("Apartment", APARTMENT, 5, 20.0, (1.0, 0.26, 0.18)),
    ("Shop", SHOP, 2, 17.6, (0.62, 0.27, 1.0)),
)


def street(p):
    """プレビュー用の道路と歩道 (書き出しには含めない)。"""
    road = bkit.material("PreviewRoad", (0.13, 0.2, 0.32), roughness=0.9)
    walk = bkit.material("PreviewSidewalk", (1.0, 0.94, 0.77), roughness=0.9)
    mb = bkit.MeshBuilder()
    mb.box((90, 0.1, 16), (0, -0.05, -20), road)
    for k in range(-11, 12):
        mb.box((2.0, 0.02, 0.3), (k * 4, 0.01, -20), p["cream"])
    mb.box((90, 0.12, 12), (0, -0.06, -6), walk)
    return mb.build("_PreviewStreet")


if __name__ == "__main__":
    import player_tank

    args = bkit.script_args()
    bkit.reset_scene()
    out = args["out"]
    roots = []
    for name, style, floors, width, color in SAMPLES:
        root = building(style, floors, width, color, name)
        bkit.export_fbx(os.path.join(out, f"City{name}.fbx"), root)
        roots.append(root)
    bkit.save_blend(os.path.join(out, "CityBuildings.blend"))

    # 道路に並べ、戦車を置いて大きさを比べる
    for root, x in zip(roots, (-22, 0, 22)):
        bkit.place(root, (x, 0, 0))
    street(palette())
    tank = player_tank.build_tank()
    bkit.place(tank, (-4, 0.8, -17), yaw=-70)
    buildings = [o for root in roots for o in bkit.hierarchy(root) if o.type == "MESH"]
    bkit.render_preview(os.path.join(out, "CityBuildings.png"), direction=(0.55, 0.42, -1.0), focus=buildings,
                        distance=2.3, lens=35, resolution=(1600, 900), floor_color=(0.3, 0.75, 0.45))
    bkit.render_preview(os.path.join(out, "CityBuildings_close.png"), direction=(-0.55, 0.2, -1.0),
                        focus=[o for o in bkit.hierarchy(roots[2]) if o.type == "MESH"], distance=2.3, lens=35,
                        resolution=(1600, 900), floor_color=(0.3, 0.75, 0.45))
