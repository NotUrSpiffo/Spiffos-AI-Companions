using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Humanoid), "HaveQueuedChain")]
internal static class CompanionChainPatch
{
	private static void Postfix(Humanoid __instance, ref bool __result)
	{
		if (__result || __instance == null)
		{
			return;
		}
		CompanionCombatAI component = __instance.GetComponent<CompanionCombatAI>();
		if (component != null && component.WantsChain)
		{
			__result = component.CurrentAttackCanChain();
			return;
		}
		HarvestController component2 = __instance.GetComponent<HarvestController>();
		if (component2 != null && component2.WantsChain)
		{
			__result = component2.CurrentAttackCanChain();
		}
	}
}
