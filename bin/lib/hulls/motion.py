from typing import NamedTuple

from transform import between, orientation

ROTATION = tuple("rotation" + axis for axis in "XYZ")
OFFSET = tuple("offset" + axis for axis in "XYZ")
STEPS = 24
# What the eye doesn't see in a blend: a tenth of a right angle, and a voxel.
FLIP = 10
SHIFT = 1


def path(rest, start, end):
    """The degrees an element turns through as the engine lerps each Euler channel from one pose to the other."""
    a = [rest[key] + start[key] for key in ROTATION]
    d = [end[key] - start[key] for key in ROTATION]
    swept, before = 0, orientation(*a)
    for step in range(1, STEPS + 1):
        t = step / STEPS
        now = orientation(a[0] + d[0] * t, a[1] + d[1] * t, a[2] + d[2] * t)
        swept += between(before, now)
        before = now
    return swept


def apart(rest, a, b):
    return between(*(orientation(*(rest[key] + pose[key] for key in ROTATION)) for pose in (a, b)))


def forms(start, end):
    """The end's orientation in each Euler form that lies within half a turn of the start, channel by channel."""
    x, y, z = (end[key] for key in ROTATION)
    for form in ((x, y, z), (x + 180, 180 - y, z + 180)):
        yield {key: round(v + 360 * round((start[key] - v) / 360), 2) for key, v in zip(ROTATION, form)}


def closest(rest, start, end):
    return min(forms(start, end), key=lambda form: path(rest, start, form))


def flip(rest, start, end):
    """How much further the element turns between the two poses than it would with the end's orientation written in
    its best Euler form."""
    written = path(rest, start, end)
    if written - apart(rest, start, end) <= FLIP:
        return 0
    return written - path(rest, start, closest(rest, start, end))


def rests(elements):
    found = {}

    def descend(children):
        for element in children:
            found[element["name"]] = {key: element.get(key, 0) for key in ROTATION}
            descend(element.get("children", []))

    descend(elements)
    return found


def keyed(animation):
    """Each element's keys in the order the animation plays them, as its keyframes hold them."""
    keys = {}
    for keyframe in sorted(animation["keyframes"], key=lambda k: k["frame"]):
        for name, channels in keyframe["elements"].items():
            if ROTATION[0] in channels:
                keys.setdefault(name, []).append((keyframe["frame"], channels))
    return keys


def leaving(animation, anywhere):
    """The keys the game blends out of. A rower finishes the stroke or the change he is in before the next of the same
    tool (`MountStrokes`), so it is left from its end: a stroke's loop comes round to its first key, a change holds its
    last. A new tool in the hand cuts it wherever it has got to."""
    if anywhere:
        return list
    return (lambda keys: list(keys)[-1:]) if animation.get("onAnimationEnd") == "Hold" else (lambda keys: list(keys)[:1])


SLOTS = ("ready", "forwards", "backwards")
SIDES = ("-starboard", "-port")


def switches(strokes):
    """Each switch the game blends, from one animation into another, as `MountStrokes` plays a vessel's strokes, and
    whether it may come anywhere in the first: a kind's three into each other, a side's three into the other side's
    change (`Paddler` changes sides only from a stroke), a change into its side's three, and, anywhere, every
    animation of one tool into the three of another."""
    tool = lambda kind: kind.removesuffix(SIDES[0]).removesuffix(SIDES[1])
    found = set()
    for kind, codes in strokes.items():
        for other, theirs in strokes.items():
            for slot in SLOTS:
                if kind == other:
                    found |= {(codes[s], theirs[slot], False) for s in SLOTS if s != slot}
                    if "change" in codes:
                        found.add((codes["change"], theirs[slot], False))
                elif tool(kind) == tool(other):
                    found |= {(codes[s], theirs["change"], False) for s in SLOTS}
                else:
                    found |= {(code, theirs[slot], True) for code in codes.values()}
    return found


def unwind(elements, animations, blended):
    """Writes each element's turns in the Euler forms the game blends the short way: the animations are joined through
    their closest switches first, each written in the form of the pose it joins there and then key by key outward."""
    rest = rests(elements)
    keys = {animation["code"]: keyed(animation) for animation in animations}
    by_code = {animation["code"]: animation for animation in animations}
    for name in {name for each in keys.values() for name in each}:
        poses = {code: [pose for _, pose in keys[code][name]] for code in keys if name in keys[code]}
        joins = [(a, b, leaving(by_code[a], anywhere)) for a, b, anywhere in blended if a in poses and b in poses]
        written = {next(iter(poses))}
        while len(written) < len(poses):
            options = [(apart(rest[name], poses[a][k], poses[b][0]), (b, 0, poses[a][k]) if a in written else (a, k, poses[b][0]))
                       for a, b, left in joins if (a in written) != (b in written) for k in left(range(len(poses[a])))]
            if not options:
                options = [(0, (next(code for code in poses if code not in written), 0, None))]
            code, at, reference = min(options, key=lambda option: option[0])[1]
            run = poses[code]
            run[at] |= closest(rest[name], reference or run[at], run[at])
            for i in list(range(at + 1, len(run))) + list(range(at - 1, -1, -1)):
                run[i] |= closest(rest[name], run[i - 1] if i > at else run[i + 1], run[i])
            written.add(code)


class Fault(NamedTuple):
    size: float
    code: str
    element: str
    frame: int
    kind: str
    tools: bool

    def __str__(self):
        how = {"flip": f"turns {self.size:.0f}° the long way round", "turn": f"jumps {self.size:.0f}°",
               "move": f"jumps {self.size:.1f} voxels"}[self.kind]
        return f"{self.code}: {self.element} {how} from frame {self.frame}"


def faults(elements, animations, blended):
    """Every turn the engine draws the long way round, from key to key within an animation and from any key of one
    animation to the start of another the game blends it into; and every jump where a switch within one tool blends two
    poses that don't meet. A switch to another tool is marked: a new tool in the hand is a new hold, with no pose to
    meet."""
    rest = rests(elements)
    keys = {animation["code"]: keyed(animation) for animation in animations}
    found = {}

    def note(fault):
        key = (fault.code, fault.element, fault.kind)
        if key not in found or fault.size > found[key].size:
            found[key] = fault

    def check(code, name, frame, start, end, tools=False, meeting=False):
        extra = flip(rest[name], start, end)
        if extra > FLIP:
            note(Fault(extra, code, name, frame, "flip", tools))
        if meeting:
            turned = apart(rest[name], start, end)
            moved = max(abs(end.get(key, 0) - start.get(key, 0)) for key in OFFSET)
            if turned > FLIP:
                note(Fault(turned, code, name, frame, "turn", tools))
            if moved > SHIFT:
                note(Fault(moved, code, name, frame, "move", tools))

    for animation in animations:
        looped = animation.get("onAnimationEnd", "Repeat") == "Repeat"
        for name, ends in keys[animation["code"]].items():
            for (frame, start), (_, end) in list(zip(ends, ends[1:])) + ([(ends[-1], ends[0])] if looped else []):
                check(animation["code"], name, frame, start, end)
    by_code = {animation["code"]: animation for animation in animations}
    for a, b, anywhere in blended:
        for name, ends in keys.get(a, {}).items():
            if name in keys.get(b, {}):
                for frame, start in leaving(by_code[a], anywhere)(ends):
                    check(f"{a} → {b}", name, frame, start, keys[b][name][0][1], anywhere, not anywhere)
    return sorted(found.values(), reverse=True)
