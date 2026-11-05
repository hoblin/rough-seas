import functools
import math

from held import PADDLE, paddling, poling, tools, waterline
from stroke import player_patch, seat
from physics import LATH_SQUARE_METRES, ROPE_METRES, afloat, kilograms, per_rope, ropes, stands
from fit import APART, TOLERANCE, bounded, depth_into, lined as lined_section
from shape import BILGE, VOXEL_METRES, VOXELS_A_METRE, Hull, Shape, box, clear_of, corners_of, group, num, path, plank, renamed, rested, span, stages, widest
from transform import add, scale, sub, unit
from wounds import wound

PROFILE = [(0, 0), (2.6, 0.3), (4.6, 1.5), (5.7, 3.4), (6, 6)]
SHEER = 6
CENTRE = 8
SKIN = 0.6

GUNWALE = 0.6
SHEATHING = 0.15
SHEATHED_STRAKES = 3
RIB_WIDTH = 1.2
RIB_DEPTH = 0.45
RIBS = range(-18, 36, 4)
THWARTS = (-8, 8, 24)
THWART = (1.4, 0.8)
CENTRE_THWART = THWARTS[1]
POLER_AFT = 5
STERN = [(-19, 2.1), (-22, 2.75), (-24, 3.65), (-25.2, 5), (-25.6, 7), (-25.7, 9), (-25.5, 10.6), (-24.9, 11.4)]
STEM_WIDTH = 0.8
STEM_THICKNESS = 0.8
GORES = [-20, -13, 29, 36]

LASHING_STEP = 2.5
STEM_STITCH_STEP = 1.6
# Adney & Chapelle 1964, the Malecite canoe: groups of eight to ten turns of split root, about two inches apart,
# round gunwales about 1 1/4 inch square on either side of the bark; the stem bound by a stitch to the inch.
TURNS_PER_GROUP = 9
ROOT_METRES = 0.003
GROUP_GAP_METRES = 0.0508
GUNWALE_METRES = 0.032
BARK_METRES = 0.005
STITCH_METRES = 0.0254
STEM_GIRTH_METRES = 0.08
SHEET_LENGTH = 80
SHEET_THICKNESS = SKIN
SHEET_CURL = 2.4
SHEET_TILE = 8
GUNWALE_FRAME = 5
FOLD = 2 * SKIN
RIB_INSET = SKIN + SHEATHING + APART


def section(x, breadth, rocker, rise):
    top = SHEER + rise
    return (x, [(h * breadth, rocker + y * (top - rocker) / SHEER) for h, y in PROFILE])


# Her ends are as broad as the stem the bark is sewn round, and the bark over it.
STEM_BREADTH = (STEM_WIDTH / 2 + SKIN) / PROFILE[-1][0]
STATIONS = [
    section(-26, STEM_BREADTH, 4.2, 3.6),
    section(-23, 0.4, 2.2, 1.9),
    section(-16, 0.75, 0.7, 0.7),
    section(-8, 0.93, 0.1, 0.2),
    section(0, 1, 0, 0),
    section(8, 1, 0, 0),
    section(16, 1, 0, 0),
    section(24, 0.93, 0.1, 0.2),
    section(32, 0.75, 0.7, 0.7),
    section(39, 0.4, 2.2, 1.9),
    section(42, STEM_BREADTH, 4.2, 3.6),
]


def lined(stations, depth):
    return [(x, lined_section(edges, depth)) for x, edges in stations]


def sheet_ends():
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    return middle - SHEET_LENGTH / 2, middle + SHEET_LENGTH / 2


def sheet_half():
    """How far the sheet reaches either side of her centreline, flat and curled: a little more than her bark's girth."""
    return sum(math.dist(a, b) for a, b in zip(PROFILE, PROFILE[1:])) - 2, SHEET_CURL


def cuts_of(a, b, step):
    n = math.ceil((b - a) / step - 1e-9)
    return [a + (b - a) * k / n for k in range(n + 1)]


