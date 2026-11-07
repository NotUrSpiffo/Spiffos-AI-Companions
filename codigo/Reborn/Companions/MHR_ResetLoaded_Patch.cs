using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Humanoid), "ResetLoadedWeapon")]
public static class MHR_ResetLoaded_Patch
{
	private static void Postfix(Humanoid __instance)
	{
		if (MHR_Crossbow.IsCompanion(__instance))
		{
			MHR_Crossbow.Unload(__instance);
		}
	}
}
