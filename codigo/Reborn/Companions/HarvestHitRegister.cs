using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch]
internal static class HarvestHitRegister
{
	[HarmonyPatch(typeof(TreeLog), "Damage")]
	[HarmonyPostfix]
	private static void TreeLogHit(TreeLog __instance, HitData hit)
	{
		Register(__instance, hit);
	}

	[HarmonyPatch(typeof(TreeBase), "Damage")]
	[HarmonyPostfix]
	private static void TreeBaseHit(TreeBase __instance, HitData hit)
	{
		Register(__instance, hit);
	}

	[HarmonyPatch(typeof(Destructible), "Damage")]
	[HarmonyPostfix]
	private static void DestructibleHit(Destructible __instance, HitData hit)
	{
		Register(__instance, hit);
	}

	[HarmonyPatch(typeof(MineRock), "Damage")]
	[HarmonyPostfix]
	private static void MineRockHit(MineRock __instance, HitData hit)
	{
		Register(__instance, hit);
	}

	[HarmonyPatch(typeof(MineRock5), "Damage")]
	[HarmonyPostfix]
	private static void MineRock5Hit(MineRock5 __instance, HitData hit)
	{
		Register(__instance, hit);
	}

	private static void Register(Component target, HitData hit)
	{
		try
		{
			if (!(target == null) && hit != null)
			{
				Character attacker = hit.GetAttacker();
				if (!(attacker == null) && !(attacker is Player))
				{
					attacker.GetComponent<HarvestController>()?.OnHitRegistered(target.gameObject);
				}
			}
		}
		catch
		{
		}
	}
}
