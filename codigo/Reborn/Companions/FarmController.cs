using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;

namespace Companions;

public class FarmController : MonoBehaviour
{
	internal enum FarmPhase
	{
		Idle,
		Scanning,
		MovingToPickable,
		Harvesting,
		CollectingDrops,
		MovingToSeedChest,
		TakingSeeds,
		MovingToPlantSpot,
		Planting,
		MovingToOutputChest,
		StoringOutput
	}

	internal struct SeedPlantInfo
	{
		public string SeedPrefabName;

		public GameObject PlantPrefab;

		public float GrowRadius;

		public bool NeedsCultivated;

		public Heightmap.Biome Biome;
	}

	private struct PlantOrder
	{
		public bool AlongZ;

		public float RowSign;
	}

	internal static Dictionary<string, SeedPlantInfo> s_seedToPlant;

	internal static HashSet<string> s_cropOutputNames;

	private static bool s_mappingBuilt;

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private ZSyncAnimation _zanim;

	private CompanionTalk _talk;

	private DoorHandler _doorHandler;

	private HarvestController _harvest;

	private SmeltController _smelt;

	private RepairController _repair;

	private FarmPhase _phase;

	private float _scanTimer;

	private float _actionTimer;

	private float _stuckTimer;

	private bool _moveArrived;

	private bool _chestOpened;

	private bool _allDoneNotified;

	private bool _whyNoZones;

	private bool _whyNoSeeds;

	private bool _whyNoChest;

	private bool _shouldRun;

	private readonly List<CompanionStatus.Item> _farmWhy = new List<CompanionStatus.Item>();

	private bool _preferHarvest = true;

	private float _rotationTimer;

	private const float RotationInterval = 30f;

	private static readonly HashSet<int> s_claimedPickables = new HashSet<int>();

	private int _claimedPickableId;

	private List<FarmZone> _farmZones;

	private float _zoneRefreshTimer;

	private int _activeZoneIndex = -1;

	private Pickable _targetPickable;

	private Container _targetChest;

	private Container _outputChest;

	private Vector3 _plantPosition;

	private SeedPlantInfo _plantInfo;

	private string _seedPrefabToTake;

	private int _seedAmountToTake;

	private readonly Collider[] _spacingBuffer = new Collider[64];

	private readonly Collider[] _dropBuffer = new Collider[64];

	private readonly Collider[] _pickableScanBuffer = new Collider[512];

	private readonly List<Piece> _tempPieces = new List<Piece>();

	private readonly List<Container> _nearbyChests = new List<Container>();

	private int _spaceMask;

	private int _roofMask;

	private int _itemMask;

	private int _pickableMask;

	private const float MoveTimeout = 10f;

	private const float DropPickupDelay = 0.4f;

	private const float PostHarvestCooldown = 2f;

	private static bool s_mhrCultBroken;

	private List<FarmZone> _tilled;

	private float _nextTilledScan;

	private readonly HashSet<int> _unreachableChests = new HashSet<int>();

	private float _unreachableClearAt;

	private readonly Dictionary<string, PlantOrder> _plantOrders = new Dictionary<string, PlantOrder>();

	private Container _approachChest;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	private Vector3 _stuckLeg;

	private readonly List<KeyValuePair<float, Vector3>> _plantCandidates = new List<KeyValuePair<float, Vector3>>();

	private readonly Dictionary<Vector3Int, float> _unreachable = new Dictionary<Vector3Int, float>();

	private const int MaxReachChecks = 8;

	internal FarmPhase Phase => _phase;

	public bool IsActive => _phase != FarmPhase.Idle;

	private static float ScanRadius => ModConfig.FarmScanRadius.Value;

	private static float ScanInterval => ModConfig.FarmScanInterval.Value;

	private static float PlantSpacing => ModConfig.FarmPlantSpacing.Value;

	private static float UseDistance => ModConfig.FarmUseDistance.Value;

	private void FarmWhy(string key, params object[] args)
	{
		string text = key + "|" + string.Join("|", args);
		foreach (CompanionStatus.Item item in _farmWhy)
		{
			if (item.Key + "|" + string.Join("|", item.Args) == text)
			{
				return;
			}
		}
		if (_farmWhy.Count < 8)
		{
			_farmWhy.Add(new CompanionStatus.Item(null, key, args));
		}
	}

	internal string DoingText()
	{
		switch (_phase)
		{
		case FarmPhase.MovingToPickable:
		case FarmPhase.Harvesting:
			return ModLocalization.Loc("hc_status_farm_harvesting");
		case FarmPhase.CollectingDrops:
			return ModLocalization.Loc("hc_status_farm_pickup");
		case FarmPhase.MovingToSeedChest:
		case FarmPhase.TakingSeeds:
			return ModLocalization.Loc("hc_status_farm_seeds");
		case FarmPhase.MovingToPlantSpot:
		case FarmPhase.Planting:
		{
			string seedPrefabName = _plantInfo.SeedPrefabName;
			string text = ObjectDB.instance?.GetItemPrefab(seedPrefabName ?? "")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
			text = (string.IsNullOrEmpty(text) ? seedPrefabName : (Localization.instance?.Localize(text) ?? text));
			return ModLocalization.LocFmt("hc_status_farm_planting", text ?? "");
		}
		case FarmPhase.MovingToOutputChest:
		case FarmPhase.StoringOutput:
			return ModLocalization.Loc("hc_status_farm_storing");
		default:
			return null;
		}
	}

	public void NotifyActionModeChanged()
	{
		if (IsFarmMode())
		{
			_scanTimer = 0f;
			_allDoneNotified = false;
			_preferHarvest = true;
			_rotationTimer = 0f;
			Log("Mode changed to Farm — scan timer reset, rotation=HARVEST");
		}
	}

	internal void MHR_NotifyIfIdle()
	{
		if (_phase <= FarmPhase.Scanning)
		{
			NotifyActionModeChanged();
		}
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_setup = GetComponent<CompanionSetup>();
		_nview = GetComponent<ZNetView>();
		_zanim = GetComponent<ZSyncAnimation>();
		_talk = GetComponent<CompanionTalk>();
		_doorHandler = GetComponent<DoorHandler>();
		_harvest = GetComponent<HarvestController>();
		_smelt = GetComponent<SmeltController>();
		_repair = GetComponent<RepairController>();
		_spaceMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
		_roofMask = LayerMask.GetMask("Default", "static_solid", "piece");
		_itemMask = LayerMask.GetMask("item");
		_pickableMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
	}

	private void OnDestroy()
	{
		UnclaimPickable();
		CloseAnyOpenChest();
	}

	private void UnclaimPickable()
	{
		if (_claimedPickableId != 0)
		{
			s_claimedPickables.Remove(_claimedPickableId);
			_claimedPickableId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			CloseAnyOpenChest();
		}
		else if (MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			CloseAnyOpenChest();
		}
		else if (ShouldAbort())
		{
			if (_phase != FarmPhase.Idle)
			{
				Abort("interrupted");
			}
		}
		else if (!IsFarmMode())
		{
			if (_phase != FarmPhase.Idle)
			{
				Abort("left farm mode");
			}
		}
		else if ((!(_harvest != null) || !_harvest.IsActive) && (!(_smelt != null) || !_smelt.IsActive) && (!(_repair != null) || !_repair.IsActive))
		{
			float deltaTime = Time.deltaTime;
			_zoneRefreshTimer -= deltaTime;
			if (_zoneRefreshTimer <= 0f || _farmZones == null)
			{
				RefreshFarmZones();
				_zoneRefreshTimer = 5f;
			}
			switch (_phase)
			{
			case FarmPhase.Idle:
				UpdateIdle(deltaTime);
				break;
			case FarmPhase.Scanning:
				UpdateScanning();
				break;
			case FarmPhase.MovingToPickable:
				UpdateMovingToPickable(deltaTime);
				break;
			case FarmPhase.Harvesting:
				UpdateHarvesting();
				break;
			case FarmPhase.CollectingDrops:
				UpdateCollectingDrops();
				break;
			case FarmPhase.MovingToSeedChest:
				UpdateMovingToSeedChest(deltaTime);
				break;
			case FarmPhase.TakingSeeds:
				UpdateTakingSeeds();
				break;
			case FarmPhase.MovingToPlantSpot:
				UpdateMovingToPlantSpot(deltaTime);
				break;
			case FarmPhase.Planting:
				UpdatePlanting();
				break;
			case FarmPhase.MovingToOutputChest:
				UpdateMovingToOutputChest(deltaTime);
				break;
			case FarmPhase.StoringOutput:
				UpdateStoringOutput();
				break;
			}
		}
	}

