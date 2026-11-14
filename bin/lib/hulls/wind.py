import math

import units
from shape import ORIGIN, num, profile
from transform import cross, sub

STEP = 0.5
LINE = 0.3
WINDAGE = "roughseas:windage"


def along(x):
    return units.metres(x - ORIGIN[0])


def point(name, values):
    return {name + "Metres": [num(units.metres(v)) for v in values], name + "Voxels": [num(v) for v in values]}


def centre(cells):
    return tuple((sum(cell[axis] for cell in cells) / len(cells) + 0.5) * STEP for axis in range(2))


def area(cells):
    return units.metres(len(cells) * STEP * STEP, 2)


def windage(standing, furled, below, keel_voxels):
    def bluff(above, waterline):
        side, front = profile(above, low=waterline), profile(above, low=waterline, across=2)
        x, y = centre(side)
        return {**units.both("windage", area(side), 2), **units.both("front", area(front), 2), **point("windageCentre", (x - ORIGIN[0], y))}

    def at(draft_metres):
        waterline = keel_voxels + units.voxels(draft_metres)
        under = profile(below, high=waterline)
        return {"behaviorConfigs": {WINDAGE: {
            **bluff([*standing, *furled], waterline), "set": bluff(standing, waterline),
            **units.both("lateralCentre", along(centre(under)[0]))}}}
    return at


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
            total += math.hypot(*cross(sub(c, a), sub(d, b))) / 2
    return units.metres(total, 2)


def sail(corners, half):
    half_reach, half_span = half
    return {"behaviorConfigs": {WINDAGE: {"sail": {
        **units.both("sail", cloth_area(corners), 2), **units.both("half", cloth_area(corners, half_reach, half_span), 2),
        "halfReach": round(half_reach, 4), "halfSpan": round(half_span, 4)}}}}
