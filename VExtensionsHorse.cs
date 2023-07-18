using System;
using Bloodstone.API;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;

namespace LeadAHorseToWater;

public static class VExtensionsHorse
{
    public static void WithComponentDataH<T>(this Entity entity, ActionRefH<T> action) where T : struct
    {
        VWorld.Game.EntityManager.TryGetComponentData<T>(entity, out var componentData);
        action(ref componentData);
        VWorld.Game.EntityManager.SetComponentData<T>(entity, componentData);
    }

    public delegate void ActionRefH<T>(ref T item);
}