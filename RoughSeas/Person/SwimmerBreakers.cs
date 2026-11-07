using System;
using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace RoughSeas.Person;

/// <summary>
/// Decides on the server when a crest breaks over a swimmer, as it decides for a vessel: from the same sea its client draws
/// (<see cref="RegionSwells"/>), by one roll of the server's own. The server owns what the world does, and whatever later
/// answers a breaker there (stamina, a sound for the others) reads one outcome. The client that moves the body is told, since
/// a player's movement is simulated there.
/// </summary>
public sealed class SwimmerBreakers(PersonModSystem mod, StormsModSystem storms, SeaWeatherSystem sea, ICoreServerAPI sapi, IServerNetworkChannel channel)
{
    private const int TickMilliseconds = 100;

    private readonly Dictionary<string, Watcher> watchers = [];

    public void Start()
    {
        sapi.Event.RegisterGameTickListener(Tick, TickMilliseconds);
        sapi.Event.PlayerDisconnect += player => watchers.Remove(player.PlayerUID);
    }

    private void Tick(float deltaTime)
    {
        if (storms.Swells is not { } swells) return;
        using var _ = storms.Budget.Measure();
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            if (player is not IServerPlayer { Entity: { } entity } swimmer) continue;
            var pos = entity.Pos;
            if (!sea.DangerousOceans || !entity.Alive || entity.MountedOn != null || entity.Controls.IsFlying || pos.Dimension != Dimensions.NormalWorld
                || !(entity.Swimming || entity.FeetInLiquid))
            {
                watchers.Remove(player.PlayerUID);
                continue;
            }

            if (!watchers.TryGetValue(player.PlayerUID, out var watcher))
            {
                watchers[player.PlayerUID] = watcher = new Watcher(new CrestRoll(storms.Waves, storms.Config.Waves), sea.Clock.StartWatch());
            }
            var around = swells.Around(pos.X, pos.Z);
            var lived = watcher.Clock.Lap().LivedSeconds;
            if (!around.Sea.SwellAt(pos.X, pos.Z))
            {
                watcher.Roll.Forget();
                continue;
            }
            var state = sea.At(pos.X, pos.Z);
            var depth = WaterDepth.AtColumn(entity.World.BlockAccessor, pos.XYZ, storms.Config.Waves.MaxDepthBlocks);
            if (watcher.Roll.See(around.Sea.HeightAt(pos.X, pos.Z), lived, state, depth, entity.World.Rand.NextDouble) is not { } crest) continue;

            var toward = around.Sea.WavesTowardRadians;
            mod.Log(entity, "server: a {0:F1} m crest breaks over the swimmer ({1}, wind {2:F1} m/s, {3:P1} of waves break, {4:F1} m of water), at {5:F1} m/s toward {6:F0}°",
                crest.HeightMetres, crest.Surf ? "surf" : "at sea", state.WindSpeed, crest.Chance, depth, crest.Celerity, (toward * 180 / Math.PI + 360) % 360);
            channel.SendPacket(new BreakerPacket { HeightMetres = crest.HeightMetres, PeriodSeconds = state.WavePeriod, Celerity = crest.Celerity, TowardRadians = toward }, swimmer);
        }
    }

    private readonly record struct Watcher(CrestRoll Roll, SeaClock.Watch Clock);
}
