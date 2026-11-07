using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class CompanionStatus
{
	private class Entry
	{
		public string Text;

		public string Key;

		public string Label;

		public float At;
	}

	internal class Item
	{
		public string Label;

		public string Key;

		public object[] Args;

		public Item(string label, string key, params object[] args)
		{
			Label = label;
			Key = key;
			Args = args ?? new object[0];
		}
	}

	private static readonly Dictionary<int, Entry> s_why = new Dictionary<int, Entry>();

	private static readonly Dictionary<int, Entry> s_doing = new Dictionary<int, Entry>();

	private static readonly Dictionary<int, int> s_turn = new Dictionary<int, int>();

	internal const float DoingFresh = 90f;

	internal const float Fresh = 180f;

	internal static void Why(Component c, string key, bool speak = false, params object[] args)
	{
		WhyAs(c, null, key, speak, args);
	}

	internal static void WhyAs(Component c, string label, string key, bool speak, params object[] args)
	{
		if (!(c == null) && !string.IsNullOrEmpty(key))
		{
			string text = ((args != null && args.Length != 0) ? ModLocalization.LocFmt(key, args) : ModLocalization.Loc(key));
			int instanceID = c.gameObject.GetInstanceID();
			if (!s_why.TryGetValue(instanceID, out var value))
			{
				value = new Entry();
				s_why[instanceID] = value;
			}
			value.Text = text;
			value.Key = key;
			value.Label = label;
			value.At = Time.time;
			s_doing.Remove(instanceID);
			if (speak)
			{
				c.GetComponent<CompanionTalk>()?.SayProblem(key, text);
			}
		}
	}

	internal static void WhyRotate(Component c, List<Item> items)
	{
		if (!(c == null) && items != null && items.Count != 0)
		{
			int instanceID = c.gameObject.GetInstanceID();
			s_turn.TryGetValue(instanceID, out var value);
			s_turn[instanceID] = value + 1;
			Item item = items[value % items.Count];
			WhyAs(c, item.Label, item.Key, speak: false, item.Args);
		}
	}

	internal static void Doing(Component c, string key, params object[] args)
	{
		DoingAs(c, null, key, args);
	}

	internal static void DoingAs(Component c, string label, string key, params object[] args)
	{
		if (!(c == null) && !string.IsNullOrEmpty(key))
		{
			int instanceID = c.gameObject.GetInstanceID();
			if (!s_doing.TryGetValue(instanceID, out var value))
			{
				value = new Entry();
				s_doing[instanceID] = value;
			}
			value.Text = ((args != null && args.Length != 0) ? ModLocalization.LocFmt(key, args) : ModLocalization.Loc(key));
			value.Key = key;
			value.Label = label;
			value.At = Time.time;
			s_why.Remove(instanceID);
		}
	}

	internal static void DoingClear(Component c)
	{
		if (c != null)
		{
			s_doing.Remove(c.gameObject.GetInstanceID());
		}
	}

	private static Entry DoingEntry(Component c)
	{
		if (!(c != null) || !s_doing.TryGetValue(c.gameObject.GetInstanceID(), out var value) || !(Time.time - value.At <= 90f))
		{
			return null;
		}
		return value;
	}

	internal static void Clear(Component c)
	{
		if (c != null)
		{
			s_why.Remove(c.gameObject.GetInstanceID());
		}
	}

	internal static string Reason(Component c, float within = 180f)
	{
		if (c == null || !s_why.TryGetValue(c.gameObject.GetInstanceID(), out var value) || Time.time - value.At > within)
		{
			return null;
		}
		return value.Text;
	}

	internal static string SpokenReason(Component c, float within = 180f)
	{
		string text = Reason(c, within);
		if (text == null)
		{
			return null;
		}
		string label = s_why[c.gameObject.GetInstanceID()].Label;
		if (!string.IsNullOrEmpty(label))
		{
			return label + ": " + text;
		}
		return text;
	}

	private static string Label(Component c)
	{
		if (!(c != null) || !s_why.TryGetValue(c.gameObject.GetInstanceID(), out var value) || !(Time.time - value.At <= 180f))
		{
			return null;
		}
		return value.Label;
	}

	internal static string Describe(CompanionSetup cs)
	{
		if (cs == null)
		{
			return null;
		}
		Character component = cs.GetComponent<Character>();
		if (component == null || component.IsDead())
		{
			return null;
		}
		CompanionAI component2 = cs.GetComponent<CompanionAI>();
		CompanionRest component3 = cs.GetComponent<CompanionRest>();
		CompanionCombatAI component4 = cs.GetComponent<CompanionCombatAI>();
		IdleController component5 = cs.GetComponent<IdleController>();
		ZNetView component6 = cs.GetComponent<ZNetView>();
		int num = ((component6 != null && component6.GetZDO() != null) ? component6.GetZDO().GetInt(CompanionSetup.ActionModeHash) : 0);
		string text = JobLabel(num);
		if (component3 != null && component3.IsSleeping)
		{
			return L("hc_status_sleeping");
		}
		if (component2 != null && component2.IsInCombat && component4 != null && component4.TargetCreature != null)
		{
			string text2 = Localization.instance?.Localize(component4.TargetCreature.m_name) ?? component4.TargetCreature.m_name;
			if (!component4.HuntMode)
			{
				return ModLocalization.LocFmt("hc_status_fighting", text2);
			}
			return ModLocalization.LocFmt("hc_status_hunting", text2);
		}
		string text3 = MHR_Recover.StatusText(cs);
		if (text3 != null)
		{
			return text3;
		}
		if (MHR_SpearThrow.UnarmedAfterThrow(component2, cs.GetComponent<Humanoid>()))
		{
			return L("hc_status_spear");
		}
		if (component3 != null && component3.IsResting)
		{
			return L("hc_status_resting");
		}
		if (MHR_JobRules.OtherOrder(cs))
		{
			return L("hc_status_order");
		}
		string text4 = MHR_ToolKeeper.TripText(cs);
		if (text4 != null)
		{
			return text4;
		}
		string text5 = Reason(cs);
		if (num != 0 && num != 4 && text != null)
		{
			text = Label(cs) ?? text;
			if (component5 != null && component5.IsActive)
			{
				return text + " - " + (text5 ?? L("hc_status_nothing"));
			}
			if (cs.GetFollow() && component2 != null && component2.GetFollowTarget() != null && !JobBusy(cs, num))
			{
				return text + " - " + (text5 ?? L("hc_status_following_look"));
			}
			string text6 = FarmDoing(cs, num);
			if (text6 == null)
			{
				Entry entry = DoingEntry(cs);
				if (entry != null)
				{
					return (entry.Label ?? text) + " - " + entry.Text;
				}
			}
			if (text6 == null)
			{
				return text;
			}
			return text + " - " + text6;
		}
		if (cs.GetFollow() && component2 != null && component2.GetFollowTarget() != null)
		{
			return L("hc_status_following");
		}
		if (component5 != null && component5.IsActive)
		{
			return L("hc_status_idle");
		}
		if (num != 4)
		{
			return L("hc_status_waiting");
		}
		return L("hc_status_staying");
	}

	private static string FarmDoing(CompanionSetup cs, int mode)
	{
		if (mode == 8)
		{
			FarmController component = cs.GetComponent<FarmController>();
			if (!(component != null) || !component.IsActive)
			{
				return null;
			}
			return component.DoingText();
		}
		return null;
	}

	private static bool JobBusy(CompanionSetup cs, int mode)
	{
		switch (mode)
		{
		case 1:
		case 2:
		case 3:
		case 5:
			return cs.GetComponent<HarvestController>()?.IsActive ?? false;
		case 7:
			return cs.GetComponent<HuntController>()?.IsActive ?? false;
		case 9:
			return cs.GetComponent<FishController>()?.IsActive ?? false;
		default:
			return false;
		}
	}

	internal static string JobLabel(int mode)
	{
		return mode switch
		{
			1 => L("hc_status_job_wood"), 
			2 => L("hc_status_job_stone"), 
			3 => L("hc_status_job_ore"), 
			5 => L("hc_status_job_forage"), 
			6 => L("hc_status_job_smelt"), 
			7 => L("hc_status_job_hunt"), 
			8 => L("hc_status_job_farm"), 
			9 => L("hc_status_job_fish"), 
			15 => L("hc_status_job_upkeep"), 
			17 => L("hc_status_job_cook"), 
			_ => null, 
		};
	}

	private static string L(string key)
	{
		return ModLocalization.Loc(key);
	}
}
