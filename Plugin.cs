using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using Wetstone.API;

namespace LeadAHorseToWater
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInDependency("xyz.molenzwiebel.wetstone")]
	[Wetstone.API.Reloadable]
	public class Plugin : BasePlugin, IRunOnInitialized
	{
		private Harmony _harmony;

		public static ManualLogSource LogInstance { get; private set; }

		public override void Load()
		{
			LogInstance = this.Log;
			Settings.Initialize(Config);

			// Server plugin check
			if (!VWorld.IsServer)
			{
				Log.LogWarning("This plugin is a server-only plugin.");
				return;
			}

		}

		public void OnGameInitialized()
		{
			if (VWorld.IsClient)
			{
				return;
			}
			// Plugin startup logic
			_harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
			Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
		}

		public override bool Unload()
		{
			_harmony?.UnpatchSelf();
			return true;
		}
	}
}