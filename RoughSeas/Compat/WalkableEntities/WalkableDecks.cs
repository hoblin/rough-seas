using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RoughSeas.Storms;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;

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
    private static MethodInfo deckUnderPlayer = null!;
    private static FieldInfo deckVessel = null!;
    private static PropertyInfo[] poseParts = null!;
    private static ConstructorInfo newPose = null!;

    public static void Attach()
    {
        lock (patchLock)
        {
            if (attached++ > 0) return;
            var getPose = AccessTools.Method("WalkableEntities.BoatAdapter:GetPose") ?? throw new MissingMethodException("WalkableEntities.BoatAdapter", "GetPose");
            var pose = getPose.ReturnType;
            poseParts = [.. new[] { "X", "Y", "Z", "Rotation" }.Select(name => pose.GetProperty(name) ?? throw new MissingMemberException(pose.FullName, name))];
            newPose = pose.GetConstructor([typeof(double), typeof(double), typeof(double), typeof(Quaternion)]) ?? throw new MissingMethodException(pose.FullName, ".ctor");
            // MovingFrame.AudioBody: the platform the local player stands on, judged by its pose; its name is for the sounds it first served.
            deckUnderPlayer = AccessTools.Method("WalkableEntities.MovingFrame:AudioBody") ?? throw new MissingMethodException("WalkableEntities.MovingFrame", "AudioBody");
            deckVessel = AccessTools.Field("WalkableEntities.PlatformBody:Entity") ?? throw new MissingFieldException("WalkableEntities.PlatformBody", "Entity");
            harmony = new Harmony(HarmonyId);
            harmony.Patch(getPose, postfix: new HarmonyMethod(typeof(WalkableDecks), nameof(AsDrawn)));
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
        if (VesselMotion.Drawn(__0) is not { } hull) return;

        var plain = new DeckPose((double)poseParts[0].GetValue(__result)!, (double)poseParts[1].GetValue(__result)!, (double)poseParts[2].GetValue(__result)!,
            (Quaternion)poseParts[3].GetValue(__result)!);
        var drawn = plain.Drawn(__0.SelectionBox.Y2 / 2, hull.XAngle, hull.YAngle, hull.ZAngle, hull.Lift);
        poses.AddOrUpdate(__0, new((plain, drawn)));
        __result = newPose.Invoke([drawn.X, drawn.Y, drawn.Z, drawn.Rotation]);
    }

    private static void SendOnPlainDeck(ClientMain ___game, out (double X, double Y, double Z)? __state)
    {
        __state = null;
        var player = ___game.EntityPlayer;
        if (player == null || player.MountedOn != null || deckUnderPlayer.Invoke(null, [player]) is not { } body) return;
        if (deckVessel.GetValue(body) is not Entity vessel || !poses.TryGetValue(vessel, out var deck)) return;

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
