using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Player), "OnDeath")]
public static class PlayerDeathHooks
{
	[HarmonyPostfix]
	private static void Postfix(Player __instance)
	{
		ReturnLogic.OnPlayerDeath(__instance);
	}
}
