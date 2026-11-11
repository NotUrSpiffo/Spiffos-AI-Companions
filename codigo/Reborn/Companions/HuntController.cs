using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

public class HuntController : MonoBehaviour
{
	private enum HuntState
	{
		Idle,
		Hunting,
		Returning,
		GoingHome,
		Birding
	}

	private const float ScanInterval = 2f;

	private ZNetView _nview;

	private CompanionAI _ai;

	private CompanionCombatAI _combatAI;

	private float _nextTooStrongLog;

	private CompanionSetup _setup;

	private Character _character;

	private Humanoid _humanoid;

	private HuntState _state;

	private float _scanTimer;

	private Character _prey;

	private float _nextSwapCheck;

	private Vector3 _lastPreyPos;

	private string _preyName;

	private bool _wasChasing;

	private Vector3 _anchor;

	private bool _hasAnchor;

	private bool _wasFollow;

	private float _nextUnloadTry;

	private float _nextNothingLog;

	private float _nextFullLog;

	private float _nextCount;

	private readonly Dictionary<string, int> _meatCount = new Dictionary<string, int>();

	private readonly Dictionary<int, float> _skip = new Dictionary<int, float>();

	private RandomFlyingBird _bird;

	private string _birdName;

	private Vector3 _birdPos;

	private float _birdSince;

	private float _nextShot;

	private float _noBowUntil;

	private HashSet<int> _birdBefore;

	private const float BirdShotRange = 30f;

	private bool _birdBowOut;

	private readonly List<CompanionStatus.Item> _whyItems = new List<CompanionStatus.Item>();

	public bool IsActive
	{
		get
		{
			if (GetMode() == 7)
			{
				return _state != HuntState.Idle;
			}
			return false;
		}
	}

