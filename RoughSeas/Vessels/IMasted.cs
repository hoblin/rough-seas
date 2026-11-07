namespace RoughSeas.Vessels;

/// <summary>A vessel whose mast stands only once it is stepped.</summary>
public interface IMasted
{
    /// <summary>The height of her mast as far as she is built, from the finished vessel's.</summary>
    double MastMetres(double designed);
}
