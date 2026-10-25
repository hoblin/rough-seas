import json
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

from reconcile import DISABLED, walk

VIEWS = {"iso": (1, 1, 0.8), "side": (1, 0, 0), "top": (0, 0, 1), "end": (0, 1, 0)}


def render_views(out, focus=""):
    shape = json.loads(bpy.context.scene["vs_shape"])
    stage(shape)
    framed = {element["name"] for element in walk([find(shape, focus)])} if focus else None
    for view, direction in VIEWS.items():
        render(Vector(direction), os.path.join(out, view + ".png"), framed)


def find(shape, name):
    found = next((element for element in walk(shape["elements"]) if element["name"] == name), None)
    if not found:
        raise SystemExit(f"No element {name} to frame")
    return found


def stage(shape):
    hidden = {each["name"] for element in walk(shape["elements"]) if element.get("renderInEditor") is False for each in walk([element])}
    hidden |= {element["name"] for element in walk(shape["elements"])
               if element.get("faces") and all(face.get("texture") == "#null" for face in element["faces"].values())}
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.hide_render = obj.name in hidden
            strip_disabled(obj)
        elif obj.type != "CAMERA":
            obj.hide_render = True
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "TEXTURE"
    scene.display.shading.show_object_outline = True
    scene.render.resolution_x = scene.render.resolution_y = 768
    scene.world = scene.world or bpy.data.worlds.new("World")
    scene.world.color = (0.55, 0.65, 0.75)


def strip_disabled(obj):
    names = [slot.material.name if slot.material else "" for slot in obj.material_slots]
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    doomed = [face for face in mesh.faces if face.material_index < len(names) and names[face.material_index] == DISABLED]
    bmesh.ops.delete(mesh, geom=doomed, context="FACES")
    mesh.to_mesh(obj.data)
    mesh.free()


def render(direction, path, framed):
    shown = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.data.polygons and not obj.hide_render
             and (framed is None or obj.name in framed)]
    corners = [obj.matrix_world @ Vector(corner) for obj in shown for corner in obj.bound_box]
    low = Vector(min(corner[axis] for corner in corners) for axis in range(3))
    high = Vector(max(corner[axis] for corner in corners) for axis in range(3))
    centre, size = (low + high) / 2, (high - low).length
    camera = bpy.data.objects.get("Preview") or bpy.data.objects.new("Preview", bpy.data.cameras.new("Preview"))
    if camera.name not in bpy.context.scene.collection.objects:
        bpy.context.scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = size * 1.05
    camera.data.clip_end = size * 4
    back = direction.normalized()
    camera.location = centre + back * size * 2
    up = Vector((0, 0, 1)) if abs(back.z) < 0.99 else Vector((-1, 0, 0))
    right = up.cross(back).normalized()
    camera.rotation_euler = Matrix((right, back.cross(right), back)).transposed().to_euler()
    bpy.context.scene.camera = camera
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
