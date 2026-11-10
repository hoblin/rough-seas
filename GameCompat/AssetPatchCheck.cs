using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed record AssetPatchResult(IReadOnlyList<string> Reasons, IReadOnlyList<BehaviourChange> Changes);

internal sealed class AssetPatchCheck(GameRelease release, GameRelease current, string repository)
{
    private const string GameDomain = "game:";

    public AssetPatchResult Run()
    {
        var reasons = new List<string>();
        var changes = new List<BehaviourChange>();
        var patched = new Dictionary<string, (JToken? Document, JToken? Baseline)>();
        var folder = Path.Combine(repository, "RoughSeas", "assets", "roughseas", "patches");

        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            foreach (var operation in Parse(File.ReadAllText(file)).Children<JObject>())
            {
                var target = (string)operation["file"]!;
                var path = (string)operation["path"]!;
                var kind = (string)operation["op"]!;
                if (!target.StartsWith(GameDomain)) continue;

                if (!patched.TryGetValue(target, out var documents))
                {
                    var asset = WithExtension(target[GameDomain.Length..]);
                    documents = (Load(release, asset), Load(current, asset));
                    patched[target] = documents;
                }

                if (documents.Document == null)
                {
                    reasons.Add($"{name}: {target} is missing");
                    continue;
                }

                if (documents.Baseline != null && release.Version != current.Version)
                {
                    var (seen, expected) = (At(documents.Document, path), At(documents.Baseline, path));
                    if (seen != expected) changes.Add(Changed(name, target, path, kind, expected, seen));
                    Apply(documents.Baseline, kind, path, operation["value"]);
                }

                if (Apply(documents.Document, kind, path, operation["value"]) is { } reason) reasons.Add($"{name}: {target} {path}: {reason}");
            }
        }
        return new AssetPatchResult(reasons, changes);
    }

    internal static JToken Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        return JToken.Load(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore });
    }

    internal static string? Apply(JToken document, string operation, string path, JToken? value)
    {
        var keys = Keys(path);
        var parent = document;
        foreach (var key in keys[..^1])
        {
            parent = Child(parent, key);
            if (parent == null) return "its parent is missing";
        }

        var last = keys[^1];
        return (operation, parent) switch
        {
            ("add", JObject) when last == "-" || int.TryParse(last, out _) => "it is not an array",
            ("add", JObject map) => Set(map, last, value),
            ("add", JArray list) => Insert(list, last, value),
            ("replace", _) when Child(parent, last) is { } existing => Replaced(existing, value),
            ("replace", _) => "it is missing",
            _ => $"can't {operation} here",
        };
    }

    internal static string At(JToken document, string path)
    {
        var seen = new List<string>();
        var node = document;
        var keys = Keys(path);
        for (var index = 0; index < keys.Length && node != null; index++)
        {
            node = Child(node, keys[index]);
            if (node != null && (index == keys.Length - 1 || int.TryParse(keys[index], out _))) seen.Add(node.ToString(Formatting.Indented));
        }
        return string.Join("\n", seen);
    }

    private static BehaviourChange Changed(string name, string target, string path, string kind, string expected, string seen) =>
        new(new Reliance(target, path, $"{name} does {kind} at this path", name), BehaviourCheck.Stamp([expected], [seen]), MethodSource.TextDiff(seen, expected));

    private static JToken? Load(GameRelease from, string asset) =>
        from.FindAsset(asset) is { } path ? Parse(File.ReadAllText(path)) : null;

    private static string[] Keys(string path) => path.TrimStart('/').Split('/').Select(key => key.Replace("~1", "/").Replace("~0", "~")).ToArray();

    private static string WithExtension(string path) => path.EndsWith(".json") ? path : path + ".json";

    private static JToken? Child(JToken parent, string key) => parent switch
    {
        JObject map => map[key],
        JArray list when int.TryParse(key, out var index) && index >= 0 && index < list.Count => list[index],
        _ => null,
    };

    private static string? Set(JObject map, string key, JToken? value)
    {
        map[key] = value?.DeepClone();
        return null;
    }

    private static string? Insert(JArray list, string key, JToken? value)
    {
        if (key == "-")
        {
            list.Add(value?.DeepClone()!);
            return null;
        }
        if (!int.TryParse(key, out var index) || index < 0 || index > list.Count) return $"index {key} is past the end of {list.Count}";
        list.Insert(index, value?.DeepClone()!);
        return null;
    }

    private static string? Replaced(JToken existing, JToken? value)
    {
        existing.Replace(value?.DeepClone()!);
        return null;
    }
}
