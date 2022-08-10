using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using Il2CppDumper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeadAHorseToWater
{
    // because Config was taken
    public static class Settings
    {
		private static ManualLogSource log => Plugin.LogInstance;
		
		public static ConfigEntry<float> DISTANCE_REQUIRED;
		public static ConfigEntry<int> SECONDS_DRINK_PER_TICK;
		public static ConfigEntry<int> MAX_DRINK_AMOUNT;
		public static ConfigEntry<string> DRINKING_PREFIX;
		public static ConfigEntry<bool> ENABLE_RENAME;
		public static ConfigEntry<bool> ENABLE_PREFIX_COLOR;
		public static ConfigEntry<string> ENABLED_WELL_PREFAB;

		public static HashSet<int> EnabledWellPrefabs = new();

		internal static void Initialize(ConfigFile config)
		{
			DISTANCE_REQUIRED = config.Bind<float>("Server", "DistanceRequired", 5.0f, "Horses must be within this distance from well. (5 =1 tile)");
			SECONDS_DRINK_PER_TICK = config.Bind<int>("Server", "SecondsDrinkPerTick", 30, "How many seconds added per drink tick (~1.5seconds), default values would be about 24 minutes for the default max amount at fountain.");
			MAX_DRINK_AMOUNT = config.Bind<int>("Server", "MaxDrinkAmount", 28800, "Time in seconds, default value is roughly amount of time when you take wild horses.");

			ENABLE_RENAME = config.Bind<bool>("Server", "EnableRename", true, "If true will rename horses in drinking range with the DrinkingPrefix");
			ENABLE_PREFIX_COLOR = config.Bind<bool>("Server", "EnablePrefixColor", true, "If true use a different color for the DrinkingPrefix");
			DRINKING_PREFIX = config.Bind<string>("Server", "DrinkingPrefix", "[Drinking] ", "Prefix to use on horses that are drinking");
			ENABLED_WELL_PREFAB = config.Bind<string>("Server", "EnabledWellPrefabs", "Stone, Large", "This is a comma seperated list of prefabs to use for the well. You can choose from one of (stone, iron, bronze, small, big) or (advanced: at your own risk) you can also include an arbitrary guid hash of of a castle connected placeable.");

			ENABLED_WELL_PREFAB.SettingChanged += (_, _) => ParseEnabledWells();
			ParseEnabledWells();
		}

		private static Dictionary<string, int> _fountains = new(){
				{"stone", 986517450},
				{"iron", 1247163010},
				{"broznze", -1790149989},
				{"small", 549920910},
				{"large", 177891172},
		};

		private static void ParseEnabledWells()
		{
			EnabledWellPrefabs.Clear();
			
			var list = ENABLED_WELL_PREFAB.Value;
			var values = list.Split(",", StringSplitOptions.RemoveEmptyEntries);
			
			log.LogDebug($"Parsing {list} is value {values} are values");
			foreach (var value in values)
			{
				var key = value.Trim().ToLowerInvariant();

				if (int.TryParse(key, out var guid))
				{
					EnabledWellPrefabs.Add(guid);
					log.LogInfo($"{guid} is acting well type");

				}
				else if (_fountains.TryGetValue(key, out var wellGuid))
				{
					EnabledWellPrefabs.Add(wellGuid);
					log.LogInfo($"{wellGuid} is {key} well type");
				}
				else
				{
					log.LogWarning($"Unknown well prefab value: {key}");
				}
			}
		}
	}
}
