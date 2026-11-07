using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Crossbow
{
	private class S
	{
		public ItemDrop.ItemData Loaded;

		public ItemDrop.ItemData Reloading;

		public float Until;

		public string Anim;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static readonly Dictionary<int, bool> s_isComp = new Dictionary<int, bool>();

	public static bool IsCompanion(Humanoid h)
	{
		if (h == null || h is Player)
		{
			return false;
		}
		int instanceID = h.GetInstanceID();
		if (!s_isComp.TryGetValue(instanceID, out var value))
		{
			value = h.GetComponent<CompanionSetup>() != null;
			if (s_isComp.Count > 2000)
			{
				s_isComp.Clear();
			}
			s_isComp[instanceID] = value;
		}
		return value;
	}

	private static S Get(Humanoid h)
	{
		if (!s_s.TryGetValue(h.GetInstanceID(), out var value))
		{
			value = new S();
			s_s[h.GetInstanceID()] = value;
		}
		return value;
	}

	private static bool IsAmmo(ItemDrop.ItemData it)
	{
		if (it != null && it.m_shared != null)
		{
			if (it.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo)
			{
				return it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
			}
			return true;
		}
		return false;
	}

	public static bool NeedsAmmo(ItemDrop.ItemData it)
	{
		if (it != null && it.m_shared != null && !IsAmmo(it))
		{
			return !string.IsNullOrEmpty(it.m_shared.m_ammoType);
		}
		return false;
	}

	public static bool HasAmmo(Humanoid h, ItemDrop.ItemData weapon)
	{
		if (h == null || weapon == null || h.GetInventory() == null)
		{
			return false;
		}
		string ammoType = weapon.m_shared.m_ammoType;
		List<ItemDrop.ItemData> allItems = h.GetInventory().GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (IsAmmo(itemData) && itemData.m_shared.m_ammoType == ammoType && itemData.m_stack > 0)
			{
				return true;
			}
		}
		return false;
	}

	public static bool ReadyFor(ItemDrop.ItemData it, Humanoid h)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		if (it.m_shared.m_useDurability && it.m_durability <= 0f)
		{
			return false;
		}
		if (NeedsAmmo(it) && h != null && !HasAmmo(h, it))
		{
			return false;
		}
		return true;
	}

	public static bool HasReadyRanged(Humanoid h)
	{
		if (h == null || h.GetInventory() == null)
		{
			return false;
		}
		List<ItemDrop.ItemData> allItems = h.GetInventory().GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData it = allItems[i];
			if (NeedsAmmo(it) && ReadyFor(it, h))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsLoaded(Humanoid h)
	{
		if (h == null || !s_s.TryGetValue(h.GetInstanceID(), out var value))
		{
			return false;
		}
		ItemDrop.ItemData currentWeapon = h.GetCurrentWeapon();
		if (currentWeapon != null)
		{
			return value.Loaded == currentWeapon;
		}
		return false;
	}

	public static void Unload(Humanoid h)
	{
		if (h != null && s_s.TryGetValue(h.GetInstanceID(), out var value))
		{
			value.Loaded = null;
		}
	}

	public static void Tick(Humanoid h)
	{
		if (h == null)
		{
			return;
		}
		try
		{
			ItemDrop.ItemData currentWeapon = h.GetCurrentWeapon();
			S value;
			if (currentWeapon == null || currentWeapon.m_shared == null || currentWeapon.m_shared.m_attack == null || !currentWeapon.m_shared.m_attack.m_requiresReload || !h.IsItemEquiped(currentWeapon))
			{
				if (s_s.TryGetValue(h.GetInstanceID(), out value) && value.Reloading != null)
				{
					Cancel(h, value);
				}
				return;
			}
			value = Get(h);
			if (value.Loaded == currentWeapon)
			{
				return;
			}
			if (value.Reloading != null && value.Reloading != currentWeapon)
			{
				Cancel(h, value);
			}
			ZSyncAnimation component = h.GetComponent<ZSyncAnimation>();
			if (value.Reloading == currentWeapon)
			{
				if (!(Time.time < value.Until))
				{
					if (component != null && !string.IsNullOrEmpty(value.Anim))
					{
						component.SetBool(value.Anim, value: false);
						component.SetTrigger(value.Anim + "_done");
					}
					value.Loaded = currentWeapon;
					value.Reloading = null;
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Ranged] " + currentWeapon.m_shared.m_name + " loaded");
					}
				}
			}
			else
			{
				if (h.InAttack() || !HasAmmo(h, currentWeapon))
				{
					return;
				}
				Attack attack = currentWeapon.m_shared.m_attack;
				CompanionStamina component2 = h.GetComponent<CompanionStamina>();
				if (!(component2 != null) || !(attack.m_reloadStaminaDrain > 0f) || component2.UseStamina(attack.m_reloadStaminaDrain))
				{
					float num = 0f;
					try
					{
						num = h.GetSkillFactor(currentWeapon.m_shared.m_skillType);
					}
					catch
					{
						num = 0f;
					}
					float b = Mathf.Lerp(attack.m_reloadTime, attack.m_reloadTime * 0.5f, num);
					value.Reloading = currentWeapon;
					value.Until = Time.time + Mathf.Max(0.3f, b);
					value.Anim = attack.m_reloadAnimation;
					if (component != null && !string.IsNullOrEmpty(value.Anim))
					{
						component.SetBool(value.Anim, value: true);
					}
				}
			}
		}
		catch
		{
		}
	}

	private static void Cancel(Humanoid h, S s)
	{
		ZSyncAnimation component = h.GetComponent<ZSyncAnimation>();
		if (component != null && !string.IsNullOrEmpty(s.Anim))
		{
			component.SetBool(s.Anim, value: false);
		}
		s.Reloading = null;
	}
}
