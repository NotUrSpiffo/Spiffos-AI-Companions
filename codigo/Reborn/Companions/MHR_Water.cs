using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Water
{
	private static WaterVolume s_wv;

	private static int s_mask = -1;

	private static readonly Dictionary<int, KeyValuePair<float, bool>> s_agent = new Dictionary<int, KeyValuePair<float, bool>>();

	private static int Mask()
	{
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain", "vehicle");
		}
		return s_mask;
	}

	public static float WaterLevel(Vector3 p)
	{
		try
		{
			return Floating.GetWaterLevel(p, ref s_wv);
		}
		catch
		{
			return -10000f;
		}
	}

	public static float GroundAt(Vector3 p)
	{
		if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 60f, Mask()))
		{
			return hitInfo.point.y;
		}
		if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(p, out var height))
		{
			return height;
		}
		return p.y;
	}

	public static float DepthAt(Vector3 p)
	{
		float num = GroundAt(p);
		float num2 = WaterLevel(new Vector3(p.x, num + 0.3f, p.z));
		if (num2 < -1000f)
		{
			num2 = WaterLevel(p);
		}
		if (num2 < -1000f)
		{
			return 0f;
		}
		return Mathf.Max(0f, num2 - num);
	}

	public static float SwimLimit(Character c)
	{
		return Mathf.Max(0.3f, ((c != null) ? c.m_swimDepth : 2f) - 0.4f);
	}

	public static bool WouldSwim(Character c, Vector3 p)
	{
		return DepthAt(p) > SwimLimit(c);
	}

	public static float SwimDrainSkill(Character c)
	{
		float value = 0f;
		try
		{
			if (c != null)
			{
				value = c.GetSkillFactor(Skills.SkillType.Swim);
			}
		}
		catch
		{
		}
		return Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(value));
	}

	public static float SwimStamina(Character c, float meters)
	{
		float num = ((c != null && c.m_swimSpeed > 0.1f) ? c.m_swimSpeed : 2f);
		float num2 = ((ModConfig.StaminaSwimDrain != null) ? ModConfig.StaminaSwimDrain.Value : 5f) * SwimDrainSkill(c);
		return meters / num * num2;
	}

	public static float SwimMeters(Character c, Vector3 pos, List<Vector3> path, float maxLen)
	{
		float num = SwimLimit(c);
		float num2 = 0f;
		float num3 = 0f;
		Vector3 vector = pos;
		for (int i = 0; i < path.Count; i++)
		{
			if (!(num3 < maxLen))
			{
				break;
			}
			Vector3 vector2 = path[i] - vector;
			float magnitude = new Vector2(vector2.x, vector2.z).magnitude;
			float num4 = 0.5f;
			while (num4 < magnitude && num3 < maxLen)
			{
				if (DepthAt(vector + vector2 * (num4 / magnitude)) > num)
				{
					num2++;
				}
				num4++;
				num3++;
			}
			vector = path[i];
		}
		return num2;
	}

	public static float DistToWater(Character c, Vector3 pos, List<Vector3> path)
	{
		float num = SwimLimit(c);
		float num2 = 0f;
		Vector3 vector = pos;
		for (int i = 0; i < path.Count; i++)
		{
			if (!(num2 < 30f))
			{
				break;
			}
			Vector3 vector2 = path[i] - vector;
			float magnitude = new Vector2(vector2.x, vector2.z).magnitude;
			float num3 = 0.5f;
			while (num3 < magnitude && num2 < 30f)
			{
				if (DepthAt(vector + vector2 * (num3 / magnitude)) > num)
				{
					return num2;
				}
				num3++;
				num2++;
			}
			vector = path[i];
		}
		return 999f;
	}

	public static bool SwimOnLine(Character c, Vector3 a, Vector3 b, float maxLen)
	{
		float num = SwimLimit(c);
		Vector3 vector = b - a;
		float num2 = Mathf.Min(new Vector2(vector.x, vector.z).magnitude, maxLen);
		if (num2 < 0.5f)
		{
			return DepthAt(b) > num;
		}
		Vector3 normalized = new Vector3(vector.x, 0f, vector.z).normalized;
		for (float num3 = 0.5f; num3 <= num2; num3 += 1.5f)
		{
			if (DepthAt(a + normalized * num3 + Vector3.up * (vector.y * num3 / num2)) > num)
			{
				return true;
			}
		}
		return DepthAt(b) > num;
	}

	public static void ApplyAgent(CompanionAI ai, bool lowStamina)
	{
		bool flag = false;
		try
		{
			Character component = ai.GetComponent<Character>();
			GameObject followTarget = ai.GetFollowTarget();
			if (lowStamina && component != null && !component.IsSwimming() && followTarget != null)
			{
				int instanceID = ai.gameObject.GetInstanceID();
				if (s_agent.TryGetValue(instanceID, out var value) && Time.time - value.Key < 1f)
				{
					flag = value.Value;
				}
				else
				{
					flag = SwimOnLine(component, ai.transform.position, followTarget.transform.position, 40f);
					s_agent[instanceID] = new KeyValuePair<float, bool>(Time.time, flag);
				}
			}
		}
		catch
		{
			flag = false;
		}
		ai.m_avoidWater = flag;
		ai.m_pathAgentType = ((!flag) ? Pathfinding.AgentType.Humanoid : Pathfinding.AgentType.HumanoidAvoidWater);
	}
}
