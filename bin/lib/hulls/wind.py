import units
from shape import ORIGIN, num, profile

STEP = 0.5
WINDAGE = "roughseas:windage"


def along(x):
    return units.metres(x - ORIGIN[0])


def across(z):
    return units.metres(z - ORIGIN[2])


def point(name, values):
    return {name + "Metres": [num(units.metres(v)) for v in values], name + "Voxels": [num(v) for v in values]}


def centre(cells):
    return tuple((sum(cell[axis] for cell in cells) / len(cells) + 0.5) * STEP for axis in range(2))


def area(cells):
    return units.metres(len(cells) * STEP * STEP, 2)


def windage(above, below, keel_voxels):
    def at(draft_metres):
        waterline = keel_voxels + units.voxels(draft_metres)
        side, front, under = profile(above, low=waterline), profile(above, low=waterline, across=2), profile(below, high=waterline)
        x, y = centre(side)
        return {"behaviorConfigs": {WINDAGE: {
            **units.both("windage", area(side), 2), **units.both("front", area(front), 2),
            **point("windageCentre", (x - ORIGIN[0], y)), **units.both("lateralCentre", along(centre(under)[0]))}}}
    return at


def polygon(points):
    twice = sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))
    cx = sum((a[0] + b[0]) * (a[0] * b[1] - b[0] * a[1]) for a, b in zip(points, points[1:] + points[:1])) / (3 * twice)
    cy = sum((a[1] + b[1]) * (a[0] * b[1] - b[0] * a[1]) for a, b in zip(points, points[1:] + points[:1])) / (3 * twice)
    return abs(twice) / 2, (cx, cy)


def corner(name, at):
    x, y, z = at
    return point(name, (x - ORIGIN[0], y, z - ORIGIN[2]))


def bilinear(corners, reach, span):
    luff_foot, luff_head, leech_head, leech_foot = corners
    foot = [a + (b - a) * reach for a, b in zip(luff_foot, leech_foot)]
    head = [a + (b - a) * reach for a, b in zip(luff_head, leech_head)]
    return [f + (h - f) * span for f, h in zip(foot, head)]


def cloth_area(corners, reach=1, span=1, cells=32):
    total = 0
    for i in range(cells):
        for j in range(cells):
            at = lambda s, t: bilinear(corners, reach * s / cells, 1 - span + span * t / cells)
            a, b, c, d = at(i, j), at(i + 1, j), at(i + 1, j + 1), at(i, j + 1)
            u, v = [p - q for p, q in zip(c, a)], [p - q for p, q in zip(d, b)]
            total += sum(x * x for x in (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])) ** 0.5 / 2
    return units.metres(total, 2)


def sail(polar, corners, pivot, half, least_trim_degrees, trim_animation=None):
    half_reach, half_span = half
    names = ("luffFoot", "luffHead", "leechHead", "leechFoot")
    return {"behaviorConfigs": {WINDAGE: {"sail": {
        "polar": polar, **({"trimAnimation": trim_animation} if trim_animation else {}),
        **units.both("sail", cloth_area(corners), 2), **units.both("half", cloth_area(corners, half_reach, half_span), 2),
        "halfReach": round(half_reach, 4), "halfSpan": round(half_span, 4),
        **{k: v for name, at in zip(names, corners) for k, v in corner(name, at).items()}, **corner("pivot", pivot),
        "leastTrimDegrees": round(least_trim_degrees, 1)}}}}
