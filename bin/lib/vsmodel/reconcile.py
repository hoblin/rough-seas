import math

from pose import same_pose

EPSILON = 1e-3
DECIMALS = 4
GEOMETRY = ("from", "to", "rotationOrigin")
ROTATION = ("rotationX", "rotationY", "rotationZ")
PLACEHOLDER = "#0"
DISABLED = "vs_disabled"
CORNERS = ((1, 0), (1, 1), (0, 1), (0, 0))
AUTHORED = {"name", "faces", "children", "attachmentpoints", "group", *GEOMETRY, *ROTATION}


def reconcile(original, exported):
    exported = tidy(exported)
    result = {key: original[key] for key in original if key not in ("textures", "elements")}
    result["textures"] = {**original.get("textures", {}), **new_textures(original, exported)}
    if "textureSizes" in exported and "textureSizes" not in original:
        result.pop("textureSizes", None)
    result["elements"] = elements(original.get("elements", []), exported.get("elements", []))
    if exported.get("animations"):
        before = {animation["code"]: animation for animation in original.get("animations", [])}
        order = {code: index for index, code in enumerate(before)}
        merged = [
            before[each["code"]] if each["code"] in before and same_pose(original, before[each["code"]], {"elements": result["elements"]}, each) else each
            for each in exported["animations"]
        ]
        result["animations"] = sorted(merged, key=lambda each: order.get(each["code"], len(order)))
    if "animations" in result:
        result["animations"] = without_lost(result["animations"], {element["name"] for element in walk(result["elements"])})
    return {key: result[key] for key in [*original, *exported] if key in result}


def new_textures(original, exported):
    known = original.get("textures", {})
    return {code: path for code, path in exported.get("textures", {}).items() if code not in known}


def elements(originals, exported):
    by_name = {element["name"]: element for element in originals}
    order = {element["name"]: index for index, element in enumerate(originals)}
    merged = [element(by_name.get(each["name"]), each) for each in exported]
    return sorted(merged, key=lambda each: order.get(each["name"], len(order)))


def element(original, exported):
    if original is None:
        return fresh(exported)
    result = dict(original)
    for key in GEOMETRY:
        if key in exported and not close(original.get(key, [0, 0, 0]), exported[key]):
            result[key] = exported[key]
    if not same_rotation(original, exported):
        for key in ROTATION:
            result.pop(key, None)
            if exported.get(key):
                result[key] = exported[key]
    if "faces" in original and not flat(result):
        result["faces"] = faces(original["faces"], exported.get("faces", {}), with_engine_uvs(original)["faces"])
    points = attachment_points(original.get("attachmentpoints", []), exported.get("attachmentpoints", []))
    if points or "attachmentpoints" in original:
        result["attachmentpoints"] = points
    children = elements(original.get("children", []), exported.get("children", []))
    if children or "children" in original:
        result["children"] = children
    for key, value in exported.items():
        if key not in result and key not in AUTHORED:
            result[key] = value
    return result


def flat(element):
    return any(abs(float(a) - float(b)) < EPSILON for a, b in zip(element["from"], element["to"]))


def fresh(exported):
    result = {key: value for key, value in exported.items() if key not in ("group", "children")}
    result["faces"] = {side: new_face(face) for side, face in exported.get("faces", {}).items()}
    if exported.get("children"):
        result["children"] = [fresh(child) for child in exported["children"]]
    return result


def new_face(face):
    result = {key: value for key, value in face.items() if key != "autoUv"}
    if face.get("texture") == "#" + DISABLED:
        result["texture"] = "#null"
    return result


def tidy(value):
    if isinstance(value, dict):
        return {key: tidy(each) for key, each in value.items()}
    if isinstance(value, list):
        return [tidy(each) for each in value]
    if isinstance(value, float):
        return round(value, DECIMALS) + 0.0
    return value


def faces(originals, exported, drawn):
    result = {}
    for side, original in originals.items():
        face = exported.get(side)
        result[side] = original if face is None or face.get("texture") == PLACEHOLDER else merged_face(original, face, drawn[side])
    return result


def merged_face(original, exported, drawn):
    if exported.get("texture") == "#" + DISABLED:
        return {**original, "enabled": False}
    result = {key: value for key, value in original.items() if key != "enabled"}
    if exported.get("texture") != "#null":
        result["texture"] = exported["texture"]
    if not same_mapping(drawn, exported):
        result["uv"] = exported["uv"]
        result.pop("rotation", None)
        if exported.get("rotation"):
            result["rotation"] = exported["rotation"]
    return result


def attachment_points(originals, exported):
    by_code = {point["code"]: point for point in originals}
    result = []
    for point in exported:
        original = by_code.get(point["code"])
        if original and same_point(original, point):
            result.append(original)
        else:
            result.append(point)
    order = {point["code"]: index for index, point in enumerate(originals)}
    return sorted(result, key=lambda point: order.get(point["code"], len(order)))


def same_point(a, b):
    position = ("posX", "posY", "posZ")
    return close([a.get(key, 0) for key in position], [b.get(key, 0) for key in position]) and all(
        close_angle(a.get(key, 0), b.get(key, 0)) for key in ROTATION
    )


def without_lost(animations, names):
    return [
        {**animation, "keyframes": [{**frame, "elements": {name: pose for name, pose in frame["elements"].items() if name in names}} for frame in animation["keyframes"]]}
        for animation in animations
    ]


def walk(elements):
    for element in elements:
        yield element
        yield from walk(element.get("children", []))


def close(a, b):
    return len(a) == len(b) and all(abs(float(x) - float(y)) < EPSILON for x, y in zip(a, b))


def same_mapping(a, b):
    return "uv" in b and close(corners(a), corners(b))


def with_engine_uvs(element):
    size = [abs(float(b) - float(a)) for a, b in zip(element["from"], element["to"])]
    spans = {"north": (0, 1), "south": (0, 1), "east": (2, 1), "west": (2, 1), "up": (0, 2), "down": (0, 2)}
    faces = {side: face if "uv" in face else {**face, "uv": [0, 0, size[spans[side][0]], size[spans[side][1]]]} for side, face in element.get("faces", {}).items()}
    return {**element, "faces": faces, "children": [with_engine_uvs(child) for child in element.get("children", [])]}


def corners(face):
    u1, v1, u2, v2 = (float(each) for each in face["uv"])
    turn = int(face.get("rotation", 0)) // 90
    return [value for i in range(4) for value in corner(u1, v1, u2, v2, CORNERS[(turn + i) % 4])]


def corner(u1, v1, u2, v2, template):
    return u1 + (u2 - u1) * template[0], v2 + (v1 - v2) * template[1]


def close_angle(a, b):
    return a is not None and abs((float(a) - float(b) + 180) % 360 - 180) < EPSILON


def same_rotation(a, b):
    ma, mb = rotation_matrix(a), rotation_matrix(b)
    return all(abs(x - y) < EPSILON for row_a, row_b in zip(ma, mb) for x, y in zip(row_a, row_b))


def rotation_matrix(element):
    x, y, z = (math.radians(float(element.get(key, 0))) for key in ROTATION)
    rx = [[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]]
    ry = [[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]
    rz = [[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]]
    return multiply(multiply(rx, ry), rz)


def multiply(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
