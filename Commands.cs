using BepInEx.IL2CPP;
using BepInEx.Logging;

namespace LeadAHorseToWater.VCFCompat
{
	using ProjectM;
	using Unity.Entities;
	using Unity.Transforms;
	using VampireCommandFramework;
	using Wetstone.API;
	using Unity.Mathematics;
	using System;
	using LeadAHorseToWater.Processes;
	using ProjectM.Network;
	using System.Text;

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

				[Command("breed", adminOnly: false)]
				public void Breed(ChatCommandContext ctx)
				{
					var character = ctx.Event.SenderCharacterEntity;

					if (BreedHorseProcess.NextBabyData != null)
					{
						throw ctx.Error("There's already a pair breeding, try again in a few seconds.");
					}

					if (!InventoryUtilities.TryGetInventoryEntity(VWorld.Server.EntityManager, character, out Entity invEntity))
					{
						return;
					}

					var horses = HorseUtil.ClosestHorses(character);
					if (horses.Count != 2)
					{
						throw ctx.Error($"Must have only two nearby horses, found {horses.Count}");
					}

					var breedItem = new PrefabGUID(Settings.HORSE_BREED_PREFAB.Value);
					var breedAmount = Settings.HORSE_BREED_COST.Value;

					var didRemove = InventoryUtilitiesServer.TryRemoveItem(VWorld.Server.EntityManager, invEntity, breedItem, breedAmount);
					_log?.LogWarning($"Tried to remove {breedAmount}, removed={didRemove}");
					if (!didRemove)
					{
						throw ctx.Error("You must have at least one special fish in your inventory.");
					};

					var pos1 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[0]).Value;
					var pos2 = VWorld.Server.EntityManager.GetComponentData<Translation>(horses[1]).Value;

					var babyPos = UnityEngine.Vector3.Lerp(pos1, pos2, 0.5f);

					var mountData1 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[0]);
					_log.LogInfo($"Parent {horses[0].Index}\n Speed: {mountData1.MaxSpeed}\n Acceleration: {mountData1.Acceleration}\n Rotation: {mountData1.RotationSpeed}");
					var mountData2 = VWorld.Server.EntityManager.GetComponentData<Mountable>(horses[1]);
					_log.LogInfo($"Parent {horses[1].Index}\n Speed: {mountData2.MaxSpeed}\n Acceleration: {mountData2.Acceleration}\n Rotation: {mountData2.RotationSpeed}");

					StringBuilder sb = new();
					var applyMutation = (string label, float parent1, float parent2, float maxValue) =>
					{

						var value = _random.NextDouble() > 0.5 ? parent1 : parent2;
						sb.AppendLine($"Choosing from <{parent1:F1}, {parent2:f1}> = {value}");
						var adjustmentScale = maxValue * Settings.HORSE_BREED_MUTATION_RANGE.Value;
						var mutation = (float)(_random.NextDouble() * 2.0 - 1.0);
						var adjustment = mutation * adjustmentScale;
						sb.AppendLine($"Mutation of {adjustment:F1} [{mutation:P}]");
						var output = MathF.Min(value + adjustment, maxValue);
						var atMax = output == maxValue;
						sb.AppendLine($"{label} = {output:F1}{(atMax ? " [MAX]" : "")}");
						return output;
					};

					var babySpeed = applyMutation("Speed", mountData1.MaxSpeed, mountData2.MaxSpeed, Settings.HORSE_BREED_MAX_SPEED.Value);
					var babyAcceleration = applyMutation("Acceleration", mountData1.Acceleration, mountData2.Acceleration, Settings.HORSE_BREED_MAX_ACCELERATION.Value);
					var babyRotation = applyMutation("Rotation", mountData1.RotationSpeed / 10f, mountData2.RotationSpeed / 10f, Settings.HORSE_BREED_MAX_ROTATION.Value);

					// todo: check we can breed in that these are ours
					var team = VWorld.Server.EntityManager.GetComponentData<Team>(horses[0]);

					HorseUtil.SpawnHorse(1, babyPos);
					ctx.Reply(sb.ToString());
					var name = $"Baby Horse <color=#ef0>Ⓢ {babySpeed:F1} Ⓐ {babyAcceleration:F1} Ⓡ {(babyRotation):F1}";
					BreedHorseProcess.NextBabyData = new BabyHorseData(name, team, babyPos, babySpeed, babyAcceleration, babyRotation * 10f, horses[0].Index, horses[1].Index, DateTime.Now.AddSeconds(1.5));
				}


				[Command("speed", adminOnly: true)]
				public void SetSpeed(ChatCommandContext ctx, float speed) => SetSpeed(ctx, GetRequiredClosestHorse(ctx), speed);

				[Command("speed", adminOnly: true)]
				public void SetSpeed(ChatCommandContext ctx, Horse horse, float speed)
				{
					horse.Entity.WithComponentData((ref Mountable mount) => mount.MaxSpeed = speed);
					ctx.Reply($"Horse speed set to {speed}");
				}

				[Command("acceleration", adminOnly: true)]
				public void SetAcceleration(ChatCommandContext ctx, float acceleration) => SetAcceleration(ctx, GetRequiredClosestHorse(ctx), acceleration);

				[Command("acceleration", adminOnly: true)]
				public void SetAcceleration(ChatCommandContext ctx, Horse horse, float acceleration)
				{
					horse.Entity.WithComponentData((ref Mountable mount) => mount.Acceleration = acceleration);
					ctx.Reply($"Horse acceleration set to {acceleration}");
				}

				[Command("rotation", adminOnly: true)]
				public void SetRotation(ChatCommandContext ctx, float rotation) => SetRotation(ctx, GetRequiredClosestHorse(ctx), rotation);

				[Command("rotation", adminOnly: true)]
				public void SetRotation(ChatCommandContext ctx, Horse horse, float rotation)
				{
					horse.Entity.WithComponentData((ref Mountable mount) => mount.RotationSpeed = rotation * 10f);
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
						Position = position.xz,
						Target = PlayerTeleportDebugEvent.TeleportTarget.Self,
					});

					ctx.Reply("Warped to horse.");
				}

				[Command("spawn", adminOnly: true)]
				public void HorseMe(ChatCommandContext ctx, int num = 1)
				{
					float3 localPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
					HorseUtil.SpawnHorse(num, localPos);
					ctx.Reply($"Spawned {num} horse{(num > 1 ? "s" : "")} near you.");
				}

				[Command("whistle", adminOnly: true)]
				public void Whistle(ChatCommandContext ctx, Horse horse = null)
				{
					horse ??= GetRequiredClosestHorse(ctx);
					float3 userPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
					float3 horsePos = VWorld.Server.EntityManager.GetComponentData<Translation>(horse.Entity).Value;

					horse.Entity.WithComponentData((ref Translation t) => { t.Value = userPos; });
					ctx.Reply("Closest horse moved to you.");
				}

				[Command("rename", adminOnly: true)]
				public void Rename(ChatCommandContext ctx, string newName) => Rename(ctx, GetRequiredClosestHorse(ctx), newName);

				[Command("rename", adminOnly: true)]
				public void Rename(ChatCommandContext ctx, Horse horse, string newName)
				{
					string oldName = "";
					horse.Entity.WithComponentData((ref NameableInteractable t) =>
					{
						oldName = t.Name.ToString();
						t.Name = newName;
					});
					ctx.Reply($"Closest horse {oldName} renamed {newName}.");
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
					ctx.Reply($"♥ I'm sure you were a good horse.");
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