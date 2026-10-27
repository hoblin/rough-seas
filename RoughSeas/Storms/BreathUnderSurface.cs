using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoughSeas.Storms;

/// <summary>
/// Lets a player's breath follow the drawn sea: out of air under a crest, breathing in a trough. Vanilla's
/// <c>EntityBehaviorBreathe.Check</c> judges the eye, once a second on the server, by the block it is in; while a player
/// is in the water, the server builds the sea their client draws around them (<see cref="SwellAround"/>) and judges the
/// eye against it (<see cref="DrawnBreath"/>). Vanilla's oxygen numbers stand. Players out of the water or aboard a
/// vessel, suffocation in solid blocks and every other entity stay vanilla's. On the server alone.
/// </summary>
public static class BreathUnderSurface
{
    private const string HarmonyId = "roughseas.storms.breath";

    private static readonly Dictionary<string, SwellAround> swells = [];
    private static readonly Dictionary<string, BreathVerdict> verdicts = [];
    private static Harmony? harmony;
    private static StormsModSystem? system;
    private static SeaWeatherSystem? sea;
    private static ICoreServerAPI? sapi;

    public static void Attach(StormsModSystem storms, SeaWeatherSystem seaWeather, ICoreServerAPI api)
    {
        if (harmony != null) return;
        (system, sea, sapi) = (storms, seaWeather, api);
        api.Event.PlayerDisconnect += Forget;
        harmony = new Harmony(HarmonyId);
        harmony.Patch(AccessTools.Method(typeof(EntityBehaviorBreathe), nameof(EntityBehaviorBreathe.Check)),
            postfix: new HarmonyMethod(typeof(BreathUnderSurface), nameof(Check)) { priority = Priority.High });
    }

    public static void Detach()
    {
        harmony?.UnpatchAll(HarmonyId);
        harmony = null;
        if (sapi != null) sapi.Event.PlayerDisconnect -= Forget;
        (system, sea, sapi) = (null, null, null);
        swells.Clear();
        verdicts.Clear();
    }

    private static void Forget(IServerPlayer player)
    {
        swells.Remove(player.PlayerUID);
        verdicts.Remove(player.PlayerUID);
    }

    private static void Check(EntityBehaviorBreathe __instance)
    {
        // in a single player game the client's Check runs through here too
        if (sapi == null || sea == null || system == null || __instance.entity is not EntityPlayer entity || entity.World.Side != EnumAppSide.Server) return;
        if (sapi.World.PlayerByUid(entity.PlayerUID) is not IServerPlayer player) return;
        // vanilla gives these air before it looks at the eye
        if (player.WorldData.CurrentGameMode is EnumGameMode.Creative or EnumGameMode.Spectator) return;

        using var _ = system.Budget.Measure();
        var pos = entity.Pos;
        var (x, z) = (pos.X + entity.LocalEyePos.X, pos.Z + entity.LocalEyePos.Z);
        var eyeY = pos.Y + (entity.Swimming ? entity.Properties.SwimmingEyeHeight : entity.Properties.EyeHeight);
        var vanilla = __instance.HasAir;
        var swell = double.NaN;
        BreathVerdict verdict;
        // the drawn water is read from blocks of the main dimension
        if (pos.Dimension != 0 || !(entity.Swimming || entity.FeetInLiquid))
        {
            verdict = BreathVerdict.Dry;
            swells.Remove(player.PlayerUID);
        }
        // a rider's position here is the vessel's at still water; their client lifts both onto the drawn sea
        else if (entity.MountedOn?.Entity is EntityBoat) verdict = BreathVerdict.Aboard;
        else
        {
            if (!swells.TryGetValue(player.PlayerUID, out var around))
            {
                swells[player.PlayerUID] = around = new SwellAround(system.Config.Surface, system.Waves, sea.Config, sea.Clock);
            }
            around.At(sea.Map, x, z, sea.Clock.Seconds, sapi.WorldManager.RegionSize);
            swell = around.Sea.HeightAt(x, z);
            verdict = DrawnBreath.Judge(around.Water, new BlockColumn(sapi.World.BlockAccessor, (int)Math.Floor(x), (int)Math.Floor(z)), x, eyeY, z);
        }
        if (DrawnBreath.HasAir(verdict) is { } air) __instance.HasAir = air;
        Decided(player, verdict, x, eyeY, z, swell, vanilla, __instance.HasAir);
    }

    private static void Decided(IServerPlayer player, BreathVerdict verdict, double x, double eyeY, double z, double swell, bool vanilla, bool hasAir)
    {
        if (verdicts.TryGetValue(player.PlayerUID, out var last) && last == verdict) return;
        verdicts[player.PlayerUID] = verdict;
        system?.Log("breath of {0}, eye at {1:0} {2:0.00} {3:0}: {4}, {5} (vanilla's: {6})", player.PlayerName, x, eyeY, z,
            Describe(verdict, swell), hasAir ? "has air" : "out of air", vanilla ? "has air" : "out of air");
    }

    private static string Describe(BreathVerdict verdict, double swell) => verdict switch
    {
        BreathVerdict.Dry => "not in the water, by the blocks",
        BreathVerdict.Aboard => "aboard a vessel riding the drawn sea, by the blocks",
        BreathVerdict.NoSurface => string.Format(CultureInfo.InvariantCulture, "no drawn surface over the head (swell {0:0.00} m), by the blocks", swell),
        BreathVerdict.Above => string.Format(CultureInfo.InvariantCulture, "above the drawn surface (swell {0:0.00} m), by the blocks", swell),
        BreathVerdict.Under => string.Format(CultureInfo.InvariantCulture, "under the drawn surface (swell {0:0.00} m)", swell),
        BreathVerdict.Bared => string.Format(CultureInfo.InvariantCulture, "bared by a trough (swell {0:0.00} m)", swell),
        _ => verdict.ToString(),
    };
}
