using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(TombStone), "Awake")]
public static class TombstoneAwakeResurrectPatch
{
	[HarmonyPostfix]
	private static void Postfix(TombStone __instance)
	{
		ZDO zDO = __instance.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		if (zDO.GetBool(MHR_RespawnTracker.CleanupHash))
		{
			(__instance.GetComponent<CompanionTombstoneResurrect>() ?? __instance.gameObject.AddComponent<CompanionTombstoneResurrect>())._cleanupOnly = true;
		}
		else if (!string.IsNullOrEmpty(zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash)) && !(__instance.GetComponent<CompanionTombstoneResurrect>() != null))
		{
			__instance.gameObject.AddComponent<CompanionTombstoneResurrect>();
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[TombstoneAwake] Re-attached CompanionTombstoneResurrect on zone reload");
			}
		}
	}
}
