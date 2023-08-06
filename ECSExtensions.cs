
using System;
using Bloodstone.API;
using Il2CppInterop.Runtime;
using Unity.Entities;


/// <remarks>
/// Copying these around is an anti-pattern - they should live in Bloodstone. I've put them here for now to uncouple things.
/// CS8500
/// </remarks>
#pragma warning disable CS8500
internal static class ECSExtensions
{
	internal static void With<T>(this Entity entity, VExtensions.ActionRef<T> action) where T : struct
	{
		T item = entity.RW<T>();
		action(ref item);
		VWorld.Game.EntityManager.SetComponentData(entity, item);
	}

	internal static bool Has<T>(this Entity entity) where T : struct
	{
		int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<T>());
		
		return VWorld.Game.EntityManager.HasComponentRaw(entity, typeIndex);
	}

	internal unsafe static T RW<T>(this Entity entity) where T: struct
	{
		int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<T>());
		T* componentDataRawRW = (T*)VWorld.Game.EntityManager.GetComponentDataRawRW(entity, typeIndex);
		if (componentDataRawRW == null)
		{
			throw new InvalidOperationException($"Failure to access ReadWrite <{typeof(T).Name}> typeIndex({typeIndex}) on entity({entity}).");
		}
		return *componentDataRawRW;
	}

	internal unsafe static T Read<T>(this Entity entity) where T : struct
	{
		int typeIndex = TypeManager.GetTypeIndex(Il2CppType.Of<T>());
		T* componentDataRawRO = (T*)VWorld.Game.EntityManager.GetComponentDataRawRO(entity, typeIndex);
		if(componentDataRawRO == null)
		{
			throw new InvalidOperationException($"Failure to access ReadOnly <{typeof(T).Name}> typeIndex({typeIndex}) on entity({entity}).");
		}
		return *componentDataRawRO;
	}
}
#pragma warning restore CS8500
