using System;
using System.Collections.Generic;
using System.Linq;

namespace RoughSeas.Surf;

/// <param name="Distance">How far the water here lies from the face. Up to <see cref="FirstWaterVertex"/> the point is a vertex of the face itself, shared with the water held under the land; the reflection grows from there.</param>
public readonly record struct FaceReading(double Distance, double NormalX, double NormalZ, double Reflection)
{
    public const double FirstWaterVertex = 0.5;
}

public readonly record struct WallFace(int X, int Z, int Level, int SideX, int SideZ, double NormalX, double NormalZ, double Rise, int Run, double Wavelength, double Reflection,
    bool StandsAboveCrest, double FaceX, double FaceZ);

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
/// depth's slope along X and Z, and over land the top of the ground in world height, over water the share of waves breaking there. <see cref="Gains"/>: for each wave slot, its height here over its height in deep water (<see cref="WaveEnergy"/>). <see cref="Lags"/>: for each wave
/// slot, the phase the bed has held the wave back by (<see cref="PhaseField"/>); over land, the lag at the shoreline the
/// sheet comes from, carried to here. The lags hold for the wave vectors in <see cref="Numbers"/> alone. <see cref="Face"/>:
/// over water, the distance to the nearest face a wave meets, its outward normal along X and Z, and the share of a wave
/// it sends back; the normal is half length where the crest stands higher than the face, which then doesn't foam. Over land
/// the distance is -0.5, so it interpolates to 0 on the face itself; -1.5 marks the foot of a step that stops a sheet.
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

    public float[][] Gains { get; } = [Filled(size * size * Channels), Filled(size * size * Channels)];

    /// <summary>The steepest the reflected water may rise per metre from the face.</summary>
    public double FaceSlope { get; set; } = double.PositiveInfinity;

    public IReadOnlyList<WallFace> Walls { get; set; } = [];

    public IReadOnlyList<StepFace> Steps { get; set; } = [];

    public FaceReading FaceAt(double x, double z)
    {
        var (normalX, normalZ) = (Sample(Face, x, z, 1), Sample(Face, x, z, 2));
        var length = Math.Sqrt(normalX * normalX + normalZ * normalZ);
        return length > 0 ? new FaceReading(Math.Max(Sample(Face, x, z, 0), 0), normalX / length, normalZ / length, Sample(Face, x, z, 3)) : default;
    }

    public double DepthAt(double x, double z) => Sample(Bed, x, z, 0);

    public double SlopeXAt(double x, double z) => Sample(Aux, x, z, 1);

    public double SlopeZAt(double x, double z) => Sample(Aux, x, z, 2);

    public double LagAt(int slot, double x, double z) =>
        slot / Channels < Lags.Length ? Sample(Lags[slot / Channels], x, z, slot % Channels) : 0;

    public double GainAt(int slot, double x, double z) =>
        slot / Channels < Gains.Length ? Sample(Gains[slot / Channels], x, z, slot % Channels) : 1;

    public double BreakingAt(double x, double z) => DepthAt(x, z) > 0 ? Sample(Aux, x, z, 3) : 0;

    public string Describe(double x, double z) => !Covers(x, z)
        ? "outside the seabed read"
        : $"depth {Sample(Bed, x, z, 0):F2} m, {Sample(Bed, x, z, 1):F1} m from the shoreline, run-up {Sample(Bed, x, z, 2):F2} m, reached by {Sample(Bed, x, z, 3):F2} m, "
          + $"soaks {Sample(Aux, x, z, 0):E1} m/s, bed slope {Sample(Aux, x, z, 1):F2} {Sample(Aux, x, z, 2):F2}; "
          + $"nearest face {Sample(Face, x, z, 0):F1} m, reflects {Sample(Face, x, z, 3):F2}; "
          + $"breaking {BreakingAt(x, z):F2}, edge {EdgeShare(x, z):F2}, gains {string.Join(" ", Enumerable.Range(0, Slots).Select(slot => GainAt(slot, x, z).ToString("F2")))}";

    public bool Covers(double x, double z) => x >= OriginX && z >= OriginZ && x < OriginX + Size && z < OriginZ + Size;

    public int EdgeFadeBlocks { get; set; } = 1;

    public double EdgeShare(double x, double z)
    {
        var inside = Math.Min(Math.Min(x - OriginX, z - OriginZ), Math.Min(OriginX + Size - x, OriginZ + Size - z));
        var share = Math.Clamp(inside / EdgeFadeBlocks, 0, 1);
        return share * share * (3 - 2 * share);
    }

    private static float[] Filled(int length)
    {
        var values = new float[length];
        Array.Fill(values, 1f);
        return values;
    }

    public void Set(float[] into, int x, int z, int channel, float value) => into[((z - OriginZ) * Size + x - OriginX) * Channels + channel] = value;

    public float Get(float[] from, int x, int z, int channel) => from[((z - OriginZ) * Size + x - OriginX) * Channels + channel];

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
