using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(PlayerProfile), "SetCustomSpawnPoint")]
public static class SpawnPointBedPatch
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		CompanionRest.MHR_RecheckBedsSoon();
	}
}