def tiled(name, frm, to):
    """Bark from frm to to in tiles no larger than her skin's boards, each drawn at the texture's own scale; the faces
    where two tiles meet are left out."""
    (x0, y0, z0), (x1, y1, z1) = frm, to
    xs, zs = cuts_of(x0, x1, SHEET_TILE), cuts_of(z0, z1, SHEET_TILE)
    tiles = []
    for i, (a, b) in enumerate(zip(xs, xs[1:])):
        for j, (c, d) in enumerate(zip(zs, zs[1:])):
            joints = (("west",) if i else ()) + (("east",) if i < len(xs) - 2 else ()) + (("north",) if j else ()) + (("south",) if j < len(zs) - 2 else ())
            tiles.append(box(f"{name}{i + 1}x{j + 1}", (a, y0, c), (b, y1, d), "bark", skip=joints))
    return tiles


def sheet():
    flat, curl = sheet_half()
    x0, x1 = sheet_ends()
    return group("Sheet", (0, 0, 0), (0, 0, 0), [
        group("SheetFlat", (0, 0, 0), (0, 0, 0), tiled("SheetFlat", (x0, 0, CENTRE - flat), (x1, SHEET_THICKNESS, CENTRE + flat))),
        group("SheetCurlPort", (0, 0, CENTRE - flat), (28, 0, 0), tiled("SheetCurlPort", (x0 + 1, 0, -curl), (x1 - 1, SHEET_THICKNESS, 0))),
        group("SheetCurlStarboard", (0, 0, CENTRE + flat), (-28, 0, 0), tiled("SheetCurlStarboard", (x0 + 1, 0, 0), (x1 - 1, SHEET_THICKNESS, curl))),
    ])


def sheer_at(stations, x):
    for (x0, e0), (x1, e1) in zip(stations, stations[1:]):
        if x0 <= x <= x1:
            return e0[-1][1] + (x - x0) / (x1 - x0) * (e1[-1][1] - e0[-1][1])
    return stations[-1][1][-1][1]


def frame_pieces(hull, bark, cuts, stems):
    """Her frame on the plan of hull, seated on the bark: inwales and thwarts inside it, outwales outside, the inwales
    stopping short of the stems."""
    inside, outside = bark.outline("the bark's inside", inset=SKIN), bark.outline("an outwale's outside", inset=-GUNWALE)
    return [
        gunwale(hull, "InwalePort", -1, True, inside, cuts, stems),
        gunwale(hull, "InwaleStarboard", 1, True, inside, cuts, stems),
        *(thwart(n, x, hull, bark, cuts) for n, x in enumerate(THWARTS, 1)),
        gunwale(hull, "OutwalePort", -1, False, outside, cuts),
        gunwale(hull, "OutwaleStarboard", 1, False, outside, cuts),
    ]


# The gunwale frame lies on the bark with its thwarts' undersides on it, the inwales mortised round their middles.
FLAT_SHEER = SHEET_THICKNESS + APART + THWART[1] / 2 + GUNWALE / 2


def laid_flat(element):
    """A frame piece as it lies on the bark before the frame is raised: its plan as raised, its sheer level."""
    if "children" not in element:
        dy = SHEET_THICKNESS + APART - element["from"][1]
        return dict(element, **{"from": [element["from"][0], num(element["from"][1] + dy), element["from"][2]],
                                "to": [element["to"][0], num(element["to"][1] + dy), element["to"][2]]})
    pitch = math.radians(element.get("rotationZ", 0))
    start = element["from"]
    lying = [start[0], num(start[1] + FLAT_SHEER - sheer_at(STATIONS, start[0])), start[2]]
    board = element["children"][0]
    level = dict(board, to=[num(board["from"][0] + (board["to"][0] - board["from"][0]) * math.cos(pitch)), *board["to"][1:]])
    flat = {key: value for key, value in element.items() if key != "rotationZ"}
    return dict(flat, **{"from": lying, "to": lying, "rotationOrigin": lying, "children": [level]})


def staked(margin):
    """Her bark turned up square round the gunwale frame lying on it, as stakes hold it: flat under the frame, upright
    beside it, as tall as the sheet's width leaves, folded flat together past the frame's ends."""
    flat, curl = sheet_half()
    reach = flat + curl
    x0, x1 = sheet_ends()
    fold = FOLD
    plan = [(x0, 2 * fold), *((x, max(2 * fold, edges[-1][0] + margin)) for x, edges in STATIONS), (x1, 2 * fold)]
    turn = fold * (1 - math.sqrt(0.5))
    return [(x, [(0, 0), (half - fold, 0), (half - turn, turn), (half, fold), (half, fold + reach - half - fold * (math.pi / 2 - 1))])
            for x, half in plan]