	public void NotifyActionModeChanged()
	{
		ReleaseBirdBow();
		if (_combatAI != null && _combatAI.HuntMode)
		{
			_combatAI.HuntMode = false;
		}
		_state = HuntState.Idle;
		_prey = null;
		_scanTimer = 0f;
		_hasAnchor = false;
		_nextCount = 0f;
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
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _setup == null || _ai == null || _combatAI == null || CompanionRest.MHR_ShouldPauseWork(this))
		{
			return;
		}
		if (GetMode() != 7)
		{
			ReleaseBirdBow();
			if (_state != HuntState.Idle || _prey != null)
			{
				if (_combatAI.HuntMode)
				{
					_combatAI.HuntMode = false;
				}
				_state = HuntState.Idle;
				_prey = null;
			}
			_hasAnchor = false;
			return;
		}
		bool follow = _setup.GetFollow();
		if (follow != _wasFollow)
		{
			_wasFollow = follow;
			_hasAnchor = false;
			if (_state == HuntState.Returning || _state == HuntState.GoingHome)
			{
				Stop();
			}
		}
		if (!follow && !_hasAnchor)
		{
			_hasAnchor = true;
			bool flag = _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup);
			_anchor = (flag ? _setup.GetHomePosition() : transform.position);
			if (CompanionsPlugin.LogNormal)
			{
				Log("Hunting around " + (flag ? "home" : "here") + " " + MHR_Mover.Fmt(_anchor) + ", up to " + Range(follow: false).ToString("F0") + " m out");
			}
		}
		if (!MHR_ToolKeeper.IsBusy(this) && !MHR_Loot.Active(this) && !MHR_JobRules.OrderPending(this) && !CompanionInteractPanel.IsOpenFor(_setup) && !CompanionRadialMenu.IsOpenFor(_setup))
		{
			switch (_state)
			{
			case HuntState.Hunting:
				UpdateHunting(follow);
				break;
			case HuntState.Birding:
				UpdateBirding(follow);
				break;
			case HuntState.GoingHome:
				UpdateGoingHome(follow);
				break;
			default:
				UpdateIdle(follow);
				break;
			}
		}
	}

	private float Range(bool follow)
	{
		if (!follow)
		{
			return ModConfig.HuntRange?.Value ?? 40f;
		}
		return ModConfig.HuntFollowRange?.Value ?? 25f;
	}

	private Vector3 Center(bool follow)
	{
		if (follow && MHR_Owner.Of(this) != null)
		{
			return MHR_Owner.Of(this).transform.position;
		}
		if (!_hasAnchor)
		{
			return transform.position;
		}
		return _anchor;
	}

	private void Stop()
	{
		ReleaseBirdBow();
		_state = HuntState.Idle;
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_setup.ApplyFollowMode(7, force: true);
	}

	private void UpdateIdle(bool follow)
	{
		if (_combatAI.IsEngaged)
		{
			return;
		}
		if (_state == HuntState.Returning)
		{
			if ((Vector3.Distance(transform.position, _anchor) <= 5f) | follow)
			{
				Stop();
			}
			else
			{
				MHR_Mover.Go(_ai, Time.deltaTime, _anchor, 4f, allowRun: true, "Hunt");
				if (MHR_Mover.GaveUp(_ai))
				{
					if (CompanionsPlugin.LogNormal)
					{
						Log("Can't get back to the hunting spot - hunting from here");
					}
					_anchor = transform.position;
					Stop();
				}
			}
		}
		_scanTimer -= Time.deltaTime;
		if (_scanTimer > 0f)
		{
			return;
		}
		_scanTimer = 2f;
		if (PackFull())
		{
			if (!follow && _setup.HasHomePosition() && MHR_ToolKeeper.NearHome(_setup))
			{
				if (MHR_ToolKeeper.AtHome(_setup))
				{
					if (Time.time >= _nextUnloadTry)
					{
						_nextUnloadTry = Time.time + 60f;
						if (CompanionsPlugin.LogNormal)
						{
							Log("Pack is full - putting the catch away");
						}
						MHR_ToolKeeper.RequestUnload(_setup);
						_nextCount = 0f;
					}
				}
				else if (Time.time >= _nextUnloadTry)
				{
					if (CompanionsPlugin.LogNormal)
					{
						Log("Pack is full - taking the catch home");
					}
					_state = HuntState.GoingHome;
					MHR_Mover.Release(_ai);
				}
				return;
			}
			if (Time.time >= _nextFullLog)
			{
				_nextFullLog = Time.time + 120f;
				if (CompanionsPlugin.LogNormal)
				{
					Log(follow ? "Pack is full - keeping the catch until we're home" : "Pack is full and home is too far - not hunting until it's emptied");
				}
			}
			CompanionStatus.Why(this, follow ? "hc_why_pack_full" : "hc_why_pack_full_far", true);
			return;
		}
		if (MHR_SpearThrow.UnarmedAfterThrow(_ai, _humanoid))
		{
			CompanionStatus.Why(this, "hc_why_hunt_spear", true);
			return;
		}
		Character character = FindPrey(follow, out var why);
		if (character == null && StartBird(follow))
		{
			return;
		}
		if (character == null)
		{
			if (Time.time >= _nextNothingLog)
			{
				_nextNothingLog = Time.time + 60f;
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Nothing to hunt" + ((why != null) ? (" (" + why + ")") : ""));
				}
			}
			if (_whyItems.Count == 0)
			{
				_whyItems.Add(new CompanionStatus.Item(null, (why != null && why.StartsWith("only")) ? "hc_why_hunt_reach" : "hc_why_hunt_none_range", Range(follow).ToString("F0")));
			}
			CompanionStatus.WhyRotate(this, _whyItems);
			if (!follow && _state == HuntState.Idle && Vector3.Distance(transform.position, _anchor) > 10f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Heading back to the hunting spot");
				}
				_state = HuntState.Returning;
				MHR_Mover.Release(_ai);
			}
			return;
		}
		_combatAI.EngagePrey(character);
		if (!_combatAI.HuntMode)
		{
			if (Time.time >= _nextNothingLog)
			{
				_nextNothingLog = Time.time + 60f;
				if (CompanionsPlugin.LogNormal)
				{
					Log("Can't hunt with the Passive stance");
				}
			}
			CompanionStatus.Why(this, "hc_why_hunt_passive", true);
			return;
		}
		_prey = character;
		_preyName = character.m_name;
		_lastPreyPos = character.transform.position;
		_wasChasing = true;
		_state = HuntState.Hunting;
		CompanionStatus.Clear(this);
		CompanionStatus.Doing(this, "hc_doing_hunt", Loc(character.m_name));
		MHR_Mover.Release(_ai);
		if (CompanionsPlugin.LogNormal)
		{
			Log("Going after " + Loc(character.m_name) + " (" + Vector3.Distance(transform.position, character.transform.position).ToString("F0") + " m away)" + NeedText(PrefabName(character)));
		}
	}

	private void UpdateHunting(bool follow)
	{
		Character prey = _prey;
		bool flag = prey == null || prey.GetComponent<ZNetView>() == null || !prey.GetComponent<ZNetView>().IsValid();
		if (!flag && (prey.GetHealth() <= 0f || prey.IsDead()))
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug(Loc(_preyName) + " is down");
			}
			MHR_Loot.OnKill(_setup, prey);
			Done();
			return;
		}
		if (flag)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug(Loc(_preyName) + " is gone");
			}
			Done();
			return;
		}
		_lastPreyPos = prey.transform.position;
		if (_combatAI.IsEngaged && _combatAI.TargetCreature != null && _combatAI.TargetCreature != prey)
		{
			_wasChasing = false;
			return;
		}
		if (!_combatAI.IsEngaged || _combatAI.TargetCreature != prey)
		{
			if (_wasChasing)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Lost " + Loc(_preyName) + " - leaving it for a while");
				}
				Skip(prey, 60f);
			}
			Done();
			return;
		}
		if (!MHR_Loaded.Safe(prey.transform.position, 5f) || !MHR_Loaded.Safe(transform.position, 5f))
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Letting " + Loc(_preyName) + " go - it ran to the edge of where the world is loaded around you");
			}
			Skip(prey, 45f);
			Done();
			return;
		}
		_wasChasing = true;
		float num = ModConfig.HuntSwitchCloserBy?.Value ?? 5f;
		if (num > 0f && Time.time >= _nextSwapCheck && !_combatAI.HasAttackedTarget)
		{
			_nextSwapCheck = Time.time + 2f;
			Character character = FindPrey(follow, out var _);
			if (character != null && character != prey)
			{
				float num2 = Vector3.Distance(transform.position, prey.transform.position);
				float num3 = Vector3.Distance(transform.position, character.transform.position);
				if (num3 < num2 * (ModConfig.HuntSwitchShare?.Value ?? 0.6f) && num2 - num3 > num)
				{
					_combatAI.EngagePrey(character);
					if (_combatAI.HuntMode && _combatAI.TargetCreature == character)
					{
						if (CompanionsPlugin.LogNormal)
						{
							Log("Going for a closer " + Loc(character.m_name) + " (" + num3.ToString("F0") + " m) instead of the " + Loc(_preyName) + " " + num2.ToString("F0") + " m away");
						}
						_prey = character;
						_preyName = character.m_name;
						_lastPreyPos = character.transform.position;
						return;
					}
				}
			}
		}
		float num4 = Range(follow) + (ModConfig.HuntChaseExtra?.Value ?? 15f);
		if (Vector3.Distance(prey.transform.position, Center(follow)) > num4)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log(Loc(_preyName) + " got away");
			}
			GetComponent<CompanionTalk>()?.Speak(ModLocalization.Loc("hc_why_hunt_got_away"), CompanionTalk.Priority.Event, "hunt:away");
			Skip(prey, 60f);
			_combatAI.Disengage();
			Done();
		}
	}

	private void Done()
	{
		if (_combatAI.HuntMode)
		{
			_combatAI.Disengage();
		}
		_prey = null;
		_state = HuntState.Idle;
		_scanTimer = 1f;
		_nextCount = 0f;
	}

	private bool StartBird(bool follow)
	{
		ConfigEntry<bool> huntBirds = ModConfig.HuntBirds;
		if ((huntBirds != null && !huntBirds.Value) || Time.time < _noBowUntil || _humanoid == null)
		{
			return false;
		}
		Vector3 b = Center(follow);
		float num = Range(follow);
		Vector3 position = transform.position;
		RandomFlyingBird randomFlyingBird = null;
		float num2 = float.MaxValue;
		foreach (IMonoUpdater instance in RandomFlyingBird.Instances)
		{
			RandomFlyingBird randomFlyingBird2 = instance as RandomFlyingBird;
			if (randomFlyingBird2 == null || !Landed(randomFlyingBird2))
			{
				continue;
			}
			Vector3 position2 = randomFlyingBird2.transform.position;
			if (Vector3.Distance(position2, b) > num || Vector3.Distance(position2, position) > 40f || (_skip.TryGetValue(randomFlyingBird2.GetInstanceID(), out var value) && Time.time < value))
			{
				continue;
			}
			string prefab = BirdPrefab(randomFlyingBird2);
			if (HuntCatalog.CanHuntBird(prefab) && NeedsBirdDrop(prefab) && !MHR_Water.WouldSwim(_character, position2))
			{
				float num3 = Vector3.Distance(position, position2);
				if (num3 < num2)
				{
					num2 = num3;
					randomFlyingBird = randomFlyingBird2;
				}
			}
		}
		if (randomFlyingBird == null)
		{
			return false;
		}
		if (WeaponLogic.FindReadyBow(_humanoid, null) == null)
		{
			_noBowUntil = Time.time + 60f;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("A bird has landed nearby, but there's no bow or crossbow with ammo to shoot it");
			}
			return false;
		}
		_bird = randomFlyingBird;
		_birdName = BirdPrefab(randomFlyingBird);
		_birdPos = randomFlyingBird.transform.position;
		_birdBefore = MHR_Loot.ItemsAround(_birdPos);
		_birdSince = Time.time;
		_state = HuntState.Birding;
		MHR_Mover.Release(_ai);
		if (CompanionsPlugin.LogNormal)
		{
			Log("Going after a " + _birdName + " that landed " + num2.ToString("F0") + " m away" + NeedText(_birdName));
		}
		CompanionStatus.Doing(this, "hc_doing_hunt", MHR_Items.NiceName(randomFlyingBird.gameObject));
		return true;
	}

	private void UpdateBirding(bool follow)
	{
		if (_combatAI.IsEngaged)
		{
			EndBird();
			return;
		}
		ZNetView zNetView = ((_bird != null) ? _bird.GetComponent<ZNetView>() : null);
		if (_bird == null || zNetView == null || !zNetView.IsValid())
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Got the " + _birdName);
			}
			MHR_Loot.OnKillAt(_setup, _birdPos, _birdName, _birdBefore);
			EndBird();
			_nextCount = 0f;
			return;
		}
		_birdPos = _bird.transform.position;
		if (!Landed(_bird) || Time.time - _birdSince > 30f)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("The " + _birdName + (Landed(_bird) ? " - taking too long" : " flew off"));
			}
			_skip[_bird.GetInstanceID()] = Time.time + 30f;
			EndBird();
			return;
		}
		ItemDrop.ItemData currentWeapon = _humanoid.GetCurrentWeapon();
		if (currentWeapon == null || (currentWeapon.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow && currentWeapon.m_shared.m_aiAttackRange <= 10f))
		{
			ItemDrop.ItemData itemData = WeaponLogic.FindReadyBow(_humanoid, null);
			if (itemData == null)
			{
				_noBowUntil = Time.time + 60f;
				EndBird();
				return;
			}
			if (_setup != null)
			{
				_setup.SuppressAutoEquip = true;
				_birdBowOut = true;
			}
			_humanoid.EquipItem(itemData);
		}
		Vector3 position = transform.position;
		Vector3 vector = _birdPos + Vector3.up * 0.25f;
		float num = Vector3.Distance(position, _birdPos);
		if (num > 30f || !MHR_Mover.CanSee(position - Vector3.up * 0.2f, vector - Vector3.up * 1.6f))
		{
			MHR_Mover.Go(_ai, Time.deltaTime, _birdPos, 24f, allowRun: true, "Hunt", 2);
			if (MHR_Mover.GaveUp(_ai) || MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim())
			{
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Can't get a shot at the " + _birdName + ((MHR_Mover.RouteSwim(_ai) > MHR_Mover.JobSwim()) ? " without a long swim" : "") + " - leaving it");
				}
				_skip[_bird.GetInstanceID()] = Time.time + 120f;
				EndBird();
			}
			return;
		}
		_ai.StopMoving();
		_ai.LookAtPoint(vector);
		bool flag = MHR_BowDraw.NeedsDraw(_humanoid);
		if (Time.time < _nextShot || (_character.InAttack() && !MHR_BowDraw.IsDrawing(_humanoid)) || !_ai.IsLookingAtPoint(vector, flag ? 45f : 3f))
		{
			return;
		}
		CompanionProjectileAimPatch.AimAt(_humanoid, vector);
		bool flag2;
		if (flag)
		{
			flag2 = MHR_BowDraw.Tick(_humanoid, null, _ai.IsLookingAtPoint(vector, 3f), clear: true);
			if (!flag2)
			{
				return;
			}
		}
		else
		{
			flag2 = _character.StartAttack(null, charge: false);
		}
		_nextShot = Time.time + 1.5f;
		if (CompanionsPlugin.LogDetailed)
		{
			LogDebug((flag2 ? "Shooting at the " : "Couldn't shoot at the ") + _birdName + " (" + num.ToString("F0") + " m, " + (_humanoid.GetCurrentWeapon()?.m_shared?.m_name ?? "no weapon") + ")");
		}
	}

	private void ReleaseBirdBow()
	{
		if (_birdBowOut && _setup != null)
		{
			_birdBowOut = false;
			_setup.SuppressAutoEquip = false;
			_setup.SyncEquipmentToInventory();
		}
	}

	private void EndBird()
	{
		ReleaseBirdBow();
		_ai.MHR_SetCrouch(sneak: false);
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		_bird = null;
		_state = HuntState.Idle;
		_scanTimer = 1f;
	}

	private static bool Landed(RandomFlyingBird bird)
	{
		Vector3 position = bird.transform.position;
		return position.y - MHR_Water.GroundAt(position) < 0.8f;
	}

	private static string BirdPrefab(RandomFlyingBird bird)
	{
		ZNetView component = bird.GetComponent<ZNetView>();
		ZDO zDO = ((component != null && component.IsValid()) ? component.GetZDO() : null);
		GameObject gameObject = ((zDO != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zDO.GetPrefab()) : null);
		if (gameObject != null)
		{
			return gameObject.name;
		}
		string text = bird.gameObject.name;
		int num = text.IndexOf("(Clone)", StringComparison.Ordinal);
		if (num < 0)
		{
			return text;
		}
		return text.Substring(0, num).Trim();
	}

	private bool NeedsBirdDrop(string prefab)
	{
		return NeedsDrop(prefab, ModConfig.HuntFeatherTarget?.Value ?? 100);
	}

	private void UpdateGoingHome(bool follow)
	{
		if (_combatAI.IsEngaged)
		{
			return;
		}
		if (follow || !_setup.HasHomePosition())
		{
			Stop();
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
			Stop();
			return;
		}
		MHR_Mover.Go(_ai, Time.deltaTime, _setup.GetHomePosition(), 4f, allowRun: true, "Hunt");
		if (MHR_Mover.GaveUp(_ai))
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("Can't find the way home with the catch - trying again later");
			}
			_nextUnloadTry = Time.time + 60f;
			Stop();
		}
	}

	private bool PackFull()
	{
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		float num = ModConfig.HuntBagFull?.Value ?? 0.9f;
		if (inventory.GetEmptySlots() != 0)
		{
			return inventory.GetTotalWeight() >= CompanionTierData.CarryLimit(this) * num;
		}
		return true;
	}

	private void Skip(Character c, float seconds)
	{
		if (!(c == null))
		{
			if (_skip.Count > 100)
			{
				_skip.Clear();
			}
			_skip[c.GetInstanceID()] = Time.time + seconds;
		}
	}

	private Character FindPrey(bool follow, out string why)
	{
		why = null;
		Vector3 b = Center(follow);
		float num = Range(follow);
		Vector3 position = transform.position;
		float num2 = (follow ? (num + 10f) : (num + (ModConfig.HuntChaseExtra?.Value ?? 15f)));
		RefreshCounts(follow);
		Character character = null;
		float num3 = float.MaxValue;
		int num4 = 0;
		int num5 = 0;
		_whyItems.Clear();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter == null || allCharacter == _character || allCharacter.GetHealth() <= 0f || allCharacter.IsDead() || Vector3.Distance(allCharacter.transform.position, b) > num || Vector3.Distance(allCharacter.transform.position, position) > num2 || (_skip.TryGetValue(allCharacter.GetInstanceID(), out var value) && Time.time < value))
			{
				continue;
			}
			string prefab = PrefabName(allCharacter);
			if (!HuntCatalog.CanHunt(allCharacter, prefab))
			{
				continue;
			}
			if (!MHR_Loaded.Safe(allCharacter.transform.position, 12f))
			{
				continue;
			}
			if (_combatAI != null && _combatAI.LeavingAlone(allCharacter))
			{
				if (Time.time >= _nextTooStrongLog)
				{
					_nextTooStrongLog = Time.time + 30f;
					if (CompanionsPlugin.LogNormal)
					{
						Log("Not going after " + Loc(allCharacter.m_name) + " - it's leaving that one alone (too strong or out of reach)");
					}
				}
				continue;
			}
			num4++;
			if (!NeedsMeat(prefab))
			{
				num5++;
				List<string> list = HuntCatalog.MeatsOf(prefab);
				if (list == null || _whyItems.Count >= 8)
				{
					continue;
				}
				foreach (string item in list)
				{
					if (hashSet.Add(item))
					{
						_meatCount.TryGetValue(item, out var value2);
						_whyItems.Add(new CompanionStatus.Item(null, "hc_why_hunt_at_target", CookController.Name(item), value2, HuntCatalog.Target(prefab, item, ModConfig.HuntMeatTarget?.Value ?? 100)));
					}
				}
			}
			else if (!MHR_Water.WouldSwim(_character, allCharacter.transform.position))
			{
				float num6 = Vector3.Distance(position, allCharacter.transform.position);
				if (num6 < num3)
				{
					num3 = num6;
					character = allCharacter;
				}
			}
		}
		if (character == null)
		{
			ref string reference = ref why;
			string text;
			if (num4 != 0)
			{
				text = ((num5 == num4) ? "enough meat in store" : "only out of reach");
			}
			else
			{
				text = "no game within " + num.ToString("F0") + " m";
			}
			reference = text;
		}
		return character;
	}

	private bool NeedsMeat(string prefab)
	{
		return NeedsDrop(prefab, ModConfig.HuntMeatTarget?.Value ?? 100);
	}

	private bool NeedsDrop(string prefab, int dflt)
	{
		List<string> list = HuntCatalog.MeatsOf(prefab);
		if (list == null || list.Count == 0)
		{
			return false;
		}
		foreach (string item in list)
		{
			int num = HuntCatalog.Target(prefab, item, dflt);
			if (num <= 0 || !_meatCount.TryGetValue(item, out var value) || value < num)
			{
				return true;
			}
		}
		return false;
	}

	private string NeedText(string prefab)
	{
		int dflt = ((!HuntCatalog.CanHuntBird(prefab)) ? (ModConfig.HuntMeatTarget?.Value ?? 100) : (ModConfig.HuntFeatherTarget?.Value ?? 100));
		List<string> list = HuntCatalog.MeatsOf(prefab);
		if (list == null)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (string item in list)
		{
			int num = HuntCatalog.Target(prefab, item, dflt);
			_meatCount.TryGetValue(item, out var value);
			stringBuilder.Append((stringBuilder.Length == 0) ? " - " : ", ").Append(CookController.Name(item)).Append(' ')
				.Append(value)
				.Append('/')
				.Append((num > 0) ? num.ToString() : "no limit");
		}
		return stringBuilder.ToString();
	}

	private void RefreshCounts(bool follow)
	{
		if (Time.time < _nextCount)
		{
			return;
		}
		_nextCount = Time.time + 15f;
		_meatCount.Clear();
		Inventory inventory = _humanoid?.GetInventory();
		if (inventory != null)
		{
			Add(inventory);
		}
		Vector3 center;
		float radius;
		if (_setup.HasHomePosition())
		{
			center = _setup.GetHomePosition();
			radius = MHR_ToolKeeper.HomeRange();
		}
		else
		{
			center = (follow ? transform.position : Center(follow));
			radius = 25f;
		}
		foreach (Container item in MHR_Chests.Near(center, radius))
		{
			Inventory inventory2 = item?.GetInventory();
			if (inventory2 != null)
			{
				Add(inventory2);
			}
		}
	}

	private void Add(Inventory inv)
	{
		foreach (ItemDrop.ItemData allItem in inv.GetAllItems())
		{
			string text = allItem?.m_dropPrefab?.name;
			if (text != null && HuntCatalog.IsMeat(text))
			{
				_meatCount.TryGetValue(text, out var value);
				_meatCount[text] = value + allItem.m_stack;
			}
		}
	}

	internal static string PrefabName(Character c)
	{
		ZNetView component = c.GetComponent<ZNetView>();
		ZDO zDO = ((component != null && component.IsValid()) ? component.GetZDO() : null);
		GameObject gameObject = ((zDO != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zDO.GetPrefab()) : null);
		if (gameObject != null)
		{
			return gameObject.name;
		}
		string text = c.gameObject.name;
		int num = text.IndexOf("(Clone)", StringComparison.Ordinal);
		if (num < 0)
		{
			return text;
		}
		return text.Substring(0, num).Trim();
	}

	private static string Loc(string s)
	{
		try
		{
			return (Localization.instance != null) ? Localization.instance.Localize(s) : s;
		}
		catch
		{
			return s;
		}
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Hunt] " + msg);
		}
	}

	private static void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log?.LogDebug("[Hunt] " + msg);
		}
	}
}
