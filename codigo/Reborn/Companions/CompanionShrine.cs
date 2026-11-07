using UnityEngine;

namespace Companions;

public class CompanionShrine : MonoBehaviour, Hoverable, Interactable
{
	internal float m_hoverOffset = 1f;

	public string GetHoverText()
	{
		return Localization.instance.Localize("Odin's Shrine\n[<color=yellow><b>$KEY_Use</b></color>] " + ModLocalization.Loc("hc_shrine_use"));
	}

	public string GetHoverName()
	{
		return "Odin's Shrine";
	}

	public float GetHoverOffset()
	{
		return m_hoverOffset;
	}

	public bool Interact(Humanoid user, bool hold, bool alt)
	{
		if (hold || user == null || user != Player.m_localPlayer || ShrinePanel.IsOpen || ShrinePanel.JustClosed || StarterCompanionPanel.Instance != null)
		{
			return false;
		}
		ShrinePanel.Show(this);
		return true;
	}

	public bool UseItem(Humanoid user, ItemDrop.ItemData item)
	{
		return false;
	}
}
