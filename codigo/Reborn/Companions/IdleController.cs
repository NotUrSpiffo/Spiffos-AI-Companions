using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public class IdleController : MonoBehaviour
{
	private enum Act
	{
		Off,
		Pause,
		Chore,
		ToSeat,
		Seated,
		Wander,
		Social
	}

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionSetup _setup;

	private Character _character;

	private CompanionTalk _talk;

	private CompanionRest _rest;

	private HarvestController _harvest;

	private SmeltController _smelt;

	private FarmController _farm;

	private CookController _cook;

	private HuntController _hunt;

	private FishController _fish;

	private UpkeepController _upkeep;

	private RepairController _gearRepair;

	private DoorHandler _door;

	private Act _act;

	private float _eligibleSince = -1f;

	private Vector3 _center;

	private float _until;

	private float _nextChoreTry;

	private Chair _chair;

	private Vector3 _goal;

	private Vector3 _look;

	private Character _buddy;

	private bool _hasGoal;

	private float _lastWhy = -9999f;

	private float _nextChat;

	private static readonly List<Piece> s_pieces = new List<Piece>();

	public bool IsActive => _act != Act.Off;

	private static bool On
	{
		get
		{
			if (ModConfig.IdleEnabled != null)
			{
				return ModConfig.IdleEnabled.Value;
			}
			return true;
		}
	}

	private static float Radius => ModConfig.IdleRadius?.Value ?? 8f;

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_ai = GetComponent<CompanionAI>();
		_setup = GetComponent<CompanionSetup>();
		_character = GetComponent<Character>();
		_talk = GetComponent<CompanionTalk>();
		_rest = GetComponent<CompanionRest>();
		_harvest = GetComponent<HarvestController>();
		_smelt = GetComponent<SmeltController>();
		_farm = GetComponent<FarmController>();
		_cook = GetComponent<CookController>();
		_hunt = GetComponent<HuntController>();
		_fish = GetComponent<FishController>();
		_upkeep = GetComponent<UpkeepController>();
		_gearRepair = GetComponent<RepairController>();
		_door = GetComponent<DoorHandler>();
	}

	private int Mode()
	{
		return (_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
	}

	private bool Free(out string why)
	{
		why = null;
		if (!On)
		{
			why = "idle behaviour off";
			return false;
		}
		if (Mode() == 4)
		{
			why = "told to stay";
			return false;
		}
		if (_ai.GetFollowTarget() != null || (_setup != null && _setup.GetFollow()))
		{
			why = "following";
			return false;
		}
		if (_ai.IsInCombat || _character.IsSwimming() || _ai.IsOnShip)
		{
			why = "busy";
			return false;
		}
		if (MHR_JobRules.OrderPending(this))
		{
			why = "an order";
			return false;
		}
		if (MHR_ToolKeeper.IsBusy(this) || MHR_Loot.Active(this))
		{
			why = "a trip";
			return false;
		}
		if (_rest != null && (_rest.IsSleeping || _rest.IsNavigating || _rest.IsResting))
		{
			why = "resting";
			return false;
		}
		if (CompanionInteractPanel.IsOpenFor(_setup) || CompanionRadialMenu.IsOpenFor(_setup))
		{
			why = "being talked to";
			return false;
		}
		bool flag = _act == Act.Chore && _upkeep != null && _upkeep.IdleChoreActive;
		if ((_harvest != null && _harvest.IsActive) || (_smelt != null && _smelt.IsActive) || (_farm != null && _farm.IsActive) || (_cook != null && _cook.IsActive) || (_hunt != null && _hunt.IsActive) || (_fish != null && _fish.IsActive) || (_gearRepair != null && _gearRepair.IsActive) || (_door != null && _door.IsActive) || (!flag && _upkeep != null && _upkeep.IsActive))
		{
			why = "the job has work";
			return false;
		}
		return true;
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsValid() || !_nview.IsOwner() || _ai == null || _character == null || _character.IsDead())
		{
			return;
		}
		if (!Free(out var why))
		{
			_eligibleSince = -1f;
			if (_act != Act.Off)
			{
				Stop(why);
			}
			return;
		}
		if (_act == Act.Off)
		{
			if (_eligibleSince < 0f)
			{
				_eligibleSince = Time.time;
			}
			if (!(Time.time - _eligibleSince < (ModConfig.IdleStartAfter?.Value ?? 10f)))
			{
				Begin();
			}
			return;
		}
		switch (_act)
		{
		case Act.Pause:
			UpdatePause();
			break;
		case Act.Chore:
			if (_upkeep == null || !_upkeep.IdleChoreActive)
			{
				Pause(3f, 6f);
			}
			break;
		case Act.ToSeat:
			UpdateToSeat();
			break;
		case Act.Seated:
			if (!MHR_Follow.IdleSeated(_ai) || Time.time >= _until)
			{
				MHR_Follow.IdleStand(_ai, "had a rest");
				Pause(2f, 5f);
			}
			break;
		case Act.Wander:
			UpdateWander();
			break;
		case Act.Social:
			UpdateSocial();
			break;
		}
	}

	private void Begin()
	{
		_center = transform.position;
		if (Mode() == 0 && _setup != null && _setup.HasHomePosition() && Vector3.Distance(_setup.GetHomePosition(), transform.position) <= 15f)
		{
			_center = _setup.GetHomePosition();
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log("Nothing to do - idling around " + MHR_Mover.Fmt(_center));
		}
		SayWhy();
		_nextChoreTry = 0f;
		Pause(1f, 3f);
	}

	private void Stop(string why)
	{
		if (_act == Act.Chore && _upkeep != null)
		{
			_upkeep.StopIdleChore();
		}
		if (_act == Act.Seated || _act == Act.ToSeat)
		{
			MHR_Follow.IdleStand(_ai, why);
		}
		if (_act != Act.Pause && _act != Act.Chore)
		{
			_ai.StopMoving();
			MHR_Mover.Release(_ai);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug("Stopped idling (" + why + ")");
		}
		_act = Act.Off;
		_chair = null;
		_buddy = null;
		_hasGoal = false;
	}

	private void Pause(float min, float max)
	{
		_act = Act.Pause;
		_until = Time.time + Random.Range(min, max);
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		Vector2 vector = Random.insideUnitCircle.normalized * 4f;
		_look = transform.position + new Vector3(vector.x, 1.2f, vector.y);
	}

	private void UpdatePause()
	{
		_ai.StopMoving();
		_ai.LookAtPoint(_look);
		if (!(Time.time < _until))
		{
			Next();
		}
	}

	private void Next()
	{
		if ((ModConfig.IdleChores == null || ModConfig.IdleChores.Value) && _upkeep != null && Time.time >= _nextChoreTry)
		{
			_nextChoreTry = Time.time + 20f;
			if (_upkeep.StartIdleChore(_center, ModConfig.IdleChoreRadius?.Value ?? 15f))
			{
				_act = Act.Chore;
				return;
			}
		}
		List<Act> list = new List<Act>();
		if (ModConfig.IdleWander == null || ModConfig.IdleWander.Value)
		{
			list.Add(Act.Wander);
			list.Add(Act.Wander);
		}
		if (ModConfig.IdleSit == null || ModConfig.IdleSit.Value)
		{
			list.Add(Act.ToSeat);
		}
		if ((ModConfig.IdleSocialize == null || ModConfig.IdleSocialize.Value) && FindBuddy() != null)
		{
			list.Add(Act.Social);
		}
		if (list.Count == 0)
		{
			Pause(5f, 10f);
		}
		else if (!(list[Random.Range(0, list.Count)] switch
		{
			Act.Social => StartSocial(), 
			Act.ToSeat => StartSit(), 
			_ => StartWander(), 
		}))
		{
			Pause(3f, 6f);
		}
	}

	private bool StartSit()
	{
		_chair = MHR_Follow.IdleFindChair(_ai, _center, Radius);
		_until = Time.time + Random.Range(20f, 60f);
		if (_chair == null)
		{
			MHR_Follow.IdleSitFloor(_ai, transform.position + transform.forward);
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Sitting down for a bit");
			}
			_act = Act.Seated;
			return true;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug("Going to sit on the " + _chair.m_name);
		}
		_act = Act.ToSeat;
		_goal = _chair.m_attachPoint.position;
		_until = Time.time + 20f;
		return true;
	}

	private void UpdateToSeat()
	{
		if (_chair == null || _chair.m_attachPoint == null || Time.time >= _until)
		{
			MHR_Follow.IdleStand(_ai, "couldn't get to the seat");
			Pause(2f, 4f);
			return;
		}
		MHR_Follow.IdleClaimChair(_ai, _chair);
		bool flag = MHR_Mover.Go(_ai, Time.deltaTime, _chair.m_attachPoint.position, 0.8f, allowRun: false, "Idle", 1);
		if (MHR_Mover.GaveUp(_ai))
		{
			MHR_Follow.IdleStand(_ai, "couldn't get to the seat");
			Pause(2f, 4f);
		}
		else if (flag)
		{
			MHR_Follow.IdleSit(_ai, _chair);
			_act = Act.Seated;
			_until = Time.time + Random.Range(20f, 60f);
		}
	}

	private bool StartWander()
	{
		Vector3 position = transform.position;
		if (Random.value < 0.4f && PickStation(out _goal, out _look))
		{
			_hasGoal = true;
		}
		else
		{
			_hasGoal = false;
			for (int i = 0; i < 8; i++)
			{
				if (_hasGoal)
				{
					break;
				}
				Vector2 vector = Random.insideUnitCircle * Radius;
				Vector3 vector2 = _center + new Vector3(vector.x, 0f, vector.y);
				if (Physics.Raycast(vector2 + Vector3.up * 4f, Vector3.down, out var hitInfo, 10f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")) && !(hitInfo.normal.y < 0.6f))
				{
					vector2 = hitInfo.point;
					if (!(Mathf.Abs(vector2.y - position.y) > 3f) && !MHR_Water.WouldSwim(_character, vector2) && !MHR_Mover.NearFire(vector2, 1.2f) && !(Vector3.Distance(vector2, position) < 2f) && MHR_Mover.CanGetTo(position, vector2))
					{
						_goal = vector2;
						Vector3 vector3 = vector2 - position;
						vector3.y = 0f;
						_look = vector2 + vector3.normalized * 3f + Vector3.up * 1.2f;
						_hasGoal = true;
					}
				}
			}
		}
		if (!_hasGoal)
		{
			return false;
		}
		_act = Act.Wander;
		_until = Time.time + 25f;
		return true;
	}

	private bool PickStation(out Vector3 stand, out Vector3 look)
	{
		stand = (look = Vector3.zero);
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(_center, Radius, s_pieces);
		List<Component> list = new List<Component>();
		foreach (Piece s_piece in s_pieces)
		{
			if (!(s_piece == null))
			{
				Component component = (Component)(s_piece.GetComponent<CraftingStation>() ?? s_piece.GetComponent<Smelter>() ?? s_piece.GetComponent<Fireplace>() ?? s_piece.GetComponent<CookingStation>() ?? s_piece.GetComponent<Fermenter>() ?? ((object)s_piece.GetComponent<Beehive>()) ?? ((object)s_piece.GetComponentInChildren<Container>()));
				if (component != null)
				{
					list.Add(component);
				}
			}
		}
		for (int i = 0; i < 4; i++)
		{
			if (list.Count <= 0)
			{
				break;
			}
			Component component2 = list[Random.Range(0, list.Count)];
			list.Remove(component2);
			if (MHR_Chests.PickApproachPiece(transform.position, component2, out var spot))
			{
				stand = spot;
				look = component2.transform.position + Vector3.up * 0.8f;
				return true;
			}
		}
		return false;
	}

	private void UpdateWander()
	{
		if (MHR_Mover.Go(_ai, Time.deltaTime, _goal, 0.8f, allowRun: false, "Idle", 1))
		{
			Pause(4f, 9f);
			_look = ((_look == Vector3.zero) ? (transform.position + transform.forward * 3f) : _look);
		}
		else if (MHR_Mover.GaveUp(_ai) || Time.time >= _until)
		{
			Pause(2f, 4f);
		}
	}

	private Character FindBuddy()
	{
		Vector3 position = transform.position;
		float num = Radius * 2f;
		Player player = MHR_Owner.Of(this);
		if (player != null && !player.IsDead() && Vector3.Distance(position, player.transform.position) <= num && Mathf.Abs(player.transform.position.y - position.y) < 3f)
		{
			return player;
		}
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		if (allCompanions != null)
		{
			foreach (CompanionSetup item in allCompanions)
			{
				if (!(item == null) && !(item == _setup))
				{
					Character component = item.GetComponent<Character>();
					if (component != null && !component.IsDead() && Vector3.Distance(position, component.transform.position) <= num && Mathf.Abs(component.transform.position.y - position.y) < 3f)
					{
						return component;
					}
				}
			}
		}
		return null;
	}

	private bool StartSocial()
	{
		_buddy = FindBuddy();
		if (_buddy == null)
		{
			return false;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug("Going over to " + _buddy.m_name);
		}
		_act = Act.Social;
		_until = Time.time + Random.Range(15f, 30f);
		return true;
	}

	private void UpdateSocial()
	{
		if (_buddy == null || _buddy.IsDead() || Time.time >= _until || Vector3.Distance(_center, _buddy.transform.position) > Radius * 2f)
		{
			Pause(2f, 5f);
			return;
		}
		Vector3 position = _buddy.transform.position;
		if (Vector3.Distance(transform.position, position) > 2.5f)
		{
			MHR_Mover.Go(_ai, Time.deltaTime, position, 2f, allowRun: false, "Idle", 1);
			if (MHR_Mover.GaveUp(_ai))
			{
				Pause(2f, 4f);
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_ai.LookAtPoint(position + Vector3.up * 1.5f);
		float num = ModConfig.IdleSpeechInterval?.Value ?? 300f;
		if (num > 0f && Time.time >= _nextChat)
		{
			_nextChat = Time.time + Mathf.Max(60f, num * 0.5f);
			if (Random.value < 0.6f && _talk != null)
			{
				_talk.Speak(_talk.Pick(SpeechConfig.Instance.Chat), CompanionTalk.Priority.Ambient, null);
			}
		}
	}

	private void SayWhy()
	{
		float num = ModConfig.IdleSpeechInterval?.Value ?? 300f;
		if (num <= 0f || Time.time - _lastWhy < num || _talk == null)
		{
			return;
		}
		string text = CompanionStatus.SpokenReason(this);
		if (string.IsNullOrEmpty(text))
		{
			string text2 = "hc_idle_why_" + Mode();
			text = ModLocalization.Loc(text2);
			if (string.IsNullOrEmpty(text) || text == text2 || text.StartsWith("["))
			{
				return;
			}
		}
		_lastWhy = Time.time;
		_talk.Say(text);
	}

	private void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Idle|" + ((_character != null) ? _character.m_name : "?") + "] " + msg);
		}
	}

	private void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log?.LogDebug("[Idle|" + ((_character != null) ? _character.m_name : "?") + "] " + msg);
		}
	}
}
