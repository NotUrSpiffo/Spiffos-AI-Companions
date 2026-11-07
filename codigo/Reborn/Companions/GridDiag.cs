using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class GridDiag
{
	private static int _streak;

	private static int _total;

	internal static void Ok()
	{
		_streak = 0;
	}

	internal static void Report(Exception ex, InventoryGrid grid, Inventory inv, Player player, object drag)
	{
		_total++;
		_streak++;
		if (_streak > 2)
		{
			if (_streak == 100 || _streak == 1000 || _streak == 10000)
			{
				Warn($"[GRIDDIAG] UpdateInventory still faulting: streak={_streak} total={_total} (further reports suppressed)");
			}
			return;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("[GRIDDIAG] UpdateInventory threw (streak ");
			stringBuilder.Append($"{_streak}, total {_total})");
			stringBuilder.Append("\n").Append(ex.ToString());
			stringBuilder.Append("\n[GRIDDIAG] top frame: ").Append(TopFrame(ex));
			stringBuilder.Append("\n[GRIDDIAG] playerArgNull=").Append((!(player == null)) ? "false" : "true");
			stringBuilder.Append(" dragItemNull=").Append((drag != null) ? "false" : "true");
			Warn(stringBuilder.ToString());
			StringBuilder stringBuilder2 = new StringBuilder();
			stringBuilder2.Append("[GRIDDIAG] state dump:");
			AppendInventory(stringBuilder2, inv);
			AppendGrid(stringBuilder2, grid, inv);
			stringBuilder2.Append("\n[GRIDDIAG] UpdateGui patch owners: ").Append(HarmonyOwners());
			Warn(stringBuilder2.ToString());
		}
		catch (Exception ex2)
		{
			Warn("[GRIDDIAG] report failed: " + ex2.Message);
		}
	}

	private static void Warn(string s)
	{
		CompanionsPlugin.Log?.LogWarning(s);
	}

	private static string TopFrame(Exception ex)
	{
		try
		{
			StackFrame frame = new StackTrace(ex, fNeedFileInfo: false).GetFrame(0);
			if (frame == null)
			{
				return "(none)";
			}
			MethodBase method = frame.GetMethod();
			return ((!(method == null)) ? method.Name : "(null method)") + $" ilOffset=0x{frame.GetILOffset():X4}";
		}
		catch (Exception ex2)
		{
			return "(failed: " + ex2.Message + ")";
		}
	}

	private static void AppendInventory(StringBuilder sb, Inventory inv)
	{
		try
		{
			if (inv == null)
			{
				sb.Append("\n[GRIDDIAG] inventory: NULL");
				return;
			}
			IList allItems = inv.GetAllItems();
			sb.Append("\n[GRIDDIAG] inventory: ").Append($"{inv.GetWidth()}x{inv.GetHeight()} items={allItems.Count}");
			int num = allItems.Count;
			if (num > 3)
			{
				num = 3;
			}
			for (int i = 0; i < num; i++)
			{
				ItemDrop.ItemData itemData = (ItemDrop.ItemData)allItems[i];
				sb.Append("\n[GRIDDIAG]   item[").Append($"{i}").Append("]: ");
				if (itemData == null)
				{
					sb.Append("NULL");
					continue;
				}
				if (itemData.m_shared == null)
				{
					sb.Append("m_shared=NULL ");
				}
				else
				{
					sb.Append("name=").Append(itemData.m_shared.m_name).Append(" ");
				}
				sb.Append(string.Format("gridPos=({0},{1}) stack={2} equipped={3} dropPrefab={4}", itemData.m_gridPos.x, itemData.m_gridPos.y, itemData.m_stack, itemData.m_equipped, (!(itemData.m_dropPrefab == null)) ? "ok" : "NULL"));
				try
				{
					Sprite icon = itemData.GetIcon();
					sb.Append(" icon=").Append((!(icon == null)) ? "ok" : "NULL");
				}
				catch (Exception ex)
				{
					sb.Append(" GetIcon threw: ").Append(ex.GetType().Name);
				}
			}
		}
		catch (Exception ex2)
		{
			sb.Append("\n[GRIDDIAG] inventory dump failed: ").Append(ex2.Message);
		}
	}

	private static void AppendGrid(StringBuilder sb, InventoryGrid grid, Inventory inv)
	{
		try
		{
			if (grid == null)
			{
				sb.Append("\n[GRIDDIAG] grid: NULL");
				return;
			}
			sb.Append("\n[GRIDDIAG] grid fields: ").Append(DumpFields(grid));
			BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			FieldInfo field = typeof(InventoryGrid).GetField("m_elements", bindingAttr);
			IList list = ((!(field == null)) ? ((IList)field.GetValue(grid)) : null);
			if (list == null)
			{
				sb.Append("\n[GRIDDIAG] m_elements: NULL");
			}
			else
			{
				int num = 0;
				for (int i = 0; i < list.Count; i++)
				{
					object obj = list[i];
					if (obj == null || (obj is UnityEngine.Object && (UnityEngine.Object)obj == null))
					{
						num++;
					}
				}
				sb.Append("\n[GRIDDIAG] m_elements: count=").Append($"{list.Count} nullOrDestroyed={num}");
				if (list.Count > 0)
				{
					sb.Append("\n[GRIDDIAG] element[0] fields: ").Append(DumpFields(list[0]));
				}
				if (inv != null)
				{
					IList allItems = inv.GetAllItems();
					if (allItems.Count > 0)
					{
						ItemDrop.ItemData itemData = (ItemDrop.ItemData)allItems[0];
						if (itemData != null)
						{
							int num2 = itemData.m_gridPos.y * inv.GetWidth() + itemData.m_gridPos.x;
							if (num2 > 0 && num2 < list.Count)
							{
								sb.Append("\n[GRIDDIAG] element[").Append($"{num2}").Append("] (first item's slot) fields: ")
									.Append(DumpFields(list[num2]));
							}
							else
							{
								sb.Append("\n[GRIDDIAG] first item's slot index out of range: ").Append($"{num2}");
							}
						}
					}
				}
			}
			FieldInfo field2 = typeof(InventoryGrid).GetField("m_elementPrefab", bindingAttr);
			GameObject gameObject = ((!(field2 == null)) ? ((GameObject)field2.GetValue(grid)) : null);
			if (gameObject == null)
			{
				sb.Append("\n[GRIDDIAG] m_elementPrefab: NULL");
				return;
			}
			sb.Append("\n[GRIDDIAG] m_elementPrefab: ").Append(gameObject.name);
			Component component = gameObject.GetComponent(typeof(InventoryElement));
			if (component == null)
			{
				sb.Append("  has NO InventoryElement component");
			}
			else
			{
				sb.Append("\n[GRIDDIAG] prefab InventoryElement fields: ").Append(DumpFields(component));
			}
			Transform transform = gameObject.transform.Find("binding");
			sb.Append("\n[GRIDDIAG] prefab 'binding' child: ").Append((!(transform == null)) ? "present" : "MISSING");
		}
		catch (Exception ex)
		{
			sb.Append("\n[GRIDDIAG] grid dump failed: ").Append(ex.Message);
		}
	}

	private static string HarmonyOwners()
	{
		try
		{
			MethodBase method = typeof(InventoryGrid).GetMethod("UpdateGui", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return "(UpdateGui not found)";
			}
			Patches patchInfo = Harmony.GetPatchInfo(method);
			if (patchInfo == null)
			{
				return "(no patches)";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (object item in (IEnumerable)patchInfo.Owners)
			{
				stringBuilder.Append(item).Append("; ");
			}
			if (stringBuilder.Length == 0)
			{
				return "(patch info present, no owners)";
			}
			return stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			return "(failed: " + ex.Message + ")";
		}
	}

	private static string DumpFields(object o)
	{
		if (o == null)
		{
			return "(object is null)";
		}
		if (o is UnityEngine.Object && (UnityEngine.Object)o == null)
		{
			return "(object is DESTROYED)";
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = new StringBuilder();
		FieldInfo[] fields = o.GetType().GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			object obj = null;
			try
			{
				obj = fieldInfo.GetValue(o);
			}
			catch (Exception)
			{
				stringBuilder.Append(fieldInfo.Name).Append("(unreadable) ");
				continue;
			}
			if (obj == null)
			{
				stringBuilder.Append(fieldInfo.Name).Append(" ");
			}
			else if (obj is UnityEngine.Object && (UnityEngine.Object)obj == null)
			{
				stringBuilder.Append(fieldInfo.Name).Append("(DESTROYED) ");
			}
			else if (obj is ValueType || obj is string)
			{
				stringBuilder2.Append(fieldInfo.Name).Append("=").Append(obj.ToString())
					.Append(" ");
			}
		}
		return "NULL[" + stringBuilder.ToString() + "] VALUES[" + stringBuilder2.ToString() + "]";
	}
}
