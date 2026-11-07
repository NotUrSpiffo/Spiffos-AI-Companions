using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

public static class MHR_SeedRules
{
	private class Fam
	{
		public string Seed;

		public string Source;
	}

	private class Rot
	{
		public string Cur;

		public int Count;
	}

	private static readonly Dictionary<string, float> s_countTime = new Dictionary<string, float>();

	private static readonly Dictionary<string, int> s_countVal = new Dictionary<string, int>();

	private static readonly Dictionary<int, Dictionary<string, Rot>> s_rot = new Dictionary<int, Dictionary<string, Rot>>();

	private static readonly Dictionary<string, string> s_sourceFor = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> s_lower = new Dictionary<string, string>();

	private static int s_mapCount = -1;

	private static string s_listRaw;

	private static List<Fam> s_list = new List<Fam>();

	private static readonly HashSet<string> s_warned = new HashSet<string>();

	private static float s_nextNothingLog;

	private static string OutputOf(string item)
	{
		if (FarmController.s_seedToPlant == null || !FarmController.s_seedToPlant.TryGetValue(item, out var value) || value.PlantPrefab == null)
		{
			return null;
		}
		Plant component = value.PlantPrefab.GetComponent<Plant>();
		if (component == null || component.m_grownPrefabs == null)
		{
			return null;
		}
		for (int i = 0; i < component.m_grownPrefabs.Length; i++)
		{
			GameObject gameObject = component.m_grownPrefabs[i];
			if (!(gameObject == null))
			{
				Pickable component2 = gameObject.GetComponent<Pickable>();
				if (component2 != null && component2.m_itemPrefab != null)
				{
					return component2.m_itemPrefab.name;
				}
			}
		}
		return null;
	}

