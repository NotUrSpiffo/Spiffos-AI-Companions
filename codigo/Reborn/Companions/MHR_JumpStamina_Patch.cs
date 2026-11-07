using System;
using HarmonyLib;

namespace Companions;

[HarmonyPatch(typeof(Character), "OnJump")]
public static class MHR_JumpStamina_Patch
{
	private static void Postfix(Character __instance)
	{
		try
		{
			if (__instance == null || __instance is Player)
			{
				return;
			}
			CompanionStamina component = __instance.GetComponent<CompanionStamina>();
			if (!(component == null))
			{
				ZNetView component2 = __instance.GetComponent<ZNetView>();
				if (!(component2 == null) && component2.IsOwner())
				{
					component.UseStamina(MHR_Jump.Cost(__instance));
				}
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Jump] " + ex.Message);
			}
		}
	}
}
