namespace LeadAHorseToWater.VCFCompat;

using System.Collections.Generic;
using LeadAHorseToWater.Compat;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

internal static class HorseUtil
{
	private static Entity empty_entity = new Entity();

	public static PrefabGUID DeadVampHorseBuffGuid = new PrefabGUID(525019977);

	private static Dictionary<string, PrefabGUID> HorseGuids = new()
	{
			{ "Regular", new(1149585723) },
			//{ "Gloomrot", new(1213710323) },
			{ "Spectral", new(2022889449) },
			// HARD CRASH { "Vampire", new(-1502865710) },// CHAR_Mount_Horse
	};


	internal static void SpawnHorse(int countlocal, float3 localPos)
	{
		var horse = HorseGuids["Regular"];
		// TODO: Cache and Improve (np now :P)
		VWorld.Server.GetExistingSystemManaged<UnitSpawnerUpdateSystem>().SpawnUnit(empty_entity, horse, localPos, countlocal, 1, 2, -1);
	}

	internal static NativeArray<Entity> GetHorses()
	{
		var horseQuery = VWorld.Server.EntityManager.CreateEntityQuery(new EntityQueryDesc()
		{
			All = new[] {
					ComponentType.ReadWrite<NameableInteractable>(),
					ComponentType.ReadWrite<Mountable>(),
					ComponentType.ReadOnly<LocalToWorld>(),
					ComponentType.ReadOnly<Team>()
				},
			None = new[] { ComponentType.ReadOnly<Dead>(), ComponentType.ReadOnly<DestroyTag>() }
		});

		return horseQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
	}

	internal static Entity? GetClosetHorse(Entity e)
	{
		var horseEntityQuery = GetHorses();

		var origin = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(e).Position;
		var closest = float.MaxValue;

		Entity? closestHorse = null;
		foreach (var horse in horseEntityQuery)
		{
			var position = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horse).Position;
			var distance = UnityEngine.Vector3.Distance(origin, position); // wait really?
			if (distance < closest)
			{
				closest = distance;
				closestHorse = horse;
			}
		}

		return closestHorse;
	}


	internal static List<Entity> ClosestHorses(Entity e, float radius = 5f)
	{
		var horses = GetHorses();
		var results = new List<Entity>();
		var origin = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(e).Position;

		foreach (var horse in horses)
		{
			var position = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horse).Position;
			var distance = UnityEngine.Vector3.Distance(origin, position); // wait really?
			if (distance < radius)
			{
				results.Add(horse);
			}
		}

		return results;
	}

    /// <summary>
	/// Kill a horse without dropping any loot
	/// </summary>
	/// <param name="horse"></param>
	internal static void KillWithNoDrops(Entity horse)
	{
		if (horse.Has<Immortal>()) {
			// Don't actually kill a vampire horse, otherwise the owner won't be able to resummon it.
			// Instead, the game has a special buff for temporarily "killing" vamp horses.
			var des = VWorld.Server.GetExistingSystemManaged<DebugEventsSystem>();
			var buffEvent = new ApplyBuffDebugEvent()
			{
				BuffPrefabGUID = DeadVampHorseBuffGuid
			};

			// using FromCharacter like this feels dirty but it works. Manually managing a buff entity for a BuffBuffer to reference would be wayyyyy too complicated.
			var fromCharacter = new FromCharacter()
			{
				User = horse,
				Character = horse
			};
			des.ApplyBuff(fromCharacter, buffEvent);
			return;
		}

		horse.With((ref Health t) =>
		{
			t.IsDead = true;
		});
		VWorld.Server.EntityManager.AddComponent(horse, Il2CppType.Of<Dead>());
	}
}