def depth(stations):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    return round(max(edges[-1][1] for x, edges in stations if abs(x - middle) <= 8) / VOXELS_A_METRE, 4)


def raising(flat, stems):
    """Her bark as each piece of the frame leaves it, and each piece drawn wherever it lies from when it is laid. The
    inwales and thwarts are laid flat on the sheet as her gunwale frame; once it is whole the bark is staked up round
    it, and with the first outwale the frame is raised to her sheer, the bark sewn to it in her finished form."""
    hull = Hull("Frame", STATIONS, CENTRE, SKIN, "bark", "bark")
    levels = len(frame_pieces(hull, hull, [], []))
    finished = Hull(f"Skin{levels}x", STATIONS, CENTRE, SKIN, "bark", "bark", edge="bark")
    cuts = []
    boards = finished.strakes(cuts)
    final = frame_pieces(hull, finished, cuts, stems)
    upright = staked(FOLD - SKIN + APART)
    stakes = Hull(f"Skin{GUNWALE_FRAME}x", upright, CENTRE, SKIN, "bark", "bark", edge="bark")
    sheet_level = depth([(x, [(0, 0), (0, SHEET_THICKNESS)]) for x, _ in STATIONS])

    def bark(k):
        if k < GUNWALE_FRAME:
            return [renamed(e, f"At{k}") for e in flat["children"]], sheet_level
        if k == GUNWALE_FRAME:
            return stakes.strakes([]), depth(upright)
        return (boards if k == levels else [renamed(e, f"At{k}") for e in boards]), depth(STATIONS)

    skins = []
    for k in range(1, levels + 1):
        elements, metres = bark(k)
        skins.append(dict(group(f"Skin{k}", (0, 0, 0), (0, 0, 0), elements), depthMetres=metres))
    pieces = []
    for n, laid in enumerate(final, 1):
        variants = [group(f"Frame{n}At{k}", (0, 0, 0), (0, 0, 0), [renamed(laid_flat(e) if k <= GUNWALE_FRAME else e, f"At{k}") for e in laid])
                    for k in range(n, levels + 1)]
        piece = group(f"Frame{n}", (0, 0, 0), (0, 0, 0), variants)
        piece["metres"] = round((run(hull, len(PROFILE) - 1) if "wale" in laid[0]["name"] else span(laid, "frame")) / VOXELS_A_METRE, 2)
        pieces.append(piece)
    skin = dict(group("Skin", (0, 0, 0), (0, 0, 0), skins), depthMetres=sheet_level)
    return skin, group("Frame", (0, 0, 0), (0, 0, 0), pieces), cuts, {GUNWALE_FRAME: stakes, levels: finished}


def gunwale(hull, name, side, inside, surface, cuts, stems=()):
    """A gunwale along her sheer, seated against the bark; at the ends it stops where it no longer fits beside the
    stem or would pass into it. Every other piece is a little lower and shorter, so that two share no plane where they
    meet, and no piece ends in the plane of an end already cut."""
    top = len(PROFILE) - 1
    pieces = []
    for i in range(len(hull.stations) - 1):
        a, b = hull.point(i, top, side), hull.point(i + 1, top, side)
        if abs(a[2] - b[2]) < 1e-6 and abs(a[2] - CENTRE) < 1e-6:
            continue
        drop, base = GUNWALE + APART * (i % 2), SKIN if inside else 0
        piece = lambda a, b, i=i, drop=drop, base=base: lambda offset: plank(
            f"{name}{i + 1}", add((a[0], a[1] - drop, a[2] - side * base), offset), add((b[0], b[1] - drop, b[2] - side * base), offset),
            GUNWALE - 2 * APART * (i % 2), GUNWALE, 0, side if inside else -side, "frame", "frame", overlap=0)

        def stands_out(a, b):
            offset = rested(piece(a, b), surface)
            corners = corners_of(piece(a, b)(offset))
            return max(surface.beyond(p) for p in corners) > TOLERANCE / 2 or any(depth_into(corners, stem) for stem in stems)

        if i in (0, len(hull.stations) - 2) and stands_out(a, b):
            stem, inner = (a, b) if abs(a[2] - CENTRE) < abs(b[2] - CENTRE) else (b, a)
            towards = lambda cut: sorted((tuple(p + cut * (q - p) for p, q in zip(stem, inner)), inner))
            short, fits = 0.0, 1.0
            for _ in range(20):
                cut = (short + fits) / 2
                short, fits = (cut, fits) if stands_out(*towards(cut)) else (short, cut)
            if fits > 1 - 1e-6:
                continue
            a, b = towards(fits)
        along = unit(sub(b, a))

        def made(short, a=a, b=b, i=i):
            made = piece(add(a, scale(along, short[0])), sub(b, scale(along, short[1])))
            offset = add(rested(made, surface), (0, APART * (i % 2), 0))
            return made(offset), add(add(a, scale(along, short[0])), offset), add(sub(b, scale(along, short[1])), offset)

        pieces.append(clear_of(cuts, along, made)[0])
    return pieces


