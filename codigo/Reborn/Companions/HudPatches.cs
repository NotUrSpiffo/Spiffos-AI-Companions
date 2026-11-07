using HarmonyLib;

namespace Companions;

public static class HudPatches
{
	[HarmonyPatch(typeof(Minimap), "Awake")]
	private static class Minimap_Awake_Patch
	{
		private static void Postfix()
		{
			CompanionHudPanel.EnsureInstance();
		}
	}
}
