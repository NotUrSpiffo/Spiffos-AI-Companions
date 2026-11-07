using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(ZNetScene), "Awake")]
public static class ZNetScenePatch
{
	[HarmonyPostfix]
	private static void Postfix(ZNetScene __instance)
	{
		CompanionPrefabs.Init(__instance);
		MHR_Shrine.Init(__instance);
		CompanionRestedBuff.ResetCache();
	}
}
