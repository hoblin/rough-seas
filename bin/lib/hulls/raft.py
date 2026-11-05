import json
import os
import sys
from pathlib import Path
from types import SimpleNamespace

import json5

from held import PADDLE, paddling, poling, tools
from physics import ASSETS, SWIM_LINE_METRES, stands
from shape import VOXELS_A_METRE
from stroke import player_patch, seat

SHAPE = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/survival/shapes/entity/nonliving/boat/raft.json"
SEATED = "BackSeat"
STANCE_WIDTH = 4
HITBOX_HEIGHT = 0.6
SWIMMING_OFFSET = 0.45
WEIGHT_KG = 120
DEPTH_METRES = 0.25
HANDS_ABOVE = 4
BEND = 15
KNEELS_OUT = 5.75
POLE_OUT = 3


def hull_box():
    boxes = json.loads((ASSETS / "patches/vessels-hull-boxes.json").read_text())
    return next(op["value"] for op in boxes if op["file"].endswith("boat/raft.json"))


def build(_):
    vanilla = json5.loads(SHAPE.read_text())
    shape = SimpleNamespace(elements=vanilla["elements"], path=SHAPE,
                            textures={code: f"game:{path}" for code, path in vanilla["textures"].items()})
    hull = hull_box()
    length, beam = ((hull["to"][i] - hull["from"][i]) / VOXELS_A_METRE for i in (0, 2))
    at = seat(shape, SEATED)
    edge = hull["from"][2]
    line = HITBOX_HEIGHT + SWIM_LINE_METRES - SWIMMING_OFFSET
    pole = poling(stands("raft", at[1] / VOXELS_A_METRE, form="rectangle", hull_kg=WEIGHT_KG, waterplane=length * beam,
                         beam_metres=beam, depth_metres=DEPTH_METRES),
                  at, line * VOXELS_A_METRE, edge - POLE_OUT, (at[0], at[1], edge + STANCE_WIDTH), STANCE_WIDTH)
    player_patch("player-raft", tools(shape, "raft", SEATED, "roughseas-kneel", {"rotationZ": 10}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, HANDS_ABOVE, edge + 1, bend=BEND, shift=KNEELS_OUT,
                                              middle=(hull["from"][2] + hull["to"][2]) / 2)),
        "pole": pole,
    }, ("origin",)))


if __name__ == "__main__":
    build(sys.argv[1])
