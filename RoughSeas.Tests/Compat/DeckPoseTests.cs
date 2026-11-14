using System.Numerics;
using RoughSeas.Compat.WalkableEntities;

namespace RoughSeas.Tests.Compat;

public class DeckPoseTests
{
    private const float PivotY = 0.6f;
    private const float Precision = 1e-5f;

    private static readonly DeckPose Afloat = new(100, 110, -40, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.2f));

    [Fact]
    public void ACalmSeaLeavesTheDeckWhereItLies()
    {
        var drawn = Afloat.Drawn(PivotY, 0, 0, 0, 0);

        Assert.Equal(Afloat.X, drawn.X, Precision);
        Assert.Equal(Afloat.Y, drawn.Y, Precision);
        Assert.Equal(Afloat.Z, drawn.Z, Precision);
        Assert.Equal(Afloat.Rotation, drawn.Rotation);
    }

    [Fact]
    public void TheSwellLiftsTheWholeDeck()
    {
        var drawn = Afloat.Drawn(PivotY, 0, 0, 0, 0.4);

        var bow = drawn.ToWorld(new Vector3(-3, 0.2f, 0.5f));
        var plainBow = Afloat.ToWorld(new Vector3(-3, 0.2f, 0.5f));
        Assert.Equal(plainBow.Y + 0.4, bow.Y, Precision);
        Assert.Equal(plainBow.X, bow.X, Precision);
    }

    [Fact]
    public void TheHullHeelsAboutTheMiddleOfItsSelectionBox()
    {
        var pivot = new Vector3(0, PivotY, 0);
        var drawn = Afloat.Drawn(PivotY, 0.2f, 0.05f, -0.1f, 0.3);

        var turned = drawn.ToWorld(pivot);
        var level = Afloat.ToWorld(pivot);
        Assert.Equal(level.X, turned.X, Precision);
        Assert.Equal(level.Y + 0.3, turned.Y, Precision);
        Assert.Equal(level.Z, turned.Z, Precision);
    }

    [Fact]
    public void ARollHeelsTheGunwalesOppositeWays()
    {
        var drawn = Afloat.Drawn(PivotY, 0.2f, 0, 0, 0);

        var port = drawn.ToWorld(new Vector3(0, PivotY, -1)).Y;
        var starboard = drawn.ToWorld(new Vector3(0, PivotY, 1)).Y;
        Assert.Equal(-(starboard - Afloat.Y - PivotY), port - Afloat.Y - PivotY, Precision);
        Assert.NotEqual(port, starboard);
    }

    [Fact]
    public void APlayerOnTheDrawnDeckStandsOnTheSamePlankOfTheLevelOne()
    {
        var drawn = Afloat.Drawn(PivotY, 0.15f, 0, 0.08f, -0.5);
        var plank = new Vector3(-2, 0.125f, 0.75f);
        var (x, y, z) = drawn.ToWorld(plank);

        var local = drawn.ToLocal(x, y, z);
        var (levelX, levelY, levelZ) = Afloat.ToWorld(local);
        var level = Afloat.ToWorld(plank);
        Assert.Equal(level.X, levelX, 1e-4);
        Assert.Equal(level.Y, levelY, 1e-4);
        Assert.Equal(level.Z, levelZ, 1e-4);
    }
}