def thwart(n, x, hull, bark, cuts):
    """A thwart mortised through the inwales at their middle height, its ends short of the bark, slid along clear of
    the ends already cut."""
    width, height = THWART
    y = sheer_at(hull.stations, x + width / 2) - GUNWALE / 2 - height / 2

    def placed(short):
        pole = lambda reach: box(f"Thwart{n}", (x + short[0], y, CENTRE - reach), (x + width + short[0], y + height, CENTRE + reach), "frame", grain="z")
        return pole(widest(pole, bark.outline("the bark's inside", inset=SKIN + APART), PROFILE[-1][0])), (x + short[0], y, CENTRE), (x + width + short[0], y, CENTRE)

    return [clear_of(cuts, (1, 0, 0), placed, slide=True)[0]]


@functools.cache
def stern_line():
    """Her stem's curve, moved in from her end and up into the bark sewn round it, as far as it takes to lie inside."""
    inside = Hull("Skin", STATIONS, CENTRE, SKIN, "bark", "bark").outline("the bark's inside", inset=SKIN)
    out = lambda dx, dy: max(inside.beyond(p) for p in corners_of(group("Stem", (0, 0, 0), (0, 0, 0),
                                                                         stem("SternStem", on_centre([(x + dx, y + dy) for x, y in STERN])))))
    dx = max(0, inside.x0 - min(p[0] for p in corners_of(group("Stem", (0, 0, 0), (0, 0, 0), stem("SternStem", on_centre(STERN)))))) + APART
    low, high = 0.0, SHEER
    for _ in range(30):
        low, high = ((low + high) / 2, high) if out(dx, (low + high) / 2) > TOLERANCE / 2 else (low, (low + high) / 2)
    return [(x + dx, y + high) for x, y in STERN]


def stem(name, points):
    return path(name, points, STEM_WIDTH, STEM_THICKNESS, "frame", grain="x")


def bow(points):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    return [(2 * middle - x, y) for x, y in points]


def on_centre(points, lift=0):
    return [(x, y + lift, CENTRE) for x, y in points]


def run(hull, edge):
    """How long a lath lying along the edge runs, station to station."""
    return sum(math.dist(hull.point(i, edge, 1), hull.point(i + 1, edge, 1)) for i in range(len(hull.stations) - 1))


def sheathing(hull):
    cuts = []
    Hull("Skin", STATIONS, CENTRE, SKIN, "bark", "bark").strakes(cuts)
    liner = Hull("Sheathing", lined(hull.stations, SKIN + APART)[1:-1], CENTRE, SHEATHING, "frame", "frame", edge="frame")
    strakes = liner.strakes(cuts)
    laths = []
    for k in range(1, SHEATHED_STRAKES + 1):
        for label in ("Port", "Starboard"):
            prefix = f"Sheathing{label}{k}x"
            laths.append([strake for strake in strakes if strake["name"].startswith(prefix)])
    return laths


def sheathed(hull):
    """The sheathing, each lath as long as the two edges it lies between run."""
    stage = stages("Sheathing", sheathing(hull))
    liner = Hull("Sheathing", lined(hull.stations, SKIN + APART)[1:-1], CENTRE, SHEATHING, "frame", "frame")
    for piece, k in zip(stage["children"], (k for k in range(SHEATHED_STRAKES) for _ in ("Port", "Starboard"))):
        piece["metres"] = round((run(liner, k) + run(liner, k + 1)) / 2 / VOXELS_A_METRE, 2)
    return stage


