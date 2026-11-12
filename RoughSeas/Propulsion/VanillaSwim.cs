using RoughSeas.SeaAndWeather;
using Vintagestory.API.MathTools;

namespace RoughSeas.Propulsion;

/// <summary>
/// Vanilla's water drag on a driven boat. Its swim drag needs nothing: it never acts on a boat steered from a client
/// (<see cref="EntityBehaviorPropulsion.Drive"/>).
/// </summary>
public static class VanillaSwim
{
    public static void LeaveWaterDragOnHeave(Vec3d motion, double waterDrag, float seconds)
    {
        var kept = WaterDrag.Kept(waterDrag, seconds);
        motion.X /= kept;
        motion.Z /= kept;
    }
}
