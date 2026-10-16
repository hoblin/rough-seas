namespace RoughSeas.Vessels;

/// <summary>
/// A material a vessel is built from, and the material its impact capacity was set for: the hull is as strong as
/// its weakest part against that reference.
/// </summary>
public class VesselPart
{
    public string Material = "";
    public string Reference = "";
}
