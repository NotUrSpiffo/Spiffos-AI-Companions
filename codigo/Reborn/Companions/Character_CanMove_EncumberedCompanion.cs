using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "CanMove")]
internal static class Character_CanMove_EncumberedCompanion
{
	private static void Postfix(Character __instance, ref bool __result)
	{
		if (__result && !(__instance == null) && !__instance.IsPlayer() && MHR_Encumbrance.IsCompanion(__instance) && MHR_Encumbrance.Over(__instance))
		{
			CompanionStamina component = __instance.GetComponent<CompanionStamina>();
			if (component != null && component.Stamina <= 0f)
			{
				__result = false;
			}
		}
	}
}
