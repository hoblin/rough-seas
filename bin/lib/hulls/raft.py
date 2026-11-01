from types import SimpleNamespace

from held import PADDLE, POLE, paddling, poling, tools
from physics import SWIM_LINE_METRES
from stroke import player_patch, seat

SEAT = (16, 5, 1)
EDGE = -7
HITBOX_HEIGHT = 0.6
SWIMMING_OFFSET = 0.45


def build(_):
    shape = SimpleNamespace(elements=[{"name": "Seat", "from": list(SEAT), "to": list(SEAT), "faces": {},
                                       "attachmentpoints": [{"code": "SeatAP", "posX": "0", "posY": "0", "posZ": "0"}]}])
    at = seat(shape, "Seat")
    out = EDGE - 1
    player_patch("player-raft", tools(shape, "raft", "Seat", "sitflooridle", {"rotationZ": 14}, HITBOX_HEIGHT + SWIM_LINE_METRES - SWIMMING_OFFSET, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 5, 2, 3, out + 1)),
        "pole": ("pole", POLE, poling(POLE, at, 4, 4, 7, out + 0.5)),
    }))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
