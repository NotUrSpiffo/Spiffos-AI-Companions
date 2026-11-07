using UnityEngine;

namespace Companions;

public static class MHR_Nav
{
	public const float LegTimeout = 120f;

	public static bool MoveFarm(CompanionAI ai, float dt, Vector3 point, float dist, bool run)
	{
		return Move(ai, dt, point, dist, run, "Farm");
	}

	public static bool MovePlant(CompanionAI ai, float dt, Vector3 point, float dist, bool run)
	{
		return Move(ai, dt, point, dist, run, "Plant");
	}

	public static bool MoveSmelt(CompanionAI ai, float dt, Vector3 point, float dist, bool run)
	{
		return Move(ai, dt, point, dist, run, "Smelt");
	}

	public static float DoorDist(Vector3 a, Vector3 b)
	{
		if (!(Mathf.Abs(a.y - b.y) <= 3f))
		{
			return Vector3.Distance(a, b);
		}
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	public static float RepairThreshold(Component c)
	{
		float num = ((ModConfig.RepairDurabilityThreshold != null) ? ModConfig.RepairDurabilityThreshold.Value : 0.5f);
		if (num > 1f)
		{
			num /= 100f;
		}
		CompanionSetup companionSetup = ((c != null) ? c.GetComponent<CompanionSetup>() : null);
		bool flag = false;
		if (companionSetup != null && !companionSetup.HasHomePosition())
		{
			try
			{
				flag = EffectArea.IsPointInsideArea(companionSetup.transform.position, EffectArea.Type.PlayerBase) != null;
			}
			catch
			{
			}
		}
		if (companionSetup != null && ((companionSetup.HasHomePosition() && MHR_ToolKeeper.AtHome(companionSetup)) | flag))
		{
			if (ModConfig.RepairHomeThreshold == null)
			{
				return 0.8f;
			}
			return Mathf.Max(num, ModConfig.RepairHomeThreshold.Value);
		}
		return num;
	}

	public static void TurnTo(Transform t, Quaternion q)
	{
		if (!(t == null))
		{
			t.rotation = Quaternion.RotateTowards(t.rotation, q, 540f * Time.deltaTime);
		}
	}

	public static float StationScanDist(Vector3 me, Vector3 station, Component c)
	{
		float num = Vector3.Distance(me, station);
		try
		{
			CompanionSetup companionSetup = ((c != null) ? c.GetComponent<CompanionSetup>() : null);
			if (companionSetup != null && companionSetup.HasHomePosition() && MHR_ToolKeeper.NearHome(companionSetup) && Vector3.Distance(companionSetup.GetHomePosition(), station) <= MHR_ToolKeeper.HomeRange())
			{
				return num * 0.01f;
			}
		}
		catch
		{
		}
		return num;
	}

	public static float ReachDist(Vector3 a, Vector3 b)
	{
		if (!(Mathf.Abs(a.y - b.y) <= 2.2f))
		{
			return Vector3.Distance(a, b);
		}
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	public static bool FarEnoughToRun(CompanionAI ai, Vector3 point)
	{
		float num = ((ModConfig.WorkRunDistance != null) ? ModConfig.WorkRunDistance.Value : 25f);
		if (num > 0f)
		{
			return Vector3.Distance(ai.transform.position, point) > num;
		}
		return false;
	}

	public static bool Move(CompanionAI ai, float dt, Vector3 point, float dist, bool run, string tag)
	{
		Vector3 vector = point - ai.transform.position;
		float magnitude = new Vector2(vector.x, vector.z).magnitude;
		float dist2 = dist;
		if (tag == "Plant" && Mathf.Abs(vector.y) > 1f && magnitude <= dist)
		{
			dist2 = Mathf.Min(dist, 0.5f);
		}
		return MHR_Mover.Go(ai, dt, point, dist2, run, tag);
	}
}
