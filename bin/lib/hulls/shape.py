import copy
from bisect import bisect_left
import hashlib
import json
import math
import re
import tempfile
from pathlib import Path

import units
from fit import APART, TOLERANCE, Loft, check, coplanar, corners, depth_into, enclosed, faces, overlap, posed
from transform import add, apply, cross, direct, dot, scale, sub, unit

FACES = ("north", "east", "south", "west", "up", "down")
FACE_AXES = {
    "north": ("x", "y"), "south": ("x", "y"),
    "east": ("z", "y"), "west": ("z", "y"),
    "up": ("x", "z"), "down": ("x", "z"),
}
AXIS = {"x": 0, "y": 1, "z": 2}
ROUNDED = 0.68
ORIGIN = (8, 0, 8)
MOST_COLLISION_BOXES = 10
ABOVE = 1


def num(value):
    value = round(value, 3)
    return int(value) if value == int(value) else value


def seed(name, salt):
    return int(hashlib.md5(f"{name}/{salt}".encode()).hexdigest(), 16) % 1000 / 1000


def uv(name, face, size, grain):
    u_axis, v_axis = FACE_AXES[face]
    w, h = size[AXIS[u_axis]], size[AXIS[v_axis]]
    turned = grain == v_axis
    if turned:
        w, h = h, w
    w, h = min(w, 16), min(h, 16)
    u = math.floor(seed(name, face + "u") * (16 - w) * 2) / 2
    v = math.floor(seed(name, face + "v") * (16 - h) * 2) / 2
    rect = [num(u), num(v), num(u + w), num(v + h)]
    return rect, turned


def box(name, frm, to, tex, *, origin=None, rot=(0, 0, 0), grain="x", skip=(), children=()):
    size = [b - a for a, b in zip(frm, to)]
    textures = tex if isinstance(tex, dict) else {face: tex for face in FACES}
    faces = {}
    for face in FACES:
        if face in skip or not textures.get(face):
            continue
        rect, turned = uv(name, face, size, grain)
        faces[face] = {"texture": "#" + textures[face], "uv": rect}
        if turned:
            faces[face]["rotation"] = 90
    element = {"name": name, "from": [num(c) for c in frm], "to": [num(c) for c in to]}
    if any(rot) or origin is not None:
        element["rotationOrigin"] = [num(c) for c in (origin if origin is not None else frm)]
    for axis, angle in zip("XYZ", rot):
        if angle:
            element["rotation" + axis] = num(angle)
    element["faces"] = faces
    if children:
        element["children"] = list(children)
    return element


def run(name, frm, to, tex, *, axis="x", step=units.voxels(1), **options):
    i = AXIS[axis]
    pieces, start, n = [], frm[i], 1
    while start < to[i] - 1e-6:
        end = min(start + step, to[i])
        a, b = list(frm), list(to)
        a[i], b[i] = start, end
        pieces.append(box(f"{name}{n}", a, b, tex, grain=axis, **options))
        start, n = end, n + 1
    return pieces


def mirror(element, centre=0):
    flipped = dict(element)
    flipped["from"] = [num(2 * centre - element["to"][0])] + element["from"][1:]
    flipped["to"] = [num(2 * centre - element["from"][0])] + element["to"][1:]
    if "rotationOrigin" in element:
        flipped["rotationOrigin"] = [num(2 * centre - element["rotationOrigin"][0])] + element["rotationOrigin"][1:]
    for axis in ("rotationY", "rotationZ"):
        if axis in element:
            flipped[axis] = num(-element[axis])
    faces = element["faces"]
    flipped["faces"] = {{"east": "west", "west": "east"}.get(face, face): value for face, value in faces.items()}
    if "children" in element:
        assert element["from"] == element["to"], f"{element['name']} has children and a size"
        flipped["children"] = [mirror(child) for child in element["children"]]
    return flipped


def group(name, at, rot, children):
    return box(name, at, at, {}, origin=at, rot=rot, children=children)


def renamed(element, suffix):
    copy = dict(element, name=element["name"] + suffix)
    if "children" in element:
        copy["children"] = [renamed(child, suffix) for child in element["children"]]
    return copy


def bounds(*elements):
    """The corners of the box round the elements as they are posed."""
    points = [corner for element, m in posed(flattened(elements)) for corner in corners(element, m)]
    return tuple([num(pick(point[axis] for point in points)) for axis in range(3)] for pick in (min, max))


def hull_box(*elements):
    """The box round her hull's posed corners, which the mechanics measure her by, and that box as her entity takes it:
    from the point she stands at, in metres and in voxels."""
    return hull_of(*bounds(*elements))


