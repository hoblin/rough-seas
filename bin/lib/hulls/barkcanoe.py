import functools
import math

from held import ARM_REACH_METRES, worked_from_middle, PADDLE, paddling, poling, tools, waterline_metres
from stroke import player_patch, seat
from physics import LATH_SQUARE_METRES, ROPE_METRES, afloat, kilograms, per_rope, ropes, shares, stands
from fit import APART, TOLERANCE, bounded, depth_into, lined as lined_section
import units
from entity import merged, record
from shape import BILGE, Hull, Shape, bilge_levels, box, clear_of, corners_of, group, hull_box, num, path, plank, renamed, rested, span, stages, widest, windage
from transform import add, scale, sub, unit
from wounds import backed, wound

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
THWARTS = (8, -8, 24)
THWART = (1.4, 0.8)
CENTRE_THWART = THWARTS[0]
POLER_AFT = 5
STERN = [(-19, 2.1), (-22, 2.75), (-24, 3.65), (-25.2, 5), (-25.6, 7), (-25.7, 9), (-25.5, 10.6), (-24.9, 11.4)]
STEM_WIDTH = 0.8
STEM_THICKNESS = 0.8
GORES = [-20, -13, 29, 36]
SLASHES = (-13, 29, -20, 36)

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
# A sheet five metres long, a little longer than the canoe, trimmed to her as her sides are raised.
SHEET_LENGTH = 80
SHEET_THICKNESS = SKIN
SHEET_CURL = 2.4
SHEET_TILE = 8
GUNWALE_FRAME = 5
LEVELS = GUNWALE_FRAME + len(SLASHES) + 2 + 2
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


def tiled(name, frm, to, open=()):
    """Bark from frm to to in tiles no larger than her skin's boards, each drawn at the texture's own scale; the faces
    where two tiles meet are left out, and those on its open sides."""
    (x0, y0, z0), (x1, y1, z1) = frm, to
    xs, zs = cuts_of(x0, x1, SHEET_TILE), cuts_of(z0, z1, SHEET_TILE)
    tiles = []
    for i, (a, b) in enumerate(zip(xs, xs[1:])):
        for j, (c, d) in enumerate(zip(zs, zs[1:])):
            joints = (("west",) if i or "west" in open else ()) + (("east",) if i < len(xs) - 2 or "east" in open else ()) \
                + (("north",) if j else ()) + (("south",) if j < len(zs) - 2 else ())
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


def plan_half(x):
    """Her sheer's half-breadth at x, as her gunwales stand in plan; past her ends, her ends'."""
    edges = [(at, edges[-1][0]) for at, edges in STATIONS]
    if x <= edges[0][0]:
        return edges[0][1]
    for (x0, h0), (x1, h1) in zip(edges, edges[1:]):
        if x0 <= x <= x1:
            return h0 + (x - x0) / (x1 - x0) * (h1 - h0)
    return edges[-1][1]


def upright(x):
    """Her bark at x turned up square round the gunwale frame lying on it, as stakes hold it: flat under the frame,
    upright beside it, as tall as the sheet's width leaves; folded flat together past the frame's ends."""
    flat, curl = sheet_half()
    half = max(2 * FOLD, plan_half(x) + FOLD - SKIN + APART)
    turn = FOLD * (1 - math.sqrt(0.5))
    return [(0, 0), (half - FOLD, 0), (half - turn, turn), (half, FOLD), (half, FOLD + flat + curl - half - FOLD * (math.pi / 2 - 1))]


def staked(a, b):
    return [(x, upright(x)) for x in (a, *(at for at, _ in STATIONS if a < at < b), b)]


def spans():
    """How far along her the bark stands turned up as each slash is cut: her middle once the frame is whole, where
    her sides run straight and turn up uncut; then out to the next slash, or the sheet's end, past each one cut."""
    x0, x1 = sheet_ends()
    marks = sorted(GORES)
    low, high = marks[1], marks[2]
    turned = [(low, high)]
    for cut in SLASHES:
        if cut == low:
            low = max([mark for mark in marks if mark < cut], default=x0)
        else:
            high = min([mark for mark in marks if mark > cut], default=x1)
        turned.append((low, high))
    return turned


