using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Player), "OnSpawned")]
public static class PlayerHooks
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		CompanionManager.RestoreFollowTargets();
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			CompanionAI component = allCompanion.GetComponent<CompanionAI>();
			if (component != null)
			{
				component.IgnorePlayerCollision();
			}
		}
		ReturnLogic.OnPlayerRespawn();
	}
}
