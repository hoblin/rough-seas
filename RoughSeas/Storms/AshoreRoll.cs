using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using RoughSeas.SeaAndWeather;
using RoughSeas.Vessels;

namespace RoughSeas.Storms;

public sealed class AshoreRoll
{
    private readonly StormsModSystem storms;
    private readonly ISeaWeather sea;
    private readonly Entity vessel;
    private readonly double mooredX, mooredZ;
    private readonly double reach;
    private readonly double slept;
    private readonly double hoursPerSample;
    private readonly List<double> heights = [];
    private readonly int searchBlocks;

    public AshoreRoll(StormsModSystem storms, ISeaWeather sea, Entity vessel, double mooredX, double mooredZ, double sleptSince, double reach)
    {
        using var _ = storms.Budget.Measure();
        this.storms = storms;
        this.sea = sea;
        this.vessel = vessel;
        this.mooredX = mooredX;
        this.mooredZ = mooredZ;
        this.reach = reach;
        slept = sea.NowHours - sleptSince;
        hoursPerSample = Math.Max(1, slept / Config.MaxSamples);
        for (var hours = sleptSince; hours < sea.NowHours; hours += hoursPerSample)
        {
            heights.Add(sea.At(vessel.Pos.X, vessel.Pos.Z, hours).WaveHeight);
        }
        searchBlocks = (int)Math.Min(Config.ShoreSearchBlocks, Math.Ceiling(reach + Config.SurfReachPerMetre * Highest));
    }

    private AshoreConfig Config => storms.Config.Ashore;

    private double Highest => heights.DefaultIfEmpty(0).Max();

    public void Roll(EntityBehaviorSeaForces forces, EntityBehaviorVesselIntegrity? hull)
    {
        using var _ = storms.Budget.Measure();
        var shore = ShoreDistance();
        if (double.IsPositiveInfinity(shore))
        {
            storms.Log(vessel, "slept {0:F0} h moored with no shore within {1} blocks of its swing and the surf", slept, searchBlocks);
            return;
        }

        var openWaterToShore = shore - reach;
        var chance = new ShoreHazard(Config).Chance(heights, hoursPerSample, openWaterToShore);
        var thrown = vessel.World.Rand.NextDouble() < chance;
        if (thrown) hull?.Strike(forces.BreakerEnergy(Highest) * Config.ImpactFactor);
        storms.Log(vessel, "slept {0:F0} h moored, {1:F0} blocks of open water to the shore, highest waves {2:F1} m: ashore chance {3:F3}, thrown {4}, hull now {5:F2}",
            slept, openWaterToShore, Highest, chance, thrown, hull?.Integrity ?? 1);
    }

    private double ShoreDistance()
    {
        var surface = vessel.Pos.AsBlockPos;
        var probe = new BlockPos(surface.dimension);
        var (centreX, centreZ) = ((int)Math.Floor(mooredX), (int)Math.Floor(mooredZ));
        for (var radius = 0; radius <= searchBlocks; radius++)
        {
            foreach (var (dx, dz) in Ring(radius))
            {
                probe.Set(centreX + dx, surface.Y, centreZ + dz);
                if (Land(probe)) return radius;
            }
        }
        return double.PositiveInfinity;
    }

    private bool Land(BlockPos probe)
    {
        var accessor = vessel.World.BlockAccessor;
        if (accessor.GetChunkAtBlockPos(probe) == null) return !sea.IsSea(probe.X, probe.Z);

        return !accessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() && accessor.GetBlock(probe).SideSolid.Any;
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
