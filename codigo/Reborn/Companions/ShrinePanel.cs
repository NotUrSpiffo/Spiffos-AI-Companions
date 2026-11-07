using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Companions;

public class ShrinePanel : MonoBehaviour
{
	private static ShrinePanel _instance;

	private CompanionShrine _shrine;

	private bool _active;

	private float _openedAt;

	private const float CloseDistance = 4f;

	private static float s_closedAt = -10f;

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_leftText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonLeftText");

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_rightText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonRightText");

	private static readonly AccessTools.FieldRef<UnifiedPopup, TMP_Text> s_centerText = AccessTools.FieldRefAccess<UnifiedPopup, TMP_Text>("buttonCenterText");

	private static readonly FieldInfo s_popupInstance = AccessTools.Field(typeof(UnifiedPopup), "instance");

	internal static bool IsOpen
	{
		get
		{
			if (_instance != null)
			{
				return _instance._active;
			}
			return false;
		}
	}

	internal static bool JustClosed => Time.time - s_closedAt < 0.3f;

	internal static void Show(CompanionShrine shrine)
	{
		if (UnifiedPopup.IsAvailable() && !UnifiedPopup.IsVisible())
		{
			if (_instance == null)
			{
				GameObject gameObject = new GameObject("HC_ShrinePanel");
				Object.DontDestroyOnLoad(gameObject);
				_instance = gameObject.AddComponent<ShrinePanel>();
			}
			_instance._shrine = shrine;
			_instance._openedAt = Time.time;
			_instance.ShowMenu();
		}
	}

