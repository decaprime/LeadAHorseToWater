using BepInEx.IL2CPP;
using BepInEx.Logging;

namespace LeadAHorseToWater.VCFCompat
{
	using ProjectM;
	using Unity.Entities;
	using Unity.Transforms;
	using VampireCommandFramework;
	using Wetstone.API;
	using Unity.Collections;
	using Unity.Mathematics;
	using System.Collections.Generic;
	using System;

	public static class Commands
	{
		private static ManualLogSource _log => Plugin.LogInstance;

		static Commands()
		{
			Enabled = IL2CPPChainloader.Instance.Plugins.TryGetValue("gg.deca.VampireCommandFramework", out var info);
			if (Enabled) _log.LogWarning($"VCF Version: {info.Metadata.Version}");
		}

		public static bool Enabled { get; private set; }

		public static void Register() => CommandRegistry.RegisterCommandType(typeof(HorseCommands));

		private static System.Random _random = new();

		public class HorseCommands
		{
			private static PrefabGUID BreedItemType = new PrefabGUID(-570287766);

			[ChatCommand("breed")]
			public void Breed(CommandContext ctx)
			{
				var character = ctx.Event.SenderCharacterEntity;

				if (BreedHorseProcess.NextBabyData != null)
				{
					throw ctx.Error("Already breeding horse...wait a sec..");
				}

				if (!InventoryUtilities.TryGetInventoryEntity(VWorld.Server.EntityManager, character, out Entity invEntity))
				{
					return;
				}

				var horses = ClosestHorses(character);
				if (horses.Count != 2)
				{
					throw ctx.Error($"Must have only two nearby horses, found {horses.Count}");
				}

				var didRemove = InventoryUtilitiesServer.TryRemoveItem(VWorld.Server.EntityManager, invEntity, BreedItemType, 1);
				_log?.LogWarning($"Tried to remove 1, removed={didRemove}");
				if (!didRemove)
				{
					throw ctx.Error("No fish given, no fuck given");
				};

				var pos1 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[0]).Value;
				var pos2 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[1]).Value;

				var babyPos = UnityEngine.Vector3.Lerp(pos1, pos2, 0.5f);

				var mountData1 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[0]);
				ctx.Reply($"Parent {horses[0].Index}\n Speed: {mountData1.MaxSpeed}\n Acceleration: {mountData1.Acceleration}\n Rotation: {mountData1.RotationSpeed}");
				var mountData2 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[1]);
				ctx.Reply($"Parent {horses[1].Index}\n Speed: {mountData2.MaxSpeed}\n Acceleration: {mountData2.Acceleration}\n Rotation: {mountData2.RotationSpeed}");

				var babySpeed = _random.NextDouble() > 0.5 ? mountData1.MaxSpeed : mountData2.MaxSpeed;
				var babyAcceleration = _random.NextDouble() > 0.5 ? mountData1.Acceleration : mountData2.Acceleration;
				var babyRotation = _random.NextDouble() > 0.5 ? mountData1.RotationSpeed : mountData2.RotationSpeed;


				// todo: check we can breed in that these are ours
				var team = VWorld.Server.EntityManager.GetComponentData<Team>(horses[0]);

				SpawnHorse(1, babyPos);
				ctx.Reply($"Spawned Baby\n Speed {babySpeed}\n Acceleration: {babyAcceleration}\n Rotation: {babyRotation}");

				BreedHorseProcess.NextBabyData = new BabyHorseData("Baby Horse", team, babyPos, babySpeed, babyAcceleration, babyRotation, horses[0].Index, horses[1].Index, DateTime.Now.AddSeconds(1.5));
			}
		}

		private static List<Entity> ClosestHorses(Entity e, float radius = 5f)
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

		private static NativeArray<Entity> GetHorses()
		{
			var horseQuery = VWorld.Server.EntityManager.CreateEntityQuery(
				ComponentType.ReadWrite<FeedableInventory>(),
				ComponentType.ReadWrite<NameableInteractable>(),
				ComponentType.ReadWrite<Mountable>(),
				ComponentType.ReadOnly<LocalToWorld>(),
				ComponentType.ReadOnly<Team>()
			);

			return horseQuery.ToEntityArray(Allocator.Temp);

		}
		private static Entity empty_entity = new Entity();

		private static void SpawnHorse(int countlocal, float3 localPos)
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
	}
}