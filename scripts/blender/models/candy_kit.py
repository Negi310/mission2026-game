"""お菓子の街の家の飾り一式。ゲームは TownModelKit で Models/CandyKit を読み込む。

部品は空オブジェクト "<部品名>" の下の "<部品名>|<オブジェクト名>" (1マテリアルずつ) で、
メートル単位・Unity座標で作る。お菓子のテクスチャを貼れるよう、メートル単位のUVを付ける。
基準点 (原点) と標準の大きさはゲーム側 (TankTownCandyWorld) と合わせてある。

- 砂糖がけの縁: 帯の中心。標準の幅9 (ゲーム側で横に伸ばす)、高さ0.32、奥行0.48
- ビスケットの家の窓: 窓板の中心、壁の外面。標準の窓板3.0×1.7
- パステルの店の窓: ガラスの中心、壁の外面。標準のガラス6.0×1.86
- 日よけ: 付け根の中央、壁の外面。標準の幅6
- 包み紙のキャンディ: 中心。おおよそ2.3×1.1×1.1
- 屋上の飾り: 屋上の板の上面の中心。カップケーキとドームは屋根の幅18.4 (パステルの店) が標準、アイスクリームは固定の大きさ

    pwsh -File scripts/blender/Invoke-Blender.ps1 -Script scripts/blender/models/candy_kit.py -ScriptArguments --assets
"""

import math
import os
import sys

sys.dont_write_bytecode = True  # scripts/blender に __pycache__ を作らない
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import bkit  # noqa: E402

ASSET_PATH = os.path.join("Assets", "TankTown", "Resources", "Models", "CandyKit.fbx")


def palette():
    """マテリアル名はゲーム側の対応表のキー。色はプレビュー用の目安。

    CandyPaint は報酬の色、CandyFrame は家ごとの縁取りの色、CandyGlass は家ごとのガラスの色に塗り替える。
    """
    m = bkit.material
    return {
        "cream": m("CandyCream", (1.0, 0.95, 0.86), roughness=0.6),
        "wafer": m("CandyWafer", (0.93, 0.70, 0.40), roughness=0.7),
        "chocolate": m("CandyChocolate", (0.36, 0.18, 0.09), roughness=0.35),
        "jam": m("CandyJam", (1.0, 0.07, 0.16), roughness=0.3),
        "leaf": m("CandyLeaf", (0.13, 0.77, 0.25), roughness=0.5),
        "paint": m("CandyPaint", (1.0, 0.45, 0.75), roughness=0.35),
        "frame": m("CandyFrame", (1.0, 0.97, 0.9), roughness=0.6),
        "glass": m("CandyGlass", (0.035, 0.48, 0.70), metallic=0.2, roughness=0.15),
        "pink": m("CandyPastelPink", (1.0, 0.34, 0.57), roughness=0.35),
        "aqua": m("CandyAqua", (0.10, 0.82, 1.0), roughness=0.35),
        "yellow": m("CandyYellow", (1.0, 0.83, 0.07), roughness=0.35),
    }


def wave(count, depth):
    """管の太さを、count 個のふくらみが並ぶように揺らす (絞り出しクリームの粒)。"""
    return lambda i, t: 1.0 - depth + depth * abs(math.cos(t * count * math.pi))


def arc(cx, cy, rx, ry, start, end, steps):
    return [(cx + rx * math.cos(math.radians(start + (end - start) * k / steps)),
             cy + ry * math.sin(math.radians(start + (end - start) * k / steps))) for k in range(steps + 1)]


def rounded_rect(width, height, radius, steps=3):
    """正面から見た角丸の長方形 (反時計回り)。"""
    w, h = width / 2 - radius, height / 2 - radius
    points = []
    for cx, cy, start in ((w, -h, -90), (w, h, 0), (-w, h, 90), (-w, -h, 180)):
        points += arc(cx, cy, radius, radius, start, start + 90, steps)
    return points


def profile_radius(profile, y):
    """回転体の輪郭 [(半径, 高さ), ...] の、高さ y での半径。"""
    for (r0, y0), (r1, y1) in zip(profile, profile[1:]):
        if min(y0, y1) <= y <= max(y0, y1) and y1 != y0:
            return r0 + (r1 - r0) * (y - y0) / (y1 - y0)
    return 0.0


