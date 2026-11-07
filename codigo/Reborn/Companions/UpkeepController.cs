using System;
using System.Collections.Generic;
using Splatform;
using UnityEngine;

namespace Companions;

public class UpkeepController : MonoBehaviour
{
	private enum Step
	{
		Idle,
		Repair,
		FuelFetch,
		Fuel,
		SortTake,
		SortPut,
		Tidy,
		Stow,
		PileTake,
		PileBuild,
		PileDown,
		PileCollect,
		Hive,
		HiveCollect,
		Home
	}

	private sealed class Stock
	{
		public string Name;

		public readonly Dictionary<Container, int> Count = new Dictionary<Container, int>();
	}

	private sealed class PileKind
	{
		public string Item;

		public string Shared;

		public int Cost;

		public GameObject Prefab;

		public readonly List<Piece> Piles = new List<Piece>();
	}

	private const string Tag = "Upkeep";

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private CompanionSetup _setup;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionStamina _stamina;

	private CompanionTalk _talk;

	private RepairController _gearRepair;

	private float _orderHoldUntil;

	private bool _idleChore;

	private float _idleRange;

	private Step _step;

	private float _scanTimer;

	private float _stepStart;

	private float _nextAct;

	private float _nextSpotPick;

	private bool _hasSpot;

	private Vector3 _spot;

	private bool _hasAnchor;

	private Vector3 _anchor;

	private bool _atHome;

	private WearNTear _piece;

	private string _whyNoFuel;

	private int _whyNoStation;

	private Fireplace _fire;

	private Container _chest;

	private Container _target;

	private Container _open;

	private string _key;

	private int _amount;

	private bool _holdingHammer;

	private bool _idleSaid;

	private float _lastSpeech;

	private float _nextWaitLog;

	private float _nextNothingLog;

	private readonly Dictionary<string, int> _carried = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	private static readonly List<Piece> s_pieces = new List<Piece>();

	private string _lastBlocker;

	private bool _arrived;

	private float _arrivedAt;

	private static int s_mask = -1;

	private Beehive _hive;

	private Vector3 _hiveAt;

	private HashSet<int> _dropsBefore = new HashSet<int>();

	private string _honey;

	private PileKind _pile;

	private bool _pileHasSpot;

	private Vector3 _pileSpot;

	private Quaternion _pileRot;

	private Piece _pileNextTo;

	private Vector3 _pileStand;

	private readonly Dictionary<string, float> _pileNoSpot = new Dictionary<string, float>();

	private readonly HashSet<Vector3Int> _pileBadSpots = new HashSet<Vector3Int>();

	private static int s_buildMask = -1;

	private static readonly Dictionary<string, KeyValuePair<Vector3, Vector3>> s_prefabBox = new Dictionary<string, KeyValuePair<Vector3, Vector3>>();

	private Piece _downPile;

	private float _downAt;

	private HashSet<int> _pileDropsBefore = new HashSet<int>();

	internal bool IdleChoreActive => _idleChore;

	public bool IsActive
	{
		get
		{
			if (GetMode() == 15)
			{
				return _step != Step.Idle;
			}
			return false;
		}
	}

	private static float Reach => ModConfig.UpkeepReach?.Value ?? 4.5f;

	private static float WorkTime => ModConfig.UpkeepWorkTime?.Value ?? 1f;

	private static float GiveUp => ModConfig.UpkeepGiveUp?.Value ?? 90f;

	internal bool StartIdleChore(Vector3 center, float range)
	{
		if (_idleChore || GetMode() == 15 || _step != Step.Idle)
		{
			return false;
		}
		_idleChore = true;
		_idleRange = range;
		_atHome = false;
		_anchor = center;
		_hasAnchor = true;
		if (PlanRepair() || PlanFuel() || PlanHoney())
		{
			return true;
		}
		_idleChore = false;
		_hasAnchor = false;
		return false;
	}

	internal void StopIdleChore()
	{
		if (_idleChore)
		{
			Stop();
			EndIdleChore();
			if (_carried.Count > 0)
			{
				MHR_ToolKeeper.RequestUnload(_setup);
			}
		}
	}

	private void EndIdleChore()
	{
		PutHammerAway();
		_idleChore = false;
		_hasAnchor = false;
		_step = Step.Idle;
	}

	private int GetMode()
	{
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_combatAI = GetComponent<CompanionCombatAI>();
		_setup = GetComponent<CompanionSetup>();
		_humanoid = GetComponent<Humanoid>();
		_character = GetComponent<Character>();
		_stamina = GetComponent<CompanionStamina>();
		_talk = GetComponent<CompanionTalk>();
		_gearRepair = GetComponent<RepairController>();
	}