def hull_of(frm, to):
    """The Hull element spanning frm to to, and that box as her entity takes it."""
    element = {"name": "Hull", "from": frm, "to": to, "faces": {},
               "attachmentpoints": [{"code": "HullAP", "posX": str(num((to[0] - frm[0]) / 2)), "posY": "0", "posZ": str(num((to[2] - frm[2]) / 2)),
                                     "rotationX": "0", "rotationY": "0", "rotationZ": "0"}]}
    voxels = {end: [num(c - o) for c, o in zip(corner, ORIGIN)] for end, corner in (("from", frm), ("to", to))}
    measured = {"hullBoxMetres": {end: [num(units.metres(c)) for c in corner] for end, corner in voxels.items()}, "hullBoxVoxels": voxels}
    placed = collision(element)
    placed["behaviorConfigs"]["roughseas:vesselintegrity"] = measured
    return element, placed


def reachable(elements):
    hull = next(element for element in elements if element["name"] == "Hull")
    boxes = [element for element in elements if element is not hull and element.get("attachmentpoints") and not element.get("faces")]
    stowage = next(box for box in boxes if box["name"] == "OarStorage")
    stowage["to"] = [stowage["to"][0], stowage["to"][1], num(hull["to"][2] - APART)]
    by_size = sorted(boxes, key=footprint, reverse=True)
    for at, box in enumerate(by_size):
        under = [hull, *(larger for larger in by_size[:at] if overlaps(box, larger))]
        box["to"] = [box["to"][0], num(max(box["to"][1], max(other["to"][1] for other in under) + ABOVE)), box["to"][2]]


def rest(elements):
    hull = next(element for element in elements if element["name"] == "Hull")
    stowage = next(element for element in elements if element["name"] == "OarStorage")
    middle = [num((hull["from"][0] + hull["to"][0]) / 2 - stowage["from"][0]), 0, 0]
    stowage["children"] = [{"name": "OarRest", "from": middle, "to": middle, "faces": {}}]


def footprint(box):
    return (box["to"][0] - box["from"][0]) * (box["to"][2] - box["from"][2])


def overlaps(box, other):
    return all(box["from"][axis] < other["to"][axis] and other["from"][axis] < box["to"][axis] for axis in (0, 2))


def side_area(elements, low=-math.inf, high=math.inf, step=0.5):
    """The square metres of the elements as seen from abeam, between the heights low and high (voxels): above her
    waterline what the wind meets, below it what the water does."""
    return units.metres(len(profile(elements, low, high, step)) * step * step, 2)


def profile(elements, low=-math.inf, high=math.inf, step=0.5, across=0):
    cells = set()
    for element, m in posed(flattened(elements)):
        outline = convex([(point[across], point[1]) for point in corners(element, m)])
        (x0, y0), (x1, y1) = ([pick(p[axis] for p in outline) for axis in range(2)] for pick in (min, max))
        for i in range(math.floor(x0 / step), math.ceil(x1 / step)):
            for j in range(max(math.floor(y0 / step), math.ceil(low / step) if low > -math.inf else math.floor(y0 / step)),
                           min(math.ceil(y1 / step), math.floor(high / step) if high < math.inf else math.ceil(y1 / step))):
                if (i, j) not in cells and enclosed(((i + 0.5) * step, (j + 0.5) * step), outline):
                    cells.add((i, j))
    return cells


