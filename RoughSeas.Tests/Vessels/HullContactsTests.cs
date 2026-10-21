using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class HullContactsTests
{
    private static readonly HullShape Hull = new(HalfLength: 2, HalfBeam: 0.5, Depth: 0.6, CentreAlong: 0, CentreAcross: 0, Keel: 0);

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
}
