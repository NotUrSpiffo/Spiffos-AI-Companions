using System.Reflection;
using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(TombStone), "UpdateDespawn")]
public static class TombstonePreventDespawnPatch
{
	private static readonly MethodInfo s_floater = AccessTools.Method(typeof(TombStone), "UpdateFloater");

	private static readonly MethodInfo s_positionCheck = AccessTools.Method(typeof(TombStone), "PositionCheck");

	[HarmonyPrefix]
	private static bool Prefix(TombStone __instance)
	{
		if (__instance.GetComponent<CompanionTombstoneResurrect>() == null)
		{
			return true;
		}
		try
		{
			if (__instance.m_floater != null)
			{
				s_floater?.Invoke(__instance, null);
			}
			ZNetView component = __instance.GetComponent<ZNetView>();
			if (component != null && component.IsValid() && component.IsOwner())
			{
				s_positionCheck?.Invoke(__instance, null);
			}
		}
		catch
		{
		}
		return false;
	}
}