	private void ShowMenu()
	{
		string text = ModLocalization.Loc("hc_shrine_flavor") + "\n\n" + ModLocalization.LocFmt("hc_shrine_offering", CompanionTierData.Price.ToString("N0"));
		Push(new YesNoPopup(ModLocalization.Loc("hc_shrine_title"), text, Summon, ShowRecover, localizeText: false), ModLocalization.Loc("hc_shrine_btn_summon"), ModLocalization.Loc("hc_shrine_btn_recover_list"), null);
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Shrine] Menu opened");
		}
	}

	private void ShowRecover()
	{
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> roster = MHR_Roster.GetRoster();
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Shrine] Recover list - {roster.Count} companion(s) on the roster");
		}
		List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> candidates = new List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>>();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> item in roster)
		{
			stringBuilder.Append(item.Key.Name).Append(" - ").Append(StatusText(item.Value))
				.Append('\n');
			if (CanRecover(item.Value))
			{
				candidates.Add(item);
			}
		}
		candidates.Sort((KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> a, KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status> b) => Order(a.Value).CompareTo(Order(b.Value)));
		string text = ((roster.Count == 0) ? ModLocalization.Loc("hc_shrine_none") : stringBuilder.ToString().TrimEnd(new char[1] { '\n' }));
		string header = ModLocalization.Loc("hc_shrine_recover_title");
		if (candidates.Count == 0)
		{
			Push(new WarningPopup(header, text, ShowMenu, localizeText: false), null, null, ModLocalization.Loc("hc_shrine_back"));
			return;
		}
		Push(new YesNoPopup(header, text, () =>
		{
			AskRecover(candidates, 0);
		}, ShowMenu, localizeText: false), ModLocalization.Loc("hc_shrine_btn_recover"), ModLocalization.Loc("hc_shrine_back"), null);
	}

	private static bool CanRecover(MHR_Roster.Status s)
	{
		if (s != MHR_Roster.Status.Lost && s != MHR_Roster.Status.Unknown)
		{
			return s == MHR_Roster.Status.Unsummoned;
		}
		return true;
	}

	private static int Order(MHR_Roster.Status s)
	{
		return s switch
		{
			MHR_Roster.Status.Unsummoned => 1, 
			MHR_Roster.Status.Lost => 0, 
			_ => 2, 
		};
	}

	private static string StatusText(MHR_Roster.Status s)
	{
		return s switch
		{
			MHR_Roster.Status.Alive => "<color=#80E080>" + ModLocalization.Loc("hc_shrine_alive") + "</color>", 
			MHR_Roster.Status.Dead => "<color=#C0C0C0>" + ModLocalization.Loc("hc_shrine_dead") + "</color>", 
			MHR_Roster.Status.Lost => "<color=#FF7060>" + ModLocalization.Loc("hc_shrine_lost") + "</color>", 
			MHR_Roster.Status.Unsummoned => "<color=#FF9F50>" + ModLocalization.Loc("hc_shrine_unsummoned") + "</color>", 
			_ => "<color=#FFCC50>" + ModLocalization.Loc("hc_shrine_unknown") + "</color>", 
		};
	}

	private void AskRecover(List<KeyValuePair<MHR_Roster.Entry, MHR_Roster.Status>> candidates, int index)
	{
		if (index >= candidates.Count)
		{
			ShowRecover();
			return;
		}
		MHR_Roster.Entry entry = candidates[index].Key;
		MHR_Roster.Status status = candidates[index].Value;
		string text = "Bring " + entry.Name + " back here? It returns with its gear and settings from the last roster save. If the old " + entry.Name + " turns up later, it will be removed.";
		if (status == MHR_Roster.Status.Unsummoned)
		{
			text = "Bring " + entry.Name + " back here? It returns with its looks, skills and settings, but without its gear - that stays in the tombstone where it was unsummoned.";
		}
		else if (status == MHR_Roster.Status.Unknown)
		{
			text = text + "\n\n<color=#FFCC50>Warning:</color> " + entry.Name + " may still be alive somewhere that isn't loaded" + (entry.Dead ? " (last known: it died, and its tombstone keeps its items too)." : ".");
		}
		Push(new YesNoPopup(ModLocalization.Loc("hc_shrine_recover_title"), text, () =>
		{
			Close();
			CompanionShrine shrine = _shrine;
			if (shrine != null)
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo($"[Shrine] Recovering {entry.Name} ({status})");
				}
				MHR_Roster.RecoverAt(entry, shrine.transform.position + shrine.transform.forward * 2f);
			}
		}, () =>
		{
			AskRecover(candidates, index + 1);
		}, localizeText: false), null, null, null);
	}

	private void Summon()
	{
		int price = CompanionTierData.Price;
		int num = CompanionManager.CoinsInPack();
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Shrine] Summon - offering {price}, you have {num} coins");
		}
		Close();
		if (num < price)
		{
			CompanionManager.ShowShortPopup(price, num);
		}
		else
		{
			StarterCompanionPanel.ShowHirePanel();
		}
	}

	private void Push(PopupBase popup, string right, string left, string center)
	{
		if (_active && UnifiedPopup.IsVisible())
		{
			UnifiedPopup.Pop();
		}
		UnifiedPopup.Push(popup);
		_active = true;
		UnifiedPopup unifiedPopup = s_popupInstance?.GetValue(null) as UnifiedPopup;
		if (!(unifiedPopup == null))
		{
			SetLabel(s_rightText(unifiedPopup), right);
			SetLabel(s_leftText(unifiedPopup), left);
			SetLabel(s_centerText(unifiedPopup), center);
		}
	}

	private static void SetLabel(TMP_Text t, string label)
	{
		if (t != null && label != null)
		{
			t.text = label;
		}
	}

	private void Update()
	{
		if (!_active)
		{
			return;
		}
		if (!UnifiedPopup.IsVisible())
		{
			_active = false;
			s_closedAt = Time.time;
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		bool flag = Time.time - _openedAt > 0.3f && (ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse"));
		bool flag2 = _shrine == null || localPlayer == null || localPlayer.IsDead() || Vector3.Distance(localPlayer.transform.position, _shrine.transform.position) > 4f;
		if ((Input.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB")) | flag | flag2)
		{
			if (flag2 && CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Shrine] Menu closed - walked away");
			}
			Close();
		}
	}

	private void Close()
	{
		if (_active && UnifiedPopup.IsVisible())
		{
			UnifiedPopup.Pop();
		}
		_active = false;
		s_closedAt = Time.time;
	}

	private void OnDestroy()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}
}
