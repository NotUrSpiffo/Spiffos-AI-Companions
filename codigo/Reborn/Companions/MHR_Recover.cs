using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class MHR_Recover
{
	private class State
	{
		public float NextCheck;

		public float NoTombSince = -1f;

		public float GearSince = -1f;

		public bool WaitSaid;

		public string Status;
	}

	internal static readonly int RecoveringHash = "HC_Recovering".GetStableHashCode();

	internal static readonly int RecoverStanceHash = "HC_RecoverStance".GetStableHashCode();

	internal static readonly int RecoverPickupHash = "HC_RecoverPickup".GetStableHashCode();

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	private static readonly Dictionary<int, KeyValuePair<float, TombStone>> s_tombCache = new Dictionary<int, KeyValuePair<float, TombStone>>();

	internal static string Mode => ModConfig.RespawnStance?.Value ?? "RecoverFirst";

	internal static void OnRevived(ZDO zdo, int diedStance, bool autoPickup)
	{
		if (zdo == null)
		{
			return;
		}
		string mode = Mode;
		if (mode == "Keep")
		{
			return;
		}
		zdo.Set(CompanionSetup.CombatStanceHash, 1);
		if (!(mode != "RecoverFirst"))
		{
			zdo.Set(RecoveringHash, value: true);
			zdo.Set(RecoverStanceHash, diedStance);
			zdo.Set(RecoverPickupHash, autoPickup);
			zdo.Set(CompanionSetup.AutoPickupHash, value: false);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Recover] Revived Passive with auto-pickup off - getting its gear back before going back to stance " + diedStance);
			}
		}
	}

	internal static bool IsRecovering(Component c)
	{
		return (c?.GetComponent<ZNetView>()?.GetZDO())?.GetBool(RecoveringHash) ?? false;
	}

	internal static string StatusText(Component c)
	{
		if (c == null || !IsRecovering(c) || !s_state.TryGetValue(c.gameObject.GetInstanceID(), out var value) || value.Status == null)
		{
			return null;
		}
		return ModLocalization.Loc(value.Status);
	}

	internal static void Cancel(CompanionSetup cs)
	{
		ZDO zDO = cs?.GetComponent<ZNetView>()?.GetZDO();
		if (zDO != null && zDO.GetBool(RecoveringHash))
		{
			zDO.Set(RecoveringHash, value: false);
			zDO.Set(CompanionSetup.AutoPickupHash, zDO.GetBool(RecoverPickupHash, defaultValue: true));
			s_state.Remove(cs.gameObject.GetInstanceID());
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Recover] You set a stance - recovery over");
			}
		}
	}

	internal static bool MayDefend(Component c, Character target)
	{
		if (target == null || !IsRecovering(c))
		{
			return false;
		}
		Character component = c.GetComponent<Character>();
		Humanoid humanoid = component as Humanoid;
		if (target.GetBaseAI()?.GetTargetCreature() == component && humanoid != null && humanoid.GetCurrentWeapon() != null)
		{
			return humanoid.GetCurrentWeapon() != humanoid.m_unarmedWeapon?.m_itemData;
		}
		return false;
	}

	internal static bool MayDefendHit(Component c, Character attacker)
	{
		if (attacker == null || attacker.IsPlayer() || !IsRecovering(c))
		{
			return false;
		}
		Humanoid component = c.GetComponent<Humanoid>();
		if (component != null && component.GetCurrentWeapon() != null)
		{
			return component.GetCurrentWeapon() != component.m_unarmedWeapon?.m_itemData;
		}
		return false;
	}

	internal static bool SafeToGo(Character me, Vector3 tomb, out string why, GameObject owner = null, bool committed = false)
	{
		why = null;
		float num = ModConfig.RecoverThreatRange?.Value ?? 15f;
		int num2 = ModConfig.RecoverMaxMobs?.Value ?? 3;
		Vector3 position = me.transform.position;
		float num3 = Vector3.Distance(position, tomb);
		float num4 = ((owner != null) ? Vector3.Distance(position, owner.transform.position) : float.MaxValue);
		committed = committed && num3 < num4;
		int num5 = 0;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == null || allCharacter == me || allCharacter.IsDead() || allCharacter.IsPlayer() || !BaseAI.IsEnemy(me, allCharacter))
			{
				continue;
			}
			float num6 = Vector3.Distance(allCharacter.transform.position, position);
			float num7 = Vector3.Distance(allCharacter.transform.position, tomb);
			if (num6 > num && num7 > num)
			{
				continue;
			}
			BaseAI baseAI = allCharacter.GetBaseAI();
			Character character = baseAI?.GetTargetCreature();
			if (!(baseAI != null) || (!baseAI.IsAlerted() && !(character != null)))
			{
				if (num7 < 4f)
				{
					why = allCharacter.m_name + " is right by the tombstone";
					return false;
				}
			}
			else if (character == me)
			{
				if (!(num3 + 3f < num6) && (!committed || !(num7 > num3) || !(num6 > 2f)))
				{
					why = allCharacter.m_name + " is after it";
					return false;
				}
			}
			else if (!committed || !(num7 > 3f))
			{
				num5++;
				float num8 = ((character != null) ? Vector3.Distance(allCharacter.transform.position, character.transform.position) : 0f);
				if (character != null && num7 < num8 + 2f)
				{
					why = allCharacter.m_name + " is fighting by the tombstone";
					return false;
				}
			}
		}
		if (num5 > num2)
		{
			why = num5 + " enemies around";
			return false;
		}
		return true;
	}

	internal static void Tick(CompanionSetup cs)
	{
		ZDO zDO = cs?.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || !zDO.GetBool(RecoveringHash))
		{
			return;
		}
		int instanceID = cs.gameObject.GetInstanceID();
		if (!s_state.TryGetValue(instanceID, out var value))
		{
			value = new State();
			s_state[instanceID] = value;
		}
		float time = Time.time;
		if (time < value.NextCheck)
		{
			return;
		}
		value.NextCheck = time + 1f;
		Character component = cs.GetComponent<Character>();
		if (component == null || component.IsDead())
		{
			return;
		}
		if (OwnTombWithItems(cs, zDO, out var tomb))
		{
			value.NoTombSince = -1f;
			value.GearSince = -1f;
			value.Status = "hc_status_recover";
			if (!SafeToGo(component, tomb.transform.position, out var why))
			{
				value.Status = "hc_status_recover_wait";
				if (!value.WaitSaid)
				{
					value.WaitSaid = true;
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Recover] Waiting before going for its tombstone: " + why);
					}
					cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_wait"), CompanionTalk.Priority.Event, "recover:wait");
				}
			}
			else
			{
				value.WaitSaid = false;
			}
			return;
		}
		Humanoid humanoid = component as Humanoid;
		bool flag = humanoid != null && humanoid.GetCurrentWeapon() != null && humanoid.GetCurrentWeapon() != humanoid.m_unarmedWeapon?.m_itemData;
		if (zDO.GetLong(CompanionSetup.TombstoneIdHash, 0L) != 0L && !flag)
		{
			if (value.NoTombSince < 0f)
			{
				value.NoTombSince = time;
			}
			if (time - value.NoTombSince < 60f)
			{
				value.Status = "hc_status_recover_far";
				return;
			}
			End(cs, zDO, restoreStance: false);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Recover] Its tombstone is out of reach - staying Passive until you set a stance");
			}
			cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_far"), CompanionTalk.Priority.Problem, "recover:far");
			return;
		}
		value.Status = "hc_status_recover_equip";
		if (cs.IsEquipping)
		{
			value.GearSince = -1f;
		}
		else if (value.GearSince < 0f)
		{
			value.GearSince = time;
		}
		else if (!(time - value.GearSince < 1.5f))
		{
			cs.GetComponent<CompanionFood>()?.EatNow();
			End(cs, zDO, flag);
			if (flag)
			{
				cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_speech_recover_done"), CompanionTalk.Priority.Event, "recover:done");
			}
		}
	}

	private static void End(CompanionSetup cs, ZDO zdo, bool restoreStance)
	{
		zdo.Set(RecoveringHash, value: false);
		zdo.Set(CompanionSetup.AutoPickupHash, zdo.GetBool(RecoverPickupHash, defaultValue: true));
		if (restoreStance)
		{
			int value = zdo.GetInt(RecoverStanceHash);
			zdo.Set(CompanionSetup.CombatStanceHash, value);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Recover] Gear back on and fed - back to stance " + value);
			}
		}
		s_state.Remove(cs.gameObject.GetInstanceID());
	}

	internal static bool OwnTombWithItems(Component c, ZDO me, out TombStone tomb)
	{
		tomb = null;
		int instanceID = c.gameObject.GetInstanceID();
		if (s_tombCache.TryGetValue(instanceID, out var value) && Time.time - value.Key < 3f)
		{
			tomb = value.Value;
		}
		else
		{
			Character component = c.GetComponent<Character>();
			TombStone[] array = Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
			foreach (TombStone tombStone in array)
			{
				ZDO zDO = tombStone?.GetComponent<ZNetView>()?.GetZDO();
				if (zDO != null && CompanionAI.MHR_IsOwnGrave(zDO, me, component))
				{
					tomb = tombStone;
					break;
				}
			}
			s_tombCache[instanceID] = new KeyValuePair<float, TombStone>(Time.time, tomb);
		}
		if (tomb == null)
		{
			return false;
		}
		Inventory inventory = tomb.GetComponent<Container>()?.GetInventory();
		if (inventory != null)
		{
			return inventory.NrOfItems() > 0;
		}
		return false;
	}
}
