import copy
import json
import math

from physics import ASSETS, STANDING_HIP_METRES, SWIM_LINE_METRES
import units
from entity import read, record
from shape import ORIGIN
from motion import faults, switches, unwind
from stroke import Rower, centre, drive_share, facing, fit, moved, named, played, posed, resting, rowed, seat, seraph, turn, unturn
from transform import angles, apply, depth, euler, inverse, mul, translation, undirect, unit

ANCHOR = "ItemAnchor"
HAND_POINT_ROTATION = (0, -180, 0)
FRAMES = 30
TOOL = "Tool"
RIGHT = "ToolRight"
LEFT = "ToolLeft"
BLADE = "Blade"
STARBOARD = {"R": RIGHT, "L": LEFT}
PORT = {"R": LEFT, "L": RIGHT}
CHANGE = "to-"
LOOPED = {"sculling": 2}
STANDING = "roughseas-stand"
PLANT_DEPTH_METRES = 1
# A blade swept at arm's length works some 0.7 m beyond the side; a sculled one about 0.3 m ahead of the bow.
ARM_REACH_METRES = 0.7
SCULL_REACH_METRES = 0.3
HAND_SPAN = 5
STRIDE = 3
FOOT_HALF = 1.5
DROP = 2
HIP_TURN = 45
HANDS_INBOARD = 1
PLANT = (12, 40)
SEATED_CHEST = 7
SEATED_REACH = (-3, 3)
REST = 8
CLEARANCE = 0.05


def marker(name, point):
    return {"name": name, "from": list(point), "to": list(point), "faces": {}}


class Tool:

    def __init__(self, item, shape, right, left, turns=(0,)):
        transform = json.loads((ASSETS / "itemtypes" / f"{item}.json").read_text())["tpHandTransform"]
        if "origin" not in transform or any(transform["origin"].get(axis, 0) for axis in "xyz") or transform.get("scale", 1) != 1:
            raise ValueError(f"{item}: a held tool's tpHandTransform turns about its hand point, unscaled")
        rotation = [transform.get("rotation", {}).get(axis, 0) + r for axis, r in zip("xyz", HAND_POINT_ROTATION)]
        self.placing = mul(translation([transform.get("translation", {}).get(axis, 0) for axis in "xyz"]), euler(rotation))
        self.elements = json.loads((ASSETS / "shapes/item" / f"{shape}.json").read_text())["elements"]
        self.right, self.left = right, left
        self.turns = turns

    def turned(self, degrees):
        """The tool turned about its grips' line, which `turns` say leaves it looking the same."""
        axis = [units.metres(c) for c in (0, *self.right[1:])]
        return mul(mul(translation(axis), euler((degrees, 0, 0))), translation([-c for c in axis]))

    def group(self):
        return {"name": TOOL, "from": [0, 0, 0], "to": [0, 0, 0], "rotationOrigin": [0, 0, 0], "faces": {},
                "children": self.elements + [marker(RIGHT, self.right), marker(LEFT, self.left)]}

    def held_at(self, hand, turned, at=None):
        reach = turn(turned, at or self.right)
        return {TOOL: moved((0, 0, 0), (0, 0, 0), [h - r for h, r in zip(hand, reach)], turned)}

    def gripped(self, hand, toward, grips):
        """The tool laid from the hand toward a point, each grip at its distance along the tool's x, the hand on the
        nearest."""
        d = unit([t - h for t, h in zip(toward, hand)])
        turned = {"rotationX": math.degrees(math.atan2(-d[2], -d[1])), "rotationZ": -math.degrees(math.acos(d[0]))}
        marks = {RIGHT: self.right, LEFT: self.left}
        upper = min(grips, key=grips.get)
        return self.held_at(hand, turned, (grips[upper], *marks[upper][1:])) | {
            name: {"offsetX": grips[name] - mark[0]} for name, mark in marks.items()}