def scatter(profile, count, low, high, lift=0.03):
    """回転体の表面 (高さ low〜high) に、ひまわりの種の並びで散らした点。"""
    points = []
    for k in range(count):
        a = math.radians(k * 137.508)
        y = low + (high - low) * (k + 0.5) / count
        r = profile_radius(profile, y) + lift
        points.append((math.cos(a) * r, y, math.sin(a) * r))
    return points


def sprinkles(mb, mats, points, seed=7, length=0.34, thickness=0.09):
    """チョコスプレー。向きはばらばらにし、色は mats を順に使う。"""
    for k, position in enumerate(points):
        yaw = (k * 67 + seed * 13) % 360
        pitch = (k * 41 + seed) % 70 - 35
        mb.box((thickness, thickness, length), position, mats[k % len(mats)], yaw=yaw, pitch=pitch)


# ---------------------------------------------------------------- 壁の飾り

def icing_drip_edge(p):
    """壁の上端や屋根の縁の、報酬の色の砂糖がけ。下へ垂れたしずく付き。"""
    mb = bkit.MeshBuilder()
    width, height = 9.0, 0.32
    top, bottom = height / 2, -height / 2
    # 壁ごとに付く数が多いので、輪郭の点は少なめにする (しずく7つ、先の丸みは4分割)
    lengths = [0.44, 0.3, 0.62, 0.34, 0.54, 0.32, 0.48]
    outline = [(-width / 2, top), (-width / 2, bottom)]
    step = width / len(lengths)
    for k, length in enumerate(lengths):
        # なで肩で垂れ下がり、先が丸くふくらむしずく
        x = -width / 2 + step * (k + 0.5) + (0.16 if k % 2 else -0.1)
        r = 0.11 + 0.03 * (k % 3)
        outline += [(x - r - 0.22, bottom), (x - r * 0.9, bottom - 0.12)]
        outline += arc(x, bottom - length + r, r, r, 180, 360, 4)
        outline += [(x + r * 0.9, bottom - 0.12), (x + r + 0.22, bottom)]
    outline += [(width / 2, bottom), (width / 2, top)]
    mb.prism(outline, 0.48, mat=p["paint"], axis="z")
    return mb, {"CandyPaint": "Reward-colored icing"}


def biscuit_window(p):
    """ビスケットの家の窓。アーチ形のチョコの窓板、絞ったクリームの縁、報酬の色の砂糖の桟。"""
    mb = bkit.MeshBuilder()
    width, rect_top, bottom, rise = 3.0, 0.35, -0.85, 0.5
    half = width / 2
    pane = [(-half, bottom), (half, bottom)] + arc(0, rect_top, half, rise, 0, 180, 12)
    mb.prism(pane, 0.1, (0, 0, 0.05), p["chocolate"], axis="z")
    # 窓板の外周を少し外へ広げた線に沿って、粒の並ぶクリームを絞る
    margin = 0.11
    piping = [(half + margin, bottom - margin)] + [(x, y) for x, y in arc(0, rect_top, half + margin, rise + margin, 0, 180, 16)]
    piping += [(-half - margin, bottom - margin)]
    path = []
    for a, b in zip(piping, piping[1:]):
        steps = max(1, round(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.24))
        path += [(a[0] + (b[0] - a[0]) * s / steps, a[1] + (b[1] - a[1]) * s / steps, 0.12) for s in range(steps)]
    beads = len(path) // 3
    mb.tube(path, 0.1, mat=p["cream"], sides=5, closed=True, radius_at=wave(beads, 0.35))
    mb.box((width + 0.5, 0.14, 0.38), (0, bottom - 0.2, 0.19), p["cream"], bevel=0.05, segments=1)
    # 砂糖の桟 (十字)
    mb.box((0.12, rect_top + rise - bottom, 0.08), (0, (rect_top + rise + bottom) / 2, 0.13), p["paint"])
    mb.box((width, 0.12, 0.08), (0, 0.05, 0.13), p["paint"])
    return mb, {"CandyChocolate": "Biscuit window", "CandyCream": "Biscuit window frame", "CandyPaint": "Sugar window cross"}


