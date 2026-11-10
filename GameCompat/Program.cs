using System.Text.RegularExpressions;
using GameCompat;
using Newtonsoft.Json.Linq;

const string Usage = """
    bin/compat                   check the floor (modinfo.json) and the current game (GAME in mise.toml)
    bin/compat <version>...      check these game versions
    bin/compat walk              walk down the minors and name the oldest version we work on
    bin/compat decompile <v>     C# of that version's game, into the cache
    bin/compat help              this text

    Answers "compatible" or lists the reasons, with no game running, from each version's server release
    (downloaded once into ~/.cache/rough-seas-compat):
      Runtime                 the game's .NET can run the mod
      Build                   the source compiles against that version
      References              every game type and member the shipped build names exists there
      Harmony and reflection  every patch target and reflected member exists, with the signature we patch
      Asset patches           every JSON patch's file and path exist

    Needs reading: what no tool can decide. A [ReliesOn] method whose instructions differ from the current
    game's beyond how locals are numbered is listed with the C# diff of that method, beside the sentence
    saying what we rely on. Methods that didn't change are not shown.
    Read it. If what we rely on still holds, add the entry's first line to GameCompat/read-changes.txt, with
    why after it. An entry holds for that version against the current game's method, so it is read once
    until GAME moves. If it doesn't hold, the version is not compatible: raise the floor.

    A patch target that may be absent is the same: the patch doesn't apply there, and that needs reading.

    A check exits 1 unless every checked version is compatible. A walk reports and exits 0.
    """;

var repository = Directory.GetCurrentDirectory();
var current = Regex.Match(File.ReadAllText("mise.toml"), @"^GAME = ""(.+)""", RegexOptions.Multiline).Groups[1].Value;

switch (args)
{
    case ["help", ..]:
        Console.WriteLine(Usage);
        return 0;
    case ["walk"]:
        return await new Walk(repository, current).Run();
    case ["decompile", var version]:
        Console.WriteLine(await Decompiler.Run(version));
        return 0;
    case []:
        var floor = (string)JObject.Parse(File.ReadAllText("RoughSeas/modinfo.json"))["dependencies"]!["game"]!;
        return await Check(floor == current ? [current] : [floor, current]);
    default:
        return await Check(args);
}

async Task<int> Check(IEnumerable<string> versions)
{
    var checker = await Checker.Start(repository, current);
    var compatible = true;
    foreach (var version in versions)
    {
        var report = await checker.Check(version);
        report.Print();
        compatible &= report.Compatible;
    }
    return compatible ? 0 : 1;
}
