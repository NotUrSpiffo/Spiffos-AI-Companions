using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(TombStone), "GetHoverText")]
public static class TombstoneHoverTextPatch
{
	[HarmonyPostfix]
	private static void Postfix(TombStone __instance, ref string __result)
	{
		CompanionTombstoneResurrect component = __instance.GetComponent<CompanionTombstoneResurrect>();
		if (!(component == null))
		{
			string resurrectHoverText = component.GetResurrectHoverText();
			if (resurrectHoverText != null)
			{
				__result = Localization.instance.Localize(resurrectHoverText);
			}
		}
	}
}