	private bool ShouldAbort()
	{
		if (_ai != null && _ai.IsInCombat)
		{
			return true;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			return true;
		}
		return false;
	}

	private void Abort(string reason)
	{
		Log($"Aborted — {reason} (phase was {_phase})");
		UnclaimPickable();
		CloseAnyOpenChest();
		RestoreCombatLoadout();
		_targetPickable = null;
		_targetChest = null;
		_outputChest = null;
		_allDoneNotified = false;
		_phase = FarmPhase.Idle;
		_scanTimer = ScanInterval;
		RestoreFollow();
	}

	internal bool IsFarmMode()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO == null)
		{
			return false;
		}
		return zDO.GetInt(CompanionSetup.ActionModeHash) == 8;
	}

	private void RefreshFarmZones()
	{
		ZDO zdo = _nview?.GetZDO();
		_farmZones = FarmZoneSerializer.Load(zdo);
		if (ModConfig.FarmPlantTilled == null || ModConfig.FarmPlantTilled.Value)
		{
			if (_tilled == null || Time.time >= _nextTilledScan)
			{
				_nextTilledScan = Time.time + 30f;
				_tilled = FindTilledPatches(_farmZones);
			}
			_farmZones.AddRange(_tilled);
		}
	}

	private List<FarmZone> FindTilledPatches(List<FarmZone> real)
	{
		List<FarmZone> list = new List<FarmZone>();
		Vector3 position = transform.position;
		Vector3 vector = ((_setup != null && _setup.HasHomePosition() && Vector3.Distance(_setup.GetHomePosition(), position) <= ScanRadius) ? _setup.GetHomePosition() : position);
		int num = Mathf.CeilToInt(ScanRadius / 2f);
		int num2 = num * 2 + 1;
		bool[,] array = new bool[num2, num2];
		int num3 = 0;
		for (int i = 0; i < num2; i++)
		{
			for (int j = 0; j < num2; j++)
			{
				float num4 = (float)(i - num) * 2f;
				float num5 = (float)(j - num) * 2f;
				if (num4 * num4 + num5 * num5 > ScanRadius * ScanRadius)
				{
					continue;
				}
				Vector3 vector2 = new Vector3(vector.x + num4, vector.y, vector.z + num5);
				if (ZoneSystem.instance.GetGroundHeight(vector2, out var height))
				{
					vector2.y = height;
					if (MHR_SafeCultivated(vector2) && !InRealZone(real, vector2))
					{
						array[i, j] = true;
						num3++;
					}
				}
			}
		}
		if (num3 == 0)
		{
			return list;
		}
		bool[,] array2 = new bool[num2, num2];
		Stack<Vector2Int> stack = new Stack<Vector2Int>();
		for (int k = 0; k < num2; k++)
		{
			for (int l = 0; l < num2; l++)
			{
				if (!array[k, l] || array2[k, l])
				{
					continue;
				}
				int num6 = k;
				int num7 = k;
				int num8 = l;
				int num9 = l;
				array2[k, l] = true;
				stack.Push(new Vector2Int(k, l));
				while (stack.Count > 0)
				{
					Vector2Int vector2Int = stack.Pop();
					num6 = Mathf.Min(num6, vector2Int.x);
					num7 = Mathf.Max(num7, vector2Int.x);
					num8 = Mathf.Min(num8, vector2Int.y);
					num9 = Mathf.Max(num9, vector2Int.y);
					for (int m = 0; m < 4; m++)
					{
						int num10 = vector2Int.x + m switch
						{
							1 => -1, 
							0 => 1, 
							_ => 0, 
						};
						int num11 = vector2Int.y + m switch
						{
							3 => -1, 
							2 => 1, 
							_ => 0, 
						};
						if (num10 >= 0 && num11 >= 0 && num10 < num2 && num11 < num2 && array[num10, num11] && !array2[num10, num11])
						{
							array2[num10, num11] = true;
							stack.Push(new Vector2Int(num10, num11));
						}
					}
				}
				Vector3 vector3 = new Vector3(vector.x + ((float)(num6 + num7) * 0.5f - (float)num) * 2f, vector.y, vector.z + ((float)(num8 + num9) * 0.5f - (float)num) * 2f);
				if (ZoneSystem.instance.GetGroundHeight(vector3, out var height2))
				{
					vector3.y = height2;
				}
				float halfSize = (float)Mathf.Max(num7 - num6, num9 - num8) * 2f * 0.5f + 1.5f;
				list.Add(new FarmZone
				{
					Center = vector3,
					HalfSize = halfSize,
					CropSeed = "",
					Rotation = 0f,
					Tilled = true
				});
			}
		}
		Log($"[Plant] Tilled ground: {num3} cell(s) in {list.Count} patch(es) around {vector:F0}");
		return list;
	}

	private static bool InRealZone(List<FarmZone> zones, Vector3 p)
	{
		if (zones == null)
		{
			return false;
		}
		foreach (FarmZone zone in zones)
		{
			if (zone.IsValid && !zone.Tilled && zone.Contains(p))
			{
				return true;
			}
		}
		return false;
	}

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (!(_scanTimer > 0f))
		{
			_scanTimer = ScanInterval;
			_rotationTimer += ScanInterval;
			if (_rotationTimer >= 30f)
			{
				_rotationTimer = 0f;
				_preferHarvest = !_preferHarvest;
				Log("Rotation switch → " + (_preferHarvest ? "HARVEST" : "PLANT") + " priority");
			}
			_phase = FarmPhase.Scanning;
		}
	}

	private void UpdateScanning()
	{
		BuildSeedMapping();
		Log("Scanning — mode=" + (_preferHarvest ? "HARVEST" : "PLANT") + " " + $"rotTimer={_rotationTimer:F0}s cropNames={s_cropOutputNames?.Count ?? 0} " + $"seedMap={s_seedToPlant?.Count ?? 0} pos={transform.position:F1}");
		_whyNoZones = (_whyNoSeeds = (_whyNoChest = false));
		_farmWhy.Clear();
		if (PackNearlyFull() && TryScanStore())
		{
			CompanionStatus.Clear(this);
			return;
		}
		if (_preferHarvest)
		{
			if (TryScanHarvest() || TryScanStore() || TryScanPlant())
			{
				CompanionStatus.Clear(this);
				return;
			}
		}
		else if (TryScanStore() || TryScanPlant() || TryScanHarvest())
		{
			CompanionStatus.Clear(this);
			return;
		}
		if (_whyNoChest)
		{
			CompanionStatus.Why(this, "hc_why_farm_no_chest", true);
		}
		else
		{
			if (_whyNoSeeds)
			{
				_talk?.SayProblem("hc_speech_farm_no_seeds", ModLocalization.Loc("hc_speech_farm_no_seeds"));
				FarmWhy("hc_speech_farm_no_seeds");
			}
			if (_whyNoZones)
			{
				FarmWhy("hc_why_farm_no_zones");
			}
			if (_farmWhy.Count == 0)
			{
				FarmWhy("hc_why_farm_waiting");
			}
			CompanionStatus.WhyRotate(this, _farmWhy);
		}
		if (!_allDoneNotified)
		{
			_allDoneNotified = true;
			Log("Nothing to farm — idle (30s backoff)");
			if (_talk != null)
			{
				_talk.Speak(ModLocalization.Loc("hc_speech_farm_done"), CompanionTalk.Priority.Event, "farm:done", 600f);
			}
			RestoreFollow();
		}
		_phase = FarmPhase.Idle;
		_scanTimer = ((_farmZones != null && _farmZones.Count > 0) ? 5f : 30f);
	}

	private void UpdateMovingToPickable(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetPickable == null || IsPicked(_targetPickable))
		{
			Abort("pickable gone or already picked");
			return;
		}
		float num = MHR_Nav.ReachDist(transform.position, _targetPickable.transform.position);
		if (num < UseDistance || _moveArrived)
		{
			_moveArrived = false;
			_ai.StopMoving();
			_ai.LookAtPoint(_targetPickable.transform.position);
			_phase = FarmPhase.Harvesting;
			_actionTimer = 0f;
			Log($"At pickable (dist={num:F1}m) — harvesting");
		}
		else
		{
			_shouldRun = num > 8f;
			LogMovement(dt, "pickable", num, _moveArrived = MHR_Nav.MoveFarm(_ai, dt, _targetPickable.transform.position, UseDistance, _shouldRun));
			UpdateStuckDetection(dt, "pickable");
		}
	}

	private void UpdateHarvesting()
	{
		if (_targetPickable == null)
		{
			if (_actionTimer != 0f)
			{
				_phase = FarmPhase.CollectingDrops;
				_actionTimer = 0.5f;
			}
			else
			{
				_phase = FarmPhase.Scanning;
			}
			return;
		}
		if (_actionTimer == 0f)
		{
			_targetPickable.Interact(_humanoid, repeat: false, alt: false);
			_actionTimer = 1.5f;
			Log("Interacted with \"" + _targetPickable.name + "\"");
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (!(_actionTimer > 0f))
		{
			_phase = FarmPhase.CollectingDrops;
			_actionTimer = 0.5f;
		}
	}

	private void UpdateCollectingDrops()
	{
		_actionTimer -= Time.deltaTime;
		if (_actionTimer > 0f)
		{
			return;
		}
		Vector3 pos = ((_targetPickable != null) ? _targetPickable.transform.position : transform.position);
		if (PickUpOneDrop(pos))
		{
			_actionTimer = 0.4f;
			return;
		}
		_targetPickable = null;
		UnclaimPickable();
		if (!TryNextCropNearby())
		{
			_phase = FarmPhase.Idle;
			_scanTimer = 2f;
			Log($"Drops collected — idle for {2f}s before next scan");
		}
	}

	private void UpdateMovingToSeedChest(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetChest == null)
		{
			Abort("seed chest destroyed");
			return;
		}
		float num = MHR_Nav.ReachDist(transform.position, _targetChest.transform.position);
		if (num < UseDistance || AtChestSpot(_targetChest, num))
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_targetChest.transform.position);
			_chestOpened = false;
			_phase = FarmPhase.TakingSeeds;
			Log($"At seed chest (dist={num:F1}m) — taking seeds");
		}
		else
		{
			_shouldRun = num > 8f;
			Vector3 point = ChestGoal(_targetChest);
			bool moveOk = MHR_Nav.MoveFarm(_ai, dt, point, _hasApproach ? 0.5f : UseDistance, _shouldRun);
			LogMovement(dt, "seed chest", num, moveOk);
			UpdateStuckDetection(dt, "seed chest");
		}
	}

	private void UpdateTakingSeeds()
	{
		if (_targetChest == null)
		{
			if (_chestOpened)
			{
				_chestOpened = false;
			}
			Abort("chest destroyed while taking seeds");
			return;
		}
		if (!_chestOpened && !MHR_Chests.Open(_targetChest))
		{
			Abort("someone has the chest open");
			return;
		}
		if (!_chestOpened)
		{
			if (_zanim != null)
			{
				_zanim.SetTrigger("interact");
			}
			_chestOpened = true;
			_actionTimer = 0.8f;
			Log("Opened seed chest — waiting for animation");
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (_actionTimer > 0f)
		{
			return;
		}
		Inventory inventory = _targetChest.GetInventory();
		Inventory inventory2 = _humanoid?.GetInventory();
		if (inventory == null || inventory2 == null)
		{
			_targetChest.SetInUse(inUse: false);
			_chestOpened = false;
			Abort("inventory null");
			return;
		}
		int num = 0;
		List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
		int num2 = allItems.Count - 1;
		while (num2 >= 0 && num < _seedAmountToTake)
		{
			ItemDrop.ItemData itemData = allItems[num2];
			if (!(itemData?.m_dropPrefab == null) && !(itemData.m_dropPrefab.name != _seedPrefabToTake))
			{
				int num3 = Mathf.Min(itemData.m_stack, _seedAmountToTake - num);
				while (num3 > 0 && !inventory2.CanAddItem(itemData, num3))
				{
					num3--;
				}
				if (num3 <= 0)
				{
					Log($"  No inventory space — stopping at {num} seeds");
					break;
				}
				inventory.RemoveItem(itemData, num3);
				inventory2.AddItem(itemData.m_dropPrefab.name, num3, itemData.m_quality, itemData.m_variant, 0L, "", cheated: false);
				num += num3;
				Log($"  Took {num3}x \"{_seedPrefabToTake}\" (total={num})");
			}
			num2--;
		}
		MHR_SeedRules.TakeExtras(this, inventory, inventory2);
		_targetChest.SetInUse(inUse: false);
		_chestOpened = false;
		if (num == 0)
		{
			Log("Failed to take seeds — back to scanning");
			_phase = FarmPhase.Scanning;
		}
		else
		{
			_phase = FarmPhase.Scanning;
			_scanTimer = 0f;
		}
	}

	private void UpdateMovingToPlantSpot(float dt)
	{
		if (!(_doorHandler != null) || !_doorHandler.IsActive)
		{
			float num = Vector3.Distance(transform.position, _plantPosition);
			if (num < UseDistance || _moveArrived)
			{
				_moveArrived = false;
				_ai.StopMoving();
				_ai.LookAtPoint(_plantPosition);
				_phase = FarmPhase.Planting;
				_actionTimer = 0f;
				Log($"At plant spot (dist={num:F1}m) — planting");
			}
			else
			{
				_shouldRun = num > 8f;
				LogMovement(dt, "plant spot", num, _moveArrived = MHR_Nav.MovePlant(_ai, dt, _plantPosition, UseDistance, _shouldRun));
				UpdateStuckDetection(dt, "plant spot");
			}
		}
	}

	private void UpdatePlanting()
	{
		if (_actionTimer == 0f)
		{
			ItemDrop.ItemData itemData = FindCultivator();
			if (itemData == null)
			{
				Log("Lost cultivator during plant — aborting");
				_phase = FarmPhase.Scanning;
				return;
			}
			EquipCultivator(itemData);
			if (_zanim != null)
			{
				_zanim.SetTrigger("attack");
			}
			_actionTimer = 0.8f;
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (_actionTimer > 0f)
		{
			return;
		}
		PlantSeed(_plantInfo, _plantPosition);
		Inventory inventory = _humanoid?.GetInventory();
		int num = ((inventory != null) ? CountItemByPrefab(inventory, _plantInfo.SeedPrefabName) : 0);
		Log($"Post-plant: seed=\"{_plantInfo.SeedPrefabName}\" remaining={num} " + $"activeZone={_activeZoneIndex} zones={_farmZones?.Count ?? 0}");
		if (!MHR_SeedRules.GroupDone(this, _plantInfo.SeedPrefabName) && num > 0 && _activeZoneIndex >= 0 && _farmZones != null && _activeZoneIndex < _farmZones.Count)
		{
			FarmZone zone = _farmZones[_activeZoneIndex];
			if (zone.IsValid && FindPlantPositionInZone(_plantInfo, zone, out var result))
			{
				_plantPosition = result;
				ResetStuck();
				_phase = FarmPhase.MovingToPlantSpot;
				Log($"More seeds — next plant spot at {result:F1} in zone[{_activeZoneIndex}] {zone.Center:F0}");
				return;
			}
			Log($"Zone[{_activeZoneIndex}] full or no positions — will re-scan all zones");
		}
		_activeZoneIndex = -1;
		_phase = FarmPhase.Scanning;
		_scanTimer = 0f;
		Log("Transitioning to Scanning for next zone");
	}

	private void UpdateMovingToOutputChest(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_outputChest == null)
		{
			_phase = FarmPhase.Scanning;
			return;
		}
		float num = MHR_Nav.ReachDist(transform.position, _outputChest.transform.position);
		if (num < UseDistance || AtChestSpot(_outputChest, num))
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_outputChest.transform.position);
			_chestOpened = false;
			_phase = FarmPhase.StoringOutput;
			Log($"At output chest (dist={num:F1}m) — storing crops");
		}
		else
		{
			_shouldRun = num > 8f;
			Vector3 point = ChestGoal(_outputChest);
			bool moveOk = MHR_Nav.MoveFarm(_ai, dt, point, _hasApproach ? 0.5f : UseDistance, _shouldRun);
			LogMovement(dt, "output chest", num, moveOk);
			UpdateStuckDetection(dt, "output chest");
		}
	}

	private void UpdateStoringOutput()
	{
		if (_outputChest == null)
		{
			if (_chestOpened)
			{
				_chestOpened = false;
			}
			_phase = FarmPhase.Scanning;
			return;
		}
		if (!_chestOpened && !MHR_Chests.Open(_outputChest))
		{
			_phase = FarmPhase.Scanning;
			return;
		}
		if (!_chestOpened)
		{
			if (_zanim != null)
			{
				_zanim.SetTrigger("interact");
			}
			_chestOpened = true;
			_actionTimer = 0.8f;
			Log("Opened output chest");
			return;
		}
		_actionTimer -= Time.deltaTime;
		if (!(_actionTimer > 0f))
		{
			Inventory inventory = _humanoid?.GetInventory();
			Inventory inventory2 = _outputChest.GetInventory();
			if (inventory == null || inventory2 == null)
			{
				_outputChest.SetInUse(inUse: false);
				_chestOpened = false;
				_phase = FarmPhase.Scanning;
				return;
			}
			int num = MHR_SeedRules.StoreOutput(this, inventory, inventory2);
			_outputChest.SetInUse(inUse: false);
			_chestOpened = false;
			Log($"Stored {num} items in output chest");
			_phase = FarmPhase.Scanning;
			_scanTimer = 0f;
		}
	}

	private void LateUpdate()
	{
		if (!(_character == null) && (_phase == FarmPhase.MovingToPickable || _phase == FarmPhase.MovingToSeedChest || _phase == FarmPhase.MovingToPlantSpot || _phase == FarmPhase.MovingToOutputChest || _phase == FarmPhase.CollectingDrops))
		{
			_character.SetRun(_shouldRun);
		}
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		MHR_FarmLog(CompanionsPlugin.Log, "[Farm|" + text + "] " + msg);
	}

	private static void BuildSeedMapping()
	{
		if (s_mappingBuilt)
		{
			return;
		}
		s_mappingBuilt = true;
		s_seedToPlant = new Dictionary<string, SeedPlantInfo>();
		s_cropOutputNames = new HashSet<string>();
		if (ZNetScene.instance == null)
		{
			return;
		}
		foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
		{
			if (prefab == null)
			{
				continue;
			}
			Plant component = prefab.GetComponent<Plant>();
			Piece component2 = prefab.GetComponent<Piece>();
			if (component == null || component2 == null || component2.m_resources == null || component2.m_resources.Length == 0 || component2.m_resources[0].m_resItem == null)
			{
				continue;
			}
			string text = component2.m_resources[0].m_resItem.gameObject.name;
			if (s_seedToPlant.ContainsKey(text))
			{
				continue;
			}
			s_seedToPlant[text] = new SeedPlantInfo
			{
				SeedPrefabName = text,
				PlantPrefab = prefab,
				GrowRadius = component.m_growRadius,
				NeedsCultivated = component.m_needCultivatedGround,
				Biome = component.m_biome
			};
			if (component.m_grownPrefabs == null)
			{
				continue;
			}
			GameObject[] grownPrefabs = component.m_grownPrefabs;
			foreach (GameObject gameObject in grownPrefabs)
			{
				if (!(gameObject == null))
				{
					Pickable component3 = gameObject.GetComponent<Pickable>();
					if (component3?.m_itemPrefab != null)
					{
						s_cropOutputNames.Add(component3.m_itemPrefab.name);
					}
				}
			}
		}
	}

	private bool TryScanStore()
	{
		if (HasCropOutput())
		{
			ScanNearbyChests();
			Container container = null;
			for (int i = 0; i < 12; i++)
			{
				container = FindChestWithSpace();
				if (container == null || ChestReachable(container))
				{
					break;
				}
				container = null;
			}
			if (container != null)
			{
				_outputChest = container;
				ClearFollowForMovement();
				ResetStuck();
				_phase = FarmPhase.MovingToOutputChest;
				Log($"[Store] Have crop output — storing in chest at {container.transform.position:F1}");
				return true;
			}
			Log("[Store] Have crop output but no chest with space found");
			_whyNoChest = true;
		}
		else
		{
			Log("[Store] No crop output in inventory");
		}
		return false;
	}

	private bool TryScanHarvest()
	{
		Pickable pickable = FindRipePickable();
		if (pickable != null)
		{
			_targetPickable = pickable;
			ClaimPickable();
			ClearFollowForMovement();
			ResetStuck();
			_phase = FarmPhase.MovingToPickable;
			if (_talk != null)
			{
				_talk.Speak(ModLocalization.Loc("hc_speech_farm_harvesting"), CompanionTalk.Priority.Event, "farm:harvest");
			}
			Log("[Harvest] Found ripe crop \"" + pickable.name + "\" at " + $"{pickable.transform.position:F1} dist={Vector3.Distance(transform.position, pickable.transform.position):F1}m");
			return true;
		}
		Log("[Harvest] No ripe crops found");
		return false;
	}

	private bool TryScanPlant()
	{
		ItemDrop.ItemData itemData = FindCultivator();
		if (itemData == null)
		{
			if (!_allDoneNotified)
			{
				Log("No cultivator in inventory — harvest-only mode");
				if (_talk != null)
				{
					_talk.SayProblem("tool:cultivator", ModLocalization.Loc("hc_speech_farm_no_cultivator"));
				}
			}
			return false;
		}
		if (_farmZones == null || _farmZones.Count == 0)
		{
			Log("[Plant] No farm zones defined — skipping planting");
			_whyNoZones = true;
			return false;
		}
		Log($"[Plant] TryScanPlant: {_farmZones.Count} zones, pos={transform.position:F1}");
		ScanNearbyChests();
		int num = -1;
		float num2 = float.MaxValue;
		for (int i = 0; i < _farmZones.Count; i++)
		{
			if (_farmZones[i].IsValid)
			{
				float num3 = Vector3.Distance(transform.position, _farmZones[i].Center);
				if (num3 < num2)
				{
					num2 = num3;
					num = i;
				}
			}
		}
		if (num < 0)
		{
			Log("[Plant] No valid zones found");
			return false;
		}
		for (int j = 0; j < _farmZones.Count; j++)
		{
			int num4 = (num + j) % _farmZones.Count;
			FarmZone zone = _farmZones[num4];
			if (!zone.IsValid)
			{
				continue;
			}
			string cropSeed = zone.CropSeed;
			bool flag = string.IsNullOrEmpty(cropSeed);
			cropSeed = MHR_SeedRules.PickSeed(this, cropSeed);
			if (string.IsNullOrEmpty(cropSeed))
			{
				Log($"[Plant] Zone[{num4}] nothing to plant here right now");
				FarmWhy("hc_why_farm_seed_min", (ModConfig.SeedReserve?.Value ?? 20).ToString());
				continue;
			}
			flag = false;
			float num5 = Vector3.Distance(transform.position, zone.Center);
			Log($"[Plant] Zone[{num4}] center={zone.Center:F1} hs={zone.HalfSize:F0} " + string.Format("crop=\"{0}\" dist={1:F0}m", flag ? "Any" : cropSeed, num5));
			SeedPlantInfo? seedPlantInfo = (flag ? FindPlantableSeedInInventory(itemData) : FindSpecificSeedInInventory(cropSeed));
			if (seedPlantInfo.HasValue)
			{
				Log($"[Plant] Zone[{num4}] has seed \"{seedPlantInfo.Value.SeedPrefabName}\" in inventory");
				if (FindPlantPositionInZone(seedPlantInfo.Value, zone, out var result))
				{
					_plantInfo = seedPlantInfo.Value;
					_plantPosition = result;
					_activeZoneIndex = num4;
					ClearFollowForMovement();
					ResetStuck();
					_phase = FarmPhase.MovingToPlantSpot;
					if (_talk != null)
					{
						_talk.Speak(ModLocalization.Loc("hc_speech_farm_planting"), CompanionTalk.Priority.Event, "farm:plant");
					}
					Log($"[Zone] Planting \"{seedPlantInfo.Value.SeedPrefabName}\" at {result:F1} in zone[{num4}] {zone.Center:F0}");
					return true;
				}
				Log($"[Plant] Zone[{num4}] no valid plant position (zone full or uncultivated)");
				FarmWhy("hc_why_farm_zone_full");
			}
			else
			{
				Log($"[Plant] Zone[{num4}] no seed \"{cropSeed}\" in inventory");
			}
			Vector3 result2;
			if (flag)
			{
				Container container = null;
				SeedPlantInfo seedInfo = default;
				string seedPrefab = null;
				int seedCount = 0;
				for (int k = 0; k < 12; k++)
				{
					container = FindChestWithSeeds(itemData, out seedInfo, out seedPrefab, out seedCount);
					if (container == null || ChestReachable(container))
					{
						break;
					}
					container = null;
				}
				if (container != null)
				{
					Log($"[Plant] Zone[{num4}] found {seedCount}x \"{seedPrefab}\" in chest");
					if (FindPlantPositionInZone(seedInfo, zone, out result2))
					{
						_targetChest = container;
						_seedPrefabToTake = seedPrefab;
						_seedAmountToTake = Mathf.Min(seedCount, MHR_SeedRules.Carry());
						_activeZoneIndex = num4;
						ClearFollowForMovement();
						ResetStuck();
						_phase = FarmPhase.MovingToSeedChest;
						Log($"[Zone] Fetching {_seedAmountToTake}x \"{seedPrefab}\" for Any zone[{num4}] {zone.Center:F0}");
						return true;
					}
					Log($"[Plant] Zone[{num4}] chest seeds found but no plant position in zone");
				}
				else
				{
					Log($"[Plant] Zone[{num4}] no seeds in chests either");
					_whyNoSeeds = true;
				}
				continue;
			}
			Container container2 = null;
			SeedPlantInfo seedInfo2 = default;
			int seedCount2 = 0;
			for (int l = 0; l < 12; l++)
			{
				container2 = FindChestWithSpecificSeed(cropSeed, out seedInfo2, out seedCount2);
				if (container2 == null || ChestReachable(container2))
				{
					break;
				}
				container2 = null;
			}
			if (container2 != null)
			{
				Log($"[Plant] Zone[{num4}] found {seedCount2}x \"{cropSeed}\" in chest");
				if (FindPlantPositionInZone(seedInfo2, zone, out result2))
				{
					_targetChest = container2;
					_seedPrefabToTake = cropSeed;
					_seedAmountToTake = Mathf.Min(seedCount2, MHR_SeedRules.Carry());
					_activeZoneIndex = num4;
					ClearFollowForMovement();
					ResetStuck();
					_phase = FarmPhase.MovingToSeedChest;
					Log($"[Zone] Fetching {_seedAmountToTake}x \"{cropSeed}\" for zone[{num4}] {zone.Center:F0}");
					return true;
				}
				Log($"[Plant] Zone[{num4}] chest seeds found but no plant position in zone");
				FarmWhy("hc_why_farm_zone_full");
			}
			else
			{
				Log($"[Plant] Zone[{num4}] no \"{cropSeed}\" in chests either");
				FarmWhy("hc_why_farm_no_seed", CookController.Name(cropSeed));
			}
		}
		Log("[Plant] All zones checked — no planting possible");
		return false;
	}

	private void RestoreFollow()
	{
		if (!(_ai == null) && _setup != null && _setup.GetFollow() && MHR_Owner.Of(this) != null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
			Log("Restored follow target to player");
		}
	}

	private Pickable FindRipePickable()
	{
		return FindRipePickable(transform.position, ScanRadius, summary: true);
	}

	private Pickable FindRipePickable(Vector3 center, float radius, bool summary)
	{
		int num = Physics.OverlapSphereNonAlloc(center, radius, _pickableScanBuffer, _pickableMask);
		Pickable pickable = null;
		float num2 = float.MaxValue;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		for (int i = 0; i < num; i++)
		{
			Pickable pickable2 = _pickableScanBuffer[i].GetComponent<Pickable>();
			if (pickable2 == null)
			{
				pickable2 = _pickableScanBuffer[i].GetComponentInParent<Pickable>();
			}
			if (pickable2 == null)
			{
				continue;
			}
			num3++;
			if (IsPicked(pickable2) || s_claimedPickables.Contains(pickable2.GetInstanceID()))
			{
				num7++;
				continue;
			}
			bool flag = false;
			string item = ((pickable2.m_itemPrefab != null) ? pickable2.m_itemPrefab.name : "null");
			if (s_cropOutputNames != null && pickable2.m_itemPrefab != null && s_cropOutputNames.Contains(item))
			{
				flag = true;
			}
			if (!flag && MHR_SafeCultivated(pickable2.transform.position))
			{
				flag = true;
			}
			if (!flag)
			{
				num5++;
				continue;
			}
			num4++;
			if (_ai != null && _ai.IsPositionBlacklisted(pickable2.transform.position))
			{
				num6++;
				continue;
			}
			float num8 = Vector3.Distance(center, pickable2.transform.position);
			if (num8 < num2)
			{
				pickable = pickable2;
				num2 = num8;
			}
		}
		if (summary)
		{
			Log($"FindRipePickable: {num} colliders in {radius:F0}m, {num3} pickables, {num4} crops " + $"({num7} picked/claimed, {num5} not crops, {num6} unreachable), " + "best=\"" + (pickable?.name ?? "none") + "\"" + ((pickable != null) ? $" at {num2:F1}m" : ""));
		}
		return pickable;
	}

	private bool TryNextCropNearby()
	{
		float value = ModConfig.FarmNextCropRange.Value;
		if (value <= 0f || PackNearlyFull())
		{
			return false;
		}
		Pickable pickable = FindRipePickable(transform.position, value, summary: false);
		if (pickable == null)
		{
			return false;
		}
		_targetPickable = pickable;
		ClaimPickable();
		ResetStuck();
		_phase = FarmPhase.MovingToPickable;
		Log($"[Harvest] Next crop nearby \"{pickable.name}\" dist={Vector3.Distance(transform.position, pickable.transform.position):F1}m");
		return true;
	}

	private void ClaimPickable()
	{
		UnclaimPickable();
		_claimedPickableId = _targetPickable?.GetInstanceID() ?? 0;
		if (_claimedPickableId != 0)
		{
			s_claimedPickables.Add(_claimedPickableId);
		}
	}

	private void ClearFollowForMovement()
	{
		_allDoneNotified = false;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
			_ai.StopMoving();
			Log("Cleared follow target + stopped AI — FarmController driving movement");
		}
	}

	private void ResetStuck()
	{
		_stuckTimer = 0f;
		_moveArrived = false;
	}

	private bool PackNearlyFull()
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		if (inventory.GetEmptySlots() <= 1)
		{
			return true;
		}
		return inventory.GetTotalWeight() >= ModConfig.HarvestOverweightThreshold.Value + CompanionTierData.CarryBonus(this);
	}

	private bool HasCropOutput()
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (IsCropItem(allItem))
			{
				return true;
			}
		}
		return false;
	}

	private bool SkipChest(Container c)
	{
		if (c == null || _unreachableChests.Contains(c.GetInstanceID()))
		{
			return true;
		}
		if (_ai != null)
		{
			return _ai.IsPositionBlacklisted(c.transform.position);
		}
		return false;
	}

	private bool ChestReachable(Container c)
	{
		if (MHR_Chests.CanReach(transform.position, c))
		{
			return true;
		}
		_unreachableChests.Add(c.GetInstanceID());
		Log($"Skipping chest at {c.transform.position:F1} - can't get within reach of it");
		return false;
	}

	private void ScanNearbyChests()
	{
		if (Time.time >= _unreachableClearAt)
		{
			_unreachableChests.Clear();
			_unreachableClearAt = Time.time + 60f;
		}
		_nearbyChests.Clear();
		_tempPieces.Clear();
		Piece.GetAllPiecesInRadius(transform.position, ScanRadius, _tempPieces);
		foreach (Piece tempPiece in _tempPieces)
		{
			if (!(tempPiece == null))
			{
				Container componentInChildren = tempPiece.GetComponentInChildren<Container>();
				if (!(componentInChildren == null) && !(componentInChildren.gameObject == gameObject) && MHR_Chests.Usable(componentInChildren, this))
				{
					_nearbyChests.Add(componentInChildren);
				}
			}
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug($"Scan: found {_nearbyChests.Count} chests within {ScanRadius}m");
		}
	}

	private Container FindChestWithSpace()
	{
		Container result = null;
		float num = float.MaxValue;
		foreach (Container nearbyChest in _nearbyChests)
		{
			if (SkipChest(nearbyChest))
			{
				continue;
			}
			Inventory inventory = nearbyChest.GetInventory();
			if (inventory != null && inventory.GetEmptySlots() > 0)
			{
				float num2 = Vector3.Distance(transform.position, nearbyChest.transform.position);
				if (num2 < num)
				{
					result = nearbyChest;
					num = num2;
				}
			}
		}
		return result;
	}

	private ItemDrop.ItemData FindCultivator()
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return null;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem?.m_shared == null || !(allItem.m_shared.m_buildPieces != null))
			{
				continue;
			}
			PieceTable buildPieces = allItem.m_shared.m_buildPieces;
			if (buildPieces.m_pieces == null)
			{
				continue;
			}
			foreach (GameObject piece in buildPieces.m_pieces)
			{
				if (piece != null && piece.GetComponent<Plant>() != null)
				{
					return allItem;
				}
			}
		}
		return null;
	}

	private SeedPlantInfo? FindSpecificSeedInInventory(string seedPrefabName)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null || s_seedToPlant == null)
		{
			return null;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (!(allItem?.m_dropPrefab == null) && !(allItem.m_dropPrefab.name != seedPrefabName) && s_seedToPlant.TryGetValue(seedPrefabName, out var value))
			{
				return value;
			}
		}
		return null;
	}

	private SeedPlantInfo? FindPlantableSeedInInventory(ItemDrop.ItemData cultivator)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null || s_seedToPlant == null)
		{
			return null;
		}
		HashSet<string> cultivatorSeeds = GetCultivatorSeeds(cultivator);
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (!(allItem?.m_dropPrefab == null))
			{
				string text = allItem.m_dropPrefab.name;
				if (cultivatorSeeds.Contains(text) && MHR_SeedRules.AllowPlant(this, text) && s_seedToPlant.TryGetValue(text, out var value))
				{
					return value;
				}
			}
		}
		return null;
	}

	private bool FindPlantPositionInZone(SeedPlantInfo info, FarmZone zone, out Vector3 result)
	{
		result = Vector3.zero;
		float num = Mathf.Max(PlantSpacing, info.GrowRadius);
		float halfSize = zone.HalfSize;
		float num2 = Mathf.Ceil((0f - halfSize) / num) * num;
		float num3 = Mathf.Floor(halfSize / num) * num;
		bool flag = false;
		PlantOrder plantOrder = GetPlantOrder(zone);
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int num10 = 0;
		_plantCandidates.Clear();
		for (float num11 = num2; num11 <= num3; num11 += num)
		{
			for (float num12 = num2; num12 <= num3; num12 += num)
			{
				num4++;
				Vector3 vector = zone.LocalToWorld(num11, num12);
				if (!ZoneSystem.instance.GetGroundHeight(vector, out var height))
				{
					num8++;
					continue;
				}
				vector.y = height;
				if (info.Biome != Heightmap.Biome.None && (Heightmap.FindBiome(vector) & info.Biome) == 0)
				{
					num7++;
					continue;
				}
				if ((info.NeedsCultivated || zone.Tilled) && !MHR_SafeCultivated(vector))
				{
					num5++;
					continue;
				}
				if (zone.Tilled && InRealZone(_farmZones, vector))
				{
					num6++;
					continue;
				}
				int num13 = Physics.OverlapSphereNonAlloc(vector, info.GrowRadius * 0.5f, _spacingBuffer, _spaceMask);
				bool flag2 = false;
				for (int i = 0; i < num13; i++)
				{
					Collider collider = _spacingBuffer[i];
					if (collider.GetComponent<Plant>() != null || collider.GetComponent<Pickable>() != null || collider.GetComponent<Piece>() != null)
					{
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					num6++;
					continue;
				}
				if (NoSunOrRoom(vector, info.GrowRadius))
				{
					num9++;
					continue;
				}
				if (KnownUnreachable(vector))
				{
					num10++;
					continue;
				}
				if (_ai != null && _ai.IsPositionBlacklisted(vector))
				{
					num6++;
					continue;
				}
				if (!PrivateArea.CheckAccess(vector, 0f, flash: false))
				{
					num6++;
					continue;
				}
				float num14 = (plantOrder.AlongZ ? num12 : num11) * plantOrder.RowSign;
				float num15 = (plantOrder.AlongZ ? num11 : num12);
				int num16 = Mathf.RoundToInt((num14 + halfSize) / num);
				float key = (float)num16 * 10000f + ((num16 % 2 == 0) ? num15 : (0f - num15));
				_plantCandidates.Add(new KeyValuePair<float, Vector3>(key, vector));
			}
		}
		_plantCandidates.Sort((KeyValuePair<float, Vector3> a, KeyValuePair<float, Vector3> b) => a.Key.CompareTo(b.Key));
		int num17 = 0;
		for (int num18 = 0; num18 < _plantCandidates.Count; num18++)
		{
			if (num17 >= 8)
			{
				break;
			}
			Vector3 value = _plantCandidates[num18].Value;
			num17++;
			if (MHR_Mover.Reachable(transform.position, value, UseDistance))
			{
				result = value;
				flag = true;
				break;
			}
			MarkUnreachable(value);
			num10++;
		}
		if (!flag)
		{
			_plantOrders.Remove(ZoneKey(zone));
		}
		Log($"FindPlantPositionInZone: cells={num4} noHeight={num8} noBiome={num7} " + $"noCultivated={num5} blocked={num6} roofOrWall={num9} cantReach={num10} found={flag} " + $"seed=\"{info.SeedPrefabName}\" needsCultivated={info.NeedsCultivated} zone={zone.Center:F1}");
		return flag;
	}

	private static string ZoneKey(FarmZone zone)
	{
		return $"{zone.Center.x:F1},{zone.Center.z:F1}";
	}

	private PlantOrder GetPlantOrder(FarmZone zone)
	{
		string key = ZoneKey(zone);
		if (_plantOrders.TryGetValue(key, out var value))
		{
			return value;
		}
		zone.WorldToLocal(transform.position, out var lx, out var lz);
		value = new PlantOrder
		{
			AlongZ = (Mathf.Abs(lz) >= Mathf.Abs(lx))
		};
		float num = (value.AlongZ ? lz : lx);
		value.RowSign = ((num >= 0f) ? 1f : (-1f));
		_plantOrders[key] = value;
		Log(string.Format("[Plant] Zone at {0:F0}: rows along {1}, starting at the far edge", zone.Center, value.AlongZ ? "x" : "z"));
		return value;
	}

	private Container FindChestWithSeeds(ItemDrop.ItemData cultivator, out SeedPlantInfo seedInfo, out string seedPrefab, out int seedCount)
	{
		seedInfo = default;
		seedPrefab = null;
		seedCount = 0;
		if (s_seedToPlant == null)
		{
			return null;
		}
		HashSet<string> cultivatorSeeds = GetCultivatorSeeds(cultivator);
		Container result = null;
		float num = float.MaxValue;
		foreach (Container nearbyChest in _nearbyChests)
		{
			if (SkipChest(nearbyChest))
			{
				continue;
			}
			Inventory inventory = nearbyChest.GetInventory();
			if (inventory == null)
			{
				continue;
			}
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem?.m_dropPrefab == null)
				{
					continue;
				}
				string text = allItem.m_dropPrefab.name;
				if (cultivatorSeeds.Contains(text) && MHR_SeedRules.AllowPlant(this, text) && s_seedToPlant.TryGetValue(text, out var value))
				{
					float num2 = Vector3.Distance(transform.position, nearbyChest.transform.position);
					if (num2 < num)
					{
						result = nearbyChest;
						num = num2;
						seedInfo = value;
						seedPrefab = text;
						seedCount = allItem.m_stack;
					}
				}
			}
		}
		return result;
	}

	private Container FindChestWithSpecificSeed(string seedPrefabName, out SeedPlantInfo seedInfo, out int seedCount)
	{
		seedInfo = default;
		seedCount = 0;
		if (!s_seedToPlant.TryGetValue(seedPrefabName, out seedInfo))
		{
			return null;
		}
		foreach (Container nearbyChest in _nearbyChests)
		{
			if (SkipChest(nearbyChest))
			{
				continue;
			}
			Inventory inventory = nearbyChest.GetInventory();
			if (inventory != null)
			{
				ItemDrop.ItemData itemByPrefab = MHR_SeedRules.GetItemByPrefab(inventory, seedPrefabName, -1, worstQuality: false);
				if (itemByPrefab != null)
				{
					seedCount = itemByPrefab.m_stack;
					return nearbyChest;
				}
			}
		}
		return null;
	}

	private static bool IsPicked(Pickable pickable)
	{
		ZNetView component = pickable.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return true;
		}
		return component.GetZDO().GetBool(ZDOVars.s_picked);
	}

	private void LogMovement(float dt, string target, float dist, bool moveOk)
	{
	}

	private bool AtChestSpot(Container chest, float dist)
	{
		if (!_hasApproach || chest != _approachChest)
		{
			return false;
		}
		Vector3 position = transform.position;
		if (new Vector2(position.x - _approachSpot.x, position.z - _approachSpot.z).magnitude > 0.7f || Mathf.Abs(position.y - _approachSpot.y) > 1.2f)
		{
			return false;
		}
		return MHR_Chests.InReach(position, chest);
	}

	private Vector3 ChestGoal(Container chest)
	{
		if (chest == null)
		{
			return transform.position;
		}
		if (chest != _approachChest)
		{
			_approachChest = chest;
			_hasApproach = MHR_Chests.PickApproach(transform.position, chest, out _approachSpot);
		}
		if (!_hasApproach)
		{
			return chest.transform.position;
		}
		return _approachSpot;
	}

	private void UpdateStuckDetection(float dt, string targetName)
	{
		Vector3 currentTargetPosition = GetCurrentTargetPosition();
		if (Vector3.Distance(currentTargetPosition, _stuckLeg) > 1.5f)
		{
			_stuckLeg = currentTargetPosition;
			_stuckTimer = 0f;
		}
		_stuckTimer += dt;
		if ((!(_ai != null) || !MHR_Mover.GaveUp(_ai)) && !(_stuckTimer > 120f))
		{
			return;
		}
		Vector3 currentTargetPosition2 = GetCurrentTargetPosition();
		if (currentTargetPosition2 != Vector3.zero && _ai != null)
		{
			_ai.BlacklistPosition(currentTargetPosition2);
			if (_phase == FarmPhase.MovingToPlantSpot)
			{
				MarkUnreachable(currentTargetPosition2);
			}
			Log($"Blacklisted stuck target \"{targetName}\" at {currentTargetPosition2:F1}");
		}
		Abort("stuck moving to " + targetName);
	}

	private bool PickUpOneDrop(Vector3 pos)
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		int num = Physics.OverlapSphereNonAlloc(pos, 5f, _dropBuffer, _itemMask);
		for (int i = 0; i < num; i++)
		{
			ItemDrop componentInParent = _dropBuffer[i].GetComponentInParent<ItemDrop>();
			if (!(componentInParent == null) && componentInParent.m_itemData != null)
			{
				ZNetView component = componentInParent.GetComponent<ZNetView>();
				if (!(component == null) && component.GetZDO() != null && !componentInParent.CanPickup(autoPickupDelay: false))
				{
					componentInParent.RequestOwn();
				}
				else if (!(component == null) && component.GetZDO() != null && inventory.CanAddItem(componentInParent.m_itemData, componentInParent.m_itemData.m_stack))
				{
					inventory.AddItem(componentInParent.m_itemData);
					component.ClaimOwnership();
					component.Destroy();
					Log($"Picked up {componentInParent.m_itemData.m_stack}x " + "\"" + componentInParent.m_itemData.m_shared.m_name + "\"");
					return true;
				}
			}
		}
		return false;
	}

	private void EquipCultivator(ItemDrop.ItemData cultivator)
	{
		if (cultivator == null || _humanoid == null)
		{
			return;
		}
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = true;
		}
		if (!_humanoid.IsItemEquiped(cultivator))
		{
			ItemDrop.ItemData rightItem = ReflectionHelper.GetRightItem(_humanoid);
			ItemDrop.ItemData leftItem = ReflectionHelper.GetLeftItem(_humanoid);
			if (rightItem != null)
			{
				_humanoid.UnequipItem(rightItem, triggerEquipEffects: false);
			}
			if (leftItem != null)
			{
				_humanoid.UnequipItem(leftItem, triggerEquipEffects: false);
			}
			_humanoid.EquipItem(cultivator, triggerEquipEffects: false);
			Log("Equipped cultivator \"" + cultivator.m_shared.m_name + "\"");
		}
	}

	private void PlantSeed(SeedPlantInfo info, Vector3 pos)
	{
		if (info.PlantPrefab == null)
		{
			Log("PlantPrefab is null — cannot plant");
			return;
		}
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return;
		}
		int num = CountItemByPrefab(inventory, info.SeedPrefabName);
		if (num <= 0)
		{
			Log("No seeds left — cannot plant");
			return;
		}
		int num2 = Physics.OverlapSphereNonAlloc(pos, info.GrowRadius * 0.5f, _spacingBuffer, _spaceMask);
		for (int i = 0; i < num2; i++)
		{
			Collider collider = _spacingBuffer[i];
			if (collider != null && (collider.GetComponent<Plant>() != null || collider.GetComponent<Pickable>() != null || collider.GetComponent<Piece>() != null))
			{
				Log($"Something was planted at {pos:F1} meanwhile — picking another spot");
				return;
			}
		}
		RemoveItemByPrefab(inventory, info.SeedPrefabName, 1);
		MHR_SeedRules.OnPlanted(this, info.SeedPrefabName);
		Quaternion quaternion = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
		TerrainModifier.SetTriggerOnPlaced(trigger: true);
		GameObject gameObject = UnityEngine.Object.Instantiate(info.PlantPrefab, pos, quaternion);
		TerrainModifier.SetTriggerOnPlaced(trigger: false);
		Piece component = gameObject.GetComponent<Piece>();
		if (component != null)
		{
			component.SetCreator(MHR_Owner.CreatorId(this), default);
			component.m_placeEffect.Create(pos, quaternion, gameObject.transform, 1f, -1, ZDOID.None);
		}
		Log($"Planted \"{info.PlantPrefab.name}\" at {pos:F1} " + $"(seeds remaining: {num - 1})");
	}

	private static int CountItemByPrefab(Inventory inv, string prefabName)
	{
		int num = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (itemData?.m_dropPrefab != null && itemData.m_dropPrefab.name == prefabName)
			{
				num += itemData.m_stack;
			}
		}
		return num;
	}

	internal static void BuildSeedMappingPublic()
	{
		BuildSeedMapping();
	}

	private HashSet<string> GetCultivatorSeeds(ItemDrop.ItemData cultivator)
	{
		HashSet<string> hashSet = new HashSet<string>();
		if (cultivator?.m_shared?.m_buildPieces == null)
		{
			return hashSet;
		}
		PieceTable buildPieces = cultivator.m_shared.m_buildPieces;
		if (buildPieces.m_pieces == null)
		{
			return hashSet;
		}
		foreach (GameObject piece in buildPieces.m_pieces)
		{
			if (!(piece == null))
			{
				Piece component = piece.GetComponent<Piece>();
				Plant component2 = piece.GetComponent<Plant>();
				if (!(component == null) && !(component2 == null) && component.m_resources != null && component.m_resources.Length != 0 && !(component.m_resources[0].m_resItem == null))
				{
					string item = component.m_resources[0].m_resItem.gameObject.name;
					hashSet.Add(item);
				}
			}
		}
		return hashSet;
	}

	internal static bool MHR_SafeCultivated(Vector3 pos)
	{
		if (s_mhrCultBroken)
		{
			return false;
		}
		try
		{
			return MHR_CultivatedRaw(pos);
		}
		catch (Exception ex)
		{
			s_mhrCultBroken = true;
			CompanionsPlugin.Log.LogWarning("[Farm] Can't check for cultivated ground (another mod may have changed the game's terrain lookup). Farming will only use crops it already recognizes. Error: " + ex.ToString());
			return false;
		}
	}

	private static Vector3Int ReachKey(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
	}

	internal void MarkUnreachable(Vector3 p)
	{
		if (_unreachable.Count > 2000)
		{
			_unreachable.Clear();
		}
		_unreachable[ReachKey(p)] = Time.time;
	}

	private bool KnownUnreachable(Vector3 p)
	{
		if (!_unreachable.TryGetValue(ReachKey(p), out var value))
		{
			return false;
		}
		if (Time.time - value < ModConfig.FarmUnreachableForget.Value)
		{
			return true;
		}
		_unreachable.Remove(ReachKey(p));
		return false;
	}

	private bool NoSunOrRoom(Vector3 p, float growRadius)
	{
		if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.up, 100f, _roofMask))
		{
			return true;
		}
		int num = Physics.OverlapSphereNonAlloc(p, growRadius, _spacingBuffer, _spaceMask);
		for (int i = 0; i < num; i++)
		{
			Collider collider = _spacingBuffer[i];
			if (collider != null && collider.GetComponentInParent<Plant>() == null && collider.GetComponentInParent<Pickable>() == null)
			{
				return true;
			}
		}
		return false;
	}

	private bool FindPlantPosition(SeedPlantInfo info, out Vector3 result)
	{
		result = Vector3.zero;
		float num = Mathf.Max(PlantSpacing, info.GrowRadius);
		float scanRadius = ScanRadius;
		Vector3 position = transform.position;
		float num2 = Mathf.Floor(position.x / num) * num;
		float num3 = Mathf.Floor(position.z / num) * num;
		float num4 = float.MaxValue;
		bool result2 = false;
		int num5 = Mathf.CeilToInt(scanRadius / num);
		for (int i = -num5; i <= num5; i++)
		{
			for (int j = -num5; j <= num5; j++)
			{
				float num6 = num2 + (float)i * num;
				float num7 = num3 + (float)j * num;
				Vector3 vector = new Vector3(num6, 0f, num7);
				float num8 = num6 - position.x;
				float num9 = num7 - position.z;
				if (Mathf.Sqrt(num8 * num8 + num9 * num9) > scanRadius || !ZoneSystem.instance.GetGroundHeight(vector, out var height))
				{
					continue;
				}
				vector.y = height;
				if ((info.Biome != Heightmap.Biome.None && (Heightmap.FindBiome(vector) & info.Biome) == 0) || (info.NeedsCultivated && !MHR_SafeCultivated(vector)))
				{
					continue;
				}
				int num10 = Physics.OverlapSphereNonAlloc(vector, info.GrowRadius * 0.5f, _spacingBuffer, _spaceMask);
				bool flag = false;
				for (int k = 0; k < num10; k++)
				{
					Collider collider = _spacingBuffer[k];
					if (collider.GetComponent<Plant>() != null || collider.GetComponent<Pickable>() != null || collider.GetComponent<Piece>() != null)
					{
						flag = true;
						break;
					}
				}
				if (flag || NoSunOrRoom(vector, info.GrowRadius) || KnownUnreachable(vector))
				{
					continue;
				}
				if (_farmZones != null)
				{
					bool flag2 = false;
					foreach (FarmZone farmZone in _farmZones)
					{
						if (farmZone.IsValid && !string.IsNullOrEmpty(farmZone.CropSeed) && farmZone.Contains(vector) && farmZone.CropSeed != info.SeedPrefabName)
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
				}
				float num11 = Vector3.Distance(transform.position, vector);
				if (num11 < num4)
				{
					num4 = num11;
					result = vector;
					result2 = true;
				}
			}
		}
		return result2;
	}

	private static void RemoveItemByPrefab(Inventory inv, string prefabName, int amount)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		int num = amount;
		int num2 = allItems.Count - 1;
		while (num2 >= 0 && num > 0)
		{
			ItemDrop.ItemData itemData = allItems[num2];
			if (!(itemData?.m_dropPrefab == null) && !(itemData.m_dropPrefab.name != prefabName))
			{
				int num3 = Mathf.Min(itemData.m_stack, num);
				inv.RemoveItem(itemData, num3);
				num -= num3;
			}
			num2--;
		}
	}

	private void LogDebug(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Farm|" + text + "] " + msg);
		}
	}

	internal bool IsCropItem(ItemDrop.ItemData item)
	{
		if (item == null || item.m_shared == null)
		{
			return false;
		}
		if (_humanoid != null && _humanoid.IsItemEquiped(item))
		{
			return false;
		}
		if (MHR_SeedRules.WantsStore(this, item) && s_cropOutputNames != null)
		{
			return s_cropOutputNames.Contains(item.m_dropPrefab.name);
		}
		return false;
	}

	private void Finish()
	{
		Log("Finish — immediate re-scan for next task");
		UnclaimPickable();
		CloseAnyOpenChest();
		RestoreCombatLoadout();
		_targetPickable = null;
		_targetChest = null;
		_outputChest = null;
		_allDoneNotified = false;
		_phase = FarmPhase.Idle;
		_scanTimer = 0f;
	}

	private void CloseAnyOpenChest()
	{
		if (_chestOpened)
		{
			if (_phase == FarmPhase.StoringOutput && _outputChest != null)
			{
				_outputChest.SetInUse(inUse: false);
			}
			else if (_targetChest != null)
			{
				_targetChest.SetInUse(inUse: false);
			}
			_chestOpened = false;
		}
	}

	private void RestoreCombatLoadout()
	{
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
			Log("SuppressAutoEquip=false, triggered auto-equip");
		}
	}

	private Vector3 GetCurrentTargetPosition()
	{
		switch (_phase)
		{
		case FarmPhase.MovingToPickable:
			if (!(_targetPickable != null))
			{
				return Vector3.zero;
			}
			return _targetPickable.transform.position;
		case FarmPhase.MovingToSeedChest:
			if (!(_targetChest != null))
			{
				return Vector3.zero;
			}
			return _targetChest.transform.position;
		case FarmPhase.MovingToPlantSpot:
			return _plantPosition;
		case FarmPhase.MovingToOutputChest:
			if (!(_outputChest != null))
			{
				return Vector3.zero;
			}
			return _outputChest.transform.position;
		default:
			return Vector3.zero;
		}
	}

	internal static void MHR_FarmLog(ManualLogSource log, object msg)
	{
		string text = msg.ToString();
		string[] array = new string[10] { "  ", "FindRipePickable", "Scanning", "[Store] No crop", "Mode changed", "Cleared follow", "Restored follow", "[Harvest] No ripe", "Nothing to farm", "Scan: found" };
		for (int i = 0; i < array.Length; i++)
		{
			if (text.Contains(array[i]))
			{
				if (CompanionsPlugin.LogDetailed)
				{
					log.LogDebug(msg);
				}
				return;
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			log.LogInfo(msg);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static bool MHR_CultivatedRaw(Vector3 pos)
	{
		return Heightmap.FindHeightmap(pos)?.IsCultivated(pos) ?? false;
	}
}
