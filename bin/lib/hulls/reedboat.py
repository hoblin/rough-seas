import math
from pathlib import Path

from physics import MATERIALS, ropes, stands
from held import PADDLE, paddling, poling, tools, waterline
from stroke import player_patch, seat
from fit import APART, Tube, Union
from shape import ROUNDED, VOXEL_METRES, VOXEL_METRES_CUBED_LITRES, Shape, beam, box, group, path, section, stages, write

KNOT_METRES = 0.3

BODY = (-8, 24)
ENDS = ((BODY[0], -1), (BODY[1], 1))
CENTRE = 8
SIDES = (("Port", -1), ("Starboard", 1))
BUNDLE_REEDS = 144
REED_KG = 0.15

TIE_PITCH = 4
TIE_TURNS = 2
LASHINGS = (-6, 1, 8, 15, 22)
LASHING_TURNS = 2
TIE_MARGIN = 0.15
TIP_OVERLAP = 0.1
LASHING_MARGIN = 0.2
TIP_SHARES = (1, 0.8125, 0.5625, 0.34375)
TIP_REACH = (0, 3, 6, 8)


def round_girth():
    litres = BUNDLE_REEDS * REED_KG / MATERIALS["reed"]["density"] * 1000
    length = BODY[1] - BODY[0] + 2 * sum((b - a) * share ** 2 for a, b, share in zip(TIP_REACH, TIP_REACH[1:], TIP_SHARES))
    return math.sqrt(litres / (math.pi / 4 * length * VOXEL_METRES_CUBED_LITRES))


GIRTH = round_girth()
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
                 ties=ties, tie=(0.5, TIE_MARGIN), tie_tex="rope", rounded=True, overlap=0)]


def flat_end(name, side, start, direction):
    z = bundle_z(side)
    points = [(start + direction * reach, girth / 2, z) for reach, girth in zip(TIP_REACH, TIP_GIRTHS)]
    return path(name, points, lambda i: TIP_GIRTHS[i - 1], lambda i: TIP_GIRTHS[i - 1], "reed", rounded=True, overlap=0, continues=True)


def raised_points(start, direction, droop):
    """Where a bound end's segments run, the last drawn back by the overlap its beam reaches past its tip."""
    lift = 1 - droop / (DROOPS - 1)
    points = [(start + direction * reach, TIP_GIRTHS[0] / 2 + rise * lift, CENTRE) for reach, rise in zip(TIP_REACH, TIP_RISE)]
    points[-1] = tuple(q - (q - p) * TIP_OVERLAP / math.dist(points[-2], points[-1]) for p, q in zip(points[-2], points[-1]))
    return points


def raised_end(name, start, direction, droop):
    points = raised_points(start, direction, droop)
    segments = []
    for i, (a, b) in enumerate(zip(points, points[1:]), 1):
        turns = math.dist(a, b) / TIP_TURN_PITCH
        ties = [(t + 0.5) / turns for t in range(math.floor(turns))]
        thinner = 2 * APART if i == 1 else 0
        segments.append(beam(f"{name}{i}", a, b, 2 * TIP_GIRTHS[i - 1] - thinner, TIP_GIRTHS[i - 1] - thinner, "reed",
                             ties=ties, tie=(0.3, TIE_MARGIN), tie_tex="lashing", rounded=True, overlap=TIP_OVERLAP))
    return segments


def lashing(name, x):
    """Turns round both bundles: round each of them, and straight across between them."""
    reach = GIRTH / 2 + LASHING_MARGIN
    rounds = [e for label, side in SIDES for e in section(f"{name}{label}", (x - 0.5, GIRTH / 2, bundle_z(side)), 1, reach, reach, "lashing", "z", ROUNDED)]
    return [box(name, (x - 0.5 + 2 * APART, GIRTH / 2 - reach, bundle_z(-1)), (x + 0.5 - 2 * APART, GIRTH / 2 + reach, bundle_z(1)), "lashing", grain="z"),
            *rounds]


def bundles(margin=0):
    return Union("the bundles", [Tube(f"the {label} bundle", (BODY[0], GIRTH / 2, bundle_z(side)), (BODY[1], GIRTH / 2, bundle_z(side)),
                                      GIRTH + 2 * margin, GIRTH + 2 * margin) for label, side in SIDES])


def ends(points, widths, heights, margin=0, overlap=0):
    """The round of each segment between the points, as long as it is drawn: reaching overlap past both its ends."""
    rounds = []
    for a, b, w, h in zip(points, points[1:], widths, heights):
        reach = [(q - p) * overlap / math.dist(a, b) for p, q in zip(a, b)]
        rounds.append(Tube("an end", [p - r for p, r in zip(a, reach)], [q + r for q, r in zip(b, reach)], w + 2 * margin, h + 2 * margin))
    return rounds


def flat_rounds(side, start, direction):
    return ends([(start + direction * reach, girth / 2, bundle_z(side)) for reach, girth in zip(TIP_REACH, TIP_GIRTHS)], TIP_GIRTHS, TIP_GIRTHS)


def raised_rounds(start, direction, droop, margin=0):
    return ends(raised_points(start, direction, droop), [2 * g for g in TIP_GIRTHS], TIP_GIRTHS, margin, TIP_OVERLAP)


