using RoughSeas.Vessels;
using Vintagestory.API.Common;

namespace RoughSeas.Tests.Vessels;

public class HullDamageTests
{
    private static readonly ShapeElement[] Hull =
    [
        Board("SidePort1", Wound("PortBow", ""), Wound("Bottom", "")),
        Board("Blank3", Wound("PortBow", "Blank3"))
    ];

    [Fact]
    public void APlaceShowsTheWoundOfTheStageItsCrackReachedOnEveryLevel()
    {
        var hidden = HullDamage.Unreached(Hull, place => place == HullPlace.PortBow ? 3 : 0).ToList();

        Assert.DoesNotContain("WoundPortBowSplit", hidden);
        Assert.DoesNotContain("WoundPortBowSplitBlank3", hidden);
        Assert.Contains("WoundPortBowCracked", hidden);
        Assert.Contains("WoundPortBowStoveInBlank3", hidden);
    }

    [Fact]
    public void ASoundPlaceShowsNoWound()
    {
        var hidden = HullDamage.Unreached(Hull, _ => 0).ToList();

        Assert.Equal(3 * HullDamage.WoundForms.Length, hidden.Count);
    }

    [Fact]
    public void AScratchIsAlreadyDrawn()
    {
        Assert.DoesNotContain("WoundBottomScratched", HullDamage.Unreached(Hull, _ => 1));
    }

    [Fact]
    public void HidesAWoundWholeNotPieceByPiece()
    {
        Assert.DoesNotContain("WoundBottomStoveInx1", HullDamage.Unreached(Hull, _ => 0));
    }

    private static ShapeElement Board(string name, params ShapeElement[] wounds) => new() { Name = name, Children = wounds };

    private static ShapeElement Wound(string place, string level) => new()
    {
        Name = $"Wound{place}{level}",
        Children = HullDamage.WoundForms.Select(form => new ShapeElement
        {
            Name = $"Wound{place}{form}{level}",
            Children = [new ShapeElement { Name = $"Wound{place}{form}{level}x1" }]
        }).ToArray()
    };
}