def under_gunwales(stations):
    """Each half-section cut short where a rib's head is forced in against the inwale: its inner corner, tilted out
    with the side, stays under the thwarts mortised through the inwales."""
    clipped = []
    for x, edges in stations:
        (h0, y0), (h1, y1) = edges[-2:]
        tilt = (h1 - h0) / math.dist((h0, y0), (h1, y1))
        top = y1 - GUNWALE / 2 - THWART[1] / 2 - (RIB_INSET + RIB_DEPTH) * tilt - APART
        clipped.append((x, edges[:-1] + [(h0 + (h1 - h0) * (top - y0) / (y1 - y0), top)]))
    return clipped


def ribs(hull):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    order = sorted(RIBS, key=lambda x: (abs(x + RIB_WIDTH / 2 - middle), x))
    headed = Hull(hull.prefix, under_gunwales(hull.stations), hull.cz, hull.t, hull.outer, hull.inner, hull.edge)
    return [headed.rib(f"Rib{n}x", x, "frame", width=RIB_WIDTH, depth=RIB_DEPTH, inset=RIB_INSET) for n, x in enumerate(order, 1)]


def wraps(hull, cuts):
    top = len(PROFILE) - 1
    margin = 0.1
    girth = 2 * (2 * GUNWALE_METRES + BARK_METRES + GUNWALE_METRES)
    turns_per_metre = TURNS_PER_GROUP / (TURNS_PER_GROUP * ROOT_METRES + GROUP_GAP_METRES)
    turns, metres = [], 0
    for side, label in ((-1, "Port"), (1, "Starboard")):
        x, n = STATIONS[1][0] + 1, 1
        while x < STATIONS[-2][0] - 1:
            if any(at - 0.25 < x < at + THWART[0] + 0.25 for at in THWARTS):
                x += LASHING_STEP
                continue
            _, y, z = hull.at(x, top, side)
            z0, z1 = sorted((z + side * (GUNWALE + margin), z - side * (SKIN + GUNWALE + margin)))
            wrap = lambda short, x=x, y=y, z0=z0, z1=z1, n=n: (
                box(f"Wrap{label}{n}", (x - 0.25 + short[0], y - GUNWALE - margin, z0), (x + 0.25 - short[1], y + margin, z1), "lashing", grain="y"),
                (x - 0.25 + short[0], y, z), (x + 0.25 - short[1], y, z))
            turns.append(clear_of(cuts, (1, 0, 0), wrap, slide=True)[0])
            x, n = x + LASHING_STEP, n + 1
        sheer = [hull.point(i, top, side) for i in range(len(hull.stations))]
        metres += sum(math.dist(a, b) for a, b in zip(sheer, sheer[1:])) * VOXEL_METRES * turns_per_metre * girth
    for name, points in (("Stern", stern_line()), ("Bow", bow(stern_line()))):
        along = on_centre(points[2:-1])
        for n, (a, b) in enumerate(zip(along, along[1:]), 1):
            steps = max(1, round(math.dist(a, b) / STEM_STITCH_STEP))
            for s in range(steps):
                f = (s + 0.5) / steps
                at = [p + f * (q - p) for p, q in zip(a, b)]
                turns.append(box(f"Stitch{name}{n}x{s + 1}", (at[0] - 0.25, at[1] - 0.25, CENTRE - 1), (at[0] + 0.25, at[1] + 0.25, CENTRE + 1),
                                 "lashing", grain="z"))
            metres += math.dist(a, b) * VOXEL_METRES / STITCH_METRES * STEM_GIRTH_METRES
    count = ropes(metres)
    print(f"lashing: {metres:.0f} m of root in {len(turns)} wraps, {count} ropes of {ROPE_METRES} m")
    return per_rope(turns, count)


def gores(hull, side):
    pieces = []
    for n, x in enumerate(GORES, 1):
        points = [hull.at(x, k, side) for k in range(1, len(PROFILE))]
        points = [(p[0], p[1], p[2] + side * 0.05) for p in points]
        pieces += path(f"Gore{'Port' if side < 0 else 'Starboard'}{n}x", points, 0.5, 0.15, "pitch", grain="y")
    return pieces


def seam(name, points):
    return path(name, on_centre(points[1:-2]), STEM_WIDTH + 0.4, STEM_THICKNESS + 0.3, "pitch", grain="x", overlap=0.25 + APART)


