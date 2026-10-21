using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace RoughSeas.Vessels;

public class MaterialTable
{
    private const string WoodPrefix = "wood-";
    private const string UnknownWood = "wood";
    private const double PascalsPerMegapascal = 1e6;

    private readonly Dictionary<string, Material> materials;
    private readonly ILogger logger;
    private readonly HashSet<string> warned = [];

    public MaterialTable(Dictionary<string, Material> materials, ILogger logger)
    {
        this.materials = materials;
        this.logger = logger;
    }

    public double StrengthFactor(IEnumerable<VesselPart> parts) =>
        parts.Aggregate(1.0, (factor, part) => factor * StrengthRatio(part.Material, ReferenceOf(part)));

    public HullBuild Build(IReadOnlyCollection<VesselPart> parts, double referenceKilograms)
    {
        double shares = parts.Sum(part => part.Share);
        if (parts.Count == 0 || shares <= 0) return new HullBuild(referenceKilograms, Litres(referenceKilograms, UnknownWood));

        return parts.Aggregate(new HullBuild(0, 0), (build, part) =>
        {
            double referenceShare = referenceKilograms * part.Share / shares;
            double litres = Litres(referenceShare, ReferenceOf(part));
            return new HullBuild(build.Kilograms + litres / 1000 * Density(part.Material), build.Litres + litres);
        });
    }

    public double BreakingKilonewtons(string rope)
    {
        if (materials.TryGetValue(rope, out var known) && known.Strength is { } strength) return strength;

        logger.Warning("Rough Seas has no breaking load for {0}; it counts as unbreakable", rope);
        return double.PositiveInfinity;
    }

    public double Toughness(string material) =>
        Row(material, row => row.Toughness, "toughness", "it takes that of an unknown wood")
        ?? materials[UnknownWood].Toughness!.Value;

    public double? CutToughness(string material) => materials.GetValueOrDefault(material)?.CutToughness;

    public double ModulusOfRupture(string material) =>
        (Strength(material) ?? materials[UnknownWood].Strength!.Value) * PascalsPerMegapascal;

    private static string ReferenceOf(VesselPart part) => part.Reference == "" ? part.Material : part.Reference;

    private double Litres(double kilograms, string material) => kilograms / Density(material) * 1000;

    private double StrengthRatio(string material, string reference) =>
        material != reference && Strength(material) is { } built && Strength(reference) is { } baseline ? built / baseline : 1;

    private double? Strength(string material) =>
        Row(material, row => row.Strength, "strength", "vessels of it count as their reference build");

    private double Density(string material) =>
        Row(material, row => row.Density, "density", "it weighs as an unknown wood does")
        ?? materials[UnknownWood].Density!.Value;

    private double? Row(string material, System.Func<Material, double?> value, string quantity, string fallback)
    {
        if (!material.StartsWith(WoodPrefix))
        {
            if (materials.TryGetValue(material, out var known) && value(known) is { } found) return found;

            WarnOnce(material, quantity, fallback);
            return null;
        }

        var wood = Wood.Parse(material[WoodPrefix.Length..]);
        foreach (var key in wood.Keys)
        {
            if (materials.TryGetValue(WoodPrefix + key, out var species) && value(species) is { } found) return found;
        }

        WarnOnce(material, quantity, "it takes that of an unknown wood");
        return value(materials[UnknownWood]);
    }

    private void WarnOnce(string material, string quantity, string fallback)
    {
        if (warned.Add($"{quantity} {material}")) logger.Warning("Rough Seas has no {0} for {1}; {2}", quantity, material, fallback);
    }
}

public readonly record struct HullBuild(double Kilograms, double Litres);
