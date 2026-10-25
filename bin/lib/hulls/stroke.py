import json
import math
import os
from pathlib import Path

from arm import Arm
from transform import apply, direct, euler, identity, local, mul, undirect

ROOT = Path(__file__).resolve().parents[3]
SERAPH = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/game/shapes/entity/humanoid/seraph-faceless.json"
POSES = ROOT / "RoughSeas/assets/roughseas/patches/player-poses.json"
PATCHES = ROOT / "RoughSeas/assets/roughseas/patches"

ARMS = {"R": ("UpperArmR", "LowerArmR"), "L": ("UpperArmL", "LowerArmL")}
OUTBOARD = {"R": -1, "L": 1}
# Outweighs the seat pose (80) so far that the fit can leave it out of the arms and torso.
WEIGHT = 10000
SPEED = 0.75
CENTRE = (8, 0, 8)


def turn(rotations, v):
    return apply(euler([rotations.get("rotation" + axis, 0) for axis in "XYZ"]), v)


def unturn(rotations, v):
    return undirect(euler([rotations.get("rotation" + axis, 0) for axis in "XYZ"]), v)


def gripped(rest, throat, hand, turned):
    origin, resting = rest
    reach = turn(turned, throat)
    return {"Paddle": moved(origin, resting, [h - r for h, r in zip(hand, reach)], turned)}


def paddling(vessel, rest, throat, catch, finish, height, out, side=-1, frames=30):
    def placed(hand, lean, pitch):
        return gripped(rest, throat, hand, {"rotationX": -side * lean, "rotationZ": pitch})

    def stroke(frame):
        u = frame / frames
        drive = u < 0.55
        f = u / 0.55 if drive else (u - 0.55) / 0.45
        ease = 0.5 - 0.5 * math.cos(math.pi * f)
        reach = ease if drive else 1 - ease
        lift = 0 if drive else math.sin(math.pi * f)
        hand = (catch + (finish - catch) * reach, height + 2 * (1 - reach) ** 2 + 4.5 * lift, out + side * lift)
        return placed(hand, 30 + 20 * lift, -95 + 40 * reach)

    return strokes(vessel, frames, lambda frame: placed(((catch + finish) / 2, height + 2, out - side), 62, -95),
                   stroke, lambda frame: stroke(frames - frame))


def strokes(vessel, frames, ready, forward, backward):
    keys = list(range(0, frames, 2))
    return [{"code": f"roughseas-{vessel}-{name}", "frames": frames, "keys": keys if name != "ready" else [0],
             "pose": pose} for name, pose in (("ready", ready), ("forward", forward), ("backward", backward))]


def moved(origin, rest, target, turned):
    shift = unturn(turned, [t - o for t, o in zip(target, origin)])
    pose = {"offset" + axis: value for axis, value in zip("XYZ", shift)}
    return pose | {"rotation" + axis: turned.get("rotation" + axis, 0) - r for axis, r in zip("XYZ", rest)}


def posed(elements, pose_of):
    matrices = {}

    def descend(children, parent):
        for element in children:
            matrices[element["name"]] = mul(parent, local(element, pose_of(element["name"])))
            descend(element.get("children", []), matrices[element["name"]])

    descend(elements, identity())
    return matrices


def find(elements, name):
    for element in elements:
        if element["name"] == name:
            return element
        found = find(element.get("children", []), name)
        if found:
            return found
    return None


def centre(elements, matrices, name):
    element = find(elements, name)
    size = [t - f for f, t in zip(element["from"], element["to"])]
    return tuple(16 * c for c in apply(matrices[name], [s / 32 for s in size]))


def seat(shape, name):
    element = find(shape.elements, name)
    point = element["attachmentpoints"][0]
    return tuple(f + float(point["pos" + axis]) for f, axis in zip(element["from"], "XYZ"))


# Every grip lies along its gear's x: a paddle's shaft, which a T-grip's palm caps, or an oar's loom.
def held(matrices, name):
    return direct(matrices[name], (1, 0, 0))


class Rower:

    def __init__(self, seat, idle):
        self.seraph = json.loads(SERAPH.read_text())
        self.seat = seat
        animations = self.seraph["animations"] + [op["value"] for op in json.loads(POSES.read_text())
                                                  if op["path"] == "/animations/-"]
        sitting = next(a for a in animations if a["code"] == idle)
        self.idle = next(k["elements"] for k in sitting["keyframes"] if k["frame"] == 0)
        self.arms = {side: Arm(self.chain(lower), self.idle, OUTBOARD[side]) for side, (_, lower) in ARMS.items()}

    def chain(self, name):
        def descend(elements, path):
            for element in elements:
                found = [*path, element] if element["name"] == name else descend(element.get("children", []),
                                                                                 [*path, element])
                if found:
                    return found
            return None

        return descend(self.seraph["elements"], [])

    def body(self, point):
        return tuple((p - s + h) / 16 for p, s, h in zip(point, self.seat, CENTRE))

    def hand(self, pose, side):
        return tuple(s + 16 * c - h for s, c, h in zip(self.seat, self.arms[side].joints(pose)[-1], CENTRE))


