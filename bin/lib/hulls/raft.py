from types import SimpleNamespace

from held import PADDLE, paddling, poling, tools
from physics import SWIM_LINE_METRES, stands
from shape import VOXELS_A_METRE
from stroke import player_patch, seat

SEAT = (16, 5, 1)
LOGS_TOP = 7
STANCE_WIDTH = 4
EDGE = -7
HITBOX_HEIGHT = 0.6
SWIMMING_OFFSET = 0.45
WEIGHT_KG = 120
SIDE_METRES = 2


def build(_):
    shape = SimpleNamespace(elements=[{"name": "Seat", "from": list(SEAT), "to": list(SEAT), "faces": {},
                                       "attachmentpoints": [{"code": "SeatAP", "posX": "0", "posY": "0", "posZ": "0"}]}])
    at = seat(shape, "Seat")
    out = EDGE - 1
    line = HITBOX_HEIGHT + SWIM_LINE_METRES - SWIMMING_OFFSET
    pole = poling(stands("raft", LOGS_TOP / VOXELS_A_METRE, form="rectangle", hull_kg=WEIGHT_KG, waterplane=SIDE_METRES ** 2,
                         beam_metres=SIDE_METRES, depth_metres=HITBOX_HEIGHT),
                  at, line * VOXELS_A_METRE, out, (at[0], LOGS_TOP, at[2]), STANCE_WIDTH)
    player_patch("player-raft", tools(shape, "raft", "Seat", "sitflooridle", {"rotationZ": 14}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 5, 2, 3, out + 1)),
        "pole": pole,
    }))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
