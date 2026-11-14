using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace RoughSeas.Compat.WalkableEntities;

/// <summary>
/// Walkable Entities places a deck by its boat's <c>Pos</c>, which our swell never moves: it is drawn on each client,
/// and the server keeps the boat on still water. A deck of a boat we pose takes the lift and the angles this client
/// draws it at, and a player standing on one tells the server where they stand on the level deck.
/// </summary>
public static class WalkableDecks
{
    private const string HarmonyId = "roughseas.compat.walkableentities.decks";

    private static readonly ConditionalWeakTable<Entity, StrongBox<(DeckPose Plain, DeckPose Drawn)>> poses = new();
    private static readonly object patchLock = new();
    private static int attached;
    private static Harmony? harmony;
    private static MethodInfo standingOn = null!;
    private static FieldInfo bodyEntity = null!;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            standingOn = AccessTools.Method("WalkableEntities.MovingFrame:AudioBody") ?? throw new MissingMethodException("WalkableEntities.MovingFrame", "AudioBody");
            bodyEntity = AccessTools.Field("WalkableEntities.PlatformBody:Entity") ?? throw new MissingFieldException("WalkableEntities.PlatformBody", "Entity");
            harmony = new Harmony(HarmonyId);
            harmony.Patch(AccessTools.Method("WalkableEntities.BoatAdapter:GetPose"), postfix: new HarmonyMethod(typeof(WalkableDecks), nameof(AsDrawn)));
            harmony.Patch(AccessTools.Method(typeof(NetworkAPI), nameof(NetworkAPI.SendPlayerPositionPacket)),
                prefix: new HarmonyMethod(typeof(WalkableDecks), nameof(SendOnPlainDeck)),
                postfix: new HarmonyMethod(typeof(WalkableDecks), nameof(RestoreOnDrawnDeck)));
        }
    }

    public static void Detach()
    {
        lock (patchLock)
        {
            if (--attached > 0 || harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }
    }

    private static void AsDrawn(Entity __0, ref object __result)
    {
        if (__0.Api is not ICoreClientAPI || __0.Properties.Client.Renderer is not EntityShapeRenderer renderer || __0.GetBehavior<EntityBehaviorSeaForces>() == null) return;

        var type = __result.GetType();
        var plain = new DeckPose(Read<double>(__result, "X"), Read<double>(__result, "Y"), Read<double>(__result, "Z"), Read<Quaternion>(__result, "Rotation"));
        var drawn = plain.Drawn(__0.SelectionBox.Y2 / 2, renderer.xangle, renderer.yangle, renderer.zangle, VesselMotion.Lift(__0));
        poses.AddOrUpdate(__0, new((plain, drawn)));
        __result = Activator.CreateInstance(type, drawn.X, drawn.Y, drawn.Z, drawn.Rotation)!;
    }

    private static T Read<T>(object pose, string property) => (T)pose.GetType().GetProperty(property)!.GetValue(pose)!;

    private static void SendOnPlainDeck(ClientMain ___game, out (double X, double Y, double Z)? __state)
    {
        __state = null;
        var player = ___game.EntityPlayer;
        if (player == null || player.MountedOn != null || standingOn.Invoke(null, [player]) is not { } body) return;
        if (bodyEntity.GetValue(body) is not Entity vessel || !poses.TryGetValue(vessel, out var deck)) return;

        var pos = player.Pos;
        __state = (pos.X, pos.Y, pos.Z);
        (pos.X, pos.Y, pos.Z) = deck.Value.Plain.ToWorld(deck.Value.Drawn.ToLocal(pos.X, pos.Y, pos.Z));
    }

    private static void RestoreOnDrawnDeck(ClientMain ___game, (double X, double Y, double Z)? __state)
    {
        if (__state is not { } drawn) return;
        var pos = ___game.EntityPlayer.Pos;
        (pos.X, pos.Y, pos.Z) = drawn;
    }
}
