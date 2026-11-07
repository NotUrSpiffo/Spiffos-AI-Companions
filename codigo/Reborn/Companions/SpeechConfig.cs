using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Companions;

[Serializable]
public class SpeechConfig
{
	public int Version;

	public string[] Action;

	public string[] Wood;

	public string[] Stone;

	public string[] Ore;

	public string[] Gather;

	public string[] Forage;

	public string[] Combat;

	public string[] Follow;

	public string[] Hungry;

	public string[] Repair;

	public string[] Overweight;

	public string[] Smelt;

	public string[] Idle;

	public string[] Hunt;

	public string[] Farm;

	public string[] Fish;

	public string[] Cook;

	public string[] Upkeep;

	public string[] NoBed;

	public string[] Chat;

	public const int CurrentVersion = 3;

	private static SpeechConfig _instance;

	public static SpeechConfig Instance
	{
		get
		{
			if (_instance == null)
			{
				Load();
			}
			return _instance;
		}
	}

	public static void Load()
	{
		string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string text = "English";
		try
		{
			if (Localization.instance != null)
			{
				OfflineCompanionsAI.ModLang.UpdateGameLanguage(Localization.instance.GetSelectedLanguage());
				text = OfflineCompanionsAI.ModLang.IsEnglish ? "English" : "Spanish";
			}
		}
		catch
		{
		}
		if (!TryLoadFrom(Path.Combine(directoryName, "Translations", "speech", text + ".json"), upgradeOld: false)
			&& !TryLoadFrom(Path.Combine(directoryName, "OfflineCompanionsReborn", "Translations", "speech", text + ".json"), upgradeOld: false))
		{
			string path = Path.Combine(directoryName, "speech.json");
			if (!TryLoadFrom(path, upgradeOld: true))
			{
				_instance = Defaults();
				Write(path, "Created default");
			}
		}
	}

	public static void Reload()
	{
		_instance = null;
		Load();
	}

