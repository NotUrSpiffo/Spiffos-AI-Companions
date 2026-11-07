using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(Character), "UpdateGroundContact")]
internal static class CompanionFallDamagePatch
{
	private static readonly AccessTools.FieldRef<Character, bool> s_groundContact = AccessTools.FieldRefAccess<Character, bool>("m_groundContact");

	private static readonly AccessTools.FieldRef<Character, float> s_maxAirAltitude = AccessTools.FieldRefAccess<Character, float>("m_maxAirAltitude");

	private static readonly Dictionary<int, Vector3> s_lastPos = new Dictionary<int, Vector3>();

	private static void Prefix(Character __instance, out float __state)
	{
		__state = 0f;
		try
		{
			if (!__instance.IsPlayer() && !(__instance.GetComponent<CompanionSetup>() == null))
			{
				int instanceID = __instance.GetInstanceID();
				Vector3 position = __instance.transform.position;
				if (s_lastPos.TryGetValue(instanceID, out var value) && (position - value).sqrMagnitude > 36f)
				{
					s_maxAirAltitude(__instance) = position.y;
				}
				s_lastPos[instanceID] = position;
				if (s_groundContact(__instance))
				{
					__state = Mathf.Max(0f, s_maxAirAltitude(__instance) - position.y);
				}
			}
		}
		catch
		{
		}
	}

	private static void Postfix(Character __instance, float __state)
	{
		if (__state <= 4f)
		{
			return;
		}
		try
		{
			ZNetView component = __instance.GetComponent<ZNetView>();
			if (component == null || !component.IsOwner() || __instance.IsDead() || __instance.InLiquid())
			{
				return;
			}
			float damage = Mathf.Clamp01((__state - 4f) / 16f) * 100f;
			__instance.GetSEMan()?.ModifyFallDamage(damage, ref damage);
			if (damage > 0f)
			{
				HitData hitData = new HitData();
				hitData.m_damage.m_damage = damage;
				hitData.m_point = __instance.transform.position;
				hitData.m_dir = Vector3.up;
				hitData.m_hitType = HitData.HitType.Fall;
				__instance.Damage(hitData);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo($"[Move] \"{__instance.m_name}\" fell {__state:F1} m and took {damage:F0} fall damage");
				}
			}
		}
		catch
		{
		}
	}

	internal static float SafeDrop(Character c, float share)
	{
		if (c == null)
		{
			return 4f;
		}
		return 4f + 16f * Mathf.Clamp01(c.GetHealth() * share / 100f);
	}
}