def shop_window(p):
    """パステルの店の大きな窓。角の丸いガラスと、太く絞った砂糖の縁、キャンディの棒の方立て。"""
    mb = bkit.MeshBuilder()
    width, height = 6.0, 1.86
    mb.prism(rounded_rect(width, height, 0.35), 0.06, (0, 0, 0.05), p["glass"], axis="z")
    # 直線の部分は管の輪を足さず、角の点だけでつなぐ (壁ごとに付くので軽く)
    path = [(x, y, 0.13) for x, y in rounded_rect(width + 0.24, height + 0.24, 0.45, steps=3)]
    mb.tube(path, 0.12, mat=p["frame"], sides=6, closed=True)
    mb.cylinder(0.08, height, (0, 0, 0.1), p["frame"], segments=8)
    for y in (-0.45, 0.45):
        mb.sphere(0.13, (0, y, 0.14), p["frame"], segments=6)
    mb.box((width + 0.5, 0.16, 0.42), (0, -height / 2 - 0.2, 0.21), p["frame"], bevel=0.06, segments=1)
    return mb, {"CandyGlass": "Pastel candy window", "CandyFrame": "Sugar window frame"}


def candy_awning(p):
    """1階の、ふくらんだしま模様の日よけと、波形の縁飾り。"""
    mb = bkit.MeshBuilder()
    width, depth, rise, thick = 6.0, 1.7, 0.42, 0.08
    stripes = 6
    outer = [(depth * math.sin(math.radians(a)), rise * math.cos(math.radians(a)) - 0.12) for a in range(0, 91, 15)]
    inner = [(z, y - thick) for z, y in reversed(outer)]
    profile = [(0.0, outer[0][1])] + outer[1:] + inner[:-1] + [(0.0, inner[-1][1])]
    stripe = width / stripes
    for k in range(stripes):
        x = -width / 2 + stripe * (k + 0.5)
        mb.prism(profile, stripe, (x, 0, 0), p["paint"] if k % 2 == 0 else p["cream"])
    # 前の縁から垂れる、半円が並んだ縁飾り
    edge_top = outer[-1][1]
    scallops = stripes * 2
    fringe = [(width / 2, edge_top), (-width / 2, edge_top)]
    span = width / scallops
    for k in range(scallops):
        cx = -width / 2 + span * (k + 0.5)
        fringe += arc(cx, edge_top - 0.12, span / 2, 0.2, 180, 360, 4)[0 if k == 0 else 1:]
    mb.prism(fringe, 0.07, (0, 0, depth - 0.02), p["frame"], axis="z")
    return mb, {"CandyPaint": "Awning stripes", "CandyCream": "Awning light stripes", "CandyFrame": "Awning fringe"}


# ---------------------------------------------------------------- 屋上の飾り

def wrapped_candy(p):
    """包み紙のキャンディ。ふくらんだ中身 (報酬の色) と白いしま、ねじった両端の包み紙。"""
    mb = bkit.MeshBuilder()
    length, radius = 0.72, 0.52

    def body(y):
        return radius * math.sqrt(max(0.0, 1 - (y / length) ** 2))

    # 回転体をX軸向きに寝かせる (roll=90 でY軸がX軸へ倒れる)
    profile = [(body(length * math.cos(math.pi * k / 10)), length * math.cos(math.pi * k / 10)) for k in range(11)]
    mb.lathe(list(reversed(profile)), mat=p["paint"], segments=14, roll=90)
    for center in (-0.38, 0.0, 0.38):
        a, b = center - 0.05, center + 0.05
        mb.lathe([(body(a) + 0.025, a), (body(b) + 0.025, b)], mat=p["frame"], segments=14, roll=90)
    for side in (-1, 1):
        mb.cylinder(0.12, 0.22, (side * 0.74, 0, 0), p["cream"], axis="x", segments=8)
        # 円柱の太さは -X 側が radius、+X 側が radius_end。外側へ開くように向きを合わせる
        narrow, wide = 0.1, 0.44
        mb.cylinder(narrow if side > 0 else wide, 0.42, (side * 0.98, 0, 0), p["cream"], axis="x", segments=8,
                    radius_end=wide if side > 0 else narrow)
    return mb, {"CandyPaint": "Roof candy", "CandyCream": "Candy wrapper", "CandyFrame": "Candy stripes"}


def cherry(mb, p, position, radius):
    x, y, z = position
    mb.sphere(radius, position, p["jam"], segments=12, scale=(1, 0.92, 1))
    stem = [(x, y + radius * 0.8, z), (x + 0.12 * radius, y + radius * 1.4, z),
            (x + 0.4 * radius, y + radius * 1.9, z + 0.1 * radius)]
    mb.tube(stem, 0.07 * radius / 0.8, mat=p["leaf"], sides=5)


