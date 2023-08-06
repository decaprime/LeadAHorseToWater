using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Bloodstone.API;
using HarmonyLib;

namespace LeadAHorseToWater;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("gg.deca.VampireCommandFramework", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("gg.deca.Bloodstone")]
[Reloadable]
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
		BreedTimerProcess process = new();
		process.Setup();
	}

	public void OnGameInitialized()
	{
		if (VWorld.IsClient)
		{
			return;
		}
		// Plugin startup logic
		_harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
		Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

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
		if (VCFCompat.Commands.Enabled)
		{
			VCFCompat.Commands.Unregister();
		}

		_harmony?.UnpatchSelf();
		return true;
	}
}
