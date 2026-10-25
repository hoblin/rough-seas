import math


def identity():
    return [[float(i == j) for j in range(4)] for i in range(4)]


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def translation(v):
    m = identity()
    for i in range(3):
        m[i][3] = v[i]
    return m


def rotation(axis, degrees):
    c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
    i, j = [(1, 2), (2, 0), (0, 1)]["XYZ".index(axis)]
    m = identity()
    m[i][i], m[i][j], m[j][i], m[j][j] = c, -s, s, c
    return m


def euler(angles):
    m = identity()
    for axis, degrees in zip("XYZ", angles):
        m = mul(m, rotation(axis, degrees))
    return m


def angles(m):
    return [math.degrees(a) for a in (math.atan2(-m[1][2], m[2][2]), math.asin(max(-1.0, min(1.0, m[0][2]))),
                                      math.atan2(-m[0][1], m[0][0]))]


def apply(m, p):
    return tuple(sum(m[i][k] * c for k, c in enumerate((*p, 1))) for i in range(3))


def direct(m, v):
    return tuple(sum(m[i][k] * c for k, c in enumerate(v)) for i in range(3))


def undirect(m, v):
    return tuple(sum(m[k][i] * c for k, c in enumerate(v)) for i in range(3))


def unapply(m, p):
    return undirect(m, [c - m[i][3] for i, c in enumerate(p)])


# The engine's element transform for animations of version 0, as bin/lib/vsmodel/pose.py has it for Blender.
def local(element, pose):
    origin = [c / 16 for c in element.get("rotationOrigin", (0, 0, 0))]
    shift = [(f + pose.get("offset" + a, 0)) / 16 - o for f, a, o in zip(element["from"], "XYZ", origin)]
    m = translation(origin)
    for axis in "XYZ":
        m = mul(m, rotation(axis, element.get("rotation" + axis, 0) + pose.get("rotation" + axis, 0)))
    return mul(m, translation(shift))
