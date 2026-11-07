using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class MHR_Hive
{
	private class S
	{
		public Beehive Hive;

		public string Honey;

		public bool Extracted;

		public float Since;

		public float StepAt;

		public float ActAt;

		public Vector3 At;

		public HashSet<int> Before = new HashSet<int>();

		public int Took;

		public int HadBefore;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Hive] " + msg);
		}
	}

	private static S Peek(CompanionAI ai)
	{
		if (!(ai != null) || !s_s.TryGetValue(ai.GetInstanceID(), out var value))
		{
			return null;
		}
		return value;
	}

	internal static bool Active(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null)
		{
			return s.Hive != null;
		}
		return false;
	}

	internal static int HoneyIn(Beehive hive)
	{
		return (hive?.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level)).GetValueOrDefault();
	}

	internal static void Start(CompanionAI ai, Beehive hive)
	{
		if (!(ai == null) && !(hive == null) && !(hive.m_honeyItem == null))
		{
			S value = new S
			{
				Hive = hive,
				Honey = hive.m_honeyItem.m_itemData.m_shared.m_name,
				Since = Time.time,
				StepAt = Time.time,
				HadBefore = (ai.GetComponent<Humanoid>()?.GetInventory()?.CountItems(hive.m_honeyItem.m_itemData.m_shared.m_name)).GetValueOrDefault()
			};
			s_s[ai.GetInstanceID()] = value;
			MHR_Mover.Release(ai);
			if (CompanionsPlugin.LogNormal)
			{
				Log($"{ai.GetComponent<Character>()?.m_name} collecting {HoneyIn(hive)} honey from the beehive at {MHR_Mover.Fmt(hive.transform.position)}");
			}
		}
	}

	internal static void Cancel(CompanionAI ai)
	{
		if (Active(ai))
		{
			s_s.Remove(ai.GetInstanceID());
			MHR_Mover.Release(ai);
		}
	}

	private static bool End(CompanionAI ai, string why)
	{
		if (why != null && CompanionsPlugin.LogNormal)
		{
			Log(ai.GetComponent<Character>()?.m_name + " - " + why);
		}
		s_s.Remove(ai.GetInstanceID());
		MHR_Mover.Release(ai);
		ai.StopMoving();
		ai.BoardingEnded();
		return false;
	}

	private static int Gained(CompanionAI ai, S s)
	{
		int valueOrDefault = (ai.GetComponent<Humanoid>()?.GetInventory()?.CountItems(s.Honey)).GetValueOrDefault();
		return Mathf.Max(s.Took, valueOrDefault - s.HadBefore);
	}

	internal static bool Update(CompanionAI ai, float dt)
	{
		S s = Peek(ai);
		Humanoid component = ai.GetComponent<Humanoid>();
		if (s == null || component == null)
		{
			return false;
		}
		float time = Time.time;
		Beehive hive = s.Hive;
		ZNetView zNetView = ((hive != null) ? hive.GetComponent<ZNetView>() : null);
		Vector3 position = ai.transform.position;
		if (!s.Extracted)
		{
			if (zNetView == null || !zNetView.IsValid())
			{
				return End(ai, "the beehive is gone");
			}
			if (HoneyIn(hive) <= 0)
			{
				return End(ai, "someone emptied the beehive first");
			}
			Vector3 vector = hive.transform.position + Vector3.up * 0.5f;
			if (Vector3.Distance(position + Vector3.up * 1.6f, vector) > 2.5f)
			{
				MHR_Mover.Go(ai, dt, hive.transform.position, 1.2f, allowRun: false, "Hive");
				if (MHR_Mover.GaveUp(ai) || time - s.Since > 30f)
				{
					return End(ai, "couldn't get to the beehive");
				}
				s.ActAt = 0f;
				return true;
			}
			ai.StopMoving();
			MHR_Mover.Release(ai);
			ai.LookAtPoint(vector);
			if (s.ActAt == 0f)
			{
				s.ActAt = time + 0.4f;
				return true;
			}
			if (time < s.ActAt || (!ai.IsLookingAtPoint(vector, 20f) && time < s.ActAt + 2f))
			{
				return true;
			}
			ai.GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			s.At = ((hive.m_spawnPoint != null) ? hive.m_spawnPoint.position : hive.transform.position);
			s.Before = MHR_Chests.DropsNear(s.At, 3f);
			zNetView.InvokeRPC("RPC_Extract");
			s.Extracted = true;
			s.StepAt = time;
			return true;
		}
		if (time - s.StepAt < 0.6f)
		{
			return true;
		}
		if (time - s.StepAt > 20f)
		{
			return End(ai, $"picked up {Gained(ai, s)} honey");
		}
		Inventory inventory = component.GetInventory();
		ItemDrop itemDrop = null;
		float num = float.MaxValue;
		Collider[] array = Physics.OverlapSphere(s.At, 3f);
		foreach (Collider collider in array)
		{
			ItemDrop itemDrop2 = ((collider != null) ? collider.GetComponentInParent<ItemDrop>() : null);
			if (!(itemDrop2 == null) && itemDrop2.m_itemData != null && !(itemDrop2.m_itemData.m_shared.m_name != s.Honey) && !s.Before.Contains(itemDrop2.GetInstanceID()) && inventory.CanAddItem(itemDrop2.m_itemData, 1))
			{
				float num2 = Vector3.Distance(position, itemDrop2.transform.position);
				if (num2 < num)
				{
					num = num2;
					itemDrop = itemDrop2;
				}
			}
		}
		if (itemDrop == null)
		{
			return End(ai, $"picked up {Gained(ai, s)} honey - keeping it");
		}
		if (num > 1.5f)
		{
			MHR_Mover.Go(ai, dt, itemDrop.transform.position, 1f, allowRun: false, "Hive");
			return true;
		}
		if (!itemDrop.CanPickup())
		{
			itemDrop.RequestOwn();
			ai.StopMoving();
			return true;
		}
		int stack = itemDrop.m_itemData.m_stack;
		if (component.Pickup(itemDrop.gameObject, autoequip: false, autoPickupDelay: false))
		{
			s.Took += stack;
		}
		return true;
	}
}
