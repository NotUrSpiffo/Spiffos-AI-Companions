using System;
using System.Reflection;
using HarmonyLib;

namespace Companions;

[HarmonyPatch]
public static class MHR_MoveItemToThis2_Patch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(Inventory), "MoveItemToThis", new Type[2]
		{
			typeof(Inventory),
			typeof(ItemDrop.ItemData)
		});
	}

	[HarmonyPrefix]
	private static void Prefix(Inventory __instance, Inventory fromInventory, ItemDrop.ItemData item)
	{
		try
		{
			MHR_EquipMove.UnequipIfFromCompanion(__instance, fromInventory, item);
		}
		catch (Exception)
		{
		}
	}
}
