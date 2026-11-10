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

    public static IEnumerable<string> Undeclared(ModReach reach, IReadOnlyList<Reliance> reliances) =>
        reach.Members.Where(member => member.AnyOverload && !reliances.Any(reliance => reliance.Type == member.Type && reliance.Method == member.Name))
            .Select(member => $"{member.Type}.{member.Name} is patched only where it exists, so it needs a [ReliesOn] ({member.Site})");

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
            var named = owner.Methods.Where(method => method.Name == reliance.Method).OrderBy(method => method.FullName).ToList();
            if (named.Count == 0) continue;
            var seen = new HashSet<string>();
            var lines = new List<string>();
            foreach (var method in named) Append(method, lines, seen);
            return lines;
        }
        return null;
    }

    internal static string? Token(OpCode opcode, object? operand)
    {
        var name = opcode.Name.EndsWith(".s") ? opcode.Name[..^2] : opcode.Name;
        if (name.StartsWith("ldloc") || name.StartsWith("stloc") || opcode.Code == Code.Nop) return null;
        return operand switch
        {
            null or Instruction or Instruction[] or VariableDefinition => name,
            ParameterDefinition parameter => $"{name} {parameter.Index}",
            MemberReference member => $"{name} {Clean(member.FullName)}",
            string text => $"{name} \"{text}\"",
            IFormattable number => $"{name} {number.ToString(null, CultureInfo.InvariantCulture)}",
            var other => $"{name} {other}",
        };
    }

    private static string Stamp(IEnumerable<string> baseline, IEnumerable<string> candidate) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", baseline) + "\0" + string.Join("\n", candidate))))[..12];

    private static IEnumerable<IMemberDefinition> Members(TypeDefinition type) =>
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

        lines.AddRange(method.Body.Instructions.Select(instruction => Token(instruction.OpCode, instruction.Operand)).OfType<string>());
        lines.AddRange(method.Body.ExceptionHandlers.Select(handler => $"{handler.HandlerType} {handler.CatchType?.FullName}"));

        foreach (var generated in method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>().Select(Generated).OfType<MethodDefinition>())
        {
            Append(generated, lines, seen);
            if (!IsGenerated(generated.DeclaringType.Name)) continue;
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
