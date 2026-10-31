using RoughSeas.Storms;
using RoughSeas.Vessels;

namespace RoughSeas.Tests.Storms;

public class UnderwaterBodyTests
{
    private const double FrameSeconds = 1 / 30.0;
    private const double BoundaryMps = 2.0;
    private const double InteriorMps = 0.3;

    private static readonly DriftConfig Drift = Fixtures.Config().Drift;

    // a dugout's length and beam, as her sources give them
    private static UnderwaterBody Dugout(double cargoKg = 0)
    {
        var hull = Fixtures.Hull("dugout");
        var aboard = hull.Crewed(containers: cargoKg);
        return UnderwaterBody.Of(4.5, 0.75, hull.Float(aboard).DraftMetres, aboard.Total, Fixtures.Underwater("dugout"), Drift)!;
    }

    private static UnderwaterBody Raft() =>
        UnderwaterBody.Of(3, 2, 0.1, 120 + 75, Fixtures.Underwater("boat-raft"), Drift)!;

    [Fact]
    public void ALoadedHullComesUpToTheCurrentSlowerThanAnEmptyOne()
    {
        var empty = Dugout();
        var loaded = Dugout(cargoKg: 300);

        Assert.True(loaded.DraftMetres > empty.DraftMetres);
        Assert.True(SecondsToCurrent(loaded, yaw: 0, current: (BoundaryMps, 0)) > SecondsToCurrent(empty, yaw: 0, current: (BoundaryMps, 0)));
        Assert.True(SecondsToCurrent(loaded, yaw: Math.PI / 2, current: (BoundaryMps, 0)) > SecondsToCurrent(empty, yaw: Math.PI / 2, current: (BoundaryMps, 0)));
    }

    [Fact]
    public void LeftAloneEveryHullComesUpToTheCurrentsSpeed()
    {
        foreach (var body in new[] { Dugout(), Dugout(cargoKg: 300), Raft() })
        {
            var (x, z) = Drifted(body, yaw: 0.3, current: (BoundaryMps, -0.5), row: (0, 0), seconds: 600);

            // drag on the square of a speed tails off slowly as the speed goes
            Assert.Equal(BoundaryMps, x, 1);
            Assert.Equal(-0.5, z, 1);
        }
    }

    [Fact]
    public void ACurrentAcrossHerCarriesHerSoonerThanOneAlongHer()
    {
        var dugout = Dugout();
        // yaw 0 lies her along z
        var (alongX, alongZ) = HullShape.ToWorld(1, 0, 0);
        var (acrossX, acrossZ) = HullShape.ToWorld(0, 1, 0);

        Assert.True(SecondsToCurrent(dugout, 0, (acrossX * BoundaryMps, acrossZ * BoundaryMps)) < SecondsToCurrent(dugout, 0, (alongX * BoundaryMps, alongZ * BoundaryMps)) / 3);
    }

    [Fact]
    public void PaddledUpstreamSheMakesHerStillWaterSpeedThroughTheWater()
    {
        var raft = Raft();
        const double yaw = 0;
        // her bow is toward her frame's negative along
        var (upX, upZ) = HullShape.ToWorld(-1, 0, yaw);
        var current = (-upX * BoundaryMps, -upZ * BoundaryMps);

        var (fastX, fastZ) = Drifted(raft, yaw, current, (upX * 2.5, upZ * 2.5), seconds: 300);
        var (slowX, slowZ) = Drifted(raft, yaw, current, (upX * 1.5, upZ * 1.5), seconds: 300);

        Assert.Equal(0.5, fastX * upX + fastZ * upZ, 2);
        Assert.Equal(-0.5, slowX * upX + slowZ * upZ, 2);
    }

    [Fact]
    public void PaddledAcrossTheCurrentSheIsSetDownstreamByAllOfIt()
    {
        var dugout = Dugout();
        const double yaw = 1;
        var (aheadX, aheadZ) = HullShape.ToWorld(-1, 0, yaw);
        var (acrossX, acrossZ) = HullShape.ToWorld(0, 1, yaw);

        var (x, z) = Drifted(dugout, yaw, (acrossX * BoundaryMps, acrossZ * BoundaryMps), (aheadX * 1.2, aheadZ * 1.2), seconds: 600);

        Assert.Equal(1.2, x * aheadX + z * aheadZ, 2);
        Assert.Equal(BoundaryMps, x * acrossX + z * acrossZ, 2);
    }