def lying(prefix, a, b):
    """The sheet as it still lies beyond a and b, where its bark is not turned up yet, each part open where it meets
    the bark turned up."""
    flat, curl = sheet_half()
    x0, x1 = sheet_ends()
    parts = []
    for name, (xa, xb), joint in (("Stern", (x0, a), "east"), ("Bow", (b, x1), "west")):
        if xb - xa < 1e-6:
            continue
        parts += tiled(f"{prefix}Flat{name}", (xa, 0, CENTRE - flat), (xb, SHEET_THICKNESS, CENTRE + flat), (joint,))
        ca, cb = max(xa, x0 + 1), min(xb, x1 - 1)
        parts += [group(f"{prefix}Curl{name}Port", (0, 0, CENTRE - flat), (28, 0, 0),
                        tiled(f"{prefix}Curl{name}Port", (ca, 0, -curl), (cb, SHEET_THICKNESS, 0), (joint,))),
                  group(f"{prefix}Curl{name}Starboard", (0, 0, CENTRE + flat), (-28, 0, 0),
                        tiled(f"{prefix}Curl{name}Starboard", (ca, 0, 0), (cb, SHEET_THICKNESS, curl), (joint,)))]
    return parts


def surplus(name, end):
    """The sheet's surplus past one of her ends, running on from it folded flat together, until a cut trims it off
    along the end."""
    x0, x1 = sheet_ends()
    station = STATIONS[0] if end < 0 else STATIONS[-1]
    tip = (x0 if end < 0 else x1, station[1])
    near = (station[0] + end * APART, station[1])
    return Hull(name, [tip, near] if end < 0 else [near, tip], CENTRE, SKIN, "bark", "bark", edge="bark")


def depth(stations):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    return round(units.metres(max(edges[-1][1] for x, edges in stations if abs(x - middle) <= 8)), 4)


def variants(name, laid, first, raised):
    """A piece laid with level first drawn at every level from then on: flat on the bark until the frame is raised."""
    return group(name, (0, 0, 0), (0, 0, 0), [group(f"{name}At{k}", (0, 0, 0), (0, 0, 0), [renamed(e if k >= raised else laid_flat(e), f"At{k}") for e in laid])
                                              for k in range(first, LEVELS + 1)])


def raising(flat, stems):
    """Her bark as each piece of work leaves it, and each frame piece drawn wherever it lies from when it is laid, as
    the Malecite built one. The inwales and thwarts lie flat on the sheet as her gunwale frame, and once it is whole
    her middle is turned up round it; each slash lets the bark beyond it turn up; the first outwale raises the frame
    to her sheer and the bark is sewn to it, its ends still standing as staked until a cut trims and shapes each."""
    hull = Hull("Frame", STATIONS, CENTRE, SKIN, "bark", "bark")
    finished = Hull(f"Skin{LEVELS}x", STATIONS, CENTRE, SKIN, "bark", "bark", edge="bark")
    cuts = []
    boards = finished.strakes(cuts)
    final = frame_pieces(hull, finished, cuts, stems)
    raised = GUNWALE_FRAME + len(SLASHES) + 1
    sheet_level = depth([(x, [(0, 0), (0, SHEET_THICKNESS)]) for x, _ in STATIONS])
    skins, fits = [], {LEVELS: [(finished, boards)]}
    for k in range(1, GUNWALE_FRAME):
        skins.append((k, [renamed(e, f"At{k}") for e in flat["children"]], sheet_level))
    for k, (a, b) in enumerate(spans(), GUNWALE_FRAME):
        stakes = Hull(f"Skin{k}x", staked(a, b), CENTRE, SKIN, "bark", "bark", edge="bark")
        standing = stakes.strakes([])
        skins.append((k, standing + lying(f"Skin{k}x", a, b), depth(stakes.stations)))
        if (a, b) == sheet_ends():
            fits[k] = [(stakes, standing)]
    ends = {end: surplus(f"Surplus{label}x", end) for end, label in ((-1, "Stern"), (1, "Bow"))}
    offcuts = {end: hull.strakes([]) for end, hull in ends.items()}
    for k in range(raised, LEVELS):
        kept = [end for end in ends if k < LEVELS - 1 or end > 0]
        sewn = [renamed(e, f"At{k}") for e in boards]
        parts = {end: [renamed(e, f"At{k}") for e in offcuts[end]] for end in kept}
        skins.append((k, sewn + [e for end in kept for e in parts[end]], depth(STATIONS)))
        fits[k] = [(finished, sewn), *((ends[end], parts[end]) for end in kept)]
    skins.append((LEVELS, boards, depth(STATIONS)))
    skin = dict(group("Skin", (0, 0, 0), (0, 0, 0), [dict(group(f"Skin{k}", (0, 0, 0), (0, 0, 0), elements), depthMetres=metres)
                                                      for k, elements, metres in skins]), depthMetres=sheet_level)

    def pieces(name, laid, first):
        made = []
        for n, piece in enumerate(laid, 1):
            made.append(variants(f"{name}{n}", piece, first + n - 1, raised))
            made[-1]["metres"] = round(units.metres(run(hull, len(PROFILE) - 1) if "wale" in piece[0]["name"] else span(piece, "frame")), 2)
        return group(name, (0, 0, 0), (0, 0, 0), made)

    frame = pieces("Frame", final[:GUNWALE_FRAME], 1)
    outwales = pieces("Outwales", final[GUNWALE_FRAME:], raised)
    slashes = stages("Slashes", [[] for _ in SLASHES])
    trims = stages("Ends", [[], []])
    return skin, frame, slashes, outwales, trims, cuts, fits


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
        piece["metres"] = round(units.metres((run(liner, k) + run(liner, k + 1)) / 2), 2)
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


