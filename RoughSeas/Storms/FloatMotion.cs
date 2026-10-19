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
    private static IDrawnSea? drawn;

    public static void Attach(IDrawnSea sea)
    {
        if (harmony != null) return;
        drawn = sea;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityItemRenderer), "LoadModelMatrix"),
            postfix: new HarmonyMethod(typeof(FloatMotion), nameof(Lift)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        drawn = null;
    }

    private static void Lift(EntityItemRenderer __instance, EntityItem ___entityitem)
    {
        if (drawn is not { Swell: true } || !___entityitem.Swimming) return;
        if (___entityitem.Itemstack?.Collectible.MaterialDensity > WaterDensity) return;

        var pos = ___entityitem.Pos;
        __instance.ModelMat[ModelMatrixY] += (float)drawn.HeightAt(pos.X, pos.Z);
    }
}
