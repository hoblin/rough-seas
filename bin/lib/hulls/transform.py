import math


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def add(a, b):
    return tuple(x + y for x, y in zip(a, b))


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def scale(v, k):
    return tuple(c * k for c in v)


def unit(v):
    return scale(v, 1 / math.sqrt(dot(v, v)))


def identity():
    return [[float(i == j) for j in range(4)] for i in range(4)]


# Every sum here starts from 0 as sum() does, so negative zeros add up to 0.0 and no shape writes -0.0.
def mul(a, b):
    b0, b1, b2, b3 = b
    return [[0 + r[0] * b0[j] + r[1] * b1[j] + r[2] * b2[j] + r[3] * b3[j] for j in range(4)] for r in a]


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


def nearest(turn, previous):
    x, y, z = turn
    options = ([x, y, z], [x + 180, 180 - y, z + 180])
    unwound = ([v + 360 * round((p - v) / 360) for v, p in zip(option, previous)] for option in options)
    return min(unwound, key=lambda option: sum(abs(v - p) for v, p in zip(option, previous)))


def apply(m, p):
    x, y, z = p
    return tuple(0 + r[0] * x + r[1] * y + r[2] * z + r[3] for r in m[:3])


def direct(m, v):
    x, y, z = v
    return tuple(0 + r[0] * x + r[1] * y + r[2] * z for r in m[:3])


def undirect(m, v):
    x, y, z = v
    r0, r1, r2 = m[0], m[1], m[2]
    return (0 + r0[0] * x + r1[0] * y + r2[0] * z, 0 + r0[1] * x + r1[1] * y + r2[1] * z,
            0 + r0[2] * x + r1[2] * y + r2[2] * z)


def inverse(m):
    r = [[m[j][i] for j in range(3)] for i in range(3)]
    t = [-sum(r[i][k] * m[k][3] for k in range(3)) for i in range(3)]
    return [r[0] + [t[0]], r[1] + [t[1]], r[2] + [t[2]], [0, 0, 0, 1]]


def unapply(m, p):
    return undirect(m, (p[0] - m[0][3], p[1] - m[1][3], p[2] - m[2][3]))


def depth(m, size, p):
    x, y, z = unapply(m, p)
    return min(x, size[0] - x, y, size[1] - y, z, size[2] - z)


# The engine's element transform for animations of version 0, as bin/lib/vsmodel/pose.py has it for Blender.
def local(element, pose):
    origin = [c / 16 for c in element.get("rotationOrigin", (0, 0, 0))]
    shift = [(f + pose.get("offset" + a, 0)) / 16 - o for f, a, o in zip(element["from"], "XYZ", origin)]
    m = translation(origin)
    for axis in "XYZ":
        m = mul(m, rotation(axis, element.get("rotation" + axis, 0) + pose.get("rotation" + axis, 0)))
    return mul(m, translation(shift))
