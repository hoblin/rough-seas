import json
import os
import sys

import addon_utils
import bpy
from mathutils import Matrix, Vector

sys.path[:0] = [os.path.dirname(__file__), os.path.join(os.environ["BLENDER_USER_SCRIPTS"], "modules")]
import json5  # noqa: E402
from animation import TO_GAME, load_animations, play, save_animations  # noqa: E402
from pose import animation_error, difference, joints, within_tolerance  # noqa: E402
from preview import render_views  # noqa: E402
from reconcile import DISABLED, reconcile, walk, with_engine_uvs  # noqa: E402
from report import diff, report  # noqa: E402
from textures import link_textures, located  # noqa: E402

SIDES = {(-1, 0, 0): "north", (0, 1, 0): "east", (0, -1, 0): "west", (1, 0, 0): "south", (0, 0, 1): "up", (0, 0, -1): "down"}
# Undoes the add-on's default import offset (translate_origin -8, -8, 0).
TO_GAME_SPACE = Matrix.Diagonal((1 / 16, 1 / 16, 1 / 16, 1)) @ TO_GAME.to_4x4() @ Matrix.Translation((8, 8, 0))


def main():
    bpy.context.preferences.filepaths.save_version = 0
    addon_utils.enable("vintagestory_json", default_set=False)
    command, *args = sys.argv[sys.argv.index("--") + 1:]
    globals()["cmd_" + command](*args)


def fail(message):
    print(message)
    sys.exit(1)


def cmd_import(shape, blend):
    load(shape)
    bpy.ops.wm.save_as_mainfile(filepath=blend)


def cmd_run(blend, script):
    bpy.ops.wm.open_mainfile(filepath=blend)
    run(script)
    bpy.ops.wm.save_mainfile()


def cmd_export(blend, shape=""):
    bpy.ops.wm.open_mainfile(filepath=blend)
    save(shape or bpy.context.scene["vs_path"])


def cmd_edit(shape, script):
    writable(shape)
    load(shape)
    run(script)
    save(shape)


def cmd_preview(source, out, code="", frame="0", focus=""):
    open_or_load(source)
    if code:
        if code not in bpy.data.actions:
            fail(f"No animation {code}; this shape has: {', '.join(action.name for action in bpy.data.actions) or 'none'}")
        play(armature(), bpy.data.actions[code])
        bpy.context.scene.frame_set(int(frame))
    render_views(out, focus)


def cmd_check(source, out):
    open_or_load(source)
    edited = source.endswith(".blend")
    original, exported = round_trip(os.path.join(out, "raw.json"))
    report("add-on alone", list(diff(original, exported)), os.path.join(out, "addon.txt"))
    result = reconcile(original, located(original, exported))
    changes = list(diff(original, result))
    report("bin/model", changes, os.path.join(out, "model.txt"))
    failed = bool(changes) and not edited
    before = {animation["code"]: animation for animation in original.get("animations", [])}
    for animation in result.get("animations", []):
        shown = shown_error(result, animation)
        failed |= not within_tolerance(shown)
        line = f"{animation['code']}: Blender plays the game's motion {error(shown)}"
        if animation["code"] in before and not edited:
            kept = animation_error(original, before[animation["code"]], result, animation)
            failed |= not within_tolerance(kept)
            line += f"; the export keeps the original's motion {error(kept)}"
        print(line)
    hidden = [element["name"] for element in walk(result["elements"]) if element["name"] not in {each["name"] for each in comparable(result["elements"])}]
    if result.get("animations") and hidden:
        print(f"Not compared, under a scaled element Blender cannot show: {', '.join(hidden)}")
    if failed:
        fail("FAILED")
    print("PASSED")


def open_or_load(source):
    if source.endswith(".blend"):
        bpy.ops.wm.open_mainfile(filepath=source)
    else:
        load(source)


def load(shape):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    addon_utils.enable("vintagestory_json", default_set=False)
    data = read(shape)
    strict = os.path.join(bpy.app.tempdir, "shape.json")
    with open(strict, "w") as file:
        json.dump({**data, "elements": [with_engine_uvs(element) for element in data.get("elements", [])]}, file)
    bpy.ops.vintagestory.import_json(filepath=strict, import_animations=bool(data.get("animations")))
    if armature():
        load_animations(data, armature())
    scene = bpy.context.scene
    scene["vs_path"] = os.path.abspath(shape)
    scene["vs_shape"] = json.dumps(data)
    link_textures(data.get("textures", {}))
    hide_disabled_faces(data.get("elements", []))


