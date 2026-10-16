import json
import math

import bpy
from mathutils import Euler, Matrix, Vector

from pose import OFFSET, ROTATION, STRETCH, element_pose, game_rotation
from reconcile import walk

TO_BLENDER = Matrix(((0, 0, 1), (1, 0, 0), (0, 1, 0)))
TO_GAME = TO_BLENDER.transposed()
KEYS = (*OFFSET, *ROTATION, *STRETCH)
SHORTEST = ("rotShortestDistanceX", "rotShortestDistanceY", "rotShortestDistanceZ")
FIT = (0.005, 0.001)  # degrees, 1/16 block
VANISHED = 1e-4


def load_animations(data, armature):
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    elements = {element["name"]: element for element in walk(data["elements"])}
    for animation in data.get("animations", []):
        action = bpy.data.actions.new(animation["code"])
        action.use_fake_user = True
        action["vs"] = json.dumps({key: value for key, value in animation.items() if key != "keyframes"})
        length = animation["quantityframes"]
        version = animation.get("version", 0)
        anchors = {0, length, *(keyframe["frame"] for keyframe in animation["keyframes"])}
        for name in dict.fromkeys(name for keyframe in animation["keyframes"] for name in keyframe["elements"]):
            if name not in armature.pose.bones:
                continue
            armature.pose.bones[name].rotation_mode = "XZY"
            track, previous = [], None
            for frame in range(length + 1):
                location, rotation, stretch = to_blender(elements[name], element_pose(animation, name, frame % length), version)
                previous = rotation.to_euler("XZY", previous) if previous else rotation.to_euler("XZY")
                track.append([*location, *previous, *stretch])
            for frame in fit(track, anchors, lambda frame, values: excess_over_fit(basis(values), basis(track[frame]))):
                key(action, name, frame, track[frame])
        for curve in action.fcurves:
            curve.update()
    armature.animation_data_create().action = None
    for bone in armature.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)


def save_animations(elements, version, armature):
    by_name = {element["name"]: element for element in walk(elements)}
    animations = []
    data = armature.animation_data_create()
    playing, slot, current = data.action, data.action_slot, bpy.context.scene.frame_current
    for action in bpy.data.actions:
        meta = json.loads(action.get("vs", "{}"))
        length = meta.get("quantityframes") or int(action.frame_range[1])
        keyed = {}
        for curve in action.fcurves:
            bone = curve.data_path.split('"')[1] if curve.data_path.startswith("pose.bones") else None
            if bone in by_name and bone in armature.pose.bones:
                keyed.setdefault(bone, {0, length}).update(int(point.co[0]) for point in curve.keyframe_points if point.co[0] < length)
        if not keyed:
            continue
        play(armature, action)
        shown = {bone: [] for bone in keyed}
        for frame in range(length + 1):
            bpy.context.scene.frame_set(frame)
            for bone in keyed:
                shown[bone].append(armature.pose.bones[bone].matrix_basis.copy())
        poses = {bone: game_keyframes(by_name[bone], shown[bone], anchors, version) for bone, anchors in keyed.items()}
        resolved = {frame for keyframes in poses.values() for frame in keyframes}
        for bone, keyframes in poses.items():
            if any(pose.get(SHORTEST[0]) for pose in keyframes.values()):
                poses[bone] = game_keyframes(by_name[bone], shown[bone], keyed[bone] | resolved, version)
        frames = {}
        for bone, keyframes in poses.items():
            for frame, pose in keyframes.items():
                frames.setdefault(frame, {})[bone] = pose
        animations.append({
            "name": meta.get("name", action.name),
            "code": action.name,
            "quantityframes": length,
            **{key: value for key, value in meta.items() if key not in ("name", "code", "quantityframes", "version")},
            **({"version": version} if version else {}),
            "keyframes": [{"frame": frame, "elements": frames[frame]} for frame in sorted(frames)],
        })
    data.action = playing
    if playing and slot:
        data.action_slot = slot
    bpy.context.scene.frame_set(current)
    return animations


def game_keyframes(element, shown, anchors, version):
    length = len(shown) - 1
    track, previous = [], None
    for matrix in shown:
        location, rotation, stretch = matrix.decompose()
        pose, previous = to_game(element, location, rotation.to_matrix(), stretch, version, previous)
        track.append(pose)
    shortest = any(abs(track[length][key] - track[0][key]) >= 180 for key in ROTATION)
    track[length] = track[0]

    def excess(frame, values):
        return excess_over_fit(blender_matrix(element, dict(zip(KEYS, values)), version), shown[frame])

    values = [[pose[key] for key in KEYS] for pose in track]
    chosen = fit(values, anchors, excess, shortest_lerp if shortest else linear)
    stretched = any(abs(pose[key] - 1) > 1e-6 for pose in track for key in STRETCH)
    result = {}
    for frame in chosen:
        if frame < length:
            pose = {key: track[frame][key] for key in KEYS if stretched or key not in STRETCH}
            result[frame] = {**pose, **(dict.fromkeys(SHORTEST, True) if shortest else {})}
    return result


