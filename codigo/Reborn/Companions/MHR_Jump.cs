using UnityEngine;

namespace Companions;

public static class MHR_Jump
{
	public static float Cost(Character c)
	{
		if (c == null)
		{
			return 10f;
		}
		float staminaUse = c.m_jumpStaminaUsage;
		try
		{
			c.GetSEMan().ModifyJumpStaminaUsage(staminaUse, ref staminaUse);
		}
		catch
		{
		}
		return staminaUse * Game.m_moveStaminaRate;
	}

	public static bool CanAfford(Character c)
	{
		if (c == null)
		{
			return false;
		}
		CompanionStamina component = c.GetComponent<CompanionStamina>();
		if (component != null)
		{
			return component.Stamina >= Cost(c);
		}
		return c.HaveStamina(Cost(c));
	}

	public static bool HasSpare(Character c, float share)
	{
		if (c == null)
		{
			return false;
		}
		CompanionStamina component = c.GetComponent<CompanionStamina>();
		if (component == null)
		{
			return CanAfford(c);
		}
		return component.Stamina >= Cost(c) + share * component.MaxStamina;
	}

	public static float RunSpeed(Character c, Vector3 dir)
	{
		if (c == null)
		{
			return 6f;
		}
		float speed = c.m_runSpeed;
		try
		{
			c.GetSEMan().ApplyStatusEffectSpeedMods(ref speed, dir);
		}
		catch
		{
		}
		Vector3 velocity = c.GetVelocity();
		velocity.y = 0f;
		return Mathf.Max(speed, velocity.magnitude);
	}

	public static float Reach(Character c, Vector3 dir, float dy)
	{
		if (c == null)
		{
			return 2f;
		}
		float num = 0f;
		try
		{
			num = c.GetSkillFactor(Skills.SkillType.Jump);
		}
		catch
		{
		}
		float num2 = 1f + num * 0.4f;
		float num3 = c.m_jumpForce * num2;
		float num4 = RunSpeed(c, dir) + c.m_jumpForceForward * num2;
		if (!CanAfford(c))
		{
			num3 *= c.m_jumpForceTiredFactor;
			num4 *= c.m_jumpForceTiredFactor;
		}
		float num5 = Mathf.Max(1f, 0f - Physics.gravity.y);
		float num6 = num3 * num3 - 2f * num5 * dy;
		if (num6 < 0f)
		{
			return 0f;
		}
		float num7 = (num3 + Mathf.Sqrt(num6)) / num5;
		return num4 * num7 * 0.9f;
	}

	public static void Leap(CompanionAI ai, Character c, Vector3 dir)
	{
		ai.transform.rotation = Quaternion.LookRotation(dir);
		c.SetRun(run: true);
		Rigidbody component = ai.GetComponent<Rigidbody>();
		float num = RunSpeed(c, dir);
		if (component != null)
		{
			Vector3 linearVelocity = component.linearVelocity;
			linearVelocity.x = dir.x * num;
			linearVelocity.z = dir.z * num;
			component.linearVelocity = linearVelocity;
		}
		c.Jump();
	}
}
