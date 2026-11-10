using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class AssetPatchCheck(GameRelease release, string repository)
{
    private const string GameDomain = "game:";

    public IEnumerable<string> Run()
    {
        var patched = new Dictionary<string, JToken?>();
        var folder = Path.Combine(repository, "RoughSeas", "assets", "roughseas", "patches");
        foreach (var file in Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            foreach (var operation in Parse(File.ReadAllText(file)).Children<JObject>())
            {
                var target = (string)operation["file"]!;
                var path = (string)operation["path"]!;
                if (!target.StartsWith(GameDomain)) continue;

                if (!patched.TryGetValue(target, out var document))
                {
                    var asset = release.FindAsset(WithExtension(target[GameDomain.Length..]));
                    document = asset == null ? null : Parse(File.ReadAllText(asset));
                    patched[target] = document;
                }

                if (document == null) yield return $"{name}: {target} is missing";
                else if (Apply(document, (string)operation["op"]!, path, operation["value"]) is { } reason) yield return $"{name}: {target} {path}: {reason}";
            }
        }
    }

    internal static string? Apply(JToken document, string operation, string path, JToken? value)
    {
        var keys = path.TrimStart('/').Split('/').Select(key => key.Replace("~1", "/").Replace("~0", "~")).ToArray();
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

    private static JToken Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json));
        return JToken.Load(reader, new JsonLoadSettings { CommentHandling = CommentHandling.Ignore });
    }

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
