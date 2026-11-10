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
        for (var index = SwashSheets.Scales.Length - 1; index >= 0; index--)
        {
            if (SwashSheets.Scales[index] <= share) return (Heads[index], Lenses[index]);
        }
        return default;
    }
}

/// <summary>
/// The waves over the seabed around a player, a texel a block, as <see cref="Storms.SeaSurface"/> reads them and <c>seasurface.vsh</c> and <c>seawet.fsh</c> sample them.
/// Only this class knows how its layers are laid out; the shaders' side of that is <see cref="Samplers"/>:
/// <list type="bullet">
/// <item><c>seaBed</c>: the depth the waves feel under still water, averaged at their scale (negative over land); over land the distance a sheet runs from the shoreline, the run-up of the shore it comes from, and the run-up a wave needs to get here (<see cref="Unreached"/> where none does).</item>
/// <item><c>seaBedAux</c>: the bed's hydraulic conductivity, the depth's slope along X and Z, and over land the top of the ground in world height.</item>
/// <item><c>seaLagA/B</c>: for each slot, the phase the bed has held the wave back by (<see cref="PhaseField"/>); over land, the lag at the shoreline the sheet comes from, carried to here. They hold for the wave vectors in <see cref="Numbers"/> alone.</item>
/// <item><c>seaFace</c>: over water, the distance to the nearest face a wave meets, its outward normal along X and Z, and the share of a wave it sends back; the normal is half length where the crest stands higher than the face, which then doesn't foam. Over land the distance is <see cref="LandDistance"/>, so it interpolates to 0 on the face itself; <see cref="StepFootDistance"/> marks the foot of a step that stops a sheet.</item>
/// <item><c>seaGainA/B</c>: for each slot, its height here over its height in deep water (<see cref="WaveEnergy"/>).</item>
/// <item><c>seaSurf</c>: over water, the share of waves breaking, 0 over land; and everywhere the sea's height coming into the last breaking water, against the deep sea's, over land that of the shoreline nearest; and the depth that sets the waves' speed (<see cref="Water.TransformedWaves.CelerityDepth"/>), over land that of the shoreline nearest.</item>
/// </list>
/// </summary>
public sealed class SurfField(int originX, int originZ, int size, (double X, double Z)[] numbers)
{
    public const int Slots = 8;
    public const int Unreached = 1_000_000;
    public const float LandDistance = -0.5f;
    public const float StepFootDistance = -1.5f;

    private const int Channels = 4;

    public static readonly string[] Samplers = ["seaBed", "seaBedAux", "seaLagA", "seaLagB", "seaFace", "seaGainA", "seaGainB", "seaSurf"];

    private readonly float[] bed = new float[size * size * Channels];
    private readonly float[] aux = new float[size * size * Channels];
    private readonly float[] surf = Enumerable.Range(0, size * size * Channels).Select(i => i % Channels == 1 ? 1f : 0f).ToArray();
    private readonly float[] face = new float[size * size * Channels];
    private readonly float[][] lags = [new float[size * size * Channels], new float[size * size * Channels]];
    private readonly float[][] gains = [Filled(size * size * Channels), Filled(size * size * Channels)];

    public int OriginX { get; } = originX;

    public int OriginZ { get; } = originZ;

    public int Size { get; } = size;

    public (double X, double Z)[] Numbers { get; } = numbers;

    /// <summary>The layers in the order of <see cref="Samplers"/>.</summary>
    public float[][] Layers => [bed, aux, lags[0], lags[1], face, gains[0], gains[1], surf];

    /// <summary>The steepest the reflected water may rise per metre from the face.</summary>
    public double FaceSlope { get; set; } = double.PositiveInfinity;

    public IReadOnlyList<WallFace> Walls { get; set; } = [];

    public IReadOnlyList<StepFace> Steps { get; set; } = [];

    public int EdgeFadeBlocks { get; set; } = 1;

    /// <summary>Whether this slot's lags and gains were solved for this wave vector.</summary>
    public bool Holds(int slot, double numberX, double numberZ) => slot < Numbers.Length && slot < Slots && Numbers[slot] == (numberX, numberZ);

    public FaceReading FaceAt(double x, double z)
    {
        var (normalX, normalZ) = (Sample(face, x, z, 1), Sample(face, x, z, 2));
        var length = Math.Sqrt(normalX * normalX + normalZ * normalZ);
        return length > 0 ? new FaceReading(Math.Max(Sample(face, x, z, 0), 0), normalX / length, normalZ / length, Sample(face, x, z, 3)) : default;
    }

    public double DepthAt(double x, double z) => Sample(bed, x, z, 0);

