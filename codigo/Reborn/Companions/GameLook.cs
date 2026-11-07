using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Companions;

internal static class GameLook
{
	private static Sprite s_panel;

	private static Material s_panelMat;

	private static float s_panelPpu = 1f;

	private static bool s_looked;

	private static bool s_scaleLogged;

	private static readonly FieldInfo s_popupInstance = AccessTools.Field(typeof(UnifiedPopup), "instance");

	private static readonly FieldInfo s_popupCenterText = AccessTools.Field(typeof(UnifiedPopup), "buttonCenterText");

	internal static Sprite PanelSprite()
	{
		if (s_looked && s_panel != null)
		{
			return s_panel;
		}
		InventoryGui instance = InventoryGui.instance;
		if (instance == null)
		{
			return null;
		}
		s_looked = true;
		Image image = null;
		float num = 0f;
		string arg = null;
		RectTransform[] array = new RectTransform[3] { instance.m_player, instance.m_container, instance.m_crafting };
		foreach (RectTransform rectTransform in array)
		{
			if (rectTransform == null)
			{
				continue;
			}
			Image component = rectTransform.GetComponent<Image>();
			if (component != null && component.sprite != null)
			{
				float num2 = rectTransform.rect.width * rectTransform.rect.height;
				if (num2 > num)
				{
					image = component;
					num = num2;
					arg = rectTransform.name;
				}
			}
			Image[] componentsInChildren = rectTransform.GetComponentsInChildren<Image>(includeInactive: true);
			foreach (Image image2 in componentsInChildren)
			{
				if (!(image2 == null) && !(image2.sprite == null) && image2.gameObject.name.IndexOf("bkg", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					RectTransform rectTransform2 = image2.rectTransform;
					float num3 = rectTransform2.rect.width * rectTransform2.rect.height;
					if (num3 > num)
					{
						image = image2;
						num = num3;
						arg = rectTransform.name + "/" + image2.gameObject.name;
					}
				}
			}
		}
		if (image != null)
		{
			s_panel = image.sprite;
			s_panelMat = image.material;
			s_panelPpu = image.pixelsPerUnitMultiplier;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[UI] Game look: panel background '{s_panel.name}' from the inventory window ({arg}), border scale {s_panelPpu:F2}");
			}
		}
		else if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo("[UI] Game look: no panel background found in the inventory window - plain colour");
		}
		return s_panel;
	}

	internal static void ApplyPanel(Image img, Color fallback)
	{
		if (img == null)
		{
			return;
		}
		Sprite sprite = PanelSprite();
		if (sprite != null)
		{
			img.sprite = sprite;
			img.type = ((sprite.border.sqrMagnitude > 0f) ? Image.Type.Sliced : Image.Type.Simple);
			img.pixelsPerUnitMultiplier = s_panelPpu;
			if (s_panelMat != null && s_panelMat != img.defaultMaterial)
			{
				img.material = s_panelMat;
			}
			img.color = Color.white;
		}
		else
		{
			img.sprite = null;
			img.color = fallback;
		}
	}

	internal static void MatchGameScale(CanvasScaler ours)
	{
		if (ours == null)
		{
			return;
		}
		UnifiedPopup unifiedPopup = s_popupInstance?.GetValue(null) as UnifiedPopup;
		Canvas canvas = ((!(unifiedPopup != null)) ? null : unifiedPopup.GetComponentInParent<Canvas>()?.rootCanvas);
		CanvasScaler canvasScaler = ((canvas != null) ? canvas.GetComponent<CanvasScaler>() : null);
		if (canvasScaler == null)
		{
			if (!s_scaleLogged)
			{
				s_scaleLogged = true;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[UI] Game look: no game canvas scaler found - keeping 1920x1080 scaling");
				}
			}
			return;
		}
		ours.uiScaleMode = canvasScaler.uiScaleMode;
		ours.referenceResolution = canvasScaler.referenceResolution;
		ours.screenMatchMode = canvasScaler.screenMatchMode;
		ours.matchWidthOrHeight = canvasScaler.matchWidthOrHeight;
		ours.scaleFactor = canvasScaler.scaleFactor;
		ours.referencePixelsPerUnit = canvasScaler.referencePixelsPerUnit;
		if (!s_scaleLogged)
		{
			s_scaleLogged = true;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[UI] Game look: canvas '{canvas.name}' {canvasScaler.uiScaleMode}, reference {canvasScaler.referenceResolution}, match {canvasScaler.matchWidthOrHeight:F2}, scale factor {canvasScaler.scaleFactor:F2} (canvas scale {canvas.scaleFactor:F2})");
			}
		}
	}

	internal static void ApplyButtonText(TMP_Text t)
	{
		UnifiedPopup unifiedPopup = s_popupInstance?.GetValue(null) as UnifiedPopup;
		TMP_Text tMP_Text = ((unifiedPopup != null) ? (s_popupCenterText?.GetValue(unifiedPopup) as TMP_Text) : null);
		if (!(t == null) && !(tMP_Text == null))
		{
			if (tMP_Text.font != null)
			{
				t.font = tMP_Text.font;
				t.fontSharedMaterial = tMP_Text.fontSharedMaterial;
			}
			float num = (tMP_Text.enableAutoSizing ? tMP_Text.fontSizeMax : tMP_Text.fontSize);
			t.enableAutoSizing = true;
			t.fontSizeMax = num;
			t.fontSizeMin = Mathf.Min(10f, num);
			t.fontSize = num;
			t.fontStyle = tMP_Text.fontStyle;
		}
	}

	internal static GameObject ButtonTemplate()
	{
		if (!(InventoryGui.instance != null) || !(InventoryGui.instance.m_craftButton != null))
		{
			return null;
		}
		return InventoryGui.instance.m_craftButton.gameObject;
	}
}
