using RoughSeas.SeaAndWeather;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>Vanilla's water drag on a driven boat, which ours replaces along the water.</summary>
public static class VanillaSwim
{
    public static void LeaveWaterDragOnHeave(Vec3d motion, double waterDrag, float seconds)
    {
        var kept = WaterDrag.Kept(waterDrag, seconds);
        motion.X /= kept;
        motion.Z /= kept;
    }
}
