from transform import identity, local, mul


class Limb:

    def __init__(self, chain, idle, outboard):
        *self.body, self.upper, self.lower = chain
        self.idle = idle
        self.outboard = outboard

    def channels(self, pose, element):
        return pose.get(element["name"]) or self.idle.get(element["name"]) or {}

    def trunk(self, pose):
        m = identity()
        out = []
        for element in self.body:
            m = mul(m, local(element, self.channels(pose, element)))
            out.append(m)
        return out

    def frames(self, pose, trunk=None):
        trunk = trunk or self.trunk(pose)
        upper = mul(trunk[-1], local(self.upper, self.channels(pose, self.upper)))
        return [*trunk, upper, mul(upper, local(self.lower, self.channels(pose, self.lower)))]
