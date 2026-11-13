using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed record GameReferenceResult(IReadOnlyList<string> Reasons, IReadOnlyList<BehaviourChange> Changes);

internal sealed class GameReferenceCheck(GameRelease release, string repository)
{
    private const string Domain = "game:";

    private static readonly Regex Placeholder = new(@"\{([^}]*)\}", RegexOptions.Compiled);

    public GameReferenceResult Run(ModuleDefinition mod)
    {
        var codes = new GameCodes(release);
        var files = AssetStems(release.AssetFiles);
        var texts = LangKeys(release);
        var reasons = new List<string>();
        var changes = new List<BehaviourChange>();
        foreach (var (reference, source) in References(mod))
        {
            var path = reference[Domain.Length..];
            if (reference.Contains('{')) changes.Add(Unenumerable(reference, source));
            else if (!FileExists(path, files) && !codes.Contains(path) && !texts.Contains(path)) reasons.Add($"{reference} is missing ({source})");
        }
        return new GameReferenceResult(reasons, changes);
    }

    private static HashSet<string> LangKeys(GameRelease release) =>
        release.FindAsset("lang/en.json") is { } english
            ? AssetPatchCheck.Parse(File.ReadAllText(english)).Children<JProperty>().Select(text => text.Name).ToHashSet()
            : [];

    internal static bool FileExists(string wanted, HashSet<string> stems)
    {
        var stem = WithoutExtension(wanted);
        if (!stem.Contains('*')) return stems.Contains(stem);
        var pattern = Wildcard.Of(stem);
        return stems.Any(pattern.IsMatch);
    }

    internal static HashSet<string> AssetStems(IEnumerable<string> files)
    {
        var stems = new HashSet<string>();
        foreach (var stem in files.Select(WithoutExtension))
        {
            foreach (var form in new[] { stem, stem.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9') })
            {
                stems.Add(form);
                if (form.Contains('/')) stems.Add(form[(form.IndexOf('/') + 1)..]);
            }
        }
        return stems;
    }

    internal static IEnumerable<string> Mentions(JToken document)
    {
        var groups = (document as JObject)?["variantgroups"]?.Children<JObject>().Where(group => group["states"] != null)
            .ToDictionary(group => (string)group["code"]!, group => group["states"]!.Values<string>().OfType<string>().ToList()) ?? [];

        foreach (var token in ((JContainer)document).DescendantsAndSelf())
        {
            switch (token)
            {
                case JValue { Type: JTokenType.String, Value: string text }:
                    foreach (var mention in Concrete(text, token, groups)) yield return mention;
                    break;
                case JProperty property:
                    yield return property.Name;
                    break;
            }
        }
    }

    private static IEnumerable<string> Concrete(string text, JToken token, Dictionary<string, List<string>> groups)
    {
        if (text.Contains('*') && token.Parent is JProperty { Name: "code", Parent: JObject owner } && owner["allowedVariants"] is JArray allowed)
        {
            return allowed.Values<string>().OfType<string>().Select(variant => text.Replace("*", variant));
        }

        return Placeholder.Matches(text).Select(match => match.Groups[1].Value).Distinct().Where(groups.ContainsKey)
            .Aggregate((IEnumerable<string>)[text], (mentions, name) => mentions.SelectMany(mention => groups[name].Select(state => mention.Replace($"{{{name}}}", state))));
    }

    private static BehaviourChange Unenumerable(string reference, string source) =>
        new(new Reliance(source, reference, "this reference's placeholder has values we can't enumerate", source), BehaviourCheck.Stamp([reference], []));

    private static string WithoutExtension(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot > path.LastIndexOf('/') ? path[..dot] : path;
    }

    private Dictionary<string, string> References(ModuleDefinition mod)
    {
        var found = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "RoughSeas", "assets"), "*.json", SearchOption.AllDirectories).Where(file => !file.Contains("/lang/")))
        {
            foreach (var reference in Mentions(AssetPatchCheck.Parse(File.ReadAllText(file))).Where(IsReference))
            {
                found.TryAdd(reference, Path.GetRelativePath(repository, file));
            }
        }

        foreach (var method in mod.GetTypes().SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            foreach (var text in method.Body.Instructions.Where(instruction => instruction.OpCode.Code == Code.Ldstr).Select(instruction => (string)instruction.Operand).Where(text => IsReference(text) && !text.EndsWith('-')))
            {
                found.TryAdd(text, method.FullName);
            }
        }
        return found;
    }

    private static bool IsReference(string text) => text.StartsWith(Domain) && text.Length > Domain.Length && !text.Contains(' ');
}
