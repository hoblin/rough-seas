import hashlib
import json
import math
import re
from pathlib import Path

FACES = ("north", "east", "south", "west", "up", "down")
FACE_AXES = {
    "north": ("x", "y"), "south": ("x", "y"),
    "east": ("z", "y"), "west": ("z", "y"),
    "up": ("x", "z"), "down": ("x", "z"),
}
AXIS = {"x": 0, "y": 1, "z": 2}


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


def run(name, frm, to, tex, *, axis="x", step=16, **options):
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


def stages(name, pieces):
    return group(name, (0, 0, 0), (0, 0, 0), [group(f"{name}{n}", (0, 0, 0), (0, 0, 0), piece) for n, piece in enumerate(pieces, 1)])


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
    if rounded:
        parts = [
            box(name, (-overlap, -h * 0.68, -w), (length + overlap, h * 0.68, w), tex, grain=grain),
            box(name + "Round", (-overlap - 0.01, -h, -w * 0.68), (length + overlap - 0.01, h, w * 0.68), tex, grain=grain),
        ]
    else:
        parts = [box(name, (-overlap, -h, -w), (length + overlap, h, w), tex, grain=grain)]
    tie_width, margin = tie or (0.8, 0.35)
    for n, f in enumerate(ties, 1):
        x = f * length
        parts.append(box(f"{name}Tie{n}", (x - tie_width / 2, -h - margin, -w - margin),
                         (x + tie_width / 2, h + margin, w + margin), tie_tex, grain="z"))
    return group(name + "Frame", start, (0, yaw, pitch), parts)


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


def path(name, points, width, height, tex, **options):
    return [beam(f"{name}{i}", a, b, width(i) if callable(width) else width, height(i) if callable(height) else height,
                 tex, **options) for i, (a, b) in enumerate(zip(points, points[1:]), 1)]


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


def across(start, end, up):
    yaw, pitch = (math.radians(angle) for angle in heading(start, end)[:2])
    along = [math.cos(pitch) * math.cos(yaw), math.sin(pitch), -math.cos(pitch) * math.sin(yaw)]
    rising = [-math.sin(pitch) * math.cos(yaw), math.cos(pitch), math.sin(pitch) * math.sin(yaw)]
    outward = [math.sin(yaw), 0, math.cos(yaw)]
    dot = lambda u, v: sum(p * q for p, q in zip(u, v))
    square = [p - dot(up, along) * q for p, q in zip(up, along)]
    return math.sqrt(dot(square, square)), math.degrees(math.atan2(dot(square, outward), dot(square, rising)))


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

    def strakes(self):
        elements = []
        count = len(self.stations[0][1]) - 1
        for side, label in ((-1, "Port"), (1, "Starboard")):
            for k in range(count):
                for i in range(len(self.stations) - 1):
                    a, b = self.point(i, k, side), self.point(i + 1, k, side)
                    c, d = self.point(i + 1, k + 1, side), self.point(i, k + 1, side)
                    up = [((p - q) + (r - t)) / 2 for p, q, r, t in zip(d, a, c, b)]
                    height, flare = across(a, b, up)
                    if height < 1e-6:
                        continue
                    elements.append(plank(f"{self.prefix}{label}{k + 1}x{i + 1}", a, b, height, self.t, side * flare,
                                          side, self.outer, self.inner, edge=self.edge))
        return elements

    def rail(self, name, tex, height=1, thickness=None, lift=0):
        elements = []
        top = len(self.stations[0][1]) - 1
        thickness = thickness or self.t + 0.5
        for side, label in ((-1, "Port"), (1, "Starboard")):
            for i in range(len(self.stations) - 1):
                a, b = self.point(i, top, side), self.point(i + 1, top, side)
                a, b = (a[0], a[1] + lift, a[2]), (b[0], b[1] + lift, b[2])
                if abs(a[2] - b[2]) < 1e-6 and a[2] == self.cz:
                    continue
                elements.append(plank(f"{name}{label}{i + 1}", a, b, height, thickness, 0, side, tex, tex))
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
        edges = range(len(self.stations[0][1]))
        return [(abs(self.at(x, k, 1)[2] - self.cz), self.at(x, k, 1)[1]) for k in edges]

    def rib(self, name, x, tex, width=1, depth=1):
        edges = self.section(x)
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
                z0, z1 = sorted((z - side * self.t, z - side * (self.t + depth)))
                elements.append(box(f"{name}{label}{k + 1}", (x - width / 2, y0, z0), (x + width / 2, y0 + length, z1),
                                    tex, origin=(x, y0, z), rot=(side * flare, 0, 0), grain="y"))
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
    path.write_text(text + "\n")


def count(elements):
    return sum(1 + count(e.get("children", [])) for e in elements)
