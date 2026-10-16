using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public sealed class AshoreRoll(StormsModSystem storms, ISeaWeather sea, Entity vessel, double sleptSince, double reach)
{
    private AshoreConfig Config => storms.Config.Ashore;

    public bool AreaLoaded()
    {
        var accessor = vessel.World.BlockAccessor;
        var pos = vessel.Pos.AsBlockPos;
        var size = GlobalConstants.ChunkSize;
        var radius = Config.ShoreSearchBlocks;
        for (var chunkX = (pos.X - radius) / size; chunkX <= (pos.X + radius) / size; chunkX++)
        {
            for (var chunkZ = (pos.Z - radius) / size; chunkZ <= (pos.Z + radius) / size; chunkZ++)
            {
                if (accessor.GetChunk(chunkX, pos.Y / size, chunkZ) == null) return false;
            }
        }
        return true;
    }

    public void Roll(EntityBehaviorSeaForces forces, EntityBehaviorVesselIntegrity? hull)
    {
        using var _ = storms.Budget.Measure();
        var slept = sea.NowHours - sleptSince;
        var shore = ShoreDistance();
        if (double.IsPositiveInfinity(shore))
        {
            storms.Log(vessel, "slept {0:F0} h moored with no shore within {1} blocks", slept, Config.ShoreSearchBlocks);
            return;
        }

        var hoursPerSample = Math.Max(1, slept / Config.MaxSamples);
        var heights = Heights(hoursPerSample);
        var openWaterToShore = shore - reach;
        var chance = new ShoreHazard(Config).Chance(heights, hoursPerSample, openWaterToShore);
        var thrown = vessel.World.Rand.NextDouble() < chance;
        var highest = heights.DefaultIfEmpty(0).Max();
        if (thrown) hull?.Strike(forces.BreakerEnergy(highest) * Config.ImpactFactor);
        storms.Log(vessel, "slept {0:F0} h moored, {1:F0} blocks of open water to the shore, highest waves {2:F1} m: ashore chance {3:F3}, thrown {4}, hull now {5:F2}",
            slept, openWaterToShore, highest, chance, thrown, hull?.Integrity ?? 1);
    }

    private List<double> Heights(double hoursPerSample)
    {
        var heights = new List<double>();
        for (var hours = sleptSince; hours < sea.NowHours; hours += hoursPerSample)
        {
            heights.Add(sea.At(vessel.Pos.X, vessel.Pos.Z, hours).WaveHeight);
        }
        return heights;
    }

    private double ShoreDistance()
    {
        var accessor = vessel.World.BlockAccessor;
        var centre = vessel.Pos.AsBlockPos;
        var probe = new BlockPos(centre.dimension);
        for (var radius = 0; radius <= Config.ShoreSearchBlocks; radius++)
        {
            foreach (var (dx, dz) in Ring(radius))
            {
                probe.Set(centre.X + dx, centre.Y, centre.Z + dz);
                if (!accessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() && accessor.GetBlock(probe).SideSolid.Any) return radius;
            }
        }
        return double.PositiveInfinity;
    }

    private static IEnumerable<(int X, int Z)> Ring(int radius)
    {
        if (radius == 0)
        {
            yield return (0, 0);
            yield break;
        }
        for (var along = -radius; along <= radius; along++)
        {
            yield return (along, -radius);
            yield return (along, radius);
        }
        for (var along = 1 - radius; along < radius; along++)
        {
            yield return (-radius, along);
            yield return (radius, along);
        }
    }
}
