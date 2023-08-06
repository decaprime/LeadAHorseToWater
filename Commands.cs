using System.Collections.Generic;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppSystem.Collections;
using UnityEngine;

namespace LeadAHorseToWater.VCFCompat
{
	using System;
	using System.Text;
	using Bloodstone.API;
	using Processes;
	using ProjectM;
	using ProjectM.Network;
	using Unity.Entities;
	using Unity.Mathematics;
	using Unity.Transforms;
	using VampireCommandFramework;
	using static UnityEngine.SpookyHash;

	public static partial class Commands
	{
		private static ManualLogSource _log => Plugin.LogInstance;

		static Commands()
		{
			Enabled = IL2CPPChainloader.Instance.Plugins.TryGetValue("gg.deca.VampireCommandFramework", out var info);
			if (Enabled) _log.LogWarning($"VCF Version: {info.Metadata.Version}");
		}

		public static bool Enabled { get; private set; }

		public static void Register() => CommandRegistry.RegisterAll();

		private static System.Random _random = new();

		public record Horse(Entity Entity);

		public class NamedHorseConverter : CommandArgumentConverter<Horse, ChatCommandContext>
		{
			const float radius = 25f;

			public override Horse Parse(ChatCommandContext ctx, string input)
			{
				var horses = HorseUtil.ClosestHorses(ctx.Event.SenderCharacterEntity, radius);

				foreach (var horse in horses)
				{
					var name = VWorld.Server.EntityManager.GetComponentData<NameableInteractable>(horse);
					if (name.Name.ToString().Contains(input, StringComparison.OrdinalIgnoreCase))
					{
						return new Horse(horse);
					}
				}

				throw ctx.Error($"Could not find a horse within {radius:F1} units named like \"{input}\"");
			}

			[CommandGroup("horse", "h")]
			public class HorseCommands
			{
				private static PrefabGUID BreedItemType = new PrefabGUID(-570287766);

				private Horse GetRequiredClosestHorse(ChatCommandContext ctx)
				{
					var closest = HorseUtil.GetClosetHorse(ctx.Event.SenderCharacterEntity);
					if (closest == null)
					{
						throw ctx.Error("No closest horse and expected to find one based on usage.");
					}

					return new Horse(closest.Value);
				}

				[Command("breedable", shortHand: "brab")]
				public void Breedable(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					EntityManager em = VWorld.Server.EntityManager;
					ComponentDataFromEntity<Team> getTeam =
						VWorld.Server.EntityManager.GetComponentDataFromEntity<Team>();


					if (em.HasComponent<Team>(horse.Entity))
					{
						var teamhorse = getTeam[horse.Entity];
						var isUnit = Team.IsInUnitTeam(teamhorse);
						if (isUnit)
						{
							ctx.Reply($"This horse cannot be breeded because it is not tamed");
							return;
						}
						String name = null;
						horse.Entity.WithComponentDataH((ref NameableInteractable nameable) =>
						{
							name = nameable.Name.ToString();
						});
						if (name.Equals("") || name == null)
						{
							ctx.Reply($"Horse without a name is breedable");
							return;
						}
						ctx.Reply($"Horse with name:<{name}> is breedable");
						return;
					}
					throw ctx.Error($"Horse doesn't have team component!");
				}


				[Command("breed", shortHand: "br")]
				public void Breed(ChatCommandContext ctx)
				{
					var character = ctx.Event.SenderCharacterEntity;

					if (BreedTimerProcess.Instance.IsBreedCooldownActive && Settings.ENABLE_HORSE_BREED_COOLDOWN.Value)
					{
						throw ctx.Error(
							$"You've already bred recently, try again in {Mathf.FloorToInt(BreedTimerProcess.Instance.RemainingTime)} second(s).");
					}

					if (BreedHorseProcess.NextBabyData != null)
					{
						throw ctx.Error($"There's already a pair breeding, try again in a few seconds.");
					}

					if (Settings.ENABLE_HORSE_BREED_COOLDOWN.Value)
					{
						BreedTimerProcess.Instance.StartCooldown();
					}


					if (!InventoryUtilities.TryGetInventoryEntity(VWorld.Server.EntityManager, character,
							out Entity invEntity))
					{
						BreedTimerProcess.Instance.StopCooldown();
						return;
					}

					var horses = HorseUtil.ClosestHorses(character);
					if (horses.Count < 2)
					{
						BreedTimerProcess.Instance.StopCooldown();
						throw ctx.Error(
							$"Too little horses in the area: found {horses.Count}. Please move some closer by.");
					}
					if (horses.Count != 2)
					{
						BreedTimerProcess.Instance.StopCooldown();
						throw ctx.Error(
							$"Too many horses in the area: found {horses.Count}. Please move some further away.");
					}
					if (!HorseUtil.isTamed(horses[0]) || !HorseUtil.isTamed(horses[1]))
					{
						BreedTimerProcess.Instance.StopCooldown();
						throw ctx.Error("You can only breed tamed horses.");

					}


					var breedItem = new PrefabGUID(Settings.HORSE_BREED_PREFAB.Value);
					var breedAmount = Settings.HORSE_BREED_COST.Value;

					var didRemove = InventoryUtilitiesServer.TryRemoveItem(VWorld.Server.EntityManager, invEntity,
						breedItem, breedAmount);
					if (!didRemove)
					{
						BreedTimerProcess.Instance.StopCooldown();
						throw ctx.Error($"You must have at least one {Settings.HORSE_BREED_ITEM_NAME.Value} in your inventory.");
					}

					var pos1 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[0]).Value;
					var pos2 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[1]).Value;

