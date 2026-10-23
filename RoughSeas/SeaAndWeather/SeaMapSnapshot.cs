using System;
using System.Buffers.Binary;
using System.Linq;
using ProtoBuf;

namespace RoughSeas.SeaAndWeather;

[ProtoContract(SkipConstructor = true)]
public class SeaMapSnapshot
{
    public const string ModdataKey = "roughseas:seamap";
    public const int CurrentVersion = 2;

    private static readonly (Func<SeaSample, double> Read, double Scale)[] Fields =
    [
        (sample => sample.WindX, 100),
        (sample => sample.WindZ, 100),
        (sample => sample.StormPresence, 10000),
        (sample => sample.RainCloudness, 10000),
        (sample => sample.WaveHeight, 100),
        (sample => sample.WavePeriod, 100),
        (sample => sample.CurrentX, 1000),
        (sample => sample.CurrentZ, 1000),
        (sample => sample.WaterCelsius, 100)
    ];

    [ProtoMember(1)] public int Version = CurrentVersion;
    [ProtoMember(2)] public int RegionX;
    [ProtoMember(3)] public int RegionZ;
    [ProtoMember(4)] public double TotalHours;
    [ProtoMember(5)] public int CellsPerSide;
    [ProtoMember(6)] public float RegionMps;
    [ProtoMember(7)] public byte[] Cells = [];
    [ProtoMember(8)] public long FirstWaveSlot;
    [ProtoMember(9)] public double[] WaveTowards = [];
    [ProtoMember(10)] public double[] WavePeriods = [];

    public static SeaMapSnapshot Of(int regionX, int regionZ, double totalHours, double regionMps, int cellsPerSide, SeaSample[] samples,
        long firstWaveSlot = 0, WaveGeneration[]? generations = null)
    {
        generations ??= [];
        var cells = new byte[samples.Length * Fields.Length * sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        for (var field = 0; field < Fields.Length; field++)
        {
            var stored = Math.Clamp(Math.Round(Fields[field].Read(samples[i]) * Fields[field].Scale), short.MinValue, short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(cells.AsSpan((i * Fields.Length + field) * sizeof(short)), (short)stored);
        }
        return new SeaMapSnapshot
        {
            RegionX = regionX,
            RegionZ = regionZ,
            TotalHours = totalHours,
            CellsPerSide = cellsPerSide,
            RegionMps = (float)regionMps,
            Cells = cells,
            FirstWaveSlot = firstWaveSlot,
            WaveTowards = generations.Select(generation => generation.TowardRadians).ToArray(),
            WavePeriods = generations.Select(generation => generation.Period).ToArray()
        };
    }

    public SeaSample[] Samples()
    {
        var samples = new SeaSample[CellsPerSide * CellsPerSide];
        var values = new double[Fields.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            for (var field = 0; field < Fields.Length; field++)
            {
                values[field] = BinaryPrimitives.ReadInt16LittleEndian(Cells.AsSpan((i * Fields.Length + field) * sizeof(short))) / Fields[field].Scale;
            }
            samples[i] = new SeaSample(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }
        return samples;
    }

    public WaveGeneration[] Generations() =>
        WaveTowards.Zip(WavePeriods, (toward, period) => new WaveGeneration(toward, period)).ToArray();
}
