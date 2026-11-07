using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class PlayerCombatLog
{
	[HarmonyPatch(typeof(Humanoid), "StartAttack")]
	private static class PlayerSwing
	{
		private static void Prefix(Humanoid __instance, out float __state)
		{
			__state = ((__instance != null) ? __instance.GetTimeSinceLastAttack() : 0f);
		}

		private static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result, float __state)
		{
			if (!__result || !On || __instance == null || __instance != Player.m_localPlayer)
			{
				return;
			}
			Player player = (Player)__instance;
			string text = "";
			try
			{
				if (s_curAttack?.GetValue(__instance) is Attack { m_attackChainLevels: >1 } attack)
				{
					text = $", combo swing {(int)s_chainLevel.GetValue(attack) + 1}/{attack.m_attackChainLevels}";
				}
			}
			catch
			{
			}
			if (!(text == s_lastCombo) || !(Time.time - s_lastSwingLog < 0.4f))
			{
				s_lastCombo = text;
				s_lastSwingLog = Time.time;
				Character character = Nearest(player, 6f, out var gap);
				string text2 = player.GetCurrentWeapon()?.m_shared?.m_name ?? "?";
				if (CompanionsPlugin.LogDetailed)
				{
					Log(string.Format("{0} with {1}{2}, {3:F2} s after the last swing ended", secondaryAttack ? "Secondary attack" : "Swing", text2, text, __state) + ((character != null) ? string.Format(" - {0} {1:F1} m away (its reach {2:F1}){3}{4}", character.m_name, gap, CompanionCombatAI.SwingReach(character), character.InAttack() ? ", it's mid-swing" : "", character.IsStaggering() ? ", it's staggered" : "") : "") + ", " + Stam(player));
				}
			}
		}
	}

	private static readonly Dictionary<int, bool> s_enemyInAttack = new Dictionary<int, bool>();

	private static bool s_blocking;

	private static bool s_dodging;

	private static float s_lastHp = -1f;

	private static float s_nextSpacing;

	private static float s_guardUpAt;

	private static string s_lastCombo;

	private static float s_lastSwingLog = -10f;

	private static readonly FieldInfo s_curAttack = AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	private static readonly FieldInfo s_chainLevel = AccessTools.Field(typeof(Attack), "m_currentAttackCainLevel");

	internal static bool On
	{
		get
		{
			if (CompanionsPlugin.LogDetailed)
			{
				return ModConfig.LogPlayerCombat?.Value ?? true;
			}
			return false;
		}
	}

	private static void Log(string s)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[You] " + s);
		}
	}

	private static string Stam(Player p)
	{
		return $"stamina {p.GetStamina():F0}/{p.GetMaxStamina():F0}";
	}

	private static float Gap(Player p, Character c)
	{
		return Vector3.Distance(p.transform.position, c.transform.position) - p.GetRadius() - c.GetRadius();
	}

	private static Character Nearest(Player p, float range, out float gap)
	{
		Character result = null;
		gap = float.MaxValue;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter == p) && !(allCharacter.GetHealth() <= 0f) && BaseAI.IsEnemy(p, allCharacter))
			{
				float num = Gap(p, allCharacter);
				if (num < range && num < gap)
				{
					gap = num;
					result = allCharacter;
				}
			}
		}
		return result;
	}

	private static float AwaySpeed(Player p, Character c)
	{
		Vector3 vector = p.transform.position - c.transform.position;
		vector.y = 0f;
		Vector3 velocity = p.GetVelocity();
		velocity.y = 0f;
		if (!(vector.sqrMagnitude > 0.0001f))
		{
			return 0f;
		}
		return Vector3.Dot(velocity, vector.normalized);
	}

	internal static void Tick()
	{
		Player localPlayer = Player.m_localPlayer;
		if (!On || localPlayer == null || localPlayer.IsDead())
		{
			return;
		}
		float time = Time.time;
		bool flag = localPlayer.IsBlocking();
		if (flag != s_blocking)
		{
			s_blocking = flag;
			Character character = Nearest(localPlayer, 6f, out var gap);
			if (flag)
			{
				s_guardUpAt = time;
				if (CompanionsPlugin.LogDetailed)
				{
					Log("Guard up" + ((character != null) ? string.Format(" - {0} {1:F1} m away (its reach {2:F1}){3}", character.m_name, gap, CompanionCombatAI.SwingReach(character), character.InAttack() ? ", mid-swing" : "") : "") + ", " + Stam(localPlayer));
				}
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				Log($"Guard down after {time - s_guardUpAt:F2} s, " + Stam(localPlayer));
			}
		}
		bool flag2 = localPlayer.InDodge();
		if (flag2 && !s_dodging)
		{
			Character character2 = Nearest(localPlayer, 6f, out var gap2);
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Dodge roll" + ((character2 != null) ? string.Format(" - {0} {1:F1} m away{2}", character2.m_name, gap2, character2.InAttack() ? ", mid-swing" : "") : "") + ", " + Stam(localPlayer));
			}
		}
		s_dodging = flag2;
		float health = localPlayer.GetHealth();
		if (s_lastHp >= 0f && health < s_lastHp - 0.5f && CompanionsPlugin.LogDetailed)
		{
			Log(string.Format("Took {0:F0} damage ({1:F0} hp left){2}", s_lastHp - health, health, flag ? ", guard up" : ""));
		}
		s_lastHp = health;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == null || allCharacter == localPlayer || allCharacter.GetHealth() <= 0f || !BaseAI.IsEnemy(localPlayer, allCharacter))
			{
				continue;
			}
			int instanceID = allCharacter.GetInstanceID();
			bool flag3 = allCharacter.InAttack();
			s_enemyInAttack.TryGetValue(instanceID, out var value);
			if (flag3 && !value)
			{
				float num = Gap(localPlayer, allCharacter);
				if (num < 6f)
				{
					float num2 = CompanionCombatAI.SwingReach(allCharacter);
					float num3 = AwaySpeed(localPlayer, allCharacter);
					bool flag4 = allCharacter.GetBaseAI()?.GetTargetCreature() == localPlayer;
					if (CompanionsPlugin.LogDetailed)
					{
						Log(string.Format("{0} swings{1} from {2:F1} m (its reach {3:F1}) - you're {4}, {5}{6}{7}, ", allCharacter.m_name, flag4 ? " at you" : "", num, num2, (num > num2) ? "out of reach" : "in reach", (num3 > 0.3f) ? $"moving away {num3:F1} m/s" : ((num3 < -0.3f) ? $"closing in {0f - num3:F1} m/s" : "standing"), flag ? ", guard up" : "", localPlayer.InAttack() ? ", mid-swing" : "") + Stam(localPlayer));
					}
				}
			}
			s_enemyInAttack[instanceID] = flag3;
		}
		if (s_enemyInAttack.Count > 200)
		{
			s_enemyInAttack.Clear();
		}
		if (!(time >= s_nextSpacing))
		{
			return;
		}
		s_nextSpacing = time + 1f;
		Character character3 = Nearest(localPlayer, 5f, out var gap3);
		if (character3 != null)
		{
			float num4 = AwaySpeed(localPlayer, character3);
			if (CompanionsPlugin.LogDetailed)
			{
				Log(string.Format("Spacing: {0} {1:F1} m (its reach {2:F1}{3}{4}), you're {5}{6}, {7}", character3.m_name, gap3, CompanionCombatAI.SwingReach(character3), character3.InAttack() ? ", mid-swing" : "", character3.IsStaggering() ? ", staggered" : "", (num4 > 0.3f) ? $"moving away {num4:F1} m/s" : ((num4 < -0.3f) ? $"closing in {0f - num4:F1} m/s" : "standing"), flag ? ", guard up" : "", Stam(localPlayer)));
			}
		}
	}

	internal static void NoteBlock(Humanoid h, Character attacker, bool parry, float before, float after)
	{
		if (On && !(h != Player.m_localPlayer) && CompanionsPlugin.LogDetailed)
		{
			Log(string.Format("{0} {1} {2}: {3:F0} damage, {4:F0} got through, ", parry ? "Parried" : "Blocked", (attacker != null) ? attacker.m_name : "a hit", (s_guardUpAt > 0f) ? $"{Time.time - s_guardUpAt:F2} s after raising the guard" : "", before, after) + Stam((Player)h));
		}
	}

	internal static void NoteHit(Character target, HitData hit)
	{
		if (On && !(target == null) && CompanionsPlugin.LogDetailed)
		{
			Log(string.Format("Hit {0} for {1:F0}{2}", target.m_name, hit.GetTotalDamage(), target.IsStaggering() ? " (staggered - double damage)" : ""));
		}
	}
}
