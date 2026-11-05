import os
import sys
from pathlib import Path
from types import SimpleNamespace

import json5

from held import PADDLE, paddling, poling, tools
import units
from jsontext import load
from physics import ASSETS, SWIM_LINE_METRES, stands
from stroke import player_patch, seat

VANILLA = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/survival"
SHAPE = VANILLA / "shapes/entity/nonliving/boat/raft.json"
ENTITY = VANILLA / "entities/nonliving/boat-raft.json"
SEATED = "BackSeat"
LOGS = ("origin",)
STANCE_WIDTH = 4
HANDS_ABOVE = 4
BEND = 15
KNEELS_OUT = 5.75
POLE_OUT = 3


def patched(patches, file, path=None):
    ops = load(ASSETS / "patches" / patches)
    return next(op["value"] for op in ops if op["file"].endswith(file) and path in (None, op["path"]))


def build(_):
    vanilla = json5.loads(SHAPE.read_text())
    entity = json5.loads(ENTITY.read_text())
    shape = SimpleNamespace(elements=vanilla["elements"], path=SHAPE,
                            textures={code: f"game:{path}" for code, path in vanilla["textures"].items()})
    hull = patched("vessels-hull-boxes.json", "boat/raft.json")
    depth = patched("vessels-vanilla-boats.json", "boat-raft.json", "/behaviorConfigs/roughseas:vesselintegrity")["depthMetres"]
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
