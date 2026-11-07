using Newtonsoft.Json.Linq;
using RoughSeas.Units;

namespace RoughSeas.Tests.Shipyard;

public class FirstStageTests
{
    private const double Centre = 8;

    private static JObject Shape(string vessel) => JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", $"{vessel}.json")));

    private static JToken Entity(string vessel) => JToken.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vessel", $"{vessel}.json")));

    private static JToken Named(JToken elements, string name) => Find(elements, name) ?? throw new KeyNotFoundException(name);

    private static JToken? Find(JToken elements, string name) =>
        elements.FirstOrDefault(element => element.Value<string>("name") == name)
        ?? elements.Select(element => element["children"] is { } children ? Find(children, name) : null).FirstOrDefault(found => found != null);

    private static IEnumerable<JToken> Boxes(JToken element) =>
        element.Value<string>("name")!.StartsWith("Wound") ? []
        : element["children"] is { } children ? children.SelectMany(Boxes) : [element];

    private static (double[] From, double[] To) Bounds(params JToken[] groups)
    {
        var boxes = groups.SelectMany(Boxes).ToArray();
        double Edge(int axis, string end, Func<IEnumerable<double>, double> pick) => pick(boxes.Select(box => box[end]![axis]!.Value<double>()));
        return ([.. Enumerable.Range(0, 3).Select(axis => Edge(axis, "from", values => values.Min()))],
                [.. Enumerable.Range(0, 3).Select(axis => Edge(axis, "to", values => values.Max()))]);
    }

    [Fact]
    public void ADugoutsBlankIsTheFiveLogsAsTheyLieInTheirBark()
    {
        var elements = Shape("dugout")["elements"]!;
        var (from, to) = Bounds(Named(elements, "Blank0"), Named(elements, "Bark"));

        Assert.Equal([Centre - 2.5 * Voxels.AMetre, 0, 0], from);
        Assert.Equal([Centre + 2.5 * Voxels.AMetre, Voxels.AMetre, Voxels.AMetre], to);
    }

    [Fact]
    public void AnOutriggersFloatJoinsAsTheThreeLogsBesideHer()
    {
        var off = Entity("outrigger")["behaviorConfigs"]!["roughseas:rigging"]!.Value<double>("floatOffVoxels");
        var (from, to) = Bounds(Named(Shape("outrigger")["elements"]!, "FloatLog1"));

        Assert.Equal([Centre - 1.5 * Voxels.AMetre, 0, Centre + off - Voxels.AMetre / 2], from);
        Assert.Equal([Centre + 1.5 * Voxels.AMetre, Voxels.AMetre, Centre + off + Voxels.AMetre / 2], to);
    }

    [Fact]
    public void ABarkSheetIsAsLongAsTheTrunkItIsStrippedFromAndLiesOnTheGround()
    {
        var blocks = Entity("barkcanoe")["behaviorConfigs"]!["roughseas:framing"]!.Value<double>("trunkBlocks");
        var (from, to) = Bounds(Named(Shape("barkcanoe")["elements"]!, "SheetFlat"));
        var curl = Named(Shape("barkcanoe")["elements"]!, "SheetCurlPort");

        Assert.Equal(blocks * Voxels.AMetre, to[0] - from[0], 3);
        Assert.Equal(0, from[1]);
        Assert.True(curl.Value<double>("rotationX") > 0, "a curl lifts off the ground");
    }

    [Fact]
    public void ABarkCanoesGunwaleFrameLiesOnHerSheetUntilItIsRaised()
    {
        var elements = Shape("barkcanoe")["elements"]!;
        var sheet = Bounds(Named(elements, "SheetFlat")).To[1];
        var lying = Enumerable.Range(1, 5).SelectMany(n => Enumerable.Range(n, 10 - n).Select(k => Named(elements, $"Frame{n}At{k}")))
            .SelectMany(Boxes).Select(box => Start(box)[1] + box["from"]![1]!.Value<double>());

        Assert.All(lying, bottom => Assert.InRange(bottom, sheet, sheet + 0.5));
    }

    [Fact]
    public void ACoraclesLatticeLiesWhereThePileOfShortLathsLaysItsFirstLayer()
    {
        var laths = Named(Shape("coracle")["elements"]!, "Lattice")["children"]!.Select(lath => Boxes(lath).First()).ToArray();
        var pile = Pile(2).Take(laths.Length).ToArray();
        var along = laths.Take(7).Select(Start).Select(start => start[2]).ToArray();
        var across = laths.Skip(7).Select(Start).Select(start => start[0]).ToArray();

        Assert.Equal(pile.Take(7).Select(lath => MiddleOf(lath, 2)), along);
        Assert.Equal(pile.Skip(7).Select(lath => MiddleOf(lath, 0)), across);
        Assert.All(laths.Zip(pile), pair => Assert.Equal(pair.Second["from"]![1]!.Value<double>(), Bottom(pair.First), 3));
    }

    private static double Bottom(JToken lath) => Start(lath)[1] + lath["from"]![1]!.Value<double>();

    private static double Top(JToken lath) => Start(lath)[1] + lath["to"]![1]!.Value<double>();

    [Fact]
    public void ACurrachsGunwaleLathsSpreadSomeSixCentimetresASideFromThePileTheyLay()
    {
        var pile = Pile(5).Take(2).Select(lath => MiddleOf(lath, 2)).ToArray();
        var elements = Shape("currach")["elements"]!;
        double Middle(string name)
        {
            var astern = Named(elements, name);
            var board = astern["children"]![0]!;
            return astern["from"]![2]!.Value<double>() + (board["from"]![2]!.Value<double>() + board["to"]![2]!.Value<double>()) / 2;
        }

        var spread = (Middle("GunwaleStarboardAsternFrame") - Middle("GunwalePortAsternFrame") - Math.Abs(pile[1] - pile[0])) / 2;

        Assert.InRange(spread / Voxels.AMetre, 0, 0.07);
    }

    private static double[] Start(JToken box) => box.Parent!.Parent!.Parent!["from"]!.Values<double>().ToArray();

    private static IEnumerable<JToken> Pile(int metres) =>
        JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "shapes", "block", "pile", $"lathpile-{metres}.json")))["elements"]!;

    private static double MiddleOf(JToken box, int axis) => (box["from"]![axis]!.Value<double>() + box["to"]![axis]!.Value<double>()) / 2;
}
