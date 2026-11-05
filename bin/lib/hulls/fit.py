import math
from itertools import combinations

from transform import apply, direct, identity, local, mul, unapply

TOLERANCE = 0.01
APART = 2 * TOLERANCE
FACE_NORMALS = {"north": (0, 0, -1), "south": (0, 0, 1), "east": (1, 0, 0), "west": (-1, 0, 0), "up": (0, 1, 0), "down": (0, -1, 0)}


class Surface:
    def __init__(self, name):
        self.name = name

    def beyond(self, point):
        raise NotImplementedError

    def inward(self, point):
        raise NotImplementedError


class Oval(Surface):
    def __init__(self, name, centre, axes, floor=None, inset=0):
        super().__init__(name)
        self.cx, self.cz = centre
        self.axes = axes
        self.floor = floor
        self.inset = inset

    def beyond(self, point):
        x, y, z = point
        a, b = (axis - self.inset for axis in self.axes(y))
        out = radial(x - self.cx, z - self.cz, a, b)
        return out if self.floor is None else max(out, self.floor + self.inset - y)


class Loft(Surface):
    """A hull lofted through half-sections, each a line of (half-breadth, height) from keel to sheer, open above it;
    a point lies beyond it by its distance outside the section at its station, or past either end."""

    def __init__(self, name, section, span, centre_z, inset=0, ends=True):
        super().__init__(name)
        self.section = section
        self.x0, self.x1 = span
        self.cz = centre_z
        self.inset = inset
        self.ends = ends

    def beyond(self, point):
        x, y, z = point
        p, outline = self.across(point)
        reach = min(math.dist(p, nearest(p, a, b)) for a, b in zip(outline[:-2], outline[1:-1]))
        side = -reach if enclosed(p, outline) else reach
        return max([side, *((self.x0 - x, x - self.x1) if self.ends else ())])

    def inward(self, point):
        if self.ends and max(self.x0 - point[0], point[0] - self.x1) >= self.beyond(point) - 1e-9:
            return (1 if point[0] < self.x0 else -1, 0, 0)
        p, outline = self.across(point)
        q = min((nearest(p, a, b) for a, b in zip(outline[:-2], outline[1:-1])), key=lambda q: math.dist(p, q))
        h, y = q[0] - p[0], q[1] - p[1]
        length = math.hypot(h, y) or 1
        return (0, y / length, math.copysign(1, point[2] - self.cz) * h / length)

    def across(self, point):
        x, y, z = point
        edges = lined(self.section(min(max(x, self.x0), self.x1)), self.inset)
        return (abs(z - self.cz), y), [(0, edges[0][1]), *edges, (edges[-1][0], edges[-1][1] + 1e6), (0, edges[-1][1] + 1e6)]


class Swept(Surface):
    """A solid as wide as half(x, y) either side of its centre line, from bottom(x) up, between the span's ends."""

    def __init__(self, name, half, bottom, span, centre_z):
        super().__init__(name)
        self.half = half
        self.bottom = bottom
        self.x0, self.x1 = span
        self.cz = centre_z

    def beyond(self, point):
        x, y, z = point
        at = min(max(x, self.x0), self.x1)
        return max(abs(z - self.cz) - self.half(at, y), self.bottom(at) - y, self.x0 - x, x - self.x1)


def lined(edges, depth):
    """A half-section's line moved depth in along its normals."""
    if not depth:
        return edges
    inner = []
    for k, (h, y) in enumerate(edges):
        (h0, y0), (h1, y1) = edges[max(k - 1, 0)], edges[min(k + 1, len(edges) - 1)]
        run, rise = h1 - h0, y1 - y0
        norm = math.hypot(run, rise) or 1
        inner.append((max(h - depth * rise / norm, 0), y + depth * run / norm))
    return inner


def nearest(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    t = max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / (dx * dx + dy * dy or 1)))
    return a[0] + t * dx, a[1] + t * dy


def enclosed(p, polygon):
    inside = False
    for (x0, y0), (x1, y1) in zip(polygon, polygon[1:] + polygon[:1]):
        if (y0 > p[1]) != (y1 > p[1]) and p[0] < x0 + (p[1] - y0) * (x1 - x0) / (y1 - y0):
            inside = not inside
    return inside


class Tube(Surface):
    def __init__(self, name, start, end, width, height):
        super().__init__(name)
        dx, dy, dz = (e - s for s, e in zip(start, end))
        yaw, pitch = math.degrees(math.atan2(-dz, dx)), math.degrees(math.atan2(dy, math.hypot(dx, dz)))
        self.frame = local({"from": start, "rotationOrigin": start, "rotationY": yaw, "rotationZ": pitch}, {})
        self.length = math.dist(start, end)
        self.w, self.h = width / 2, height / 2

    def beyond(self, point):
        x, y, z = (c * 16 for c in unapply(self.frame, [c / 16 for c in point]))
        return max(radial(z, y, self.w, self.h), -x, x - self.length)


class Union(Surface):
    def __init__(self, name, parts):
        super().__init__(name)
        self.parts = parts

    def beyond(self, point):
        return min(part.beyond(point) for part in self.parts)


def radial(u, v, a, b):
    if a <= 0 or b <= 0:
        return math.hypot(u, v)
    scale = math.hypot(u / a, v / b)
    reach = math.hypot(u, v)
    return reach - reach / scale if scale else -min(a, b)


def posed(elements, parent=None):
    for element in elements:
        m = mul(parent or identity(), local(element, {}))
        if element.get("faces"):
            yield element, m
        yield from posed(element.get("children", []), m)


def corners(element, m):
    f, t = element["from"], element["to"]
    return [tuple(c * 16 for c in apply(m, [(p - q) / 16 for p, q in zip((x, y, z), f)]))
            for x in (f[0], t[0]) for y in (f[1], t[1]) for z in (f[2], t[2])]


