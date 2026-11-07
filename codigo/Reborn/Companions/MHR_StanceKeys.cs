using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

internal static class MHR_StanceKeys
{
	private static readonly List<string> s_names = new List<string>();

	internal static void Tick(Player player)
	{
		if (player == null)
		{
			return;
		}
		int num = -1;
		if (Pressed(ModConfig.PassiveKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadPassive, ModConfig.PadPassiveModifier)))
		{
			num = 1;
		}
		else if (Pressed(ModConfig.BalancedKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadBalanced, ModConfig.PadBalancedModifier)))
		{
			num = 0;
		}
		else if (Pressed(ModConfig.RangedKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadRanged, ModConfig.PadRangedModifier)))
		{
			num = 2;
		}
		else if (Pressed(ModConfig.MeleeKey) || (ZInput.IsGamepadActive() && MHR_Pad.Down(ModConfig.PadMelee, ModConfig.PadMeleeModifier)))
		{
			num = 3;
		}
		if (num < 0)
		{
			return;
		}
		s_names.Clear();
		CompanionSetup companionSetup = player.GetHoverObject()?.GetComponentInParent<CompanionSetup>();
		if (companionSetup != null && MHR_Owner.IsMine(companionSetup))
		{
			Set(companionSetup, num);
		}
		else
		{
			float num2 = ModConfig.RecallRange?.Value ?? 40f;
			foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
			{
				if (!(allCompanion == null) && MHR_Owner.IsMine(allCompanion) && allCompanion.GetIsCommandable())
				{
					Character component = allCompanion.GetComponent<Character>();
					if (!(component == null) && !component.IsDead() && (!(num2 > 0f) || !(Vector3.Distance(allCompanion.transform.position, player.transform.position) > num2)))
					{
						Set(allCompanion, num);
					}
				}
			}
		}
		string text = ModLocalization.Loc(num switch
		{
			3 => "hc_radial_melee", 
			2 => "hc_radial_ranged", 
			1 => "hc_radial_passive", 
			_ => "hc_radial_balanced", 
		});
		string text2 = ((s_names.Count > 0) ? ModLocalization.LocFmt("hc_msg_stance_set", string.Join(", ", s_names.ToArray()), text) : ModLocalization.LocFmt("hc_msg_stance_none", text));
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, text2);
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Stance] " + text2);
		}
	}

	private static bool Pressed(ConfigEntry<KeyCode> key)
	{
		if (key != null && key.Value != KeyCode.None)
		{
			return Input.GetKeyDown(key.Value);
		}
		return false;
	}

	private static void Set(CompanionSetup cs, int stance)
	{
		cs.SetCombatStance(stance);
		ZDO zDO = cs.GetComponent<ZNetView>()?.GetZDO();
		s_names.Add((zDO != null) ? zDO.GetString(CompanionSetup.NameHash, "Companion") : "Companion");
	}
}
