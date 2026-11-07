using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(Attack), "GetProjectileSpawnPoint")]
internal static class CompanionProjectileAimPatch
{
	private static readonly Dictionary<Humanoid, KeyValuePair<Vector3, float>> s_point = new Dictionary<Humanoid, KeyValuePair<Vector3, float>>();

	private static readonly AccessTools.FieldRef<Attack, Humanoid> s_character = AccessTools.FieldRefAccess<Attack, Humanoid>("m_character");

	private static readonly AccessTools.FieldRef<Attack, BaseAI> s_baseAI = AccessTools.FieldRefAccess<Attack, BaseAI>("m_baseAI");

	private static readonly AccessTools.FieldRef<Attack, ItemDrop.ItemData> s_ammo = AccessTools.FieldRefAccess<Attack, ItemDrop.ItemData>("m_ammoItem");

	private static readonly AccessTools.FieldRef<Attack, float> s_drawPct = AccessTools.FieldRefAccess<Attack, float>("m_attackDrawPercentage");

	internal static void AimAt(Humanoid h, Vector3 point)
	{
		if (h != null)
		{
			s_point[h] = new KeyValuePair<Vector3, float>(point, Time.time);
		}
	}

	private static void Postfix(Attack __instance, ref Vector3 spawnPoint, ref Vector3 aimDir)
	{
		try
		{
			Humanoid humanoid = s_character(__instance);
			if (humanoid == null || humanoid.GetComponent<CompanionSetup>() == null)
			{
				return;
			}
			Character character = s_baseAI(__instance)?.GetTargetCreature();
			bool flag = s_point.TryGetValue(humanoid, out var value) && Time.time - value.Value < 2f;
			if (character == null && !flag)
			{
				return;
			}
			float num = __instance.m_projectileVel;
			float num2 = __instance.m_projectileVelMin;
			AnimationCurve drawVelocityCurve = __instance.m_drawVelocityCurve;
			GameObject attackProjectile = __instance.m_attackProjectile;
			ItemDrop.ItemData itemData = s_ammo(__instance);
			if (itemData?.m_shared?.m_attack != null)
			{
				num += itemData.m_shared.m_attack.m_projectileVel;
				num2 += itemData.m_shared.m_attack.m_projectileVelMin;
				drawVelocityCurve = itemData.m_shared.m_attack.m_drawVelocityCurve;
				if (itemData.m_shared.m_attack.m_attackProjectile != null)
				{
					attackProjectile = itemData.m_shared.m_attack.m_attackProjectile;
				}
			}
			if (__instance.m_bowDraw && drawVelocityCurve != null)
			{
				num = Mathf.Lerp(num2, num, drawVelocityCurve.Evaluate(s_drawPct(__instance)));
			}
			if (!(num < 1f))
			{
				float num3 = 0f;
				Projectile projectile = ((attackProjectile != null) ? attackProjectile.GetComponent<Projectile>() : null);
				if (projectile != null)
				{
					num3 = projectile.m_gravity;
				}
				Vector3 vector = ((character != null) ? character.GetCenterPoint() : value.Key);
				Vector3 vector2 = ((character != null) ? character.GetVelocity() : Vector3.zero);
				Vector3 vector3 = vector;
				float num4 = 0f;
				for (int i = 0; i < 3; i++)
				{
					num4 = Vector3.Distance(spawnPoint, vector3) / num;
					vector3 = vector + vector2 * num4;
				}
				vector3.y += 0.5f * num3 * num4 * num4;
				Vector3 to = vector3 - spawnPoint;
				if (to.sqrMagnitude > 0.01f && Vector3.Angle(aimDir, to) <= 25f)
				{
					aimDir = to.normalized;
				}
			}
		}
		catch
		{
		}
	}
}
