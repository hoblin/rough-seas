using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace RoughSeas.Vessels;

/// <summary>A hull's drag as she floats now, from her form, her live draft and her mass; built anew only when they change.</summary>
public sealed class LiveDrag(Entity entity, IHull hull, JsonObject attributes, VesselsModSystem vessels)
{
    private const double KilogramsPerCubicMetre = 1000;

    private (HullForm Form, double Draft, double Kg) builtFor;
    private HullDrag? built;

    /// <summary>Her drag now; null while she has no draft to take it on.</summary>
    public HullDrag? Now
    {
        get
        {
            if (!hull.HasDraft || hull.Afloat.DraftMetres <= 0 || hull.Aboard.Total <= 0) return null;

            var key = (HullForm.From(attributes, vessels.Drag, hull.Shape), hull.Afloat.DraftMetres, hull.Aboard.Total);
            if (built != null && key == builtFor) return built;

            if (key.Item1 != builtFor.Form)
            {
                vessels.Log(entity, "her drag on a hull {0:F2} by {1:F2} m, bow {2:F2}, side {3:F2}, fin {4:F2} m²",
                    key.Item1.LengthMetres, key.Item1.BeamMetres, key.Item1.BowDrag, key.Item1.SideDrag, key.Item1.FinSquareMetres);
            }
            builtFor = key;
            return built = new HullDrag(key.Item1, key.DraftMetres, key.Total, VesselMass.SeawaterKgPerLitre * KilogramsPerCubicMetre, vessels.Drag);
        }
    }
}
