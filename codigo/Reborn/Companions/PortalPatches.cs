using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch]
internal static class PortalPatches
{
	private static readonly List<ZDOID> _pendingCompanions = new List<ZDOID>();

	private static bool s_viaPortal;

	private static bool s_portalAllowAll;

	private static float s_portalAt = -10f;

	private const float ChestRange = 15f;

	[HarmonyPatch(typeof(TeleportWorld), "Teleport")]
	[HarmonyPrefix]
	private static void PortalTeleport_Prefix(TeleportWorld __instance)
	{
		s_portalAllowAll = __instance != null && __instance.m_allowAllItems;
		s_portalAt = Time.time;
	}

	[HarmonyPatch(typeof(Player), "TeleportTo")]
	[HarmonyPostfix]
	private static void TeleportTo_Postfix(Player __instance, bool __result)
	{
		if (!__result || __instance != Player.m_localPlayer)
		{
			return;
		}
		_pendingCompanions.Clear();
		s_viaPortal = Time.time - s_portalAt < 1f;
		string text = __instance.GetPlayerID().ToString();
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			ZNetView component = allCompanion.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				ZDO zDO = component.GetZDO();
				if (!(zDO.GetString(CompanionSetup.OwnerHash) != text) && zDO.GetInt(CompanionSetup.ActionModeHash) != 4 && zDO.GetBool(CompanionSetup.FollowHash, defaultValue: true) && MayGoThrough(allCompanion))
				{
					_pendingCompanions.Add(zDO.m_uid);
				}
			}
		}
		if (_pendingCompanions.Count > 0 && CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] Player teleporting — {_pendingCompanions.Count} companion(s) queued");
		}
	}

	private static bool MayGoThrough(CompanionSetup cs)
	{
		if (!s_viaPortal || cs == null || (ModConfig.PortalItems?.Value ?? "Game") == "AllowAll")
		{
			return true;
		}
		Humanoid component = cs.GetComponent<Humanoid>();
		Inventory inventory = ((component != null) ? component.GetInventory() : null);
		if (inventory == null || inventory.IsTeleportable(s_portalAllowAll))
		{
			return true;
		}
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
		bool flag = s_portalAllowAll || ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll);
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem.m_shared.m_toolTier >= 1000 || (!flag && !allItem.m_shared.m_teleportable))
			{
				list.Add(allItem);
			}
		}
		string text = cs.GetComponent<Character>()?.m_name ?? "companion";
		string text2 = ((list.Count > 0) ? MHR_List.Local(list[0].m_shared.m_name) : "that");
		bool flag2 = list.Exists((ItemDrop.ItemData i) => i.m_shared.m_toolTier >= 1000);
		if (!flag2 && list.Count > 0 && LeaveInChest(cs, component, inventory, list, out var left))
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Portal] " + text + " left " + left + " in a chest by the portal and comes through");
			}
			cs.GetComponent<CompanionTalk>()?.Say(ModLocalization.LocFmt("hc_say_portal_left", left), force: true);
			return true;
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Portal] " + text + " can't take " + text2 + " through the portal" + (flag2 ? " (a boss item)" : (" and found no chest with room within " + 15f + " m")) + " - staying here");
		}
		cs.GetComponent<CompanionTalk>()?.Say(ModLocalization.LocFmt("hc_say_portal_stay", text2), force: true);
		return false;
	}

	private static bool LeaveInChest(CompanionSetup cs, Humanoid h, Inventory inv, List<ItemDrop.ItemData> stuck, out string left)
	{
		left = null;
		List<Container> list = MHR_Chests.Near(h.transform.position, 15f).FindAll((Container c) => MHR_Chests.Usable(c, cs));
		if (list.Count == 0)
		{
			return false;
		}
		Dictionary<ItemDrop.ItemData, Container> dictionary = new Dictionary<ItemDrop.ItemData, Container>();
		Dictionary<Container, int> dictionary2 = new Dictionary<Container, int>();
		foreach (ItemDrop.ItemData item in stuck)
		{
			Container container = null;
			int num = -1;
			foreach (Container item2 in list)
			{
				Inventory inventory = item2.GetInventory();
				dictionary2.TryGetValue(item2, out var value);
				if ((inventory.CanAddItem(item, item.m_stack) && inventory.GetEmptySlots() - value >= 1) || (inventory.CanAddItem(item, item.m_stack) && inventory.CountItems(item.m_shared.m_name) > 0))
				{
					int num2 = inventory.CountItems(item.m_shared.m_name);
					if (num2 > num)
					{
						num = num2;
						container = item2;
					}
				}
			}
			if (container == null)
			{
				return false;
			}
			dictionary[item] = container;
			if (num <= 0)
			{
				dictionary2[container] = (dictionary2.TryGetValue(container, out var value2) ? value2 : 0) + 1;
			}
		}
		Dictionary<string, int> dictionary3 = new Dictionary<string, int>();
		foreach (KeyValuePair<ItemDrop.ItemData, Container> item3 in dictionary)
		{
			ItemDrop.ItemData key = item3.Key;
			Container value3 = item3.Value;
			MHR_Chests.Claim(value3);
			int stack = key.m_stack;
			if (key.m_equipped)
			{
				h.UnequipItem(key, triggerEquipEffects: false);
			}
			if (value3.GetInventory().AddItem(key))
			{
				inv.RemoveItem(key);
				string key2 = MHR_List.Local(key.m_shared.m_name);
				dictionary3[key2] = (dictionary3.TryGetValue(key2, out var value4) ? value4 : 0) + stack;
			}
		}
		List<string> list2 = new List<string>();
		foreach (KeyValuePair<string, int> item4 in dictionary3)
		{
			list2.Add(item4.Value + " " + item4.Key);
		}
		left = string.Join(", ", list2);
		if (inv.IsTeleportable(s_portalAllowAll))
		{
			return list2.Count > 0;
		}
		return false;
	}

	[HarmonyPatch(typeof(Player), "UpdateTeleport")]
	[HarmonyPrefix]
	private static void UpdateTeleport_Prefix(Player __instance, out bool __state)
	{
		__state = __instance == Player.m_localPlayer && __instance.IsTeleporting();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Player), "UpdateTeleport")]
	private static void UpdateTeleport_Postfix(Player __instance, bool __state)
	{
		if (!__state || __instance.IsTeleporting() || __instance != Player.m_localPlayer || _pendingCompanions.Count == 0)
		{
			return;
		}
		Vector3 position = __instance.transform.position;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] Teleport complete at {position:F1} — warping {_pendingCompanions.Count} companion(s)");
		}
		foreach (ZDOID pendingCompanion in _pendingCompanions)
		{
			WarpCompanion(pendingCompanion, position);
		}
		_pendingCompanions.Clear();
	}

	private static void WarpCompanion(ZDOID zdoid, Vector3 targetPos)
	{
		ZDO zDO = ZDOMan.instance.GetZDO(zdoid);
		if (zDO == null)
		{
			CompanionsPlugin.Log.LogWarning($"[Portal] Companion ZDO {zdoid} not found — skipping");
			return;
		}
		Vector3 vector = ((Player.m_localPlayer != null) ? CompanionAI.MHR_SafeSpotNear(targetPos, -Player.m_localPlayer.transform.forward) : FindNearbyPosition(targetPos, 3f));
		if (!zDO.IsOwner())
		{
			zDO.SetOwner(ZDOMan.GetSessionID());
		}
		zDO.SetPosition(vector);
		GameObject gameObject = ZNetScene.instance?.FindInstance(zdoid);
		if (gameObject != null)
		{
			CompanionRest component = gameObject.GetComponent<CompanionRest>();
			if (component != null)
			{
				component.CancelDirected();
			}
			gameObject.transform.position = vector;
			Rigidbody component2 = gameObject.GetComponent<Rigidbody>();
			if (component2 != null)
			{
				component2.position = vector;
				component2.linearVelocity = Vector3.zero;
			}
			CompanionAI component3 = gameObject.GetComponent<CompanionAI>();
			if (component3 != null)
			{
				component3.CancelMoveTarget();
				component3.CancelPendingCart();
				component3.CancelPendingShipBoard();
				component3.CancelHiveOrder();
				component3.CancelPendingDeposit();
				component3.CancelTombstoneRecovery();
				component3.SetFollowTarget(Player.m_localPlayer?.gameObject);
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Portal] Warped companion to {vector:F1} (GameObject present)");
			}
		}
		else if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Portal] Updated companion ZDO position to {vector:F1} (not loaded)");
		}
	}

	private static Vector3 FindNearbyPosition(Vector3 origin, float radius)
	{
		for (int i = 0; i < 20; i++)
		{
			Vector2 vector = Random.insideUnitCircle * radius;
			Vector3 vector2 = origin + new Vector3(vector.x, 0f, vector.y);
			if (ZoneSystem.instance != null && ZoneSystem.instance.FindFloor(vector2, out var height))
			{
				vector2.y = height;
				return vector2;
			}
		}
		return origin + Vector3.right * 2f;
	}
}