	public static bool IsSeedSource(string item)
	{
		if (string.IsNullOrEmpty(item) || FarmController.s_seedToPlant == null || !FarmController.s_seedToPlant.TryGetValue(item, out var value) || value.PlantPrefab == null)
		{
			return false;
		}
		if (value.PlantPrefab.name.IndexOf("seed", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		string text = OutputOf(item);
		if (text != null && text != item)
		{
			return FarmController.s_seedToPlant.ContainsKey(text);
		}
		return false;
	}

	public static bool IsStorableCrop(string item)
	{
		if (string.IsNullOrEmpty(item) || FarmController.s_seedToPlant == null)
		{
			return true;
		}
		if (!FarmController.s_seedToPlant.ContainsKey(item))
		{
			return true;
		}
		return IsSeedSource(item);
	}

	public static ItemDrop.ItemData GetItemByPrefab(Inventory inv, string prefab, int quality, bool worstQuality)
	{
		if (inv == null || string.IsNullOrEmpty(prefab))
		{
			return null;
		}
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i] != null && allItems[i].m_dropPrefab != null && allItems[i].m_dropPrefab.name == prefab)
			{
				return allItems[i];
			}
		}
		return inv.GetItem(prefab, quality, worstQuality);
	}

	private static int Min()
	{
		if (ModConfig.SeedReserve == null)
		{
			return 20;
		}
		return Mathf.Max(0, ModConfig.SeedReserve.Value);
	}

	private static int Target()
	{
		return Mathf.Max(Min(), (ModConfig.SeedTarget != null) ? ModConfig.SeedTarget.Value : 100);
	}

	private static bool NeededForSeeds(Component farm, string item)
	{
		if (!IsSeedSource(item))
		{
			return false;
		}
		string text = OutputOf(item);
		if (text != null)
		{
			return CountNear(farm, text) < Target();
		}
		return false;
	}

	public static int Carry()
	{
		return Mathf.Max(1, (ModConfig.SeedCarry != null) ? ModConfig.SeedCarry.Value : 40);
	}

	private static int Keep()
	{
		return Mathf.Max(Min(), Carry());
	}

	private static int GroupSize()
	{
		return Mathf.Max(1, (ModConfig.PlantGroupSize != null) ? ModConfig.PlantGroupSize.Value : 4);
	}

	public static bool AllowPlant(Component farm, string item)
	{
		try
		{
			if (!IsSeedSource(item))
			{
				return true;
			}
			return CountNear(farm, OutputOf(item)) < Target();
		}
		catch
		{
			return true;
		}
	}

	private static void CheckMap()
	{
		int num = ((FarmController.s_seedToPlant != null) ? FarmController.s_seedToPlant.Count : 0);
		if (num == s_mapCount)
		{
			return;
		}
		s_mapCount = num;
		s_sourceFor.Clear();
		s_lower.Clear();
		s_listRaw = null;
		if (FarmController.s_seedToPlant == null)
		{
			return;
		}
		List<string> list = new List<string>(FarmController.s_seedToPlant.Keys);
		list.Sort(StringComparer.OrdinalIgnoreCase);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Farm] Things a cultivator can plant (" + list.Count + "): " + string.Join(", ", list.ToArray()));
		}
		foreach (string key in FarmController.s_seedToPlant.Keys)
		{
			s_lower[key.ToLowerInvariant()] = key;
			if (IsSeedSource(key))
			{
				string text = OutputOf(key);
				if (text != null && !s_sourceFor.ContainsKey(text))
				{
					s_sourceFor[text] = key;
				}
			}
		}
	}

	private static Fam Resolve(string name)
	{
		if (string.IsNullOrEmpty(name) || FarmController.s_seedToPlant == null)
		{
			return null;
		}
		if (!FarmController.s_seedToPlant.ContainsKey(name))
		{
			if (!s_lower.TryGetValue(name.ToLowerInvariant(), out var value))
			{
				string value2 = name.ToLowerInvariant();
				value = null;
				int num = 0;
				foreach (KeyValuePair<string, string> item in s_lower)
				{
					if (item.Key.EndsWith(value2))
					{
						value = item.Value;
						num++;
					}
				}
				if (num != 1)
				{
					return null;
				}
			}
			name = value;
		}
		if (IsSeedSource(name))
		{
			return new Fam
			{
				Seed = OutputOf(name),
				Source = name
			};
		}
		string value3;
		return new Fam
		{
			Seed = name,
			Source = (s_sourceFor.TryGetValue(name, out value3) ? value3 : null)
		};
	}

	private static List<Fam> ListFams()
	{
		string text = ((ModConfig.PlantAny != null) ? ModConfig.PlantAny.Value : "");
		if (text == s_listRaw)
		{
			return s_list;
		}
		s_listRaw = text;
		s_list = new List<Fam>();
		MHR_List mHR_List = MHR_List.Get(text);
		HashSet<string> hashSet = new HashSet<string>();
		List<string> list = new List<string>(mHR_List.Order);
		if (mHR_List.All && FarmController.s_seedToPlant != null)
		{
			foreach (KeyValuePair<string, FarmController.SeedPlantInfo> item in FarmController.s_seedToPlant)
			{
				if (item.Value.NeedsCultivated && !list.Contains(item.Key))
				{
					list.Add(item.Key);
				}
			}
		}
		foreach (string item2 in list)
		{
			Fam fam = Resolve(item2);
			if (fam == null)
			{
				if (s_warned.Add(item2) && CompanionsPlugin.Log != null)
				{
					CompanionsPlugin.Log.LogWarning("[Farm] PlantAny: \"" + item2 + "\" isn't something a cultivator can plant - skipped");
				}
			}
			else if (!mHR_List.IsExcluded(item2, fam.Seed, fam.Source) && hashSet.Add(fam.Seed))
			{
				s_list.Add(fam);
			}
		}
		return s_list;
	}

	private static int CropAmount(Fam f)
	{
		MHR_List mHR_List = MHR_List.Get((ModConfig.PlantAny != null) ? ModConfig.PlantAny.Value : "");
		int num = mHR_List.AmountFor(f.Seed, 0);
		if (num <= 0 && f.Source != null)
		{
			num = mHR_List.AmountFor(f.Source, 0);
		}
		return num;
	}

	private static Rot GetRot(Component farm, string key)
	{
		int instanceID = farm.GetInstanceID();
		if (!s_rot.TryGetValue(instanceID, out var value))
		{
			value = new Dictionary<string, Rot>();
			s_rot[instanceID] = value;
		}
		if (!value.TryGetValue(key, out var value2))
		{
			value2 = (value[key] = new Rot());
		}
		return value2;
	}

	public static string PickSeed(Component farm, string zoneCrop)
	{
		try
		{
			if (farm == null || FarmController.s_seedToPlant == null)
			{
				return string.IsNullOrEmpty(zoneCrop) ? null : zoneCrop;
			}
			CheckMap();
			string text = (string.IsNullOrEmpty(zoneCrop) ? "*" : zoneCrop);
			List<Fam> list;
			if (!string.IsNullOrEmpty(zoneCrop))
			{
				Fam fam = Resolve(zoneCrop);
				if (fam == null || zoneCrop == fam.Source)
				{
					return zoneCrop;
				}
				list = new List<Fam> { fam };
			}
			else
			{
				list = ListFams();
			}
			int num = Min();
			int num2 = Target();
			int num3 = GroupSize();
			List<string> list2 = new List<string>();
			List<bool> list3 = new List<bool>();
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < list.Count; i++)
			{
				Fam fam2 = list[i];
				int num4 = CountNear(farm, fam2.Seed);
				int num5 = ((fam2.Source != null) ? CountNear(farm, fam2.Source) : 0);
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append("; ");
				}
				stringBuilder.Append(fam2.Seed).Append(' ').Append(num4);
				if (fam2.Source != null)
				{
					stringBuilder.Append('/').Append(num).Append(", ")
						.Append(fam2.Source)
						.Append(' ')
						.Append(num5);
				}
				list2.Add(fam2.Seed);
				bool item = ((fam2.Source == null) ? (num4 > 0) : (num4 > num || (num4 > 0 && num5 == 0)));
				int num6 = CropAmount(fam2);
				if (num6 > 0 && ((fam2.Source != null) ? num5 : num4) >= num6)
				{
					item = false;
					stringBuilder.Append(" (enough, ").Append(num6).Append(')');
				}
				list3.Add(item);
				if (fam2.Source != null)
				{
					list2.Add(fam2.Source);
					list3.Add(num4 < num2 && num5 > 0);
				}
			}
			int count = list2.Count;
			if (count == 0)
			{
				return null;
			}
			Rot rot = GetRot(farm, text);
			int num7 = ((rot.Cur != null) ? list2.IndexOf(rot.Cur) : (-1));
			if (num7 >= 0 && list3[num7] && rot.Count < num3)
			{
				return rot.Cur;
			}
			for (int j = 1; j <= count; j++)
			{
				int index = ((num7 + j) % count + count) % count;
				if (list3[index])
				{
					rot.Cur = list2[index];
					rot.Count = 0;
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Farm] Planting group: next up \"" + rot.Cur + "\" (" + ((text == "*") ? "Any zone" : ("zone " + text)) + ")");
					}
					return rot.Cur;
				}
			}
			if (Time.time >= s_nextNothingLog && CompanionsPlugin.Log != null)
			{
				s_nextNothingLog = Time.time + 60f;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Farm] Nothing to plant in " + ((text == "*") ? "Any zones" : ("\"" + text + "\" zones")) + " (pack + chests near home): " + ((stringBuilder.Length > 0) ? stringBuilder.ToString() : "the PlantAny list is empty"));
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning("[Farm] PickSeed: " + ex.Message);
			}
			return string.IsNullOrEmpty(zoneCrop) ? null : zoneCrop;
		}
	}

	public static bool GroupDone(Component farm, string item)
	{
		try
		{
			if (farm == null || item == null || !s_rot.TryGetValue(farm.GetInstanceID(), out var value))
			{
				return false;
			}
			int num = GroupSize();
			foreach (Rot value2 in value.Values)
			{
				if (value2.Cur == item && value2.Count >= num)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	public static int TakeExtras(Component farm, Inventory chest, Inventory pack)
	{
		int num = 0;
		try
		{
			if (farm == null || chest == null || pack == null || FarmController.s_seedToPlant == null)
			{
				return 0;
			}
			CheckMap();
			int num2 = Carry();
			int num3 = Target();
			List<Fam> list = ListFams();
			List<string> list2 = new List<string>();
			for (int i = 0; i < list.Count; i++)
			{
				list2.Add(list[i].Seed);
				if (list[i].Source != null && CountNear(farm, list[i].Seed) < num3)
				{
					list2.Add(list[i].Source);
				}
			}
			for (int j = 0; j < list2.Count; j++)
			{
				string text = list2[j];
				int num4 = num2 - CountIn(pack, text);
				if (num4 <= 0)
				{
					continue;
				}
				List<ItemDrop.ItemData> list3 = new List<ItemDrop.ItemData>(chest.GetAllItems());
				for (int k = 0; k < list3.Count; k++)
				{
					if (num4 <= 0)
					{
						break;
					}
					ItemDrop.ItemData itemData = list3[k];
					if (itemData != null && !(itemData.m_dropPrefab == null) && !(itemData.m_dropPrefab.name != text))
					{
						int num5 = Mathf.Min(num4, itemData.m_stack);
						ItemDrop.ItemData itemData2 = itemData.Clone();
						itemData2.m_stack = num5;
						if (!pack.CanAddItem(itemData2, num5) || !pack.AddItem(itemData2))
						{
							break;
						}
						if (num5 >= itemData.m_stack)
						{
							chest.RemoveItem(itemData);
						}
						else
						{
							chest.RemoveItem(itemData, num5);
						}
						num4 -= num5;
						num += num5;
						s_countTime.Remove(text);
						if (CompanionsPlugin.LogDetailed)
						{
							CompanionsPlugin.Log.LogDebug("[Farm]   Also took " + num5 + "x \"" + text + "\"");
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null)
			{
				CompanionsPlugin.Log.LogWarning("[Farm] TakeExtras: " + ex.Message);
			}
		}
		return num;
	}

	public static void OnPlanted(Component farm, string item)
	{
		try
		{
			if (farm == null || item == null)
			{
				return;
			}
			s_countTime.Remove(item);
			if (!s_rot.TryGetValue(farm.GetInstanceID(), out var value))
			{
				return;
			}
			foreach (Rot value2 in value.Values)
			{
				if (value2.Cur == item)
				{
					value2.Count++;
				}
			}
		}
		catch
		{
		}
	}

	public static bool WantsStore(Component farm, ItemDrop.ItemData item)
	{
		if (item == null || item.m_dropPrefab == null)
		{
			return false;
		}
		string name = item.m_dropPrefab.name;
		if (IsStorableCrop(name) && !NeededForSeeds(farm, name))
		{
			return true;
		}
		Humanoid humanoid = ((farm != null) ? farm.GetComponent<Humanoid>() : null);
		if (humanoid == null || humanoid.GetInventory() == null)
		{
			return false;
		}
		return CountIn(humanoid.GetInventory(), name) > Keep();
	}

	public static int StoreOutput(FarmController farm, Inventory pack, Inventory chest)
	{
		int num = 0;
		if (farm == null || pack == null || chest == null)
		{
			return 0;
		}
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(pack.GetAllItems());
		int num2 = list.Count - 1;
		while (num2 >= 0)
		{
			ItemDrop.ItemData itemData = list[num2];
			try
			{
				string name;
				int num3;
				if (itemData != null && !(itemData.m_dropPrefab == null) && farm.IsCropItem(itemData))
				{
					name = itemData.m_dropPrefab.name;
					num3 = itemData.m_stack;
					if (IsStorableCrop(name) && !NeededForSeeds(farm, name))
					{
						goto IL_00a5;
					}
					int num4 = CountIn(pack, name) - Keep();
					if (num4 > 0)
					{
						num3 = Mathf.Min(num4, itemData.m_stack);
						goto IL_00a5;
					}
				}
				continue;
				IL_00a5:
				ItemDrop.ItemData itemData2 = itemData.Clone();
				itemData2.m_stack = num3;
				if (chest.CanAddItem(itemData2, num3) && chest.AddItem(itemData2))
				{
					if (num3 >= itemData.m_stack)
					{
						pack.RemoveItem(itemData);
					}
					else
					{
						pack.RemoveItem(itemData, num3);
					}
					s_countTime.Remove(name);
					num++;
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Farm]   Stored \"" + name + "\" x" + num3);
					}
				}
			}
			catch (Exception ex)
			{
				if (CompanionsPlugin.Log != null)
				{
					CompanionsPlugin.Log.LogWarning("[Farm] Store: " + ex.Message);
				}
			}
			num2--;
		}
		return num;
	}

	private static int CountNear(Component farm, string prefab)
	{
		if (s_countTime.TryGetValue(prefab, out var value) && Time.time - value < 10f)
		{
			return s_countVal[prefab];
		}
		int num = 0;
		Humanoid component = farm.GetComponent<Humanoid>();
		if (component != null && component.GetInventory() != null)
		{
			num += CountIn(component.GetInventory(), prefab);
		}
		CompanionSetup component2 = farm.GetComponent<CompanionSetup>();
		List<Container> list = MHR_Chests.Near((component2 != null && component2.HasHomePosition()) ? component2.GetHomePosition() : farm.transform.position, 30f);
		for (int i = 0; i < list.Count; i++)
		{
			Container container = list[i];
			if (!(container == null) && !(container.GetComponentInParent<Character>() != null) && !(container.GetComponent<TombStone>() != null) && container.GetInventory() != null)
			{
				num += CountIn(container.GetInventory(), prefab);
			}
		}
		s_countTime[prefab] = Time.time;
		s_countVal[prefab] = num;
		return num;
	}

	private static int CountIn(Inventory inv, string prefab)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i] != null && allItems[i].m_dropPrefab != null && allItems[i].m_dropPrefab.name == prefab)
			{
				num += allItems[i].m_stack;
			}
		}
		return num;
	}
}
