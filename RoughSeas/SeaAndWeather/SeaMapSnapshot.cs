using System;
using System.Buffers.Binary;
using ProtoBuf;

namespace RoughSeas.SeaAndWeather;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic, SkipConstructor = true)]
public class SeaMapSnapshot
{
    public const string ModdataKey = "roughseas:seamap";
    public const int CurrentVersion = 1;

    private static readonly double[] Scales = [100, 100, 10000, 10000, 100, 100, 1000, 1000, 100];

    public int Version = CurrentVersion;
    public int RegionX;
    public int RegionZ;
    public double TotalHours;
    public int CellsPerSide;
    public float RegionMps;
    public byte[] Cells = [];

    public static SeaMapSnapshot Of(int regionX, int regionZ, double totalHours, double regionMps, int cellsPerSide, SeaSample[] samples)
    {
        var cells = new byte[samples.Length * Scales.Length * sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        {
            var values = Values(samples[i]);
            for (var field = 0; field < Scales.Length; field++)
            {
                var stored = Math.Clamp(Math.Round(values[field] * Scales[field]), short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(cells.AsSpan((i * Scales.Length + field) * sizeof(short)), (short)stored);
            }
        }
        return new SeaMapSnapshot
        {
            RegionX = regionX,
            RegionZ = regionZ,
            TotalHours = totalHours,
            CellsPerSide = cellsPerSide,
            RegionMps = (float)regionMps,
            Cells = cells
        };
    }

    public SeaSample[] Samples()
    {
        var samples = new SeaSample[CellsPerSide * CellsPerSide];
        var values = new double[Scales.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            for (var field = 0; field < Scales.Length; field++)
            {
                values[field] = BinaryPrimitives.ReadInt16LittleEndian(Cells.AsSpan((i * Scales.Length + field) * sizeof(short))) / Scales[field];
            }
            samples[i] = new SeaSample(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]);
        }
        return samples;
    }

    private static double[] Values(SeaSample sample) =>
    [
        sample.WindX, sample.WindZ, sample.StormPresence, sample.RainCloudness, sample.WaveHeight, sample.WavePeriod,
        sample.CurrentX, sample.CurrentZ, sample.WaterCelsius
    ];
}
