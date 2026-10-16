namespace RoughSeas.Vessels;

/// <summary>
/// A material a vessel is built from, and the material its impact capacity was set for. Each part scales the
/// capacity by its strength against that reference.
/// </summary>
public class VesselPart
{
    public string Material = "";
    public string Reference = "";
}