    public double SlopeXAt(double x, double z) => Sample(aux, x, z, 1);

    public double SlopeZAt(double x, double z) => Sample(aux, x, z, 2);

    public double LagAt(int slot, double x, double z) => slot / Channels < lags.Length ? Sample(lags[slot / Channels], x, z, slot % Channels) : 0;

    public double GainAt(int slot, double x, double z) => slot / Channels < gains.Length ? Sample(gains[slot / Channels], x, z, slot % Channels) : 1;

    public double BreakingAt(double x, double z) => Sample(surf, x, z, 0);

    public double ArrivingAt(double x, double z) => Sample(surf, x, z, 1);

    public double CelerityDepthAt(double x, double z) => Sample(surf, x, z, 2);

    public bool Covers(double x, double z) => x >= OriginX && z >= OriginZ && x < OriginX + Size && z < OriginZ + Size;

    public double EdgeShare(double x, double z)
    {
        var inside = Math.Min(Math.Min(x - OriginX, z - OriginZ), Math.Min(OriginX + Size - x, OriginZ + Size - z));
        var share = Math.Clamp(inside / EdgeFadeBlocks, 0, 1);
        return share * share * (3 - 2 * share);
    }

    public string Describe(double x, double z) => !Covers(x, z)
        ? "outside the seabed read"
        : $"depth {DepthAt(x, z):F2} m, {Sample(bed, x, z, 1):F1} m from the shoreline, run-up {Sample(bed, x, z, 2):F2} m, reached by {Sample(bed, x, z, 3):F2} m, "
          + $"soaks {Sample(aux, x, z, 0):E1} m/s, bed slope {SlopeXAt(x, z):F2} {SlopeZAt(x, z):F2}; "
          + $"nearest face {Sample(face, x, z, 0):F1} m, reflects {Sample(face, x, z, 3):F2}; "
          + $"breaking {BreakingAt(x, z):F2}, edge {EdgeShare(x, z):F2}, gains {string.Join(" ", Enumerable.Range(0, Slots).Select(slot => GainAt(slot, x, z).ToString("F2")))}";

    /// <summary>Sets the depth the waves feel here, and with it the depth that sets their speed until <see cref="SetCelerityDepth"/> says otherwise.</summary>
    public void SetDepth(int x, int z, float depth)
    {
        Put(bed, x, z, 0, depth);
        Put(surf, x, z, 2, depth);
    }

    public void SetCelerityDepth(int x, int z, float depth) => Put(surf, x, z, 2, depth);

    public void SetSheet(int x, int z, float distance, float runUp, float needed)
    {
        Put(bed, x, z, 1, distance);
        Put(bed, x, z, 2, runUp);
        Put(bed, x, z, 3, needed);
    }

    public float RunUpOf(int x, int z) => bed[Index(x, z) + 2];

    public void SetConductivity(int x, int z, float conductivity) => Put(aux, x, z, 0, conductivity);

    public void SetSlope(int x, int z, float slopeX, float slopeZ)
    {
        Put(aux, x, z, 1, slopeX);
        Put(aux, x, z, 2, slopeZ);
    }

    public void SetGroundTop(int x, int z, float top) => Put(aux, x, z, 3, top);

    public void SetBreaking(int x, int z, float share) => Put(surf, x, z, 0, share);

    public void SetArriving(int x, int z, float share) => Put(surf, x, z, 1, share);

    public void SetLag(int slot, int x, int z, float lag) => Put(lags[slot / Channels], x, z, slot % Channels, lag);

    public void SetGain(int slot, int x, int z, float gain) => Put(gains[slot / Channels], x, z, slot % Channels, gain);

    public float GainOf(int slot, int x, int z) => gains[slot / Channels][Index(x, z) + slot % Channels];

    public void SetFaceDistance(int x, int z, float distance) => Put(face, x, z, 0, distance);

    public void SetFace(int x, int z, float distance, float normalX, float normalZ, float reflection)
    {
        Put(face, x, z, 0, distance);
        Put(face, x, z, 1, normalX);
        Put(face, x, z, 2, normalZ);
        Put(face, x, z, 3, reflection);
    }

    public (float Distance, float NormalX, float NormalZ, float Reflection) FaceOf(int x, int z)
    {
        var i = Index(x, z);
        return (face[i], face[i + 1], face[i + 2], face[i + 3]);
    }

    private static float[] Filled(int length)
    {
        var values = new float[length];
        Array.Fill(values, 1f);
        return values;
    }

    private void Put(float[] into, int x, int z, int channel, float value) => into[Index(x, z) + channel] = value;

    private int Index(int x, int z) => ((z - OriginZ) * Size + x - OriginX) * Channels;

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
