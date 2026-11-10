using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;

namespace GameCompat;

internal sealed class GameReferenceCheck(GameRelease release, string repository)
{
    private const string Domain = "game:";

    private static readonly Regex Placeholder = new(@"\{[^}]*\}", RegexOptions.Compiled);

    public IEnumerable<string> Run(ModuleDefinition mod)
    {
        var codes = new GameCodes(release);
        var files = AssetStems(release.AssetFiles);
        foreach (var (reference, source) in References(mod))
        {
            var wanted = Normalised(reference);
            if (!FileExists(wanted, files) && !codes.Contains(wanted)) yield return $"{reference} is missing ({source})";
        }
    }

    internal static string Normalised(string reference) => Placeholder.Replace(reference[Domain.Length..], "*");

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
            foreach (var reference in Strings(AssetPatchCheck.Parse(File.ReadAllText(file))).Where(IsReference))
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

    private static IEnumerable<string> Strings(JToken document) => ((JContainer)document).DescendantsAndSelf().SelectMany(token => token switch
    {
        JValue { Type: JTokenType.String, Value: string text } => [text],
        JProperty property => [property.Name],
        _ => Array.Empty<string>(),
    });
}
