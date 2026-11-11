using System;
using System.Collections.Generic;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace RoughSeas.Person;

/// <summary>
/// Decides on the server when a crest breaks over a swimmer, by the vessels' own rule (<see cref="PassingWaves"/>) on the
/// same sea its client draws (<see cref="RegionSwells"/>). The client that moves the body is told, since a player's
/// movement is simulated there.
/// </summary>
public sealed class SwimmerBreakers(PersonModSystem mod, StormsModSystem storms, SeaWeatherSystem sea, ICoreServerAPI sapi, IServerNetworkChannel channel)
{
    private readonly Dictionary<string, PassingWaves> swimmers = [];

    public void Start()
    {
        sapi.Event.RegisterGameTickListener(Tick, (int)(storms.Config.TickSeconds * 1000));
        sapi.Event.PlayerDisconnect += player => swimmers.Remove(player.PlayerUID);
    }

    private void Tick(float deltaTime)
    {
        if (storms.Swells is not { } swells) return;
        var on = Mechanic.SwimmerBreakers.On(sapi.World);
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            if (player is not IServerPlayer { Entity: { } entity } swimmer) continue;
            if (!swimmers.TryGetValue(player.PlayerUID, out var passing))
            {
                swimmers[player.PlayerUID] = passing = new PassingWaves(sea.Clock, storms.Waves, storms.Config.Waves.MinHeight);
            }
            var pos = entity.Pos;
            var inWater = on && entity.Alive && entity.MountedOn == null && !entity.Controls.IsFlying && pos.Dimension == Dimensions.NormalWorld
                && (entity.Swimming || entity.FeetInLiquid);
            if (!passing.Lap(inWater)) continue;

            using var _ = storms.Budget.Measure();
            var here = swells.At(pos.X, pos.Z).WaveHere(pos.X, pos.Z);
            var windMps = sea.At(pos.X, pos.Z).WindSpeed;
            foreach (var wave in passing.Over(here, windMps, entity.World.Rand))
            {
                var cause = wave.Surf ? "surf" : wave.Breaks ? "whitecap" : "swell";
                if (!wave.Breaks)
                {
                    mod.Log(entity, "server: a {0:F1} m wave in a {1:F1} m sea passes the swimmer unbroken", wave.Height, here.Height);
                    continue;
                }
                mod.Log(entity, "server: a {0:F1} m {1} crest in a {2:F1} m sea breaks over the swimmer ({3:F1} m of water), at {4:F1} m/s toward {5:F0}°",
                    wave.Height, cause, here.Height, here.Depth, wave.Celerity, (here.TowardRadians * 180 / Math.PI + 360) % 360);
                channel.SendPacket(new BreakerPacket { HeightMetres = wave.Height, PeriodSeconds = here.Period, Celerity = wave.Celerity, TowardRadians = here.TowardRadians }, swimmer);
            }
        }
    }
}
