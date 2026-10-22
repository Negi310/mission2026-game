"""Blenderをバックグラウンドで動かしてモデルを作るための共通処理。

座標・寸法・回転はUnityと同じ (X右, Y上, Z前, メートル, 度) で書く。
Blender内部の座標 (Z上, -Y前) への変換と、FBX書き出し時のUnity向け変換はこのモジュールが受け持つ。

    root = bkit.empty("Prop")
    mb = bkit.MeshBuilder()
    mb.box((1, 2, 1), (0, 1, 0), paint, bevel=0.05)
    mb.build("Body", parent=root)

FBXのbake_space_transformは2階層以上の子の位置を誤るため使わない。代わりに、メッシュと
子の位置をFBXの座標系 (Y上, Unityから見てX反転) で持ち、ルートだけをBlender用に回しておく。
書き出し時はルートの回転がFBXの軸変換と打ち消し合い、すべての階層が単純な平行移動になる。
"""

import math
import os
import sys
from contextlib import contextmanager

import bmesh
import bpy
from bpy_extras.io_utils import axis_conversion
from mathutils import Matrix, Vector

FBX_FORWARD, FBX_UP = "-Z", "Y"
# Blender → FBX の軸変換 (書き出し時にルートへ掛かる) と、それを打ち消すルートの向き
BLENDER_TO_FBX = axis_conversion(to_forward=FBX_FORWARD, to_up=FBX_UP).to_4x4()
ROOT_BASIS = BLENDER_TO_FBX.inverted()
# Unity (左手系) → FBX (右手系)。UnityはFBXのXを反転して読み込む。
UNITY_TO_FBX = Matrix.Diagonal((-1, 1, 1, 1))


def log(message):
    print(f"[bkit] {message}", flush=True)


def to_fbx(position):
    x, y, z = position
    return Vector((-x, y, z))


def to_blender(position):
    """Unity座標をBlenderのワールド座標にする (カメラなど、FBXに出さない物の配置用)。"""
    return (ROOT_BASIS @ to_fbx(position).to_4d()).to_3d()


def script_args():
    """`--` より後ろの引数を {名前: 値} にする。`--out` は必ず入る。"""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {}
    key = None
    for item in argv:
        if item.startswith("--"):
            key = item[2:]
            args[key] = True
        elif key:
            args[key] = item
            key = None
    args.setdefault("out", os.path.abspath("output/blender"))
    os.makedirs(args["out"], exist_ok=True)
    return args


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def _linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def material(name, color, metallic=0.0, roughness=0.55, emission=0.0, alpha=1.0):
    """color はUnityのインスペクターと同じsRGB (0〜1)。emission は発光の強さ。"""
    linear = tuple(_linear(c) for c in color)
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*linear, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha
    if emission > 0:
        bsdf.inputs["Emission Color"].default_value = (*linear, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*linear, 1.0)
    return mat


def _rotation(yaw=0.0, pitch=0.0, roll=0.0):
    # UnityのEuler順 (Z→X→Y) に合わせる。
    return (Matrix.Rotation(math.radians(yaw), 4, "Y")
            @ Matrix.Rotation(math.radians(pitch), 4, "X")
            @ Matrix.Rotation(math.radians(roll), 4, "Z"))


_AXIS = {
    "x": Matrix.Rotation(math.radians(90), 4, "Y"),
    "y": Matrix.Rotation(math.radians(-90), 4, "X"),
    "z": Matrix.Identity(4),
}


