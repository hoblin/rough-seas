import json
import math
import os
from functools import cache
from pathlib import Path

from arm import Arm
from leg import Leg
from transform import apply, direct, euler, identity, local, mul, rotation, translation, unapply, undirect

ROOT = Path(__file__).resolve().parents[3]
SERAPH = Path(os.environ.get("VINTAGE_STORY", "")) / "assets/game/shapes/entity/humanoid/seraph-faceless.json"
POSES = ROOT / "RoughSeas/assets/roughseas/patches/player-poses.json"
PROPULSION = ROOT / "RoughSeas/assets/roughseas/config/propulsion.json"
PATCHES = ROOT / "RoughSeas/assets/roughseas/patches"
ENTITIES = ROOT / "RoughSeas/assets/roughseas/entities/vessel"

ARMS = {"R": "LowerArmR", "L": "LowerArmL"}
LEGS = {"R": "LowerFootR", "L": "LowerFootL"}
OUTBOARD = {"R": -1, "L": 1}
# Outweighs the seat pose (80) so far that the fit can leave it out of the arms and torso.
WEIGHT = 10000
FRAMES_A_SECOND = 30
# The trunk's rotation each way, AAOS normal value for the thoracolumbar spine.
TRUNK_ROTATION = 45
CENTRE = (8, 0, 8)


def turn(rotations, v):
    return apply(euler([rotations.get("rotation" + axis, 0) for axis in "XYZ"]), v)


def unturn(rotations, v):
    return undirect(euler([rotations.get("rotation" + axis, 0) for axis in "XYZ"]), v)


@cache
def propelled(stroke):
    return json.loads(PROPULSION.read_text())["strokes"][stroke]


def speed(stroke, frames, per_loop=1):
    return round(frames * propelled(stroke)["strokesPerMinute"] / per_loop / 60 / FRAMES_A_SECOND, 4)


def drive_share(stroke):
    return propelled(stroke)["driveShare"]


def strokes(vessel, frames, ready, forward, backward, stroke, per_loop=1):
    return named(vessel, frames, {"ready": ready, "forward": forward, "backward": backward}, stroke, per_loop)


def named(vessel, frames, poses, stroke, per_loop=1):
    keys = list(range(0, frames, 2))
    return [{"code": f"roughseas-{vessel}-{name}", "frames": frames, "keys": [0] if name.endswith("ready") else keys,
             "speed": speed(stroke, frames, per_loop), "pose": pose}
            for name, pose in poses.items()]


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


def facing(shape, vessel, name):
    entity = ENTITIES / f"{vessel}.json"
    if not entity.exists():
        return 0
    code = find(shape.elements, name)["attachmentpoints"][0]["code"]
    seats = json.loads(entity.read_text())["behaviorConfigs"]["creaturecarrier"]["seats"]
    return next(s for s in seats if s["apName"] == code).get("mountRotation", {}).get("y", 0)


# Every grip lies along its gear's x: a paddle's shaft, which a T-grip's palm caps, or an oar's loom.
def held(matrices, name):
    return direct(matrices[name], (1, 0, 0))


class Rower:

    def __init__(self, seat, idle, yaw=0):
        self.seraph = json.loads(SERAPH.read_text())
        self.seat = seat
        self.yaw = yaw
        self.placing = mul(mul(translation([h / 16 for h in CENTRE]), rotation("Y", yaw)), translation([-s / 16 for s in seat]))
        animations = self.seraph["animations"] + [op["value"] for op in json.loads(POSES.read_text())
                                                  if op["path"] == "/animations/-"]
        sitting = next(a for a in animations if a["code"] == idle)
        self.idle = next(k["elements"] for k in sitting["keyframes"] if k["frame"] == 0)
        self.arms = {side: Arm(self.chain(lower), self.idle, OUTBOARD[side]) for side, lower in ARMS.items()}
        self.legs = {side: Leg(self.chain(lower), self.idle, OUTBOARD[side]) for side, lower in LEGS.items()}

    def chain(self, name):
        def descend(elements, path):
            for element in elements:
                found = [*path, element] if element["name"] == name else descend(element.get("children", []),
                                                                                 [*path, element])
                if found:
                    return found
            return None

        return descend(self.seraph["elements"], [])

    def facing(self, pose, point):
        hips = self.arms["R"].body[1]
        middle = [(a + b) / 32 - a / 16 for a, b in zip(hips["from"], hips["to"])]
        aim = [p - m for p, m in zip(unapply(self.arms["R"].frames(pose)[1], point), middle)]
        turn = math.degrees(math.atan2(aim[2], -aim[0]))
        return round(max(-TRUNK_ROTATION, min(TRUNK_ROTATION, turn)), 2)

    def body(self, point):
        return apply(self.placing, [p / 16 for p in point])

    def along(self, axis):
        return direct(self.placing, axis)

    def hand(self, pose, side):
        return tuple(16 * c for c in unapply(self.placing, self.arms[side].posture(pose).joints[-1]))


def crew(shape, vessel, seated, idle, strokes, grips, lean):
    rower = Rower(seat(shape, seated), idle, facing(shape, vessel, seated))
    shape.animations, player = fit(rower, shape.elements, strokes, grips, lean)
    shape.write()
    rowed(shape, rower, shape.animations, player)
    return player


