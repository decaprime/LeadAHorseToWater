namespace LeadAHorseToWater.VCFCompat
{
	using ProjectM;
	using Unity.Entities;
	using Unity.Transforms;
	using Wetstone.API;
	using Unity.Collections;
	using Unity.Mathematics;
	using System.Collections.Generic;

	public static partial class Commands
	{
		internal static class HorseUtil
		{
			private static Entity empty_entity = new Entity();

			internal static void SpawnHorse(int countlocal, float3 localPos)
			{
				// TODO: Cache and Improve
				var prefabCollectionSystem = VWorld.Server.GetExistingSystem<PrefabCollectionSystem>();
				var entityName = "CHAR_Town_Horse";
				foreach (var kv in prefabCollectionSystem._PrefabGuidToNameMap)
				{
					if (kv.Value.ToString().ToLower() != entityName.ToLower()) continue;
					VWorld.Server.GetExistingSystem<UnitSpawnerUpdateSystem>().SpawnUnit(empty_entity, kv.Key, new float3(localPos.x, 0, localPos.z), countlocal, 1, 2, -1);
					break;
				}
			}

			internal static NativeArray<Entity> GetHorses()
			{
				var horseQuery = VWorld.Server.EntityManager.CreateEntityQuery(new EntityQueryDesc()
				{
					All = new[] { ComponentType.ReadWrite<FeedableInventory>(),
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
		}
	}
}