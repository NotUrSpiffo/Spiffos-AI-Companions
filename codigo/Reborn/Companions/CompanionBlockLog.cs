using System.Reflection;
using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Humanoid), "BlockAttack")]
internal static class CompanionBlockLog
{
	private static readonly MethodInfo s_blocker = AccessTools.Method(typeof(Humanoid), "GetCurrentBlocker");

	private static void Prefix(Humanoid __instance, HitData hit, out float __state)
	{
		__state = -1f;
		if (!(__instance == null) && hit != null && (!(__instance.GetComponent<CompanionCombatAI>() == null) || !(__instance != Player.m_localPlayer)))
		{
			__state = hit.GetTotalBlockableDamage();
		}
	}

	private static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result, float __state)
	{
		if (__state < 0f || !__result)
		{
			return;
		}
		__instance.GetComponent<CompanionCombatAI>()?.NoteIncoming(attacker);
		try
		{
			ItemDrop.ItemData itemData = s_blocker?.Invoke(__instance, null) as ItemDrop.ItemData;
			float blockTimer = ReflectionHelper.GetBlockTimer(__instance);
			bool flag = itemData != null && itemData.m_shared.m_timedBlockBonus > 1f && blockTimer != -1f && blockTimer < 0.25f;
			if (__instance == Player.m_localPlayer)
			{
				PlayerCombatLog.NoteBlock(__instance, attacker, flag, __state, hit.GetTotalBlockableDamage());
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug(string.Format("[Block] {0} {1} with {2}: {3:F0} damage, {4:F0} got through", flag ? "Parried" : "Blocked", (attacker != null) ? attacker.m_name : "a hit", itemData?.m_shared?.m_name ?? "?", __state, hit.GetTotalBlockableDamage()));
			}
		}
		catch
		{
		}
	}
}
