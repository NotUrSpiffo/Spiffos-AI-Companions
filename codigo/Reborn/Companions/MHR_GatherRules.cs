using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

public static class MHR_GatherRules
{
	private class Per
	{
		public int LimitRejects;

		public int OtherRejects;

		public int NearBaseRejects;

		public readonly Dictionary<string, KeyValuePair<int, int>> AtLimit = new Dictionary<string, KeyValuePair<int, int>>();

		public float NextPlenty;

		public bool PackFull;

		public float DodgeUntil;

		public Vector3 DodgePoint;

		public int ApproachFor;

		public Vector3 ApproachOffset;

		public bool ApproachHas;

		public bool TooHeavy;

		public float LastHeavyCall;

		public float NextDropOffCheck;

		public float NextHeavySpeak;

		public bool PileActive;

		public float PileUntil;

		public Vector3 PilePoint;

		public bool PileHas;

		public Vector3 PileSpot;
	}

	private static readonly Dictionary<int, Per> s_per = new Dictionary<int, Per>();

	private static readonly List<Piece> s_pieces = new List<Piece>();

	private static readonly Dictionary<string, float> s_countTime = new Dictionary<string, float>();

	private static readonly Dictionary<string, int> s_countVal = new Dictionary<string, int>();

	private static readonly Dictionary<int, KeyValuePair<float, bool>> s_protect = new Dictionary<int, KeyValuePair<float, bool>>();

	private static readonly Dictionary<int, List<string>> s_dropsCache = new Dictionary<int, List<string>>();

	private static readonly Dictionary<string, Dictionary<string, int>> s_listCache = new Dictionary<string, Dictionary<string, int>>();

	private static readonly HashSet<string> s_checkedNames = new HashSet<string>();

	private static readonly List<Piece> s_piles = new List<Piece>();

	private static Per P(Component c)
	{
		int instanceID = c.GetInstanceID();
		if (!s_per.TryGetValue(instanceID, out var value))
		{
			value = new Per();
			s_per[instanceID] = value;
		}
		return value;
	}

	private static string ListFor(int mode)
	{
		switch (mode)
		{
		case 1:
			if (ModConfig.GatherWood == null)
			{
				return null;
			}
			return ModConfig.GatherWood.Value;
		case 2:
			if (ModConfig.GatherStone == null)
			{
				return null;
			}
			return ModConfig.GatherStone.Value;
		case 3:
			if (ModConfig.GatherOre == null)
			{
				return null;
			}
			return ModConfig.GatherOre.Value;
		default:
			return null;
		}
	}

	private static int DefaultLimit(int mode)
	{
		return ((ConfigEntry<int>)(mode switch
		{
			3 => (object)ModConfig.OreLimit, 
			2 => ModConfig.StoneLimit, 
			1 => ModConfig.WoodLimit, 
			_ => null, 
		}))?.Value ?? 500;
	}

	private static MHR_List Parse(int mode)
	{
		string text = ListFor(mode);
		if (text == null)
		{
			return null;
		}
		MHR_List mHR_List = MHR_List.Get(text);
		foreach (string item in mHR_List.Order)
		{
			CheckKnown(item, mode);
		}
		return mHR_List;
	}

	private static string ItemLocal(string prefab)
	{
		try
		{
			GameObject gameObject = ((ObjectDB.instance != null) ? ObjectDB.instance.GetItemPrefab(prefab) : null);
			ItemDrop itemDrop = ((gameObject != null) ? gameObject.GetComponent<ItemDrop>() : null);
			return (itemDrop != null) ? MHR_List.Local(itemDrop.m_itemData.m_shared.m_name) : null;
		}
		catch
		{
			return null;
		}
	}

	private static void CheckKnown(string name, int mode)
	{
		if (!s_checkedNames.Add(mode + ":" + name))
		{
			return;
		}
		try
		{
			if (ObjectDB.instance != null && ObjectDB.instance.m_items != null && ObjectDB.instance.m_items.Count > 0 && ObjectDB.instance.GetItemPrefab(name) == null)
			{
				Warn("\"" + name + "\" in " + mode switch
				{
					2 => "GatherStone", 
					1 => "GatherWood", 
					_ => "GatherOre", 
				} + " isn't an item the game knows - check the spelling");
			}
		}
		catch
		{
		}
	}

