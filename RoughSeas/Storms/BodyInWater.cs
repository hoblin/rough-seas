namespace RoughSeas.Storms;

/// <summary>
/// A body the water moves across by its force: its velocity is the waves' water at its place and its own beside it.
/// The water's pressure and added mass give it their share of the water's own change and push it by −(s·∇)u for its
/// velocity s through the water; a body that displaces its own mass, as a hull does, takes both whole. Its drag and its
/// own forces are its kind's. It holds its own lock, for a kind its physics thread reads.
/// </summary>
public abstract class BodyInWater
{
    protected readonly object gate = new();
    protected double ownX;
    protected double ownZ;
    protected double waveX;
    protected double waveZ;
    protected bool met;

    public (double X, double Z) Velocity
    {
        get
        {
            lock (gate) return (waveX + ownX, waveZ + ownZ);
        }
    }

    /// <summary>Its velocity beside the waves' water at its place, m/s: the part forces change.</summary>
    public (double X, double Z) Own
    {
        get
        {
            lock (gate) return (ownX, ownZ);
        }
    }

    /// <summary>What the waves pushed it by in the last step, m/s², for its velocity through their water.</summary>
    public (double X, double Z) Pushed { get; private set; }

    /// <summary>
    /// The waves' water at its place, as <paramref name="water"/> reads it now, meets the body running through it at
    /// <paramref name="through"/>: it takes <paramref name="share"/> of the water's change and of its push, which
    /// <see cref="Pushed"/> holds for its drag to settle with. Called under the lock.
    /// </summary>
    /// <returns>The water's acceleration of it besides its drag, m/s².</returns>
    protected (double X, double Z) Meet(WaterOnHull water, (double X, double Z) through, double share, double seconds)
    {
        var (x, z) = water.Gradient.Moving(through.X, through.Z);
        Pushed = (-share * x, -share * z);
        var (changeX, changeZ) = met ? (water.WaveX - waveX, water.WaveZ - waveZ) : (0, 0);
        ownX += (share - 1) * changeX;
        ownZ += (share - 1) * changeZ;
        (waveX, waveZ, met) = (water.WaveX, water.WaveZ, true);
        return (share * changeX / seconds + Pushed.X, share * changeZ / seconds + Pushed.Z);
    }
}
