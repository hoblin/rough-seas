using System;
using System.Collections.Generic;

namespace RoughSeas.Surf;

public readonly record struct FaceReading(double Distance, double NormalX, double NormalZ, double Reflection);

public readonly record struct WallFace(int X, int Z, int Level, int SideX, int SideZ, double NormalX, double NormalZ, double Rise, int Run, double Wavelength, double Reflection,
    bool StandsAboveCrest);

public sealed record StepFace(int X, int Z, int Level, int SideX, int SideZ, double Ground, double Rise, double Distance, double RunUp, double[] Heads, double[] Lenses)
{
    public (double Head, double Lens) At(double share)
    {
        for (var index = SurfBuilder.Scales.Length - 1; index >= 0; index--)
        {
            if (SurfBuilder.Scales[index] <= share) return (Heads[index], Lenses[index]);
        }
        return default;
    }
}

/// <summary>
/// The seabed around a player, a texel a block, as <c>seasurface.vsh</c> and <c>seawet.fsh</c> read it and
/// <see cref="Storms.SeaSurface"/> mirrors it. <see cref="Bed"/>: the depth under still water (negative over land), the
/// distance a sheet runs from the shoreline to here, the run-up of the shore it comes from, and the run-up a wave needs
/// to reach here (<see cref="Unreached"/> where none does). <see cref="Aux"/>: the bed's hydraulic conductivity, the
/// depth's slope along X and Z, and over land the top of the ground in world height. <see cref="Lags"/>: for each wave
/// slot, the phase the bed has held the wave back by (<see cref="PhaseField"/>); over land, the lag at the shoreline the
/// sheet comes from, carried to here. The lags hold for the wave vectors in <see cref="Numbers"/> alone. <see cref="Face"/>:
/// over water, the distance to the nearest face a wave meets, its outward normal along X and Z, and the share of a wave
/// it sends back, negative where the crest stands higher than the face, which then neither jets nor foams. Over land the
/// distance is -0.5, so it interpolates to 0 on the face itself; -1.5 marks the foot of a step that stops a sheet.
/// </summary>
public sealed class SurfField(int originX, int originZ, int size, (double X, double Z)[] numbers)
{
    public const int Channels = 4;
    public const int Unreached = 1_000_000;
    public const int Slots = 8;

    public int OriginX { get; } = originX;

    public int OriginZ { get; } = originZ;

    public int Size { get; } = size;

    public (double X, double Z)[] Numbers { get; } = numbers;

    public float[] Bed { get; } = new float[size * size * Channels];

    public float[] Aux { get; } = new float[size * size * Channels];

    public float[][] Lags { get; } = [new float[size * size * Channels], new float[size * size * Channels]];

    public float[] Face { get; } = new float[size * size * Channels];

    public IReadOnlyList<WallFace> Walls { get; set; } = [];

    public IReadOnlyList<StepFace> Steps { get; set; } = [];

    public FaceReading FaceAt(double x, double z)
    {
        var (normalX, normalZ) = (Sample(Face, x, z, 1), Sample(Face, x, z, 2));
        var length = Math.Sqrt(normalX * normalX + normalZ * normalZ);
        return length > 0 ? new FaceReading(Math.Max(Sample(Face, x, z, 0), 0), normalX / length, normalZ / length, Math.Abs(Sample(Face, x, z, 3))) : default;
    }

    public double DepthAt(double x, double z) => Sample(Bed, x, z, 0);

    public double SlopeXAt(double x, double z) => Sample(Aux, x, z, 1);

    public double SlopeZAt(double x, double z) => Sample(Aux, x, z, 2);

    public double LagAt(int slot, double x, double z) =>
        slot / Channels < Lags.Length ? Sample(Lags[slot / Channels], x, z, slot % Channels) : 0;

    public string Describe(double x, double z) => !Covers(x, z)
        ? "outside the seabed read"
        : $"depth {Sample(Bed, x, z, 0):F2} m, {Sample(Bed, x, z, 1):F1} m from the shoreline, run-up {Sample(Bed, x, z, 2):F2} m, reached by {Sample(Bed, x, z, 3):F2} m, "
          + $"soaks {Sample(Aux, x, z, 0):E1} m/s, bed slope {Sample(Aux, x, z, 1):F2} {Sample(Aux, x, z, 2):F2}; "
          + $"nearest face {Sample(Face, x, z, 0):F1} m, reflects {Sample(Face, x, z, 3):F2}";

    public bool Covers(double x, double z) => x >= OriginX && z >= OriginZ && x < OriginX + Size && z < OriginZ + Size;

    public void Set(float[] into, int x, int z, int channel, float value) => into[((z - OriginZ) * Size + x - OriginX) * Channels + channel] = value;

    private double Sample(float[] from, double x, double z, int channel)
    {
        var u = Math.Clamp(x - OriginX - 0.5, 0, Size - 1);
        var v = Math.Clamp(z - OriginZ - 0.5, 0, Size - 1);
        var (left, top) = ((int)Math.Floor(u), (int)Math.Floor(v));
        var (right, bottom) = (Math.Min(left + 1, Size - 1), Math.Min(top + 1, Size - 1));
        var (tu, tv) = (u - left, v - top);
        double At(int column, int row) => from[(row * Size + column) * Channels + channel];
        var upper = At(left, top) + (At(right, top) - At(left, top)) * tu;
        var lower = At(left, bottom) + (At(right, bottom) - At(left, bottom)) * tu;
        return upper + (lower - upper) * tv;
    }
}