	private static bool OnAnyList(string name)
	{
		for (int i = 1; i <= 3; i++)
		{
			MHR_List mHR_List = Parse(i);
			if (mHR_List != null && mHR_List.Allows(name))
			{
				return true;
			}
		}
		return false;
	}

	private static void AddTable(DropTable t, List<string> outList)
	{
		if (t == null || t.m_drops == null)
		{
			return;
		}
		for (int i = 0; i < t.m_drops.Count; i++)
		{
			GameObject item = t.m_drops[i].m_item;
			if (item != null && !outList.Contains(item.name))
			{
				outList.Add(item.name);
			}
		}
	}

	private static void AddLog(GameObject logPrefab, List<string> outList, int depth)
	{
		if (!(logPrefab == null) && depth <= 4)
		{
			TreeLog component = logPrefab.GetComponent<TreeLog>();
			if (!(component == null))
			{
				AddTable(component.m_dropWhenDestroyed, outList);
				AddLog(component.m_subLogPrefab, outList, depth + 1);
			}
		}
	}

	private static List<string> DropsOf(GameObject go)
	{
		int instanceID = go.GetInstanceID();
		if (s_dropsCache.TryGetValue(instanceID, out var value))
		{
			return value;
		}
		value = new List<string>();
		TreeBase component = go.GetComponent<TreeBase>();
		if (component != null)
		{
			AddTable(component.m_dropWhenDestroyed, value);
			AddLog(component.m_logPrefab, value, 0);
		}
		TreeLog component2 = go.GetComponent<TreeLog>();
		if (component2 != null)
		{
			AddTable(component2.m_dropWhenDestroyed, value);
			AddLog(component2.m_subLogPrefab, value, 0);
		}
		MineRock component3 = go.GetComponent<MineRock>();
		if (component3 != null)
		{
			AddTable(component3.m_dropItems, value);
		}
		MineRock5 component4 = go.GetComponent<MineRock5>();
		if (component4 != null)
		{
			AddTable(component4.m_dropItems, value);
		}
		DropOnDestroyed component5 = go.GetComponent<DropOnDestroyed>();
		if (component5 != null)
		{
			AddTable(component5.m_dropWhenDestroyed, value);
		}
		if (s_dropsCache.Count > 2000)
		{
			s_dropsCache.Clear();
		}
		s_dropsCache[instanceID] = value;
		return value;
	}

	internal static int Stock(Component c, string item)
	{
		if (s_countTime.TryGetValue(item, out var value) && Time.time - value < 10f)
		{
			return s_countVal[item];
		}
		int num = 0;
		Humanoid component = c.GetComponent<Humanoid>();
		if (component != null && component.GetInventory() != null)
		{
			num += CountIn(component.GetInventory(), item);
		}
		CompanionSetup component2 = c.GetComponent<CompanionSetup>();
		Vector3 vector = ((component2 != null && component2.HasHomePosition()) ? component2.GetHomePosition() : c.transform.position);
		List<Container> list = MHR_Chests.Near(vector, 40f);
		for (int i = 0; i < list.Count; i++)
		{
			Container container = list[i];
			if (!(container == null) && !(container.GetComponentInParent<Character>() != null) && !(container.GetComponent<TombStone>() != null) && container.GetInventory() != null)
			{
				num += CountIn(container.GetInventory(), item);
			}
		}
		s_piles.Clear();
		Piece.GetAllPiecesInRadius(vector, 40f, s_piles);
		for (int j = 0; j < s_piles.Count; j++)
		{
			Piece piece = s_piles[j];
			if (UpkeepController.IsPile(piece))
			{
				Piece.Requirement requirement = piece.m_resources[0];
				if (string.Equals(requirement.m_resItem.gameObject.name, item, StringComparison.OrdinalIgnoreCase))
				{
					num += requirement.m_amount;
				}
			}
		}
		s_countTime[item] = Time.time;
		s_countVal[item] = num;
		return num;
	}

