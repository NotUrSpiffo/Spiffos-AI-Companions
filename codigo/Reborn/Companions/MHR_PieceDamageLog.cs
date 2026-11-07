using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(WearNTear), "ApplyDamage")]
internal static class MHR_PieceDamageLog
{
	private static readonly Dictionary<int, float> s_last = new Dictionary<int, float>();

	private static void Prefix(WearNTear __instance, float damage, HitData hitData)
	{
		try
		{
			if (__instance == null || damage <= 0f)
			{
				return;
			}
			Vector3 position = __instance.transform.position;
			CompanionSetup companionSetup = null;
			foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
			{
				if (allCompanion != null && Vector3.Distance(allCompanion.transform.position, position) <= 5f)
				{
					companionSetup = allCompanion;
					break;
				}
			}
			if (companionSetup == null)
			{
				return;
			}
			int instanceID = __instance.GetInstanceID();
			if (!s_last.TryGetValue(instanceID, out var value) || !(Time.time - value < 2f))
			{
				if (s_last.Count > 500)
				{
					s_last.Clear();
				}
				s_last[instanceID] = Time.time;
				Piece component = __instance.GetComponent<Piece>();
				string text = ((component != null) ? (MHR_List.Local(component.m_name) ?? component.m_name) : __instance.name);
				string text2;
				if (hitData == null)
				{
					text2 = "wear (rain, water, lava or missing support)";
				}
				else
				{
					Character attacker = hitData.GetAttacker();
					text2 = "hit " + hitData.m_hitType.ToString() + ((attacker != null) ? (" by " + attacker.m_name) : " (no attacker)") + ", fire " + hitData.m_damage.m_fire.ToString("F1") + ", chop " + hitData.m_damage.m_chop.ToString("F1") + ", blunt " + hitData.m_damage.m_blunt.ToString("F1");
				}
				ZNetView component2 = companionSetup.GetComponent<ZNetView>();
				string text3 = ((component2?.GetZDO() != null) ? component2.GetZDO().GetString(CompanionSetup.NameHash, "companion") : "companion");
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Piece] " + text + " at " + MHR_Mover.Fmt(position) + " took " + damage.ToString("F1") + " damage (" + text2 + ") - " + text3 + " is " + Vector3.Distance(companionSetup.transform.position, position).ToString("F1") + " m away at " + MHR_Mover.Fmt(companionSetup.transform.position));
				}
			}
		}
		catch
		{
		}
	}
}
