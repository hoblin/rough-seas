using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using RoughSeas.SeaAndWeather;
using Vintagestory.API.Common;

namespace RoughSeas.Water;

/// <summary>Reads the seabed around a point again, off the main thread, once the point moves, the waves change or the sea is due a fresh look; hands over each field once it is built.</summary>
public sealed class SurfMaker(SurfBuilder builder, Beds beds, ShoreSurfConfig config, int highestRise, Action<string, object[]> log)
{
    private const int SeaStep = 8;
    private const int RegionsAcross = 3;
    private const int CoarseBlocks = 32;

    private Task<SurfField>? building;
    private (double X, double Z)[] vectors = [];
    private double builtSeconds = double.NegativeInfinity;
    private int builtDominant;

    public SurfField? Field { get; private set; }

    public SurfField? Update(IWorldAccessor world, ISeaSource sea, double x, double z, (double X, double Z)[] numbers, double[] amplitudeShares, int dominant, double seconds)
    {
        if (building is { IsCompleted: true } done)
        {
            building = null;
            if (done.IsCompletedSuccessfully) return Field = done.Result;
            log("surf field failed: {0}", [done.Exception?.GetBaseException().ToString() ?? "cancelled"]);
        }
        if (building != null || !Due(x, z, numbers, dominant, seconds)) return null;

        var size = config.MapBlocks;
        var (originX, originZ) = ((int)Math.Floor(x / config.MoveBlocks) * config.MoveBlocks - size / 2, (int)Math.Floor(z / config.MoveBlocks) * config.MoveBlocks - size / 2);
        var across = size / SeaStep + 1;
        var seas = new Sea[across * across];
        for (var i = 0; i < seas.Length; i++)
        {
            var sample = sea.Sample(originX + i % across * SeaStep, originZ + i / across * SeaStep);
            seas[i] = new Sea(sample.WaveHeight, sample.WavePeriod);
        }
        Sea SeaAt(int atX, int atZ) => seas[Math.Clamp((atZ - originZ) / SeaStep, 0, across - 1) * across + Math.Clamp((atX - originX) / SeaStep, 0, across - 1)];

        (vectors, builtSeconds, builtDominant) = (numbers, seconds, dominant);
        var coarse = Coarse(sea, x, z, world.BlockAccessor.RegionSize);
        var blocks = world.GetLockFreeBlockAccessor();
        var heights = SeabedMap.Heights(world.BlockAccessor, originX, originZ, size);
        building = Task.Run(() =>
        {
            var started = Stopwatch.StartNew();
            var map = SeabedMap.Read(blocks, heights, originX, originZ, size, beds, highestRise, config.DeepestBlocks);
            var read = started.ElapsedMilliseconds;
            var field = builder.Build(map, SeaAt, numbers, amplitudeShares, dominant, coarse);
            field.EdgeFadeBlocks = config.EdgeFadeBlocks;
            log("surf field {0} {1}, {2} blocks across: seabed read in {3} ms, built in {4} ms; {5} faces the sea meets, {6} steps stop a sheet",
                [originX, originZ, size, read, started.ElapsedMilliseconds - read, field.Walls.Count, field.Steps.Count]);
            return field;
        });
        return null;
    }

    private static CoarseBed Coarse(ISeaSource sea, double x, double z, int regionSize)
    {
        var (originX, originZ) = (((int)Math.Floor(x / regionSize) - 1) * regionSize, ((int)Math.Floor(z / regionSize) - 1) * regionSize);
        var across = RegionsAcross * regionSize / CoarseBlocks;
        var samples = Enumerable.Range(0, across * across).Select(i => sea.Sample(originX + (i % across + 0.5) * CoarseBlocks, originZ + (i / across + 0.5) * CoarseBlocks)).ToArray();
        return new CoarseBed(originX, originZ, CoarseBlocks, across, samples.Select(sample => (float)sample.Depth).ToArray(), samples.Select(sample => sample.WaveHeight).ToArray());
    }

    private bool Due(double x, double z, (double X, double Z)[] numbers, int dominant, double seconds)
    {
        if (Field is not { } field) return true;
        var centre = (field.OriginX + field.Size / 2.0, field.OriginZ + field.Size / 2.0);
        return Math.Abs(x - centre.Item1) > config.MoveBlocks || Math.Abs(z - centre.Item2) > config.MoveBlocks
            || !numbers.SequenceEqual(vectors) || dominant != builtDominant || seconds - builtSeconds > config.RefreshSeconds;
    }
}

public class ShoreSurfConfig
{
    public int MapBlocks = 256;
    public int MoveBlocks = 32;
    public int EdgeFadeBlocks = 32;
    public double BreakingDissipation = 1;
    public double BoreFrontSlope = 0.21;
    public double BoreCelerityShare = 0.45;
    public int DeepestBlocks = 32;
    public double RefreshSeconds = 20;
    public double WetReflectance = 0.5;
    public double WaterAttenuation = 1;
}
