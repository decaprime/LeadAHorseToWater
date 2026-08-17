using Unity.Entities;

namespace LeadAHorseToWater.Compat;

/// <summary>
/// Drop-in replacement for Bloodstone's Entity.WithComponentData extension.
/// Forwards to the mod's own ECSExtensions.With, which does exactly the same job.
/// </summary>
internal static class EntityExtensions
{
    internal static void WithComponentData<T>(this Entity entity, VExtensions.ActionRef<T> action) where T : struct
    {
        entity.With(action);
    }
}
