using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionSetup : MonoBehaviour
{
	private static readonly List<CompanionSetup> _allCompanions = new List<CompanionSetup>();

	internal static readonly int AppearanceHash = "HC_Appearance".GetStableHashCode();

	internal static readonly int OwnerHash = "HC_Owner".GetStableHashCode();

	internal static readonly int DeathItemRuleHash = "HC_DeathItemRule".GetStableHashCode();

	private float _mhrRuleStampTimer;

	internal bool _mhrDespawning;

	internal bool _mhrDying;

	internal bool _mhrStale;

	internal float _mhrAwakeTime;

	internal static readonly int NameHash = "HC_Name".GetStableHashCode();

	internal static readonly int ActionModeHash = "HC_ActionMode".GetStableHashCode();

	internal static readonly int ActionModeSchemaHash = "HC_ActionModeSchema".GetStableHashCode();

	internal static readonly int StaminaHash = "HC_Stamina".GetStableHashCode();

	internal static readonly int AutoPickupHash = "HC_AutoPickup".GetStableHashCode();

	internal static readonly int WanderHash = "HC_Wander".GetStableHashCode();

	internal static readonly int WanderFollowMigratedHash = "HC_WanderFollowMigrated".GetStableHashCode();

	internal static readonly int FormationSlotHash = "HC_FormationSlot".GetStableHashCode();

	internal static readonly int IsCommandableHash = "HC_IsCommandable".GetStableHashCode();

	internal static readonly int StayHomeHash = "HC_StayHome".GetStableHashCode();

	internal static readonly int HomePosHash = "HC_HomePos".GetStableHashCode();

	internal static readonly int HomePosSetHash = "HC_HomePosSet".GetStableHashCode();

	internal static readonly int HomeRadiusHash = "HC_HomeRadius".GetStableHashCode();

	private static readonly int StarterGearHash = "HC_StarterGear".GetStableHashCode();

	internal static readonly int CombatStanceHash = "HC_CombatStance".GetStableHashCode();

	internal static readonly int FollowHash = "HC_Follow".GetStableHashCode();

	internal static readonly int TombstoneIdHash = "HC_TombstoneId".GetStableHashCode();

	internal static readonly int MHR_TombOwnerHash = "HC_TombOwner".GetStableHashCode();

	internal static readonly int MHR_TombItemsHash = "HC_TombItems".GetStableHashCode();

	internal static readonly int MHR_CompanionIdHash = "HC_CompanionId".GetStableHashCode();

	internal static readonly int MHR_TombCompanionIdHash = "HC_TombCompanionId".GetStableHashCode();

	private bool _mhrHasId;

	internal static readonly int TombInvWidthHash = "HC_TombInvW".GetStableHashCode();

	internal static readonly int TombInvHeightHash = "HC_TombInvH".GetStableHashCode();

	internal static readonly int HomeBedPosHash = "HC_HomeBedPos".GetStableHashCode();

	internal static readonly int HasHomeBedHash = "HC_HasHomeBed".GetStableHashCode();

	private static readonly HashSet<VisEquipment> _companionVisEquips = new HashSet<VisEquipment>();

	private bool _wasHeavy;

	internal const int ModeFollow = 0;

	internal const int ModeGatherWood = 1;

	internal const int ModeGatherStone = 2;

	internal const int ModeGatherOre = 3;

	internal const int ModeStay = 4;

	internal const int ModeForage = 5;

	internal const int ModeSmelt = 6;

	internal const int ModeHunt = 7;

	internal const int ModeFarm = 8;

	internal const int ModeFish = 9;

	internal const int ModeRepairBuildings = 15;

	internal const int ModeRestock = 16;

	internal const int ModeCook = 17;

	internal const int StanceBalanced = 0;

	internal const int StancePassive = 1;

	internal const int StanceRanged = 2;

	internal const int StanceMelee = 3;

	private const int ActionModeSchemaVersion = 3;

	private static readonly MethodInfo _updateVisuals = AccessTools.Method(typeof(VisEquipment), "UpdateVisuals");

	internal static readonly FieldInfo _rightItemField = AccessTools.Field(typeof(Humanoid), "m_rightItem");

	internal static readonly FieldInfo _leftItemField = AccessTools.Field(typeof(Humanoid), "m_leftItem");

	private static readonly FieldInfo _chestItemField = AccessTools.Field(typeof(Humanoid), "m_chestItem");

	private static readonly FieldInfo _legItemField = AccessTools.Field(typeof(Humanoid), "m_legItem");

	private static readonly FieldInfo _helmetItemField = AccessTools.Field(typeof(Humanoid), "m_helmetItem");

	private static readonly FieldInfo _shoulderItemField = AccessTools.Field(typeof(Humanoid), "m_shoulderItem");

	private static readonly FieldInfo _utilityItemField = AccessTools.Field(typeof(Humanoid), "m_utilityItem");

	private static readonly FieldInfo _trinketItemField = AccessTools.Field(typeof(Humanoid), "m_trinketItem");

	private ZNetView _nview;

	private VisEquipment _visEquip;

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private ZSyncAnimation _zanim;

	private HarvestController _harvestCached;

	private CompanionRest _restCached;

	private SmeltController _smeltCached;

	private HuntController _huntCached;

	private CompanionCombatAI _combatAI;

	private bool _initialized;

	private bool _ownerMismatchLogged;

	private bool _uiFrozen;

	private bool _dead;

	private Minimap.PinData _minimapPin;

	private ZDOID _zdoid = ZDOID.None;

	private bool _removePinOnDestroy;

	private float _pinHiddenUntil;

	private static Minimap s_pinMinimapInstance;

	private static readonly Dictionary<ZDOID, Minimap.PinData> s_persistentPins = new Dictionary<ZDOID, Minimap.PinData>();

	private bool _equipping;

	private float _autoEquipCooldown;

	private bool _autoEquipPending;

	private const float AutoEquipMinInterval = 0.5f;

	private readonly List<ItemDrop.ItemData> _equipQueue = new List<ItemDrop.ItemData>();

	private float _equipTimer;

	private bool _equipAnimActive;

	private Action _inventoryCallback;

	private bool _mhrNoDestroyDone;

	private float _instantEquipUntil;

	internal static IReadOnlyList<CompanionSetup> AllCompanions => _allCompanions;

	internal bool SuppressAutoEquip { get; set; }

	internal bool IsEquipping
	{
		get
		{
			if (_equipQueue.Count <= 0)
			{
				return _equipAnimActive;
			}
			return true;
		}
	}

	internal void RestoreFollowTarget()
	{
		if (_ai == null || Player.m_localPlayer == null)
		{
			return;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID == 0L)
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			zDO.Set(OwnerHash, text2);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] RestoreFollow — claimed orphan, set owner to " + text2);
			}
			text = text2;
		}
		if (text != text2)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] RestoreFollow — owner mismatch: zdo=\"" + text + "\" local=\"" + text2 + "\"");
			}
		}
		else
		{
			int mode = zDO.GetInt(ActionModeHash);
			ApplyFollowMode(mode);
			AssignFormationSlot();
		}
	}

	internal void ApplyAppearance(CompanionAppearance a)
	{
		if (CanWearArmor() && _visEquip != null)
		{
			_visEquip.SetModel(a.ModelIndex);
			_visEquip.SetHairItem((string.IsNullOrEmpty(a.HairItem) ? "Hair1" : a.HairItem).GetStableHashCode());
			_visEquip.SetBeardItem(((a.ModelIndex == 0) ? (a.BeardItem ?? "") : "").GetStableHashCode());
			_visEquip.SetSkinColor(a.SkinColor);
			_visEquip.SetHairColor(a.HairColor);
		}
		if (_visEquip != null)
		{
			_updateVisuals?.Invoke(_visEquip, null);
		}
		int num = LayerMask.NameToLayer("character");
		if (num < 0)
		{
			num = 9;
		}
		Transform[] componentsInChildren = GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = num;
		}
	}

	internal float GetTotalArmor()
	{
		float num = 0f;
		ItemDrop.ItemData equipSlot = GetEquipSlot(_chestItemField);
		ItemDrop.ItemData equipSlot2 = GetEquipSlot(_legItemField);
		ItemDrop.ItemData equipSlot3 = GetEquipSlot(_helmetItemField);
		ItemDrop.ItemData equipSlot4 = GetEquipSlot(_shoulderItemField);
		ItemDrop.ItemData equipSlot5 = GetEquipSlot(_trinketItemField);
		if (equipSlot != null)
		{
			num += equipSlot.GetArmor();
		}
		if (equipSlot2 != null)
		{
			num += equipSlot2.GetArmor();
		}
		if (equipSlot3 != null)
		{
			num += equipSlot3.GetArmor();
		}
		if (equipSlot4 != null)
		{
			num += equipSlot4.GetArmor();
		}
		if (equipSlot5 != null)
		{
			num += equipSlot5.GetArmor();
		}
		return num;
	}

	internal static bool IsCompanionVisEquip(VisEquipment ve)
	{
		return _companionVisEquips.Contains(ve);
	}

	internal bool CanWearArmor()
	{
		if (_nview == null || _nview.GetZDO() == null)
		{
			return true;
		}
		return !CompanionTierData.IsDvergerVariant(_nview.GetZDO().GetPrefab());
	}

	internal bool HasHomePosition()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		if (zDO.GetBool(HomePosSetHash))
		{
			return true;
		}
		if (zDO.GetVec3(HomePosHash, Vector3.zero) != Vector3.zero)
		{
			zDO.Set(HomePosSetHash, value: true);
			return true;
		}
		return false;
	}

	internal bool GetFollow()
	{
		return _nview?.GetZDO()?.GetBool(FollowHash, defaultValue: true) ?? true;
	}

	internal Vector3 GetHomePosition()
	{
		return _nview?.GetZDO()?.GetVec3(HomePosHash, Vector3.zero) ?? Vector3.zero;
	}

	internal bool GetWander()
	{
		ZNetView nview = _nview;
		if ((object)nview == null)
		{
			return false;
		}
		return nview.GetZDO()?.GetBool(WanderHash) == true;
	}

	internal float GetHomeRadius()
	{
		return _nview?.GetZDO()?.GetFloat(HomeRadiusHash, ModConfig.HomeZoneRadius.Value) ?? ModConfig.HomeZoneRadius.Value;
	}

	internal int GetCombatStance()
	{
		return (_nview?.GetZDO()?.GetInt(CombatStanceHash)).GetValueOrDefault();
	}

	internal int AssignFormationSlot()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return 0;
		}
		int num = zDO.GetInt(FormationSlotHash, -1);
		if (num >= 0)
		{
			return num;
		}
		HashSet<int> hashSet = new HashSet<int>();
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return 0;
		}
		string text = localPlayer.GetPlayerID().ToString();
		foreach (CompanionSetup allCompanion in _allCompanions)
		{
			if (allCompanion == this)
			{
				continue;
			}
			ZDO zDO2 = allCompanion._nview?.GetZDO();
			if (zDO2 != null && !(zDO2.GetString(OwnerHash) != text))
			{
				int num2 = zDO2.GetInt(FormationSlotHash, -1);
				if (num2 >= 0)
				{
					hashSet.Add(num2);
				}
			}
		}
		for (int i = 0; i < 8; i++)
		{
			if (!hashSet.Contains(i))
			{
				zDO.Set(FormationSlotHash, i);
				return i;
			}
		}
		zDO.Set(FormationSlotHash, 0);
		return 0;
	}

	internal void SyncEquipmentToInventory()
	{
		if (!(_humanoid == null) && !(_nview == null) && _nview.IsOwner())
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup] SyncEquipmentToInventory — SuppressAutoEquip={SuppressAutoEquip}");
			}
			UnequipMissingEquippedItems();
			AutoEquipBest(SuppressAutoEquip);
		}
	}

	internal void SetFollow(bool v)
	{
		_nview?.GetZDO()?.Set(FollowHash, v);
	}

	internal bool GetIsCommandable()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return true;
		}
		return zDO.GetInt(IsCommandableHash, 1) != 0;
	}

	internal string MHR_EnsureCompanionId()
	{
		if (!(_nview == null))
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null)
			{
				string text = zDO.GetString(MHR_CompanionIdHash);
				if (string.IsNullOrEmpty(text))
				{
					if (!_nview.IsOwner())
					{
						goto IL_0093;
					}
					text = Guid.NewGuid().ToString("N");
					zDO.Set(MHR_CompanionIdHash, text);
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo(string.Format("[Setup] Companion \"{0}\" was given its permanent ID {1}", zDO.GetString(NameHash, "Companion"), text));
					}
				}
				return text;
			}
		}
		goto IL_0093;
		IL_0093:
		return "";
	}

	internal ItemDrop.ItemData GetHelmetItem()
	{
		return GetEquipSlot(_helmetItemField);
	}

	internal ItemDrop.ItemData GetEquipSlot(FieldInfo field)
	{
		return field?.GetValue(_humanoid) as ItemDrop.ItemData;
	}

	internal ItemDrop.ItemData GetChestItem()
	{
		return GetEquipSlot(_chestItemField);
	}

	internal ItemDrop.ItemData GetLegsItem()
	{
		return GetEquipSlot(_legItemField);
	}

	internal ItemDrop.ItemData GetShoulderItem()
	{
		return GetEquipSlot(_shoulderItemField);
	}

	internal ItemDrop.ItemData GetUtilityItem()
	{
		return GetEquipSlot(_utilityItemField);
	}

	internal ItemDrop.ItemData GetTrinketItem()
	{
		return GetEquipSlot(_trinketItemField);
	}

	private void Awake()
	{
		MHR_MarkNoDestroy();
		_mhrAwakeTime = Time.time;
		_allCompanions.Add(this);
		_nview = GetComponent<ZNetView>();
		_visEquip = GetComponent<VisEquipment>();
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_zanim = GetComponent<ZSyncAnimation>();
		_harvestCached = GetComponent<HarvestController>();
		_restCached = GetComponent<CompanionRest>();
		_smeltCached = GetComponent<SmeltController>();
		_huntCached = GetComponent<HuntController>();
		_combatAI = GetComponent<CompanionCombatAI>();
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo($"[Setup] Awake — nview={_nview != null} visEquip={_visEquip != null} " + $"ai={_ai != null} humanoid={_humanoid != null} name=\"{gameObject.name}\"");
		}
		if (_visEquip != null)
		{
			_companionVisEquips.Add(_visEquip);
		}
		if (_humanoid != null)
		{
			_humanoid.m_onDeath = (Action)Delegate.Combine(_humanoid.m_onDeath, new Action(OnCompanionDeath));
		}
		TryInit();
	}

	internal void MHR_MarkNoDestroy()
	{
		if (_mhrNoDestroyDone || _nview == null)
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null)
		{
			return;
		}
		if (!zDO.GetBool("SKK_NoDestroy"))
		{
			if (!_nview.IsOwner())
			{
				return;
			}
			zDO.Set("SKK_NoDestroy", value: true);
		}
		_mhrNoDestroyDone = true;
	}

	private void OnCompanionDeath()
	{
		_mhrDying = true;
		if (_dead)
		{
			return;
		}
		_dead = true;
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null)
		{
			return;
		}
		MHR_Roster.OnDied(this);
		if (CompanionInteractPanel.IsOpenFor(this))
		{
			CompanionInteractPanel.Instance?.Hide();
			InventoryGui.instance?.Hide();
		}
		string text = ((_humanoid != null) ? _humanoid.m_name : "Companion");
		string text2 = zDO.GetString(NameHash);
		if (!string.IsNullOrEmpty(text2))
		{
			text = text2;
		}
		string appearance = zDO.GetString(AppearanceHash);
		string ownerId = zDO.GetString(OwnerHash);
		int actionMode = zDO.GetInt(ActionModeHash);
		int actionModeSchema = zDO.GetInt(ActionModeSchemaHash);
		bool follow = zDO.GetBool(FollowHash, defaultValue: true);
		bool stayHome = zDO.GetBool(StayHomeHash);
		bool autoPickup = zDO.GetBool(AutoPickupHash, defaultValue: true);
		bool wander = zDO.GetBool(WanderHash);
		int num = zDO.GetInt(IsCommandableHash, 1);
		int formationSlot = zDO.GetInt(FormationSlotHash, -1);
		Vector3 vector = ((_humanoid != null) ? _humanoid.GetCenterPoint() : transform.position);
		int prefab = zDO.GetPrefab();
		string prefabName = CompanionTierData.Companion.PrefabName;
		GameObject gameObject = ZNetScene.instance?.GetPrefab(prefab);
		if (gameObject != null)
		{
			prefabName = gameObject.name;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo($"[Setup] Companion \"{text}\" died at {vector:F1} — prefab={prefabName}");
		}
		try
		{
			ConfigEntry<bool> speechAnnounceDeath = ModConfig.SpeechAnnounceDeath;
			if ((speechAnnounceDeath == null || speechAnnounceDeath.Value) && MHR_Owner.IsMine(this) && MessageHud.instance != null)
			{
				string text3 = "";
				if (WorldGenerator.instance != null)
				{
					Heightmap.Biome biome = WorldGenerator.instance.GetBiome(vector);
					text3 = Localization.instance?.Localize("$biome_" + biome.ToString().ToLowerInvariant()) ?? biome.ToString();
				}
				float num2 = ((Player.m_localPlayer != null) ? Vector3.Distance(Player.m_localPlayer.transform.position, vector) : 0f);
				MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, ModLocalization.LocFmt("hc_msg_companion_died", text, text3, num2.ToString("F0")));
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Setup] Death announcement failed: " + ex.Message);
		}
		string skillsSerialized = "";
		CompanionSkills component = GetComponent<CompanionSkills>();
		if (component != null)
		{
			component.OnDeath();
			skillsSerialized = component.SerializeSkills();
		}
		long num3 = zDO.GetLong(TombstoneIdHash, 0L);
		int num4;
		long num5;
		if (_humanoid != null && _humanoid.GetInventory() != null)
		{
			num4 = ((_humanoid.GetInventory().NrOfItems() > 0) ? 1 : 0);
			if (num4 != 0)
			{
				num5 = DateTime.UtcNow.Ticks;
				goto IL_035b;
			}
		}
		else
		{
			num4 = 0;
		}
		num5 = num3;
		goto IL_035b;
		IL_035b:
		long tombstoneId = num5;
		if (num4 == 0 && num3 != 0L && CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo($"[Setup] No items — preserving existing tombstoneId={num3} " + "so respawned companion can find previous tombstone");
		}
		bool flag = HasHomePosition();
		Vector3 homePos = (flag ? GetHomePosition() : Vector3.zero);
		int combatStance = zDO.GetInt(CombatStanceHash);
		bool hasHomeBed = zDO.GetBool(HasHomeBedHash);
		Vector3 vec = zDO.GetVec3(HomeBedPosHash, Vector3.zero);
		CreateCompanionTombstone(text, vector, tombstoneId, prefabName, appearance, ownerId, actionMode, actionModeSchema, follow, stayHome, autoPickup, wander, num != 0, formationSlot, skillsSerialized, flag, homePos, combatStance, hasHomeBed, vec);
	}

	private void TryInit()
	{
		if (!_initialized && !(_nview == null) && _nview.GetZDO() != null)
		{
			ZDO zDO = _nview.GetZDO();
			string text = zDO.GetString(AppearanceHash);
			CompanionAppearance a = (string.IsNullOrEmpty(text) ? CompanionAppearance.Default() : CompanionAppearance.Deserialize(text));
			EnsureActionModeMigration();
			ApplyAppearance(a);
			RestoreFollowTarget();
			RestoreActionMode();
			RestoreName();
			RegisterInventoryCallback();
			AddStarterGear(zDO);
			int num = zDO.GetInt(ActionModeHash);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo(string.Format("[Setup] Initialized — mode={0} appearance={1} ", num, string.IsNullOrEmpty(text) ? "default" : "saved") + $"pos={transform.position:F1}");
			}
			_instantEquipUntil = Time.time + 10f;
			_initialized = true;
		}
	}

	private void Start()
	{
		if (!_initialized)
		{
			TryInit();
		}
	}

	private void Update()
	{
		if (!_initialized)
		{
			TryInit();
			return;
		}
		MHR_StampOwnerDeathRule();
		MHR_MarkNoDestroy();
		MHR_RiseWatch.Tick(this);
		if (!_mhrHasId && !string.IsNullOrEmpty(MHR_EnsureCompanionId()))
		{
			_mhrHasId = true;
		}
		if (CompanionInteractPanel.IsOpenFor(this) || CompanionRadialMenu.IsOpenFor(this))
		{
			_ai?.SetPatrolPoint();
			_ai?.SetFollowTarget(null);
			_uiFrozen = true;
			return;
		}
		if (_uiFrozen)
		{
			_uiFrozen = false;
			if (_ai != null && _ai.IsRecoveringTombstone)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] UI closed during tombstone recovery — skipping follow restore");
				}
			}
			else
			{
				ZDO zDO = _nview?.GetZDO();
				if (zDO != null && _ai != null)
				{
					int mode = zDO.GetInt(ActionModeHash);
					ApplyFollowMode(mode, force: true);
				}
			}
		}
		if (_autoEquipCooldown > 0f)
		{
			_autoEquipCooldown -= Time.deltaTime;
			if (_autoEquipCooldown <= 0f && _autoEquipPending)
			{
				_autoEquipPending = false;
				if (_humanoid != null && _nview != null && _nview.IsOwner())
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Setup] Deferred auto-equip firing now" + (SuppressAutoEquip ? " (armour only - a job holds the hands)" : ""));
					}
					AutoEquipBest(SuppressAutoEquip);
				}
			}
		}
		UpdateEquipQueue(Time.deltaTime);
		UpdateMinimapPin();
		if (_nview != null && _nview.IsOwner() && _humanoid != null)
		{
			Inventory inventory = _humanoid.GetInventory();
			SEMan sEMan = _humanoid.GetSEMan();
			if (inventory != null && sEMan != null)
			{
				bool flag = inventory.GetTotalWeight() > CompanionTierData.CarryLimit(_humanoid);
				if (flag)
				{
					sEMan.AddStatusEffect(SEMan.s_statusEffectEncumbered, resetTime: false, 0, 0f, -1);
					CompanionStatus.Doing(this, "hc_doing_encumbered");
					if (!_wasHeavy)
					{
						GetComponent<CompanionTalk>()?.SayProblem("encumbered", ModLocalization.Loc("hc_say_encumbered"));
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo($"[Carry] {_humanoid.m_name} is encumbered - {inventory.GetTotalWeight():F0}/{CompanionTierData.CarryLimit(_humanoid):F0}");
						}
					}
				}
				else
				{
					sEMan.RemoveStatusEffect(SEMan.s_statusEffectEncumbered);
					if (_wasHeavy)
					{
						CompanionStatus.DoingClear(this);
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo("[Carry] " + _humanoid.m_name + " is no longer encumbered");
						}
					}
				}
				_wasHeavy = flag;
			}
		}
		if (!(_ai != null) || !(_ai.GetFollowTarget() == null) || !(Player.m_localPlayer != null))
		{
			return;
		}
		ZDO zDO2 = _nview?.GetZDO();
		if (zDO2 == null)
		{
			return;
		}
		string text = zDO2.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID == 0L)
		{
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			zDO2.Set(OwnerHash, text2);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Claimed orphan companion — set owner to " + text2);
			}
			text = text2;
		}
		if (text != text2 && MHR_Owner.Of(this) == null)
		{
			if (!_ownerMismatchLogged)
			{
				_ownerMismatchLogged = true;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Another player's companion, its player isn't here — follow target not restored");
				}
			}
			return;
		}
		int num = zDO2.GetInt(ActionModeHash);
		if ((!(_harvestCached != null) || !_harvestCached.IsActive) && (!(_smeltCached != null) || !_smeltCached.IsActive) && (!(_huntCached != null) || !_huntCached.IsActive) && (!(_restCached != null) || (!_restCached.IsNavigating && !_restCached.IsResting)) && !(_ai.PendingCartAttach != null) && !_ai.PendingMoveTarget.HasValue && !(_ai.PendingDepositContainer != null) && !MHR_ToolKeeper.IsBusy(this) && !_ai.IsRecoveringTombstone && GetFollow() && num != 4)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Follow target was null — restored to its player " + $"(mode={num} harvestActive={_harvestCached?.IsActive ?? false})");
			}
		}
	}

	private void MHR_StampOwnerDeathRule()
	{
		_mhrRuleStampTimer -= Time.deltaTime;
		if (!(_mhrRuleStampTimer <= 0f))
		{
			return;
		}
		_mhrRuleStampTimer = 2f;
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null || Player.m_localPlayer == null)
		{
			return;
		}
		long playerID = Player.m_localPlayer.GetPlayerID();
		if (zDO.GetString(OwnerHash) == playerID.ToString())
		{
			int value = ModConfig.DeathItemRule.Value;
			if (zDO.GetInt(DeathItemRuleHash, -1) != value)
			{
				zDO.Set(DeathItemRuleHash, value);
			}
		}
	}

	internal void ApplyFollowMode(int mode, bool force = false)
	{
		if (_ai == null)
		{
			return;
		}
		bool follow = GetFollow();
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] ApplyFollowMode — mode={mode} follow={follow} force={force}");
		}
		switch (mode)
		{
		case 0:
			if (follow && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (mode={mode})");
				}
			}
			else
			{
				_ai.SetFollowTarget(null);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup]   → Follow OFF, no StayHome — idle");
				}
			}
			return;
		case 1:
		case 2:
		case 3:
		case 5:
		case 6:
		case 7:
		case 8:
		case 17:
			if (!force)
			{
				HarvestController component = GetComponent<HarvestController>();
				if (component != null && component.IsActive)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup]   → Gather mode={mode}, harvest active — skipping follow override");
					}
					return;
				}
				SmeltController component2 = GetComponent<SmeltController>();
				if (component2 != null && component2.IsActive)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup]   → Smelt mode={mode}, smelt active — skipping follow override");
					}
					return;
				}
				FarmController component3 = GetComponent<FarmController>();
				if (component3 != null && component3.IsActive)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup]   → Farm mode={mode}, farm active — skipping follow override");
					}
					return;
				}
				HuntController component4 = GetComponent<HuntController>();
				if (component4 != null && component4.IsActive)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup]   → Hunt mode={mode}, hunt active — skipping follow override");
					}
					return;
				}
				CookController component5 = GetComponent<CookController>();
				if (component5 != null && component5.IsActive)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup]   → Cook mode={mode}, cook active — skipping follow override");
					}
					return;
				}
			}
			if (follow && MHR_Owner.Of(this) != null)
			{
				_ai.SetFollowTarget(MHR_Owner.Go(this));
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (mode={mode})");
				}
			}
			else
			{
				_ai.SetFollowTarget(null);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow OFF, gather mode={mode} — idle");
				}
			}
			return;
		case 9:
			_ai.SetFollowTarget(null);
			_ai.SetPatrolPoint();
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup]   → Fish mode — stationary, patrol at current pos");
			}
			return;
		case 4:
			_ai.SetFollowTarget(null);
			_ai.SetPatrolPoint();
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Stay/patrol at {transform.position:F1}");
			}
			return;
		}
		if (follow && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow player (default fallback, mode={mode})");
			}
		}
		else
		{
			_ai.SetFollowTarget(null);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup]   → Follow OFF (default fallback, mode={mode})");
			}
		}
	}

	private void AutoEquipBest(bool armorOnly = false)
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || TorchOpener.IsActive(_humanoid))
		{
			return;
		}
		if (_equipQueue.Count > 0)
		{
			_autoEquipPending = true;
			_autoEquipCooldown = 1f;
			return;
		}
		ClearEquipQueue();
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		ItemDrop.ItemData itemData = null;
		float num = 0f;
		ItemDrop.ItemData itemData2 = null;
		float num2 = 0f;
		float num3 = 0f;
		ItemDrop.ItemData itemData3 = null;
		float num4 = 0f;
		ItemDrop.ItemData itemData4 = null;
		float num5 = 0f;
		ItemDrop.ItemData itemData5 = null;
		float num6 = 0f;
		ItemDrop.ItemData itemData6 = null;
		float num7 = 0f;
		ItemDrop.ItemData shoulder = null;
		float num8 = 0f;
		ItemDrop.ItemData utility = null;
		float num9 = 0f;
		ItemDrop.ItemData itemData7 = null;
		float num10 = 0f;
		foreach (ItemDrop.ItemData item in allItems)
		{
			if (item == null || item.m_shared == null || (item.m_shared.m_useDurability && item.m_durability <= 0f) || item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				continue;
			}
			ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
			float num11 = WeaponLogic.Score(item, null);
			float armor = item.GetArmor();
			switch (itemType)
			{
			case ItemDrop.ItemData.ItemType.OneHandedWeapon:
				if (num11 > num)
				{
					itemData = item;
					num = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
			case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
				if (item.GetDamage().m_pickaxe > 0f)
				{
					if (num11 > num3)
					{
						num3 = num11;
					}
				}
				else if (num11 > num2)
				{
					itemData2 = item;
					num2 = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.Tool:
				if (num11 > num3)
				{
					num3 = num11;
				}
				break;
			case ItemDrop.ItemData.ItemType.Chest:
				if (armor > num5)
				{
					itemData4 = item;
					num5 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Legs:
				if (armor > num6)
				{
					itemData5 = item;
					num6 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Helmet:
				if (armor > num7)
				{
					itemData6 = item;
					num7 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Shoulder:
				if (armor > num8)
				{
					shoulder = item;
					num8 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Utility:
				if (armor > num9)
				{
					utility = item;
					num9 = armor;
				}
				break;
			case ItemDrop.ItemData.ItemType.Shield:
			{
				float num12 = item.m_shared.m_timedBlockBonus * 1000f + item.GetBaseBlockPower();
				if (num12 > num4)
				{
					itemData3 = item;
					num4 = num12;
				}
				break;
			}
			case ItemDrop.ItemData.ItemType.Trinket:
				if (armor > num10)
				{
					itemData7 = item;
					num10 = armor;
				}
				break;
			}
		}
		EquipDiag.OnScan(allItems, itemData);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug(string.Format("[Setup] AutoEquipBest — 1H=\"{0}\"({1:F0}) ", itemData?.m_shared?.m_name ?? "none", num) + string.Format("2H=\"{0}\"({1:F0}) ", itemData2?.m_shared?.m_name ?? "none", num2) + string.Format("shield=\"{0}\"(block={1:F0}) ", itemData3?.m_shared?.m_name ?? "none", num4) + "helm=\"" + (itemData6?.m_shared?.m_name ?? "none") + "\" chest=\"" + (itemData4?.m_shared?.m_name ?? "none") + "\" legs=\"" + (itemData5?.m_shared?.m_name ?? "none") + "\" trinket=\"" + (itemData7?.m_shared?.m_name ?? "none") + "\"");
		}
		if (armorOnly)
		{
			EquipBestArmor(itemData4, itemData5, itemData6, shoulder, utility, itemData7);
			return;
		}
		ItemDrop.ItemData itemData8 = GetEquipSlot(_rightItemField);
		ItemDrop.ItemData itemData9 = GetEquipSlot(_leftItemField);
		if (GetCombatStance() == 2 && (!(_combatAI != null) || !_combatAI.IsEngaged))
		{
			ItemDrop.ItemData itemData10 = WeaponLogic.FindReadyBow(_humanoid, null);
			if (itemData10 != null)
			{
				if (itemData9 != itemData10 && itemData8 != itemData10)
				{
					if (itemData8 != null)
					{
						_humanoid.UnequipItem(itemData8, triggerEquipEffects: false);
					}
					if (itemData9 != null)
					{
						_humanoid.UnequipItem(itemData9, triggerEquipEffects: false);
					}
					QueueEquip(itemData10);
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Setup] Ranged stance - queued the bow: \"" + (itemData10.m_shared?.m_name ?? "?") + "\"");
					}
				}
				EquipBestArmor(itemData4, itemData5, itemData6, shoulder, utility, itemData7);
				return;
			}
		}
		bool flag = itemData2 != null && (itemData == null || num2 >= num);
		ItemDrop.ItemData itemData11 = (flag ? itemData2 : itemData);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] Weapon decision: use2H={flag} " + "desired=\"" + (itemData11?.m_shared?.m_name ?? "none") + "\" current=\"" + (itemData8?.m_shared?.m_name ?? "none") + "\" " + $"needsSwap={itemData11 != null && itemData8 != itemData11}");
		}
		bool num13 = _combatAI != null && _combatAI.IsEngaged;
		bool flag2 = (itemData8 != null && itemData8.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) || (itemData9 != null && itemData9.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
		bool flag3 = itemData8 != null && itemData8.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && _ai != null && _ai.WantsTorch;
		if (!num13 && !flag3 && (!flag2 || GetCombatStance() != 2) && itemData11 != null && itemData8 != itemData11)
		{
			if (itemData8 != null)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Unequip right: \"" + (itemData8.m_shared?.m_name ?? "?") + "\"");
				}
				_humanoid.UnequipItem(itemData8, triggerEquipEffects: false);
			}
			if (IsTwoHandedWeapon(itemData11) && itemData9 != null)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Unequip left for 2H: \"" + (itemData9.m_shared?.m_name ?? "?") + "\"");
				}
				_humanoid.UnequipItem(itemData9, triggerEquipEffects: false);
				itemData9 = null;
			}
			QueueEquip(itemData11);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Queued right: \"" + (itemData11.m_shared?.m_name ?? "?") + "\"");
			}
			itemData8 = itemData11;
		}
		bool flag4 = itemData8 != null && IsTwoHandedWeapon(itemData8);
		bool flag5 = itemData9 != null && IsTwoHandedWeapon(itemData9);
		bool flag6 = itemData9 != null && itemData9.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch && _ai != null && _ai.WantsTorch;
		if (!num13 && !flag4 && !flag5 && !flag6 && itemData3 != null)
		{
			if (itemData9 != itemData3)
			{
				if (itemData9 != null)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Setup] Unequip left for shield swap: \"" + (itemData9.m_shared?.m_name ?? "?") + "\"");
					}
					_humanoid.UnequipItem(itemData9, triggerEquipEffects: false);
				}
				QueueEquip(itemData3);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Setup] Queued shield: \"" + (itemData3.m_shared?.m_name ?? "?") + "\"");
				}
			}
		}
		else if (flag4 && itemData9 != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Unequip left (2H in right): \"" + (itemData9.m_shared?.m_name ?? "?") + "\"");
			}
			_humanoid.UnequipItem(itemData9, triggerEquipEffects: false);
		}
		EquipBestArmor(itemData4, itemData5, itemData6, shoulder, utility, itemData7);
	}

	internal void MHR_DropQueuedHandItems()
	{
		int num = _equipQueue.RemoveAll((ItemDrop.ItemData it) => it?.m_shared != null && IsHandItem(it));
		if (num > 0 && CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue: dropped {num} weapon/shield item(s) - a job holds the hands");
		}
	}

	private static bool IsHandItem(ItemDrop.ItemData it)
	{
		switch (it.m_shared.m_itemType)
		{
		case ItemDrop.ItemData.ItemType.OneHandedWeapon:
		case ItemDrop.ItemData.ItemType.Bow:
		case ItemDrop.ItemData.ItemType.Shield:
		case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
		case ItemDrop.ItemData.ItemType.Torch:
		case ItemDrop.ItemData.ItemType.Tool:
		case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
			return true;
		default:
			return false;
		}
	}

	private void EquipBestArmor(ItemDrop.ItemData chest, ItemDrop.ItemData legs, ItemDrop.ItemData helmet, ItemDrop.ItemData shoulder, ItemDrop.ItemData utility, ItemDrop.ItemData trinket)
	{
		if (CanWearArmor())
		{
			EquipBestArmorSlot(_chestItemField, chest);
			EquipBestArmorSlot(_legItemField, legs);
			EquipBestArmorSlot(_helmetItemField, helmet);
			EquipBestArmorSlot(_shoulderItemField, shoulder);
		}
		EquipBestArmorSlot(_utilityItemField, utility);
		EquipBestArmorSlot(_trinketItemField, trinket);
	}

	private void UpdateEquipQueue(float dt)
	{
		TorchOpener.Tick(_humanoid);
		if (_equipQueue.Count == 0)
		{
			if (_equipAnimActive)
			{
				if (_zanim != null && CanWearArmor())
				{
					_zanim.SetBool("equipping", value: false);
				}
				_equipAnimActive = false;
			}
			return;
		}
		Inventory inventory = _humanoid?.GetInventory();
		while (_equipQueue.Count > 0 && (inventory == null || !inventory.ContainsItem(_equipQueue[0])))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Equip queue: skipping \"" + (_equipQueue[0]?.m_shared?.m_name ?? "?") + "\" — no longer in inventory");
			}
			_equipQueue.RemoveAt(0);
		}
		if (_equipQueue.Count == 0)
		{
			return;
		}
		if (!_equipAnimActive)
		{
			_equipAnimActive = true;
			if (_zanim != null && CanWearArmor())
			{
				_zanim.SetBool("equipping", value: true);
			}
			float equipDuration = _equipQueue[0].m_shared.m_equipDuration;
			_equipTimer = ((equipDuration > 0f) ? equipDuration : 0.1f);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Equip queue: started \"" + _equipQueue[0].m_shared.m_name + "\" " + $"duration={_equipTimer:F1}s ({_equipQueue.Count} items queued)");
			}
			return;
		}
		_equipTimer -= dt;
		if (_equipTimer > 0f)
		{
			return;
		}
		ItemDrop.ItemData itemData = _equipQueue[0];
		_equipQueue.RemoveAt(0);
		_equipping = true;
		try
		{
			_humanoid.EquipItem(itemData);
			itemData.m_shared.m_equipEffect.Create(transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
		}
		finally
		{
			_equipping = false;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue: equipped \"{itemData.m_shared.m_name}\" ({_equipQueue.Count} remaining)");
		}
		if (_equipQueue.Count > 0)
		{
			float equipDuration2 = _equipQueue[0].m_shared.m_equipDuration;
			_equipTimer = ((equipDuration2 > 0f) ? equipDuration2 : 0.1f);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue: next \"{_equipQueue[0].m_shared.m_name}\" duration={_equipTimer:F1}s");
			}
		}
		else
		{
			if (_zanim != null && CanWearArmor())
			{
				_zanim.SetBool("equipping", value: false);
			}
			_equipAnimActive = false;
		}
	}

	private void UpdateMinimapPin()
	{
		if (Minimap.instance == null || _pinHiddenUntil > Time.time)
		{
			return;
		}
		if (s_pinMinimapInstance != Minimap.instance)
		{
			s_pinMinimapInstance = Minimap.instance;
			s_persistentPins.Clear();
			_minimapPin = null;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null || Player.m_localPlayer == null)
		{
			RemoveMinimapPin();
			return;
		}
		_zdoid = zDO.m_uid;
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		if (playerID == 0L)
		{
			return;
		}
		if (text != playerID.ToString())
		{
			RemoveMinimapPin();
			return;
		}
		if (_minimapPin == null && s_persistentPins.TryGetValue(_zdoid, out var value))
		{
			_minimapPin = value;
			s_persistentPins.Remove(_zdoid);
		}
		if (_minimapPin == null)
		{
			string arg = zDO.GetString(NameHash, "Companion");
			_minimapPin = Minimap.instance.AddPin(transform.position, Minimap.PinType.Player, arg, save: false, isChecked: false, 0L);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Minimap] Created pin for \"{arg}\" at {transform.position:F1}");
			}
		}
		if (_minimapPin.m_pos != transform.position)
		{
			_minimapPin.m_pos = transform.position;
		}
	}

	private void RemoveMinimapPin()
	{
		if (_minimapPin != null && Minimap.instance != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Minimap] Removed pin for \"" + _minimapPin.m_name + "\"");
			}
			Minimap.instance.RemovePin(_minimapPin);
			_minimapPin = null;
		}
	}

	public void HidePinFor(float seconds)
	{
		_pinHiddenUntil = Time.time + seconds;
		RemoveMinimapPin();
	}

	private void EnsureActionModeMigration()
	{
		if (_nview == null || !_nview.IsOwner())
		{
			return;
		}
		ZDO zDO = _nview.GetZDO();
		if (zDO == null)
		{
			return;
		}
		if (zDO.GetInt(ActionModeSchemaHash) < 3)
		{
			int num = zDO.GetInt(ActionModeHash);
			if (num == 2)
			{
				num = 4;
			}
			if ((num < 0 || num > 9) && num != 15 && num != 16 && num != 17)
			{
				num = 0;
			}
			zDO.Set(ActionModeHash, num);
			zDO.Set(ActionModeSchemaHash, 3);
		}
		if (!zDO.GetBool(WanderFollowMigratedHash))
		{
			zDO.Set(WanderHash, value: true);
			zDO.Set(WanderFollowMigratedHash, value: true);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Wander switched on (it now also controls how the companion moves around while following)");
			}
		}
		if (zDO.GetInt(ActionModeHash) == 16)
		{
			zDO.Set(ActionModeHash, 15);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Setup] Restock job moved to Repair and Restock");
			}
		}
		if (zDO.GetBool(StayHomeHash))
		{
			zDO.Set(StayHomeHash, value: false);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Setup] Homestead is retired - turned off (Wander still lets it move around home)");
			}
		}
		int mode = zDO.GetInt(ActionModeHash);
		if (MHR_JobRules.IsJobMode(mode))
		{
			MHR_JobRules.OnJobSet(this, mode);
		}
	}

	private void RestoreActionMode()
	{
		if (_ai == null || Player.m_localPlayer == null)
		{
			return;
		}
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string text = zDO.GetString(OwnerHash);
		long playerID = Player.m_localPlayer.GetPlayerID();
		string text2 = playerID.ToString();
		if (playerID != 0L)
		{
			if (string.IsNullOrEmpty(text))
			{
				zDO.Set(OwnerHash, text2);
				text = text2;
			}
			if (!(text != text2))
			{
				int mode = zDO.GetInt(ActionModeHash);
				ApplyFollowMode(mode);
			}
		}
	}

	private void RestoreName()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return;
		}
		string value = zDO.GetString(NameHash);
		if (!string.IsNullOrEmpty(value))
		{
			Character component = GetComponent<Character>();
			if (component != null)
			{
				component.m_name = value;
			}
		}
	}

	private void RegisterInventoryCallback()
	{
		if (!(_humanoid == null))
		{
			Inventory inventory = _humanoid.GetInventory();
			if (inventory != null)
			{
				_inventoryCallback = OnInventoryChanged;
				inventory.m_onChanged = (Action)Delegate.Combine(inventory.m_onChanged, _inventoryCallback);
			}
		}
	}

	private void AddStarterGear(ZDO zdo)
	{
		if (_nview.IsOwner())
		{
			zdo.Set(StarterGearHash, value: true);
		}
	}

	private void OnInventoryChanged()
	{
		if (_equipping || _humanoid == null)
		{
			return;
		}
		bool flag = _nview != null && _nview.IsOwner();
		_equipping = true;
		try
		{
			UnequipMissingEquippedItems();
			if (flag)
			{
				if (_autoEquipCooldown > 0f)
				{
					_autoEquipPending = true;
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug($"[Setup] OnInventoryChanged — deferred (cooldown={_autoEquipCooldown:F2}s)");
					}
				}
				else
				{
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Setup] OnInventoryChanged — auto-equip triggered" + (SuppressAutoEquip ? " (armour only)" : ""));
					}
					AutoEquipBest(SuppressAutoEquip);
					_autoEquipCooldown = 0.5f;
				}
			}
			if (flag)
			{
				NotifyControllersInventoryChanged();
			}
		}
		finally
		{
			_equipping = false;
		}
	}

	private void UnregisterInventoryCallback()
	{
		if (!(_humanoid == null) && _inventoryCallback != null)
		{
			Inventory inventory = _humanoid.GetInventory();
			if (inventory != null)
			{
				inventory.m_onChanged = (Action)Delegate.Remove(inventory.m_onChanged, _inventoryCallback);
				_inventoryCallback = null;
			}
		}
	}

	internal void PrepareForDespawn()
	{
		_removePinOnDestroy = true;
		MHR_Roster.Forget(this);
	}

	internal void DespawnWithTombstone()
	{
		_mhrDespawning = true;
		if (_humanoid == null)
		{
			return;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null || inventory.NrOfItems() == 0)
		{
			return;
		}
		string companionName = _humanoid.m_name ?? "Companion";
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			string text = zDO.GetString(NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				companionName = text;
			}
		}
		Vector3 centerPoint = _humanoid.GetCenterPoint();
		long ticks = DateTime.UtcNow.Ticks;
		_mhrDespawning = true;
		CreateCompanionTombstone(companionName, centerPoint, ticks, "", "", "", 0, 0, follow: false, stayHome: false, autoPickup: false, wander: false, commandable: false, -1, "", hasHome: false, Vector3.zero, 0, hasHomeBed: false, Vector3.zero);
	}

	private void CreateCompanionTombstone(string companionName, Vector3 position, long tombstoneId, string prefabName, string appearance, string ownerId, int actionMode, int actionModeSchema, bool follow, bool stayHome, bool autoPickup, bool wander, bool commandable, int formationSlot, string skillsSerialized, bool hasHome, Vector3 homePos, int combatStance, bool hasHomeBed, Vector3 homeBedPos)
	{
		GameObject gameObject = Player.m_localPlayer?.m_tombstone;
		if (gameObject == null)
		{
			gameObject = Game.instance?.m_playerPrefab?.GetComponent<Player>()?.m_tombstone;
		}
		if (gameObject == null)
		{
			gameObject = ZNetScene.instance?.GetPrefab("Player_tombstone");
		}
		if (gameObject == null)
		{
			CompanionsPlugin.Log.LogError("[Setup] Could not find tombstone prefab!");
			return;
		}
		SuppressAutoEquip = true;
		string inventorySerialized = "";
		Inventory inventory;
		if ((object)_humanoid != null && (inventory = _humanoid.GetInventory()) != null)
		{
			int num = MHR_GetDeathItemRule();
			int num2 = 0;
			int num3 = 0;
			List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>(inventory.GetAllItems());
			Inventory inventory2 = null;
			for (int i = 0; i < list.Count; i++)
			{
				ItemDrop.ItemData itemData = list[i];
				int num4 = MHR_DeathItemAction(num, itemData.m_equipped);
				if (num4 == 1)
				{
					continue;
				}
				inventory.RemoveItem(itemData);
				if (num4 == 2)
				{
					num3++;
					continue;
				}
				if (inventory2 == null)
				{
					inventory2 = new Inventory("hc_tomb_keep", null, 8, 4);
				}
				if (!inventory2.AddItem(itemData))
				{
					inventory.AddItem(itemData);
				}
				else
				{
					num2++;
				}
			}
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[Setup] Death items (rule={num}): kept {num2}, deleted {num3}, the rest go to the tombstone");
			}
			if (inventory2 != null && inventory2.NrOfItems() != 0)
			{
				ZPackage zPackage = new ZPackage();
				inventory2.Save(zPackage);
				inventorySerialized = zPackage.GetBase64();
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo($"[Setup] Companion keeps {inventory2.NrOfItems()} item(s) through death/respawn");
				}
			}
		}
		inventory = _humanoid?.GetInventory();
		int num5 = inventory?.NrOfItems() ?? 0;
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo($"[Setup] Creating tombstone for \"{companionName}\" — {num5} items, " + $"tombstoneId={tombstoneId}");
		}
		if (inventory != null && num5 > 0)
		{
			_humanoid.UnequipAllItems();
			int num6 = 0;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem.m_equipped)
				{
					num6++;
				}
				allItem.m_equipped = false;
			}
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup] Unequipped all items for grave transfer (cleared {num6} stale equipped flags)");
			}
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject, position, transform.rotation);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Setup] Tombstone spawned at {position:F1}");
		}
		if (inventory != null && num5 > 0)
		{
			Container component = gameObject2.GetComponent<Container>();
			if (component != null)
			{
				Inventory inventory3 = component.GetInventory();
				if (inventory3 != null)
				{
					inventory3.MoveInventoryToGrave(inventory);
					int num7 = inventory3.NrOfItems();
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogInfo($"[Setup] Grave transfer complete — {num7} items in tombstone");
					}
					int width = inventory3.GetWidth();
					int num8 = 0;
					foreach (ItemDrop.ItemData allItem2 in inventory3.GetAllItems())
					{
						allItem2.m_gridPos = new Vector2i(num8 % width, num8 / width);
						num8++;
					}
				}
			}
		}
		TombStone component2 = gameObject2.GetComponent<TombStone>();
		if (component2 != null)
		{
			component2.Setup(companionName, tombstoneId);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup] Tombstone ownership set — name=\"{companionName}\" id={tombstoneId}");
			}
		}
		ZNetView component3 = gameObject2.GetComponent<ZNetView>();
		if (component3?.GetZDO() != null)
		{
			ZDO zDO = component3.GetZDO();
			zDO.Set(TombstoneIdHash, tombstoneId);
			if (ownerId != null)
			{
				zDO.Set(MHR_TombOwnerHash, ownerId);
			}
			zDO.Set(MHR_TombCompanionIdHash, MHR_EnsureCompanionId());
			Inventory inventory4 = gameObject2.GetComponent<Container>()?.GetInventory();
			if (inventory4 != null)
			{
				zDO.Set(TombInvWidthHash, inventory4.GetWidth());
				zDO.Set(TombInvHeightHash, inventory4.GetHeight());
				zDO.Set(MHR_TombItemsHash, inventory4.NrOfItems());
			}
			CompanionTombstoneResurrect.WriteRespawnData(zDO, prefabName, companionName, appearance, ownerId, actionMode, actionModeSchema, follow, stayHome, autoPickup, wander, commandable, formationSlot, skillsSerialized, hasHome, homePos, combatStance, hasHomeBed, homeBedPos, inventorySerialized);
			zDO.Set(CompanionTombstoneResurrect.RespawnFollowPausedHash, _nview?.GetZDO()?.GetBool(CompanionTombstoneResurrect.FollowPausedByJobHash) == true);
			zDO.Set(MHR_Roster.RespawnGenHash, (_nview?.GetZDO()?.GetInt(MHR_Roster.GenHash)).GetValueOrDefault());
			zDO.Set(CompanionTombstoneResurrect.RespawnFarmZonesHash, _nview?.GetZDO()?.GetString(FarmZoneSerializer.FarmZonesHash) ?? "");
			gameObject2.AddComponent<CompanionTombstoneResurrect>();
			MHR_RespawnTracker.OnCompanionDied(zDO, ownerId);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Setup] Tombstone resurrection data written and component attached");
			}
		}
		if (Minimap.instance != null)
		{
			string text = companionName + " " + ModLocalization.Loc("hc_minimap_grave");
			Minimap.instance.AddPin(position, Minimap.PinType.Death, text, save: true, isChecked: false, 0L);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[Setup] Added tombstone minimap pin at {position:F1}");
			}
		}
		SuppressAutoEquip = false;
	}

	internal int MHR_GetDeathItemRule()
	{
		if (_mhrDespawning)
		{
			return 3;
		}
		int num = ModConfig.DeathItemRule.Value;
		if (!(_nview == null))
		{
			ZDO zDO = _nview.GetZDO();
			if (zDO != null)
			{
				int num2 = zDO.GetInt(DeathItemRuleHash, -1);
				if (num2 >= 0)
				{
					num = num2;
				}
			}
		}
		if (num >= 0 && num <= 3)
		{
			return num;
		}
		return 0;
	}

	internal static int MHR_DeathItemAction(int rule, bool equipped)
	{
		switch (rule)
		{
		case 1:
			return 0;
		case 2:
			if (equipped)
			{
				return 0;
			}
			goto case 3;
		default:
		{
			ZoneSystem instance = ZoneSystem.instance;
			if (!(instance == null))
			{
				if (instance.GetGlobalKey(GlobalKeys.DeathKeepInventory))
				{
					return 0;
				}
				if (equipped)
				{
					if (instance.GetGlobalKey(GlobalKeys.DeathKeepEquip))
					{
						return 0;
					}
					if (instance.GetGlobalKey(GlobalKeys.DeathDeleteItems) && !instance.GetGlobalKey(GlobalKeys.DeathDeleteUnequipped))
					{
						return 2;
					}
				}
				else if (instance.GetGlobalKey(GlobalKeys.DeathDeleteItems) || instance.GetGlobalKey(GlobalKeys.DeathDeleteUnequipped))
				{
					break;
				}
			}
			goto case 3;
		}
		case 3:
			return 1;
		}
		return 2;
	}

	private void OnDestroy()
	{
		_allCompanions.Remove(this);
		if (_dead || _removePinOnDestroy)
		{
			RemoveMinimapPin();
			if (_zdoid != ZDOID.None)
			{
				s_persistentPins.Remove(_zdoid);
			}
		}
		else if (_minimapPin != null && _zdoid != ZDOID.None)
		{
			s_persistentPins[_zdoid] = _minimapPin;
			_minimapPin = null;
		}
		UnregisterInventoryCallback();
		if (_visEquip != null)
		{
			_companionVisEquips.Remove(_visEquip);
		}
	}

	private void QueueEquip(ItemDrop.ItemData item)
	{
		if (item == null)
		{
			return;
		}
		if (Time.time < _instantEquipUntil && _humanoid != null)
		{
			_equipping = true;
			try
			{
				_humanoid.EquipItem(item, triggerEquipEffects: false);
				return;
			}
			finally
			{
				_equipping = false;
			}
		}
		_equipQueue.Add(item);
	}

	internal void ClearEquipQueue()
	{
		if (_equipQueue.Count > 0)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug($"[Setup] Equip queue cleared ({_equipQueue.Count} items dropped)");
			}
			_equipQueue.Clear();
		}
		if (_equipAnimActive)
		{
			if (_zanim != null && CanWearArmor())
			{
				_zanim.SetBool("equipping", value: false);
			}
			_equipAnimActive = false;
		}
		_equipTimer = 0f;
	}

	private void UnequipMissingEquippedItems()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory != null)
		{
			List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
			UnequipIfMissing(allItems, GetEquipSlot(_rightItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_leftItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_chestItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_legItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_helmetItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_shoulderItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_utilityItemField));
			UnequipIfMissing(allItems, GetEquipSlot(_trinketItemField));
		}
	}

	private void NotifyControllersInventoryChanged()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			int num = zDO.GetInt(ActionModeHash);
			FarmController component = GetComponent<FarmController>();
			if (component != null && num == 8)
			{
				component.MHR_NotifyIfIdle();
			}
			HarvestController component2 = GetComponent<HarvestController>();
			if (component2 != null && ((num >= 1 && num <= 3) || num == 5))
			{
				component2.MHR_NotifyIfIdle();
			}
		}
	}

	private static bool IsTwoHandedWeapon(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		if (itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon && itemType != ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft)
		{
			return itemType == ItemDrop.ItemData.ItemType.Bow;
		}
		return true;
	}

	private void EquipBestArmorSlot(FieldInfo slotField, ItemDrop.ItemData bestItem)
	{
		if (_humanoid == null || slotField == null || bestItem == null)
		{
			return;
		}
		ItemDrop.ItemData equipSlot = GetEquipSlot(slotField);
		if (equipSlot == bestItem)
		{
			return;
		}
		if (equipSlot != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Setup] Armor swap " + slotField.Name + ": \"" + (equipSlot.m_shared?.m_name ?? "?") + "\" → \"" + (bestItem.m_shared?.m_name ?? "?") + "\"");
			}
			_humanoid.UnequipItem(equipSlot, triggerEquipEffects: false);
		}
		else if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Setup] Armor queued " + slotField.Name + ": \"" + (bestItem.m_shared?.m_name ?? "?") + "\"");
		}
		QueueEquip(bestItem);
	}

	internal void SetCombatStance(int stance)
	{
		MHR_Recover.Cancel(this);
		_nview?.GetZDO()?.Set(CombatStanceHash, stance);
	}

	internal void SetWander(bool v)
	{
		_nview?.GetZDO()?.Set(WanderHash, v);
	}

	internal void SetIsCommandable(bool value)
	{
		(_nview?.GetZDO())?.Set(IsCommandableHash, value ? 1 : 0);
	}

	internal void SetHomePosition(Vector3 p)
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			zDO.Set(HomePosHash, p);
			zDO.Set(HomePosSetHash, value: true);
			CompanionRest component = GetComponent<CompanionRest>();
			if (component != null)
			{
				component.TryClaimBedIfNeeded();
			}
		}
	}

	internal void SetHomeRadius(float r)
	{
		_nview?.GetZDO()?.Set(HomeRadiusHash, Mathf.Clamp(r, 5f, 200f));
	}

	private void UnequipIfMissing(List<ItemDrop.ItemData> items, ItemDrop.ItemData equipped)
	{
		if (equipped != null && !items.Contains(equipped))
		{
			MHR_Equip.SwapOrUnequip(_humanoid, items, equipped);
		}
	}
}
