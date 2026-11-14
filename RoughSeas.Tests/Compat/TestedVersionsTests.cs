using RoughSeas.Compat;

namespace RoughSeas.Tests.Compat;

public class TestedVersionsTests
{
    private static readonly TestedVersions Range = new() { From = "1.8.5", To = "1.9.2" };

    [Theory]
    [InlineData("1.8.5")]
    [InlineData("1.9.0")]
    [InlineData("1.9.2")]
    public void CoversTheTestedRange(string version) => Assert.True(Range.Covers(version));

    [Theory]
    [InlineData("1.8.4")]
    [InlineData("1.9.3")]
    [InlineData("2.0.0")]
    [InlineData("not a version")]
    public void LeavesOtherVersionsAlone(string version) => Assert.False(Range.Covers(version));
}
