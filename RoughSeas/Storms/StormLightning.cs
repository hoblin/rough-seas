using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public class StormLightning(StormsModSystem storms, SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const int SpotTries = 8;

    private WeatherSystemServer weather = null!;

    private LightningConfig Config => storms.Config.Lightning;

    public void Start()
    {
        weather = sapi.ModLoader.GetModSystem<WeatherSystemServer>();
        weather.OnLightningImpactBegin += OnImpact;
        sapi.Event.RegisterGameTickListener(_ => Strike(), (int)(Config.CheckSeconds * 1000));
    }

    private void Strike()
    {
        using var _ = sea.Budget.Measure();
        var rand = sapi.World.Rand;
        foreach (var player in sapi.World.AllOnlinePlayers)
        {
            var pos = player.Entity?.Pos;
            if (pos == null) continue;

            var presence = sea.StormsAt(pos.X, pos.Z, sea.NowHours).Presence;
            var chance = Config.StrikesPerMinute * Config.CheckSeconds / 60 * presence;
            if (presence < Config.MinPresence || rand.NextDouble() >= chance || !WarmEnough(pos.AsBlockPos)) continue;

            if (Spot(pos.X, pos.Z) is not { } spot) continue;

            storms.Log("lightning in a storm at {0:F0}% presence, near {1}, at {2:F0} {3:F0} {4:F0}", presence * 100, player.PlayerName, spot.X, spot.Y, spot.Z);
            weather.SpawnLightningFlash(spot);
        }
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
            if (sea.StormsAt(spotX, spotZ, sea.NowHours).Presence < Config.MinPresence) continue;

            return new Vec3d(spotX, sapi.World.BlockAccessor.GetRainMapHeightAt((int)spotX, (int)spotZ), spotZ);
        }
        return null;
    }

    private bool WarmEnough(BlockPos pos) =>
        sapi.World.BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues)?.Temperature >= Config.MinCelsius;

    private void OnImpact(ref Vec3d impactPos, ref EnumHandling handling)
    {
        if (!sea.DangerousOceans) return;

        var reach = MaxAttraction();
        var impact = impactPos;
        var mast = sapi.World.GetEntitiesAround(impact, (float)reach, (float)reach, Masted)
            .Select(vessel => (vessel, distance: vessel.Pos.HorDistanceTo(impact)))
            .Where(near => near.distance <= Attraction(near.vessel))
            .OrderBy(near => near.distance)
            .Select(near => near.vessel)
            .FirstOrDefault();
        if (mast != null)
        {
            impactPos = mast.Pos.XYZ.Add(0, mast.GetBehavior<EntityBehaviorSeaForces>()!.MastMetres, 0);
            storms.Log(mast, "its mast draws lightning from {0:F0} blocks away", mast.Pos.HorDistanceTo(impact));
        }

        var radius = (float)Config.StrikeRadiusBlocks;
        foreach (var vessel in sapi.World.GetEntitiesAround(impactPos, radius, radius + (float)reach, e => e.HasBehavior<EntityBehaviorVesselIntegrity>()))
        {
            var hull = vessel.GetBehavior<EntityBehaviorVesselIntegrity>()!;
            hull.Strike(Config.StrikeJoules);
            storms.Log(vessel, "struck by lightning, {0:F0} J, hull now {1:F2}", Config.StrikeJoules, hull.Integrity);
        }
    }

    private static bool Masted(Entity entity) => entity.GetBehavior<EntityBehaviorSeaForces>()?.MastMetres > 0;

    private double Attraction(Entity vessel) => vessel.GetBehavior<EntityBehaviorSeaForces>()!.MastMetres * Config.AttractBlocksPerMastMetre;

    private double MaxAttraction() => Config.AttractBlocksPerMastMetre * Config.TallestMastMetres;
}
