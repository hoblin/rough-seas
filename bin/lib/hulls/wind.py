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


def sail(polar, tack, peak, clew, reef, least_trim_degrees, trim_animation):
    full, _ = polygon([p[:2] for p in (tack, peak, clew)])
    return {"behaviorConfigs": {WINDAGE: {"sail": {
        "polar": polar, "trimAnimation": trim_animation, **units.both("sail", units.metres(full, 2), 2), **units.both("half", units.metres(full * reef * reef, 2), 2),
        **corner("tack", tack), **corner("peak", peak), **corner("clew", clew), "leastTrimDegrees": round(least_trim_degrees, 1)}}}}
