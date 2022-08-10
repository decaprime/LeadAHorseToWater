using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Wetstone.API;

namespace LeadAHorseToWater
{
	[HarmonyPatch(typeof(FeedableInventorySystem_Update), "OnUpdate")]
	public static class FeedSystemUpdatePatch
	{
		private static ManualLogSource _log => Plugin.LogInstance;

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

					horseEntity.WithComponentData<FeedableInventory>((ref FeedableInventory inventory) =>
					{
						_log?.LogDebug($"Feeding horse <{horseEntity.Index}> Found inventory: FeedTime={inventory.FeedTime} FeedProgressTime={inventory.FeedProgressTime} IsFed={inventory.IsFed} DamageTickTime={inventory.DamageTickTime} IsActive={inventory.IsActive}");
						inventory.FeedProgressTime = Mathf.Min(inventory.FeedProgressTime + Settings.SECONDS_DRINK_PER_TICK.Value, Settings.MAX_DRINK_AMOUNT.Value);
						inventory.IsFed = true; // don't drink canteens?
					});
				}
			}
			catch (Exception e)
			{
				_log?.LogError(e.ToString());
			}
		}

		private static void HandleRename(Entity horseEntity, bool closeEnough)
		{
			if (!Settings.ENABLE_RENAME.Value) return;

			horseEntity.WithComponentData<NameableInteractable>((ref NameableInteractable nameable) =>
			{
				var name = nameable.Name.ToString();
				var prefix = Settings.ENABLE_PREFIX_COLOR.Value ?
					$"<color=#0ef>{Settings.DRINKING_PREFIX.Value}</color> " :
					$"{Settings.DRINKING_PREFIX.Value} ";
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
			_log?.LogDebug($"Horse <{horse.Index}]> IsUnit={isUnit}");

			// Wild horses are Units, appear to no longer be units after you ride them.
			return !isUnit;
		}

		private static Vector3 FromFloat3(float3 vec) => new Vector3(vec.x, vec.y, vec.z);
	}
}
