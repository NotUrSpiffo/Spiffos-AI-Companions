using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace Companions;

internal class KeyBindListener : MonoBehaviour
{
	private ConfigEntry<KeyCode> _entry;

	private TMP_Text _label;

	private Func<bool> _isListening;

	private Action<bool> _setListening;

	private RectTransform _rect;

	private int _startFrame = -1;

	internal static bool Capturing;

	internal static int CaptureEndFrame = -1;

	internal void Init(ConfigEntry<KeyCode> entry, TMP_Text label, Func<bool> isListening, Action<bool> setListening)
	{
		_entry = entry;
		_label = label;
		_isListening = isListening;
		_setListening = setListening;
		_rect = GetComponent<RectTransform>();
	}

	internal void Begin()
	{
		if (_isListening != null && !_isListening())
		{
			_setListening(obj: true);
			Capturing = true;
			_startFrame = Time.frameCount;
			_label.text = "Press a key...";
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Config] Waiting for a key for " + _entry.Definition.Key + " (Esc cancels)");
			}
		}
	}

	private void Update()
	{
		if (_isListening == null)
		{
			return;
		}
		if (!_isListening())
		{
			if (Input.GetMouseButtonDown(0) && _rect != null && RectTransformUtility.RectangleContainsScreenPoint(_rect, Input.mousePosition, null))
			{
				Begin();
			}
		}
		else
		{
			if (Time.frameCount == _startFrame || !Input.anyKeyDown)
			{
				return;
			}
			foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
			{
				if (value != KeyCode.None && !IsMouse(value) && Input.GetKeyDown(value))
				{
					Take(value);
					break;
				}
			}
		}
	}

	private void OnGUI()
	{
		if (_isListening != null && _isListening() && Time.frameCount != _startFrame)
		{
			Event current = Event.current;
			if (current != null && current.type == EventType.KeyDown && current.keyCode != KeyCode.None)
			{
				Take(current.keyCode);
				current.Use();
			}
		}
	}

	private void Take(KeyCode key)
	{
		Capturing = false;
		CaptureEndFrame = Time.frameCount;
		if (key == KeyCode.Escape)
		{
			_label.text = _entry.Value.ToString();
			_label.color = new Color(0.83f, 0.52f, 0.18f, 1f);
			_setListening(obj: false);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Config] Key binding for " + _entry.Definition.Key + " cancelled");
			}
			return;
		}
		_entry.Value = key;
		_label.text = key.ToString();
		_label.color = new Color(0.83f, 0.52f, 0.18f, 1f);
		_setListening(obj: false);
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Config] " + _entry.Definition.Key + " = " + key);
		}
	}

	private static bool IsMouse(KeyCode k)
	{
		if (k >= KeyCode.Mouse0)
		{
			return k <= KeyCode.Mouse6;
		}
		return false;
	}
}
