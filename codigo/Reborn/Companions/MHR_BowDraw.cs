using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class MHR_BowDraw
{
	private class State
	{
		public float LastWant;

		public float Started;

		public float Held;

		public float Jitter = UnityEngine.Random.Range(0.9f, 1.1f);

		public bool LooseNow;

		public string Why = "full draw";
	}

	private static readonly Dictionary<int, State> s_states = new Dictionary<int, State>();

	private static readonly HashSet<int> s_resting = new HashSet<int>();

	private static int s_blockMask;

	private static readonly AccessTools.FieldRef<VisEquipment, GameObject> s_leftInst = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_leftItemInstance");

	private static bool s_loggedCompanion;

	private static bool s_loggedPlayer;

	internal static bool NeedsDraw(Humanoid h)
	{
		ItemDrop.ItemData itemData = h?.GetCurrentWeapon();
		if (itemData?.m_shared?.m_attack != null)
		{
			return itemData.m_shared.m_attack.m_bowDraw;
		}
		return false;
	}

	internal static bool IsResting(Humanoid h)
	{
		if (h != null)
		{
			return s_resting.Contains(h.GetInstanceID());
		}
		return false;
	}

	internal static bool IsDrawing(Humanoid h)
	{
		if (h != null)
		{
			return s_states.ContainsKey(h.GetInstanceID());
		}
		return false;
	}

	internal static bool Tick(Humanoid h, Character target, bool aimed, bool clear)
	{
		ItemDrop.ItemData itemData = h?.GetCurrentWeapon();
		if (itemData == null || !NeedsDraw(h))
		{
			return false;
		}
		int instanceID = h.GetInstanceID();
		if (!s_states.TryGetValue(instanceID, out var value))
		{
			if (h.InAttack() || h.IsBlocking() || TooTiredToDraw(h, itemData, target))
			{
				return false;
			}
			if (!itemData.m_shared.m_attack.StartDraw(h, itemData))
			{
				return false;
			}
			value = new State
			{
				Started = Time.time
			};
			s_states[instanceID] = value;
			Attack attack = itemData.m_shared.m_attack;
			float num = Mathf.Lerp(attack.m_drawDurationMin, attack.m_drawDurationMin * 0.2f, h.GetSkillFactor(itemData.m_shared.m_skillType));
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Bow] Drawing {itemData.m_shared.m_name} - full draw in {num:F1} s");
			}
			ReflectionHelper.TrySetAttackDrawTime(h, 0.0001f);
			itemData.m_shared.m_holdStartEffect.Create(h.transform.position, Quaternion.identity, h.transform);
			LogBowOnce(h, "companion");
		}
		value.LastWant = Time.time;
		float attackDrawPercentage = h.GetAttackDrawPercentage();
		if (attackDrawPercentage < (value.LooseNow ? 0f : ReleaseAt(h, itemData, target, value)))
		{
			return false;
		}
		if (!aimed || !clear)
		{
			if (!clear && value.Held == 0f && CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Bow] Fully drawn but something is in the way of the shot - holding");
			}
			value.Held += Time.deltaTime;
			return false;
		}
		bool flag = h.StartAttack(target, false);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug(string.Format("[Bow] {0} at {1:F0}% draw ({2}) after {3:F1} s", flag ? "Loosed" : "Couldn't loose", attackDrawPercentage * 100f, value.LooseNow ? "out of stamina" : value.Why, Time.time - value.Started) + ((value.Held > 0.05f) ? $", held {value.Held:F1} s" : ""));
		}
		Cancel(h);
		return flag;
	}

	internal static void Cancel(Humanoid h)
	{
		if (!(h == null))
		{
			s_states.Remove(h.GetInstanceID());
			h.m_turnSpeed = (h.m_runTurnSpeed = ModConfig.TurnSpeed?.Value ?? 300f);
			ReflectionHelper.TrySetAttackDrawTime(h, 0f);
			string text = h.GetCurrentWeapon()?.m_shared?.m_attack?.m_drawAnimationState;
			ZSyncAnimation component = h.GetComponent<ZSyncAnimation>();
			if (component != null && !string.IsNullOrEmpty(text))
			{
				component.SetBool(text, value: false);
			}
		}
	}

	internal static void Maintain(Humanoid h, float dt)
	{
		LogPlayerBowOnce();
		if (h == null || !s_states.TryGetValue(h.GetInstanceID(), out var value))
		{
			return;
		}
		ItemDrop.ItemData currentWeapon = h.GetCurrentWeapon();
		if (currentWeapon == null || !NeedsDraw(h) || h.IsBlocking() || Time.time - value.LastWant > 1f)
		{
			Cancel(h);
			return;
		}
		float attackDrawPercentage = h.GetAttackDrawPercentage();
		float num = DrawDrain(h, currentWeapon) * ((attackDrawPercentage >= 1f) ? 0.5f : 1f);
		h.m_turnSpeed = (h.m_runTurnSpeed = Mathf.Max(ModConfig.TurnSpeed?.Value ?? 300f, ModConfig.CombatBowAimTurnSpeed?.Value ?? 600f));
		if (num > 0f && !h.HaveStamina())
		{
			if (attackDrawPercentage >= MinDraw())
			{
				value.LooseNow = true;
				return;
			}
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Bow] Out of stamina - letting go of the string, getting its breath back");
			}
			s_resting.Add(h.GetInstanceID());
			Cancel(h);
			return;
		}
		ReflectionHelper.TrySetAttackDrawTime(h, ReflectionHelper.GetAttackDrawTime(h) + dt);
		attackDrawPercentage = h.GetAttackDrawPercentage();
		ZSyncAnimation component = h.GetComponent<ZSyncAnimation>();
		string drawAnimationState = currentWeapon.m_shared.m_attack.m_drawAnimationState;
		if (component != null && !string.IsNullOrEmpty(drawAnimationState))
		{
			component.SetBool(drawAnimationState, value: true);
			component.SetFloat("drawpercent", attackDrawPercentage);
		}
		if (num > 0f)
		{
			h.UseStamina(num * dt);
		}
	}

	private static bool TooTiredToDraw(Humanoid h, ItemDrop.ItemData w, Character target)
	{
		CompanionStamina component = h.GetComponent<CompanionStamina>();
		if (component == null || component.MaxStamina <= 0f)
		{
			return false;
		}
		int instanceID = h.GetInstanceID();
		float num = component.Stamina / component.MaxStamina;
		if (s_resting.Contains(instanceID))
		{
			if (num < (ModConfig.CombatStaminaResume?.Value ?? 0.5f))
			{
				return true;
			}
			s_resting.Remove(instanceID);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Bow] Got its breath back ({component.Stamina:F0}/{component.MaxStamina:F0}) - drawing again");
			}
			return false;
		}
		bool flag = target == null || Vector3.Distance(h.transform.position, target.transform.position) >= (ModConfig.CombatBowFullDrawRange?.Value ?? 15f);
		Attack attack = w.m_shared.m_attack;
		float num2 = Mathf.Lerp(attack.m_drawDurationMin, attack.m_drawDurationMin * 0.2f, h.GetSkillFactor(w.m_shared.m_skillType));
		float num3 = DrawDrain(h, w) * num2 * (flag ? 1f : MinDraw()) + 1f;
		if (component.Stamina < num3 || (flag && num < (ModConfig.CombatStaminaReserve?.Value ?? 0.3f)))
		{
			s_resting.Add(instanceID);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Bow] Too tired to draw ({component.Stamina:F0}/{component.MaxStamina:F0} stamina) - waiting for its breath");
			}
			return true;
		}
		return false;
	}

	private static float DrawDrain(Humanoid h, ItemDrop.ItemData w)
	{
		float drawStaminaDrain = w.m_shared.m_attack.m_drawStaminaDrain;
		if (drawStaminaDrain <= 0f)
		{
			return 0f;
		}
		return drawStaminaDrain - drawStaminaDrain * 0.33f * h.GetSkillFactor(w.m_shared.m_skillType);
	}

	private static float MinDraw()
	{
		return Mathf.Clamp01(ModConfig.CombatBowMinDraw?.Value ?? 0.5f);
	}

	private static float ReleaseAt(Humanoid h, ItemDrop.ItemData w, Character target, State st)
	{
		float num = MinDraw();
		if (target == null)
		{
			st.Why = "full draw";
			return 1f;
		}
		Vector3 eyePoint = h.GetEyePoint();
		Vector3 centerPoint = target.GetCenterPoint();
		float num2 = Vector3.Distance(eyePoint, centerPoint);
		float num3 = ModConfig.CombatBowFullDrawRange?.Value ?? 15f;
		CompanionStamina component = h.GetComponent<CompanionStamina>();
		if (num2 < num3 && component != null && component.MaxStamina > 0f && component.Stamina / component.MaxStamina < (ModConfig.CombatStaminaReserve?.Value ?? 0.3f))
		{
			st.Why = "low on stamina";
			return num;
		}
		Attack attack = w.m_shared.m_attack;
		Vector3 vector = eyePoint - centerPoint;
		vector.y = 0f;
		float num4 = ((vector.sqrMagnitude > 0.01f) ? Vector3.Dot(target.GetVelocity(), vector.normalized) : 0f);
		if (num2 < 8f && num4 > 1f)
		{
			float num5 = Mathf.Max(0.1f, Mathf.Lerp(attack.m_drawDurationMin, attack.m_drawDurationMin * 0.2f, h.GetSkillFactor(w.m_shared.m_skillType)));
			float num6 = (1f - h.GetAttackDrawPercentage()) * num5;
			if ((num2 - 2f) / num4 < num6)
			{
				st.Why = $"it's rushing in, {num2:F0} m";
				return num;
			}
		}
		if (num2 >= num3)
		{
			st.Why = "full draw";
			return 1f;
		}
		ItemDrop.ItemData ammoItem = h.GetAmmoItem();
		float num7 = attack.m_projectileAccuracy + (ammoItem?.m_shared?.m_attack?.m_projectileAccuracy).GetValueOrDefault();
		float num8 = attack.m_projectileAccuracyMin + (ammoItem?.m_shared?.m_attack?.m_projectileAccuracyMin).GetValueOrDefault();
		float num9 = Mathf.Atan2(Mathf.Max(0.3f, target.GetRadius()), Mathf.Max(0.5f, num2)) * 57.29578f;
		float num10 = 1f;
		if (num8 > num7 + 0.01f && num9 > num7)
		{
			float num11 = Mathf.Clamp01((num8 - num9) / (num8 - num7));
			num10 = num11 * num11;
		}
		num10 = Mathf.Clamp(num10 * st.Jitter, num, 1f);
		st.Why = ((num10 < 0.99f) ? $"close, {num2:F0} m" : "full draw");
		return num10;
	}

	internal static bool ClearShot(Character me, Character target)
	{
		if (me == null || target == null)
		{
			return true;
		}
		Vector3 centerPoint = target.GetCenterPoint();
		if (ClearLine(me.GetEyePoint(), centerPoint, target))
		{
			return ClearLine(me.transform.position + Vector3.up * 1.3f + me.transform.forward * 0.3f, centerPoint, target);
		}
		return false;
	}

	private static bool ClearLine(Vector3 from, Vector3 to, Character target)
	{
		if (s_blockMask == 0)
		{
			s_blockMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "vehicle");
		}
		Vector3 vector = to - from;
		float magnitude = vector.magnitude;
		if (magnitude < 0.5f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(from, vector / magnitude, magnitude - 0.3f, s_blockMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (raycastHit.collider != null && raycastHit.collider.GetComponentInParent<Character>() == null)
			{
				return false;
			}
		}
		return true;
	}

	private static void LogPlayerBowOnce()
	{
		if (CompanionsPlugin.LogDetailed)
		{
			Player localPlayer = Player.m_localPlayer;
			if (!s_loggedPlayer && localPlayer != null && localPlayer.IsDrawingBow() && localPlayer.GetAttackDrawPercentage() > 0.5f)
			{
				LogBowOnce(localPlayer, "you");
			}
		}
	}

	private static void LogBowOnce(Humanoid h, string who)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		bool flag = who == "you";
		if (flag ? s_loggedPlayer : s_loggedCompanion)
		{
			return;
		}
		if (flag)
		{
			s_loggedPlayer = true;
		}
		else
		{
			s_loggedCompanion = true;
		}
		try
		{
			VisEquipment component = h.GetComponent<VisEquipment>();
			GameObject gameObject = ((component != null) ? s_leftInst(component) : null);
			if (gameObject == null)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[BowDiag] " + who + ": no bow model in the left hand");
				}
				return;
			}
			string text = string.Join(", ", (from c in gameObject.GetComponentsInChildren<Component>(includeInactive: true)
				where c != null && !(c is Transform)
				select c.GetType().Name + "@" + c.gameObject.name).Distinct().Take(30));
			string text2 = string.Join("; ", from a in gameObject.GetComponentsInChildren<Animator>(includeInactive: true)
				select a.gameObject.name + " ctrl=" + ((a.runtimeAnimatorController != null) ? a.runtimeAnimatorController.name : "none") + " enabled=" + a.enabled + " params=[" + string.Join(",", a.parameters.Select((AnimatorControllerParameter x) => x.name)) + "]");
			string text3 = string.Join("; ", from s in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true)
				select s.gameObject.name + " bones=" + (s.bones?.Length ?? 0) + " root=" + ((s.rootBone != null) ? s.rootBone.name : "none"));
			Animator componentInChildren = h.GetComponentInChildren<Animator>();
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo(string.Format("[BowDiag] {0}: bow model \"{1}\" under \"{2}\" | components: {3} | animators: {4} | skinned: {5} | body animator: cull={6} drawpercent={7}", who, gameObject.name, (gameObject.transform.parent != null) ? gameObject.transform.parent.name : "none", text, (text2.Length > 0) ? text2 : "none", (text3.Length > 0) ? text3 : "none", componentInChildren?.cullingMode, (componentInChildren != null) ? componentInChildren.GetFloat("drawpercent").ToString("F2") : "?"));
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[BowDiag] " + who + ": " + ex.Message);
			}
		}
	}
}
