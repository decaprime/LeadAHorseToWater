using System;
using BepInEx.Logging;
using LeadAHorseToWater.Compat;
using HarmonyLib;
using LeadAHorseToWater.Processes;
using LeadAHorseToWater.VCFCompat;
using ProjectM;
using ProjectM.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace LeadAHorseToWater.Patches;
 
/**
 * We don't need to patch any particular system, we just need our own pseudo-system to run during the OnUpdate phase
 * Some attempted things that didn't work:
 *  - FeedableInventorySystem_Update (this was hooked in previous versions of this mod. It used to extend SystemBase but not any more. guessing deprecated?)
 *  - FeedableInventorySystem_Spawn (seems to handle spawning of feedable inventories. rarely updates)
 *  - FeedInteractionProgressSystem (this seems to be for sucking blood) 
 *  - MountInventorySystem_Server (this doesn't implement SystemBase. guessing depcrecated?)
 */
[HarmonyPatch(typeof(CheckInSunSystem), "OnUpdate")]
public static class LeadAHorseToWaterSystem
{
	private static ManualLogSource _log => Plugin.LogInstance;

	private static DateTime NoUpdateBefore = DateTime.MinValue;

	public static WellCacheProcess Wells { get; } = new();

	public static void Prefix(CheckInSunSystem __instance)
	{
		try
		{
			if (NoUpdateBefore > DateTime.Now)
			{
				return;
			}

			NoUpdateBefore = DateTime.Now.AddSeconds(1.5);

            var horses = HorseUtil.GetHorses();
			if (horses.Length == 0)
			{
				return;
			}

			Wells.Update();
			BreedHorseProcess.Update(horses);

			foreach (var horseEntity in horses)
			{
				if (!IsHorseWeFeed(horseEntity)) continue;

				var localToWorld = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horseEntity);
				var horsePosition = localToWorld.Position;

				//_log?.LogDebug($"Horse <{horseEntity.Index}> Found at {horsePosition}:");
				bool closeEnough = false;
				foreach (var wellPosition in Wells.Positions)
				{
					var distance = Vector3.Distance(wellPosition, horsePosition);
					//_log?.LogDebug($"\t\tWell={wellPosition} Distance={distance}");

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
					//_log?.LogDebug($"Feeding horse <{horseEntity.Index}> Found inventory: FeedTime={inventory.FeedTime} FeedProgressTime={inventory.FeedProgressTime} IsFed={inventory.IsFed} DamageTickTime={inventory.DamageTickTime} IsActive={inventory.IsActive}");
					inventory.FeedProgressTime = Mathf.Max(inventory.FeedProgressTime, Mathf.Min(inventory.FeedProgressTime + Settings.SECONDS_DRINK_PER_TICK.Value, Settings.MAX_DRINK_AMOUNT.Value));
					inventory.IsFed = true; // re-enable ticking of feed progress in case it was already depleted (e.g. a starving horse).
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

	private static bool IsHorseWeFeed(Entity horse)
	{
		EntityManager em = VWorld.Server.EntityManager;
		if (!horse.Has<FeedableInventory>()) {
			return false;
		}

		ComponentLookup<Team> getTeam = em.GetComponentLookup<Team>(true);
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
}
