using System;
using BepInEx.Logging;

namespace LeadAHorseToWater.VCFCompat
{
	using System.Collections.Generic;
	using Bloodstone.API;
	using ProjectM;
	using Unity.Collections;
	using Unity.Entities;
	using Unity.Mathematics;
	using Unity.Transforms;

	internal static class HorseUtil
	{
		private static ManualLogSource _log => Plugin.LogInstance;
		private static Entity empty_entity = new Entity();

		internal static void SpawnHorse(int countlocal, float3 localPos)
		{
			// TODO: Cache and Improve
			var prefabCollectionSystem = VWorld.Server.GetExistingSystem<PrefabCollectionSystem>();
			var entityName = "char_mount_horse";
			foreach (var kv in prefabCollectionSystem._SpawnableNameToPrefabGuidDictionary)
			{
				if (kv.Key.ToLower() != entityName.ToLower()) continue;
				var usus = VWorld.Server.GetExistingSystem<UnitSpawnerUpdateSystem>();
				usus.SpawnUnit(empty_entity, kv.Value, new float3(localPos.x, 0, localPos.z), countlocal, 1, 2, -1);
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

			return horseQuery.ToEntityArray(Allocator.Temp);
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

		internal static bool isTamed(Entity e)
		{
			EntityManager em = VWorld.Server.EntityManager;
			ComponentDataFromEntity<Team> getTeam = VWorld.Server.EntityManager.GetComponentDataFromEntity<Team>();

			if (!em.HasComponent<Team>(e)) return false;
			var teamhorse = getTeam[e];
			var isUnit = Team.IsInUnitTeam(teamhorse);

			// Wild horses are Units, appear to no longer be units after you ride them.
			return !isUnit;

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
