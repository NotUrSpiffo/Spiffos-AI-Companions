using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

public class CompanionTalk : MonoBehaviour
{
	public enum Priority
	{
		Ambient,
		Event,
		Problem,
		Order
	}

	private struct Pending
	{
		public string Text;

		public string Topic;

		public Priority Prio;

		public float Until;
	}

	private ZNetView _nview;

	private Character _character;

	private Humanoid _humanoid;

	private CompanionFood _food;

	private CompanionSetup _setup;

	private CompanionRest _rest;

	private CompanionAI _ai;

	private IdleController _idle;

	private float _talkTimer;

	private const float RepairThreshold = 0.25f;

	private float _lastSayTime = float.NegativeInfinity;

	private static float _globalLastSpeechTime = float.NegativeInfinity;

	private readonly List<Pending> _queue = new List<Pending>();

	private readonly Dictionary<string, float> _topicUntil = new Dictionary<string, float>();

	private static readonly Dictionary<string[], int> s_lastPick = new Dictionary<string[], int>();

	private const float ProblemRepeat = 300f;

	private const float EventRepeat = 120f;

	private const float NeedRepeat = 600f;

	private static float CullDistance => ModConfig.SpeechCullDistance.Value;

	private static float SpeechTTL => ModConfig.SpeechDuration.Value;

	private static float SayCooldown => ModConfig.SpeechSayCooldown.Value;

