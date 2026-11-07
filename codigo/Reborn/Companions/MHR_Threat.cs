using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Threat
{
	private static readonly Dictionary<int, float> s_ignore = new Dictionary<int, float>();

	private static float s_nextLog;

	public static bool IgnoredInWater(Character c)
	{
		if (c == null)
		{
			return false;
		}
		if (s_ignore.TryGetValue(c.GetInstanceID(), out var value) && Time.time < value)
		{
			return MHR_Water.DepthAt(c.transform.position) > 1f;
		}
		return false;
	}

	public static bool IsPassive(Character c)
	{
		if (c == null)
		{
			return false;
		}
		try
		{
			string name = c.gameObject.name;
			if (name.StartsWith("Deer") || name.StartsWith("Hare") || name.StartsWith("Chicken"))
			{
				return true;
			}
			BaseAI baseAI = c.GetBaseAI();
			if (s_ignore.TryGetValue(c.GetInstanceID(), out var value))
			{
				bool flag = baseAI != null && baseAI.GetTargetCreature() != null;
				if (Time.time < value && !flag && MHR_Water.DepthAt(c.transform.position) > 1f)
				{
					return true;
				}
				s_ignore.Remove(c.GetInstanceID());
			}
			if (baseAI is AnimalAI)
			{
				return true;
			}
			MonsterAI monsterAI = baseAI as MonsterAI;
			if (baseAI != null && baseAI.GetTargetCreature() == null && !baseAI.IsAlerted() && (baseAI.m_passiveAggresive || (monsterAI != null && (monsterAI.m_avoidLand || monsterAI.m_fleeIfNotAlerted))))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool IsAfterUs(Character c)
	{
		if (c == null)
		{
			return false;
		}
		try
		{
			BaseAI baseAI = c.GetBaseAI();
			if (baseAI == null)
			{
				return true;
			}
			Character targetCreature = baseAI.GetTargetCreature();
			if (targetCreature == null)
			{
				return false;
			}
			if (ModConfig.EngageOnNotice != null && !ModConfig.EngageOnNotice.Value && !baseAI.IsAlerted())
			{
				return false;
			}
			return targetCreature.IsPlayer() || targetCreature.GetComponent<CompanionSetup>() != null;
		}
		catch
		{
			return true;
		}
	}

	public static bool AvoidChase(Character self, Character target)
	{
		if (self == null || target == null)
		{
			return false;
		}
		try
		{
			if (self.IsSwimming())
			{
				return false;
			}
			if (!MHR_Water.WouldSwim(self, target.transform.position))
			{
				return false;
			}
			BaseAI baseAI = target.GetBaseAI();
			bool flag = baseAI != null && (baseAI.IsAlerted() || baseAI.GetTargetCreature() != null);
			Humanoid humanoid = self as Humanoid;
			if (flag && humanoid != null && MHR_Crossbow.HasReadyRanged(humanoid))
			{
				return false;
			}
			if (flag && Vector3.Distance(self.transform.position, target.transform.position) < 3f)
			{
				return false;
			}
			if (s_ignore.Count > 200)
			{
				s_ignore.Clear();
			}
			s_ignore[target.GetInstanceID()] = Time.time + 20f;
			CompanionCombatAI component = self.GetComponent<CompanionCombatAI>();
			if (component != null)
			{
				component.Disengage();
			}
			if (CompanionsPlugin.Log != null && Time.time >= s_nextLog)
			{
				s_nextLog = Time.time + 5f;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] " + target.m_name + " is in deep water - leaving it");
				}
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
