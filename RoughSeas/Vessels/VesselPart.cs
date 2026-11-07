namespace RoughSeas.Vessels;

/// <summary>
/// A material a vessel is built from, and the material its impact capacity and weight were set for. Each part scales
/// the capacity by its strength against that reference, and its share of the weight by its density.
/// </summary>
public class VesselPart
{
    /// <summary>What a build calls the part by, whatever it is made of.</summary>
    public string Name = "";

    public string Material = "";

    /// <summary>The material itself when empty.</summary>
    public string Reference = "";

    /// <summary>Its share of the reference build's weight, in proportion to the other parts' shares.</summary>
    public double Share = 1;

    /// <summary>How much of it is in her yet, by weight and strength: all of it unless she is still being built. Above 1 while it is still more wood than it will be, as an unhewn float.</summary>
    public double Present = 1;

    /// <summary>Whether a knock on her lands on it: a float or a rig lashed to her hull weighs, but takes none.</summary>
    public bool TakesKnocks = true;

    public VesselPart As(string material, double present) =>
        new() { Name = Name, Material = material, Reference = Reference == "" ? Material : Reference, Share = Share, Present = present, TakesKnocks = TakesKnocks };
}
