import functools
import math

from held import PADDLE, paddling, poling, tools, waterline
from stroke import player_patch, seat
from physics import LATH_SQUARE_METRES, ROPE_METRES, afloat, shares, kilograms, per_rope, ropes, stands
from fit import APART, TOLERANCE, lined as lined_section
import units
from entity import merged, record
from shape import BILGE, Hull, Shape, box, clear_of, corners_of, group, path, plank, renamed, rested, span, stages, widest
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
SHEET_THICKNESS = 0.4


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


def sheet():
    girth = sum(math.dist(a, b) for a, b in zip(PROFILE, PROFILE[1:]))
    flat, curl = girth - 2, 2.4
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    x0, x1 = middle - SHEET_LENGTH / 2, middle + SHEET_LENGTH / 2
    return group("Sheet", (0, 0, 0), (0, 0, 0), [
        box("SheetFlat", (x0, 0, CENTRE - flat), (x1, SHEET_THICKNESS, CENTRE + flat), "bark"),
        box("SheetCurlPort", (x0 + 1, 0, CENTRE - flat - curl), (x1 - 1, SHEET_THICKNESS, CENTRE - flat), "bark",
            origin=(0, 0, CENTRE - flat), rot=(28, 0, 0)),
        box("SheetCurlStarboard", (x0 + 1, 0, CENTRE + flat), (x1 - 1, SHEET_THICKNESS, CENTRE + flat + curl), "bark",
            origin=(0, 0, CENTRE + flat), rot=(-28, 0, 0)),
    ])


def bend(edges, share):
    """A half-section of bark bent up from flat by a share of its final angles, each length of it kept."""
    points = [(0.0, share * edges[0][1])]
    for (h0, y0), (h1, y1) in zip(edges, edges[1:]):
        h, y = points[-1]
        angle = share * math.atan2(y1 - y0, h1 - h0)
        reach = math.dist((h0, y0), (h1, y1))
        points.append((h + reach * math.cos(angle), y + reach * math.sin(angle)))
    return points


def height(edges, half):
    for (h0, y0), (h1, y1) in zip(edges, edges[1:]):
        if h0 <= half <= h1 and h1 > h0:
            return y0 + (half - h0) / (h1 - h0) * (y1 - y0)
    return edges[-1][1]


def raised(share):
    return [(x, bend(edges, share)) for x, edges in STATIONS]


def frame_at(share):
    """The final frame's plan, lying on the bark where it has risen to at that share."""
    return [(x, edges[:-1] + [(edges[-1][0], height(bend(edges, share), edges[-1][0]) + GUNWALE * (1 - share))]) for x, edges in STATIONS]


def sheer_at(stations, x):
    for (x0, e0), (x1, e1) in zip(stations, stations[1:]):
        if x0 <= x <= x1:
            return e0[-1][1] + (x - x0) / (x1 - x0) * (e1[-1][1] - e0[-1][1])
    return stations[-1][1][-1][1]


def frame_pieces(hull, bark, cuts):
    """Her frame on the plan of hull, seated on the bark as it is raised: inwales and thwarts inside it, outwales outside."""
    inside, outside = bark.outline("the bark's inside", inset=SKIN), bark.outline("an outwale's outside", inset=-GUNWALE)
    return [
        gunwale(hull, "InwalePort", -1, True, inside, cuts),
        gunwale(hull, "InwaleStarboard", 1, True, inside, cuts),
        *(thwart(n, x, hull, bark, cuts) for n, x in enumerate(THWARTS, 1)),
        gunwale(hull, "OutwalePort", -1, False, outside, cuts),
        gunwale(hull, "OutwaleStarboard", 1, False, outside, cuts),
    ]


def raising():
    """The bark raised a step with each piece of the frame, and each piece drawn at every step from when it is laid."""
    hull = Hull("Frame", STATIONS, CENTRE, SKIN, "bark", "bark")
    levels = len(frame_pieces(hull, hull, []))
    barks = {k: Hull(f"Skin{k}x", raised(k / levels), CENTRE, SKIN, "bark", "bark", edge="bark") for k in range(1, levels + 1)}
    cuts = {k: [] for k in barks}
    skins = [group(f"Skin{k}", (0, 0, 0), (0, 0, 0), bark.strakes(cuts[k])) for k, bark in barks.items()]
    at = {k: frame_pieces(Hull("Frame", frame_at(k / levels), CENTRE, SKIN, "bark", "bark"), barks[k], cuts[k]) for k in range(1, levels + 1)}
    pieces = []
    for n in range(1, levels + 1):
        final = at[levels][n - 1]
        variants = [group(f"Frame{n}At{k}", (0, 0, 0), (0, 0, 0), [renamed(e, f"At{k}") for e in at[k][n - 1]]) for k in range(n, levels + 1)]
        piece = group(f"Frame{n}", (0, 0, 0), (0, 0, 0), variants)
        piece["metres"] = round(units.metres(run(hull, len(PROFILE) - 1) if "wale" in final[0]["name"] else span(final, "frame")), 2)
        pieces.append(piece)
    return group("Skin", (0, 0, 0), (0, 0, 0), skins), group("Frame", (0, 0, 0), (0, 0, 0), pieces), cuts[levels]


