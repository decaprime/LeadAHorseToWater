using ProjectM;
using ProjectM.CastleBuilding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wetstone.API;

namespace LeadAHorseToWater
{
	public partial class Plugin
	{
		public class WellCache
		{
			public Dictionary<Entity, (bool IsInvalidated, float3 Location)> Cache { get; } = new();
			public IEnumerable<float3> Positions => Cache.Values.Select(x => x.Location);

			private const int MAX_BATCH = 1000;
				
			private bool _initialized = false;

			private Queue<Entity> _possibleWells = new();
			
			private bool _scanForAdded;

			public void PlanScanForAdded() => _scanForAdded = true;

			public void InvalidateEntity(Entity e)
			{
				if (Cache.ContainsKey(e))
				{
					Cache[e] = (true, Cache[e].Location);

				}
			}
			
			public void InvalidateAll()
			{
				foreach (var key in Cache.Keys.ToList())
				{
					Cache[key] = (true, Cache[key].Location);
				}
			}

			public void Update()
			{
				if (!_initialized)
				{
					_initialized = true;
					QueryForPossible(true);
				}
				else if (_scanForAdded)
				{
					QueryForPossible(false);
					_scanForAdded = false;
				}

				if (_possibleWells.Any())
				{

					Stopwatch sw = new();
					sw.Start();
					var batchSize = Math.Min(MAX_BATCH, _possibleWells.Count);
					for (var i = 0; i < batchSize; i++)
					{
						var entity = _possibleWells.Dequeue();
						// not needed because we know what we queried for, but things may have drifted
						UpdateCache(entity);
					}

					logger?.LogDebug($"Updated {batchSize} entities in {sw.ElapsedMilliseconds}ms, {_possibleWells.Count} remain, ending update.");
					return;
				}

				var invalidatedEntities = Cache.Where(x => x.Value.IsInvalidated).ToList();
				if (invalidatedEntities.Any())
				{
					Stopwatch sw = new();
					sw.Start();
					foreach (var needsUpdate in invalidatedEntities)
					{
						var entity = needsUpdate.Key;
						UpdateCache(entity);
					}
					logger?.LogDebug($"Invalidated {invalidatedEntities.Count} entities in {sw.ElapsedMilliseconds}ms, ending update.");
				}
			}
			public static bool IsWellPrefab(PrefabGUID guid) => guid.GuidHash == 986517450;

			private void UpdateCache(Entity entity)
			{
				if (VWorld.Server.EntityManager.HasComponent<BlueprintData>(entity) && VWorld.Server.EntityManager.HasComponent<LocalToWorld>(entity))
				{
					// todo: check for destroyed here
					var blueprintData = VWorld.Server.EntityManager.GetComponentData<BlueprintData>(entity);
					if (IsWellPrefab(blueprintData.Guid))
					{
						var location = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(entity);
						logger?.LogMessage($"Well Found:  Blueprint GUID={blueprintData.Guid}, Location={location.Position}");

						var newValue = (false, location.Position);

						if (Cache.ContainsKey(entity)) Cache[entity] = newValue;
						else Cache.Add(entity, newValue);

						return;
					}
				}

				if (Cache.Remove(entity))
				{
					logger?.LogDebug($"Updating {entity.Index} but no longer matches well.");
				}
			}

			public void QueryForPossible(bool includeDisabled)
			{
				logger?.LogDebug($"Querying for Wells FullMap={includeDisabled}");

				Stopwatch sw = new();
				sw.Start();
				var wellQuery = VWorld.Server.EntityManager.CreateEntityQuery(new EntityQueryDesc()
				{
					All = new ComponentType[] {
							ComponentType.ReadOnly<Team>(),
							ComponentType.ReadOnly<CastleHeartConnection>(),
							ComponentType.ReadOnly<BlueprintData>(),
							ComponentType.ReadOnly<LocalToWorld>(),
							ComponentType.ReadOnly<CastleAreaRequirement>(),
							ComponentType.ReadOnly<BlobAssetOwner>(),
							ComponentType.ReadOnly<TileModelRegistrationState>(),
						},
					Options = includeDisabled ? EntityQueryOptions.IncludeDisabled : EntityQueryOptions.Default
				});

				var wellEntities = wellQuery.ToEntityArray(Allocator.Temp);

				_possibleWells = new Queue<Entity>(wellEntities.ToArray());

				logger?.LogDebug($"Enqueued {_possibleWells.Count} in {sw.ElapsedMilliseconds}ms");
			}
		}
	}
}
