using System;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

/// <summary>
/// Where the world's ocean map turns to sea, learned once from the columns of the built world that do and don't hold salt
/// water at sea level: each worldgen mod gives the map's numbers its own meaning. Until both are seen often enough it
/// reads the configured default; once settled it is saved with the world and never moves, since the storms brewed far
/// away depend on it.
/// </summary>
public class OceanCalibration(OceanConfig config)
{
    public const int Bins = 16;
    private const int BinWidth = 256 / Bins;

    private Counts counts = new();

    public int SeaFrom => counts.SeaFrom ?? config.DefaultSeaFrom;

    public bool Settled => counts.SeaFrom != null;

    public (long Water, long Land) Seen => (counts.Water.Sum(), counts.Land.Sum());

    public bool IsSea(int oceanValue) => oceanValue >= SeaFrom;

    public bool Add(int oceanValue, bool water)
    {
        if (Settled) return false;
        var bin = Math.Clamp(oceanValue / BinWidth, 0, Bins - 1);
        (water ? counts.Water : counts.Land)[bin]++;
        if (counts.Water.Sum() < config.CalibrationSamples || counts.Land.Sum() < config.CalibrationSamples) return false;
        counts.SeaFrom = Enumerable.Range(0, Bins + 1).MinBy(Misread) * BinWidth;
        return true;
    }

    public byte[] ToBytes() => SerializerUtil.Serialize(counts);

    public void Load(byte[]? data)
    {
        if (data != null && SerializerUtil.Deserialize<Counts>(data) is { Version: Counts.CurrentVersion } saved) counts = saved;
    }

    private double Misread(int seaBin) =>
        (double)counts.Land.Skip(seaBin).Sum() / counts.Land.Sum() + (double)counts.Water.Take(seaBin).Sum() / counts.Water.Sum();

    [ProtoContract(SkipConstructor = true)]
    public class Counts
    {
        public const int CurrentVersion = 1;

        [ProtoMember(1)] public int Version = CurrentVersion;
        [ProtoMember(2)] public long[] Water = new long[Bins];
        [ProtoMember(3)] public long[] Land = new long[Bins];
        [ProtoMember(4)] public int? SeaFrom;
    }
}
