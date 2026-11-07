using System;
using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "OnDeath")]
public static class MHR_Loot_OnDeath_Patch
{
	private static void Prefix(Character __instance, HitData ___m_lastHit)
	{
		try
		{
			Character character = ___m_lastHit?.GetAttacker();
			if (character == null)
			{
				return;
			}
			CompanionSetup component = character.GetComponent<CompanionSetup>();
			if (!(component == null))
			{
				ZNetView component2 = component.GetComponent<ZNetView>();
				if (!(component2 == null) && component2.IsValid() && component2.IsOwner())
				{
					MHR_Loot.OnKill(component, __instance);
				}
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning("[Loot] " + ex.Message);
			}
		}
	}
}
