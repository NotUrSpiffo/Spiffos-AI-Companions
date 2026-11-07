using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Companions;

public class HarvestController : MonoBehaviour
{
	private enum HarvestState
	{
		Idle,
		Moving,
		Attacking,
		CollectingDrops
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private DoorHandler _doorHandler;

	private CompanionStamina _stamina;

	private CompanionTalk _talk;

	private HarvestState _state;

	internal GameObject _target;

	private float _scanTimer;

	private float _attackTimer;

	private float _moveTimer;

	internal bool _toolEquipped;

	internal string _targetType;

	private int _swingCount;

	private int _totalAttempts;

	private int _consecutiveFailures;

	private int _rock5ActiveSections = -1;

	private Collider _rock5CurrentSegment;

	private bool _reapproach;

	private bool _shouldRun;

	private Vector3 _lockedMoveTarget;

	private bool _moveTargetLocked;

	internal Vector3 _attackNodePos;

	private Vector3 _lockedAttackFaceTarget;

	private bool _attackFaceLocked;

	private int _claimedTargetId;

	private int _lastAoeSwing = -1;

	private static readonly HashSet<int> s_claimedTargets = new HashSet<int>();

	internal static bool IsApplyingAoeDamage;

	private ItemDrop.ItemData _pendingToolReequip;

	private GameObject _directedTarget;

	private int _directedMode;

	private bool _isDirectedHarvest;

	internal readonly Dictionary<int, float> _blacklist = new Dictionary<int, float>();

	private static HashSet<string> _forageFilter;

	private static string _forageFilterRaw;

	private const HarvestState State_Idle = HarvestState.Idle;

	private const HarvestState State_Moving = HarvestState.Moving;

	private const HarvestState State_Attacking = HarvestState.Attacking;

	private const HarvestState State_CollectingDrops = HarvestState.CollectingDrops;

	private const float AttackRetry = 0.25f;

	private const float MoveTimeout = 10f;

	private const float ArrivalSlack = 0.5f;

	private const int WhiffRetryMax = 20;

	private const int TotalAttemptMax = 30;

	private const float DropPickupRange = 3f;

	private const float DropTimeout = 10f;

	private const float DropScanDelay = 0.5f;

	private static readonly int s_pickedHash = "picked".GetStableHashCode();

	private float _overweightMsgTimer;

	private float _chestScanTimer;

	private readonly Collider[] _scanBuffer = new Collider[4096];

	private readonly Collider[] _dropBuffer = new Collider[128];

	private readonly HashSet<int> _seenIds = new HashSet<int>();

	private GameObject _bestTree;

	private float _bestTreeDist;

	private Vector3 _lastDestroyPos;

	private GameObject _currentDrop;

	private float _dropTimer;

	private float _dropScanDelayTimer;

	private int _scanSeen;

	private int _scanUnwanted;

	private int _scanNoAuto;

	private int _scanFull;

	private int _dropsPickedUp;

	private int _itemLayerMask;

	private string _tag;

	private float _moveLogTimer;

	private float _heartbeatTimer;

	private float _attackDiagTimer;

	private const float MoveLogInterval = 1f;

	private const float HeartbeatInterval = 5f;

	private const float AttackDiagInterval = 2f;

	private bool _chopChain;

	private bool _catchingBreath;

	private static readonly FieldInfo s_curAttack = AccessTools.Field(typeof(Humanoid), "m_currentAttack");

	private Vector3 _anchor;

	private bool _hasAnchor;

	private bool _anchorAtHome;

	private int _anchorMode;

	private bool _wasFollow;

	private bool _goingBack;

	private Vector3 _lastTargetPos;

	private Vector3 _splitPos;

	private float _splitAt = -100f;

	private GameObject _hitCheckFor;

	private float _lastHitAt = -100f;

	private float _lastSwingAt = -100f;

	private int _noDamageSwings;

	private bool _triedOtherSide;

	private bool _hasGoodSpot;

	private Vector3 _goodSpot;

	private readonly HashSet<int> _preLogs = new HashSet<int>();

	private readonly HashSet<int> _ownPieces = new HashSet<int>();

	private GameObject _settleFor;

	private float _settleSince;

	private GameObject _settleLoggedFor;

	private bool _logPointFound;

	private readonly List<Vector3> _logBadPoints = new List<Vector3>();

	private float _closeInTimer;

	private bool _closingIn;

	private static int s_lineMask = -1;

	private GameObject _logPointFor;

	private float _logPointAt;

	private Vector3 _logPoint;

	private Vector3 _logPointLogPos;

	private static int s_groundMask = -1;

	internal int LastTierSkips;

	internal string LastTierName;

	private float _nextEarlyCheck;

	private bool _early;

	private bool _earlyLogged;

	private static float BlacklistDuration => ModConfig.HarvestBlacklistDuration.Value;

	private static float ScanInterval => ModConfig.HarvestScanInterval.Value;

	private static float ScanRadius => ModConfig.HarvestScanRadius.Value;

	internal bool WantsChain
	{
		get
		{
			if (_chopChain && _state == HarvestState.Attacking)
			{
				if (!(_ai == null))
				{
					return !_ai.IsInCombat;
				}
				return true;
			}
			return false;
		}
	}

	private static float DropScanRadius => ModConfig.HarvestDropScanRadius.Value;

	private float OverweightThreshold => ModConfig.HarvestOverweightThreshold.Value + CompanionTierData.CarryBonus(this);

	public bool IsInGatherMode { get; private set; }

	public bool IsActive
	{
		get
		{
			if (_state == HarvestState.Idle)
			{
				return _goingBack;
			}
			return true;
		}
	}

	private static float AreaRange => ModConfig.HarvestRange?.Value ?? 40f;

	private bool Following
	{
		get
		{
			if (_setup != null)
			{
				return _setup.GetFollow();
			}
			return false;
		}
	}

	internal bool IsMoving => _state == HarvestState.Moving;

	internal bool IsAttacking => _state == HarvestState.Attacking;

	internal string CurrentStateName => _state.ToString();

	private bool HoldingGoodSpot
	{
		get
		{
			if (_hasGoodSpot && _hitCheckFor == _target && _noDamageSwings == 0)
			{
				return Vector3.Distance(_goodSpot, transform.position) < 0.8f;
			}
			return false;
		}
	}

	internal bool CurrentAttackCanChain()
	{
		try
		{
			return (s_curAttack?.GetValue(_humanoid) as Attack)?.CanStartChainAttack() ?? false;
		}
		catch
		{
			return false;
		}
	}

	private bool WantsPowerChop(ItemDrop.ItemData tool)
	{
		if (GetMode() != 1 || _target == null || tool == null || !tool.HaveSecondaryAttack())
		{
			return false;
		}
		if (_targetType == "Stump")
		{
			return true;
		}
		if (_targetType != "TreeLog")
		{
			return false;
		}
		float num = float.MinValue;
		Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (collider != null && collider.enabled && !collider.isTrigger)
			{
				num = Mathf.Max(num, collider.bounds.max.y);
			}
		}
		if (num > float.MinValue)
		{
			return num < transform.position.y + (ModConfig.GatherPowerChopLogBelow?.Value ?? 0.3f);
		}
		return false;
	}