					var babyPos = UnityEngine.Vector3.Lerp(pos1, pos2, 0.5f);

					VWorld.Server.EntityManager.TryGetComponentData<Mountable>(horses[0], out var mountData1);
					//var mountData1 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[0]);
					VWorld.Server.EntityManager.TryGetComponentData<Mountable>(horses[1], out var mountData2);
					//var mountData2 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[1]);
					_log?.LogInfo(
						$"Parent {horses[1].Index}\n Speed: {mountData2.MaxSpeed}\n Acceleration: {mountData2.Acceleration}\n Rotation: {mountData2.RotationSpeed}");

					StringBuilder sb = new();
					sb.AppendLine("Offspring Details");
					var applyMutation = (string label, float parent1, float parent2, float maxValue) =>
					{
						float parentValue;
						string par1 = parent1.ToString("F1"), par2 = parent2.ToString("F2");
						string parentValueFormatted;
						if (_random.NextDouble() > 0.5)
						{
							parentValue = parent1;
							parentValueFormatted = $"( {par1.Underline()} | {par2.Color("#888")} )";
						}
						else
						{
							parentValue = parent2;
							parentValueFormatted = $"( {par1.Color("#888")} | {par2.Underline()} )";
						}

						var adjustmentScale = maxValue * Settings.HORSE_BREED_MUTATION_RANGE.Value;
						var mutation = (float)(_random.NextDouble() * 2.0 - 1.0);
						var adjustment = mutation * adjustmentScale;
						char? sign = mutation > 0 ? '+' : null;

						var mutationValues =
							$"{sign}{adjustment:F1}[{mutation:P}]".Color(adjustment > 0 ? "#1d1" : "#d11");
						var output = MathF.Min(parentValue + adjustment, maxValue);
						var atMax = output == maxValue ? " [MAX]".Color("#e0e") : "";
						var outPutValueStr = $"{output:F1}{atMax}".Bold();

						sb.AppendLine(
							$"{label.Bold().Color(Color.White)} {parentValueFormatted} {mutationValues} = {outPutValueStr}");
						return output;
					};

					var babySpeed = applyMutation("Speed", mountData1.MaxSpeed, mountData2.MaxSpeed,
						Settings.HORSE_BREED_MAX_SPEED.Value);
					var babyAcceleration = applyMutation("Acceleration", mountData1.Acceleration,
						mountData2.Acceleration, Settings.HORSE_BREED_MAX_ACCELERATION.Value);
					var babyRotation = applyMutation("Rotation", mountData1.RotationSpeed / 10f,
						mountData2.RotationSpeed / 10f, Settings.HORSE_BREED_MAX_ROTATION.Value);

					// todo: check we can breed and that these are ours

					var team = VWorld.Server.EntityManager.GetComponentData<Team>(horses[0]);

					HorseUtil.SpawnHorse(1, babyPos);
					ctx.Reply(sb.ToString());
					var name = $"Baby Horse {StatTag(babySpeed, babyAcceleration, babyRotation)}";
					BreedHorseProcess.NextBabyData = new BabyHorseData(name, team, babyPos, babySpeed, babyAcceleration,
						babyRotation * 10f, horses[0].Index, horses[1].Index, DateTime.Now.AddSeconds(1.5));
				}

				private string StatTag(float speed, float acceleration, float rotation) =>
					$"<color={TagColor}>Ⓢ {speed:F1} Ⓐ {acceleration:F1} Ⓡ {(rotation):F1}</color>";

				private const string TagColor = "#BF5";

				[Command("tag-stats")]
				public void TagStats(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					var nameComponent =
						VWorld.Server.EntityManager.GetComponentData<NameableInteractable>(horse.Entity);
					var name = nameComponent.Name.ToString();

					var statStart = name.LastIndexOf("Ⓢ", StringComparison.Ordinal);
					if (statStart > 0)
					{
						var colorStart = name.LastIndexOf("<color=", StringComparison.Ordinal);
						if (colorStart > 0 && colorStart < statStart)
						{
							name = name[..(colorStart - 1)];
						}
					}

					if (name.Length >= 20)
					{
						ctx.Reply(
							$"Name is too long, must be less than 20 characters, currently [without tag] {name.Length} characters");
						return;
					}

					VWorld.Server.EntityManager.TryGetComponentData<Mountable>(horse.Entity, out var mountData);
					var tag = StatTag(mountData.MaxSpeed, mountData.Acceleration, mountData.RotationSpeed / 10f);

					horse.Entity.WithComponentData((ref NameableInteractable nameInteract) =>
					{
						nameInteract.Name = $"{name} {tag}";
					});
				}

