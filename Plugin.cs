using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using Wetstone.API;

namespace LeadAHorseToWater
{
	[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
	[BepInDependency("gg.deca.VampireCommandFramework", BepInDependency.DependencyFlags.SoftDependency)]
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

			Log.LogInfo("Trying to find VCF:");
			if (VCFCompat.Commands.Enabled)
			{
				VCFCompat.Commands.Register();
			}
			else
			{
				Log.LogError("This mod has commands, you need to install VampireCommandFramework to use them, find whereever you get mods or : https://a.deca.gg/vcf .");
			}
		}

		public override bool Unload()
		{
			_harmony?.UnpatchSelf();
			return true;
		}
	}
}