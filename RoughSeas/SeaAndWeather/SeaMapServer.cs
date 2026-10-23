using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

public class SeaMapServer(SeaWeatherSystem sea, ICoreServerAPI sapi)
{
    private const int TickMilliseconds = 250;

    private static readonly (double X, double Z)[] CellSamples = [(0.5, 0.5), (0.25, 0.25), (0.75, 0.25), (0.25, 0.75), (0.75, 0.75)];

    private readonly Dictionary<(int X, int Z), DateTime> sent = [];
    private IServerNetworkChannel channel = null!;

    private SeaMapConfig Config => sea.Config.Map;

    private SeaModel Model => sea.Model;

    public void Start()
    {
        channel = sapi.Network.GetChannel(SeaWeatherSystem.Channel);
        sapi.Event.RegisterGameTickListener(_ => Send(), TickMilliseconds);
    }

    public SeaMapSnapshot Snapshot(int regionX, int regionZ)
    {
        var regionSize = sapi.WorldManager.RegionSize;
        var side = regionSize / Config.CellBlocks;
        var samples = new SeaSample[side * side];
        for (var cellZ = 0; cellZ < side; cellZ++)
        for (var cellX = 0; cellX < side; cellX++)
        {
            samples[cellZ * side + cellX] = Cell(regionX * regionSize + cellX * Config.CellBlocks, regionZ * regionSize + cellZ * Config.CellBlocks);
        }
        var now = sea.Clock.Hours;
        var firstSlot = (long)Math.Floor(now / sea.Config.SeaSampleHours) - 1;
        var wettest = Enumerable.Range(0, samples.Length).MaxBy(i => samples[i].WaveHeight);
        var (wetX, wetZ) = (regionX * regionSize + wettest % side * Config.CellBlocks, regionZ * regionSize + wettest / side * Config.CellBlocks);
        var water = samples[wettest].WaveHeight > 0 ? CellSamples.Select(at => (wetX + at.X * Config.CellBlocks, wetZ + at.Z * Config.CellBlocks)).ToArray() : [];
        var generations = Enumerable.Range(0, 3).Select(i => Model.GenerationOf(regionX, regionZ, firstSlot + i, water)).ToArray();
        return SeaMapSnapshot.Of(regionX, regionZ, now, Model.RegionMps(regionX, regionZ), side, samples, firstSlot, generations);
    }

    private SeaSample Cell(double x, double z)
    {
        sea.Calibrate(sapi, (int)(x + 0.5 * Config.CellBlocks), (int)(z + 0.5 * Config.CellBlocks));
        var centre = Model.Sample(x + 0.5 * Config.CellBlocks, z + 0.5 * Config.CellBlocks);
        if (centre.WaveHeight > 0) return centre;
        var (height, period) = CellSamples.Skip(1)
            .Select(at => Model.WavesAt(x + at.X * Config.CellBlocks, z + at.Z * Config.CellBlocks))
            .FirstOrDefault(waves => waves.Height > 0);
        return height > 0 ? centre with { WaveHeight = height, WavePeriod = period } : centre;
    }

    private void Send()
    {
        using var _ = sea.Budget.Measure();
        var players = sapi.World.AllOnlinePlayers.OfType<IServerPlayer>().Where(player => player.Entity != null).ToArray();
        var wanted = players.SelectMany(player => Around(player)).ToHashSet();
        foreach (var gone in sent.Keys.Where(region => !wanted.Contains(region)).ToArray()) sent.Remove(gone);

        var due = DateTime.UtcNow - TimeSpan.FromSeconds(Config.UpdateSeconds);
        var budget = Stopwatch.StartNew();
        foreach (var region in wanted.Where(region => !sent.TryGetValue(region, out var at) || at < due).OrderBy(region => sent.GetValueOrDefault(region)))
        {
            if (budget.ElapsedMilliseconds > Config.TickBudgetMilliseconds) break;
            var mapRegion = sapi.WorldManager.GetMapRegion(region.X, region.Z);
            if (mapRegion == null) continue;
            var snapshot = Snapshot(region.X, region.Z);
            var data = SerializerUtil.Serialize(snapshot);
            var near = players.Where(player => Around(player).Contains(region)).ToArray();
            if (near.Length > 0) channel.SendPacket(snapshot, data, near);
            mapRegion.SetModdata(SeaMapSnapshot.ModdataKey, data);
            sent[region] = DateTime.UtcNow;
        }
    }

    private IEnumerable<(int X, int Z)> Around(IServerPlayer player)
    {
        var regionSize = sapi.WorldManager.RegionSize;
        var regionX = (int)player.Entity.Pos.X / regionSize;
        var regionZ = (int)player.Entity.Pos.Z / regionSize;
        for (var dx = -1; dx <= 1; dx++)
        for (var dz = -1; dz <= 1; dz++)
        {
            yield return (regionX + dx, regionZ + dz);
        }
    }
}
