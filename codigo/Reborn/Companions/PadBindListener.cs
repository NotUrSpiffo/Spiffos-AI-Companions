using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Companions;

internal class PadBindListener : MonoBehaviour
{
	private ConfigEntry<string> _entry;

	private TMP_Text _label;

	private bool _listening;

	private bool _released;

	private float _startedAt;

	private int _startFrame = -1;

	private RectTransform _rect;

	internal void Init(ConfigEntry<string> entry, TMP_Text label)
	{
		_entry = entry;
		_label = label;
		_rect = GetComponent<RectTransform>();
	}

	internal static string Label(string v)
	{
		switch (v)
		{
		default:
			return MHR_Pad.Glyph(v);
		case "Alt":
			return "Alt (" + MHR_Pad.Glyph("Alt") + ")";
		case null:
		case "None":
			return "None";
		}
	}

	internal void Begin()
	{
		if (!_listening)
		{
			_listening = true;
			_released = false;
			_startedAt = Time.unscaledTime;
			_startFrame = Time.frameCount;
			KeyBindListener.Capturing = true;
			_label.text = "Press a button...";
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Config] Waiting for a controller button for " + _entry.Definition.Key + " (Esc cancels)");
			}
		}
	}

	private void End(string value)
	{
		_listening = false;
		KeyBindListener.Capturing = false;
		KeyBindListener.CaptureEndFrame = Time.frameCount;
		if (value != null)
		{
			_entry.Value = value;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Config] " + _entry.Definition.Key + " = " + value);
			}
		}
		_label.text = Label(_entry.Value);
	}

	private void Update()
	{
		if (!_listening)
		{
			if (Input.GetMouseButtonDown(0) && _rect != null && RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, null))
			{
				Begin();
			}
		}
		else
		{
			if (Time.frameCount == _startFrame)
			{
				return;
			}
			if (Input.GetKeyDown(KeyCode.Escape) || Time.unscaledTime - _startedAt > 6f)
			{
				End(null);
				return;
			}
			bool flag = false;
			string[] physical = MHR_Pad.Physical;
			foreach (string text in physical)
			{
				if (!ZInput.GetButton(MHR_Pad.Joy(text)))
				{
					continue;
				}
				flag = true;
				if (_released && ZInput.GetButtonDown(MHR_Pad.Joy(text)))
				{
					string value = text;
					if (_entry.Definition.Key.EndsWith("Modifier") && MHR_Pad.PathOf(MHR_Pad.Joy(text)) == MHR_Pad.PathOf("JoyAltKeys"))
					{
						value = "Alt";
					}
					End(value);
					return;
				}
			}
			if (!flag)
			{
				_released = true;
			}
		}
	}
}