def lowest(matrices, elements):
    corners = []
    for element in elements:
        f, t = element["from"], element["to"]
        corners += [apply(matrices[element["name"]], [units.metres(c[i] - f[i]) for i in range(3)])
                    for c in ((x, y, z) for x in (f[0], t[0]) for y in (f[1], t[1]) for z in (f[2], t[2]))]
    return min(corner[1] for corner in corners)


# The game draws a held item from the hand's ItemAnchor through the item's tpHandTransform (EntityShapeRenderer.RenderItem),
# so turning ItemAnchor puts the tool where the stroke has it.
def wrist(rower, tool, body, matrix):
    lower = rower.arms["R"].frames(body)[-1]
    anchor = rower.chain(ANCHOR)[-1]
    wanted = mul(mul(inverse(lower), matrix), inverse(tool.placing))
    origin = [units.metres(c) for c in anchor.get("rotationOrigin", (0, 0, 0))]
    turned = angles(wanted)
    rest = undirect(wanted, [wanted[i][3] - origin[i] for i in range(3)])
    shift = [units.voxels(r + o) - f for r, o, f in zip(rest, origin, anchor["from"])]
    return {"rotation" + a: round(v, 2) for a, v in zip("XYZ", turned)} | {"offset" + a: round(v, 3) for a, v in zip("XYZ", shift)}


def held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, waterline_metres, hull=()):
    stance = poses if isinstance(poses, Stance) else None
    rower = Rower(stance.at, stance.idle) if stance else Rower(seat(shape, seated), idle, facing(shape, vessel, seated))
    if stance:
        shift = [round(units.metres(a - s), 3) for a, s in zip(stance.at, seat(shape, seated))]
        hands = round(units.metres(stance.at[1]) - waterline_metres + STANDING_HIP_METRES, 2)
        print(f"{vessel}-{kind}: stands {', '.join(f'{a} {v:+.3f}' for a, v in zip('xyz', shift))} m from {seated}, "
              f"his hands {hands:.2f} m over the water at the end of the shove")
        record(vessel, {"behaviorConfigs": {"roughseas:propulsion": {"pole": units.both("handsAboveWater", hands) | {"stance": {"riderOffset": dict(zip("xyz", shift))}}}}})
    elif stroke == "seatedpole":
        hands = round(units.metres(rower.seat[1] + SEATED_CHEST) - waterline_metres, 2)
        print(f"{vessel}-{kind}: his hands {hands:.2f} m over the water as he shoves from the seat")
        record(vessel, {"behaviorConfigs": {"roughseas:propulsion": {"pole": units.both("handsAboveWater", hands)}}})
    group = tool.group()
    poses = stance.poses if stance else poses
    plan = named(f"{vessel}-{kind}", FRAMES, {name: pose for name, (pose, *_) in poses.items()}, stroke,
                 LOOPED.get(stroke, 1))
    for step, (name, (pose, grips, *body)) in zip(plan, poses.items()):
        step |= {"grips": grips, "body": body[0](rower) if body else None}
        if name.startswith(CHANGE):
            step |= {"keys": list(range(FRAMES)), "ends": "Hold"}
    walls = hull_walls(shape, hull) if hull else []
    if hull and not walls:
        raise ValueError(f"{vessel}: no element of her shape is named {' or '.join(hull)}, so nothing checks the tool")
    if walls:
        middle = sum(m[2][3] for _, _, m, _ in walls) / len(walls)
        for step in plan:
            rests = {frame: rested(walls, tool, step["pose"](frame), middle) for frame in step["keys"]}
            step["pose"] = lambda frame, rests=rests: rests[frame][0]
            push, frame = max((d, f) for f, (_, d) in rests.items())
            if round(push, 1):
                print(f"{step['code']}: the hull holds the tool out by {push:.1f} at frame {frame}")
    if stance:
        for step in plan:
            faults = {}
            for frame in step["keys"]:
                body = step["body"](frame)
                for side, foot in stance.feet.items():
                    for name, excess in rower.legs[side].faults(body, rower.body(foot)).items():
                        faults[f"{side} {name}"] = max(faults.get(f"{side} {name}", (0, 0)), (excess, frame))
            for name, (excess, frame) in faults.items():
                if round(excess, 1):
                    print(f"{step['code']}: {name} by {excess:.1f} at frame {frame}")
    own, player = fit(rower, [group], plan, STARBOARD, lean)
    reversed_change(list(poses), own, player)
    if hasattr(shape, "path"):
        rowed(shape, rower, own, player, f"{vessel}-{kind}", [group])
    through = (0, 0, None, None)
    for step, animation in zip(plan, player):
        deepest = math.inf
        abeam = []
        for keyframe, frame in zip(animation["keyframes"], step["keys"]):
            matrices = posed([group], lambda name, pose=step["pose"](frame): pose.get(name, {}))
            if step["code"].endswith("starboard-forward") and (frame + resting(stroke, FRAMES)) % FRAMES < FRAMES * drive_share(stroke):
                abeam.append(units.metres(abs(centre(tool.elements, matrices, BLADE)[2] - ORIGIN[2])))
            body = keyframe["elements"]
            holds = {turn: wrist(rower, tool, body, mul(rower.placing, mul(matrices[TOOL], tool.turned(turn))))
                     for turn in tool.turns}
            keyframe["elements"][ANCHOR] = holds[0]
            keyframe["holds"] = holds
            deepest = min(deepest, lowest(matrices, tool.elements) - waterline_metres)
            if walls:
                depth, part, wall = into(walls, along_tool(matrices, tool.elements))
                through = max(through, (units.voxels(depth), frame, part, wall), key=lambda t: t[0])
        print(f"{animation['code']}: the tool reaches {-deepest:.2f} m under the water" if deepest < 0
              else f"{animation['code']}: the tool stays {deepest:.2f} m over the water")
        if walls and round(through[0], 1):
            print(f"  the {through[2]} through the hull's {through[3]} by {through[0]:.1f} at frame {through[1]}")
        through = (0, 0, None, None)
        if step["code"].endswith("starboard-forward"):
            print(f"{animation['code']}: the blade drives {sum(abeam) / len(abeam):.2f} m out from her middle")
            record(vessel, {"behaviorConfigs": {"roughseas:propulsion": units.both("paddleAbeam", sum(abeam) / len(abeam))}})
    return player


