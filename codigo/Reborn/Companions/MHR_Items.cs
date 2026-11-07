using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

internal static class MHR_Items
{
	internal static string NiceName(GameObject go)
	{
		if (go == null)
		{
			return "";
		}
		string text = null;
		try
		{
			text = go.GetComponentInParent<Hoverable>()?.GetHoverName();
			if (string.IsNullOrEmpty(text))
			{
				text = go.GetComponentInParent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
			}
			if (!string.IsNullOrEmpty(text) && Localization.instance != null)
			{
				text = Localization.instance.Localize(text);
			}
		}
		catch
		{
			text = null;
		}
		if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
		{
			return text;
		}
		string text2 = go.name.Replace("(Clone)", "").Trim().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', ' ');
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < text2.Length; i++)
		{
			char c = text2[i];
			if (c == '_')
			{
				stringBuilder.Append(' ');
				continue;
			}
			if (i > 0 && char.IsUpper(c) && char.IsLower(text2[i - 1]))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append((stringBuilder.Length == 0) ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
		}
		return stringBuilder.ToString();
	}

	internal static int CountItemInInventory(Inventory inv, string prefabName)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if ((allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name) == prefabName)
			{
				num += allItems[i].m_stack;
			}
		}
		return num;
	}

	internal static ItemDrop.ItemData FindItemByPrefab(Inventory inv, string prefabName)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i].m_dropPrefab != null && allItems[i].m_dropPrefab.name == prefabName)
			{
				return allItems[i];
			}
		}
		return null;
	}

	internal static bool TransferOne(Inventory source, Inventory dest, ItemDrop.ItemData item)
	{
		ItemDrop.ItemData itemData = item.Clone();
		itemData.m_stack = 1;
		if (!dest.CanAddItem(itemData, 1))
		{
			return false;
		}
		if (!dest.AddItem(itemData))
		{
			return false;
		}
		source.RemoveItem(item, 1);
		return true;
	}

	internal static bool ConsumeOneFromInventory(Inventory inv, string prefabName)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if ((allItems[i].m_dropPrefab?.name ?? allItems[i].m_shared?.m_name) == prefabName)
			{
				inv.RemoveItem(allItems[i], 1);
				return true;
			}
		}
		return false;
	}
}
