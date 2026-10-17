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
    private static SeaWeatherSystem? sea;
    private static SeaSurface? surface;

    public static void Attach(SeaWeatherSystem weather, SeaSurface waves)
    {
        if (harmony != null) return;
        (sea, surface) = (weather, waves);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityItemRenderer), "LoadModelMatrix"),
            postfix: new HarmonyMethod(typeof(FloatMotion), nameof(Lift)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (sea, surface) = (null, null);
    }

    private static void Lift(EntityItemRenderer __instance, EntityItem ___entityitem)
    {
        if (surface == null || sea == null || surface.Calm || !___entityitem.Swimming) return;
        if (___entityitem.Itemstack?.Collectible.MaterialDensity > WaterDensity) return;

        var pos = ___entityitem.Pos;
        var openSea = SeaSurface.OpenSea(sea.Oceanity(pos.X, pos.Z));
        if (openSea > 0) __instance.ModelMat[ModelMatrixY] += (float)(openSea * surface.DisplacementAt(pos.X, pos.Z).Y);
    }
}
