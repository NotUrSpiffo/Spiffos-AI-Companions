using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Player), "OnRespawn")]
public static class PlayerRespawnHooks
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		RespawnHooks.FullRestore();
	}
}