def build(path_):
    shape = Shape(path_, {
        "bark": "game:block/wood/bark/birch-h",
        "frame": "game:block/wood/debarked/birch",
        "lashing": "game:item/resource/rope",
        "pitch": "game:block/liquid/glue-cold",
        "bilge": BILGE,
    })
    hull = Hull("Skin", STATIONS, CENTRE, SKIN, "bark", "bark", edge="bark")
    stems = stages("Stems", [stem("SternStem", on_centre(stern_line())), stem("BowStem", on_centre(bow(stern_line())))])
    flat = sheet()
    skin, frame, cuts, shaped = raising(flat, [points for _, points, _, _ in bounded([stems])])
    laths = [frame, stems, sheathed(hull), stages("Ribs", ribs(hull))]
    lashed = stages("Lashing", wraps(hull, cuts))
    pitched = stages("Pitch", [seam("SternSeam", stern_line()), seam("BowSeam", bow(stern_line())), gores(hull, -1), gores(hull, 1)])
    shape.add(flat, skin, *laths[:2], lashed, *laths[2:], pitched)
    levels = len(skin["children"])
    shape.together(flat)
    for k, level in enumerate(skin["children"], 1):
        laid = [piece["children"][k - n] for n, piece in enumerate(frame["children"][:k], 1)]
        if bark := shaped.get(k):
            shape.within(bark.outline(f"the bark at frame piece {k}"), level)
            shape.within(bark.outline("the bark's inside", inset=SKIN), laid, only=lambda name: "Outwale" not in name)
            shape.within(bark.outline("an outwale's outside", inset=-GUNWALE), laid, only=lambda name: "Outwale" in name)
        shape.apart(level, laid, pressed=SKIN / 2)
        shape.together(level, laid, *((laths[1:], lashed, pitched) if k == levels else ()))
    raised = [piece["children"][-1] for piece in frame["children"]]
    shape.apart(laths[1:], raised)
    shape.apart(laths[3], laths[1:3], pressed=SHEATHING)
    shape.within(hull.outline("the bark's inside", inset=SKIN), laths[1])
    shape.within(Hull("Sheathing", lined(hull.stations, SKIN + APART), CENTRE, SHEATHING, "frame", "frame").outline("the sheathing"), laths[2])
    shape.within(hull.outline("the sheathing's inside", inset=SKIN + SHEATHING + APART), laths[3])
    shape.add(hull.hide_water(5.7), hull.bilge(5.7))
    shape.keep("Hull", "FrontSeat", "BackSeat", "OarStorage")
    *rising, finished = skin["children"]
    wound(shape, [("", [finished], "bark"), ("Sheet", [flat], "bark"), *((level["name"], [level], "bark") for level in rising)],
          lining=SHEATHING, framing=([laths[3]], "frame"))
    shape.write()
    at = seat(shape, "BackSeat")
    line = waterline("barkcanoe")
    poler = CENTRE_THWART + POLER_AFT
    pole = poling(stands("barkcanoe", SKIN * VOXEL_METRES), at, line * VOXELS_A_METRE, CENTRE - SHEER - 1,
                  (poler, SKIN, CENTRE), hull.half(poler, SKIN + 1, inset=SKIN),
                  floor_at=lambda x, across: hull.floor(x, across, inset=SKIN))
    player_patch("player-barkcanoe", tools(shape, "barkcanoe", "BackSeat", "roughseas-kneel", {"rotationZ": 10}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, 6.5, 1.5)),
        "pole": pole,
    }, (f"Skin{levels}",)))
    report(hull, laths)


def report(hull, laths):
    bark = hull.skin_square_metres()
    metres = sum(piece.get("metres", 0) for stage in laths for piece in stage["children"])
    bark_kg = kilograms("bark", bark * BARK_METRES * 1000)
    frame_kg = kilograms("wood-tuja", metres * LATH_SQUARE_METRES * 1000)
    print(f"barkcanoe: {bark:.2f} m² of bark, {bark_kg:.1f} kg; {metres:.1f} m of lath, {frame_kg:.1f} kg of white cedar; "
          f"sheet {SHEET_LENGTH / VOXELS_A_METRE:.2f} m, {SHEET_THICKNESS / VOXELS_A_METRE:.3f} m thick")
    afloat("barkcanoe", bark_kg + frame_kg, SHEER / VOXELS_A_METRE, hull.litres(SHEER, inset=SKIN),
           hull.square_metres(SHEER - 0.01, inset=SKIN))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