def crew(shape, vessel, seated, idle, strokes, grips, lean):
    rower = Rower(seat(shape, seated), idle)
    shape.animations, player = fit(rower, shape.elements, strokes, grips, lean)
    shape.write()
    player_patch(f"player-{vessel}", player)
    rowed(shape, rower, shape.animations, player)


def player_patch(name, animations):
    ops = []
    for animation in animations:
        ops.append({"op": "add", "file": "game:shapes/entity/humanoid/seraph-faceless.json", "path": "/animations/-",
                    "value": animation})
    for animation in animations:
        elements = sorted({e for k in animation["keyframes"] for e in k["elements"]})
        ops.append({"op": "add", "file": "game:entities/humanoid/player.json", "path": "/client/animations/-",
                    "side": "Server", "value": {
                        "code": animation["code"], "animation": animation["code"], "animationSpeed": SPEED,
                        "blendMode": "Average",
                        "easeInSpeed": 4, "easeOutSpeed": 4,
                        "elementWeight": {e: WEIGHT for e in elements},
                        "elementBlendMode": {e: "Average" for e in elements}}})
    path = PATCHES / f"{name}.json"
    path.write_text(json.dumps(ops, indent=4) + "\n")
    print(f"{path}: {len(animations)} animations")


def rowed(shape, rower, vessel, player):
    offset = [s - h for s, h in zip(rower.seat, CENTRE)]
    seated = {"name": "Rower", "from": offset, "to": offset, "faces": {}, "children": rower.seraph["elements"]}
    animations = []
    for own, arms in zip(vessel, player):
        keyframes = [{"frame": a["frame"], "elements": every_axis(rower.idle) | a["elements"] | b["elements"]}
                     for a, b in zip(own["keyframes"], arms["keyframes"])]
        animations.append(own | {"keyframes": keyframes})
    path = Path(os.environ.get("BENCH_TMP", "/tmp")) / "model" / f"{shape.path.stem}-rowed.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"textureWidth": 16, "textureHeight": 16,
                                "textures": shape.textures | rower.seraph["textures"],
                                "elements": [e for e in shape.elements if e["name"] not in ("hideWater", "Bilge")]
                                + [seated], "animations": animations}))
    print(f"{path}: the vessel rowed, for bin/model pose")


def every_axis(pose):
    keyed = {}
    for name, channels in pose.items():
        keyed[name] = {}
        for prefix in ("offset", "rotation"):
            if any(key.startswith(prefix) for key in channels):
                keyed[name] |= {prefix + axis: round(channels.get(prefix + axis, 0), 2) for axis in "XYZ"}
    return keyed


def fit(rower, elements, strokes, grips, lean):
    vessel, player = [], []
    for stroke in strokes:
        poses = [stroke["pose"](frame) for frame in stroke["keys"]]
        placed = [posed(elements, lambda name, pose=pose: pose.get(name, {})) for pose in poses]
        bodies = [{"UpperTorso": lean} for _ in poses]
        faults = {}
        for side, grip in grips.items():
            arm = rower.arms[side]
            hands = [rower.body(centre(elements, m, grip)) for m in placed]
            axes = [held(m, grip) for m in placed]
            thumbs = [[tuple(thumb * c for c in axis) for axis in axes] for thumb in (1, -1)]
            holds = [arm.stroke(bodies, hands, held_as) for held_as in thumbs]
            chosen = min(range(2), key=lambda i: holds[i][1])
            arms = holds[chosen][0]
            for i in range(1, len(arms)):
                arms[i] = arm.follow(arms[i], arms[i - 1])
            for frame, body, pose, hand, axis in zip(stroke["keys"], bodies, arms, hands, thumbs[chosen]):
                body |= pose
                for name, (excess, _) in arm.faults(body, hand, axis).items():
                    faults[f"{side} {name}"] = max(faults.get(f"{side} {name}", (0, 0)), (excess, frame))
        worst = max(math.dist(rower.hand(body, side), centre(elements, m, grip))
                    for body, m in zip(bodies, placed) for side, grip in grips.items())
        own = [{"frame": frame, "elements": every_axis(pose)} for frame, pose in zip(stroke["keys"], poses)]
        arms = [{"frame": frame, "elements": every_axis(body)} for frame, body in zip(stroke["keys"], bodies)]
        print(f"{stroke['code']}: hands within {worst:.2f} of the grips")
        for name, (excess, frame) in faults.items():
            if round(excess, 1):
                print(f"  {name} by {excess:.1f} at frame {frame}")
        common = {"code": stroke["code"], "quantityframes": stroke["frames"], "onActivityStopped": "EaseOut",
                  "onAnimationEnd": "Repeat"}
        vessel.append({"name": stroke["code"], **common, "keyframes": own})
        player.append({"name": stroke["code"], **common, "keyframes": arms})
    return vessel, player
