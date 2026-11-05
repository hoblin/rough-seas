using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class HullContactsTests
{
    private static readonly HullShape Hull = new(HalfLengthMetres: 2, HalfBeamMetres: 0.5, DepthMetres: 0.6, CentreAlongMetres: 0, CentreAcrossMetres: 0, KeelMetres: 0);

    [Fact]
    public void TwoHullsAfloatTogetherMeet()
    {
        Assert.True(HullContacts.Level(110, Hull, 110.2, Hull));
    }

    [Fact]
    public void AHullPassesOverOneSunkBelowIt()
    {
        Assert.False(HullContacts.Level(110, Hull, 105, Hull));
    }

    [Fact]
    public void HullsSideBySideLieCloseEnoughToHaveMet()
    {
        Assert.True(HullContacts.Within(0, 0, Hull, 4, 0, Hull, slack: 0));
    }

    [Fact]
    public void AHullFarOffCannotHaveBeenRammed()
    {
        Assert.False(HullContacts.Within(0, 0, Hull, 30, 0, Hull, slack: 2));
    }
}
