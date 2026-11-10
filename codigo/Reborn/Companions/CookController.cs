using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CookController : MonoBehaviour
{
	internal enum CookPhase
	{
		Idle,
		Fetching,
		MovingToStation,
		Crafting,
		Cooking,
		Fermenting,
		CollectingDrops,
		Storing,
		TakingPile
	}

	private enum TaskKind
	{
		None,
		Craft,
		Grill,
		Rescue,
		Fill,
		Tap,
		Store,
		Hive,
		Stoke,
		Pile
	}

	private class Task
	{
		public TaskKind Kind;

		public Recipe Recipe;

		public CraftingStation Station;

		public CookingStation Cooker;

		public Fermenter Fermenter;

		public Beehive Hive;

		public Fireplace Fire;

		public Piece Pile;

		public string Item;

		public int Count;

		public string Final;

		public readonly List<KeyValuePair<string, int>> Fetch = new List<KeyValuePair<string, int>>();

		public string Describe()
		{
			return Kind switch
			{
				TaskKind.Craft => "make " + Count + "x " + Name(Recipe?.m_item?.gameObject.name) + " at the " + Loc(Station?.m_name), 
				TaskKind.Grill => "cook " + Count + "x " + Name(Item) + " on the " + Loc(Cooker?.m_name), 
				TaskKind.Rescue => "take finished food off the " + Loc(Cooker?.m_name), 
				TaskKind.Fill => "fill a fermenter with " + Name(Item), 
				TaskKind.Tap => "tap a fermenter", 
				TaskKind.Store => "put away " + Name(Item), 
				TaskKind.Hive => "collect honey from a beehive", 
				TaskKind.Stoke => "add " + Count + "x " + Name(Item) + " to the " + Loc((Fire != null) ? Fire.m_name : Cooker?.m_name), 
				TaskKind.Pile => "take down a pile for " + Name(Item), 
				_ => "nothing", 
			};
		}
	}

	private class Why
	{
		public bool Mead;

		public string Key;

		public object[] Args;
	}

	private CompanionAI _ai;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionSetup _setup;

	private ZNetView _nview;

	private ZSyncAnimation _zanim;

	private CompanionTalk _talk;

	private CookPhase _phase;

	private Task _task;

	private float _scanTimer;

	private float _timer;

	private float _phaseStart;

	private float _nextWatchLog;

	private int _fetchIndex;

	private Container _chest;

	private bool _chestOpen;

	private Vector3 _cookArea;

	private bool _hasCookArea;

	private bool _allDoneNotified;

	private readonly List<Container> _chests = new List<Container>();

	private readonly List<CraftingStation> _stations = new List<CraftingStation>();

	private readonly List<CookingStation> _cookers = new List<CookingStation>();

	private readonly List<Fermenter> _fermenters = new List<Fermenter>();

	private readonly List<Beehive> _hives = new List<Beehive>();

	private readonly List<Piece> _pieces = new List<Piece>();

	private readonly HashSet<int> _unreachable = new HashSet<int>();

	private float _unreachableClearAt;

	private readonly Dictionary<int, float> _stationProblemUntil = new Dictionary<int, float>();

	private readonly Dictionary<string, float> _skipUntil = new Dictionary<string, float>();

	private readonly Dictionary<string, int> _ledger = new Dictionary<string, int>();

	private bool _ledgerLoaded;

	private string _approachKey;

	private Vector3 _approachSpot;

	private bool _hasApproach;

	private float _stuckTimer;

	private static readonly HashSet<int> s_claimed = new HashSet<int>();

	private int _claimedId;

	private static readonly Collider[] s_drops = new Collider[48];

	private string _hiveDrop;

	private static int s_itemMask = -1;

	private static readonly int LedgerHash = "HC_CookLedger".GetStableHashCode();

	private static readonly FieldInfo s_haveFire = AccessTools.Field(typeof(CraftingStation), "m_haveFire");

	private static readonly MethodInfo s_isFireLit = AccessTools.Method(typeof(CookingStation), "IsFireLit");

	private const float ScanInterval = 4f;

	private const float UseReach = 2.5f;

	private readonly Dictionary<string, string> _brewWhy = new Dictionary<string, string>();

	private readonly List<Why> _whys = new List<Why>();

	private bool _foodWanted;

	private bool _foodShortSaid;

	private const int MaxWhys = 8;

	private ItemDrop _honeyTarget;

	private float _honeyTargetSince;

	private readonly HashSet<int> _honeySkip = new HashSet<int>();

	private int _honeyHave;

	private string _stuckLeg;

	private MHR_PileTaker _pileTaker;

	private readonly Dictionary<int, float> _pileSkip = new Dictionary<int, float>();

	internal CookPhase Phase => _phase;

	public bool IsActive => _phase != CookPhase.Idle;

	private static float Range => ModConfig.CookRange?.Value ?? 25f;

	private static int Reserve => ModConfig.CookIngredientReserve?.Value ?? 20;

	private static int FoodTarget => ModConfig.CookFoodTarget?.Value ?? 20;

	private static int BaseTarget => ModConfig.CookMeadBaseTarget?.Value ?? 10;

	private static int Batch => ModConfig.CookBatchSize?.Value ?? 5;

	private static int MeadTarget => ModConfig.CookMeadTarget?.Value ?? 20;

	private Inventory Bag => _humanoid?.GetInventory();

	private static float FuelLevel => ModConfig.CookFuelLevel?.Value ?? 0.5f;

	public void NotifyActionModeChanged()
	{
		if (_phase != CookPhase.Idle)
		{
			Abort("mode changed");
		}
	}

	internal bool IsCookMode()
	{
		ZDO zDO = _nview?.GetZDO();
		if (zDO != null)
		{
			return zDO.GetInt(CompanionSetup.ActionModeHash) == 17;
		}
		return false;
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
	}

	private void OnDestroy()
	{
		Unclaim();
		CloseChest();
	}

	private void Update()
	{
		if (CompanionRest.MHR_ShouldPauseWork(this) || _nview == null || _nview.GetZDO() == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f || MHR_Loot.Active(this) || MHR_JobRules.OrderPending(this))
		{
			return;
		}
		if (!_ledgerLoaded)
		{
			LoadLedger();
		}
		if ((_ai != null && _ai.IsInCombat) || CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			if (_phase != CookPhase.Idle)
			{
				Abort("interrupted");
			}
			return;
		}
		if (!IsCookMode())
		{
			if (_phase != CookPhase.Idle)
			{
				Abort("mode changed");
			}
			return;
		}
		float deltaTime = Time.deltaTime;
		try
		{
			switch (_phase)
			{
			case CookPhase.Idle:
				UpdateIdle(deltaTime);
				break;
			case CookPhase.Fetching:
				UpdateFetching(deltaTime);
				break;
			case CookPhase.MovingToStation:
				UpdateMovingToStation(deltaTime);
				break;
			case CookPhase.Crafting:
				UpdateCrafting(deltaTime);
				break;
			case CookPhase.Cooking:
				UpdateCooking(deltaTime);
				break;
			case CookPhase.Fermenting:
				UpdateFermenting(deltaTime);
				break;
			case CookPhase.CollectingDrops:
				UpdateCollecting(deltaTime);
				break;
			case CookPhase.Storing:
				UpdateStoring(deltaTime);
				break;
			case CookPhase.TakingPile:
				UpdateTakingPile();
				break;
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] " + ex.Message + " - starting over");
			Abort("error");
		}
	}

	private void UpdateIdle(float dt)
	{
		_scanTimer -= dt;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = 4f;
		CookCatalog.Ensure();
		Scan();
		_whys.Clear();
		_foodWanted = false;
		_foodShortSaid = false;
		Task task = PickTask();
		if (task == null)
		{
			ShowWhy();
			if (!_allDoneNotified)
			{
				_allDoneNotified = true;
				if (CompanionsPlugin.LogNormal)
				{
					Log("Nothing to cook right now");
				}
				RestoreFollow();
			}
			_scanTimer = 10f;
		}
		else
		{
			_allDoneNotified = false;
			Begin(task);
		}
	}

	private Task PickTask()
	{
		foreach (CookingStation cooker in _cookers)
		{
			if (CountSlots(cooker, 1) + CountSlots(cooker, 2) > 0 && Usable(cooker))
			{
				return new Task
				{
					Kind = TaskKind.Rescue,
					Cooker = cooker
				};
			}
		}
		foreach (Fermenter fermenter in _fermenters)
		{
			if (!s_claimed.Contains(fermenter.GetInstanceID()) && FermenterState(fermenter) == 2 && Usable(fermenter))
			{
				return new Task
				{
					Kind = TaskKind.Tap,
					Fermenter = fermenter
				};
			}
		}
		Task task = HiveTask(idle: false);
		if (task != null)
		{
			return task;
		}
		Task task2 = StokeTask();
		if (task2 != null)
		{
			return task2;
		}
		int num = LedgerCarried();
		if (num >= 10)
		{
			Task task3 = StoreTask();
			if (task3 != null)
			{
				return task3;
			}
		}
		bool flag = ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value;
		Task task4 = (flag ? (FillTask() ?? BrewTask(focus: true)) : null);
		if (task4 != null)
		{
			return task4;
		}
		Task task5 = FoodTask();
		if (task5 != null)
		{
			return task5;
		}
		Task task6 = (flag ? BrewTask(focus: false) : null);
		if (task6 != null)
		{
			return task6;
		}
		return ((num > 0) ? StoreTask() : null) ?? HiveTask(idle: true);
	}

	private static int HoneyIn(Beehive hive)
	{
		return (hive.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level)).GetValueOrDefault();
	}

	private Task HiveTask(bool idle)
	{
		bool flag = false;
		if (!idle && (ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value))
		{
			foreach (string item in CookCatalog.Focus())
			{
				Recipe recipe = CookCatalog.RecipeFor(item);
				if (recipe == null)
				{
					continue;
				}
				Piece.Requirement[] resources = recipe.m_resources;
				foreach (Piece.Requirement requirement in resources)
				{
					if (requirement?.m_resItem != null && requirement.m_resItem.gameObject.name == "Honey" && Usable("Honey") < requirement.GetAmount(1))
					{
						flag = true;
					}
				}
			}
		}
		Beehive beehive = null;
		float num = float.MaxValue;
		foreach (Beehive hive in _hives)
		{
			if (hive == null || !Usable(hive) || Skipped(TaskKind.Hive, hive.GetInstanceID().ToString()))
			{
				continue;
			}
			int num2 = HoneyIn(hive);
			if (num2 > 0 && (idle || flag || num2 >= hive.m_maxHoney) && (!idle || num2 * 2 >= hive.m_maxHoney))
			{
				float num3 = Vector3.Distance(transform.position, hive.transform.position);
				if (num3 < num)
				{
					beehive = hive;
					num = num3;
				}
			}
		}
		if (!(beehive != null))
		{
			return null;
		}
		return new Task
		{
			Kind = TaskKind.Hive,
			Hive = beehive,
			Item = beehive.GetInstanceID().ToString()
		};
	}

	private bool Skipped(TaskKind kind, string item)
	{
		if (_skipUntil.TryGetValue(kind.ToString() + ":" + item, out var value))
		{
			return Time.time < value;
		}
		return false;
	}

	private Task FoodTask()
	{
		Task result = null;
		float num = 0f;
		foreach (Recipe recipe in CookCatalog.Recipes)
		{
			string text = recipe.m_item.gameObject.name;
			string text2 = CookCatalog.FinalFood(text);
			if (text2 == null || CookCatalog.IsMeadBase(text) || !CookCatalog.Allowed(text2) || !Known(recipe) || Skipped(TaskKind.Craft, text))
			{
				continue;
			}
			float num2 = CookCatalog.Value(text2);
			if (num2 <= num)
			{
				continue;
			}
			int num3 = Count(text2) + ((text2 != text) ? Count(text) : 0);
			int num4 = CookCatalog.Target(text2, FoodTarget);
			if (num3 >= num4)
			{
				continue;
			}
			_foodWanted = true;
			if (text2 != text && FindCooker(text, needFree: false) == null)
			{
				continue;
			}
			CraftingStation craftingStation = FindStation(recipe);
			if (!(craftingStation == null))
			{
				if (!IngredientsOk(recipe))
				{
					FoodShort(recipe, text2);
					continue;
				}
				result = CraftTask(recipe, craftingStation, text2, num4 - num3);
				num = num2;
			}
		}
		foreach (CookingStation cooker in _cookers)
		{
			if (!Usable(cooker) || CountSlots(cooker, -1) == 0 || !HasHeat(cooker, orFuelInReach: true))
			{
				continue;
			}
			foreach (CookingStation.ItemConversion item2 in cooker.m_conversion)
			{
				if (item2?.m_from == null || item2.m_to == null)
				{
					continue;
				}
				string item = item2.m_from.gameObject.name;
				string text3 = item2.m_to.gameObject.name;
				if (!CookCatalog.Allowed(text3) || Skipped(TaskKind.Grill, item))
				{
					continue;
				}
				float num5 = CookCatalog.Value(text3) + (CookCatalog.IsMade(item) ? 0.5f : 0f);
				if (num5 <= num)
				{
					continue;
				}
				int num6 = CookCatalog.Target(text3, FoodTarget) - Count(text3);
				int b = Usable(item);
				if (num6 > 0)
				{
					_foodWanted = true;
				}
				int num7 = Mathf.Min(Mathf.Min(CountSlots(cooker, -1), Batch), Mathf.Min(num6, b));
				if (num7 <= 0)
				{
					continue;
				}
				Task task = new Task
				{
					Kind = TaskKind.Grill,
					Cooker = cooker,
					Item = item,
					Final = text3,
					Count = num7
				};
				AddFetch(task, item, num7);
				if (cooker.m_useFuel && cooker.m_fuelItem != null)
				{
					int num8 = Mathf.Max(0, FuelTarget(cooker.m_maxFuel) - Mathf.FloorToInt(Fuel(cooker)));
					int num9 = Count(cooker.m_fuelItem.gameObject.name);
					if (num8 > 0 && num9 > 0)
					{
						AddFetch(task, cooker.m_fuelItem.gameObject.name, Mathf.Min(num8, num9));
					}
				}
				result = task;
				num = num5;
			}
		}
		return result;
	}

	private void BrewWhy(string mead, string why)
	{
		if (CookCatalog.Focus().Contains(mead) && (!_brewWhy.TryGetValue(mead, out var value) || value != why))
		{
			_brewWhy[mead] = why;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Mead base " + Name(mead) + ": " + why);
			}
		}
	}

	private Task BrewTask(bool focus)
	{
		List<string> list = CookCatalog.Focus();
		foreach (string item in focus ? list : CookCatalog.MeadBases)
		{
			if (!focus && list.Contains(item))
			{
				continue;
			}
			Recipe recipe = CookCatalog.RecipeFor(item);
			if (recipe == null)
			{
				BrewWhy(item, "no recipe for it");
			}
			else if (!Known(recipe))
			{
				BrewWhy(item, "you don't know the recipe yet");
			}
			else
			{
				if (Skipped(TaskKind.Craft, item))
				{
					continue;
				}
				string text = CookCatalog.MeadOf(item);
				int num = Count(item);
				if (num >= BaseTarget)
				{
					BrewWhy(item, $"enough ({num} of {BaseTarget} bases)");
					continue;
				}
				int num2 = ((text != null) ? Count(text) : 0);
				if (text != null && num2 >= MeadTarget)
				{
					BrewWhy(item, $"enough {Name(text)} ({num2} of {MeadTarget})");
					continue;
				}
				CraftingStation craftingStation = FindStation(recipe);
				if (craftingStation == null)
				{
					BrewWhy(item, "no usable " + Loc(recipe.m_craftingStation?.m_name) + " in range (needs the right level, a fire and a roof)");
					AddWhy(true, "hc_why_cook_no_station", Loc(recipe.m_craftingStation?.m_name));
					continue;
				}
				string text2 = ShortIngredient(recipe, item);
				if (text2 == null)
				{
					BrewWhy(item, "brewing");
					return CraftTask(recipe, craftingStation, item, BaseTarget - num);
				}
				BrewWhy(item, text2);
			}
		}
		return null;
	}

	private void AddWhy(bool mead, string key, params object[] args)
	{
		if (_whys.Count >= 8)
		{
			return;
		}
		foreach (Why why in _whys)
		{
			if (why.Key == key && why.Mead == mead && string.Join("|", why.Args) == string.Join("|", args))
			{
				return;
			}
		}
		_whys.Add(new Why
		{
			Mead = mead,
			Key = key,
			Args = args
		});
	}

	private void ShowWhy()
	{
		bool flag = ModConfig.CookMakeMead == null || ModConfig.CookMakeMead.Value;
		AddFoodWhy();
		if (_whys.Count == 0)
		{
			CompanionStatus.Why(this, "hc_why_cook_nothing", false);
			return;
		}
		List<CompanionStatus.Item> list = new List<CompanionStatus.Item>();
		foreach (Why why in _whys)
		{
			list.Add(new CompanionStatus.Item(ModLocalization.Loc((why.Mead & flag) ? "hc_status_job_mead" : "hc_status_job_cook"), why.Key, why.Args));
		}
		CompanionStatus.WhyRotate(this, list);
	}

	private void ShowDoing(Task t)
	{
		string label = null;
		string text = null;
		string text2 = "";
		switch (t.Kind)
		{
		case TaskKind.Craft:
		{
			string text3 = t.Recipe?.m_item?.gameObject.name;
			label = ((text3 != null && CookCatalog.IsMeadBase(text3)) ? ModLocalization.Loc("hc_status_job_mead") : null);
			text = "hc_doing_cook_make";
			text2 = Name(text3);
			break;
		}
		case TaskKind.Grill:
			text = "hc_doing_cook_make";
			text2 = Name(t.Final ?? t.Item);
			break;
		case TaskKind.Rescue:
			text = "hc_doing_cook_rescue";
			text2 = Loc(t.Cooker?.m_name);
			break;
		case TaskKind.Fill:
			label = ModLocalization.Loc("hc_status_job_mead");
			text = "hc_doing_mead_fill";
			text2 = Name(t.Item);
			break;
		case TaskKind.Tap:
			label = ModLocalization.Loc("hc_status_job_mead");
			text = "hc_doing_mead_tap";
			break;
		case TaskKind.Store:
			text = "hc_doing_cook_store";
			text2 = Name(t.Item);
			break;
		case TaskKind.Hive:
			text = "hc_doing_upkeep_honey";
			break;
		case TaskKind.Stoke:
			text = "hc_doing_upkeep_fuel";
			text2 = Loc((t.Fire != null) ? t.Fire.m_name : t.Cooker?.m_name);
			break;
		case TaskKind.Pile:
			text = "hc_doing_take_pile";
			text2 = Loc(t.Pile?.m_name);
			break;
		}
		if (text != null)
		{
			CompanionStatus.DoingAs(this, label, text, text2);
		}
	}

	private void AddFoodWhy()
	{
		if (!_foodShortSaid)
		{
			if (_whys.Count >= 8)
			{
				_whys.RemoveAt(_whys.Count - 1);
			}
			_whys.Add(new Why
			{
				Mead = false,
				Key = (_foodWanted ? "hc_why_cook_no_food" : "hc_why_cook_stocked"),
				Args = new object[0]
			});
		}
	}

	private void FoodShort(Recipe recipe, string final)
	{
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (!(requirement?.m_resItem == null) && requirement.GetAmount(1) > 0)
			{
				string text = requirement.m_resItem.gameObject.name;
				if (Usable(text) < requirement.GetAmount(1))
				{
					bool flag = CookCatalog.IsMade(text);
					AddWhy(false, "hc_why_mead_short", Name(text), Count(text), requirement.GetAmount(1) + ((!flag) ? Reserve : 0), Name(final));
					_foodShortSaid = true;
					break;
				}
			}
		}
	}

	private string ShortIngredient(Recipe recipe, string mead)
	{
		string text = null;
		string text2 = Name(CookCatalog.MeadOf(mead) ?? mead);
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (requirement?.m_resItem == null || requirement.GetAmount(1) <= 0)
			{
				continue;
			}
			string text3 = requirement.m_resItem.gameObject.name;
			if (Usable(text3) < requirement.GetAmount(1))
			{
				bool flag = CookCatalog.IsMade(text3);
				int num = requirement.GetAmount(1) + ((!flag) ? Reserve : 0);
				AddWhy(true, "hc_why_mead_short", Name(text3), Count(text3), num, text2);
				if (text == null)
				{
					text = (flag ? $"needs {requirement.GetAmount(1)} {Name(text3)}, {Count(text3)} in reach" : $"needs {requirement.GetAmount(1)} {Name(text3)}, {Count(text3)} in reach and {Reserve} are kept back");
				}
			}
		}
		return text;
	}

	private Task FillTask()
	{
		List<string> list = new List<string>(CookCatalog.Focus());
		foreach (string meadBasis in CookCatalog.MeadBases)
		{
			if (!list.Contains(meadBasis))
			{
				list.Add(meadBasis);
			}
		}
		foreach (string item in list)
		{
			string text = CookCatalog.MeadOf(item);
			if (text == null || Count(text) >= MeadTarget || Count(item) <= 0 || Skipped(TaskKind.Fill, item))
			{
				continue;
			}
			foreach (Fermenter fermenter in _fermenters)
			{
				if (!s_claimed.Contains(fermenter.GetInstanceID()) && FermenterState(fermenter) == 0 && Usable(fermenter) && Accepts(fermenter, item))
				{
					Task task = new Task
					{
						Kind = TaskKind.Fill,
						Fermenter = fermenter,
						Item = item
					};
					AddFetch(task, item, 1);
					return task;
				}
			}
		}
		return null;
	}

	private Task CraftTask(Recipe recipe, CraftingStation station, string final, int room)
	{
		int num = Mathf.Max(1, recipe.m_amount);
		int num2 = Mathf.Min(Batch, Mathf.Max(1, (room + num - 1) / num));
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (requirement?.m_resItem != null && requirement.GetAmount(1) > 0)
			{
				num2 = Mathf.Min(num2, Usable(requirement.m_resItem.gameObject.name) / requirement.GetAmount(1));
			}
		}
		num2 = Mathf.Max(1, num2);
		Task task = new Task
		{
			Kind = TaskKind.Craft,
			Recipe = recipe,
			Station = station,
			Item = recipe.m_item.gameObject.name,
			Final = final,
			Count = num2
		};
		resources = recipe.m_resources;
		foreach (Piece.Requirement requirement2 in resources)
		{
			if (requirement2?.m_resItem != null && requirement2.GetAmount(1) > 0)
			{
				AddFetch(task, requirement2.m_resItem.gameObject.name, requirement2.GetAmount(1) * num2);
			}
		}
		return task;
	}

	private void AddFetch(Task task, string item, int amount)
	{
		int num = MHR_Items.CountItemInInventory(Bag, item);
		if (amount > num)
		{
			task.Fetch.Add(new KeyValuePair<string, int>(item, amount));
		}
	}

	private Task StoreTask()
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			if (item.Value > 0 && MHR_Items.CountItemInInventory(bag, item.Key) > 0 && StoreChest(item.Key) != null)
			{
				return new Task
				{
					Kind = TaskKind.Store,
					Item = item.Key
				};
			}
		}
		return null;
	}

	private void Begin(Task task)
	{
		_task = task;
		_fetchIndex = 0;
		_chest = null;
		_approachKey = null;
		if (_ai != null)
		{
			_ai.SetFollowTarget(null);
		}
		if (task.Fermenter != null)
		{
			Claim(task.Fermenter);
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log("Going to " + task.Describe());
		}
		ShowDoing(task);
		Say(task.Kind);
		if (task.Kind == TaskKind.Pile)
		{
			_pileTaker = new MHR_PileTaker(task.Pile, "Cook");
			Enter(CookPhase.TakingPile);
		}
		else if (task.Kind == TaskKind.Store)
		{
			_chest = StoreChest(task.Item);
			Enter(CookPhase.Storing);
		}
		else if (task.Fetch.Count > 0)
		{
			Enter(CookPhase.Fetching);
		}
		else
		{
			Enter(CookPhase.MovingToStation);
		}
	}

	private void Enter(CookPhase phase)
	{
		_phase = phase;
		_phaseStart = Time.time;
		_timer = 0f;
		_approachKey = null;
		ResetStuck();
	}

	private void UpdateFetching(float dt)
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			Abort("no inventory");
			return;
		}
		while (_fetchIndex < _task.Fetch.Count && MHR_Items.CountItemInInventory(bag, _task.Fetch[_fetchIndex].Key) >= _task.Fetch[_fetchIndex].Value)
		{
			_fetchIndex++;
			_chest = null;
		}
		if (_fetchIndex >= _task.Fetch.Count)
		{
			CloseChest();
			Enter(CookPhase.MovingToStation);
			return;
		}
		string key = _task.Fetch[_fetchIndex].Key;
		int num = _task.Fetch[_fetchIndex].Value - MHR_Items.CountItemInInventory(bag, key);
		if (_chest == null)
		{
			_chest = ChestWith(key);
			if (_chest == null)
			{
				Abort("no chest in reach has " + Name(key));
				return;
			}
		}
		if (!AtChest(_chest, dt, "chest with " + Name(key)) || !OpenChest(_chest, dt))
		{
			return;
		}
		Inventory inventory = _chest.GetInventory();
		int i;
		for (i = 0; i < num; i++)
		{
			ItemDrop.ItemData itemData = MHR_Items.FindItemByPrefab(inventory, key);
			if (itemData == null || !MHR_Items.TransferOne(inventory, bag, itemData))
			{
				break;
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log($"Took {i}x {Name(key)}");
		}
		CloseChest();
		if (i == 0)
		{
			_unreachable.Add(_chest.GetInstanceID());
		}
		_chest = null;
		_approachKey = null;
	}

	private void UpdateMovingToStation(float dt)
	{
		Component component = TaskTarget();
		if (component == null)
		{
			Abort("station gone");
			return;
		}
		Vector3 point = TaskPoint();
		if (!AtPoint(component, point, dt, Loc(TaskName())))
		{
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(component.transform.position);
		_cookArea = component.transform.position;
		_hasCookArea = true;
		switch (_task.Kind)
		{
		case TaskKind.Craft:
		{
			if (!StationReady(_task.Station, out var why))
			{
				_stationProblemUntil[_task.Station.GetInstanceID()] = Time.time + 60f;
				Abort("the " + Loc(_task.Station.m_name) + " " + why);
				break;
			}
			_task.Station.PokeInUse();
			_zanim?.SetTrigger("interact");
			_task.Station.m_craftItemEffects?.Create(_task.Station.transform.position, Quaternion.identity);
			Enter(CookPhase.Crafting);
			break;
		}
		case TaskKind.Grill:
			AddToCooker();
			Enter(CookPhase.Cooking);
			break;
		case TaskKind.Rescue:
			Enter(CookPhase.Cooking);
			break;
		case TaskKind.Stoke:
			Stoke();
			Finish();
			break;
		case TaskKind.Fill:
		case TaskKind.Tap:
			_zanim?.SetTrigger("interact");
			Enter(CookPhase.Fermenting);
			break;
		case TaskKind.Hive:
		{
			_zanim?.SetTrigger("interact");
			int num = HoneyIn(_task.Hive);
			_honeyHave = CountIn(Bag, (_task.Hive.m_honeyItem != null) ? _task.Hive.m_honeyItem.gameObject.name : "Honey");
			_honeySkip.Clear();
			_honeyTarget = null;
			if (num > 0)
			{
				_honeySkip.UnionWith(MHR_Chests.DropsNear((_task.Hive.m_spawnPoint != null) ? _task.Hive.m_spawnPoint.position : _task.Hive.transform.position, 4f));
				_task.Hive.GetComponent<ZNetView>()?.InvokeRPC("RPC_Extract");
				if (CompanionsPlugin.LogNormal)
				{
					Log(string.Format("Collected {0}x {1} from the beehive", num, Name("Honey")));
				}
			}
			Enter(CookPhase.CollectingDrops);
			break;
		}
		default:
			Finish();
			break;
		}
	}

	private void UpdateCrafting(float dt)
	{
		CraftingStation station = _task.Station;
		if (station == null)
		{
			Abort("station gone");
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(station.transform.position);
		station.PokeInUse();
		_timer += dt;
		if (_timer < 2.5f)
		{
			return;
		}
		Inventory bag = Bag;
		Recipe recipe = _task.Recipe;
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (requirement?.m_resItem != null && MHR_Items.CountItemInInventory(bag, requirement.m_resItem.gameObject.name) < requirement.GetAmount(1))
			{
				Abort("missing " + Name(requirement.m_resItem.gameObject.name));
				return;
			}
		}
		resources = recipe.m_resources;
		foreach (Piece.Requirement requirement2 in resources)
		{
			if (requirement2?.m_resItem != null)
			{
				for (int j = 0; j < requirement2.GetAmount(1); j++)
				{
					MHR_Items.ConsumeOneFromInventory(bag, requirement2.m_resItem.gameObject.name);
				}
			}
		}
		string text = recipe.m_item.gameObject.name;
		int num = Mathf.Max(1, recipe.m_amount);
		if (bag.AddItem(text, num, 1, 0, 0L, "", cheated: false) == null)
		{
			GameObject gameObject = ObjectDB.instance?.GetItemPrefab(text);
			if (gameObject != null)
			{
				ItemDrop component = UnityEngine.Object.Instantiate(gameObject, transform.position + Vector3.up * 0.5f, Quaternion.identity).GetComponent<ItemDrop>();
				if (component != null)
				{
					component.m_itemData.m_stack = num;
				}
			}
			if (CompanionsPlugin.LogNormal)
			{
				Log("Bag full - dropped " + Name(text));
			}
		}
		AddLedger(text, num);
		station.m_craftItemDoneEffects?.Create(station.transform.position, Quaternion.identity);
		if (CompanionsPlugin.LogNormal)
		{
			Log($"Made {num}x {Name(text)} at the {Loc(station.m_name)}");
		}
		_task.Count--;
		if (_task.Count > 0 && CanCraftAgain(recipe))
		{
			_timer = 0.5f;
			_zanim?.SetTrigger("interact");
			station.m_craftItemEffects?.Create(station.transform.position, Quaternion.identity);
			return;
		}
		if (CookCatalog.IsMeadBase(text))
		{
			Task task = FillTask();
			if (task != null && task.Item == text)
			{
				Begin(task);
				return;
			}
		}
		Finish();
	}

	private bool CanCraftAgain(Recipe recipe)
	{
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (requirement?.m_resItem != null && MHR_Items.CountItemInInventory(Bag, requirement.m_resItem.gameObject.name) < requirement.GetAmount(1))
			{
				return false;
			}
		}
		return true;
	}

	private void AddToCooker()
	{
		CookingStation cooker = _task.Cooker;
		Inventory bag = Bag;
		ZNetView component = cooker.GetComponent<ZNetView>();
		if (bag == null || component == null || !component.IsValid())
		{
			return;
		}
		if (cooker.m_useFuel && cooker.m_fuelItem != null)
		{
			string text = cooker.m_fuelItem.gameObject.name;
			int i;
			for (i = 0; Fuel(cooker) + (float)i < (float)FuelTarget(cooker.m_maxFuel); i++)
			{
				if (!MHR_Items.ConsumeOneFromInventory(bag, text))
				{
					break;
				}
				component.InvokeRPC("RPC_AddFuel");
			}
			if (i > 0 && CompanionsPlugin.LogNormal)
			{
				Log($"Added {i}x {Name(text)} to the {Loc(cooker.m_name)}");
			}
		}
		int j = 0;
		for (int num = CountSlots(cooker, -1); j < _task.Count && j < num; j++)
		{
			if (!MHR_Items.ConsumeOneFromInventory(bag, _task.Item))
			{
				break;
			}
			component.InvokeRPC("RPC_AddItem", _task.Item, false);
		}
		_zanim?.SetTrigger("interact");
		if (CompanionsPlugin.LogNormal)
		{
			Log($"Put {j}x {Name(_task.Item)} on the {Loc(cooker.m_name)}");
		}
	}

	private void UpdateCooking(float dt)
	{
		CookingStation cooker = _task.Cooker;
		if (cooker == null)
		{
			Abort("cooking station gone");
			return;
		}
		ZNetView component = cooker.GetComponent<ZNetView>();
		_ai?.StopMoving();
		_ai?.LookAtPoint(cooker.transform.position);
		_timer -= dt;
		if (_timer > 0f)
		{
			return;
		}
		_timer = 0.5f;
		PickUpDrops(DropPoint(cooker), 4f, ledger: true);
		if (CountSlots(cooker, 1) + CountSlots(cooker, 2) > 0)
		{
			component?.InvokeRPC("RPC_RemoveDoneItem", transform.position, 1);
			_zanim?.SetTrigger("interact");
			_timer = 0.8f;
		}
		else if (_task.Kind == TaskKind.Grill && CountCooking(cooker, _task.Item) > 0 && Time.time - _phaseStart < 240f)
		{
			if (Time.time >= _nextWatchLog)
			{
				_nextWatchLog = Time.time + 20f;
				if (CompanionsPlugin.LogNormal)
				{
					Log("Waiting for the " + Loc(cooker.m_name) + " (" + CountCooking(cooker, _task.Item) + " cooking)");
				}
			}
		}
		else
		{
			PickUpDrops(DropPoint(cooker), 4f, ledger: true);
			Finish();
		}
	}

	private void UpdateFermenting(float dt)
	{
		Fermenter fermenter = _task.Fermenter;
		if (fermenter == null)
		{
			Abort("fermenter gone");
			return;
		}
		_ai?.StopMoving();
		_ai?.LookAtPoint(fermenter.transform.position);
		_timer += dt;
		if (_timer < 0.6f)
		{
			return;
		}
		ZNetView component = fermenter.GetComponent<ZNetView>();
		if (_task.Kind == TaskKind.Fill)
		{
			if (FermenterState(fermenter) == 0 && MHR_Items.ConsumeOneFromInventory(Bag, _task.Item))
			{
				component?.InvokeRPC("RPC_AddItem", _task.Item.GetStableHashCode(), false);
				TakeFromLedger(_task.Item, 1);
				if (CompanionsPlugin.LogNormal)
				{
					Log("Put " + Name(_task.Item) + " in the fermenter");
				}
			}
			Finish();
			return;
		}
		if (FermenterState(fermenter) == 2)
		{
			component?.InvokeRPC("RPC_Tap");
			if (CompanionsPlugin.LogNormal)
			{
				Log("Tapped the fermenter");
			}
		}
		Enter(CookPhase.CollectingDrops);
	}

	private void UpdateCollecting(float dt)
	{
		_timer += dt;
		if (!(_timer < 2f))
		{
			Fermenter fermenter = _task.Fermenter;
			Vector3 center = ((fermenter != null && fermenter.m_outputPoint != null) ? fermenter.m_outputPoint.position : transform.position);
			if (_task.Hive != null)
			{
				UpdateHoneyPickup(dt);
				return;
			}
			_hiveDrop = null;
			PickUpDrops(center, 4f, ledger: true);
			Finish();
		}
	}

	private void UpdateHoneyPickup(float dt)
	{
		Beehive hive = _task.Hive;
		Vector3 center = ((hive.m_spawnPoint != null) ? hive.m_spawnPoint.position : hive.transform.position);
		_hiveDrop = ((hive.m_honeyItem != null) ? hive.m_honeyItem.gameObject.name : "Honey");
		PickUpDrops(transform.position + Vector3.up * 0.5f, 1.8f, ledger: false);
		int num = CountIn(Bag, _hiveDrop);
		if (num > _honeyHave)
		{
			AddLedger(_hiveDrop, num - _honeyHave);
			if (CompanionsPlugin.LogNormal)
			{
				Log($"Picked up {num - _honeyHave}x {Name(_hiveDrop)}");
			}
		}
		_honeyHave = num;
		if (_timer > 30f)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Leaving the rest of the honey - took too long to get to it");
			}
			EndHoney();
			return;
		}
		if (_honeyTarget != null)
		{
			ZNetView component = _honeyTarget.GetComponent<ZNetView>();
			if (component == null || !component.IsValid())
			{
				_honeyTarget = null;
			}
			else if (Time.time - _honeyTargetSince > 10f || (_ai != null && MHR_Mover.GaveUp(_ai)))
			{
				if (CompanionsPlugin.LogNormal)
				{
					Log($"Can't get to the honey at {_honeyTarget.transform.position:F1} - leaving it");
				}
				_honeySkip.Add(_honeyTarget.GetInstanceID());
				_honeyTarget = null;
				if (_ai != null)
				{
					MHR_Mover.Release(_ai);
				}
			}
		}
		if (_honeyTarget == null)
		{
			_honeyTarget = NearestHoney(center, 10f);
			_honeyTargetSince = Time.time;
			if (_honeyTarget == null)
			{
				EndHoney();
				return;
			}
		}
		MHR_Nav.Move(_ai, dt, _honeyTarget.transform.position, 0.6f, run: false, "Cook");
	}

	private static int CountIn(Inventory inv, string prefab)
	{
		int num = 0;
		if (inv != null && prefab != null)
		{
			foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
			{
				if (allItem?.m_dropPrefab != null && allItem.m_dropPrefab.name == prefab)
				{
					num += allItem.m_stack;
				}
			}
		}
		return num;
	}

	internal bool IsWorkFood(ItemDrop.ItemData item)
	{
		string text = item?.m_dropPrefab?.name;
		if (text == null)
		{
			return false;
		}
		if (!_ledgerLoaded)
		{
			LoadLedger();
		}
		int num = LedgerOf(text);
		if (num > 0)
		{
			return CountIn(Bag, text) <= num;
		}
		return false;
	}

	private void EndHoney()
	{
		_hiveDrop = null;
		_honeyTarget = null;
		_honeySkip.Clear();
		_ai?.StopMoving();
		Finish();
	}

	private ItemDrop NearestHoney(Vector3 center, float radius)
	{
		if (s_itemMask < 0)
		{
			s_itemMask = LayerMask.GetMask("item");
		}
		Inventory bag = Bag;
		ItemDrop result = null;
		float num = float.MaxValue;
		int num2 = Physics.OverlapSphereNonAlloc(center, radius, s_drops, s_itemMask);
		for (int i = 0; i < num2; i++)
		{
			ItemDrop itemDrop = s_drops[i]?.GetComponentInParent<ItemDrop>();
			ZNetView zNetView = itemDrop?.GetComponent<ZNetView>();
			if (!(itemDrop == null) && itemDrop.m_itemData != null && !(zNetView == null) && zNetView.IsValid() && !_honeySkip.Contains(itemDrop.GetInstanceID()) && !((itemDrop.m_itemData.m_dropPrefab?.name ?? Utils.GetPrefabName(itemDrop.gameObject)) != _hiveDrop) && bag != null && bag.CanAddItem(itemDrop.m_itemData, itemDrop.m_itemData.m_stack))
			{
				float num3 = Vector3.Distance(transform.position, itemDrop.transform.position);
				if (num3 < num)
				{
					num = num3;
					result = itemDrop;
				}
			}
		}
		return result;
	}

	private void UpdateStoring(float dt)
	{
		Inventory bag = Bag;
		if (_chest == null || bag == null)
		{
			Finish();
		}
		else
		{
			if (!AtChest(_chest, dt, "chest to store " + Name(_task.Item)) || !OpenChest(_chest, dt))
			{
				return;
			}
			Inventory inventory = _chest.GetInventory();
			int num = 0;
			List<string> list = new List<string> { _task.Item };
			foreach (string key in _ledger.Keys)
			{
				if (!list.Contains(key) && (HasItem(inventory, key) || key == _task.Item))
				{
					list.Add(key);
				}
			}
			foreach (string item in list)
			{
				int num2 = Mathf.Min(LedgerOf(item), MHR_Items.CountItemInInventory(bag, item));
				int i;
				for (i = 0; i < num2; i++)
				{
					ItemDrop.ItemData itemData = MHR_Items.FindItemByPrefab(bag, item);
					if (itemData == null || !inventory.CanAddItem(itemData, 1) || !MHR_Items.TransferOne(bag, inventory, itemData))
					{
						break;
					}
				}
				if (i > 0)
				{
					TakeFromLedger(item, i);
					num += i;
					if (CompanionsPlugin.LogNormal)
					{
						Log($"Put away {i}x {Name(item)}");
					}
				}
			}
			CloseChest();
			if (num == 0)
			{
				_unreachable.Add(_chest.GetInstanceID());
			}
			_chest = null;
			Finish();
		}
	}

	private Component TaskTarget()
	{
		switch (_task.Kind)
		{
		case TaskKind.Craft:
			return _task.Station;
		case TaskKind.Grill:
		case TaskKind.Rescue:
			return _task.Cooker;
		case TaskKind.Fill:
		case TaskKind.Tap:
			return _task.Fermenter;
		case TaskKind.Hive:
			return _task.Hive;
		case TaskKind.Stoke:
			if (!(_task.Fire != null))
			{
				return _task.Cooker;
			}
			return _task.Fire;
		default:
			return null;
		}
	}

	private string TaskName()
	{
		Component component = TaskTarget();
		if (component is CraftingStation craftingStation)
		{
			return craftingStation.m_name;
		}
		if (component is CookingStation cookingStation)
		{
			return cookingStation.m_name;
		}
		if (component is Beehive beehive)
		{
			return beehive.m_name;
		}
		if (component is Fireplace fireplace)
		{
			return fireplace.m_name;
		}
		return "$piece_fermenter";
	}

	private Vector3 TaskPoint()
	{
		if (_task.Cooker != null && (_task.Kind == TaskKind.Grill || _task.Kind == TaskKind.Rescue))
		{
			if (!(_task.Cooker.m_addFoodSwitch != null))
			{
				return _task.Cooker.transform.position;
			}
			return _task.Cooker.m_addFoodSwitch.transform.position;
		}
		if (_task.Kind == TaskKind.Stoke && _task.Cooker != null && _task.Cooker.m_addFuelSwitch != null)
		{
			return _task.Cooker.m_addFuelSwitch.transform.position;
		}
		if (_task.Fermenter != null && (_task.Kind == TaskKind.Fill || _task.Kind == TaskKind.Tap))
		{
			if (!(_task.Fermenter.m_outputPoint != null))
			{
				return _task.Fermenter.transform.position;
			}
			return _task.Fermenter.m_outputPoint.position;
		}
		return TaskTarget().transform.position;
	}

	private bool AtPoint(Component target, Vector3 point, float dt, string what)
	{
		string text = "p" + target.GetInstanceID();
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproachPoint(transform.position, point, target, out _approachSpot);
		}
		float num = MHR_Nav.ReachDist(transform.position, point);
		if (num < 2f || (_hasApproach && OnSpot(num)))
		{
			return true;
		}
		Vector3 point2 = (_hasApproach ? _approachSpot : point);
		MHR_Nav.Move(_ai, dt, point2, _hasApproach ? 0.5f : 2f, FarEnough(num), "Cook");
		CheckStuck(dt, num, what);
		return false;
	}

	private bool AtChest(Container chest, float dt, string what)
	{
		string text = "c" + chest.GetInstanceID();
		if (_approachKey != text)
		{
			_approachKey = text;
			_hasApproach = MHR_Chests.PickApproach(transform.position, chest, out _approachSpot);
			if (!_hasApproach && !MHR_Chests.CanReach(transform.position, chest))
			{
				_unreachable.Add(chest.GetInstanceID());
				if (CompanionsPlugin.LogNormal)
				{
					Log($"Skipping chest at {chest.transform.position:F1} - can't get within reach of it");
				}
				Abort("chest out of reach");
				return false;
			}
		}
		float num = MHR_Nav.ReachDist(transform.position, chest.transform.position);
		if (num < 2.5f || (_hasApproach && OnSpot(num)))
		{
			_ai?.StopMoving();
			return true;
		}
		Vector3 point = (_hasApproach ? _approachSpot : chest.transform.position);
		MHR_Nav.Move(_ai, dt, point, _hasApproach ? 0.5f : 2.5f, FarEnough(num), "Cook");
		CheckStuck(dt, num, what);
		return false;
	}

	private bool OnSpot(float dist)
	{
		Vector3 position = transform.position;
		if (dist <= 4f && new Vector2(position.x - _approachSpot.x, position.z - _approachSpot.z).magnitude <= 0.7f)
		{
			return Mathf.Abs(position.y - _approachSpot.y) <= 1.5f;
		}
		return false;
	}

	private static bool FarEnough(float dist)
	{
		float num = ModConfig.WorkRunDistance?.Value ?? 25f;
		if (num > 0f)
		{
			return dist > num;
		}
		return false;
	}

	private void ResetStuck()
	{
		_stuckTimer = 0f;
	}

	private void CheckStuck(float dt, float dist, string what)
	{
		if (what != _stuckLeg)
		{
			_stuckLeg = what;
			_stuckTimer = 0f;
		}
		_stuckTimer += dt;
		if ((_ai != null && MHR_Mover.GaveUp(_ai)) || _stuckTimer > 120f)
		{
			Abort("couldn't get to the " + what);
		}
	}

	private bool OpenChest(Container chest, float dt)
	{
		if (!_chestOpen && MHR_Chests.CompanionHeld(chest))
		{
			return false;
		}
		if (!_chestOpen && !MHR_Chests.Open(chest))
		{
			Abort("someone has the chest open");
			return false;
		}
		if (!_chestOpen)
		{
			_chestOpen = true;
			_zanim?.SetTrigger("interact");
			_ai?.LookAtPoint(chest.transform.position);
			_timer = 0.8f;
			return false;
		}
		_timer -= dt;
		return _timer <= 0f;
	}

	private void CloseChest()
	{
		if (_chestOpen && _chest != null)
		{
			_chest.SetInUse(inUse: false);
		}
		_chestOpen = false;
	}

	private Container ChestWith(string item)
	{
		List<Container> list = new List<Container>();
		foreach (Container chest in _chests)
		{
			if (chest != null && !chest.IsInUse() && !_unreachable.Contains(chest.GetInstanceID()) && HasItem(chest.GetInventory(), item))
			{
				list.Add(chest);
			}
		}
		Vector3 position = transform.position;
		list.Sort((Container a, Container b) => Vector3.Distance(position, a.transform.position).CompareTo(Vector3.Distance(position, b.transform.position)));
		foreach (Container item2 in list)
		{
			if (MHR_Chests.CanReach(position, item2))
			{
				return item2;
			}
			_unreachable.Add(item2.GetInstanceID());
			if (CompanionsPlugin.LogNormal)
			{
				Log($"Skipping chest at {item2.transform.position:F1} - can't get within reach of it");
			}
		}
		return null;
	}

	private Container StoreChest(string item)
	{
		ItemDrop.ItemData itemData = (ObjectDB.instance?.GetItemPrefab(item))?.GetComponent<ItemDrop>()?.m_itemData;
		if (itemData == null)
		{
			return null;
		}
		bool flag = ModConfig.CookStoreWithCookedFood?.Value ?? true;
		Vector3 a = (_hasCookArea ? _cookArea : transform.position);
		Container container = null;
		Container container2 = null;
		Container container3 = null;
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		float num3 = float.MaxValue;
		foreach (Container chest in _chests)
		{
			if (chest == null || chest.IsInUse() || _unreachable.Contains(chest.GetInstanceID()))
			{
				continue;
			}
			Inventory inventory = chest.GetInventory();
			if (inventory != null && inventory.CanAddItem(itemData, 1))
			{
				float num4 = Vector3.Distance(a, chest.transform.position);
				if (flag && HasItem(inventory, item) && num4 < num)
				{
					container = chest;
					num = num4;
				}
				else if (flag && HasCookedFood(inventory) && num4 < num2)
				{
					container2 = chest;
					num2 = num4;
				}
				else if (inventory.GetEmptySlots() > 0 && num4 < num3)
				{
					container3 = chest;
					num3 = num4;
				}
			}
		}
		return container ?? container2 ?? container3;
	}

	private static bool HasItem(Inventory inv, string item)
	{
		if (inv != null)
		{
			return MHR_Items.CountItemInInventory(inv, item) > 0;
		}
		return false;
	}

	private static bool HasCookedFood(Inventory inv)
	{
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			string text = allItem?.m_dropPrefab?.name;
			if (text != null && CookCatalog.IsMade(text))
			{
				return true;
			}
		}
		return false;
	}

	private int Count(string item)
	{
		int num = MHR_Items.CountItemInInventory(Bag, item);
		foreach (Container chest in _chests)
		{
			if (chest != null && !_unreachable.Contains(chest.GetInstanceID()))
			{
				num += MHR_Items.CountItemInInventory(chest.GetInventory(), item);
			}
		}
		return num;
	}

	private int Usable(string item)
	{
		int num = Count(item);
		if (!CookCatalog.IsMade(item))
		{
			return num - Reserve;
		}
		return num;
	}

	private bool IngredientsOk(Recipe recipe)
	{
		Piece.Requirement[] resources = recipe.m_resources;
		foreach (Piece.Requirement requirement in resources)
		{
			if (requirement?.m_resItem != null && requirement.GetAmount(1) > 0 && Usable(requirement.m_resItem.gameObject.name) < requirement.GetAmount(1))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Known(Recipe recipe)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return true;
		}
		string text = recipe.m_item?.m_itemData?.m_shared?.m_name;
		if (text != null)
		{
			return localPlayer.IsRecipeKnown(text);
		}
		return false;
	}

	private void Scan()
	{
		if (Time.time >= _unreachableClearAt)
		{
			_unreachable.Clear();
			_unreachableClearAt = Time.time + 60f;
		}
		_chests.Clear();
		_stations.Clear();
		_cookers.Clear();
		_fermenters.Clear();
		_hives.Clear();
		_pieces.Clear();
		Vector3 position = transform.position;
		MHR_Chests.AddHomeChests(_chests, _setup);
		Piece.GetAllPiecesInRadius(position, Range, _pieces);
		foreach (Piece piece in _pieces)
		{
			if (piece == null)
			{
				continue;
			}
			Container componentInChildren = piece.GetComponentInChildren<Container>();
			if (componentInChildren != null && componentInChildren.gameObject != gameObject && !_chests.Contains(componentInChildren) && MHR_Chests.Usable(componentInChildren, this))
			{
				_chests.Add(componentInChildren);
			}
			if (ApplianceOn(piece))
			{
				CookingStation component = piece.GetComponent<CookingStation>();
				if (component != null && component.GetComponent<ZNetView>()?.GetZDO() != null)
				{
					_cookers.Add(component);
				}
				Fermenter component2 = piece.GetComponent<Fermenter>();
				if (component2 != null && component2.GetComponent<ZNetView>()?.GetZDO() != null)
				{
					_fermenters.Add(component2);
				}
				CraftingStation component3 = piece.GetComponent<CraftingStation>();
				if (component3 != null && CookCatalog.IsCookingStation(component3.m_name))
				{
					_stations.Add(component3);
				}
			}
		}
		Vector3 p = position;
		float radius = Range;
		if (_setup != null && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
		{
			p = _setup.GetHomePosition();
			radius = MHR_ToolKeeper.HomeRange();
		}
		_pieces.Clear();
		Piece.GetAllPiecesInRadius(p, radius, _pieces);
		foreach (Piece piece2 in _pieces)
		{
			Beehive beehive = ((piece2 != null) ? piece2.GetComponent<Beehive>() : null);
			if (beehive != null && beehive.GetComponent<ZNetView>()?.GetZDO() != null && ApplianceOn(piece2))
			{
				_hives.Add(beehive);
			}
		}
	}

	private static bool ApplianceOn(Piece piece)
	{
		if (piece.GetComponent<CookingStation>() == null && piece.GetComponent<Fermenter>() == null && piece.GetComponent<Beehive>() == null && piece.GetComponent<CraftingStation>() == null)
		{
			return true;
		}
		string text = Utils.GetPrefabName(piece.gameObject).ToLowerInvariant();
		ConfigEntry<bool> configEntry;
		if (text.Contains("meadcauldron") || text.Contains("ketill"))
		{
			configEntry = ModConfig.CookMeadKetill;
		}
		else if (text.Contains("cauldron"))
		{
			configEntry = ModConfig.CookCauldron;
		}
		else if (text.Contains("preptable"))
		{
			configEntry = ModConfig.CookPrepTable;
		}
		else if (text.Contains("cookingstation_iron"))
		{
			configEntry = ModConfig.CookIronCookingStation;
		}
		else if (text.Contains("cookingstation"))
		{
			configEntry = ModConfig.CookCookingStation;
		}
		else if (text.Contains("oven"))
		{
			configEntry = ModConfig.CookOven;
		}
		else if (piece.GetComponent<Fermenter>() != null)
		{
			configEntry = ModConfig.CookFermenter;
		}
		else if (piece.GetComponent<Beehive>() != null)
		{
			configEntry = ModConfig.CookBeehives;
		}
		else
		{
			if (piece.GetComponent<CraftingStation>() != null && piece.GetComponent<CookingStation>() == null && !CookCatalog.IsCookingStation(piece.GetComponent<CraftingStation>().m_name))
			{
				return true;
			}
			configEntry = ModConfig.CookOtherStations;
		}
		return configEntry?.Value ?? true;
	}

	private bool Usable(Component c)
	{
		if (c == null || (_ai != null && _ai.IsPositionBlacklisted(c.transform.position)))
		{
			return false;
		}
		if ((c.GetComponent<Fermenter>() != null || c.GetComponent<Beehive>() != null) && !PrivateArea.CheckAccess(c.transform.position, 0f, flash: false))
		{
			return false;
		}
		if (_stationProblemUntil.TryGetValue(c.GetInstanceID(), out var value))
		{
			return Time.time >= value;
		}
		return true;
	}

	private CraftingStation FindStation(Recipe recipe)
	{
		CraftingStation result = null;
		float num = float.MaxValue;
		foreach (CraftingStation station in _stations)
		{
			if (!(station == null) && !(recipe.m_craftingStation == null) && !(station.m_name != recipe.m_craftingStation.m_name) && station.GetLevel() >= recipe.GetRequiredStationLevel(1) && Usable(station) && StationReady(station, out var _))
			{
				float num2 = Vector3.Distance(transform.position, station.transform.position);
				if (num2 < num)
				{
					result = station;
					num = num2;
				}
			}
		}
		return result;
	}

	private static bool StationReady(CraftingStation station, out string why)
	{
		why = "";
		if (station.m_craftRequireRoof && station.m_roofCheckPoint != null)
		{
			Cover.GetCoverForPoint(station.m_roofCheckPoint.position, out var coverPercentage, out var underRoof);
			if (!underRoof || coverPercentage < 0.7f)
			{
				why = "needs a roof";
				return false;
			}
		}
		if (station.m_craftRequireFire && s_haveFire != null && !(bool)s_haveFire.GetValue(station))
		{
			why = "needs a fire";
			return false;
		}
		return true;
	}

	private CookingStation FindCooker(string from, bool needFree)
	{
		foreach (CookingStation cooker in _cookers)
		{
			if (cooker == null || !Usable(cooker) || (needFree && CountSlots(cooker, -1) == 0))
			{
				continue;
			}
			foreach (CookingStation.ItemConversion item in cooker.m_conversion)
			{
				if (item?.m_from != null && item.m_from.gameObject.name == from)
				{
					return cooker;
				}
			}
		}
		return null;
	}

	private bool HasHeat(CookingStation cooker, bool orFuelInReach)
	{
		if (cooker.m_requireFire && s_isFireLit != null && (bool)s_isFireLit.Invoke(cooker, null))
		{
			return true;
		}
		if (cooker.m_useFuel)
		{
			if (!(Fuel(cooker) >= 1f))
			{
				if (orFuelInReach && cooker.m_fuelItem != null)
				{
					return Count(cooker.m_fuelItem.gameObject.name) > 0;
				}
				return false;
			}
			return true;
		}
		return !cooker.m_requireFire;
	}

	private static float Fuel(CookingStation cooker)
	{
		return (cooker.GetComponent<ZNetView>()?.GetZDO()?.GetFloat(ZDOVars.s_fuel)).GetValueOrDefault();
	}

	private static int CountSlots(CookingStation cooker, int status)
	{
		ZDO zDO = cooker.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || cooker.m_slots == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < cooker.m_slots.Length; i++)
		{
			string text = zDO.GetString("slot" + i);
			if (status < 0)
			{
				if (text == "")
				{
					num++;
				}
			}
			else if (text != "" && zDO.GetInt("slotstatus" + i) == status)
			{
				num++;
			}
		}
		return num;
	}

	private static int CountCooking(CookingStation cooker, string from)
	{
		ZDO zDO = cooker.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || cooker.m_slots == null)
		{
			return 0;
		}
		int num = 0;
		for (int i = 0; i < cooker.m_slots.Length; i++)
		{
			if (zDO.GetString("slot" + i) == from && zDO.GetInt("slotstatus" + i) == 0)
			{
				num++;
			}
		}
		return num;
	}

	private static Vector3 DropPoint(CookingStation cooker)
	{
		if (!(cooker.m_spawnPoint != null))
		{
			return cooker.transform.position;
		}
		return cooker.m_spawnPoint.position;
	}

	private static int FermenterState(Fermenter fermenter)
	{
		ZDO zDO = fermenter.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null || zDO.GetInt(ZDOVars.s_content) == 0)
		{
			return 0;
		}
		long num = zDO.GetLong(ZDOVars.s_startTime, 0L);
		if (num == 0L || ZNet.instance == null)
		{
			return 1;
		}
		if (!((ZNet.instance.GetTime() - new DateTime(num)).TotalSeconds > (double)fermenter.m_fermentationDuration))
		{
			return 1;
		}
		return 2;
	}

	private static bool Accepts(Fermenter fermenter, string item)
	{
		foreach (Fermenter.ItemConversion item2 in fermenter.m_conversion)
		{
			if (item2?.m_from != null && item2.m_from.gameObject.name == item)
			{
				return true;
			}
		}
		return false;
	}

	private void Claim(Fermenter f)
	{
		Unclaim();
		_claimedId = f.GetInstanceID();
		s_claimed.Add(_claimedId);
	}

	private void Unclaim()
	{
		if (_claimedId != 0)
		{
			s_claimed.Remove(_claimedId);
			_claimedId = 0;
		}
	}

	private void PickUpDrops(Vector3 center, float radius, bool ledger)
	{
		if (s_itemMask < 0)
		{
			s_itemMask = LayerMask.GetMask("item");
		}
		Inventory bag = Bag;
		if (bag == null)
		{
			return;
		}
		int num = Physics.OverlapSphereNonAlloc(center, radius, s_drops, s_itemMask);
		for (int i = 0; i < num; i++)
		{
			ItemDrop itemDrop = s_drops[i]?.GetComponentInParent<ItemDrop>();
			ZNetView zNetView = itemDrop?.GetComponent<ZNetView>();
			if (itemDrop == null || itemDrop.m_itemData == null || zNetView == null || zNetView.GetZDO() == null)
			{
				continue;
			}
			string text = itemDrop.m_itemData.m_dropPrefab?.name ?? Utils.GetPrefabName(itemDrop.gameObject);
			if ((!CookCatalog.IsMade(text) && !CookCatalog.IsMead(text) && text != "Coal" && text != _hiveDrop) || !bag.CanAddItem(itemDrop.m_itemData, itemDrop.m_itemData.m_stack))
			{
				continue;
			}
			if (!itemDrop.CanPickup(autoPickupDelay: false))
			{
				itemDrop.RequestOwn();
				continue;
			}
			int stack = itemDrop.m_itemData.m_stack;
			bag.AddItem(itemDrop.m_itemData);
			zNetView.ClaimOwnership();
			zNetView.Destroy();
			if (ledger)
			{
				AddLedger(text, stack);
			}
			if (CompanionsPlugin.LogNormal)
			{
				Log($"Picked up {stack}x {Name(text)}");
			}
		}
	}

	private void LoadLedger()
	{
		_ledgerLoaded = true;
		_ledger.Clear();
		string[] array = (_nview?.GetZDO()?.GetString(LedgerHash) ?? "").Split(new char[1] { ';' });
		foreach (string text in array)
		{
			int num = text.LastIndexOf(':');
			if (num > 0 && int.TryParse(text.Substring(num + 1), out var result) && result > 0)
			{
				_ledger[text.Substring(0, num)] = result;
			}
		}
	}

	private void SaveLedger()
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			if (item.Value > 0)
			{
				stringBuilder.Append(item.Key).Append(':').Append(item.Value)
					.Append(';');
			}
		}
		_nview?.GetZDO()?.Set(LedgerHash, stringBuilder.ToString());
	}

	private void AddLedger(string item, int n)
	{
		_ledger[item] = LedgerOf(item) + n;
		SaveLedger();
	}

	private void TakeFromLedger(string item, int n)
	{
		int num = LedgerOf(item) - n;
		if (num > 0)
		{
			_ledger[item] = num;
		}
		else
		{
			_ledger.Remove(item);
		}
		SaveLedger();
	}

	private int LedgerOf(string item)
	{
		if (!_ledger.TryGetValue(item, out var value))
		{
			return 0;
		}
		return value;
	}

	private int LedgerCarried()
	{
		Inventory bag = Bag;
		if (bag == null)
		{
			return 0;
		}
		int num = 0;
		List<string> list = null;
		foreach (KeyValuePair<string, int> item in _ledger)
		{
			int num2 = MHR_Items.CountItemInInventory(bag, item.Key);
			if (num2 < item.Value)
			{
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(item.Key);
			}
			num += Mathf.Min(num2, item.Value);
		}
		if (list != null)
		{
			foreach (string item2 in list)
			{
				int num3 = MHR_Items.CountItemInInventory(bag, item2);
				if (num3 > 0)
				{
					_ledger[item2] = num3;
				}
				else
				{
					_ledger.Remove(item2);
				}
			}
			SaveLedger();
		}
		return num;
	}

	private void Finish()
	{
		DropPileTaker();
		Unclaim();
		CloseChest();
		_task = null;
		_phase = CookPhase.Idle;
		_scanTimer = 1f;
	}

	private void Abort(string reason)
	{
		if (CompanionsPlugin.LogNormal)
		{
			Log("Stopped - " + reason + " (was " + (_task?.Describe() ?? "idle") + ")");
		}
		if (_task != null && !string.IsNullOrEmpty(_task.Item) && reason != "interrupted" && reason != "mode changed")
		{
			_skipUntil[_task.Kind.ToString() + ":" + _task.Item] = Time.time + 60f;
		}
		DropPileTaker();
		Unclaim();
		CloseChest();
		_task = null;
		_chest = null;
		_phase = CookPhase.Idle;
		_scanTimer = 5f;
	}

	private void RestoreFollow()
	{
		if (_setup != null && _ai != null && _setup.GetFollow() && MHR_Owner.Of(this) != null && _ai.GetFollowTarget() == null)
		{
			_ai.SetFollowTarget(MHR_Owner.Go(this));
		}
	}

	private void Say(TaskKind kind)
	{
		if (!(_talk == null))
		{
			string key;
			switch (kind)
			{
			default:
				return;
			case TaskKind.Craft:
			case TaskKind.Grill:
				key = "hc_speech_cook_cooking";
				break;
			case TaskKind.Fill:
				key = "hc_speech_cook_fermenter";
				break;
			case TaskKind.Tap:
				key = "hc_speech_cook_tapping";
				break;
			case TaskKind.Store:
				key = "hc_speech_cook_storing";
				break;
			case TaskKind.Rescue:
				return;
			}
			_talk.Say(ModLocalization.Loc(key));
		}
	}

	private void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Cook|" + (_character?.m_name ?? "?") + "] " + msg);
		}
	}

	internal static string Name(string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return "?";
		}
		string text = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
		if (text == null)
		{
			return prefab;
		}
		return Loc(text);
	}

	internal static string Loc(string token)
	{
		if (string.IsNullOrEmpty(token))
		{
			return "?";
		}
		return Localization.instance?.Localize(token) ?? token;
	}

	private static int FuelTarget(float max)
	{
		return Mathf.Max(1, Mathf.CeilToInt(max * FuelLevel));
	}

	private Task StokeTask()
	{
		Task result = null;
		float num = float.MaxValue;
		Vector3 position = transform.position;
		foreach (KeyValuePair<Component, string> item in HeatSources())
		{
			Component key = item.Key;
			float max = ((key is Fireplace fireplace) ? fireplace.m_maxFuel : ((float)((CookingStation)key).m_maxFuel));
			float valueOrDefault = (key.GetComponent<ZNetView>()?.GetZDO()?.GetFloat(ZDOVars.s_fuel)).GetValueOrDefault();
			int num2 = FuelTarget(max);
			if (valueOrDefault >= (float)num2 * 0.5f && valueOrDefault >= 1f)
			{
				continue;
			}
			string value = item.Value;
			if (Skipped(TaskKind.Stoke, value))
			{
				continue;
			}
			int num3 = num2 - Mathf.FloorToInt(valueOrDefault);
			if (num3 <= 0)
			{
				continue;
			}
			float num4 = Vector3.Distance(position, key.transform.position);
			if (num4 >= num)
			{
				continue;
			}
			int num5 = Count(value);
			Task task;
			if (num5 > 0)
			{
				task = new Task
				{
					Kind = TaskKind.Stoke,
					Fire = (key as Fireplace),
					Cooker = (key as CookingStation),
					Item = value,
					Count = Mathf.Min(num3, num5)
				};
				AddFetch(task, value, task.Count);
			}
			else
			{
				if (ModConfig.CookUsePiles != null && !ModConfig.CookUsePiles.Value)
				{
					continue;
				}
				Piece piece = MHR_PileTaker.Find(position, Range, value, position, _pileSkip);
				if (piece == null || MHR_PileTaker.Hammer(_humanoid) == null)
				{
					continue;
				}
				task = new Task
				{
					Kind = TaskKind.Pile,
					Pile = piece,
					Item = value
				};
			}
			result = task;
			num = num4;
		}
		return result;
	}

	private List<KeyValuePair<Component, string>> HeatSources()
	{
		List<KeyValuePair<Component, string>> list = new List<KeyValuePair<Component, string>>();
		HashSet<int> hashSet = new HashSet<int>();
		List<Vector3> list2 = new List<Vector3>();
		foreach (CookingStation cooker in _cookers)
		{
			if (cooker == null)
			{
				continue;
			}
			if (cooker.m_useFuel && cooker.m_fuelItem != null && cooker.m_maxFuel > 0)
			{
				if (hashSet.Add(cooker.GetInstanceID()))
				{
					list.Add(new KeyValuePair<Component, string>(cooker, cooker.m_fuelItem.gameObject.name));
				}
			}
			else if (cooker.m_requireFire)
			{
				list2.Add(cooker.transform.position);
			}
		}
		foreach (CraftingStation station in _stations)
		{
			if (station != null && station.m_craftRequireFire)
			{
				list2.Add(station.transform.position);
			}
		}
		List<Piece> list3 = new List<Piece>();
		foreach (Vector3 item in list2)
		{
			list3.Clear();
			Piece.GetAllPiecesInRadius(item, 2.5f, list3);
			foreach (Piece item2 in list3)
			{
				Fireplace fireplace = ((item2 != null) ? item2.GetComponent<Fireplace>() : null);
				if (!(fireplace == null) && fireplace.m_canRefill && !fireplace.m_infiniteFuel && !(fireplace.m_fuelItem == null) && !(fireplace.m_maxFuel <= 0f) && hashSet.Add(fireplace.GetInstanceID()))
				{
					list.Add(new KeyValuePair<Component, string>(fireplace, fireplace.m_fuelItem.gameObject.name));
				}
			}
		}
		return list;
	}

	private void Stoke()
	{
		Component component = ((_task.Fire != null) ? ((Component)_task.Fire) : ((Component)_task.Cooker));
		ZNetView zNetView = ((component != null) ? component.GetComponent<ZNetView>() : null);
		Inventory bag = Bag;
		if (zNetView == null || !zNetView.IsValid() || bag == null)
		{
			return;
		}
		if (!zNetView.HasOwner())
		{
			zNetView.ClaimOwnership();
		}
		int num = FuelTarget((_task.Fire != null) ? _task.Fire.m_maxFuel : ((float)_task.Cooker.m_maxFuel));
		int i;
		for (i = 0; zNetView.GetZDO().GetFloat(ZDOVars.s_fuel) + (float)i < (float)num; i++)
		{
			if (!MHR_Items.ConsumeOneFromInventory(bag, _task.Item))
			{
				break;
			}
			zNetView.InvokeRPC("RPC_AddFuel");
		}
		_zanim?.SetTrigger("interact");
		if (CompanionsPlugin.LogNormal)
		{
			Log($"Added {i}x {Name(_task.Item)} to the {Loc(TaskName())} (kept at {FuelLevel * 100f:F0}%)");
		}
	}

	private void UpdateTakingPile()
	{
		if (_pileTaker == null)
		{
			Finish();
			return;
		}
		int num = _pileTaker.Tick(_ai, _humanoid, _setup);
		if (num == 0)
		{
			return;
		}
		if (num < 0)
		{
			if (_pileTaker.Pile != null)
			{
				_pileSkip[_pileTaker.Pile.GetInstanceID()] = Time.time + 600f;
			}
			if (CompanionsPlugin.LogNormal)
			{
				Log("Gave up on the pile: " + _pileTaker.Why);
			}
		}
		else if (CompanionsPlugin.LogNormal)
		{
			Log("Took down a pile - picked up " + _pileTaker.Got + " " + MHR_List.Local(_pileTaker.Shared));
		}
		_ai?.StopMoving();
		MHR_Mover.Release(_ai);
		Finish();
		_scanTimer = 0.5f;
	}

	private void DropPileTaker()
	{
		if (_pileTaker != null)
		{
			_pileTaker.PutAway(_humanoid, _setup);
			_pileTaker = null;
		}
	}
}
