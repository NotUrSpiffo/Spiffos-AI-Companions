using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(InventoryGui), "Hide")]
public static class InventoryGuiHidePatch
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		CompanionInteractPanel instance = CompanionInteractPanel.Instance;
		if (instance != null && instance.IsNameInputFocused)
		{
			instance.SaveAndDeselectName();
		}
	}
}
