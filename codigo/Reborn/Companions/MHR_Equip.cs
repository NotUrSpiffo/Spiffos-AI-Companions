using System.Collections.Generic;

namespace Companions;

public static class MHR_Equip
{
	public static ItemDrop.ItemData DurabilityItem(Humanoid h)
	{
		ItemDrop.ItemData itemData = ((h != null) ? h.GetCurrentWeapon() : null);
		if (itemData == null || !h.IsItemEquiped(itemData))
		{
			return null;
		}
		return itemData;
	}

	public static void OnToolBroke(Humanoid h)
	{
		try
		{
			if (!(h == null))
			{
				HarvestController component = h.GetComponent<HarvestController>();
				if (component != null)
				{
					component.RestoreLoadout();
				}
				CompanionSetup component2 = h.GetComponent<CompanionSetup>();
				if (component2 != null)
				{
					component2.SuppressAutoEquip = false;
					component2.SyncEquipmentToInventory();
				}
			}
		}
		catch
		{
		}
	}

	public static void ClearLeftFor(Humanoid h, ItemDrop.ItemData left, ItemDrop.ItemData tool)
	{
		if (!(h == null) && left != null)
		{
			bool num = tool != null && tool.m_shared != null && (tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || tool.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
			bool flag = left.m_shared != null && (left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield || left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch);
			if (!(!num & flag))
			{
				h.UnequipItem(left, triggerEquipEffects: false);
			}
		}
	}

	public static void SwapOrUnequip(Humanoid h, List<ItemDrop.ItemData> items, ItemDrop.ItemData old)
	{
		if (h == null || old == null)
		{
			return;
		}
		ItemDrop.ItemData itemData = null;
		try
		{
			string text = ((old.m_dropPrefab != null) ? old.m_dropPrefab.name : null);
			if (text != null && items != null)
			{
				for (int i = 0; i < items.Count; i++)
				{
					ItemDrop.ItemData itemData2 = items[i];
					if (itemData2 != null && itemData2 != old && !itemData2.m_equipped && !(itemData2.m_dropPrefab == null) && itemData2.m_dropPrefab.name == text && itemData2.m_quality == old.m_quality && itemData2.m_variant == old.m_variant)
					{
						itemData = itemData2;
						break;
					}
				}
			}
		}
		catch
		{
			itemData = null;
		}
		h.UnequipItem(old, triggerEquipEffects: false);
		if (itemData != null && (!itemData.m_shared.m_useDurability || !(itemData.m_durability <= 0f)))
		{
			h.EquipItem(itemData, triggerEquipEffects: false);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Inventory reloaded - kept \"" + itemData.m_shared.m_name + "\" equipped");
			}
		}
	}
}
