namespace RoughSeas.Wind;

public interface IWindOnHull
{
    WindLoad Wind((double X, double Z) velocity);
}
