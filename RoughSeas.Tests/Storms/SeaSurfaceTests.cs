using System.Globalization;
using System.Text.RegularExpressions;
using RoughSeas.SeaAndWeather;
using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class SeaSurfaceTests
{
    private const double FarX = 512031.25;
    private const double FarZ = 498877.5;
    private const double Frame = 1 / 60.0;
    private const int FramesPerAim = 30;
    private const double Slot = 360;

    private static SeaSurface Surface() => new(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves));

    private static WaveGeneration Generation(double height, double toward) => new(toward, 3.5 * Math.Sqrt(height));

    private static void Run(SeaSurface surface, double height, WaveGeneration earlier, WaveGeneration latest, double born,
        double from, double seconds = 120, double beaufort = 6, Action? eachFrame = null, bool crossed = false)
    {
        for (var frame = 0; frame * Frame < seconds; frame++)
        {
            if (frame % FramesPerAim == 0)
            {
                surface.Aim(height, beaufort);
                surface.Raise(earlier, latest, born, crossed);
            }
            surface.Advance(from + frame * Frame);
            eachFrame?.Invoke();
        }
    }

    private static void Settle(SeaSurface surface, double height, double toward, double from = 0, double seconds = 120, double beaufort = 6)
    {
        var raised = Generation(height, toward);
        Run(surface, height, raised, raised, from, from, seconds, beaufort);
    }

    private static void AssertSameSea(SeaSurface expected, SeaSurface actual)
    {
        for (var i = 0; i < 20; i++)
        {
            var (x, z) = (FarX + i * 7.3, FarZ - i * 3.1);
            Assert.Equal(expected.DisplacementAt(x, z).Y, actual.DisplacementAt(x, z).Y, 9);
        }
    }

    [Fact]
    public void DrawsTheSignificantHeightOfTheSeaState()
    {
        var surface = Surface();
        Settle(surface, 5, 0);
        Assert.Equal(5, surface.Trains.DrawnHeight, 2);
    }

    [Fact]
    public void TheWatersVerticalMotionIsTheRateOfItsSurface()
    {
        const double step = 0.01;
        var sea = Fixtures.OneTrain(2, 14);
        double Y(double time, double restX)
        {
            sea.Surface.Advance(time);
            return sea.Surface.DisplacementAt(restX, 0).Y;
        }

        for (var restX = 0.0; restX < sea.Length; restX += sea.Length / 7)
        {
            var (before, at, after) = (Y(120 - step, restX), Y(120, restX), Y(120 + step, restX));
            sea.Surface.Advance(120);
            var x = restX + sea.Surface.DisplacementAt(restX, 0).X;

            Assert.Equal((after - before) / (2 * step), sea.Surface.RiseVelocityAt(x, 0), 2);
            Assert.Equal((after - 2 * at + before) / (step * step), sea.Surface.RiseAccelerationAt(x, 0), 1);
        }
    }

    [Fact]
    public void TheWatersVerticalMotionFadesWithDepth()
    {
        var sea = Fixtures.OneTrain(2, 14);
        sea.Surface.Advance(120);
        var (x, shallow) = (0.0, 0.0);
        for (var at = 0.0; at < sea.Length; at += 1)
        {
            if (Math.Abs(sea.Surface.RiseVelocityAt(at, 0)) > Math.Abs(shallow)) (x, shallow) = (at, sea.Surface.RiseVelocityAt(at, 0));
        }

        Assert.Equal(shallow * Math.Exp(-sea.Number * 3), sea.Surface.RiseVelocityAt(x, 0, 3), 6);
    }

    [Fact]
    public void RisesAndFallsSmoothlyFarFromTheOrigin()
    {
        var surface = Surface();
        var previous = double.NaN;
        Run(surface, 5, Generation(5, 0), Generation(5, 0), 0, 0, beaufort: 8, eachFrame: () =>
        {
            var height = surface.HeightAt(FarX, FarZ);
            Assert.True(double.IsNaN(previous) || Math.Abs(height - previous) < 0.1, $"jumped from {previous:F3} to {height:F3}");
            previous = height;
        });
    }

    [Fact]
    public void DrawsTheSameWavesAsAClientThatJoinedLater()
    {
        var (calm, gale) = (Generation(3, 0), Generation(3, Math.PI / 2));
        var early = Surface();
        Run(early, 3, calm, calm, -Slot, 0, seconds: Slot);
        Run(early, 3, calm, gale, Slot, Slot, seconds: 15);

        var late = Surface();
        Run(late, 3, calm, gale, Slot, Slot + 14, seconds: 1);
        AssertSameSea(early, late);

        Run(early, 3, calm, gale, Slot, Slot + 15, seconds: 60);
        Run(late, 3, calm, gale, Slot, Slot + 15, seconds: 60);
        AssertSameSea(early, late);
    }

    [Fact]
    public void KeepsItsWavesWhileTheSeaTurnsWithinAHeading()
    {
        var surface = Surface();
        var changes = 0;
        surface.Trains.Changed += _ => changes++;
        Settle(surface, 3.7, 0);
        Run(surface, 3.7, Generation(3.7, 0), Generation(3.7, 0.05), Slot, Slot);

        Assert.Equal(1, changes);
        Assert.Equal(0, surface.Trains.TowardRadians);
    }

    [Fact]
    public void RaisesNewWavesWhenTheSeaTurnsAndLetsTheOldOnesFade()
    {
        var surface = Surface();
        var changes = 0;
        surface.Trains.Changed += _ => changes++;
        Settle(surface, 3, 0);
        Run(surface, 3, Generation(3, 0), Generation(3, 1.75), Slot, Slot);

        Assert.Equal(2, changes);
        Assert.Equal(1.75, surface.Trains.TowardRadians, tolerance: Math.PI / 18);
        Assert.Equal(3, surface.Trains.DrawnHeight, 2);
    }

    [Fact]
    public void KeepsItsHeightWhileNewWavesTakeOverFromOld()
    {
        var surface = Surface();
        Settle(surface, 5.5, 0);
        Run(surface, 5.5, Generation(5.5, 0), Generation(5.5, Math.PI / 2), Slot, Slot, seconds: 60, beaufort: 8,
            eachFrame: () => Assert.True(surface.Trains.DrawnHeight > 0.9 * 5.5, $"the sea drew {surface.Trains.DrawnHeight:F2} m"));
    }

    [Fact]
    public void CrossesIntoAnotherRegionsWavesWithoutAJump()
    {
        var (here, there) = (Generation(3, 0), Generation(4, 2));
        var crossing = Surface();
        Settle(crossing, 3, 0);
        var previous = crossing.HeightAt(FarX, FarZ);
        Run(crossing, 3, there, there, -Slot, 120, crossed: true, eachFrame: () =>
        {
            var height = crossing.HeightAt(FarX, FarZ);
            Assert.True(Math.Abs(height - previous) < 0.1, $"jumped from {previous:F3} to {height:F3}");
            previous = height;
        });

        var native = Surface();
        Run(native, 3, there, there, -Slot, 239, seconds: 1);
        AssertSameSea(native, crossing);
        Assert.NotEqual(here, there);
    }

    [Fact]
    public void CrossesWithoutAJumpIntoARegionWhoseNewWavesAlreadyTookOver()
    {
        var (drawn, theirs) = (Generation(3, 0), Generation(3, 2));
        var crossing = Surface();
        Settle(crossing, 3, 0);
        var previous = crossing.HeightAt(FarX, FarZ);
        Run(crossing, 3, drawn, theirs, 60, 120, seconds: 30, crossed: true, eachFrame: () =>
        {
            var height = crossing.HeightAt(FarX, FarZ);
            Assert.True(Math.Abs(height - previous) < 0.1, $"jumped from {previous:F3} to {height:F3}");
            previous = height;
        });
    }

    [Fact]
    public void DrawsTheSameWavesAsAClientThatJoinedAfterTimeLeptOverASlot()
    {
        var (first, skipped, latest) = (Generation(3, 0), Generation(3, 2), Generation(3, 4));
        var early = Surface();
        Run(early, 3, first, first, -Slot, 0, seconds: 30);
        Run(early, 3, skipped, latest, 2 * Slot, 2 * Slot + 10, seconds: 1);

        var late = Surface();
        Run(late, 3, skipped, latest, 2 * Slot, 2 * Slot + 10, seconds: 1);
        AssertSameSea(late, early);
    }

    [Fact]
    public void HandsOverFromTheSlotsStartHoweverLateItIsSeen()
    {
        var (calm, gale) = (Generation(3, 0), Generation(3, 2));
        var early = Surface();
        Run(early, 3, calm, calm, -Slot, 0, seconds: Slot);
        Run(early, 3, calm, gale, Slot, Slot + 5, seconds: 1);

        var late = Surface();
        Run(late, 3, calm, gale, Slot, Slot + 5, seconds: 1);
        AssertSameSea(late, early);
    }

    [Fact]
    public void RunsAYoungSeasWavesShorterThanAnOldOnesOfTheSameHeight()
    {
        static double LengthOf(double period)
        {
            var surface = Surface();
            var length = 0.0;
            surface.Trains.Changed += change => length = double.Parse(Regex.Match(change, @"(\d+) m long").Groups[1].Value, CultureInfo.InvariantCulture);
            surface.Raise(default, new WaveGeneration(0, period), 0);
            return length;
        }

        Assert.True(LengthOf(3) < LengthOf(8), $"{LengthOf(3)} m young, {LengthOf(8)} m old");
    }

    [Fact]
    public void WhitensLittleWhereTheWavesStandLow()
    {
        var shallows = Surface();
        var sea = Surface();
        shallows.Aim(0.1, 8, foamShare: 0.2);
        sea.Aim(2, 8);

        Assert.True(shallows.Foam.Caps < 0.3 * sea.Foam.Caps, $"caps {shallows.Foam.Caps:F2} in the shallows, {sea.Foam.Caps:F2} at sea");
    }

    [Fact]
    public void FallsCalmWhenTheSeaDoes()
    {
        var surface = Surface();
        Settle(surface, 4, 0);
        surface.Aim(0, 0);
        surface.Advance(121);
        Assert.True(surface.Trains.Calm);
    }

    [Fact]
    public void WhitensWithTheBeaufortForce()
    {
        var calm = Surface();
        var gale = Surface();
        Settle(calm, 0.2, 0, beaufort: 2);
        Settle(gale, 5.5, 0, beaufort: 8);

        Assert.Equal(0, calm.Foam.Caps);
        Assert.True(gale.Foam.Caps > 0.1);
        Assert.True(gale.Foam.Streaks > 0.5);
    }

    [Fact]
    public void BreaksMoreOfItsCrestsInAGale()
    {
        var breeze = Surface();
        var gale = Surface();
        Settle(breeze, 1, 0, beaufort: 4);
        Settle(gale, 5.5, 0, beaufort: 8);

        Assert.True(gale.CapsFold < breeze.CapsFold);
    }

    [Fact]
    public void FoldsMostAtTheCrest()
    {
        var surface = Surface();
        Settle(surface, 3, 0);
        var samples = Enumerable.Range(0, 400).Select(i => (x: FarX + i * 0.37, z: FarZ)).ToArray();
        var crest = samples.MaxBy(at => surface.DisplacementAt(at.x, at.z).Y);
        var trough = samples.MinBy(at => surface.DisplacementAt(at.x, at.z).Y);

        Assert.True(surface.FoldAt(crest.x, crest.z) > surface.FoldAt(trough.x, trough.z));
    }

    [Fact]
    public void FindsTheHeightTheShaderDrawsWhereTheWavesAreLower()
    {
        var surface = Surface();
        Settle(surface, 4, 0.3);
        const double share = 0.4;
        var (shiftX, rise, shiftZ) = surface.DisplacementAt(FarX, FarZ);

        Assert.Equal(share * rise, surface.HeightAt(FarX + share * shiftX, FarZ + share * shiftZ, share), 2);
    }

    [Fact]
    public void ShowsTheShaderTheSamePhaseTheBoatsRideOn()
    {
        var surface = Surface();
        Settle(surface, 3, 0.7);
        var uniforms = new float[surface.Trains.Slots * 4];
        surface.Trains.WriteUniforms(FarX, FarZ, uniforms);
        var (fromCameraX, fromCameraZ) = (37.0, -21.0);

        var rise = 0.0;
        for (var slot = 0; slot < surface.Trains.Slots; slot++)
        {
            var theta = uniforms[slot * 4] * fromCameraX + uniforms[slot * 4 + 1] * fromCameraZ + uniforms[slot * 4 + 3];
            rise += uniforms[slot * 4 + 2] * Math.Sin(theta);
        }
        var shortUniforms = new float[surface.Trains.ShortSlots * 4];
        surface.Trains.WriteShortUniforms(FarX, FarZ, shortUniforms);
        for (var slot = 0; slot < surface.Trains.ShortSlots; slot++)
        {
            var theta = shortUniforms[slot * 4] * fromCameraX + shortUniforms[slot * 4 + 1] * fromCameraZ + shortUniforms[slot * 4 + 3];
            rise += shortUniforms[slot * 4 + 2] * Math.Sin(theta);
        }
        Assert.Equal(surface.DisplacementAt(FarX + fromCameraX, FarZ + fromCameraZ).Y, rise, 3);
    }

    private static double ShortRiseAt(SeaSurface surface, double x, double z) =>
        surface.DisplacementAt(x, z).Y - surface.DisplacementAt(x, z, shortWaves: false).Y;

    private static double ShortHeight(SeaSurface surface)
    {
        var energy = 0.0;
        for (var i = 0; i < 4000; i++)
        {
            var rise = ShortRiseAt(surface, FarX + i * 0.37, FarZ + i * 0.91);
            energy += rise * rise;
        }
        return 4 * Math.Sqrt(energy / 4000);
    }

    [Fact]
    public void DrawsTheShortWavesAsHighAsThePhillipsRangeBetweenTheirLengths()
    {
        var surface = Surface();
        Settle(surface, 3, 0.7);

        var bands = Fixtures.Config().Surface.ShortWaves.Bands;
        var expected = 4 * Math.Sqrt(bands.Sum(band => Fixtures.Config().Surface.ShortWaves.PhillipsConstant / (4 * 4 * Math.PI * Math.PI) * (band.ToBlocks * band.ToBlocks - band.FromBlocks * band.FromBlocks)));
        Assert.InRange(ShortHeight(surface), expected * 0.8, expected * 1.2);
    }

    [Fact]
    public void AStillerSeaThanItsShortWavesCarriesLess()
    {
        var (full, still) = (Surface(), Surface());
        Settle(full, 3, 0.7);
        Settle(still, 0.05, 0.7);

        Assert.True(ShortHeight(still) < ShortHeight(full) * 0.4);
    }

    [Fact]
    public void MovesTheWaterForwardUnderACrestAndBackInATrough()
    {
        var (surface, number, frequency) = OneTrain(2);

        for (var i = 0; i < 12; i++)
        {
            var x = FarX + i * 3.1;
            var rise = surface.DisplacementAt(x, FarZ).Y;
            var (alongX, acrossZ) = Orbital(surface, x, FarZ);

            Assert.Equal(frequency * rise * Math.Exp(number * rise), alongX, 9);
            Assert.Equal(0, acrossZ, 9);
            Assert.Equal(Math.Sign(rise), Math.Sign(alongX));
        }
    }

    [Fact]
    public void FadesWithDepthAsTheWavenumberSays()
    {
        var (surface, number, _) = OneTrain(2);
        var x = FarX + 4;
        var (atSurface, _) = Orbital(surface, x, FarZ);
        var (deeper, _) = Orbital(surface, x, FarZ, depth: 3);

        Assert.Equal(Math.Exp(-number * 3), deeper / atSurface, 9);
    }

    [Fact]
    public void AHullAsLongAsTheWaveFeelsItsWaterCancelOut()
    {
        var (surface, number, _) = OneTrain(2);
        var length = 2 * Math.PI / number;
        var x = FarX + 4;
        var (alone, _) = Orbital(surface, x, FarZ);
        var (halfAWave, _) = Orbital(surface, x, FarZ, side: length / 2);
        var (wholeWave, _) = Orbital(surface, x, FarZ, side: length);

        Assert.Equal(2 / Math.PI, halfAWave / alone, 9);
        Assert.Equal(0, wholeWave, 9);
    }

    [Fact]
    public void CarriesAFloatingBodyDownTheWavesByTheStokesDrift()
    {
        const double height = 2;
        var (surface, number, frequency) = OneTrain(height);
        var amplitude = height / (2 * Math.Sqrt(2));
        var period = 2 * Math.PI / frequency;
        var x = FarX;
        var seconds = 0.0;
        for (; seconds < 200 * period; seconds += Frame)
        {
            surface.Advance(seconds);
            x += Orbital(surface, x, FarZ).X * Frame;
        }

        // Stokes (1847): a^2 omega k a second along the waves' travel at the surface
        Assert.InRange((x - FarX) / seconds / (amplitude * amplitude * frequency * number), 0.95, 1.05);
    }

    [Fact]
    public void CarriesABodyUnderTheSurfaceLess()
    {
        var (surface, number, frequency) = OneTrain(2);
        var period = 2 * Math.PI / frequency;
        var (atSurface, deep) = (FarX, FarX);
        for (var seconds = 0.0; seconds < 200 * period; seconds += Frame)
        {
            surface.Advance(seconds);
            atSurface += Orbital(surface, atSurface, FarZ).X * Frame;
            deep += Orbital(surface, deep, FarZ, depth: 2).X * Frame;
        }

        // held 2 m under the surface it rises and falls with the whole wave but sways with the water 2 m down:
        // half the drift from each, (e^(-2kd) + e^(-kd)) / 2 of the surface's
        var fade = Math.Exp(-number * 2);
        Assert.True(deep - FarX > 0, $"drifted {deep - FarX:F2} m");
        Assert.InRange((deep - FarX) / (atSurface - FarX), 0.95 * (fade * fade + fade) / 2, 1.05 * (fade * fade + fade) / 2);
    }

    [Fact]
    public void AveragesTheWaterAlongAHullLyingAlongTheWavesAndNotAcrossThem()
    {
        var (surface, number, _) = OneTrain(2);
        var length = 2 * Math.PI / number;
        var x = FarX + 4;
        var lift = surface.DisplacementAt(x, FarZ).Y;
        var point = surface.HullVelocityAt(x, FarZ, lift, 0, 0, 0, 0).X;
        // yaw -π/2 lays the hull's length along world X, the waves' travel; yaw 0 lays it along Z, along their crests
        var along = surface.HullVelocityAt(x, FarZ, lift, 0, -Math.PI / 2, length / 2, 0.5).X;
        var across = surface.HullVelocityAt(x, FarZ, lift, 0, 0, length / 2, 0.5).X;

        Assert.Equal(2 / Math.PI, along / point, 9);
        Assert.Equal(Sinc(number * 0.25), across / point, 9);
    }

    [Fact]
    public void AveragesTheWaterOverTheDraft()
    {
        var (surface, number, _) = OneTrain(2);
        var x = FarX + 4;
        var lift = surface.DisplacementAt(x, FarZ).Y;
        var atSurface = surface.HullVelocityAt(x, FarZ, lift, 0, 0, 0, 0).X;
        var deep = surface.HullVelocityAt(x, FarZ, lift, 2, 0, 0, 0).X;

        Assert.Equal((1 - Math.Exp(-number * 2)) / (number * 2), deep / atSurface, 9);
    }

    private static double Sinc(double x) => Math.Sin(x) / x;

    /// <summary>The water's velocity under the drawn surface at a point, averaged over a square as wide as <paramref name="side"/>.</summary>
    private static (double X, double Z) Orbital(SeaSurface surface, double x, double z, double depth = 0, double side = 0) =>
        surface.HullVelocityAt(x, z, surface.DisplacementAt(x, z).Y - depth, 0, 0, side, side);

    /// <summary>One train along +X, settled at once.</summary>
    private static (SeaSurface Surface, double Number, double Frequency) OneTrain(double height)
    {
        var train = Fixtures.OneTrain(height, 16);
        train.Surface.Advance(0);
        return (train.Surface, train.Number, train.Frequency);
    }
}
