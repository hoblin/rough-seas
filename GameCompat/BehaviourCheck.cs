using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GameCompat;

internal sealed record Reliance(string Type, string Method, string What, string Where)
{
    public string Target => $"{Type}.{Method}";
}

internal sealed record BehaviourChange(Reliance Reliance, string Stamp, string SourceDiff = "")
{
    public bool Gone => Stamp == BehaviourCheck.GoneStamp;
}

internal static class BehaviourCheck
{
    public const string GoneStamp = "gone";

    private const string AttributeName = "RoughSeas.ReliesOnAttribute";

    private static readonly Regex GeneratedNumber = new(@"(<>c__DisplayClass|b__|g__\w+\||<>9__|CS\$<>8__locals|d__)\d+(_\d+)?", RegexOptions.Compiled);

    public static IReadOnlyList<Reliance> Declared(ModuleDefinition mod) =>
        mod.GetTypes().SelectMany(Members).SelectMany(member => member.CustomAttributes
                .Where(attribute => attribute.AttributeType.FullName == AttributeName)
                .Select(attribute => ToReliance(attribute, member.FullName)))
            .ToList();

    public static IEnumerable<string> Unknown(IEnumerable<Reliance> reliances, GameRelease current) =>
        reliances.Where(reliance => Instructions(reliance, current) == null).Select(reliance => $"{reliance.Target} names nothing ({reliance.Where})");

    public static IEnumerable<string> Undeclared(ModReach reach, IReadOnlyList<Reliance> reliances)
    {
        bool Declares(Reach member) => reliances.Any(reliance => reliance.Type == member.Type && reliance.Method == member.Name);

        foreach (var member in reach.Members.Where(member => member.MayBeAbsent && !Declares(member)))
        {
            yield return $"{member.Type}.{member.Name} is patched only where it exists, so it needs a [ReliesOn] ({member.Site})";
        }

        foreach (var patch in reach.Patches.Where(patch => patch.Kind == HandlerKind.Transpiler && !Declares(patch.Target)))
        {
            yield return $"{patch.Target.Type}.{patch.Target.Name} is transpiled, so it needs a [ReliesOn] ({patch.Handler.FullName})";
        }
    }

    public static IReadOnlyList<BehaviourChange> Changes(IEnumerable<Reliance> reliances, GameRelease current, GameRelease release)
    {
        var changes = new List<BehaviourChange>();
        foreach (var reliance in reliances.DistinctBy(reliance => reliance.Target))
        {
            if (Instructions(reliance, current) is not { } baseline) continue;
            var candidate = Instructions(reliance, release);
            if (candidate == null) changes.Add(new BehaviourChange(reliance, GoneStamp));
            else if (!candidate.SequenceEqual(baseline)) changes.Add(new BehaviourChange(reliance, Stamp(baseline, candidate)));
        }
        return changes;
    }

    internal static IReadOnlyList<string>? Instructions(Reliance reliance, GameRelease release)
    {
        if (release.FindType(reliance.Type) is not { } type) return null;
        for (var owner = type; owner != null; owner = owner.BaseType?.Resolve())
        {
            var named = owner.Methods.Where(method => method.Name == reliance.Method).OrderBy(method => method.FullName, StringComparer.Ordinal).ToList();
            if (named.Count == 0) continue;
            var seen = new HashSet<string>();
            var lines = new List<string>();
            foreach (var method in named) Append(method, lines, seen);
            return lines;
        }
        return null;
    }

    internal static IReadOnlyList<string> Tokens(MethodBody body)
    {
        var positions = body.Instructions.Select((instruction, position) => (instruction, position)).ToDictionary(pair => pair.instruction, pair => pair.position);
        var locals = new Dictionary<int, int>();
        return body.Instructions.Where(instruction => instruction.OpCode.Code != Code.Nop).Select(instruction => Token(instruction, positions, locals)).ToList();
    }

    private static string Token(Instruction instruction, Dictionary<Instruction, int> positions, Dictionary<int, int> locals)
    {
        var name = instruction.OpCode.Name.EndsWith(".s") ? instruction.OpCode.Name[..^2] : instruction.OpCode.Name;
        if (LocalIndex(instruction) is { } local)
        {
            locals.TryAdd(local, locals.Count);
            return $"{name.Split('.')[0]} L{locals[local]}";
        }

        return instruction.Operand switch
        {
            null => name,
            Instruction target => $"{name} {positions[target] - positions[instruction]}",
            Instruction[] targets => $"{name} {string.Join(",", targets.Select(target => positions[target] - positions[instruction]))}",
            ParameterDefinition parameter => $"{name} {parameter.Index}",
            MemberReference member => $"{name} {Clean(member.FullName)}",
            string text => $"{name} \"{text}\"",
            IFormattable number => $"{name} {number.ToString(null, CultureInfo.InvariantCulture)}",
            var other => $"{name} {other}",
        };
    }

    private static int? LocalIndex(Instruction instruction) => instruction.OpCode.Code switch
    {
        Code.Ldloc_0 or Code.Stloc_0 => 0,
        Code.Ldloc_1 or Code.Stloc_1 => 1,
        Code.Ldloc_2 or Code.Stloc_2 => 2,
        Code.Ldloc_3 or Code.Stloc_3 => 3,
        _ => (instruction.Operand as VariableDefinition)?.Index,
    };

    internal static string Stamp(IEnumerable<string> baseline, IEnumerable<string> candidate) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", baseline) + "\0" + string.Join("\n", candidate))))[..12];

    internal static IEnumerable<IMemberDefinition> Members(TypeDefinition type) =>
        new ICustomAttributeProvider[] { type }.Concat(type.Methods).Concat(type.Fields).Concat(type.Properties).OfType<IMemberDefinition>();

    private static Reliance ToReliance(CustomAttribute attribute, string where)
    {
        var arguments = attribute.ConstructorArguments.Select(argument => (string)argument.Value).ToArray();
        return new Reliance(arguments[0].Replace('+', '/'), arguments[1], arguments[2], where);
    }

    private static void Append(MethodDefinition method, List<string> lines, HashSet<string> seen)
    {
        if (!seen.Add(method.FullName)) return;
        lines.Add(Clean(method.FullName));
        if (!method.HasBody) return;

        lines.AddRange(Tokens(method.Body));
        lines.AddRange(method.Body.ExceptionHandlers.Select(handler => $"{handler.HandlerType} {handler.CatchType?.FullName}"));

        foreach (var generated in method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().Select(Generated).OfType<MethodDefinition>())
        {
            Append(generated, lines, seen);
            if (!generated.DeclaringType.Name.Contains(">d__")) continue;
            foreach (var sibling in generated.DeclaringType.Methods) Append(sibling, lines, seen);
        }
    }

    private static MethodDefinition? Generated(MethodReference reference)
    {
        if (!IsGenerated(reference.Name) && !IsGenerated(reference.DeclaringType.Name)) return null;
        try
        {
            return reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    private static bool IsGenerated(string name) => name.Contains('<');

    private static string Clean(string name) => GeneratedNumber.Replace(name, "$1");
}
