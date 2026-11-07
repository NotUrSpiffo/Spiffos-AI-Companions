using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionCombatAI : MonoBehaviour
{
	private class Reach
	{
		public float At;

		public bool Ok;

		public string Why;
	}

	private const float AlertRange = 9999f;

	private const float InterceptTimeMin = 0f;

	private const float InterceptTimeMax = 4f;

	private const float CircleTargetInterval = 3f;

	private const float CircleTargetDuration = 1f;

	private const float CircleTargetDistance = 4f;

	private const float UpdateWeaponInterval = 1f;

	private const float GiveUpTime = 30f;

	private const float UpdateTargetIntervalNear = 2f;

	private const float UpdateTargetIntervalFar = 6f;

	private const float UnableToAttackDuration = 15f;

	private const float RangedAttackRange = 30f;

	private const float RangedStandoff = 20f;

	private const float RangedFleeRadius = 10f;

	private CompanionAI _ai;

	private Character _character;

	private Humanoid _humanoid;

	private CompanionSetup _setup;

	private Character _targetCreature;

	private StaticTarget _targetStatic;

	private Vector3 _lastKnownTargetPos;

	private bool _beenAtLastPos;

	private float _timeSinceAttacking;

	private float _timeSinceSensedTarget;

	private float _updateTargetTimer;

	private float _updateWeaponTimer;

	private float _interceptTime;

	private float _pauseTimer;

	private float _unableToAttackTargetTimer;

	private float _weaponSwitchCooldown;

	private bool _engaged;

	private Character _recallIgnored;

	private float _recallTimer;

	private const float RecallDuration = 8f;

	private int _stance;

	private float _blockHoldTimer;

	private float _enemySwingTimer;

	private int _parryAttackCount;

	private float _parryGraceTimer;

	private float _logTimer;

	private const float LogInterval = 3f;

	private const float WeaponSwitchCooldown = 4f;

	private Character _huntPrey;

	private bool _stalking;

	private float _nextStalkLog;

	private float _nextHuntMoveLog;

	private float _nextBlockedShotLog;

	private float _nextFleeLog;

	private float _nextCloseLog;

	private float _swingAt = -1f;

	private float _swingWindow;

	private Character _swingTarget;

	private string _swingKind;

	private string _swingName;

	private bool _attackedTarget;

	private CompanionStamina _stam;

	private bool _sneakRest;

	private Character _engageLogged;

	private float _engageLoggedAt;

	private Character _blockAgainst;

	private bool _blockParry;

	private bool _blockRaised;

	private float _reactionTime = 0.2f;

	private string _lastSwingInfo = "";

	private static readonly FieldInfo s_curAttack = AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	private static readonly FieldInfo s_chainLevel = AccessTools.Field(typeof(Attack), "m_currentAttackCainLevel");

	private const float CombatLeash = 40f;

	private const float BowReach = 30f;

	private Vector3 _wanderPoint;

	private float _wanderUntil;

	private float _nextUnsafeLog;

	private bool _tacticsOn;

	private int _burstLeft;

	private bool _burstPending;

	private float _burstPauseUntil;

	private Vector3 _stepDir;

	private float _stepUntil;

	private bool _stepRun;

	private Character _stepFrom;

	private bool _recovering;

	private float _recoverSince;

	private float _recoverStepReady;

	private bool _cornered;

	private float _corneredUntil;

	private float _repositionSince = -1f;

	private float _repositionBlockedUntil;

	private Vector3 _moveDir;

	private float _moveDirUntil;

	private float _swingStartedAt;

	private float _swingLandedAt = -1f;

	private bool _evading;

	private string _lastTacticLog;

	private float _nextTacticLog;

	private readonly List<Character> _attackers = new List<Character>();

	private readonly List<Character> _close = new List<Character>();

	private bool _dipping;

	private float _dipUntil;

	private float _flankSince = -1f;

	private float _flankBlockedUntil;

	private bool _flankLogged;

	private bool _spacingHold;

	private bool _stepToReach = true;

	private static int s_pieceMask = -1;

	private static readonly Collider[] s_pieceHits = new Collider[32];

	private string _wallName;

	private static int s_obstacleMask = -1;

	private float _obstacleCheckAt;

	private string _obstacleName;

	private Character _dmgTarget;

	private float _dmgLastHp;

	private float _perHit;

	private float _lastOwnHitAt = -10f;

	private bool _finishing;

	private int _finishHits;

	private bool _punishing;

	private bool _punishUsed;

	private static readonly FieldInfo s_curAttackT = AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	private bool _awaitingSwing;

	private float _swingStartedTime;

	private Character _ordered;

	private float _holdUntil;

	private readonly Dictionary<Character, float> _ignoreUntil = new Dictionary<Character, float>();

	private readonly HashSet<Character> _letGoUnreachable = new HashSet<Character>();

	private float _liftCheckAt;

	private float _noSightSince = -1f;

	private float _indoorCheckAt;

	private bool _indoors;

	private float _nextTargetLog;

	private Character _latched;

	private string _latchedName;

	private readonly Dictionary<Character, Reach> _reach = new Dictionary<Character, Reach>();

	private Character _stuckOn;

	private float _stuckSince;

	private float _stuckHealth;

	private float _latchedCheckAt;

	internal bool IsEngaged => _engaged;

	internal bool HuntMode
	{
		get
		{
			if (_huntPrey != null)
			{
				return _targetCreature == _huntPrey;
			}
			return false;
		}
		set
		{
			_huntPrey = (value ? _targetCreature : null);
		}
	}

	internal Character HuntPrey => _huntPrey;

	internal bool HasAttackedTarget => _attackedTarget;

	internal bool IsRecalled => _recallTimer > 0f;

	internal Character TargetCreature => _targetCreature;

	private static float Reserve => ModConfig.CombatStaminaReserve?.Value ?? 0.3f;

	private static float Resume => Mathf.Max(Reserve, ModConfig.CombatStaminaResume?.Value ?? 0.5f);

	internal static bool FightLikePlayer => ModConfig.FightLikePlayer?.Value ?? true;

	internal static bool SpacingOn => ModConfig.CombatSpacing?.Value ?? true;

	internal bool WantsChain
	{
		get
		{
			if (!_tacticsOn || _burstLeft <= 0)
			{
				if (LatchedTarget())
				{
					return StaminaNow() >= AttackCost(_humanoid.GetCurrentWeapon());
				}
				return false;
			}
			return true;
		}
	}

	internal bool Holding => Time.time < _holdUntil;

	private static float LeashFromYou => ModConfig.CombatLeashFromYou?.Value ?? 25f;

	private void UpdateStalk()
	{
		int num;
		if (HuntMode)
		{
			ConfigEntry<bool> huntSneak = ModConfig.HuntSneak;
			if ((huntSneak == null || huntSneak.Value) && !_character.IsSwimming())
			{
				num = (Unaware(_targetCreature) ? 1 : 0);
				goto IL_0036;
			}
		}
		num = 0;
		goto IL_0036;
		IL_0036:
		bool flag = (byte)num != 0;
		bool flag2 = false;
		if (flag && Vector3.Distance(transform.position, _targetCreature.transform.position) > (ModConfig.HuntSneakFrom?.Value ?? 20f) + (_stalking ? 2f : 0f))
		{
			flag = false;
			flag2 = true;
		}
		if (flag == _stalking)
		{
			return;
		}
		_stalking = flag;
		_ai.MHR_SetCrouch(flag);
		if (flag)
		{
			_character.SetRun(run: false);
		}
		if (Time.time >= _nextStalkLog)
		{
			_nextStalkLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				ManualLogSource log = CompanionsPlugin.Log;
				string text;
				if (flag)
				{
					text = "Sneaking up on ";
				}
				else
				{
					text = (flag2 ? "Too far off to sneak yet - walking towards " : "Stopped sneaking - spotted by ");
				}
				log.LogDebug("[Hunt] " + text + _targetCreature?.m_name);
			}
		}
	}

	private void StopStalk()
	{
		if (_stalking)
		{
			_stalking = false;
			_ai.MHR_SetCrouch(sneak: false);
		}
	}

	private void Update()
	{
		if (_swingAt >= 0f)
		{
			CheckMiss(force: false);
		}
		MHR_BowDraw.Maintain(_humanoid, Time.deltaTime);
	}

	internal void NoteSwing(Character target, string kind, string detail, float window)
	{
		_attackedTarget = true;
		CheckMiss(force: true);
		_swingAt = Time.time;
		_swingWindow = window;
		_swingTarget = target;
		_swingKind = kind;
		_swingName = ((target != null) ? target.m_name : "?");
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Attack] " + kind + " at " + ((target != null) ? target.m_name : "?") + ": " + detail);
		}
	}

	internal void NoteHit(Character target, HitData hit)
	{
		NoteOwnHit(target);
		if (_swingAt < 0f)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Attack] hit {target.m_name} for {hit.GetTotalDamage():F1}");
			}
			return;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug(string.Format("[Attack] {0} hit {1}{2} for {3:F1} after {4:F2} s", _swingKind, target.m_name, (target != _swingTarget) ? " (not the one aimed at)" : "", hit.GetTotalDamage(), Time.time - _swingAt));
		}
		if (target == _swingTarget)
		{
			_swingAt = -1f;
		}
	}

	private void CheckMiss(bool force)
	{
		if (!(_swingAt < 0f) && (force || !(Time.time - _swingAt < _swingWindow)))
		{
			string text;
			if (_swingTarget != null)
			{
				Vector3 vector = _swingTarget.transform.position - transform.position;
				text = string.Format(" (now {0:F1} m away, {1:+0.0;-0.0} m up, {2})", new Vector2(vector.x, vector.z).magnitude, vector.y, _swingTarget.IsDead() ? "dead - something else killed it" : $"{_swingTarget.GetHealth():F0} hp");
			}
			else
			{
				text = " (it's gone - killed by something else or out of range)";
			}
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Attack] " + _swingKind + " missed " + _swingName + text);
			}
			_swingAt = -1f;
		}
	}

	private bool ChaseMove(float dt, Vector3 point, float dist, bool run)
	{
		if (_tacticsOn && StaminaShare() < Resume + 0.15f)
		{
			run = false;
		}
		if (!_character.IsSwimming() && !IsOrdered(_targetCreature) && MHR_Water.WouldSwim(_character, point))
		{
			_ai.StopMoving();
			if (CompanionsPlugin.LogDetailed)
			{
				UnsafeLog("not swimming out after " + ((_targetCreature != null) ? _targetCreature.m_name : "it") + " - waiting on the bank");
			}
			return false;
		}
		if (!IsOrdered(_targetCreature) && _targetCreature != null && Defensive() && MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()) && BowOnly(_targetCreature, out var _))
		{
			_ai.StopMoving();
			return false;
		}
		bool flag = MHR_Mover.Go(_ai, dt, point, Mathf.Max(dist, 1f), run, IsOrdered(_targetCreature) ? "Order" : "Combat", run ? 3 : 2);
		if (!flag && MHR_Mover.DoorBlocked(_ai))
		{
			DoorInTheWay();
			return false;
		}
		if (!flag && MHR_Mover.GaveUp(_ai))
		{
			return SafeGameMove(dt, point, dist, run);
		}
		return flag;
	}

	private float StaminaShare()
	{
		if (_stam == null)
		{
			_stam = GetComponent<CompanionStamina>();
		}
		if (!(_stam != null) || !(_stam.MaxStamina > 0f))
		{
			return 1f;
		}
		return _stam.Stamina / _stam.MaxStamina;
	}

	private bool HuntMove(float dt, Vector3 point, float dist, bool run)
	{
		if (!HuntMode || _targetCreature == null)
		{
			return false;
		}
		float num = StaminaShare();
		if (_stalking)
		{
			float num2 = ModConfig.HuntSneakRestStamina?.Value ?? 0.3f;
			float num3 = Mathf.Max(num2, ModConfig.HuntSneakResumeStamina?.Value ?? 0.6f);
			if (!_sneakRest && num < num2)
			{
				_sneakRest = true;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Hunt] Out of breath sneaking up on {_targetCreature.m_name} - resting crouched ({num:P0} stamina)");
				}
			}
			else if (_sneakRest && num >= num3)
			{
				_sneakRest = false;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Hunt] Got its breath back ({num:P0}) - sneaking on");
				}
			}
			if (_sneakRest)
			{
				_ai.StopMoving();
				_ai.LookAtPoint(_targetCreature.GetCenterPoint());
				return true;
			}
		}
		else
		{
			_sneakRest = false;
		}
		bool flag = run && num < (ModConfig.HuntChaseRunStamina?.Value ?? 0.3f);
		if (!_stalking)
		{
			ConfigEntry<bool> huntSneak = ModConfig.HuntSneak;
			if ((huntSneak == null || huntSneak.Value) && Unaware(_targetCreature))
			{
				run = false;
				flag = false;
			}
		}
		Vector3 position = transform.position;
		Vector3 goal = point;
		if (MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()) && point.y < position.y - 2.5f)
		{
			if (!EdgeSpot(position, _targetCreature, out var spot))
			{
				LeavePrey("it's down where I can't get a shot from up here");
				return true;
			}
			goal = spot;
			dist = 0.5f;
		}
		MHR_Mover.Go(_ai, dt, goal, Mathf.Max(dist, 1f), run && !flag, "Hunt", flag ? 2 : ((!run) ? 1 : 0));
		if (MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim())
		{
			LeavePrey("I'd have to swim");
		}
		else if (MHR_Mover.GaveUp(_ai))
		{
			LeavePrey("I can't find a way to it");
		}
		return true;
	}

	private void LeavePrey(string why)
	{
		if (Time.time >= _nextHuntMoveLog)
		{
			_nextHuntMoveLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Hunt] Leaving " + ((_targetCreature != null) ? _targetCreature.m_name : "it") + " - " + why);
			}
		}
		MHR_Mover.Release(_ai);
		Disengage();
	}

	private static bool EdgeSpot(Vector3 me, Character prey, out Vector3 spot)
	{
		spot = me;
		Vector3 topPoint = prey.GetTopPoint();
		Vector3 vector = prey.transform.position - me;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 0.5f)
		{
			return false;
		}
		vector /= magnitude;
		Vector3 vector2 = Vector3.zero;
		bool flag = false;
		for (float num = 0f; num < magnitude; num += 0.5f)
		{
			Vector3 vector3 = me + vector * num;
			float num2 = MHR_Water.GroundAt(vector3);
			if (Mathf.Abs(num2 - me.y) > 1.5f || MHR_Water.DepthAt(vector3) > 0.4f)
			{
				break;
			}
			vector3.y = num2;
			if (Vector3.Distance(vector3, topPoint) < 27f && MHR_Mover.CanSee(vector3 + Vector3.up * 1.6f, topPoint))
			{
				vector2 = vector3;
				flag = true;
				break;
			}
			vector2 = vector3;
		}
		if (!flag && vector2 != Vector3.zero && Vector3.Distance(vector2, topPoint) < 27f && MHR_Mover.CanSee(vector2 + Vector3.up * 1.6f, topPoint))
		{
			flag = true;
		}
		spot = vector2;
		return flag;
	}

	private static float MHR_MeleeReach(ItemDrop.ItemData item)
	{
		float a = item.m_shared.m_aiAttackRange;
		Attack attack = item.m_shared.m_attack;
		if (attack != null && attack.m_attackRange > 0f)
		{
			a = Mathf.Min(a, attack.m_attackRange + 0.5f);
		}
		return Mathf.Max(a, 1f);
	}

	internal static bool MHR_IsProjectileWeapon(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		switch (item.m_shared.m_itemType)
		{
		case ItemDrop.ItemData.ItemType.Consumable:
		case ItemDrop.ItemData.ItemType.Ammo:
		case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
			return false;
		default:
			if (string.IsNullOrEmpty(item.m_shared.m_ammoType))
			{
				if (item.m_shared.m_attack != null)
				{
					return item.m_shared.m_attack.m_attackType == Attack.AttackType.Projectile;
				}
				return false;
			}
			goto case ItemDrop.ItemData.ItemType.Bow;
		case ItemDrop.ItemData.ItemType.Bow:
			return true;
		}
	}

	private static bool Unaware(Character c)
	{
		if (c == null)
		{
			return false;
		}
		BaseAI baseAI = c.GetBaseAI();
		if (baseAI == null || baseAI.IsAlerted() || baseAI.GetTargetCreature() != null)
		{
			return false;
		}
		MonsterAI monsterAI = baseAI as MonsterAI;
		if (!(baseAI is AnimalAI))
		{
			if (monsterAI != null)
			{
				return monsterAI.m_fleeIfNotAlerted;
			}
			return false;
		}
		return true;
	}

	internal void EngagePrey(Character prey)
	{
		Engage(prey);
		if (_engaged && _targetCreature == prey)
		{
			_huntPrey = prey;
		}
	}

	internal void Engage(Character target)
	{
		if (target == null || target.GetHealth() <= 0f)
		{
			return;
		}
		_stance = _setup?.GetCombatStance() ?? 0;
		if (_stance == 1 && !MHR_Recover.MayDefend(this, target))
		{
			return;
		}
		_targetCreature = target;
		_targetStatic = null;
		_lastKnownTargetPos = target.transform.position;
		_beenAtLastPos = false;
		_timeSinceAttacking = 0f;
		_timeSinceSensedTarget = 0f;
		_updateTargetTimer = 0f;
		_updateWeaponTimer = 0f;
		_weaponSwitchCooldown = 0f;
		_pauseTimer = Random.Range(0f, 3f);
		_interceptTime = Random.Range(0f, 4f);
		_unableToAttackTargetTimer = 0f;
		_parryAttackCount = 0;
		_engaged = true;
		_attackedTarget = false;
		Character character = target.GetBaseAI()?.GetTargetCreature();
		if (target != _engageLogged || Time.time - _engageLoggedAt > 30f)
		{
			_engageLogged = target;
			_engageLoggedAt = Time.time;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug(string.Format("[Combat] Engage -> target=\"{0}\" stance={1} dist={2:F1}m its target=\"{3}\" alerted={4}", target.m_name, _stance, Vector3.Distance(transform.position, target.transform.position), (character != null) ? character.m_name : "none", target.GetBaseAI()?.IsAlerted() ?? false));
			}
		}
		_ai.SetAlerted(alert: true);
	}

	internal bool UpdateCombat(float dt)
	{
		CheckMiss(force: false);
		if (WaterSafety.ShouldAvoidChase(_humanoid, _targetCreature))
		{
			return false;
		}
		if (!_engaged)
		{
			return false;
		}
		if (_humanoid == null)
		{
			return false;
		}
		_stance = _setup?.GetCombatStance() ?? 0;
		if (_stance == 1 && !MHR_Recover.MayDefend(this, _targetCreature))
		{
			Disengage();
			return false;
		}
		UpdateTarget(dt, out var canHearTarget, out var canSeeTarget);
		if (_targetCreature == null && _targetStatic == null)
		{
			Disengage();
			return false;
		}
		_ai.m_targetCreature = _targetCreature;
		_ai.m_targetStatic = _targetStatic;
		if (HuntMode && MHR_SpearThrow.UnarmedAfterThrow(_ai, _humanoid))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Hunt] Threw its only weapon at " + _targetCreature?.m_name + " - going to pick the spear up first");
			}
			StopStalk();
			Disengage();
			return false;
		}
		UpdateStalk();
		_tacticsOn = TacticsFor(_humanoid.GetCurrentWeapon());
		UpdateBlocking(dt);
		float healthPercentage = _character.GetHealthPercentage();
		bool flag = Latched() != null;
		if (flag && healthPercentage < (ModConfig.FleeHealthPct?.Value ?? 0.15f) && Time.time >= _nextFleeLog)
		{
			_nextFleeLog = Time.time + 5f;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Flee] hp={healthPercentage:P0} but {Latched().m_name} is latched on - running won't shake it, fighting it");
			}
		}
		if (!flag && healthPercentage < (ModConfig.FleeHealthPct?.Value ?? 0.15f) && _targetCreature != null && Player.m_localPlayer != null)
		{
			bool flag2 = Time.time >= _nextFleeLog;
			if (flag2)
			{
				_nextFleeLog = Time.time + 5f;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo($"[Flee] triggered -> hp={healthPercentage:P0} (below flee threshold) target=\"{_targetCreature.m_name}\"");
				}
			}
			TryEquipShield();
			if (FleeToOwner(dt) || SafeFlee(dt, _targetCreature.transform.position))
			{
				return true;
			}
			if (flag2 && CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Flee] could not create distance (blocked/cornered) -> defending itself, will keep trying to flee");
			}
		}
		_logTimer += dt;
		if (_logTimer >= 3f)
		{
			_logTimer = 0f;
			if (_targetCreature != null)
			{
				_ = _targetCreature.m_name;
			}
			if (_targetCreature != null)
			{
				Vector3.Distance(transform.position, _targetCreature.transform.position);
			}
			_ = _humanoid.GetCurrentWeapon()?.m_shared?.m_name;
		}
		bool blocking = ReflectionHelper.GetBlocking(_character);
		bool flag3 = _stance == 0 || _stance == 3;
		if (_targetCreature != null && !blocking && _parryAttackCount <= 0 && !HuntMode && !flag3 && !_tacticsOn)
		{
			_pauseTimer += dt;
			if (_pauseTimer > 3f)
			{
				if (_pauseTimer > 4f)
				{
					_pauseTimer = Random.Range(0f, 0.3f);
				}
				SafeWander(dt, _targetCreature.transform.position, 4f, _ai.IsAlerted());
				return true;
			}
		}
		ItemDrop.ItemData itemData = SelectBestAttack(dt);
		bool tacticsOn = _tacticsOn;
		_tacticsOn = TacticsFor(itemData);
		if (tacticsOn && !_tacticsOn)
		{
			ResetTactics();
		}
		float num = ((itemData != null && itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) ? 0.5f : 0f);
		bool flag4 = itemData != null && Time.time - itemData.m_lastAttackTime > itemData.m_shared.m_aiAttackInterval + num;
		float value = ModConfig.AttackCooldown.Value;
		bool flag5 = _character.GetTimeSinceLastAttack() >= value;
		bool flag6 = _parryAttackCount > 0;
		bool flag7 = itemData != null && !_ai.IsTakingOff() && (_tacticsOn ? ReadyToSwing(itemData) : (flag6 || (flag4 & flag5)));
		if (((!_ai.IsCharging() && (_targetStatic != null || _targetCreature != null) && itemData != null) & flag7) && !_character.InAttack() && itemData.m_shared.m_attack != null && !itemData.m_shared.m_attack.IsDone() && !string.IsNullOrEmpty(itemData.m_shared.m_attack.m_chargeAnimationBool))
		{
			_ai.ChargeStart(itemData.m_shared.m_attack.m_chargeAnimationBool);
		}
		bool flag8 = ((itemData == null) ? (_stance == 2) : MHR_IsProjectileWeapon(itemData));
		if (_targetCreature != null && itemData != null && !flag7 && !_character.InAttack() && !flag8 && !HuntMode && !flag3 && !_tacticsOn)
		{
			Vector3 position = _targetCreature.transform.position;
			SafeWander(dt, position, (_ai.m_randomMoveRange > 0f) ? _ai.m_randomMoveRange : 5f, _ai.IsAlerted());
			return true;
		}
		if ((_targetStatic == null && _targetCreature == null) || itemData == null)
		{
			_ai.ChargeStop();
			Disengage();
			return false;
		}
		if (itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Enemy)
		{
			if (_targetCreature != null)
			{
				if (canHearTarget | canSeeTarget)
				{
					_beenAtLastPos = false;
					_lastKnownTargetPos = _targetCreature.transform.position;
					float num2 = Vector3.Distance(_lastKnownTargetPos, transform.position) - _targetCreature.GetRadius();
					float num3 = 9999f * _targetCreature.GetStealthFactor();
					if (canSeeTarget && num2 < num3)
					{
						_ai.SetAlerted(alert: true);
					}
					bool flag9 = flag8;
					float num4 = ModConfig.CombatBowEdgeMargin?.Value ?? 3f;
					float num5;
					if (flag9)
					{
						num5 = (MHR_BowDraw.IsDrawing(_humanoid) ? (30f + num4 * 2f) : (30f - num4));
					}
					else
					{
						num5 = MHR_MeleeReach(itemData);
					}
					if (!flag9)
					{
						num5 = Mathf.Max(0.8f, num5 - (ModConfig.MeleeReachMargin?.Value ?? 0.2f));
					}
					bool flag10 = num2 < num5;
					float num6 = _targetCreature.transform.position.y - transform.position.y;
					bool flag11 = LatchedTarget();
					if (!flag9 && !flag11 && (num6 > 1.4f || num6 < -1.8f))
					{
						flag10 = false;
					}
					if (!flag9 && num6 > 0.5f && MHR_Mover.OnLadderChain(_ai))
					{
						flag10 = false;
					}
					float dist = (flag9 ? 20f : 0f);
					if (!flag9 && HuntMode && MHR_SpearThrow.ReadyToThrow(_ai, _humanoid, _targetCreature))
					{
						dist = MHR_SpearThrow.Standoff();
					}
					bool flag12 = !flag10 || !canSeeTarget || !_ai.IsAlerted() || (!flag9 && itemData.m_shared.m_aiAttackRangeMin < 0f);
					int num7 = ((_tacticsOn && !flag9) ? Tactics(dt, itemData) : 0);
					if (num7 == 0)
					{
						if (((flag9 && !_character.InAttack() && num2 < 10f) & canSeeTarget) && _ai.IsAlerted() && !MHR_BowDraw.IsResting(_humanoid))
						{
							SafeFlee(dt, _targetCreature.transform.position);
						}
						else if ((flag9 & flag10 & canSeeTarget) && MHR_BowDraw.NeedsDraw(_humanoid))
						{
							if (num2 > 30f)
							{
								ChaseMove(dt, _targetCreature.transform.position, 30f - num4, run: false);
							}
							else if (MHR_BowDraw.ClearShot(_character, _targetCreature))
							{
								_ai.StopMoving();
							}
							else
							{
								if (Time.time >= _nextBlockedShotLog)
								{
									_nextBlockedShotLog = Time.time + 5f;
									if (CompanionsPlugin.LogDetailed)
									{
										CompanionsPlugin.Log.LogDebug($"[Bow] Something is in the way of a shot at {_targetCreature.m_name} ({num2:F1} m) - moving closer");
									}
								}
								ChaseMove(dt, _targetCreature.transform.position, Mathf.Max(5f, num2 - 4f), run: false);
							}
						}
						else if ((flag9 & flag10 & canSeeTarget) && _ai.IsAlerted() && !flag12)
						{
							SafeWander(dt, _targetCreature.transform.position, 20f, run: true);
						}
						else if (flag12)
						{
							Vector3 velocity = _targetCreature.GetVelocity();
							Vector3 vector = velocity * _interceptTime;
							Vector3 lastKnownTargetPos = _lastKnownTargetPos;
							if (num2 > vector.magnitude / 4f)
							{
								lastKnownTargetPos += velocity * _interceptTime;
							}
							if (!HuntMove(dt, lastKnownTargetPos, dist, !_stalking))
							{
								ChaseMove(dt, lastKnownTargetPos, dist, !_stalking);
							}
							if (_timeSinceAttacking > 15f)
							{
								_unableToAttackTargetTimer = 15f;
							}
						}
						else
						{
							_ai.StopMoving();
						}
					}
					if ((HuntMode & canSeeTarget) && !blocking && MHR_SpearThrow.WantsThrow(_ai, _humanoid, _targetCreature, num2))
					{
						_ai.StopMoving();
						_ai.LookAtPoint(_targetCreature.GetCenterPoint());
						if (_ai.IsLookingAtPoint(_targetCreature.GetCenterPoint(), 10f))
						{
							MHR_SpearThrow.Throw(_ai, _humanoid, _targetCreature);
						}
					}
					else if ((flag10 & canSeeTarget) && _ai.IsAlerted() && !blocking && num7 != 1)
					{
						Vector3 centerPoint = _targetCreature.GetCenterPoint();
						_ai.LookAtPoint(centerPoint);
						float num8 = ModConfig.MeleeTooClose?.Value ?? 0.3f;
						Attack attack = itemData.m_shared.m_attack;
						if (!flag9 && !flag11 && num8 > 0f && num2 < num8 && !_character.InAttack() && (attack == null || attack.m_attackAngle < 40f))
						{
							Vector3 dir = transform.position - _targetCreature.transform.position;
							dir.y = 0f;
							if (dir.sqrMagnitude < 0.01f)
							{
								dir = -transform.forward;
							}
							if (MHR_Mover.SafeStep(transform.position, dir, 1f))
							{
								_character.SetMoveDir(dir.normalized);
							}
							_character.SetRun(run: false);
							_ai.LookAtPoint(centerPoint);
							if (Time.time >= _nextCloseLog)
							{
								_nextCloseLog = Time.time + 5f;
								if (CompanionsPlugin.LogDetailed)
								{
									CompanionsPlugin.Log.LogDebug($"[Combat] Too close to swing at {_targetCreature.m_name} ({num2:F1} m) - stepping back");
								}
							}
						}
						else
						{
							float num9 = itemData.m_shared.m_aiAttackMaxAngle;
							Attack attack2 = itemData.m_shared.m_attack;
							if (!flag9 && attack2 != null && attack2.m_attackAngle < 40f)
							{
								num9 = Mathf.Min(num9, Mathf.Max(8f, attack2.m_attackAngle * 0.5f + 5f));
							}
							else if (!flag9 && _tacticsOn && attack2 != null)
							{
								num9 = Mathf.Max(num9, Mathf.Min(30f, attack2.m_attackAngle * 0.3f));
							}
							bool flag13 = MHR_BowDraw.NeedsDraw(_humanoid);
							bool flag14 = (flag13 ? flag5 : flag7);
							float angle = (flag13 ? Mathf.Max(num9, 45f) : num9);
							if (flag14 && (flag11 || _ai.IsLookingAtPoint(_lastKnownTargetPos, angle, itemData.m_shared.m_aiInvertAngleCheck)) && DoAttack(_targetCreature))
							{
								if (_tacticsOn)
								{
									NoteTacticSwing(itemData);
								}
								Vector3 vector2 = _targetCreature.transform.position - transform.position;
								Vector3 to = new Vector3(vector2.x, 0f, vector2.z);
								Vector3 forward = transform.forward;
								forward.y = 0f;
								NoteSwing(_targetCreature, flag9 ? "Shot" : "Swing", $"{itemData.m_shared.m_name}, {num2:F1} m from its edge, {vector2.y:+0.0;-0.0} m up, facing {Vector3.Angle(forward, to):F0} deg off (limit {num9:F0}), reach {num5:F1} m, " + ((attack2 != null) ? $"sweep {attack2.m_attackAngle:F0} deg wide ({attack2.m_attackType}), tilt up to {attack2.m_maxYAngle:F0} deg, weapon range {attack2.m_attackRange:F2} m, AI range {itemData.m_shared.m_aiAttackRange:F2} m, hit width {attack2.m_attackRayWidth:F2} m (+{attack2.m_attackRayWidthCharExtra:F2} vs creatures), hit height {attack2.m_attackHeight:F2} m" : "no attack data") + ", " + _lastSwingInfo + (_stalking ? ", sneaking" : "") + ((_targetCreature.GetBaseAI() != null && !_targetCreature.GetBaseAI().IsAlerted()) ? ", it hasn't noticed" : ""), flag9 ? 4f : 1.2f);
								if (_parryAttackCount > 0)
								{
									_parryAttackCount--;
								}
							}
						}
					}
				}
				else
				{
					_ai.ChargeStop();
					float num10 = (flag8 ? 20f : 0f);
					if (_beenAtLastPos)
					{
						SafeWander(dt, _lastKnownTargetPos, 5f, run: false);
						if (_timeSinceAttacking > 15f)
						{
							_unableToAttackTargetTimer = 15f;
						}
					}
					else
					{
						bool num11;
						if (!HuntMode)
						{
							num11 = ChaseMove(dt, _lastKnownTargetPos, num10, !_stalking);
						}
						else
						{
							if (!HuntMove(dt, _lastKnownTargetPos, num10, !_stalking))
							{
								goto IL_1154;
							}
							num11 = Vector3.Distance(transform.position, _lastKnownTargetPos) <= num10 + 1.5f;
						}
						if (num11)
						{
							_beenAtLastPos = true;
						}
					}
				}
			}
		}
		else if (itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.FriendHurt || itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Friend)
		{
			Character character = ((itemData.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.FriendHurt) ? _ai.HaveHurtFriendInRange(_ai.m_viewRange) : _ai.HaveFriendInRange(_ai.m_viewRange));
			if (character != null)
			{
				if (Vector3.Distance(character.transform.position, transform.position) < itemData.m_shared.m_aiAttackRange)
				{
					if (flag7)
					{
						_ai.StopMoving();
						_ai.LookAtPoint(character.transform.position);
						DoAttack(character);
					}
					else
					{
						SafeWander(dt, character.transform.position, 5f, run: false);
					}
				}
				else
				{
					ChaseMove(dt, character.transform.position, 0f, run: true);
				}
			}
			else
			{
				SafeWander(dt, transform.position, 5f, run: false);
			}
		}
		goto IL_1154;
		IL_1154:
		return true;
	}

	internal void OnDamaged(Character attacker)
	{
		if (attacker == null || (attacker.IsPlayer() && _character.IsTamed()))
		{
			return;
		}
		if (_recallTimer > 0f)
		{
			ClearRecall();
		}
		if (_recallIgnored != null && attacker == _recallIgnored)
		{
			_recallIgnored = null;
		}
		if (_engaged && (_targetCreature == null || _targetCreature.GetHealth() <= 0f || (HuntMode && attacker != _targetCreature)))
		{
			if (HuntMode && attacker != _targetCreature && CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Combat] " + attacker.m_name + " attacked while hunting " + _targetCreature.m_name + " - fighting it first");
			}
			_targetCreature = attacker;
			_lastKnownTargetPos = attacker.transform.position;
			_beenAtLastPos = false;
			_targetStatic = null;
			_timeSinceSensedTarget = 0f;
		}
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
		_setup = GetComponent<CompanionSetup>();
	}

	internal void Disengage()
	{
		if (_engaged)
		{
			if (_targetCreature != null)
			{
				_ = _targetCreature.m_name;
			}
			_targetCreature = null;
			_targetStatic = null;
			_engaged = false;
			_ai.m_targetCreature = null;
			_ai.m_targetStatic = null;
			_ai.SetAlerted(alert: false);
			_ai.StopMoving();
			_ai.ChargeStop();
			ClearBlock();
			_parryAttackCount = 0;
			HuntMode = false;
			StopStalk();
			ResetTactics();
			_ordered = null;
			_noSightSince = -1f;
		}
	}

	private void ClearBlock()
	{
		_enemySwingTimer = 0f;
		_blockAgainst = null;
		if (ReflectionHelper.GetBlocking(_character))
		{
			ReflectionHelper.TrySetBlocking(_character, value: false);
		}
		_blockHoldTimer = 0f;
	}

	internal void Recall()
	{
		_recallIgnored = _targetCreature;
		_recallTimer = 8f;
		if (_engaged)
		{
			Disengage();
			return;
		}
		_targetCreature = null;
		_targetStatic = null;
		_ai.m_targetCreature = null;
		_ai.m_targetStatic = null;
		_ai.SetAlerted(alert: false);
	}

	internal void ClearRecall()
	{
		_recallTimer = 0f;
		_recallIgnored = null;
	}

	private void UpdateTarget(float dt, out bool canHearTarget, out bool canSeeTarget)
	{
		_unableToAttackTargetTimer -= dt;
		_updateTargetTimer -= dt;
		if (_recallTimer > 0f)
		{
			_recallTimer -= dt;
		}
		CheckTargetStillValid();
		Character character = (HuntMode ? null : Latched());
		if (character != null && character != _targetCreature && !character.IsDead())
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Combat] " + character.m_name + " is latched on - turning on it" + ((_targetCreature != null) ? (" (was fighting " + _targetCreature.m_name + ")") : ""));
			}
			_targetCreature = character;
			_noSightSince = -1f;
		}
		if (_updateTargetTimer <= 0f && !_character.InAttack())
		{
			bool flag = Player.IsPlayerInRange(transform.position, 50f);
			_updateTargetTimer = (flag ? 1f : 6f);
			Character character2 = (HuntMode ? _ai.FindNearbyEnemy() : PickTarget());
			if (character2 != null && IsPassiveAnimal(character2))
			{
				character2 = null;
			}
			if (character2 != null && character2 != _targetCreature && !MHR_Threat.IsAfterUs(character2))
			{
				character2 = null;
			}
			if (_recallIgnored != null)
			{
				if (!_recallIgnored || _recallIgnored.GetHealth() <= 0f)
				{
					_recallIgnored = null;
				}
				else if (character2 == _recallIgnored)
				{
					character2 = null;
				}
				else if (character2 != null)
				{
					_recallIgnored = null;
				}
			}
			if (character2 != null)
			{
				_ = _targetCreature != character2;
				_targetCreature = character2;
				_targetStatic = null;
			}
		}
		if (_targetCreature != null && _character.IsTamed())
		{
			bool flag2 = false;
			Vector3 b;
			if (_ai.GetPatrolPoint(out var point))
			{
				b = point;
				flag2 = true;
			}
			else
			{
				GameObject followTarget = _ai.GetFollowTarget();
				if (followTarget != null)
				{
					b = followTarget.transform.position;
					flag2 = true;
				}
				else
				{
					b = Vector3.zero;
				}
			}
			if (flag2 && Vector3.Distance(_targetCreature.transform.position, b) > 9999f)
			{
				_targetCreature = null;
			}
		}
		if (_targetCreature != null)
		{
			ZNetView component = _targetCreature.GetComponent<ZNetView>();
			if (component != null && !component.IsValid())
			{
				_targetCreature = null;
			}
			else if (_targetCreature.GetHealth() <= 0f)
			{
				_targetCreature = null;
			}
			else if (!HuntMode && !_ai.IsEnemy(_targetCreature))
			{
				_targetCreature = null;
			}
			else if (!HuntMode && Vector3.Distance(transform.position, _targetCreature.transform.position) > 40f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Combat] {_targetCreature.m_name} is over {40f:F0} m away - letting it go");
				}
				_targetCreature = null;
			}
			else if (!HuntMode && IsPassiveAnimal(_targetCreature) && _targetCreature.GetBaseAI()?.GetTargetCreature() == null)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] " + _targetCreature.m_name + " isn't a threat - leaving it");
				}
				_targetCreature = null;
			}
		}
		canHearTarget = false;
		canSeeTarget = false;
		if (_targetCreature != null)
		{
			canHearTarget = _ai.CanHearTarget(_targetCreature);
			canSeeTarget = _ai.CanSeeTarget(_targetCreature) || LatchedTarget();
			if (canSeeTarget | canHearTarget)
			{
				_timeSinceSensedTarget = 0f;
			}
		}
		_timeSinceSensedTarget += dt;
		if (!_ai.IsAlerted() && !(_targetCreature != null))
		{
			return;
		}
		_timeSinceAttacking += dt;
		float num = (HuntMode ? 180f : 60f);
		if (_timeSinceSensedTarget > 30f || _timeSinceAttacking > num)
		{
			if (_targetCreature != null && CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Combat] Giving up on " + _targetCreature.m_name + ((_timeSinceSensedTarget > 30f) ? " - lost sight and sound of it for 30 s" : $" - no attack in {num:F0} s"));
			}
			_ai.SetAlerted(alert: false);
			_targetCreature = null;
			_targetStatic = null;
			_timeSinceAttacking = 0f;
			_updateTargetTimer = 5f;
		}
	}

	private float ParryChance()
	{
		float a = ModConfig.ParryChance?.Value ?? 0.3f;
		float b = Mathf.Max(a, ModConfig.ParryChanceMax?.Value ?? 0.7f);
		return Mathf.Lerp(a, b, Mathf.Clamp01(_character.GetSkillFactor(Skills.SkillType.Blocking)));
	}

	private Character SwingAtUs()
	{
		Character result = null;
		float num = float.MaxValue;
		Vector3 position = transform.position;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == null || allCharacter.GetHealth() <= 0f || !allCharacter.InAttack() || !BaseAI.IsEnemy(_character, allCharacter))
			{
				continue;
			}
			float num2 = Vector3.Distance(position, allCharacter.transform.position);
			if (num2 > 8f || num2 >= num)
			{
				continue;
			}
			float num3 = SwingReach(allCharacter);
			if (num2 - allCharacter.GetRadius() - _character.GetRadius() > num3 + 1f)
			{
				continue;
			}
			if (allCharacter.GetBaseAI()?.GetTargetCreature() != _character)
			{
				Vector3 to = position - allCharacter.transform.position;
				to.y = 0f;
				Vector3 forward = allCharacter.transform.forward;
				forward.y = 0f;
				if (Vector3.Angle(forward, to) > 60f)
				{
					continue;
				}
			}
			result = allCharacter;
			num = num2;
		}
		return result;
	}

	private bool TorchIsBlocker()
	{
		ItemDrop.ItemData itemData = ReflectionHelper.GetLeftItem(_humanoid) ?? ReflectionHelper.GetRightItem(_humanoid);
		if (itemData != null && itemData.m_shared != null)
		{
			return itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch;
		}
		return false;
	}

	private void UpdateBlocking(float dt)
	{
		if (Latched() != null)
		{
			ClearBlock();
			return;
		}
		if (_targetCreature == null || _character.InAttack() || _character.IsStaggering() || _parryAttackCount > 0 || (_tacticsOn && _burstLeft > 0))
		{
			ClearBlock();
			return;
		}
		Character character = SwingAtUs();
		if (character != null)
		{
			if (character != _blockAgainst)
			{
				_blockAgainst = character;
				_enemySwingTimer = 0f;
				_blockRaised = false;
				_evading = false;
				_swingStartedAt = Time.time;
				_blockParry = Random.value < ParryChance();
				_reactionTime = (ModConfig.BlockReactionTime?.Value ?? 0.2f) * Random.Range(0.75f, 1.5f);
			}
			if (_evading)
			{
				return;
			}
			if (_tacticsOn && character == _blockAgainst && _swingLandedAt >= _swingStartedAt && !character.IsStaggering())
			{
				if (ReflectionHelper.GetBlocking(_character))
				{
					ReflectionHelper.TrySetBlocking(_character, value: false);
				}
				_blockHoldTimer = 0f;
				return;
			}
			_ai.LookAtPoint(character.GetTopPoint());
			_enemySwingTimer += dt;
			if (_enemySwingTimer >= _reactionTime && TorchIsBlocker())
			{
				if (!_blockRaised)
				{
					_blockRaised = true;
					if (TryEvade(character, always: true))
					{
						_evading = true;
						return;
					}
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Block] Only a torch to block " + character.m_name + " with and no room to step out - not blocking");
					}
				}
				if (ReflectionHelper.GetBlocking(_character))
				{
					ReflectionHelper.TrySetBlocking(_character, value: false);
				}
			}
			else
			{
				if (!(_enemySwingTimer >= _reactionTime))
				{
					return;
				}
				if (!_blockRaised && _tacticsOn && TryEvade(character))
				{
					_blockRaised = true;
					_evading = true;
					return;
				}
				ReflectionHelper.TrySetBlocking(_character, value: true);
				if (!_blockRaised)
				{
					_blockRaised = true;
					ReflectionHelper.TrySetBlockTimer(_humanoid, _blockParry ? 0f : 0.3f);
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Block] Raising its guard against " + character.m_name + (_blockParry ? " - timed for a parry" : " - a plain block"));
					}
				}
				else if (_blockParry)
				{
					ReflectionHelper.TrySetBlockTimer(_humanoid, 0f);
				}
				_blockHoldTimer = 0.2f;
			}
			return;
		}
		_blockAgainst = null;
		if (_blockHoldTimer > 0f)
		{
			_blockHoldTimer -= dt;
			ReflectionHelper.TrySetBlocking(_character, value: true);
			_enemySwingTimer = 0f;
			return;
		}
		if (_blockHoldTimer < 0f)
		{
			_parryGraceTimer = 0.15f;
		}
		if (_parryGraceTimer > 0f)
		{
			_parryGraceTimer -= dt;
			if (_targetCreature != null && _targetCreature.IsStaggering() && !_tacticsOn)
			{
				_parryAttackCount = 2;
				_parryGraceTimer = 0f;
			}
		}
		ClearBlock();
	}

	private bool TryEquipShield()
	{
		ItemDrop.ItemData itemData = ShieldLogic.SelectShield(_humanoid, _humanoid.GetComponent<Character>());
		if (itemData == null)
		{
			return false;
		}
		_humanoid.EquipItem(itemData);
		return true;
	}

	private ItemDrop.ItemData SelectBestAttack(float dt)
	{
		if (_targetCreature != null || _targetStatic != null)
		{
			_updateWeaponTimer -= dt;
			if (_weaponSwitchCooldown > 0f)
			{
				_weaponSwitchCooldown -= dt;
			}
			if (_updateWeaponTimer <= 0f && !_character.InAttack() && _weaponSwitchCooldown <= 0f)
			{
				_updateWeaponTimer = 1f;
				if (!TorchOpener.TrySelect(_humanoid, _targetCreature, _stance, HuntMode))
				{
					string text = _humanoid.GetCurrentWeapon()?.m_shared?.m_name;
					bool flag = false;
					ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
					if (_stance == 2 && !HuntMode && RangedWantsMelee(currentWeapon))
					{
						if (currentWeapon == null || MHR_IsProjectileWeapon(currentWeapon))
						{
							_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend, out var friend);
							_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend, friend);
							TryForceMeleeWeapon();
							flag = true;
							if (CompanionsPlugin.LogDetailed)
							{
								CompanionsPlugin.Log.LogDebug("[Ranged] " + _targetCreature?.m_name + " is in close - melee weapon out (" + (_humanoid.GetCurrentWeapon()?.m_shared?.m_name ?? "none") + ")");
							}
						}
					}
					else if (_stance == 2 || HuntMode)
					{
						if (currentWeapon == null || currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow)
						{
							if (!TryForceRangedWeapon() && (!HuntMode || !MHR_SpearThrow.EquipForHunt(_ai, _humanoid, _huntPrey)))
							{
								_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend2, out var friend2);
								_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend2, friend2);
								TryForceMeleeWeapon();
							}
							flag = true;
						}
					}
					else if (!HuntMode && _stance == 3)
					{
						if (currentWeapon == null || (currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon && currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon && currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) || currentWeapon.GetDamage().m_pickaxe != 0f || !(GetCombatDamage(currentWeapon) > 0f))
						{
							_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend3, out var friend3);
							_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend3, friend3);
							TryForceMeleeWeapon();
							flag = true;
						}
					}
					else
					{
						_ai.HaveFriendsInRange(_ai.m_viewRange, out var hurtFriend4, out var friend4);
						_humanoid.EquipBestWeapon(_targetCreature, _targetStatic, hurtFriend4, friend4);
					}
					TryEquipShield();
					if ((_humanoid.GetCurrentWeapon()?.m_shared?.m_name != text) | flag)
					{
						_weaponSwitchCooldown = 4f;
					}
				}
			}
		}
		return _humanoid.GetCurrentWeapon();
	}

	private string ComboInfo(Attack shared)
	{
		try
		{
			if (!(s_curAttack?.GetValue(_humanoid) is Attack obj) || s_chainLevel == null || shared == null || shared.m_attackChainLevels <= 1)
			{
				return "no combo";
			}
			int num = (int)s_chainLevel.GetValue(obj);
			return $"combo swing {num + 1}/{shared.m_attackChainLevels}" + ((num == shared.m_attackChainLevels - 1) ? $" (the finisher, x{shared.m_lastChainDamageMultiplier:F1} damage)" : "");
		}
		catch
		{
			return "?";
		}
	}

	private bool DoAttack(Character target)
	{
		ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
		if (currentWeapon == null)
		{
			return false;
		}
		if (!_ai.CanUseAttack(currentWeapon))
		{
			return false;
		}
		Attack shared = currentWeapon.m_shared?.m_attack;
		float timeSinceLastAttack = _character.GetTimeSinceLastAttack();
		bool flag;
		if (MHR_BowDraw.NeedsDraw(_humanoid))
		{
			float aiAttackMaxAngle = currentWeapon.m_shared.m_aiAttackMaxAngle;
			bool aimed = target == null || _ai.IsLookingAtPoint(target.GetCenterPoint(), aiAttackMaxAngle, currentWeapon.m_shared.m_aiInvertAngleCheck);
			flag = MHR_BowDraw.Tick(_humanoid, target, aimed, MHR_BowDraw.ClearShot(_character, target));
		}
		else
		{
			if (target != null && MaybeClearOfObstacle(currentWeapon))
			{
				return false;
			}
			flag = _character.StartAttack(target, charge: false);
		}
		if (flag)
		{
			_lastSwingInfo = ComboInfo(shared) + $", {timeSinceLastAttack:F2} s after the last swing ended";
		}
		if (flag)
		{
			_timeSinceAttacking = 0f;
		}
		return flag;
	}

	private static bool IsPassiveAnimal(Character c)
	{
		return MHR_Threat.IsPassive(c);
	}

	private bool RangedWantsMelee(ItemDrop.ItemData current)
	{
		if (_targetCreature == null)
		{
			return false;
		}
		float num = ModConfig.CombatRangedMeleeRange?.Value ?? 4f;
		float num2 = Vector3.Distance(transform.position, _targetCreature.transform.position) - _targetCreature.GetRadius();
		bool flag = current != null && !MHR_IsProjectileWeapon(current);
		if (num2 <= num)
		{
			return true;
		}
		if (num2 <= num * 2f)
		{
			if (!flag)
			{
				return MHR_BowDraw.IsResting(_humanoid);
			}
			return true;
		}
		return false;
	}

	private bool TryForceRangedWeapon()
	{
		ItemDrop.ItemData itemData = WeaponLogic.FindReadyBow(_humanoid, _targetCreature);
		if (itemData != null)
		{
			_humanoid.EquipItem(itemData);
			return true;
		}
		return false;
	}

	private bool TryForceMeleeWeapon()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		ItemDrop.ItemData itemData = null;
		float num = -1f;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (!allItem.IsEquipable() || MHR_IsProjectileWeapon(allItem))
			{
				continue;
			}
			ItemDrop.ItemData.ItemType itemType = allItem.m_shared.m_itemType;
			if ((itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft) && !(allItem.GetDamage().m_pickaxe > 0f) && (!(allItem.GetDamage().m_chop > 0f) || allItem.GetDamage().m_slash != 0f || allItem.GetDamage().m_blunt != 0f || allItem.GetDamage().m_pierce != 0f) && itemType != ItemDrop.ItemData.ItemType.Tool && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)))
			{
				float combatDamage = GetCombatDamage(allItem);
				if (combatDamage > num)
				{
					num = combatDamage;
					itemData = allItem;
				}
			}
		}
		if (itemData != null)
		{
			_humanoid.EquipItem(itemData);
			return true;
		}
		return false;
	}

	private static float GetCombatDamage(ItemDrop.ItemData item)
	{
		return WeaponLogic.Score(item, null);
	}

	private bool SafeGameMove(float dt, Vector3 point, float dist, bool run)
	{
		Vector3 position = transform.position;
		Vector3 vector = point - position;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude <= Mathf.Max(dist, 0.5f))
		{
			_ai.StopMoving();
			return true;
		}
		if (!MHR_Mover.SafeStep(position, vector / magnitude, Mathf.Min(2f, magnitude)))
		{
			_ai.StopMoving();
			if (CompanionsPlugin.LogDetailed)
			{
				UnsafeLog("not walking on towards " + ((_targetCreature != null) ? _targetCreature.m_name : "it") + " - a drop, water, fire or spikes in the way");
			}
			return false;
		}
		return _ai.GameMoveTo(dt, point, dist, run);
	}

	private bool SafeFlee(float dt, Vector3 from)
	{
		Vector3 position = transform.position;
		Vector3 vector = position - from;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = -transform.forward;
		}
		vector.Normalize();
		for (int i = 0; i < 8; i++)
		{
			float num = (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 30f;
			if (Mathf.Abs(num) > 100f)
			{
				break;
			}
			Vector3 vector2 = Quaternion.Euler(0f, num, 0f) * vector;
			if (MHR_Mover.SafeStep(position, vector2, 3f))
			{
				_character.SetMoveDir(vector2);
				_character.SetRun(run: true);
				return true;
			}
		}
		_ai.StopMoving();
		if (CompanionsPlugin.LogDetailed)
		{
			UnsafeLog("no safe way to back off (a drop, water, fire or spikes) - standing its ground");
		}
		return false;
	}

	private void SafeWander(float dt, Vector3 center, float radius, bool run)
	{
		Vector3 position = transform.position;
		if (Time.time >= _wanderUntil || Vector3.Distance(position, _wanderPoint) < 1f || Vector3.Distance(center, _wanderPoint) > radius + 2f)
		{
			_wanderUntil = Time.time + Random.Range(2f, 4f);
			_wanderPoint = position;
			for (int i = 0; i < 8; i++)
			{
				Vector2 vector = Random.insideUnitCircle * radius;
				Vector3 vector2 = center + new Vector3(vector.x, 0f, vector.y);
				Vector3 vector3 = vector2 - position;
				vector3.y = 0f;
				float magnitude = vector3.magnitude;
				if (magnitude > 1f && MHR_Mover.SafeStep(position, vector3 / magnitude, Mathf.Min(magnitude, 4f)))
				{
					_wanderPoint = vector2;
					break;
				}
			}
		}
		if (Vector3.Distance(position, _wanderPoint) < 1f)
		{
			_ai.StopMoving();
		}
		else
		{
			MHR_Mover.Go(_ai, dt, _wanderPoint, 0.8f, run, "Combat", run ? 3 : 2);
		}
	}

	private void UnsafeLog(string msg)
	{
		if (Time.time >= _nextUnsafeLog)
		{
			_nextUnsafeLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Combat] " + msg);
			}
		}
	}

	private bool TacticsFor(ItemDrop.ItemData weapon)
	{
		if (FightLikePlayer && weapon != null && !MHR_IsProjectileWeapon(weapon) && _targetCreature != null && !_stalking && !Unaware(_targetCreature))
		{
			return !LatchedTarget();
		}
		return false;
	}

	private void TLog(string s)
	{
		if (s != _lastTacticLog || Time.time >= _nextTacticLog)
		{
			_lastTacticLog = s;
			_nextTacticLog = Time.time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Tactics] " + s);
			}
		}
	}

	internal static float SwingReach(Character c)
	{
		ItemDrop.ItemData itemData = (c as Humanoid)?.GetCurrentWeapon();
		if (itemData == null || itemData.m_shared == null)
		{
			return 2f;
		}
		return MHR_MeleeReach(itemData);
	}

	private float Danger(Character c)
	{
		return SwingReach(c) + c.GetRadius() + _character.GetRadius();
	}

	private void GatherAttackers()
	{
		_attackers.Clear();
		float num = ModConfig.CombatGroupRange?.Value ?? 6f;
		Vector3 position = transform.position;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter == _character) && !(allCharacter.GetHealth() <= 0f) && BaseAI.IsEnemy(_character, allCharacter) && !(Vector3.Distance(position, allCharacter.transform.position) > num) && (!(allCharacter != _targetCreature) || !(allCharacter.GetBaseAI()?.GetTargetCreature() != _character)))
			{
				_attackers.Add(allCharacter);
			}
		}
	}

	private Character NearestAttacker(out float dist)
	{
		Character result = null;
		dist = float.MaxValue;
		foreach (Character attacker in _attackers)
		{
			if (!(attacker == null))
			{
				float num = Vector3.Distance(transform.position, attacker.transform.position);
				if (num < dist)
				{
					dist = num;
					result = attacker;
				}
			}
		}
		return result;
	}

	private void GatherClose()
	{
		_close.Clear();
		foreach (Character attacker in _attackers)
		{
			if (!(attacker == null) && Vector3.Distance(transform.position, attacker.transform.position) < Danger(attacker) + 1.5f)
			{
				_close.Add(attacker);
			}
		}
	}

	private bool Flanked()
	{
		float num = ModConfig.CombatFlankAngle?.Value ?? 100f;
		Vector3 position = transform.position;
		for (int i = 0; i < _close.Count; i++)
		{
			Vector3 vector = _close[i].transform.position - position;
			vector.y = 0f;
			for (int j = i + 1; j < _close.Count; j++)
			{
				Vector3 to = _close[j].transform.position - position;
				to.y = 0f;
				if (Vector3.Angle(vector, to) > num)
				{
					return true;
				}
			}
		}
		return false;
	}

	private Vector3 BestDir(float dist, Character from)
	{
		Vector3 position = transform.position;
		Vector3 rhs = Vector3.zero;
		Player player = MHR_Owner.Of(this);
		if (player != null)
		{
			Vector3 vector = player.transform.position - position;
			vector.y = 0f;
			if (vector.magnitude > 2f && vector.magnitude < 30f)
			{
				rhs = vector.normalized;
			}
		}
		Vector3 result = Vector3.zero;
		float num = float.MinValue;
		for (int i = 0; i < 12; i++)
		{
			Vector3 vector2 = Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward;
			float num2 = 0.5f * Vector3.Dot(vector2, rhs);
			foreach (Character attacker in _attackers)
			{
				if (!(attacker == null))
				{
					Vector3 vector3 = attacker.transform.position - position;
					vector3.y = 0f;
					float num3 = ((attacker == from) ? 2f : 1f);
					num2 -= num3 * Vector3.Dot(vector2, vector3.normalized) / Mathf.Max(1f, vector3.magnitude * 0.3f);
				}
			}
			if (from != null && !_attackers.Contains(from))
			{
				Vector3 vector4 = from.transform.position - position;
				vector4.y = 0f;
				num2 -= 2f * Vector3.Dot(vector2, vector4.normalized);
			}
			num2 += Random.Range(0f, 0.15f);
			if (num2 > num && MHR_Mover.SafeStep(position, vector2, dist))
			{
				num = num2;
				result = vector2;
			}
		}
		return result;
	}

	private void Move(Vector3 dir, bool run, Character face)
	{
		dir.y = 0f;
		_character.SetMoveDir(dir.normalized);
		_character.SetRun(run);
		if (face != null)
		{
			_ai.LookAtPoint(face.GetCenterPoint());
		}
		else
		{
			_ai.LookTowards(dir);
		}
	}

	private float AttackCost(ItemDrop.ItemData w)
	{
		Attack attack = w?.m_shared?.m_attack;
		if (attack == null)
		{
			return 0f;
		}
		return attack.m_attackStamina * (1f - 0.33f * _character.GetSkillFactor(w.m_shared.m_skillType));
	}

	private float StaminaNow()
	{
		StaminaShare();
		if (!(_stam != null))
		{
			return 999f;
		}
		return _stam.Stamina;
	}

	private float StaminaMax()
	{
		StaminaShare();
		if (!(_stam != null) || !(_stam.MaxStamina > 0f))
		{
			return 100f;
		}
		return _stam.MaxStamina;
	}

	private int Tactics(float dt, ItemDrop.ItemData weapon)
	{
		_spacingHold = false;
		GatherAttackers();
		TrackDamage();
		if (_burstPending && !_character.InAttack())
		{
			_burstPending = false;
			float num = ModConfig.CombatComboPause?.Value ?? 0.5f;
			_burstPauseUntil = Time.time + num * Random.Range(0.6f, 1.4f);
			if (!MaybeClearOfWalls(weapon))
			{
				MaybeStepOut();
			}
		}
		Character character = NearestAttacker(out var _);
		if (Time.time < _stepUntil)
		{
			if (_character.InAttack())
			{
				return 1;
			}
			if (_stepToReach && _stepFrom != null && Vector3.Distance(transform.position, _stepFrom.transform.position) >= Danger(_stepFrom) + 0.3f)
			{
				_stepUntil = 0f;
			}
			else
			{
				if (MHR_Mover.SafeStep(transform.position, _stepDir, 0.8f))
				{
					Move(_stepDir, _stepRun, _stepFrom ?? character);
					return 1;
				}
				_stepUntil = 0f;
			}
		}
		if (_character.InAttack())
		{
			return 0;
		}
		if ((_finishing && _burstLeft > 0) || FinishOff(weapon) || Punish(weapon))
		{
			_stepUntil = 0f;
			return 0;
		}
		float num2 = StaminaShare();
		if (!_recovering && num2 < Reserve)
		{
			_recovering = true;
			_recoverSince = Time.time;
			_burstLeft = 0;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog($"Low on stamina ({num2:P0}) - backing out of reach to get its breath back");
			}
		}
		else if (_recovering && num2 >= Resume)
		{
			_recovering = false;
			_cornered = false;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog($"Got its breath back ({num2:P0}) - back in");
			}
		}
		if (_cornered && Time.time >= _corneredUntil)
		{
			_cornered = false;
			_recoverSince = Time.time;
		}
		if (_recovering && !_cornered)
		{
			Character character2 = null;
			foreach (Character attacker in _attackers)
			{
				if (!(attacker == null) && Vector3.Distance(transform.position, attacker.transform.position) < Danger(attacker) + 0.3f)
				{
					character2 = attacker;
					break;
				}
			}
			if (character2 == null)
			{
				Character targetCreature = _targetCreature;
				if (targetCreature != null && Vector3.Distance(transform.position, targetCreature.transform.position) > Danger(targetCreature) + 3f)
				{
					MHR_Mover.Go(_ai, dt, targetCreature.transform.position, Danger(targetCreature) + 2f, allowRun: false, "Combat", 2);
					return 1;
				}
				_ai.StopMoving();
				if (character != null)
				{
					_ai.LookAtPoint(character.GetCenterPoint());
				}
				return 2;
			}
			if (Time.time - _recoverSince > (ModConfig.CombatRecoverTime?.Value ?? 8f))
			{
				return Cornered(character2.m_name + " keeps after it");
			}
			if (Time.time >= _recoverStepReady)
			{
				float num3 = Mathf.Max(0.6f, Danger(character2) + 0.4f - Vector3.Distance(transform.position, character2.transform.position));
				Vector3 vector = transform.position - character2.transform.position;
				vector.y = 0f;
				Vector3 vector2 = ((vector.sqrMagnitude > 0.01f && !TowardsOther(vector.normalized, character2) && MHR_Mover.SafeStep(transform.position, vector.normalized, num3)) ? vector.normalized : EvadeDir(character2, num3));
				if (vector2 == Vector3.zero)
				{
					return Cornered("nowhere to step back to");
				}
				StartStep(vector2, num3, character2, run: false);
				_recoverStepReady = Time.time + 1.5f;
				if (CompanionsPlugin.LogDetailed)
				{
					TLog("Stepping " + StepWord(character2, vector2) + $" out of {character2.m_name}'s reach to get its breath back ({num3:F1} m)");
				}
				return 1;
			}
			_ai.StopMoving();
			_ai.LookAtPoint(character2.GetCenterPoint());
			return 2;
		}
		int num4 = ModConfig.CombatGroupSize?.Value ?? 2;
		GatherClose();
		if (_close.Count >= num4 && Flanked() && Time.time >= _repositionBlockedUntil)
		{
			if (_repositionSince < 0f)
			{
				_repositionSince = Time.time;
				if (CompanionsPlugin.LogDetailed)
				{
					TLog($"{_close.Count} on it from different sides - getting clear");
				}
			}
			if (Time.time - _repositionSince > 2.5f)
			{
				_repositionSince = -1f;
				_repositionBlockedUntil = Time.time + 6f;
				if (CompanionsPlugin.LogDetailed)
				{
					TLog("Couldn't get them on one side - fighting where it stands");
				}
				return 0;
			}
			if (Time.time >= _moveDirUntil)
			{
				_moveDir = BestDir(2f, null);
				_moveDirUntil = Time.time + 0.3f;
			}
			if (_moveDir != Vector3.zero)
			{
				Move(_moveDir, StaminaShare() > Resume, character);
				return 1;
			}
		}
		else
		{
			if (_repositionSince >= 0f)
			{
				_repositionBlockedUntil = Time.time + 4f;
			}
			_repositionSince = -1f;
		}
		return Spacing(dt, weapon);
	}

	private static float EnemyReadyIn(Character c)
	{
		float num = 0f;
		try
		{
			ItemDrop.ItemData itemData = (c as Humanoid)?.GetCurrentWeapon();
			if (itemData != null && itemData.m_shared != null)
			{
				num = itemData.m_lastAttackTime + itemData.m_shared.m_aiAttackInterval - Time.time;
			}
			if (c.GetBaseAI() is MonsterAI monsterAI)
			{
				num = Mathf.Max(num, monsterAI.m_minAttackInterval - c.GetTimeSinceLastAttack());
			}
		}
		catch
		{
		}
		return Mathf.Max(0f, num);
	}

	private bool SpacingTarget()
	{
		Character targetCreature = _targetCreature;
		if (targetCreature != null && _close.Count <= 1 && (_close.Count == 0 || _close[0] == targetCreature))
		{
			return targetCreature.GetBaseAI()?.GetTargetCreature() == _character;
		}
		return false;
	}

	private bool SpacingFor(Character t)
	{
		if (SpacingOn && t != null && _burstLeft <= 0 && _close.Count <= 1 && (_close.Count == 0 || _close[0] == t))
		{
			return t.GetBaseAI()?.GetTargetCreature() == _character;
		}
		return false;
	}

	private int Flank(float dt, ItemDrop.ItemData weapon)
	{
		Character targetCreature = _targetCreature;
		Character character = targetCreature?.GetBaseAI()?.GetTargetCreature();
		ConfigEntry<bool> combatFlank = ModConfig.CombatFlank;
		if ((combatFlank != null && !combatFlank.Value) || targetCreature == null || character == null || character == _character || _burstLeft > 0 || _close.Count > 1 || (_close.Count == 1 && _close[0] != targetCreature) || targetCreature.IsStaggering() || Time.time < _flankBlockedUntil)
		{
			_flankSince = -1f;
			return 0;
		}
		Vector3 position = transform.position;
		Vector3 vector = position - targetCreature.transform.position;
		vector.y = 0f;
		Vector3 forward = targetCreature.transform.forward;
		forward.y = 0f;
		float num = Vector3.Angle(forward, vector);
		if (num >= 110f)
		{
			if (_flankSince >= 0f && CompanionsPlugin.LogDetailed)
			{
				TLog("Behind " + targetCreature.m_name + " - attacking while it's busy with " + character.m_name);
			}
			_flankSince = -1f;
			return 0;
		}
		if (_flankSince < 0f)
		{
			_flankSince = Time.time;
			_flankLogged = false;
		}
		if (Time.time - _flankSince > 4f)
		{
			_flankSince = -1f;
			_flankBlockedUntil = Time.time + 6f;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog("Couldn't get behind " + targetCreature.m_name + " - attacking from here");
			}
			return 0;
		}
		if (!_flankLogged)
		{
			_flankLogged = true;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog(targetCreature.m_name + " is busy with " + character.m_name + " - working round behind it");
			}
		}
		float num2 = Mathf.Max(1f, MHR_MeleeReach(weapon) + targetCreature.GetRadius() + _character.GetRadius() - 0.3f);
		Vector3 vector2 = Vector3.Cross(Vector3.up, forward.normalized);
		float num3 = ((Vector3.Dot(vector, vector2) >= 0f) ? 1f : (-1f));
		Vector3 vector3 = ((num < 70f) ? (targetCreature.transform.position + vector2 * num3 * num2) : (targetCreature.transform.position - forward.normalized * num2));
		vector3.y = position.y;
		Vector3 vector4 = vector3 - position;
		vector4.y = 0f;
		float magnitude = vector4.magnitude;
		if (magnitude > 0.3f && !MHR_Mover.SafeStep(position, vector4 / magnitude, magnitude))
		{
			_flankSince = -1f;
			_flankBlockedUntil = Time.time + 6f;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog("No safe way round " + targetCreature.m_name + " (a drop or water) - attacking from here");
			}
			return 0;
		}
		MHR_Mover.Go(_ai, dt, vector3, 0.4f, StaminaShare() > Resume, "Combat", 2);
		return 1;
	}

	private int Spacing(float dt, ItemDrop.ItemData weapon)
	{
		_spacingHold = false;
		Character targetCreature = _targetCreature;
		int num = Flank(dt, weapon);
		if (num != 0)
		{
			_dipping = false;
			return num;
		}
		if (!SpacingFor(targetCreature))
		{
			_dipping = false;
			return 0;
		}
		float num2 = Vector3.Distance(transform.position, targetCreature.transform.position);
		float num3 = Danger(targetCreature) + (ModConfig.CombatSpacingMargin?.Value ?? 0.4f);
		float num4 = MHR_MeleeReach(weapon) + targetCreature.GetRadius() + _character.GetRadius();
		if (num2 <= num4 - 0.2f && num2 > Danger(targetCreature))
		{
			_dipping = false;
			return 2;
		}
		float num5 = Mathf.Max(0f, num2 - (num4 - 0.3f)) / 4f + 0.35f;
		float num6 = EnemyReadyIn(targetCreature);
		bool flag = targetCreature.IsStaggering() || (!targetCreature.InAttack() && num6 > num5);
		if (_dipping && (Time.time > _dipUntil || targetCreature.InAttack()))
		{
			_dipping = false;
		}
		if ((!_dipping & flag) && Time.time >= _burstPauseUntil && StaminaNow() - AttackCost(weapon) >= Reserve * StaminaMax())
		{
			_dipping = true;
			_dipUntil = Time.time + num5 + 0.6f;
			if (CompanionsPlugin.LogDetailed)
			{
				TLog($"{targetCreature.m_name} can't swing again for {num6:F1} s - going in");
			}
		}
		if (_dipping)
		{
			return 0;
		}
		if (num2 > num3 + 0.5f)
		{
			MHR_Mover.Go(_ai, dt, targetCreature.transform.position, num3, allowRun: false, "Combat", 2);
			_ai.LookAtPoint(targetCreature.GetCenterPoint());
			return 1;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			TLog($"Keeping just out of {targetCreature.m_name}'s reach (its reach {Danger(targetCreature):F1} m) - waiting for an opening");
		}
		_spacingHold = true;
		_ai.StopMoving();
		_ai.LookAtPoint(targetCreature.GetCenterPoint());
		return 2;
	}

	private int Cornered(string why)
	{
		_cornered = true;
		_corneredUntil = Time.time + 4f;
		if (CompanionsPlugin.LogDetailed)
		{
			TLog("Can't get clear (" + why + ") - fighting on what stamina it has");
		}
		return 0;
	}

	private void MaybeStepOut()
	{
		if (_targetCreature == null || (!(StaminaShare() < Reserve + 0.15f) && !(Random.value < 0.15f) && (!SpacingOn || !SpacingTarget())))
		{
			return;
		}
		float num = Danger(_targetCreature) + 0.7f - Vector3.Distance(transform.position, _targetCreature.transform.position);
		if (num <= 0f || num > 2.5f)
		{
			return;
		}
		Vector3 vector = EvadeDir(_targetCreature, Mathf.Max(num, 0.8f));
		if (!(vector == Vector3.zero))
		{
			StartStep(vector, num, _targetCreature, run: false);
			if (CompanionsPlugin.LogDetailed)
			{
				TLog("Stepping " + StepWord(_targetCreature, vector) + " out of " + _targetCreature.m_name + "'s reach after its combo");
			}
		}
	}

	private void StartStep(Vector3 dir, float dist, Character from, bool run, bool untilOutOfReach = true)
	{
		_stepDir = dir;
		_stepRun = run;
		_stepFrom = from;
		_stepToReach = untilOutOfReach;
		_stepUntil = Time.time + Mathf.Max(dist, 0.8f) / 3f + 0.3f;
		_burstLeft = 0;
	}

	private bool WallInSwing(Vector3 pos, Vector3 toTarget, ItemDrop.ItemData w)
	{
		if (s_pieceMask == -1)
		{
			s_pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
		}
		toTarget.y = 0f;
		float num = MHR_MeleeReach(w) + _character.GetRadius() + 0.3f;
		Attack attack = w?.m_shared?.m_attack;
		float num2 = ((attack != null) ? (attack.m_attackAngle * 0.5f) : 45f) + 10f;
		Vector3 vector = pos + Vector3.up * 1f;
		int num3 = Physics.OverlapSphereNonAlloc(vector, num, s_pieceHits, s_pieceMask);
		for (int i = 0; i < num3; i++)
		{
			Collider collider = s_pieceHits[i];
			Piece piece = ((collider != null) ? collider.GetComponentInParent<Piece>() : null);
			if (!(piece == null) && piece.IsPlacedByPlayer())
			{
				Vector3 to = collider.ClosestPoint(vector) - vector;
				to.y = 0f;
				if (to.magnitude < num && Vector3.Angle(toTarget, to) < num2)
				{
					_wallName = piece.m_name;
					return true;
				}
			}
		}
		return false;
	}

	private bool MaybeClearOfWalls(ItemDrop.ItemData w)
	{
		Character targetCreature = _targetCreature;
		if (targetCreature == null || w == null)
		{
			return false;
		}
		Vector3 position = transform.position;
		if (!WallInSwing(position, targetCreature.transform.position - position, w))
		{
			return false;
		}
		string wallName = _wallName;
		Vector3 vector = position - targetCreature.transform.position;
		vector.y = 0f;
		float num = Mathf.Max(0.8f, Mathf.Min(vector.magnitude, MHR_MeleeReach(w) + targetCreature.GetRadius() - 0.2f));
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = -targetCreature.transform.forward;
		}
		vector.Normalize();
		float[] array = new float[6] { 30f, -30f, 60f, -60f, 90f, -90f };
		foreach (float angle in array)
		{
			Vector3 vector2 = targetCreature.transform.position + Quaternion.AngleAxis(angle, Vector3.up) * vector * num;
			vector2.y = position.y;
			Vector3 dir = vector2 - position;
			dir.y = 0f;
			float magnitude = dir.magnitude;
			if (!(magnitude < 0.4f) && !TowardsOther(dir, targetCreature) && MHR_Mover.SafeStep(position, dir, magnitude) && !WallInSwing(vector2, targetCreature.transform.position - vector2, w))
			{
				StartStep(dir.normalized, magnitude, targetCreature, run: false, untilOutOfReach: false);
				if (CompanionsPlugin.LogDetailed)
				{
					TLog("Moving to a clear spot - its swings were catching the " + wallName);
				}
				return true;
			}
		}
		return false;
	}

	private bool ObstacleInLine(Vector3 pos, Character t)
	{
		if (s_obstacleMask == -1)
		{
			s_obstacleMask = LayerMask.GetMask("Default", "static_solid", "Default_small");
		}
		Vector3 vector = pos + Vector3.up * 1f;
		Vector3 vector2 = t.GetCenterPoint() - vector;
		float num = vector2.magnitude - t.GetRadius() * 0.5f;
		if (num <= 0.2f)
		{
			return false;
		}
		RaycastHit[] array = Physics.SphereCastAll(vector, 0.15f, vector2.normalized, num, s_obstacleMask, QueryTriggerInteraction.Ignore);
		foreach (RaycastHit raycastHit in array)
		{
			Collider collider = raycastHit.collider;
			if (!(collider == null) && !(collider.GetComponentInParent<Character>() != null) && !(collider.GetComponentInParent<Piece>() != null))
			{
				Component component = (Component)(collider.GetComponentInParent<TreeBase>() ?? collider.GetComponentInParent<TreeLog>() ?? collider.GetComponentInParent<Destructible>() ?? ((object)collider.GetComponentInParent<MineRock5>()) ?? ((object)collider.GetComponentInParent<MineRock>()));
				_obstacleName = ((component != null) ? MHR_Items.NiceName(component.gameObject) : collider.gameObject.name);
				return true;
			}
		}
		return false;
	}

	internal bool MaybeClearOfObstacle(ItemDrop.ItemData w)
	{
		Character targetCreature = _targetCreature;
		if (targetCreature == null || w == null || Time.time < _obstacleCheckAt || Time.time < _stepUntil)
		{
			return false;
		}
		_obstacleCheckAt = Time.time + 0.5f;
		Vector3 position = transform.position;
		if (!ObstacleInLine(position, targetCreature))
		{
			return false;
		}
		string obstacleName = _obstacleName;
		Vector3 vector = position - targetCreature.transform.position;
		vector.y = 0f;
		float num = Mathf.Max(0.8f, Mathf.Min(vector.magnitude, MHR_MeleeReach(w) + targetCreature.GetRadius() - 0.2f));
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = -targetCreature.transform.forward;
		}
		vector.Normalize();
		float[] array = new float[8] { 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };
		foreach (float angle in array)
		{
			Vector3 vector2 = targetCreature.transform.position + Quaternion.AngleAxis(angle, Vector3.up) * vector * num;
			vector2.y = position.y;
			Vector3 dir = vector2 - position;
			dir.y = 0f;
			float magnitude = dir.magnitude;
			if (!(magnitude < 0.4f) && !TowardsOther(dir, targetCreature) && MHR_Mover.SafeStep(position, dir, magnitude) && !ObstacleInLine(vector2, targetCreature))
			{
				StartStep(dir.normalized, magnitude, targetCreature, run: false, untilOutOfReach: false);
				if (CompanionsPlugin.LogDetailed)
				{
					TLog("Moving round the " + obstacleName + " - it was in the way of its swing at " + targetCreature.m_name);
				}
				return true;
			}
		}
		if (CompanionsPlugin.LogDetailed)
		{
			TLog("The " + obstacleName + " is in the way of its swing at " + targetCreature.m_name + " and there's no clear spot - swinging anyway");
		}
		_obstacleCheckAt = Time.time + 3f;
		return false;
	}

	private bool TryEvade(Character swinger, bool always = false)
	{
		float num = (always ? 1f : (ModConfig.StepAwayChance?.Value ?? 0.3f));
		if (StaminaShare() < Reserve)
		{
			num = Mathf.Min(1f, num * 2f);
		}
		if (SpacingOn && swinger == _targetCreature && SpacingTarget() && Danger(swinger) + 0.6f - Vector3.Distance(transform.position, swinger.transform.position) <= 1.2f)
		{
			num = Mathf.Max(num, ModConfig.CombatSpacingStepChance?.Value ?? 0.6f);
		}
		if (!always && Random.value >= num)
		{
			return false;
		}
		float num2 = Danger(swinger) + 0.6f - Vector3.Distance(transform.position, swinger.transform.position);
		if (num2 > 2.5f)
		{
			return false;
		}
		num2 = Mathf.Max(num2, 0.8f);
		Vector3 vector = EvadeDir(swinger, num2);
		if (vector == Vector3.zero)
		{
			return false;
		}
		bool run = num2 > 1.5f && StaminaShare() > Reserve;
		StartStep(vector, num2, swinger, run);
		if (CompanionsPlugin.LogDetailed)
		{
			TLog("Stepping " + StepWord(swinger, vector) + $" out of {swinger.m_name}'s swing ({num2:F1} m)");
		}
		return true;
	}

	private string StepWord(Character from, Vector3 dir)
	{
		Vector3 vector = transform.position - from.transform.position;
		vector.y = 0f;
		float num = Vector3.Angle(vector, dir);
		if (!(num < 30f))
		{
			if (!(num < 70f))
			{
				return "aside";
			}
			return "back and aside";
		}
		return "back";
	}

	private Vector3 EvadeDir(Character from, float dist)
	{
		Vector3 position = transform.position;
		Vector3 vector = position - from.transform.position;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.01f)
		{
			vector = -transform.forward;
		}
		vector.Normalize();
		float num = 0f;
		Vector3 lhs = Vector3.Cross(Vector3.up, vector);
		foreach (Character attacker in _attackers)
		{
			if (!(attacker == null) && attacker != from)
			{
				Vector3 vector2 = attacker.transform.position - position;
				vector2.y = 0f;
				num -= Vector3.Dot(lhs, vector2.normalized);
			}
		}
		Player player = MHR_Owner.Of(this);
		if (num == 0f && player != null)
		{
			Vector3 vector3 = player.transform.position - position;
			vector3.y = 0f;
			num = Vector3.Dot(lhs, vector3.normalized);
		}
		float num2 = ((num > 0f || (num == 0f && Random.value < 0.5f)) ? 1f : (-1f));
		float value = Random.value;
		float[] array;
		if (value < 0.25f)
		{
			array = new float[3] { 0f, 45f, 90f };
		}
		else
		{
			array = ((!(value < 0.6f)) ? new float[3] { 90f, 45f, 0f } : new float[3] { 45f, 90f, 0f });
		}
		foreach (float num3 in array)
		{
			float[] array2 = ((num3 != 0f) ? new float[2]
			{
				num2,
				0f - num2
			} : new float[1] { 1f });
			for (int j = 0; j < array2.Length; j++)
			{
				Vector3 vector4 = Quaternion.AngleAxis((0f - array2[j]) * num3, Vector3.up) * vector;
				if (!TowardsOther(vector4, from) && MHR_Mover.SafeStep(position, vector4, dist))
				{
					return vector4;
				}
			}
		}
		return Vector3.zero;
	}

	private bool TowardsOther(Vector3 dir, Character except)
	{
		foreach (Character attacker in _attackers)
		{
			if (!(attacker == null) && !(attacker == except))
			{
				Vector3 vector = attacker.transform.position - transform.position;
				vector.y = 0f;
				if (vector.magnitude < 4f && Vector3.Dot(dir, vector.normalized) > 0.5f)
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool Opening()
	{
		if (_targetCreature != null && _targetCreature.IsStaggering())
		{
			return true;
		}
		Character character = SwingAtUs();
		if (character == null)
		{
			return true;
		}
		if (character == _blockAgainst)
		{
			return _swingLandedAt >= _swingStartedAt;
		}
		return false;
	}

	internal void NoteIncoming(Character attacker)
	{
		if (attacker != null && attacker == _blockAgainst)
		{
			_swingLandedAt = Time.time;
		}
	}

	internal void NoteOwnHit(Character target)
	{
		if (target == _targetCreature)
		{
			_lastOwnHitAt = Time.time;
		}
	}

	private void TrackDamage()
	{
		Character targetCreature = _targetCreature;
		if (targetCreature != _dmgTarget)
		{
			_dmgTarget = targetCreature;
			_perHit = 0f;
			_dmgLastHp = ((targetCreature != null) ? targetCreature.GetHealth() : 0f);
		}
		else if (!(targetCreature == null))
		{
			float health = targetCreature.GetHealth();
			if (health < _dmgLastHp - 0.1f && Time.time - _lastOwnHitAt < 1f)
			{
				float num = _dmgLastHp - health;
				_perHit = (targetCreature.IsStaggering() ? (num * 0.5f) : num);
			}
			_dmgLastHp = health;
		}
	}

	private int HitsToKill()
	{
		if (_targetCreature == null || _perHit <= 0f)
		{
			return 99;
		}
		float num = _perHit * 0.9f * (_targetCreature.IsStaggering() ? 2f : 1f);
		return Mathf.Max(1, Mathf.CeilToInt(_targetCreature.GetHealth() / num));
	}

	private bool BreathingRoomAfter()
	{
		foreach (Character attacker in _attackers)
		{
			if (!(attacker == null) && attacker != _targetCreature && Vector3.Distance(transform.position, attacker.transform.position) < Danger(attacker) + 2f)
			{
				return false;
			}
		}
		return true;
	}

	private bool FinishOff(ItemDrop.ItemData w)
	{
		if (_targetCreature == null || w == null || _targetCreature.GetHealth() <= 0f)
		{
			return false;
		}
		float num = AttackCost(w);
		if (_burstLeft > 0)
		{
			if (_finishing)
			{
				return StaminaNow() >= num;
			}
			return false;
		}
		int num2 = HitsToKill();
		if (num2 > (ModConfig.CombatFinishHits?.Value ?? 2) || StaminaNow() < (float)num2 * num || !BreathingRoomAfter() || !Opening())
		{
			return false;
		}
		_finishHits = num2;
		return true;
	}

	private bool Punish(ItemDrop.ItemData w)
	{
		if (_targetCreature == null || w == null || !_targetCreature.IsStaggering())
		{
			_punishUsed = false;
			return false;
		}
		if (_burstLeft > 0)
		{
			if (_punishing)
			{
				return StaminaNow() - AttackCost(w) >= Reserve * StaminaMax() * 0.5f;
			}
			return false;
		}
		if (!_punishUsed)
		{
			return StaminaNow() >= AttackCost(w);
		}
		return false;
	}

	private bool ReadyToSwing(ItemDrop.ItemData w)
	{
		if (w == null)
		{
			return false;
		}
		if (_awaitingSwing)
		{
			if (!_character.InAttack() && !(Time.time - _swingStartedTime > 0.4f))
			{
				return false;
			}
			_awaitingSwing = false;
		}
		if (_character.InAttack() && (!WantsChain || !CurrentAttackCanChain()))
		{
			return false;
		}
		if (FinishOff(w))
		{
			if (!_finishing)
			{
				_finishing = true;
				if (CompanionsPlugin.LogDetailed)
				{
					TLog($"{_targetCreature.m_name} is nearly dead ({_targetCreature.GetHealth():F0} hp, about {_finishHits} hit(s)) - spending its stamina to finish it");
				}
			}
			return true;
		}
		if (_finishing && _burstLeft <= 0)
		{
			_finishing = false;
		}
		if (Punish(w))
		{
			if (!_punishing)
			{
				_punishing = true;
				if (CompanionsPlugin.LogDetailed)
				{
					TLog(_targetCreature.m_name + " is staggered - going in with a combo" + (_recovering ? " (catching its breath can wait)" : ""));
				}
			}
			return true;
		}
		if (_punishing && _burstLeft <= 0)
		{
			_punishing = false;
		}
		if (_spacingHold && _burstLeft <= 0)
		{
			return false;
		}
		if ((_recovering && !_cornered) || _burstPending)
		{
			EndBurst();
			return false;
		}
		if (_burstLeft <= 0 && Time.time < _burstPauseUntil)
		{
			return false;
		}
		if (_burstLeft <= 0 && !Opening())
		{
			return false;
		}
		float num = (_cornered ? 0f : (Reserve * StaminaMax() * ((_burstLeft > 0) ? 0.5f : 1f)));
		if (StaminaNow() - AttackCost(w) < num)
		{
			EndBurst();
			return false;
		}
		return true;
	}

	private void EndBurst()
	{
		if (_burstLeft > 0)
		{
			_burstLeft = 0;
			_burstPending = true;
		}
	}

	private int BurstSize(ItemDrop.ItemData w)
	{
		int b = ModConfig.CombatComboHits?.Value ?? 3;
		Attack attack = w.m_shared.m_attack;
		int value = ((attack != null && attack.m_attackChainLevels > 1) ? attack.m_attackChainLevels : 2);
		value = Mathf.Clamp(value, 1, Mathf.Max(1, b));
		if (value > 1 && !_finishing && !_punishing && Random.value < 0.2f)
		{
			value--;
		}
		float num = AttackCost(w);
		float num2 = (_cornered ? 0f : (Reserve * StaminaMax() * 0.5f));
		if (_finishing)
		{
			value = Mathf.Max(value, _finishHits);
			num2 = 0f;
		}
		int num3 = value;
		while (value > 1 && StaminaNow() - (float)value * num < num2)
		{
			value--;
		}
		if (value < num3 && CompanionsPlugin.LogDetailed)
		{
			TLog($"{value}-hit combo, not {num3} - saving stamina to block ({StaminaNow():F0}/{StaminaMax():F0})");
		}
		return value;
	}

	internal bool CurrentAttackCanChain()
	{
		try
		{
			return (s_curAttackT?.GetValue(_humanoid) as Attack)?.CanStartChainAttack() ?? false;
		}
		catch
		{
			return false;
		}
	}

	private void NoteTacticSwing(ItemDrop.ItemData w)
	{
		_awaitingSwing = true;
		_swingStartedTime = Time.time;
		if (_burstLeft <= 0)
		{
			_burstPending = false;
			if (_punishing)
			{
				_punishUsed = true;
			}
			_burstLeft = BurstSize(w);
			if (_burstLeft > 1 && CompanionsPlugin.LogDetailed)
			{
				TLog($"{_burstLeft}-hit combo at {_targetCreature?.m_name}");
			}
		}
		_burstLeft--;
		if (_burstLeft <= 0)
		{
			_burstPending = true;
		}
	}

	private bool FleeToOwner(float dt)
	{
		Player player = MHR_Owner.Of(this);
		if (player == null || _targetCreature == null)
		{
			return false;
		}
		Vector3 position = transform.position;
		Vector3 vector = player.transform.position - position;
		vector.y = 0f;
		if (vector.magnitude < 4f || vector.magnitude > 60f)
		{
			return false;
		}
		Vector3 to = _targetCreature.transform.position - position;
		to.y = 0f;
		if (Vector3.Angle(vector, to) < 60f)
		{
			return false;
		}
		MHR_Mover.Go(_ai, dt, player.transform.position, 3f, allowRun: true, "Flee", 3);
		if (MHR_Mover.GaveUp(_ai))
		{
			return false;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			TLog("Badly hurt - running to its owner, away from " + _targetCreature.m_name);
		}
		return true;
	}

	private void ResetTactics()
	{
		_burstLeft = 0;
		_burstPending = false;
		_stepUntil = 0f;
		_recovering = false;
		_cornered = false;
		_repositionSince = -1f;
		_evading = false;
		_punishing = false;
		_punishUsed = false;
		_finishing = false;
		_dipping = false;
		_spacingHold = false;
		_flankSince = -1f;
	}

	internal bool IsOrdered(Character c)
	{
		if (c != null)
		{
			return c == _ordered;
		}
		return false;
	}

	internal void Order(Character target)
	{
		_ordered = target;
		_holdUntil = 0f;
		_ignoreUntil.Remove(target);
		_letGoUnreachable.Remove(target);
	}

	internal void HoldForOrder(string what)
	{
		float num = ModConfig.CombatOrderHoldTime?.Value ?? 10f;
		if (_engaged)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug(string.Format("[Combat] {0} - leaving {1}, no new fights for {2:F0} s", what, (_targetCreature != null) ? _targetCreature.m_name : "the fight", num));
			}
			Disengage();
		}
		_ordered = null;
		_holdUntil = Time.time + num;
	}

	internal bool MayFightBack(Character attacker)
	{
		if (!Holding || attacker == null)
		{
			return true;
		}
		return Vector3.Distance(transform.position, attacker.transform.position) - attacker.GetRadius() < 3f;
	}

	internal bool IndoorsNow()
	{
		if (Time.time >= _indoorCheckAt)
		{
			_indoorCheckAt = Time.time + 1f;
			_indoors = _character != null && (_character.InInterior() || Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.up, 30f, LayerMask.GetMask("piece", "static_solid", "Default")));
		}
		return _indoors;
	}

	private bool Defensive()
	{
		if (_character != null && _character.InInterior())
		{
			return ModConfig.CombatDungeonDefensive?.Value ?? true;
		}
		return false;
	}

	internal bool CanGetAt(Character c)
	{
		if (c == null)
		{
			return false;
		}
		if (!(Vector3.Distance(transform.position, c.transform.position) - c.GetRadius() < 2.5f))
		{
			return MHR_BowDraw.ClearShot(_character, c);
		}
		return true;
	}

	internal bool MeleeCanReach(Character c, out string why)
	{
		why = null;
		if (c == null || _character == null)
		{
			return true;
		}
		if (_reach.TryGetValue(c, out var value) && Time.time < value.At)
		{
			why = value.Why;
			return value.Ok;
		}
		if (_reach.Count > 40)
		{
			_reach.Clear();
		}
		value = new Reach
		{
			At = Time.time + 2f,
			Ok = true
		};
		Vector3 position = transform.position;
		Vector3 position2 = c.transform.position;
		Vector3 vector = position2 - position;
		float y = vector.y;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (!_character.IsSwimming() && MHR_Water.WouldSwim(_character, position2))
		{
			value.Ok = false;
			value.Why = "it's out in deep water";
		}
		else if (MHR_Mover.Lava(position2))
		{
			value.Ok = false;
			value.Why = "it's standing in lava";
		}
		else if (magnitude < 3f && (y > 1.4f || y < -1.8f) && (c.IsOnGround() || c.IsFlying()) && MHR_Latched.LatchedOn(_character) != c)
		{
			value.Ok = false;
			value.Why = ((y > 0f) ? "it's above, out of reach" : "it's below, out of reach");
		}
		else if (magnitude > 2.5f)
		{
			if (!MHR_Mover.WalkRoute(position, position2, out var len))
			{
				value.Ok = false;
				value.Why = "there's no way to walk there";
			}
			else if (len > magnitude * 2f + 15f)
			{
				value.Ok = false;
				value.Why = $"the only way there is a long way round ({len:F0} m)";
			}
		}
		_reach[c] = value;
		why = value.Why;
		return value.Ok;
	}

	internal bool BowOnly(Character c, out string why)
	{
		if (!MeleeCanReach(c, out why))
		{
			return true;
		}
		if (Defensive())
		{
			Player player = MHR_Owner.Of(_setup);
			float num = ModConfig.CombatDefendRange?.Value ?? 8f;
			if (player != null && c != null && Vector3.Distance(player.transform.position, c.transform.position) > num)
			{
				why = $"fighting defensively, it's more than {num:F0} m from you";
				return true;
			}
		}
		return false;
	}

	private bool BowReady()
	{
		if (!MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()))
		{
			return MHR_Crossbow.HasReadyRanged(_humanoid);
		}
		return true;
	}

	internal Character Latched()
	{
		if (Time.time >= _latchedCheckAt)
		{
			_latchedCheckAt = Time.time + 0.25f;
			Character latched = _latched;
			string latchedName = _latchedName;
			bool flag = latchedName != null;
			_latched = MHR_Latched.LatchedOn(_character);
			_latchedName = ((_latched != null) ? _latched.m_name : null);
			if ((object)_latched != latched)
			{
				if (_latched != null && CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] " + _latched.m_name + " latched on to " + _character.m_name);
				}
				if (flag && CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Combat] " + latchedName + " let go of " + _character.m_name + ((latched == null || latched.IsDead()) ? " (dead)" : ""));
				}
			}
		}
		return _latched;
	}

	private bool LatchedTarget()
	{
		if (_targetCreature != null)
		{
			return Latched() == _targetCreature;
		}
		return false;
	}

	internal Character PickTarget()
	{
		Character character = Latched();
		if (character != null && !character.IsDead())
		{
			return character;
		}
		if (Holding || _character == null)
		{
			return null;
		}
		bool flag = IndoorsNow() && (ModConfig.CombatIndoorAggroOnly?.Value ?? true);
		bool flag2 = Defensive();
		float num = ModConfig.CombatDefendRange?.Value ?? 8f;
		Player player = MHR_Owner.Of(_setup);
		Vector3 position = transform.position;
		float num2 = Mathf.Max(_ai.m_viewRange, 20f);
		if (!MHR_IsProjectileWeapon(_humanoid.GetCurrentWeapon()))
		{
			CompanionSetup setup = _setup;
			if ((object)setup == null || setup.GetCombatStance() != 2)
			{
				goto IL_00d4;
			}
		}
		num2 = Mathf.Max(num2, 30f);
		goto IL_00d4;
		IL_00d4:
		Character result = null;
		float num3 = float.MaxValue;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == null || allCharacter == _character || allCharacter.IsDead() || allCharacter.m_aiSkipTarget || !_ai.IsEnemy(allCharacter) || IsPassiveAnimal(allCharacter) || MHR_Threat.IgnoredInWater(allCharacter) || (_ignoreUntil.TryGetValue(allCharacter, out var value) && Time.time < value && !LiftIgnore(allCharacter)))
			{
				continue;
			}
			float num4 = Vector3.Distance(position, allCharacter.transform.position);
			if (num4 > num2)
			{
				continue;
			}
			BaseAI baseAI = allCharacter.GetBaseAI();
			if (baseAI != null && baseAI.IsSleeping())
			{
				continue;
			}
			Character character2 = baseAI?.GetTargetCreature();
			bool flag3 = character2 == _character;
			if (!MHR_Threat.IsAfterUs(allCharacter) || (flag && baseAI != null && !baseAI.IsAlerted()))
			{
				continue;
			}
			bool flag4 = flag3 && num4 - allCharacter.GetRadius() < 3f;
			if (player != null && !flag4 && Vector3.Distance(player.transform.position, allCharacter.transform.position) > LeashFromYou)
			{
				continue;
			}
			bool flag5 = false;
			if (flag2 && player != null && Vector3.Distance(player.transform.position, allCharacter.transform.position) > num && !flag4)
			{
				if (!BowReady() || num4 > 30f || (baseAI != null && !baseAI.IsAlerted()))
				{
					continue;
				}
				flag5 = true;
			}
			if (CanGetAt(allCharacter) && (!flag5 || MHR_BowDraw.ClearShot(_character, allCharacter)))
			{
				float num5 = num4 - (flag3 ? 6f : 0f) - ((character2 != null && character2.IsPlayer()) ? 2f : 0f) - ((allCharacter == _targetCreature) ? 3f : 0f);
				if (!flag4 && !MeleeCanReach(allCharacter, out var _))
				{
					num5 += 15f;
				}
				if (num5 < num3)
				{
					num3 = num5;
					result = allCharacter;
				}
			}
		}
		return result;
	}

	private void CheckTargetStillValid()
	{
		if (_targetCreature == null || HuntMode || IsOrdered(_targetCreature))
		{
			_noSightSince = -1f;
			return;
		}
		Character targetCreature = _targetCreature;
		bool flag = targetCreature.GetBaseAI()?.GetTargetCreature() == _character;
		float num = Vector3.Distance(transform.position, targetCreature.transform.position) - targetCreature.GetRadius();
		if (flag && num < 3f)
		{
			_noSightSince = -1f;
			return;
		}
		Player player = MHR_Owner.Of(_setup);
		if (player != null && Vector3.Distance(player.transform.position, targetCreature.transform.position) > LeashFromYou)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				TargetLog($"[Combat] {targetCreature.m_name} is more than {LeashFromYou:F0} m from you - letting it go and coming back");
			}
			LetGo(targetCreature, 10f);
		}
		else
		{
			if (GiveUpOnUnreachable(targetCreature))
			{
				return;
			}
			if (Defensive())
			{
				Player player2 = MHR_Owner.Of(_setup);
				float num2 = (ModConfig.CombatDefendRange?.Value ?? 8f) + 3f;
				if (player2 != null && Vector3.Distance(player2.transform.position, targetCreature.transform.position) > num2 && (!BowReady() || !MHR_BowDraw.ClearShot(_character, targetCreature)))
				{
					if (CompanionsPlugin.LogDetailed)
					{
						TargetLog($"[Combat] {targetCreature.m_name} is more than {num2:F0} m from you - fighting defensively, letting it come to us");
					}
					_ignoreUntil[targetCreature] = Time.time + 3f;
					_targetCreature = null;
					return;
				}
			}
			if (CanGetAt(targetCreature))
			{
				_noSightSince = -1f;
			}
			else if (_noSightSince < 0f)
			{
				_noSightSince = Time.time;
			}
			else if (Time.time - _noSightSince > 3f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					TargetLog("[Combat] Lost sight of " + targetCreature.m_name + " (behind a wall or door) - letting it go");
				}
				_ignoreUntil[targetCreature] = Time.time + 8f;
				_targetCreature = null;
				_noSightSince = -1f;
			}
		}
	}

	private bool LiftIgnore(Character c)
	{
		if (Time.time < _liftCheckAt)
		{
			return false;
		}
		Character character = c.GetBaseAI()?.GetTargetCreature();
		Player player = MHR_Owner.Of(_setup);
		bool flag = c.InAttack() && character != null && (character == _character || character == player) && Vector3.Distance(c.transform.position, character.transform.position) < 5f;
		bool flag2 = !flag && _letGoUnreachable.Contains(c) && MeleeCanReach(c, out var _);
		if (!flag && !flag2)
		{
			_liftCheckAt = Time.time + 0.5f;
			return false;
		}
		_ignoreUntil.Remove(c);
		_letGoUnreachable.Remove(c);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Combat] " + c.m_name + " " + (flag ? ("is attacking " + ((character == _character) ? "it" : "you")) : "came within reach") + " - back on it");
		}
		return true;
	}

	private void LetGo(Character c, float secs)
	{
		_ignoreUntil[c] = Time.time + secs;
		_letGoUnreachable.Remove(c);
		_targetCreature = null;
		_noSightSince = -1f;
		_stuckOn = null;
		MHR_Mover.Release(_ai);
	}

	private bool GiveUpOnUnreachable(Character c)
	{
		if (MeleeCanReach(c, out var why))
		{
			_stuckOn = null;
			return false;
		}
		float health = c.GetHealth();
		if (_stuckOn != c || health < _stuckHealth - 0.01f)
		{
			_stuckOn = c;
			_stuckSince = Time.time;
			_stuckHealth = health;
			return false;
		}
		float num = ModConfig.CombatGiveUpTime?.Value ?? 10f;
		if (Time.time - _stuckSince < num)
		{
			return false;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Combat] Can't get at {c.m_name} ({why}) and not a hit on it for {num:F0} s - letting it go");
		}
		LetGo(c, 20f);
		_letGoUnreachable.Add(c);
		return true;
	}

	private void DoorInTheWay()
	{
		if (!(_targetCreature == null))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				TargetLog("[Combat] " + _targetCreature.m_name + " is behind a closed door - not opening it in a fight");
			}
			_ignoreUntil[_targetCreature] = Time.time + 15f;
			_targetCreature = null;
			MHR_Mover.Release(_ai);
		}
	}

	private void TargetLog(string msg)
	{
		if (Time.time >= _nextTargetLog)
		{
			_nextTargetLog = Time.time + 1f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug(msg);
			}
		}
	}
}