def bundle_ropes():
    ties = len(range(TIE_PITCH // 2, BODY[1] - BODY[0], TIE_PITCH))
    metres = ties * (TIE_TURNS * math.pi * GIRTH * VOXEL_METRES + KNOT_METRES)
    print(f"bundle: {ties} ties {TIE_PITCH * VOXEL_METRES * 100:.0f} cm apart, {metres:.1f} m of cord, {ropes(metres)} ropes")


def lashings():
    around = (2 * GIRTH + math.pi * GIRTH) * VOXEL_METRES
    each = LASHING_TURNS * around + KNOT_METRES
    total = each * len(LASHINGS)
    count = ropes(total)
    order = sorted(LASHINGS, key=lambda x: abs(x - CENTRE))
    per_rope = [[] for _ in range(count)]
    for n, x in enumerate(order):
        per_rope[n * count // len(order)] += lashing(f"Lash{n + 1}", x)
    print(f"lashing: {len(LASHINGS)} lashings of {LASHING_TURNS} turns, {total:.1f} m of cord, {count} ropes")
    return per_rope


def tips():
    points = list(zip(TIP_REACH, TIP_RISE))
    turns = [math.dist(a, b) / TIP_TURN_PITCH for a, b in zip(points, points[1:])]
    metres = sum(n * (2 * g + math.pi * g) for n, g in zip(turns, TIP_GIRTHS)) * VOXEL_METRES + KNOT_METRES
    turns = sum(turns)
    assert ropes(metres) == 1, "an end takes more than one rope"
    print(f"ends: {turns:.0f} turns bind each end up, {metres:.1f} m of cord, one rope an end")
    ends = []
    for n, (start, direction) in enumerate(ENDS, 1):
        ends.append([group(f"Tips{n}Droop{k}", (0, 0, 0), (0, 0, 0), raised_end(f"Tip{n}Droop{k}x", start, direction, k))
                     for k in range(DROOPS)])
    return ends


def flats(sides):
    return [[element for name, side in sides for element in flat_end(f"Flat{name}{n}x", side, start, direction)]
            for n, (start, direction) in enumerate(ENDS, 1)]


def laid(sides):
    return [element for name, side in sides for element in bundle(name, side)] + [e for end in flats(sides) for e in end]


def envelope():
    body = GIRTH ** 2 * (BODY[1] - BODY[0])
    ends = sum((b - a) * g ** 2 for a, b, g in zip(TIP_REACH, TIP_REACH[1:], TIP_GIRTHS))
    litres = math.pi / 4 * (body + 2 * ends) * VOXEL_METRES_CUBED_LITRES
    print(f"a bundle of {BUNDLE_REEDS} reeds: {(BODY[1] - BODY[0] + 2 * TIP_REACH[-1]) * VOXEL_METRES:.1f} m long, "
          f"{GIRTH * VOXEL_METRES * 100:.1f} cm across, {litres:.1f} L; two: {2 * litres:.1f} L")


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
    blank = group("Blank", (0, 0, 0), (0, 0, 0), [e for name, side in SIDES for e in bundle(name, side)])
    flat, lashed, bound = stages("Flat", flats(SIDES)), stages("Lashing", lashings()), stages("Tips", tips())
    shape.add(blank, flat, lashed, bound)
    tied, untied = (lambda name: "Tie" in name), (lambda name: "Tie" not in name)
    shape.within(bundles(), blank, only=untied)
    shape.within(bundles(TIE_MARGIN), blank, only=tied)
    shape.within(Union("the flat ends", [t for _, side in SIDES for start, direction in ENDS for t in flat_rounds(side, start, direction)]), flat)
    shape.within(bundles(LASHING_MARGIN), lashed)
    shape.together(blank, flat, lashed)
    for droop in range(DROOPS):
        ends_bound = [end["children"][droop] for end in bound["children"]]
        for margin, only in ((0, untied), (TIE_MARGIN, tied)):
            shape.within(Union("the bound ends", [*bundles(margin).parts, *(t for start, direction in ENDS for t in raised_rounds(start, direction, droop, margin))]),
                         ends_bound, only=only)
        shape.together(blank, lashed, ends_bound)
    shape.keep("Hull", "FrontSeat", "Cargo", "OarStorage")
    shape.write()
    at = seat(shape, "FrontSeat")
    line = waterline("reedboat")
    girth, length = GIRTH * VOXEL_METRES, (BODY[1] - BODY[0]) * VOXEL_METRES
    pole = poling(stands("reedboat", girth, form="rectangle", waterplane=2 * girth * length, beam_metres=2 * girth, depth_metres=girth),
                  at, line / VOXEL_METRES, CENTRE - GIRTH - 1, (at[0], GIRTH, CENTRE), GIRTH)
    player_patch("player-reedboat", tools(shape, "reedboat", "FrontSeat", "roughseas-sitlegsforward", {"rotationZ": 6}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, 5, 3.5)),
        "pole": pole,
    }, ("Blank", "Lashing")))
    blocks = Path(target).parents[2] / "block"
    block(blocks / "reedbundle-one.json", SIDES[:1])
    block(blocks / "reedbundle-two.json", SIDES)


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
