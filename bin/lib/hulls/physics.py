import json
from pathlib import Path

VESSELS = Path(__file__).resolve().parents[3] / "RoughSeas/assets/roughseas/config/vessels"
MASS = json.loads((VESSELS / "mass.json").read_text())
MATERIALS = json.loads((VESSELS / "materials.json").read_text())
SEAWATER_KG_PER_LITRE = 1.025
SWIM_LINE_METRES = 0.018


def kilograms(material, litres):
    return litres / 1000 * MATERIALS[material]["density"]


def afloat(name, hull_kg, depth, hold, deck, hitbox, waterplane=None):
    full = hull_kg + MASS["crewKg"] + MASS["playerGearKg"] + SEAWATER_KG_PER_LITRE * hold
    rule = waterplane is None
    waterplane = full / (SEAWATER_KG_PER_LITRE * 1000 * depth) if rule else waterplane
    draft = (hull_kg + MASS["crewKg"]) / (SEAWATER_KG_PER_LITRE * 1000 * waterplane)
    offset = hitbox + SWIM_LINE_METRES - draft
    print(f"{name} afloat: {hull_kg:.1f} kg, depth {depth:.4g} m, hold {hold:.0f} L, deck {deck:.2f} m², "
          f"waterplane {waterplane:.2f} m²{' (full hold and one aboard bring her gunwale to the water)' if rule else ''}, "
          f"draft {draft:.3f} m with one aboard, swimmingOffsetY {offset:.3f}")
    return waterplane