	private static bool Enabled => ModConfig.SpeechEnabled?.Value ?? true;

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
		_humanoid = GetComponent<Humanoid>();
		_food = GetComponent<CompanionFood>();
		_setup = GetComponent<CompanionSetup>();
		_rest = GetComponent<CompanionRest>();
		_ai = GetComponent<CompanionAI>();
		_idle = GetComponent<IdleController>();
		ResetTimer();
		_talkTimer += UnityEngine.Random.Range(0f, _talkTimer * 0.5f);
	}

	public void Say(string text, bool force = false)
	{
		Speak(text, (!force) ? Priority.Event : Priority.Order, null);
	}

	public void SayOrder(string text)
	{
		Speak(text, Priority.Order, null);
	}

	public void SayProblem(string topic, string text)
	{
		Speak(text, Priority.Problem, topic);
	}

	public void Speak(string text, Priority prio, string topic)
	{
		Speak(text, prio, topic, -1f);
	}

	public void Speak(string text, Priority prio, string topic, float repeat)
	{
		if (string.IsNullOrEmpty(text) || !Enabled || Chat.instance == null)
		{
			return;
		}
		float time = Time.time;
		if (topic != null)
		{
			if (_topicUntil.TryGetValue(topic, out var value) && time < value)
			{
				return;
			}
			if (repeat >= 0f)
			{
				_topicUntil[topic] = time + repeat;
			}
			else
			{
				switch (prio)
				{
				case Priority.Problem:
					_topicUntil[topic] = time + 300f;
					break;
				case Priority.Event:
					_topicUntil[topic] = time + 120f;
					break;
				}
			}
		}
		if (TryShow(text, prio) || prio == Priority.Ambient)
		{
			return;
		}
		for (int i = 0; i < _queue.Count; i++)
		{
			if (_queue[i].Text == text)
			{
				return;
			}
		}
		if (_queue.Count >= 4)
		{
			_queue.RemoveAt(0);
		}
		_queue.Add(new Pending
		{
			Text = text,
			Topic = topic,
			Prio = prio,
			Until = time + ((prio == Priority.Event) ? 6f : 60f)
		});
	}

	private bool TryShow(string text, Priority prio)
	{
		float time = Time.time;
		if (prio != Priority.Order && (time - _lastSayTime < SayCooldown || time - _globalLastSpeechTime < SayCooldown))
		{
			return false;
		}
		Player localPlayer = Player.m_localPlayer;
		if (!(localPlayer != null) || !(Vector3.Distance(transform.position, localPlayer.transform.position) <= CullDistance))
		{
			if (prio == Priority.Problem)
			{
				ConfigEntry<bool> speechFarMessages = ModConfig.SpeechFarMessages;
				if ((speechFarMessages == null || speechFarMessages.Value) && MessageHud.instance != null)
				{
					MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, Name() + ": " + text);
					if (CompanionsPlugin.LogDetailed)
					{
						CompanionsPlugin.Log.LogDebug("[Speech] (far away, top left) " + Name() + ": " + text);
					}
				}
			}
			return true;
		}
		_lastSayTime = time;
		_globalLastSpeechTime = time;
		Chat.instance.SetNpcText(gameObject, Vector3.up * 2f, CullDistance, SpeechTTL, "", text, large: false);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Speech] " + prio.ToString() + " " + Name() + ": " + text);
		}
		return true;
	}

	private string Name()
	{
		if (_character == null)
		{
			return "?";
		}
		string text = _character.m_name;
		string text2;
		if (!string.IsNullOrEmpty(text))
		{
			text2 = Localization.instance?.Localize(text);
			if (text2 == null)
			{
				return text;
			}
		}
		else
		{
			text2 = "?";
		}
		return text2;
	}

	private void ResetTimer()
	{
		string text = ModConfig.SpeechChatter?.Value ?? "Normal";
		float minInclusive = 90f;
		float maxInclusive = 180f;
		if (text == "Rare")
		{
			minInclusive = 240f;
			maxInclusive = 480f;
		}
		else if (text == "Often")
		{
			minInclusive = 30f;
			maxInclusive = 60f;
		}
		_talkTimer = UnityEngine.Random.Range(minInclusive, maxInclusive);
	}

	private void Update()
	{
		if (_nview == null || !_nview.IsOwner() || _character == null || _character.GetHealth() <= 0f || Chat.instance == null || Player.m_localPlayer == null || (_setup != null && (CompanionRadialMenu.IsOpenFor(_setup) || CompanionInteractPanel.IsOpenFor(_setup))))
		{
			return;
		}
		if (_queue.Count > 0)
		{
			float time = Time.time;
			int num = -1;
			for (int num2 = _queue.Count - 1; num2 >= 0; num2--)
			{
				if (time > _queue[num2].Until)
				{
					_queue.RemoveAt(num2);
				}
			}
			for (int i = 0; i < _queue.Count; i++)
			{
				if (num < 0 || _queue[i].Prio > _queue[num].Prio)
				{
					num = i;
				}
			}
			if (num >= 0 && TryShow(_queue[num].Text, _queue[num].Prio))
			{
				_queue.RemoveAt(num);
			}
		}
		if (!((ModConfig.SpeechChatter?.Value ?? "Normal") == "Off") && Enabled && !(Vector3.Distance(transform.position, Player.m_localPlayer.transform.position) > CullDistance))
		{
			_talkTimer -= Time.deltaTime;
			if (_talkTimer <= 0f)
			{
				ResetTimer();
				SayContextual();
			}
		}
	}

	private void SayAmbient(string[] pool, string topic = null, float repeat = 0f)
	{
		string text = Pick(pool);
		if (text == null)
		{
			return;
		}
		if (topic != null)
		{
			if (_topicUntil.TryGetValue(topic, out var value) && Time.time < value)
			{
				return;
			}
			_topicUntil[topic] = Time.time + repeat;
		}
		Speak(text, Priority.Ambient, null);
	}

	internal string Pick(string[] pool)
	{
		if (pool == null || pool.Length == 0)
		{
			return null;
		}
		List<int> list = new List<int>();
		for (int i = 0; i < pool.Length; i++)
		{
			if (!string.IsNullOrEmpty(pool[i]) && Fits(pool[i], out var _))
			{
				list.Add(i);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		s_lastPick.TryGetValue(pool, out var value);
		if (list.Count > 1)
		{
			list.Remove(value);
		}
		int num = list[UnityEngine.Random.Range(0, list.Count)];
		s_lastPick[pool] = num;
		Fits(pool[num], out var text2);
		return text2;
	}

	private bool Fits(string line, out string text)
	{
		text = line;
		if (line.Length == 0 || line[0] != '[')
		{
			return true;
		}
		int num = line.IndexOf(']');
		if (num < 0)
		{
			return true;
		}
		text = line.Substring(num + 1).TrimStart(Array.Empty<char>());
		string[] array = line.Substring(1, num - 1).Split(new char[1] { ',' });
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim().ToLowerInvariant();
			if (text2.Length != 0)
			{
				bool flag = text2[0] == '!';
				if (flag)
				{
					text2 = text2.Substring(1);
				}
				if (Holds(text2) == flag)
				{
					return false;
				}
			}
		}
		return true;
	}

	private bool Holds(string tag)
	{
		Vector3 position = transform.position;
		switch (tag)
		{
		case "home":
			return AtHome();
		case "away":
			return !AtHome();
		case "night":
			return EnvMan.IsNight();
		case "day":
			return !EnvMan.IsNight();
		case "rain":
			return EnvMan.IsWet();
		case "dry":
			return !EnvMan.IsWet();
		case "cold":
			return EnvMan.IsCold();
		case "indoors":
			return Indoors();
		case "outdoors":
			return !Indoors();
		default:
		{
			Heightmap.Biome biome = ((WorldGenerator.instance != null) ? WorldGenerator.instance.GetBiome(position) : Heightmap.Biome.None);
			return tag switch
			{
				"meadows" => biome == Heightmap.Biome.Meadows, 
				"blackforest" => biome == Heightmap.Biome.BlackForest, 
				"swamp" => biome == Heightmap.Biome.Swamp, 
				"mountain" => biome == Heightmap.Biome.Mountain, 
				"plains" => biome == Heightmap.Biome.Plains, 
				"mistlands" => biome == Heightmap.Biome.Mistlands, 
				"ashlands" => biome == Heightmap.Biome.AshLands, 
				"deepnorth" => biome == Heightmap.Biome.DeepNorth, 
				"ocean" => biome == Heightmap.Biome.Ocean, 
				_ => false, 
			};
		}
		}
	}

	private bool AtHome()
	{
		if (_setup != null && _setup.HasHomePosition())
		{
			return Vector3.Distance(transform.position, _setup.GetHomePosition()) <= MHR_ToolKeeper.AtHomeRange();
		}
		return false;
	}

	private bool Indoors()
	{
		if (_character != null && _character.InInterior())
		{
			return true;
		}
		return Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.up, 30f, LayerMask.GetMask("piece", "static_solid", "Default"));
	}

	private void SayContextual()
	{
		SpeechConfig instance = SpeechConfig.Instance;
		if ((_rest != null && (_rest.IsSleeping || _rest.IsResting || _rest.IsNavigating)) || _character.IsSwimming() || (_ai != null && (_ai.IsOnShip || _ai.IsBoardingShip)))
		{
			return;
		}
		CompanionCombatAI component = GetComponent<CompanionCombatAI>();
		if (_ai != null && _ai.IsInCombat)
		{
			if (component != null && !component.HuntMode)
			{
				SayAmbient(instance.Combat);
			}
		}
		else
		{
			if (MHR_JobRules.OtherOrder(_setup))
			{
				return;
			}
			if (IsOverweight())
			{
				SayAmbient(instance.Overweight, "need:weight", 600f);
				return;
			}
			if (IsHungry())
			{
				SayAmbient(instance.Hungry, "need:food", 600f);
				return;
			}
			if (NeedsRepair())
			{
				SayAmbient(instance.Repair, "need:repair", 600f);
				return;
			}
			if (_rest != null && _rest.NeedsBed() && EnvMan.IsNight() && AtHome())
			{
				SayAmbient(instance.NoBed, "need:bed", 600f);
				return;
			}
			int num = _nview.GetZDO()?.GetInt(CompanionSetup.ActionModeHash) ?? 0;
			string[] array = JobPool(instance, num);
			if (array != null)
			{
				SayAmbient(array);
			}
			else if (_idle != null && _idle.IsActive)
			{
				SayAmbient(instance.Idle);
			}
			else if (_setup != null && _setup.GetFollow() && _ai != null && _ai.GetFollowTarget() != null)
			{
				SayAmbient((UnityEngine.Random.value < 0.25f) ? instance.Idle : instance.Follow);
			}
			else if (num == 4 || num == 0)
			{
				SayAmbient(instance.Idle);
			}
		}
	}

	private string[] JobPool(SpeechConfig s, int mode)
	{
		switch (mode)
		{
		case 1:
			if (!Busy<HarvestController>())
			{
				return null;
			}
			return Or(s.Wood, s.Gather);
		case 2:
			if (!Busy<HarvestController>())
			{
				return null;
			}
			return Or(s.Stone, s.Gather);
		case 3:
			if (!Busy<HarvestController>())
			{
				return null;
			}
			return Or(s.Ore, s.Gather);
		case 5:
			if (!Busy<HarvestController>())
			{
				return null;
			}
			return s.Forage;
		case 6:
			if (!Busy<SmeltController>())
			{
				return null;
			}
			return s.Smelt;
		case 7:
			if (!Busy<HuntController>())
			{
				return null;
			}
			return s.Hunt;
		case 8:
			if (!Busy<FarmController>())
			{
				return null;
			}
			return s.Farm;
		case 9:
			if (!Busy<FishController>())
			{
				return null;
			}
			return s.Fish;
		case 17:
			if (!Busy<CookController>())
			{
				return null;
			}
			return s.Cook;
		case 15:
			if (!Busy<UpkeepController>())
			{
				return null;
			}
			return s.Upkeep;
		default:
			return null;
		}
	}

	private static string[] Or(string[] a, string[] b)
	{
		if (a == null || a.Length == 0)
		{
			return b;
		}
		return a;
	}

	private bool Busy<T>() where T : MonoBehaviour
	{
		if (_idle != null && _idle.IsActive)
		{
			return false;
		}
		T component = GetComponent<T>();
		if (component == null)
		{
			return false;
		}
		if (!(component is HarvestController harvestController))
		{
			if (!(component is SmeltController smeltController))
			{
				if (!(component is HuntController huntController))
				{
					if (!(component is FarmController farmController))
					{
						if (!(component is FishController fishController))
						{
							if (!(component is CookController cookController))
							{
								if (component is UpkeepController upkeepController)
								{
									return upkeepController.IsActive;
								}
								return false;
							}
							return cookController.IsActive;
						}
						return fishController.IsActive;
					}
					return farmController.IsActive;
				}
				return huntController.IsActive;
			}
			return smeltController.IsActive;
		}
		return harvestController.IsActive;
	}

	private bool IsOverweight()
	{
		if (_humanoid == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory != null)
		{
			return inventory.GetTotalWeight() >= 298f;
		}
		return false;
	}

	private bool IsHungry()
	{
		if (_setup != null && !_setup.CanWearArmor())
		{
			return false;
		}
		if (_food == null)
		{
			return false;
		}
		for (int i = 0; i < CompanionFood.MaxFoodSlots; i++)
		{
			if (_food.GetFood(i).IsActive)
			{
				return false;
			}
		}
		return true;
	}

	private bool NeedsRepair()
	{
		if (_humanoid == null)
		{
			return false;
		}
		Inventory inventory = _humanoid.GetInventory();
		if (inventory == null)
		{
			return false;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			if (allItem.m_shared.m_useDurability && _humanoid.IsItemEquiped(allItem))
			{
				float maxDurability = allItem.GetMaxDurability();
				if (maxDurability > 0f && allItem.m_durability / maxDurability < 0.25f)
				{
					return true;
				}
			}
		}
		return false;
	}
}