def ribs(hull, lining):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    order = sorted(RIBS, key=lambda x: (abs(x + RIB_WIDTH / 2 - middle), x))
    headed = Hull(hull.prefix, under_gunwales(hull.stations), hull.cz, hull.t, hull.outer, hull.inner, hull.edge)
    return [headed.rib(f"Rib{n}x", x, "frame", width=RIB_WIDTH, depth=RIB_DEPTH, inset=RIB_INSET,
                       clear=[(points, low, high) for _, points, low, high in bounded([lining])]) for n, x in enumerate(order, 1)]


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
        metres += units.metres(sum(math.dist(a, b) for a, b in zip(sheer, sheer[1:]))) * turns_per_metre * girth
    for name, points in (("Stern", stern_line()), ("Bow", bow(stern_line()))):
        along = on_centre(points[2:-1])
        for n, (a, b) in enumerate(zip(along, along[1:]), 1):
            steps = max(1, round(math.dist(a, b) / STEM_STITCH_STEP))
            for s in range(steps):
                f = (s + 0.5) / steps
                at = [p + f * (q - p) for p, q in zip(a, b)]
                turns.append(box(f"Stitch{name}{n}x{s + 1}", (at[0] - 0.25, at[1] - 0.25, CENTRE - 1), (at[0] + 0.25, at[1] + 0.25, CENTRE + 1),
                                 "lashing", grain="z"))
            metres += units.metres(math.dist(a, b)) / STITCH_METRES * STEM_GIRTH_METRES
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
    skin, frame, slashes, outwales, trims, cuts, fits = raising(flat, [points for _, points, _, _ in bounded([stems])])
    lining = sheathed(hull)
    ribbed = stages("Ribs", ribs(hull, lining))
    laths = [frame, outwales, stems, lining, ribbed]
    lashed = stages("Lashing", wraps(hull, cuts))
    pitched = stages("Pitch", [seam("SternSeam", stern_line()), seam("BowSeam", bow(stern_line())), gores(hull, -1), gores(hull, 1)])
    shape.add(flat, skin, frame, slashes, outwales, trims, stems, lashed, lining, ribbed, pitched)
    raised = GUNWALE_FRAME + len(SLASHES) + 1
    shape.together(flat)
    for k, level in enumerate(skin["children"], 1):
        laid = [piece["children"][k - n] for n, piece in enumerate(frame["children"][:k], 1)]
        laid += [piece["children"][k - first] for first, piece in enumerate(outwales["children"], raised) if k >= first]
        for n, (bark, elements) in enumerate(fits.get(k, [])):
            shape.within(bark.outline(f"the bark at level {k}"), elements)
            if n == 0:
                shape.within(bark.outline("the bark's inside", inset=SKIN), laid, only=lambda name: "Outwale" not in name)
                shape.within(bark.outline("an outwale's outside", inset=-GUNWALE), laid, only=lambda name: "Outwale" in name)
        shape.apart(level, laid, pressed=SKIN / 2)
        shape.together(level, laid, *((stems, lashed, lining, ribbed, pitched) if k == LEVELS else ()))
    framed = [piece["children"][-1] for piece in frame["children"] + outwales["children"]]
    shape.apart([stems, lining, ribbed], framed)
    shape.apart(ribbed, [stems, lining])
    shape.within(hull.outline("the bark's inside", inset=SKIN), stems)
    shape.within(Hull("Sheathing", lined(hull.stations, SKIN + APART), CENTRE, SHEATHING, "frame", "frame").outline("the sheathing"), lining)
    shape.within(hull.outline("the sheathing's inside", inset=SKIN + SHEATHING + APART), ribbed)
    shape.add(hull.hide_water(5.7), hull.bilge(5.7))
    *rising, finished = skin["children"]
    box, measured = hull_box(finished)
    shape.add(box)
    sides, bottom = backed([finished], ["bark"], [lining], box)
    apart = units.metres(RIBS.step)
    record("barkcanoe", merged(measured, {"behaviorConfigs": {"roughseas:vesselintegrity": {"shell": {"backedShare": sides, "bottomBackedShare": bottom, **units.both("span", apart), "frame": units.both("spacing", apart)}}}}))
    shape.keep("FrontSeat", "BackSeat", "OarStorage")
    wound(shape, [("", [finished], "bark"), ("Sheet", [flat], "bark"), *((level["name"], [level], "bark") for level in rising)],
          lining=SHEATHING, framing=([ribbed], "frame"))
    shape.write()
    framing = {"trunkBlocks": round(units.metres(SHEET_LENGTH))}
    record("barkcanoe", merged(report(hull, laths, windage([finished], box["from"][1])), measured, bilge_levels(shape.elements), {"behaviorConfigs": {"roughseas:framing": framing}}))
    at = seat(shape, "BackSeat")
    record("barkcanoe", {"behaviorConfigs": {"roughseas:propulsion": worked_from_middle(box, at, ARM_REACH_METRES)}})
    water_metres = waterline_metres("barkcanoe")
    poler = CENTRE_THWART + POLER_AFT
    pole = poling(stands("barkcanoe", units.metres(SKIN)), at, units.voxels(water_metres), CENTRE - SHEER - 1,
                  (poler, SKIN, CENTRE), hull.half(poler, SKIN + 1, inset=SKIN),
                  floor_at=lambda x, across: hull.floor(x, across, inset=SKIN))
    player_patch("player-barkcanoe", tools(shape, "barkcanoe", "BackSeat", "roughseas-kneel", {"rotationZ": 10}, water_metres, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, 6.5, 1.5)),
        "pole": pole,
    }, (f"Skin{LEVELS}",)))


def report(hull, laths, side):
    bark = hull.skin_square_metres()
    metres = sum(piece.get("metres", 0) for stage in laths for piece in stage["children"])
    bark_kg = kilograms("bark", bark * BARK_METRES * 1000)
    frame_kg = kilograms("wood-tuja", metres * LATH_SQUARE_METRES * 1000)
    print(f"barkcanoe: {bark:.2f} m² of bark, {bark_kg:.1f} kg; {metres:.1f} m of lath, {frame_kg:.1f} kg of white cedar; "
          f"sheet {units.metres(SHEET_LENGTH):.2f} m, {units.metres(SHEET_THICKNESS):.3f} m thick")
    return merged(afloat("barkcanoe", bark_kg + frame_kg, units.metres(SHEER), hull.litres(SHEER, inset=SKIN),
                         hull.square_metres(SHEER - 0.01, inset=SKIN), at_draft=side), shares([bark_kg, frame_kg]))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