def cupcake_topper(p):
    """カップケーキのカフェの屋上 (屋根の幅18.4用)。波打つピンクのアイシング、渦を巻くクリーム、さくらんぼ、スプレー。"""
    mb = bkit.MeshBuilder()
    cap = [(9.6, -0.7), (9.5, 0.1), (8.9, 0.8), (7.6, 1.4), (5.0, 1.85), (0.0, 2.05)]
    mb.lathe(cap, mat=p["pink"], segments=32, radius_at=lambda a: 1 + 0.035 * math.cos(a * 14))
    # 3回りで細くなりながら上る、絞り出しクリームの渦
    turns, steps = 3.0, 72
    path = []
    for k in range(steps + 1):
        t = k / steps
        angle = 2 * math.pi * turns * t
        radius = 7.0 * (1 - t) + 0.8 * t
        path.append((math.cos(angle) * radius, 1.7 + 4.5 * t, math.sin(angle) * radius))
    mb.tube(path, 1.65, mat=p["cream"], sides=8, radius_at=lambda i, t: 1.0 - 0.55 * t)
    mb.lathe([(1.2, 5.9), (0.9, 6.6), (0.0, 7.3)], mat=p["cream"], segments=10)
    cherry(mb, p, (0.3, 8.1, 0.0), 1.25)
    sprinkles(mb, [p["paint"], p["aqua"], p["yellow"]], scatter(cap, 40, -0.3, 1.0), length=0.55, thickness=0.15)
    return mb, {"CandyPastelPink": "Cupcake icing cap", "CandyCream": "Cupcake cream swirl", "CandyJam": "Cafe cherry",
                "CandyLeaf": "Cherry stem", "CandyPaint": "Cupcake sprinkles", "CandyAqua": "Aqua sprinkles",
                "CandyYellow": "Lemon sprinkles"}


def ice_cream_topper(p):
    """アイスクリーム店の屋上。ひだのあるワッフルコーン、垂れるバニラ、いちごの段、ウエハース、さくらんぼ。"""
    mb = bkit.MeshBuilder()
    cone = [(0.0, 0.0), (0.4, 0.6), (2.25, 4.9), (0.0, 4.9)]
    mb.lathe(cone, mat=p["wafer"], segments=24, radius_at=lambda a: 1 + 0.035 * abs(math.cos(a * 6)))
    mb.lathe([(2.2, 4.75), (2.5, 4.8), (2.5, 5.35), (2.2, 5.4)], mat=p["wafer"], segments=24)
    vanilla = [(2.55, 5.2), (2.95, 5.6), (3.0, 6.3), (2.6, 7.1), (1.6, 7.7), (0.0, 7.9)]
    mb.lathe(vanilla, mat=p["cream"], segments=24, radius_at=lambda a: 1 + 0.05 * math.cos(a * 7))
    # 縁から垂れるバニラのしずく
    for k in range(9):
        a = math.radians(k * 40 + 10)
        length = 0.55 + 0.35 * ((k * 5) % 3) / 2
        top = (math.cos(a) * 2.75, 5.6, math.sin(a) * 2.75)
        bottom = (math.cos(a) * 2.62, 5.6 - length, math.sin(a) * 2.62)
        mb.tube([top, ((top[0] + bottom[0]) / 2, 5.6 - length / 2, (top[2] + bottom[2]) / 2), bottom], 0.24,
                mat=p["cream"], sides=6, radius_at=lambda i, t: 1.0 + 0.15 * t)
        mb.sphere(0.27, bottom, p["cream"], segments=6)
    strawberry = [(2.1, 7.2), (2.35, 7.6), (2.3, 8.3), (1.7, 8.95), (0.8, 9.3), (0.0, 9.4)]
    mb.lathe(strawberry, mat=p["pink"], segments=22, radius_at=lambda a: 1 + 0.06 * math.cos(a * 6))
    mb.lathe([(0.75, 9.25), (0.7, 9.6), (0.0, 10.4)], mat=p["cream"], segments=12,
             radius_at=lambda a: 1 + 0.22 * math.cos(a * 6))
    mb.box((0.5, 2.6, 0.22), (1.1, 9.8, -0.3), p["wafer"], roll=-22, bevel=0.04, segments=1)
    cherry(mb, p, (-0.45, 10.55, 0.25), 0.7)
    sprinkles(mb, [p["paint"], p["aqua"], p["yellow"]], scatter(strawberry, 24, 7.7, 9.2), seed=3)
    return mb, {"CandyWafer": "Ice cream wafer cone", "CandyCream": "Ice cream scoop", "CandyPastelPink": "Ice cream swirl",
                "CandyJam": "Ice cream cherry", "CandyLeaf": "Cherry stem", "CandyPaint": "Ice cream sprinkles",
                "CandyAqua": "Aqua sprinkles", "CandyYellow": "Lemon sprinkles"}


