import json
import math
from pathlib import Path

import units
from jsontext import load

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
# Reed or flax cord 1 cm thick breaks as flax rope does at 12 mm (rope-flax), by its section.
CORD_BREAKING_NEWTONS = MATERIALS["rope-flax"]["strength"] * 1000 * (10 / 12) ** 2
CORD_STRETCH_AT_BREAK = MATERIALS["rope-flax"]["elongation"]
# A lashing takes a blow as the raft's do: her impactCapacity over the three ropes of her recipe.
LASHING_JOULES_PER_METRE = next(op["value"]["impactCapacity"] for op in load(ASSETS / "patches/vessels-vanilla-boats.json")
                                if op["file"].endswith("boat-raft.json") and op["path"].endswith("roughseas:vesselintegrity")) / (3 * ROPE_METRES)
# A hide seam weeps once its tallow has washed out: some 15 L an hour across a 2.3 m seam (an estimate).
SEAM_LITRES_PER_HOUR_A_METRE = 15 / 2.3


def ropes(metres):
    return math.ceil(metres / ROPE_METRES)


def per_rope(pieces, count):
    return [pieces[len(pieces) * r // count:len(pieces) * (r + 1) // count] for r in range(count)]


def hide_sections(area_square_metres):
    sections = math.ceil(area_square_metres / SECTION_SQUARE_METRES)
    print(f"cover: {area_square_metres:.2f} m² of hide, {sections} sections of {SECTION_SQUARE_METRES} m²")
    return sections


def lacing_metres(perimeter_metres):
    return perimeter_metres / LACING_STEP_METRES * LACING_TURN_METRES + perimeter_metres


def stiffness(hull_kg, waterplane_square_metres, beam_metres, floor_metres, centre_metres, form="ellipse", outboard=(0, 0)):
    kg = hull_kg + MASS["crewKg"] + MASS["playerGearKg"]
    volume = kg / (SEAWATER_KG_PER_LITRE * 1000)
    if form == "ellipse":
        length = 4 * waterplane_square_metres / (math.pi * beam_metres)
        inertia = math.pi / 64 * length * beam_metres ** 3
    else:
        inertia = waterplane_square_metres * beam_metres ** 2 / 12
    float_square_metres, float_off_metres = outboard
    inertia += float_square_metres * float_off_metres ** 2
    draft = volume / (waterplane_square_metres + float_square_metres)
    centre = (MASS["crewKg"] * (floor_metres + STANDING_CENTRE_METRES) + MASS["playerGearKg"] * floor_metres
              + hull_kg * centre_metres) / kg
    return draft / 2 + inertia / volume - centre


def built(name):
    entity = load(ASSETS / f"entities/vessel/{name}.json")
    integrity = entity["behaviorConfigs"]["roughseas:vesselintegrity"]
    return {"hull_kg": entity["weight"], "waterplane_square_metres": integrity.get("waterplaneSquareMetres"),
            "beam_metres": integrity["hullBoxMetres"]["to"][2] - integrity["hullBoxMetres"]["from"][2], "depth_metres": integrity["depthMetres"]}


def stands(name, floor_metres, form="ellipse", outboard=(0, 0), **given):
    canoe = built(CANOE)
    narrowed = NARROWEST_STOOD_IN_METRES / canoe["beam_metres"]
    least = stiffness(canoe["hull_kg"], canoe["waterplane_square_metres"] * narrowed, NARROWEST_STOOD_IN_METRES, CANOE_FLOOR_METRES,
                      canoe["depth_metres"] / 3)
    hull = (built(name) if (ASSETS / f"entities/vessel/{name}.json").exists() else {}) | given
    own = stiffness(hull["hull_kg"], hull["waterplane_square_metres"], hull["beam_metres"], floor_metres, hull["depth_metres"] / 3,
                    form, outboard)
    standing = own >= least
    print(f"{name}: GM {own:+.2f} m with a man standing, against {least:+.2f} m in the narrowest canoe polers stand in: "
          f"{'stands' if standing else 'sits'} to pole")
    return standing


def kilograms(material, litres):
    return litres / 1000 * MATERIALS[material]["density"]


def hitbox_height_of(name):
    return load(ASSETS / f"entities/vessel/{name}.json")["hitboxSize"]["y"]


def afloat(name, hull_kg, depth_metres, hold_litres=0, deck_square_metres=0, waterplane_square_metres=None, solid_litres=None,
           hitbox_height_metres=None, at_draft=None):
    """Her weight, the depth and waterplane she floats by, her hold and the offset vanilla floats her at with one
    aboard, as her entity takes them, with what at_draft measures at that draft. A solid hull is pressed down through
    her depth as her material is."""
    crewed = hull_kg + MASS["crewKg"]
    integrity = units.both("depth", depth_metres)
    configs = {"roughseas:vesselintegrity": integrity}
    if solid_litres is not None:
        draft = depth_metres * crewed / (solid_litres * SEAWATER_KG_PER_LITRE)
        print(f"{name} afloat: {hull_kg:.1f} kg, depth {depth_metres:.4g} m, {solid_litres:.0f} L of material, "
              f"draft {draft:.3f} m with one aboard", end="")
    else:
        full = crewed + MASS["playerGearKg"] + SEAWATER_KG_PER_LITRE * hold_litres
        by_full_hold = waterplane_square_metres is None
        if by_full_hold:
            waterplane_square_metres = full / (SEAWATER_KG_PER_LITRE * 1000 * depth_metres)
        draft = crewed / (SEAWATER_KG_PER_LITRE * 1000 * waterplane_square_metres)
        integrity |= units.both("waterplane", waterplane_square_metres, 2)
        configs["roughseas:bilge"] = units.both("capacity", hold_litres, 3) | units.both("deck", deck_square_metres, 2)
        print(f"{name} afloat: {hull_kg:.1f} kg, depth {depth_metres:.4g} m, hold {hold_litres:.0f} L, deck {deck_square_metres:.2f} m², "
              f"waterplane {waterplane_square_metres:.2f} m²{' (full hold and one aboard bring her gunwale to the water)' if by_full_hold else ''}, "
              f"draft {draft:.3f} m with one aboard", end="")
    offset = (hitbox_height_of(name) if hitbox_height_metres is None else hitbox_height_metres) + SWIM_LINE_METRES - draft
    print(f", swimmingOffsetY {offset:.3f}")
    afloat_ = {"weight": round(hull_kg, 1), "attributes": {"swimmingOffsetY": round(offset, 3)}, "behaviorConfigs": configs}
    return merged(afloat_, at_draft(draft)) if at_draft else afloat_


def merged(into, *more):
    for values in more:
        for key, value in values.items():
            if isinstance(value, dict) and isinstance(into.get(key), dict):
                merged(into[key], value)
            else:
                into[key] = value
    return into


def shares(kilograms_by_part):
    """Each part's share of her weight, in the order her parts are listed."""
    return {"behaviorConfigs": {"roughseas:vesselintegrity": {"parts": {n: {"share": round(kg, 1)} for n, kg in enumerate(kilograms_by_part)}}}}
