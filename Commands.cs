using BepInEx.Logging;

namespace LeadAHorseToWater.VCFCompat;

using ProjectM;
using Unity.Entities;
using Unity.Transforms;
using VampireCommandFramework;
using Unity.Mathematics;
using System;
using Bloodstone.API;
using LeadAHorseToWater.Processes;
using ProjectM.Network;
using System.Text;
using Il2CppInterop.Runtime;
using static Bloodstone.API.VExtensions;

public static partial class Commands
{
	private static ManualLogSource _log => Plugin.LogInstance;

	static Commands()
	{
		// Enabled = IL2CPPChainloader.Instance.Plugins.TryGetValue("gg.deca.VampireCommandFramework", out var info);
		Enabled = true; // hard required for Bloodstone
						// if (Enabled) _log.LogWarning($"VCF Version: {info.Metadata.Version}");
	}

	public static bool Enabled { get; private set; }

	public static void Register() => CommandRegistry.RegisterAll();
	public static void Unregister() => CommandRegistry.UnregisterAssembly();

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

				var pos1 = horses[0].Read<Translation>().Value;
				var pos2 = horses[1].Read<Translation>().Value;

				var babyPos = UnityEngine.Vector3.Lerp(pos1, pos2, 0.5f);

				var mountData1 = horses[0].Read<Mountable>();
				_log.LogDebug($"Parent {horses[0].Index}\n Speed: {mountData1.MaxSpeed}\n Acceleration: {mountData1.Acceleration}\n Rotation: {mountData1.RotationSpeed}");
				var mountData2 = horses[1].Read<Mountable>();
				_log.LogDebug($"Parent {horses[1].Index}\n Speed: {mountData2.MaxSpeed}\n Acceleration: {mountData2.Acceleration}\n Rotation: {mountData2.RotationSpeed}");

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

					var mutationValues = $"{sign}{adjustment:F1}[{mutation:P}]".Color(adjustment > 0 ? "#1d1" : "#d11");
					var output = MathF.Min(parentValue + adjustment, maxValue);
					var atMax = output == maxValue ? " [MAX]".Color("#e0e") : "";
					var outPutValueStr = $"{output:F1}{atMax}".Bold();

