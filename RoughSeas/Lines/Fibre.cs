namespace RoughSeas.Lines;

public readonly record struct Fibre(double KilogramsPerCubicMetre, double BreakingNewtons, double MeasuredMetres, double StretchAtBreak)
{
    public double BreakingAt(double diameterMetres) => BreakingNewtons * diameterMetres * diameterMetres / (MeasuredMetres * MeasuredMetres);
}
