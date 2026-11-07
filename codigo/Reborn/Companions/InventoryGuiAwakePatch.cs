using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(InventoryGui), "Awake")]
public static class InventoryGuiAwakePatch
{
	[HarmonyPostfix]
	private static void Postfix()
	{
		CompanionInteractPanel.EnsureInstance();
		CompanionRadialMenu.EnsureInstance();
		CompanionRadialMenu.WarmCache();
	}
}