	private static int CountIn(Inventory inv, string prefab)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i] != null && allItems[i].m_dropPrefab != null && string.Equals(allItems[i].m_dropPrefab.name, prefab, StringComparison.OrdinalIgnoreCase))
			{
				num += allItems[i].m_stack;
			}
		}
		return num;
	}

	public static GameObject Scan(HarvestController h, int mode)
	{
		if (mode == 3)
		{
			Humanoid component = h.GetComponent<Humanoid>();
			if (component != null && component.GetInventory() != null && CountIn(component.GetInventory(), "Stone") >= 50 && StartPile(h))
			{
				return null;
			}
		}
		Per per = P(h);
		per.LimitRejects = 0;
		per.OtherRejects = 0;
		per.NearBaseRejects = 0;
		per.AtLimit.Clear();
		GameObject gameObject = h.ScanForTarget(mode);
		if (gameObject == null)
		{
			List<CompanionStatus.Item> list = new List<CompanionStatus.Item>();
			foreach (KeyValuePair<string, KeyValuePair<int, int>> item in per.AtLimit)
			{
				list.Add(new CompanionStatus.Item(null, "hc_why_gather_limit", ItemLocal(item.Key) ?? item.Key, item.Value.Key, item.Value.Value));
			}
			if (h.LastTierSkips > 0)
			{
				list.Add(new CompanionStatus.Item(null, "hc_why_gather_tier", h.LastTierName ?? "?", ModLocalization.Loc((mode == 1) ? "hc_tool_axe" : "hc_tool_pickaxe")));
			}
			if (per.NearBaseRejects > 0)
			{
				list.Add(new CompanionStatus.Item(null, "hc_why_gather_near_base", per.NearBaseRejects, ((ModConfig.BaseProtectRadius != null) ? ModConfig.BaseProtectRadius.Value : 10f).ToString("F0")));
			}
			if (list.Count == 0)
			{
				list.Add(new CompanionStatus.Item(null, "hc_why_gather_none"));
			}
			CompanionStatus.WhyRotate(h, list);
		}
		if (gameObject == null && per.LimitRejects > 0 && mode >= 1 && mode <= 3 && Time.time >= per.NextPlenty)
		{
			per.NextPlenty = Time.time + 120f;
			if (CompanionsPlugin.LogNormal)
			{
				Info("Everything it can reach on the " + mode switch
				{
					2 => "stone", 
					1 => "wood", 
					_ => "ore", 
				} + " list is at its limit - waiting");
			}
			CompanionTalk component2 = h.GetComponent<CompanionTalk>();
			if (component2 != null)
			{
				component2.Say(ModLocalization.Loc(mode switch
				{
					2 => "hc_speech_plenty_stone", 
					1 => "hc_speech_plenty_wood", 
					_ => "hc_speech_plenty_ore", 
				}));
			}
		}
		return gameObject;
	}

	public static bool AllowTarget(HarvestController h, GameObject go, int mode)
	{
		try
		{
			if (go == null || mode < 1 || mode > 3)
			{
				return true;
			}
			Per per = P(h);
			if (go.GetComponent<TreeBase>() != null)
			{
				string text = ((ModConfig.WoodSkipTrees != null) ? ModConfig.WoodSkipTrees.Value : "");
				if (!string.IsNullOrEmpty(text))
				{
					string[] array = text.Split(new char[1] { ',' });
					for (int i = 0; i < array.Length; i++)
					{
						string text2 = array[i].Trim();
						if (text2.Length > 0 && go.name.IndexOf(text2, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							per.OtherRejects++;
							return false;
						}
					}
				}
				if (NearBuildings(go))
				{
					per.OtherRejects++;
					per.NearBaseRejects++;
					return false;
				}
			}
			MHR_List mHR_List = Parse(mode);
			if (mHR_List == null)
			{
				return true;
			}
			List<string> list = DropsOf(go);
			if (list.Count == 0)
			{
				return true;
			}
			bool flag = false;
			for (int j = 0; j < list.Count; j++)
			{
				if (mHR_List.Allows(list[j], ItemLocal(list[j])))
				{
					int num = mHR_List.AmountFor(list[j], DefaultLimit(mode));
					flag = true;
					int num2 = Stock(h, list[j]);
					if (num <= 0 || num2 < num)
					{
						return true;
					}
					if (per.AtLimit.Count < 8)
					{
						per.AtLimit[list[j]] = new KeyValuePair<int, int>(num2, num);
					}
				}
			}
			if (flag)
			{
				per.LimitRejects++;
			}
			else
			{
				per.OtherRejects++;
			}
			return false;
		}
		catch (Exception ex)
		{
			Warn("AllowTarget: " + ex.Message);
			return true;
		}
	}

	private static bool NearBuildings(GameObject tree)
	{
		float num = ((ModConfig.BaseProtectRadius != null) ? ModConfig.BaseProtectRadius.Value : 10f);
		if (num <= 0f)
		{
			return false;
		}
		int instanceID = tree.GetInstanceID();
		if (s_protect.TryGetValue(instanceID, out var value) && Time.time - value.Key < 30f)
		{
			return value.Value;
		}
		bool flag = false;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(tree.transform.position, num, s_pieces);
		for (int i = 0; i < s_pieces.Count; i++)
		{
			if (flag)
			{
				break;
			}
			Piece piece = s_pieces[i];
			if (piece != null && piece.IsPlacedByPlayer())
			{
				flag = true;
			}
		}
		if (s_protect.Count > 2000)
		{
			s_protect.Clear();
		}
		s_protect[instanceID] = new KeyValuePair<float, bool>(Time.time, flag);
		return flag;
	}

	public static bool LooseItem(int mode, string item)
	{
		if (mode != 1 || !(item == "Wood"))
		{
			if (mode == 2)
			{
				if (!(item == "Stone"))
				{
					return item == "Flint";
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public static bool AllowLoose(HarvestController h, Pickable pick, int mode)
	{
		try
		{
			if (pick == null || pick.m_itemPrefab == null)
			{
				return false;
			}
			string name = pick.m_itemPrefab.name;
			if (!LooseItem(mode, name))
			{
				return false;
			}
			MHR_List mHR_List = Parse(mode);
			if (mHR_List == null)
			{
				return true;
			}
			if (!mHR_List.Allows(name, ItemLocal(name)))
			{
				return false;
			}
			int num = mHR_List.AmountFor(name, DefaultLimit(mode));
			return num <= 0 || Stock(h, name) < num;
		}
		catch (Exception ex)
		{
			Warn("AllowLoose: " + ex.Message);
			return false;
		}
	}

	public static bool WantDrop(HarvestController h, ItemDrop drop)
	{
		try
		{
			if (drop == null || drop.m_itemData == null)
			{
				return true;
			}
			if (!drop.m_autoPickup)
			{
				return false;
			}
			int num = Mode(h);
			if (num < 1 || num > 3)
			{
				return true;
			}
			MHR_List mHR_List = Parse(num);
			if (mHR_List == null)
			{
				return true;
			}
			string text = ((drop.m_itemData.m_dropPrefab != null) ? drop.m_itemData.m_dropPrefab.name : drop.gameObject.name.Replace("(Clone)", ""));
			if (num == 3 && text == "Stone")
			{
				return true;
			}
			return mHR_List.Allows(text, MHR_List.Local(drop.m_itemData.m_shared.m_name));
		}
		catch
		{
			return true;
		}
	}

	public static bool TryMakeRoom(HarvestController h, ItemDrop.ItemData incoming)
	{
		try
		{
			Humanoid component = h.GetComponent<Humanoid>();
			if (component == null || incoming == null)
			{
				return false;
			}
			Inventory inventory = component.GetInventory();
			if (inventory == null)
			{
				return false;
			}
			CompanionSetup component2 = h.GetComponent<CompanionSetup>();
			Per per = P(h);
			if (Mode(h) == 3 && StartPile(h))
			{
				return false;
			}
			if (h.FindNearestChest((component2 != null && component2.HasHomePosition()) ? component2.GetHomePosition() : h.transform.position) != null)
			{
				if (!per.PackFull && CompanionsPlugin.LogNormal)
				{
					Info("Pack is full - taking a load to the chest");
				}
				per.PackFull = true;
				return false;
			}
			if (ModConfig.JunkCleanup == null || !ModConfig.JunkCleanup.Value)
			{
				return false;
			}
			List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory.GetAllItems());
			int num = 0;
			string text = "";
			for (int i = 0; i < list.Count; i++)
			{
				if (inventory.CanAddItem(incoming, incoming.m_stack))
				{
					break;
				}
				ItemDrop.ItemData itemData = list[i];
				if (IsJunk(component, itemData))
				{
					string text2 = ((itemData.m_dropPrefab != null) ? itemData.m_dropPrefab.name : itemData.m_shared.m_name);
					Vector3 position = component.transform.position + component.transform.forward * 0.8f + Vector3.up * 0.5f;
					ItemDrop itemDrop = ItemDrop.DropItem(itemData, itemData.m_stack, position, Quaternion.identity);
					if (!(itemDrop == null))
					{
						itemDrop.m_autoPickup = false;
						inventory.RemoveItem(itemData);
						num++;
						text = text + ((text.Length > 0) ? ", " : "") + text2;
					}
				}
			}
			if (num > 0 && CompanionsPlugin.LogNormal)
			{
				Info("Dropped " + num + " junk item(s) to make room: " + text);
			}
			return inventory.CanAddItem(incoming, incoming.m_stack);
		}
		catch (Exception ex)
		{
			Warn("TryMakeRoom: " + ex.Message);
			return false;
		}
	}

	private static bool IsJunk(Humanoid hum, ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null || it.m_equipped || hum.IsItemEquiped(it))
		{
			return false;
		}
		ItemDrop.ItemData.ItemType itemType = it.m_shared.m_itemType;
		if (itemType != ItemDrop.ItemData.ItemType.Material && itemType != ItemDrop.ItemData.ItemType.Trophy && itemType != ItemDrop.ItemData.ItemType.Misc)
		{
			return false;
		}
		if (it.m_shared.m_food > 0f || it.m_shared.m_foodStamina > 0f)
		{
			return false;
		}
		string text = ((it.m_dropPrefab != null) ? it.m_dropPrefab.name : "");
		if (text.Length == 0)
		{
			return false;
		}
		if (OnAnyList(text))
		{
			return false;
		}
		if (FarmController.s_seedToPlant != null && FarmController.s_seedToPlant.ContainsKey(text))
		{
			return false;
		}
		string text2 = ((ModConfig.NeverDiscard != null) ? ModConfig.NeverDiscard.Value : "");
		if (!string.IsNullOrEmpty(text2))
		{
			string[] array = text2.Split(new char[1] { ',' });
			for (int i = 0; i < array.Length; i++)
			{
				string text3 = array[i].Trim();
				if (text3.Length > 0 && (text.IndexOf(text3, StringComparison.OrdinalIgnoreCase) >= 0 || (it.m_shared.m_name != null && it.m_shared.m_name.IndexOf(text3, StringComparison.OrdinalIgnoreCase) >= 0)))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static bool PackFull(HarvestController h)
	{
		if (!s_per.TryGetValue(h.GetInstanceID(), out var value) || !value.PackFull)
		{
			return false;
		}
		Humanoid component = h.GetComponent<Humanoid>();
		if (component == null || component.GetInventory() == null || component.GetInventory().HaveEmptySlot())
		{
			value.PackFull = false;
			return false;
		}
		return true;
	}

	public static void OnTargetDestroyed(HarvestController h, string targetType)
	{
		try
		{
			if (targetType != "TreeBase")
			{
				return;
			}
			Transform transform = h.transform;
			Vector3 forward = transform.forward;
			forward.y = 0f;
			if (!(forward.sqrMagnitude < 0.01f))
			{
				forward.Normalize();
				Vector3 vector = new Vector3(0f - forward.z, 0f, forward.x);
				Vector3 vector2 = transform.position + vector * 4f;
				Vector3 vector3 = transform.position - vector * 4f;
				float num = Ground(vector2);
				float num2 = Ground(vector3);
				Vector3 dodgePoint = ((num >= num2) ? vector2 : vector3);
				Per per = P(h);
				per.DodgePoint = dodgePoint;
				per.DodgeUntil = Time.time + 2.5f;
				if (CompanionsPlugin.LogDetailed)
				{
					Debug("Stepping aside from the falling " + ((targetType == "TreeBase") ? "tree" : "log"));
				}
			}
		}
		catch
		{
		}
	}

	public static bool TickDodge(HarvestController h)
	{
		if (!s_per.TryGetValue(h.GetInstanceID(), out var value))
		{
			return false;
		}
		if (value.PileActive)
		{
			CompanionAI component = h.GetComponent<CompanionAI>();
			if (component == null || component.IsInCombat)
			{
				return false;
			}
			if (new Vector2(h.transform.position.x - value.PilePoint.x, h.transform.position.z - value.PilePoint.z).magnitude <= 1.5f || Time.time >= value.PileUntil)
			{
				component.StopMoving();
				DropStone(h, value);
				value.PileActive = false;
				h.ResetToIdle();
				return true;
			}
			component.MHR_Go(Time.deltaTime, value.PilePoint, 1f, run: false, "Pile");
			return true;
		}
		if (Time.time >= value.DodgeUntil)
		{
			return false;
		}
		CompanionAI component2 = h.GetComponent<CompanionAI>();
		if (component2 == null || component2.IsInCombat)
		{
			value.DodgeUntil = 0f;
			return false;
		}
		if (Vector3.Distance(h.transform.position, value.DodgePoint) > 0.6f)
		{
			component2.MHR_Go(Time.deltaTime, value.DodgePoint, 0.4f, run: true, "Dodge");
		}
		else
		{
			component2.StopMoving();
		}
		return true;
	}

	public static void HandleOverweight(HarvestController h)
	{
		try
		{
			Per per = P(h);
			float time = Time.time;
			if (time - per.LastHeavyCall > 1.5f)
			{
				per.TooHeavy = false;
				per.NextDropOffCheck = 0f;
			}
			per.LastHeavyCall = time;
			if (per.PileActive || (Mode(h) == 3 && StartPile(h)))
			{
				return;
			}
			CompanionAI component = h.GetComponent<CompanionAI>();
			Humanoid component2 = h.GetComponent<Humanoid>();
			if (time >= per.NextDropOffCheck)
			{
				per.NextDropOffCheck = time + 5f;
				CompanionSetup component3 = h.GetComponent<CompanionSetup>();
				Container container = h.FindNearestChest((component3 != null && component3.HasHomePosition()) ? component3.GetHomePosition() : h.transform.position);
				if (container != null && component != null && component2 != null)
				{
					if (CompanionsPlugin.LogNormal)
					{
						Info("Too heavy - taking a load to \"" + container.m_name + "\" " + Vector3.Distance(h.transform.position, container.transform.position).ToString("F0") + "m away");
					}
					h.ResetToIdle();
					h.RestoreLoadout();
					component.SetPendingDeposit(container, component2);
					per.TooHeavy = false;
					per.PackFull = false;
					return;
				}
			}
			if (!per.TooHeavy)
			{
				per.TooHeavy = true;
				if (CompanionsPlugin.LogNormal)
				{
					Info("Too heavy and nowhere to put things - pausing the job until there's room");
				}
				h.ResetToIdle();
				h.RestoreLoadout();
			}
			if (time >= per.NextHeavySpeak)
			{
				per.NextHeavySpeak = time + 60f;
				CompanionTalk component4 = h.GetComponent<CompanionTalk>();
				if (component4 != null)
				{
					component4.SayProblem("overweight", ModLocalization.Loc("hc_speech_overweight"));
				}
			}
		}
		catch (Exception ex)
		{
			Warn("HandleOverweight: " + ex.Message);
		}
	}

	public static Container DropOff(HarvestController h, Vector3 searchCenter)
	{
		try
		{
			CompanionSetup component = h.GetComponent<CompanionSetup>();
			Vector3 position = h.transform.position;
			bool flag = component != null && component.HasHomePosition() && MHR_ToolKeeper.NearHome(component);
			List<Container> list = (flag ? MHR_Chests.Near(component.GetHomePosition(), 50f) : MHR_Chests.Near(position, 30f));
			Container result = null;
			float num = float.MaxValue;
			for (int i = 0; i < list.Count; i++)
			{
				Container container = list[i];
				if (container == null || container.GetInventory() == null || !container.GetInventory().HaveEmptySlot() || container.GetComponent<TombStone>() != null || container.GetComponentInParent<TombStone>() != null || container.GetComponentInParent<Character>() != null)
				{
					continue;
				}
				Piece componentInParent = container.GetComponentInParent<Piece>();
				if (componentInParent == null || !componentInParent.IsPlacedByPlayer())
				{
					continue;
				}
				bool flag2 = container.GetComponentInParent<Vagon>() != null || container.GetComponentInParent<Ship>() != null;
				if ((flag2 & flag) || MHR_Chests.Busy(container) || !PrivateArea.CheckAccess(container.transform.position, 0f, flash: false))
				{
					continue;
				}
				ZNetView component2 = container.GetComponent<ZNetView>();
				if (component2 == null || !component2.IsValid())
				{
					continue;
				}
				if (container.m_privacy == Container.PrivacySetting.Private)
				{
					Piece component3 = container.GetComponent<Piece>();
					if (component3 == null || component3.GetCreator() != MHR_Owner.CreatorId(h))
					{
						continue;
					}
				}
				float num2 = Vector3.Distance(position, container.transform.position);
				if (!(num2 >= num) && (flag2 || MHR_Chests.CanReach(position, container)))
				{
					num = num2;
					result = container;
				}
			}
			return result;
		}
		catch (Exception ex)
		{
			Warn("DropOff: " + ex.Message);
			return null;
		}
	}

	public static GameObject NearestLog(HarvestController h, Vector3 center)
	{
		try
		{
			int mode = Mode(h);
			GameObject result = null;
			float num = float.MaxValue;
			Collider[] array = Physics.OverlapSphere(center, 12f);
			HashSet<int> hashSet = new HashSet<int>();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == null)
				{
					continue;
				}
				TreeLog componentInParent = array[i].GetComponentInParent<TreeLog>();
				if (componentInParent == null || !hashSet.Add(componentInParent.GetInstanceID()))
				{
					continue;
				}
				GameObject gameObject = componentInParent.gameObject;
				if ((h._blacklist == null || !h._blacklist.ContainsKey(gameObject.GetInstanceID())) && AllowTarget(h, gameObject, mode))
				{
					float num2 = Vector3.Distance(center, gameObject.transform.position);
					if (num2 < num)
					{
						num = num2;
						result = gameObject;
					}
				}
			}
			return result;
		}
		catch
		{
			return null;
		}
	}

	public static Vector3 DropCenter(HarvestController h)
	{
		switch (h._targetType)
		{
		case "MineRock":
		case "MineRock5":
		case "Destructible":
			if (h._attackNodePos != Vector3.zero)
			{
				return h._attackNodePos;
			}
			break;
		}
		return h.transform.position;
	}

	public static void BlacklistTarget(HarvestController h)
	{
		try
		{
			if (h._target != null && h._blacklist != null)
			{
				h._blacklist[h._target.GetInstanceID()] = Time.time;
			}
		}
		catch
		{
		}
	}

	public static bool StartPile(HarvestController h)
	{
		Per per = P(h);
		if (per.PileActive)
		{
			return true;
		}
		Humanoid component = h.GetComponent<Humanoid>();
		if (component == null || component.GetInventory() == null || CountIn(component.GetInventory(), "Stone") <= 0)
		{
			return false;
		}
		Vector3 position = h.transform.position;
		if (!per.PileHas || Vector3.Distance(position, per.PileSpot) > 25f)
		{
			Vector3 vector = ((h._attackNodePos != Vector3.zero) ? h._attackNodePos : position);
			Vector3 vector2 = position - vector;
			vector2.y = 0f;
			if (vector2.sqrMagnitude < 0.25f)
			{
				vector2 = h.transform.right;
			}
			vector2.Normalize();
			vector2 = Quaternion.Euler(0f, 50f, 0f) * vector2;
			Vector3 vector3 = vector + vector2 * 6f;
			vector3.y = Ground(vector3);
			per.PileSpot = vector3;
			per.PileHas = true;
		}
		per.PilePoint = per.PileSpot;
		per.PileActive = true;
		per.PileUntil = Time.time + 25f;
		if (CompanionsPlugin.LogNormal)
		{
			Info("Taking the stone to a pile out of the way");
		}
		return true;
	}

	private static void DropStone(HarvestController h, Per p)
	{
		Humanoid component = h.GetComponent<Humanoid>();
		if (component == null || component.GetInventory() == null)
		{
			return;
		}
		Inventory inventory = component.GetInventory();
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory.GetAllItems());
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			ItemDrop.ItemData itemData = list[i];
			if (itemData != null && !(itemData.m_dropPrefab == null) && !(itemData.m_dropPrefab.name != "Stone") && !itemData.m_equipped)
			{
				Vector3 position = p.PilePoint + Vector3.up * 0.4f + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0f, UnityEngine.Random.Range(-0.3f, 0.3f));
				int stack = itemData.m_stack;
				ItemDrop itemDrop = ItemDrop.DropItem(itemData, stack, position, Quaternion.identity);
				if (!(itemDrop == null))
				{
					itemDrop.m_autoPickup = false;
					inventory.RemoveItem(itemData);
					num += stack;
				}
			}
		}
		P(h).PackFull = false;
		if (num > 0 && CompanionsPlugin.LogNormal)
		{
			Info("Piled " + num + " stone");
		}
	}

	public static Vector3 ApproachPoint(HarvestController h, Vector3 point)
	{
		try
		{
			GameObject target = h._target;
			if (target == null || (target.GetComponent<TreeBase>() == null && target.GetComponent<TreeLog>() == null))
			{
				return point;
			}
			Per per = P(h);
			int instanceID = target.GetInstanceID();
			if (per.ApproachFor != instanceID)
			{
				per.ApproachFor = instanceID;
				per.ApproachHas = false;
				float num = Ground(point);
				float num2 = num;
				Vector3 vector = Vector3.zero;
				for (int i = 0; i < 8; i++)
				{
					float f = (float)i * Mathf.PI / 4f;
					Vector3 vector2 = new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f));
					float num3 = Ground(point + vector2 * 2.5f);
					if (num3 > num2)
					{
						num2 = num3;
						vector = vector2;
					}
				}
				if (num2 - num > 0.4f)
				{
					per.ApproachHas = true;
					per.ApproachOffset = vector * 0.8f;
				}
			}
			return per.ApproachHas ? (point + per.ApproachOffset) : point;
		}
		catch
		{
			return point;
		}
	}

	private static float Ground(Vector3 pos)
	{
		if (!(ZoneSystem.instance != null))
		{
			return pos.y;
		}
		return ZoneSystem.instance.GetGroundHeight(pos);
	}

	private static int Mode(Component c)
	{
		ZNetView component = c.GetComponent<ZNetView>();
		if (!(component != null) || !component.IsValid() || component.GetZDO() == null)
		{
			return 0;
		}
		return component.GetZDO().GetInt(CompanionSetup.ActionModeHash);
	}

	private static void Info(string m)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Gather] " + m);
		}
	}

	private static void Debug(string m)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Gather] " + m);
		}
	}

	private static void Warn(string m)
	{
		if (CompanionsPlugin.Log != null)
		{
			CompanionsPlugin.Log.LogWarning("[Gather] " + m);
		}
	}
}
