using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class GameCodes
{
    private static readonly string[] Registries = ["blocktypes/", "itemtypes/", "entities/"];

    private readonly GameRelease release;
    private readonly List<(string Code, JObject Type)> types = [];

    public GameCodes(GameRelease release)
    {
        this.release = release;
        foreach (var file in release.AssetFiles.Where(file => file.EndsWith(".json") && Registries.Any(file.StartsWith)))
        {
            if (Load(file) is JObject { } type && (string?)type["code"] is { } code) types.Add((code, type));
        }
    }

    public bool Contains(string reference)
    {
        var wanted = Wildcard.Of(reference);
        return types.Where(entry => Related(entry.Code, reference)).Any(entry => Expand(entry.Type, Property).Any(wanted.IsMatch));
    }

    internal static IEnumerable<string> Expand(JObject type, Func<string, IEnumerable<string>> property)
    {
        var code = (string)type["code"]!;
        var groups = type["variantgroups"]?.Children<JObject>().ToList() ?? [];
        var crossed = groups.Where(group => (string?)group["combine"] != "add")
            .Aggregate((IEnumerable<string>)[code], (codes, group) => codes.SelectMany(prefix => States(group, property).Select(state => $"{prefix}-{state}")));
        var added = groups.Where(group => (string?)group["combine"] == "add").SelectMany(group => States(group, property)).Select(state => $"{code}-{state}");

        var allowed = Patterns(type["allowedVariants"]);
        var skipped = Patterns(type["skipVariants"]);
        return crossed.Concat(added).Where(variant => (allowed.Count == 0 || allowed.Any(pattern => pattern.IsMatch(variant))) && !skipped.Any(pattern => pattern.IsMatch(variant)));
    }

    private static List<Regex> Patterns(JToken? patterns) => patterns?.Values<string>().Select(pattern => Wildcard.Of(pattern!)).ToList() ?? [];

    private static IEnumerable<string> States(JObject group, Func<string, IEnumerable<string>> property)
    {
        var listed = group["states"]?.Values<string>().OfType<string>() ?? [];
        return (string?)group["loadFromProperties"] is { } source ? listed.Concat(property(source)) : listed;
    }

    private static bool Related(string code, string reference)
    {
        var known = reference.Split('*')[0];
        return known.StartsWith(code) || code.StartsWith(known);
    }

    private IEnumerable<string> Property(string source)
    {
        var path = source.Contains(':') ? source[(source.IndexOf(':') + 1)..] : source;
        return (Load($"worldproperties/{path}.json") as JObject)?["variants"]?.Children<JObject>().Select(variant => (string)variant["code"]!) ?? [];
    }

    private JToken? Load(string file)
    {
        if (release.FindAsset(file) is not { } path) return null;
        try
        {
            return AssetPatchCheck.Parse(File.ReadAllText(path));
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}

internal static class Wildcard
{
    public static Regex Of(string pattern) => new("^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$", RegexOptions.CultureInvariant);
}
