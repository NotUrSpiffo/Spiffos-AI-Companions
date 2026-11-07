using UnityEngine;

namespace Companions;

public static class MHR_JobRules
{
	private static readonly int FollowPausedHash = "HC_FollowPausedByJob".GetStableHashCode();

	public static bool HasJob(CompanionSetup cs)
	{
		if (cs == null)
		{
			return false;
		}
		ZNetView component = cs.GetComponent<ZNetView>();
		ZDO zDO = ((component != null) ? component.GetZDO() : null);
		if (zDO != null)
		{
			return zDO.GetInt(CompanionSetup.ActionModeHash) != 0;
		}
		return false;
	}

	public static bool OrderPending(Component c)
	{
		if (OtherOrder(c))
		{
			return true;
		}
		RepairController repairController = ((c != null) ? c.GetComponent<RepairController>() : null);
		if (repairController != null)
		{
			return repairController.IsDirect;
		}
		return false;
	}

	public static bool OtherOrder(Component c)
	{
		if (c == null)
		{
			return false;
		}
		CompanionAI component = c.GetComponent<CompanionAI>();
		if (component != null && (component.PendingMoveTarget.HasValue || component.PendingCartAttach != null || component.PendingDepositContainer != null || component.IsRecoveringTombstone || component.IsBoardingShip))
		{
			return true;
		}
		DoorHandler component2 = c.GetComponent<DoorHandler>();
		if (component2 != null && component2.IsActive)
		{
			return true;
		}
		CompanionRest component3 = c.GetComponent<CompanionRest>();
		if (component3 != null)
		{
			return component3.IsDirectedOrder;
		}
		return false;
	}

	public static bool IsJobMode(int mode)
	{
		if (mode != 0)
		{
			return mode != 4;
		}
		return false;
	}

	public static bool KeepsFollow(int mode)
	{
		if (mode != 7 && mode != 1 && mode != 2 && mode != 3)
		{
			return mode == 5;
		}
		return true;
	}

	public static void FollowSetByPlayer(CompanionSetup cs)
	{
		ZNetView zNetView = ((cs != null) ? cs.GetComponent<ZNetView>() : null);
		ZDO zDO = ((zNetView != null) ? zNetView.GetZDO() : null);
		if (zDO != null && zDO.GetBool(FollowPausedHash))
		{
			zDO.Set(FollowPausedHash, value: false);
		}
	}

	public static void OnJobSet(CompanionSetup cs, int mode)
	{
		if (cs == null)
		{
			return;
		}
		ZNetView component = cs.GetComponent<ZNetView>();
		ZDO zDO = ((component != null) ? component.GetZDO() : null);
		if (mode != 0)
		{
			if (!KeepsFollow(mode) && cs.GetFollow())
			{
				cs.SetFollow(v: false);
				zDO?.Set(FollowPausedHash, value: true);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Jobs] Job " + mode + " picked - Follow turned off");
				}
			}
		}
		else if (zDO != null && zDO.GetBool(FollowPausedHash))
		{
			zDO.Set(FollowPausedHash, value: false);
			cs.SetFollow(v: true);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Jobs] Job turned off - Follow back on");
			}
		}
	}
}
