using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(AutoJumpLedge), "OnTriggerStay")]
internal static class CompanionLadderBumpPatch
{
	private static bool Prefix(AutoJumpLedge __instance, Collider collider)
	{
		try
		{
			if (collider == null || __instance == null)
			{
				return true;
			}
			Character component = collider.GetComponent<Character>();
			if (component == null || component.IsPlayer() || component.GetComponent<CompanionSetup>() == null)
			{
				return true;
			}
			Vector3 moveDir = component.GetMoveDir();
			moveDir.y = 0f;
			Vector3 forward = __instance.transform.forward;
			forward.y = 0f;
			if (moveDir.sqrMagnitude < 0.0025f || forward.sqrMagnitude < 0.01f)
			{
				return false;
			}
			moveDir.Normalize();
			if (Vector3.Dot(moveDir, forward.normalized) < 0.5f)
			{
				return false;
			}
			component.SetMoveDir(moveDir);
			return true;
		}
		catch
		{
			return true;
		}
	}
}
