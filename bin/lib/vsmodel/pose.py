import math

AXES = "XYZ"
FLAGS = (("offsetX", "offsetY", "offsetZ"), ("rotationX", "rotationY", "rotationZ"), ("stretchX", "stretchY", "stretchZ"))
TOLERANCE = (0.1, 0.02)  # degrees, 1/16 block


def same_pose(shape, animation, other_shape, other):
    degrees, units, _ = animation_error(shape, animation, other_shape, other)
    return degrees < TOLERANCE[0] and units < TOLERANCE[1]


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
            rest[element["name"]] = multiply(rest_parent, local(element, {}, 0))
            posed[element["name"]] = multiply(posed_parent, local(element, element_pose(animation, element["name"], frame), version))
            descend(element.get("children", []), rest[element["name"]], posed[element["name"]])

    descend(shape["elements"], identity(), identity())
    return {name: multiply(posed[name], invert(rest[name])) for name in rest}


def element_pose(animation, name, frame):
    resolved = sorted({keyframe["frame"] for keyframe in animation["keyframes"]})
    if frame in resolved:
        return keyed_pose(animation, name, frame)
    left = max((each for each in resolved if each < frame), default=resolved[-1])
    right = min((each for each in resolved if each > frame), default=resolved[0])
    length = animation["quantityframes"]
    span = (right - left) % length or length
    t = ((frame - left) % length) / span
    start, end = keyed_pose(animation, name, left), keyed_pose(animation, name, right)
    shortest = shortest_flags(animation, name, left)
    pose = {}
    for key in start:
        a, b = start[key], end.get(key, start[key])
        step = ((b - a + 180) % 360) - 180 if key in shortest else b - a
        pose[key] = a + step * t
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
    for flag, keys in enumerate(FLAGS):
        setting = [index for index, keyframe in enumerate(keyframes) if any(key in keyframe["elements"].get(name, {}) for key in keys)]
        if not setting:
            continue
        right = next((index for index in setting if keyframes[index]["frame"] > frame), setting[0])
        left = setting[setting.index(right) - 1]
        before, after = keyframes[left], keyframes[right]
        if left == right:
            t = 0
        elif after["frame"] < before["frame"]:
            t = ((frame - before["frame"]) % animation["quantityframes"]) / (after["frame"] + animation["quantityframes"] - before["frame"])
        else:
            t = (frame - before["frame"]) / (after["frame"] - before["frame"])
        for key in keys:
            start = float(before["elements"][name].get(key, 0))
            end = float(after["elements"][name].get(key, 0))
            pose[key] = start + (end - start) * t
    return pose


def local(element, pose, version):
    origin = [float(value) / 16 for value in element.get("rotationOrigin", [0, 0, 0])]
    corner = [float(value) / 16 for value in element["from"]]
    offset = [pose.get("offset" + axis, 0) / 16 for axis in AXES]
    scale = [float(element.get("scale" + axis, 1)) for axis in AXES]
    stretch = [pose.get("stretch" + axis, 1) for axis in AXES]
    rotation = [float(element.get("rotation" + axis, 0)) for axis in AXES]
    turn = [pose.get("rotation" + axis, 0) for axis in AXES]
    shift = [c + o - r for c, o, r in zip(corner, offset, origin)]
    if version == 1:
        return chain(translation(origin), scaling(scale), rotating(rotation), translation(shift), scaling(stretch), rotating(turn))
    return chain(translation(origin), rotating([r + t for r, t in zip(rotation, turn)]), scaling([s * t for s, t in zip(scale, stretch)]), translation(shift))


def difference(a, b):
    chord = math.sqrt(sum((a[row][column] - b[row][column]) ** 2 for row in range(3) for column in range(3)))
    degrees = math.degrees(2 * math.asin(min(1.0, chord / (2 * math.sqrt(2)))))
    units = 16 * max(abs(a[row][3] - b[row][3]) for row in range(3))
    return degrees, units


def chain(*matrices):
    result = identity()
    for matrix in matrices:
        result = multiply(result, matrix)
    return result


def identity():
    return [[float(row == column) for column in range(4)] for row in range(4)]


def translation(vector):
    matrix = identity()
    for row in range(3):
        matrix[row][3] = vector[row]
    return matrix


def scaling(vector):
    matrix = identity()
    for row in range(3):
        matrix[row][row] = vector[row]
    return matrix


def rotating(degrees):
    x, y, z = (math.radians(value) for value in degrees)
    rx = [[1, 0, 0, 0], [0, math.cos(x), -math.sin(x), 0], [0, math.sin(x), math.cos(x), 0], [0, 0, 0, 1]]
    ry = [[math.cos(y), 0, math.sin(y), 0], [0, 1, 0, 0], [-math.sin(y), 0, math.cos(y), 0], [0, 0, 0, 1]]
    rz = [[math.cos(z), -math.sin(z), 0, 0], [math.sin(z), math.cos(z), 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]]
    return chain(rx, ry, rz)


def multiply(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def invert(matrix):
    linear = [row[:3] for row in matrix[:3]]
    inverse = inverse3(linear)
    move = [-sum(inverse[row][k] * matrix[k][3] for k in range(3)) for row in range(3)]
    return [inverse[row] + [move[row]] for row in range(3)] + [[0.0, 0.0, 0.0, 1.0]]


def inverse3(m):
    determinant = (
        m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
        - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
        + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0])
    )
    cofactors = [
        [m[1][1] * m[2][2] - m[1][2] * m[2][1], m[0][2] * m[2][1] - m[0][1] * m[2][2], m[0][1] * m[1][2] - m[0][2] * m[1][1]],
        [m[1][2] * m[2][0] - m[1][0] * m[2][2], m[0][0] * m[2][2] - m[0][2] * m[2][0], m[0][2] * m[1][0] - m[0][0] * m[1][2]],
        [m[1][0] * m[2][1] - m[1][1] * m[2][0], m[0][1] * m[2][0] - m[0][0] * m[2][1], m[0][0] * m[1][1] - m[0][1] * m[1][0]],
    ]
    return [[value / determinant for value in row] for row in cofactors]
