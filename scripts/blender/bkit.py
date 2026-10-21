"""Blenderをバックグラウンドで動かしてモデルを作るための共通処理。

モデルスクリプトは `import bkit` してから、
`bkit.reset_scene()` → メッシュ作成 → `bkit.export_fbx()` / `bkit.render_preview()` の順で使う。
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.dont_write_bytecode = True


def log(message):
    print(f"[bkit] {message}", flush=True)


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


def material(name, color, metallic=0.0, roughness=0.6, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    mat.diffuse_color = (*color, 1.0)
    return mat


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return obj


def box(name, size, location=(0, 0, 0), mat=None, bevel=0.0):
    """size は (幅X, 奥行Y, 高さZ) メートル。location は底面中心ではなく中心。"""
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
    if mat:
        assign(obj, mat)
    return obj


def cylinder(name, radius, depth, location=(0, 0, 0), rotation=(0, 0, 0), mat=None, vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(
        radius=radius, depth=depth, vertices=vertices, location=location, rotation=rotation
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.shade_auto_smooth()
    if mat:
        assign(obj, mat)
    return obj


def join(name, objects):
    """モディファイアを適用してから1つのメッシュにまとめる。"""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        bpy.context.view_layer.objects.active = obj
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = name
    obj.data.name = name
    return obj


def set_origin_to_bottom(obj):
    """Unityで地面に置きやすいよう、原点をバウンディングボックス底面の中心にする。"""
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    bottom = Vector((
        sum(c.x for c in corners) / 8,
        sum(c.y for c in corners) / 8,
        min(c.z for c in corners),
    ))
    bpy.context.scene.cursor.location = bottom
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    obj.location = (0, 0, 0)


def export_fbx(path, objects=None):
    """Unity向けFBX。Blenderの+Z上/-Y前をUnityの+Y上/+Z前に合わせ、スケール1で出す。"""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects or [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH", "EMPTY", "ARMATURE"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    log(f"FBXを書き出しました: {path}")


def save_blend(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
    log(f".blendを保存しました: {path}")


def render_preview(path, resolution=(960, 720), engine="BLENDER_EEVEE"):
    """全体が収まる斜め上カメラとライトを置いてPNGを1枚出す（形の確認用）。"""
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.1)

    for name in ("_PreviewCamera", "_PreviewSun", "_PreviewFloor"):
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name])

    direction = Vector((1.0, -1.3, 0.9)).normalized()
    bpy.ops.object.camera_add(location=center + direction * radius * 4.2)
    cam = bpy.context.active_object
    cam.name = "_PreviewCamera"
    cam.data.lens = 50
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam

    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), math.radians(10), math.radians(35)))
    sun = bpy.context.active_object
    sun.name = "_PreviewSun"
    sun.data.energy = 3.5

    bpy.ops.mesh.primitive_plane_add(size=radius * 12, location=(center.x, center.y, lo.z))
    floor = bpy.context.active_object
    floor.name = "_PreviewFloor"
    assign(floor, material("_PreviewFloorMat", (0.55, 0.57, 0.6), roughness=0.9))

    world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.68, 0.8, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8

    scene.render.engine = engine
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

    for obj in (cam, sun, floor):
        bpy.data.objects.remove(obj)
    log(f"プレビューを描画しました: {path}")
