using System.Reflection;
using GameCompat;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Xunit;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace RoughSeas.Tests.GameCompat;

public class CompatibilityTests
{
    private static readonly ModuleDefinition Module = ModuleDefinition.CreateModule("fixture", ModuleKind.Dll);

    private static readonly Lazy<ModReach> Fixture = new(() => ModReach.Read(ModuleDefinition.ReadModule(typeof(CompatibilityTests).Assembly.Location)));

    private static JToken Boat() => JToken.Parse("""{ "behaviors": [{ "code": "a" }], "attributes": { "weight": 1 } }""");

    private static Mono.Cecil.Cil.MethodBody Body(int locals, Action<ILProcessor> write)
    {
        var method = new MethodDefinition("m", MethodAttributes.Public, Module.TypeSystem.Void);
        for (var index = 0; index < locals; index++) method.Body.Variables.Add(new VariableDefinition(Module.TypeSystem.Int32));
        write(method.Body.GetILProcessor());
        return method.Body;
    }

    private static Mono.Cecil.Cil.MethodBody Reading(params OpCode[] loads) => Body(2, il =>
    {
        foreach (var load in loads) il.Emit(load);
    });

    private static Mono.Cecil.Cil.MethodBody Jumping(int stepsAhead) => Body(0, il =>
    {
        var ending = Enumerable.Range(0, 3).Select(_ => il.Create(OpCodes.Nop)).ToList();
        il.Emit(OpCodes.Br, ending[stepsAhead]);
        ending.ForEach(il.Append);
    });

    [Fact]
    public void AnAddUnderAMissingParentFails() =>
        Assert.Equal("its parent is missing", AssetPatchCheck.Apply(Boat(), "add", "/server/behaviors/-", new JValue(1)));

    [Fact]
    public void AnAppendToAnArrayPasses() =>
        Assert.Null(AssetPatchCheck.Apply(Boat(), "add", "/behaviors/-", new JValue(1)));

    [Fact]
    public void AnAppendToAnObjectFails() =>
        Assert.Equal("it is not an array", AssetPatchCheck.Apply(Boat(), "add", "/attributes/-", new JValue(1)));

    [Fact]
    public void AnIndexPastTheEndFails() =>
        Assert.NotNull(AssetPatchCheck.Apply(Boat(), "add", "/behaviors/5", new JValue(1)));

    [Fact]
    public void AReplaceOfAMissingKeyFails() =>
        Assert.Equal("it is missing", AssetPatchCheck.Apply(Boat(), "replace", "/attributes/durability", new JValue(1)));

    [Fact]
    public void ALaterPatchSeesWhatAnEarlierOneAdded()
    {
        var boat = Boat();
        AssetPatchCheck.Apply(boat, "add", "/behaviorConfigs", new JObject());
        Assert.Null(AssetPatchCheck.Apply(boat, "add", "/behaviorConfigs/drift", new JValue(1)));
    }

    [Fact]
    public void WhatSitsAtAPathIsSeen() =>
        Assert.NotEqual(AssetPatchCheck.At(JToken.Parse("""{ "seats": [{ "a": 1 }, { "b": 2 }] }"""), "/seats/0/selectionBox"),
            AssetPatchCheck.At(JToken.Parse("""{ "seats": [{ "b": 2 }, { "a": 1 }] }"""), "/seats/0/selectionBox"));

    [Fact]
    public void AnAppendSeesNothingToCompare() =>
        Assert.Equal("", AssetPatchCheck.At(Boat(), "/behaviors/-"));

    [Fact]
    public void AReferenceFindsAFileWithoutItsFolderOrExtension() =>
        Assert.True(GameReferenceCheck.FileExists("block/planks", GameReferenceCheck.AssetStems(["textures/block/planks.png"])));

    [Fact]
    public void ANumberedSoundAnswersItsBareName() =>
        Assert.True(GameReferenceCheck.FileExists("sounds/block/chop", GameReferenceCheck.AssetStems(["sounds/block/chop1.ogg"])));

    [Fact]
    public void AWildcardReferenceMatchesAFile() =>
        Assert.True(GameReferenceCheck.FileExists("sounds/block/chop*", GameReferenceCheck.AssetStems(["sounds/block/chop2.ogg"])));

    [Fact]
    public void AMissingFileIsMissing() =>
        Assert.False(GameReferenceCheck.FileExists("block/gone", GameReferenceCheck.AssetStems(["textures/block/planks.png"])));

    [Fact]
    public void APlaceholderTakesItsValuesFromTheFilesVariantGroups()
    {
        var boat = JToken.Parse("""{ "variantgroups": [{ "code": "port", "states": ["thatch", "reed"] }], "wreckage": "game:{port}" }""");
        var mentions = GameReferenceCheck.Mentions(boat).ToList();
        Assert.Contains("game:thatch", mentions);
        Assert.Contains("game:reed", mentions);
    }

    [Fact]
    public void AWildcardCodeTakesItsValuesFromAllowedVariants()
    {
        var recipe = JToken.Parse("""[{ "code": "game:*", "allowedVariants": ["thatch", "reed"] }]""");
        var mentions = GameReferenceCheck.Mentions(recipe).ToList();
        Assert.Contains("game:thatch", mentions);
        Assert.DoesNotContain("game:*", mentions);
    }

