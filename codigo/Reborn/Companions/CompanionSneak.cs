using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class CompanionSneak
{
	[HarmonyPatch(typeof(Character), "IsCrouching")]
	private static class IsCrouching_Patch
	{
		private static void Postfix(Character __instance, ref bool __result)
		{
			if (!__result && Crouching.Count > 0 && Crouching.Contains(__instance) && !__instance.IsSwimming())
			{
				__result = true;
			}
		}
	}

	[HarmonyPatch(typeof(Character), "GetStealthFactor")]
	private static class GetStealthFactor_Patch
	{
		private static void Postfix(Character __instance, ref float __result)
		{
			if (Crouching.Count == 0 || !Crouching.Contains(__instance))
			{
				return;
			}
			float num = 1f;
			try
			{
				if (StealthSystem.instance != null)
				{
					num = StealthSystem.instance.GetLightFactor(__instance.GetCenterPoint());
				}
			}
			catch
			{
			}
			float skillFactor = __instance.GetSkillFactor(Skills.SkillType.Sneak);
			__result = Mathf.Clamp01(Mathf.Lerp(0.5f + num * 0.5f, 0.2f + num * 0.4f, skillFactor));
		}
	}

	internal static readonly HashSet<Character> Crouching = new HashSet<Character>();
}
