import math

from transform import apply, identity, local, mul

SOLE = (2, 0, 1.5)
# Knee flexion and hip abduction in degrees, AAOS normal values.
KNEE = (0, 140)
SPREAD = (-30, 45)
STEP = 1e-3
ROUNDS = 60
BENT = 20
MOST = 0.2
DAMPING = 1e-3


class Leg:

    def __init__(self, chain, idle, outboard):
        *self.body, self.upper, self.lower = chain
        self.idle = idle
        self.outboard = outboard

    def sole(self, pose):
        m = identity()
        for element in [*self.body, self.upper, self.lower]:
            m = mul(m, local(element, pose.get(element["name"]) or self.idle.get(element["name"]) or {}))
        return apply(m, [c / 16 for c in SOLE])

    def plant(self, pose, foot):
        upper, lower = self.upper["name"], self.lower["name"]
        start = self.idle.get(upper, {}), self.idle.get(lower, {})
        angles = [start[0].get("rotationX", 0), start[0].get("rotationZ", 0) - BENT / 2, start[1].get("rotationZ", 0) + BENT]

        def posed(a):
            return pose | {upper: {"rotationX": a[0], "rotationY": start[0].get("rotationY", 0), "rotationZ": a[1]},
                           lower: {"rotationX": start[1].get("rotationX", 0), "rotationY": 0, "rotationZ": a[2]}}

        def miss(a):
            return [s - f for s, f in zip(self.sole(posed(a)), foot)]

        for _ in range(ROUNDS):
            error = miss(angles)
            if math.hypot(*error) < 1e-5:
                break
            columns = []
            for i in range(3):
                nudged = list(angles)
                nudged[i] += math.degrees(STEP)
                columns.append([(n - e) / STEP for n, e in zip(miss(nudged), error)])
            step = solve(columns, [-e for e in error])
            largest = max(abs(s) for s in step)
            step = [s * min(1, MOST / largest) for s in step]
            angles = [a + math.degrees(s) for a, s in zip(angles, step)]
            angles[2] = min(max(angles[2], KNEE[0]), KNEE[1])
        return {name: {axis: round(value, 2) for axis, value in channels.items()}
                for name, channels in posed(angles).items() if name in (upper, lower)}

    def faults(self, pose, foot):
        knee = pose[self.lower["name"]]["rotationZ"]
        spread = self.outboard * pose[self.upper["name"]]["rotationX"]
        found = {"foot off the floor": 16 * math.dist(self.sole(pose), foot),
                 "knee flexion": max(0.0, KNEE[0] - knee, knee - KNEE[1]),
                 "hip abduction": max(0.0, SPREAD[0] - spread, spread - SPREAD[1])}
        return {name: excess for name, excess in found.items() if excess > 1e-3}


def solve(columns, b):
    a = [[columns[j][i] for j in range(3)] for i in range(3)]
    normal = [[sum(a[k][i] * a[k][j] for k in range(3)) + (DAMPING if i == j else 0) for j in range(3)] for i in range(3)]
    rhs = [sum(a[k][i] * b[k] for k in range(3)) for i in range(3)]
    for i in range(3):
        pivot = normal[i][i]
        for j in range(i + 1, 3):
            f = normal[j][i] / pivot
            normal[j] = [x - f * y for x, y in zip(normal[j], normal[i])]
            rhs[j] -= f * rhs[i]
    x = [0.0] * 3
    for i in reversed(range(3)):
        x[i] = (rhs[i] - sum(normal[i][j] * x[j] for j in range(i + 1, 3))) / normal[i][i]
    return x
