using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

public static class FloatMotion
{
    private const string HarmonyId = "roughseas.storms.float";
    private const int ModelMatrixY = 13;
    private const float WaterDensity = 1000;

    private static Harmony? harmony;
    private static SurfaceField? field;
    private static SeaSurface? surface;

    public static void Attach(SurfaceField heights, SeaSurface waves)
    {
        if (harmony != null) return;
        (field, surface) = (heights, waves);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityItemRenderer), "LoadModelMatrix"),
            postfix: new HarmonyMethod(typeof(FloatMotion), nameof(Lift)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (field, surface) = (null, null);
    }

    private static void Lift(EntityItemRenderer __instance, EntityItem ___entityitem)
    {
        if (surface == null || field == null || surface.Calm || !___entityitem.Swimming) return;
        if (___entityitem.Itemstack?.Collectible.MaterialDensity > WaterDensity) return;

        var pos = ___entityitem.Pos;
        var openSea = field.Factor(pos.X, pos.Z);
        if (openSea > 0) __instance.ModelMat[ModelMatrixY] += (float)surface.HeightAt(pos.X, pos.Z, openSea);
    }
}
