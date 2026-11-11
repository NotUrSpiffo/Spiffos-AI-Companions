using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

public class CompanionAI : BaseAI
{
	[Header("Sleep")]
	public bool m_sleeping;

	public float m_wakeupRange = 5f;

	public bool m_noiseWakeup;

	public float m_maxNoiseWakeupRange = 50f;

	public EffectList m_wakeupEffects = new EffectList();

	public EffectList m_sleepEffects = new EffectList();

	public float m_wakeUpDelayMin;

	public float m_wakeUpDelayMax;

	public float m_fallAsleepDistance;

	private float m_sleepDelay = 0.5f;

	private float m_sleepTimer;

	private static readonly int s_sleeping = ZSyncAnimation.GetHash("sleeping");

	private static readonly int s_crouching = ZSyncAnimation.GetHash("crouching");

	internal Character m_targetCreature;

	internal StaticTarget m_targetStatic;

	internal Vector3 m_lastKnownTargetPos = Vector3.zero;

	internal bool m_beenAtLastPos;

	private GameObject m_follow;

	internal float FreezeTimer;

	private bool _isDvergerSupport;

	private Character _healTarget;

	private float _healScanTimer;

	private float _healEquipTimer;

	private float _healLogTimer;

	private float _healUnreachableTimer;

	private float _healLastDistance = -1f;

	private const float HealThreshold = 0.7f;

	private const float HealScanInterval = 1f;

	private const float HealLogInterval = 3f;

	private const float HealRange = 50f;

	private const float HealUnreachableTimeout = 6f;

	private bool _isOnShip;

	private Ship _attachedShip;

	private Chair _attachedChair;

	private Vector3 _shipLocalPos;

	private Quaternion _shipLocalRot;

	private Collider[] _shipColliders;

	private ZSyncAnimation _zanim;

	internal Vagon PendingCartAttach;

	internal Humanoid PendingCartHumanoid;

	private float _pendingCartTimeout;

	internal Vector3? PendingMoveTarget;

	private float _pendingMoveTimeout;

	internal Container PendingDepositContainer;

	internal Humanoid PendingDepositHumanoid;

	private float _pendingDepositTimeout;

	private bool _depositChestOpen;

	private float _depositWorkTimer;

	private int _depositCount;

	private readonly List<ItemDrop.ItemData> _depositQueue = new List<ItemDrop.ItemData>();

	private const float DepositItemInterval = 0.6f;

	private TombStone _pendingTombstone;

	private float _tombstoneScanTimer;

	private float _tombstoneNavTimeout;

	private float _tombstoneNavStuckTime;

	private float _tombRecoverSnooze;

	private const float TombstoneLootRange = 3f;

	private int _autoPickupMask;

	private readonly Collider[] _autoPickupBuffer = new Collider[32];

	private const float AutoPickupPullSpeed = 15f;

	internal static EffectList DrownEffects;

	private CompanionStamina _companionStamina;

	private float _drownDamageTimer;

	private float _hazardRecoveryTimer;

	private static readonly int s_taredHash = "Tared".GetStableHashCode();

	private bool _wasInFormation;

	private int _lastFollowMode;

	private int _formationSlot = -1;

	private CompanionSetup _setup;

	private CompanionCombatAI _combatAI;

	private HarvestController _harvest;

	private RepairController _repair;

	private SmeltController _smelt;

	private FarmController _farm;

	private HuntController _hunt;

	private FishController _fish;

	private UpkeepController _upkeep;

	private IdleController _idle;

	private CookController _cook;

	private DoorHandler _doorHandler;

	private CompanionRest _rest;

	private CompanionTalk _talk;

	private Rigidbody _body;

	private float _torchCheckTimer;

	private const float TorchCheckInterval = 3f;

	private bool _torchWanted;

	private float _torchDrainAt;

	private bool _torchBurnedOut;

	private string _lightLogKey;

	private const float SuppressedMoveInterval = 9999f;

	private float _furnitureStuckTimer;

	private const float FurnitureStuckThreshold = 3f;

	private float _continuousSwimTimer;

	private readonly Dictionary<Vector3Int, float> _targetBlacklist = new Dictionary<Vector3Int, float>();

	private const float BlacklistDuration = 30f;

	private const float BlacklistCellSize = 2f;

	private float _blacklistCleanupTimer;

	private const float RSItemTransferDelay = 0.6f;

	private const float RSMoveTimeout = 15f;

	internal bool WantsTorch => _torchWanted;

	internal bool IsOnShip => _isOnShip;

	internal bool IsBoardingShip => MHR_Board.Active(this);

	private static float TombstoneScanInterval => ModConfig.TombstoneScanInterval.Value;

	private static float TombstoneNavTimeoutMax => ModConfig.TombstoneNavTimeout.Value;

	private static float AutoPickupRange => ModConfig.AutoPickupRange.Value;

	private static float FollowTeleportDist => ModConfig.FollowTeleportDistance.Value;

	private static float FormationOffset => ModConfig.FormationOffset.Value;

	private static float FormationCatchupDist => ModConfig.FormationCatchupDist.Value;

	internal bool IsInCombat
	{
		get
		{
			if (_combatAI != null)
			{
				return _combatAI.IsEngaged;
			}
			return false;
		}
	}

	internal bool IsRecoveringTombstone => _pendingTombstone != null;

	internal bool HasOrder
	{
		get
		{
			if (!MHR_Hive.Active(this) && !MHR_Board.Active(this) && !PendingMoveTarget.HasValue && !(PendingCartAttach != null) && !(PendingDepositContainer != null))
			{
				return _pendingTombstone != null;
			}
			return true;
		}
	}

	internal void SetPatrolPointAt(Vector3 point)
	{
		if (m_nview?.GetZDO() != null)
		{
			m_nview.GetZDO().Set(ZDOVars.s_patrol, value: true);
			m_nview.GetZDO().Set(ZDOVars.s_patrolPoint, point);
		}
	}

	public void SetFollowTarget(GameObject go)
	{
		m_follow = go;
		if (go == null)
		{
			_formationSlot = -1;
		}
	}

	internal void IgnorePlayerCollision()
	{
		Player localPlayer = Player.m_localPlayer;
		if (!(localPlayer == null))
		{
			Collider component = GetComponent<Collider>();
			Collider component2 = localPlayer.GetComponent<Collider>();
			if (component != null && component2 != null)
			{
				Physics.IgnoreCollision(component, component2, ignore: true);
			}
		}
	}

	internal void CancelMoveTarget()
	{
		if (PendingMoveTarget.HasValue)
		{
			PendingMoveTarget = null;
			RestoreFollowOrPatrol();
		}
	}

	internal void CancelPendingCart()
	{
		if (!(PendingCartAttach == null))
		{
			PendingCartAttach = null;
			PendingCartHumanoid = null;
			RestoreFollowOrPatrol();
		}
	}

	internal void CancelPendingShipBoard()
	{
		if (MHR_Board.Active(this))
		{
			MHR_Board.Cancel(this);
			RestoreFollowOrPatrol();
		}
	}

	internal void SetHiveOrder(Beehive hive)
	{
		SetFollowTarget(null);
		MHR_Hive.Start(this, hive);
	}

	internal void CancelHiveOrder()
	{
		if (MHR_Hive.Active(this))
		{
			MHR_Hive.Cancel(this);
			RestoreFollowOrPatrol();
		}
	}

	internal void BoardingEnded()
	{
		RestoreFollowOrPatrol();
	}

	internal void CancelPendingDeposit()
	{
		if (!(PendingDepositContainer == null))
		{
			if (_depositChestOpen && PendingDepositContainer != null)
			{
				PendingDepositContainer.SetInUse(inUse: false);
				_depositChestOpen = false;
			}
			PendingDepositContainer = null;
			PendingDepositHumanoid = null;
			_depositQueue.Clear();
			_depositCount = 0;
			RestoreFollowOrPatrol();
		}
	}

	internal void CancelTombstoneRecovery()
	{
		bool num = _pendingTombstone != null || (object)_pendingTombstone != null;
		bool flag = false;
		ZDO zDO = m_nview?.GetZDO();
		if (zDO != null)
		{
			flag = zDO.GetLong(CompanionSetup.TombstoneIdHash, 0L) != 0;
		}
		if (num | flag)
		{
			_tombRecoverSnooze = 30f;
			_pendingTombstone = null;
			ClearTombstoneId();
			RestoreFollowOrPatrol();
		}
	}

