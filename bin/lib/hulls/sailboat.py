import os
import re
import sys
from pathlib import Path

import json5

import units
import wind
from entity import merged, record, write
from fit import corners, posed
from jsontext import load
from physics import ASSETS, afloat
from shape import bounds, hull_of
from transform import depth

VANILLA = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/survival"
SHAPE = VANILLA / "shapes/entity/nonliving/boat/sailed.json"
ENTITY = VANILLA / "entities/nonliving/boat-sailed.json"
HULL = re.compile(r"Hull|KeelBeam")
PLANKING = re.compile(r"Hull|KeelBeam|Floor|ProwFloor|Gunwhale")
MAST = re.compile(r"Mast")
MAST_POLE = re.compile(r"Mast\d$")
SPAR = re.compile(r"Spar[LR]\d$")
CLOTH = re.compile(r"Sail\d+$|SailUnfurled[NS]$")
HALF_CLOTH = re.compile(r"[LR]Sail(Cen|Mid|Out)")
SHEET = re.compile(r"ClewRope[LR]\d$")
BUNT = re.compile(r"SailBund")
BACKSTAY = re.compile(r"MastRope\d$")
ABOVE = re.compile(r"Hull|KeelBeam|Gunwhale|Mast$|MastORIGIN|SailFurled|Furl")
STEP = 0.5


def kept(elements, pattern):
    """The elements whose names match, under the parents that place them, the parents drawn faceless."""
    found = []
    for element in elements:
        children = kept(element.get("children", []), pattern)
        if pattern.match(element["name"]):
            found.append(dict(element, children=children))
        elif children:
            found.append(dict(element, faces={}, children=children))
    return found


def solids(elements):
    found = []
    for element, m in posed(elements):
        points = corners(element, m)
        low, high = ([pick(point[axis] for point in points) for axis in range(3)] for pick in (min, max))
        found.append((m, [units.metres(t - f) for f, t in zip(element["from"], element["to"])], low, high))
    return found


def hold(planking, frm, to):
    """Her hold to the top of her planking, and its opening there: at each station and height, the water from her
    centreline out to her planking on either side, where both sides close it."""
    middle, top = (frm[2] + to[2]) / 2, to[1]
    cubic, square = 0, 0
    x = frm[0] + STEP / 2
    while x < to[0]:
        y = frm[1] + STEP / 2
        while y < top:
            near = [s for s in planking if s[2][0] <= x <= s[3][0] and s[2][1] <= y <= s[3][1]]
            across = [reach(near, x, y, middle, side, frm[2], to[2]) for side in (-1, 1)]
            if None not in across:
                cubic += sum(across) * STEP * STEP
                if y + STEP >= top:
                    square += sum(across) * STEP
            y += STEP
        x += STEP
    return units.litres(units.metres(cubic, 3)), units.metres(square, 2)


def reach(near, x, y, middle, side, low, high):
    z = middle
    while low - 1 <= z <= high + 1:
        if any(lo[2] <= z <= hi[2] and depth(m, size, [units.metres(x), units.metres(y), units.metres(z)]) >= 0 for m, size, lo, hi in near):
            return 0 if z == middle else abs(z - middle)
        z += side * STEP
    return None


def points(elements, pattern):
    return [point for element, m in posed(elements) if pattern.match(element["name"]) for point in corners(element, m)]


def middle(found):
    return tuple(sum(point[axis] for point in found) / len(found) for axis in range(3))


def at_height(found, pick):
    y = pick(point[1] for point in found)
    x, _, z = middle(found)
    return (x, y, z)


def at_side(found, pick):
    z = pick(point[2] for point in found)
    x, y, _ = middle(found)
    return (x, y, z)


def square(elements):
    mast, yard = points(elements, MAST_POLE), points(elements, SPAR)
    cloth = points(elements, CLOTH)
    head, foot = max(point[1] for point in cloth), min(point[1] for point in cloth)
    port, starboard = min(point[2] for point in cloth), max(point[2] for point in cloth)
    along = middle(cloth)[0]
    named = {element["name"]: middle(corners(element, m)) for element, m in posed(elements)}
    backstay = points(elements, BACKSTAY)
    found = {
        "mastHeel": at_height(points(elements, re.compile(r"Mast1$")), min), "masthead": at_height(mast, max),
        "yardPort": at_side(yard, min), "yardStarboard": at_side(yard, max),
        "headPort": (along, head, port), "headStarboard": (along, head, starboard),
        "clewPort": (along, foot, port), "clewStarboard": (along, foot, starboard),
        "leadPort": named["ClewKnotR"], "leadStarboard": named["ClewRopeL"],
        "forestayHead": named["RopeKnot"], "forestayFoot": named["HalyardForeKnot2"],
        "backstayHead": max(backstay, key=lambda point: point[1]), "backstayFoot": named["Cube7"],
    }
    rig = {"kind": "square", **{k: v for name, at in found.items() for k, v in wind.corner(name, at).items()},
           **units.both("spar", units.metres(thinnest(elements, SPAR)), 1), **units.both("pole", units.metres(thinnest(elements, MAST_POLE)), 1),
           **units.both("shore", 0, 1), **units.both("line", units.metres(thinnest(elements, SHEET)), 1),
           **units.both("roll", units.metres(thinnest(elements, BUNT)), 1)}
    corners_set = ((along, foot, port), (along, head, port), (along, head, starboard), (along, foot, starboard))
    reefed = min(point[1] for point in points(elements, HALF_CLOTH))
    return merged(wind.sail("square", corners_set, (1, (head - reefed) / (head - foot))), {"behaviorConfigs": {wind.WINDAGE: {"rig": rig}}})


def thinnest(elements, pattern):
    return min(min(t - f for f, t in zip(element["from"], element["to"])) for element, _ in posed(elements) if pattern.match(element["name"]))


def build(_):
    elements = json5.loads(SHAPE.read_text())["elements"]
    entity = json5.loads(ENTITY.read_text())
    frm, to = bounds(*kept(elements, HULL))
    box, _ = hull_of(frm, to)
    path = ASSETS / "patches/vessels-hull-boxes.json"
    write(path, [next(n for n, op in enumerate(load(path)) if op["file"].endswith("boat/sailed.json")), "value"], box)
    litres, deck = hold(solids(kept(elements, PLANKING)), frm, to)
    rigged = kept(elements, ABOVE)
    measured = afloat("sailboat", entity["weight"], units.metres(to[1] - frm[1]), litres, deck, hitbox_height_metres=entity["hitboxSize"]["y"],
                      at_draft=wind.windage(rigged, kept(elements, HULL), frm[1]))
    mast = bounds(*kept(elements, MAST))[1][1]
    measured["behaviorConfigs"].setdefault("roughseas:seaforces", {}).update(units.both("mast", units.metres(mast)))
    record("sailed", {"behaviorConfigs": measured["behaviorConfigs"]})
    record("sailed", square(elements))


if __name__ == "__main__":
    build(sys.argv[1])
