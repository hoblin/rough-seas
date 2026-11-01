using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Shipyard;

/// <summary>
/// A vessel framed or rigged where she lies: her stages are laid in order, piece by piece, and she is finished once
/// the last is laid and what was laid wet has set. Her state lives in the vessel's watched <c>roughseas:build</c> tree; a vessel with none is
/// finished.
/// </summary>
public sealed class Framing
{
    public const string SheetGroup = "Sheet";
    public const string SkinGroup = "Skin";

    public const string WoodKey = "wood";
    public const string SetsAtKey = "setsAt";
    private const string PieceWoodSuffix = "Wood";
    private const string BentSuffix = "Bent";
    private const string LevelSuffix = "At";

    private readonly Func<ITreeAttribute?> tree;
    private readonly Action changed;

    public Framing(Func<ITreeAttribute?> tree, Action changed, IReadOnlyList<FramingStage> stages)
    {
        (this.tree, this.changed, Stages) = (tree, changed, stages);
    }

    public IReadOnlyList<FramingStage> Stages { get; }

    private ITreeAttribute? Tree => tree();

    /// <summary>The stage to work on next; none once every piece is laid.</summary>
    public FramingStage? Current => Stages.FirstOrDefault(stage => Laid(stage) < stage.Pieces);

    /// <summary>Whether every stage she needs is laid; an optional one may still be to come.</summary>
    public bool Built => Stages.Where(stage => !stage.Optional).All(stage => Laid(stage) >= stage.Pieces);

    /// <summary>The wood her laths or poles are split from, once the first is laid.</summary>
    public string? Wood => Tree?.GetString(WoodKey);

    /// <summary>The wood the pieces of a stage that makes a part of her are of, once the first is laid.</summary>
    public string? WoodOf(FramingStage stage) => Tree?.GetString(stage.Group + PieceWoodSuffix);

    /// <summary>When her last rib has dried enough to hold its bend, in game hours.</summary>
    public double SetsAt => Tree?.GetDouble(SetsAtKey) ?? 0;

    /// <summary>How much of her frame's wood is in her.</summary>
    public double Framed => Share(stage => stage.Work is FramingWork.Lath or FramingWork.Pole);

    /// <summary>How much of her lining is laid.</summary>
    public double Lined => Share(stage => stage.Lines);

    public bool Sealed => Stages.Where(stage => stage.Seals).All(stage => Laid(stage) >= stage.Pieces);

    /// <summary>Changes whenever what she shows changes.</summary>
    public string ShapeKey => $"{string.Join("/", Stages.Select(Laid))}/{Wood}/{string.Join("/", Stages.Select(WoodOf))}";

    /// <summary>The groups of her shape that her build doesn't show yet, or no longer shows.</summary>
    public IEnumerable<string> Hidden()
    {
        var pieces = Stages.SelectMany(stage =>
            Enumerable.Range(Laid(stage) + 1, Math.Max(0, stage.Pieces - Laid(stage))).Select(n => $"{stage.Group}{n}")
                .Concat(stage.Replaces is { } replaced ? Enumerable.Range(1, Laid(stage)).Select(n => $"{replaced}{n}") : [])
                .Concat(stage.Successive ? Enumerable.Range(1, Math.Max(0, Laid(stage) - 1)).Select(n => $"{stage.Group}{n}") : [])
                .Concat(stage.Follows is { } followed ? Following(stage, followed) : [])
                .Concat(stage.Lifts is { } lifted ? Others(lifted, stage.Pieces, Laid(stage)) : [])
                .Concat(stage.Bends is { } bent ? Bent(stage, bent) : []));
        if (Tree == null) return pieces.Append(SheetGroup);

        bool started = Stages.Any(stage => Laid(stage) > 0);
        return pieces.Append(started ? SheetGroup : SkinGroup);
    }

    /// <summary>What a look at her says of her build at <paramref name="hours"/>; nothing once she is finished.</summary>
    public string? Progress(double hours)
    {
        if (Tree == null) return null;
        if (Current is { } stage && (!Built || Laid(stage) > 0))
        {
            return Lang.Get($"roughseas:shipyard-stage-{stage.Group.ToLowerInvariant()}", Laid(stage), stage.Pieces);
        }

        return Finished(hours) ? null : Lang.Get("roughseas:shipyard-ribs-setting", Math.Ceiling(SetsAt - hours));
    }

    private int Level(string group) => Stages.FirstOrDefault(stage => stage.Group == group) is { } stage ? Laid(stage) : 0;

    private IEnumerable<string> Following(FramingStage stage, string followed)
    {
        int levels = Stages.FirstOrDefault(other => other.Group == followed)?.Pieces ?? 0;
        return Enumerable.Range(1, Laid(stage)).SelectMany(n => Others($"{stage.Group}{n}{LevelSuffix}", levels, Level(followed), 0));
    }

