import math
from pathlib import Path

from physics import MATERIALS
from shape import ROUNDED_SHARE, VOXEL_METRES_CUBED_LITRES, Shape, beam, box, group, path, stages, write

VOXEL_METRES = 1 / 16
ROPE_METRES = 10
KNOT_METRES = 0.3

BODY = (-8, 24)
CENTRE = 8
SIDES = (("Port", -1), ("Starboard", 1))
BUNDLE_REEDS = 144
REED_KG = 0.15

TIE_PITCH = 4
TIE_TURNS = 2
LASHINGS = (-6, 1, 8, 15, 22)
LASHING_TURNS = 2
TIP_SHARES = (1, 0.8125, 0.5625, 0.34375)
TIP_REACH = (0, 3, 6, 8)


def drawn_girth():
    litres = BUNDLE_REEDS * REED_KG / MATERIALS["reed"]["density"] * 1000
    length = BODY[1] - BODY[0] + sum((b - a) * share ** 2 for a, b, share in zip(TIP_REACH, TIP_REACH[1:], TIP_SHARES))
    return math.sqrt(litres / (ROUNDED_SHARE * length * VOXEL_METRES_CUBED_LITRES))


GIRTH = drawn_girth()
TIP_GIRTHS = tuple(GIRTH * share for share in TIP_SHARES)
TIP_RISE = (0, 1.2, 3.4, 6.4)
TIP_TURN_PITCH = 1
DROOPS = 4


def bundle_z(side):
    return CENTRE + side * GIRTH / 2


def bundle(name, side):
    z = bundle_z(side)
    ties = [f / (BODY[1] - BODY[0]) for f in range(TIE_PITCH // 2, BODY[1] - BODY[0], TIE_PITCH)]
    return [beam(f"{name}Body", (BODY[0], GIRTH / 2, z), (BODY[1], GIRTH / 2, z), GIRTH, GIRTH, "reed",
                 ties=ties, tie=(0.5, 0.15), tie_tex="rope", rounded=True, overlap=0)]


def flat_end(name, side, start, direction):
    z = bundle_z(side)
    points = [(start + direction * reach, girth / 2, z) for reach, girth in zip(TIP_REACH, TIP_GIRTHS)]
    return path(name, points, lambda i: TIP_GIRTHS[i - 1], lambda i: TIP_GIRTHS[i - 1], "reed", rounded=True, overlap=0)


def raised_end(name, start, direction, droop):
    lift = 1 - droop / (DROOPS - 1)
    points = [(start + direction * reach, TIP_GIRTHS[0] / 2 + rise * lift, CENTRE) for reach, rise in zip(TIP_REACH, TIP_RISE)]
    segments = []
    for i, (a, b) in enumerate(zip(points, points[1:]), 1):
        turns = math.dist(a, b) / TIP_TURN_PITCH
        ties = [(t + 0.5) / turns for t in range(math.floor(turns))]
        segments.append(beam(f"{name}{i}", a, b, 2 * TIP_GIRTHS[i - 1], TIP_GIRTHS[i - 1], "reed",
                             ties=ties, tie=(0.3, 0.15), tie_tex="lashing", rounded=True, overlap=0.1))
    return segments


def lashing(name, x):
    return [box(name, (x - 0.5, -0.2, CENTRE - GIRTH - 0.2), (x + 0.5, GIRTH + 0.2, CENTRE + GIRTH + 0.2), "lashing", grain="z")]


def ropes_of(metres):
    return math.ceil(metres / ROPE_METRES)


def bundle_ropes():
    ties = len(range(TIE_PITCH // 2, BODY[1] - BODY[0], TIE_PITCH))
    metres = ties * (TIE_TURNS * math.pi * GIRTH * VOXEL_METRES + KNOT_METRES)
    print(f"bundle: {ties} ties {TIE_PITCH * VOXEL_METRES * 100:.0f} cm apart, {metres:.1f} m of cord, {ropes_of(metres)} ropes")
    return ropes_of(metres)


def lashings():
    around = (2 * GIRTH + math.pi * GIRTH) * VOXEL_METRES
    each = LASHING_TURNS * around + KNOT_METRES
    total = each * len(LASHINGS)
    ropes = ropes_of(total)
    order = sorted(LASHINGS, key=lambda x: abs(x - CENTRE))
    per_rope = [[] for _ in range(ropes)]
    for n, x in enumerate(order):
        per_rope[n * ropes // len(order)] += lashing(f"Lash{n + 1}", x)
    print(f"lashing: {len(LASHINGS)} lashings of {LASHING_TURNS} turns, {total:.1f} m of cord, {ropes} ropes")
    return per_rope


def tips():
    points = list(zip(TIP_REACH, TIP_RISE))
    turns = [math.dist(a, b) / TIP_TURN_PITCH for a, b in zip(points, points[1:])]
    metres = sum(n * (2 * g + math.pi * g) for n, g in zip(turns, TIP_GIRTHS)) * VOXEL_METRES + KNOT_METRES
    turns = sum(turns)
    assert ropes_of(metres) == 1, "an end takes more than one rope"
    print(f"ends: {turns:.0f} turns bind each end up, {metres:.1f} m of cord, one rope an end")
    ends = []
    for n, (start, direction) in enumerate(((BODY[0], -1), (BODY[1], 1)), 1):
        ends.append([group(f"Tips{n}Droop{k}", (0, 0, 0), (0, 0, 0), raised_end(f"Tip{n}Droop{k}x", start, direction, k))
                     for k in range(DROOPS)])
    return ends


def flats(sides):
    return [[element for name, side in sides for element in flat_end(f"Flat{name}{n}x", side, start, direction)]
            for n, (start, direction) in enumerate(((BODY[0], -1), (BODY[1], 1)), 1)]


def laid(sides):
    return [element for name, side in sides for element in bundle(name, side)] + [e for end in flats(sides) for e in end]


def envelope():
    body = GIRTH ** 2 * (BODY[1] - BODY[0])
    ends = sum((b - a) * g ** 2 for a, b, g in zip(TIP_REACH, TIP_REACH[1:], TIP_GIRTHS))
    litres = ROUNDED_SHARE * (body + ends) * VOXEL_METRES_CUBED_LITRES
    print(f"a bundle of {BUNDLE_REEDS} reeds: {(BODY[1] - BODY[0] + 2 * TIP_REACH[-1]) * VOXEL_METRES:.1f} m long, "
          f"{GIRTH * VOXEL_METRES * 100:.1f} cm across, {litres:.1f} L as drawn; two: {2 * litres:.1f} L")


def block(target, sides):
    write(target, {"textureWidth": 16, "textureHeight": 16,
                   "textures": {"reed": "roughseas:entity/vessel/reed", "rope": "game:block/cloth/reedrope"},
                   "elements": laid(sides)})


def build(target):
    shape = Shape(target, {
        "reed": "roughseas:entity/vessel/reed",
        "rope": "game:block/cloth/reedrope",
        "lashing": "game:item/resource/rope",
    })
    envelope()
    bundle_ropes()
    shape.add(group("Blank", (0, 0, 0), (0, 0, 0), [e for name, side in SIDES for e in bundle(name, side)]))
    shape.add(stages("Flat", flats(SIDES)))
    shape.add(stages("Lashing", lashings()))
    shape.add(stages("Tips", tips()))
    shape.keep("Hull", "FrontSeat", "Cargo")
    shape.write()
    blocks = Path(target).parents[2] / "block"
    block(blocks / "reedbundle-one.json", SIDES[:1])
    block(blocks / "reedbundle-two.json", SIDES)


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