def gunwale(hull, name, side, inside, surface, cuts):
    """A gunwale along her sheer, seated against the bark; at the ends it stops where it no longer fits beside the
    stem. Every other piece is a little lower and shorter, so that two share no plane where they meet, and no piece
    ends in the plane of an end already cut."""
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
            return max(surface.beyond(p) for p in corners_of(piece(a, b)(offset))) > TOLERANCE / 2

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
    Hull("Skin", raised(1), CENTRE, SKIN, "bark", "bark").strakes(cuts)
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


def ribs(hull):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    order = sorted(RIBS, key=lambda x: (abs(x + RIB_WIDTH / 2 - middle), x))
    return [hull.rib(f"Rib{n}x", x, "frame", width=RIB_WIDTH, depth=RIB_DEPTH, inset=SKIN + SHEATHING + APART) for n, x in enumerate(order, 1)]


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
    skin, frame, cuts = raising()
    laths = [frame,
             stages("Stems", [stem("SternStem", on_centre(stern_line())), stem("BowStem", on_centre(bow(stern_line())))]),
             sheathed(hull),
             stages("Ribs", ribs(hull))]
    flat = sheet()
    lashed = stages("Lashing", wraps(hull, cuts))
    pitched = stages("Pitch", [seam("SternSeam", stern_line()), seam("BowSeam", bow(stern_line())), gores(hull, -1), gores(hull, 1)])
    shape.add(flat, skin, *laths[:2], lashed, *laths[2:], pitched)
    levels = len(skin["children"])
    shape.together(flat)
    for k, level in enumerate(skin["children"], 1):
        bark = Hull(f"Skin{k}x", raised(k / levels), CENTRE, SKIN, "bark", "bark")
        laid = [piece["children"][k - n] for n, piece in enumerate(frame["children"][:k], 1)]
        shape.within(bark.outline(f"the bark raised to frame piece {k}"), level)
        shape.within(bark.outline("the bark's inside", inset=SKIN), laid, only=lambda name: "Outwale" not in name)
        shape.within(bark.outline("an outwale's outside", inset=-GUNWALE), laid, only=lambda name: "Outwale" in name)
        shape.together(level, laid, *((laths[1:], lashed, pitched) if k == levels else ()))
    shape.within(hull.outline("the bark's inside", inset=SKIN), laths[1])
    shape.within(Hull("Sheathing", lined(hull.stations, SKIN + APART), CENTRE, SHEATHING, "frame", "frame").outline("the sheathing"), laths[2])
    shape.within(hull.outline("the sheathing's inside", inset=SKIN + SHEATHING + APART), laths[3])
    shape.add(hull.hide_water(5.7), hull.bilge(5.7))
    shape.keep("Hull", "FrontSeat", "BackSeat", "OarStorage")
    *rising, finished = skin["children"]
    wound(shape, [("", [finished], "bark"), ("Sheet", [flat], "bark"), *((level["name"], [level], "bark") for level in rising)],
          lining=SHEATHING, framing=([laths[3]], "frame"))
    shape.write()
    record("barkcanoe", report(hull, laths))
    at = seat(shape, "BackSeat")
    line = waterline("barkcanoe")
    poler = CENTRE_THWART + POLER_AFT
    pole = poling(stands("barkcanoe", units.metres(SKIN)), at, units.voxels(line), CENTRE - SHEER - 1,
                  (poler, SKIN, CENTRE), hull.half(poler, SKIN + 1, inset=SKIN),
                  floor_at=lambda x, across: hull.floor(x, across, inset=SKIN))
    player_patch("player-barkcanoe", tools(shape, "barkcanoe", "BackSeat", "roughseas-kneel", {"rotationZ": 10}, line, {
        "paddle": ("paddling", PADDLE, paddling(PADDLE, at, 9, 3, 6.5, 1.5)),
        "pole": pole,
    }, ("Skin",)))


def report(hull, laths):
    bark = hull.skin_square_metres()
    metres = sum(piece.get("metres", 0) for stage in laths for piece in stage["children"])
    bark_kg = kilograms("bark", bark * BARK_METRES * 1000)
    frame_kg = kilograms("wood-tuja", metres * LATH_SQUARE_METRES * 1000)
    print(f"barkcanoe: {bark:.2f} m² of bark, {bark_kg:.1f} kg; {metres:.1f} m of lath, {frame_kg:.1f} kg of white cedar; "
          f"sheet {units.metres(SHEET_LENGTH):.2f} m, {units.metres(SHEET_THICKNESS):.3f} m thick")
    return merged(afloat("barkcanoe", bark_kg + frame_kg, units.metres(SHEER), hull.litres(SHEER, inset=SKIN),
                         hull.square_metres(SHEER - 0.01, inset=SKIN)), shares([bark_kg, frame_kg]))


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
