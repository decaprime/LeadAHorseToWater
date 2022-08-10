using BepInEx.Logging;
using HarmonyLib;
using ProjectM;
using Unity.Entities;

namespace LeadAHorseToWater
{
	[HarmonyPatch(typeof(PlaceTileModelSystem))]
	public static class PlaceTileModelSystem_Patches
	{
		private static ManualLogSource _log => Plugin.LogInstance;
		
		[HarmonyPostfix]
		[HarmonyPatch(nameof(PlaceTileModelSystem.ClearEditing))]
		public static void Moving(PlaceTileModelSystem __instance, EntityManager entityManager, Entity tileModelEntity, Entity character)
		{
			// ideally we can just invalidate this tileModelEntity and get it's location ext hooked update
			FeedSystemUpdatePatch.Wells.InvalidateEntity(tileModelEntity);
			_log?.LogDebug($"Pseudo Moving Event Entity:{tileModelEntity.Index}");
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(PlaceTileModelSystem.VerifyCanBuildTileModels))]
		public static void Destroying(PlaceTileModelSystem __instance, EntityManager entityManager, Entity character)
		{
			// we don't even need the entity id, just that a well was destroyed so we can invalidate the cache
			// and search for which entities no longer match a well. We may also scan for some in-destruction event/component.
			// we can also use the location to filter our cache and only invalidate the wells near the destroying player.			
			FeedSystemUpdatePatch.Wells.InvalidateAll();
			_log?.LogDebug($"Pseudo Destroy Event");
		}

		[HarmonyPostfix]
		[HarmonyPatch(nameof(PlaceTileModelSystem.HasUnlockedBlueprint))]
		public static void Building(PlaceTileModelSystem __instance, Entity user, PrefabGUID prefabGuid, Entity prefab)
		{
			if (!WellCache.IsWellPrefab(prefabGuid)) return;
			// we need to scan again if we build, new entity id will need to be tracked, is this prefab entity id the well id?
			// however we can do an 'active' component scan to find the well id since it should be near a player
			FeedSystemUpdatePatch.Wells.PlanScanForAdded();
			_log?.LogDebug($"Pseudo Building Event prefab: {prefabGuid}, entity: {prefab.Index}");
		}
	}
}
