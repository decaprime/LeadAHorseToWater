using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LeadAHorseToWater;

// Bloodstone dependency removed. It is abandoned and its network hooks break every
// server network event on V Rising 1.1.x. [BepInProcess] keeps this server-only, which
// is what Bloodstone's IsServer/IsClient checks were doing.
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("VRisingServer.exe")]
[BepInDependency("gg.deca.VampireCommandFramework", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
	private Harmony _harmony;

	public static ManualLogSource LogInstance { get; private set; }

	public override void Load()
	{
		LogInstance = this.Log;
		Settings.Initialize(Config);

		_harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
		Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded! (Bloodstone-free build)");

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
