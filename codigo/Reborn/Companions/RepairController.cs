using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Companions;

public class RepairController : MonoBehaviour
{
	internal enum RepairPhase
	{
		Idle,
		MovingToStation,
		Repairing
	}

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private ZSyncAnimation _zanim;

	private CompanionTalk _talk;

	private HarvestController _harvest;

	private DoorHandler _doorHandler;

	private SmeltController _smelt;

	private CookController _cook;

	private FarmController _farm;

	private HuntController _hunt;

	private FishController _fish;

	private static readonly HashSet<int> s_claimedStations = new HashSet<int>();

	private int _claimedStationId;

	private RepairPhase _phase;

	private CraftingStation _targetStation;

	private float _scanTimer;

	private float _repairTickTimer;

	private float _stuckTimer;

	private bool _lastScanFailed;

	private readonly List<ItemDrop.ItemData> _repairQueue = new List<ItemDrop.ItemData>();

	private readonly List<ItemDrop.ItemData> _tempWorn = new List<ItemDrop.ItemData>();

	private readonly Dictionary<ItemDrop.ItemData, float> _noStationUntil = new Dictionary<ItemDrop.ItemData, float>();

	private readonly Dictionary<ItemDrop.ItemData, Vector3> _noStationAt = new Dictionary<ItemDrop.ItemData, Vector3>();

	private float _moveStart;

	private bool _direct;

	private const float ScanBackoffInterval = 60f;

	private const float MoveTimeout = 10f;

	private CraftingStation _approachStation;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	private string _lastScanSig;

	private float _nextSkipLog;

	public bool IsDirect
	{
		get
		{
			if (_direct)
			{
				return _phase != RepairPhase.Idle;
			}
			return false;
		}
	}

	internal RepairPhase Phase => _phase;

	public bool IsActive => _phase != RepairPhase.Idle;

	private static float DurabilityThreshold => ModConfig.RepairDurabilityThreshold.Value;

	private static float ScanInterval => ModConfig.RepairScanInterval.Value;

	private static float ScanRadius => ModConfig.RepairScanRadius.Value;

	private static float RepairTickInterval => ModConfig.RepairTickInterval.Value;

	private static float UseDistance => ModConfig.RepairUseDistance.Value;

	private Vector3 StationGoal()
	{
		if (_targetStation != _approachStation)
		{
			_approachStation = _targetStation;
			_hasApproach = MHR_Chests.PickApproachPiece(transform.position, _targetStation, out _approachSpot);
		}
		if (!_hasApproach)
		{
			return _targetStation.transform.position;
		}
		return _approachSpot;
	}

