import json
import math
from pathlib import Path

from shape import BILGE, Shape, bilge, box, group, paddle, plank, water_plane, write

CENTRE = 8
STAVES = 16
PROFILE = [(6.4, 0.6), (9.2, 2.8), (10.2, 7.4)]


def ring(radius, y, offset=0.5):
    return [(CENTRE + radius * math.cos(2 * math.pi * (i + offset) / STAVES), y,
             CENTRE + radius * math.sin(2 * math.pi * (i + offset) / STAVES)) for i in range(STAVES)]


def tiers():
    for k, ((r0, y0), (r1, y1)) in enumerate(zip(PROFILE, PROFILE[1:]), 1):
        yield k, r0, y0, math.hypot(r1 - r0, y1 - y0), math.degrees(math.atan2(r1 - r0, y1 - y0))


def bowl():
    elements = []
    for k, radius, y, length, flare in tiers():
        lower = ring(radius, y)
        for i in range(STAVES):
            a, b = lower[i], lower[(i + 1) % STAVES]
            elements.append(plank(f"Side{k}x{i + 1}", a, b, length, 0.6, flare, -1, "hide", "hide", overlap=0.5))
            x, _, z = a
            angle = math.degrees(math.atan2(z - CENTRE, x - CENTRE))
            elements.append(group(f"Rib{k}x{i + 1}Turn", (x, y, z), (0, -angle, 0), [
                box(f"Rib{k}x{i + 1}", (-1.1, 0, -0.4), (-0.6, length, 0.4), "rim", origin=(0, 0, 0), rot=(0, 0, -flare),
                    grain="y"),
            ]))
    bottom = PROFILE[0][0]
    for n, offset in enumerate(range(-6, 7, 2), 1):
        middle = max(abs(offset) - 1, 0)
        reach = math.sqrt(bottom ** 2 - middle ** 2) - 0.8
        elements.append(box(f"Bottom{n}", (CENTRE - reach, 0, CENTRE + offset - 1),
                            (CENTRE + reach, 0.7, CENTRE + offset + 1), "hide"))
    for n, offset in enumerate((-4.5, -1.5, 1.5, 4.5), 1):
        reach = math.sqrt(bottom ** 2 - offset ** 2) - 0.4
        elements.append(box(f"LathEW{n}", (CENTRE - reach, 0.7, CENTRE + offset - 0.4),
                            (CENTRE + reach, 1, CENTRE + offset + 0.4), "rim"))
        elements.append(box(f"LathNS{n}", (CENTRE + offset - 0.4, 1, CENTRE - reach),
                            (CENTRE + offset + 0.4, 1.3, CENTRE + reach), "rim", grain="z"))
    radius, top = PROFILE[-1]
    upper = ring(radius + 0.2, top)
    for i in range(STAVES):
        a, b = upper[i], upper[(i + 1) % STAVES]
        elements.append(plank(f"Rim{i + 1}", a, b, 1.1, 1.4, 0, -1, "rim", "rim", overlap=0.7))
    for n, i in enumerate(range(0, STAVES, 2), 1):
        x, y, z = upper[i]
        angle = math.degrees(math.atan2(z - CENTRE, x - CENTRE))
        elements.append(group(f"Wrap{n}Turn", (x, y, z), (0, -angle, 0), [
            box(f"Wrap{n}", (-1.9, -0.4, -0.35), (0.5, 1.4, 0.35), "lashing"),
        ]))
    seat = 9.6
    elements.append(box("Seat", (CENTRE - seat, 4.6, CENTRE - 2.6), (CENTRE + seat, 5.4, CENTRE + 2.6), "rim"))
    elements.append(paddle((3, 1.4, 6), (0, -30, 0), 7, 4.5, "rim"))
    return elements


TEXTURES = {
    "hide": "game:block/cloth/armor-padding/hide",
    "rim": "game:block/wood/debarked/oak",
    "lashing": "game:item/resource/rope",
}


def surface(prefix, height, texture="null"):
    (r0, y0), (r1, y1) = next(pair for pair in zip(PROFILE, PROFILE[1:]) if height <= pair[1][1])
    radius = r0 + (height - y0) / (y1 - y0) * (r1 - r0) - 0.8
    planes = []
    for n, offset in enumerate(range(-9, 9, 2), 1):
        middle = max(0, offset, -offset - 2)
        reach = math.sqrt(max(radius ** 2 - middle ** 2, 0))
        if reach > 0:
            planes.append(water_plane(f"{prefix}{n}", CENTRE - reach, CENTRE + reach, CENTRE + offset, CENTRE + offset + 2,
                                      height, texture))
    return planes


def build(path):
    shape = Shape(path, {**TEXTURES, "bilge": BILGE})
    shape.add(bowl(), group("hideWater", (0, 0, 0), (0, 0, 0), surface("hideWater", 7.1)),
              bilge(1.3, 7.1, lambda prefix, height: surface(prefix, height, "bilge")))
    shape.keep("Hull", "FrontSeat")
    shape.write()

    back = Path(path).with_name("coracle-back.json")
    worn = json.loads(back.read_text())
    root = worn["elements"][0]
    root["children"][0]["children"] = bowl()
    worn["textures"] = TEXTURES
    write(back, worn)
    print(f"{back}: worn bowl")


if __name__ == "__main__":
    import sys
    build(sys.argv[1])
