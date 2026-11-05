import copy
import hashlib
import json
import math
import re
from pathlib import Path

import units
from fit import APART, TOLERANCE, Loft, check, corners, posed
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
               "attachmentpoints": [{"code": "HullAP", "posX": str(num(ORIGIN[0] - frm[0])), "posY": "0", "posZ": str(num((to[2] - frm[2]) / 2)),
                                     "rotationX": "0", "rotationY": "0", "rotationZ": "0"}]}
    voxels = {end: [num(c - o) for c, o in zip(corner, ORIGIN)] for end, corner in (("from", frm), ("to", to))}
    measured = {"hullBoxMetres": {end: [num(units.metres(c)) for c in corner] for end, corner in voxels.items()}, "hullBoxVoxels": voxels}
    return element, {"behaviorConfigs": {"roughseas:vesselintegrity": measured}}


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


def clear_of(cuts, along, made, slide=False):
    """made(short), drawn back at either end by short until neither end shares a plane with an end already cut along
    the same line, or, sliding, moved along it whole; made returns the element's parts with its two ends last, and
    those are added to cuts."""
    short = [0, 0]
    while True:
        parts = made(short)
        clash = [any(abs(dot(along, other)) > 0.999 and abs(dot(sub(end, at), other)) < APART - 1e-9 for at, other in cuts) for end in parts[-2:]]
        if not any(clash):
            cuts.extend((end, along) for end in parts[-2:])
            return parts
        short = [short[0] + APART, short[1] - APART] if slide else [s + APART * hit for s, hit in zip(short, clash)]


def ends_of(elements):
    """The cuts at either end of each box along its own length, as clear_of keeps them."""
    cuts = []
    for element, m in posed(elements):
        along = unit(direct(m, (1, 0, 0)))
        (x0, y0, z0), (x1, y1, z1) = element["from"], element["to"]
        middle = ((y0 + y1) / 2, (z0 + z1) / 2)
        for x in (x0, x1):
            cuts.append(([units.voxels(c) for c in apply(m, [units.metres(x - x0), units.metres(middle[0] - y0), units.metres(middle[1] - z0)])], along))
    return cuts


def widest(made, surface, reach):
    """The widest made(width), up to reach, whose every corner lies inside the surface."""
    low, high = 0.0, reach
    for _ in range(30):
        low, high = ((low + high) / 2, high) if max(surface.beyond(p) for p in corners_of(made((low + high) / 2))) <= 0 else (low, (low + high) / 2)
    return low


def seated(made, surface, depth=0):
    """How far in, from depth on, the element made(depth) has to sit for every corner to lie inside the surface."""
    for _ in range(50):
        out = max(surface.beyond(p) for p in corners_of(made(depth)))
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

    def strakes(self, cuts=None):
        """The skin, panel by panel between neighbouring stations and edges. A tapering panel takes a board along each
        of its edges, each as tall as the panel's narrow end, the upper one hanging; each board reaches as far past a
        station as that station's line leans out, stops at the hull's ends, and sits inward until its outermost corner
        touches the loft, and no nearer the depth of a neighbour it overlaps than to keep their faces apart. Each board's
        ends keep clear of those already cut, the ones in cuts too, which collects them."""
        loft = self.outline("the hull", ends=False)
        hull = Hull(self.prefix, self.refined(), self.cz, self.t, self.outer, self.inner, self.edge)
        panels = [panel for panel in hull.panels() if across(panel[4][0], panel[4][1], rise(*panel[4]))[0] >= 1e-6]
        last = len(hull.stations) - 2
        bottoms, tops, cuts = {}, {}, [] if cuts is None else cuts

        def placed(*args, **options):
            return clear_of(cuts, unit(sub(args[2][1], args[2][0])), lambda short: self.board(*args, **options, short=short))

        for name, side, k, i, (a, b, c, d) in panels:
            near = [bottoms[side, k, i - 1][0]] if (side, k, i - 1) in bottoms else []
            bottoms[side, k, i] = placed(name, side, (a, b, d, c), 1, (i == 0, i == last), loft, near, aside=APART * (i % 2))
        for name, side, k, i, (a, b, c, d) in panels:
            if abs(across(a, b, sub(d, a))[0] - across(a, b, sub(c, b))[0]) <= TOLERANCE:
                continue
            near = [bottoms[key][0] for key in ((side, k, i - 1), (side, k, i + 1)) if key in bottoms]
            near += [tops[side, k, i - 1][0]] if (side, k, i - 1) in tops else []
            tops[side, k, i] = placed(name + "Top", side, (d, c, a, b), -1, (i == 0, i == last), loft, near, bottoms[side, k, i][0] + APART,
                                     aside=APART * (i % 2))
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

    def board(self, name, side, quad, turn, ends, loft, near, below=0, short=(0, 0), aside=0):
        a, b, d, c = quad
        along = unit(sub(b, a))
        up = rise(a, b, c, d)
        rise_a, rise_b = across(a, b, sub(d, a))[0], across(a, b, sub(c, b))[0]
        height, flare = min(rise_a, rise_b), across(a, b, up)[1]
        start = sub(a, scale(along, max(0, -dot(sub(d, a), along) * height / rise_a)))
        end = add(b, scale(along, max(0, dot(sub(c, b), along) * height / rise_b)))
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
        sunk = seated(lambda depth: made(add(start, scale(normal, depth)), add(end, scale(normal, depth))), loft)
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
        points = (self.at(x, k, 1) for k in range(len(self.stations[0][1])))
        return [(abs(z - self.cz), y) for _, y, z in points]

    def half(self, x, y, inset=0):
        edges = self.section(x)
        if y < edges[0][1] + inset or y > edges[-1][1]:
            return 0
        return max(0, breadth(edges, y) - inset)

    def floor(self, x, across, inset=0, step=0.1):
        y = self.section(x)[0][1] + inset
        while self.half(x, y, inset) < across:
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

    def rib(self, name, x, tex, width=1, depth=1, inset=None):
        """A rib at x, a piece along each edge of the section, each sunk until it lies inside the skin (and what lines
        it, inset deep); every other piece a little narrower, so that two share no plane where they meet."""
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
                half = width / 2 - APART * ((k + (side > 0)) % 2)
                normal = unit([0, spread, -side * rise])
                made = lambda s, z0=z0, z1=z1, y0=y0, z=z, half=half, normal=normal: box(
                    f"{name}{label}{k + 1}", (x - half, y0 + normal[1] * s, z0 + normal[2] * s), (x + half, y0 + length + normal[1] * s, z1 + normal[2] * s),
                    tex, origin=(x, y0 + normal[1] * s, z + normal[2] * s), rot=(side * flare, 0, 0), grain="y")
                elements.append(made(seated(made, inside)))
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

    def within(self, surface, *elements, only=lambda name: True):
        self.fits.append((surface, flattened(elements), only))

    def together(self, *elements):
        self.views.append(flattened(elements))

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
        faults = check(self.fits, self.views)
        if faults:
            raise SystemExit(f"{self.path.name}: its boxes leave the round shapes they build or share a plane\n  " + "\n  ".join(faults))
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
    part = path.with_name(path.name + ".part")
    part.write_text(text)
    part.replace(path)


def flattened(elements):
    return copy.deepcopy([e for element in elements for e in (element if isinstance(element, list) else [element])])


def count(elements):
    return sum(1 + count(e.get("children", [])) for e in elements)
