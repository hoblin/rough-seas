namespace RoughSeas.Wind;

public sealed class SailForm
{
    private const int CentroidCells = 16;

    private readonly HullPoint luffFoot;
    private readonly HullPoint luffHead;
    private readonly HullPoint leechHead;
    private readonly HullPoint leechFoot;
    private readonly HullPoint origin;
    private readonly HullPoint axis;
    private readonly double camber;
    private readonly HullPoint normal;
    private readonly (double Reach, double Span) centroid;

    public SailForm(HullPoint luffFoot, HullPoint luffHead, HullPoint leechHead, HullPoint leechFoot, HullPoint origin, HullPoint axis, double camber)
    {
        (this.luffFoot, this.luffHead, this.leechHead, this.leechFoot, this.origin, this.axis, this.camber) =
            (luffFoot, luffHead, leechHead, leechFoot, origin, axis.Unit, camber);
        normal = (leechHead - luffFoot).Cross(leechFoot - luffHead).Unit;
        centroid = Centroid();
    }

    public static readonly (double Reach, double Span) Whole = (1, 1);

    public HullPoint Origin => origin;

    public HullPoint Axis => axis;

    public HullPoint At(double reach, double span, double turn, double depth) => At(reach, span, turn, depth, Whole);

    public HullPoint At(double reach, double span, double turn, double depth, (double Reach, double Span) set)
    {
        reach *= set.Reach;
        var foot = (1 - reach) * luffFoot + reach * leechFoot;
        var head = (1 - reach) * luffHead + reach * leechHead;
        var flat = foot + (1 - set.Span + span * set.Span) * (head - foot);
        var belly = depth * 4 * camber * set.Span * (head - foot).Length * span * (1 - span);
        return Turned(flat + belly * normal, turn);
    }

    public HullPoint Turned(HullPoint point, double turn) => point.Turned(origin, axis, turn);

    public HullPoint Normal(double turn) => normal.Turned(default, axis, turn);

    public HullPoint Centre((double Reach, double Span) set, double turn, double depth) => At(centroid.Reach, centroid.Span, turn, depth, set);

    private (double Reach, double Span) Centroid()
    {
        double area = 0, reach = 0, span = 0;
        const double half = 0.5 / CentroidCells;
        for (var i = 0; i < CentroidCells; i++)
        for (var j = 0; j < CentroidCells; j++)
        {
            var (s, t) = ((i + 0.5) / CentroidCells, (j + 0.5) / CentroidCells);
            var cell = (At(s + half, t, 0, 0) - At(s - half, t, 0, 0)).Cross(At(s, t + half, 0, 0) - At(s, t - half, 0, 0)).Length;
            (area, reach, span) = (area + cell, reach + cell * s, span + cell * t);
        }
        return (reach / area, span / area);
    }
}
