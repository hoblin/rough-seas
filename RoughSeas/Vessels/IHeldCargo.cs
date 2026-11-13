namespace RoughSeas.Vessels;

/// <summary>What a vessel carries in holds of her own, beside her riders and her accessories.</summary>
public interface IHeldCargo
{
    bool Empty { get; }

    void DropAll();
}