def outside(surface, elements, only):
    for element, m in posed(elements):
        if not only(element["name"]):
            continue
        worst = max(surface.beyond(corner) for corner in corners(element, m))
        if worst > TOLERANCE:
            yield f"{element['name']} stands {worst:.2f} voxels beyond {surface.name}"


def faces(element, m):
    f, t = element["from"], element["to"]
    for name in element["faces"]:
        normal = FACE_NORMALS[name]
        axis = next(i for i, n in enumerate(normal) if n)
        level = t[axis] if normal[axis] > 0 else f[axis]
        u, v = (i for i in range(3) if i != axis)
        square = [(f[u], f[v]), (t[u], f[v]), (t[u], t[v]), (f[u], t[v])]
        points = []
        for a, b in square:
            p = [0, 0, 0]
            p[axis], p[u], p[v] = level, a, b
            points.append(tuple(c * 16 for c in apply(m, [(c - q) / 16 for c, q in zip(p, f)])))
        yield direct(m, normal), points


def coplanar(elements):
    """Each two faces of the elements that face the same way within TOLERANCE of one plane and overlap there by more
    than TOLERANCE square voxels: the faces that flicker."""
    planes = {}
    for element, m in posed(elements):
        for normal, points in faces(element, m):
            if area(points, normal) <= TOLERANCE:
                continue
            offset = sum(n * c for n, c in zip(normal, points[0]))
            key = (*(round(n, 3) + 0.0 for n in normal), math.floor(offset / TOLERANCE))
            planes.setdefault(key, []).append((element["name"], offset, normal, points))
    seen = set()
    for (*normal, step), members in planes.items():
        near = members + planes.get((*normal, step + 1), [])
        for a, b in combinations(near, 2):
            pair = tuple(sorted((a[0], b[0])))
            if a[0] == b[0] or pair in seen or abs(a[1] - b[1]) > TOLERANCE:
                continue
            shared = overlap(a[3], b[3], a[2])
            if shared > TOLERANCE:
                seen.add(pair)
                yield f"{pair[0]} and {pair[1]} share {shared:.2f} square voxels of one plane"


def flat(points, normal):
    axis = max(range(3), key=lambda i: abs(normal[i]))
    u, v = (i for i in range(3) if i != axis)
    flip = normal[axis] < 0
    return [(p[v], p[u]) if flip else (p[u], p[v]) for p in points]


def area(points, normal):
    polygon = flat(points, normal)
    return abs(shoelace(polygon)) / max(abs(c) for c in normal)


def shoelace(polygon):
    return sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(polygon, polygon[1:] + polygon[:1])) / 2


def overlap(a, b, normal):
    subject, clip = flat(a, normal), flat(b, normal)
    if shoelace(subject) < 0:
        subject = subject[::-1]
    if shoelace(clip) < 0:
        clip = clip[::-1]
    for (x0, y0), (x1, y1) in zip(clip, clip[1:] + clip[:1]):
        inside = lambda p: (x1 - x0) * (p[1] - y0) - (y1 - y0) * (p[0] - x0) >= 0
        kept = []
        for p, q in zip(subject, subject[1:] + subject[:1]):
            if inside(q):
                if not inside(p):
                    kept.append(crossing(p, q, (x0, y0), (x1, y1)))
                kept.append(q)
            elif inside(p):
                kept.append(crossing(p, q, (x0, y0), (x1, y1)))
        subject = kept
        if not subject:
            return 0
    return abs(shoelace(subject)) / max(abs(c) for c in normal)


def crossing(p, q, a, b):
    dx, dy = q[0] - p[0], q[1] - p[1]
    ex, ey = b[0] - a[0], b[1] - a[1]
    t = ((a[0] - p[0]) * ey - (a[1] - p[1]) * ex) / (dx * ey - dy * ex)
    return p[0] + t * dx, p[1] + t * dy


def edges_of(points):
    return [tuple(p - q for p, q in zip(points[i], points[0])) for i in (4, 2, 1)]


def depth_into(a, b):
    """How far two posed boxes, each its eight corners, pass into each other: their least overlap along every axis
    that could part them, 0 once one does."""
    ea, eb = edges_of(a), edges_of(b)
    crossed = [(u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]) for u in ea for v in eb]
    least = math.inf
    for axis in (*ea, *eb, *crossed):
        length = math.sqrt(axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2])
        if length < 1e-9:
            continue
        on_a = [(p[0] * axis[0] + p[1] * axis[1] + p[2] * axis[2]) / length for p in a]
        on_b = [(p[0] * axis[0] + p[1] * axis[1] + p[2] * axis[2]) / length for p in b]
        least = min(least, max(on_a) - min(on_b), max(on_b) - min(on_a))
        if least <= TOLERANCE:
            return 0
    return least


def bounded(elements):
    for element, m in posed(elements):
        points = corners(element, m)
        yield element["name"], points, [min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]


def through(first, second, pressed=0):
    """Each box of the first elements that passes into a box of the second deeper than the two are pressed together."""
    others = list(bounded(second))
    for name, points, low, high in bounded(first):
        for other, theirs, their_low, their_high in others:
            if any(low[i] > their_high[i] - TOLERANCE or their_low[i] > high[i] - TOLERANCE for i in range(3)):
                continue
            if (depth := depth_into(points, theirs)) > pressed + TOLERANCE:
                yield f"{name} passes {depth:.2f} voxels into {other}"


def check(fits, views, aparts=()):
    faults = [fault for surface, elements, only in fits for fault in outside(surface, elements, only)]
    faults += [fault for first, second, pressed in aparts for fault in through(first, second, pressed)]
    return faults + sorted({fault for elements in views for fault in coplanar(elements)})
