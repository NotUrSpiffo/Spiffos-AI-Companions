using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

internal static class MHR_SpearThrow
{
	private class State
	{
		public ItemDrop.ItemData Thrown;

		public string Name;

		public float ThrownAt;

		public ItemDrop Drop;

		public float NextTry;

		public float NextWhyLog;
	}

	private const float MinThrowDistance = 3f;

	private const float LandWaitSeconds = 15f;

	private const float GiveUpSeconds = 120f;

	private static readonly Dictionary<CompanionAI, State> s_states = new Dictionary<CompanionAI, State>();

	private static readonly Dictionary<ItemDrop.ItemData, State> s_inFlight = new Dictionary<ItemDrop.ItemData, State>();

	private static State Get(CompanionAI ai)
	{
		if (!s_states.TryGetValue(ai, out var value))
		{
			value = new State();
			s_states[ai] = value;
		}
		return value;
	}

	internal static bool IsThrowable(ItemDrop.ItemData w)
	{
		if (w == null || !w.HaveSecondaryAttack())
		{
			return false;
		}
		Attack secondaryAttack = w.m_shared.m_secondaryAttack;
		if (secondaryAttack != null && secondaryAttack.m_attackType == Attack.AttackType.Projectile)
		{
			return secondaryAttack.m_consumeItem;
		}
		return false;
	}

	private static bool Skittish(Character c)
	{
		BaseAI baseAI = c?.GetBaseAI();
		if (!(baseAI is AnimalAI))
		{
			if (baseAI is MonsterAI monsterAI)
			{
				return monsterAI.m_fleeIfNotAlerted;
			}
			return false;
		}
		return true;
	}

	private static bool Noticed(Character c)
	{
		BaseAI baseAI = c?.GetBaseAI();
		if (baseAI != null)
		{
			if (!baseAI.IsAlerted())
			{
				return baseAI.GetTargetCreature() != null;
			}
			return true;
		}
		return false;
	}

	private static bool MayThrow(Humanoid hum, ItemDrop.ItemData spear, Character prey)
	{
		if (!HasOtherWeapon(hum, spear))
		{
			return Noticed(prey);
		}
		return true;
	}

	internal static bool UnarmedAfterThrow(CompanionAI ai, Humanoid hum)
	{
		if (ai != null && hum != null && s_states.TryGetValue(ai, out var value) && value.Thrown != null)
		{
			return !HasOtherWeapon(hum, null);
		}
		return false;
	}