	private static void Write(string path, string what)
	{
		try
		{
			File.WriteAllText(path, JsonUtility.ToJson(_instance, prettyPrint: true));
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Speech] " + what + " " + path);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] Failed to write " + path + ": " + ex.Message);
		}
	}

	private static bool TryLoadFrom(string path, bool upgradeOld)
	{
		if (!File.Exists(path))
		{
			return false;
		}
		try
		{
			SpeechConfig speechConfig = JsonUtility.FromJson<SpeechConfig>(File.ReadAllText(path));
			if (speechConfig == null)
			{
				return false;
			}
			if (upgradeOld && speechConfig.Version < 3)
			{
				string text = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".v" + Mathf.Max(1, speechConfig.Version) + ".json");
				try
				{
					File.Copy(path, text, overwrite: true);
				}
				catch
				{
				}
				_instance = Defaults();
				Write(path, "Updated to the new speech lines (your old file is kept as " + Path.GetFileName(text) + ") -");
				return true;
			}
			_instance = speechConfig;
			if (_instance.FillMissing(Defaults()))
			{
				Write(path, "Added missing speech lines to");
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Speech] Loaded " + path);
			}
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Speech] Failed to parse " + path + ": " + ex.Message);
			return false;
		}
	}

	private bool FillMissing(SpeechConfig d)
	{
		bool result = false;
		FieldInfo[] fields = typeof(SpeechConfig).GetFields(BindingFlags.Instance | BindingFlags.Public);
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!(fieldInfo.FieldType != typeof(string[])) && !(fieldInfo.Name == "Gather") && (!(fieldInfo.GetValue(this) is string[] array) || array.Length == 0))
			{
				fieldInfo.SetValue(this, fieldInfo.GetValue(d));
				result = true;
			}
		}
		return result;
	}

	private static SpeechConfig Defaults()
	{
		SpeechConfig speechConfig = new SpeechConfig();
		speechConfig.Version = 3;
		speechConfig.Action = new string[10] { "By your word!", "So it shall be.", "Aye, consider it done!", "I heed your call.", "As you command.", "On it!", "Right away.", "It shall be done.", "Say no more.", "Leave it to me." };
		speechConfig.Wood = new string[8] { "Good timber here.", "Another tree falls!", "My arms grow stronger with each swing.", "Timber!", "This wood will burn well.", "[blackforest] The pines grow thick here.", "[meadows] Beech and birch, good and straight.", "[swamp] Ancient bark... tough going." };
		speechConfig.Stone = new string[5] { "There's good stone over here.", "Stone for the walls.", "Chip by chip.", "This rock won't move itself.", "Solid stone, this." };
		speechConfig.Ore = new string[7] { "This ore looks promising.", "The veins run deep in this rock.", "The ground gives up its treasures.", "Ore for the smelter.", "[blackforest] Copper and tin, our first real metal.", "[swamp] Iron, buried with the dead.", "[mountain] Silver, cold as the peaks." };
		speechConfig.Gather = new string[2] { "The land provides, if you know where to look.", "A fine haul so far." };
		speechConfig.Forage = new string[7] { "The wild provides for those who seek.", "Ripe for the picking.", "Nature hides her bounty well.", "These will make a fine stew.", "[meadows] The meadows offer their gifts.", "[blackforest] Good pickings under these trees.", "[night] Hard to spot anything in this dark." };
		speechConfig.Combat = new string[9] { "For Odin!", "Taste my steel!", "Stand and fight!", "They shall not pass!", "By Thor's hammer!", "You picked the wrong fight!", "I'll send you to Hel!", "Come then! I am ready!", "The Valkyries watch!" };
		speechConfig.Follow = new string[22]
		{
			"[away] Lead on, I am your shield.", "[away] Where the path takes us, I follow.", "Right behind you.", "I've got your back.", "[away] What lies ahead, I wonder?", "[away] Which way now?", "[away] Keep your eyes open.", "[away] Good country for an ambush.", "[home] Anything need doing around here?", "[home] Just say the word.",
			"[day,!rain] A fine day to wander the wilds.", "[night] Keep close, the night hides much.", "[rain] Wet boots again.", "[cold] My beard is freezing.", "[blackforest] Watch for greydwarfs in these woods.", "[swamp] This bog stinks of death.", "[mountain] Mind the drop.", "[plains] Watch for the fuling camps.", "[mistlands] I can barely see my own hand.", "[ashlands] Even the air burns here.",
			"[deepnorth] Cold enough to freeze a troll.", "[ocean] Nothing but water and sky."
		};
		speechConfig.Hungry = new string[5] { "My belly roars like a troll...", "I'd trade my axe for a leg of boar right now.", "Got anything to eat?", "A warrior fights on mead and meat, not empty guts.", "I could eat an entire neck right now." };
		speechConfig.Repair = new string[4] { "This blade has seen better days.", "My gear is wearing thin.", "I should find a workbench soon.", "A few more hits and this breaks." };
		speechConfig.Overweight = new string[4] { "My back is breaking under all this!", "I can barely move with all this weight!", "My pack is fit to burst!", "I need to put some of this down." };
		speechConfig.Smelt = new string[7] { "Feeding the furnace.", "Good ore makes good metal.", "Keeping the fires stoked.", "Another bar for the hoard.", "The bellows sing their song.", "Hot work, this.", "[cold] At least the furnace is warm." };
		speechConfig.Idle = new string[14]
		{
			"Skål!", "I could use a horn of mead.", "A moment of peace... rare in these lands.", "The world is quiet for now.", "Waiting for your command.", "[home] Nice place we've got here.", "[home] A fine hall you've built.", "[home] I could get used to this.", "[home,night] Good to be home for the night.", "[away] Strange place to stop.",
			"[away,night] I'd rather not linger out here after dark.", "[rain] At least the rain keeps the dust down.", "[day,!rain,!indoors] Just enjoying the view.", "[indoors] Cosy in here."
		};
		speechConfig.Hunt = new string[8] { "Stay quiet...", "The hunt is on!", "Fresh meat for the pot.", "Tracking prey through the brush.", "Easy now... nice and steady...", "Downwind, always downwind.", "[night] Hard to track in the dark.", "[rain] The rain hides our steps." };
		speechConfig.Farm = new string[5] { "Working the fields.", "Seeds in the ground, hope in the heart.", "Farm work is honest work.", "Another row done.", "[rain] Good weather for the crops." };
		speechConfig.Fish = new string[8] { "Casting my line...", "Patience is a fisherman's greatest virtue.", "Come on, take the bait...", "Fishing is peaceful work.", "Nothing yet... but I'll wait.", "[rain] Fish bite better in the rain.", "[night] Quiet water tonight.", "[ocean] Something big is down there." };
		speechConfig.Cook = new string[5] { "A good meal lifts any warrior's spirits.", "Stirring the pot...", "A pinch of this, a dash of that.", "Food for the weary!", "Mmm, smells good." };
		speechConfig.Upkeep = new string[7] { "Keeping the place in shape.", "A sound roof and a warm fire.", "Everything in its place.", "A home needs looking after.", "Who left this here?", "[rain] Let's hope the roof holds.", "[night] Keep the fires burning." };
		speechConfig.NoBed = new string[4] { "I've got no bed to call my own.", "Wish I had a bed around here.", "I'll sleep on the ground again, I suppose.", "A place to rest my head would be nice." };
		speechConfig.Chat = new string[8] { "Need anything?", "Anything need doing?", "Quiet around here.", "[day,!rain] Fine day for it.", "[rain] Smells like rain. Oh wait.", "[night] Late, isn't it?", "[home] Good to have a roof over us.", "[away] Should we head home soon?" };
		return speechConfig;
	}
}
