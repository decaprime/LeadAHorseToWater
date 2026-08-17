namespace LeadAHorseToWater.Compat;

/// <summary>
/// Drop-in replacement for Bloodstone.API.VExtensions.
/// The mod only used the ActionRef delegate from it.
/// </summary>
public static class VExtensions
{
    public delegate void ActionRef<T>(ref T item);
}
