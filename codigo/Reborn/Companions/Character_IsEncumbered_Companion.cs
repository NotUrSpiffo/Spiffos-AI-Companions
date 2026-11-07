using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "IsEncumbered")]
internal static class Character_IsEncumbered_Companion
{
	private static void Postfix(Character __instance, ref bool __result)
	{
		if (!__result && !(__instance == null) && !__instance.IsPlayer() && MHR_Encumbrance.IsCompanion(__instance))
		{
			__result = MHR_Encumbrance.Over(__instance);
		}
	}
}