	protected override void Awake()
	{
		base.Awake();
		m_pathAgentType = Pathfinding.AgentType.Humanoid;
		m_avoidWater = false;
		_setup = GetComponent<CompanionSetup>();
		_combatAI = GetComponent<CompanionCombatAI>();
		_harvest = GetComponent<HarvestController>();
		_repair = GetComponent<RepairController>();
		_smelt = GetComponent<SmeltController>();
		_farm = GetComponent<FarmController>();
		_hunt = GetComponent<HuntController>();
		_fish = GetComponent<FishController>();
		_upkeep = GetComponent<UpkeepController>();
		_idle = GetComponent<IdleController>();
		_cook = GetComponent<CookController>();
		_doorHandler = GetComponent<DoorHandler>();
		_rest = GetComponent<CompanionRest>();
		_talk = GetComponent<CompanionTalk>();
		_body = GetComponent<Rigidbody>();
		_zanim = GetComponent<ZSyncAnimation>();
		_autoPickupMask = LayerMask.GetMask("item");
		_companionStamina = GetComponent<CompanionStamina>();
		ZDO zDO = m_nview.GetZDO();
		if (zDO != null)
		{
			m_sleeping = zDO.GetBool(ZDOVars.s_sleeping, m_sleeping);
			if (m_animator != null)
			{
				m_animator.SetBool(s_sleeping, IsSleeping());
			}
		}
		if (m_wakeUpDelayMin > 0f || m_wakeUpDelayMax > 0f)
		{
			m_sleepDelay = UnityEngine.Random.Range(m_wakeUpDelayMin, m_wakeUpDelayMax);
		}
		m_nview.Register("RPC_Wakeup", RPC_Wakeup);
		m_nview.Register("RPC_Sleep", RPC_Sleep);
		Animator componentInChildren = GetComponentInChildren<Animator>();
		if (componentInChildren != null && componentInChildren.runtimeAnimatorController != null)
		{
			_ = componentInChildren.runtimeAnimatorController.name;
		}
		if (componentInChildren != null)
		{
			_ = componentInChildren.parameterCount;
		}
		gameObject.name.Contains("Dverger");
		_isDvergerSupport = gameObject.name.Contains("DvergerSupport");
		IgnorePlayerCollision();
	}

	private void RPC_Wakeup(long sender)
	{
		if (!m_nview.GetZDO().IsOwner())
		{
			m_sleeping = false;
		}
	}

	private void RPC_Sleep(long sender)
	{
		if (!m_nview.GetZDO().IsOwner())
		{
			m_sleeping = true;
		}
	}

	private static bool IsTorch(ItemDrop.ItemData it)
	{
		if (it != null && it.m_shared != null)
		{
			return it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch;
		}
		return false;
	}

