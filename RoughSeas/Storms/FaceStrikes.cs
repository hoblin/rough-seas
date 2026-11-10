using System;
using RoughSeas.Shore;
using RoughSeas.Surf;

namespace RoughSeas.Storms;

public readonly record struct SprayBurst(double X, double Y, double Z, double AlongX, double AlongZ, int Count, double Side, Impact Impact);

/// <summary>
/// Finds the crests and sheets striking the faces around the player. Each is decided from the shared sea and the world's
/// clock, the crest passing within the last tick of lived time, so two clients decide the same strikes; only the drops
/// are each client's own.
/// </summary>
public sealed class FaceStrikes(SeaSurface surface, SurfaceField shares, Waves waves, WallSpray spray, FacesConfig config, Action<SprayBurst> emit, Action<string, object[]> log)
{
    private const double StillLevel = 7.0 / 8;
    private const double OffFace = 0.45;
    private const double LogEverySeconds = 5;

    private int standing, broken, impulsive, sheets, parcels;
    private double highest, sinceLog;

    public void Update(double playerX, double playerZ, double window)
    {
        if (window <= 0 || surface.Calm || surface.Seabed is not { } field) return;
        var radius = config.RadiusBlocks * config.RadiusBlocks;
        bool Near(double x, double z) => (x - playerX) * (x - playerX) + (z - playerZ) * (z - playerZ) <= radius;

        foreach (var wall in field.Walls)
        {
            if (Near(wall.X, wall.Z)) Wall(wall, window);
        }
        foreach (var step in field.Steps)
        {
            if (Near(step.X, step.Z)) Step(step, window);
        }
        if ((sinceLog += window) < LogEverySeconds) return;
        sinceLog = 0;
        if (standing + broken + sheets > 0)
        {
            log("strikes in the last {0:F0} s: {1} stand and reflect, {2} break on a face ({3} impulsive, highest {4:F2} m), {5} sheets stopped by a step; {6} parcels of spray",
                [LogEverySeconds, standing, broken, impulsive, highest, sheets, parcels]);
        }
        (standing, broken, impulsive, sheets, parcels, highest) = (0, 0, 0, 0, 0, 0);
    }

    private void Wall(WallFace wall, double window)
    {
        var (x, z) = (wall.X + 0.5, wall.Z + 0.5);
        var share = shares.Factor(x, z);
        if (share <= 0 || surface.SinceCrest(x, z) >= window) return;
        var (height, depth) = surface.WaveAt(x, z, share);
        if (height <= 0) return;
        if (!waves.BreaksAt(depth, height) && surface.FoldAt(x, z, share) <= surface.CapsFold)
        {
            standing++;
            return;
        }

        broken++;
        highest = Math.Max(highest, height);
        var period = surface.DominantPeriod;
        var crest = height * (1 + wall.Reflection) / 2;
        var impact = spray.Wave(height, period, depth, crest, waves.BreakerCelerity(height, period, depth));
        if (impact.Impulsive) impulsive++;
        Burst(x + OffFace * wall.SideX, wall.Level + StillLevel + crest, z + OffFace * wall.SideZ, wall.SideZ, wall.SideX, impact);
    }

    private void Step(StepFace step, double window)
    {
        var (x, z) = (step.X + 0.5, step.Z + 0.5);
        var (height, twoPercent) = surface.SheetWaveAt(x, z);
        if (height <= 0 || twoPercent <= 0) return;
        var share = Math.Sqrt(height / twoPercent);
        var (head, lens) = step.At(share, SurfBuilder.Scales);
        if (head <= 0) return;
        var arrival = SheetFront.Arrival(step.RunUp * share, Math.Max(step.Ground, 0), step.Distance);
        var since = surface.SinceCrest(x, z);
        if (since < arrival || since >= arrival + window) return;

        sheets++;
        Burst(x + OffFace * step.SideX, step.Level + StillLevel + step.Ground, z + OffFace * step.SideZ, step.SideZ, step.SideX, spray.Sheet(lens, head));
    }

    private void Burst(double x, double y, double z, int alongX, int alongZ, Impact impact)
    {
        var (count, side) = spray.Parcels(impact.Litres);
        if (count <= 0) return;
        parcels += count;
        emit(new SprayBurst(x, y, z, alongX, alongZ, count, side, impact));
    }
}
