import os
import sys
from pathlib import Path
from types import SimpleNamespace

import json5

from held import ARM_REACH_METRES, worked_from_middle, PADDLE, paddling, poling, tools
import units
from jsontext import load
from entity import record, write
from physics import ASSETS, MATERIALS, SWIM_LINE_METRES, afloat, stands
from fit import posed
from shape import bounds, hull_of, windage
from transform import apply
from stroke import player_patch, seat

VANILLA = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/survival"
SHAPES = VANILLA / "shapes/entity/nonliving/boat"
ENTITY = VANILLA / "entities/nonliving/boat-raft.json"
VARIANTS = ("raft", "raft-bamboo")
SEATED = "BackSeat"
LOGS = ("origin",)
STANCE_WIDTH = 4
HANDS_ABOVE = 4
BEND = 15
KNEELS_OUT = 5.75
POLE_OUT = 3


def logs(elements):
    """Her logs alone, as they lie in her shape: what a knock meets and the water holds up."""
    return [dict(element, children=logs(element.get("children", []))) for element in elements
            if element["name"] in LOGS or element["name"].startswith("log")]


def oak_litres(kilograms):
    """Her logs' volume: her weight, as vanilla gives it, in oak."""
    return kilograms / MATERIALS["wood-oak"]["density"] * 1000


def lashed(elements):
    """The longest stretch of log between two of her lashings: a breaker bends a log there."""
    along = sorted(centre[0] for centre in (centre_of(element, m) for element, m in posed(elements) if element["name"].startswith("Tie")))
    return units.metres(max(b - a for a, b in zip(along, along[1:])))


def centre_of(element, m):
    return [units.voxels(c) for c in apply(m, [units.metres((t - f) / 2) for f, t in zip(element["from"], element["to"])])]


def measure(variant):
    """Her Hull box round her logs, into the patch that adds it to her shape."""
    box, _ = hull_of(*bounds(*logs(json5.loads((SHAPES / f"{variant}.json").read_text())["elements"])))
    path = ASSETS / "patches/vessels-hull-boxes.json"
    at = next(n for n, op in enumerate(load(path)) if op["file"].endswith(f"boat/{variant}.json"))
    write(path, [at, "value"], box)
    return box


def build(_):
    vanilla = json5.loads((SHAPES / "raft.json").read_text())
    entity = json5.loads(ENTITY.read_text())
    shape = SimpleNamespace(elements=vanilla["elements"], path=SHAPES / "raft.json",
                            textures={code: f"game:{path}" for code, path in vanilla["textures"].items()})
    hull, _ = (measure(variant) for variant in VARIANTS)
    log = next(element for element in logs(vanilla["elements"])[0]["children"])
    depth = units.metres(log["to"][1] - log["from"][1])
    shell = {"metres": depth, "voxels": log["to"][1] - log["from"][1], **units.both("span", lashed(vanilla["elements"]))}
    record("raft", {"behaviorConfigs": {"roughseas:vesselintegrity": {**units.both("depth", depth), "shell": shell}}})
    logs_litres = oak_litres(entity["weight"])
    seen = afloat("raft", entity["weight"], depth, solid_litres=logs_litres, hitbox_height_metres=entity["hitboxSize"]["y"],
                  at_draft=windage(logs(vanilla["elements"]), hull["from"][1]))
    record("raft", {"behaviorConfigs": {"roughseas:seaforces": seen["behaviorConfigs"]["roughseas:seaforces"]}})
    length, beam = (units.metres(hull["to"][i] - hull["from"][i]) for i in (0, 2))
    at = seat(shape, SEATED)
    record("raft", {"behaviorConfigs": {"roughseas:propulsion": worked_from_middle(hull, at, ARM_REACH_METRES)}})
    edge = hull["from"][2]
    water_metres = entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]
    pole = poling(stands("raft", units.metres(at[1]), form="rectangle", hull_kg=entity["weight"], waterplane_square_metres=length * beam,
                         beam_metres=beam, depth_metres=depth),
                  at, units.voxels(water_metres), edge - POLE_OUT, (at[0], at[1], edge + STANCE_WIDTH), STANCE_WIDTH)
    player_patch("player-raft", tools(shape, "raft", SEATED, "roughseas-kneel", {"rotationZ": 10}, water_metres, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, HANDS_ABOVE, edge + 1, bend=BEND, shift=KNEELS_OUT,
                                              middle=(hull["from"][2] + hull["to"][2]) / 2)),
        "pole": pole,
    }, LOGS))


if __name__ == "__main__":
    build(sys.argv[1])
