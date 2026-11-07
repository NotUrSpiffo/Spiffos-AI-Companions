using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class MHR_Deck
{
	internal class Grid
	{
		public string Type;

		public Vector3 Min;

		public int W;

		public int H;

		public bool[] Walk;

		public float[] Y;

		public int Count;
	}

	private class Walker
	{
		public Ship Ship;

		public readonly List<Vector3> Path = new List<Vector3>();

		public int Idx;

		public Vector3 GoalLocal;

		public float NextPlan;

		public float ProgressAt;

		public float BestLeft = float.MaxValue;

		public bool Walking;

		public bool StuckLogged;
	}

	private const float Cell = 0.5f;

	private static readonly Dictionary<string, Grid> s_grids = new Dictionary<string, Grid>();

	private static readonly Dictionary<int, Walker> s_walkers = new Dictionary<int, Walker>();

	private static int s_vehicle = -1;

	private static int s_block = -1;

	private static int Vehicle()
	{
		if (s_vehicle == -1)
		{
			s_vehicle = LayerMask.GetMask("vehicle");
		}
		return s_vehicle;
	}

	private static int Block()
	{
		if (s_block == -1)
		{
			s_block = LayerMask.GetMask("vehicle", "Default", "static_solid", "piece");
		}
		return s_block;
	}

	private static string TypeOf(Ship ship)
	{
		return Utils.GetPrefabName(ship.gameObject);
	}

	private static Ship ShipOf(Collider col)
	{
		if (col == null)
		{
			return null;
		}
		if (!(col.attachedRigidbody != null))
		{
			return col.GetComponentInParent<Ship>();
		}
		return col.attachedRigidbody.GetComponent<Ship>();
	}

	internal static Grid For(Ship ship)
	{
		if (ship == null)
		{
			return null;
		}
		string text = TypeOf(ship);
		if (s_grids.TryGetValue(text, out var value))
		{
			return value;
		}
		value = Build(ship, text);
		s_grids[text] = value;
		return value;
	}

	private static Grid Build(Ship ship, string type)
	{
		Transform transform = ship.transform;
		Vector3 lhs = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 lhs2 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		Collider[] componentsInChildren = ship.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!(collider == null) && !collider.isTrigger && ((1 << collider.gameObject.layer) & Vehicle()) != 0)
			{
				Bounds bounds = collider.bounds;
				for (int j = 0; j < 8; j++)
				{
					Vector3 position = new Vector3(((j & 1) == 0) ? bounds.min.x : bounds.max.x, ((j & 2) == 0) ? bounds.min.y : bounds.max.y, ((j & 4) == 0) ? bounds.min.z : bounds.max.z);
					Vector3 rhs = transform.InverseTransformPoint(position);
					lhs = Vector3.Min(lhs, rhs);
					lhs2 = Vector3.Max(lhs2, rhs);
				}
			}
		}
		Grid grid = new Grid
		{
			Type = type
		};
		if (lhs.x > lhs2.x)
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log?.LogInfo("[Deck] no deck colliders on '" + type + "' - no grid");
			}
			return grid;
		}
		grid.Min = new Vector3(lhs.x, 0f, lhs.z);
		grid.W = Mathf.Max(1, Mathf.CeilToInt((lhs2.x - lhs.x) / 0.5f));
		grid.H = Mathf.Max(1, Mathf.CeilToInt((lhs2.z - lhs.z) / 0.5f));
		grid.Walk = new bool[grid.W * grid.H];
		grid.Y = new float[grid.W * grid.H];
		bool[] array = new bool[grid.W * grid.H];
		float y = lhs2.y + 2f;
		float maxDistance = lhs2.y - lhs.y + 4f;
		for (int k = 0; k < grid.W; k++)
		{
			for (int l = 0; l < grid.H; l++)
			{
				Vector3 position2 = new Vector3(grid.Min.x + ((float)k + 0.5f) * 0.5f, y, grid.Min.z + ((float)l + 0.5f) * 0.5f);
				if (Physics.Raycast(transform.TransformPoint(position2), -transform.up, out var hitInfo, maxDistance, Vehicle()) && !(ShipOf(hitInfo.collider) != ship) && !(Vector3.Dot(hitInfo.normal, transform.up) < 0.7f))
				{
					int num = k * grid.H + l;
					array[num] = true;
					grid.Y[num] = transform.InverseTransformPoint(hitInfo.point).y;
					Vector3 start = hitInfo.point + transform.up * 0.6f;
					Vector3 end = hitInfo.point + transform.up * 1.6f;
					if (Physics.CheckCapsule(start, end, 0.25f, Block(), QueryTriggerInteraction.Ignore))
					{
						array[num] = false;
					}
				}
			}
		}
		for (int m = 0; m < grid.W; m++)
		{
			for (int n = 0; n < grid.H; n++)
			{
				int num2 = m * grid.H + n;
				if (array[num2])
				{
					bool flag = m > 0 && m < grid.W - 1 && n > 0 && n < grid.H - 1 && array[(m - 1) * grid.H + n] && array[(m + 1) * grid.H + n] && array[m * grid.H + n - 1] && array[m * grid.H + n + 1];
					grid.Walk[num2] = flag;
					if (flag)
					{
						grid.Count++;
					}
				}
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo($"[Deck] grid for '{type}': {grid.W} x {grid.H} cells of {0.5f} m, {grid.Count} to walk on");
		}
		return grid;
	}

	private static bool Cell2(Grid g, Vector3 local, out int x, out int z)
	{
		x = Mathf.FloorToInt((local.x - g.Min.x) / 0.5f);
		z = Mathf.FloorToInt((local.z - g.Min.z) / 0.5f);
		if (x >= 0 && z >= 0 && x < g.W)
		{
			return z < g.H;
		}
		return false;
	}

	private static Vector3 Center(Grid g, int x, int z)
	{
		return new Vector3(g.Min.x + ((float)x + 0.5f) * 0.5f, g.Y[x * g.H + z], g.Min.z + ((float)z + 0.5f) * 0.5f);
	}

	internal static bool Nearest(Grid g, Vector3 local, out int bx, out int bz)
	{
		bx = (bz = -1);
		if (g == null || g.Walk == null || g.Count == 0)
		{
			return false;
		}
		float num = float.MaxValue;
		for (int i = 0; i < g.W; i++)
		{
			for (int j = 0; j < g.H; j++)
			{
				if (g.Walk[i * g.H + j])
				{
					Vector3 vector = Center(g, i, j);
					float num2 = (vector.x - local.x) * (vector.x - local.x) + (vector.z - local.z) * (vector.z - local.z);
					if (num2 < num)
					{
						num = num2;
						bx = i;
						bz = j;
					}
				}
			}
		}
		return bx >= 0;
	}

	internal static bool SpotNear(Ship ship, Vector3 world, out Vector3 local)
	{
		local = Vector3.zero;
		Grid grid = For(ship);
		if (grid == null || !Nearest(grid, ship.transform.InverseTransformPoint(world), out var bx, out var bz))
		{
			return false;
		}
		local = Center(grid, bx, bz);
		return true;
	}

	internal static bool SideSpot(Ship ship, Vector3 meWorld, Vector3 world, out Vector3 local)
	{
		local = Vector3.zero;
		Grid grid = For(ship);
		if (grid == null || grid.Walk == null || grid.Count == 0)
		{
			return false;
		}
		Transform transform = ship.transform;
		Vector3 vector = transform.InverseTransformPoint(meWorld);
		Vector3 vector2 = transform.InverseTransformPoint(world);
		float num = float.MaxValue;
		for (int i = 0; i < grid.W; i++)
		{
			for (int j = 0; j < grid.H; j++)
			{
				if (!grid.Walk[i * grid.H + j])
				{
					continue;
				}
				Vector3 vector3 = Center(grid, i, j);
				Vector2 rhs = new Vector2(vector2.x - vector3.x, vector2.z - vector3.z);
				if (rhs.sqrMagnitude < 0.01f)
				{
					continue;
				}
				rhs.Normalize();
				bool flag = false;
				for (int k = 0; k < 4; k++)
				{
					if (flag)
					{
						break;
					}
					int num2 = k switch
					{
						1 => -1, 
						0 => 1, 
						_ => 0, 
					};
					int num3 = k switch
					{
						3 => -1, 
						2 => 1, 
						_ => 0, 
					};
					int num4 = i + num2;
					int num5 = j + num3;
					flag = (num4 < 0 || num5 < 0 || num4 >= grid.W || num5 >= grid.H || !grid.Walk[num4 * grid.H + num5]) && Vector2.Dot(new Vector2(num2, num3), rhs) > 0.5f;
				}
				if (flag)
				{
					float num6 = (vector3.x - vector.x) * (vector3.x - vector.x) + (vector3.z - vector.z) * (vector3.z - vector.z);
					if (num6 < num)
					{
						num = num6;
						local = vector3;
					}
				}
			}
		}
		return num < float.MaxValue;
	}

	private static bool Plan(Grid g, Vector3 fromLocal, Vector3 toLocal, List<Vector3> path)
	{
		path.Clear();
		if (!Nearest(g, fromLocal, out var bx, out var bz) || !Nearest(g, toLocal, out var bx2, out var bz2))
		{
			return false;
		}
		int num = g.W * g.H;
		float[] array = new float[num];
		int[] array2 = new int[num];
		bool[] array3 = new bool[num];
		for (int i = 0; i < num; i++)
		{
			array[i] = float.MaxValue;
			array2[i] = -1;
		}
		int num2 = bx * g.H + bz;
		int num3 = bx2 * g.H + bz2;
		array[num2] = 0f;
		List<int> list = new List<int> { num2 };
		while (list.Count > 0)
		{
			int index = 0;
			float num4 = float.MaxValue;
			for (int j = 0; j < list.Count; j++)
			{
				int num5 = list[j];
				float num6 = Mathf.Sqrt((num5 / g.H - bx2) * (num5 / g.H - bx2) + (num5 % g.H - bz2) * (num5 % g.H - bz2));
				if (array[num5] + num6 < num4)
				{
					num4 = array[num5] + num6;
					index = j;
				}
			}
			int num7 = list[index];
			list.RemoveAt(index);
			if (num7 == num3)
			{
				break;
			}
			array3[num7] = true;
			int num8 = num7 / g.H;
			int num9 = num7 % g.H;
			for (int k = -1; k <= 1; k++)
			{
				for (int l = -1; l <= 1; l++)
				{
					if (k == 0 && l == 0)
					{
						continue;
					}
					int num10 = num8 + k;
					int num11 = num9 + l;
					if (num10 < 0 || num11 < 0 || num10 >= g.W || num11 >= g.H)
					{
						continue;
					}
					int num12 = num10 * g.H + num11;
					if (!g.Walk[num12] || array3[num12] || (k != 0 && l != 0 && (!g.Walk[num8 * g.H + num11] || !g.Walk[num10 * g.H + num9])))
					{
						continue;
					}
					float num13 = array[num7] + ((k != 0 && l != 0) ? 1.414f : 1f);
					if (num13 < array[num12])
					{
						array[num12] = num13;
						array2[num12] = num7;
						if (!list.Contains(num12))
						{
							list.Add(num12);
						}
					}
				}
			}
		}
		if (num3 != num2 && array2[num3] < 0)
		{
			return false;
		}
		for (int num14 = num3; num14 >= 0; num14 = ((num14 == num2) ? (-1) : array2[num14]))
		{
			path.Insert(0, Center(g, num14 / g.H, num14 % g.H));
		}
		return path.Count > 0;
	}

	private static Walker GetWalker(CompanionAI ai)
	{
		int instanceID = ai.GetInstanceID();
		if (!s_walkers.TryGetValue(instanceID, out var value))
		{
			value = new Walker();
			s_walkers[instanceID] = value;
		}
		return value;
	}

	internal static void Release(CompanionAI ai)
	{
		if (ai != null && s_walkers.TryGetValue(ai.GetInstanceID(), out var value))
		{
			if (value.Walking)
			{
				ai.GetComponent<Character>()?.SetWalk(walk: false);
			}
			s_walkers.Remove(ai.GetInstanceID());
		}
	}

	internal static bool Walk(CompanionAI ai, Ship ship, Vector3 goalLocal, float dist, out bool stuck, float walkLast = 1f, float stuckAfter = 5f)
	{
		stuck = false;
		Grid grid = For(ship);
		Transform transform = ship.transform;
		Character component = ai.GetComponent<Character>();
		Vector3 vector = transform.InverseTransformPoint(ai.transform.position);
		float magnitude = new Vector2(vector.x - goalLocal.x, vector.z - goalLocal.z).magnitude;
		if (magnitude <= dist)
		{
			ai.StopMoving();
			Release(ai);
			return true;
		}
		Walker walker = GetWalker(ai);
		float time = Time.time;
		if (walker.Ship != ship || (walker.GoalLocal - goalLocal).sqrMagnitude > 0.25f || time >= walker.NextPlan)
		{
			bool num = walker.Ship != ship || (walker.GoalLocal - goalLocal).sqrMagnitude > 0.25f;
			walker.Ship = ship;
			walker.GoalLocal = goalLocal;
			walker.NextPlan = time + 2f;
			if (num)
			{
				walker.ProgressAt = time;
				walker.BestLeft = float.MaxValue;
				walker.StuckLogged = false;
			}
			bool flag = grid != null && Plan(grid, vector, goalLocal, walker.Path);
			if (!flag)
			{
				walker.Path.Clear();
				walker.Path.Add(goalLocal);
			}
			walker.Idx = 0;
			if (num)
			{
				float num2 = 0f;
				Vector3 vector2 = vector;
				foreach (Vector3 item in walker.Path)
				{
					num2 += new Vector2(item.x - vector2.x, item.z - vector2.z).magnitude;
					vector2 = item;
				}
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log?.LogDebug(string.Format("[Deck] {0} walking {1:F1} m over {2} cells from {3:F1} to {4:F1} on the ship{5}", component?.m_name, num2, walker.Path.Count, vector, goalLocal, flag ? "" : " (no route on the grid - straight)"));
				}
			}
		}
		if (magnitude < walker.BestLeft - 0.2f)
		{
			walker.BestLeft = magnitude;
			walker.ProgressAt = time;
		}
		if (time - walker.ProgressAt > stuckAfter)
		{
			stuck = true;
			if (!walker.StuckLogged)
			{
				walker.StuckLogged = true;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log?.LogInfo($"[Deck] {component?.m_name} stuck at {vector:F1} on the ship, {magnitude:F1} m from {goalLocal:F1}");
				}
			}
		}
		while (walker.Idx < walker.Path.Count - 1 && new Vector2(vector.x - walker.Path[walker.Idx].x, vector.z - walker.Path[walker.Idx].z).magnitude < 0.35f)
		{
			walker.Idx++;
		}
		Vector3 vector3 = transform.TransformPoint(walker.Path[Mathf.Min(walker.Idx, walker.Path.Count - 1)]) - ai.transform.position;
		vector3 -= transform.up * Vector3.Dot(vector3, transform.up);
		if (vector3.sqrMagnitude < 0.0001f)
		{
			return false;
		}
		vector3.Normalize();
		bool flag2 = magnitude < walkLast;
		if (component != null && flag2 != walker.Walking)
		{
			component.SetWalk(flag2);
			walker.Walking = flag2;
		}
		ai.MoveTowards(vector3, run: false);
		return false;
	}
}
