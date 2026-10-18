using System;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Util;

namespace RoughSeas.SeaAndWeather;

public class OceanCalibration(OceanConfig config)
{
    public const int Bins = 16;
    private const int BinWidth = 256 / Bins;

    private Counts counts = new();

    public int SeaFrom => counts.SeaFrom ?? config.DefaultSeaFrom;

    public bool Settled => counts.SeaFrom != null;

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
        if (data != null) counts = SerializerUtil.Deserialize<Counts>(data);
    }

    private long Misread(int seaBin) => counts.Land.Skip(seaBin).Sum() + counts.Water.Take(seaBin).Sum();

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
    public class Counts
    {
        public int Version = 1;
        public long[] Water = new long[Bins];
        public long[] Land = new long[Bins];
        public int? SeaFrom;
    }
}
