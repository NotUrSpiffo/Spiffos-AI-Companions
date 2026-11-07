using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class FishController : MonoBehaviour
{
	private enum FishState
	{
		Idle,
		Moving,
		Casting,
		Waiting,
		Reeling,
		GoingHome
	}

	private struct Shore
	{
		public Vector3 Stand;

		public Vector3 Water;

		public bool Wet;
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private CompanionSetup _setup;

	private Humanoid _humanoid;

	private Character _character;

	private CompanionTalk _talk;

	private FishState _state;

	private float _stateSince;

	private float _scanTimer;

	private float _lastSpeechTime;

	private float _nextWhyLog;

	private Vector3 _anchor;

	private bool _hasAnchor;

	private Fish _fish;

	private string _fishName;

	private Vector3 _stand;

	private string _bait;

	private float _castAt;

	private int _castTries;

	private bool _settled;

	private bool _retrieving;

	private float _hookTry;

	private Fish _hooked;

	private string _hookedName;

	private int _haveBefore;

	private float _castScale = 24f;

	private float _nextUnloadTry;

	private float _nextCount;

	private Transform _rodTop;

	private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	private static readonly AccessTools.FieldRef<Humanoid, float> s_drawTime = AccessTools.FieldRefAccess<Humanoid, float>("m_attackDrawTime");

	private readonly List<Shore> _shore = new List<Shore>();

	private float _shoreAt = -999f;

	private Vector3 _shoreFor;

	private const float WadeDepth = 0.8f;

	private readonly Dictionary<Vector3Int, int> _falls = new Dictionary<Vector3Int, int>();

	private readonly Dictionary<Vector3Int, float> _badSpot = new Dictionary<Vector3Int, float>();

	private float _nextRestLog;

	private const float MaxCast = 20f;

	private bool _foodLogged;

	private readonly List<CompanionStatus.Item> _whyItems = new List<CompanionStatus.Item>();

	private bool _rodOut;

	public bool IsActive
	{
		get
		{
			if (GetMode() == 9)
			{
				return _state != FishState.Idle;
			}
			return false;
		}
	}

	internal static string LastBait(Character c)
	{
		return ((c != null) ? c.GetComponent<FishController>() : null)?._bait;
	}

	public void NotifyActionModeChanged()
	{
		ReelOff();
		_state = FishState.Idle;
		_hasAnchor = false;
		_scanTimer = 0f;
		_lastSpeechTime = 0f;
		RestoreGear();
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
		_talk = GetComponent<CompanionTalk>();
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _ai == null || _humanoid == null || CompanionRest.MHR_ShouldPauseWork(this))
		{
			return;
		}
		if (GetMode() != 9)
		{
			if (_state != FishState.Idle || _rodOut)
			{
				ReelOff();
				_state = FishState.Idle;
				RestoreGear();
			}
			_hasAnchor = false;
			return;
		}
		if (!_hasAnchor)
		{
			_hasAnchor = true;
			_anchor = transform.position;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Fishing around " + MHR_Mover.Fmt(_anchor) + ", up to " + Radius().ToString("F0") + " m out");
			}
			_foodLogged = false;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			_ai.StopMoving();
		}
		else if (_combatAI != null && _combatAI.IsEngaged)
		{
			if (_state != FishState.Idle || _rodOut)
			{
				ReelOff();
				_state = FishState.Idle;
				RestoreGear();
			}
		}
		else if (!MHR_ToolKeeper.IsBusy(this) && !MHR_Loot.Active(this) && !MHR_JobRules.OrderPending(this))
		{
			switch (_state)
			{
			case FishState.Idle:
				UpdateIdle();
				break;
			case FishState.Moving:
				UpdateMoving();
				break;
			case FishState.Casting:
				UpdateCasting();
				break;
			case FishState.Waiting:
				UpdateWaiting();
				break;
			case FishState.Reeling:
				UpdateReeling();
				break;
			case FishState.GoingHome:
				UpdateGoingHome();
				break;
			}
		}
	}

	private static float Radius()
	{
		return ModConfig.FishScanRadius?.Value ?? 30f;
	}

	private void Enter(FishState s)
	{
		_state = s;
		_stateSince = Time.time;
	}

	private void UpdateIdle()
	{
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = 3f;
		FishCatalog.Ensure();
		if (FindRod() == null)
		{
			Say("hc_speech_fish_need_rod");
			return;
		}
		if (PackFull())
		{
			if (_setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
			{
				if (!(Time.time >= _nextUnloadTry))
				{
					return;
				}
				if (MHR_ToolKeeper.AtHome(_setup))
				{
					_nextUnloadTry = Time.time + 60f;
					if (CompanionsPlugin.LogNormal)
					{
						Log("Pack is full - putting the catch away");
					}
					MHR_ToolKeeper.RequestUnload(_setup);
					_nextCount = 0f;
				}
				else
				{
					if (CompanionsPlugin.LogNormal)
					{
						Log("Pack is full - taking the catch home");
					}
					MHR_Mover.Release(_ai);
					Enter(FishState.GoingHome);
				}
			}
			else
			{
				Say("hc_speech_fish_full");
				WhyLog("Pack is full and home is too far - not fishing until it's emptied");
			}
			return;
		}
		RefreshCounts();
		if (!PickTarget(out var why))
		{
			WhyLog("Nothing to fish (" + why + ")");
			if (_whyItems.Count == 0)
			{
				_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_none", Radius().ToString("F0")));
			}
			if (why.StartsWith("no bait"))
			{
				Say("hc_speech_fish_need_bait");
			}
			else if (why.StartsWith("no fish"))
			{
				Say("hc_speech_fish_no_water");
			}
			CompanionStatus.WhyRotate(this, _whyItems);
			return;
		}
		_castTries = 0;
		if (!_foodLogged)
		{
			_foodLogged = true;
			string text = MHR_ToolKeeper.StaminaFoodText(_humanoid.GetInventory());
			if (CompanionsPlugin.LogNormal)
			{
				Log((text != null) ? ("Stamina food for reeling: " + text) : "No stamina food in the pack");
			}
		}
		CompanionStatus.Clear(this);
		CompanionStatus.Doing(this, "hc_doing_fish", FishCatalog.Local(_fish));
		MHR_Mover.Release(_ai);
		Enter(FishState.Moving);
		if (CompanionsPlugin.LogNormal)
		{
			Log("Going to fish for " + _fishName + " from " + MHR_Mover.Fmt(_stand) + " with " + CookController.Name(_bait) + NeedText(_fishName));
		}
	}

	private bool PickTarget(out string why)
	{
		why = null;
		Inventory inventory = _humanoid.GetInventory();
		_ = transform.position;
		float num = Radius();
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		string text = null;
		string text2 = null;
		float num6 = float.MaxValue;
		Fish fish = null;
		Vector3 stand = Vector3.zero;
		string bait = null;
		_whyItems.Clear();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (IMonoUpdater instance in Fish.Instances)
		{
			Fish fish2 = instance as Fish;
			if (fish2 == null || fish2.IsHooked())
			{
				continue;
			}
			Vector3 position = fish2.transform.position;
			if (Flat(position, _anchor) > num || (_skip.TryGetValue(fish2.GetInstanceID(), out var value) && Time.time < value))
			{
				continue;
			}
			string text3 = FishCatalog.PrefabName(fish2.gameObject);
			if (!FishCatalog.Allowed(text3, fish2))
			{
				continue;
			}
			num2++;
			if (!Needs(text3))
			{
				num3++;
				if (hashSet.Add("enough:" + text3) && _whyItems.Count < 8)
				{
					_counts.TryGetValue(text3, out var value2);
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_at_target", FishCatalog.Local(fish2), value2, FishCatalog.Target(text3)));
				}
				continue;
			}
			string text4 = BestBait(fish2, inventory);
			Vector3 stand2;
			float cost;
			if (text4 == null)
			{
				num4++;
				text = text ?? (FishCatalog.Local(fish2) + " (takes " + FishCatalog.BaitsText(fish2) + ")");
				if (hashSet.Add("bait:" + text3) && _whyItems.Count < 8)
				{
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_bait", FishCatalog.Local(fish2), FishCatalog.BaitNames(fish2)));
				}
			}
			else if (!SpotFor(fish2, out stand2, out cost))
			{
				if (hashSet.Add("bank:" + text3) && _whyItems.Count < 8)
				{
					_whyItems.Add(new CompanionStatus.Item(null, "hc_why_fish_no_bank", FishCatalog.Local(fish2)));
				}
				num5++;
				text2 = text2 ?? (FishCatalog.Local(fish2) + " at " + MHR_Mover.Fmt(position) + ", water " + MHR_Water.WaterLevel(position).ToString("F1"));
			}
			else
			{
				float num7 = cost;
				if (num7 < num6)
				{
					num6 = num7;
					fish = fish2;
					stand = stand2;
					bait = text4;
				}
			}
		}
		if (fish == null)
		{
			ref string reference = ref why;
			string text5;
			if (num2 != 0)
			{
				if (num3 == num2)
				{
					text5 = "enough of every fish here";
				}
				else
				{
					text5 = ((num4 > 0 && num5 == 0) ? ("no bait for " + text) : ("no bank to fish from within a cast - " + num2 + " fish seen, e.g. " + text2));
				}
			}
			else
			{
				text5 = "no fish within " + num.ToString("F0") + " m";
			}
			reference = text5;
			return false;
		}
		_fish = fish;
		_fishName = FishCatalog.PrefabName(fish.gameObject);
		_stand = stand;
		_bait = bait;
		return true;
	}

	private bool PickFromHere()
	{
		Inventory inventory = _humanoid.GetInventory();
		Vector3 position = transform.position;
		Fish fish = null;
		string bait = null;
		float num = 20f;
		foreach (IMonoUpdater instance in Fish.Instances)
		{
			Fish fish2 = instance as Fish;
			if (fish2 == null || fish2.IsHooked())
			{
				continue;
			}
			Vector3 position2 = fish2.transform.position;
			float num2 = Flat(position, position2);
			if (num2 >= num || num2 < 2f || (_skip.TryGetValue(fish2.GetInstanceID(), out var value) && Time.time < value))
			{
				continue;
			}
			string text = FishCatalog.PrefabName(fish2.gameObject);
			if (!FishCatalog.Allowed(text, fish2) || !Needs(text))
			{
				continue;
			}
			string text2 = BestBait(fish2, inventory);
			if (text2 != null)
			{
				float num3 = Level(position2, position2.y);
				if (!Physics.Linecast(position + Vector3.up * 1.6f, new Vector3(position2.x, num3 + 0.3f, position2.z), LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
				{
					num = num2;
					fish = fish2;
					bait = text2;
				}
			}
		}
		if (fish == null)
		{
			return false;
		}
		_fish = fish;
		_fishName = FishCatalog.PrefabName(fish.gameObject);
		_bait = bait;
		_stand = position;
		return true;
	}

	private static string BestBait(Fish fish, Inventory inv)
	{
		string result = null;
		float num = -1f;
		foreach (Fish.BaitSetting bait in fish.m_baits)
		{
			if (!(bait?.m_bait == null))
			{
				string text = bait.m_bait.gameObject.name;
				if (bait.m_chance > num && CountIn(inv, text) > 0)
				{
					num = bait.m_chance;
					result = text;
				}
			}
		}
		return result;
	}

	private void MapShore()
	{
		if (Time.time - _shoreAt < 300f && Flat(_shoreFor, _anchor) < 2f)
		{
			return;
		}
		_shoreAt = Time.time;
		_shoreFor = _anchor;
		_shore.Clear();
		float num = Radius() + 6f;
		float num2 = MHR_Water.WaterLevel(_anchor);
		if (num2 < -1000f)
		{
			num2 = ((ZoneSystem.instance != null) ? ZoneSystem.instance.m_waterLevel : 30f);
		}
		for (float num3 = 0f - num; num3 <= num; num3 += 2f)
		{
			for (float num4 = 0f - num; num4 <= num; num4 += 2f)
			{
				if (num3 * num3 + num4 * num4 > num * num)
				{
					continue;
				}
				Vector3 p = _anchor + new Vector3(num3, 0f, num4);
				float num5 = Level(p, num2);
				if (!Top(p, num5, out var top) || top.y < num5 - 0.8f || top.y > num5 + 10f)
				{
					continue;
				}
				bool wet = top.y < num5 + 0.1f;
				bool flag = true;
				for (int i = 0; (i < 4) & flag; i++)
				{
					flag = Top(top + Quaternion.Euler(0f, (float)i * 90f, 0f) * Vector3.forward * 0.3f, num5, out var top2) && Mathf.Abs(top2.y - top.y) < 0.4f && top2.y > num5 - 0.8f;
				}
				if (!flag)
				{
					continue;
				}
				float num6 = 99f;
				Vector3 water = Vector3.zero;
				for (int j = 0; j < 8; j++)
				{
					Vector3 vector = Quaternion.Euler(0f, (float)j * 45f, 0f) * Vector3.forward;
					for (float num7 = 1f; num7 <= 5f; num7++)
					{
						Vector3 p2 = top + vector * num7;
						float num8 = Level(p2, num2);
						if (Top(p2, num8, out var top3) && num8 - top3.y > 0.8f)
						{
							if (num7 < num6)
							{
								num6 = num7;
								water = new Vector3(p2.x, num8, p2.z);
							}
							break;
						}
					}
				}
				if (!(num6 > 5f) && !Physics.CheckCapsule(top + Vector3.up * 0.6f, top + Vector3.up * 1.6f, 0.3f, LayerMask.GetMask("Default", "static_solid", "piece")) && !MHR_Mover.InFire(top))
				{
					_shore.Add(new Shore
					{
						Stand = top,
						Water = water,
						Wet = wet
					});
				}
			}
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug("Mapped " + _shore.Count + " places to fish from within " + num.ToString("F0") + " m");
		}
	}

	private static float Level(Vector3 p, float dflt)
	{
		float num = MHR_Water.WaterLevel(p);
		if (!(num < -1000f))
		{
			return num;
		}
		return dflt;
	}

	private static Vector3Int Cell(Vector3 p)
	{
		return new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
	}

	private void Fell(string when)
	{
		ReelOff();
		Vector3Int key = Cell(_stand);
		_falls.TryGetValue(key, out var value);
		value = (_falls[key] = value + 1);
		if (value >= 2)
		{
			_badSpot[key] = Time.time + 300f;
			_falls.Remove(key);
			if (CompanionsPlugin.LogNormal)
			{
				Log("Fell off its fishing spot again (" + when + ") - trying somewhere else");
			}
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
		else
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Fell off its fishing spot (" + when + ") - getting back on");
			}
			Enter(FishState.Moving);
		}
	}

	private bool SpotFor(Fish fish, out Vector3 stand)
	{
		float cost;
		return SpotFor(fish, out stand, out cost);
	}

	private bool SpotFor(Fish fish, out Vector3 stand, out float cost)
	{
		stand = Vector3.zero;
		cost = float.MaxValue;
		MapShore();
		Vector3 position = fish.transform.position;
		float y = Level(position, position.y);
		Vector3 vector = new Vector3(position.x, y, position.z);
		Vector3 position2 = transform.position;
		float num = float.MaxValue;
		bool result = false;
		foreach (Shore item in _shore)
		{
			if (_badSpot.TryGetValue(Cell(item.Stand), out var value) && Time.time < value)
			{
				continue;
			}
			float num2 = Flat(item.Stand, vector);
			if (!(num2 < 2f) && !(num2 > 20f))
			{
				float num3 = Flat(position2, item.Stand) + num2 * 1.5f + (item.Wet ? 10f : 0f);
				if (!(num3 >= num) && !Physics.Linecast(item.Stand + Vector3.up * 1.6f, vector + Vector3.up * 0.3f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
				{
					num = num3;
					stand = item.Stand;
					result = true;
				}
			}
		}
		cost = num;
		return result;
	}

	private bool BankSpot(Fish fish, out Vector3 stand)
	{
		stand = Vector3.zero;
		Vector3 position = fish.transform.position;
		float num = MHR_Water.WaterLevel(position);
		if (num < -1000f)
		{
			return false;
		}
		Vector3 target = new Vector3(position.x, num, position.z);
		Vector3 position2 = transform.position;
		if (GoodStand(position2, target, num))
		{
			stand = position2;
			return true;
		}
		Vector3 vector = position2 - position;
		vector.y = 0f;
		float num2 = ((vector.sqrMagnitude > 0.01f) ? (Mathf.Atan2(vector.x, vector.z) * 57.29578f) : 0f);
		for (int i = 0; i < 12; i++)
		{
			float y = num2 + (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 30f;
			Vector3 vector2 = Quaternion.Euler(0f, y, 0f) * Vector3.forward;
			for (float num3 = 2f; num3 <= 14f; num3++)
			{
				Vector3 vector3 = position + vector2 * num3;
				if (Top(vector3, num, out var top) && !(top.y < num + 0.1f))
				{
					if (!Top(vector3 + vector2 * 0.6f, num, out var top2) || !(top2.y >= num + 0.1f) || !GoodStand(top2, target, num))
					{
						break;
					}
					stand = top2;
					return true;
				}
			}
		}
		return false;
	}

	private static bool Top(Vector3 p, float water, out Vector3 top)
	{
		top = p;
		if (!Physics.Raycast(new Vector3(p.x, water + 30f, p.z), Vector3.down, out var hitInfo, 60f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return false;
		}
		if (hitInfo.normal.y < 0.6f)
		{
			return false;
		}
		top = hitInfo.point;
		return true;
	}

	private bool GoodStand(Vector3 q, Vector3 target, float water)
	{
		float num = Flat(q, target);
		if (num < 2f || num > 20f || q.y > water + 10f || q.y < water - 0.2f)
		{
			return false;
		}
		if (MHR_Water.WouldSwim(_character, q) || MHR_Mover.InFire(q))
		{
			return false;
		}
		if (Physics.CheckCapsule(q + Vector3.up * 0.6f, q + Vector3.up * 1.6f, 0.3f, LayerMask.GetMask("Default", "static_solid", "piece")))
		{
			return false;
		}
		return !Physics.Linecast(q + Vector3.up * 1.6f, target + Vector3.up * 0.3f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"));
	}

	private void UpdateMoving()
	{
		if (_fish == null || _fish.IsHooked())
		{
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
			return;
		}
		if (Flat(transform.position, _stand) <= 1.2f && Mathf.Abs(transform.position.y - _stand.y) < 1.5f)
		{
			_ai.StopMoving();
			MHR_Mover.Release(_ai);
			Enter(FishState.Casting);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _stand, 0.8f, allowRun: true, "Fish");
		if (MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim() || MHR_Mover.GaveUp(_ai) || Time.time - _stateSince > 60f)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Can't get to the bank by the " + _fishName + " - trying another");
			}
			_skip[_fish.GetInstanceID()] = Time.time + 120f;
			MHR_Mover.Release(_ai);
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
	}

	private void UpdateCasting()
	{
		_ai.StopMoving();
		if (_character.IsSwimming() || Flat(transform.position, _stand) > 2.5f || transform.position.y < _stand.y - 1.2f)
		{
			Fell("before casting");
			return;
		}
		CompanionStamina component = GetComponent<CompanionStamina>();
		if (component != null && component.MaxStamina > 0f && component.Stamina < component.MaxStamina * 0.9f)
		{
			_ai.LookAtPoint(AimPoint());
			_stateSince = Time.time;
			if (Time.time >= _nextRestLog)
			{
				_nextRestLog = Time.time + 20f;
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Getting my breath back before casting (" + component.Stamina.ToString("F0") + "/" + component.MaxStamina.ToString("F0") + " stamina)");
				}
			}
			return;
		}
		ItemDrop.ItemData itemData = FindRod();
		ItemDrop.ItemData itemData2 = FindItem(_humanoid.GetInventory(), _bait);
		if (itemData == null || itemData2 == null)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log((itemData == null) ? "No fishing rod" : ("Out of " + CookController.Name(_bait)));
			}
			Enter(FishState.Idle);
			return;
		}
		if (_setup != null)
		{
			_setup.SuppressAutoEquip = true;
			_rodOut = true;
		}
		if (_humanoid.GetCurrentWeapon() != itemData)
		{
			_humanoid.EquipItem(itemData);
			return;
		}
		if (_humanoid.GetAmmoItem() != itemData2)
		{
			_humanoid.EquipItem(itemData2);
		}
		if (_fish != null && Flat(transform.position, _fish.transform.position) > 26f)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("The " + _fishName + " has swum off - choosing again");
			}
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
			return;
		}
		Vector3 vector = AimPoint();
		_ai.LookAtPoint(vector);
		if (_character.InAttack() || !_ai.IsLookingAtPoint(vector, 8f) || Time.time - _stateSince < 0.5f)
		{
			if (Time.time - _stateSince > 10f)
			{
				if (CompanionsPlugin.LogNormal)
				{
					Log("Couldn't get a cast off - trying again");
				}
				Enter(FishState.Idle);
			}
			return;
		}
		float num = Flat(transform.position, vector);
		float num2 = Mathf.Clamp01(num / Mathf.Max(4f, _castScale));
		float drawDurationMin = itemData.m_shared.m_attack.m_drawDurationMin;
		float skillFactor = _character.GetSkillFactor(itemData.m_shared.m_skillType);
		float num3 = Mathf.Lerp(drawDurationMin, drawDurationMin * 0.2f, skillFactor);
		s_drawTime(_humanoid) = ((num3 > 0f) ? Mathf.Max(0.01f, num2 * num3) : 0f);
		bool flag = _humanoid.StartAttack(null, false);
		s_drawTime(_humanoid) = 0f;
		if (flag)
		{
			_castAt = Time.time;
			_castTries++;
			_settled = false;
			_retrieving = false;
			_hooked = null;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Casting at the " + _fishName + " (" + num.ToString("F0") + " m, " + (num2 * 100f).ToString("F0") + "% cast)");
			}
			Enter(FishState.Waiting);
		}
	}

	private Vector3 AimPoint()
	{
		Vector3 position = transform.position;
		Vector3 vector = ((_fish != null) ? _fish.transform.position : (position + transform.forward * 8f));
		Vector3 vector2 = vector - position;
		vector2.y = 0f;
		if (vector2.magnitude > 20f)
		{
			vector = position + vector2.normalized * 20f;
		}
		vector.y = Level(vector, position.y - 1f);
		return vector;
	}

	private void UpdateWaiting()
	{
		_ai.StopMoving();
		if (_character.IsSwimming() || transform.position.y < _stand.y - 1.2f)
		{
			Fell("waiting for a bite");
			return;
		}
		FishingFloat fishingFloat = MHR_FishingFloat.Of(_character);
		if (fishingFloat == null)
		{
			if (Time.time - _castAt > 4f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("The cast didn't come off");
				}
				if (_castTries >= 3)
				{
					SkipFish(60f);
					Enter(FishState.Idle);
				}
				else
				{
					Enter(FishState.Casting);
				}
			}
			return;
		}
		_ai.LookAtPoint(fishingFloat.transform.position);
		if (!_settled && Time.time - _castAt > 2.5f)
		{
			_settled = true;
			float num = Flat(transform.position, fishingFloat.transform.position);
			float num2 = Mathf.Clamp01(Flat(transform.position, AimPoint()) / Mathf.Max(4f, _castScale));
			if (num2 > 0.05f && fishingFloat.IsInWater())
			{
				_castScale = Mathf.Clamp(Mathf.Lerp(_castScale, num / num2, 0.5f), 4f, 40f);
			}
			if (!fishingFloat.IsInWater())
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("The float landed on dry ground - reeling in");
				}
				Retrieve();
				return;
			}
		}
		if (MHR_FishingFloat.Nibbler(fishingFloat) != null && Time.time - MHR_FishingFloat.NibbleTime(fishingFloat) < 0.4f)
		{
			_hookTry = Time.time;
			ReelOn();
			Enter(FishState.Reeling);
		}
		else if (Time.time - _castAt > (ModConfig.FishCastWait?.Value ?? 60f))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("No bite - reeling in");
			}
			Retrieve();
		}
	}

	private void Retrieve()
	{
		_retrieving = true;
		ReelOn();
		Enter(FishState.Reeling);
	}

	private void UpdateReeling()
	{
		_ai.StopMoving();
		if (_character.IsSwimming())
		{
			if (_hooked != null && CompanionsPlugin.LogNormal)
			{
				Log("Fell in with a " + _hookedName + " on the line - lost it");
			}
			_hooked = null;
			_hookedName = null;
			Fell("reeling");
			return;
		}
		FishingFloat fishingFloat = MHR_FishingFloat.Of(_character);
		if (fishingFloat == null)
		{
			ReelOff();
			Landed();
			return;
		}
		_ai.LookAtPoint(fishingFloat.transform.position);
		Fish fish = fishingFloat.GetCatch();
		if (fish == null)
		{
			if (_hooked != null)
			{
				if (CompanionsPlugin.LogNormal)
				{
					Log("The " + _hookedName + " got away");
				}
				_talk?.Say(ModLocalization.Loc("hc_speech_fish_got_away"));
				_hooked = null;
				Retrieve();
				return;
			}
			if (!_retrieving && Time.time - _hookTry > 1f)
			{
				ReelOff();
				Enter(FishState.Waiting);
			}
			else
			{
				ReelOn();
			}
		}
		else
		{
			if (_hooked == null)
			{
				_hooked = fish;
				_hookedName = FishCatalog.PrefabName(fish.gameObject);
				_haveBefore = CountIn(_humanoid.GetInventory(), _hookedName);
				if (CompanionsPlugin.LogNormal)
				{
					Log("Hooked a " + FishCatalog.Local(fish) + " - reeling it in");
				}
			}
			float num = Tension(fishingFloat);
			if (fish.IsEscaping() || num > 2.5f)
			{
				ReelOff();
			}
			else
			{
				ReelOn();
			}
		}
		if (Time.time - _stateSince > 90f)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Reeling is taking too long - letting go");
			}
			ReelOff();
			Enter(FishState.Idle);
		}
	}

	private void Landed()
	{
		if (_hooked != null || _hookedName != null)
		{
			int num = CountIn(_humanoid.GetInventory(), _hookedName);
			if (_hookedName != null && num > _haveBefore)
			{
				string text = CookController.Name(_hookedName);
				if (CompanionsPlugin.LogNormal)
				{
					Log("Caught a " + text + NeedText(_hookedName));
				}
				_talk?.Say(ModLocalization.LocFmt("hc_speech_fish_caught", text), force: true);
				_nextCount = 0f;
			}
			else if (_hooked != null && CompanionsPlugin.LogNormal)
			{
				Log("Lost the " + _hookedName + " (the line broke)");
			}
		}
		_hooked = null;
		_hookedName = null;
		_retrieving = false;
		RefreshCounts();
		if (!PackFull() && PickFromHere())
		{
			_castTries = 0;
			Enter(FishState.Casting);
		}
		else
		{
			Enter(FishState.Idle);
			_scanTimer = 0.5f;
		}
	}

	private float Tension(FishingFloat ff)
	{
		if (_rodTop == null)
		{
			_rodTop = FindChild(transform, "_RodTop");
		}
		if (_rodTop == null)
		{
			return 0f;
		}
		return Vector3.Distance(_rodTop.position, ff.transform.position) - MHR_FishingFloat.LineLength(ff);
	}

	private void ReelOn()
	{
		ReflectionHelper.TrySetBlocking(_character, value: true);
	}

	private void ReelOff()
	{
		if (_character != null)
		{
			ReflectionHelper.TrySetBlocking(_character, value: false);
		}
	}

	private void SkipFish(float seconds)
	{
		if (_fish != null)
		{
			_skip[_fish.GetInstanceID()] = Time.time + seconds;
		}
		if (_skip.Count > 100)
		{
			_skip.Clear();
		}
	}

	private void UpdateGoingHome()
	{
		if (!_setup.HasHomePosition())
		{
			Enter(FishState.Idle);
			return;
		}
		if (MHR_ToolKeeper.AtHome(_setup))
		{
			_nextUnloadTry = Time.time + 60f;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Home - putting the catch away");
			}
			MHR_ToolKeeper.RequestUnload(_setup);
			_nextCount = 0f;
			MHR_Mover.Release(_ai);
			Enter(FishState.Idle);
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _setup.GetHomePosition(), 4f, allowRun: true, "Fish");
		if (MHR_Mover.GaveUp(_ai))
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Can't find the way home with the catch - trying again later");
			}
			_nextUnloadTry = Time.time + 60f;
			Enter(FishState.Idle);
		}
	}

	private bool PackFull()
	{
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		float num = ModConfig.FishBagFull?.Value ?? 0.9f;
		if (inventory.GetEmptySlots() != 0)
		{
			return inventory.GetTotalWeight() >= CompanionTierData.CarryLimit(this) * num;
		}
		return true;
	}

	private bool Needs(string fish)
	{
		int num = FishCatalog.Target(fish);
		if (num > 0 && _counts.TryGetValue(fish, out var value))
		{
			return value < num;
		}
		return true;
	}

	private string NeedText(string fish)
	{
		int num = FishCatalog.Target(fish);
		_counts.TryGetValue(fish, out var value);
		return " (" + value + "/" + ((num > 0) ? num.ToString() : "no limit") + ")";
	}

	private void RefreshCounts()
	{
		if (Time.time < _nextCount)
		{
			return;
		}
		_nextCount = Time.time + 15f;
		_counts.Clear();
		Add(_humanoid.GetInventory());
		Vector3 center = (_setup.HasHomePosition() ? _setup.GetHomePosition() : _anchor);
		float radius = (_setup.HasHomePosition() ? MHR_ToolKeeper.HomeRange() : 25f);
		foreach (Container item in MHR_Chests.Near(center, radius))
		{
			Inventory inventory = item?.GetInventory();
			if (inventory != null)
			{
				Add(inventory);
			}
		}
	}

	private void Add(Inventory inv)
	{
		if (inv == null)
		{
			return;
		}
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			string text = allItem?.m_dropPrefab?.name;
			if (text != null && FishCatalog.IsFish(text))
			{
				_counts.TryGetValue(text, out var value);
				_counts[text] = value + allItem.m_stack;
			}
		}
	}

	private ItemDrop.ItemData FindRod()
	{
		foreach (ItemDrop.ItemData allItem in _humanoid.GetInventory().GetAllItems())
		{
			if (allItem.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				return allItem;
			}
		}
		return null;
	}

	private static ItemDrop.ItemData FindItem(Inventory inv, string prefab)
	{
		if (inv == null || prefab == null)
		{
			return null;
		}
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			if (allItem?.m_dropPrefab != null && allItem.m_dropPrefab.name == prefab)
			{
				return allItem;
			}
		}
		return null;
	}

	private static int CountIn(Inventory inv, string prefab)
	{
		int num = 0;
		if (inv == null || prefab == null)
		{
			return 0;
		}
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			if (allItem?.m_dropPrefab != null && allItem.m_dropPrefab.name == prefab)
			{
				num += allItem.m_stack;
			}
		}
		return num;
	}

	private void RestoreGear()
	{
		_rodOut = false;
		ReelOff();
		if (_humanoid != null)
		{
			ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
			if (currentWeapon != null && currentWeapon.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod)
			{
				_humanoid.UnequipItem(currentWeapon);
			}
		}
		if (_setup != null && _setup.SuppressAutoEquip)
		{
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	private static Transform FindChild(Transform t, string name)
	{
		if (t.name == name)
		{
			return t;
		}
		for (int i = 0; i < t.childCount; i++)
		{
			Transform transform = FindChild(t.GetChild(i), name);
			if (transform != null)
			{
				return transform;
			}
		}
		return null;
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	private void Say(string key)
	{
		CompanionStatus.Why(this, key, false);
		if (Time.time - _lastSpeechTime >= 60f)
		{
			_lastSpeechTime = Time.time;
			_talk?.SayProblem("fish:" + key, ModLocalization.Loc(key));
		}
	}

	private void WhyLog(string msg)
	{
		if (Time.time >= _nextWhyLog)
		{
			_nextWhyLog = Time.time + 60f;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug(msg);
			}
		}
	}

	private void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Fish|" + (_character?.m_name ?? "?") + "] " + msg);
		}
	}

	private void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log?.LogDebug("[Fish|" + (_character?.m_name ?? "?") + "] " + msg);
		}
	}
}
