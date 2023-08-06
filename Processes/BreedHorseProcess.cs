using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Bloodstone.API;
using ProjectM;
using ProjectM.Network;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace LeadAHorseToWater.Processes
{
	public record BabyHorseData(string name, Team team, float3 position, float speed, float acceleration, float rotation, int parentId1, int parentId2, DateTime notBefore);

	public static class BreedHorseProcess
	{
		private static ManualLogSource _log => Plugin.LogInstance;

		public static BabyHorseData NextBabyData = null;

		private static HashSet<int> _knownHorses = new();


		public static void Update(NativeArray<Entity> horses)
		{
			try
			{
				foreach (var horse in horses)
				{
					if (_knownHorses.Contains(horse.Index)) continue;
					VWorld.Server.EntityManager.TryGetComponentData<NetworkId>(horse, out var networkId);
					VWorld.Server.EntityManager.TryGetComponentData<Translation>(horse, out var position);

					_log.LogDebug($"Found horse {horse.Index} NetworkId={networkId} at {position.Value}");
					_knownHorses.Add(horse.Index);
				}


				if (NextBabyData == null) return;
				if (DateTime.Now < NextBabyData.notBefore) return;
				_log.LogInfo($"We're expecting a baby: {NextBabyData}");

				Entity baby = Entity.Null;
				float closestDistance = float.MaxValue;

				foreach (var horse in horses)
				{
					if (horse.Index == NextBabyData.parentId1 || horse.Index == NextBabyData.parentId2) continue;

					VWorld.Server.EntityManager.TryGetComponentData<Translation>(horse, out var position);
					var distanceFromBaby = Vector3.Distance(position.Value, NextBabyData.position);

					if (distanceFromBaby < closestDistance)
					{
						_log.LogDebug($"Closest horse <{horse.Index}> - {distanceFromBaby}");

						closestDistance = distanceFromBaby;
						baby = horse;
					}
				}

				if (closestDistance > 8) // IDK TODO tune this epislon
				{
					BreedTimerProcess.Instance.StopCooldown();
					_log.LogDebug("Closest horse is too far so I give up, resetting baby data");
					NextBabyData = null;
					return;
				}

				VWorld.Server.EntityManager.SetComponentData<Team>(baby, new()
				{
					Value = NextBabyData.team.Value
				});

				baby.WithComponentData((ref NameableInteractable ni) => ni.Name = NextBabyData.name);
				baby.WithComponentDataH((ref Mountable mount) =>
				{
					mount.MaxSpeed = NextBabyData.speed;
					mount.Acceleration = NextBabyData.acceleration;
					mount.RotationSpeed = NextBabyData.rotation;
					_log.LogDebug($"Updated baby {baby.Index} \n Speed {mount.MaxSpeed}\n Acceleration {mount.Acceleration}\n Rotation {mount.RotationSpeed}");
				});

				NextBabyData = null;
			}
			catch (Exception e)
			{
				_log?.LogError(e.ToString());
				NextBabyData = null;
			}
		}
	}
}
