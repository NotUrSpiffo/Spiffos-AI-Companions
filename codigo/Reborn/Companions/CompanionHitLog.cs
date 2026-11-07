using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "Damage")]
internal static class CompanionHitLog
{
	private static void Prefix(Character __instance, HitData hit)
	{
		if (hit == null || __instance == null)
		{
			return;
		}
		Character attacker = hit.GetAttacker();
		if (attacker == null || attacker == __instance)
		{
			return;
		}
		if (attacker == Player.m_localPlayer)
		{
			PlayerCombatLog.NoteHit(__instance, hit);
			return;
		}
		CompanionCombatAI component = attacker.GetComponent<CompanionCombatAI>();
		if (component != null)
		{
			component.NoteHit(__instance, hit);
		}
	}
}