def player_patch(name, animations):
    speeds = {animation["code"]: animation.pop("speed") for animation in animations}
    ops = []
    for animation in animations:
        ops.append({"op": "add", "file": "game:shapes/entity/humanoid/seraph-faceless.json", "path": "/animations/-",
                    "value": animation})
    for animation in animations:
        elements = sorted({e for k in animation["keyframes"] for e in k["elements"]})
        ops.append({"op": "add", "file": "game:entities/humanoid/player.json", "path": "/client/animations/-",
                    "side": "Server", "value": {
                        "code": animation["code"], "animation": animation["code"], "animationSpeed": speeds[animation["code"]],
                        "blendMode": "Average",
                        "easeInSpeed": 4, "easeOutSpeed": 4,
                        "elementWeight": {e: WEIGHT for e in elements},
                        "elementBlendMode": {e: "Average" for e in elements}}})
    path = PATCHES / f"{name}.json"
    path.write_text(json.dumps(ops, indent=4) + "\n")
    print(f"{path}: {len(animations)} animations")


def rowed(shape, rower, vessel, player, name=None, gear=()):
    offset = [s - h for s, h in zip(rower.seat, CENTRE)]
    seated = {"name": "Rower", "from": offset, "to": offset, "rotationOrigin": list(rower.seat), "rotationY": -rower.yaw,
              "faces": {}, "children": rower.seraph["elements"]}
    animations = []
    for own, arms in zip(vessel, player):
        keyframes = [{"frame": a["frame"], "elements": every_axis(rower.idle) | a["elements"] | b["elements"]}
                     for a, b in zip(own["keyframes"], arms["keyframes"])]
        animations.append(own | {"keyframes": keyframes})
    path = Path(os.environ.get("BENCH_TMP", "/tmp")) / "model" / f"{name or shape.path.stem + '-rowed'}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"textureWidth": 16, "textureHeight": 16,
                                "textures": shape.textures | rower.seraph["textures"],
                                "elements": [e for e in shape.elements if e["name"] not in ("hideWater", "Bilge")]
                                + [seated, *gear], "animations": animations}))
    print(f"{path}: the vessel rowed, for bin/model pose")


def every_axis(pose):
    keyed = {}
    for name, channels in pose.items():
        keyed[name] = {}
        for prefix in ("offset", "rotation"):
            if any(key.startswith(prefix) for key in channels):
                keyed[name] |= {prefix + axis: round(channels.get(prefix + axis, 0), 2) for axis in "XYZ"}
    return keyed


def leaning(lean, body):
    return body | {"UpperTorso": lean | body.get("UpperTorso", {})}


def fit(rower, elements, strokes, grips, lean):
    vessel, player = [], []
    for stroke in strokes:
        poses = [stroke["pose"](frame) for frame in stroke["keys"]]
        placed = [posed(elements, lambda name, pose=pose: pose.get(name, {})) for pose in poses]
        bodies = [leaning(lean, stroke["body"](frame) if stroke.get("body") else {}) for frame in stroke["keys"]]
        faults = {}
        hands_on = stroke.get("grips", grips)
        for side, grip in hands_on.items():
            arm = rower.arms[side]
            hands = [rower.body(centre(elements, m, grip)) for m in placed]
            axes = [rower.along(held(m, grip)) for m in placed]
            thumbs = [[tuple(thumb * c for c in axis) for axis in axes] for thumb in (1, -1)]
            holds = arm.stroke(bodies, hands, thumbs)
            chosen = min(range(2), key=lambda i: holds[i][1])
            arms = holds[chosen][0]
            for i in range(1, len(arms)):
                arms[i] = arm.follow(arms[i], arms[i - 1])
            for frame, body, pose, hand, axis in zip(stroke["keys"], bodies, arms, hands, thumbs[chosen]):
                body |= pose
                posture = arm.posture(body)
                for name, (excess, _) in arm.faults(posture, hand, arm.angles(posture, axis)).items():
                    faults[f"{side} {name}"] = max(faults.get(f"{side} {name}", (0, 0)), (excess, frame))
        for frame, body in zip(stroke["keys"], bodies):
            depth = rower.arms["L"].through(body, rower.arms["R"])
            faults["arms through each other"] = max(faults.get("arms through each other", (0, 0)), (depth, frame))
        worst = max(math.dist(rower.hand(body, side), centre(elements, m, grip))
                    for body, m in zip(bodies, placed) for side, grip in hands_on.items())
        own = [{"frame": frame, "elements": every_axis(pose)} for frame, pose in zip(stroke["keys"], poses)]
        arms = [{"frame": frame, "elements": every_axis(body)} for frame, body in zip(stroke["keys"], bodies)]
        print(f"{stroke['code']}: hands within {worst:.2f} of the grips")
        for name, (excess, frame) in faults.items():
            if round(excess, 1):
                print(f"  {name} by {excess:.1f} at frame {frame}")
        common = {"code": stroke["code"], "quantityframes": stroke["frames"], "onActivityStopped": "EaseOut",
                  "onAnimationEnd": stroke.get("ends", "Repeat")}
        vessel.append({"name": stroke["code"], **common, "keyframes": own})
        player.append({"name": stroke["code"], **common, "keyframes": arms, "speed": stroke["speed"]})
    return vessel, player
