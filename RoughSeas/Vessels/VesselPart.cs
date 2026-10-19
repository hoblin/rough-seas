namespace RoughSeas.Vessels;

/// <summary>
/// A material a vessel is built from, and the material its impact capacity and weight were set for. Each part scales
/// the capacity by its strength against that reference, and its share of the weight by its density.
/// </summary>
public class VesselPart
{
    public string Material = "";

    /// <summary>The material itself when empty.</summary>
    public string Reference = "";

    /// <summary>Its share of the reference build's weight, in proportion to the other parts' shares.</summary>
    public double Share = 1;
}
