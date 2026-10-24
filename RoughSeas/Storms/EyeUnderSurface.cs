using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace RoughSeas.Storms;

/// <summary>
/// Puts the camera under water by the drawn sea, not by the water block at the eye. Vanilla judges the eye from that
/// block alone: <c>ClientMain.GetEyesInWaterColorShift</c> drives the underwater fog, the tint and every shader's
/// <c>cameraUnderwater</c>, <c>ClientMain.EyesInWaterDepth</c> the muffled sound and the deep water's murk. Where
/// <see cref="DrawnWater"/> finds no drawn surface over the eye, both stay vanilla's.
/// </summary>
public static class EyeUnderSurface
{
    private const string HarmonyId = "roughseas.storms.eye";
    private const int FullShift = 100;

    private static Harmony? harmony;
    private static DrawnWater? water;
    private static EyeConfig config = new();
    private static ICoreClientAPI? capi;
    private static StormsModSystem? system;
    private static bool? under;

    public static void Attach(StormsModSystem storms, ICoreClientAPI api, DrawnWater drawn, EyeConfig eye)
    {
        if (harmony != null) return;
        (system, capi, water, config) = (storms, api, drawn, eye);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(ClientMain), nameof(ClientMain.GetEyesInWaterColorShift)),
            postfix: new HarmonyMethod(typeof(EyeUnderSurface), nameof(ColorShift)) { priority = Priority.High });
        harmony.Patch(AccessTools.Method(typeof(ClientMain), nameof(ClientMain.EyesInWaterDepth)),
            postfix: new HarmonyMethod(typeof(EyeUnderSurface), nameof(Depth)) { priority = Priority.High });
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (system, capi, water, under) = (null, null, null, null);
    }

    private static void ColorShift(ClientMain __instance, ref int __result)
    {
        var vanilla = __result;
        var depth = EyeDepth(__instance);
        if (depth is { } metres) __result = (int)Math.Clamp(config.ShiftPerBlock * (metres + config.ShiftAboveBlocks), 0, FullShift);
        Decided(__instance, depth, vanilla, __result);
    }

    private static void Depth(ClientMain __instance, ref float __result)
    {
        if (EyeDepth(__instance) is { } metres) __result = (float)Math.Max(0, metres);
    }

    // how far the eye is under the drawn surface, negative above it; null where vanilla decides
    private static double? EyeDepth(ClientMain game)
    {
        if (water == null || capi == null) return null;
        var eye = game.MainCamera.CameraEyePos;
        return water.SurfaceAt(capi.World.BlockAccessor, eye.X, eye.Y, eye.Z) is { } surface ? surface - eye.Y : null;
    }

    private static void Decided(ClientMain game, double? depth, int vanilla, int shift)
    {
        var now = depth > 0 ? true : depth.HasValue ? false : (bool?)null;
        if (now == under) return;
        under = now;
        var eye = game.MainCamera.CameraEyePos;
        if (depth is not { } metres)
        {
            system?.Log("eye at {0:0} {1:0.00} {2:0}: no drawn surface over it, vanilla's view (shift {3})", eye.X, eye.Y, eye.Z, vanilla);
            return;
        }
        system?.Log("eye at {0:0} {1:0.00} {2:0}: {3} the drawn surface by {4:0.00} m, shift {5} (vanilla's {6})",
            eye.X, eye.Y, eye.Z, metres > 0 ? "under" : "above", Math.Abs(metres), shift, vanilla);
    }
}
