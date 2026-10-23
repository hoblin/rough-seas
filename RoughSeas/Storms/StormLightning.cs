using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;

namespace RoughSeas.Storms;

public class StormLightning(StormsModSystem storms, SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const int SpotTries = 8;

    private WeatherSystemServer weather = null!;
    private SeaClock.Watch watch = null!;

    private LightningConfig Config => storms.Config.Lightning;

    public void Start()
    {
        weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        weather.OnLightningImpactBegin += DrawToMast;
        watch = sea.Clock.Start();
        sapi.Event.RegisterGameTickListener(_ => Strike(), (int)(storms.Config.TickSeconds * 1000));
    }

    private void Strike()
    {
        var lap = watch.Lap();
        if (!sea.DangerousOceans || lap.Leapt) return;

        using var _ = storms.Budget.Measure();
        var rand = sapi.World.Rand;
        var rolledNear = new List<Vec3d>();
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            var pos = player.Entity?.Pos.XYZ;
            if (pos == null || rolledNear.Any(rolled => rolled.HorizontalSquareDistanceTo(pos) < Config.RangeBlocks * Config.RangeBlocks)) continue;

            rolledNear.Add(pos);
            var presence = sea.Model.StormsAt(pos.X, pos.Z, sea.Clock.Hours).Presence;
            if (presence < Config.MinPresence || !WarmEnough(pos.AsBlockPos)) continue;

            for (var strikes = Poisson(Config.StrikesPerMinute * lap.Seconds / 60 * presence, rand); strikes > 0; strikes--)
            {
                if (Spot(pos.X, pos.Z) is not { } spot) continue;

                storms.Log("lightning in a storm at {0:F0}% presence, near {1}, at {2:F0} {3:F0} {4:F0}", presence * 100, player.PlayerName, spot.X, spot.Y, spot.Z);
                weather.SpawnLightningFlash(spot);
            }
        }
    }

    private static int Poisson(double mean, Random rand)
    {
        var strikes = 0;
        for (var waited = -Math.Log(1 - rand.NextDouble()); waited < mean; waited -= Math.Log(1 - rand.NextDouble())) strikes++;
        return strikes;
    }

    private Vec3d? Spot(double x, double z)
    {
        var rand = sapi.World.Rand;
        for (var i = 0; i < SpotTries; i++)
        {
            var angle = rand.NextDouble() * GameMath.TWOPI;
            var distance = Math.Sqrt(rand.NextDouble()) * Config.RangeBlocks;
            var spotX = x + Math.Cos(angle) * distance;
            var spotZ = z + Math.Sin(angle) * distance;
            if (sea.Model.StormsAt(spotX, spotZ, sea.Clock.Hours).Presence < Config.MinPresence) continue;

            var ground = Math.Max(sapi.World.SeaLevel, sapi.World.BlockAccessor.GetRainMapHeightAt((int)spotX, (int)spotZ));
            return new Vec3d(spotX, ground, spotZ);
        }
        return null;
    }

    private bool WarmEnough(BlockPos pos) =>
        sapi.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Temperature >= Config.MinCelsius;

    private void DrawToMast(ref Vec3d impactPos, ref EnumHandling handling)
    {
        if (!sea.DangerousOceans) return;

        var reach = (float)(Config.AttractBlocksPerMastMetre * Config.TallestMastMetres);
        var impact = impactPos;
        var mast = sapi.World.GetEntitiesAround(impact, reach, reach, Masted)
            .Select(vessel => (vessel, distance: vessel.Pos.HorDistanceTo(impact)))
            .Where(near => near.distance <= Attraction(near.vessel))
            .OrderBy(near => near.distance)
            .Select(near => near.vessel)
            .FirstOrDefault();
        if (mast == null) return;

        impactPos = mast.Pos.XYZ.Add(0, mast.GetBehavior<EntityBehaviorSeaForces>()!.MastMetres, 0);
        storms.Log(mast, "its mast draws lightning from {0:F0} blocks away", mast.Pos.HorDistanceTo(impact));
    }

    private static bool Masted(Entity entity) => entity.GetBehavior<EntityBehaviorSeaForces>()?.MastMetres > 0;

    private double Attraction(Entity vessel) => vessel.GetBehavior<EntityBehaviorSeaForces>()!.MastMetres * Config.AttractBlocksPerMastMetre;
}
