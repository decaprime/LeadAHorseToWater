using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using ProjectM.CastleBuilding.Placement;
using ProjectM.Network;
using System;
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
	public partial class Plugin : BasePlugin
	{
		public static ManualLogSource logger;
		public static ConfigEntry<float> DISTANCE_REQUIRED;
		public static ConfigEntry<int> SECONDS_DRINK_PER_TICK;
		public static ConfigEntry<int> MAX_DRINK_AMOUNT;
		private static ConfigEntry<string> DRINKING_PREFIX;
		private static ConfigEntry<bool> ENABLE_RENAME;
		private static ConfigEntry<bool> ENABLE_PREFIX_COLOR;


		private HarmonyLib.Harmony _harmony;
		public override void Load()
		{
			logger = this.Log;

			// Confg
			DISTANCE_REQUIRED = Config.Bind<float>("Server", "DistanceRequired", 5.0f, "Horses must be within this distance from well. (5 =1 tile)");
			SECONDS_DRINK_PER_TICK = Config.Bind<int>("Server", "SecondsDrinkPerTick", 30, "How many seconds added per drink tick (~1.5seconds), default values would be about 24 minutes for the default max amount at fountain.");
			MAX_DRINK_AMOUNT = Config.Bind<int>("Server", "MaxDrinkAmount", 28800, "Time in seconds, default value is roughly amount of time when you take wild horses.");

			ENABLE_RENAME = Config.Bind<bool>("Server", "EnableRename", true, "If true will rename horses in drinking range with the DrinkingPrefix");
			ENABLE_PREFIX_COLOR = Config.Bind<bool>("Server", "EnablePrefixColor", true, "If true use a different color for the DrinkingPrefix");
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

		[HarmonyPatch(typeof(PlaceTileModelSystem))]
		public static class PlaceTileModelSystem_Patches
		{
			[HarmonyPostfix]
			[HarmonyPatch(nameof(PlaceTileModelSystem.ClearEditing))]
			public static void Moving(PlaceTileModelSystem __instance, EntityManager entityManager, Entity tileModelEntity, Entity character)
			{
				// ideally we can just invalidate this tileModelEntity and get it's location ext hooked update
				FeedSystemUpdatePatch.Wells.InvalidateEntity(tileModelEntity);
				logger?.LogDebug($"Pseudo Moving Event Entity:{tileModelEntity.Index}");
			}

			[HarmonyPostfix]
			[HarmonyPatch(nameof(PlaceTileModelSystem.VerifyCanBuildTileModels))]
			public static void Destroying(PlaceTileModelSystem __instance, EntityManager entityManager, Entity character)
			{
				// we don't even need the entity id, just that a well was destroyed so we can invalidate the cache
				// and search for which entities no longer match a well. We may also scan for some in-destruction event/component.
				// we can also use the location to filter our cache and only invalidate the wells near the destroying player.			
				FeedSystemUpdatePatch.Wells.InvalidateAll();
				logger?.LogDebug($"Pseudo Destroy Event");
			}

			[HarmonyPostfix]
			[HarmonyPatch(nameof(PlaceTileModelSystem.HasUnlockedBlueprint))]
			public static void Building(PlaceTileModelSystem __instance, Entity user, PrefabGUID prefabGuid, Entity prefab)
			{
				if (!WellCache.IsWellPrefab(prefabGuid)) return;
				// we need to scan again if we build, new entity id will need to be tracked, is this prefab entity id the well id?
				// however we can do an 'active' component scan to find the well id since it should be near a player
				FeedSystemUpdatePatch.Wells.PlanScanForAdded();
				logger?.LogDebug($"Pseudo Building Event prefab: {prefabGuid}, entity: {prefab.Index}");
			}
		}

		[HarmonyPatch(typeof(FeedableInventorySystem_Update), "OnUpdate")]
		public static class FeedSystemUpdatePatch
		{
			private static DateTime NoUpdateBefore = DateTime.MinValue;

			public static WellCache Wells { get; } = new();

			public static void Prefix(FeedableInventorySystem_Update __instance)
			{
				try
				{
					if (NoUpdateBefore > DateTime.Now)
					{
						return;
					}

					NoUpdateBefore = DateTime.Now.AddSeconds(1.5);

					var horseEntityQuery = __instance.__OnUpdate_LambdaJob0_entityQuery.ToEntityArray(Allocator.Temp);


					if (horseEntityQuery.Length == 0)
					{
						return;
					}

					Wells.Update();

					foreach (var horseEntity in horseEntityQuery)
					{
						if (!IsHorseWeFeed(horseEntity, __instance)) continue;

						var localToWorld = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horseEntity);
						var horsePosition = FromFloat3(localToWorld.Position);

						logger?.LogDebug($"Horse <{horseEntity.Index}> Found at {horsePosition}:");
						bool closeEnough = false;
						foreach (var wellPosition in Wells.Positions)
						{
							var distance = Vector3.Distance(wellPosition, horsePosition);
							logger?.LogDebug($"\t\tWell={wellPosition} Distance={distance}");

							if (distance < DISTANCE_REQUIRED.Value)
							{
								closeEnough = true;
								break;
							}
						}

						HandleRename(horseEntity, closeEnough);

						if (!closeEnough) continue;

						horseEntity.WithComponentData<FeedableInventory>((ref FeedableInventory inventory) =>
						{
							logger?.LogDebug($"Feeding horse <{horseEntity.Index}> Found inventory: FeedTime={inventory.FeedTime} FeedProgressTime={inventory.FeedProgressTime} IsFed={inventory.IsFed} DamageTickTime={inventory.DamageTickTime} IsActive={inventory.IsActive}");
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

			private static void HandleRename(Entity horseEntity, bool closeEnough)
			{
				if (!ENABLE_RENAME.Value) return;

				horseEntity.WithComponentData<NameableInteractable>((ref NameableInteractable nameable) =>
				{
					var name = nameable.Name.ToString();
					var prefix = ENABLE_PREFIX_COLOR.Value ?
						$"<color=#0ef>{DRINKING_PREFIX.Value}</color> " :
						$"{DRINKING_PREFIX.Value} ";
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
