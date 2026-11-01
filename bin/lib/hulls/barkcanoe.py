import math

from physics import afloat, kilograms
from shape import BILGE, VOXELS_A_METRE, Hull, Shape, box, group, path, plank, span, stages

PROFILE = [(0, 0), (2.6, 0.3), (4.6, 1.5), (5.7, 3.4), (6, 6)]
SHEER = 6
CENTRE = 8
SKIN = 0.6
VOXEL_METRES = 1 / 16

GUNWALE = 0.6
SHEATHING = 0.15
SHEATHED_STRAKES = 3
RIB_WIDTH = 1.2
RIB_DEPTH = 0.45
RIBS = range(-18, 36, 4)
THWARTS = [(-8, 5.4, 5.6), (8, 5.8, 5.4), (24, 5.4, 5.6)]
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
# Vanilla's rope: one item pays out up to 10 m (ClothSystem.maxLen).
ROPE_METRES = 10
SHEET_LENGTH = 80
SHEET_THICKNESS = 0.4
LATH_SQUARE_METRES = 0.01 * 0.06
HITBOX_METRES = 0.44


def section(x, breadth, rocker, rise):
    top = SHEER + rise
    return (x, [(h * breadth, rocker + y * (top - rocker) / SHEER) for h, y in PROFILE])


STATIONS = [
    section(-26, 0.06, 4.2, 3.6),
    section(-23, 0.4, 2.2, 1.9),
    section(-16, 0.75, 0.7, 0.7),
    section(-8, 0.93, 0.1, 0.2),
    section(0, 1, 0, 0),
    section(8, 1, 0, 0),
    section(16, 1, 0, 0),
    section(24, 0.93, 0.1, 0.2),
    section(32, 0.75, 0.7, 0.7),
    section(39, 0.4, 2.2, 1.9),
    section(42, 0.06, 4.2, 3.6),
]


def lined(stations, depth):
    lined_stations = []
    for x, edges in stations:
        inner = []
        for k, (h, y) in enumerate(edges):
            (h0, y0), (h1, y1) = edges[max(k - 1, 0)], edges[min(k + 1, len(edges) - 1)]
            run, rise = h1 - h0, y1 - y0
            norm = math.hypot(run, rise) or 1
            inner.append((max(h - depth * rise / norm, 0), y + depth * run / norm))
        lined_stations.append((x, inner))
    return lined_stations


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
    return [(x, edges[:-1] + [(edges[-1][0], height(bend(edges, share), edges[-1][0]))]) for x, edges in STATIONS]


def sheer_at(stations, x):
    for (x0, e0), (x1, e1) in zip(stations, stations[1:]):
        if x0 <= x <= x1:
            return e0[-1][1] + (x - x0) / (x1 - x0) * (e1[-1][1] - e0[-1][1])
    return stations[-1][1][-1][1]


def frame_pieces(hull):
    lift = lambda x: sheer_at(hull.stations, x + 0.7) - sheer_at(STATIONS, x + 0.7)
    return [
        gunwale(hull, "InwalePort", -1, True),
        gunwale(hull, "InwaleStarboard", 1, True),
        *(thwart(n, x, half, y + lift(x)) for n, (x, half, y) in enumerate(THWARTS, 1)),
        gunwale(hull, "OutwalePort", -1, False),
        gunwale(hull, "OutwaleStarboard", 1, False),
    ]


def renamed(element, suffix):
    copy = dict(element, name=element["name"] + suffix)
    if "children" in element:
        copy["children"] = [renamed(child, suffix) for child in element["children"]]
    return copy


