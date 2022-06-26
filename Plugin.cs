using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using ProjectM.CastleBuilding;
using ProjectM.Gameplay;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wetstone.API;

namespace LeadAHorseToWater
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInDependency("xyz.molenzwiebel.wetstone")]
	[Wetstone.API.Reloadable]
	public class Plugin : BasePlugin
	{
		public static ManualLogSource logger;
		public static ConfigEntry<float> DISTANCE_REQUIRED;
		public static ConfigEntry<int> SECONDS_DRINK_PER_TICK;
		public static ConfigEntry<int> MAX_DRINK_AMOUNT;
		private static ConfigEntry<string> DRINKING_PREFIX;

		private HarmonyLib.Harmony _harmony;
		public override void Load()
		{
			logger = this.Log;

			// Confg
			DISTANCE_REQUIRED = Config.Bind<float>("Server", "DistanceRequired", 5.0f, "Horses must be within this distance from well. (5 =1 tile)");
			SECONDS_DRINK_PER_TICK = Config.Bind<int>("Server", "SecondsDrinkPerTick", 30, "How many seconds added per drink tick (~1.5seconds), default values would be about 24 minutes for the default max amount at fountain.");
			MAX_DRINK_AMOUNT = Config.Bind<int>("Server", "MaxDrinkAmount", 28800, "Time in seconds, default value is roughly amount of time when you take wild horses.");
			DRINKING_PREFIX = Config.Bind<string>("Server", "DrinkingPrefix", "[Drinking] ", "Prefix to use on horses that are drinking");

			// Server plugin check
			if (!VWorld.IsServer)
			{
				Log.LogWarning("This plugin is a server-only plugin.");
				return;
			}

			// Plugin startup logic
			_harmony = new HarmonyLib.Harmony(PluginInfo.PLUGIN_GUID);
			_harmony.PatchAll();
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
		}

		public override bool Unload()
		{
			_harmony.UnpatchSelf();
			return true;
		}

		[HarmonyPatch(typeof(FeedableInventorySystem_Update), "OnUpdate")]
		public static class FeedSystem_OnUpdate_Patch
		{
			private static DateTime NoUpdateBefore = DateTime.MinValue;

			public static void Prefix(FeedableInventorySystem_Update __instance)
			{
				try
				{
					if (NoUpdateBefore > DateTime.Now)
					{
						return;
					}

					NoUpdateBefore = DateTime.Now.AddMilliseconds(1500);

					// Find Wells (TODO: Possibly Async of Update)
					var wellQuery = VWorld.Server.EntityManager.CreateEntityQuery(
						ComponentType.ReadOnly<Team>(),
						ComponentType.ReadOnly<CastleHeartConnection>(),
						ComponentType.ReadOnly<BlueprintData>(),
						ComponentType.ReadOnly<LocalToWorld>()
					);

					var wellEntities = wellQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
					var count = 0;
					var wellLocations = new List<Vector3>();
					foreach (var well in wellEntities)
					{
						count++;
						var blueprintData = VWorld.Server.EntityManager.GetComponentData<BlueprintData>(well);
						var location = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(well);
						if (blueprintData.Guid.GuidHash == 986517450)
						{
							logger?.LogDebug($"Well Found: [{count}]: Blueprint GUID={blueprintData.Guid}, Location={location.Position}");
							wellLocations.Add(FromFloat3(location.Position));
						}
					}

					// Find Horses
					// - Question: which one of these is lost when you get on the mount?
					var horseQuery = __instance.EntityManager.CreateEntityQuery(
						ComponentType.ReadWrite<FeedableInventory>(),
						ComponentType.ReadWrite<NameableInteractable>(),
						ComponentType.ReadOnly<Team>()
					);

					var horseEntityQuery = horseQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

					var ids = new List<string>();
					foreach (var horseEntity in horseEntityQuery)
					{
						ids.Add(horseEntity.Index.ToString());
					}

					logger?.LogDebug($"Found {horseEntityQuery.Length} horses with Ids [{string.Join(", ", ids)}]");

					foreach (var horseEntity in horseEntityQuery)
					{
						if (!VWorld.Server.EntityManager.HasComponent(horseEntity, ComponentType.ReadOnly<LocalToWorld>()))
						{
							logger?.LogWarning($"Horse <{horseEntity.Index}> No location identified, was this maybe culled or still loading?");
							continue;
						}

						var localToWorld = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horseEntity);
						var horsePosition = FromFloat3(localToWorld.Position);

						logger?.LogDebug($"Horse Found at {horsePosition}:");
						bool closeEnough = false;
						foreach (var wellPosition in wellLocations)
						{
							var distance = Vector3.Distance(wellPosition, horsePosition);
							logger?.LogDebug($"\t\tWell={wellPosition} Distance={distance}");

							if (distance < DISTANCE_REQUIRED.Value)
							{
								closeEnough = true;
								break;
							}
						}

						horseEntity.WithComponentData<NameableInteractable>((ref NameableInteractable nameable) =>
						{
							var name = nameable.Name.ToString();
							var prefix = $"<color=#0ef>{DRINKING_PREFIX.Value}</color> ";
							bool hasPrefix = name.StartsWith(prefix);

							if (!closeEnough && hasPrefix)
							{
								nameable.Name = name.Substring(prefix.Length);
								return;
							}

							if (closeEnough && !hasPrefix)
							{
								nameable.Name = prefix + name;
								return;
							}
						});

						if (!closeEnough) continue;

						if (!IsHorseWeFeed(horseEntity, __instance)) continue;

						horseEntity.WithComponentData<FeedableInventory>((ref FeedableInventory inventory) =>
						{
							logger?.LogDebug($"Horse <{horseEntity.Index}> Found inventory: FeedTime={inventory.FeedTime} FeedProgressTime={inventory.FeedProgressTime} IsFed={inventory.IsFed} DamageTickTime={inventory.DamageTickTime} IsActive={inventory.IsActive}");
							inventory.FeedProgressTime = Mathf.Min(inventory.FeedProgressTime + SECONDS_DRINK_PER_TICK.Value, MAX_DRINK_AMOUNT.Value);
							inventory.IsFed = true; // don't drink canteens?
						});
					}
				}
				catch (Exception e)
				{
					logger?.LogError(e.ToString());
				}
			}

			private static bool IsHorseWeFeed(Entity horse, ComponentSystemBase instance)
			{
				var tc = TeamChecker.CreateWithoutCache(instance);
				var horseTeam = tc.GetTeam(horse);
				var isUnit = tc.IsUnit(horseTeam);
				logger?.LogDebug($"Horse <{horse.Index}]> IsUnit={isUnit}");

				// Wild horses are Units, appear to no longer be units after you ride them.
				return !isUnit;
			}

			private static Vector3 FromFloat3(float3 vec) => new Vector3(vec.x, vec.y, vec.z);
		}
	}
}
