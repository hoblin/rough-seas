namespace RoughSeas.Wind;

public interface IRigged
{
    IAloft Aloft { get; }

    void Hoist();

    void Blown(double seconds);
}
