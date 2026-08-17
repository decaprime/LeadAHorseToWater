using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace LeadAHorseToWater;

// because Config was taken
public static class Settings
{
	private static ManualLogSource log => Plugin.LogInstance;

	public static ConfigEntry<float> DISTANCE_REQUIRED;
	public static ConfigEntry<int> SECONDS_DRINK_PER_TICK;
	public static ConfigEntry<int> MAX_DRINK_AMOUNT;

	public static ConfigEntry<bool> ENABLE_RENAME;
	public static ConfigEntry<string> ENABLED_WELL_PREFAB;

	public static ConfigEntry<int> HORSE_BREED_PREFAB { get; private set; }
	public static ConfigEntry<string> HORSE_BREED_ITEM_NAME { get; private set; }
	public static ConfigEntry<int> HORSE_BREED_COST { get; private set; }
	public static ConfigEntry<float> HORSE_BREED_MUTATION_RANGE { get; private set; }
	public static ConfigEntry<float> HORSE_BREED_MAX_SPEED { get; private set; }
	public static ConfigEntry<float> HORSE_BREED_MAX_ROTATION { get; private set; }
	public static ConfigEntry<float> HORSE_BREED_MAX_ACCELERATION { get; private set; }

	public static HashSet<int> EnabledWellPrefabs = new();

	internal static void Initialize(ConfigFile config)
	{

		DISTANCE_REQUIRED = config.Bind<float>("Server", "DistanceRequired", 5.0f, "Horses must be within this distance from well. (5 =1 tile)");
		SECONDS_DRINK_PER_TICK = config.Bind<int>("Server", "SecondsDrinkPerTick", 30, "How many seconds added per drink tick (~1.5seconds), default values would be about 24 minutes for the default max amount at fountain.");
		MAX_DRINK_AMOUNT = config.Bind<int>("Server", "MaxDrinkAmount", 28800, "Time in seconds, default value is roughly amount of time when you take wild horses.");

		ENABLE_RENAME = config.Bind<bool>("Server", "EnableRename", true, "If true will rename horses in drinking range with a symbol");
		ENABLED_WELL_PREFAB = config.Bind<string>("Server", "EnabledWellPrefabs", "Stone, Large", "This is a comma seperated list of prefabs to use for the well. You can choose from one of (stone, iron, bronze, small, big) or (advanced: at your own risk) you can also include an arbitrary guid hash of of a castle connected placeable.");

		// Breeding
		HORSE_BREED_PREFAB = config.Bind<int>("Breeding", "BreedingRequiredItem", -570287766, "This prefab is consumed as a cost to breed horses.");
		HORSE_BREED_ITEM_NAME = config.Bind<string>("Breeding", "BreedingCostItemName", "special fish (twilight snapper)", "This is the name of the required item that will be consumed.");
		HORSE_BREED_COST = config.Bind<int>("Breeding", "BreedingCostAmount", 1, "This is the amount of the required item consumed.");

		HORSE_BREED_MUTATION_RANGE = config.Bind<float>("Breeding", "MutationRange", 0.05f, "This is the half range +/- this value for applied for mutation.");

		HORSE_BREED_MAX_SPEED = config.Bind<float>("Breeding", "MaxSpeed", 14f, "The absolute maximum speed for horses including selective breeding and mutations.");
		HORSE_BREED_MAX_ROTATION = config.Bind<float>("Breeding", "MaxRotation", 16f, "The absolute maximum rotation for horses including selective breeding and mutations.");
		HORSE_BREED_MAX_ACCELERATION = config.Bind<float>("Breeding", "MaxAcceleration", 9f, "The absolute maximum acceleration for horses including selective breeding and mutations.");

		ENABLED_WELL_PREFAB.SettingChanged += (_, _) => ParseEnabledWells();
		ParseEnabledWells();
	}

	private static Dictionary<string, int> _fountains = new(){
			{"stone", 986517450},
			{"iron", 1247163010},
			{"bronze", -1790149989},
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
				log.LogDebug($"{guid} is acting well type");

			}
			else if (_fountains.TryGetValue(key, out var wellGuid))
			{
				EnabledWellPrefabs.Add(wellGuid);
				log.LogDebug($"{wellGuid} is {key} well type");
			}
			else
			{
				log.LogWarning($"Unknown well prefab value: {key}");
			}
		}
	}
}
