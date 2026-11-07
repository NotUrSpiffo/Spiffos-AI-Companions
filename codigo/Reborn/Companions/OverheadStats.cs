using System;
using System.Reflection;
using UnityEngine;

namespace Companions;

internal static class OverheadStats
{
	private static FieldInfo _hudsField;

	private static FieldInfo _healthFastField;

	private static FieldInfo _healthFastFriendlyField;

	private static Type _hudDataType;

	private static int _logs;

	internal static void HideForever(GuiBar bar)
	{
		try
		{
			GameObject gameObject = VisualRoot(bar);
			if (gameObject != null)
			{
				gameObject.SetActive(value: false);
			}
		}
		catch (Exception)
		{
		}
	}

	internal static void Update(Character companion, GuiBar staminaBar, Component staminaComp)
	{
		try
		{
			if (!(companion == null))
			{
				object obj = FindHudData(companion);
				if (obj != null)
				{
					GuiBar barField = GetBarField(obj, "m_healthFast", ref _healthFastField);
					GuiBar barField2 = GetBarField(obj, "m_healthFastFriendly", ref _healthFastFriendlyField);
					ApplyFade((!(barField2 != null) || !barField2.gameObject.activeSelf) ? barField : barField2, companion.GetHealth(), companion.GetMaxHealth());
				}
				if (staminaBar != null && staminaComp != null)
				{
					PropertyInfo property = staminaComp.GetType().GetProperty("Stamina", BindingFlags.Instance | BindingFlags.Public);
					PropertyInfo property2 = staminaComp.GetType().GetProperty("MaxStamina", BindingFlags.Instance | BindingFlags.Public);
					float current = ((!(property == null)) ? ((float)property.GetValue(staminaComp, null)) : 0f);
					float max = ((!(property2 == null)) ? ((float)property2.GetValue(staminaComp, null)) : 1f);
					ApplyFade(staminaBar, current, max);
				}
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("[OverheadStats] update failed: " + ex.Message);
			}
		}
	}

	private static void Log(string s)
	{
		if (_logs < 20)
		{
			_logs++;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log?.LogInfo(s);
			}
		}
	}

	private static bool ShowBars()
	{
		try
		{
			return ModConfig.ShowOverheadBars == null || ModConfig.ShowOverheadBars.Value;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static object FindHudData(Character companion)
	{
		EnemyHud instance = EnemyHud.instance;
		if (instance == null)
		{
			return null;
		}
		if (_hudsField == null)
		{
			_hudsField = typeof(EnemyHud).GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic);
		}
		if (_hudsField == null)
		{
			return null;
		}
		object value = _hudsField.GetValue(instance);
		if (value == null)
		{
			return null;
		}
		if (_hudDataType == null)
		{
			_hudDataType = value.GetType().GetGenericArguments()[1];
		}
		MethodInfo method = value.GetType().GetMethod("TryGetValue");
		object[] array = new object[2] { companion, null };
		if ((bool)method.Invoke(value, array))
		{
			return array[1];
		}
		return null;
	}

	private static GuiBar GetBarField(object hudData, string name, ref FieldInfo cache)
	{
		if (cache == null)
		{
			cache = _hudDataType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}
		if (cache == null)
		{
			return null;
		}
		return (GuiBar)cache.GetValue(hudData);
	}

	internal static GameObject VisualRoot(GuiBar bar)
	{
		if (bar == null)
		{
			return null;
		}
		Transform parent = bar.transform.parent;
		if (parent != null)
		{
			return parent.gameObject;
		}
		return bar.gameObject;
	}

	private static void ApplyFade(GuiBar bar, float current, float max)
	{
		bool flag = ShowBars();
		bool flag2 = max > 0f && current >= max - 0.05f;
		bool flag3 = flag && !flag2;
		GameObject gameObject = VisualRoot(bar);
		if (gameObject != null && gameObject.activeSelf != flag3)
		{
			gameObject.SetActive(flag3);
		}
	}
}