def raising():
    """The bark raised a step with each piece of the frame, and each piece drawn at every step from when it is laid."""
    levels = len(frame_pieces(Hull("Frame", STATIONS, CENTRE, SKIN, "bark", "bark")))
    skins = [group(f"Skin{k}", (0, 0, 0), (0, 0, 0), Hull(f"Skin{k}x", raised(k / levels), CENTRE, SKIN, "bark", "bark", edge="bark").strakes())
             for k in range(1, levels + 1)]
    at = {k: frame_pieces(Hull("Frame", frame_at(k / levels), CENTRE, SKIN, "bark", "bark")) for k in range(1, levels + 1)}
    pieces = []
    for n in range(1, levels + 1):
        final = at[levels][n - 1]
        variants = [group(f"Frame{n}At{k}", (0, 0, 0), (0, 0, 0), [renamed(e, f"At{k}") for e in at[k][n - 1]]) for k in range(n, levels + 1)]
        piece = group(f"Frame{n}", (0, 0, 0), (0, 0, 0), variants)
        piece["metres"] = round(span(final, "frame") / VOXELS_A_METRE, 2)
        pieces.append(piece)
    return group("Skin", (0, 0, 0), (0, 0, 0), skins), group("Frame", (0, 0, 0), (0, 0, 0), pieces)


def gunwale(hull, name, side, inside):
    top = len(PROFILE) - 1
    pieces = []
    for i in range(len(hull.stations) - 1):
        a, b = hull.point(i, top, side), hull.point(i + 1, top, side)
        if inside:
            a, b = ((p[0], p[1] - GUNWALE, p[2] - side * SKIN) for p in (a, b))
        else:
            a, b = ((p[0], p[1] - GUNWALE, p[2]) for p in (a, b))
        if abs(a[2] - b[2]) < 1e-6 and abs(a[2] - CENTRE) < 1e-6:
            continue
        pieces.append(plank(f"{name}{i + 1}", a, b, GUNWALE, GUNWALE, 0, side if inside else -side, "frame", "frame"))
    return pieces


def thwart(n, x, half, y):
    return [box(f"Thwart{n}", (x, y, CENTRE - half), (x + 1.4, y + 0.8, CENTRE + half), "frame", grain="z")]


def stem(name, points):
    return path(name, points, STEM_WIDTH, STEM_THICKNESS, "frame", grain="x")


def bow(points):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    return [(2 * middle - x, y) for x, y in points]


def on_centre(points, lift=0):
    return [(x, y + lift, CENTRE) for x, y in points]


def sheathing(hull):
    liner = Hull("Sheathing", lined(hull.stations, SKIN)[1:-1], CENTRE, SHEATHING, "frame", "frame", edge="frame")
    strakes = liner.strakes()
    laths = []
    for k in range(1, SHEATHED_STRAKES + 1):
        for label in ("Port", "Starboard"):
            prefix = f"Sheathing{label}{k}x"
            laths.append([strake for strake in strakes if strake["name"].startswith(prefix)])
    return laths


def ribs(hull):
    middle = (STATIONS[0][0] + STATIONS[-1][0]) / 2
    order = sorted(RIBS, key=lambda x: (abs(x + RIB_WIDTH / 2 - middle), x))
    return [hull.rib(f"Rib{n}x", x, "frame", width=RIB_WIDTH, depth=RIB_DEPTH) for n, x in enumerate(order, 1)]


