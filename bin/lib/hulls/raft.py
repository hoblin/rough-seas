import os
import sys
from pathlib import Path
from types import SimpleNamespace

import json5

from held import PADDLE, paddling, poling, tools
import units
from jsontext import load
from entity import record, write
from physics import ASSETS, SWIM_LINE_METRES, stands
from shape import bounds, hull_of
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
    record("raft", {"behaviorConfigs": {"roughseas:vesselintegrity": units.both("depth", depth)}})
    length, beam = (units.metres(hull["to"][i] - hull["from"][i]) for i in (0, 2))
    at = seat(shape, SEATED)
    edge = hull["from"][2]
    line = entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]
    pole = poling(stands("raft", units.metres(at[1]), form="rectangle", hull_kg=entity["weight"], waterplane=length * beam,
                         beam_metres=beam, depth_metres=depth),
                  at, units.voxels(line), edge - POLE_OUT, (at[0], at[1], edge + STANCE_WIDTH), STANCE_WIDTH)
    player_patch("player-raft", tools(shape, "raft", SEATED, "roughseas-kneel", {"rotationZ": 10}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, HANDS_ABOVE, edge + 1, bend=BEND, shift=KNEELS_OUT,
                                              middle=(hull["from"][2] + hull["to"][2]) / 2)),
        "pole": pole,
    }, LOGS))


if __name__ == "__main__":
    build(sys.argv[1])