def convex(points):
    points = sorted(set(points))
    if len(points) < 3:
        return points
    turn = lambda o, a, b: (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower, upper = [], []
    for point in points:
        while len(lower) >= 2 and turn(lower[-2], lower[-1], point) <= 0:
            lower.pop()
        lower.append(point)
    for point in reversed(points):
        while len(upper) >= 2 and turn(upper[-2], upper[-1], point) <= 0:
            upper.pop()
        upper.append(point)
    return lower[:-1] + upper[:-1]


def windage(elements, keel_voxels):
    """What the wind meets of the elements above her waterline, at the draft afloat finds her at."""
    return lambda draft_metres: {"behaviorConfigs": {"roughseas:seaforces": units.both("windage", side_area(elements, keel_voxels + units.voxels(draft_metres)), 2)}}


def collision(box, *more):
    """Her hitbox, as wide as she is and as tall, and her collision boxes in the entity's frame, where her bow points
    along +z: the hull box from her origin up, in squares as wide as she is spread from end to end, and the box round
    each of more (an outrigger's float) in as many squares as her hull leaves of vanilla's ten. Vanilla turns only each
    box's middle with her yaw, so any other piece turned across her reaches past her; and a box starting above her
    origin rests on the ground by float rounding, which lets her sink through the block under her."""
    def entity(frm, to):
        (x0, y0, z0), (x1, y1, z1) = frm, to
        return {"x1": num(units.metres(ORIGIN[2] - z1)), "y1": num(units.metres(y0)), "z1": num(units.metres(ORIGIN[0] - x1)),
                "x2": num(units.metres(ORIGIN[2] - z0)), "y2": num(units.metres(y1)), "z2": num(units.metres(ORIGIN[0] - x0))}

    def squares(frm, to):
        return math.ceil((to[0] - frm[0]) / (to[2] - frm[2]) - TOLERANCE)

    def cut(frm, to, pieces):
        (x0, y0, z0), (x1, y1, z1) = frm, to
        side = min(z1 - z0, x1 - x0)
        step = (x1 - x0 - side) / max(1, pieces - 1)
        return [entity((x0 + step * n, y0, z0), (x0 + step * n + side, y1, z1)) for n in reversed(range(pieces))]

    (x0, y0, z0), (x1, y1, z1) = box["from"], box["to"]
    keel = ([x0, ORIGIN[1], z0], [x1, y1, z1])
    boxes = cut(*keel, squares(*keel))
    left = (MOST_COLLISION_BOXES - len(boxes)) // max(1, len(more))
    assert left > 0 or not more, f"no collision box left for {len(more)} more after her hull's {len(boxes)}"
    for element in more:
        boxes += cut(*bounds(element), min(squares(*bounds(element)), left))
    assert len(boxes) <= MOST_COLLISION_BOXES, f"{len(boxes)} collision boxes, vanilla's physics takes {MOST_COLLISION_BOXES}"
    return {"hitboxSize": {"x": num(units.metres(z1 - z0)), "y": num(units.metres(y1 - y0))},
            "behaviorConfigs": {"passivephysicsmultibox": {"collisionBoxes": boxes}}}


def stages(name, pieces, wood="frame"):
    """A stage's pieces, each its own group; a piece of wood names the metres of lath or pole it takes, the length
    drawn in that texture along its run."""
    children = []
    for n, piece in enumerate(pieces, 1):
        child = group(f"{name}{n}", (0, 0, 0), (0, 0, 0), piece)
        metres = units.metres(span(piece, wood))
        if metres > 0:
            child["metres"] = round(metres, 2)
        children.append(child)
    return group(name, (0, 0, 0), (0, 0, 0), children)


def span(elements, tex):
    total = 0
    for element in elements:
        children = element.get("children", [])
        if element["name"].endswith("Frame") and children:
            board = children[0]
            if any(face["texture"] == "#" + tex for face in board["faces"].values()):
                total += board["to"][0] + board["from"][0]
        elif children:
            total += span(children, tex)
        elif any(face["texture"] == "#" + tex for face in element["faces"].values()):
            total += max(b - a for a, b in zip(element["from"], element["to"]))
    return total


def heading(start, end):
    dx, dy, dz = (e - s for s, e in zip(start, end))
    level = math.hypot(dx, dz)
    return math.degrees(math.atan2(-dz, dx)), math.degrees(math.atan2(dy, level)), math.hypot(level, dy)


def plank(name, start, end, height, thickness, flare, side, outer, inner, *, edge=None, overlap=0.25):
    yaw, pitch, length = heading(start, end)
    z0, z1 = (0, thickness) if side < 0 else (-thickness, 0)
    out_face, in_face = ("north", "south") if side < 0 else ("south", "north")
    edge = edge or inner
    tex = {out_face: outer, in_face: inner, "up": edge, "down": outer, "east": edge, "west": edge}
    board = box(name, (-overlap, 0, z0), (length + overlap, height, z1), tex,
                origin=(0, 0, 0), rot=(side * flare, 0, 0))
    return group(name + "Frame", start, (0, yaw, pitch), [board])


def beam(name, start, end, width, height, tex, *, ties=(), tie=None, tie_tex=None, overlap=0.25, grain="x",
         rounded=False):
    yaw, pitch, length = heading(start, end)
    w, h = width / 2, height / 2
    roundness = ROUNDED if rounded else None
    parts = section(name, (-overlap, 0, 0), length + 2 * overlap, w, h, tex, grain, roundness)
    tie_width, margin = tie or (0.8, 0.35)
    for n, f in enumerate(ties, 1):
        parts += section(f"{name}Tie{n}", (f * length - tie_width / 2, 0, 0), tie_width, w + margin, h + margin, tie_tex, "z", roundness)
    return group(name + "Frame", start, (0, yaw, pitch), parts)


def section(name, at, length, w, h, tex, grain="x", roundness=None):
    """Boxes along x from at, round the axis through it: one 2w by 2h, or two crossed, the second roundness as wide
    as the first is high, with every corner on the ellipse of half-axes w and h."""
    x, y, z = at
    if roundness is None:
        return [box(name, (x, y - h, z - w), (x + length, y + h, z + w), tex, grain=grain)]
    w, h = w / math.hypot(1, roundness), h / math.hypot(1, roundness)
    return [box(name, (x, y - h * roundness, z - w), (x + length, y + h * roundness, z + w), tex, grain=grain),
            box(name + "Round", (x + APART, y - h, z - w * roundness), (x + length - APART, y + h, z + w * roundness), tex, grain=grain)]


def paddle(at, rot, shaft, blade, tex):
    x = throat(shaft)[0]
    return group("Paddle", at, rot, [
        box("PaddleShaft", (0, 0, 0), (shaft, 0.7, 0.7), tex),
        box("PaddleThroat", (x - 1, -0.1, -0.1), (x + 1, 0.8, 0.8), tex),
        box("PaddleBlade", (shaft, -0.1, -1.3), (shaft + blade, 0.5, 2), tex),
        box("PaddleGrip", (-1.2, -0.2, -0.5), (0, 0.9, 1.2), tex),
    ])


def throat(shaft):
    return (0.6 * shaft, 0.35, 0.35)


def path(name, points, width, height, tex, *, continues=False, **options):
    """Beams through the points, every other one a little thinner so that neighbours share no plane where they meet;
    one that continues a beam already drawn starts thinner."""
    return [beam(f"{name}{i}", a, b, (width(i) if callable(width) else width) - shrunk(i, continues),
                 (height(i) if callable(height) else height) - shrunk(i, continues), tex, **options)
            for i, (a, b) in enumerate(zip(points, points[1:]), 1)]


def shrunk(i, continues=False):
    return 2 * APART * ((i + continues) % 2 == 0)


def water_plane(name, x0, x1, z0, z1, height, texture="null"):
    uv = [0, 0, 16, 16] if texture == "null" else [0, 0, num(min(x1 - x0, 16)), num(min(z1 - z0, 16))]
    return {"name": name, "from": [num(x0), num(height), num(z0)], "to": [num(x1), num(height), num(z1)],
            "faces": {"up": {"texture": "#" + texture, "uv": uv}}}


BILGE = "game:block/liquid/waterportion"
BILGE_LEVELS = 8
BILGE_BELOW_MASK = 0.4


def bilge(floor, mask, planes):
    """The water aboard: BILGE_LEVELS stacked groups, Bilge1 to BilgeN, from just above the floor to just under
    the mask; the bilge shows one of them, by how full it is. planes(prefix, height) lays one level's surface."""
    top = mask - BILGE_BELOW_MASK
    levels = [group(f"Bilge{n}", (0, 0, 0), (0, 0, 0), planes(f"Bilge{n}x", floor + (top - floor) * n / BILGE_LEVELS))
              for n in range(1, BILGE_LEVELS + 1)]
    return group("Bilge", (0, 0, 0), (0, 0, 0), levels)


def bilge_levels(elements):
    """The height above her origin of each level of her bilge's water, as her bilge config takes them."""
    levels = next(element for element in elements if element["name"] == "Bilge")["children"]
    heights = [level["children"][0]["from"][1] for level in levels]
    return {"behaviorConfigs": {"roughseas:bilge": {"levelHeightsMetres": [num(units.metres(h)) for h in heights],
                                                    "levelHeightsVoxels": [num(h) for h in heights]}}}


def across(start, end, up):
    yaw, pitch = (math.radians(angle) for angle in heading(start, end)[:2])
    along = [math.cos(pitch) * math.cos(yaw), math.sin(pitch), -math.cos(pitch) * math.sin(yaw)]
    rising = [-math.sin(pitch) * math.cos(yaw), math.cos(pitch), math.sin(pitch) * math.sin(yaw)]
    outward = [math.sin(yaw), 0, math.cos(yaw)]
    dot = lambda u, v: sum(p * q for p, q in zip(u, v))
    square = [p - dot(up, along) * q for p, q in zip(up, along)]
    return math.sqrt(dot(square, square)), math.degrees(math.atan2(dot(square, outward), dot(square, rising)))


def rise(a, b, c, d):
    return [((p - q) + (r - s)) / 2 for p, q, r, s in zip(d, a, c, b)]


def clear_of(cuts, along, made, slide=False, carried=(False, False)):
    """made(short), drawn back at either end by short until neither end's faces share a plane with an end already cut,
    or, sliding, moved along it whole; an end carried on into the next piece goes on past the cut instead of opening a
    gap. made returns the element's parts with its two ends last. A cut is an end face, its point, normal and corners,
    which clashes with a face turned the same way that it overlaps; or a plane, its point and normal alone, which
    clashes with any end in it. The ends' faces are added to cuts."""
    short = [0, 0]
    while True:
        parts = made(short)
        ends = end_faces([part for part in parts[:-2] if isinstance(part, (dict, list))], along)
        clash = [any(cutting(face, cut) for face in faces for cut in cuts) for faces in ends]
        if not any(clash):
            cuts.extend(face for faces in ends for face in faces)
            return parts
        short = [short[0] + APART, short[1] - APART] if slide else [s + APART * hit * (-1 if on else 1) for s, hit, on in zip(short, clash, carried)]


def end_faces(elements, along):
    ends = ([], [])
    for element, m in posed(flattened(elements)):
        for normal, points in faces(element, m):
            facing = dot(normal, along)
            if abs(facing) > 0.999:
                ends[facing > 0].append((points[0], normal, points))
    return ends


def cutting(face, cut):
    point, normal, points = face
    at, other, *corners = cut
    if abs(dot(sub(point, at), other)) >= APART - 1e-9:
        return False
    if not corners:
        return abs(dot(normal, other)) > 0.999
    return dot(normal, other) > 0.999 and overlap(points, corners[0], normal) > TOLERANCE


def ends_of(elements):
    """The faces at either end of each box along its own length, as clear_of keeps them."""
    return [(points[0], normal, points) for element, m in posed(elements) for normal, points in faces(element, m)
            if abs(dot(normal, direct(m, (1, 0, 0)))) > 0.999]


def widest(made, surface, reach):
    """The widest made(width), up to reach, whose every corner lies inside the surface."""
    low, high = 0.0, reach
    for _ in range(30):
        low, high = ((low + high) / 2, high) if max(surface.beyond(p) for p in corners_of(made((low + high) / 2))) <= 0 else (low, (low + high) / 2)
    return low


def seated(made, surface, depth=0, clear=()):
    """How far in, from depth on, the element made(depth) has to sit for every corner to lie inside the surface and
    the element to pass into none of the boxes it is to clear, each its corners and bounds as fit.bounded gives them."""
    for _ in range(50):
        points = corners_of(made(depth))
        out = max(surface.beyond(p) for p in points)
        if clear:
            low, high = [min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]
            out = max([out, *(depth_into(points, box) for box, box_low, box_high in clear
                              if all(low[i] < box_high[i] and box_low[i] < high[i] for i in range(3)))])
        if out <= TOLERANCE / 2:
            break
        depth += out
    return depth


def rested(made, surface):
    """Where the element made(offset) comes to rest inside the surface, moved each time toward the surface's nearest
    point by as far as its outermost corner stands beyond it."""
    offset = [0, 0, 0]
    for _ in range(50):
        worst = max(corners_of(made(offset)), key=surface.beyond)
        out = surface.beyond(worst)
        if out <= TOLERANCE / 2:
            break
        offset = add(offset, scale(surface.inward(worst), out))
    return offset


def corners_of(element):
    return [corner for part, m in posed([element]) for corner in corners(part, m)]


def breadth(edges, y):
    for (h0, y0), (h1, y1) in zip(edges, edges[1:]):
        if y0 <= y <= y1 and y1 > y0:
            return h0 + (y - y0) / (y1 - y0) * (h1 - h0)
    return edges[-1][0] if y > edges[-1][1] else edges[0][0]


class Hull:
    def __init__(self, prefix, stations, centre_z, thickness, outer, inner, edge=None):
        self.prefix = prefix
        self.stations = stations
        self.cz = centre_z
        self.t = thickness
        self.outer = outer
        self.inner = inner
        self.edge = edge

    def point(self, station, edge, side):
        x, edges = self.stations[station]
        half, y = edges[edge]
        return (x, y, self.cz + side * half)

    def at(self, x, edge, side):
        for i in range(len(self.stations) - 1):
            a, b = self.point(i, edge, side), self.point(i + 1, edge, side)
            if a[0] <= x <= b[0]:
                f = (x - a[0]) / (b[0] - a[0])
                return tuple(p + f * (q - p) for p, q in zip(a, b))
        raise ValueError(f"x {x} lies outside the hull")

    def strakes(self, cuts=None, clear=()):
        """The skin, panel by panel between neighbouring stations and edges. A tapering panel takes a board along each
        of its edges, each as tall as the panel's narrow end, the upper one hanging; each board reaches as far past a
        station as that station's line leans out, stops at the hull's ends, and sits inward until its outermost corner
        touches the loft and it passes into none of the boxes to clear, and no nearer the depth of a neighbour it
        overlaps than to keep their faces apart. Each board's ends keep clear of those already cut, the ones in cuts
        too, which collects them: an end inside the hull goes on past such a cut into the next board, so no gap opens
        through the skin, and the board after it starts as far on as it went."""
        loft = self.outline("the hull", ends=False)
        hull = Hull(self.prefix, self.refined(), self.cz, self.t, self.outer, self.inner, self.edge)
        panels = [panel for panel in hull.panels() if across(panel[4][0], panel[4][1], rise(*panel[4]))[0] >= 1e-6]
        last = len(hull.stations) - 2
        bottoms, tops, cuts = {}, {}, [] if cuts is None else cuts
        carries = {}

        def placed(boards, key, *args, **options):
            start, end = args[2][:2]
            side, k, i = key
            after = carries.get((boards is tops, side, k, i - 1), 0)
            shorts = []

            def made(short):
                shorts.append(short)
                return self.board(*args, **options, short=(short[0] + after, short[1]), clear=clear)

            boards[key] = clear_of(cuts, unit(sub(end, start)), made, carried=(False, not args[4][1]))
            carries[boards is tops, side, k, i] = max(0, -shorts[-1][1])

        quads = {(side, k, i): quad for _, side, k, i, quad in panels}

        def straight(side, k, i, j):
            """Whether the panel runs on into its neighbour j without a bend, its edges in line through their station."""
            if (side, k, j) not in quads:
                return False
            (a, b, c, d), (e, f, g, h) = quads[side, k, i], quads[side, k, j]
            return all(dot(unit(sub(q, p)), unit(sub(s, r))) > 1 - 1e-9 for (p, q), (r, s) in (((a, b), (e, f)), ((d, c), (h, g))))

        def flush(side, k, i):
            return straight(side, k, i, i - 1), straight(side, k, i, i + 1)

        def sharing(board, beside):
            named = {element["name"] for element, _ in posed([board])}
            return any(coplanar([board, *beside], among=named))

        for name, side, k, i, (a, b, c, d) in panels:
            near = [bottoms[side, k, i - 1][0]] if (side, k, i - 1) in bottoms else []
            below = 0
            while True:
                placed(bottoms, (side, k, i), name, side, (a, b, d, c), 1, (i == 0, i == last), loft, near, below, aside=APART * (i % 2), flush=flush(side, k, i))
                if not sharing(bottoms[side, k, i][1], [bottoms[side, k, i - 1][1]] if (side, k, i - 1) in bottoms else []):
                    break
                below = bottoms[side, k, i][0] + APART
        for name, side, k, i, (a, b, c, d) in panels:
            if abs(across(a, b, sub(d, a))[0] - across(a, b, sub(c, b))[0]) <= TOLERANCE:
                continue
            near = [bottoms[key][0] for key in ((side, k, i - 1), (side, k, i + 1)) if key in bottoms]
            near += [tops[side, k, i - 1][0]] if (side, k, i - 1) in tops else []
            below = bottoms[side, k, i][0] + APART
            while True:
                placed(tops, (side, k, i), name + "Top", side, (d, c, a, b), -1, (i == 0, i == last), loft, near, below,
                       aside=APART * (i % 2), flush=flush(side, k, i))
                beside = [boards[key][1] for boards, key in ((bottoms, (side, k, i)), (bottoms, (side, k, i - 1)), (bottoms, (side, k, i + 1)), (tops, (side, k, i - 1))) if key in boards]
                if not sharing(tops[side, k, i][1], beside):
                    break
                below = tops[side, k, i][0] + APART
        return [made[1] for made in (*bottoms.values(), *tops.values())]

    def refined(self):
        """The stations, with more between two of them wherever a panel tapers more than its two boards can cover: each
        added where the panel's narrow end has grown twice as tall."""
        stations = [self.stations[0]]
        for (x0, _), (x1, _) in zip(self.stations, self.stations[1:]):
            stations += [(x, self.section(x)) for x in self.split(x0, x1)]
        return stations

    def split(self, x0, x1):
        for k in range(len(self.stations[0][1]) - 1):
            a, b, c, d = self.at(x0, k, 1), self.at(x1, k, 1), self.at(x1, k + 1, 1), self.at(x0, k + 1, 1)
            low, high = sorted((across(a, b, sub(d, a))[0], across(a, b, sub(c, b))[0]))
            hanging = min(across(d, c, sub(a, d))[0], across(d, c, sub(b, c))[0])
            if low + hanging < high - TOLERANCE and hanging > 1e-6:
                narrow = x0 if across(a, b, sub(d, a))[0] < across(a, b, sub(c, b))[0] else x1
                x = narrow + (x1 + x0 - 2 * narrow) * hanging / (high - low)
                return self.split(min(x0, x), max(x0, x)) + self.split(min(x, x1), max(x, x1))
        return [x1]

    def panels(self):
        for side, label in ((-1, "Port"), (1, "Starboard")):
            for k in range(len(self.stations[0][1]) - 1):
                for i in range(len(self.stations) - 1):
                    yield f"{self.prefix}{label}{k + 1}x{i + 1}", side, k, i, (
                        self.point(i, k, side), self.point(i + 1, k, side), self.point(i + 1, k + 1, side), self.point(i, k + 1, side))

    def board(self, name, side, quad, turn, ends, loft, near, below=0, short=(0, 0), aside=0, flush=(False, False), clear=()):
        a, b, d, c = quad
        along = unit(sub(b, a))
        up = rise(a, b, c, d)
        rise_a, rise_b = across(a, b, sub(d, a))[0], across(a, b, sub(c, b))[0]
        height, flare = min(rise_a, rise_b), across(a, b, up)[1]
        start = a if flush[0] else sub(a, scale(along, max(0, -dot(sub(d, a), along) * height / rise_a)))
        end = b if flush[1] else add(b, scale(along, max(0, dot(sub(c, b), along) * height / rise_b)))
        lift = unit(sub(up, scale(along, dot(up, along))))
        start, end, height = add(start, scale(lift, aside)), add(end, scale(lift, aside)), height - aside
        normal = unit(cross(along, lift))
        normal = scale(normal, math.copysign(1, dot(normal, (0, 1, self.cz - (a[2] + b[2]) / 2))))
        made = lambda s, e: plank(name, s, e, height, self.t, turn * side * flare, turn * side, self.outer, self.inner, edge=self.edge, overlap=0)
        x0, x1 = self.span()
        reach = corners_of(made(start, end))
        past = max(0, x0 - min(p[0] for p in reach)), max(0, max(p[0] for p in reach) - x1)
        start = add(start, scale(along, (past[0] + APART * (past[0] > 0 and not ends[0])) / along[0]))
        end = sub(end, scale(along, (past[1] + APART * (past[1] > 0 and not ends[1])) / along[0]))
        start, end = add(start, scale(along, short[0])), sub(end, scale(along, short[1]))
        sunk = seated(lambda depth: made(add(start, scale(normal, depth)), add(end, scale(normal, depth))), loft, clear=clear)
        depth = max(sunk, below)
        while clashing := [other for other in near if abs(depth - other) < APART - 1e-9]:
            depth = max(clashing) + APART
        board = made(add(start, scale(normal, depth)), add(end, scale(normal, depth)))
        if abs(a[2] - self.cz) < 1e-6 and abs(b[2] - self.cz) < 1e-6:
            del board["children"][0]["faces"]["down"]
        return depth, board, start, end

    def rail(self, name, tex, height=1, thickness=None, lift=0, inset=0):
        elements = []
        top = len(self.stations[0][1]) - 1
        thickness = thickness or self.t + 0.5
        inside = self.outline("the skin's inside", inset=inset, ends=False)
        for side, label in ((-1, "Port"), (1, "Starboard")):
            for i in range(len(self.stations) - 1):
                a, b = self.point(i, top, side), self.point(i + 1, top, side)
                if abs(a[2] - b[2]) < 1e-6 and a[2] == self.cz:
                    continue
                made = lambda depth, a=a, b=b, side=side, label=label, i=i: plank(
                    f"{name}{label}{i + 1}", (a[0], a[1] + lift, a[2] - side * depth), (b[0], b[1] + lift, b[2] - side * depth),
                    height, thickness, 0, side, tex, tex, overlap=0)
                elements.append(made(seated(made, inside, inset) if inset else 0))
        return elements

    def hide_water(self, height):
        return group("hideWater", (0, 0, 0), (0, 0, 0), self.surface("hideWater", height))

    def bilge(self, mask):
        floor = min(edges[0][1] for _, edges in self.stations) + self.t
        return bilge(floor, mask, lambda prefix, height: self.surface(prefix, height, "bilge"))

    def surface(self, prefix, height, texture="null"):
        planes = []
        for i in range(len(self.stations) - 1):
            (x0, edges0), (x1, edges1) = self.stations[i], self.stations[i + 1]
            if not (edges0[0][1] < height < edges0[-1][1] and edges1[0][1] < height < edges1[-1][1]):
                continue
            half = min(breadth(edges0, height), breadth(edges1, height)) - self.t
            if half > 0:
                planes.append(water_plane(f"{prefix}{i + 1}", x0, x1, self.cz - half, self.cz + half, height, texture))
        return planes

    def section(self, x):
        xs = [at for at, _ in self.stations]
        if not xs[0] <= x <= xs[-1]:
            raise ValueError(f"x {x} lies outside the hull")
        i = max(0, bisect_left(xs, x) - 1)
        (x0, edges0), (x1, edges1) = self.stations[i], self.stations[i + 1]
        f = (x - x0) / (x1 - x0)
        drawn = []
        for (h0, y0), (h1, y1) in zip(edges0, edges1):
            z0, z1 = self.cz + h0, self.cz + h1
            drawn.append((abs(z0 + f * (z1 - z0) - self.cz), y0 + f * (y1 - y0)))
        return drawn

    def half(self, x, y, inset=0):
        edges = self.section(x)
        if y < edges[0][1] + inset or y > edges[-1][1]:
            return 0
        return max(0, breadth(edges, y) - inset)

    def floor(self, x, across, inset=0, step=0.1):
        edges = self.section(x)
        y = edges[0][1] + inset
        while y < edges[-1][1] and self.half(x, y, inset) < across:
            y += step
        return y

    def span(self):
        return self.stations[0][0], self.stations[-1][0]

    def outline(self, name, inset=0, ends=True):
        return Loft(name, self.section, self.span(), self.cz, inset, ends)

    def litres(self, top=None, inset=0, step=0.25):
        x0, x1 = self.span()
        total, x = 0, x0 + step / 2
        while x < x1:
            edges = self.section(x)
            high = min(top, edges[-1][1]) if top is not None else edges[-1][1]
            y = edges[0][1] + step / 2
            while y < high:
                total += 2 * self.half(x, y, inset) * step * step
                y += step
            x += step
        return units.litres(units.metres(total, 3))

    def square_metres(self, height, inset=0, step=0.25):
        x0, x1 = self.span()
        total, x = 0, x0 + step / 2
        while x < x1:
            total += 2 * self.half(x, height, inset) * step
            x += step
        return units.metres(total, 2)

    def skin_square_metres(self):
        return units.metres(sum(math.sqrt(dot(spread := cross(sub(c, a), sub(d, b)), spread)) / 2 for *_, (a, b, c, d) in self.panels()), 2)

    def rib(self, name, x, tex, width=1, depth=1, inset=None, clear=()):
        """A rib at x, a piece along each edge of the section, each sunk until it lies inside the skin (and what lines
        it, inset deep); the pieces are of three widths in turn, so that no two share a plane where they meet, nor two
        a piece apart where a sharp section brings them together."""
        edges = self.section(x)
        inset = self.t if inset is None else inset
        inside = self.outline("the skin's inside", inset=inset, ends=False)
        elements = []
        for side, label in ((-1, "Port"), (1, "Starboard")):
            for k in range(len(edges) - 1):
                (h0, y0), (h1, y1) = edges[k], edges[k + 1]
                rise, spread = y1 - y0, h1 - h0
                length = math.hypot(rise, spread)
                if length < 1e-6:
                    continue
                flare = math.degrees(math.atan2(spread, rise))
                z = self.cz + side * h0
                z0, z1 = sorted((z - side * inset, z - side * (inset + depth)))
                half = width / 2 - APART * ((k + 2 * (side > 0)) % 3)
                normal = unit([0, spread, -side * rise])
                made = lambda s, z0=z0, z1=z1, y0=y0, z=z, half=half, normal=normal: box(
                    f"{name}{label}{k + 1}", (x - half, y0 + normal[1] * s, z0 + normal[2] * s), (x + half, y0 + length + normal[1] * s, z1 + normal[2] * s),
                    tex, origin=(x, y0 + normal[1] * s, z + normal[2] * s), rot=(side * flare, 0, 0), grain="y")
                elements.append(made(seated(made, inside, clear=clear)))
        return elements

    def transom(self, name, x, thickness, tex, step=1.5):
        edges = self.section(x)
        top = edges[-1][1]
        elements, y, n = [], edges[0][1], 1
        while y < top - 1e-6:
            high = min(y + step, top)
            half = breadth(edges, (y + high) / 2)
            elements.append(box(f"{name}{n}", (x - thickness, y, self.cz - half), (x, high, self.cz + half), tex, grain="z"))
            y, n = high, n + 1
        return elements


class Shape:
    def __init__(self, path, textures):
        self.path = Path(path)
        self.textures = textures
        self.elements = []
        self.animations = []
        self.fits = []
        self.views = []
        self.aparts = []

    def within(self, surface, *elements, only=lambda name: True):
        self.fits.append((surface, flattened(elements), only))

    def together(self, *elements):
        self.views.append(flattened(elements))

    def apart(self, first, second, pressed=0):
        self.aparts.append((flattened([first]), flattened([second]), pressed))

    def add(self, *elements):
        for element in elements:
            if isinstance(element, list):
                self.elements.extend(element)
            else:
                self.elements.append(element)

    def turn_about(self, centre):
        self.elements = [mirror(element, centre) for element in self.elements]

    def keep(self, *names, source=None):
        current = {e["name"]: e for e in json.loads(Path(source or self.path).read_text())["elements"]}
        for name in names:
            self.elements.append(current[name])

    def write(self):
        reachable(self.elements)
        rest(self.elements)
        faults = check(self.fits, self.views, self.aparts)
        if faults:
            raise SystemExit(f"{self.path.name}: its boxes leave the round shapes they build, pass into each other or share a plane\n  "
                             + "\n  ".join(faults))
        shape = {
            "editor": {"allAngles": False, "entityTextureMode": False},
            "textureWidth": 16,
            "textureHeight": 16,
            "textures": self.textures,
            "elements": self.elements,
        }
        if self.animations:
            shape["animations"] = self.animations
        write(self.path, shape)
        print(f"{self.path}: {count(self.elements)} elements")


def write(path, shape):
    text = json.dumps(shape, indent=1)
    for leaf in (r"[\[{]\n[^\[\]{}]*?\n\s*[\]}]", r"\{\n[^{}]*?\n\s*\}"):
        text = re.sub(leaf, lambda m: " ".join(m.group(0).split()), text)
    save(path, text + "\n")


def save(path, text):
    """Replaces the file whole, in one step: the vessels build side by side and read each other's files."""
    with tempfile.NamedTemporaryFile("w", dir=path.parent, prefix=path.name, suffix=".part", delete=False) as part:
        part.write(text)
    Path(part.name).replace(path)


def flattened(elements):
    return copy.deepcopy([e for element in elements for e in (element if isinstance(element, list) else [element])])


def count(elements):
    return sum(1 + count(e.get("children", [])) for e in elements)
