namespace RoughSeas.Wind;

/// <summary>
/// The one shape of a cloth between its four corners: physics asks it for the centre of effort, the renderer for every
/// vertex. Reach runs from luff to leech, span from foot to head; a reef rolls the cloth up from the foot onto the boom.
/// </summary>
public sealed class SailForm
{
    private const int CentroidCells = 16;

    private readonly HullPoint luffFoot;
    private readonly HullPoint luffHead;
    private readonly HullPoint leechHead;
    private readonly HullPoint leechFoot;
    private readonly double camber;
    private readonly (double Reach, double Span) centroid;

    public SailForm(HullPoint luffFoot, HullPoint luffHead, HullPoint leechHead, HullPoint leechFoot, double camber)
        : this(luffFoot, luffHead, leechHead, leechFoot, camber, default)
    {
        centroid = Centroid();
    }

    private SailForm(HullPoint luffFoot, HullPoint luffHead, HullPoint leechHead, HullPoint leechFoot, double camber, (double Reach, double Span) centroid)
    {
        (this.luffFoot, this.luffHead, this.leechHead, this.leechFoot, this.camber, this.centroid) = (luffFoot, luffHead, leechHead, leechFoot, camber, centroid);
        Normal = (leechHead - luffFoot).Cross(leechFoot - luffHead).Unit;
    }

    public static readonly (double Reach, double Span) Whole = (1, 1);

    public HullPoint Normal { get; }

    public SailForm Posed(HullPoint luffFoot, HullPoint luffHead, HullPoint leechHead, HullPoint leechFoot) =>
        new(luffFoot, luffHead, leechHead, leechFoot, camber, centroid);

    public HullPoint At(double reach, double span, double depth) => At(reach, span, depth, Whole);

    public HullPoint At(double reach, double span, double depth, (double Reach, double Span) set)
    {
        reach *= set.Reach;
        var foot = (1 - reach) * luffFoot + reach * leechFoot;
        var head = (1 - reach) * luffHead + reach * leechHead;
        var flat = foot + (1 - set.Span + span * set.Span) * (head - foot);
        return flat + depth * 4 * camber * set.Span * (head - foot).Length * span * (1 - span) * Normal;
    }

    public HullPoint Centre((double Reach, double Span) set, double depth) => At(centroid.Reach, centroid.Span, depth, set);

    private (double Reach, double Span) Centroid()
    {
        double area = 0, reach = 0, span = 0;
        const double half = 0.5 / CentroidCells;
        for (var i = 0; i < CentroidCells; i++)
        for (var j = 0; j < CentroidCells; j++)
        {
            var (s, t) = ((i + 0.5) / CentroidCells, (j + 0.5) / CentroidCells);
            var cell = (At(s + half, t, 0) - At(s - half, t, 0)).Cross(At(s, t + half, 0) - At(s, t - half, 0)).Length;
            (area, reach, span) = (area + cell, reach + cell * s, span + cell * t);
        }
        return (reach / area, span / area);
    }
}
