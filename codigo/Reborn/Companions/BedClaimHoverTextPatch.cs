using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Bed), "GetHoverText")]
public static class BedClaimHoverTextPatch
{
	[HarmonyPostfix]
	private static void Postfix(Bed __instance, ref string __result)
	{
		long bedOwnerSafe = CompanionRest.GetBedOwnerSafe(__instance);
		if (bedOwnerSafe != 0L)
		{
			string text = CompanionRest.MHR_GetBedClaimName(__instance);
			if (text != null && Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() == bedOwnerSafe && !__instance.IsCurrent())
			{
				int num = __result.IndexOf('\n');
				__result = text + "'s Bed" + ((num >= 0) ? __result.Substring(num) : "");
			}
		}
		else
		{
			string value = CompanionRest.MHR_GetBedClaimName(__instance);
			if (value != null || CompanionRest._bedClaimNames.TryGetValue(__instance.GetInstanceID(), out value))
			{
				__result = value + "'s Bed";
			}
		}
	}
}