class MeshBuilder:
    """複数の形状を1つのメッシュにまとめて作る。部品ごとにマテリアルを持てる。

    bpy.ops を使わずbmeshへ直接書くので、数百個の部品でも速い。
    """

    def __init__(self):
        self.bm = bmesh.new()
        self.materials = []
        self.stack = [Matrix.Identity(4)]

    @contextmanager
    def at(self, position=(0, 0, 0), yaw=0.0, pitch=0.0, roll=0.0):
        """このブロック内の形状を、指定した位置・回転のローカル座標で置く。"""
        self.stack.append(self.stack[-1] @ Matrix.Translation(position) @ _rotation(yaw, pitch, roll))
        try:
            yield
        finally:
            self.stack.pop()

    def _paint(self, faces, mat):
        if mat not in self.materials:
            self.materials.append(mat)
        index = self.materials.index(mat)
        for face in faces:
            face.material_index = index
        return index

    def _bevel(self, verts, width, segments, index):
        edges = list({e for v in verts for e in v.link_edges})
        result = bmesh.ops.bevel(
            self.bm, geom=edges + list(verts), offset=width, offset_type="OFFSET",
            segments=segments, profile=0.5, affect="EDGES", clamp_overlap=True,
        )
        # 面取りで増えた面は元の形のマテリアルを継がないことがあるので、明示的に揃える
        for face in result["faces"]:
            face.smooth = segments > 1
            face.material_index = index

    def _matrix(self, position, yaw, pitch, roll):
        return self.stack[-1] @ Matrix.Translation(position) @ _rotation(yaw, pitch, roll)

    def box(self, size, position=(0, 0, 0), mat=None, bevel=0.0, yaw=0.0, pitch=0.0, roll=0.0, segments=2):
        """size は (幅X, 高さY, 奥行Z)。position は箱の中心。segments は角の丸めの分割数 (1で面取り)。"""
        matrix = self._matrix(position, yaw, pitch, roll) @ Matrix.Diagonal((*size, 1))
        verts = bmesh.ops.create_cube(self.bm, size=1.0, matrix=matrix)["verts"]
        index = self._paint({f for v in verts for f in v.link_faces}, mat)
        if bevel > 0:
            self._bevel(verts, bevel, segments, index)

    def cylinder(self, radius, length, position=(0, 0, 0), mat=None, axis="y", segments=24,
                 radius_end=None, yaw=0.0, pitch=0.0, roll=0.0):
        """axis 方向に伸びる円柱。radius_end を渡すと円すい台になる。"""
        matrix = self._matrix(position, yaw, pitch, roll) @ _AXIS[axis]
        verts = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=segments,
            radius1=radius, radius2=radius if radius_end is None else radius_end,
            depth=length, matrix=matrix,
        )["verts"]
        faces = {f for v in verts for f in v.link_faces}
        self._paint(faces, mat)
        for face in faces:
            face.smooth = len(face.verts) == 4 and segments > 8

    def sphere(self, radius, position=(0, 0, 0), mat=None, segments=16, scale=(1, 1, 1)):
        """球。scale で軸ごとに伸ばすと楕円体になる。"""
        matrix = self._matrix(position, 0, 0, 0) @ Matrix.Diagonal((*scale, 1))
        verts = bmesh.ops.create_uvsphere(
            self.bm, u_segments=segments, v_segments=max(4, segments // 2), radius=radius, matrix=matrix,
        )["verts"]
        faces = {f for v in verts for f in v.link_faces}
        self._paint(faces, mat)
        for face in faces:
            face.smooth = True

    def prism(self, profile, depth, position=(0, 0, 0), mat=None, axis="x", bevel=0.0,
              yaw=0.0, pitch=0.0, roll=0.0, segments=2):
        """2Dの輪郭を押し出した柱体。

        axis="x": 横から見た輪郭 (Z前, Y上) をX方向へ押し出す
        axis="y": 上から見た輪郭 (X右, Z前) をY方向へ押し出す
        axis="z": 正面から見た輪郭 (X右, Y上) をZ方向へ押し出す
        """
        matrix = self._matrix(position, yaw, pitch, roll)
        half = depth / 2

        def point(a, b, d):
            local = {"x": (d, b, a), "y": (a, d, b), "z": (a, b, d)}[axis]
            return matrix @ Vector(local)

        front = [self.bm.verts.new(point(a, b, -half)) for a, b in profile]
        back = [self.bm.verts.new(point(a, b, half)) for a, b in profile]
        faces = [self.bm.faces.new(front), self.bm.faces.new(list(reversed(back)))]
        count = len(profile)
        for i in range(count):
            j = (i + 1) % count
            faces.append(self.bm.faces.new((front[i], front[j], back[j], back[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        index = self._paint(faces, mat)
        if bevel > 0:
            self._bevel(front + back, bevel, segments, index)

    def decal(self, outline, position=(0, 0, 0), mat=None, yaw=0.0):
        """上から見た輪郭 (X右, Z前) の、上向きの面1枚。路面の白線など、上からしか見ない模様用。"""
        matrix = self._matrix(position, yaw, 0, 0)
        face = self.bm.faces.new([self.bm.verts.new(matrix @ Vector((x, 0, z))) for x, z in outline])
        face.normal_update()
        if face.normal.y < 0:
            face.normal_flip()
        self._paint([face], mat)

    def build_split(self, piece, origin=(0, 0, 0), parent=None, roles=None, normalize=False, shade=None):
        """ゲームの部品組み (TownModelKit) 用に、マテリアルごとの別オブジェクトへ分けて書き出す。

        空オブジェクト piece の下に "<piece>|<名前>" のメッシュを並べる。名前は roles[マテリアル名]、
        無ければマテリアル名。各メッシュの原点はその形の中心にし、位置は piece の原点からの相対で持つ。
        normalize=True ではまとめて縦横奥行き1の箱に収める (ゲーム側で拡大して使う形)。
        shade(x, y, z) を渡すと、その位置の明るさ (0〜1以上、または (r, g, b)) を頂点色に入れる。
        """
        roles = roles or {}
        bm = self.bm
        bmesh.ops.transform(bm, matrix=Matrix.Translation(-Vector(origin)), verts=bm.verts)
        if normalize:
            lo, hi = _bounds_of(bm.verts)
            size = Vector([max(h - l, 1e-6) for l, h in zip(lo, hi)])
            bmesh.ops.transform(bm, matrix=Matrix.Diagonal((1 / size.x, 1 / size.y, 1 / size.z, 1))
                                @ Matrix.Translation(-(lo + hi) / 2), verts=bm.verts)
        group = empty(piece, origin, parent)
        for index, mat in enumerate(self.materials):
            part = bm.copy()
            bmesh.ops.delete(part, geom=[f for f in part.faces if f.material_index != index], context="FACES")
            # 面を消しても頂点は残るので、使われない頂点も消す (残すと中心と範囲がずれる)
            bmesh.ops.delete(part, geom=[v for v in part.verts if not v.link_faces], context="VERTS")
            if not part.faces:
                part.free()
                continue
            for face in part.faces:
                face.material_index = 0
            if shade:
                layer = part.loops.layers.color.new("Col")
                for face in part.faces:
                    for loop in face.loops:
                        value = shade(*loop.vert.co)
                        if isinstance(value, tuple):
                            loop[layer] = (*(min(1.0, c) for c in value), 1.0)
                        else:
                            v = min(1.0, value)
                            loop[layer] = (v * 0.92, v, v * 0.90, 1.0)
            lo, hi = _bounds_of(part.verts)
            center = (lo + hi) / 2
            bmesh.ops.transform(part, matrix=UNITY_TO_FBX @ Matrix.Translation(-center), verts=part.verts)
            bmesh.ops.reverse_faces(part, faces=part.faces)
            name = f"{piece}|{roles.get(mat.name, mat.name)}"
            mesh = bpy.data.meshes.new(name)
            part.to_mesh(mesh)
            part.free()
            mesh.materials.append(mat)
            _attach(bpy.data.objects.new(name, mesh), tuple(Vector(origin) + center), group)
        bm.free()
        return group

    def build(self, name, origin=(0, 0, 0), parent=None):
        """メッシュオブジェクトにする。origin はオブジェクトの原点 (回転軸) にする位置。"""
        bm = self.bm
        bmesh.ops.transform(bm, matrix=UNITY_TO_FBX @ Matrix.Translation(-Vector(origin)), verts=bm.verts)
        # 鏡映変換で面の向きが裏返るので戻す
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        for mat in self.materials:
            mesh.materials.append(mat)
        return _attach(bpy.data.objects.new(name, mesh), origin, parent)


def _bounds_of(verts):
    lo = Vector((min(v.co.x for v in verts), min(v.co.y for v in verts), min(v.co.z for v in verts)))
    hi = Vector((max(v.co.x for v in verts), max(v.co.y for v in verts), max(v.co.z for v in verts)))
    return lo, hi


def _attach(obj, origin, parent):
    """Unity座標の origin に置く。親があれば親からの相対位置 (FBX座標) を持たせる。"""
    obj["unity_origin"] = tuple(origin)
    bpy.context.scene.collection.objects.link(obj)
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.location = to_fbx(origin) - to_fbx(parent["unity_origin"])
    else:
        obj.matrix_world = ROOT_BASIS @ Matrix.Translation(to_fbx(origin))
    return obj


def empty(name, position=(0, 0, 0), parent=None, size=0.3):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_type = "PLAIN_AXES"
    obj.empty_display_size = size
    return _attach(obj, position, parent)


def place(root, position=(0, 0, 0), yaw=0.0):
    """ルートをUnity座標の位置・Y回転へ動かす（プレビューの配置用）。"""
    root.matrix_world = (Matrix.Translation(to_blender(position))
                         @ Matrix.Rotation(-math.radians(yaw), 4, "Z")
                         @ ROOT_BASIS @ Matrix.Translation(to_fbx(root["unity_origin"])))
    bpy.context.view_layer.update()


def hierarchy(root):
    return [root, *root.children_recursive]


def export_fbx(path, root):
    """root以下をUnity向けFBX (Y上, Z前, スケール1) で書き出す。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    saved = root.matrix_world.copy()
    root.matrix_world = ROOT_BASIS @ Matrix.Translation(to_fbx(root["unity_origin"]))
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    for obj in hierarchy(root):
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward=FBX_FORWARD,
        axis_up=FBX_UP,
        bake_space_transform=False,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    root.matrix_world = saved
    bpy.context.view_layer.update()
    meshes = [o for o in hierarchy(root) if o.type == "MESH"]
    triangles = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
    log(f"FBXを書き出しました: {path} (メッシュ{len(meshes)}個, 約{triangles}三角形)")


def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    log(f".blendを保存しました: {path}")


def _bounds(objects):
    corners = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def render_preview(path, direction=(1.0, 0.8, 1.3), focus=None, distance=3.6, lens=50,
                   resolution=(1280, 860), floor_color=(0.62, 0.66, 0.7)):
    """形の確認用PNGを1枚描画する。

    direction はUnity座標で見た「対象からカメラへの向き」。focus を渡すとその範囲に合わせる。
    """
    scene = bpy.context.scene
    bpy.context.view_layer.update()
    meshes = [o for o in scene.objects if o.type == "MESH"]
    lo, hi = _bounds(focus or meshes)
    floor_z = _bounds(meshes)[0].z
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.1)

    camera_data = bpy.data.cameras.new("_PreviewCamera")
    camera_data.lens = lens
    camera_data.clip_end = 2000
    camera = bpy.data.objects.new("_PreviewCamera", camera_data)
    camera.location = center + to_blender(direction).normalized() * radius * distance
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(camera)
    scene.camera = camera

    sun_data = bpy.data.lights.new("_PreviewSun", "SUN")
    sun_data.energy = 2.6
    sun_data.angle = math.radians(3)
    sun = bpy.data.objects.new("_PreviewSun", sun_data)
    sun.rotation_euler = (math.radians(42), math.radians(8), math.radians(-35))
    scene.collection.objects.link(sun)

    floor_mesh = bpy.data.meshes.new("_PreviewFloor")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=radius * 20)
    bm.to_mesh(floor_mesh)
    bm.free()
    floor_mesh.materials.append(material("_PreviewFloorMat", floor_color, roughness=0.95))
    floor = bpy.data.objects.new("_PreviewFloor", floor_mesh)
    floor.location = (center.x, center.y, floor_z - 0.002)
    scene.collection.objects.link(floor)

    world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    background = world.node_tree.nodes["Background"]
    background.inputs["Color"].default_value = (0.32, 0.62, 1.0, 1.0)
    background.inputs["Strength"].default_value = 0.9

    scene.render.engine = "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

    for obj in (camera, sun, floor):
        bpy.data.objects.remove(obj)
    log(f"プレビューを描画しました: {path}")
