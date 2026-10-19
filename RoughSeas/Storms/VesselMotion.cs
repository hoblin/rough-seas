using System;
using System.Collections.Generic;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

public static class VesselMotion
{
    private const string HarmonyId = "roughseas.storms.motion";
    private const int ModelMatrixY = 13;
    private const double LogEverySeconds = 5;

    private static readonly Dictionary<long, (long Frame, SeaPose Pose)> poses = [];
    private static readonly Dictionary<long, (HullResponse Response, double Seconds)> responses = [];
    private static Harmony? harmony;
    private static StormsModSystem? storms;
    private static IDrawnSea? drawn;
    private static SeaSurface? surface;
    private static double largestStep;
    private static double loggedAt = double.NegativeInfinity;
    private static double tracedUntil = double.NegativeInfinity;

    public static void Attach(StormsModSystem system, IDrawnSea sea, SeaSurface waves)
    {
        if (harmony != null) return;
        (storms, drawn, surface) = (system, sea, waves);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(NetworkAPI), nameof(NetworkAPI.SendPlayerPositionPacket)),
            prefix: new HarmonyMethod(typeof(VesselMotion), nameof(SendUnlifted)),
            postfix: new HarmonyMethod(typeof(VesselMotion), nameof(RestoreLift)));
        harmony.Patch(AccessTools.Method(typeof(EntityBoat), nameof(EntityBoat.OnRenderFrame)),
            postfix: new HarmonyMethod(typeof(VesselMotion), nameof(Respond)));
        harmony.Patch(AccessTools.Method(typeof(EntityShapeRenderer), nameof(EntityShapeRenderer.loadModelMatrix)),
            postfix: new HarmonyMethod(typeof(VesselMotion), nameof(LiftModel)));
        harmony.Patch(AccessTools.PropertyGetter(typeof(EntityRideableSeat), nameof(EntityRideableSeat.SeatPosition)),
            postfix: new HarmonyMethod(typeof(VesselMotion), nameof(LiftSeat)));
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorSelectionBoxes), "applyBoxTransform"),
            prefix: new HarmonyMethod(typeof(VesselMotion), nameof(LiftSelectionBox)));
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        (storms, drawn, surface) = (null, null, null);
        poses.Clear();
        responses.Clear();
        (largestStep, loggedAt, tracedUntil) = (0, double.NegativeInfinity, double.NegativeInfinity);
    }

    private static void Respond(EntityBoat __instance)
    {
        if (__instance.Api is ICoreClientAPI { IsGamePaused: true }) return;
        var forces = __instance.GetBehavior<EntityBehaviorSeaForces>();
        if (forces == null || __instance.Properties.Client.Renderer is not EntityShapeRenderer renderer) return;

        var seaworthiness = forces.Seaworthiness;
        var rocking = __instance.mountAngle;
        var pose = PoseOf(__instance);
        renderer.xangle += (float)(GameMath.Lerp(rocking.X * seaworthiness.RollResponse, pose.Roll, pose.Hold) - rocking.X);
        renderer.zangle += (float)(GameMath.Lerp(rocking.Z * seaworthiness.PitchResponse, pose.Pitch, pose.Hold) - rocking.Z);
        if (pose.Hold > 0) Report(__instance, pose);
        if (surface!.Seconds < tracedUntil)
        {
            storms!.Log(__instance, "surface trace: frame {0} at {1:F3} s, swimming {2}, at {3:F2} {4:F2}; lifted {5:F3} m, rolled {6:F2}°, pitched {7:F2}°",
                surface.Frame, surface.Seconds, __instance.Swimming, __instance.Pos.X, __instance.Pos.Z, pose.Lift, pose.Roll * GameMath.RAD2DEG, pose.Pitch * GameMath.RAD2DEG);
        }
    }

    public static void Trace(double seconds)
    {
        if (surface != null) tracedUntil = surface.Seconds + seconds;
    }

    private static void LiftModel(EntityShapeRenderer __instance, Entity entity, bool isShadowPass)
    {
        if (entity is EntityBoat boat && PoseOf(boat).Lift is var lift and not 0) __instance.ModelMat[ModelMatrixY] += (float)lift;
        if (entity is EntityBoat traced && surface!.Seconds < tracedUntil && !isShadowPass)
        {
            storms!.Log(traced, "surface trace: frame {0}, model at {1:F3} {2:F3} {3:F3} from the camera",
                surface.Frame, __instance.ModelMat[12], __instance.ModelMat[13], __instance.ModelMat[14]);
        }
    }

    private static void LiftSeat(EntityRideableSeat __instance, EntityPos __result)
    {
        if (__instance.Entity is EntityBoat { Api.Side: EnumAppSide.Client } boat) __result.Y += PoseOf(boat).Lift;
    }

    private static void LiftSelectionBox(EntityBehaviorSelectionBoxes __instance, Matrixf mvmat)
    {
        if (__instance.entity is EntityBoat { Api.Side: EnumAppSide.Client } boat && PoseOf(boat).Lift is var lift and not 0)
        {
            mvmat.Translate(0, (float)lift, 0);
        }
    }

    private static void SendUnlifted(ClientMain ___game, out double __state)
    {
        __state = ___game.EntityPlayer?.MountedOn?.Entity is EntityBoat boat ? PoseOf(boat).Lift : 0;
        if (__state != 0) ___game.EntityPlayer!.Pos.Y -= __state;
    }

    private static void RestoreLift(ClientMain ___game, double __state)
    {
        if (__state != 0) ___game.EntityPlayer.Pos.Y += __state;
    }

    private static SeaPose PoseOf(EntityBoat boat)
    {
        if (surface == null || drawn == null) return default;
        if (poses.TryGetValue(boat.EntityId, out var cached) && cached.Frame == surface.Frame) return cached.Pose;

        var pose = Ride(boat, drawn);
        if (cached.Frame == surface.Frame - 1) largestStep = Math.Max(largestStep, Math.Abs(pose.Lift - cached.Pose.Lift));
        poses[boat.EntityId] = (surface.Frame, pose);
        return pose;
    }

    private static SeaPose Ride(EntityBoat boat, IDrawnSea sea)
    {
        var forces = boat.GetBehavior<EntityBehaviorSeaForces>();
        if (forces == null || !boat.Swimming) return default;

        var pos = boat.Pos;
        var box = boat.CollisionBox;
        var side = Math.Max(box.XSize, box.ZSize);
        var yaw = pos.Yaw + (boat.Properties.Client.Shape?.rotateY ?? 0) * GameMath.DEG2RAD;
        var pose = Answer(boat, forces, sea.PoseOf(pos.X, pos.Z, yaw, side, side), side);
        var seaworthiness = forces.Seaworthiness;
        return pose with { Roll = pose.Roll * seaworthiness.RollResponse, Pitch = pose.Pitch * seaworthiness.PitchResponse };
    }

    private static SeaPose Answer(EntityBoat boat, EntityBehaviorSeaForces forces, SeaPose pose, double length)
    {
        if (!responses.TryGetValue(boat.EntityId, out var answered))
        {
            answered = (new HullResponse(0, 0, storms!.Config.Response), surface!.Seconds);
        }
        var response = answered.Response;
        if (boat.GetBehavior<EntityBehaviorVesselIntegrity>() is { Floats: true } hull)
        {
            double draft = hull.Afloat.DraftMetres;
            response.HeavePeriodSeconds = HullResponse.HeavePeriod(draft, storms!.Config.Response);
            response.RollPeriodSeconds = HullResponse.RollPeriod(length, forces.BeamMetres, draft, hull.DepthMetres);
        }
        responses[boat.EntityId] = (response, surface!.Seconds);
        return response.Follow(pose, surface.Seconds - answered.Seconds);
    }

    private static void Report(EntityBoat boat, SeaPose pose)
    {
        if (surface!.Seconds - loggedAt < LogEverySeconds) return;
        loggedAt = surface.Seconds;
        storms!.Log(boat, "surface: rides lifted {0:F2} m, rolled {1:F1}°, pitched {2:F1}°; largest lift step between frames {3:F3} m",
            pose.Lift, pose.Roll * GameMath.RAD2DEG, pose.Pitch * GameMath.RAD2DEG, largestStep);
        largestStep = 0;
    }
}
