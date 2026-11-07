using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Game), "SavePlayerProfile")]
public static class RosterSavePatch
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		MHR_Roster.OnProfileSave();
	}
}
