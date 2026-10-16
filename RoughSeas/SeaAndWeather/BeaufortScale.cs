using System.Linq;

namespace RoughSeas.SeaAndWeather;

public class BeaufortScale
{
    private const double LastBandWidthMps = 6;

    private readonly BeaufortForce[] forces;
    private readonly double[] bandMidpoints;

    public BeaufortScale(BeaufortForce[] forces)
    {
        this.forces = forces.OrderBy(force => force.MinMps).ToArray();
        bandMidpoints = this.forces
            .Select((force, i) => i + 1 < this.forces.Length
                ? (force.MinMps + this.forces[i + 1].MinMps) / 2
                : force.MinMps + LastBandWidthMps / 2)
            .ToArray();
    }

    public BeaufortForce ForceAt(double mps) => forces.LastOrDefault(force => force.MinMps <= mps) ?? forces[0];

    public double WaveHeightAt(double mps)
    {
        if (mps <= bandMidpoints[0]) return forces[0].WaveHeight;
        for (var i = 1; i < forces.Length; i++)
        {
            if (mps > bandMidpoints[i]) continue;
            var t = (mps - bandMidpoints[i - 1]) / (bandMidpoints[i] - bandMidpoints[i - 1]);
            return forces[i - 1].WaveHeight + (forces[i].WaveHeight - forces[i - 1].WaveHeight) * t;
        }
        return forces[^1].WaveHeight;
    }
}
