using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(ItemDrop), "DropItem")]
internal static class SpearLandedPatch
{
	private static void Postfix(ItemDrop.ItemData item, ItemDrop __result)
	{
		MHR_SpearThrow.OnDropped(item, __result);
	}
}
