using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(TombStone), "Interact")]
public static class TombstoneInteractPatch
{
	[HarmonyPrefix]
	private static bool Prefix(TombStone __instance, Humanoid character, bool hold, ref bool __result)
	{
		CompanionTombstoneResurrect component = __instance.GetComponent<CompanionTombstoneResurrect>();
		if (component == null)
		{
			return true;
		}
		Player player = character as Player;
		if (player == null)
		{
			return true;
		}
		if (component.TryStartResurrect(player, hold))
		{
			__result = true;
			return false;
		}
		if (!hold)
		{
			long num = (__instance.GetComponent<ZNetView>()?.GetZDO())?.GetLong(CompanionSetup.TombstoneIdHash, 0L) ?? 0;
			if (num != 0L)
			{
				foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
				{
					ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
					if (zDO != null && zDO.GetLong(CompanionSetup.TombstoneIdHash, 0L) == num)
					{
						allCompanion.GetComponent<CompanionAI>()?.CancelTombstoneRecovery();
						break;
					}
				}
			}
		}
		return true;
	}
}