    private static IEnumerable<string> Others(string prefix, int levels, int level, int first = 1) =>
        Enumerable.Range(first, levels - first + 1).Where(k => k != level).Select(k => $"{prefix}{k}");

    private IEnumerable<string> Bent(FramingStage stage, string bent)
    {
        var flat = Stages.First(other => other.Group == bent);
        return Laid(stage) > 0
            ? Enumerable.Range(1, flat.Pieces).Select(n => $"{bent}{n}")
            : Enumerable.Range(1, flat.Pieces).Select(n => $"{bent}{BentSuffix}{n}");
    }

    public double NextMetres => Current is { } stage && Laid(stage) < stage.Metres.Length ? stage.Metres[Laid(stage)] : 0;

    public int Laid(FramingStage stage) => Tree?.GetInt(stage.Group) ?? stage.Pieces;

    /// <summary>What the stages done bring her, each its <paramref name="brings"/>.</summary>
    public double Raised(Func<FramingStage, double> brings) => Stages.Where(stage => Laid(stage) >= stage.Pieces).Sum(brings);

    /// <summary>How much of the stage is laid.</summary>
    public double Share(FramingStage stage) => stage.Pieces == 0 ? 1 : (double)Laid(stage) / stage.Pieces;

    public bool Finished(double hours) => Built && hours >= SetsAt;

    /// <summary>Lays the current stage's next piece, of <paramref name="wood"/> when it is of one, setting by <paramref name="setsAt"/>.</summary>
    public void Lay(string? wood, double setsAt, int pieces = 1)
    {
        if (Current is not { } stage) return;

        Tree!.SetInt(stage.Group, Math.Min(stage.Pieces, Laid(stage) + pieces));
        if (wood != null) Tree.SetString(stage.Part != null ? stage.Group + PieceWoodSuffix : WoodKey, wood);
        if (stage.Sets) Tree.SetDouble(SetsAtKey, Math.Max(SetsAt, setsAt));
        changed();
    }

    private double Share(Func<FramingStage, bool> counted)
    {
        var stages = Stages.Where(counted).ToArray();
        int pieces = stages.Sum(stage => stage.Pieces);
        return pieces == 0 ? 1 : (double)stages.Sum(Laid) / pieces;
    }
}

public sealed class FramingStage
{
    /// <summary>The shape's group that draws this stage, one child per piece, and the key that counts it.</summary>
    public string Group = "";

    /// <summary>What a piece of it is laid with.</summary>
    public FramingWork Work;

    /// <summary>How long laying one piece takes: the real time of that work over sixty.</summary>
    public double Seconds;

    /// <summary>Whether its pieces line the shell, backing it where they lie.</summary>
    public bool Lines;

    /// <summary>Whether its pieces are bent wet and hold her shape only once dry.</summary>
    public bool Sets;

    /// <summary>Whether her hold holds water only once it is done.</summary>
    public bool Seals;

    /// <summary>The litres of pitch one piece takes.</summary>
    public double Litres;

    /// <summary>The group whose pieces this stage's take away, piece for piece: <c>{Replaces}{n}</c> goes once <c>{Group}{n}</c> is laid.</summary>
    public string? Replaces;

    /// <summary>Whether she is finished without it, and it may go on her any time after.</summary>
    public bool Optional;

    /// <summary>How much higher her side stands once the stage is done.</summary>
    public double RaisesMetres;

    /// <summary>How much more her hold takes once the stage is done.</summary>
    public double RaisesLitres;

    /// <summary>The part of her <c>parts</c>, by its name, that this stage's pieces make: there as far as they are laid, of the wood they are.</summary>
    public string? Part;

    /// <summary>The stage this one bends once its first piece is laid: from then on that stage shows from <c>{Bends}Bent{n}</c>.</summary>
    public string? Bends;

    /// <summary>The shape's texture that draws the pieces, retextured in the wood they are of.</summary>
    public string? Texture;

    /// <summary>Whether each piece is the whole work again, carried one step further: only the last laid shows.</summary>
    public bool Successive;

    /// <summary>The stage whose progress this stage's pieces are drawn at: <c>{Group}{n}At{k}</c> shows once that stage has <c>k</c> laid.</summary>
    public string? Follows;

    /// <summary>The group this stage raises piece by piece: <c>{Lifts}{k}</c> shows while <c>k</c> of its pieces are laid.</summary>
    public string? Lifts;

    /// <summary>How many pieces the stage takes, counted from the shape.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public int Pieces;

    /// <summary>The metres of lath or pole each piece takes, as its shape names them.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public double[] Metres = [];

    /// <summary>The litres of wood each piece leaves, as its shape measures them, and what lay there before the first.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public double[] WoodLitres = [];

    /// <summary>The square metres at the waterline each piece leaves, as its shape measures them, and what lay there before the first.</summary>
    [Newtonsoft.Json.JsonIgnore]
    public double[] WaterlineSquareMetres = [];
}

public enum FramingWork { Lath, Pole, Rope, Pitch, Hew, Cloth, Plank, Hide }
