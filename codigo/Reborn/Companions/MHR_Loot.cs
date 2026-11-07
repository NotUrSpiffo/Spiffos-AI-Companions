using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Loot
{
	private class Kill
	{
		public Vector3 Pos;

		public float Time;

		public string Name;

		public HashSet<int> Before = new HashSet<int>();

		public HashSet<int> Seen = new HashSet<int>();

		public HashSet<int> Skipped = new HashSet<int>();

		public bool SawDrop;

		public float LastDrop;

		public float NextBodyCheck;

		public bool Body;

		public float Start;

		public float ArrivedAt = -1f;

		public float LastDrive;
	}

	private class State
	{
		public Kill Current;

		public readonly List<Kill> Queue = new List<Kill>();

		public ItemDrop Target;

		public float TargetSince;
	}

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	private static readonly Collider[] s_buf = new Collider[128];

	private static int s_mask = -1;

	private const float NoSwim = 0.5f;

	private static int Mask()
	{
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("item");
		}
		return s_mask;
	}

	private static float Radius()
	{
		if (ModConfig.LootRadius == null)
		{
			return 6f;
		}
		return ModConfig.LootRadius.Value;
	}

	private static float Wait()
	{
		if (ModConfig.LootWait == null)
		{
			return 10f;
		}
		return ModConfig.LootWait.Value;
	}

	private static float MaxDist()
	{
		if (ModConfig.LootMaxDistance == null)
		{
			return 30f;
		}
		return ModConfig.LootMaxDistance.Value;
	}

	public static bool Active(Component c)
	{
		CompanionAI companionAI = ((c != null) ? c.GetComponent<CompanionAI>() : null);
		if (companionAI != null && s_state.TryGetValue(companionAI.GetInstanceID(), out var value))
		{
			return value.Current != null;
		}
		return false;
	}

	public static void OnKill(CompanionSetup cs, Character victim)
	{
		if (!(cs == null) && !(victim == null) && !victim.IsPlayer() && !(victim.GetComponent<CompanionSetup>() != null))
		{
			OnKillAt(cs, victim.transform.position, victim.m_name, null);
		}
	}

	public static void OnKillAt(CompanionSetup cs, Vector3 pos, string name, HashSet<int> before)
	{
		if (cs == null || (ModConfig.LootKills != null && !ModConfig.LootKills.Value))
		{
			return;
		}
		CompanionAI component = cs.GetComponent<CompanionAI>();
		if (component == null)
		{
			return;
		}
		int instanceID = component.GetInstanceID();
		if (!s_state.TryGetValue(instanceID, out var value))
		{
			value = new State();
			s_state[instanceID] = value;
		}
		if (value.Current != null && Vector3.Distance(value.Current.Pos, pos) < 1f)
		{
			return;
		}
		for (int i = 0; i < value.Queue.Count; i++)
		{
			if (Vector3.Distance(value.Queue[i].Pos, pos) < 1f)
			{
				return;
			}
		}
		Kill kill = new Kill
		{
			Pos = pos,
			Time = Time.time,
			Name = name
		};
		if (before != null)
		{
			kill.Before.UnionWith(before);
		}
		else
		{
			kill.Before.UnionWith(ItemsAround(pos));
		}
		if (value.Queue.Count > 6)
		{
			value.Queue.RemoveAt(0);
		}
		value.Queue.Add(kill);
	}

	public static bool Drive(CompanionAI ai, float dt)
	{
		if (ai == null)
		{
			return false;
		}
		if (!s_state.TryGetValue(ai.GetInstanceID(), out var value))
		{
			return false;
		}
		if (value.Current == null)
		{
			if (value.Queue.Count == 0)
			{
				return false;
			}
			value.Current = value.Queue[0];
			value.Queue.RemoveAt(0);
			value.Target = null;
			value.Current.Start = Time.time;
			value.Current.LastDrive = Time.time;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Going to pick up what " + Loc(value.Current.Name) + " dropped (" + Vector3.Distance(ai.transform.position, value.Current.Pos).ToString("F0") + " m away)");
			}
		}
		Kill current = value.Current;
		if (Time.time - current.LastDrive > 0.5f)
		{
			float num = Time.time - current.LastDrive;
			current.Start += num;
			if (current.ArrivedAt >= 0f)
			{
				current.ArrivedAt += num;
			}
			current.LastDrive = Time.time;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("back to what " + Loc(current.Name) + " dropped");
			}
		}
		current.LastDrive = Time.time;
		CompanionSetup component = ai.GetComponent<CompanionSetup>();
		Humanoid component2 = ai.GetComponent<Humanoid>();
		Inventory inventory = ((component2 != null) ? component2.GetInventory() : null);
		ZNetView component3 = ai.GetComponent<ZNetView>();
		ZDO zDO = ((component3 != null) ? component3.GetZDO() : null);
		string text = null;
		Vector3 position = ai.transform.position;
		if (inventory == null || zDO == null)
		{
			text = "no pack";
		}
		else if (!zDO.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true))
		{
			text = "auto pickup is off";
		}
		else if (Vector3.Distance(position, current.Pos) > MaxDist() + 5f)
		{
			text = "too far away";
		}
		else if (MHR_Owner.Of(component) != null && component != null && component.GetFollow() && ai.GetFollowTarget() == MHR_Owner.Go(component) && Vector3.Distance(MHR_Owner.Of(component).transform.position, current.Pos) > MaxDist())
		{
			text = "you've moved on";
		}
		else if (inventory.GetTotalWeight() >= CompanionTierData.CarryLimit(ai) || (inventory.GetEmptySlots() == 0 && !RoomForAny(inventory, current)))
		{
			text = "pack is full";
		}
		else if (Time.time - current.Start > Wait() + 25f)
		{
			text = "took too long";
		}
		if (text != null)
		{
			Finish(ai, value, "stopped picking up (" + text + ")");
			return false;
		}
		if (Time.time >= current.NextBodyCheck)
		{
			current.NextBodyCheck = Time.time + 1f;
			current.Body = false;
			try
			{
				Ragdoll[] array = Object.FindObjectsByType<Ragdoll>(FindObjectsSortMode.None);
				foreach (Ragdoll ragdoll in array)
				{
					if (!(ragdoll == null) && ragdoll.m_dropItems)
					{
						Vector3 averageBodyPosition = ragdoll.GetAverageBodyPosition();
						if (Vector3.Distance(averageBodyPosition, current.Pos) < Radius() + 3f)
						{
							current.Body = true;
							current.Pos = averageBodyPosition;
							break;
						}
					}
				}
			}
			catch
			{
			}
		}
		if (value.Target != null)
		{
			ZNetView component4 = value.Target.GetComponent<ZNetView>();
			if (component4 == null || !component4.IsValid() || Time.time - value.TargetSince > 12f)
			{
				value.Target = null;
			}
		}
		if (value.Target == null)
		{
			value.Target = NextDrop(current, position, inventory, ai.GetComponent<Character>());
			value.TargetSince = Time.time;
			if (value.Target != null)
			{
				current.SawDrop = true;
				current.LastDrop = Time.time;
			}
		}
		if (value.Target != null)
		{
			Vector3 position2 = value.Target.transform.position;
			if (new Vector2(position.x - position2.x, position.z - position2.z).magnitude <= 1.6f && Mathf.Abs(position.y - position2.y) <= 2f)
			{
				string s = ((value.Target.m_itemData != null) ? value.Target.m_itemData.m_shared.m_name : "?");
				int num2 = ((value.Target.m_itemData != null) ? value.Target.m_itemData.m_stack : 0);
				if (!value.Target.CanPickup())
				{
					value.Target.RequestOwn();
					ai.StopMoving();
					return true;
				}
				value.Target.Load();
				if (component2.Pickup(value.Target.gameObject, autoequip: false, autoPickupDelay: false) && CompanionsPlugin.LogDetailed)
				{
					LogDebug("  picked up " + Loc(s) + " x" + num2);
				}
				current.LastDrop = Time.time;
				value.Target = null;
				return true;
			}
			MHR_Mover.Go(ai, dt, position2, 0.8f, allowRun: true, "Loot");
			if (MHR_Mover.RouteSwim(ai) > 0.5f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("  " + Loc((value.Target.m_itemData != null) ? value.Target.m_itemData.m_shared.m_name : "?") + " is across water - leaving it");
				}
				current.Skipped.Add(value.Target.GetInstanceID());
				value.Target = null;
				MHR_Mover.Release(ai);
				return true;
			}
			if (MHR_Mover.GaveUp(ai))
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("  can't get to " + Loc((value.Target.m_itemData != null) ? value.Target.m_itemData.m_shared.m_name : "?") + " - leaving it");
				}
				current.Skipped.Add(value.Target.GetInstanceID());
				value.Target = null;
				MHR_Mover.Release(ai);
			}
			return true;
		}
		Vector3 goal = DrySpot(ai.GetComponent<Character>(), position, current.Pos);
		float magnitude = new Vector2(position.x - goal.x, position.z - goal.z).magnitude;
		if (current.ArrivedAt < 0f && magnitude <= 4f)
		{
			current.ArrivedAt = Time.time;
		}
		bool flag = current.ArrivedAt >= 0f;
		if ((current.SawDrop && Time.time - current.LastDrop > 1.5f && !current.Body) || (((!current.SawDrop && !current.Body) & flag) && Time.time - current.ArrivedAt > 2f && Time.time - current.Time > 8f) || (flag && Time.time - current.ArrivedAt > Wait()))
		{
			Finish(ai, value, current.SawDrop ? "picked up everything" : ("nothing dropped (waited " + (Time.time - current.Time).ToString("F0") + " s after the kill)" + Around(current)));
			return false;
		}
		if (!flag)
		{
			MHR_Mover.Go(ai, dt, goal, 2f, allowRun: true, "Loot");
			if (MHR_Mover.GaveUp(ai))
			{
				Finish(ai, value, "stopped picking up (can't get to the body)");
				return false;
			}
			if (MHR_Mover.RouteSwim(ai) > 0.5f)
			{
				Finish(ai, value, "stopped picking up (it's across water)");
				return false;
			}
		}
		else
		{
			ai.StopMoving();
		}
		return true;
	}

	private static void Finish(CompanionAI ai, State st, string what)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug(what);
		}
		st.Current = null;
		st.Target = null;
		ai.StopMoving();
		MHR_Mover.Release(ai);
		if (st.Queue.Count == 0)
		{
			CompanionSetup component = ai.GetComponent<CompanionSetup>();
			ZNetView component2 = ai.GetComponent<ZNetView>();
			if (component != null && component2 != null && component2.GetZDO() != null)
			{
				component.ApplyFollowMode(component2.GetZDO().GetInt(CompanionSetup.ActionModeHash), force: true);
			}
		}
	}

	private static string Around(Kill k)
	{
		int num = Physics.OverlapSphereNonAlloc(k.Pos, Radius() * 2f, s_buf, Mask());
		int num2 = 0;
		for (int i = 0; i < num && i < s_buf.Length; i++)
		{
			ItemDrop itemDrop = DropOf(s_buf[i]);
			if (itemDrop != null && k.Before.Contains(itemDrop.GetInstanceID()))
			{
				num2++;
			}
		}
		if (num2 <= 0)
		{
			return "";
		}
		return " (" + num2 + " item(s) nearby were already lying there before the kill)";
	}

	private static Vector3 DrySpot(Character self, Vector3 me, Vector3 p)
	{
		if (!MHR_Water.WouldSwim(self, p))
		{
			return p;
		}
		Vector3 vector = me - p;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 0.5f)
		{
			return me;
		}
		vector /= magnitude;
		for (float num = 0.5f; num < magnitude; num += 0.5f)
		{
			Vector3 vector2 = p + vector * num;
			if (!MHR_Water.WouldSwim(self, vector2))
			{
				vector2.y = MHR_Water.GroundAt(vector2);
				return vector2;
			}
		}
		return me;
	}

	public static HashSet<int> ItemsAround(Vector3 pos)
	{
		HashSet<int> hashSet = new HashSet<int>();
		int num = Physics.OverlapSphereNonAlloc(pos, Radius() * 2f + 2f, s_buf, Mask());
		for (int i = 0; i < num && i < s_buf.Length; i++)
		{
			ItemDrop itemDrop = DropOf(s_buf[i]);
			if (itemDrop != null)
			{
				hashSet.Add(itemDrop.GetInstanceID());
			}
		}
		return hashSet;
	}

	private static ItemDrop DropOf(Collider col)
	{
		if (col == null || col.attachedRigidbody == null)
		{
			return null;
		}
		ItemDrop component = col.attachedRigidbody.GetComponent<ItemDrop>();
		if (component == null || !component.m_autoPickup)
		{
			return null;
		}
		ZNetView component2 = component.GetComponent<ZNetView>();
		if (!(component2 != null) || !component2.IsValid())
		{
			return null;
		}
		return component;
	}

	private static bool RoomForAny(Inventory inv, Kill k)
	{
		int num = Physics.OverlapSphereNonAlloc(k.Pos, Radius(), s_buf, Mask());
		for (int i = 0; i < num && i < s_buf.Length; i++)
		{
			ItemDrop itemDrop = DropOf(s_buf[i]);
			if (itemDrop != null && !k.Before.Contains(itemDrop.GetInstanceID()) && itemDrop.m_itemData != null && inv.CanAddItem(itemDrop.m_itemData, 1))
			{
				return true;
			}
		}
		return false;
	}

	private static ItemDrop NextDrop(Kill k, Vector3 me, Inventory inv, Character self)
	{
		float num = ((k.ArrivedAt >= 0f) ? (Radius() * 2f) : Radius());
		int num2 = Physics.OverlapSphereNonAlloc(k.Pos, (k.Seen.Count > 0) ? MaxDist() : num, s_buf, Mask());
		ItemDrop result = null;
		float num3 = float.MaxValue;
		for (int i = 0; i < num2 && i < s_buf.Length; i++)
		{
			ItemDrop itemDrop = DropOf(s_buf[i]);
			if (itemDrop == null || k.Before.Contains(itemDrop.GetInstanceID()) || k.Skipped.Contains(itemDrop.GetInstanceID()) || itemDrop.InTar() || (!k.Seen.Contains(itemDrop.GetInstanceID()) && Vector3.Distance(itemDrop.transform.position, k.Pos) > num))
			{
				continue;
			}
			if (MHR_Water.WouldSwim(self, itemDrop.transform.position))
			{
				k.Skipped.Add(itemDrop.GetInstanceID());
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("  " + Loc((itemDrop.m_itemData != null) ? itemDrop.m_itemData.m_shared.m_name : "?") + " is in deep water - leaving it");
				}
				continue;
			}
			if (MHR_Mover.Lava(itemDrop.transform.position))
			{
				k.Skipped.Add(itemDrop.GetInstanceID());
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("  " + Loc((itemDrop.m_itemData != null) ? itemDrop.m_itemData.m_shared.m_name : "?") + " is on lava - leaving it");
				}
				continue;
			}
			k.Seen.Add(itemDrop.GetInstanceID());
			if (itemDrop.m_itemData != null && inv.CanAddItem(itemDrop.m_itemData, 1) && !(inv.GetTotalWeight() + itemDrop.m_itemData.GetWeight() > CompanionTierData.CarryLimit(self)))
			{
				float num4 = Vector3.Distance(me, itemDrop.transform.position);
				if (num4 < num3)
				{
					num3 = num4;
					result = itemDrop;
				}
			}
		}
		return result;
	}

	private static string Loc(string s)
	{
		try
		{
			return (Localization.instance != null) ? Localization.instance.Localize(s) : s;
		}
		catch
		{
			return s;
		}
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Loot] " + msg);
		}
	}

	private static void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Loot] " + msg);
		}
	}
}