def linear(start, end, t):
    return [a + (b - a) * t for a, b in zip(start, end)]


def shortest_lerp(start, end, t):
    return [a + ((((b - a + 180) % 360) - 180) if KEYS[index] in ROTATION else b - a) * t for index, (a, b) in enumerate(zip(start, end))]


def fit(track, anchors, excess, lerp=linear):
    keys = sorted(anchors)
    while True:
        added = []
        for start, end in zip(keys, keys[1:]):
            worst, frame = 0.0, None
            for between in range(start + 1, end):
                over = excess(between, lerp(track[start], track[end], (between - start) / (end - start)))
                if over > worst:
                    worst, frame = over, between
            if frame is not None:
                added.append(frame)
        if not added:
            return keys
        keys = sorted(keys + added)


def excess_over_fit(a, b):
    degrees = math.degrees(a.to_quaternion().rotation_difference(b.to_quaternion()).angle)
    units = max((a.translation - b.translation).length, 16 * max(abs(x - y) for x, y in zip(a.to_scale(), b.to_scale())))
    return max(degrees / FIT[0], units / FIT[1]) - 1


def basis(values):
    return Matrix.LocRotScale(Vector(values[0:3]), Euler(values[3:6], "XZY"), Vector(values[6:9]))


def blender_matrix(element, pose, version):
    location, rotation, stretch = to_blender(element, pose, version)
    return Matrix.LocRotScale(location, rotation, stretch)


def play(armature, action):
    for bone in armature.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    armature.animation_data_create().action = action
    if action.slots:
        armature.animation_data.action_slot = action.slots[0]


def to_blender(element, pose, version):
    scale, corner = shape_of(element)
    offset = Vector([pose.get(key, 0) for key in OFFSET])
    stretch = Vector([pose.get(key, 1) for key in STRETCH])
    if version == 1:
        rotation = game_rotation(pose)
        location = scale * (corner + offset) - scale * stretch * (rotation @ corner)
    else:
        rotation = game_rotation(element).transposed() @ game_rotation({axis: float(element.get(axis, 0)) + pose.get(axis, 0) for axis in ROTATION})
        location = rotation @ (scale * stretch * offset)
    return TO_BLENDER @ location, TO_BLENDER @ rotation @ TO_GAME, TO_BLENDER @ stretch


def to_game(element, location, turned, stretch, version, previous):
    scale, corner = shape_of(element)
    location = TO_GAME @ location
    rotation = TO_GAME @ turned @ TO_BLENDER
    stretch = Vector([value if abs(value) > VANISHED else VANISHED for value in TO_GAME @ stretch])
    if version == 1:
        offset = divide(location + scale * stretch * (rotation @ corner), scale) - corner
        total = TO_BLENDER @ rotation @ TO_GAME
        angles = total.to_euler("XZY", previous) if previous else total.to_euler("XZY")
        turn = degrees(angles)
    else:
        offset = divide(rotation.transposed() @ location, scale * stretch)
        total = TO_BLENDER @ game_rotation(element) @ rotation @ TO_GAME
        angles = total.to_euler("XZY", previous) if previous else total.to_euler("XZY")
        turn = [value - float(element.get(axis, 0)) for value, axis in zip(degrees(angles), ROTATION)]
    pose = {**dict(zip(OFFSET, offset)), **dict(zip(ROTATION, turn)), **dict(zip(STRETCH, stretch))}
    return pose, angles


def shape_of(element):
    scale = Vector([float(element.get("scale" + axis, 1)) for axis in "XYZ"])
    corner = Vector(element["from"]) - Vector(element.get("rotationOrigin", (0, 0, 0)))
    return scale, corner


def key(action, bone, frame, values):
    path = f'pose.bones["{bone}"]'
    for channel, start in (("location", 0), ("rotation_euler", 3), ("scale", 6)):
        for index in range(3):
            curve = action.fcurves.find(f"{path}.{channel}", index=index) or action.fcurves.new(f"{path}.{channel}", index=index, action_group=bone)
            curve.keyframe_points.insert(frame, values[start + index], options={"FAST"}).interpolation = "LINEAR"


def degrees(euler):
    return [math.degrees(euler.y), math.degrees(euler.z), math.degrees(euler.x)]


def divide(a, b):
    return Vector([x / y for x, y in zip(a, b)])
