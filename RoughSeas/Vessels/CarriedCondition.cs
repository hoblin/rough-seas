using System;

namespace RoughSeas.Vessels;

public static class CarriedCondition
{
    public static int Durability(float integrity, int maxDurability) =>
        Math.Clamp((int)Math.Round(integrity * maxDurability), 1, maxDurability);

    public static float Integrity(int durability, int maxDurability) =>
        maxDurability > 0 ? Math.Clamp((float)durability / maxDurability, 0, 1) : 1;
}
