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
                surface.Aim(height, latest.TowardRadians, beaufort);
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
        Assert.Equal(5, surface.DrawnHeight, 2);
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
        surface.Changed += _ => changes++;
        Settle(surface, 3.7, 0);
        Run(surface, 3.7, Generation(3.7, 0), Generation(3.7, 0.05), Slot, Slot);

        Assert.Equal(1, changes);
        Assert.Equal(0, surface.WavesTowardRadians);
    }

    [Fact]
    public void RaisesNewWavesWhenTheSeaTurnsAndLetsTheOldOnesFade()
    {
        var surface = Surface();
        var changes = 0;
        surface.Changed += _ => changes++;
        Settle(surface, 3, 0);
        Run(surface, 3, Generation(3, 0), Generation(3, 1.75), Slot, Slot);

        Assert.Equal(2, changes);
        Assert.Equal(1.75, surface.WavesTowardRadians, tolerance: Math.PI / 18);
        Assert.Equal(3, surface.DrawnHeight, 2);
    }

    [Fact]
    public void KeepsItsHeightWhileNewWavesTakeOverFromOld()
    {
        var surface = Surface();
        Settle(surface, 5.5, 0);
        Run(surface, 5.5, Generation(5.5, 0), Generation(5.5, Math.PI / 2), Slot, Slot, seconds: 60, beaufort: 8,
            eachFrame: () => Assert.True(surface.DrawnHeight > 0.9 * 5.5, $"the sea drew {surface.DrawnHeight:F2} m"));
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
            surface.Changed += change => length = double.Parse(Regex.Match(change, @"(\d+) m long").Groups[1].Value, CultureInfo.InvariantCulture);
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
        shallows.Aim(0.1, 0, 8, foamShare: 0.2);
        sea.Aim(2, 0, 8);

        Assert.True(shallows.Foam.Caps < 0.3 * sea.Foam.Caps, $"caps {shallows.Foam.Caps:F2} in the shallows, {sea.Foam.Caps:F2} at sea");
    }

    [Fact]
    public void FallsCalmWhenTheSeaDoes()
    {
        var surface = Surface();
        Settle(surface, 4, 0);
        surface.Aim(0, 0, 0);
        surface.Advance(121);
        Assert.True(surface.Calm);
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
        var uniforms = new float[surface.Slots * 4];
        surface.WriteUniforms(FarX, FarZ, uniforms);
        var (fromCameraX, fromCameraZ) = (37.0, -21.0);

        var rise = 0.0;
        for (var slot = 0; slot < surface.Slots; slot++)
        {
            var theta = uniforms[slot * 4] * fromCameraX + uniforms[slot * 4 + 1] * fromCameraZ + uniforms[slot * 4 + 3];
            rise += uniforms[slot * 4 + 2] * Math.Sin(theta);
        }
        Assert.Equal(surface.DisplacementAt(FarX + fromCameraX, FarZ + fromCameraZ).Y, rise, 3);
    }
}