def candy_dome(p):
    """ピンクの店の屋上 (屋根の幅18.4用)。ひだのあるメレンゲのドーム、報酬の色の粒の輪、砂糖の粒、さくらんぼ。"""
    mb = bkit.MeshBuilder()
    dome = [(5.1, -0.2), (5.25, 0.5), (4.9, 1.6), (4.0, 2.7), (2.5, 3.5), (0.8, 3.9), (0.0, 3.95)]
    mb.lathe(dome, mat=p["cream"], segments=40, radius_at=lambda a: 1 + 0.06 * math.cos(a * 10))
    ring = [(math.cos(2 * math.pi * k / 72) * 5.45, 0.6, math.sin(2 * math.pi * k / 72) * 5.45) for k in range(72)]
    mb.tube(ring, 0.32, mat=p["paint"], sides=6, closed=True, radius_at=wave(24, 0.4))
    for k, point in enumerate(scatter(dome, 24, 1.0, 3.6, lift=0.05)):
        mb.sphere(0.24, point, p["aqua"] if k % 2 else p["yellow"], segments=6)
    cherry(mb, p, (0.0, 5.1, 0.0), 1.25)
    return mb, {"CandyCream": "Candy dome", "CandyPaint": "Reward sugar ring", "CandyAqua": "Aqua sugar pearls",
                "CandyYellow": "Lemon sugar pearls", "CandyJam": "Candy shop cherry", "CandyLeaf": "Cherry stem"}


PIECES = [
    ("Icing drip edge", icing_drip_edge), ("Candy biscuit window", biscuit_window), ("Candy shop window", shop_window),
    ("Candy awning", candy_awning), ("Wrapped candy", wrapped_candy),
    ("Cupcake topper", cupcake_topper), ("Ice cream topper", ice_cream_topper), ("Candy dome topper", candy_dome),
]


def build_kit():
    p = palette()
    root = bkit.empty("Candy kit")
    groups = {}
    for name, builder in PIECES:
        mb, roles = builder(p)
        groups[name] = mb.build_split(name, parent=root, roles=roles, uv=True)
    return root, groups


def move(group, position, yaw=0.0):
    group.location = bkit.to_fbx(position)
    group.rotation_euler = (0, -math.radians(yaw), 0)


def layout_preview(groups):
    """書き出し後に、確認用に部品を並べる (書き出したFBXには影響しない)。"""
    move(groups["Icing drip edge"], (0, 3.2, 0))
    move(groups["Candy biscuit window"], (-7.5, 1.4, 0))
    move(groups["Candy shop window"], (0, 1.2, 0))
    move(groups["Candy awning"], (7.5, 2.4, 0))
    move(groups["Wrapped candy"], (7.5, 0.6, 1.5), yaw=30)
    move(groups["Cupcake topper"], (-14, 0.7, -14))
    move(groups["Ice cream topper"], (0, 0, -10))
    move(groups["Candy dome topper"], (13, 0.2, -12))


if __name__ == "__main__":
    args = bkit.script_args()
    bkit.reset_scene()
    root, groups = build_kit()
    out = args["out"]
    bkit.export_fbx(os.path.join(out, "CandyKit.fbx"), root)
    if args.get("assets"):
        repo = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
        bkit.export_fbx(os.path.join(repo, ASSET_PATH), root)
    bkit.save_blend(os.path.join(out, "CandyKit.blend"))
    layout_preview(groups)
    bkit.render_preview(os.path.join(out, "CandyKit.png"), direction=(0.3, 0.55, 1.0), distance=1.9, lens=35,
                        resolution=(1600, 1000), floor_color=(0.98, 0.86, 0.72))
    walls = ["Icing drip edge", "Candy biscuit window", "Candy shop window", "Candy awning", "Wrapped candy"]
    shown = {o for name in walls for o in groups[name].children}
    hidden = [o for o in bkit.bpy.context.scene.objects if o.type == "MESH" and o not in shown]
    for o in hidden:
        o.hide_render = True
    bkit.render_preview(os.path.join(out, "CandyWalls.png"), focus=list(shown), direction=(0.35, 0.3, 1.0), distance=2.0,
                        lens=40, resolution=(1600, 900), floor_color=(0.98, 0.86, 0.72))
