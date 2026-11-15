using RoughSeas.Compat;

namespace RoughSeas.Tests.Compat;

public class ReachTests
{
    [Fact]
    public void FindsWhatIsThere()
    {
        Assert.Equal(typeof(Probe), Reach.Type(typeof(Probe).FullName!));
        Assert.Equal(nameof(Probe.Field), Reach.Field(typeof(Probe), nameof(Probe.Field)).Name);
        Assert.Equal(nameof(Probe.Method), Reach.Method(typeof(Probe), nameof(Probe.Method), typeof(int)).Name);
    }

    [Fact]
    public void AMissingTypeIsAMissingMember() =>
        Assert.ThrowsAny<MissingMemberException>(() => Reach.Type("NoSuchMod.NoSuchType"));

    [Fact]
    public void AMissingFieldIsAMissingMember() =>
        Assert.ThrowsAny<MissingMemberException>(() => Reach.Field(typeof(Probe), "gone"));

    [Fact]
    public void AFieldOfAnotherTypeIsAMissingMember()
    {
        Assert.Equal(nameof(Probe.Field), Reach.Field(typeof(Probe), nameof(Probe.Field), typeof(int)).Name);
        Assert.ThrowsAny<MissingMemberException>(() => Reach.Field(typeof(Probe), nameof(Probe.Field), typeof(float)));
    }

    [Fact]
    public void AMethodWithOtherParametersIsAMissingMember() =>
        Assert.ThrowsAny<MissingMemberException>(() => Reach.Method(typeof(Probe), nameof(Probe.Method), typeof(string)));

    [Fact]
    public void AMethodReturningAnotherTypeIsAMissingMember()
    {
        var method = Reach.Method(typeof(Probe), nameof(Probe.Method), typeof(int));

        Assert.Equal(method, method.Returning(typeof(int)));
        Assert.ThrowsAny<MissingMemberException>(() => method.Returning(typeof(string)));
    }

    private sealed class Probe
    {
        public int Field;

        public int Method(int value) => value + Field;
    }
}