	public bool DirectRepairAt(CraftingStation station)
	{
		if (station == null)
		{
			return false;
		}
		if (_phase != RepairPhase.Idle)
		{
			Abort("new directed repair");
		}
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		_tempWorn.Clear();
		inventory.GetWornItems(_tempWorn);
		_tempWorn.RemoveAll((ItemDrop.ItemData it) => it?.m_shared != null && it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch);
		if (_tempWorn.Count == 0)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("DirectRepairAt — no worn items");
			}
			return false;
		}
		_repairQueue.Clear();
		for (int num = 0; num < _tempWorn.Count; num++)
		{
			if (CanRepairAt(station, _tempWorn[num]))
			{
				_repairQueue.Add(_tempWorn[num]);
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				Log("DirectRepairAt: \"" + _tempWorn[num].m_shared.m_name + "\" — " + GetRepairRejectReason(station, _tempWorn[num]));
			}
		}
		if (_repairQueue.Count == 0)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Log("DirectRepairAt — no items repairable at \"" + station.m_name + "\"");
			}
			return false;
		}
		_targetStation = station;
		ClaimStation();
		_stuckTimer = 0f;
		_phase = RepairPhase.MovingToStation;
		_moveStart = Time.time;
		_direct = true;
		_lastScanFailed = false;
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"DirectRepairAt — {_repairQueue.Count} items to repair at \"{station.m_name}\"");
		}
		return true;
	}

	public void CancelDirected()
	{
		if (_phase != RepairPhase.Idle)
		{
			Abort("cancelled by new command");
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
		_harvest = GetComponent<HarvestController>();
		_doorHandler = GetComponent<DoorHandler>();
		_smelt = GetComponent<SmeltController>();
		_cook = GetComponent<CookController>();
		_farm = GetComponent<FarmController>();
		_hunt = GetComponent<HuntController>();
		_fish = GetComponent<FishController>();
	}

	private void OnDestroy()
	{
		UnclaimStation();
	}

	private void UnclaimStation()
	{
		if (_claimedStationId != 0)
		{
			s_claimedStations.Remove(_claimedStationId);
			_claimedStationId = 0;
		}
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		string text = ShouldAbort();
		if (text != null)
		{
			if (_phase != RepairPhase.Idle)
			{
				Abort("interrupted (" + text + ")");
			}
			return;
		}
		switch (_phase)
		{
		case RepairPhase.Idle:
			UpdateIdle(deltaTime);
			break;
		case RepairPhase.MovingToStation:
			UpdateMoving(deltaTime);
			break;
		case RepairPhase.Repairing:
			UpdateRepairing(deltaTime);
			break;
		}
	}

	private string ShouldAbort()
	{
		if (_ai != null && _ai.IsInCombat)
		{
			return "combat";
		}
		if (MHR_Loot.Active(this))
		{
			return "picking up loot";
		}
		if (MHR_JobRules.OtherOrder(this))
		{
			return "another order";
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			return "being talked to";
		}
		if (IsDirect)
		{
			return null;
		}
		if ((_harvest != null && _harvest.IsActive) || (_smelt != null && _smelt.IsActive))
		{
			return "the job has work";
		}
		if ((_cook != null && _cook.IsActive) || (_farm != null && _farm.IsActive) || (_hunt != null && _hunt.IsActive) || (_fish != null && _fish.IsActive))
		{
			return "the job has work";
		}
		return null;
	}

	private void Abort(string reason)
	{
		if (_zanim != null && _setup != null && _setup.CanWearArmor())
		{
			_zanim.SetInt("crafting", 0);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"Aborted — {reason} (phase was {_phase})");
		}
		UnclaimStation();
		_targetStation = null;
		_repairQueue.Clear();
		_phase = RepairPhase.Idle;
		_direct = false;
		_scanTimer = ScanInterval;
	}

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ScanInterval;
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return;
		}
		_tempWorn.Clear();
		inventory.GetWornItems(_tempWorn);
		_tempWorn.RemoveAll((ItemDrop.ItemData it) => it?.m_shared != null && it.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch);
		if (_tempWorn.Count == 0)
		{
			return;
		}
		bool flag = false;
		if (CompanionsPlugin.LogDetailed)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (ItemDrop.ItemData item in _tempWorn)
			{
				float maxDurability = item.GetMaxDurability();
				stringBuilder.Append(item.m_shared.m_name).Append(':').Append((maxDurability > 0f) ? ((int)(item.m_durability / maxDurability * 100f)) : 100)
					.Append(';');
			}
			string text = stringBuilder.ToString();
			flag = text != _lastScanSig;
			_lastScanSig = text;
		}
		if (!_lastScanFailed & flag)
		{
			for (int num = 0; num < _tempWorn.Count; num++)
			{
				ItemDrop.ItemData itemData = _tempWorn[num];
				float maxDurability2 = itemData.GetMaxDurability();
				float num2 = ((maxDurability2 > 0f) ? (itemData.m_durability / maxDurability2 * 100f) : 100f);
				Recipe recipe = ObjectDB.instance?.GetRecipe(itemData);
				string arg = recipe?.m_craftingStation?.m_name ?? recipe?.m_repairStation?.m_name ?? "none";
				int num3 = recipe?.m_minStationLevel ?? 0;
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Scan: \"{itemData.m_shared.m_name}\" dur={itemData.m_durability:F0}/{maxDurability2:F0} ({num2:F0}%) " + $"station=\"{arg}\" minLevel={num3} canRepair={itemData.m_shared.m_canBeReparied}");
				}
			}
		}
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
		float num4 = MHR_Nav.RepairThreshold(this);
		float time = Time.time;
		if (_noStationUntil.Count > 0)
		{
			foreach (ItemDrop.ItemData item2 in new List<ItemDrop.ItemData>(_noStationUntil.Keys))
			{
				if (time >= _noStationUntil[item2] || (_noStationAt.TryGetValue(item2, out var value) && Vector3.Distance(value, transform.position) > ScanRadius))
				{
					_noStationUntil.Remove(item2);
					_noStationAt.Remove(item2);
				}
			}
		}
		for (int num5 = 0; num5 < _tempWorn.Count; num5++)
		{
			float maxDurability3 = _tempWorn[num5].GetMaxDurability();
			if ((!_noStationUntil.TryGetValue(_tempWorn[num5], out var value2) || !(time < value2)) && maxDurability3 > 0f && _tempWorn[num5].m_durability / maxDurability3 < num4)
			{
				list.Add(_tempWorn[num5]);
			}
		}
		if (list.Count <= 0)
		{
			if (flag || time >= _nextSkipLog)
			{
				_nextSkipLog = time + 60f;
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Scan: {_tempWorn.Count} worn items but none below {num4 * 100f:F0}% that a station nearby can fix — skipping");
				}
			}
			return;
		}
		List<CraftingStation> allCraftingStations = ReflectionHelper.GetAllCraftingStations();
		if (allCraftingStations == null)
		{
			return;
		}
		CraftingStation craftingStation = null;
		float num6 = float.MaxValue;
		HashSet<ItemDrop.ItemData> hashSet = new HashSet<ItemDrop.ItemData>();
		for (int num7 = 0; num7 < allCraftingStations.Count; num7++)
		{
			CraftingStation craftingStation2 = allCraftingStations[num7];
			if (craftingStation2 == null)
			{
				continue;
			}
			float num8 = MHR_Nav.StationScanDist(transform.position, craftingStation2.transform.position, this);
			if (num8 > ScanRadius)
			{
				continue;
			}
			bool flag2 = _ai == null || !_ai.IsPositionBlacklisted(craftingStation2.transform.position);
			bool flag3 = false;
			for (int num9 = 0; num9 < list.Count; num9++)
			{
				if (CanRepairAt(craftingStation2, list[num9]))
				{
					flag3 = true;
					if (flag2)
					{
						hashSet.Add(list[num9]);
					}
				}
			}
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Scan: station \"{craftingStation2.m_name}\" level={craftingStation2.GetLevel()} " + $"dist={num8:F1}m canRepairWorn={flag3}");
			}
			if (!flag3 || !(num8 < num6) || s_claimedStations.Contains(craftingStation2.GetInstanceID()))
			{
				continue;
			}
			if (_ai != null && _ai.IsPositionBlacklisted(craftingStation2.transform.position))
			{
				if (CompanionsPlugin.LogDetailed)
				{
					Log("Scan: skipping \"" + craftingStation2.m_name + "\" — position blacklisted");
				}
			}
			else
			{
				craftingStation = craftingStation2;
				num6 = num8;
			}
		}
		float num10 = ModConfig.RepairNoStationWait?.Value ?? 600f;
		foreach (ItemDrop.ItemData item3 in list)
		{
			if (!hashSet.Contains(item3))
			{
				_noStationUntil[item3] = time + num10;
				_noStationAt[item3] = transform.position;
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"No station within {ScanRadius}m can repair \"{item3.m_shared.m_name}\" ({item3.m_durability / item3.GetMaxDurability() * 100f:F0}%) - not trying again for {num10 / 60f:F0} min");
				}
				if (_setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.AtHome(_setup))
				{
					CompanionStatus.Why(this, "hc_why_repair_no_station", true, Localization.instance?.Localize(item3.m_shared.m_name) ?? item3.m_shared.m_name);
				}
			}
		}
		if (craftingStation == null)
		{
			if (!_lastScanFailed && CompanionsPlugin.LogDetailed)
			{
				Log($"Scan: no reachable station can repair the items below {num4 * 100f:F0}% within {ScanRadius}m — backing off {60f}s");
			}
			_lastScanFailed = true;
			_scanTimer = 60f;
			return;
		}
		_lastScanFailed = false;
		_repairQueue.Clear();
		for (int num11 = 0; num11 < _tempWorn.Count; num11++)
		{
			if (CanRepairAt(craftingStation, _tempWorn[num11]))
			{
				_repairQueue.Add(_tempWorn[num11]);
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				Log("Scan: \"" + _tempWorn[num11].m_shared.m_name + "\" cannot be repaired at \"" + craftingStation.m_name + "\" — " + GetRepairRejectReason(craftingStation, _tempWorn[num11]));
			}
		}
		_targetStation = craftingStation;
		ClaimStation();
		_stuckTimer = 0f;
		_phase = RepairPhase.MovingToStation;
		_moveStart = Time.time;
		if (CompanionsPlugin.LogDetailed)
		{
			Log($"Found {_repairQueue.Count} items to repair at " + $"\"{_targetStation.m_name}\" level={_targetStation.GetLevel()} ({num6:F1}m away)");
		}
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_repair_start"));
		}
	}

	private void UpdateMoving(float dt)
	{
		if (_doorHandler != null && _doorHandler.IsActive)
		{
			return;
		}
		if (_targetStation == null)
		{
			Abort("station destroyed while moving");
			return;
		}
		float num = MHR_Nav.ReachDist(transform.position, _targetStation.transform.position);
		if (num < UseDistance)
		{
			_ai.StopMoving();
			_ai.LookAtPoint(_targetStation.transform.position);
			if (_zanim != null && _setup != null && _setup.CanWearArmor())
			{
				_zanim.SetInt("crafting", _targetStation.m_useAnimation);
			}
			_targetStation.PokeInUse();
			_repairTickTimer = RepairTickInterval;
			_phase = RepairPhase.Repairing;
			if (CompanionsPlugin.LogDetailed)
			{
				Log($"Arrived at \"{_targetStation.m_name}\" — starting repair of {_repairQueue.Count} items");
			}
			return;
		}
		Vector3 goal = StationGoal();
		MHR_Mover.Go(_ai, dt, goal, _hasApproach ? 0.5f : UseDistance, num > 8f, "Repair");
		if (MHR_Mover.GaveUp(_ai) || Time.time - _moveStart > 120f)
		{
			_ai.BlacklistPosition(_targetStation.transform.position);
			Abort($"couldn't get to \"{_targetStation.m_name}\" ({num:F1}m away, {Time.time - _moveStart:F0}s)");
			return;
		}
		_stuckTimer = MHR_Mover.StuckSeconds(_ai);
		if (_stuckTimer > 10f)
		{
			if (_targetStation != null && _ai != null)
			{
				_ai.BlacklistPosition(_targetStation.transform.position);
			}
			Abort($"stuck moving to \"{_targetStation.m_name}\" ({num:F1}m away, stuck {_stuckTimer:F1}s)");
		}
	}

	private void UpdateRepairing(float dt)
	{
		if (_targetStation == null)
		{
			Abort("station destroyed while repairing");
			return;
		}
		float num = Vector3.Distance(transform.position, _targetStation.transform.position);
		if (num > UseDistance * 2f)
		{
			Abort($"drifted too far from station ({num:F1}m)");
			return;
		}
		_ai.LookAtPoint(_targetStation.transform.position);
		_targetStation.PokeInUse();
		_repairTickTimer -= dt;
		if (_repairTickTimer > 0f)
		{
			return;
		}
		_repairTickTimer = RepairTickInterval;
		while (_repairQueue.Count > 0)
		{
			ItemDrop.ItemData itemData = _repairQueue[0];
			_repairQueue.RemoveAt(0);
			Inventory inventory = _humanoid?.GetInventory();
			if (inventory != null && inventory.ContainsItem(itemData) && !(itemData.m_durability >= itemData.GetMaxDurability()))
			{
				float durability = itemData.m_durability;
				float num2 = (itemData.m_durability = itemData.GetMaxDurability());
				_humanoid?.GetInventory()?.m_onChanged?.Invoke();
				_targetStation.m_repairItemDoneEffects.Create(_targetStation.transform.position, Quaternion.identity, null, 1f, -1, ZDOID.None);
				if (CompanionsPlugin.LogDetailed)
				{
					Log($"Repaired \"{itemData.m_shared.m_name}\" — {durability:F0} → {num2:F0}");
				}
				return;
			}
		}
		FinishRepair();
	}

	private void Log(string msg)
	{
		string text = _character?.m_name ?? "?";
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Repair|" + text + "] " + msg);
		}
	}

	internal static bool CanRepairAt(CraftingStation station, ItemDrop.ItemData item)
	{
		if (station == null || item == null)
		{
			return false;
		}
		if (!item.m_shared.m_canBeReparied || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch)
		{
			return false;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer != null && !station.CheckUsable(localPlayer, showMessage: false))
		{
			return false;
		}
		Recipe recipe = ObjectDB.instance?.GetRecipe(item);
		if (recipe == null)
		{
			return false;
		}
		if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
		{
			return false;
		}
		if ((!(recipe.m_repairStation != null) || !(recipe.m_repairStation.m_name == station.m_name)) && (!(recipe.m_craftingStation != null) || !(recipe.m_craftingStation.m_name == station.m_name)) && item.m_worldLevel >= Game.m_worldLevel)
		{
			return false;
		}
		if (Mathf.Min(station.GetLevel(), 4) < recipe.m_minStationLevel)
		{
			return false;
		}
		return true;
	}

	private static string GetRepairRejectReason(CraftingStation station, ItemDrop.ItemData item)
	{
		if (!item.m_shared.m_canBeReparied)
		{
			return "canBeRepaired=false";
		}
		Recipe recipe = ObjectDB.instance?.GetRecipe(item);
		if (recipe == null)
		{
			return "no recipe found";
		}
		if (recipe.m_craftingStation == null && recipe.m_repairStation == null)
		{
			return "recipe has no station";
		}
		string arg = recipe.m_repairStation?.m_name ?? recipe.m_craftingStation?.m_name ?? "?";
		if ((!(recipe.m_repairStation != null) || !(recipe.m_repairStation.m_name == station.m_name)) && (!(recipe.m_craftingStation != null) || !(recipe.m_craftingStation.m_name == station.m_name)) && item.m_worldLevel >= Game.m_worldLevel)
		{
			return $"wrong station (needs \"{arg}\", worldLevel={item.m_worldLevel} gameLevel={Game.m_worldLevel})";
		}
		int num = Mathf.Min(station.GetLevel(), 4);
		if (num < recipe.m_minStationLevel)
		{
			return $"station level too low ({num} < {recipe.m_minStationLevel})";
		}
		return "unknown";
	}

	private void ClaimStation()
	{
		UnclaimStation();
		_claimedStationId = _targetStation?.GetInstanceID() ?? 0;
		if (_claimedStationId != 0)
		{
			s_claimedStations.Add(_claimedStationId);
		}
	}

	private void FinishRepair()
	{
		if (_zanim != null && _setup != null && _setup.CanWearArmor())
		{
			_zanim.SetInt("crafting", 0);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Log("All items repaired at \"" + (_targetStation?.m_name ?? "?") + "\"");
		}
		if (_talk != null)
		{
			_talk.Say(ModLocalization.Loc("hc_speech_repair_done"));
		}
		UnclaimStation();
		_targetStation = null;
		_repairQueue.Clear();
		_phase = RepairPhase.Idle;
		_direct = false;
		_scanTimer = 1f;
	}
}
