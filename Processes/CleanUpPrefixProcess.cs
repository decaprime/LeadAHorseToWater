using BepInEx.Logging;
using ProjectM;
using Unity.Collections;
using Unity.Entities;
using Bloodstone.API;

namespace LeadAHorseToWater.Processes;

public class CleanUpPrefixProcess
{
	private static bool Processed = false;
	private static ManualLogSource _log => Plugin.LogInstance;
	
	public static void Update(NativeArray<Entity> horses)
	{
		if (Processed) return;

		foreach (var horse in horses)
		{
			horse.WithComponentData((ref NameableInteractable nameable) =>
			{
				var name = nameable.Name.ToString();

				var prefix = Settings.ENABLE_PREFIX_COLOR.Value ?
							$"<color=#0ef>{Settings.DRINKING_PREFIX.Value}</color> " :
							$"{Settings.DRINKING_PREFIX.Value} ";
				bool hasOldPrefix = name.StartsWith(prefix);
				if (hasOldPrefix)
				{
					nameable.Name = name.Substring(prefix.Length);
					_log.LogInfo($"Cleaned up prefix for {nameable.Name}");
				}
			});

			Processed = true;
		}
	}
}