def reversed_change(names, own, player):
    """The change back to a side is the change from it played backwards, frame for frame: the same arc over the bow and
    the same slide of the hands."""
    changes = [i for i, name in enumerate(names) if name.startswith(CHANGE)]
    if len(changes) != 2:
        return
    there, back = changes
    for animations in (own, player):
        frames = animations[there]["keyframes"]
        animations[back]["keyframes"] = [{"frame": f["frame"], "elements": copy.deepcopy(frames[len(frames) - 1 - i]["elements"])}
                                         for i, f in enumerate(frames)]


def hull_walls(shape, names):
    matrices = posed(shape.elements, lambda name: {})
    walls = []

    def collect(elements):
        for element in elements:
            if element.get("faces"):
                size = [units.metres(t - f) for f, t in zip(element["from"], element["to"])]
                m = matrices[element["name"]]
                corners = [apply(m, (i * size[0], j * size[1], k * size[2])) for i in (0, 1) for j in (0, 1) for k in (0, 1)]
                walls.append((element["name"], size, m, [[f(c[i] for c in corners) for i in range(3)] for f in (min, max)]))
            collect(element.get("children", []))

    def named(elements):
        for element in elements:
            if element["name"].startswith(names):
                yield element
            else:
                yield from named(element.get("children", []))

    collect(list(named(shape.elements)))
    return walls


def along_tool(matrices, elements):
    points = []
    for element in elements:
        f, t = element["from"], element["to"]
        steps = max(1, round(t[0] - f[0]))
        points += [(element["name"], apply(matrices[element["name"]],
                                           [units.metres((t[0] - f[0]) * k / steps), units.metres(t[1] - f[1]) / 2, units.metres(t[2] - f[2]) / 2]))
                   for k in range(steps + 1)]
    return points


