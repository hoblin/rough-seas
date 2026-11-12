using Newtonsoft.Json.Linq;
using RoughSeas.Soundscape;

namespace RoughSeas.Tests.Shipyard;

public class WedgeTests
{
    private const float FallBeginsSeconds = 1.62f;

    private static readonly SoundCut Crack = JObject.Parse(File.ReadAllText(Path.Combine(Repository.Mod, "assets", "roughseas", "blocktypes", "wedge.json")))
        ["attributes"]!["crack"]!.ToObject<SoundCut>()!;

    [Fact]
    public void ASplitSoundsVanillasTreeFallOnlyUpToTheFall()
    {
        Assert.Equal("game:sounds/effect/treefell", Crack.Location);
        Assert.InRange(Crack.UntilSeconds, 1.54f, FallBeginsSeconds);
    }
}
