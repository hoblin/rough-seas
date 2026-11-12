import sys

from arm import basis
from stroke import CENTRE, FIRST_PERSON, Rower, fit, moved, player_patch
from transform import add, angles, sub, unit

CODE = "roughseas-tie"
FRAMES = 40
KNOTS_A_MINUTE = 45
LEAN = {"rotationX": 0, "rotationY": 0, "rotationZ": 5}
IN_VIEW = (-4, 9.5, 0)
HANDS = {"R": "KnotR", "L": "KnotL"}
# Each key: its frame, then the right and the left hand, in the seraph's voxels; it faces -x.
KNOT = [
    (0, (2.5, 15, 6.5), (3, 15, 9.5)),
    (6, (1.5, 17, 8), (3, 15.3, 9.5)),
    (12, (1, 15.5, 10.5), (3, 15, 9.8)),
    (18, (2, 13.5, 9.5), (3, 16, 9.5)),
    (24, (2.5, 14.5, 7.5), (3, 15.5, 9)),
    (30, (2, 16.5, 5), (2.5, 16.5, 11)),
    (34, (2, 16.5, 5.3), (2.5, 16.5, 10.7)),
]


def at(frame, side):
    keys = [(f, r if side == "R" else l) for f, r, l in KNOT]
    count = len(keys)
    k = max(i for i, (f, _) in enumerate(keys) if f <= frame)
    start, end = keys[k][0], keys[(k + 1) % count][0] + (FRAMES if k + 1 == count else 0)
    t = (frame - start) / (end - start)
    p0, p1, p2, p3 = (keys[(k + i) % count][1] for i in (-1, 0, 1, 2))
    return tuple(0.5 * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t ** 3)
                 for a, b, c, d in zip(p0, p1, p2, p3))


def turned(along):
    e = basis(along, (0, 1, 0))
    return dict(zip(("rotationX", "rotationY", "rotationZ"), angles([[e[j][i] for j in range(3)] + [0] for i in range(3)])))


def tied(shift):
    return lambda frame: pose(*(add(at(frame, side), shift) for side in HANDS))


def pose(right, left):
    return {HANDS["R"]: moved((0, 0, 0), (0, 0, 0), right, turned(rope(left, right))),
            HANDS["L"]: moved((0, 0, 0), (0, 0, 0), left, turned((0, 0, -1)))}


def rope(to, hand):
    """Where the working end leaves the right fist: toward the left, which holds the standing part across the body."""
    return add(unit(sub(to, hand)), (0, 0, 1))


def build(_):
    rower = Rower(CENTRE, "roughseas-stand")
    knots = [{"name": name, "from": [0, 0, 0], "to": [0, 0, 0], "faces": {}} for name in HANDS.values()]
    ties = [{"code": code, "frames": FRAMES, "keys": list(range(0, FRAMES, 2)), "pose": tied(shift),
             "speed": round(FRAMES * KNOTS_A_MINUTE / 60 / 30, 4)}
            for code, shift in ((CODE, (0, 0, 0)), (CODE + FIRST_PERSON, IN_VIEW))]
    _, player = fit(rower, knots, ties, HANDS, LEAN)
    player_patch("player-tying", player, blended=set())


if __name__ == "__main__":
    build(sys.argv[1] if len(sys.argv) > 1 else None)
