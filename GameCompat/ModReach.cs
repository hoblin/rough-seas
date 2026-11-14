using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace GameCompat;

internal enum ReachKind { Method, Field, PropertyGetter, PropertySetter, Type }

internal enum HandlerKind { Prefix, Postfix, Transpiler, Finalizer }

internal sealed record Reach(ReachKind Kind, string Type, string Name, string? Parameters, bool DeclaredOnly, bool MayBeAbsent, string Site, string? FieldType = null, bool? FieldStatic = null);

internal sealed record Patch(Reach Target, HandlerKind Kind, MethodDefinition Handler);

internal sealed class ModReach
{
    private const string OwnNamespace = "RoughSeas.";
    private const string OtherModsNamespace = "RoughSeas.Compat.";
    private const int DeepestHelper = 4;

    private static readonly Regex HarmonyTargetName = new(@"^(Vintagestory\.[\w.]+):(\w+)$", RegexOptions.Compiled);
    private static readonly HandlerKind[] HandlerArguments = [HandlerKind.Prefix, HandlerKind.Postfix, HandlerKind.Transpiler, HandlerKind.Finalizer];

    private readonly ModuleDefinition module;
    private readonly Dictionary<(ReachKind, string, string, string?), Reach> members = [];
    private readonly HashSet<(string, string, string?, HandlerKind, string)> seenPatches = [];
    private readonly List<Patch> patches = [];
    private readonly SortedSet<string> unreadable = [];

    private ModReach(ModuleDefinition module) => this.module = module;

    public IReadOnlyCollection<Reach> Members => members.Values;

    public IReadOnlyList<Patch> Patches => patches;

    public IReadOnlyCollection<string> Unreadable => unreadable;

