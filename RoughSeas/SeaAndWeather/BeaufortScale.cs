using System.Linq;

namespace RoughSeas.SeaAndWeather;

public class BeaufortScale
{
    private readonly BeaufortForce[] forces;

    public BeaufortScale(BeaufortForce[] forces)
    {
        this.forces = forces.OrderBy(force => force.MinMps).ToArray();
    }

    public BeaufortForce ForceAt(double mps) => forces.LastOrDefault(force => force.MinMps <= mps) ?? forces[0];
}
