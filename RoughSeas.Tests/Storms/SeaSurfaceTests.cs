using RoughSeas.Storms;

namespace RoughSeas.Tests.Storms;

public class SeaSurfaceTests
{
    private const double FarX = 512031.25;
    private const double FarZ = 498877.5;
    private const double Frame = 1 / 60.0;
    private const int FramesPerAim = 30;

    private static SeaSurface Surface() => new(Fixtures.Config().Surface, new Waves(Fixtures.Config().Waves));

    private static void Settle(SeaSurface surface, double height, double toward, double from = 0, double seconds = 120, double beaufort = 6)
    {
        for (var frame = 0; frame * Frame < seconds; frame++)
        {
            if (frame % FramesPerAim == 0) surface.Aim(height, toward, beaufort);
            surface.Advance(from + frame * Frame);
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
        surface.Aim(5, 0, 8);
        surface.Advance(0);
        var previous = surface.HeightAt(FarX, FarZ);
        for (var frame = 1; frame < 1200; frame++)
        {
            if (frame % FramesPerAim == 0) surface.Aim(5, frame * 0.0005, 8);
            surface.Advance(frame * Frame);
            var height = surface.HeightAt(FarX, FarZ);
            Assert.True(Math.Abs(height - previous) < 0.1, $"frame {frame} jumped from {previous:F3} to {height:F3}");
            previous = height;
        }
    }

    [Fact]
    public void RaisesNewWavesWhenTheWindVeersAndLetsTheOldOnesFade()
    {
        var surface = Surface();
        var changes = 0;
        surface.Changed += _ => changes++;
        Settle(surface, 3, 0);
        Settle(surface, 3, 0.1, from: 120);
        Assert.Equal(1, changes);

        Settle(surface, 3, Math.PI / 2, from: 240);
        Assert.Equal(2, changes);
        Assert.Equal(3, surface.DrawnHeight, 2);
    }

    [Fact]
    public void KeepsItsHeightWhileNewWavesTakeOverFromOld()
    {
        var surface = Surface();
        Settle(surface, 5.5, 0);
        for (var frame = 0; frame * Frame < 60; frame++)
        {
            if (frame % FramesPerAim == 0) surface.Aim(5.5, Math.PI / 2, 8);
            surface.Advance(120 + frame * Frame);
            Assert.True(surface.DrawnHeight > 0.9 * 5.5, $"at {frame * Frame:F1} s the sea drew {surface.DrawnHeight:F2} m");
        }
    }

    [Fact]
    public void FallsCalmWhenTheSeaDoes()
    {
        var surface = Surface();
        Settle(surface, 4, 0);
        Settle(surface, 0, 0, from: 120);
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
    public void FindsTheHeightTheShaderDrawsNearTheShore()
    {
        var surface = Surface();
        Settle(surface, 4, 0.3);
        const double openSea = 0.4;
        var (shiftX, rise, shiftZ) = surface.DisplacementAt(FarX, FarZ);

        Assert.Equal(openSea * rise, surface.HeightAt(FarX + openSea * shiftX, FarZ + openSea * shiftZ, openSea), 2);
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