    public static ModReach Read(ModuleDefinition module)
    {
        var reach = new ModReach(module);
        foreach (var type in module.GetTypes().Where(type => !type.FullName.StartsWith(OtherModsNamespace)))
        {
            if (type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "HarmonyLib.HarmonyPatch")) reach.unreadable.Add($"{type.FullName}: [HarmonyPatch]");
            foreach (var method in type.Methods.Where(method => method.HasBody))
            {
                reach.Evaluate(method, method.Parameters.Select(_ => (Value)new PendingArgument()).Prepend(new PendingArgument()).ToArray(), 0);
            }
        }
        return reach;
    }

    private Value Evaluate(MethodDefinition method, Value[] arguments, int depth)
    {
        var stack = new Stack<Value>();
        var locals = new Dictionary<int, Value>();
        Value result = new Opaque();

        Value Pop() => stack.Count > 0 ? stack.Pop() : new Opaque();

        foreach (var instruction in method.Body.Instructions)
        {
            var code = instruction.OpCode.Code;
            switch (code)
            {
                case Code.Ldtoken when instruction.Operand is TypeReference type:
                    stack.Push(new TypeValue(type.FullName));
                    break;
                case Code.Ldstr:
                    var text = (string)instruction.Operand;
                    NoteHarmonyTargetName(text, method);
                    stack.Push(new Text(text));
                    break;
                case Code.Ldnull:
                    stack.Push(new Nothing());
                    break;
                case Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3 or Code.Ldarg_S or Code.Ldarg:
                    stack.Push(ArgumentAt(ArgumentIndex(instruction, method), arguments));
                    break;
                case Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S or Code.Stloc:
                    locals[LocalIndex(instruction)] = Pop();
                    break;
                case Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 or Code.Ldloc_S or Code.Ldloc:
                    stack.Push(locals.GetValueOrDefault(LocalIndex(instruction), new Opaque()));
                    break;
                case Code.Dup:
                    var top = Pop();
                    stack.Push(top);
                    stack.Push(top);
                    break;
                case Code.Newarr:
                    Pop();
                    stack.Push(new ArrayValue([]));
                    break;
                case Code.Stelem_Ref:
                    var item = Pop();
                    Pop();
                    if (Pop() is ArrayValue filled) filled.Items.Add(item);
                    break;
                case Code.Ldelem_Ref:
                    Pop();
                    stack.Push(Pop() is ArrayValue read ? read : new Opaque());
                    break;
                case Code.Ret:
                    if (method.ReturnType.MetadataType != MetadataType.Void) result = Pop();
                    break;
                case Code.Call or Code.Callvirt:
                    var called = (MethodReference)instruction.Operand;
                    var calledArguments = PopArguments(called, Pop);
                    var returned = OnCall(called, calledArguments, method, depth);
                    if (called.ReturnType.MetadataType != MetadataType.Void) stack.Push(returned);
                    break;
                case Code.Newobj:
                    var constructor = (MethodReference)instruction.Operand;
                    var constructorArguments = new Value[constructor.Parameters.Count];
                    for (var index = constructorArguments.Length - 1; index >= 0; index--) constructorArguments[index] = Pop();
                    stack.Push(OnNew(constructor, constructorArguments, depth));
                    break;
                default:
                    Simulate(instruction.OpCode, stack, Pop);
                    break;
            }
        }
        return result;
    }

    private static Value[] PopArguments(MethodReference called, Func<Value> pop)
    {
        var count = called.Parameters.Count + (called.HasThis ? 1 : 0);
        var arguments = new Value[count];
        for (var index = count - 1; index >= 0; index--) arguments[index] = pop();
        return arguments;
    }

    private Value OnCall(MethodReference called, Value[] arguments, MethodDefinition site, int depth)
    {
        var declaring = called.DeclaringType.FullName;
        if (declaring == "System.Type" && called.Name == "GetTypeFromHandle") return arguments[0];
        if (declaring == "HarmonyLib.AccessTools") return OnAccessTools(called, arguments, site);
        if (declaring == "HarmonyLib.Harmony" && called.Name == "Patch") return OnPatch(arguments, site);
        if (declaring == "HarmonyLib.Traverse" || (declaring == "System.Type" && called.Name is "GetMethod" or "GetField" or "GetProperty" or "GetMember"))
        {
            return CantRead(new Opaque(), site, $"{declaring}.{called.Name}");
        }

        if (depth < DeepestHelper && called.DeclaringType.Scope == module && arguments.Any(argument => argument is not PendingArgument))
        {
            var helper = called.Resolve();
            if (helper is { HasBody: true }) return Evaluate(helper, Bind(helper, arguments), depth + 1);
        }
        return new Opaque();
    }

    private static Value[] Bind(MethodDefinition helper, Value[] arguments) =>
        helper.IsStatic ? arguments.Prepend(new Opaque()).ToArray() : arguments;

    private Value OnNew(MethodReference constructor, Value[] arguments, int depth)
    {
        if (depth < DeepestHelper && constructor.DeclaringType.Scope == module && arguments.Any(argument => argument is TypeValue or Text)
            && constructor.Resolve() is { HasBody: true } built)
        {
            Evaluate(built, arguments.Prepend(new Opaque()).ToArray(), depth + 1);
            return new Opaque();
        }
        if (constructor.DeclaringType.FullName != "HarmonyLib.HarmonyMethod" || arguments.Length < 2) return new Opaque();
        if (arguments[0] is TypeValue owner && arguments[1] is Text name)
        {
            var handler = module.GetType(owner.Name)?.Methods.FirstOrDefault(method => method.Name == name.Content);
            return handler != null ? new Handler(handler) : new Opaque();
        }
        return arguments.Take(2).Any(argument => argument is PendingArgument) ? new PendingArgument() : new Opaque();
    }

    private Value OnAccessTools(MethodReference called, Value[] arguments, MethodDefinition site)
    {
        switch (called.Name)
        {
            case "Method" or "DeclaredMethod" when arguments[0] is Text qualified:
                return ColonForm(qualified.Content, site);
            case "Method" or "DeclaredMethod":
                return Noted(arguments[0], arguments[1], ReachKind.Method, called.Name == "DeclaredMethod", site, arguments.ElementAtOrDefault(2));
            case "PropertyGetter" or "DeclaredPropertyGetter":
                return Noted(arguments[0], arguments[1], ReachKind.PropertyGetter, called.Name.StartsWith("Declared"), site);
            case "PropertySetter" or "DeclaredPropertySetter":
                return Noted(arguments[0], arguments[1], ReachKind.PropertySetter, called.Name.StartsWith("Declared"), site);
            case "Field" or "DeclaredField":
                return Noted(arguments[0], arguments[1], ReachKind.Field, called.Name == "DeclaredField", site);
            case "StaticFieldRefAccess":
                return Noted(arguments[0], arguments[1], ReachKind.Field, false, site, fieldType: ((GenericInstanceMethod)called).GenericArguments[0].FullName, fieldStatic: true);
            case "FieldRefAccess" when ((GenericInstanceMethod)called).GenericArguments is { Count: 1 } fieldType:
                return Noted(arguments[0], arguments[1], ReachKind.Field, false, site, fieldType: fieldType[0].FullName, fieldStatic: false);
            case "FieldRefAccess":
                var generics = ((GenericInstanceMethod)called).GenericArguments;
                return Noted(new TypeValue(generics[0].FullName), arguments[0], ReachKind.Field, false, site, fieldType: generics[1].FullName, fieldStatic: false);
            case "TypeByName":
                if (arguments[0] is Text typeName)
                {
                    Note(new Reach(ReachKind.Type, typeName.Content, "", null, false, false, SiteName(site)));
                    return new TypeValue(typeName.Content);
                }
                return CantRead(arguments[0], site);
            case "MethodDelegate":
                return new Opaque();
            default:
                return CantRead(arguments.FirstOrDefault(new Opaque()), site, $"AccessTools.{called.Name}");
        }
    }

    private Value ColonForm(string qualified, MethodDefinition site)
    {
        var parts = qualified.Split(':');
        return Noted(new TypeValue(parts[0]), new Text(parts[1]), ReachKind.Method, false, site);
    }

    private Value Noted(Value type, Value name, ReachKind kind, bool declaredOnly, MethodDefinition site, Value? parameters = null, string? fieldType = null, bool? fieldStatic = null)
    {
        if (type is not TypeValue owner) return CantRead(type, site);
        if (name is not Text member) return CantRead(name, site);
        var reach = new Reach(kind, owner.Name, member.Content, ParameterNames(parameters), declaredOnly, false, SiteName(site), fieldType, fieldStatic);
        Note(reach);
        return new MemberValue(reach);
    }

    private static string? ParameterNames(Value? parameters) =>
        parameters is ArrayValue list ? string.Join(",", list.Items.OfType<TypeValue>().Select(type => type.Name)) : null;

    private void Note(Reach reach)
    {
        if (reach.Type.StartsWith(OwnNamespace)) return;
        members.TryAdd((reach.Kind, reach.Type, reach.Name, reach.Parameters), reach);
    }

    private void NoteHarmonyTargetName(string text, MethodDefinition site)
    {
        var match = HarmonyTargetName.Match(text);
        if (match.Success) Note(new Reach(ReachKind.Method, match.Groups[1].Value, match.Groups[2].Value, null, true, true, SiteName(site)));
    }

    private Value OnPatch(Value[] arguments, MethodDefinition site)
    {
        var targets = Flatten(arguments[1]).ToList();
        foreach (var target in targets)
        {
            if (target is not MemberValue { Reach: var reach })
            {
                CantRead(target, site);
                continue;
            }
            for (var index = 0; index < HandlerArguments.Length; index++)
            {
                var argument = arguments.ElementAtOrDefault(index + 2);
                if (argument is Handler handler) NotePatch(reach, HandlerArguments[index], handler.Method);
                else if (argument is not (null or Nothing)) CantRead(argument, site, $"the {HandlerArguments[index]} of {reach.Name}");
            }
        }
        return new Opaque();
    }

    private void NotePatch(Reach target, HandlerKind kind, MethodDefinition handler)
    {
        if (seenPatches.Add((target.Type, target.Name, target.Parameters, kind, handler.FullName))) patches.Add(new Patch(target, kind, handler));
    }

    private static IEnumerable<Value> Flatten(Value value) => value is ArrayValue array ? array.Items.SelectMany(Flatten) : [value];

    private Value CantRead(Value value, MethodDefinition site, string? what = null)
    {
        if (value is PendingArgument) return value;
        if (!IsReachedAtRuntime(site)) unreadable.Add(what == null ? SiteName(site) : $"{SiteName(site)}: {what}");
        return new Opaque();
    }

    private static bool IsReachedAtRuntime(MethodDefinition site) =>
        site.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "RoughSeas.ReachedAtRuntimeAttribute");

    private static string SiteName(MethodDefinition site) => $"{site.DeclaringType.FullName}.{site.Name}";

    private static Value ArgumentAt(int index, Value[] arguments) => index < arguments.Length ? arguments[index] : new Opaque();

    private static int ArgumentIndex(Instruction instruction, MethodDefinition method) => instruction.OpCode.Code switch
    {
        Code.Ldarg_0 => 0,
        Code.Ldarg_1 => 1,
        Code.Ldarg_2 => 2,
        Code.Ldarg_3 => 3,
        _ => ((ParameterDefinition)instruction.Operand).Index + (method.IsStatic ? 0 : 1),
    } + (method.IsStatic ? 1 : 0);

    private static int LocalIndex(Instruction instruction) => instruction.OpCode.Code switch
    {
        Code.Stloc_0 or Code.Ldloc_0 => 0,
        Code.Stloc_1 or Code.Ldloc_1 => 1,
        Code.Stloc_2 or Code.Ldloc_2 => 2,
        Code.Stloc_3 or Code.Ldloc_3 => 3,
        _ => ((VariableDefinition)instruction.Operand).Index,
    };

    private static void Simulate(OpCode opcode, Stack<Value> stack, Func<Value> pop)
    {
        if (opcode.StackBehaviourPop == StackBehaviour.PopAll) stack.Clear();
        for (var popped = Pops(opcode.StackBehaviourPop); popped > 0; popped--) pop();
        for (var pushed = Pushes(opcode.StackBehaviourPush); pushed > 0; pushed--) stack.Push(new Opaque());
    }

    private static int Pops(StackBehaviour behaviour) => behaviour switch
    {
        StackBehaviour.Pop0 or StackBehaviour.PopAll or StackBehaviour.Varpop => 0,
        StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
        StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8
            or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
        _ => 2,
    };

    private static int Pushes(StackBehaviour behaviour) => behaviour switch
    {
        StackBehaviour.Push0 or StackBehaviour.Varpush => 0,
        StackBehaviour.Push1_push1 => 2,
        _ => 1,
    };

    private abstract record Value;

    private sealed record Opaque : Value;

    private sealed record PendingArgument : Value;

    private sealed record Nothing : Value;

    private sealed record TypeValue(string Name) : Value;

    private sealed record Text(string Content) : Value;

    private sealed record ArrayValue(List<Value> Items) : Value;

    private sealed record MemberValue(Reach Reach) : Value;

    private sealed record Handler(MethodDefinition Method) : Value;
}
