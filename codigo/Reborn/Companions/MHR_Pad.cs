using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class MHR_Pad
{
	[HarmonyPatch(typeof(ZInput), "TryGetButtonState")]
	private static class HidePatch
	{
		private static bool Prefix(string name, ref bool __result)
		{
			if (Hide(name))
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	internal static readonly string[] Choices = new string[19]
	{
		"None", "Use", "Alt", "A", "B", "X", "Y", "LB", "RB", "LT",
		"RT", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "LStick", "RStick", "Back", "Start"
	};

	internal static readonly string[] Physical = new string[16]
	{
		"A", "B", "X", "Y", "LB", "RB", "LT", "RT", "DPadUp", "DPadDown",
		"DPadLeft", "DPadRight", "LStick", "RStick", "Back", "Start"
	};

	private static bool s_own;

	private static readonly AccessTools.FieldRef<ZInput, Dictionary<string, ZInput.ButtonDef>> s_buttons = AccessTools.FieldRefAccess<ZInput, Dictionary<string, ZInput.ButtonDef>>("m_buttons");

	private static readonly Dictionary<string, string> s_paths = new Dictionary<string, string>();

	private static float s_pathsAt = -100f;

	private static readonly HashSet<string> s_hidden = new HashSet<string>();

	private static int s_hiddenFrame = -1;

	internal static string Joy(string v)
	{
		return v switch
		{
			"Use" => "JoyUse", 
			"Alt" => "JoyAltKeys", 
			"A" => "JoyButtonA", 
			"B" => "JoyButtonB", 
			"X" => "JoyButtonX", 
			"Y" => "JoyButtonY", 
			"LB" => "JoyLBumper", 
			"RB" => "JoyRBumper", 
			"LT" => "JoyLTrigger", 
			"RT" => "JoyRTrigger", 
			"DPadUp" => "JoyDPadUp", 
			"DPadDown" => "JoyDPadDown", 
			"DPadLeft" => "JoyDPadLeft", 
			"DPadRight" => "JoyDPadRight", 
			"LStick" => "JoyLStick", 
			"RStick" => "JoyRStick", 
			"Back" => "JoyBack", 
			"Start" => "JoyStart", 
			_ => null, 
		};
	}

	private static bool Raw(string joy, int kind)
	{
		if (joy == null)
		{
			return false;
		}
		bool flag = s_own;
		s_own = true;
		try
		{
			return kind switch
			{
				1 => ZInput.GetButton(joy), 
				0 => ZInput.GetButtonDown(joy), 
				_ => ZInput.GetButtonUp(joy), 
			};
		}
		finally
		{
			s_own = flag;
		}
	}

	private static IEnumerable<KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>> Binds()
	{
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadCommand, ModConfig.PadCommandModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadWheel, ModConfig.PadWheelModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadSettings, ModConfig.PadSettingsModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadFarmZone, ModConfig.PadFarmZoneModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadPassive, ModConfig.PadPassiveModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadBalanced, ModConfig.PadBalancedModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadRanged, ModConfig.PadRangedModifier);
		yield return new KeyValuePair<ConfigEntry<string>, ConfigEntry<string>>(ModConfig.PadMelee, ModConfig.PadMeleeModifier);
	}

	internal static bool IsSet(ConfigEntry<string> b)
	{
		if (b != null)
		{
			return Joy(b.Value) != null;
		}
		return false;
	}

	internal static bool Plain(ConfigEntry<string> b, ConfigEntry<string> m, string value)
	{
		if (b != null && b.Value == value)
		{
			if (m != null)
			{
				return Joy(m.Value) == null;
			}
			return true;
		}
		return false;
	}

	private static bool ModOk(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		string text = ((m != null) ? Joy(m.Value) : null);
		if (text != null)
		{
			return Raw(text, 1);
		}
		string text2 = PathOf(Joy(b.Value));
		foreach (KeyValuePair<ConfigEntry<string>, ConfigEntry<string>> item in Binds())
		{
			string text3 = ((item.Value != null) ? Joy(item.Value.Value) : null);
			if (text3 != null && item.Key != null && PathOf(Joy(item.Key.Value)) == text2 && Raw(text3, 1))
			{
				return false;
			}
		}
		return true;
	}

	internal static bool Down(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		if (IsSet(b) && Raw(Joy(b.Value), 0))
		{
			return ModOk(b, m);
		}
		return false;
	}

	internal static bool Held(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		if (IsSet(b) && Raw(Joy(b.Value), 1))
		{
			return ModOk(b, m);
		}
		return false;
	}

	internal static bool Up(ConfigEntry<string> b)
	{
		if (IsSet(b))
		{
			return Raw(Joy(b.Value), 2);
		}
		return false;
	}

	internal static string PathOf(string joy)
	{
		if (joy == null || ZInput.instance == null)
		{
			return null;
		}
		float unscaledTime = Time.unscaledTime;
		if (unscaledTime - s_pathsAt > 5f)
		{
			s_pathsAt = unscaledTime;
			s_paths.Clear();
		}
		if (s_paths.TryGetValue(joy, out var value))
		{
			return value;
		}
		value = null;
		try
		{
			Dictionary<string, ZInput.ButtonDef> dictionary = s_buttons(ZInput.instance);
			if (dictionary != null && dictionary.TryGetValue(joy, out var value2) && value2 != null)
			{
				value = value2.GetActionPath();
			}
		}
		catch
		{
		}
		s_paths[joy] = value;
		return value;
	}

	private static bool Hide(string name)
	{
		if (s_own || name == null || !name.StartsWith("Joy") || ZInput.instance == null)
		{
			return false;
		}
		int frameCount = Time.frameCount;
		if (frameCount != s_hiddenFrame)
		{
			s_hiddenFrame = frameCount;
			s_hidden.Clear();
			foreach (KeyValuePair<ConfigEntry<string>, ConfigEntry<string>> item in Binds())
			{
				string text = ((item.Key != null) ? Joy(item.Key.Value) : null);
				string text2 = ((item.Value != null) ? Joy(item.Value.Value) : null);
				if (text != null && text2 != null && Raw(text2, 1))
				{
					string text3 = PathOf(text);
					if (text3 != null && text3 != PathOf(text2))
					{
						s_hidden.Add(text3);
					}
				}
			}
		}
		if (s_hidden.Count == 0)
		{
			return false;
		}
		string text4 = PathOf(name);
		if (text4 != null)
		{
			return s_hidden.Contains(text4);
		}
		return false;
	}

	internal static string Glyph(string v)
	{
		string text = Joy(v);
		if (text == null || ZInput.instance == null)
		{
			return v;
		}
		string boundKeyString = ZInput.instance.GetBoundKeyString(text, emptyStringOnMissing: true);
		if (string.IsNullOrEmpty(boundKeyString))
		{
			return v;
		}
		if (Localization.instance == null)
		{
			return boundKeyString;
		}
		return Localization.instance.Localize(boundKeyString);
	}

	internal static string Show(ConfigEntry<string> b, ConfigEntry<string> m)
	{
		if (!IsSet(b))
		{
			return null;
		}
		string text = Glyph(b.Value);
		if (m == null || Joy(m.Value) == null)
		{
			return text;
		}
		return Glyph(m.Value) + " + " + text;
	}

	internal static string GlyphOf(string joy)
	{
		if (ZInput.instance == null)
		{
			return joy;
		}
		string boundKeyString = ZInput.instance.GetBoundKeyString(joy, emptyStringOnMissing: true);
		if (string.IsNullOrEmpty(boundKeyString))
		{
			return joy;
		}
		if (Localization.instance == null)
		{
			return boundKeyString;
		}
		return Localization.instance.Localize(boundKeyString);
	}
}
