using System;
using BepInEx.Logging;
using Bloodstone.API;
using HarmonyLib;
using LeadAHorseToWater.Processes;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace LeadAHorseToWater.Patches;

[HarmonyPatch(typeof(FeedableInventorySystem_Update), "OnUpdate")]
public static class FeedableInventorySystem_Update_Patch
{
	private static ManualLogSource _log => Plugin.LogInstance;

	private static DateTime NoUpdateBefore = DateTime.MinValue;

	public static WellCacheProcess Wells { get; } = new();

	public static void Prefix(FeedableInventorySystem_Update __instance)
	{
		try
		{
			if (NoUpdateBefore > DateTime.Now)
			{
				return;
			}

			NoUpdateBefore = DateTime.Now.AddSeconds(1);

			var horses = __instance.__OnUpdate_LambdaJob0_entityQuery.ToEntityArray(Allocator.Temp);

			if (horses.Length == 0)
			{
				return;
			}

			Wells.Update();

			BreedHorseProcess.Update(horses);
			CleanUpPrefixProcess.Update(horses);
			BreedTimerProcess.Instance.Update();

			foreach (var horseEntity in horses)
			{
				if (!IsHorseWeFeed(horseEntity, __instance)) continue;

				var localToWorld = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horseEntity);
				var horsePosition = localToWorld.Position;

				_log?.LogDebug($"Horse <{horseEntity.Index}> Found at {horsePosition}:");
				bool closeEnough = false;
				foreach (var wellPosition in Wells.Positions)
				{
					var distance = Vector3.Distance(wellPosition, horsePosition);
					_log?.LogDebug($"\t\tWell={wellPosition} Distance={distance}");

					if (distance < Settings.DISTANCE_REQUIRED.Value)
					{
						closeEnough = true;
						break;
					}
				}

				HandleRename(horseEntity, closeEnough);

				if (!closeEnough) continue;

				horseEntity.WithComponentData((ref FeedableInventory inventory) =>
				{
					_log?.LogDebug(
						$"Feeding horse <{horseEntity.Index}> Found inventory: FeedTime={inventory.FeedTime} FeedProgressTime={inventory.FeedProgressTime} IsFed={inventory.IsFed} DamageTickTime={inventory.DamageTickTime} IsActive={inventory.IsActive}");
					inventory.FeedProgressTime =
						Mathf.Min(inventory.FeedProgressTime + Settings.SECONDS_DRINK_PER_TICK.Value,
							Settings.MAX_DRINK_AMOUNT.Value);
					inventory.IsFed = true; // don't drink canteens?
				});
			}
		}
		catch (Exception e)
		{
			_log?.LogError(e.ToString());
		}
	}

	private const string DRINKING_PREFIX = "♻ ";

	private static void HandleRename(Entity horseEntity, bool closeEnough)
	{
		if (!Settings.ENABLE_RENAME.Value) return;

		horseEntity.WithComponentData((ref NameableInteractable nameable) =>
		{
			var name = nameable.Name.ToString();
			var hasPrefix = name.StartsWith(DRINKING_PREFIX);

			if (!closeEnough && hasPrefix)
			{
				nameable.Name = name.Substring(DRINKING_PREFIX.Length);
				return;
			}

			if (closeEnough && !hasPrefix)
			{
				nameable.Name = DRINKING_PREFIX + name;
				return;
			}
		});
	}

	private static bool IsHorseWeFeed(Entity horse, ComponentSystemBase instance)
	{
		EntityManager em = instance.World.EntityManager;
		ComponentDataFromEntity<Team> getTeam = instance.GetComponentDataFromEntity<Team>();

		if (em.HasComponent<Team>(horse))
		{
			var teamhorse = getTeam[horse];
			var isUnit = Team.IsInUnitTeam(teamhorse);

			// Wild horses are Units, appear to no longer be units after you ride them.
			return !isUnit;
		}

		// Handle the case when the horse entity does not have the Team component.
		_log?.LogDebug($"Horse <{horse.Index}> does not have Team component. {horse}");
		return false;
	}
	//old code pre gloomrot
	//var tc = TeamChecker.CreateWithoutCache(instance);
	//var horseTeam = tc.GetTeam(horse);
	//var isUnit = tc.IsUnit(horseTeam);
	//return !isUnit;
}