def wraps(hull):
    top = len(PROFILE) - 1
    margin = 0.1
    girth = 2 * (2 * GUNWALE_METRES + BARK_METRES + GUNWALE_METRES)
    turns_per_metre = TURNS_PER_GROUP / (TURNS_PER_GROUP * ROOT_METRES + GROUP_GAP_METRES)
    turns, metres = [], 0
    for side, label in ((-1, "Port"), (1, "Starboard")):
        x, n = STATIONS[1][0] + 1, 1
        while x < STATIONS[-2][0] - 1:
            _, y, z = hull.at(x, top, side)
            z0, z1 = sorted((z + side * (GUNWALE + margin), z - side * (SKIN + GUNWALE + margin)))
            turns.append(box(f"Wrap{label}{n}", (x, y - GUNWALE - margin, z0), (x + 0.5, y + margin, z1), "lashing", grain="y"))
            x, n = x + LASHING_STEP, n + 1
        sheer = [hull.point(i, top, side) for i in range(len(hull.stations))]
        metres += sum(math.dist(a, b) for a, b in zip(sheer, sheer[1:])) * VOXEL_METRES * turns_per_metre * girth
    for name, points in (("Stern", STERN), ("Bow", bow(STERN))):
        along = on_centre(points[2:-1])
        for n, (a, b) in enumerate(zip(along, along[1:]), 1):
            steps = max(1, round(math.dist(a, b) / STEM_STITCH_STEP))
            for s in range(steps):
                f = (s + 0.5) / steps
                at = [p + f * (q - p) for p, q in zip(a, b)]
                turns.append(box(f"Stitch{name}{n}x{s + 1}", (at[0] - 0.25, at[1] - 0.25, CENTRE - 1), (at[0] + 0.25, at[1] + 0.25, CENTRE + 1),
                                 "lashing", grain="z"))
            metres += math.dist(a, b) * VOXEL_METRES / STITCH_METRES * STEM_GIRTH_METRES
    ropes = math.ceil(metres / ROPE_METRES)
    print(f"lashing: {metres:.0f} m of root in {len(turns)} wraps, {ropes} ropes of {ROPE_METRES} m")
    return [turns[len(turns) * r // ropes:len(turns) * (r + 1) // ropes] for r in range(ropes)]


def gores(hull, side):
    pieces = []
    for n, x in enumerate(GORES, 1):
        points = [hull.at(x, k, side) for k in range(1, len(PROFILE))]
        points = [(p[0], p[1], p[2] + side * 0.05) for p in points]
        pieces += path(f"Gore{'Port' if side < 0 else 'Starboard'}{n}x", points, 0.5, 0.15, "pitch", grain="y")
    return pieces


def seam(name, points):
    return path(name, on_centre(points[1:-2]), STEM_WIDTH + 0.4, STEM_THICKNESS + 0.3, "pitch", grain="x")


def build(path_):
    shape = Shape(path_, {
        "bark": "game:block/wood/bark/birch-h",
        "frame": "game:block/wood/debarked/birch",
        "lashing": "game:item/resource/rope",
        "pitch": "game:block/liquid/glue-cold",
        "bilge": BILGE,
    })
    hull = Hull("Skin", STATIONS, CENTRE, SKIN, "bark", "bark", edge="bark")
    skin, frame = raising()
    laths = [frame,
             stages("Stems", [stem("SternStem", on_centre(STERN)), stem("BowStem", on_centre(bow(STERN)))]),
             stages("Sheathing", sheathing(hull)),
             stages("Ribs", ribs(hull))]
    shape.add(sheet(), skin, laths[0], laths[1])
    shape.add(stages("Lashing", wraps(hull)))
    shape.add(laths[2], laths[3])
    shape.add(stages("Pitch", [seam("SternSeam", STERN), seam("BowSeam", bow(STERN)), gores(hull, -1), gores(hull, 1)]))
    shape.add(hull.hide_water(5.7), hull.bilge(5.7))
    shape.keep("Hull", "FrontSeat", "BackSeat")
    shape.write()
    report(hull, laths)


def report(hull, laths):
    bark = hull.skin_square_metres()
    metres = sum(piece.get("metres", 0) for stage in laths for piece in stage["children"])
    bark_kg = kilograms("bark", bark * BARK_METRES * 1000)
    frame_kg = kilograms("wood-tuja", metres * LATH_SQUARE_METRES * 1000)
    print(f"barkcanoe: {bark:.2f} m² of bark, {bark_kg:.1f} kg; {metres:.1f} m of lath, {frame_kg:.1f} kg of white cedar; "
          f"sheet {SHEET_LENGTH / VOXELS_A_METRE:.2f} m, {SHEET_THICKNESS / VOXELS_A_METRE:.3f} m thick")
    afloat("barkcanoe", bark_kg + frame_kg, SHEER / VOXELS_A_METRE, hull.litres(inset=SKIN),
           hull.square_metres(SHEER - 0.01, inset=SKIN), HITBOX_METRES)


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
