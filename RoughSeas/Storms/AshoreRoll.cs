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
    private readonly ISeaModel sea;
    private readonly Entity vessel;
    private readonly double mooredX, mooredZ;
    private readonly double reach;
    private readonly double slept;
    private readonly double hoursPerSample;
    private readonly List<SeaState> states = [];
    private readonly int searchBlocks;

    public AshoreRoll(StormsModSystem storms, ISeaModel sea, Entity vessel, double mooredX, double mooredZ, double sleptSince, double reach)
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
            states.Add(sea.At(vessel.Pos.X, vessel.Pos.Z, hours));
        }
        searchBlocks = (int)Math.Min(Config.ShoreSearchBlocks, Math.Ceiling(reach + Config.SurfReachPerMetre * Highest));
    }

    private AshoreConfig Config => storms.Config.Ashore;

    private SeaState HighestSea => states.MaxBy(state => state.WaveHeight);

    private double Highest => HighestSea.WaveHeight;

    public void Roll(EntityBehaviorVesselIntegrity? hull)
    {
        using var _ = storms.Budget.Measure();
        var shore = ShoreDistance();
        if (double.IsPositiveInfinity(shore))
        {
            storms.Log(vessel, "slept {0:F0} h moored with no shore within {1} blocks of its swing and the surf", slept, searchBlocks);
            return;
        }

        var openWaterToShore = shore - reach;
        var chance = new ShoreHazard(Config).Chance(states.Select(state => state.WaveHeight), hoursPerSample, openWaterToShore);
        var thrown = vessel.World.Rand.NextDouble() < chance;
        var surge = thrown ? storms.Waves.BreakerCelerity(Highest, HighestSea.WavePeriod, storms.Waves.BreakingDepth(Highest)) : 0;
        storms.Log(vessel, "slept {0:F0} h moored, {1:F0} blocks of open water to the shore, highest waves {2:F1} m: ashore chance {3:F3}, thrown {4} at {5:F1} m/s",
            slept, openWaterToShore, Highest, chance, thrown, surge);
        if (thrown && hull != null) hull.Strike(hull.JoulesAt(surge), HullPlace.Bottom);
    }

    private double ShoreDistance()
    {
        var surface = vessel.Pos.AsBlockPos;
        var probe = new BlockPos(surface.dimension);
        var (centreX, centreZ) = ((int)Math.Floor(mooredX), (int)Math.Floor(mooredZ));
        var nearest = double.PositiveInfinity;
        for (var radius = 0; radius <= searchBlocks && radius <= nearest; radius++)
        {
            var stride = radius <= Config.FineSearchBlocks ? 1 : Config.CoarseSearchStride;
            if (radius % stride != 0) continue;

            foreach (var (dx, dz) in Ring(radius, stride))
            {
                probe.Set(centreX + dx, surface.Y, centreZ + dz);
                if (Land(probe)) nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dz * dz));
            }
        }
        return nearest;
    }

    private bool Land(BlockPos probe)
    {
        var accessor = vessel.World.BlockAccessor;
        if (accessor.GetChunkAtBlockPos(probe) == null) return !sea.IsSea(probe.X, probe.Z);

        return !accessor.GetBlock(probe, BlockLayersAccess.Fluid).IsLiquid() && accessor.GetBlock(probe).SideSolid.Any;
    }

    private static IEnumerable<(int X, int Z)> Ring(int radius, int stride)
    {
        if (radius == 0)
        {
            yield return (0, 0);
            yield break;
        }
        for (var along = -radius; along <= radius; along += stride)
        {
            yield return (along, -radius);
            yield return (along, radius);
        }
        for (var along = stride - radius; along < radius; along += stride)
        {
            yield return (-radius, along);
            yield return (radius, along);
        }
    }
}