def into(walls, points):
    return max(((depth(m, size, p), part, wall) for wall, size, m, (low, high) in walls for part, p in points
                if low[0] <= p[0] <= high[0] and low[1] <= p[1] <= high[1] and low[2] <= p[2] <= high[2]),
               default=(0, None, None))


def near(walls, points, margin):
    low = [min(p[i] for _, p in points) - margin for i in range(3)]
    high = [max(p[i] for _, p in points) + margin for i in range(3)]
    return [wall for wall in walls if all(wall[3][0][i] <= high[i] and wall[3][1][i] >= low[i] for i in range(3))]


def rested(walls, tool, pose, middle):
    """The pose with its tool moved the shortest way out of the walls, outboard of `middle` or up, and how far in
    voxels: a stopgap over guessed stroke lines, #252."""
    points = along_tool(posed([tool.group()], lambda name: pose.get(name, {})), tool.elements)
    touching = near(walls, points, units.metres(REST))

    def clear(direction, metres):
        moved_points = [(part, [c + metres * u for c, u in zip(p, direction)]) for part, p in points]
        return units.voxels(into(touching, moved_points)[0]) < CLEARANCE

    if not touching or clear((0, 0, 0), 0):
        return pose, 0
    side = 1 if sum(p[2] for _, p in points) / len(points) > middle else -1
    pushes = []
    for direction in ((0, 0, side), (0, 1, 0)):
        low, high = 0, units.metres(REST)
        if clear(direction, high):
            for _ in range(10):
                low, high = (low, (low + high) / 2) if clear(direction, (low + high) / 2) else ((low + high) / 2, high)
            pushes.append((high, direction))
    if not pushes:
        return pose, 0
    metres, direction = min(pushes)
    shift = unturn(pose[TOOL], [units.voxels(metres) * u for u in direction])
    return pose | {TOOL: pose[TOOL] | {"offset" + a: pose[TOOL].get("offset" + a, 0) + s for a, s in zip("XYZ", shift)}}, \
        units.voxels(metres)


PADDLE = Tool("tool/paddle", "paddle", (13, 8, 8), (0.5, 8, 8))
POLE = Tool("resource/pole", "pole-3", (12.8, 0.75, 8), (8, 0.75, 8), turns=(0, 90, 180, 270))


def worked_from_middle(box, at, reach_metres, ahead=False):
    """How far from her middle the stroke works: across, reach beyond her side, as far along as the rower is from her
    middle; or, ahead, reach beyond her bow."""
    (x0, _, z0), (x1, _, z1) = box["from"], box["to"]
    if ahead:
        return units.both("sweep", units.metres((x1 - x0) / 2) + reach_metres)
    return units.both("sweep", math.hypot(units.metres(at[0] - (x0 + x1) / 2), units.metres((z1 - z0) / 2) + reach_metres))


def waterline_metres(vessel):
    entity = read(vessel)
    return entity["hitboxSize"]["y"] + SWIM_LINE_METRES - entity["attributes"]["swimmingOffsetY"]


def tools(shape, vessel, seated, idle, lean, waterline_metres, by_kind, hull=()):
    """Every tool's strokes, each tool held in whichever of the holds that look alike the game blends the shortest way
    into the other tools'."""
    kinds = {kind: held(shape, vessel, seated, idle, kind, stroke, tool, poses, lean, waterline_metres, hull)
             for kind, (stroke, tool, poses) in by_kind.items()}
    every = [animation for animations in kinds.values() for animation in animations]
    blended = switches(played(vessel))
    elements = seraph()["elements"]
    for kind, animations in kinds.items():
        def holding(turn):
            for animation in animations:
                for keyframe in animation["keyframes"]:
                    keyframe["elements"][ANCHOR] = dict(keyframe["holds"][turn])
            trial = copy.deepcopy(every)
            unwind(elements, trial, blended)
            return sum(fault.size for fault in faults(elements, trial, blended) if fault.kind == "flip")

        holding(min(by_kind[kind][1].turns, key=holding))
        for animation in animations:
            for keyframe in animation["keyframes"]:
                del keyframe["holds"]
    return every


