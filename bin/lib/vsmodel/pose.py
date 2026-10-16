import math

from mathutils import Matrix, Vector

AXES = "XYZ"
OFFSET = ("offsetX", "offsetY", "offsetZ")
ROTATION = ("rotationX", "rotationY", "rotationZ")
STRETCH = ("stretchX", "stretchY", "stretchZ")
TOLERANCE = (0.1, 0.02)  # degrees, 1/16 block


def same_pose(shape, animation, other_shape, other):
    return within_tolerance(animation_error(shape, animation, other_shape, other))


def within_tolerance(measured):
    return measured[0] < TOLERANCE[0] and measured[1] < TOLERANCE[1]


def animation_error(shape, animation, other_shape, other):
    worst = (0.0, 0.0, None)
    for frame in range(max(animation["quantityframes"], other["quantityframes"])):
        a = joints(shape, animation, frame)
        b = joints(other_shape, other, frame)
        for name, matrix in a.items():
            if name not in b:
                return math.inf, math.inf, f"{animation['code']} {name} lost"
            degrees, units = difference(matrix, b[name])
            if degrees > worst[0] or units > worst[1]:
                worst = (max(degrees, worst[0]), max(units, worst[1]), f"{animation['code']} frame {frame} {name}")
    return worst


def joints(shape, animation, frame):
    rest, posed = {}, {}
    version = animation.get("version", 0)

    def descend(elements, rest_parent, posed_parent):
        for element in elements:
            rest[element["name"]] = rest_parent @ local(element, {}, 0)
            posed[element["name"]] = posed_parent @ local(element, element_pose(animation, element["name"], frame), version)
            descend(element.get("children", []), rest[element["name"]], posed[element["name"]])

    descend(shape["elements"], Matrix.Identity(4), Matrix.Identity(4))
    return {name: posed[name] @ rest[name].inverted() for name in rest}


def element_pose(animation, name, frame):
    resolved = sorted({keyframe["frame"] for keyframe in animation["keyframes"]})
    if frame in resolved:
        return keyed_pose(animation, name, frame)
    left = max((each for each in resolved if each < frame), default=resolved[-1])
    right = min((each for each in resolved if each > frame), default=resolved[0])
    length = animation["quantityframes"]
    t = ((frame - left) % length) / ((right - left) % length or length)
    start, end = keyed_pose(animation, name, left), keyed_pose(animation, name, right)
    shortest = shortest_flags(animation, name, left)
    pose = {}
    for key in start:
        a, b = start[key], end.get(key, start[key])
        pose[key] = a + (((b - a + 180) % 360) - 180 if key in shortest else b - a) * t
    return pose


def shortest_flags(animation, name, frame):
    keyframes = [keyframe for keyframe in animation["keyframes"] if name in keyframe["elements"]]
    if not keyframes:
        return set()
    own = max((keyframe for keyframe in keyframes if keyframe["frame"] <= frame), key=lambda keyframe: keyframe["frame"], default=keyframes[-1])
    return {"rotation" + axis for axis in AXES if own["elements"][name].get("rotShortestDistance" + axis)}


def keyed_pose(animation, name, frame):
    keyframes = animation["keyframes"]
    pose = {}
    for keys in (OFFSET, ROTATION, STRETCH):
        setting = [index for index, keyframe in enumerate(keyframes) if any(key in keyframe["elements"].get(name, {}) for key in keys)]
        if not setting:
            continue
        right = next((index for index in setting if keyframes[index]["frame"] > frame), setting[0])
        left = setting[setting.index(right) - 1]
        before, after = keyframes[left], keyframes[right]
        if left == right:
            t = 0
        else:
            t = ((frame - before["frame"]) % animation["quantityframes"]) / ((after["frame"] - before["frame"]) % animation["quantityframes"])
        for key in keys:
            start = float(before["elements"][name].get(key, 0))
            end = float(after["elements"][name].get(key, 0))
            pose[key] = start + (end - start) * t
    return pose


def local(element, pose, version):
    origin = Vector([float(value) for value in element.get("rotationOrigin", (0, 0, 0))]) / 16
    shift = Vector([float(value) for value in element["from"]]) / 16 + Vector([pose.get(key, 0) for key in OFFSET]) / 16 - origin
    scale = Vector([float(element.get("scale" + axis, 1)) for axis in AXES])
    stretch = Vector([pose.get(key, 1) for key in STRETCH])
    if version == 1:
        return (
            Matrix.Translation(origin) @ scaling(scale) @ game_rotation(element).to_4x4()
            @ Matrix.Translation(shift) @ scaling(stretch) @ game_rotation(pose).to_4x4()
        )
    summed = {key: float(element.get(key, 0)) + pose.get(key, 0) for key in ROTATION}
    return Matrix.Translation(origin) @ game_rotation(summed).to_4x4() @ scaling(scale * stretch) @ Matrix.Translation(shift)


def game_rotation(values):
    x, y, z = (math.radians(float(values.get(key, 0))) for key in ROTATION)
    return Matrix.Rotation(x, 3, "X") @ Matrix.Rotation(y, 3, "Y") @ Matrix.Rotation(z, 3, "Z")


def scaling(vector):
    return Matrix.Diagonal((*vector, 1))


def difference(a, b):
    chord = math.sqrt(sum((a[row][column] - b[row][column]) ** 2 for row in range(3) for column in range(3)))
    degrees = math.degrees(2 * math.asin(min(1.0, chord / (2 * math.sqrt(2)))))
    units = 16 * max(abs(a[row][3] - b[row][3]) for row in range(3))
    return degrees, units
