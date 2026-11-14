import io
import os
import re
import sys
import urllib.request
import zipfile
from dataclasses import dataclass
from pathlib import Path

import json5

import units
from entity import record
from jsontext import load
from physics import ASSETS, afloat
from sailboat import hold, kept, solids
from shape import bounds, hull_of, windage

sys.setrecursionlimit(20000)

COMPAT = load(ASSETS / "config/compat/shipwright.json")
PATCH = ASSETS / "patches/compat-shipwright.json"
CACHE = Path(os.environ.get("BENCH_TMP", "/tmp")) / f"shipwright-{COMPAT['tested']['to']}.zip"
HIDDEN = re.compile(r"SailUnfurled|SailHalf|ExtraSailUnfurled|ExtraSailHalf|Collision|hideWater")
LONG_HULL_VOXELS = 160


@dataclass
class Hull:
    shape: str
    planking: str
    rigged: bool = False
    bundled: bool = False


HULLS = {
    "catboat": Hull("catboat", r"BoardSide|BoatRim|BoatSide|BoatFloor|FloorSpine", rigged=True),
    "coracle": Hull("coracle", r"boatstick|pelt|wattle"),
    "drakkar": Hull("drakkar", r"FlooR|Rim|Hull|Keel|SideFloorPlanks|Sides[LR]", rigged=True),
    "faering": Hull("faering", r"Hull|LHull|RHull|KeelBase|Floor|Joist|Jois", rigged=True),
    "kayak": Hull("kayak", r"BoardSide|BoatRim|BoatSide|BoatFloor"),
    "largejunker": Hull("largejunker", r"Bullkhead|HullUnderSide|Railing|Floor|Bottom|FrontWall", rigged=True),
    "punter": Hull("punter-shallow", r"BoardSide|BoardFloor"),
    "reedlongboat": Hull("reedlongboat", r"Floor", bundled=True),
    "sailedreedboat": Hull("sailedreedboat", r"Floor", rigged=True, bundled=True),
    "totora": Hull("totora", r"Floor", bundled=True),
}


def release():
    if not CACHE.exists():
        urllib.request.urlretrieve(COMPAT["release"], CACHE)
    return zipfile.ZipFile(io.BytesIO(CACHE.read_bytes()))


def asset(zipped, path):
    return json5.loads(zipped.read(f"assets/shipwright/{path}").decode("utf-8-sig"))


def shown(elements):
    """What is drawn of her with her sail furled: her hull, spars and furled sail, none of the boxes that only pick or
    collide."""
    return [dict(element, children=shown(element.get("children", []))) for element in elements if not HIDDEN.match(element["name"])]


def midships(planking, frm, to):
    """Her gunwale amidships: the top of what is drawn of her hull over the middle third of her length, under her stems."""
    third = (to[0] - frm[0]) / 3
    return max(high[1] for _, _, low, high in solids(planking) if frm[0] + third <= (low[0] + high[0]) / 2 <= to[0] - third)


def bundle_litres(planking):
    return units.litres(sum(size[0] * size[1] * size[2] for _, size, _, _ in solids(planking)))


def weight(entity):
    return next(op["value"] for op in load(PATCH) if op["file"] == entity and op["path"] == "/weight")


def measure(vessel, hull, zipped):
    elements = asset(zipped, f"shapes/entity/{hull.shape}.json")["elements"]
    entity = asset(zipped, f"entities/nonliving/{vessel}.json")
    kg = weight(f"shipwright:entities/nonliving/{vessel}.json")
    planking = kept(elements, re.compile(hull.planking))
    frm, to = bounds(*planking)
    gunwale = midships(planking, frm, to)
    _, placed = hull_of(frm, [to[0], gunwale, to[2]])
    integrity = placed["behaviorConfigs"]["roughseas:vesselintegrity"]
    depth = units.metres(gunwale - frm[1])
    above = shown(elements)
    if hull.bundled:
        measured = afloat(vessel, kg, depth, solid_litres=bundle_litres(planking),
                          hitbox_height_metres=entity["hitboxSize"]["y"], at_draft=windage(above, frm[1]))
    else:
        step = 1 if to[0] - frm[0] > LONG_HULL_VOXELS else 0.5
        litres, deck = hold(solids(planking), frm, [to[0], gunwale, to[2]], step)
        measured = afloat(vessel, kg, depth, litres, deck, hitbox_height_metres=entity["hitboxSize"]["y"],
                          at_draft=windage(above, frm[1]))
    configs = measured["behaviorConfigs"]
    configs["roughseas:vesselintegrity"] |= integrity
    configs["roughseas:seaforces"] |= units.both("mast", units.metres(bounds(*above)[1][1]) if hull.rigged else 0)
    record(vessel, {"behaviorConfigs": configs}, entity=f"shipwright:entities/nonliving/{vessel}.json")


def build(_):
    zipped = release()
    for vessel, hull in HULLS.items():
        measure(vessel, hull, zipped)


if __name__ == "__main__":
    build(sys.argv[1])