				[Command("speed", adminOnly: true)]
				public void SetSpeed(ChatCommandContext ctx, float speed) =>
					SetSpeed(ctx, GetRequiredClosestHorse(ctx), speed);

				[Command("speed", adminOnly: true)]
				public void SetSpeed(ChatCommandContext ctx, Horse horse, float speed)
				{
					horse.Entity.WithComponentDataH((ref Mountable mount) => mount.MaxSpeed = speed);
					ctx.Reply($"Horse speed set to {speed}");
				}

				[Command("acceleration", shortHand: "accel", adminOnly: true)]
				public void SetAcceleration(ChatCommandContext ctx, float acceleration) =>
					SetAcceleration(ctx, GetRequiredClosestHorse(ctx), acceleration);

				[Command("acceleration", shortHand: "accel", adminOnly: true)]
				public void SetAcceleration(ChatCommandContext ctx, Horse horse, float acceleration)
				{
					horse.Entity.WithComponentDataH((ref Mountable mount) => mount.Acceleration = acceleration);
					ctx.Reply($"Horse acceleration set to {acceleration}");
				}

				[Command("rotation", adminOnly: true)]
				public void SetRotation(ChatCommandContext ctx, float rotation) =>
					SetRotation(ctx, GetRequiredClosestHorse(ctx), rotation);

				[Command("rotation", adminOnly: true)]
				public void SetRotation(ChatCommandContext ctx, Horse horse, float rotation)
				{
					horse.Entity.WithComponentDataH((ref Mountable mount) => mount.RotationSpeed = rotation * 10f);
					ctx.Reply($"Horse rotation set to {rotation}");
				}

				[Command("warp", adminOnly: true)]
				public void WarpHorse(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					var position = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(horse.Entity).Position;

					var entity = VWorld.Server.EntityManager.CreateEntity(
						ComponentType.ReadWrite<FromCharacter>(),
						ComponentType.ReadWrite<PlayerTeleportDebugEvent>()
					);

					VWorld.Server.EntityManager.SetComponentData<FromCharacter>(entity, new()
					{
						User = ctx.Event.SenderUserEntity,
						Character = ctx.Event.SenderCharacterEntity
					});

					VWorld.Server.EntityManager.SetComponentData<PlayerTeleportDebugEvent>(entity, new()
					{
						Position = position.xyz,
						Target = PlayerTeleportDebugEvent.TeleportTarget.Self,
					});

					ctx.Reply("Warped to horse.");
				}

				[Command("spawn", adminOnly: true)]
				public void HorseMe(ChatCommandContext ctx, int num = 1)
				{
					float3 localPos = VWorld.Server.EntityManager
						.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
					HorseUtil.SpawnHorse(num, localPos);
					ctx.Reply($"Spawned {num} horse{(num > 1 ? "s" : "")} near you.");
				}

				[Command("whistle", shortHand: "w", adminOnly: true)]
				public void Whistle(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					float3 userPos = VWorld.Server.EntityManager
						.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
					float3 horsePos = VWorld.Server.EntityManager.GetComponentData<Translation>(horse.Entity).Value;

					horse.Entity.WithComponentData((ref Translation t) => { t.Value = userPos; });
					ctx.Reply("Your horse came to you.");
				}

				[Command("rename", adminOnly: true)]
				public void Rename(ChatCommandContext ctx, string newName) =>
					Rename(ctx, GetRequiredClosestHorse(ctx), newName);

				[Command("rename", adminOnly: true)]
				public void Rename(ChatCommandContext ctx, Horse horse, string newName)
				{
					string oldName = "";
					horse.Entity.WithComponentData((ref NameableInteractable t) =>
					{
						oldName = t.Name.ToString();
						t.Name = newName;
					});
					ctx.Reply($"Horse '{oldName}' renamed to '{newName}'.");
				}

				[Command("kill", adminOnly: true)]
				public void Kill(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					horse.Entity.WithComponentData((ref Health t) =>
					{
						t.Value = 0;
						t.TimeOfDeath = 0;
						t.IsDead = true;
					});
					VWorld.Server.EntityManager.AddComponent(horse.Entity, ComponentType.ReadOnly<DestroyTag>());
					ctx.Reply($"Horse removed.");
				}

				[Command("cull", adminOnly: true)]
				public void Kill(ChatCommandContext ctx, float radius = 5f, float percentage = 1f)
				{
					var horses = HorseUtil.ClosestHorses(ctx.Event.SenderCharacterEntity, radius);
					var count = horses.Count;
					var toRemove = Math.Clamp((int)(count * percentage), 0, count);
					var remaining = toRemove;
					foreach (var horse in horses)
					{
						if (remaining == 0) break;
						horse.WithComponentData((ref Health t) =>
						{
							t.Value = 0;
							t.TimeOfDeath = 0;
							t.IsDead = true;
						});
						VWorld.Server.EntityManager.AddComponent(horse, ComponentType.ReadOnly<DestroyTag>());
						remaining--;
					}

					ctx.Reply($"Removed {toRemove} horses.");
				}
			}
		}
	}
}