	private void UpdateAnchor(int mode)
	{
		bool following = Following;
		if (following != _wasFollow || mode != _anchorMode)
		{
			_wasFollow = following;
			_anchorMode = mode;
			_hasAnchor = false;
			if (_goingBack)
			{
				_goingBack = false;
				MHR_Mover.Release(_ai);
			}
		}
		if (!following && !_isDirectedHarvest && !_hasAnchor)
		{
			_hasAnchor = true;
			_anchorAtHome = _setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
			_anchor = (_anchorAtHome ? _setup.GetHomePosition() : transform.position);
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo("Working around " + (_anchorAtHome ? "home" : "here") + " " + MHR_Mover.Fmt(_anchor) + ", up to " + AreaRange.ToString("F0") + " m out");
			}
		}
	}

	private bool InWorkArea(Vector3 p, string type = null)
	{
		if (Following || _isDirectedHarvest || !_hasAnchor)
		{
			float num = PlayerRange();
			if ((type == "TreeLog" || type == "Stump") && _setup != null && _setup.GetFollow() && !_isDirectedHarvest)
			{
				num = Mathf.Max(num, ModConfig.HarvestFollowLogRange?.Value ?? 20f);
			}
			if (!(MHR_Owner.Of(this) == null))
			{
				return Vector3.Distance(MHR_Owner.Of(this).transform.position, p) <= num;
			}
			return true;
		}
		return Vector3.Distance(_anchor, p) <= AreaRange;
	}

	private Vector3 ScanCenter()
	{
		if (Following || _isDirectedHarvest || !_hasAnchor)
		{
			return transform.position;
		}
		return _anchor;
	}

	private float ScanRad()
	{
		if (Following || _isDirectedHarvest || !_hasAnchor)
		{
			return ScanRadius;
		}
		return AreaRange;
	}

	private void UpdateGoingBack()
	{
		if (!_goingBack)
		{
			return;
		}
		if (Following || Vector3.Distance(transform.position, _anchor) <= 5f)
		{
			_goingBack = false;
			_ai?.StopMoving();
			MHR_Mover.Release(_ai);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _anchor, 4f, allowRun: true, "Gather");
		if (MHR_Mover.GaveUp(_ai))
		{
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo("Can't get back to the work area - working from here");
			}
			_anchor = transform.position;
			_anchorAtHome = false;
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
	}

	private float PlayerRange()
	{
		if (_setup == null || !_setup.GetFollow() || _isDirectedHarvest)
		{
			return 40f;
		}
		if (GetMode() != 5)
		{
			return ModConfig.HarvestFollowRange?.Value ?? 20f;
		}
		return ModConfig.ForageFollowRange?.Value ?? 10f;
	}

	internal bool IsAttackingRockTarget(GameObject obj)
	{
		if (_state == HarvestState.Attacking && _target == obj)
		{
			return IsRockLikeTarget();
		}
		return false;
	}

	private bool TryFollowCrackedRock()
	{
		string targetType = _targetType;
		if (targetType != "MineRock" && targetType != "MineRock5" && targetType != "Destructible")
		{
			return false;
		}
		Collider[] array = Physics.OverlapSphere(_lastTargetPos, 2.5f, -1, QueryTriggerInteraction.Ignore);
		GameObject gameObject = null;
		foreach (Collider collider in array)
		{
			if (collider == null || collider.GetComponentInParent<Piece>() != null || collider.GetComponentInParent<Character>() != null)
			{
				continue;
			}
			Component component = (Component)(collider.GetComponentInParent<MineRock5>() ?? ((object)collider.GetComponentInParent<MineRock>()) ?? ((object)collider.GetComponentInParent<Destructible>()));
			if (!(component == null) && !(Vector3.Distance(component.transform.position, _lastTargetPos) > 0.5f))
			{
				ZNetView component2 = component.GetComponent<ZNetView>();
				if (!(component2 == null) && component2.IsValid())
				{
					gameObject = component.gameObject;
					break;
				}
			}
		}
		if (gameObject == null)
		{
			return false;
		}
		UnclaimTarget();
		_target = gameObject;
		_targetType = ClassifyTargetType(gameObject, GetMode());
		ClaimTarget();
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		_rock5CurrentSegment = null;
		_swingCount = 0;
		_totalAttempts = 0;
		_consecutiveFailures = 0;
		if (_targetType == "MineRock5")
		{
			_rock5ActiveSections = CountActiveRock5Sections();
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log("Rock cracked - carrying on with \"" + gameObject.name + "\" (" + _targetType + ")");
		}
		return true;
	}

	private bool RockSurface(out Vector3 surface, out float dist)
	{
		surface = Vector3.zero;
		dist = 0f;
		if (_target == null)
		{
			return false;
		}
		Vector3 vector = transform.position + Vector3.up * 1f;
		Vector3 vector2 = GetEffectiveTargetPosition() - vector;
		float magnitude = vector2.magnitude;
		if (magnitude < 0.05f)
		{
			return false;
		}
		RaycastHit[] array = Physics.RaycastAll(vector, vector2 / magnitude, magnitude + 0.5f, -1, QueryTriggerInteraction.Ignore);
		float num = float.MaxValue;
		for (int i = 0; i < array.Length; i++)
		{
			Collider collider = array[i].collider;
			if (collider != null && collider.transform.IsChildOf(_target.transform) && array[i].distance < num)
			{
				num = array[i].distance;
				surface = array[i].point;
			}
		}
		if (num == float.MaxValue)
		{
			return false;
		}
		dist = num;
		return true;
	}

	internal void TryApplyRockAoe()
	{
		if (_state == HarvestState.Attacking && IsRockLikeTarget() && _lastAoeSwing != _swingCount)
		{
			_lastAoeSwing = _swingCount;
			if (_targetType == "MineRock5")
			{
				ApplyDirectRock5Damage();
			}
			else
			{
				ApplyDirectDamage();
			}
		}
	}

	internal bool IsAttackingTreeTarget(GameObject obj)
	{
		if (_state == HarvestState.Attacking && _target == obj)
		{
			return IsTreeTarget();
		}
		return false;
	}

	internal void TryApplyTreeAoe()
	{
		if (_state == HarvestState.Attacking && IsTreeTarget() && _lastAoeSwing != _swingCount)
		{
			_lastAoeSwing = _swingCount;
			ApplyDirectDamage();
		}
	}

	public void NotifyTerrainHit()
	{
	}

	public void NotifyActionModeChanged()
	{
		int mode = GetMode();
		bool flag = (mode >= 1 && mode <= 3) || mode == 5;
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"NotifyActionModeChanged — new mode={mode} isGather={flag} " + $"(wasGather={IsInGatherMode}, state={_state})");
		}
		CancelDirectedTarget();
		if (!flag && IsInGatherMode)
		{
			ExitGatherMode();
			return;
		}
		RestoreLoadout();
		ResetToIdle();
	}

	internal static int DetermineHarvestModeStatic(GameObject target)
	{
		if (target.GetComponent<TreeBase>() != null)
		{
			return 1;
		}
		if (target.GetComponent<TreeLog>() != null)
		{
			return 1;
		}
		Destructible component = target.GetComponent<Destructible>();
		if (component != null && target.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return 1;
		}
		if (target.GetComponent<MineRock5>() != null)
		{
			return 3;
		}
		MineRock component2 = target.GetComponent<MineRock>();
		if (component2 != null)
		{
			if (!HasOreDrops(component2.m_dropItems) && !IsOreNamedRock(target.name))
			{
				return 2;
			}
			return 3;
		}
		if (component != null && component.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (component.m_damages.m_chop == HitData.DamageModifier.Immune || component.m_damages.m_chop == HitData.DamageModifier.Ignore))
		{
			DropOnDestroyed component3 = component.GetComponent<DropOnDestroyed>();
			if (component3 != null && HasOreDrops(component3.m_dropWhenDestroyed))
			{
				return 3;
			}
			if (IsOreNamedRock(component.gameObject.name))
			{
				return 3;
			}
			return 2;
		}
		if (component != null && component.m_damages.m_chop != HitData.DamageModifier.Immune)
		{
			return 1;
		}
		return -1;
	}

	public void SetDirectedTarget(GameObject target)
	{
		if (target == null)
		{
			return;
		}
		int num = DetermineHarvestModeStatic(target);
		if (num < 0)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("SetDirectedTarget REJECTED — can't determine tool for \"" + target.name + "\"");
			}
			return;
		}
		int minToolTier = GetMinToolTier(target);
		int bestToolTier = GetBestToolTier(num);
		if (minToolTier > bestToolTier)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"SetDirectedTarget REJECTED — \"{target.name}\" requires tier {minToolTier}, " + $"best tool is tier {bestToolTier}");
			}
			string text = "Companion";
			if (_nview?.GetZDO() != null)
			{
				string text2 = _nview.GetZDO().GetString(CompanionSetup.NameHash);
				if (!string.IsNullOrEmpty(text2))
				{
					text = text2;
				}
			}
			if (MHR_Owner.IsMine(this))
			{
				Player.m_localPlayer?.Message(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_tools_weak", text));
			}
			return;
		}
		_directedTarget = target;
		_directedMode = num;
		_isDirectedHarvest = true;
		_target = target;
		_targetType = ClassifyTargetType(target, num);
		_state = HarvestState.Moving;
		_moveTimer = 0f;
		_moveTargetLocked = false;
		_swingCount = 0;
		_totalAttempts = 0;
		ClaimTarget();
		_scanTimer = ScanInterval;
		ItemDrop.ItemData itemData = FindBestTool(num);
		if (itemData != null)
		{
			EquipTool(itemData);
		}
		float num2 = Vector3.Distance(transform.position, target.transform.position);
		if (CompanionsPlugin.LogDetailed)
		{
			Log("[PointTo] SetDirectedTarget — \"" + target.name + "\" type=" + _targetType + " " + string.Format("mode={0} tool=\"{1}\" ", num, itemData?.m_shared?.m_name ?? "NONE") + $"dist={num2:F1}m targetPos={target.transform.position:F1} " + $"myPos={transform.position:F1}");
		}
	}

	public void CancelDirectedTarget()
	{
		if (_isDirectedHarvest)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("CancelDirectedTarget — clearing directed harvest");
			}
			_directedTarget = null;
			_isDirectedHarvest = false;
			if (!IsInGatherMode)
			{
				ResetToIdle();
				RestoreLoadout();
			}
		}
	}

	internal void MHR_NotifyIfIdle()
	{
		if (_state == HarvestState.Idle)
		{
			NotifyActionModeChanged();
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_setup = GetComponent<CompanionSetup>();
		_doorHandler = GetComponent<DoorHandler>();
		_stamina = GetComponent<CompanionStamina>();
		_talk = GetComponent<CompanionTalk>();
		int instanceID = GetInstanceID();
		_tag = $"[Harvest#{instanceID & 0xFFFF}]";
		_itemLayerMask = LayerMask.GetMask("item");
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"Awake — nview={_nview != null} ai={_ai != null} " + $"humanoid={_humanoid != null} character={_character != null} " + $"setup={_setup != null} instanceId={instanceID}");
		}
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Gather|" + text + "] " + msg);
		}
	}

	private void OnDestroy()
	{
		UnclaimTarget();
	}

	private void UnclaimTarget()
	{
		if (_claimedTargetId != 0)
		{
			s_claimedTargets.Remove(_claimedTargetId);
			_claimedTargetId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || !_nview.IsOwner() || MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this) || MHR_GatherRules.TickDodge(this))
		{
			return;
		}
		if (_character != null && !_tag.Contains("|"))
		{
			string text = _character.m_name;
			if (!string.IsNullOrEmpty(text) && text != "HC_Companion" && text != "HC_Dverger")
			{
				_tag = $"[Harvest#{GetInstanceID() & 0xFFFF}|{text}]";
			}
		}
		int mode = GetMode();
		bool flag = (mode >= 1 && mode <= 3) || mode == 5;
		if (!flag && !_isDirectedHarvest)
		{
			if (IsInGatherMode)
			{
				ExitGatherMode();
			}
			return;
		}
		if (flag && !IsInGatherMode)
		{
			EnterGatherMode();
		}
		UpdateAnchor(mode);
		if (_ai != null && _ai.IsInCombat)
		{
			RestoreLoadout();
			return;
		}
		if (IsOverweight())
		{
			if (!(_ai != null) || !(_ai.PendingDepositContainer != null))
			{
				MHR_GatherRules.HandleOverweight(this);
			}
			return;
		}
		_overweightMsgTimer -= Time.deltaTime;
		_chestScanTimer -= Time.deltaTime;
		if (_ai != null && _ai.IsInCombat)
		{
			RestoreLoadout();
		}
		else
		{
			if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
			{
				return;
			}
			if (IsCompanionUIOpen())
			{
				if (_state != HarvestState.Idle)
				{
					_ai?.StopMoving();
				}
				return;
			}
			if (_state != HarvestState.Idle && !_isDirectedHarvest && _setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null && Vector3.Distance(transform.position, MHR_Owner.Of(this).transform.position) > PlayerRange() + 15f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					Log("You moved on - leaving \"" + ((_target != null) ? _target.name : "the drops") + "\" and catching up");
				}
				ResetToIdle();
				RestoreLoadout();
				_scanTimer = 3f;
				return;
			}
			_heartbeatTimer -= Time.deltaTime;
			if (_heartbeatTimer <= 0f)
			{
				_heartbeatTimer = 5f;
				LogHeartbeat();
			}
			switch (_state)
			{
			case HarvestState.Idle:
				UpdateGoingBack();
				UpdateIdle(mode);
				break;
			case HarvestState.Moving:
				UpdateMoving();
				break;
			case HarvestState.Attacking:
				UpdateAttacking();
				break;
			case HarvestState.CollectingDrops:
				UpdateCollectingDrops();
				break;
			}
		}
	}

	private int GetMode()
	{
		if (_isDirectedHarvest)
		{
			return _directedMode;
		}
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	private void ExitGatherMode()
	{
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"EXIT gather mode — was state={_state} " + "target=\"" + ((_target != null) ? _target.name : "null") + "\" " + $"toolEquipped={_toolEquipped}");
		}
		IsInGatherMode = false;
		_hasAnchor = false;
		if (_goingBack)
		{
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
		ResetToIdle();
		RestoreLoadout();
		bool flag = _setup != null && _setup.GetFollow();
		if (((_ai != null) & flag) && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Restored follow target to player (Follow=ON)");
			}
		}
		else if (CompanionsPlugin.LogDetailed)
		{
			Log("Follow OFF — idle");
		}
	}

	private void EnterGatherMode()
	{
		int mode = GetMode();
		string arg = mode switch
		{
			5 => "Forage", 
			3 => "Ore", 
			2 => "Stone", 
			1 => "Wood", 
			_ => "Unknown", 
		};
		IsInGatherMode = true;
		_scanTimer = 0f;
		_heartbeatTimer = 0f;
		GameObject gameObject = _ai?.GetFollowTarget();
		if (mode == 5 && CompanionsPlugin.LogNormal)
		{
			LogInfo($"Foraging started — scanning for pickables near {transform.position:F1}");
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"ENTER gather mode: {arg} (mode={mode}) " + "followTarget=\"" + (gameObject?.name ?? "null") + "\" " + $"pos={transform.position:F1}");
		}
	}

	public bool IsOverweight()
	{
		if (GetCurrentWeight() >= OverweightThreshold)
		{
			return true;
		}
		return MHR_GatherRules.PackFull(this);
	}

	internal void RestoreLoadout()
	{
		if (_toolEquipped || ((object)_setup != null && _setup.SuppressAutoEquip))
		{
			_toolEquipped = false;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("RestoreLoadout — SuppressAutoEquip=false, re-equipping best gear");
			}
			if (_setup != null)
			{
				_setup.SuppressAutoEquip = false;
				_setup.SyncEquipmentToInventory();
			}
		}
	}

	private bool IsCompanionUIOpen()
	{
		if (!CompanionInteractPanel.IsOpenFor(_setup) && !CompanionRadialMenu.IsOpenFor(_setup))
		{
			return HomeZonePanel.IsOpenFor(_setup);
		}
		return true;
	}

	private void LogHeartbeat()
	{
	}

	private void UpdateIdle(int mode)
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		if (_blacklist.Count > 0)
		{
			float time = Time.time;
			List<int> list = null;
			foreach (KeyValuePair<int, float> item in _blacklist)
			{
				if (time - item.Value > BlacklistDuration)
				{
					if (list == null)
					{
						list = new List<int>();
					}
					list.Add(item.Key);
				}
			}
			if (list != null)
			{
				for (int i = 0; i < list.Count; i++)
				{
					_blacklist.Remove(list[i]);
				}
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Blacklist cleanup: removed {list.Count} expired, {_blacklist.Count} remain");
				}
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			LogInfo($"Scanning for targets... mode={mode} " + $"pos={transform.position:F1} radius={ScanRadius}");
		}
		GameObject gameObject = null;
		bool noTool = false;
		int num;
		if (!_isDirectedHarvest && (mode == 1 || mode == 2))
		{
			num = (LooseOn(mode, out noTool) ? 1 : 0);
			if (num != 0)
			{
				gameObject = ScanForPickable(mode);
			}
		}
		else
		{
			num = 0;
		}
		GameObject gameObject2 = ((((uint)num & (noTool ? 1u : 0u)) != 0) ? null : MHR_GatherRules.Scan(this, mode));
		bool flag = false;
		if (gameObject != null && (gameObject2 == null || Vector3.Distance(transform.position, gameObject.transform.position) <= Vector3.Distance(transform.position, gameObject2.transform.position)))
		{
			gameObject2 = gameObject;
			flag = true;
		}
		if (gameObject2 != null)
		{
			CompanionStatus.Clear(this);
			CompanionStatus.Doing(this, flag ? "hc_doing_pick" : (mode switch
			{
				1 => "hc_doing_chop", 
				5 => "hc_doing_forage", 
				_ => "hc_doing_mine", 
			}), MHR_Items.NiceName(gameObject2));
		}
		if (gameObject2 == null)
		{
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo("Scan found 0 valid targets — staying idle");
			}
			if (!_goingBack && !Following && !_isDirectedHarvest && _hasAnchor && Vector3.Distance(transform.position, _anchor) > 8f)
			{
				if (CompanionsPlugin.LogNormal)
				{
					LogInfo("Nothing left nearby - walking back to the middle of the work area");
				}
				_goingBack = true;
			}
			return;
		}
		if (_goingBack)
		{
			_goingBack = false;
			MHR_Mover.Release(_ai);
		}
		if ((mode == 5) | flag)
		{
			float num2 = Vector3.Distance(transform.position, gameObject2.transform.position);
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo(string.Format("{0}: \"{1}\" dist={2:F1}m", flag ? "Picking up" : "Forage target", gameObject2.name, num2));
			}
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Forage target acquired: \"{gameObject2.name}\" dist={num2:F1}m " + $"pos={gameObject2.transform.position:F1}");
			}
			_target = gameObject2;
			_targetType = "Pickable";
			_swingCount = 0;
			_totalAttempts = 0;
			ClaimTarget();
		}
		else
		{
			ItemDrop.ItemData itemData = FindBestTool(mode);
			if (itemData == null)
			{
				LogWarn($"No tool in inventory for mode {mode} — cannot harvest! " + "(need " + ((mode == 1) ? "axe (chop dmg)" : "pickaxe (pickaxe dmg)") + ")");
				LogInventoryContents();
				return;
			}
			float num3 = Vector3.Distance(transform.position, gameObject2.transform.position);
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo($"Target acquired: \"{gameObject2.name}\" dist={num3:F1}m " + "tool=\"" + itemData.m_shared.m_name + "\" " + $"pick={itemData.GetDamage().m_pickaxe:F0}");
			}
			_target = gameObject2;
			_targetType = ClassifyTargetType(gameObject2, mode);
			_swingCount = 0;
			_totalAttempts = 0;
			EquipTool(itemData);
			ClaimTarget();
		}
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Target set: \"" + _target.name + "\" — HarvestController driving movement");
			}
		}
		NoteLogsAround();
		_moveTimer = 0f;
		_moveLogTimer = 0f;
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		_state = HarvestState.Moving;
	}

	private void UpdateMoving()
	{
		if (LogSettling() || LogOutOfReach())
		{
			return;
		}
		if (_pendingToolReequip != null && _humanoid != null && !_humanoid.InAttack())
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Retrying deferred equip: \"" + _pendingToolReequip.m_shared.m_name + "\"");
			}
			EquipTool(_pendingToolReequip);
		}
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (!IsTargetValid(_target))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Target gone while moving — waiting 1.5s then rescanning");
			}
			ResetToIdle();
			_scanTimer = 1.5f;
			return;
		}
		Vector3 vector = GetEffectiveTargetPosition() - transform.position;
		float num = Mathf.Abs(vector.y);
		vector.y = 0f;
		float num2 = ((_targetType == "Pickable") ? 1.5f : GetAttackRange());
		bool flag = (!(_targetType == "TreeLog") && !(_targetType == "TreeBase")) || num < 3f;
		if ((vector.magnitude <= num2) & flag)
		{
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			if (IsRockLikeTarget())
			{
				_lockedAttackFaceTarget = GetRockFacePosition();
				_attackFaceLocked = true;
			}
			else
			{
				_attackFaceLocked = false;
			}
			FaceTarget();
			_reapproach = false;
			_attackTimer = 0.3f;
			_attackDiagTimer = 0f;
			_closeInTimer = 0f;
			_closingIn = false;
			_state = HarvestState.Attacking;
			if (IsRockLikeTarget())
			{
				_attackNodePos = GetEffectiveTargetPosition();
			}
			if (_targetType == "MineRock5")
			{
				_rock5ActiveSections = CountActiveRock5Sections();
			}
			return;
		}
		float deltaTime = Time.deltaTime;
		bool flag2 = _humanoid != null && _humanoid.InAttack();
		if (!flag2)
		{
			Vector3 lhs = _character?.GetVelocity() ?? Vector3.zero;
			Vector3 vector2 = _target.transform.position - transform.position;
			vector2.y = 0f;
			float num3 = ((((vector2.sqrMagnitude > 0.01f) ? Vector3.Dot(lhs, vector2.normalized) : 0f) > 1f || MHR_Mover.Progressing(_ai)) ? 0.3f : 1f);
			if (MHR_DoorBusy())
			{
				goto IL_04df;
			}
			_moveTimer += deltaTime * num3;
		}
		if (_moveTimer > 0.5f && MHR_Mover.GaveUp(_ai))
		{
			_moveTimer = 10.01f;
		}
		if (_moveTimer > 10f)
		{
			float num4 = Vector3.Distance(transform.position, _target.transform.position);
			Vector3 vector3 = _character?.GetVelocity() ?? Vector3.zero;
			if (_targetType == "TreeLog")
			{
				_ = 1;
			}
			else
				_ = _targetType == "TreeBase";
			bool flag3 = _targetType == "Destructible" || _targetType == "MineRock";
			LogWarn($"STUCK timeout ({10f}s) — giving up on \"{_target.name}\" " + $"dist={num4:F1}m vel={vector3.magnitude:F1} " + $"myPos={transform.position:F1} targetPos={_target.transform.position:F1} " + $"targetY={_target.transform.position.y:F2} myY={transform.position.y:F2} " + $"heightDiff={_target.transform.position.y - transform.position.y:F2}");
			_blacklist[_target.GetInstanceID()] = Time.time;
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Blacklisted \"{_target.name}\" (id={_target.GetInstanceID()}) for {BlacklistDuration}s");
			}
			ResetToIdle();
			return;
		}
		goto IL_04df;
		IL_04df:
		bool flag4 = _targetType == "Pickable";
		float num5 = (flag4 ? 1.5f : GetAttackRange());
		if (!_moveTargetLocked)
		{
			Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
			if (IsRockLikeTarget() && NavMesh.SamplePosition(effectiveTargetPosition, out var hit, 6f, -1))
			{
				_lockedMoveTarget = hit.position;
			}
			else
			{
				_lockedMoveTarget = effectiveTargetPosition;
			}
			_moveTargetLocked = true;
		}
		Vector3 lockedMoveTarget = _lockedMoveTarget;
		float num6 = Vector3.Distance(transform.position, lockedMoveTarget);
		bool flag5 = IsRockLikeTarget();
		float num7;
		bool flag6;
		if (flag4)
		{
			num7 = 1.2f;
			flag6 = false;
		}
		else if (_reapproach)
		{
			num7 = num5 * 0.5f;
			flag6 = false;
		}
		else if (IsLowTarget())
		{
			num7 = num5 * 0.8f;
			flag6 = num6 > 8f;
		}
		else if (flag5)
		{
			num7 = num5 * 0.8f;
			flag6 = num6 > 6f;
		}
		else
		{
			num7 = num5 * 0.8f;
			flag6 = num6 > 8f;
		}
		_shouldRun = flag6;
		bool flag7 = MHR_Mover.Go(_ai, deltaTime, MHR_GatherRules.ApproachPoint(this, lockedMoveTarget), num7, flag6, "Gather");
		if (!flag7 && num6 < 4f && (_character?.GetVelocity().magnitude ?? 0f) < 1f)
		{
			Vector3 dir = lockedMoveTarget - transform.position;
			dir.y = 0f;
			if (dir.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(transform.position, dir, 0.8f) && !MHR_Mover.Climbing(_ai))
			{
				_ai.MoveTowards(dir.normalized, run: false);
			}
		}
		else if (flag7 && num6 > num7 + 0.5f && (_character?.GetVelocity() ?? Vector3.zero).magnitude < 0.3f)
		{
			Vector3 dir2 = lockedMoveTarget - transform.position;
			dir2.y = 0f;
			if (dir2.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(transform.position, dir2, 0.8f))
			{
				_ai.MoveTowards(dir2.normalized, run: false);
			}
		}
		_moveLogTimer -= deltaTime;
		if (_moveLogTimer <= 0f)
		{
			_moveLogTimer = 1f;
			float num8 = Vector3.Distance(transform.position, lockedMoveTarget);
			Vector3 vector4 = _character?.GetVelocity() ?? Vector3.zero;
			GameObject gameObject = _ai?.GetFollowTarget();
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Moving → \"{_target.name}\" dist={num8:F1}m " + $"range={num5:F1} timer={_moveTimer:F1}s " + $"moveOK={flag7} vel={vector4.magnitude:F1} velY={vector4.y:F2} " + "follow=\"" + (gameObject?.name ?? "null") + "\" " + $"inAttack={flag2} " + $"pos={transform.position:F1} tgt={lockedMoveTarget:F1} " + $"heightDiff={lockedMoveTarget.y - transform.position.y:F2}");
			}
		}
		num6 = Vector3.Distance(transform.position, lockedMoveTarget);
		Vector3 vector5 = lockedMoveTarget - transform.position;
		float magnitude = new Vector3(vector5.x, 0f, vector5.z).magnitude;
		bool flag8 = IsLowTarget();
		float num9;
		if (flag4)
		{
			num9 = 1.5f;
		}
		else if (_reapproach)
		{
			num9 = num5 * 0.7f;
		}
		else if (flag8)
		{
			num9 = num5;
		}
		else
		{
			num9 = ((!flag5) ? (num5 + 0.5f) : num5);
		}
		if (_reapproach && _moveTimer > 3f)
		{
			float num10 = ((flag8 | flag5) ? num5 : (num5 + 0.5f));
			if (num6 <= num10 || magnitude <= num10)
			{
				num9 = num10;
			}
		}
		if (num6 <= num9 || magnitude <= num9)
		{
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			if (flag5)
			{
				_lockedAttackFaceTarget = GetRockFacePosition();
				_attackFaceLocked = true;
			}
			else
			{
				_attackFaceLocked = false;
			}
			FaceTarget();
			_reapproach = false;
			_attackTimer = 0.3f;
			_attackDiagTimer = 0f;
			_closeInTimer = 0f;
			_closingIn = false;
			_state = HarvestState.Attacking;
			if (flag5)
			{
				_attackNodePos = GetEffectiveTargetPosition();
			}
			if (_targetType == "MineRock5")
			{
				_rock5ActiveSections = CountActiveRock5Sections();
			}
		}
	}

	private void UpdateAttacking()
	{
		if (LogSettling() || LogOutOfReach())
		{
			return;
		}
		if (GetMode() == 5 || _targetType == "Pickable")
		{
			Pickable pickable = _target?.GetComponentInParent<Pickable>();
			if (pickable != null)
			{
				ZNetView component = pickable.GetComponent<ZNetView>();
				if (component != null && component.GetZDO() != null && component.GetZDO().GetBool(s_pickedHash))
				{
					if (CompanionsPlugin.LogNormal)
					{
						LogInfo("Forage target already picked — skipping \"" + pickable.name + "\"");
					}
				}
				else
				{
					FaceTarget();
					pickable.Interact(_humanoid, repeat: false, alt: false);
					if (CompanionsPlugin.LogNormal)
					{
						LogInfo($"Foraged: \"{pickable.name}\" at {pickable.transform.position:F1}");
					}
				}
			}
			else
			{
				LogWarn("Forage target has no Pickable — skipping \"" + (_target?.name ?? "null") + "\"");
			}
			_lastDestroyPos = _target?.transform.position ?? transform.position;
			_currentDrop = null;
			_dropTimer = 0f;
			_dropScanDelayTimer = 0.5f;
			_dropsPickedUp = 0;
			_state = HarvestState.CollectingDrops;
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			return;
		}
		if (_target != null)
		{
			_lastTargetPos = _target.transform.position;
		}
		if (!IsTargetValid(_target) && TryFollowCrackedRock())
		{
			return;
		}
		if (!IsTargetValid(_target))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Target DESTROYED after {_swingCount} hits, {_totalAttempts} attempts " + "— entering drop collection");
			}
			if (_targetType == "TreeLog" || _targetType == "TreeBase")
			{
				if (Time.time - _splitAt > 60f)
				{
					_ownPieces.Clear();
				}
				_splitPos = _lastTargetPos;
				_splitAt = Time.time;
			}
			MHR_GatherRules.OnTargetDestroyed(this, _targetType);
			_lastDestroyPos = MHR_GatherRules.DropCenter(this);
			if (_targetType == "TreeBase")
			{
				GameObject gameObject = TryFindNearbyLog(transform.position);
				if (gameObject != null && !s_claimedTargets.Contains(gameObject.GetInstanceID()))
				{
					UnclaimTarget();
					_target = gameObject;
					_targetType = ClassifyTargetType(gameObject, 1);
					_swingCount = 0;
					_totalAttempts = 0;
					ClaimTarget();
					_moveTargetLocked = false;
					_moveTimer = 0f;
					_state = HarvestState.Moving;
					if (CompanionsPlugin.LogDetailed)
					{
						Log("Tree fell — immediately targeting fallen log \"" + gameObject.name + "\"");
					}
					if (_ai != null)
					{
						_ai.StopMoving();
					}
					return;
				}
			}
			_currentDrop = null;
			_dropTimer = 0f;
			_dropScanDelayTimer = 0.5f;
			_dropsPickedUp = 0;
			_state = HarvestState.CollectingDrops;
			if (_ai != null)
			{
				_ai.StopMoving();
			}
			return;
		}
		FaceTarget();
		bool flag = IsRockLikeTarget();
		Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
		float num = Vector3.Distance(transform.position, effectiveTargetPosition);
		float attackRange = GetAttackRange();
		float num2 = (flag ? (attackRange + 1.2f) : (attackRange + 1.5f));
		bool num3 = _targetType == "TreeLog" || _targetType == "TreeBase";
		float num4 = num;
		if (num3)
		{
			Vector3 vector = effectiveTargetPosition - transform.position;
			num4 = new Vector3(vector.x, 0f, vector.z).magnitude;
		}
		_attackDiagTimer -= Time.deltaTime;
		if (_targetType == "TreeLog" && num4 <= attackRange && num > attackRange + 0.5f && !_character.InAttack())
		{
			SetAside("it's above its reach");
			return;
		}
		if (num4 > num2 || (_targetType == "TreeLog" && num > attackRange + 0.5f && !_character.InAttack()))
		{
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
			return;
		}
		if (flag && _character != null && !_character.InAttack())
		{
			if (RockSurface(out var surface, out var dist) && dist > attackRange * 0.85f && _closeInTimer < 2.5f)
			{
				_closeInTimer += Time.deltaTime;
				Vector3 dir = surface - transform.position;
				dir.y = 0f;
				if (dir.sqrMagnitude > 0.01f && !MHR_Mover.HazardAhead(transform.position, dir, 0.8f))
				{
					_ai.MoveTowards(dir.normalized, run: false);
				}
				if (!_closingIn)
				{
					_closingIn = true;
					if (CompanionsPlugin.LogDetailed)
					{
						Log($"Rock surface is {dist:F1}m away (reach {attackRange * 0.85f:F1}m) — stepping in");
					}
				}
				return;
			}
			if (_closingIn)
			{
				_closingIn = false;
				_ai.StopMoving();
			}
		}
		_attackTimer -= Time.deltaTime;
		if (_attackTimer > 0f)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		int mode = GetMode();
		bool flag2 = rightItem == null;
		if (!flag2)
		{
			HitData.DamageTypes damage = rightItem.GetDamage();
			flag2 = ((mode != 1) ? (damage.m_pickaxe <= 0f) : (damage.m_chop <= 0f));
		}
		if (flag2)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log(string.Format("Wrong/missing tool: \"{0}\" — re-equipping for mode={1}", rightItem?.m_shared?.m_name ?? "NONE", mode));
			}
			ItemDrop.ItemData itemData = FindBestTool(mode);
			if (itemData == null)
			{
				RestoreLoadout();
				ResetToIdle();
				return;
			}
			EquipTool(itemData);
			rightItem = ReflectionHelper.GetRightItem(_humanoid);
			if (rightItem == null)
			{
				_attackTimer = 0.25f;
				return;
			}
		}
		bool flag3 = _humanoid != null && _humanoid.InAttack();
		bool flag4 = _humanoid != null && _humanoid.InDodge();
		bool flag5 = _character != null && _character.IsStaggering();
		bool flag6 = flag3 && !flag4 && !flag5 && _chopChain && CurrentAttackCanChain();
		if ((flag3 && !flag6) | flag4 | flag5)
		{
			if ((flag4 | flag5) && CompanionsPlugin.LogNormal)
			{
				LogInfo($"Attack blocked: inDodge={flag4} stagger={flag5} — retry in {0.25f}s");
			}
			_attackTimer = (flag3 ? 0f : 0.25f);
			return;
		}
		bool flag7 = WantsPowerChop(rightItem);
		float num5 = (flag7 ? rightItem.m_shared.m_secondaryAttack.m_attackStamina : rightItem.m_shared.m_attack.m_attackStamina);
		if (_stamina != null && num5 > 0f)
		{
			float num6 = Mathf.Max(1f, _stamina.MaxStamina);
			if (_catchingBreath)
			{
				if (_stamina.Stamina < (ModConfig.GatherResumeStamina?.Value ?? 0.6f) * num6)
				{
					_chopChain = false;
					_attackTimer = 0.5f;
					return;
				}
				_catchingBreath = false;
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Got its breath back ({_stamina.Stamina:F0}/{num6:F0}) — back to work");
				}
			}
			if (_stamina.Stamina < num5 + (ModConfig.GatherRestStamina?.Value ?? 0.15f) * num6)
			{
				_catchingBreath = true;
				_chopChain = false;
				_attackTimer = 0.5f;
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Catching its breath ({_stamina.Stamina:F0}/{num6:F0} stamina) before swinging again");
				}
				return;
			}
		}
		bool flag8 = _humanoid != null && _humanoid.StartAttack(null, flag7);
		_totalAttempts++;
		_chopChain = flag8 && !flag7;
		_attackTimer = (flag8 ? 0.4f : 0.5f);
		if (flag8)
		{
			_swingCount++;
			_consecutiveFailures = 0;
			if (IsRockLikeTarget() && _target != null)
			{
				if (_targetType == "MineRock5" && _rock5ActiveSections > 0)
				{
					int num7 = CountActiveRock5Sections();
					if (num7 < _rock5ActiveSections)
					{
						if (CountExposedRock5Sections() == 0 && num7 > 0)
						{
							MHR_GatherRules.BlacklistTarget(this);
							ResetToIdle();
							return;
						}
						_rock5ActiveSections = num7;
						_rock5CurrentSegment = null;
						_totalAttempts = 0;
						_swingCount = 0;
						_consecutiveFailures = 0;
						_reapproach = false;
						_attackFaceLocked = false;
						_moveTimer = 0f;
						_moveLogTimer = 0f;
						_moveTargetLocked = false;
						_state = HarvestState.Moving;
						return;
					}
					_rock5ActiveSections = num7;
				}
				Vector3 effectiveTargetPosition2 = GetEffectiveTargetPosition();
				float num8 = Vector3.Distance(effectiveTargetPosition2, _attackNodePos);
				if (num8 > 1.5f)
				{
					float num9 = Vector3.Distance(transform.position, effectiveTargetPosition2);
					if (CompanionsPlugin.LogDetailed)
					{
						Log(_targetType + " section destroyed — repositioning to next node " + $"(dist={num9:F1}m shift={num8:F1}m)");
					}
					_attackNodePos = effectiveTargetPosition2;
					_reapproach = true;
					_attackFaceLocked = false;
					_moveTimer = 0f;
					_moveLogTimer = 0f;
					_moveTargetLocked = false;
					_state = HarvestState.Moving;
					return;
				}
			}
		}
		else
		{
			_consecutiveFailures++;
			if (_consecutiveFailures >= 2)
			{
				_reapproach = true;
				_attackFaceLocked = false;
				_consecutiveFailures = 0;
				_moveTimer = 0f;
				_moveLogTimer = 0f;
				_moveTargetLocked = false;
				_state = HarvestState.Moving;
				return;
			}
		}
		if (flag8 && _targetType == "MineRock5" && _swingCount > 0 && _swingCount % 100000 == 0)
		{
			_rock5CurrentSegment = null;
			_swingCount = 0;
			_consecutiveFailures = 0;
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
			return;
		}
		if (flag8 && _swingCount >= 1 && !(_targetType == "TreeLog"))
		{
			_ = _targetType == "TreeBase";
		}
		if (flag8 && SwingsNotLanding())
		{
			if (_targetType == "TreeLog" && !_triedOtherSide)
			{
				_triedOtherSide = true;
				_logBadPoints.Add(_logPoint);
				_logPointAt = -10f;
				_noDamageSwings = 0;
				if (CompanionsPlugin.LogNormal)
				{
					LogInfo("Its swings aren't reaching \"" + _target.name + "\" (something in the way) - trying another side");
				}
				_reapproach = true;
				_attackFaceLocked = false;
				_moveTimer = 0f;
				_moveLogTimer = 0f;
				_moveTargetLocked = false;
				_state = HarvestState.Moving;
			}
			else
			{
				if (CompanionsPlugin.LogNormal)
				{
					LogInfo("Its swings aren't doing anything to \"" + _target.name + "\" (something in the way) - leaving it for now");
				}
				_blacklist[_target.GetInstanceID()] = Time.time;
				ResetToIdle();
			}
		}
		else if (flag8 && _swingCount > 0 && _swingCount % 2 == 0 && num > attackRange * 0.85f && !HoldingGoodSpot)
		{
			_reapproach = true;
			_attackFaceLocked = false;
			_moveTimer = 0f;
			_moveLogTimer = 0f;
			_moveTargetLocked = false;
			_state = HarvestState.Moving;
		}
		else if (_swingCount >= 20 || _totalAttempts >= 30)
		{
			LogWarn($"Bailout — {_swingCount} hits, {_totalAttempts} attempts on \"{_target.name}\" " + $"dist={num:F1}m without destruction. Abandoning target.");
			_blacklist[_target.GetInstanceID()] = Time.time;
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Blacklisted \"{_target.name}\" (id={_target.GetInstanceID()}) for {BlacklistDuration}s");
			}
			ResetToIdle();
		}
		else if (flag8)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Attack swing → \"" + _target.name + "\" " + $"success=True dist={num:F1}m " + "weapon=\"" + (rightItem?.m_shared?.m_name ?? "NONE") + "\" " + $"power={flag7} swings={_swingCount}/{20} " + $"attempts={_totalAttempts}/{30}");
			}
		}
		else if (_totalAttempts <= 1 || _totalAttempts % 5 == 0)
		{
			Animator animator = _humanoid?.GetComponentInChildren<Animator>();
			string arg = "unknown";
			if (animator != null)
			{
				AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
				arg = $"hash={currentAnimatorStateInfo.shortNameHash} norm={currentAnimatorStateInfo.normalizedTime:F2}";
			}
			float num10 = ((_stamina != null) ? _stamina.Stamina : (-1f));
			float num11 = ((_stamina != null) ? _stamina.MaxStamina : (-1f));
			LogWarn($"Attack FAILED — attempts={_totalAttempts}/{30} " + $"animState=[{arg}] power={flag7} " + "weapon=\"" + (rightItem?.m_shared?.m_name ?? "NONE") + "\" " + $"stamina={num10:F0}/{num11:F0} " + $"atkCost={rightItem.m_shared.m_attack.m_attackStamina:F0}");
		}
	}

	private void UpdateCollectingDrops()
	{
		float deltaTime = Time.deltaTime;
		_dropTimer += deltaTime;
		if (_dropTimer > 10f)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Drop collection timeout ({10f}s) — picked up {_dropsPickedUp} items, resuming harvest");
			}
			FinishDropCollection();
			return;
		}
		if (_dropScanDelayTimer > 0f)
		{
			_dropScanDelayTimer -= deltaTime;
			return;
		}
		if (_currentDrop != null)
		{
			if (_currentDrop == null || _currentDrop.GetComponent<ZNetView>() == null || !_currentDrop.GetComponent<ZNetView>().IsValid())
			{
				if (CompanionsPlugin.LogDetailed)
				{
					Log("Current drop target gone — scanning for more");
				}
				_currentDrop = null;
			}
			else
			{
				float num = Vector3.Distance(transform.position, _currentDrop.transform.position);
				if (!(num <= 2.2f))
				{
					MHR_Mover.Go(_ai, deltaTime, _currentDrop.transform.position, 1.5f, allowRun: true, "Gather");
					_moveLogTimer -= deltaTime;
					if (_moveLogTimer <= 0f)
					{
						_moveLogTimer = 2f;
						string arg = _currentDrop.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "?";
						if (CompanionsPlugin.LogDetailed)
						{
							Log($"Approaching drop \"{arg}\" dist={num:F1}m " + $"(pickup range={3f:F1}) timer={_dropTimer:F1}s");
						}
					}
					return;
				}
				TryPickupDrop(_currentDrop);
				_currentDrop = null;
			}
		}
		GameObject gameObject = ScanForDrops();
		if (gameObject == null)
		{
			if (_dropsPickedUp != 0 || !(_dropTimer < 1.5f))
			{
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"No more drops nearby — picked up {_dropsPickedUp} items, resuming harvest " + string.Format("(last scan: {0} items in {1:F0}m, skipped {2} not wanted, {3} not auto-pickup, {4} no room{5})", _scanSeen, DropScanRadius, _scanUnwanted, _scanNoAuto, _scanFull, IsOverweight() ? ", overweight" : ""));
				}
				FinishDropCollection();
			}
		}
		else
		{
			_currentDrop = gameObject;
			float num2 = Vector3.Distance(transform.position, gameObject.transform.position);
			string arg2 = gameObject.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? "?";
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Moving to drop: \"{arg2}\" dist={num2:F1}m pos={gameObject.transform.position:F1}");
			}
		}
	}

	internal void ResetToIdle()
	{
		HarvestState state = _state;
		_state = HarvestState.Idle;
		_target = null;
		_targetType = null;
		_swingCount = 0;
		_totalAttempts = 0;
		_consecutiveFailures = 0;
		_reapproach = false;
		_rock5ActiveSections = -1;
		_rock5CurrentSegment = null;
		_lastAoeSwing = -1;
		_scanTimer = 0f;
		_pendingToolReequip = null;
		_moveTargetLocked = false;
		_attackFaceLocked = false;
		UnclaimTarget();
		if (_isDirectedHarvest)
		{
			_isDirectedHarvest = false;
			_directedTarget = null;
			RestoreLoadout();
			if (CompanionsPlugin.LogDetailed)
			{
				Log("ResetToIdle (directed harvest complete) — restored loadout");
			}
		}
		bool flag = _setup != null && _setup.GetFollow();
		if (((_ai != null) & flag) && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
		if (state != HarvestState.Idle && CompanionsPlugin.LogDetailed)
		{
			Log($"ResetToIdle (was {state}) — follow={flag}");
		}
	}

	private bool IsRockLikeTarget()
	{
		if (!(_targetType == "MineRock5") && !(_targetType == "MineRock"))
		{
			return _targetType == "Destructible";
		}
		return true;
	}

	private void ApplyDirectRock5Damage()
	{
		if (_target == null)
		{
			return;
		}
		MineRock5 component = _target.GetComponent<MineRock5>();
		if (component == null)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem == null)
		{
			return;
		}
		Collider collider = null;
		float num = float.MaxValue;
		foreach (Transform item in _target.transform)
		{
			if (!item.gameObject.activeSelf)
			{
				continue;
			}
			Collider component2 = item.GetComponent<Collider>();
			if (!(component2 == null) && IsSegmentExposed(component2))
			{
				float num2 = Vector3.Distance(transform.position, component2.bounds.center);
				if (num2 < num)
				{
					num = num2;
					collider = component2;
				}
			}
		}
		if (collider == null)
		{
			return;
		}
		Vector3 vector = SafeClosestPoint(collider, transform.position);
		HitData hitData = new HitData();
		hitData.m_point = vector;
		hitData.m_dir = (vector - transform.position).normalized;
		hitData.m_hitCollider = collider;
		hitData.m_toolTier = (short)rightItem.m_shared.m_toolTier;
		hitData.m_damage = rightItem.GetDamage();
		hitData.m_hitType = HitData.HitType.PlayerHit;
		hitData.SetAttacker(_character);
		float randomSkillFactor = _character.GetRandomSkillFactor(rightItem.m_shared.m_skillType);
		if (randomSkillFactor != 1f)
		{
			hitData.m_damage.Modify(randomSkillFactor);
		}
		if (_stamina != null)
		{
			float attackStamina = rightItem.m_shared.m_attack.m_attackStamina;
			if (attackStamina > 0f)
			{
				_stamina.UseStamina(attackStamina);
			}
		}
		IsApplyingAoeDamage = true;
		component.Damage(hitData);
		IsApplyingAoeDamage = false;
	}

	private void ApplyDirectDamage()
	{
		if (_target == null)
		{
			return;
		}
		IDestructible component = _target.GetComponent<IDestructible>();
		if (component == null)
		{
			return;
		}
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem == null)
		{
			return;
		}
		Vector3 vector = _target.transform.position;
		Collider collider = null;
		if (_target.GetComponent<MineRock>() != null)
		{
			float num = float.MaxValue;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (!(collider2 == null) && collider2.gameObject.activeSelf)
				{
					float num2 = Vector3.Distance(transform.position, collider2.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = collider2;
					}
				}
			}
			if (collider == null)
			{
				return;
			}
			vector = collider.bounds.center;
		}
		HitData hitData = new HitData();
		hitData.m_point = vector;
		hitData.m_dir = (vector - transform.position).normalized;
		hitData.m_hitCollider = collider;
		hitData.m_toolTier = (short)rightItem.m_shared.m_toolTier;
		hitData.m_damage = rightItem.GetDamage();
		hitData.m_hitType = HitData.HitType.PlayerHit;
		hitData.SetAttacker(_character);
		float randomSkillFactor = _character.GetRandomSkillFactor(rightItem.m_shared.m_skillType);
		if (randomSkillFactor != 1f)
		{
			hitData.m_damage.Modify(randomSkillFactor);
		}
		if (_stamina != null)
		{
			float attackStamina = rightItem.m_shared.m_attack.m_attackStamina;
			if (attackStamina > 0f)
			{
				_stamina.UseStamina(attackStamina);
			}
		}
		IsApplyingAoeDamage = true;
		component.Damage(hitData);
		IsApplyingAoeDamage = false;
	}

	private static bool IsSegmentExposed(Collider col)
	{
		if (col == null)
		{
			return false;
		}
		Vector3 center = col.bounds.center;
		if (ZoneSystem.instance == null)
		{
			return true;
		}
		float groundHeight = ZoneSystem.instance.GetGroundHeight(center);
		return center.y >= groundHeight - 1f;
	}

	private static Vector3 SafeClosestPoint(Collider col, Vector3 point)
	{
		if (col is MeshCollider)
		{
			return col.ClosestPointOnBounds(point);
		}
		return col.ClosestPoint(point);
	}

	private static int GetMinToolTier(GameObject target)
	{
		if (target == null)
		{
			return 0;
		}
		TreeBase component = target.GetComponent<TreeBase>();
		if (component != null)
		{
			return component.m_minToolTier;
		}
		TreeLog component2 = target.GetComponent<TreeLog>();
		if (component2 != null)
		{
			return component2.m_minToolTier;
		}
		MineRock5 component3 = target.GetComponent<MineRock5>();
		if (component3 != null)
		{
			return component3.m_minToolTier;
		}
		MineRock component4 = target.GetComponent<MineRock>();
		if (component4 != null)
		{
			return component4.m_minToolTier;
		}
		Destructible component5 = target.GetComponent<Destructible>();
		if (component5 != null)
		{
			return component5.m_minToolTier;
		}
		return 0;
	}

	private int GetBestToolTier(int mode)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return -1;
		}
		int num = -1;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared != null && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)) && allItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow && string.IsNullOrEmpty(allItem.m_shared.m_ammoType))
			{
				HitData.DamageTypes damage = allItem.GetDamage();
				if (((mode != 1) ? (damage.m_pickaxe > 0f) : (damage.m_chop > 0f)) && allItem.m_shared.m_toolTier > num)
				{
					num = allItem.m_shared.m_toolTier;
				}
			}
		}
		return num;
	}

	private static string ClassifyTargetType(GameObject target, int mode)
	{
		if (target == null)
		{
			return null;
		}
		if (mode == 1)
		{
			if (target.GetComponent<TreeBase>() != null)
			{
				return "TreeBase";
			}
			if (target.GetComponent<TreeLog>() != null)
			{
				return "TreeLog";
			}
			if (target.GetComponent<Destructible>() != null && target.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return "Stump";
			}
		}
		if (target.GetComponent<MineRock5>() != null)
		{
			return "MineRock5";
		}
		if (target.GetComponent<MineRock>() != null)
		{
			return "MineRock";
		}
		if (target.GetComponent<Destructible>() != null)
		{
			return "Destructible";
		}
		return "Unknown";
	}

	private void ClaimTarget()
	{
		UnclaimTarget();
		_claimedTargetId = _target?.GetInstanceID() ?? 0;
		if (_claimedTargetId != 0)
		{
			s_claimedTargets.Add(_claimedTargetId);
		}
	}

	private ItemDrop.ItemData FindBestTool(int mode)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			LogWarn("FindBestTool — inventory is null!");
			return null;
		}
		ItemDrop.ItemData itemData = null;
		float num = 0f;
		int num2 = 0;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared == null || (allItem.m_shared.m_useDurability && allItem.m_durability <= 0f) || allItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow || !string.IsNullOrEmpty(allItem.m_shared.m_ammoType) || allItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo)
			{
				continue;
			}
			HitData.DamageTypes damage = allItem.GetDamage();
			bool flag = false;
			float num3 = 0f;
			if (mode == 1)
			{
				if (damage.m_chop > 0f)
				{
					flag = true;
					num3 = damage.m_chop;
				}
			}
			else if (damage.m_pickaxe > 0f)
			{
				flag = true;
				num3 = damage.m_pickaxe;
			}
			if (flag)
			{
				num2++;
				if (CompanionsPlugin.LogDetailed)
				{
					Log("  Tool candidate: \"" + allItem.m_shared.m_name + "\" " + $"type={allItem.m_shared.m_itemType} " + $"chop={damage.m_chop:F0} pick={damage.m_pickaxe:F0} " + $"relevantDmg={num3:F0} " + $"equipped={_humanoid.IsItemEquiped(allItem)}");
				}
				if (num3 > num)
				{
					itemData = allItem;
					num = num3;
				}
			}
		}
		if (itemData != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Best tool: \"{itemData.m_shared.m_name}\" dmg={num:F0} " + $"(out of {num2} matching tools)");
			}
		}
		else
		{
			LogWarn("No matching tools found! " + string.Format("Mode={0} need={1} ", mode, (mode == 1) ? "m_chop > 0" : "m_pickaxe > 0") + $"totalItems={inventory.GetAllItems().Count}");
		}
		return itemData;
	}

	private void EquipTool(ItemDrop.ItemData tool)
	{
		if (tool == null || _humanoid == null || _humanoid.InAttack() || _humanoid.InDodge())
		{
			return;
		}
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = true;
		}
		_setup?.MHR_DropQueuedHandItems();
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		ItemDrop.ItemData leftItem = ReflectionHelper.GetLeftItem(_humanoid);
		bool flag = rightItem == tool;
		bool flag2 = _humanoid.IsItemEquiped(tool);
		if (flag | flag2)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("EquipTool: \"" + tool.m_shared.m_name + "\" already equipped " + $"(sameRef={flag} isEquipped={flag2}) — skipping");
			}
			_toolEquipped = true;
			return;
		}
		bool flag3 = rightItem != null && rightItem.m_shared?.m_name == tool.m_shared?.m_name;
		if (CompanionsPlugin.LogDetailed)
		{
			Log("EquipTool: equipping \"" + tool.m_shared.m_name + "\" (right=\"" + (rightItem?.m_shared?.m_name ?? "none") + "\" left=\"" + (leftItem?.m_shared?.m_name ?? "none") + "\") " + $"sameNameInHand={flag3} " + $"rightHash={rightItem?.GetHashCode() ?? 0} toolHash={tool.GetHashCode()} " + "SuppressAutoEquip=true");
		}
		if (flag3)
		{
			if (leftItem != null)
			{
				_humanoid.UnequipItem(leftItem, triggerEquipEffects: false);
			}
			_toolEquipped = true;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("After equip (same-name shortcut): rightItem=\"" + (ReflectionHelper.GetRightItem(_humanoid)?.m_shared?.m_name ?? "NONE") + "\" " + $"attackRange={GetAttackRange():F1}");
			}
			return;
		}
		if (rightItem != null)
		{
			_humanoid.UnequipItem(rightItem, triggerEquipEffects: false);
		}
		MHR_Equip.ClearLeftFor(_humanoid, leftItem, tool);
		_humanoid.EquipItem(tool, triggerEquipEffects: false);
		ItemDrop.ItemData rightItem2 = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem2 != null)
		{
			_toolEquipped = true;
			_pendingToolReequip = null;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("After equip: rightItem=\"" + (rightItem2.m_shared?.m_name ?? "?") + "\" " + $"attackRange={GetAttackRange():F1}");
			}
		}
		else
		{
			_toolEquipped = false;
			_pendingToolReequip = tool;
			LogWarn("After equip: rightItem=\"NONE\" — EQUIP FAILED (will retry). " + $"Tried to equip \"{tool.m_shared.m_name}\" type={tool.m_shared.m_itemType} " + "inAttack=" + (_humanoid.InAttack() ? "true" : "false"));
		}
	}

	private static bool HasOreDrops(DropTable table)
	{
		if (table == null || table.m_drops.Count == 0)
		{
			return false;
		}
		foreach (DropTable.DropData drop in table.m_drops)
		{
			if (!(drop.m_item == null))
			{
				string text = drop.m_item.name;
				if (text.IndexOf("Ore", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Scrap", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Obsidian", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Chitin", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Flametal", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("BlackMetal", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool IsOreNamedRock(string goName)
	{
		if (goName.IndexOf("copper", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("tin", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("silver", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("iron", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("meteorite", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("blackmetal", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("flametal", StringComparison.OrdinalIgnoreCase) < 0 && goName.IndexOf("obsidian", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return goName.IndexOf("chitin", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private void LogInfo(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Gather|" + text + "] " + msg);
		}
	}

	private void LogWarn(string msg)
	{
	}

	private void LogInventoryContents()
	{
	}

	private static bool IsTargetValid(GameObject target)
	{
		return target != null;
	}

	private float TargetHealth()
	{
		ZDO zDO = ((!(_target != null)) ? null : _target.GetComponent<ZNetView>()?.GetZDO());
		if (zDO == null)
		{
			return -1f;
		}
		TreeLog component = _target.GetComponent<TreeLog>();
		if (component != null)
		{
			return zDO.GetFloat(ZDOVars.s_health, component.m_health);
		}
		TreeBase component2 = _target.GetComponent<TreeBase>();
		if (component2 != null)
		{
			return zDO.GetFloat(ZDOVars.s_health, component2.m_health);
		}
		Destructible component3 = _target.GetComponent<Destructible>();
		if (component3 != null)
		{
			return zDO.GetFloat(ZDOVars.s_health, component3.m_health);
		}
		return -1f;
	}

	internal void OnHitRegistered(GameObject go)
	{
		if (!(go == null) && !(_target == null) && (!(go != _target) || _target.transform.IsChildOf(go.transform) || go.transform.IsChildOf(_target.transform)))
		{
			_lastHitAt = Time.time;
			_noDamageSwings = 0;
			if (!_hasGoodSpot || Vector3.Distance(_goodSpot, transform.position) > 0.7f)
			{
				_hasGoodSpot = true;
				_goodSpot = transform.position;
			}
		}
	}

	private bool SwingsNotLanding()
	{
		if (_hitCheckFor != _target)
		{
			_hitCheckFor = _target;
			_noDamageSwings = 0;
			_triedOtherSide = false;
			_hasGoodSpot = false;
			_logBadPoints.Clear();
			_lastSwingAt = Time.time;
			_lastHitAt = -100f;
			return false;
		}
		bool flag = _lastHitAt < _lastSwingAt;
		_lastSwingAt = Time.time;
		if (!flag)
		{
			return false;
		}
		_noDamageSwings++;
		if (_noDamageSwings >= 1)
		{
			_hasGoodSpot = false;
		}
		return _noDamageSwings >= 2;
	}

	private bool TooFarFromYou()
	{
		if (!_isDirectedHarvest && _setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null)
		{
			return Vector3.Distance(transform.position, MHR_Owner.Of(this).transform.position) > PlayerRange() + 15f;
		}
		return false;
	}

	private void NoteLogsAround()
	{
		_preLogs.Clear();
		if (_target == null || GetMode() != 1)
		{
			return;
		}
		Collider[] array = Physics.OverlapSphere(_target.transform.position, 15f);
		foreach (Collider collider in array)
		{
			TreeLog treeLog = ((collider != null) ? collider.GetComponentInParent<TreeLog>() : null);
			if (treeLog != null && !_ownPieces.Contains(treeLog.gameObject.GetInstanceID()))
			{
				_preLogs.Add(treeLog.gameObject.GetInstanceID());
			}
		}
	}

	private bool IsOwnPiece(GameObject go, string type)
	{
		if (type != "TreeLog" || Time.time - _splitAt > 60f)
		{
			return false;
		}
		int instanceID = go.GetInstanceID();
		if (_ownPieces.Contains(instanceID))
		{
			return true;
		}
		if (_preLogs.Contains(instanceID) || Vector3.Distance(go.transform.position, _splitPos) > 30f)
		{
			return false;
		}
		_ownPieces.Add(instanceID);
		return true;
	}

	private bool LogSettling()
	{
		if (_targetType != "TreeLog" || _target == null)
		{
			return false;
		}
		Rigidbody component = _target.GetComponent<Rigidbody>();
		if (!(component != null) || component.isKinematic || (!(component.linearVelocity.magnitude > 0.3f) && !(component.angularVelocity.magnitude > 0.3f)))
		{
			_settleFor = null;
			return false;
		}
		if (_settleFor != _target)
		{
			_settleFor = _target;
			_settleSince = Time.time;
			if (_settleLoggedFor != _target)
			{
				_settleLoggedFor = _target;
				if (CompanionsPlugin.LogNormal)
				{
					LogInfo("Waiting for \"" + _target.name + "\" to stop moving");
				}
			}
		}
		if (Time.time - _settleSince > 8f)
		{
			SetAside("it keeps rolling");
			return true;
		}
		_logPointAt = -10f;
		_ai?.StopMoving();
		return true;
	}

	private void SetAside(string why)
	{
		if (!(_target == null))
		{
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo("Leaving \"" + _target.name + "\" for a moment - " + why);
			}
			_blacklist[_target.GetInstanceID()] = Time.time - Mathf.Max(0f, BlacklistDuration - 15f);
			_settleFor = null;
			ResetToIdle();
		}
	}

	private bool LogOutOfReach()
	{
		if (_targetType != "TreeLog" || _target == null)
		{
			return false;
		}
		LogReachPoint();
		if (_logPointFound)
		{
			return false;
		}
		SetAside("no part of it is in reach from the ground");
		return true;
	}

	private bool LineToLog(Vector3 ground, Vector3 p)
	{
		if (s_lineMask == -1)
		{
			s_lineMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece");
		}
		Vector3 vector = ground + Vector3.up * 1f;
		Vector3 vector2 = p - vector;
		float num = vector2.magnitude - 0.1f;
		if (num <= 0.1f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(vector, vector2.normalized, num, s_lineMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			if (!(raycastHit.collider == null) && !(raycastHit.collider.GetComponentInParent<Character>() != null) && !raycastHit.collider.transform.IsChildOf(_target.transform))
			{
				return false;
			}
		}
		return true;
	}

	private Vector3 LogReachPoint()
	{
		if (_target == _logPointFor && _logPointAt > 0f && Vector3.Distance(_target.transform.position, _logPointLogPos) < 0.5f)
		{
			return _logPoint;
		}
		_logPointLogPos = _target.transform.position;
		_logPointFor = _target;
		_logPointAt = Time.time;
		_logPoint = _target.transform.position;
		_logPointFound = false;
		if (s_groundMask == -1)
		{
			s_groundMask = LayerMask.GetMask("terrain", "Default", "static_solid", "piece");
		}
		List<Collider> list = new List<Collider>();
		Bounds bounds = default;
		bool flag = false;
		Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!(collider == null) && collider.enabled && !collider.isTrigger && !(collider is MeshCollider { convex: false }))
			{
				list.Add(collider);
				if (flag)
				{
					bounds.Encapsulate(collider.bounds);
					continue;
				}
				bounds = collider.bounds;
				flag = true;
			}
		}
		if (!flag)
		{
			return _logPoint;
		}
		Vector3 position = transform.position;
		float num = float.MaxValue;
		for (float num2 = bounds.min.x - 1.5f; num2 <= bounds.max.x + 1.5f; num2++)
		{
			for (float num3 = bounds.min.z - 1.5f; num3 <= bounds.max.z + 1.5f; num3++)
			{
				if (!Physics.Raycast(new Vector3(num2, bounds.max.y + 2f, num3), Vector3.down, out var hitInfo, bounds.size.y + 8f, s_groundMask) || hitInfo.collider.GetComponentInParent<TreeLog>() != null || hitInfo.collider.GetComponentInParent<TreeBase>() != null)
				{
					continue;
				}
				Vector3 point = hitInfo.point;
				foreach (Collider item in list)
				{
					Vector3 p;
					try
					{
						p = item.ClosestPoint(point + Vector3.up * 0.8f);
					}
					catch
					{
						continue;
					}
					float num4 = p.y - point.y;
					float magnitude = new Vector2(p.x - point.x, p.z - point.z).magnitude;
					if (!(num4 > 1.2f) && !(num4 < -0.5f) && !(magnitude > 1f) && LineToLog(point, p) && (!(_hitCheckFor == _target) || !_logBadPoints.Exists((Vector3 b) => Vector3.Distance(b, p) < 1.5f)))
					{
						float num5 = Vector3.Distance(position, point);
						if (num5 < num)
						{
							num = num5;
							_logPoint = p;
							_logPointFound = true;
						}
					}
				}
			}
		}
		return _logPoint;
	}

	private Vector3 GetEffectiveTargetPosition()
	{
		if (_target == null)
		{
			return base.transform.position;
		}
		if (_targetType == "MineRock5")
		{
			Collider collider = null;
			float num = float.MaxValue;
			foreach (Transform item in _target.transform)
			{
				if (!item.gameObject.activeSelf)
				{
					continue;
				}
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					float num2 = Vector3.Distance(transform.position, component.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = component;
					}
				}
			}
			if (collider == null)
			{
				foreach (Transform item2 in _target.transform)
				{
					if (!item2.gameObject.activeSelf)
					{
						continue;
					}
					Collider component2 = item2.GetComponent<Collider>();
					if (!(component2 == null))
					{
						float num3 = Vector3.Distance(transform.position, component2.bounds.center);
						if (num3 < num)
						{
							num = num3;
							collider = component2;
						}
					}
				}
			}
			if (collider != null)
			{
				_rock5CurrentSegment = collider;
				Vector3 center = collider.bounds.center;
				float y = ((ZoneSystem.instance != null) ? ZoneSystem.instance.GetGroundHeight(center) : center.y);
				return new Vector3(center.x, y, center.z);
			}
		}
		if (_targetType == "Destructible")
		{
			Vector3 vector = _target.transform.position;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (collider2.gameObject.activeSelf && !collider2.isTrigger)
				{
					vector = collider2.bounds.center;
					break;
				}
			}
			Vector3 vector2 = transform.position + Vector3.up * 0.8f;
			Vector3 vector3 = vector - vector2;
			if (vector3.sqrMagnitude > 0.01f)
			{
				RaycastHit[] array = Physics.RaycastAll(vector2, vector3.normalized, vector3.magnitude + 3f);
				for (int j = 0; j < array.Length; j++)
				{
					RaycastHit raycastHit = array[j];
					if (raycastHit.collider.GetComponentInParent<Destructible>()?.gameObject == _target)
					{
						return raycastHit.point;
					}
				}
			}
			return vector;
		}
		if (_targetType == "TreeLog")
		{
			return LogReachPoint();
		}
		if (_targetType == "MineRock")
		{
			float num4 = float.MaxValue;
			Vector3 result = _target.transform.position;
			bool flag = false;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider3 in componentsInChildren)
			{
				if (collider3.gameObject.activeSelf && !collider3.isTrigger)
				{
					Vector3 vector4 = collider3.bounds.ClosestPoint(transform.position);
					float num5 = Vector3.Distance(transform.position, vector4);
					if (num5 < num4)
					{
						num4 = num5;
						result = vector4;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return result;
			}
		}
		return _target.transform.position;
	}

	private float GetAttackRange()
	{
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
		if (rightItem?.m_shared?.m_attack != null)
		{
			return rightItem.m_shared.m_attack.m_attackRange;
		}
		return 2f;
	}

	private Vector3 GetRockFacePosition()
	{
		if (_target == null)
		{
			return base.transform.position;
		}
		if (_targetType == "MineRock5")
		{
			Collider rock5CurrentSegment = _rock5CurrentSegment;
			if (rock5CurrentSegment != null && rock5CurrentSegment.gameObject.activeSelf)
			{
				Vector3 result = SafeClosestPoint(rock5CurrentSegment, base.transform.position);
				result.y = Mathf.Max(result.y, base.transform.position.y + 0.1f);
				return result;
			}
			Collider collider = null;
			float num = float.MaxValue;
			foreach (Transform item in _target.transform)
			{
				if (!item.gameObject.activeSelf)
				{
					continue;
				}
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					float num2 = Vector3.Distance(transform.position, component.bounds.center);
					if (num2 < num)
					{
						num = num2;
						collider = component;
					}
				}
			}
			if (collider != null)
			{
				Vector3 result2 = SafeClosestPoint(collider, transform.position);
				result2.y = Mathf.Max(result2.y, transform.position.y + 0.1f);
				return result2;
			}
		}
		if (_targetType == "Destructible")
		{
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider2 in componentsInChildren)
			{
				if (collider2.gameObject.activeSelf && !collider2.isTrigger)
				{
					return collider2.bounds.center;
				}
			}
			return _target.transform.position;
		}
		if (_targetType == "MineRock")
		{
			float num3 = float.MaxValue;
			Vector3 result3 = _target.transform.position;
			bool flag = false;
			Collider[] componentsInChildren = _target.GetComponentsInChildren<Collider>();
			foreach (Collider collider3 in componentsInChildren)
			{
				if (collider3.gameObject.activeSelf && !collider3.isTrigger)
				{
					Vector3 b = collider3.bounds.ClosestPoint(transform.position);
					float num4 = Vector3.Distance(transform.position, b);
					if (num4 < num3)
					{
						num3 = num4;
						result3 = collider3.bounds.center;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return result3;
			}
		}
		return _target.transform.position;
	}

	private void FaceTarget()
	{
		if (_target == null || _ai == null || _character == null)
		{
			return;
		}
		Vector3 vector;
		if (_state != HarvestState.Attacking || !IsRockLikeTarget())
		{
			vector = GetEffectiveTargetPosition();
		}
		else
		{
			vector = (_attackFaceLocked ? _lockedAttackFaceTarget : GetRockFacePosition());
		}
		Vector3 vector2 = vector - transform.position;
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.1f)
		{
			vector = _target.transform.position;
		}
		_ai.LookAtPoint(vector);
		if (_state == HarvestState.Attacking)
		{
			Vector3 vector3 = vector - transform.position;
			vector3.y = 0f;
			if (vector3.sqrMagnitude > 0.001f)
			{
				Quaternion q = Quaternion.LookRotation(vector3.normalized);
				_character.SetLookDir(vector3.normalized);
				MHR_Nav.TurnTo(transform, q);
			}
		}
	}

	private int CountActiveRock5Sections()
	{
		if (_target == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Transform item in _target.transform)
		{
			if (item.gameObject.activeSelf && item.GetComponent<Collider>() != null)
			{
				num++;
			}
		}
		return num;
	}

	private bool MHR_DoorBusy()
	{
		DoorHandler component = GetComponent<DoorHandler>();
		if (component == null)
		{
			return false;
		}
		return component.IsActive;
	}

	private bool IsLowTarget()
	{
		if (_targetType == "TreeLog" || _targetType == "Stump")
		{
			return true;
		}
		if (_targetType == "TreeBase" && _target != null && _target.transform.position.y - transform.position.y < -0.3f)
		{
			return true;
		}
		return false;
	}

	private void LateUpdate()
	{
		if (_character == null || IsCompanionUIOpen())
		{
			return;
		}
		if (_state == HarvestState.Moving || (_state == HarvestState.CollectingDrops && _currentDrop != null))
		{
			bool flag = _targetType == "Pickable" && _state == HarvestState.Moving;
			_character.SetRun(!flag && _shouldRun);
		}
		if (_state != HarvestState.Attacking || !(_target != null) || !(_ai != null) || _ai.IsInCombat || MHR_Loot.Active(this) || _character.InAttack() || !IsLowTarget() || IsRockLikeTarget() || HoldingGoodSpot)
		{
			return;
		}
		Vector3 effectiveTargetPosition = GetEffectiveTargetPosition();
		float num = Vector3.Distance(transform.position, effectiveTargetPosition);
		float attackRange = GetAttackRange();
		int num2;
		float num3;
		if (_swingCount < 2)
		{
			num2 = ((_totalAttempts >= 2) ? 1 : 0);
			if (num2 == 0)
			{
				num3 = attackRange * 1f;
				goto IL_012e;
			}
		}
		else
		{
			num2 = 1;
		}
		num3 = attackRange * 1f;
		goto IL_012e;
		IL_012e:
		float num4 = num3;
		float dist = ((num2 != 0) ? (attackRange * 0.8f) : (attackRange * 0.8f));
		if (num > num4)
		{
			_ai.MHR_Go(Time.deltaTime, effectiveTargetPosition, dist, run: false, "Harvest");
		}
	}

	private GameObject TryFindNearbyLog(Vector3 center)
	{
		return MHR_GatherRules.NearestLog(this, center);
	}

	private int CountExposedRock5Sections()
	{
		if (_target == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Transform item in _target.transform)
		{
			if (item.gameObject.activeSelf)
			{
				Collider component = item.GetComponent<Collider>();
				if (!(component == null) && IsSegmentExposed(component))
				{
					num++;
				}
			}
		}
		return num;
	}

	private void FinishDropCollection()
	{
		_currentDrop = null;
		_state = HarvestState.Idle;
		_scanTimer = 0f;
		UnclaimTarget();
		bool flag = _setup != null && _setup.GetFollow();
		if (((_ai != null) & flag) && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"FinishDropCollection — collected {_dropsPickedUp} items, follow={flag}");
		}
	}

	private void TryPickupDrop(GameObject dropGO)
	{
		if (dropGO == null || _humanoid == null)
		{
			return;
		}
		if (IsOverweight())
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Skipping pickup — overweight ({GetCurrentWeight():F1} >= {OverweightThreshold})");
			}
			return;
		}
		ItemDrop component = dropGO.GetComponent<ItemDrop>();
		if (component == null)
		{
			return;
		}
		string arg = component.m_itemData?.m_shared?.m_name ?? "?";
		int num = component.m_itemData?.m_stack ?? 0;
		if (_humanoid.Pickup(dropGO, autoequip: false, autoPickupDelay: false))
		{
			_dropsPickedUp++;
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Picked up: \"{arg}\" x{num} (total {_dropsPickedUp} this cycle)");
			}
		}
		else if (CompanionsPlugin.LogDetailed)
		{
			Log($"Failed to pick up: \"{arg}\" x{num} — inventory full or item invalid");
		}
	}

	private GameObject ScanForDrops()
	{
		int num = Physics.OverlapSphereNonAlloc(_lastDestroyPos, DropScanRadius, _dropBuffer, _itemLayerMask);
		if (num > _dropBuffer.Length)
		{
			num = _dropBuffer.Length;
		}
		GameObject result = null;
		float num2 = float.MaxValue;
		int num3 = 0;
		_scanSeen = 0;
		_scanUnwanted = 0;
		_scanNoAuto = 0;
		_scanFull = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _dropBuffer[i];
			if (collider == null || collider.attachedRigidbody == null)
			{
				continue;
			}
			ItemDrop component = collider.attachedRigidbody.GetComponent<ItemDrop>();
			if (component == null)
			{
				continue;
			}
			_scanSeen++;
			if (!component.m_autoPickup)
			{
				_scanNoAuto++;
				continue;
			}
			ZNetView component2 = component.GetComponent<ZNetView>();
			if (component2 == null || !component2.IsValid())
			{
				continue;
			}
			if (!MHR_GatherRules.WantDrop(this, component))
			{
				_scanUnwanted++;
			}
			else
			{
				if (IsOverweight())
				{
					continue;
				}
				Inventory inventory = _humanoid?.GetInventory();
				if (inventory != null)
				{
					component.Load();
					if (!inventory.CanAddItem(component.m_itemData) && !MHR_GatherRules.TryMakeRoom(this, component.m_itemData))
					{
						string text = component.m_itemData?.m_shared?.m_name ?? "?";
						if (CompanionsPlugin.LogDetailed)
						{
							Log("Drop scan: skipping \"" + text + "\" — inventory full/can't add");
						}
						_scanFull++;
						continue;
					}
				}
				num3++;
				float num4 = Vector3.Distance(transform.position, component.transform.position);
				if (num4 < num2)
				{
					num2 = num4;
					result = component.gameObject;
				}
			}
		}
		if (num3 > 0 && CompanionsPlugin.LogDetailed)
		{
			Log($"Drop scan: {num3} pickable items within {DropScanRadius}m of destroy pos");
		}
		return result;
	}

	private float GetCurrentWeight()
	{
		return (_humanoid?.GetInventory())?.GetTotalWeight() ?? 0f;
	}

	internal GameObject ScanForTarget(int mode)
	{
		if (mode == 5)
		{
			return ScanForPickable();
		}
		int num = Physics.OverlapSphereNonAlloc(ScanCenter(), ScanRad(), _scanBuffer);
		bool flag = num >= _scanBuffer.Length;
		if (num > _scanBuffer.Length)
		{
			num = _scanBuffer.Length;
		}
		if (CompanionsPlugin.LogNormal)
		{
			LogInfo($"Physics scan: {num} colliders in {ScanRadius}m radius" + (flag ? " ** BUFFER FULL — may miss targets! **" : ""));
		}
		int bestToolTier = GetBestToolTier(mode);
		LastTierSkips = 0;
		LastTierName = null;
		_seenIds.Clear();
		GameObject gameObject = null;
		float num2 = float.MaxValue;
		_bestTree = null;
		_bestTreeDist = float.MaxValue;
		GameObject gameObject2 = null;
		float num3 = float.MaxValue;
		GameObject gameObject3 = null;
		float num4 = float.MaxValue;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		int num11 = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _scanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			GameObject harvestCandidateWithType = GetHarvestCandidateWithType(collider, mode, out var type);
			if (harvestCandidateWithType == null || !MHR_GatherRules.AllowTarget(this, harvestCandidateWithType, mode) || !_seenIds.Add(harvestCandidateWithType.GetInstanceID()) || ((mode == 2 || mode == 3) && IsBuilt(harvestCandidateWithType)))
			{
				continue;
			}
			int minToolTier = GetMinToolTier(harvestCandidateWithType);
			if (minToolTier > bestToolTier)
			{
				num10++;
				LastTierName = LastTierName ?? MHR_Items.NiceName(harvestCandidateWithType);
				if (num10 <= 3)
				{
					LogWarn($"  Tier SKIP \"{harvestCandidateWithType.name}\" — requires tier {minToolTier}, " + $"best tool tier={bestToolTier} (need bronze pickaxe tier≥1 for copper/silver)");
				}
			}
			else if (_blacklist.ContainsKey(harvestCandidateWithType.GetInstanceID()))
			{
				if (num5 == 0 && CompanionsPlugin.LogDetailed)
				{
					Log($"  Skipping blacklisted \"{harvestCandidateWithType.name}\" (id={harvestCandidateWithType.GetInstanceID()})");
				}
			}
			else
			{
				if (s_claimedTargets.Contains(harvestCandidateWithType.GetInstanceID()))
				{
					continue;
				}
				bool flag2 = !TooFarFromYou() && IsOwnPiece(harvestCandidateWithType, type);
				if (!flag2 && !InWorkArea(harvestCandidateWithType.transform.position, type))
				{
					num11++;
					continue;
				}
				num5++;
				float num12 = Vector3.Distance(transform.position, harvestCandidateWithType.transform.position);
				bool flag3 = type == "TreeBase";
				if (flag3)
				{
					num6++;
				}
				else if (type == "TreeLog")
				{
					num7++;
				}
				else if (type == "Stump")
				{
					num8++;
				}
				else
				{
					num9++;
				}
				if ((num5 <= 5 || type == "TreeLog") && CompanionsPlugin.LogDetailed)
				{
					Log($"  Candidate #{num5}: \"{harvestCandidateWithType.name}\" " + $"type={type} dist={num12:F1}m pos={harvestCandidateWithType.transform.position:F1}");
				}
				if (flag3)
				{
					if (num12 < _bestTreeDist)
					{
						_bestTreeDist = num12;
						_bestTree = harvestCandidateWithType;
					}
				}
				else if (flag2 && num12 < num4)
				{
					num4 = num12;
					gameObject3 = harvestCandidateWithType;
					if (num12 < num2)
					{
						num2 = num12;
						gameObject = harvestCandidateWithType;
					}
				}
				else if (type == "Stump")
				{
					if (num12 < num3)
					{
						num3 = num12;
						gameObject2 = harvestCandidateWithType;
					}
				}
				else if (num12 < num2)
				{
					num2 = num12;
					gameObject = harvestCandidateWithType;
				}
			}
		}
		if (num5 > 5 && CompanionsPlugin.LogDetailed)
		{
			Log($"  ...and {num5 - 5} more candidates");
		}
		if (num10 > 0 && CompanionsPlugin.LogDetailed)
		{
			Log($"  Skipped {num10} targets requiring higher tool tier (best tier={bestToolTier})");
		}
		LastTierSkips = num10;
		string text = mode switch
		{
			3 => "Ore", 
			1 => "Wood", 
			_ => "Stone", 
		};
		if (mode == 1)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"  Breakdown [{text}]: {num6} TreeBase, {num7} TreeLog, {num8} Stump " + $"(total {num5} unique targets, {num11} out of range)");
			}
		}
		else if (CompanionsPlugin.LogDetailed)
		{
			Log($"  Breakdown [{text}]: {num9} deposits (total {num5} unique targets)");
		}
		if (gameObject3 != null)
		{
			gameObject = gameObject3;
			num2 = num4;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("Finishing the log it just broke");
			}
		}
		if (gameObject == null && gameObject2 != null)
		{
			gameObject = gameObject2;
			num2 = num3;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("No logs found - taking the nearest stump");
			}
		}
		if (gameObject == null && _bestTree != null)
		{
			gameObject = _bestTree;
			num2 = _bestTreeDist;
			if (CompanionsPlugin.LogDetailed)
			{
				Log("No logs/stumps found — falling back to standing tree");
			}
		}
		if (gameObject != null && CompanionsPlugin.LogDetailed)
		{
			Log($"Best target: \"{gameObject.name}\" dist={num2:F1}m " + $"(out of {num5} total candidates)");
		}
		return gameObject;
	}

	private bool LooseOn(int mode, out bool noTool)
	{
		noTool = BestToolQuiet(mode) == null;
		ConfigEntry<bool> harvestPickUpLoose = ModConfig.HarvestPickUpLoose;
		if (harvestPickUpLoose != null && !harvestPickUpLoose.Value)
		{
			return false;
		}
		return EarlyGame();
	}

	private bool EarlyGame()
	{
		if (Time.time < _nextEarlyCheck)
		{
			return _early;
		}
		_nextEarlyCheck = Time.time + 30f;
		bool early = _early;
		string text = null;
		bool flag = _setup != null && _setup.HasHomePosition();
		Vector3 center = (flag ? _setup.GetHomePosition() : transform.position);
		float radius = (flag ? MHR_ToolKeeper.HomeRange() : 30f);
		if (HasGoodAxe(_humanoid?.GetInventory()))
		{
			text = "it has a bronze-or-better axe";
		}
		else if (MHR_Owner.IsMine(this) && HasGoodAxe(Player.m_localPlayer.GetInventory()))
		{
			text = "you have a bronze-or-better axe";
		}
		else
		{
			foreach (Container item in MHR_Chests.Near(center, radius))
			{
				if (item != null && item.GetComponent<Character>() == null && HasGoodAxe(item.GetInventory()))
				{
					text = "there's a bronze-or-better axe in a chest";
					break;
				}
			}
			if (text == null)
			{
				foreach (CraftingStation item2 in MHR_Chests.Stations(center, radius))
				{
					if (item2 != null && item2.m_name == "$piece_forge")
					{
						text = "there's a forge" + (flag ? " at home" : " nearby");
						break;
					}
				}
			}
		}
		_early = text == null;
		if (_early != early || !_earlyLogged)
		{
			_earlyLogged = true;
			if (CompanionsPlugin.LogNormal)
			{
				LogInfo(_early ? "Early game (no bronze-or-better axe and no forge) - picking up branches, stones and flint" : ("Not picking up branches, stones and flint: " + text));
			}
		}
		return _early;
	}

	private static bool HasGoodAxe(Inventory inv)
	{
		if (inv == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			if (allItem?.m_shared != null && allItem.m_shared.m_skillType == Skills.SkillType.Axes && !(allItem.m_shared.m_damages.m_chop <= 0f))
			{
				string text = ((allItem.m_dropPrefab != null) ? allItem.m_dropPrefab.name : "");
				if (text != "AxeStone" && text != "AxeFlint")
				{
					return true;
				}
			}
		}
		return false;
	}

	private ItemDrop.ItemData BestToolQuiet(int mode)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return null;
		}
		ItemDrop.ItemData result = null;
		float num = 0f;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared != null && (!allItem.m_shared.m_useDurability || !(allItem.m_durability <= 0f)) && allItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow && string.IsNullOrEmpty(allItem.m_shared.m_ammoType) && allItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo)
			{
				HitData.DamageTypes damage = allItem.GetDamage();
				float num2 = ((mode == 1) ? damage.m_chop : damage.m_pickaxe);
				if (num2 > num)
				{
					num = num2;
					result = allItem;
				}
			}
		}
		return result;
	}

	private GameObject ScanForPickable(int looseMode = 0)
	{
		int num = Physics.OverlapSphereNonAlloc(ScanCenter(), ScanRad(), _scanBuffer);
		if (num > _scanBuffer.Length)
		{
			num = _scanBuffer.Length;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"Forage scan: {num} colliders in {ScanRadius}m radius");
		}
		_seenIds.Clear();
		GameObject gameObject = null;
		float num2 = float.MaxValue;
		int num3 = 0;
		for (int i = 0; i < num; i++)
		{
			Collider collider = _scanBuffer[i];
			if (collider == null)
			{
				continue;
			}
			Pickable componentInParent = collider.GetComponentInParent<Pickable>();
			if (componentInParent == null)
			{
				continue;
			}
			bool num4;
			if (looseMode != 0)
			{
				num4 = !MHR_GatherRules.AllowLoose(this, componentInParent, looseMode);
			}
			else
			{
				if (!MHR_ForageAllowed(componentInParent))
				{
					continue;
				}
				num4 = !ForageUnderAmount(componentInParent);
			}
			if (num4)
			{
				continue;
			}
			GameObject gameObject2 = componentInParent.gameObject;
			if (!_seenIds.Add(gameObject2.GetInstanceID()))
			{
				continue;
			}
			ZNetView component = gameObject2.GetComponent<ZNetView>();
			if ((!(component != null) || component.GetZDO() == null || !component.GetZDO().GetBool(s_pickedHash)) && !_blacklist.ContainsKey(gameObject2.GetInstanceID()) && InWorkArea(gameObject2.transform.position))
			{
				num3++;
				float num5 = Vector3.Distance(transform.position, gameObject2.transform.position);
				if (num3 <= 5 && CompanionsPlugin.LogDetailed)
				{
					Log($"  Pickable #{num3}: \"{gameObject2.name}\" dist={num5:F1}m " + $"pos={gameObject2.transform.position:F1}");
				}
				if (num5 < num2)
				{
					num2 = num5;
					gameObject = gameObject2;
				}
			}
		}
		if (num3 > 5 && CompanionsPlugin.LogDetailed)
		{
			Log($"  ...and {num3 - 5} more pickables");
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"  Forage breakdown: {num3} pickable targets");
		}
		if (gameObject != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Best pickable: \"{gameObject.name}\" dist={num2:F1}m " + $"(out of {num3} total)");
			}
		}
		else if (num3 == 0 && CompanionsPlugin.LogNormal)
		{
			LogInfo($"Forage scan: no pickables found within {ScanRadius}m");
		}
		return gameObject;
	}

	private static bool IsBuilt(GameObject go)
	{
		if (!(go.GetComponentInParent<Piece>() != null) && !(go.GetComponentInParent<WearNTear>() != null))
		{
			return go.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static GameObject GetHarvestCandidateWithType(Collider col, int mode, out string type)
	{
		type = null;
		switch (mode)
		{
		case 1:
		{
			TreeBase componentInParent6 = col.GetComponentInParent<TreeBase>();
			if (componentInParent6 != null)
			{
				type = "TreeBase";
				return componentInParent6.gameObject;
			}
			TreeLog componentInParent7 = col.GetComponentInParent<TreeLog>();
			if (componentInParent7 != null)
			{
				type = "TreeLog";
				return componentInParent7.gameObject;
			}
			Destructible componentInParent8 = col.GetComponentInParent<Destructible>();
			if (componentInParent8 != null && componentInParent8.m_damages.m_chop != HitData.DamageModifier.Immune && componentInParent8.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				type = "Stump";
				return componentInParent8.gameObject;
			}
			break;
		}
		case 3:
		{
			MineRock5 componentInParent3 = col.GetComponentInParent<MineRock5>();
			if (componentInParent3 != null)
			{
				type = "MineRock5";
				return componentInParent3.gameObject;
			}
			MineRock componentInParent4 = col.GetComponentInParent<MineRock>();
			if (componentInParent4 != null)
			{
				bool flag = HasOreDrops(componentInParent4.m_dropItems);
				bool flag2 = IsOreNamedRock(componentInParent4.gameObject.name);
				if (flag | flag2)
				{
					type = "MineRock";
					return componentInParent4.gameObject;
				}
			}
			Destructible componentInParent5 = col.GetComponentInParent<Destructible>();
			if (componentInParent5 != null && componentInParent5.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (componentInParent5.m_damages.m_chop == HitData.DamageModifier.Immune || componentInParent5.m_damages.m_chop == HitData.DamageModifier.Ignore))
			{
				DropOnDestroyed component = componentInParent5.GetComponent<DropOnDestroyed>();
				bool num = component != null && HasOreDrops(component.m_dropWhenDestroyed);
				bool flag3 = IsOreNamedRock(componentInParent5.gameObject.name);
				if (num | flag3)
				{
					type = "Destructible";
					return componentInParent5.gameObject;
				}
			}
			break;
		}
		default:
		{
			MineRock componentInParent = col.GetComponentInParent<MineRock>();
			if (componentInParent != null && !HasOreDrops(componentInParent.m_dropItems) && !IsOreNamedRock(componentInParent.gameObject.name))
			{
				type = "MineRock";
				return componentInParent.gameObject;
			}
			Destructible componentInParent2 = col.GetComponentInParent<Destructible>();
			if (componentInParent2 != null && componentInParent2.m_damages.m_pickaxe != HitData.DamageModifier.Immune && (componentInParent2.m_damages.m_chop == HitData.DamageModifier.Immune || componentInParent2.m_damages.m_chop == HitData.DamageModifier.Ignore))
			{
				type = "Destructible";
				return componentInParent2.gameObject;
			}
			break;
		}
		}
		return null;
	}

	internal static bool MHR_ForageAllowed(Pickable p)
	{
		if (!IsForageItemAllowed(p))
		{
			return false;
		}
		string text = p.gameObject.name;
		if (!text.StartsWith("Pickable_Turnip") && !text.StartsWith("Pickable_Carrot") && !text.StartsWith("Pickable_Onion") && (text.Contains("_Wild") || (!text.StartsWith("Pickable_Barley") && !text.StartsWith("Pickable_Flax"))) && !FarmController.MHR_SafeCultivated(p.transform.position))
		{
			return true;
		}
		return false;
	}

	private static bool IsForageItemAllowed(Pickable pickable)
	{
		if (pickable.m_itemPrefab == null)
		{
			return false;
		}
		string prefabName = Utils.GetPrefabName(pickable.m_itemPrefab);
		string text = MHR_List.Local(pickable.m_itemPrefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name);
		return MHR_List.Get(ModConfig.ForageItems.Value).Allows(prefabName, text);
	}

	private bool ForageUnderAmount(Pickable pickable)
	{
		if (pickable.m_itemPrefab == null)
		{
			return true;
		}
		string prefabName = Utils.GetPrefabName(pickable.m_itemPrefab);
		int num = MHR_List.Get(ModConfig.ForageItems.Value).AmountFor(prefabName, 0);
		if (num > 0)
		{
			return MHR_GatherRules.Stock(this, prefabName) < num;
		}
		return true;
	}

	private static void RefreshForageFilter()
	{
		string value = ModConfig.ForageItems.Value;
		if (value == _forageFilterRaw)
		{
			return;
		}
		_forageFilterRaw = value;
		if (string.IsNullOrWhiteSpace(value) || value.Trim() == "*")
		{
			_forageFilter = null;
			return;
		}
		_forageFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] array = value.Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length > 0)
			{
				_forageFilter.Add(text);
			}
		}
	}

	private static bool DropsOnlyStone(DropTable table)
	{
		if (table == null || table.m_drops.Count == 0)
		{
			return true;
		}
		foreach (DropTable.DropData drop in table.m_drops)
		{
			if (!(drop.m_item == null))
			{
				string text = drop.m_item.name;
				if (text != "Stone" && text != "Flint")
				{
					return false;
				}
			}
		}
		return true;
	}

	internal Container FindNearestChest(Vector3 searchCenter)
	{
		return MHR_GatherRules.DropOff(this, searchCenter);
	}

	private bool IsTreeTarget()
	{
		if (!(_targetType == "TreeBase"))
		{
			return _targetType == "TreeLog";
		}
		return true;
	}
}
