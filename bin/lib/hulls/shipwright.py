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

# The drakkar's shape nests its elements deeper than json5's parser recurses by default.
DEEPEST_SHAPE = 20000
sys.setrecursionlimit(DEEPEST_SHAPE)

VERSION = load(ASSETS / "config/compat/shipwright.json")["tested"]["to"]
RELEASES = {"1.5.1": "https://moddbcdn.vintagestory.at/shipwright_1.5.1_bcdbee9834481e92d58ac1d17107c4e2.zip?dl=shipwright_1.5.1.zip"}
PATCH = ASSETS / "patches/compat-shipwright.json"
CACHE = Path(os.environ.get("BENCH_TMP", "/tmp")) / f"shipwright-{VERSION}.zip"
HIDDEN = re.compile(r"SailUnfurled|SailHalf|ExtraSailUnfurled|ExtraSailHalf|Collision|hideWater")
# Sampling a hold every half voxel takes minutes on a hull past ten metres; a voxel there is fine enough.
LONG_HULL_VOXELS = 160
FINE_STEP_VOXELS = 0.5
COARSE_STEP_VOXELS = 1


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
        urllib.request.urlretrieve(RELEASES[VERSION], CACHE)
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
        step = COARSE_STEP_VOXELS if to[0] - frm[0] > LONG_HULL_VOXELS else FINE_STEP_VOXELS
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
