using Newtonsoft.Json.Linq;

namespace RoughSeas.Tests.Shipyard;

public class FirstStageTests
{
    private const double VoxelsAMetre = 16;
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

    private static (double[] From, double[] To) Bounds(JToken group)
    {
        var boxes = Boxes(group).ToArray();
        double Edge(int axis, string end, Func<IEnumerable<double>, double> pick) => pick(boxes.Select(box => box[end]![axis]!.Value<double>()));
        return ([.. Enumerable.Range(0, 3).Select(axis => Edge(axis, "from", values => values.Min()))],
                [.. Enumerable.Range(0, 3).Select(axis => Edge(axis, "to", values => values.Max()))]);
    }

    [Fact]
    public void ADugoutsBlankIsTheFiveLogsAsTheyLie()
    {
        var (from, to) = Bounds(Named(Shape("dugout")["elements"]!, "Blank0"));

        Assert.Equal([Centre - 2.5 * VoxelsAMetre, 0, 0], from);
        Assert.Equal([Centre + 2.5 * VoxelsAMetre, VoxelsAMetre, VoxelsAMetre], to);
    }

    [Fact]
    public void AnOutriggersFloatJoinsAsTheThreeLogsBesideHer()
    {
        var off = Entity("outrigger")["behaviorConfigs"]!["roughseas:rigging"]!.Value<double>("floatOffMetres") * VoxelsAMetre;
        var (from, to) = Bounds(Named(Shape("outrigger")["elements"]!, "FloatLog1"));

        Assert.Equal([Centre - 1.5 * VoxelsAMetre, 0, Centre + off - VoxelsAMetre / 2], from);
        Assert.Equal([Centre + 1.5 * VoxelsAMetre, VoxelsAMetre, Centre + off + VoxelsAMetre / 2], to);
    }

    [Fact]
    public void ABarkSheetIsAsLongAsTheTrunkItIsStrippedFromAndLiesOnTheGround()
    {
        var blocks = Entity("barkcanoe")["behaviorConfigs"]!["roughseas:framing"]!.Value<double>("trunkBlocks");
        var flat = Named(Shape("barkcanoe")["elements"]!, "SheetFlat");
        var curl = Named(Shape("barkcanoe")["elements"]!, "SheetCurlPort");

        Assert.Equal(blocks * VoxelsAMetre, flat["to"]![0]!.Value<double>() - flat["from"]![0]!.Value<double>());
        Assert.Equal(0, flat["from"]![1]!.Value<double>());
        Assert.True(curl.Value<double>("rotationX") > 0, "a curl lifts off the ground");
    }

    [Fact]
    public void ACoraclesLatticeLiesOnTheGroundAsTheBedSpacesItsLaths()
    {
        var layout = Entity("coracle")["behaviorConfigs"]!["roughseas:covering"]!["layout"]!;
        var laths = Named(Shape("coracle")["elements"]!, "Lattice")["children"]!.Select(lath => Boxes(lath).First()).ToArray();
        var along = laths.Take(7).Select(Start).Select(start => start[2]).ToArray();
        var across = laths.Skip(7).Select(Start).Select(start => start[0]).ToArray();

        Assert.Equal(Spaced(layout.Value<double>("alongSpacingMetres")), along);
        Assert.Equal(Spaced(layout.Value<double>("acrossSpacingMetres")), across);
        Assert.All(laths.Take(7), lath => Assert.Equal(0, Bottom(lath), 3));
        Assert.All(laths.Skip(7), lath => Assert.Equal(Top(laths[0]), Bottom(lath), 3));
    }

    private static double Bottom(JToken lath) => Start(lath)[1] + lath["from"]![1]!.Value<double>();

    private static double Top(JToken lath) => Start(lath)[1] + lath["to"]![1]!.Value<double>();

    [Fact]
    public void ACurrachsGunwaleLathsLieAsFarApartAsHerPairIsLaid()
    {
        var beam = Entity("currach")["behaviorConfigs"]!["roughseas:covering"]!["layout"]!.Value<double>("beamMetres");
        var elements = Shape("currach")["elements"]!;
        double Middle(string name)
        {
            var astern = Named(elements, name);
            var board = astern["children"]![0]!;
            return astern["from"]![2]!.Value<double>() + (board["from"]![2]!.Value<double>() + board["to"]![2]!.Value<double>()) / 2;
        }

        Assert.Equal(beam, (Middle("GunwaleStarboardAsternFrame") - Middle("GunwalePortAsternFrame")) / VoxelsAMetre, 2);
    }

    private static double[] Start(JToken box) => box.Parent!.Parent!.Parent!["from"]!.Values<double>().ToArray();

    private static double[] Spaced(double metres) => [.. Enumerable.Range(-3, 7).Select(n => Centre + n * metres * VoxelsAMetre)];
}