	private void UpdateTorch()
	{
		Humanoid humanoid = m_character as Humanoid;
		if (humanoid == null)
		{
			return;
		}
		float time = Time.time;
		ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(humanoid);
		ItemDrop.ItemData leftItem = ReflectionHelper.GetLeftItem(humanoid);
		ItemDrop.ItemData itemData;
		if (IsTorch(rightItem))
		{
			itemData = rightItem;
		}
		else
		{
			itemData = (IsTorch(leftItem) ? leftItem : null);
		}
		if (IsTorch(itemData) && itemData.m_shared.m_useDurability)
		{
			ConfigEntry<bool> torchBurnsDown = ModConfig.TorchBurnsDown;
			if (torchBurnsDown == null || torchBurnsDown.Value)
			{
				float num = ((_torchDrainAt > 0f) ? Mathf.Min(time - _torchDrainAt, 5f) : 0f);
				_torchDrainAt = time;
				itemData.m_durability -= itemData.m_shared.m_durabilityDrain * num * Game.m_durabilityRate;
				if (itemData.m_durability <= 0f)
				{
					humanoid.UnequipItem(itemData, triggerEquipEffects: false);
					if (itemData.m_shared.m_destroyBroken)
					{
						humanoid.GetInventory()?.RemoveItem(itemData);
					}
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Torch] \"" + m_character.m_name + "\": the torch burned out");
					}
					_torchBurnedOut = true;
					_torchCheckTimer = 0f;
					itemData = null;
				}
				goto IL_014e;
			}
		}
		_torchDrainAt = 0f;
		goto IL_014e;
		IL_014e:
		_torchCheckTimer -= Time.deltaTime;
		if (_torchCheckTimer > 0f)
		{
			return;
		}
		_torchCheckTimer = 1f;
		Inventory inventory = humanoid.GetInventory();
		if (inventory != null)
		{
			List<ItemDrop.ItemData> list = null;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (IsTorch(allItem) && allItem.m_shared.m_useDurability && allItem.m_shared.m_destroyBroken && allItem.m_durability <= 0f)
				{
					(list ?? (list = new List<ItemDrop.ItemData>())).Add(allItem);
				}
			}
			if (list != null)
			{
				foreach (ItemDrop.ItemData item in list)
				{
					if (item == itemData)
					{
						itemData = null;
					}
					humanoid.UnequipItem(item, triggerEquipEffects: false);
					inventory.RemoveItem(item);
				}
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Torch] \"" + m_character.m_name + "\": the torch was used up");
				}
				_torchBurnedOut = true;
			}
		}
		bool flag = _combatAI != null && _combatAI.IsEngaged;
		bool flag2 = itemData != null && itemData == leftItem;
		bool flag3 = (_torchWanted = (!flag | flag2) && IsDarkHere() && !LitBaseNear() && !YouHoldTorchNear());
		if (TorchOpener.IsActive(humanoid) || humanoid.InAttack())
		{
			return;
		}
		bool flag4 = _setup != null && _setup.SuppressAutoEquip;
		if (flag3 && !flag && !flag4 && !IsTorch(itemData) && !m_character.IsSwimming())
		{
			ItemDrop.ItemData itemData2 = null;
			Inventory inventory2 = humanoid.GetInventory();
			if (inventory2 != null)
			{
				foreach (ItemDrop.ItemData allItem2 in inventory2.GetAllItems())
				{
					if (IsTorch(allItem2) && (!allItem2.m_shared.m_useDurability || allItem2.m_durability > 0f) && (itemData2 == null || allItem2.m_durability > itemData2.m_durability))
					{
						itemData2 = allItem2;
					}
				}
			}
			bool flag5 = rightItem == null && inventory2 != null && inventory2.GetAllItems().Exists((ItemDrop.ItemData it) => it.m_shared != null && (it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) && (!it.m_shared.m_useDurability || it.m_durability > 0f));
			if (itemData2 != null)
			{
				if (!flag5)
				{
					humanoid.EquipItem(itemData2);
					_torchBurnedOut = false;
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Torch] \"" + m_character.m_name + "\" lit a torch (" + (itemData2.m_shared.m_useDurability ? ((itemData2.m_durability / Mathf.Max(1f, itemData2.GetMaxDurability()) * 100f).ToString("F0") + "% left") : "no wear") + ")");
					}
				}
			}
			else if (_torchBurnedOut)
			{
				_torchBurnedOut = false;
				GetComponent<CompanionTalk>()?.SayProblem("no_torches", ModLocalization.Loc("hc_say_no_torches"));
			}
		}
		else if (!flag3 && IsTorch(itemData))
		{
			humanoid.UnequipItem(itemData, triggerEquipEffects: false);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Torch] \"" + m_character.m_name + "\" put the torch away");
			}
			_setup?.SyncEquipmentToInventory();
		}
	}

	private bool IsDarkHere()
	{
		Vector3 position = transform.position;
		if (!Character.InInterior(position))
		{
			return EnvMan.IsNight();
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || !localPlayer.InInterior() || Vector3.Distance(localPlayer.transform.position, position) > 100f)
		{
			return true;
		}
		float grayscale = RenderSettings.ambientLight.grayscale;
		float num = ModConfig.TorchDarkThreshold?.Value ?? 0.4f;
		bool flag = grayscale < num;
		string text = EnvMan.instance?.GetCurrentEnvironment()?.m_name ?? "?";
		string text2 = text + (flag ? "/dark" : "/lit");
		if (text2 != _lightLogKey)
		{
			_lightLogKey = text2;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo(string.Format("[Torch] \"{0}\" in {1}, light {2:F2} ({3}, DarkLevel {4:F2})", m_character.m_name, text, grayscale, flag ? "dark" : "light enough", num));
			}
		}
		return flag;
	}

	private bool LitBaseNear()
	{
		Vector3 position = transform.position;
		try
		{
			if (EffectArea.IsPointInsideArea(position, EffectArea.Type.PlayerBase) == null)
			{
				return false;
			}
			Collider[] array = Physics.OverlapSphere(position, 8f);
			foreach (Collider collider in array)
			{
				Fireplace fireplace = ((collider != null) ? collider.GetComponentInParent<Fireplace>() : null);
				if (fireplace != null && fireplace.IsBurning())
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private bool YouHoldTorchNear()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer != null && IsTorch(ReflectionHelper.GetRightItem(localPlayer)))
		{
			return Vector3.Distance(localPlayer.transform.position, transform.position) < 5f;
		}
		return false;
	}

	public GameObject GetFollowTarget()
	{
		return m_follow;
	}

	public override Character GetTargetCreature()
	{
		return m_targetCreature;
	}

	public StaticTarget GetStaticTarget()
	{
		return m_targetStatic;
	}

	public void ClearTargets()
	{
		if (!(m_targetCreature != null))
		{
			_ = m_targetStatic != null;
		}
		m_targetCreature = null;
		m_targetStatic = null;
	}

	private void ClearCreatureTarget()
	{
		m_targetCreature = null;
	}

	internal bool GameMoveTo(float dt, Vector3 point, float dist, bool run)
	{
		return MoveTo(dt, point, dist, run);
	}

	internal bool MHR_Go(float dt, Vector3 point, float dist, bool run, string tag)
	{
		return MHR_Mover.Go(this, dt, point, dist, run, tag, run ? 3 : 2);
	}

	internal void LookAtPoint(Vector3 point)
	{
		LookAt(point);
	}

	internal bool IsLookingAtPoint(Vector3 point, float angle, bool invert = false)
	{
		return IsLookingAt(point, angle, invert);
	}

	internal new void SetAlerted(bool alert)
	{
		base.SetAlerted(alert);
	}

	internal Character FindNearbyEnemy()
	{
		return FindEnemy();
	}

	private static bool IsPassiveAnimal(Character c)
	{
		return MHR_Threat.IsPassive(c);
	}

	internal void FollowObject(GameObject go, float dt)
	{
		Follow(go, dt);
	}

	internal void PushDirection(Vector3 dir, bool run)
	{
		MoveTowards(dir, run);
	}

	internal void RandomMovement(float dt, Vector3 center)
	{
		RandomMovement(dt, center, false);
	}

	internal void RandomMovementAroundPoint(float dt, Vector3 point, float distance, bool run)
	{
		RandomMovementArroundPoint(dt, point, distance, run);
	}

	internal bool FleeFrom(float dt, Vector3 from)
	{
		return Flee(dt, from);
	}

	internal new bool GetPatrolPoint(out Vector3 point)
	{
		return base.GetPatrolPoint(out point);
	}

	internal new Character HaveFriendInRange(float range)
	{
		return base.HaveFriendInRange(range);
	}

	internal new Character HaveHurtFriendInRange(float range)
	{
		return base.HaveHurtFriendInRange(range);
	}

	internal new void HaveFriendsInRange(float range, out Character hurtFriend, out Character friend)
	{
		base.HaveFriendsInRange(range, out hurtFriend, out friend);
	}

	internal new bool IsTakingOff()
	{
		return base.IsTakingOff();
	}

	internal void SetFormationSlot(int slot)
	{
		_formationSlot = slot;
	}

	internal void SetPendingCart(Vagon vagon, Humanoid humanoid)
	{
		PendingCartAttach = vagon;
		PendingCartHumanoid = humanoid;
		_pendingCartTimeout = 20f;
		SetFollowTarget(null);
	}

	private void RestoreFollowOrPatrol()
	{
		if (_setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null)
		{
			SetFollowTarget(MHR_Owner.Go(this));
		}
		else
		{
			SetFollowTarget(null);
		}
	}

	internal void AttachToShip(Ship ship, Chair chair)
	{
		if (_isOnShip)
		{
			DetachFromShip();
		}
		_attachedShip = ship;
		_attachedChair = chair;
		_isOnShip = true;
		WaterSafety.Clear(m_character);
		CapsuleCollider component = GetComponent<CapsuleCollider>();
		if (component != null)
		{
			_shipColliders = ship.GetComponentsInChildren<Collider>();
			Collider[] shipColliders = _shipColliders;
			foreach (Collider collider in shipColliders)
			{
				Physics.IgnoreCollision(component, collider, ignore: true);
			}
		}
		Vector3 position = chair.m_attachPoint.position;
		Quaternion rotation = chair.m_attachPoint.rotation;
		transform.position = position;
		transform.rotation = rotation;
		_shipLocalPos = ship.transform.InverseTransformPoint(position);
		_shipLocalRot = Quaternion.Inverse(ship.transform.rotation) * rotation;
		if (_body != null)
		{
			_body.position = position;
			_body.linearVelocity = Vector3.zero;
			ResetLiquidState();
			_body.isKinematic = false;
			_body.useGravity = false;
		}
		bool flag = _setup != null && _setup.CanWearArmor();
		if ((_zanim != null) & flag)
		{
			_zanim.SetBool("attach_chair", value: true);
		}
		Humanoid humanoid = m_character as Humanoid;
		if (humanoid != null)
		{
			humanoid.HideHandItems();
		}
		SetFollowTarget(null);
	}

	internal void DetachFromShip()
	{
		if (!_isOnShip)
		{
			return;
		}
		Ship attachedShip = _attachedShip;
		bool flag = _setup != null && _setup.CanWearArmor();
		if ((_zanim != null) & flag)
		{
			_zanim.SetBool("attach_chair", value: false);
		}
		if (_shipColliders != null)
		{
			CapsuleCollider component = GetComponent<CapsuleCollider>();
			if (component != null)
			{
				Collider[] shipColliders = _shipColliders;
				foreach (Collider collider in shipColliders)
				{
					if (collider != null)
					{
						Physics.IgnoreCollision(component, collider, ignore: false);
					}
				}
			}
			_shipColliders = null;
		}
		if (_body != null)
		{
			_body.isKinematic = false;
			_body.useGravity = true;
			_body.linearVelocity = Vector3.zero;
		}
		_isOnShip = false;
		_attachedShip = null;
		Chair attachedChair = _attachedChair;
		_attachedChair = null;
		Vector3 vector = transform.position + Vector3.up * 0.5f;
		if (attachedChair != null && attachedChair.m_attachPoint != null)
		{
			vector = attachedChair.m_attachPoint.TransformPoint(attachedChair.m_detachOffset);
			if (attachedShip != null && MHR_Deck.SpotNear(attachedShip, attachedChair.m_attachPoint.position, out var local))
			{
				Vector3 vector2 = attachedShip.transform.TransformPoint(local) + attachedShip.transform.up * 0.05f;
				if (Vector3.Distance(vector2, attachedChair.m_attachPoint.position) < 2f)
				{
					vector = vector2;
				}
			}
		}
		transform.position = vector;
		if (_body != null)
		{
			_body.position = vector;
			Rigidbody rigidbody = ((attachedShip != null) ? attachedShip.GetComponent<Rigidbody>() : null);
			_body.linearVelocity = ((rigidbody != null) ? rigidbody.GetPointVelocity(vector) : Vector3.zero);
			ResetLiquidState();
		}
		RestoreFollowOrPatrol();
	}

	private void ResetLiquidState()
	{
		((IWaterInteractable)m_character)?.SetLiquidLevel(-10000f, LiquidType.Water, (Component)null);
	}

	private void UpdateShipAttach()
	{
		if (_attachedShip == null)
		{
			DetachFromShip();
		}
		else
		{
			PinToShip();
		}
	}

	private void PinToShip()
	{
		if (_isOnShip && !(_attachedShip == null) && !(m_nview == null) && m_nview.IsOwner())
		{
			Vector3 vector = _attachedShip.transform.TransformPoint(_shipLocalPos);
			Quaternion rotation = _attachedShip.transform.rotation * _shipLocalRot;
			transform.position = vector;
			transform.rotation = rotation;
			if (_body != null && !_body.isKinematic)
			{
				_body.position = vector;
				_body.useGravity = false;
				Rigidbody component = _attachedShip.GetComponent<Rigidbody>();
				_body.linearVelocity = ((component != null) ? component.GetPointVelocity(vector) : Vector3.zero);
			}
		}
	}

	private void FixedUpdate()
	{
		PinToShip();
	}

	private void LateUpdate()
	{
		PinToShip();
	}

	internal void SetPendingShipBoard(Ship ship, Chair chair)
	{
		SetFollowTarget(null);
		MHR_Board.Start(this, ship, chair, auto: false);
	}

	internal void SetMoveTarget(Vector3 pos)
	{
		PendingMoveTarget = pos;
		_pendingMoveTimeout = 30f;
		SetFollowTarget(null);
	}

	internal void SetPendingDeposit(Container chest, Humanoid humanoid)
	{
		PendingDepositContainer = chest;
		PendingDepositHumanoid = humanoid;
		_pendingDepositTimeout = 20f;
		SetFollowTarget(null);
	}

	private bool UpdateTombstoneRecovery(float dt)
	{
		if (_pendingTombstone != null)
		{
			if (IsInCombat)
			{
				return false;
			}
			if (MHR_Recover.IsRecovering(this) && !MHR_Recover.SafeToGo(m_character, _pendingTombstone.transform.position, out var _, MHR_Owner.Go(this), committed: true) && RecoverWait(dt))
			{
				_tombstoneNavTimeout = Mathf.Max(_tombstoneNavTimeout, 10f);
				return true;
			}
			if (m_follow != null)
			{
				SetFollowTarget(null);
			}
			_tombstoneNavTimeout -= dt;
			if (_tombstoneNavTimeout <= 0f)
			{
				_pendingTombstone = null;
				ClearTombstoneId();
				RestoreFollowOrPatrol();
				_tombRecoverSnooze = 60f;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Recovery] Couldn't reach its tombstone in time; will try again in a minute");
				}
				return false;
			}
			Vector3 position = _pendingTombstone.transform.position;
			float num = Vector3.Distance(transform.position, position);
			if (num < 3f)
			{
				Container component = _pendingTombstone.GetComponent<Container>();
				if (component != null && component.IsInUse())
				{
					StopMoving();
					return true;
				}
				StopMoving();
				LookAtPoint(position);
				if (!ReviveAt(_pendingTombstone))
				{
					LootTombstone(_pendingTombstone);
					ClearTombstoneId();
				}
				_pendingTombstone = null;
				RestoreFollowOrPatrol();
				return false;
			}
			if (!FoundPath() && num < 6f)
			{
				_tombstoneNavStuckTime += dt;
				if (_tombstoneNavStuckTime > 5f)
				{
					StopMoving();
					LookAtPoint(position);
					if (!ReviveAt(_pendingTombstone))
					{
						LootTombstone(_pendingTombstone);
						ClearTombstoneId();
					}
					_pendingTombstone = null;
					RestoreFollowOrPatrol();
					_tombstoneNavStuckTime = 0f;
					return false;
				}
			}
			else
			{
				_tombstoneNavStuckTime = 0f;
			}
			_tombstoneScanTimer -= dt;
			if (_tombstoneScanTimer <= 0f)
			{
				_tombstoneScanTimer = TombstoneScanInterval;
			}
			bool run = num > 10f;
			MHR_Go(dt, position, 2.5f, run, "Tombstone");
			return true;
		}
		if ((object)_pendingTombstone != null)
		{
			_pendingTombstone = null;
			ClearTombstoneId();
			RestoreFollowOrPatrol();
			return false;
		}
		ZDO zDO = m_nview?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		if (_tombRecoverSnooze > 0f)
		{
			_tombRecoverSnooze -= dt;
			return false;
		}
		_tombstoneScanTimer -= dt;
		if (_tombstoneScanTimer > 0f)
		{
			return false;
		}
		_tombstoneScanTimer = (MHR_Recover.IsRecovering(this) ? 1f : TombstoneScanInterval);
		TombStone[] array = UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
		foreach (TombStone tombStone in array)
		{
			if (tombStone == null)
			{
				continue;
			}
			ZNetView component2 = tombStone.GetComponent<ZNetView>();
			if (component2 == null)
			{
				continue;
			}
			ZDO zDO2 = component2.GetZDO();
			if (zDO2 == null || !MHR_IsOwnGrave(zDO2, zDO, m_character))
			{
				continue;
			}
			Container component3 = tombStone.GetComponent<Container>();
			if (component3 == null)
			{
				continue;
			}
			Inventory inventory = component3.GetInventory();
			if (inventory == null || inventory.NrOfItems() <= 0)
			{
				continue;
			}
			float num2 = Vector3.Distance(transform.position, tombStone.transform.position);
			if (num2 <= ModConfig.TombstoneRecoveryRange.Value || MHR_Recover.IsRecovering(this))
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo($"[Recovery] Found its own tombstone {num2:F0}m away with {component3.GetInventory().NrOfItems()} item(s); going to collect them");
				}
				_pendingTombstone = tombStone;
				_tombstoneNavTimeout = TombstoneNavTimeoutMax;
				_tombstoneNavStuckTime = 0f;
				Vector3.Distance(transform.position, tombStone.transform.position);
				if (_talk != null)
				{
					_talk.Say(ModLocalization.Loc("hc_speech_tomb_found"));
				}
				SetFollowTarget(null);
				return true;
			}
		}
		return false;
	}

	private bool RecoverWait(float dt)
	{
		GameObject gameObject = MHR_Owner.Go(this);
		if (gameObject == null)
		{
			return false;
		}
		if (Vector3.Distance(transform.position, gameObject.transform.position) > 3f)
		{
			MHR_Go(dt, gameObject.transform.position, 2.5f, run: true, "Recover");
		}
		else
		{
			StopMoving();
		}
		return true;
	}

	private void ClearTombstoneId()
	{
		(m_nview?.GetZDO())?.Set(CompanionSetup.TombstoneIdHash, 0L);
	}

	// La tumba de otro compañero que espera revivir no se saquea: el compañero que llega lo revive, y sus cosas vuelven a
	// él como cuando lo revive el jugador.
	private bool ReviveAt(TombStone tombstone)
	{
		CompanionTombstoneResurrect companionTombstoneResurrect = ((tombstone != null) ? tombstone.GetComponent<CompanionTombstoneResurrect>() : null);
		ZDO zDO = ((tombstone != null) ? tombstone.GetComponent<ZNetView>()?.GetZDO() : null);
		if (companionTombstoneResurrect == null || zDO == null)
		{
			return false;
		}
		string text = zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash);
		if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(zDO.GetString(CompanionTombstoneResurrect.RespawnPrefabHash)))
		{
			return false;
		}
		if (text != (Player.m_localPlayer?.GetPlayerID().ToString() ?? ""))
		{
			return false;
		}
		string text2 = zDO.GetString(CompanionSetup.MHR_TombCompanionIdHash);
		if (!string.IsNullOrEmpty(text2) && _setup != null && text2 == _setup.MHR_EnsureCompanionId())
		{
			return false;
		}
		string text3 = zDO.GetString(CompanionTombstoneResurrect.RespawnNameHash);
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Resurrect] " + (m_character?.m_name ?? "?") + " revives \"" + text3 + "\" at its tombstone");
		}
		companionTombstoneResurrect.ReviveByCompanion();
		return true;
	}

	private void LootTombstone(TombStone tombstone)
	{
		Container component = tombstone.GetComponent<Container>();
		if (component == null)
		{
			return;
		}
		Inventory inventory = component.GetInventory();
		Humanoid humanoid = m_character as Humanoid;
		if (inventory == null || humanoid == null)
		{
			return;
		}
		Inventory inventory2 = humanoid.GetInventory();
		if (inventory2 == null)
		{
			return;
		}
		inventory.NrOfItems();
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory.GetAllItems());
		int num = 0;
		int num2 = 0;
		foreach (ItemDrop.ItemData item in list)
		{
			if (inventory2.AddItem(item))
			{
				inventory.RemoveItem(item);
				num++;
			}
			else
			{
				num2++;
			}
		}
		if (num > 0)
		{
			int width = inventory2.GetWidth();
			int num3 = 0;
			foreach (ItemDrop.ItemData allItem in inventory2.GetAllItems())
			{
				allItem.m_gridPos = new Vector2i(num3 % width, num3 / width);
				num3++;
			}
			inventory2.m_onChanged?.Invoke();
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Recovery] Collected {num} item(s) from its tombstone, {inventory.NrOfItems()} left");
		}
		if (inventory.NrOfItems() == 0)
		{
			CompanionTombstoneResurrect.MHR_RemoveGravePinNear(tombstone.transform.position);
		}
		if (_talk != null)
		{
			_talk.Say((num > 0) ? ModLocalization.Loc("hc_speech_tomb_recovered") : ModLocalization.Loc("hc_speech_tomb_empty"));
		}
	}

	internal static bool MHR_IsOwnGrave(ZDO tomb, ZDO me, Character ch)
	{
		if (tomb != null && me != null && tomb.GetLong(CompanionSetup.TombstoneIdHash, 0L) != 0L)
		{
			string text = tomb.GetString(CompanionSetup.MHR_TombCompanionIdHash);
			if (!string.IsNullOrEmpty(text))
			{
				return text == me.GetString(CompanionSetup.MHR_CompanionIdHash);
			}
			string text2 = me.GetString(CompanionSetup.NameHash);
			if (string.IsNullOrEmpty(text2))
			{
				if (ch == null)
				{
					goto IL_00c1;
				}
				text2 = ch.m_name;
			}
			if (tomb.GetString(ZDOVars.s_ownerName) == text2)
			{
				text = tomb.GetString(CompanionSetup.MHR_TombOwnerHash);
				if (!string.IsNullOrEmpty(text))
				{
					return text == me.GetString(CompanionSetup.OwnerHash);
				}
				return true;
			}
		}
		goto IL_00c1;
		IL_00c1:
		return false;
	}

	internal string MHR_DiagFollowName()
	{
		GameObject follow = m_follow;
		if (follow == null)
		{
			return "none";
		}
		return follow.name;
	}

	internal bool MHR_DiagFoundPath()
	{
		return FoundPath();
	}

	public void DirectEngage(Character target)
	{
		SetAlerted(alert: true);
		CompanionCombatAI combatAI = _combatAI;
		if ((object)combatAI == null)
		{
			CompanionsPlugin.Log.LogWarning("[Direct] DirectEngage -> no CompanionCombatAI component; direct-order attack cannot use normal weapon/shield/flee logic");
			return;
		}
		if (MHR_Threat.IsPassive(target))
		{
			combatAI.EngagePrey(target);
			return;
		}
		combatAI.Order(target);
		combatAI.Engage(target);
	}

	internal void SetDirectedTombstoneRecovery(TombStone tombstone)
	{
		_pendingTombstone = tombstone;
		_tombstoneNavTimeout = TombstoneNavTimeoutMax;
		_tombstoneScanTimer = TombstoneScanInterval;
		_tombstoneNavStuckTime = 0f;
		SetFollowTarget(null);
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_tomb_found"));
		}
	}

	public override bool UpdateAI(float dt)
	{
		if (!base.UpdateAI(dt))
		{
			AIGateDiag.Report(_setup, "vanilla BaseAI.UpdateAI() false");
			return false;
		}
		if (WaterSafety.Tick(this, dt))
		{
			AIGateDiag.Report(_setup, "WaterSafety");
			return true;
		}
		if (MHR_Mover.EscapeLava(this))
		{
			AIGateDiag.Report(_setup, "Lava");
			return true;
		}
		UpdateSleep(dt);
		if (IsSleeping())
		{
			AIGateDiag.Report(_setup, "Sleeping");
			return true;
		}
		if (FreezeTimer > 0f)
		{
			FreezeTimer -= dt;
			AIGateDiag.Report(_setup, "FreezeTimer");
			return true;
		}
		if (_rest != null && _rest.IsResting)
		{
			AIGateDiag.Report(_setup, "Resting");
			return true;
		}
		if (_isOnShip)
		{
			if (_attachedShip != null && MHR_Board.ShouldLeaveSeat(this, _attachedShip))
			{
				DetachFromShip();
				return true;
			}
			UpdateShipAttach();
			AIGateDiag.Report(_setup, "OnShip");
			return true;
		}
		if (_setup != null && (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup)))
		{
			StopMoving();
			AIGateDiag.Report(_setup, "UI open (panel/radial)");
			return true;
		}
		UpdateDrowning(dt);
		if (UpdateHazardRecovery(dt))
		{
			AIGateDiag.Report(_setup, "HazardRecovery");
			return true;
		}
		ReturnLogic.Tick(_setup);
		AIGateDiag.Report(_setup, null);
		bool lowStamina = _companionStamina != null && _companionStamina.MaxStamina > 0f && _companionStamina.Stamina / _companionStamina.MaxStamina < 0.5f;
		MHR_Water.ApplyAgent(this, lowStamina);
		if (m_character != null && m_character.IsSwimming())
		{
			_continuousSwimTimer += dt;
		}
		else
		{
			_continuousSwimTimer = 0f;
		}
		CleanupBlacklist(dt);
		AutoPickup(dt);
		if (!MHR_Mover.OwnsMovement(this))
		{
			UpdateFurnitureUnstuck(dt);
		}
		if (m_follow != null && _setup != null && _setup.GetFollow())
		{
			Vector3 position = m_follow.transform.position;
			float num = Vector3.Distance(transform.position, position);
			if (num > FollowTeleportDist && (num > FollowTeleportDist * 1.5f || (!MHR_Mover.CanSee(transform.position, position) && !MHR_Mover.Progressing(this))))
			{
				TeleportToFollowTarget();
			}
		}
		if (_rest != null && _rest.IsNavigating)
		{
			Vector3 navTarget = _rest.NavTarget;
			if (Vector3.Distance(transform.position, navTarget) < 2f)
			{
				_rest.ArriveAtNavTarget();
			}
			else
			{
				MHR_Mover.Go(this, dt, navTarget, 1.2f, allowRun: true, "Bed");
			}
			return true;
		}
		if (MHR_Hive.Active(this) && MHR_Hive.Update(this, dt))
		{
			return true;
		}
		if (MHR_Board.Active(this) && MHR_Board.Update(this, dt))
		{
			return true;
		}
		if (PendingCartAttach != null)
		{
			_pendingCartTimeout -= dt;
			if (_pendingCartTimeout <= 0f || PendingCartAttach == null)
			{
				CancelPendingCart();
			}
			else
			{
				Vector3 vector = PendingCartAttach.m_attachPoint.position - PendingCartAttach.m_attachOffset;
				vector.y = transform.position.y;
				if (Vector3.Distance(transform.position, vector) < 2f)
				{
					transform.position = vector;
					if (_body != null)
					{
						_body.position = vector;
						_body.linearVelocity = Vector3.zero;
					}
					Vector3 vector2 = PendingCartAttach.transform.position - transform.position;
					vector2.y = 0f;
					if (vector2.sqrMagnitude > 0.01f)
					{
						transform.rotation = Quaternion.LookRotation(vector2.normalized);
					}
					FreezeTimer = 1f;
					SetFollowTarget(PendingCartAttach.gameObject);
					PendingCartAttach.Interact(PendingCartHumanoid, hold: false, alt: false);
					PendingCartAttach = null;
					PendingCartHumanoid = null;
				}
				else
				{
					MoveTo(dt, vector, 1f, run: true);
				}
			}
			return true;
		}
		if (PendingMoveTarget.HasValue)
		{
			_pendingMoveTimeout -= dt;
			if (_pendingMoveTimeout <= 0f)
			{
				CancelMoveTarget();
			}
			else
			{
				float num2 = Vector3.Distance(transform.position, PendingMoveTarget.Value);
				if (num2 < 2f)
				{
					CancelMoveTarget();
				}
				else
				{
					bool run = num2 > 10f;
					MHR_Go(dt, PendingMoveTarget.Value, 1f, run, "Command");
					if (MHR_Mover.GaveUp(this))
					{
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo($"[Direct] Can't find a way to {PendingMoveTarget.Value:F1} - stopping here");
						}
						CancelMoveTarget();
					}
				}
			}
			return true;
		}
		if (PendingDepositContainer != null)
		{
			_pendingDepositTimeout -= dt;
			if (_pendingDepositTimeout <= 0f || PendingDepositContainer == null)
			{
				CancelPendingDeposit();
			}
			else if (_depositChestOpen)
			{
				StopMoving();
				LookAtPoint(PendingDepositContainer.transform.position);
				_depositWorkTimer -= dt;
				if (_depositWorkTimer <= 0f && _depositQueue.Count > 0)
				{
					_depositWorkTimer = 0.6f;
					ItemDrop.ItemData item = _depositQueue[0];
					_depositQueue.RemoveAt(0);
					Inventory inventory = PendingDepositContainer.GetInventory();
					Inventory inventory2 = PendingDepositHumanoid?.GetInventory();
					if (inventory != null && inventory2 != null && inventory2.ContainsItem(item) && inventory.AddItem(item))
					{
						inventory2.RemoveItem(item);
						_depositCount++;
					}
				}
				if (_depositQueue.Count == 0)
				{
					PendingDepositContainer.SetInUse(inUse: false);
					_depositChestOpen = false;
					if (_talk != null)
					{
						_talk.Say((_depositCount > 0) ? ModLocalization.Loc("hc_speech_deposit_done") : ModLocalization.Loc("hc_speech_deposit_empty"));
					}
					CancelPendingDeposit();
				}
			}
			else
			{
				Vector3 position2 = PendingDepositContainer.transform.position;
				float num3 = Vector3.Distance(transform.position, position2);
				if (num3 < 2f && !_depositChestOpen && MHR_Chests.CompanionHeld(PendingDepositContainer))
				{
					StopMoving();
					LookAtPoint(position2);
				}
				else if (num3 < 2f && !_depositChestOpen && !MHR_Chests.Open(PendingDepositContainer))
				{
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Deposit] Someone has the chest open - trying again later");
					}
					CancelPendingDeposit();
				}
				else if (num3 < 2f)
				{
					StopMoving();
					LookAtPoint(position2);
					_depositChestOpen = true;
					_depositCount = 0;
					_depositWorkTimer = 0.6f;
					_depositQueue.Clear();
					Humanoid pendingDepositHumanoid = PendingDepositHumanoid;
					if (pendingDepositHumanoid != null)
					{
						Inventory inventory3 = pendingDepositHumanoid.GetInventory();
						if (inventory3 != null)
						{
							foreach (ItemDrop.ItemData allItem in inventory3.GetAllItems())
							{
								if (!CompanionInteract.ShouldKeep(allItem, pendingDepositHumanoid))
								{
									_depositQueue.Add(allItem);
								}
							}
						}
					}
					if (_depositQueue.Count == 0)
					{
						PendingDepositContainer.SetInUse(inUse: false);
						_depositChestOpen = false;
						if (_talk != null)
						{
							_talk.Say(ModLocalization.Loc("hc_speech_deposit_empty"));
						}
						CancelPendingDeposit();
					}
				}
				else
				{
					bool run2 = num3 > 10f;
					MHR_Go(dt, position2, 1.5f, run2, "Deposit");
				}
			}
			return true;
		}
		if (UpdateTombstoneRecovery(dt))
		{
			return true;
		}
		Humanoid humanoid = m_character as Humanoid;
		if (_isDvergerSupport && humanoid != null)
		{
			_healScanTimer -= dt;
			Character healTarget = _healTarget;
			if (_healScanTimer <= 0f)
			{
				_healScanTimer = 1f;
				_healTarget = FindHurtAlly();
			}
			if (_healTarget != null)
			{
				float maxHealth = _healTarget.GetMaxHealth();
				if (_healTarget.GetHealth() <= 0f || maxHealth <= 0f || _healTarget.GetHealth() / maxHealth >= 0.7f)
				{
					_healTarget = null;
					ResetHealReachabilityTracking();
				}
			}
			if (_healTarget != healTarget)
			{
				ResetHealReachabilityTracking();
			}
			if (_healTarget != null)
			{
				_healEquipTimer -= dt;
				if (_healEquipTimer <= 0f && !m_character.InAttack())
				{
					_healEquipTimer = 1f;
					humanoid.EquipBestWeapon(null, null, _healTarget, null);
				}
				UpdateHealBehavior(humanoid, dt);
				return true;
			}
			if ((_setup?.GetCombatStance() ?? 0) != 2)
			{
				_healEquipTimer -= dt;
				if (_healEquipTimer <= 0f && !m_character.InAttack())
				{
					_healEquipTimer = 1f;
					humanoid.EquipBestWeapon(m_targetCreature, m_targetStatic, null, null);
				}
			}
		}
		UpdateDebugLog(dt);
		if (m_follow != null && _setup != null && _setup.GetFollow() && m_character != null)
		{
			Character component = m_follow.GetComponent<Character>();
			if (component != null && (!(component is Player player) || !player.IsTeleporting()) && component.InInterior() != m_character.InInterior() && Vector3.Distance(transform.position, m_follow.transform.position) > 30f)
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Follow] You went " + (component.InInterior() ? "into" : "out of") + " the dungeon - following you through");
				}
				_combatAI?.Disengage();
				TeleportToFollowTarget();
				return true;
			}
		}
		if (_combatAI != null)
		{
			if (_combatAI.IsEngaged)
			{
				if (_combatAI.UpdateCombat(dt))
				{
					return true;
				}
			}
			else
			{
				int num4 = _setup?.GetCombatStance() ?? 0;
				bool flag = _combatAI != null && false;
				if (num4 != 1 && !flag)
				{
					Character character = _combatAI.PickTarget();
					if (character != null && IsPassiveAnimal(character))
					{
						character = null;
					}
					if (character != null && (!MHR_Threat.IsAfterUs(character) || MHR_Threat.IgnoredInWater(character)))
					{
						character = null;
					}
					if (character != null)
					{
						SetAlerted(alert: true);
						_combatAI.Engage(character);
						if (_combatAI.UpdateCombat(dt))
						{
							return true;
						}
					}
					else if (m_targetCreature != null && !_combatAI.Holding && !MHR_Threat.IsPassive(m_targetCreature) && !MHR_Threat.IgnoredInWater(m_targetCreature) && _combatAI.CanGetAt(m_targetCreature))
					{
						SetAlerted(alert: true);
						_combatAI.Engage(m_targetCreature);
						m_targetCreature = null;
						m_targetStatic = null;
						if (_combatAI.UpdateCombat(dt))
						{
							return true;
						}
					}
				}
				else if (flag)
				{
					m_targetCreature = null;
					m_targetStatic = null;
				}
			}
		}
		if (_companionStamina != null && _companionStamina.IsCrouching && m_character.IsSwimming())
		{
			MHR_SetCrouch(sneak: false);
		}
		UpdateTorch();
		if (MHR_SpearThrow.Drive(this, dt, _combatAI != null && _combatAI.IsEngaged))
		{
			ClearFollowMovementOverrides();
			return true;
		}
		if (MHR_ToolKeeper.Drive(this, dt))
		{
			return true;
		}
		if (MHR_Loot.Drive(this, dt))
		{
			ClearFollowMovementOverrides();
			return true;
		}
		ZDO zDO = m_nview?.GetZDO();
		if ((_harvest != null && _harvest.IsActive) || (_repair != null && _repair.IsActive) || (_smelt != null && _smelt.IsActive) || (_farm != null && _farm.IsActive) || (_upkeep != null && _upkeep.IsActive) || (_cook != null && _cook.IsActive) || (_doorHandler != null && _doorHandler.IsActive) || (_hunt != null && _hunt.IsActive) || (_fish != null && _fish.IsActive) || (_idle != null && _idle.IsActive))
		{
			ClearFollowMovementOverrides();
			return true;
		}
		if (m_follow != null)
		{
			MHR_Mover.FollowWatch(this, m_follow);
			if (MHR_Board.FollowHook(this, m_follow, dt))
			{
				return true;
			}
			if (!MHR_Follow.Update(this, m_follow, dt))
			{
				FollowWithFormation(m_follow, dt);
			}
		}
		else
		{
			ClearFollowMovementOverrides();
			if (zDO != null && MHR_JobRules.IsJobMode(zDO.GetInt(CompanionSetup.ActionModeHash)))
			{
				StopMoving();
				return true;
			}
			StopMoving();
		}
		return true;
	}

	private void UpdateSleep(float dt)
	{
		Player player = null;
		if (m_wakeupRange > 0f || m_fallAsleepDistance > 0f)
		{
			player = Player.GetClosestPlayer(transform.position, m_wakeupRange);
			if (player != null && (player.InGhostMode() || player.IsDebugFlying()))
			{
				player = null;
			}
		}
		if (!IsSleeping())
		{
			if (m_fallAsleepDistance > 0f && (player == null || Vector3.Distance(player.transform.position, transform.position) > m_fallAsleepDistance))
			{
				Sleep();
			}
			return;
		}
		m_sleepTimer += dt;
		if (m_sleepTimer < m_sleepDelay)
		{
			return;
		}
		if (m_wakeupRange > 0f && player != null)
		{
			Wakeup();
		}
		else if (m_noiseWakeup)
		{
			Player playerNoiseRange = Player.GetPlayerNoiseRange(transform.position, m_maxNoiseWakeupRange);
			if (playerNoiseRange != null && !playerNoiseRange.InGhostMode() && !playerNoiseRange.IsDebugFlying())
			{
				Wakeup();
			}
		}
	}

	private void UpdateDrowning(float dt)
	{
		if (!m_character.IsSwimming())
		{
			_drownDamageTimer = 0f;
			return;
		}
		if (_companionStamina == null || _companionStamina.Stamina > 0f)
		{
			_drownDamageTimer = 0f;
			return;
		}
		_drownDamageTimer += dt;
		if (_drownDamageTimer > 1f)
		{
			_drownDamageTimer = 0f;
			float damage = Mathf.Ceil(m_character.GetMaxHealth() / 20f);
			HitData hitData = new HitData();
			hitData.m_damage.m_damage = damage;
			hitData.m_point = m_character.GetCenterPoint();
			hitData.m_dir = Vector3.down;
			hitData.m_pushForce = 10f;
			hitData.m_hitType = HitData.HitType.Drowning;
			m_character.Damage(hitData);
			if (DrownEffects != null)
			{
				Vector3 position = transform.position;
				position.y = m_character.GetLiquidLevel();
				DrownEffects.Create(position, transform.rotation, null, 1f, -1, ZDOID.None);
			}
		}
	}

	private bool UpdateHazardRecovery(float dt)
	{
		bool flag = m_character.GetSEMan().HaveStatusEffect(s_taredHash);
		bool flag2 = m_character.InLava();
		bool flag3 = m_character.IsSwimming();
		bool flag4 = false;
		string text = null;
		if (flag3 && _companionStamina != null && _companionStamina.Stamina <= 0f)
		{
			flag4 = true;
			text = "DROWNING(no stamina)";
		}
		if (!flag && !flag4 && !flag2)
		{
			_hazardRecoveryTimer = 0f;
			return false;
		}
		_hazardRecoveryTimer += dt;
		if ((!(_hazardRecoveryTimer < 0.1f) && (int)(_hazardRecoveryTimer * 10f) % 20 != 0) || !flag)
		{
		}
		if (m_targetCreature != null)
		{
			ClearTargets();
		}
		Vector3 vector = Vector3.zero;
		float num = float.MaxValue;
		Vector3 position = transform.position;
		float liquidLevel = m_character.GetLiquidLevel();
		float[] array = new float[3] { 5f, 10f, 20f };
		foreach (float num2 in array)
		{
			for (int j = 0; j < 12; j++)
			{
				float y = (float)j * 30f;
				Vector3 vector2 = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
				Vector3 vector3 = position + vector2 * num2;
				if (!(ZoneSystem.instance != null))
				{
					continue;
				}
				float solidHeight = ZoneSystem.instance.GetSolidHeight(vector3);
				float num3 = Mathf.Max(liquidLevel, 30f);
				bool flag5 = ZoneSystem.instance.GetSolidHeight(vector3 + vector2 * 1.5f) < solidHeight - 3f || MHR_Mover.FireArea(new Vector3(vector3.x, solidHeight, vector3.z), 0.5f) != null || MHR_Mover.Lava(new Vector3(vector3.x, solidHeight, vector3.z)) || MHR_Mover.SpikeNear(new Vector3(vector3.x, solidHeight, vector3.z), 0.7f) != null;
				if (solidHeight > num3 && !flag5)
				{
					float num4 = Vector3.Distance(position, vector3);
					if (num4 < num)
					{
						num = num4;
						vector = vector2;
					}
				}
			}
			if (vector != Vector3.zero)
			{
				break;
			}
		}
		if (vector != Vector3.zero)
		{
			MoveTowards(vector, run: true);
		}
		else if (m_follow != null)
		{
			Vector3 normalized = (m_follow.transform.position - position).normalized;
			MoveTowards(normalized, run: true);
		}
		float num5 = ((flag3 && _companionStamina != null && _companionStamina.Stamina <= 0f) ? 6f : 10f);
		if (_hazardRecoveryTimer > num5 && m_follow != null && _setup != null && _setup.GetFollow())
		{
			TeleportToFollowTarget();
			_hazardRecoveryTimer = 0f;
			_continuousSwimTimer = 0f;
		}
		return true;
	}

	private void CleanupBlacklist(float dt)
	{
		_blacklistCleanupTimer += dt;
		if (_blacklistCleanupTimer < 30f)
		{
			return;
		}
		_blacklistCleanupTimer = 0f;
		if (_targetBlacklist.Count == 0)
		{
			return;
		}
		float time = Time.time;
		List<Vector3Int> list = null;
		foreach (KeyValuePair<Vector3Int, float> item in _targetBlacklist)
		{
			if (time - item.Value >= 30f)
			{
				if (list == null)
				{
					list = new List<Vector3Int>();
				}
				list.Add(item.Key);
			}
		}
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				_targetBlacklist.Remove(list[i]);
			}
		}
	}

	private void AutoPickup(float dt)
	{
		Humanoid humanoid = m_character as Humanoid;
		if (humanoid == null)
		{
			return;
		}
		Inventory inventory = humanoid.GetInventory();
		if (inventory == null)
		{
			return;
		}
		Vector3 vector = transform.position + Vector3.up;
		int num = Physics.OverlapSphereNonAlloc(vector, AutoPickupRange, _autoPickupBuffer, _autoPickupMask);
		for (int i = 0; i < num; i++)
		{
			Collider collider = _autoPickupBuffer[i];
			if (collider == null || collider.attachedRigidbody == null)
			{
				continue;
			}
			ItemDrop component = collider.attachedRigidbody.GetComponent<ItemDrop>();
			if (component == null || !component.m_autoPickup)
			{
				continue;
			}
			ZNetView component2 = component.GetComponent<ZNetView>();
			if (component2 == null || !component2.IsValid())
			{
				continue;
			}
			if (!component.CanPickup())
			{
				component.RequestOwn();
			}
			else
			{
				if (component.InTar())
				{
					continue;
				}
				component.Load();
				_ = component.m_itemData?.m_shared?.m_name;
				if (inventory.CanAddItem(component.m_itemData) && !(inventory.GetTotalWeight() + component.m_itemData.GetWeight() > CompanionTierData.CarryLimit(this)))
				{
					if (Vector3.Distance(component.transform.position, vector) < 0.3f)
					{
						humanoid.Pickup(component.gameObject, autoequip: false, autoPickupDelay: false);
						continue;
					}
					Vector3 normalized = (vector - component.transform.position).normalized;
					component.transform.position += normalized * 15f * dt;
				}
			}
		}
	}

	private void UpdateFurnitureUnstuck(float dt)
	{
		if (m_character == null)
		{
			return;
		}
		float magnitude = m_character.GetVelocity().magnitude;
		bool flag = m_follow != null || PendingMoveTarget.HasValue || PendingCartAttach != null || MHR_Board.Active(this) || MHR_Hive.Active(this) || (_rest != null && _rest.IsNavigating);
		if ((magnitude < 0.1f) & flag)
		{
			_furnitureStuckTimer += dt;
		}
		else
		{
			_furnitureStuckTimer = 0f;
		}
		if (_furnitureStuckTimer < 3f)
		{
			return;
		}
		Vector3 origin = transform.position + Vector3.up * 0.5f;
		int mask = LayerMask.GetMask("piece", "piece_nonsolid");
		if (!Physics.Raycast(origin, Vector3.down, out var hitInfo, 1.5f, mask))
		{
			return;
		}
		Bed componentInParent = hitInfo.collider.GetComponentInParent<Bed>();
		if (!(componentInParent == null))
		{
			_furnitureStuckTimer = 0f;
			Vector3 vector = transform.position - componentInParent.transform.position;
			vector.y = 0f;
			if (vector.sqrMagnitude < 0.01f)
			{
				vector = transform.forward;
			}
			vector = vector.normalized;
			Vector3 vector2 = componentInParent.transform.position + vector * 2.5f;
			int mask2 = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
			if (Physics.Raycast(vector2 + Vector3.up * 5f, Vector3.down, out var hitInfo2, 10f, mask2))
			{
				vector2.y = hitInfo2.point.y;
			}
			else
			{
				vector2.y = transform.position.y - 0.5f;
			}
			transform.position = vector2;
			if (_body != null)
			{
				_body.position = vector2;
				_body.linearVelocity = Vector3.zero;
				ResetLiquidState();
			}
		}
	}

	internal void TeleportToFollowTarget()
	{
		if (!(m_follow == null))
		{
			Vector3 position = m_follow.transform.position;
			Vector3 preferDir = -m_follow.transform.forward;
			preferDir.y = 0f;
			if (preferDir.sqrMagnitude < 0.01f)
			{
				preferDir = Vector3.back;
			}
			else
			{
				preferDir.Normalize();
			}
			Vector3 position2 = MHR_SafeSpotNear(position, preferDir);
			transform.position = position2;
			Rigidbody body = _body;
			if (body != null)
			{
				body.position = position2;
				body.linearVelocity = Vector3.zero;
			}
			if (m_nview?.GetZDO() != null)
			{
				m_nview.GetZDO().SetPosition(position2);
			}
		}
	}

	internal static Vector3 MHR_SafeSpotNear(Vector3 at, Vector3 preferDir)
	{
		Vector3 start = at + Vector3.up * 1.2f;
		int mask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece", "vehicle");
		float num = ((preferDir.sqrMagnitude > 0.01f) ? (Mathf.Atan2(preferDir.z, preferDir.x) * 57.29578f) : 0f);
		for (int i = 0; i < 2; i++)
		{
			float num2 = ((i == 0) ? 2f : 1.2f);
			for (int j = 0; j < 8; j++)
			{
				float f = (num + (float)(((j % 2 == 0) ? 1 : (-1)) * ((j + 1) / 2)) * 45f) * (Mathf.PI / 180f);
				Vector3 vector = at + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * num2 + Vector3.up * 1.2f;
				if (!Physics.Linecast(start, vector, mask, QueryTriggerInteraction.Ignore) && Physics.Raycast(vector, Vector3.down, out var hitInfo, 3f, mask, QueryTriggerInteraction.Ignore) && !(Mathf.Abs(hitInfo.point.y - at.y) > 1.2f) && !(MHR_Water.DepthAt(hitInfo.point) > 0.4f) && !(MHR_Mover.FireArea(hitInfo.point, 0.5f) != null) && !(MHR_Mover.SpikeNear(hitInfo.point, 0.7f) != null) && !MHR_Mover.Lava(hitInfo.point))
				{
					return hitInfo.point;
				}
			}
		}
		return at;
	}

	internal void MHR_PlaceAt(Vector3 position)
	{
		transform.position = position;
		if (_body != null)
		{
			_body.position = position;
			_body.linearVelocity = Vector3.zero;
		}
		StopMoving();
		if (m_nview?.GetZDO() != null)
		{
			m_nview.GetZDO().SetPosition(position);
		}
	}

	private Character FindHurtAlly()
	{
		Character result = null;
		float num = 0.7f;
		string text = GetCompanionOwnerId();
		if (string.IsNullOrEmpty(text) && Player.m_localPlayer != null)
		{
			text = Player.m_localPlayer.GetPlayerID().ToString();
		}
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == m_character || allCharacter.GetHealth() <= 0f)
			{
				continue;
			}
			float maxHealth = allCharacter.GetMaxHealth();
			if (!(maxHealth <= 0f))
			{
				float num2 = allCharacter.GetHealth() / maxHealth;
				if (!(num2 >= 0.7f) && !(Vector3.Distance(transform.position, allCharacter.transform.position) > 50f) && IsHealCandidateOwnedByCompanionOwner(allCharacter, text) && num2 < num)
				{
					num = num2;
					result = allCharacter;
				}
			}
		}
		return result;
	}

	private void ResetHealReachabilityTracking()
	{
		_healUnreachableTimer = 0f;
		_healLastDistance = -1f;
	}

	private void UpdateHealBehavior(Humanoid humanoid, float dt)
	{
		if (_healTarget == null || _healTarget.GetHealth() <= 0f)
		{
			ResetHealReachabilityTracking();
			return;
		}
		ItemDrop.ItemData currentWeapon = humanoid.GetCurrentWeapon();
		if (currentWeapon == null)
		{
			ResetHealReachabilityTracking();
			return;
		}
		float num = Vector3.Distance(transform.position, _healTarget.transform.position) - _healTarget.GetRadius();
		float aiAttackRange = currentWeapon.m_shared.m_aiAttackRange;
		_healLogTimer -= dt;
		if (_healLogTimer <= 0f)
		{
			_healLogTimer = 3f;
		}
		if (num < aiAttackRange)
		{
			ResetHealReachabilityTracking();
			StopMoving();
			LookAt(_healTarget.GetTopPoint());
			if (CanAttackNow(currentWeapon) && IsLookingAt(_healTarget.transform.position, currentWeapon.m_shared.m_aiAttackMaxAngle, currentWeapon.m_shared.m_aiInvertAngleCheck))
			{
				m_character.StartAttack(_healTarget, charge: false);
			}
			return;
		}
		MHR_Go(dt, _healTarget.transform.position, 1f, num > 10f, "Heal");
		bool flag = FoundPath();
		bool flag2 = _healLastDistance < 0f || num < _healLastDistance - 0.2f;
		float magnitude = m_character.GetVelocity().magnitude;
		_healLastDistance = num;
		if (!flag || (magnitude < 0.3f && !flag2))
		{
			_healUnreachableTimer += dt;
		}
		else
		{
			_healUnreachableTimer = 0f;
		}
		if (_healUnreachableTimer >= 6f)
		{
			_healTarget = null;
			ResetHealReachabilityTracking();
		}
	}

	private void UpdateDebugLog(float dt)
	{
	}

	private void ClearFollowMovementOverrides()
	{
		m_character.SetWalk(walk: false);
		if (_zanim != null)
		{
			_zanim.SetBool(s_crouching, value: false);
		}
		if (_companionStamina != null)
		{
			_companionStamina.IsCrouching = false;
		}
	}

	private void FollowWithFormation(GameObject target, float dt)
	{
		if (target == null)
		{
			StopMoving();
			return;
		}
		bool flag = !m_character.IsSwimming() && _companionStamina != null && _companionStamina.MaxStamina > 0f && _companionStamina.Stamina / _companionStamina.MaxStamina < 0.5f;
		if ((m_character.IsSwimming() || m_character.InWater()) && !flag)
		{
			if (Vector3.Distance(transform.position, target.transform.position) > 3f)
			{
				Vector3 dir = target.transform.position - transform.position;
				dir.y = 0f;
				dir.Normalize();
				MoveTowards(dir, run: false);
			}
			else
			{
				StopMoving();
			}
			ClearFollowMovementOverrides();
			return;
		}
		if (_formationSlot < 0 && _setup != null)
		{
			_formationSlot = _setup.AssignFormationSlot();
		}
		float num = Vector3.Distance(transform.position, target.transform.position);
		Player component = target.GetComponent<Player>();
		bool flag2 = component != null && component.IsCrouching();
		bool flag3 = component != null && component.GetWalk() && !component.IsRunning();
		bool flag4 = component != null && component.IsRunning();
		float num2 = (_wasInFormation ? FormationCatchupDist : (FormationCatchupDist - 2f));
		if (num > num2 || _formationSlot < 0)
		{
			if (_lastFollowMode != 0)
			{
				_lastFollowMode = 0;
			}
			_wasInFormation = false;
			Follow(target, dt);
			if (!FoundPath() && num > 5f)
			{
				Vector3 moveDir = target.transform.position - transform.position;
				moveDir.y = 0f;
				if (moveDir.sqrMagnitude > 0.01f)
				{
					moveDir.Normalize();
					m_character.SetMoveDir(moveDir);
					m_character.SetRun(run: true);
				}
			}
			ClearFollowMovementOverrides();
			return;
		}
		_wasInFormation = true;
		if (num < 5f)
		{
			if (_lastFollowMode != 1)
			{
				_lastFollowMode = 1;
			}
			Follow(target, dt);
			ApplyPlayerMovementMatch(flag2, flag3, flag4);
			return;
		}
		Vector3 forward = target.transform.forward;
		Vector3 right = target.transform.right;
		float num3 = 1f;
		Vector3 vector = GetFormationOffset(_formationSlot, forward, right) * num3;
		Vector3 vector2 = target.transform.position + vector;
		if (Vector3.Distance(transform.position, vector2) < 2f)
		{
			if (_lastFollowMode != 2)
			{
				_lastFollowMode = 2;
			}
			Follow(target, dt);
			ApplyPlayerMovementMatch(flag2, flag3, flag4);
		}
		else
		{
			bool run = !(flag2 | flag3) && (flag4 || num > 10f);
			MHR_Go(dt, vector2, 1f, run, "Formation");
			ApplyPlayerMovementMatch(flag2, flag3, flag4);
		}
	}

	private bool CanAttackNow(ItemDrop.ItemData weapon)
	{
		if (weapon == null)
		{
			return false;
		}
		if (Time.time - weapon.m_lastAttackTime > weapon.m_shared.m_aiAttackInterval && CanUseAttack(weapon))
		{
			return !IsTakingOff();
		}
		return false;
	}

	internal void MHR_SetCrouch(bool sneak)
	{
		if (sneak && ((_companionStamina != null && _companionStamina.Stamina <= 0f) || m_character.IsSwimming()))
		{
			sneak = false;
		}
		if (_companionStamina != null)
		{
			_companionStamina.IsCrouching = sneak;
		}
		if (_zanim != null)
		{
			_zanim.SetBool(s_crouching, sneak);
		}
	}

	internal void MHR_FollowFallback(GameObject target, float dt)
	{
		Follow(target, dt);
	}

	private void ApplyPlayerMovementMatch(bool sneak, bool walk, bool run)
	{
		if (sneak && _companionStamina != null && _companionStamina.Stamina <= 0f)
		{
			sneak = false;
		}
		if (_companionStamina != null)
		{
			_companionStamina.IsCrouching = sneak;
		}
		if (sneak)
		{
			m_character.SetRun(run: false);
			m_character.SetWalk(walk: true);
			if (_zanim != null)
			{
				_zanim.SetBool(s_crouching, value: true);
			}
		}
		else if (walk)
		{
			m_character.SetRun(run: false);
			m_character.SetWalk(walk: true);
			if (_zanim != null)
			{
				_zanim.SetBool(s_crouching, value: false);
			}
		}
		else
		{
			m_character.SetWalk(walk: false);
			if (_zanim != null)
			{
				_zanim.SetBool(s_crouching, value: false);
			}
		}
	}

	private static Vector3 GetFormationOffset(int slot, Vector3 fwd, Vector3 right)
	{
		switch (slot)
		{
		case 0:
			return right * FormationOffset - fwd * 1f;
		case 1:
			return -right * FormationOffset - fwd * 1f;
		case 2:
			return right * (FormationOffset * 0.67f) - fwd * 3f;
		case 3:
			return -right * (FormationOffset * 0.67f) - fwd * 3f;
		default:
		{
			float f = ((float)(slot - 4) * 45f + 135f) * (Mathf.PI / 180f);
			return (right * Mathf.Cos(f) + fwd * Mathf.Sin(f)) * FormationOffset;
		}
		}
	}

	private string GetCompanionOwnerId()
	{
		ZDO zDO = m_nview?.GetZDO();
		if (zDO == null)
		{
			return string.Empty;
		}
		return zDO.GetString(CompanionSetup.OwnerHash, string.Empty);
	}

	private bool IsHealCandidateOwnedByCompanionOwner(Character candidate, string ownerId)
	{
		if (candidate == null || string.IsNullOrEmpty(ownerId))
		{
			return false;
		}
		if (candidate.IsPlayer())
		{
			Player player = candidate as Player;
			if (player == null)
			{
				return false;
			}
			return player.GetPlayerID().ToString() == ownerId;
		}
		CompanionSetup component = candidate.GetComponent<CompanionSetup>();
		if (component == null)
		{
			return false;
		}
		ZDO zDO = component.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		return zDO.GetString(CompanionSetup.OwnerHash, string.Empty) == ownerId;
	}

	protected override void OnDamaged(float damage, Character attacker)
	{
		base.OnDamaged(damage, attacker);
		Wakeup();
		_combatAI?.NoteIncoming(attacker);
		if ((_setup?.GetCombatStance() ?? 0) == 1 && !MHR_Recover.MayDefendHit(this, attacker))
		{
			return;
		}
		SetAlerted(alert: true);
		if (!(attacker != null) || (attacker.IsPlayer() && m_character.IsTamed()))
		{
			return;
		}
		if (_combatAI != null && _combatAI.IsEngaged)
		{
			_combatAI.OnDamaged(attacker);
		}
		else if (_combatAI != null && !_combatAI.IsEngaged)
		{
			if (_combatAI.MayFightBack(attacker))
			{
				_combatAI.Engage(attacker);
			}
		}
		else if (m_targetCreature == null)
		{
			m_targetCreature = attacker;
			m_lastKnownTargetPos = attacker.transform.position;
			m_beenAtLastPos = false;
			m_targetStatic = null;
		}
	}

	private void Wakeup()
	{
		if (IsSleeping())
		{
			if (m_animator != null)
			{
				m_animator.SetBool(s_sleeping, value: false);
			}
			m_nview.GetZDO().Set(ZDOVars.s_sleeping, value: false);
			m_wakeupEffects.Create(transform.position, transform.rotation, null, 1f, -1, ZDOID.None);
			m_sleeping = false;
			m_nview.InvokeRPC(ZNetView.Everybody, "RPC_Wakeup");
		}
	}

	protected override void RPC_OnNearProjectileHit(long sender, Vector3 center, float range, ZDOID attackerID)
	{
		if (!m_nview.IsOwner() || (_setup?.GetCombatStance() ?? 0) == 1)
		{
			return;
		}
		SetAlerted(alert: true);
		GameObject gameObject = ZNetScene.instance.FindInstance(attackerID);
		if (!(gameObject != null))
		{
			return;
		}
		Character component = gameObject.GetComponent<Character>();
		if (component != null)
		{
			if (_combatAI != null && !_combatAI.IsEngaged)
			{
				_combatAI.Engage(component);
			}
			else if (_combatAI != null && _combatAI.IsEngaged)
			{
				_combatAI.OnDamaged(component);
			}
			else if (m_targetCreature == null)
			{
				m_targetCreature = component;
				m_lastKnownTargetPos = component.transform.position;
				m_beenAtLastPos = false;
				m_targetStatic = null;
			}
		}
	}

	private void Sleep()
	{
		if (!IsSleeping())
		{
			SetAlerted(alert: false);
			m_sleepTimer = 0f;
			m_targetCreature = null;
			if (m_animator != null)
			{
				m_animator.SetBool(s_sleeping, value: true);
			}
			m_nview.GetZDO().Set(ZDOVars.s_sleeping, value: true);
			m_sleepEffects.Create(transform.position, transform.rotation, null, 1f, -1, ZDOID.None);
			m_sleeping = true;
			m_nview.InvokeRPC(ZNetView.Everybody, "RPC_Sleep");
		}
	}

	public override bool IsSleeping()
	{
		return m_sleeping;
	}

	private Vector3Int ToBlacklistKey(Vector3 pos)
	{
		return new Vector3Int(Mathf.FloorToInt(pos.x / 2f), Mathf.FloorToInt(pos.y / 2f), Mathf.FloorToInt(pos.z / 2f));
	}

	internal void BlacklistPosition(Vector3 pos)
	{
		Vector3Int key = ToBlacklistKey(pos);
		_targetBlacklist[key] = Time.time;
	}

	internal bool IsPositionBlacklisted(Vector3 pos)
	{
		Vector3Int key = ToBlacklistKey(pos);
		if (_targetBlacklist.TryGetValue(key, out var value))
		{
			if (Time.time - value < 30f)
			{
				return true;
			}
			_targetBlacklist.Remove(key);
		}
		return false;
	}
}
