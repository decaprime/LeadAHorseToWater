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

	public static partial class Commands
	{
		private static ManualLogSource _log => Plugin.LogInstance;

		static Commands()
		{
			Enabled = IL2CPPChainloader.Instance.Plugins.TryGetValue("gg.deca.VampireCommandFramework", out var info);
			if (Enabled) _log.LogWarning($"VCF Version: {info.Metadata.Version}");
		}

		public static bool Enabled { get; private set; }

		public static void Register() => CommandRegistry.RegisterCommandType(typeof(HorseCommands));

		private static System.Random _random = new();

		[ChatCommandGroup("horse")]
		public class HorseCommands
		{
			private static PrefabGUID BreedItemType = new PrefabGUID(-570287766);
			private Entity? _closestHorse;

			public HorseCommands(ChatCommandContext ctx)
			{
				_closestHorse = HorseUtil.GetClosetHorse(ctx.Event.SenderCharacterEntity);
				if (_closestHorse == null)
				{
					throw ctx.Error($"Could not find a horse.");
				}
			}

			[ChatCommand("breed", adminOnly: false)]
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

				var didRemove = InventoryUtilitiesServer.TryRemoveItem(VWorld.Server.EntityManager, invEntity, BreedItemType, 1);
				_log?.LogWarning($"Tried to remove 1, removed={didRemove}");
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

				var babySpeed = _random.NextDouble() > 0.5 ? mountData1.MaxSpeed : mountData2.MaxSpeed;
				var babyAcceleration = _random.NextDouble() > 0.5 ? mountData1.Acceleration : mountData2.Acceleration;
				var babyRotation = _random.NextDouble() > 0.5 ? mountData1.RotationSpeed : mountData2.RotationSpeed;

				// todo: check we can breed in that these are ours
				var team = VWorld.Server.EntityManager.GetComponentData<Team>(horses[0]);

				HorseUtil.SpawnHorse(1, babyPos);
				ctx.Reply($"Spawned Baby\n Speed {babySpeed}\n Acceleration: {babyAcceleration}\n Rotation: {babyRotation}");

				BreedHorseProcess.NextBabyData = new BabyHorseData("Baby Horse", team, babyPos, babySpeed, babyAcceleration, babyRotation, horses[0].Index, horses[1].Index, DateTime.Now.AddSeconds(1.5));
			}

			[ChatCommand("speed", adminOnly: true)]
			public void SetSpeed(ICommandContext ctx, float speed)
			{
				_closestHorse?.WithComponentData((ref Mountable mount) => mount.MaxSpeed = speed);
				ctx.Reply($"Horse speed set to {speed}");
			}

			[ChatCommand("acceleration", adminOnly: true)]
			public void SetAcceleration(ICommandContext ctx, float acceleration)
			{
				_closestHorse?.WithComponentData((ref Mountable mount) => mount.Acceleration = acceleration);
				ctx.Reply($"Horse acceleration set to {acceleration}");
			}

			[ChatCommand("rotation", adminOnly: true)]
			public void SetRotation(ICommandContext ctx, float rotation)
			{
				_closestHorse?.WithComponentData((ref Mountable mount) => mount.RotationSpeed = rotation);
				ctx.Reply($"Horse rotation set to {rotation}");
			}

			[ChatCommand("warphorse", adminOnly: true)]
			public void WarpHorse(ChatCommandContext ctx)
			{
				var position = VWorld.Server.EntityManager.GetComponentData<LocalToWorld>(_closestHorse.Value).Position;

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

				ctx.Reply("Warped to closest horse.");
			}

			[ChatCommand("spawn", adminOnly: true)]
			public void HorseMe(ChatCommandContext ctx, int num = 1)
			{
				float3 localPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
				HorseUtil.SpawnHorse(num, localPos);
				ctx.Reply($"Spawned {num} horse{(num > 1 ? "s" : "")} near you.");
			}

			[ChatCommand("whistle", adminOnly: true)]
			public void Whistle(ChatCommandContext ctx)
			{
				float3 userPos = VWorld.Server.EntityManager.GetComponentData<Translation>(ctx.Event.SenderUserEntity).Value;
				float3 horsePos = VWorld.Server.EntityManager.GetComponentData<Translation>(_closestHorse.Value).Value;

				_closestHorse?.WithComponentData((ref Translation t) => { t.Value = userPos; });
				ctx.Reply("Closest horse moved to you.");
			}
		}
	}
}