	public void NotifyActionModeChanged()
	{
		Stop();
		_hasAnchor = false;
		_scanTimer = 0f;
		_idleSaid = false;
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _ai == null || _humanoid == null || _setup == null)
		{
			return;
		}
		if (CompanionRest.MHR_ShouldPauseWork(this))
		{
			CloseChest();
			return;
		}
		if (GetMode() != 15 && !_idleChore)
		{
			if (_step != Step.Idle || _holdingHammer)
			{
				Stop();
			}
			_hasAnchor = false;
			return;
		}
		if (!_hasAnchor)
		{
			_hasAnchor = true;
			_atHome = _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
			_anchor = (_atHome ? _setup.GetHomePosition() : transform.position);
			if (CompanionsPlugin.LogNormal)
			{
				Log("Repair and Restock around " + (_atHome ? "home " : "here ") + MHR_Mover.Fmt(_anchor) + ", up to " + Range().ToString("F0") + " m out");
			}
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			_ai.StopMoving();
		}
		else if (_combatAI != null && _combatAI.IsEngaged)
		{
			if (_step != Step.Idle)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("A fight - stopping " + _step.ToString() + " for now");
				}
				Stop();
			}
		}
		else
		{
			if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this))
			{
				return;
			}
			if (MHR_JobRules.OrderPending(this))
			{
				if (_step != Step.Idle || _holdingHammer)
				{
					if (CompanionsPlugin.LogDetailed)
					{
						LogDebug("Following an order - " + _step.ToString() + " waits");
					}
					Stop();
				}
				_orderHoldUntil = Time.time + (ModConfig.UpkeepOrderWait?.Value ?? 30f);
			}
			else
			{
				if (Time.time < _orderHoldUntil)
				{
					return;
				}
				if (_gearRepair != null && _gearRepair.IsActive)
				{
					if (_step != Step.Idle || _holdingHammer)
					{
						if (CompanionsPlugin.LogDetailed)
						{
							LogDebug("Repairing its gear first - " + _step.ToString() + " waits");
						}
						Stop();
					}
					return;
				}
				if (_idleChore && _step == Step.Idle)
				{
					if (!PlanStow())
					{
						EndIdleChore();
					}
					return;
				}
				switch (_step)
				{
				case Step.Repair:
					UpdateRepair();
					break;
				case Step.FuelFetch:
				case Step.SortTake:
				case Step.SortPut:
				case Step.Tidy:
				case Step.Stow:
				case Step.PileTake:
					UpdateChest();
					break;
				case Step.PileBuild:
					UpdatePileBuild();
					break;
				case Step.PileDown:
					UpdatePileDown();
					break;
				case Step.PileCollect:
					UpdatePileCollect();
					break;
				case Step.Hive:
					UpdateHive();
					break;
				case Step.HiveCollect:
					UpdateHiveCollect();
					break;
				case Step.Fuel:
					UpdateFuel();
					break;
				case Step.Home:
					UpdateHome();
					break;
				default:
					UpdateIdle();
					break;
				}
			}
		}
	}

	private float Range()
	{
		if (_idleChore)
		{
			return _idleRange;
		}
		if (!_atHome)
		{
			return ModConfig.UpkeepAwayRadius?.Value ?? 30f;
		}
		return MHR_ToolKeeper.HomeRange();
	}

	private Vector3 Center()
	{
		return _anchor;
	}

	private void UpdateIdle()
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = ModConfig.UpkeepScanInterval?.Value ?? 5f;
		_whyNoFuel = null;
		_whyNoStation = 0;
		bool num = PlanRepair() || PlanFuel() || PlanHoney() || PlanStow() || PlanPiles() || PlanSort();
		if (_step != Step.Repair && _step != Step.PileTake && _step != Step.PileBuild && _step != Step.PileDown)
		{
			PutHammerAway();
		}
		if (num)
		{
			_idleSaid = false;
			return;
		}
		if (!_idleSaid)
		{
			_idleSaid = true;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Nothing to repair, restock or sort right now");
			}
			Say("hc_speech_upkeep_nothing");
		}
		else if (Time.time >= _nextNothingLog)
		{
			_nextNothingLog = Time.time + 120f;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Still nothing to do");
			}
		}
		List<CompanionStatus.Item> list = new List<CompanionStatus.Item>();
		if (_whyNoFuel != null)
		{
			list.Add(new CompanionStatus.Item(null, "hc_why_upkeep_no_fuel", _whyNoFuel));
		}
		if (_whyNoStation > 0)
		{
			list.Add(new CompanionStatus.Item(null, "hc_why_upkeep_no_station", _whyNoStation));
		}
		if (list.Count == 0)
		{
			list.Add(new CompanionStatus.Item(null, "hc_speech_upkeep_nothing"));
		}
		CompanionStatus.WhyRotate(this, list);
		if (Vector3.Distance(transform.position, Center()) > (_atHome ? MHR_ToolKeeper.AtHomeRange() : 10f))
		{
			Enter(Step.Home);
		}
	}

	private void Enter(Step s)
	{
		_step = s;
		_stepStart = Time.time;
		_nextAct = 0f;
		_hasSpot = false;
		_nextSpotPick = 0f;
		_arrived = false;
		_arrivedAt = 0f;
		MHR_Mover.Release(_ai);
		ShowDoing(s);
	}

	private void ShowDoing(Step s)
	{
		if (_nview?.GetZDO() != null && _nview.GetZDO().GetInt(CompanionSetup.ActionModeHash) == 15)
		{
			string text = (string.IsNullOrEmpty(_key) ? "" : (MHR_List.Local(NameOf(_key)) ?? NameOf(_key)));
			switch (s)
			{
			case Step.Repair:
				CompanionStatus.Doing(this, "hc_doing_upkeep_repair", ShortName(_piece));
				break;
			case Step.FuelFetch:
			case Step.Fuel:
				CompanionStatus.Doing(this, "hc_doing_upkeep_fuel", ShortName(_fire));
				break;
			case Step.SortTake:
			case Step.SortPut:
				CompanionStatus.Doing(this, "hc_doing_upkeep_sort", text);
				break;
			case Step.Tidy:
				CompanionStatus.Doing(this, "hc_doing_upkeep_tidy", text);
				break;
			case Step.Stow:
				CompanionStatus.Doing(this, "hc_doing_upkeep_stow", text);
				break;
			case Step.PileTake:
			case Step.PileBuild:
				CompanionStatus.Doing(this, "hc_doing_upkeep_pile", (_pile != null) ? (MHR_List.Local(_pile.Shared) ?? text) : text);
				break;
			case Step.PileDown:
			case Step.PileCollect:
				CompanionStatus.Doing(this, "hc_doing_take_pile", (_downPile != null) ? (MHR_List.Local(_downPile.m_name) ?? _downPile.m_name) : text);
				break;
			case Step.Hive:
			case Step.HiveCollect:
				CompanionStatus.Doing(this, "hc_doing_upkeep_honey");
				break;
			case Step.Home:
				CompanionStatus.Doing(this, "hc_doing_home");
				break;
			}
		}
	}

	private static string ShortName(Component c)
	{
		Piece piece = ((c != null) ? c.GetComponent<Piece>() : null);
		string text = ((piece != null) ? MHR_List.Local(piece.m_name) : null);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (!(c != null))
		{
			return "";
		}
		return Utils.GetPrefabName(c.gameObject);
	}

	private void Stop()
	{
		_pile = null;
		_downPile = null;
		CloseChest();
		PutHammerAway();
		_step = Step.Idle;
		_piece = null;
		_fire = null;
		_chest = null;
		_target = null;
		_key = null;
		_scanTimer = 1f;
		_ai?.StopMoving();
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
	}

	private void Done(string why)
	{
		if (!string.IsNullOrEmpty(why) && CompanionsPlugin.LogDetailed)
		{
			LogDebug(why);
		}
		CloseChest();
		_step = Step.Idle;
		_piece = null;
		_fire = null;
		_chest = null;
		_target = null;
		_key = null;
		_scanTimer = 0.3f;
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
	}

	private void Skip(Component c, float seconds, string why)
	{
		if (c != null)
		{
			_skip[c.GetInstanceID()] = Time.time + seconds;
		}
		Done(why);
	}

	private bool Skipped(Component c)
	{
		if (c != null && _skip.TryGetValue(c.GetInstanceID(), out var value))
		{
			return Time.time < value;
		}
		return false;
	}

	private bool PlanRepair()
	{
		if (ModConfig.UpkeepRepair != null && !ModConfig.UpkeepRepair.Value)
		{
			return false;
		}
		if (Hammer() == null)
		{
			return false;
		}
		float a = ModConfig.UpkeepRepairBelow?.Value ?? 1f;
		bool flag = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
		Vector3 position = transform.position;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		WearNTear wearNTear = null;
		float num = float.MaxValue;
		int num2 = 0;
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece piece = s_pieces[i];
			if (piece == null || !piece.IsPlacedByPlayer())
			{
				continue;
			}
			WearNTear component = piece.GetComponent<WearNTear>();
			if (component == null || Skipped(component) || component.GetHealthPercentage() >= Mathf.Min(a, 0.9999f))
			{
				continue;
			}
			Vector3 position2 = piece.transform.position;
			if (!flag && piece.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, position2) == null)
			{
				num2++;
			}
			else if (PrivateArea.CheckAccess(position2, 0f, flash: false))
			{
				float num3 = Vector3.Distance(position, position2);
				if (num3 < num)
				{
					num = num3;
					wearNTear = component;
				}
			}
		}
		if (wearNTear == null)
		{
			_whyNoStation = num2;
			if (num2 > 0 && Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 120f;
				if (CompanionsPlugin.LogNormal)
				{
					Log(num2 + " damaged piece(s) have no crafting station in range - a player couldn't repair those either");
				}
			}
			return false;
		}
		_piece = wearNTear;
		if (CompanionsPlugin.LogNormal)
		{
			Log("Repairing " + PieceName(wearNTear) + " (" + (wearNTear.GetHealthPercentage() * 100f).ToString("F0") + "%, " + num.ToString("F0") + " m away)");
		}
		if (Time.time - _lastSpeech > 120f)
		{
			Say("hc_speech_homestead_repair");
		}
		Enter(Step.Repair);
		return true;
	}

	private void UpdateRepair()
	{
		if (_piece == null || !_piece.GetComponent<ZNetView>().IsValid())
		{
			Done("The piece is gone");
			return;
		}
		if (_piece.GetHealthPercentage() >= 0.9999f)
		{
			Done(PieceName(_piece) + " is already repaired");
			return;
		}
		ItemDrop.ItemData itemData = Hammer();
		if (itemData == null)
		{
			PutHammerAway();
			Done("No working hammer any more - repairs wait until there is one");
			return;
		}
		Vector3 position = transform.position;
		if (!PieceInReach(position, _piece, out var aim))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(_piece, 300f, "Couldn't get within reach of " + PieceName(_piece) + " - leaving it for now");
			}
			else if (!Approach(_piece.transform.position, _piece))
			{
				Skip(_piece, 300f, "No way to get to " + PieceName(_piece) + " - leaving it for now");
			}
			else if (_arrived)
			{
				if (_arrivedAt == 0f)
				{
					_arrivedAt = Time.time;
				}
				else if (Time.time - _arrivedAt > 3f)
				{
					Skip(_piece, 300f, "Can't see or reach " + PieceName(_piece) + " from where it can stand" + ((_lastBlocker != null) ? (" (" + _lastBlocker + " is in the way)") : " (too far)") + " - leaving it for now");
				}
			}
			else
			{
				_arrivedAt = 0f;
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(itemData))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(itemData);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(aim);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(aim, 15f))
		{
			return;
		}
		Piece component = _piece.GetComponent<Piece>();
		if ((!(ZoneSystem.instance != null) || !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) && component != null && component.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(component.m_craftingStation.m_name, position) == null)
		{
			Skip(_piece, 300f, "No " + MHR_List.Local(component.m_craftingStation.m_name) + " in range of where it can stand to repair " + PieceName(_piece));
			return;
		}
		if (!PrivateArea.CheckAccess(_piece.transform.position, 0f, flash: false))
		{
			Skip(_piece, 600f, "A ward blocks repairing " + PieceName(_piece));
			return;
		}
		float num = SwingStamina(itemData);
		if (_stamina != null && _stamina.Stamina < num)
		{
			if (Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 20f;
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Catching its breath before the next swing (" + _stamina.Stamina.ToString("F0") + "/" + num.ToString("F0") + " stamina)");
				}
			}
			return;
		}
		_nextAct = Time.time + WorkTime;
		if (_piece.Repair())
		{
			string text = itemData.m_shared.m_attack?.m_attackAnimation;
			GetComponent<ZSyncAnimation>()?.SetTrigger((!string.IsNullOrEmpty(text)) ? text : "swing_hammer");
			if (component != null)
			{
				component.m_placeEffect.Create(component.transform.position, component.transform.rotation, null, 1f, -1, _character.GetZDOID());
			}
			_stamina?.UseStamina(num);
			if (itemData.m_shared.m_useDurability)
			{
				itemData.m_durability = Mathf.Max(0f, itemData.m_durability - itemData.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
			}
			if (CompanionsPlugin.LogNormal)
			{
				Log("Repaired " + PieceName(_piece));
			}
			_piece = null;
			_step = Step.Idle;
			_scanTimer = 0.2f;
			if (!PlanRepair())
			{
				PutHammerAway();
				Say("hc_speech_homestead_repaired");
			}
		}
	}

	private float SwingStamina(ItemDrop.ItemData hammer)
	{
		float num = ((hammer.m_shared.m_attack != null) ? hammer.m_shared.m_attack.m_attackStamina : 0f);
		PieceTable buildPieces = hammer.m_shared.m_buildPieces;
		if (buildPieces != null && buildPieces.m_skill != Skills.SkillType.None)
		{
			num -= num * 0.5f * _character.GetSkillFactor(buildPieces.m_skill);
		}
		return Mathf.Max(0f, num);
	}

	private ItemDrop.ItemData Hammer()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return null;
		}
		ItemDrop.ItemData itemData = null;
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			ItemDrop.ItemData.SharedData sharedData = allItem?.m_shared;
			if (!(sharedData?.m_buildPieces == null) && sharedData.m_buildPieces.name != null && sharedData.m_buildPieces.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0 && (!sharedData.m_useDurability || !(allItem.m_durability <= 0f)) && (itemData == null || allItem.m_durability > itemData.m_durability))
			{
				itemData = allItem;
			}
		}
		return itemData;
	}

	private void PutHammerAway()
	{
		if (!_holdingHammer || _humanoid == null)
		{
			return;
		}
		_holdingHammer = false;
		Inventory inventory = _humanoid.GetInventory();
		if (inventory != null)
		{
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem?.m_shared?.m_buildPieces != null && _humanoid.IsItemEquiped(allItem))
				{
					_humanoid.UnequipItem(allItem);
				}
			}
		}
		if (_setup != null && _setup.SuppressAutoEquip)
		{
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	private bool PieceInReach(Vector3 feet, WearNTear w, out Vector3 aim)
	{
		Vector3 vector = feet + Vector3.up * 1.6f;
		aim = w.transform.position;
		float num = float.MaxValue;
		bool result = false;
		_lastBlocker = null;
		Collider[] componentsInChildren = w.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (collider == null || !collider.enabled || collider.isTrigger)
			{
				continue;
			}
			Vector3 vector2 = collider.ClosestPoint(vector);
			Vector3 center = collider.bounds.center;
			for (int j = 0; j < 2; j++)
			{
				Vector3 vector3 = ((j == 0) ? vector2 : center);
				float num2 = Vector3.Distance(vector, vector3);
				if (!(num2 > Reach) && !(num2 >= num))
				{
					if (num2 < 1.5f || Clear(vector, vector3, w, out var blocker))
					{
						num = num2;
						aim = vector3;
						result = true;
					}
					else if (_lastBlocker == null)
					{
						_lastBlocker = blocker;
					}
				}
			}
		}
		return result;
	}

	private bool PlanFuel()
	{
		if (ModConfig.UpkeepRestock != null && !ModConfig.UpkeepRestock.Value)
		{
			return false;
		}
		float num = ModConfig.UpkeepFuelBelow?.Value ?? 0.8f;
		Inventory inventory = _humanoid.GetInventory();
		Vector3 position = transform.position;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		Fireplace fireplace = null;
		float num2 = float.MaxValue;
		bool flag = false;
		Container chest = null;
		HashSet<string> hashSet = new HashSet<string>();
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Fireplace fireplace2 = ((s_pieces[i] != null) ? s_pieces[i].GetComponent<Fireplace>() : null);
			if (fireplace2 == null || !fireplace2.m_canRefill || fireplace2.m_infiniteFuel || fireplace2.m_fuelItem == null || fireplace2.m_maxFuel <= 0f || Skipped(fireplace2) || !RefuelOn(fireplace2))
			{
				continue;
			}
			ZNetView component = fireplace2.GetComponent<ZNetView>();
			if (component == null || !component.IsValid() || Fuel(fireplace2) / fireplace2.m_maxFuel >= num)
			{
				continue;
			}
			string text = fireplace2.m_fuelItem.m_itemData.m_shared.m_name;
			bool flag2 = inventory.CountItems(text) > 0;
			Container container = null;
			if (!flag2)
			{
				if (hashSet.Contains(text))
				{
					continue;
				}
				container = ChestWith(text);
				if (container == null)
				{
					hashSet.Add(text);
					continue;
				}
			}
			float num3 = Vector3.Distance(position, fireplace2.transform.position) + (flag2 ? 0f : 20f);
			if (num3 < num2)
			{
				num2 = num3;
				fireplace = fireplace2;
				flag = flag2;
				chest = container;
			}
		}
		if (fireplace == null)
		{
			if (hashSet.Count > 0)
			{
				_whyNoFuel = string.Join(", ", new List<string>(Locals(hashSet)).ToArray());
			}
			if (hashSet.Count > 0 && Time.time >= _nextWaitLog)
			{
				_nextWaitLog = Time.time + 120f;
				if (CompanionsPlugin.LogNormal)
				{
					Log("Fires need fuel but there's none in the pack or the chests: " + string.Join(", ", Locals(hashSet)));
				}
			}
			return false;
		}
		_fire = fireplace;
		string text2 = fireplace.m_fuelItem.m_itemData.m_shared.m_name;
		int amount = Need(fireplace);
		if (Time.time - _lastSpeech > 120f)
		{
			Say("hc_speech_homestead_refuel");
		}
		if (flag)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Topping up " + PieceName(fireplace) + " (" + Fuel(fireplace).ToString("F1") + "/" + fireplace.m_maxFuel.ToString("F0") + ") with " + MHR_List.Local(text2) + " from the pack");
			}
			Enter(Step.Fuel);
			return true;
		}
		_chest = chest;
		_key = text2;
		_amount = amount;
		if (CompanionsPlugin.LogNormal)
		{
			Log("Fetching " + amount + " " + MHR_List.Local(text2) + " from a chest for " + PieceName(fireplace) + " (" + Fuel(fireplace).ToString("F1") + "/" + fireplace.m_maxFuel.ToString("F0") + ")");
		}
		Enter(Step.FuelFetch);
		return true;
	}

	private static bool RefuelOn(Fireplace f)
	{
		string text = Utils.GetPrefabName(f.gameObject).ToLowerInvariant();
		return (text.Contains("hearth") ? ModConfig.RefuelHearths : (text.Contains("brazier") ? ModConfig.RefuelBraziers : (text.Contains("groundtorch") ? ModConfig.RefuelStandingTorches : ((!text.Contains("walltorch") && !text.Contains("sconce")) ? ((!text.Contains("bathtub") && !text.Contains("hottub")) ? ((!text.Contains("fire_pit") && !text.Contains("firepit") && !text.Contains("bonfire") && !text.Contains("campfire")) ? ModConfig.RefuelOther : ModConfig.RefuelFires) : ModConfig.RefuelHotTubs) : ModConfig.RefuelWallTorches))))?.Value ?? true;
	}

	private static float Fuel(Fireplace f)
	{
		ZNetView component = f.GetComponent<ZNetView>();
		if (!(component != null) || !component.IsValid())
		{
			return 0f;
		}
		return component.GetZDO().GetFloat(ZDOVars.s_fuel);
	}

	private static int Need(Fireplace f)
	{
		return Mathf.Max(0, Mathf.FloorToInt(f.m_maxFuel - Fuel(f)));
	}

	private void UpdateFuel()
	{
		if (_fire == null || !_fire.GetComponent<ZNetView>().IsValid())
		{
			Done("The fire is gone");
			return;
		}
		Inventory inventory = _humanoid.GetInventory();
		string text = _fire.m_fuelItem.m_itemData.m_shared.m_name;
		if ((float)Mathf.CeilToInt(Fuel(_fire)) >= _fire.m_maxFuel)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log(PieceName(_fire) + " is full");
			}
			Say("hc_speech_homestead_stoked");
			Done(null);
			return;
		}
		if (inventory.CountItems(text) <= 0)
		{
			Done("Out of " + MHR_List.Local(text) + " for " + PieceName(_fire));
			return;
		}
		Vector3 position = transform.position;
		Vector3 point = _fire.transform.position + Vector3.up * 0.3f;
		if (!PointInReach(position, point, _fire))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(_fire, 300f, "Couldn't get within reach of " + PieceName(_fire) + " - leaving it for now");
			}
			else if (!Approach(_fire.transform.position, _fire))
			{
				Skip(_fire, 300f, "No way to get to " + PieceName(_fire) + " - leaving it for now");
			}
			else if (!_arrived)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			else if (Time.time - _arrivedAt > 3f)
			{
				Skip(_fire, 300f, "Can't reach " + PieceName(_fire) + " from where it can stand - leaving it for now");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(point);
		if (!(Time.time < _nextAct) && _ai.IsLookingAtPoint(point, 20f))
		{
			_nextAct = Time.time + WorkTime * 0.6f;
			ZNetView component = _fire.GetComponent<ZNetView>();
			if (!component.HasOwner())
			{
				component.ClaimOwnership();
			}
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			inventory.RemoveItem(text, 1);
			Took(text, -1);
			component.InvokeRPC("RPC_AddFuel");
		}
	}

	private bool PlanSort()
	{
		if (ModConfig.UpkeepSort != null && !ModConfig.UpkeepSort.Value)
		{
			return false;
		}
		MHR_List allow = MHR_List.Get(ModConfig.UpkeepSortItems?.Value ?? "*");
		bool gear = ModConfig.UpkeepSortGear?.Value ?? false;
		List<Container> list = Chests();
		Dictionary<string, Stock> dictionary = new Dictionary<string, Stock>();
		Container container = null;
		string key = null;
		foreach (Container item in list)
		{
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>();
			foreach (ItemDrop.ItemData allItem in item.GetInventory().GetAllItems())
			{
				if (!Sortable(allItem, allow, gear))
				{
					continue;
				}
				string text = Key(allItem);
				if (!dictionary.TryGetValue(text, out var value))
				{
					value = (dictionary[text] = new Stock
					{
						Name = allItem.m_shared.m_name
					});
				}
				value.Count.TryGetValue(item, out var value2);
				value.Count[item] = value2 + allItem.m_stack;
				if (allItem.m_shared.m_maxStackSize > 1 && allItem.m_stack < allItem.m_shared.m_maxStackSize)
				{
					dictionary2.TryGetValue(text, out value2);
					dictionary2[text] = value2 + 1;
					if (value2 + 1 >= 2 && container == null)
					{
						container = item;
						key = text;
					}
				}
			}
		}
		Vector3 position = transform.position;
		Vector3 a = Center();
		Container container2 = null;
		Container container3 = null;
		string key2 = null;
		float num = float.MaxValue;
		foreach (KeyValuePair<string, Stock> item2 in dictionary)
		{
			if (item2.Value.Count.Count < 2)
			{
				continue;
			}
			Container container4 = null;
			int num2 = -1;
			float num3 = 0f;
			foreach (KeyValuePair<Container, int> item3 in item2.Value.Count)
			{
				float num4 = Vector3.Distance(a, item3.Key.transform.position);
				if (item3.Value > num2 || (item3.Value == num2 && num4 < num3))
				{
					num2 = item3.Value;
					container4 = item3.Key;
					num3 = num4;
				}
			}
			if (!RoomIn(container4, item2.Key))
			{
				continue;
			}
			foreach (KeyValuePair<Container, int> item4 in item2.Value.Count)
			{
				if (!(item4.Key == container4))
				{
					float num5 = Vector3.Distance(position, item4.Key.transform.position) + Vector3.Distance(item4.Key.transform.position, container4.transform.position);
					if (num5 < num)
					{
						num = num5;
						container2 = item4.Key;
						container3 = container4;
						key2 = item2.Key;
					}
				}
			}
		}
		if (container2 != null)
		{
			_chest = container2;
			_target = container3;
			_key = key2;
			string token = dictionary[key2].Name;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Sorting: " + dictionary[key2].Count[container2] + " " + MHR_List.Local(token) + " from the chest at " + MHR_Mover.Fmt(container2.transform.position) + " to the one with the most (" + dictionary[key2].Count[container3] + ") at " + MHR_Mover.Fmt(container3.transform.position));
			}
			if (Time.time - _lastSpeech > 300f)
			{
				Say("hc_speech_homestead_tidy");
			}
			Enter(Step.SortTake);
			return true;
		}
		if (container != null)
		{
			_chest = container;
			_key = key;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Merging part-stacks of " + MHR_List.Local(dictionary[key].Name) + " in the chest at " + MHR_Mover.Fmt(container.transform.position));
			}
			Enter(Step.Tidy);
			return true;
		}
		return false;
	}

	private static bool Sortable(ItemDrop.ItemData it, MHR_List allow, bool gear)
	{
		if (it?.m_shared == null || it.m_dropPrefab == null)
		{
			return false;
		}
		if (it.m_shared.m_maxStackSize <= 1 && !gear)
		{
			return false;
		}
		return allow.Allows(it.m_dropPrefab.name, it.m_shared.m_name, MHR_List.Local(it.m_shared.m_name));
	}

	private static string Key(ItemDrop.ItemData it)
	{
		return it.m_shared.m_name + "|" + it.m_quality + "|" + it.m_worldLevel + "|" + it.m_variant;
	}

	private static bool RoomIn(Container c, string key)
	{
		Inventory inventory = c.GetInventory();
		if (inventory.GetEmptySlots() > 0)
		{
			return true;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (Key(allItem) == key && allItem.m_stack < allItem.m_shared.m_maxStackSize)
			{
				return true;
			}
		}
		return false;
	}

	private List<Container> Chests()
	{
		List<Container> list = new List<Container>();
		foreach (Container item in MHR_Chests.Near(Center(), Range()))
		{
			if (!Skipped(item) && MHR_Chests.Usable(item, this))
			{
				list.Add(item);
			}
		}
		return list;
	}

	private Container ChestWith(string sharedName)
	{
		Container result = null;
		float num = float.MaxValue;
		Vector3 position = transform.position;
		foreach (Container item in Chests())
		{
			if (item.GetInventory().CountItems(sharedName) > 0)
			{
				float num2 = Vector3.Distance(position, item.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	private bool PlanStow()
	{
		if (_carried.Count == 0)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		foreach (string item in new List<string>(_carried.Keys))
		{
			if (_carried[item] <= 0 || CountKey(inventory, item) <= 0)
			{
				_carried.Remove(item);
				continue;
			}
			Container container = StowChest(item);
			if (container == null)
			{
				if (Time.time >= _nextWaitLog)
				{
					_nextWaitLog = Time.time + 120f;
					if (CompanionsPlugin.LogNormal)
					{
						Log("No chest with room for the leftover " + MHR_List.Local(NameOf(item)) + " - keeping it for now");
					}
				}
				continue;
			}
			_chest = container;
			_key = item;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Putting the leftover " + Mathf.Min(_carried[item], CountKey(inventory, item)) + " " + MHR_List.Local(NameOf(item)) + " back");
			}
			Enter(Step.Stow);
			return true;
		}
		return false;
	}

	private Container StowChest(string key)
	{
		Container result = null;
		int num = -1;
		float num2 = float.MaxValue;
		Vector3 position = transform.position;
		foreach (Container item in Chests())
		{
			if (RoomIn(item, key))
			{
				int num3 = CountKey(item.GetInventory(), key);
				float num4 = Vector3.Distance(position, item.transform.position);
				if (num3 > num || (num3 == num && num4 < num2))
				{
					num = num3;
					result = item;
					num2 = num4;
				}
			}
		}
		return result;
	}

	private static string NameOf(string key)
	{
		int num = key.IndexOf('|');
		if (num < 0)
		{
			return key;
		}
		return key.Substring(0, num);
	}

	private static int CountKey(Inventory inv, string key)
	{
		int num = 0;
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			if (Key(allItem) == key || (key.IndexOf('|') < 0 && allItem.m_shared.m_name == key))
			{
				num += allItem.m_stack;
			}
		}
		return num;
	}

	private void Took(string key, int n)
	{
		_carried.TryGetValue(key, out var value);
		value += n;
		if (value <= 0)
		{
			_carried.Remove(key);
		}
		else
		{
			_carried[key] = value;
		}
	}

	private void UpdateChest()
	{
		Container container = ((_step == Step.SortPut) ? _target : _chest);
		if (container == null || container.GetInventory() == null || !container.GetComponent<ZNetView>().IsValid())
		{
			Done("The chest is gone");
			return;
		}
		Vector3 position = transform.position;
		if (!MHR_Chests.InReach(position, container))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(container, 300f, "Couldn't get within reach of the chest at " + MHR_Mover.Fmt(container.transform.position) + " - leaving it for now");
				return;
			}
			if (!_hasSpot || Time.time >= _nextSpotPick)
			{
				_nextSpotPick = Time.time + 4f;
				if (MHR_Chests.PickApproach(position, container, out var spot))
				{
					_spot = spot;
					_hasSpot = true;
				}
				else if (!_hasSpot)
				{
					Skip(container, 300f, "No spot to stand by the chest at " + MHR_Mover.Fmt(container.transform.position));
					return;
				}
			}
			MHR_Nav.Move(_ai, Time.deltaTime, _spot, 0.6f, run: true, "Upkeep");
			if (MHR_Mover.GaveUp(_ai))
			{
				Skip(container, 300f, "Couldn't get to the chest at " + MHR_Mover.Fmt(container.transform.position));
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(container.transform.position);
		if (_nextAct == 0f && !MHR_Chests.Open(container))
		{
			Skip(container, 30f, "Someone is using the chest");
		}
		else if (_nextAct == 0f)
		{
			_open = container;
			_nextAct = Time.time + Mathf.Max(0.5f, WorkTime);
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
		}
		else
		{
			if (Time.time < _nextAct)
			{
				return;
			}
			Inventory inventory = _humanoid.GetInventory();
			Inventory inventory2 = container.GetInventory();
			switch (_step)
			{
			case Step.PileTake:
				PileTaken(container, inventory2, inventory);
				break;
			case Step.FuelFetch:
			{
				int num3 = Move(inventory2, inventory, _key, byName: true, Mathf.Max(1, _amount));
				Took(_key, num3);
				CloseChest(container);
				if (num3 <= 0)
				{
					Skip(container, 120f, "No " + MHR_List.Local(_key) + " to take after all");
					break;
				}
				if (CompanionsPlugin.LogNormal)
				{
					Log("Took " + num3 + " " + MHR_List.Local(_key) + " from the chest");
				}
				Fireplace fire = _fire;
				Done(null);
				if (fire != null)
				{
					_fire = fire;
					Enter(Step.Fuel);
				}
				break;
			}
			case Step.SortTake:
			{
				int num2 = Move(inventory2, inventory, _key, byName: false, int.MaxValue);
				Took(_key, num2);
				CloseChest(container);
				if (num2 <= 0)
				{
					Skip(container, 60f, "Nothing to take after all (no room in the pack, or it's gone)");
					break;
				}
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Took " + num2 + " " + MHR_List.Local(NameOf(_key)) + " to carry over");
				}
				_chest = null;
				Enter(Step.SortPut);
				break;
			}
			case Step.SortPut:
			case Step.Stow:
			{
				int num4 = Mathf.Min(_carried.TryGetValue(_key, out var value) ? value : 0, CountKey(inventory, _key));
				int num5 = Move(inventory, inventory2, _key, _key.IndexOf('|') < 0, num4);
				Took(_key, -num5);
				Merge(inventory2, _key);
				CloseChest(container);
				if (CompanionsPlugin.LogNormal)
				{
					Log(((_step == Step.Stow) ? "Put back " : "Sorted ") + num5 + " " + MHR_List.Local(NameOf(_key)) + " into the chest at " + MHR_Mover.Fmt(container.transform.position) + ((num5 < num4) ? (" (" + (num4 - num5) + " didn't fit - they go in another chest)") : ""));
				}
				if (num5 < num4)
				{
					_skip[container.GetInstanceID()] = Time.time + 60f;
				}
				Done(null);
				break;
			}
			case Step.Tidy:
			{
				int num = Merge(inventory2, _key);
				CloseChest(container);
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Merged " + num + " part-stack(s)");
				}
				if (num == 0)
				{
					_skip[container.GetInstanceID()] = Time.time + 300f;
				}
				if (Time.time - _lastSpeech > 300f)
				{
					Say("hc_speech_homestead_tidied");
				}
				Done(null);
				break;
			}
			default:
				Done(null);
				break;
			}
		}
	}

	private void CloseChest(Container c = null)
	{
		if (_open != null)
		{
			try
			{
				_open.SetInUse(inUse: false);
			}
			catch
			{
			}
		}
		_open = null;
	}

	private int Move(Inventory from, Inventory to, string key, bool byName, int max)
	{
		bool flag = to == _humanoid.GetInventory();
		int num = 0;
		foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(from.GetAllItems()))
		{
			if (num >= max)
			{
				break;
			}
			if (byName ? (item.m_shared.m_name != key) : (Key(item) != key))
			{
				continue;
			}
			int num2 = Mathf.Min(item.m_stack, max - num);
			if (flag)
			{
				float weight = item.m_shared.m_weight;
				float num3 = CompanionTierData.CarryLimit(this) - to.GetTotalWeight();
				if (weight > 0f)
				{
					num2 = Mathf.Min(num2, Mathf.FloorToInt(num3 / weight));
				}
			}
			while (num2 > 0 && !to.CanAddItem(item, num2))
			{
				num2--;
			}
			if (num2 <= 0)
			{
				break;
			}
			ItemDrop.ItemData itemData = item.Clone();
			itemData.m_stack = num2;
			if (!to.AddItem(itemData))
			{
				break;
			}
			from.RemoveItem(item, num2);
			num += num2;
		}
		return num;
	}

	private static int Merge(Inventory inv, string key)
	{
		int num = 0;
		for (int i = 0; i < 32; i++)
		{
			ItemDrop.ItemData itemData = null;
			int num2 = 0;
			foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
			{
				if (allItem.m_shared.m_maxStackSize > 1 && allItem.m_stack < allItem.m_shared.m_maxStackSize && !((key.IndexOf('|') < 0) ? (allItem.m_shared.m_name != key) : (Key(allItem) != key)))
				{
					num2++;
					if (itemData == null || allItem.m_stack < itemData.m_stack)
					{
						itemData = allItem;
					}
				}
			}
			if (num2 < 2)
			{
				break;
			}
			ItemDrop.ItemData item = itemData.Clone();
			inv.RemoveItem(itemData);
			inv.AddItem(item);
			num++;
		}
		return num;
	}

	private bool Approach(Vector3 target, Component piece)
	{
		Vector3 position = transform.position;
		if (!_hasSpot || Time.time >= _nextSpotPick)
		{
			_nextSpotPick = Time.time + 4f;
			if (MHR_Chests.PickApproachPiece(position, piece, out var spot))
			{
				_spot = spot;
				_hasSpot = true;
			}
			else if (!_hasSpot)
			{
				_spot = target;
				_hasSpot = true;
			}
		}
		_arrived = MHR_Nav.Move(_ai, Time.deltaTime, _spot, (_spot == target) ? 1.5f : 0.6f, run: true, "Upkeep");
		return !MHR_Mover.GaveUp(_ai);
	}

	private static bool PointInReach(Vector3 feet, Vector3 point, Component target)
	{
		Vector3 vector = feet + Vector3.up * 1.6f;
		float num = Vector3.Distance(vector, point);
		if (num <= 4f)
		{
			if (!(num < 1.5f))
			{
				return Clear(vector, point, target);
			}
			return true;
		}
		return false;
	}

	private static bool Clear(Vector3 eye, Vector3 point, Component target)
	{
		string blocker;
		return Clear(eye, point, target, out blocker);
	}

	private static bool Clear(Vector3 eye, Vector3 point, Component target, out string blocker)
	{
		blocker = null;
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
		}
		Piece piece = ((target != null) ? target.GetComponentInParent<Piece>() : null);
		Vector3 vector = point - eye;
		float magnitude = vector.magnitude;
		if (magnitude < 0.05f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(eye, vector / magnitude, Mathf.Max(0f, magnitude - 0.05f), s_mask);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			Collider collider = raycastHit.collider;
			if (!(collider == null) && (!(piece != null) || !(collider.GetComponentInParent<Piece>() == piece)) && !(collider.GetComponentInParent<Character>() != null) && !(magnitude - raycastHit.distance < 0.5f))
			{
				Vector3 size = collider.bounds.size;
				if (!(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f))
				{
					Piece componentInParent = collider.GetComponentInParent<Piece>();
					blocker = ((componentInParent != null) ? (MHR_List.Local(componentInParent.m_name) ?? componentInParent.name) : collider.name);
					return false;
				}
			}
		}
		return true;
	}

	private void UpdateHome()
	{
		if (Vector3.Distance(transform.position, Center()) < 6f)
		{
			Done(_atHome ? "Back home" : "Back where it was working");
			_scanTimer = ModConfig.UpkeepScanInterval?.Value ?? 5f;
			return;
		}
		MHR_Nav.Move(_ai, Time.deltaTime, Center(), 4f, run: true, "Upkeep");
		if (MHR_Mover.GaveUp(_ai) || Time.time - _stepStart > GiveUp * 2f)
		{
			Done("Couldn't get back - staying here for now");
			_scanTimer = 30f;
		}
	}

	private static string PieceName(Component c)
	{
		Piece piece = ((c != null) ? c.GetComponent<Piece>() : null);
		string text = ((piece != null) ? MHR_List.Local(piece.m_name) : null);
		string text2;
		if (string.IsNullOrEmpty(text))
		{
			text2 = ((c != null) ? Utils.GetPrefabName(c.gameObject) : "piece");
		}
		else
		{
			text2 = text;
		}
		return "the " + text2 + ((c != null) ? (" at " + MHR_Mover.Fmt(c.transform.position)) : "");
	}

	private static IEnumerable<string> Locals(IEnumerable<string> names)
	{
		foreach (string name in names)
		{
			yield return MHR_List.Local(name) ?? name;
		}
	}

	private void Say(string key)
	{
		if (!(Time.time - _lastSpeech < 30f))
		{
			_lastSpeech = Time.time;
			_talk?.Say(ModLocalization.Loc(key));
		}
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Upkeep] " + msg);
		}
	}

	private static void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log?.LogDebug("[Upkeep] " + msg);
		}
	}

	private bool PlanHoney()
	{
		if (ModConfig.UpkeepHoney != null && !ModConfig.UpkeepHoney.Value)
		{
			return false;
		}
		float num = ModConfig.UpkeepHoneyAt?.Value ?? 0.5f;
		Vector3 position = transform.position;
		Beehive beehive = null;
		float num2 = float.MaxValue;
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		foreach (Piece s_piece in s_pieces)
		{
			Beehive beehive2 = ((s_piece != null) ? s_piece.GetComponent<Beehive>() : null);
			if (beehive2 == null || beehive2.m_honeyItem == null || Skipped(beehive2))
			{
				continue;
			}
			int num3 = HoneyIn(beehive2);
			if (num3 > 0 && num3 >= Mathf.CeilToInt((float)beehive2.m_maxHoney * num) && PrivateArea.CheckAccess(beehive2.transform.position, 0f, flash: false))
			{
				float num4 = Vector3.Distance(position, beehive2.transform.position);
				if (num4 < num2)
				{
					num2 = num4;
					beehive = beehive2;
				}
			}
		}
		if (beehive == null)
		{
			return false;
		}
		_hive = beehive;
		_honey = beehive.m_honeyItem.m_itemData.m_shared.m_name;
		if (CompanionsPlugin.LogNormal)
		{
			Log("Collecting " + HoneyIn(beehive) + " " + MHR_List.Local(_honey) + " from the beehive at " + MHR_Mover.Fmt(beehive.transform.position));
		}
		Enter(Step.Hive);
		return true;
	}

	private static int HoneyIn(Beehive hive)
	{
		return (hive.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level)).GetValueOrDefault();
	}

	private void UpdateHive()
	{
		Beehive hive = _hive;
		if (hive == null || hive.GetComponent<ZNetView>() == null || !hive.GetComponent<ZNetView>().IsValid())
		{
			Done("The beehive is gone");
			return;
		}
		if (HoneyIn(hive) <= 0)
		{
			Done("Someone emptied the beehive first");
			return;
		}
		Vector3 position = transform.position;
		Vector3 point = hive.transform.position + Vector3.up * 0.5f;
		if (!PointInReach(position, point, hive))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				Skip(hive, 300f, "Couldn't get within reach of the beehive - leaving it for now");
			}
			else if (!Approach(hive.transform.position, hive))
			{
				Skip(hive, 300f, "No way to get to the beehive");
			}
			else if (!_arrived)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			else if (Time.time - _arrivedAt > 3f)
			{
				Skip(hive, 300f, "Can't reach the beehive from where it can stand");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(point);
		if (_nextAct == 0f)
		{
			_nextAct = Time.time + 0.4f;
		}
		else if (!(Time.time < _nextAct) && _ai.IsLookingAtPoint(point, 20f))
		{
			GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			_hiveAt = ((hive.m_spawnPoint != null) ? hive.m_spawnPoint.position : hive.transform.position);
			_dropsBefore = MHR_Chests.DropsNear(_hiveAt, 3f);
			hive.GetComponent<ZNetView>().InvokeRPC("RPC_Extract");
			Enter(Step.HiveCollect);
		}
	}

	private void UpdateHiveCollect()
	{
		if (Time.time - _stepStart > 20f)
		{
			Done(null);
		}
		else
		{
			if (Time.time - _stepStart < 0.6f)
			{
				return;
			}
			Inventory inventory = _humanoid.GetInventory();
			Vector3 position = transform.position;
			ItemDrop itemDrop = null;
			float num = float.MaxValue;
			Collider[] array = Physics.OverlapSphere(_hiveAt, 3f);
			foreach (Collider collider in array)
			{
				ItemDrop itemDrop2 = ((collider != null) ? collider.GetComponentInParent<ItemDrop>() : null);
				if (!(itemDrop2 == null) && itemDrop2.m_itemData != null && !(itemDrop2.m_itemData.m_shared.m_name != _honey) && !_dropsBefore.Contains(itemDrop2.GetInstanceID()) && inventory.CanAddItem(itemDrop2.m_itemData, 1))
				{
					float num2 = Vector3.Distance(position, itemDrop2.transform.position);
					if (num2 < num)
					{
						num = num2;
						itemDrop = itemDrop2;
					}
				}
			}
			if (itemDrop == null)
			{
				if (CompanionsPlugin.LogNormal)
				{
					Log("Picked up the honey - putting it away");
				}
				Done(null);
			}
			else if (num > 1.5f)
			{
				MHR_Nav.Move(_ai, Time.deltaTime, itemDrop.transform.position, 1f, run: false, "Upkeep");
			}
			else if (!itemDrop.CanPickup())
			{
				itemDrop.RequestOwn();
				_ai.StopMoving();
			}
			else
			{
				int stack = itemDrop.m_itemData.m_stack;
				if (_humanoid.Pickup(itemDrop.gameObject, autoequip: false, autoPickupDelay: false))
				{
					Took(_honey, stack);
				}
			}
		}
	}

	private bool PlanPiles()
	{
		if (ModConfig.UpkeepPiles != null && !ModConfig.UpkeepPiles.Value)
		{
			return false;
		}
		if (Hammer() == null)
		{
			return false;
		}
		MHR_List mHR_List = MHR_List.Get(ModConfig.UpkeepPileItems?.Value ?? "*");
		int dflt = ModConfig.UpkeepPileKeep?.Value ?? 100;
		List<Container> list = Chests();
		foreach (PileKind item in PileKinds())
		{
			if ((_pileNoSpot.TryGetValue(item.Item, out var value) && Time.time < value) || !mHR_List.Allows(item.Item, MHR_List.Local(item.Shared)))
			{
				continue;
			}
			int num = mHR_List.AmountFor(item.Item, dflt);
			int num2 = 0;
			Container container = null;
			int num3 = 0;
			foreach (Container item2 in list)
			{
				int num4 = item2.GetInventory().CountItems(item.Shared);
				num2 += num4;
				if (num4 > num3)
				{
					num3 = num4;
					container = item2;
				}
			}
			_carried.TryGetValue(item.Shared, out var value2);
			if (num2 + value2 < num && value2 == 0 && PlanTakeDown(item, num2, num, list))
			{
				return true;
			}
			int num5 = (num2 - num) / Mathf.Max(1, item.Cost);
			if (num5 <= 0 || container == null)
			{
				continue;
			}
			float num6 = WeightOf(container, item.Shared);
			int a = ((num6 > 0f) ? Mathf.FloorToInt((CompanionTierData.CarryLimit(this) - _humanoid.GetInventory().GetTotalWeight()) / (num6 * (float)item.Cost)) : num5);
			int num7 = Mathf.Min(num5, Mathf.Min(a, num3 / item.Cost)) * item.Cost;
			if (num7 >= item.Cost)
			{
				_pile = item;
				_chest = container;
				_key = item.Shared;
				_amount = num7;
				_pileHasSpot = false;
				if (CompanionsPlugin.LogNormal)
				{
					Log("Stacking " + num7 + " " + MHR_List.Local(item.Shared) + " into " + num7 / item.Cost + " " + MHR_List.Local(item.Piles[0].m_name) + "(s) next to the existing ones (" + num2 + " in the chests, keeping " + num + ")");
				}
				Enter(Step.PileTake);
				return true;
			}
		}
		return false;
	}

	private static float WeightOf(Container c, string shared)
	{
		foreach (ItemDrop.ItemData allItem in c.GetInventory().GetAllItems())
		{
			if (allItem.m_shared.m_name == shared)
			{
				return allItem.m_shared.m_weight;
			}
		}
		return 0f;
	}

	private List<PileKind> PileKinds()
	{
		Dictionary<string, PileKind> dictionary = new Dictionary<string, PileKind>();
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(Center(), Range(), s_pieces);
		foreach (Piece s_piece in s_pieces)
		{
			if (!IsPile(s_piece))
			{
				continue;
			}
			Piece.Requirement requirement = s_piece.m_resources[0];
			string text = requirement.m_resItem.gameObject.name;
			if (!dictionary.TryGetValue(text, out var value))
			{
				GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(s_piece.gameObject)) : null);
				if (gameObject == null)
				{
					continue;
				}
				value = (dictionary[text] = new PileKind
				{
					Item = text,
					Shared = requirement.m_resItem.m_itemData.m_shared.m_name,
					Cost = Mathf.Max(1, requirement.m_amount),
					Prefab = gameObject
				});
			}
			value.Piles.Add(s_piece);
		}
		return new List<PileKind>(dictionary.Values);
	}

	internal static bool IsPile(Piece p)
	{
		if (p == null || !p.IsPlacedByPlayer() || p.m_resources == null || p.m_resources.Length != 1 || p.m_resources[0].m_resItem == null)
		{
			return false;
		}
		string text = Utils.GetPrefabName(p.gameObject).ToLowerInvariant();
		if (!text.Contains("stack") && !text.Contains("pile"))
		{
			return false;
		}
		if (p.GetComponentInChildren<Container>() == null && p.GetComponentInChildren<Fireplace>() == null && p.GetComponentInChildren<CraftingStation>() == null && p.GetComponentInChildren<Bed>() == null)
		{
			return p.GetComponentInChildren<Door>() == null;
		}
		return false;
	}

	private void PileTaken(Container c, Inventory inv, Inventory bag)
	{
		int num = Move(inv, bag, _key, byName: true, _amount);
		Took(_key, num);
		CloseChest(c);
		PileKind pile = _pile;
		if (pile == null || num < pile.Cost)
		{
			Done("Not enough " + MHR_List.Local(_key) + " for a pile after all (" + num + ")");
			return;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug("Took " + num + " " + MHR_List.Local(_key) + " for piles");
		}
		Done(null);
		_pile = pile;
		_pileHasSpot = false;
		Enter(Step.PileBuild);
	}

	private void UpdatePileBuild()
	{
		PileKind pile = _pile;
		Inventory inventory = _humanoid.GetInventory();
		if (pile == null || inventory.CountItems(pile.Shared) < pile.Cost)
		{
			_pile = null;
			Done(null);
			return;
		}
		ItemDrop.ItemData itemData = Hammer();
		if (itemData == null)
		{
			_pile = null;
			Done("No working hammer - no piles for now");
			return;
		}
		if (!_pileHasSpot)
		{
			if (!PileSpot(pile))
			{
				_pileNoSpot[pile.Item] = Time.time + 600f;
				if (CompanionsPlugin.LogNormal)
				{
					Log("No free spot next to the existing " + MHR_List.Local(pile.Shared) + " piles - trying again in 10 minutes");
				}
				_pile = null;
				Done(null);
				return;
			}
			_pileHasSpot = true;
			_stepStart = Time.time;
			_arrived = false;
			_arrivedAt = 0f;
			_pileStand = (MHR_Chests.PickApproachPoint(transform.position, _pileSpot, _pileNextTo, out var spot) ? spot : _pileSpot);
		}
		Vector3 position = transform.position;
		Vector3 point = _pileSpot + Vector3.up * 0.4f;
		if (!PointInReach(position, point, _pileNextTo) || Vector3.Distance(position, _pileSpot) > Reach)
		{
			bool flag = Time.time - _stepStart > GiveUp;
			_arrived = MHR_Nav.Move(_ai, Time.deltaTime, _pileStand, (_pileStand == _pileSpot) ? 1.5f : 0.6f, run: true, "Upkeep");
			if (_arrived && _arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			if (flag || MHR_Mover.GaveUp(_ai) || (_arrived && Time.time - _arrivedAt > 3f))
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Can't get to the pile spot " + MHR_Mover.Fmt(_pileSpot) + " - trying another");
				}
				_pileBadSpots.Add(Cell(_pileSpot));
				_pileHasSpot = false;
				MHR_Mover.Release(_ai);
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(itemData))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(itemData);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(point);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(point, 15f))
		{
			return;
		}
		float num = SwingStamina(itemData);
		if (_stamina != null && _stamina.Stamina < num)
		{
			return;
		}
		Piece component = pile.Prefab.GetComponent<Piece>();
		if (!SpotFree(pile, _pileSpot, _pileRot))
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
		}
		else if ((!(ZoneSystem.instance != null) || !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) && component != null && component.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(component.m_craftingStation.m_name, _pileSpot) == null)
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("No " + MHR_List.Local(component.m_craftingStation.m_name) + " in range of " + MHR_Mover.Fmt(_pileSpot));
			}
		}
		else if (!PrivateArea.CheckAccess(_pileSpot, 0f, flash: false))
		{
			_pileBadSpots.Add(Cell(_pileSpot));
			_pileHasSpot = false;
		}
		else
		{
			_nextAct = Time.time + WorkTime;
			PlacePile(pile, itemData, num);
			_pileHasSpot = false;
		}
	}

	private void PlacePile(PileKind k, ItemDrop.ItemData hammer, float stamina)
	{
		Inventory inventory = _humanoid.GetInventory();
		TerrainModifier.SetTriggerOnPlaced(trigger: true);
		GameObject gameObject = UnityEngine.Object.Instantiate(k.Prefab, _pileSpot, _pileRot);
		TerrainModifier.SetTriggerOnPlaced(trigger: false);
		Piece component = gameObject.GetComponent<Piece>();
		if (component != null)
		{
			PlatformUserID platformUserID = default;
			try
			{
				platformUserID = PlatformManager.DistributionPlatform.LocalUser.PlatformUserID;
			}
			catch
			{
			}
			component.SetCreator(MHR_Owner.CreatorId(this), platformUserID);
		}
		gameObject.GetComponent<WearNTear>()?.OnPlaced();
		string text = hammer.m_shared.m_attack?.m_attackAnimation;
		GetComponent<ZSyncAnimation>()?.SetTrigger((!string.IsNullOrEmpty(text)) ? text : "swing_hammer");
		if (component != null)
		{
			component.m_placeEffect.Create(_pileSpot, _pileRot, gameObject.transform, 1f, -1, _character.GetZDOID());
		}
		inventory.RemoveItem(k.Shared, k.Cost);
		Took(k.Shared, -k.Cost);
		_stamina?.UseStamina(stamina);
		if (hammer.m_shared.m_useDurability)
		{
			hammer.m_durability = Mathf.Max(0f, hammer.m_durability - hammer.m_shared.m_useDurabilityDrain * Game.m_durabilityRate);
		}
		if (component != null)
		{
			k.Piles.Add(component);
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log("Built a " + ((component != null) ? MHR_List.Local(component.m_name) : k.Prefab.name) + " at " + MHR_Mover.Fmt(_pileSpot) + " from " + k.Cost + " " + MHR_List.Local(k.Shared));
		}
	}

	private bool PileSpot(PileKind k)
	{
		Vector3 me = transform.position;
		List<Piece> list = new List<Piece>(k.Piles);
		list.RemoveAll((Piece p) => p == null);
		list.Sort((Piece a, Piece b) => Vector3.Distance(me, a.transform.position).CompareTo(Vector3.Distance(me, b.transform.position)));
		if (list.Count > 24)
		{
			list.RemoveRange(24, list.Count - 24);
		}
		float num = float.MinValue;
		bool flag = false;
		int num2 = 0;
		int num3 = 0;
		foreach (Piece item in list)
		{
			if (!LocalBox(item, out var center, out var half))
			{
				continue;
			}
			Vector3[] array = new Vector3[4]
			{
				new Vector3(half.x * 2f + 0.02f, 0f, 0f),
				new Vector3((0f - half.x) * 2f - 0.02f, 0f, 0f),
				new Vector3(0f, 0f, half.z * 2f + 0.02f),
				new Vector3(0f, 0f, (0f - half.z) * 2f - 0.02f)
			};
			for (int num4 = 0; num4 < array.Length; num4++)
			{
				Vector3 vector = item.transform.TransformPoint(array[num4]);
				Quaternion rotation = item.transform.rotation;
				if (_pileBadSpots.Contains(Cell(vector)) || !InArea(vector) || !SpotFree(k, vector, rotation) || !Supported(vector, rotation, center, half))
				{
					continue;
				}
				if (NearUsedThing(vector + rotation * center, half))
				{
					num3++;
					continue;
				}
				if (!SidesOk(item, vector, rotation, center, half, out var backed))
				{
					num2++;
					continue;
				}
				float num5 = (float)backed * 2f;
				num5 -= Vector3.Distance(vector, list[0].transform.position) * 0.05f;
				if (num5 > num)
				{
					num = num5;
					_pileSpot = vector;
					_pileRot = rotation;
					_pileNextTo = item;
					flag = true;
				}
			}
		}
		if (!flag && num2 + num3 > 0)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Every spot beside the " + MHR_List.Local(k.Shared) + " piles would be in the way (" + num2 + " in a walkway, " + num3 + " by a door, station, port or chest)");
			}
		}
		else if (flag && CompanionsPlugin.LogDetailed)
		{
			LogDebug("Pile spot " + MHR_Mover.Fmt(_pileSpot) + " (score " + num.ToString("F1") + ")");
		}
		return flag;
	}

	private static float GroundBelow(Vector3 p)
	{
		if (Physics.Raycast(p + Vector3.up * 0.2f, Vector3.down, out var hitInfo, 10f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return hitInfo.point.y;
		}
		return p.y;
	}

	private static bool SidesOk(Piece neighbour, Vector3 pos, Quaternion rot, Vector3 center, Vector3 half, out int backed)
	{
		backed = 0;
		Vector3 vector = pos + rot * center;
		Vector3[] array = new Vector3[4]
		{
			rot * Vector3.right,
			rot * Vector3.left,
			rot * Vector3.forward,
			rot * Vector3.back
		};
		float[] array2 = new float[4] { half.x, half.x, half.z, half.z };
		int mask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain", "vehicle");
		for (int i = 0; i < 4; i++)
		{
			float num = float.MaxValue;
			float[] array3 = new float[2] { 0.4f, 1.2f };
			foreach (float num2 in array3)
			{
				if (Physics.Raycast(new Vector3(vector.x, pos.y + num2, vector.z) + array[i] * (array2[i] + 0.01f), array[i], out var hitInfo, 1.6f, mask) && !hitInfo.collider.isTrigger)
				{
					Piece componentInParent = hitInfo.collider.GetComponentInParent<Piece>();
					num = ((!(componentInParent != null) || !(componentInParent == neighbour)) ? Mathf.Min(num, hitInfo.distance) : 0f);
				}
			}
			if (num <= 0.1f)
			{
				backed++;
			}
			else if (num < 1.6f)
			{
				return false;
			}
		}
		return true;
	}

	private static bool NearUsedThing(Vector3 mid, Vector3 half)
	{
		float num = Mathf.Max(half.x, half.z) + 1.6f;
		Collider[] array = Physics.OverlapSphere(mid, num + 1f, LayerMask.GetMask("Default", "piece", "piece_nonsolid", "static_solid", "vehicle"));
		foreach (Collider collider in array)
		{
			if (!(collider == null) && UsePoint(collider, out var at, out var need) && Vector3.Distance(new Vector3(at.x, mid.y, at.z), mid) < Mathf.Max(half.x, half.z) + need)
			{
				return true;
			}
		}
		return false;
	}

	private static bool UsePoint(Collider c, out Vector3 at, out float need)
	{
		at = c.transform.position;
		need = 0f;
		Smelter componentInParent = c.GetComponentInParent<Smelter>();
		if (componentInParent != null)
		{
			float num = float.MaxValue;
			Switch[] array = new Switch[3] { componentInParent.m_addOreSwitch, componentInParent.m_addWoodSwitch, componentInParent.m_emptyOreSwitch };
			foreach (Switch obj in array)
			{
				if (!(obj == null))
				{
					float num2 = Vector3.Distance(c.ClosestPoint(obj.transform.position), obj.transform.position);
					if (num2 < num)
					{
						num = num2;
						at = obj.transform.position;
					}
				}
			}
			need = 1.6f;
			return num < float.MaxValue;
		}
		if (c.GetComponentInParent<Door>() != null || c.GetComponentInParent<TeleportWorld>() != null)
		{
			at = ((c.GetComponentInParent<Piece>() != null) ? c.GetComponentInParent<Piece>().transform.position : at);
			need = 2f;
			return true;
		}
		Component component = (Component)(c.GetComponentInParent<Bed>() ?? c.GetComponentInParent<Chair>() ?? c.GetComponentInParent<CraftingStation>() ?? c.GetComponentInParent<CookingStation>() ?? c.GetComponentInParent<Fermenter>() ?? ((object)c.GetComponentInParent<Beehive>()) ?? ((object)c.GetComponentInParent<Fireplace>()));
		if (component != null)
		{
			at = component.transform.position;
			need = 1.4f;
			return true;
		}
		Container componentInParent2 = c.GetComponentInParent<Container>();
		if (componentInParent2 != null && componentInParent2.GetComponentInParent<Character>() == null)
		{
			at = componentInParent2.transform.position;
			need = 1f;
			return true;
		}
		return false;
	}

	private bool InArea(Vector3 p)
	{
		return Vector3.Distance(Center(), p) <= Range();
	}

	private static Vector3Int Cell(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x * 2f), Mathf.RoundToInt(p.y * 2f), Mathf.RoundToInt(p.z * 2f));
	}

	private static bool LocalBox(Piece pile, out Vector3 center, out Vector3 half)
	{
		center = Vector3.zero;
		half = Vector3.zero;
		Transform transform = pile.transform;
		bool flag = false;
		Bounds bounds = default;
		Collider[] componentsInChildren = pile.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (collider == null || !collider.enabled || collider.isTrigger)
			{
				continue;
			}
			Bounds bounds2;
			if (collider is BoxCollider boxCollider)
			{
				bounds2 = new Bounds(boxCollider.center, boxCollider.size);
			}
			else
			{
				if (!(collider is MeshCollider meshCollider) || !(meshCollider.sharedMesh != null))
				{
					continue;
				}
				bounds2 = meshCollider.sharedMesh.bounds;
			}
			for (int j = 0; j < 8; j++)
			{
				Vector3 position = bounds2.center + Vector3.Scale(bounds2.extents, new Vector3(((j & 1) == 0) ? (-1f) : 1f, ((j & 2) == 0) ? (-1f) : 1f, ((j & 4) == 0) ? (-1f) : 1f));
				Vector3 vector = transform.InverseTransformPoint(collider.transform.TransformPoint(position));
				if (!flag)
				{
					bounds = new Bounds(vector, Vector3.zero);
					flag = true;
				}
				else
				{
					bounds.Encapsulate(vector);
				}
			}
		}
		if (!flag || bounds.size.x < 0.2f || bounds.size.z < 0.2f)
		{
			return false;
		}
		center = bounds.center;
		half = bounds.extents;
		return true;
	}

	private static int BuildMask()
	{
		if (s_buildMask == -1)
		{
			s_buildMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid", "vehicle", "character", "character_net", "character_noenv");
		}
		return s_buildMask;
	}

	private static bool SpotFree(PileKind k, Vector3 pos, Quaternion rot)
	{
		if (k.Prefab.GetComponent<Piece>() == null || !LocalBoxOf(k, out var center, out var half))
		{
			return false;
		}
		Collider[] array = Physics.OverlapBox(halfExtents: new Vector3(half.x * 0.9f, half.y * 0.8f, half.z * 0.9f), center: pos + rot * (center + Vector3.up * half.y * 0.15f), orientation: rot, layerMask: BuildMask());
		foreach (Collider collider in array)
		{
			if (collider != null && !collider.isTrigger)
			{
				return false;
			}
		}
		return true;
	}

	private static bool LocalBoxOf(PileKind k, out Vector3 center, out Vector3 half)
	{
		if (s_prefabBox.TryGetValue(k.Item, out var value))
		{
			center = value.Key;
			half = value.Value;
			return true;
		}
		center = Vector3.zero;
		half = Vector3.zero;
		foreach (Piece pile in k.Piles)
		{
			if (pile != null && LocalBox(pile, out center, out half))
			{
				s_prefabBox[k.Item] = new KeyValuePair<Vector3, Vector3>(center, half);
				return true;
			}
		}
		return false;
	}

	private static bool Supported(Vector3 pos, Quaternion rot, Vector3 center, Vector3 half)
	{
		Vector3 vector = pos + rot * new Vector3(center.x, center.y - half.y, center.z);
		if (!Physics.Raycast(vector + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 1f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return false;
		}
		if (IsPile(hitInfo.collider.GetComponentInParent<Piece>()))
		{
			return false;
		}
		return Mathf.Abs(hitInfo.point.y - vector.y) <= 0.3f;
	}

	private bool PlanTakeDown(PileKind k, int total, int keep, List<Container> chests)
	{
		bool flag = false;
		foreach (Container chest in chests)
		{
			if (RoomInByName(chest, k.Shared))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return false;
		}
		Vector3 position = transform.position;
		Piece piece = null;
		float num = float.MaxValue;
		foreach (Piece pile in k.Piles)
		{
			if (!(pile == null) && !Skipped(pile) && pile.m_canBeRemoved && PrivateArea.CheckAccess(pile.transform.position, 0f, flash: false))
			{
				float num2 = Vector3.Distance(position, pile.transform.position) - (pile.transform.position.y - GroundBelow(pile.transform.position + Vector3.down * 0.05f)) * 20f;
				if (num2 < num)
				{
					num = num2;
					piece = pile;
				}
			}
		}
		if (piece == null)
		{
			return false;
		}
		_pile = k;
		_downPile = piece;
		if (CompanionsPlugin.LogNormal)
		{
			Log("Chests are down to " + total + " " + MHR_List.Local(k.Shared) + " (keeping " + keep + ") - taking down the " + MHR_List.Local(piece.m_name) + " at " + MHR_Mover.Fmt(piece.transform.position));
		}
		Enter(Step.PileDown);
		return true;
	}

	private static bool RoomInByName(Container c, string shared)
	{
		Inventory inventory = c.GetInventory();
		if (inventory.GetEmptySlots() > 0)
		{
			return true;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem.m_shared.m_name == shared && allItem.m_stack < allItem.m_shared.m_maxStackSize)
			{
				return true;
			}
		}
		return false;
	}

	private void UpdatePileDown()
	{
		PileKind pile = _pile;
		Piece downPile = _downPile;
		if (pile == null || downPile == null || downPile.GetComponent<ZNetView>() == null || !downPile.GetComponent<ZNetView>().IsValid())
		{
			_pile = null;
			Done("The pile is gone");
			return;
		}
		ItemDrop.ItemData itemData = Hammer();
		if (itemData == null)
		{
			_pile = null;
			Done("No working hammer - can't take a pile down");
			return;
		}
		Vector3 position = transform.position;
		WearNTear component = downPile.GetComponent<WearNTear>();
		Vector3 aim = downPile.transform.position + Vector3.up * 0.4f;
		if (!((component != null) ? PieceInReach(position, component, out aim) : PointInReach(position, aim, downPile)))
		{
			if (Time.time - _stepStart > GiveUp)
			{
				_pile = null;
				Skip(downPile, 600f, "Couldn't get within reach of the pile to take it down");
			}
			else if (!Approach(downPile.transform.position, downPile))
			{
				_pile = null;
				Skip(downPile, 600f, "No way to get to the pile");
			}
			else if (!_arrived)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = Time.time;
			}
			else if (Time.time - _arrivedAt > 3f)
			{
				_pile = null;
				Skip(downPile, 600f, "Can't reach the pile from where it can stand");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		if (!_humanoid.IsItemEquiped(itemData))
		{
			_setup.SuppressAutoEquip = true;
			_holdingHammer = true;
			_humanoid.EquipItem(itemData);
			_nextAct = Time.time + 0.6f;
			return;
		}
		_ai.LookAtPoint(aim);
		if (Time.time < _nextAct || _character.InAttack() || !_ai.IsLookingAtPoint(aim, 15f))
		{
			return;
		}
		if ((!(ZoneSystem.instance != null) || !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) && downPile.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(downPile.m_craftingStation.m_name, position) == null)
		{
			_pile = null;
			Skip(downPile, 600f, "No " + MHR_List.Local(downPile.m_craftingStation.m_name) + " in range to take the pile down");
			return;
		}
		if (!downPile.CanBeRemoved())
		{
			_pile = null;
			Skip(downPile, 120f, "The pile can't be taken down right now");
			return;
		}
		Vector3 position2 = downPile.transform.position;
		_pileDropsBefore = MHR_Chests.DropsNear(position2, 4f);
		ZNetView component2 = downPile.GetComponent<ZNetView>();
		if (!component2.IsOwner())
		{
			component2.ClaimOwnership();
		}
		downPile.GetComponent<IRemoved>()?.OnRemoved();
		if (component != null)
		{
			component.Remove();
		}
		else
		{
			downPile.DropResources();
			downPile.m_placeEffect.Create(position2, downPile.transform.rotation, downPile.transform, 1f, -1, _character.GetZDOID());
			ZNetScene.instance.Destroy(downPile.gameObject);
		}
		string text = itemData.m_shared.m_attack?.m_attackAnimation;
		GetComponent<ZSyncAnimation>()?.SetTrigger((!string.IsNullOrEmpty(text)) ? text : "swing_hammer");
		pile.Piles.Remove(downPile);
		if (CompanionsPlugin.LogNormal)
		{
			Log("Took down the " + MHR_List.Local(downPile.m_name) + " at " + MHR_Mover.Fmt(position2));
		}
		_downAt = Time.time;
		_spot = position2;
		_downPile = null;
		Enter(Step.PileCollect);
		_spot = position2;
	}

	private void UpdatePileCollect()
	{
		PileKind pile = _pile;
		if (pile == null || Time.time - _stepStart > 30f)
		{
			_pile = null;
			Done(null);
		}
		else
		{
			if (Time.time - _downAt < 0.6f)
			{
				return;
			}
			Inventory inventory = _humanoid.GetInventory();
			Vector3 position = transform.position;
			ItemDrop itemDrop = null;
			float num = float.MaxValue;
			Collider[] array = Physics.OverlapSphere(_spot, 4f);
			foreach (Collider collider in array)
			{
				ItemDrop itemDrop2 = ((collider != null) ? collider.GetComponentInParent<ItemDrop>() : null);
				if (itemDrop2 != null && itemDrop2.m_itemData != null && itemDrop2.m_itemData.m_shared.m_name == pile.Shared && !_pileDropsBefore.Contains(itemDrop2.GetInstanceID()) && inventory.CanAddItem(itemDrop2.m_itemData, 1))
				{
					float num2 = Vector3.Distance(position, itemDrop2.transform.position);
					if (num2 < num)
					{
						num = num2;
						itemDrop = itemDrop2;
					}
					continue;
				}
				Container container = ((collider != null) ? collider.GetComponentInParent<Container>() : null);
				if (container != null && container.GetComponent<Piece>() == null && container.GetComponentInParent<Character>() == null && container.GetInventory() != null && container.GetInventory().CountItems(pile.Shared) > 0 && Vector3.Distance(position, container.transform.position) < 2.5f)
				{
					ZNetView component = container.GetComponent<ZNetView>();
					if (component != null && !component.IsOwner())
					{
						component.ClaimOwnership();
					}
					GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
					int n = Move(container.GetInventory(), inventory, pile.Shared, byName: true, int.MaxValue);
					Took(pile.Shared, n);
					if (CompanionsPlugin.LogDetailed)
					{
						LogDebug("Took " + n + " " + MHR_List.Local(pile.Shared) + " out of what the pile left");
					}
					return;
				}
			}
			if (itemDrop == null)
			{
				_pile = null;
				Done(null);
				return;
			}
			if (num > 1.5f)
			{
				MHR_Nav.Move(_ai, Time.deltaTime, itemDrop.transform.position, 1f, run: false, "Upkeep");
				return;
			}
			if (!itemDrop.CanPickup())
			{
				itemDrop.RequestOwn();
				_ai.StopMoving();
				return;
			}
			int stack = itemDrop.m_itemData.m_stack;
			if (_humanoid.Pickup(itemDrop.gameObject, autoequip: false, autoPickupDelay: false))
			{
				Took(pile.Shared, stack);
			}
		}
	}
}
