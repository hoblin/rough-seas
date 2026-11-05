using RoughSeas.Vessels;

namespace RoughSeas.Tests.Vessels;

public class SeatingTests
{
    private const double SitDown = 1.55;
    private const double StandUp = 1.44;
    private static readonly Person Paddler = new(7, 65, 10);

    private static double Kg(IReadOnlyList<Person> borne) => borne.Sum(person => person.BodyKg + person.KitKg);

    private static Seating Empty() => new(SitDown, StandUp, []);

    private static Seating LoadedWithPaddler() => new(SitDown, StandUp, [Paddler.Id]);

    [Fact]
    public void SomeoneAboardWhenSheWasLoadedIsBorneInFull()
    {
        var seating = LoadedWithPaddler();

        Assert.Equal(75, Kg(seating.Bear([Paddler], 10)));
        Assert.True(seating.Settled([Paddler.Id]));
    }

    [Fact]
    public void SomeoneAboardWhenSheWasLoadedIsBorneInFullThoughTheyTurnUpAfterHerFirstWeighing()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([], 10);

        Assert.False(seating.Settled([Paddler.Id]));
        Assert.Equal(75, Kg(seating.Bear([Paddler], 11)));
    }

    [Fact]
    public void SomeoneAboardWhenSheWasLoadedSitsDownLikeAnyoneOnceTheyHaveLeft()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([Paddler], 0);
        seating.Bear([], 1);
        seating.Bear([], 1 + StandUp);

        Assert.Equal(0, Kg(seating.Bear([Paddler], 10)), 6);
    }

    [Fact]
    public void ABoardingPersonsWeightComesOnAsTheySitDown()
    {
        var seating = Empty();

        Assert.Equal(0, Kg(seating.Bear([Paddler], 1)), 6);
        Assert.Equal(37.5, Kg(seating.Bear([Paddler], 1 + SitDown / 2)), 6);
        Assert.False(seating.Settled([Paddler.Id]));
        Assert.Equal(75, Kg(seating.Bear([Paddler], 1.1 + SitDown)), 6);
        Assert.True(seating.Settled([Paddler.Id]));
    }

    [Fact]
    public void ALeavingPersonsWeightComesOffAsTheyStandUp()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([Paddler], 0);

        Assert.Equal(75, Kg(seating.Bear([], 1)), 6);
        Assert.Equal(37.5, Kg(seating.Bear([], 1 + StandUp / 2)), 6);
        Assert.Empty(seating.Bear([], 1 + StandUp));
        Assert.True(seating.Settled([]));
    }

    [Fact]
    public void SomeoneWhoSitsBackDownHalfwayUpTakesUpFromWhereTheyWere()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([Paddler], 0);
        seating.Bear([], 1);
        double halfwayUp = Kg(seating.Bear([], 1 + StandUp / 2));

        Assert.Equal(halfwayUp, Kg(seating.Bear([Paddler], 1 + StandUp / 2)), 6);
        Assert.Equal(75, Kg(seating.Bear([Paddler], 1 + StandUp / 2 + SitDown / 2)), 6);
    }

    [Fact]
    public void ANewcomerIsNoticedBeforeTheirWeightIsTaken()
    {
        var seating = Empty();
        seating.Bear([], 0);

        Assert.False(seating.Settled([Paddler.Id]));
    }

    [Fact]
    public void SheIsUnsettledUntilAWeighingHasTakenTheWholeWeight()
    {
        var seating = Empty();
        seating.Bear([Paddler], 1);
        seating.Bear([Paddler], SitDown);

        Assert.False(seating.Settled([Paddler.Id]));
    }

    [Fact]
    public void SomeoneJustSeatedIsNotYetSettled()
    {
        var seating = Empty();
        seating.Bear([Paddler], 1);

        Assert.False(seating.Settled([Paddler.Id]));
    }

    [Fact]
    public void SomeoneJustUpIsNotYetSettled()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([Paddler], 0);
        seating.Bear([], 1);

        Assert.False(seating.Settled([]));
    }

    [Fact]
    public void KitPutOnWhileSeatedIsBorneAtOnce()
    {
        var seating = LoadedWithPaddler();
        seating.Bear([Paddler], 0);

        Assert.Equal(95, Kg(seating.Bear([Paddler with { KitKg = 30 }], 1)), 6);
    }
}
