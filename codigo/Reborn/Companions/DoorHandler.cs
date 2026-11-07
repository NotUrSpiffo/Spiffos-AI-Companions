using UnityEngine;

namespace Companions;

public class DoorHandler : MonoBehaviour
{
	private CompanionAI _ai;

	private Humanoid _humanoid;

	private ZNetView _nview;

	private Door _door;

	private float _start;

	private bool _close;

	public bool IsActive => _door != null;

	public void Cancel()
	{
		if (_door != null)
		{
			_door = null;
			if (_ai != null)
			{
				MHR_Mover.Release(_ai);
			}
		}
	}

	public void DirectUseDoor(Door door, bool close)
	{
		if (door == null)
		{
			return;
		}
		_close = close;
		if (!IsUsableDoor(door) || IsOpen(door) != close)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Door] \"" + door.m_name + "\" is locked or already " + (close ? "shut" : "open") + " - nothing to do");
			}
			return;
		}
		_door = door;
		_start = Time.time;
		_ai?.SetFollowTarget(null);
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Door] Going to " + (close ? "close" : "open") + " the " + door.m_name + " at " + MHR_Mover.Fmt(door.transform.position) + " (" + Vector3.Distance(transform.position, door.transform.position).ToString("F1") + " m away)");
		}
	}

	private void Awake()
	{
		_ai = GetComponent<CompanionAI>();
		_humanoid = GetComponent<Humanoid>();
		_nview = GetComponent<ZNetView>();
	}

	private void Update()
	{
		if (_door == null || _ai == null || _humanoid == null || _nview == null || !_nview.IsValid() || !_nview.IsOwner())
		{
			return;
		}
		if (!IsUsableDoor(_door) || IsOpen(_door) != _close)
		{
			Done(_close ? "the door is shut now" : "the door is open now");
			return;
		}
		if (Time.time - _start > 60f)
		{
			Done("couldn't get to it");
			return;
		}
		if (_ai.IsInCombat || MHR_Loot.Active(this) || MHR_ToolKeeper.IsBusy(this) || CompanionInteractPanel.IsOpenFor(GetComponent<CompanionSetup>()) || CompanionRadialMenu.IsOpenFor(GetComponent<CompanionSetup>()))
		{
			_start += Time.deltaTime;
			return;
		}
		Vector3 position = transform.position;
		Vector3 position2 = _door.transform.position;
		if (MHR_Nav.DoorDist(position, position2) > 2f)
		{
			MHR_Mover.Go(_ai, Time.deltaTime, position2, 1.6f, allowRun: true, "Door");
			if (MHR_Mover.GaveUp(_ai))
			{
				Done("no way to get to it");
			}
			return;
		}
		_ai.StopMoving();
		MHR_Mover.Release(_ai);
		Vector3 vector = position2 - position;
		vector.y = 0f;
		if (vector.sqrMagnitude > 0.01f)
		{
			transform.rotation = Quaternion.LookRotation(vector.normalized);
		}
		bool flag = false;
		try
		{
			flag = _door.Interact(_humanoid, hold: false, alt: false);
		}
		catch
		{
		}
		DoorHandler doorHandler = this;
		string why;
		if (flag)
		{
			why = (_close ? "closed it" : "opened it");
		}
		else
		{
			why = (_close ? "couldn't close it" : "couldn't open it");
		}
		doorHandler.Done(why);
	}

	private void Done(string why)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Door] " + ((_door != null) ? _door.m_name : "Door") + ": " + why);
		}
		_door = null;
		_ai?.StopMoving();
		if (_ai != null)
		{
			MHR_Mover.Release(_ai);
		}
		GetComponent<CompanionSetup>()?.ApplyFollowMode((_nview?.GetZDO())?.GetInt(CompanionSetup.ActionModeHash) ?? 0, force: true);
	}

	internal static bool IsOpen(Door door)
	{
		ZNetView zNetView = ((door != null) ? door.GetComponent<ZNetView>() : null);
		if (zNetView != null && zNetView.GetZDO() != null)
		{
			return zNetView.GetZDO().GetInt(ZDOVars.s_state) != 0;
		}
		return false;
	}

	internal static bool IsUsableDoor(Door door)
	{
		if (door == null || !door || door.m_keyItem != null)
		{
			return false;
		}
		ZNetView component = door.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		if (door.m_checkGuardStone)
		{
			return PrivateArea.CheckAccess(door.transform.position, 0f, flash: false);
		}
		return true;
	}

	internal static bool IsValidClosedDoor(Door door)
	{
		if (door == null || !door)
		{
			return false;
		}
		ZNetView component = door.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		if (component.GetZDO().GetInt(ZDOVars.s_state) != 0)
		{
			return false;
		}
		if (door.m_keyItem != null)
		{
			return false;
		}
		if (door.m_checkGuardStone && !PrivateArea.CheckAccess(door.transform.position, 0f, flash: false))
		{
			return false;
		}
		return true;
	}
}
