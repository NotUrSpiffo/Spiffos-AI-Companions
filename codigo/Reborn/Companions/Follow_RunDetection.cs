using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(BaseAI), "Follow")]
internal static class Follow_RunDetection
{
	private static void Postfix(BaseAI __instance, GameObject go)
	{
		if (!(__instance is CompanionAI))
		{
			return;
		}
		CompanionStamina component = __instance.GetComponent<CompanionStamina>();
		if (!(component == null))
		{
			if (go == null)
			{
				component.IsRunning = false;
				return;
			}
			float num = Vector3.Distance(go.transform.position, __instance.transform.position);
			component.IsRunning = num > 10f;
		}
	}
}