def phase(frame, drive):
    u = frame / FRAMES
    driving = u < drive
    f = u / drive if driving else (u - drive) / (1 - drive)
    ease = 0.5 - 0.5 * math.cos(math.pi * f)
    return (ease if driving else 1 - ease), (0 if driving else math.sin(math.pi * f))


def eased(u):
    return 0.5 - 0.5 * math.cos(math.pi * min(1, max(0, u)))


def paddling(tool, at, ahead, behind, above, out, catch=None, fore=3, bend=0, shift=0, middle=None):
    x, y, z = at
    catch = ahead if catch is None else catch
    middle = z if middle is None else middle
    sides = {-1: out, 1: 2 * middle - out}

    def placed(hand, lean, pitch):
        return tool.held_at(hand, {"rotationX": lean, "rotationZ": pitch})

    def holding(side, frame):
        reach, rise = phase((frame + resting("paddling", FRAMES)) % FRAMES, drive_share("paddling"))
        hand = (x - catch + (catch + behind) * reach, y + above + 2 * (1 - reach) ** 2 + 4.5 * rise, sides[side] + side * rise)
        return hand, -side * (30 + 20 * rise), -95 + 40 * reach

    def stroke(side):
        return lambda frame: placed(*holding(side, frame))

    def sliding(frame):
        return eased((frame / (FRAMES - 1) - 0.2) / 0.6)

    crossing, upright = (x + (behind - ahead) / 2 - fore, y + above + 8), -95

    def switch(side):
        (start, lean, pitch), (end, _, _) = holding(-side, 0), holding(side, 0)

        def at_frame(frame):
            u = frame / (FRAMES - 1)
            across, regrip, lifted = eased(u), sliding(frame), math.sin(math.pi * u)
            hand = [s + (e - s) * across for s, e in zip(start, end)]
            hand[:2] = [h + (c - h) * lifted for h, c in zip(hand, crossing)]
            return placed(hand, lean * (1 - 2 * across), pitch + (upright - pitch) * lifted) | {
                RIGHT: {"offsetX": (tool.left[0] - tool.right[0]) * regrip, "offsetY": 2 * math.sin(math.pi * regrip), "offsetZ": 0},
                LEFT: {"offsetX": (tool.right[0] - tool.left[0]) * regrip, "offsetY": 0, "offsetZ": 0}}

        return at_frame

    def bent(toward):
        def body(rower):
            hips = rower.idle.get("LowerTorso", {})
            across = lambda frame: middle - z + toward(frame) * shift if shift else 0
            return lambda frame: {"UpperTorso": {"rotationX": toward(frame) * bend},
                                  "LowerTorso": hips | {"offsetZ": hips.get("offsetZ", 0) + across(frame)}}
        return (body,) if bend or shift else ()

    def side_strokes(side, name, grips):
        forward = stroke(side)
        over = bent(lambda frame: side)
        return {f"{name}-ready": (lambda frame: forward(0), grips, *over),
                f"{name}-forward": (forward, grips, *over),
                f"{name}-backward": (lambda frame: forward(FRAMES - frame), grips, *over),
                f"{CHANGE}{name}": (switch(side), PORT if side < 0 else STARBOARD,
                                    *bent(lambda frame: side * (2 * eased(frame / (FRAMES - 1)) - 1)))}

    return side_strokes(-1, "starboard", STARBOARD) | side_strokes(1, "port", PORT)


class Stance:
    """Where the rower stands to work the tool: his soles at `at`, in the shape's voxels, on `feet`; each of `poses`
    carries his hips and legs."""

    def __init__(self, at, idle, poses, feet):
        self.at, self.idle, self.poses, self.feet = at, idle, poses, feet