	private static bool HasOtherWeapon(Humanoid hum, ItemDrop.ItemData spear)
	{
		Inventory inventory = hum.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem != spear && allItem?.m_shared != null)
			{
				ItemDrop.ItemData.ItemType itemType = allItem.m_shared.m_itemType;
				if ((itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || itemType == ItemDrop.ItemData.ItemType.Bow) && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)) && !(allItem.m_shared.m_damages.m_pickaxe > 0f) && allItem.m_shared.m_skillType != Skills.SkillType.Fishing)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static bool ReadyToThrow(CompanionAI ai, Humanoid hum, Character prey)
	{
		if (!(ai == null) && !(hum == null) && !(prey == null))
		{
			ConfigEntry<bool> huntThrowSpear = ModConfig.HuntThrowSpear;
			if ((huntThrowSpear == null || huntThrowSpear.Value) && Skittish(prey))
			{
				if (s_states.TryGetValue(ai, out var value) && value.Thrown != null)
				{
					return false;
				}
				ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
				if (IsThrowable(currentWeapon))
				{
					return MayThrow(hum, currentWeapon, prey);
				}
				return false;
			}
		}
		return false;
	}

	internal static float Standoff()
	{
		float num = ModConfig.HuntThrowRange?.Value ?? 12f;
		return Mathf.Clamp(ModConfig.HuntThrowStandoff?.Value ?? 10f, 3.5f, Mathf.Max(3.5f, num - 1f));
	}

	private static void Why(State st, string text)
	{
		if (Time.time >= st.NextWhyLog)
		{
			st.NextWhyLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Hunt] Not throwing yet: " + text);
			}
		}
	}

	internal static bool WantsThrow(CompanionAI ai, Humanoid hum, Character prey, float dist)
	{
		if (!(ai == null) && !(hum == null) && !(prey == null))
		{
			ConfigEntry<bool> huntThrowSpear = ModConfig.HuntThrowSpear;
			if ((huntThrowSpear == null || huntThrowSpear.Value) && !hum.InAttack())
			{
				State state = Get(ai);
				if (state.Thrown != null || Time.time < state.NextTry)
				{
					return false;
				}
				if (dist < 3f || dist > (ModConfig.HuntThrowRange?.Value ?? 12f))
				{
					return false;
				}
				ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
				if (!IsThrowable(currentWeapon) || !Skittish(prey) || !MayThrow(hum, currentWeapon, prey))
				{
					return false;
				}
				float attackStamina = currentWeapon.m_shared.m_secondaryAttack.m_attackStamina;
				if (attackStamina > 0f && !hum.HaveStamina(attackStamina + 0.1f))
				{
					CompanionStamina component = hum.GetComponent<CompanionStamina>();
					Why(state, $"not enough stamina for a throw ({((component != null) ? component.Stamina : 0f):F0}, needs {attackStamina:F0})");
					return false;
				}
				return true;
			}
		}
		return false;
	}

	internal static bool EquipForHunt(CompanionAI ai, Humanoid hum, Character prey)
	{
		if (!(ai == null) && !(hum == null) && !(prey == null))
		{
			ConfigEntry<bool> huntThrowSpear = ModConfig.HuntThrowSpear;
			if ((huntThrowSpear == null || huntThrowSpear.Value) && Skittish(prey))
			{
				if (s_states.TryGetValue(ai, out var value) && value.Thrown != null)
				{
					return false;
				}
				ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
				if (IsThrowable(currentWeapon) && MayThrow(hum, currentWeapon, prey))
				{
					return true;
				}
				Inventory inventory = hum.GetInventory();
				if (inventory == null)
				{
					return false;
				}
				ItemDrop.ItemData itemData = null;
				foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
				{
					if (IsThrowable(allItem) && (!allItem.m_shared.m_useDurability || allItem.m_durability > 0f) && MayThrow(hum, allItem, prey) && (itemData == null || allItem.GetDamage().GetTotalDamage() > itemData.GetDamage().GetTotalDamage()))
					{
						itemData = allItem;
					}
				}
				if (itemData == null || !hum.EquipItem(itemData))
				{
					return false;
				}
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Hunt] Took out its " + Loc(itemData.m_shared.m_name) + " to throw at " + Loc(prey.m_name));
				}
				return true;
			}
		}
		return false;
	}

	internal static void Throw(CompanionAI ai, Humanoid hum, Character prey)
	{
		State state = Get(ai);
		ItemDrop.ItemData currentWeapon = hum.GetCurrentWeapon();
		if (currentWeapon == null)
		{
			return;
		}
		state.NextTry = Time.time + 2f;
		if (hum.StartAttack(prey, true))
		{
			state.Thrown = currentWeapon;
			state.Name = currentWeapon.m_shared.m_name;
			state.ThrownAt = Time.time;
			state.Drop = null;
			s_inFlight[currentWeapon] = state;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] Threw its " + Loc(state.Name) + " at " + Loc(prey.m_name) + $" ({Vector3.Distance(ai.transform.position, prey.transform.position):F0} m)");
			}
			ai.GetComponent<CompanionCombatAI>()?.NoteSwing(prey, "Throw", $"{Vector3.Distance(ai.transform.position, prey.transform.position):F1} m, {prey.transform.position.y - ai.transform.position.y:+0.0;-0.0} m up, prey moving {prey.GetVelocity().magnitude:F1} m/s", 4f);
		}
	}

	internal static void OnDropped(ItemDrop.ItemData item, ItemDrop drop)
	{
		if (item != null && !(drop == null) && s_inFlight.TryGetValue(item, out var value))
		{
			s_inFlight.Remove(item);
			value.Drop = drop;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Hunt] The thrown " + Loc(value.Name) + $" landed at {drop.transform.position:F1}");
			}
		}
	}

	private static void Clear(State st)
	{
		if (st.Thrown != null)
		{
			s_inFlight.Remove(st.Thrown);
		}
		st.Thrown = null;
		st.Drop = null;
	}

	internal static bool Drive(CompanionAI ai, float dt, bool inCombat)
	{
		if (ai == null || !s_states.TryGetValue(ai, out var value) || value.Thrown == null)
		{
			return false;
		}
		Humanoid component = ai.GetComponent<Humanoid>();
		Inventory inventory = component?.GetInventory();
		if (inventory == null)
		{
			Clear(value);
			return false;
		}
		if (value.Drop == null && inventory.ContainsItem(value.Thrown) && Time.time - value.ThrownAt > 3f)
		{
			Clear(value);
			return false;
		}
		if (inCombat)
		{
			return false;
		}
		if (Time.time - value.ThrownAt > 120f)
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] Gave up on getting its " + Loc(value.Name) + " back (took too long)");
			}
			ai.GetComponent<CompanionTalk>()?.SayProblem("spear:lost", ModLocalization.LocFmt("hc_why_spear_lost", Loc(value.Name)));
			Clear(value);
			return false;
		}
		if ((object)value.Drop == null)
		{
			if (Time.time - value.ThrownAt > 15f)
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Hunt] Lost its " + Loc(value.Name) + " - it never landed anywhere it could find (water?)");
				}
				ai.GetComponent<CompanionTalk>()?.SayProblem("spear:lost", ModLocalization.LocFmt("hc_why_spear_lost", Loc(value.Name)));
				Clear(value);
			}
			return false;
		}
		if (value.Drop == null)
		{
			bool flag = false;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem?.m_shared != null && allItem.m_shared.m_name == value.Name)
				{
					flag = true;
					break;
				}
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] " + (flag ? ("Picked its " + Loc(value.Name) + " back up on the way") : ("Its " + Loc(value.Name) + " isn't on the ground any more (someone else picked it up)")));
			}
			Clear(value);
			return false;
		}
		Vector3 position = value.Drop.transform.position;
		Vector3 position2 = ai.transform.position;
		if (new Vector2(position2.x - position.x, position2.z - position.z).magnitude <= 1.6f && Mathf.Abs(position2.y - position.y) <= 2f)
		{
			if (!value.Drop.CanPickup())
			{
				value.Drop.RequestOwn();
				ai.StopMoving();
				return true;
			}
			value.Drop.Load();
			bool flag2 = component.Pickup(value.Drop.gameObject, autoequip: false, autoPickupDelay: false);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] " + (flag2 ? "Picked its " : "Couldn't pick up its ") + Loc(value.Name) + (flag2 ? " back up" : " (pack full?)"));
			}
			MHR_Mover.Release(ai);
			Clear(value);
			return false;
		}
		MHR_Mover.Go(ai, dt, position, 0.8f, allowRun: true, "Spear");
		if (MHR_Mover.RouteSwim(ai) > MHR_Mover.JobSwim() || MHR_Mover.GaveUp(ai))
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Hunt] Can't get to its " + Loc(value.Name) + " - leaving it where it landed");
			}
			MHR_Mover.Release(ai);
			Clear(value);
			return false;
		}
		return true;
	}

	private static string Loc(string s)
	{
		if (Localization.instance == null || string.IsNullOrEmpty(s))
		{
			return s ?? "";
		}
		return Localization.instance.Localize(s);
	}
}
