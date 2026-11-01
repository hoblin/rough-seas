import json
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[3] / "RoughSeas/assets/roughseas"
VESSELS = ASSETS / "config/vessels"
MASS = json.loads((VESSELS / "mass.json").read_text())
MATERIALS = json.loads((VESSELS / "materials.json").read_text())
SEAWATER_KG_PER_LITRE = 1.025
# Vanilla's swim line sits this far above selectionBox.y2 - swimmingOffsetY, as the reed boat measured afloat on the bench.
SWIM_LINE_METRES = 0.018


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
