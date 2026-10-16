using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace RoughSeas.SeaAndWeather;

public static class SwellRide
{
    private const string HarmonyId = "roughseas.swellride";
    private const long LogEveryMs = 5000;
    private const int ModelMatrixY = 13;

    private static Harmony? harmony;
    private static SeaWeatherSystem? sea;
    private static ICoreClientAPI? capi;
    private static long loggedAt;
    private static double largestStep;
    private static readonly Dictionary<long, double> lastLift = [];

    public static void Attach(SeaWeatherSystem system, ICoreClientAPI api)
    {
        sea = system;
        capi = api;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBoat), nameof(EntityBoat.OnRenderFrame)), postfix: new HarmonyMethod(typeof(SwellRide), nameof(Tilt)));
        harmony.Patch(AccessTools.Method(typeof(EntityShapeRenderer), nameof(EntityShapeRenderer.loadModelMatrix)), postfix: new HarmonyMethod(typeof(SwellRide), nameof(LiftModel)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(EntityRideableSeat), nameof(EntityRideableSeat.SeatPosition)), postfix: new HarmonyMethod(typeof(SwellRide), nameof(LiftSeat)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        sea = null;
        capi = null;
    }

    private static void Tilt(EntityBoat __instance)
    {
        if (__instance.Properties.Client.Renderer is not EntityShapeRenderer renderer || Pose(__instance) is not { } pose) return;

        var rocking = __instance.mountAngle;
        renderer.xangle += pose.AboutX - rocking.X;
        renderer.zangle += pose.AboutZ - rocking.Z;
        rocking.X = pose.AboutX;
        rocking.Z = pose.AboutZ;

        if (lastLift.TryGetValue(__instance.EntityId, out var previous)) largestStep = Math.Max(largestStep, Math.Abs(pose.Lift - previous));
        lastLift[__instance.EntityId] = pose.Lift;
        if (capi!.ElapsedMilliseconds - loggedAt < LogEveryMs) return;
        loggedAt = capi.ElapsedMilliseconds;
        sea!.Mod.Logger.Debug("[swell] {0} {1} rides at {2:F0} {3:F0}: lifted {4:F2} m, tilted {5:F1}° about its X, {6:F1}° about its Z; largest lift step between frames {7:F3} m",
            __instance.Code, __instance.EntityId, __instance.Pos.X, __instance.Pos.Z, pose.Lift, pose.AboutX * GameMath.RAD2DEG, pose.AboutZ * GameMath.RAD2DEG, largestStep);
        largestStep = 0;
    }

    private static void LiftModel(EntityShapeRenderer __instance, Entity entity)
    {
        if (entity is EntityBoat boat && Pose(boat) is { } pose) __instance.ModelMat[ModelMatrixY] += (float)pose.Lift;
    }

    private static void LiftSeat(EntityRideableSeat __instance, EntityPos __result)
    {
        if (__instance.Entity is EntityBoat boat && boat.Api.Side == EnumAppSide.Client && Pose(boat) is { } pose) __result.Y += pose.Lift;
    }

    private static BoatPose? Pose(EntityBoat boat)
    {
        if (sea == null || capi == null || !boat.Swimming) return null;

        var swell = sea.Swell;
        swell.Advance(capi.InWorldEllapsedMilliseconds / 1000.0);
        var pos = boat.Pos;
        var openSea = swell.Calm ? 0 : sea.OpenSea(pos.X, pos.Z);
        if (openSea <= 0) return null;

        var steepness = sea.Surface.Steepness;
        var box = boat.CollisionBox;
        var half = Math.Max(box.XSize, box.ZSize) / 2;
        var yaw = pos.Yaw + ((boat.Properties.Client.Shape?.rotateY ?? 0) + 90) * GameMath.DEG2RAD;
        double At(double alongX, double alongZ) =>
            openSea * swell.Height(
                pos.X + (Math.Cos(yaw) * alongX + Math.Sin(yaw) * alongZ) * half,
                pos.Z + (-Math.Sin(yaw) * alongX + Math.Cos(yaw) * alongZ) * half,
                steepness);

        var (plusX, minusX, plusZ, minusZ) = (At(1, 0), At(-1, 0), At(0, 1), At(0, -1));
        return new BoatPose(
            (plusX + minusX + plusZ + minusZ) / 4,
            (float)-Math.Atan((plusZ - minusZ) / (2 * half)),
            (float)Math.Atan((plusX - minusX) / (2 * half)));
    }

    private readonly record struct BoatPose(double Lift, float AboutX, float AboutZ);
}
