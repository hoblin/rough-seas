import os
import re
import sys
from pathlib import Path

import json5

import units
from entity import record, write
from fit import corners, posed
from jsontext import load
from physics import ASSETS, afloat
from shape import bounds, hull_of, windage
from transform import depth

VANILLA = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/survival"
SHAPE = VANILLA / "shapes/entity/nonliving/boat/sailed.json"
ENTITY = VANILLA / "entities/nonliving/boat-sailed.json"
HULL = re.compile(r"Hull|KeelBeam")
PLANKING = re.compile(r"Hull|KeelBeam|Floor|ProwFloor|Gunwhale")
MAST = re.compile(r"Mast")
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
                      at_draft=windage(rigged, frm[1]))
    mast = bounds(*kept(elements, MAST))[1][1]
    measured["behaviorConfigs"]["roughseas:seaforces"] |= units.both("mast", units.metres(mast))
    record("sailed", {"behaviorConfigs": measured["behaviorConfigs"]})


if __name__ == "__main__":
    build(sys.argv[1])