					sb.AppendLine($"{label.Bold().Color(Color.White)} {parentValueFormatted} {mutationValues} = {outPutValueStr}");
					return output;
				};

				var babySpeed = applyMutation("Speed", mountData1.MaxSpeed, mountData2.MaxSpeed, Settings.HORSE_BREED_MAX_SPEED.Value);
				var babyAcceleration = applyMutation("Acceleration", mountData1.Acceleration, mountData2.Acceleration, Settings.HORSE_BREED_MAX_ACCELERATION.Value);
				var babyRotation = applyMutation("Rotation", mountData1.RotationSpeed / 10f, mountData2.RotationSpeed / 10f, Settings.HORSE_BREED_MAX_ROTATION.Value);

				// todo: check we can breed in that these are ours
				var team = horses[0].Read<Team>();

				HorseUtil.SpawnHorse(1, babyPos);
				ctx.Reply(sb.ToString());
				var name = $"Baby Horse {StatTag(babySpeed, babyAcceleration, babyRotation)}";
				BreedHorseProcess.NextBabyData = new BabyHorseData(name, team, babyPos, babySpeed, babyAcceleration, babyRotation * 10f, horses[0].Index, horses[1].Index, DateTime.Now.AddSeconds(1.5));
			}

			private string StatTag(float speed, float acceleration, float rotation) => $"<color={TagColor}>Ⓢ {speed:F1} Ⓐ {acceleration:F1} Ⓡ {(rotation):F1}";
			private const string TagColor = "#BF5";



			[Command("tag-stats")]
			public void TagStats(ChatCommandContext ctx, Horse horse = null)
			{
				horse ??= GetRequiredClosestHorse(ctx);
				var nameComponent = VWorld.Server.EntityManager.GetComponentData<NameableInteractable>(horse.Entity);
				var name = nameComponent.Name.ToString();

				var statStart = name.LastIndexOf("Ⓢ");
				if (statStart > 0)
				{
					var colorStart = name.LastIndexOf("<color=");
					if (colorStart > 0 && colorStart < statStart)
					{
						name = name[..(colorStart - 1)];
					}
				}

				if (name.Length >= 20)
				{
					ctx.Reply($"Name is too long, must be less than 20 characters, currently [without tag] {name.Length} characters");
					return;
				}

				
				var mountData = horse.Entity.Read<Mountable>();
				var tag = StatTag(mountData.MaxSpeed, mountData.Acceleration, mountData.RotationSpeed / 10f);

				horse.Entity.With((ref NameableInteractable nameInteract) =>
				{
					nameInteract.Name = $"{name} {tag}";
				});
			}

			[Command("speed", adminOnly: true)]
			public void SetSpeed(ChatCommandContext ctx, float speed) => SetSpeed(ctx, GetRequiredClosestHorse(ctx), speed);

			[Command("speed", adminOnly: true)]
			public void SetSpeed(ChatCommandContext ctx, Horse horse, float speed)
			{
				 horse.Entity.With((ref Mountable mount) => mount.MaxSpeed = speed);
				ctx.Reply($"Horse speed set to {speed}");
			}

			[Command("acceleration", shortHand: "accel", adminOnly: true)]
			public void SetAcceleration(ChatCommandContext ctx, float acceleration) => SetAcceleration(ctx, GetRequiredClosestHorse(ctx), acceleration);

			[Command("acceleration", shortHand: "accel", adminOnly: true)]
			public void SetAcceleration(ChatCommandContext ctx, Horse horse, float acceleration)
			{
				horse.Entity.With((ref Mountable mount) => mount.Acceleration = acceleration);
				ctx.Reply($"Horse acceleration set to {acceleration}");
			}

			[Command("rotation", adminOnly: true)]
			public void SetRotation(ChatCommandContext ctx, float rotation) => SetRotation(ctx, GetRequiredClosestHorse(ctx), rotation);

			[Command("rotation", adminOnly: true)]
			public void SetRotation(ChatCommandContext ctx, Horse horse, float rotation)
			{
				horse.Entity.With((ref Mountable mount) => mount.RotationSpeed = rotation * 10f);
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
					Position = position,
					Target = PlayerTeleportDebugEvent.TeleportTarget.Self,
				});

				ctx.Reply("Warped to horse.");
			}

			[Command("spawn", adminOnly: true)]
			public void HorseMe(ChatCommandContext ctx, int num = 1)
			{
				float3 userPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
				float3 charPos = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(ctx.Event.SenderCharacterEntity).Position;
				HorseUtil.SpawnHorse(num, new float3(charPos.x, userPos.y, charPos.z));
				ctx.Reply($"Spawned {num} horse{(num > 1 ? "s" : "")} near you.");
			}

			[Command("whistle", adminOnly: true)]
			public void Whistle(ChatCommandContext ctx, Horse horse = null)
			{
				horse ??= GetRequiredClosestHorse(ctx);
				float3 userPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderCharacterEntity).Value;
				float3 horsePos = VWorld.Server.EntityManager.GetComponentData<Translation>(horse.Entity).Value;

				horse.Entity.With((ref Translation t) => { t.Value = userPos; });
				ctx.Reply("Horse moved to you.");
			}

			[Command("rename", adminOnly: true)]
			public void Rename(ChatCommandContext ctx, string newName) => Rename(ctx, GetRequiredClosestHorse(ctx), newName);

			[Command("rename", adminOnly: true)]
			public void Rename(ChatCommandContext ctx, Horse horse, string newName)
			{
				string oldName = "";
				horse.Entity.With((ref NameableInteractable t) =>
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
				horse.Entity.With((ref Health t) =>
				{
					t.IsDead = true;
				});
				VWorld.Server.EntityManager.AddComponent(horse.Entity, Il2CppType.Of<Dead>());
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
					horse.With((ref Health t) =>
					{
						t.IsDead = true;
					});
					VWorld.Server.EntityManager.AddComponent(horse, Il2CppType.Of<Dead>());
					remaining--;
				}

				ctx.Reply($"Removed {toRemove} horses.");
			}
		}
	}
}