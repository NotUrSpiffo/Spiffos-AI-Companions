using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Humanoid), "IsWeaponLoaded")]
public static class MHR_WeaponLoaded_Patch
{
	private static void Postfix(Humanoid __instance, ref bool __result)
	{
		if (!__result && MHR_Crossbow.IsCompanion(__instance))
		{
			__result = MHR_Crossbow.IsLoaded(__instance);
		}
	}
}
