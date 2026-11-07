using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "SetRun")]
public static class MHR_SprintBurst_Patch
{
	private static void Prefix(Character __instance, ref bool run)
	{
		MHR_Sprint.Filter(__instance, ref run);
	}
}