    [Fact]
    public void APlaceholderWithoutKnownValuesStaysAsWritten() =>
        Assert.Contains("game:{other}", GameReferenceCheck.Mentions(JToken.Parse("""{ "wreckage": "game:{other}" }""")));

    [Fact]
    public void VariantsAreExpandedAndSkipped()
    {
        var hide = JObject.Parse("""
            { "code": "hide", "skipVariants": ["hide-raw-large"],
              "variantgroups": [{ "code": "state", "states": ["raw", "prepared"] }, { "code": "size", "loadFromProperties": "game:sizes" }] }
            """);
        var codes = GameCodes.Expand(hide, _ => ["small", "large"]).ToList();
        Assert.Contains("hide-prepared-large", codes);
        Assert.DoesNotContain("hide-raw-large", codes);
    }

    [Fact]
    public void TheFloorIsTheLowestVersionAboveTheHighestBrokenOne()
    {
        var compatible = new[] { "1.22.0", "1.22.4", "1.22.5" }.Select(Version.Parse);
        var incompatible = new[] { "1.22.3" }.Select(Version.Parse);
        Assert.Equal(Version.Parse("1.22.4"), Walk.Floor(compatible, incompatible));
    }

    [Fact]
    public void ThereIsNoFloorWhenNothingAboveTheBrokenVersionPasses() =>
        Assert.Null(Walk.Floor([], [Version.Parse("1.22.7")]));

    [Fact]
    public void HowLocalsAreNumberedIsNotAChange() =>
        Assert.Equal(BehaviourCheck.Tokens(Reading(OpCodes.Ldloc_0, OpCodes.Ldloc_1)), BehaviourCheck.Tokens(Reading(OpCodes.Ldloc_1, OpCodes.Ldloc_0)));

    [Fact]
    public void ReadingADifferentLocalIsAChange() =>
        Assert.NotEqual(BehaviourCheck.Tokens(Reading(OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_0)), BehaviourCheck.Tokens(Reading(OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_1)));

    [Fact]
    public void JumpingElsewhereIsAChange() =>
        Assert.NotEqual(BehaviourCheck.Tokens(Jumping(0)), BehaviourCheck.Tokens(Jumping(2)));

    [Fact]
    public void AChangedConstantIsAChange() =>
        Assert.NotEqual(BehaviourCheck.Tokens(Body(0, il => il.Emit(OpCodes.Ldc_R4, 1.25f))), BehaviourCheck.Tokens(Body(0, il => il.Emit(OpCodes.Ldc_R4, 1.5f))));

    [Fact]
    public void AVerdictIsReadFromAnIndentedLine()
    {
        var reliance = new Reliance("T", "M", "", "");
        Assert.True(new ReadChanges(["  1.22.0 T.M abc why it holds"]).Holds("1.22.0", new BehaviourChange(reliance, "abc")));
    }

    [Fact]
    public void AVerdictForAnotherDifferenceDoesNotHold()
    {
        var reliance = new Reliance("T", "M", "", "");
        Assert.False(new ReadChanges(["1.22.0 T.M abc why"]).Holds("1.22.0", new BehaviourChange(reliance, "def")));
    }

    [Fact]
    public void AReachThroughAHelperIsFollowed() =>
        Assert.Contains(Fixture.Value.Members, member => member.Type == typeof(Block).FullName && member.Name == "ThroughHelper");

    [Fact]
    public void AReachInAnArrayLoopIsFollowed() =>
        Assert.Contains(Fixture.Value.Patches, patch => patch.Target.Name == "InLoopB" && patch.Kind == HandlerKind.Transpiler);

    [Fact]
    public void AHandlerIsTiedToItsTarget() =>
        Assert.Contains(Fixture.Value.Patches, patch => patch.Target.Name == "ThroughHelper" && patch.Handler.Name == nameof(ReachFixture.Handler));

    [Fact]
    public void OverloadParametersAreRead() =>
        Assert.Contains(Fixture.Value.Members, member => member.Name == "Overloaded" && member.Parameters == "System.Double,System.Int32");

    [Fact]
    public void AReachItCannotReadIsReported() =>
        Assert.Contains(Fixture.Value.Unreadable, site => site.Contains(nameof(ReachFixture.Unreadable)));

    [Fact]
    public void AnUnknownReflectionFormIsReported() =>
        Assert.Contains(Fixture.Value.Unreadable, site => site.Contains(nameof(ReachFixture.ByReflection)));

    private static class ReachFixture
    {
        private static readonly Harmony Harmony = new("fixture");

        public static void Handler()
        {
        }

        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions) => instructions;

        public static void Patches()
        {
            Helper(AccessTools.Method(typeof(Block), "ThroughHelper"), nameof(Handler));
            AccessTools.Method(typeof(Block), "Overloaded", [typeof(double), typeof(int)]);
            foreach (var target in new[] { AccessTools.Method(typeof(Block), "InLoopA"), AccessTools.Method(typeof(Block), "InLoopB") })
            {
                Harmony.Patch(target, transpiler: new HarmonyMethod(typeof(ReachFixture), nameof(Rewrite)));
            }
        }

        public static void Unreadable(string name) => AccessTools.Method(name.GetType(), name.Trim());

        public static MethodInfo? ByReflection() => typeof(Block).GetMethod("Anything");

        private static void Helper(MethodBase target, string handler) =>
            Harmony.Patch(target, postfix: new HarmonyMethod(typeof(ReachFixture), handler));
    }
}