def standing_poling(tool, at, waterline_voxels, out, chine, side=-1, floor_at=None):
    x, floor, z = at
    floor_at = floor_at or (lambda foot_x, across: floor)
    bed = waterline_voxels - units.voxels(PLANT_DEPTH_METRES)
    reach_x = [x - 2, x + 4]
    reach_y = [floor + 21, floor + 19]
    plant = [x + reach for reach in PLANT]

    def placed(frame):
        reach, lift = phase(frame, drive_share("pole"))
        hand = (lerp(reach_x, reach), lerp(reach_y, reach) - DROP * reach, out - side * HANDS_INBOARD)
        return planted(tool, hand, (lerp(plant, reach), bed + 20 * lift, out))

    feet = {"R": (x + STRIDE, floor_at(x + STRIDE, chine), z + side * (chine - FOOT_HALF)),
            "L": (x - STRIDE, floor_at(x - STRIDE, 2 * FOOT_HALF), z - side * FOOT_HALF)}

    def body(rower, frame):
        reach, _ = phase(frame, drive_share("pole"))
        hips = {"LowerTorso": {"offsetY": -DROP * reach, "rotationY": side * HIP_TURN}}
        hips["UpperTorso"] = {"rotationY": toward_grips(rower, tool, placed(frame), hips), "rotationZ": 10 + 15 * reach}
        for leg_side, foot in feet.items():
            hips |= rower.legs[leg_side].plant(hips, rower.body(foot))
        return hips

    return Stance(at, STANDING, three(placed, body), feet)


def lerp(span, share):
    return span[0] + (span[1] - span[0]) * share


def planted(tool, hand, foot):
    upper = 48 - math.dist(hand, foot)
    return tool.gripped(hand, foot, {LEFT: upper, RIGHT: upper + HAND_SPAN})


def poling(standing, seat_at, waterline_voxels, out, stand_at, chine, floor_at=None):
    if standing:
        return "pole", POLE, standing_poling(POLE, stand_at, waterline_voxels, out, chine, floor_at=floor_at)
    return "seatedpole", POLE, seated_poling(POLE, seat_at, waterline_voxels, out)


def toward_grips(rower, tool, pose, hips):
    markers = posed([tool.group()], lambda name: pose.get(name, {}))
    grips = [rower.body([units.voxels(c) for c in apply(markers[name], (0, 0, 0))]) for name in (RIGHT, LEFT)]
    return rower.facing(hips, [sum(c) / 2 for c in zip(*grips)])


def three(placed, body):
    frames = {"ready": lambda frame: 0, "forward": lambda frame: frame, "backward": lambda frame: FRAMES - frame}
    return {name: (lambda frame, at=at: placed(at(frame)), STARBOARD,
                   lambda rower, at=at: lambda frame: body(rower, at(frame)))
            for name, at in frames.items()}


def seated_poling(tool, at, waterline_voxels, out, side=-1):
    x, seat_y, _ = at
    bed = waterline_voxels - units.voxels(PLANT_DEPTH_METRES)

    def placed(frame):
        reach, lift = phase(frame, drive_share("seatedpole"))
        hand = (lerp(SEATED_REACH, reach) + x, seat_y + SEATED_CHEST, out - side * HANDS_INBOARD)
        return planted(tool, hand, (x + lerp(PLANT, reach), bed + 20 * lift, out))

    def body(rower, frame):
        return {"UpperTorso": {"rotationY": toward_grips(rower, tool, placed(frame), {})}}

    return three(placed, body)


def sculling(tool, at, ahead, above, sweep):
    x, y, z = at

    def scull(frame):
        turn_ = 2 * math.pi * frame / FRAMES
        hand = (x - ahead + 0.8 * math.sin(2 * turn_), y + above, z + sweep * math.sin(turn_))
        return tool.held_at(hand, {"rotationX": -25 * math.sin(turn_), "rotationZ": -135})

    return {"ready": (lambda frame: scull(0), STARBOARD),
            "forward": (scull, STARBOARD), "backward": (lambda frame: scull(FRAMES - frame), STARBOARD)}