    [Fact]
    public void HeldAtHerBowSheSwingsToLieAlongTheStreamSternDownstream()
    {
        // broadside, quartering, and all but stern upstream
        foreach (var start in new[] { 0, 0.8, Math.PI / 2 - 0.05 })
        {
            var (along, across) = Swung(Dugout(), start, seconds: 300);

            Assert.True(along > 0.99 * BoundaryMps);
            Assert.InRange(across, -0.02, 0.02);
        }
    }

    [Fact]
    public void LyingAlongTheStreamTheCurrentPullsFarLessThanAbeam()
    {
        var dugout = Dugout();

        var (alongX, alongZ) = dugout.Pull(0, 0, BoundaryMps, 0, yaw: Math.PI / 2);
        var (abeamX, abeamZ) = dugout.Pull(0, 0, BoundaryMps, 0, yaw: 0);

        Assert.True(Math.Sqrt(alongX * alongX + alongZ * alongZ) * 10 < Math.Sqrt(abeamX * abeamX + abeamZ * abeamZ));
    }

    [Fact]
    public void TheBoundaryCurrentPullsAbout44TimesHarderThanTheInterior()
    {
        var raft = Raft();

        var (_, boundary) = raft.Drag(0, BoundaryMps);
        var (_, interior) = raft.Drag(0, InteriorMps);

        Assert.Equal(44.4, boundary / interior, 1);
    }

    [Fact]
    public void ASwingingHullDragsWaterAlongAsARoundBodyOfHerProportionsDoes()
    {
        Assert.Equal(0.5, UnderwaterBody.SpheroidSurgeAddedMass(1.4, 1.4), 3);
        Assert.Equal(0.059, UnderwaterBody.SpheroidSurgeAddedMass(5, 1), 3);
        Assert.Equal(0.021, UnderwaterBody.SpheroidSurgeAddedMass(10, 1), 3);
    }

    [Fact]
    public void AHullWithNoDraftShowsTheWaterNothing()
    {
        Assert.Null(UnderwaterBody.Of(4.5, 0.75, 0, 400, Fixtures.Underwater("dugout"), Drift));
    }

    private static double SecondsToCurrent(UnderwaterBody body, double yaw, (double X, double Z) current)
    {
        var (vx, vz) = (0.0, 0.0);
        var speed = Math.Sqrt(current.X * current.X + current.Z * current.Z);
        for (var t = 0.0; t < 3600; t += FrameSeconds)
        {
            (vx, vz) = body.Carry(vx, vz, current.X, current.Z, yaw, 0, 0, FrameSeconds);
            if ((vx * current.X + vz * current.Z) / speed >= 0.9 * speed) return t;
        }
        return double.PositiveInfinity;
    }

    private static (double X, double Z) Drifted(UnderwaterBody body, double yaw, (double X, double Z) current, (double X, double Z) row, double seconds)
    {
        var (vx, vz) = (0.0, 0.0);
        for (var t = 0.0; t < seconds; t += FrameSeconds) (vx, vz) = body.Carry(vx, vz, current.X, current.Z, yaw, row.X, row.Z, FrameSeconds);
        return (vx, vz);
    }

    // held still at her bow in a stream toward +x, turning as the mooring turns her
    private static (double Along, double Across) Swung(UnderwaterBody body, double yaw, double seconds)
    {
        var rate = 0.0;
        for (var t = 0.0; t < seconds; t += FrameSeconds)
        {
            var (_, across) = HullShape.ToHull(BoundaryMps, 0, yaw);
            (rate, var turned) = body.Swing(rate, across, FrameSeconds);
            yaw -= turned;
        }
        return HullShape.ToHull(BoundaryMps, 0, yaw);
    }
}