def writable(shape):
    if os.path.realpath(shape).startswith(os.path.realpath(os.environ["VINTAGE_STORY"]) + os.sep):
        fail(f"Not exported: {shape} is in the game install; export it into RoughSeas/assets (docs/modeling.md)")


def save(shape):
    writable(shape)
    raw = shape + ".raw"
    original, exported = round_trip(raw)
    os.remove(raw)
    result = reconcile(original, located(original, exported))
    for animation in result.get("animations", []):
        if animation not in original.get("animations", []):
            shown = shown_error(result, animation)
            if not within_tolerance(shown):
                fail(f"Not exported: {animation['code']} would play in the game {error(shown)} away from Blender")
    with open(shape, "w") as file:
        json.dump(result, file, indent=1, ensure_ascii=False)
        file.write("\n")
    print("Exported", shape)


def round_trip(raw):
    original = json.loads(bpy.context.scene["vs_shape"])
    bpy.ops.vintagestory.export_json(
        filepath=raw,
        texture_size_x_override=original.get("textureWidth", 16),
        texture_size_y_override=original.get("textureHeight", 16),
        skip_disabled_render=False,
        export_animations=False,
    )
    exported = read(raw)
    if armature():
        elements = reconcile(original, located(original, exported))["elements"]
        version = max((animation.get("version", 0) for animation in original.get("animations", [])), default=0)
        exported["animations"] = save_animations(elements, version, armature())
    return original, exported


def armature():
    return next((obj for obj in bpy.data.objects if obj.type == "ARMATURE"), None)


def run(script):
    with open(script) as file:
        exec(compile(file.read(), script, "exec"), {"bpy": bpy, "__file__": script})


def read(path):
    with open(path) as file:
        text = file.read()
    try:
        return json.loads(text)
    except ValueError:
        return json5.loads(text)


def hide_disabled_faces(elements):
    material = bpy.data.materials.get(DISABLED) or bpy.data.materials.new(DISABLED)
    material["disable"] = True
    material.diffuse_color = (1, 0, 1, 0.15)
    for element in walk(elements):
        obj = bpy.data.objects.get(element["name"])
        faces = element.get("faces")
        if obj is None or obj.type != "MESH" or faces is None:
            continue
        hidden = {side for side in SIDES.values() if side not in faces or faces[side].get("enabled") is False}
        if not hidden:
            continue
        obj.data.materials.append(material)
        for polygon in obj.data.polygons:
            if side_of(polygon) in hidden:
                polygon.material_index = len(obj.data.materials) - 1


def side_of(polygon):
    return max(SIDES.items(), key=lambda side: Vector(side[0]).dot(polygon.normal))[1]


def shown_error(shape, animation):
    rig = armature()
    names = {element["name"] for element in comparable(shape["elements"])}
    meshes = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name in names]
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    rest = {obj.name: obj.matrix_world.copy() for obj in meshes}
    rig.data.pose_position = "POSE"
    play(rig, bpy.data.actions[animation["code"]])
    worst = (0.0, 0.0, None)
    for frame in range(animation["quantityframes"]):
        bpy.context.scene.frame_set(frame)
        expected = joints(shape, animation, frame)
        for obj in meshes:
            shown = TO_GAME_SPACE @ obj.matrix_world @ rest[obj.name].inverted() @ TO_GAME_SPACE.inverted()
            degrees, units = difference(expected[obj.name], shown)
            if degrees > worst[0] or units > worst[1]:
                worst = (max(degrees, worst[0]), max(units, worst[1]), f"frame {frame} {obj.name}")
    return worst


def comparable(elements):
    for element in elements:
        yield element
        if all(float(element.get("scale" + axis, 1)) == 1 for axis in "XYZ"):
            yield from comparable(element.get("children", []))


def error(measured):
    degrees, units, where = measured
    if degrees < 0.001 and units < 0.001:
        return "exactly"
    return f"within {degrees:.3f}° and {units:.3f} units (worst: {where})"


main()
