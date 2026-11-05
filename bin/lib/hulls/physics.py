import json
import math
from pathlib import Path

import units

ASSETS = Path(__file__).resolve().parents[3] / "RoughSeas/assets/roughseas"
VESSELS = ASSETS / "config/vessels"
MASS = json.loads((VESSELS / "mass.json").read_text())
MATERIALS = json.loads((VESSELS / "materials.json").read_text())
SEAWATER_KG_PER_LITRE = 1.025
# Vanilla's swim line sits this far above selectionBox.y2 - swimmingOffsetY, as the reed boat measured afloat on the bench.
SWIM_LINE_METRES = 0.018


# Stature 1.756 m, the mean of US Army men (ANSUR II, Gordon et al. 2014); a standing man's centre of mass at 0.557
# of it, de Leva 1996 segment masses at Drillis & Contini 1966 segment heights (Winter 2009, fig. 4.1).
STANDING_CENTRE_METRES = 0.98
STANDING_HIP_METRES = 0.93
# Polers kneel in a canoe narrower than about 28 in (0.71 m): the narrowest they stand in is our bark canoe at that beam.
NARROWEST_STOOD_IN_METRES = 0.71
CANOE = "barkcanoe"
CANOE_FLOOR_METRES = units.metres(0.6)

# Vanilla's rope: one item pays out up to 10 m (ClothSystem.maxLen).
ROPE_METRES = 10
HIDE_METRES = 0.005
LATH_SQUARE_METRES = 0.01 * 0.06
# A small hide is about 1.2 m²: a moose's 3.5 m² over the three leather vanilla tans from its large hide.
SECTION_SQUARE_METRES = 1.2
# The hide laced to the gunwale every 5 cm, each turn round the gunwale and the hide's edge some 15 cm of cord
# (Hornell 1938: "laced at short intervals").
LACING_STEP_METRES = 0.05
LACING_TURN_METRES = 0.15


def ropes(metres):
    return math.ceil(metres / ROPE_METRES)


def per_rope(pieces, count):
    return [pieces[len(pieces) * r // count:len(pieces) * (r + 1) // count] for r in range(count)]


def hide_sections(area):
    sections = math.ceil(area / SECTION_SQUARE_METRES)
    print(f"cover: {area:.2f} m² of hide, {sections} sections of {SECTION_SQUARE_METRES} m²")
    return sections


def lacing_metres(perimeter):
    return perimeter / LACING_STEP_METRES * LACING_TURN_METRES + perimeter


def stiffness(hull_kg, waterplane, beam_metres, floor_metres, centre_metres, form="ellipse", outboard=(0, 0)):
    kg = hull_kg + MASS["crewKg"] + MASS["playerGearKg"]
    volume = kg / (SEAWATER_KG_PER_LITRE * 1000)
    if form == "ellipse":
        length = 4 * waterplane / (math.pi * beam_metres)
        inertia = math.pi / 64 * length * beam_metres ** 3
    else:
        inertia = waterplane * beam_metres ** 2 / 12
    float_area, float_metres = outboard
    inertia += float_area * float_metres ** 2
    draft = volume / (waterplane + float_area)
    centre = (MASS["crewKg"] * (floor_metres + STANDING_CENTRE_METRES) + MASS["playerGearKg"] * floor_metres
              + hull_kg * centre_metres) / kg
    return draft / 2 + inertia / volume - centre


def built(name):
    entity = json.loads((ASSETS / f"entities/vessel/{name}.json").read_text())
    integrity, forces = (entity["behaviorConfigs"][code] for code in ("roughseas:vesselintegrity", "roughseas:seaforces"))
    return {"hull_kg": entity["weight"], "waterplane": integrity.get("waterplaneSquareMetres"),
            "beam_metres": forces["beamMetres"], "depth_metres": integrity["depthMetres"]}


def stands(name, floor_metres, form="ellipse", outboard=(0, 0), **given):
    canoe = built(CANOE)
    narrowed = NARROWEST_STOOD_IN_METRES / canoe["beam_metres"]
    least = stiffness(canoe["hull_kg"], canoe["waterplane"] * narrowed, NARROWEST_STOOD_IN_METRES, CANOE_FLOOR_METRES,
                      canoe["depth_metres"] / 3)
    hull = (built(name) if (ASSETS / f"entities/vessel/{name}.json").exists() else {}) | given
    own = stiffness(hull["hull_kg"], hull["waterplane"], hull["beam_metres"], floor_metres, hull["depth_metres"] / 3,
                    form, outboard)
    standing = own >= least
    print(f"{name}: GM {own:+.2f} m with a man standing, against {least:+.2f} m in the narrowest canoe polers stand in: "
          f"{'stands' if standing else 'sits'} to pole")
    return standing


def kilograms(material, litres):
    return litres / 1000 * MATERIALS[material]["density"]


def hitbox_height(name):
    return json.loads((ASSETS / f"entities/vessel/{name}.json").read_text())["hitboxSize"]["y"]


def afloat(name, hull_kg, depth, hold, deck, waterplane=None):
    full = hull_kg + MASS["crewKg"] + MASS["playerGearKg"] + SEAWATER_KG_PER_LITRE * hold
    by_full_hold = waterplane is None
    waterplane = full / (SEAWATER_KG_PER_LITRE * 1000 * depth) if by_full_hold else waterplane
    draft = (hull_kg + MASS["crewKg"]) / (SEAWATER_KG_PER_LITRE * 1000 * waterplane)
    offset = hitbox_height(name) + SWIM_LINE_METRES - draft
    print(f"{name} afloat: {hull_kg:.1f} kg, depth {depth:.4g} m, hold {hold:.0f} L, deck {deck:.2f} m², "
          f"waterplane {waterplane:.2f} m²{' (full hold and one aboard bring her gunwale to the water)' if by_full_hold else ''}, "
          f"draft {draft:.3f} m with one aboard, swimmingOffsetY {offset:.3f}")
    return waterplane
