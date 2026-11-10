using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Companions;

public static class MHR_Chests
{
	private static readonly List<Piece> s_pieces = new List<Piece>();

	private static readonly Collider[] s_small = new Collider[32];

	private static readonly List<Vector3> s_path = new List<Vector3>();

	private static int s_mask = -1;

	// Cofres que abrió un compañero: siguen contando como suyos unos segundos, para que otro compañero espere su turno
	// en vez de elegir otro cofre.
	private static readonly Dictionary<int, float> s_held = new Dictionary<int, float>();

	private static readonly FieldInfo s_guiContainer = HarmonyLib.AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

	public const float HoldWait = 8f;

	public const float EyeHeight = 1.6f;

	public const float EyeReach = 4f;

	private static readonly Dictionary<int, KeyValuePair<float, bool>> s_reach = new Dictionary<int, KeyValuePair<float, bool>>();

	private static readonly float[] s_pieceRadii = new float[3] { 1.3f, 1.8f, 2.2f };

	private static readonly float[] s_pointRadii = new float[3] { 0.9f, 1.4f, 1.9f };

	public static bool Usable(Container c, Component who)
	{
		if (c == null || c.GetInventory() == null)
		{
			return false;
		}
		if (c.GetComponent<Character>() != null || c.GetComponentInParent<Character>() != null)
		{
			return false;
		}
		if (c.GetComponent<TombStone>() != null)
		{
			return false;
		}
		if (c.GetComponent<Vagon>() != null || c.GetComponentInParent<Ship>() != null)
		{
			return false;
		}
		if (c.GetComponent<Piece>() == null)
		{
			return false;
		}
		if (!c.GetComponent<Piece>().IsPlacedByPlayer())
		{
			return false;
		}
		ZNetView component = c.GetComponent<ZNetView>();
		if (component == null || !component.IsValid())
		{
			return false;
		}
		if (Busy(c))
		{
			return false;
		}
		if (!PrivateArea.CheckAccess(c.transform.position, 0f, flash: false))
		{
			return false;
		}
		if (c.m_privacy == Container.PrivacySetting.Private)
		{
			Piece component2 = c.GetComponent<Piece>();
			if (component2 == null || component2.GetCreator() != MHR_Owner.CreatorId(who))
			{
				return false;
			}
		}
		return true;
	}

	public static bool InUse(Container c)
	{
		if (c == null)
		{
			return true;
		}
		if (c.IsInUse())
		{
			return true;
		}
		ZNetView component = c.GetComponent<ZNetView>();
		if (component != null && component.IsValid() && !component.IsOwner())
		{
			return component.GetZDO().GetInt(ZDOVars.s_inUse) == 1;
		}
		return false;
	}

	public static bool CompanionHeld(Container c)
	{
		if (c == null)
		{
			return false;
		}
		int instanceID = c.GetInstanceID();
		if (!s_held.TryGetValue(instanceID, out var value))
		{
			return false;
		}
		if (Time.time - value > HoldWait || !InUse(c) || PlayerHas(c))
		{
			s_held.Remove(instanceID);
			return false;
		}
		return true;
	}

	public static bool Busy(Container c)
	{
		if (InUse(c))
		{
			return !CompanionHeld(c);
		}
		return false;
	}

	private static bool PlayerHas(Container c)
	{
		try
		{
			return InventoryGui.instance != null && InventoryGui.IsVisible() && s_guiContainer != null && (object)(s_guiContainer.GetValue(InventoryGui.instance) as Container) == c;
		}
		catch
		{
			return false;
		}
	}

	public static void Claim(Container c)
	{
		ZNetView zNetView = ((c != null) ? c.GetComponent<ZNetView>() : null);
		if (zNetView != null && zNetView.IsValid() && !zNetView.IsOwner())
		{
			zNetView.ClaimOwnership();
		}
	}

	public static bool Open(Container c)
	{
		if (c == null || InUse(c))
		{
			return false;
		}
		if (Player.m_localPlayer == null)
		{
			return false;
		}
		ZNetView component = c.GetComponent<ZNetView>();
		if (component == null || !component.IsValid())
		{
			return false;
		}
		Claim(c);
		c.SetInUse(inUse: true);
		s_held[c.GetInstanceID()] = Time.time;
		return true;
	}

	public static void Close(Container c)
	{
		if (!(c == null) && !(Player.m_localPlayer == null))
		{
			ZNetView component = c.GetComponent<ZNetView>();
			if (!(component == null) && component.IsValid())
			{
				c.SetInUse(inUse: false);
			}
		}
	}

	public static void AddHomeChests(List<Container> list, CompanionSetup cs)
	{
		if (list == null || cs == null || !cs.HasHomePosition() || !MHR_ToolKeeper.NearHome(cs))
		{
			return;
		}
		foreach (Container item in Near(cs.GetHomePosition(), MHR_ToolKeeper.HomeRange()))
		{
			if (item != null && item.gameObject != cs.gameObject && !list.Contains(item) && Usable(item, cs))
			{
				list.Add(item);
			}
		}
	}

	public static HashSet<int> DropsNear(Vector3 center, float radius)
	{
		HashSet<int> hashSet = new HashSet<int>();
		Collider[] array = Physics.OverlapSphere(center, radius);
		foreach (Collider collider in array)
		{
			ItemDrop itemDrop = ((collider != null) ? collider.GetComponentInParent<ItemDrop>() : null);
			if (itemDrop != null)
			{
				hashSet.Add(itemDrop.GetInstanceID());
			}
		}
		return hashSet;
	}

	private static int Mask()
	{
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
		}
		return s_mask;
	}

	public static List<Container> Near(Vector3 center, float radius)
	{
		List<Container> list = new List<Container>();
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(center, radius, s_pieces);
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece piece = s_pieces[i];
			if (!(piece == null))
			{
				Container componentInChildren = piece.GetComponentInChildren<Container>();
				if (!(componentInChildren == null) && hashSet.Add(componentInChildren.GetInstanceID()))
				{
					list.Add(componentInChildren);
				}
			}
		}
		return list;
	}

	public static List<CraftingStation> Stations(Vector3 center, float radius)
	{
		List<CraftingStation> list = new List<CraftingStation>();
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(center, radius, s_pieces);
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece piece = s_pieces[i];
			if (!(piece == null))
			{
				CraftingStation componentInChildren = piece.GetComponentInChildren<CraftingStation>();
				if (!(componentInChildren == null) && hashSet.Add(componentInChildren.GetInstanceID()))
				{
					list.Add(componentInChildren);
				}
			}
		}
		return list;
	}

	private static bool IsChestPart(Collider col, Container chest)
	{
		if (col != null && chest != null)
		{
			return col.GetComponentInParent<Container>() == chest;
		}
		return false;
	}

	public static bool InReach(Vector3 feet, Container chest)
	{
		if (chest == null)
		{
			return false;
		}
		if (Vector3.Distance(feet + Vector3.up * 1.6f, chest.transform.position + Vector3.up * 0.5f) <= 4f)
		{
			return LineClear(feet, chest);
		}
		return false;
	}

	public static bool LineClear(Vector3 from, Container chest)
	{
		if (!LineClear(from, chest, 1.6f, 0.5f) && !LineClear(from, chest, 1.9f, 0.9f))
		{
			return LineClear(from, chest, 1f, 0.5f);
		}
		return true;
	}

	private static bool LineClear(Vector3 from, Container chest, float height, float target)
	{
		if (chest == null)
		{
			return false;
		}
		Vector3 vector = from + Vector3.up * height;
		Vector3 vector2 = chest.transform.position + Vector3.up * target - vector;
		float magnitude = vector2.magnitude;
		if (magnitude < 0.05f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(vector, vector2 / magnitude, magnitude, Mask());
		for (int i = 0; i < array.Length; i++)
		{
			Collider collider = array[i].collider;
			if (!(collider == null) && !IsChestPart(collider, chest) && !(collider.GetComponentInParent<Character>() != null) && !(collider.GetComponentInParent<Container>() != null))
			{
				Vector3 size = collider.bounds.size;
				if (!(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f))
				{
					return false;
				}
			}
		}
		return true;
	}

	public static bool CanReach(Vector3 me, Container chest)
	{
		if (chest == null)
		{
			return false;
		}
		if (InReach(me, chest))
		{
			return true;
		}
		int instanceID = chest.GetInstanceID();
		if (s_reach.TryGetValue(instanceID, out var value) && Time.time - value.Key < 30f)
		{
			return value.Value;
		}
		bool flag = PickApproach(me, chest, out var _);
		if (s_reach.Count > 500)
		{
			s_reach.Clear();
		}
		s_reach[instanceID] = new KeyValuePair<float, bool>(Time.time, flag);
		return flag;
	}

	public static string Fmt(Vector3 v)
	{
		return "(" + v.x.ToString("F1") + ", " + v.y.ToString("F1") + ", " + v.z.ToString("F1") + ")";
	}

	public static bool PickApproach(Vector3 me, Container chest, out Vector3 spot)
	{
		spot = Vector3.zero;
		if (chest == null)
		{
			return false;
		}
		Vector3 position = chest.transform.position;
		float num = float.MaxValue;
		bool flag = false;
		List<Vector3> list = null;
		float[] array = new float[4] { 1.2f, 1.7f, 2.2f, 2.8f };
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		int num6 = 0;
		int num7 = 0;
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				float f = (float)j * 22.5f * (Mathf.PI / 180f);
				RaycastHit[] array2 = Physics.RaycastAll(position + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * array[i] + Vector3.up * 1.6f, Vector3.down, 4.5f, Mask());
				if (array2.Length == 0)
				{
					num2++;
					continue;
				}
				Array.Sort(array2, (RaycastHit raycastHit2, RaycastHit b) => raycastHit2.distance.CompareTo(b.distance));
				for (int num8 = 0; num8 < array2.Length; num8++)
				{
					RaycastHit raycastHit = array2[num8];
					if (raycastHit.normal.y < 0.6f || IsChestPart(raycastHit.collider, chest) || IsFurniture(raycastHit.collider))
					{
						continue;
					}
					Vector3 point = raycastHit.point;
					if (point.y > position.y + 1.2f || point.y < position.y - 2.6f)
					{
						num3++;
						continue;
					}
					if (MHR_Mover.InFire(point))
					{
						num4++;
						continue;
					}
					if (Blocked(point + Vector3.up * 1f, chest))
					{
						num5++;
						continue;
					}
					if (!InReach(point, chest))
					{
						num6++;
						continue;
					}
					s_path.Clear();
					if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(me, point, s_path, MHR_CompanionNavMesh.AgentOrHumanoid, requireFullPath: false, cleanup: true, havePath: true) || s_path.Count == 0)
					{
						num7++;
						if (list == null)
						{
							list = new List<Vector3>();
						}
						list.Add(point);
						continue;
					}
					Vector3 vector = s_path[s_path.Count - 1];
					if (new Vector2(vector.x - point.x, vector.z - point.z).magnitude > 0.6f || Mathf.Abs(vector.y - point.y) > 1f)
					{
						num7++;
						if (list == null)
						{
							list = new List<Vector3>();
						}
						list.Add(point);
						continue;
					}
					float num9 = 0f;
					Vector3 a = me;
					for (int num10 = 0; num10 < s_path.Count; num10++)
					{
						num9 += Vector3.Distance(a, s_path[num10]);
						a = s_path[num10];
					}
					if (num9 < num)
					{
						num = num9;
						spot = point;
						flag = true;
					}
				}
			}
			if (flag)
			{
				break;
			}
		}
		if (!flag && list != null)
		{
			for (int num11 = 0; num11 < list.Count && num11 < 4; num11++)
			{
				if (MHR_Mover.ReachableViaDoor(me, list[num11]))
				{
					spot = list[num11];
					flag = true;
					break;
				}
			}
		}
		if (!flag && CompanionsPlugin.Log != null && CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Chests] No spot to stand by the chest at " + Fmt(position) + ": " + num2 + " no ground, " + num3 + " too high/low, " + num4 + " in a fire, " + num5 + " no room, " + num6 + " out of reach/sight, " + num7 + " no route");
		}
		return flag;
	}

	public static bool PickApproachPiece(Vector3 me, Component target, out Vector3 spot)
	{
		spot = Vector3.zero;
		if (target == null)
		{
			return false;
		}
		return PickNear(me, target.transform.position, target.GetComponentInParent<Piece>(), s_pieceRadii, 8, 1.5f, out spot);
	}

	public static bool PickApproachPoint(Vector3 me, Vector3 point, Component owner, out Vector3 spot)
	{
		return PickNear(me, point, (owner != null) ? owner.GetComponentInParent<Piece>() : null, s_pointRadii, 12, 1.8f, out spot);
	}

	private static bool PickNear(Vector3 me, Vector3 cp, Piece piece, float[] radii, int dirs, float maxDy, out Vector3 spot)
	{
		spot = Vector3.zero;
		float num = float.MaxValue;
		bool flag = false;
		for (int i = 0; i < radii.Length; i++)
		{
			for (int j = 0; j < dirs; j++)
			{
				float f = (float)j * (360f / (float)dirs) * (Mathf.PI / 180f);
				RaycastHit[] array = Physics.RaycastAll(cp + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * radii[i] + Vector3.up * 1.6f, Vector3.down, 3.2f + maxDy, Mask());
				Array.Sort(array, (RaycastHit raycastHit2, RaycastHit b) => raycastHit2.distance.CompareTo(b.distance));
				for (int num2 = 0; num2 < array.Length; num2++)
				{
					RaycastHit raycastHit = array[num2];
					if (IsPart(raycastHit.collider, piece) || raycastHit.normal.y < 0.6f || IsFurniture(raycastHit.collider))
					{
						continue;
					}
					Vector3 point = raycastHit.point;
					if (Mathf.Abs(point.y - cp.y) > maxDy || MHR_Mover.InFire(point) || BlockedNear(point + Vector3.up * 1f, piece) || !ClearTo(point, cp, piece))
					{
						continue;
					}
					s_path.Clear();
					if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(me, point, s_path, MHR_CompanionNavMesh.AgentOrHumanoid, requireFullPath: false, cleanup: true, havePath: true) || s_path.Count == 0)
					{
						continue;
					}
					Vector3 vector = s_path[s_path.Count - 1];
					if (!(new Vector2(vector.x - point.x, vector.z - point.z).magnitude > 0.6f) && !(Mathf.Abs(vector.y - point.y) > 1f))
					{
						float num3 = 0f;
						Vector3 a = me;
						for (int num4 = 0; num4 < s_path.Count; num4++)
						{
							num3 += Vector3.Distance(a, s_path[num4]);
							a = s_path[num4];
						}
						if (num3 < num)
						{
							num = num3;
							spot = point;
							flag = true;
						}
						break;
					}
				}
			}
			if (flag)
			{
				break;
			}
		}
		return flag;
	}

	private static bool IsPart(Collider col, Piece piece)
	{
		if (col != null && piece != null)
		{
			return col.GetComponentInParent<Piece>() == piece;
		}
		return false;
	}

	public static bool IsFurniture(Collider col)
	{
		if (col == null)
		{
			return false;
		}
		if (!(col.GetComponentInParent<Container>() != null) && !(col.GetComponentInParent<Fermenter>() != null) && !(col.GetComponentInParent<CraftingStation>() != null) && !(col.GetComponentInParent<CookingStation>() != null) && !(col.GetComponentInParent<Smelter>() != null) && !(col.GetComponentInParent<Bed>() != null))
		{
			return col.GetComponentInParent<Chair>() != null;
		}
		return true;
	}

	private static bool BlockedNear(Vector3 p, Piece piece)
	{
		int num = Physics.OverlapSphereNonAlloc(p, 0.35f, s_small, Mask());
		for (int i = 0; i < num; i++)
		{
			Collider collider = s_small[i];
			if (!(collider == null) && !IsPart(collider, piece) && !(collider.GetComponentInParent<Character>() != null))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ClearTo(Vector3 from, Vector3 to, Piece piece)
	{
		Vector3 vector = from + Vector3.up * 1f;
		Vector3 vector2 = to + Vector3.up * 0.5f - vector;
		float magnitude = vector2.magnitude;
		if (magnitude < 0.05f)
		{
			return true;
		}
		RaycastHit[] array = Physics.RaycastAll(vector, vector2 / magnitude, magnitude, Mask());
		for (int i = 0; i < array.Length; i++)
		{
			Collider collider = array[i].collider;
			if (!(collider == null) && !IsPart(collider, piece) && !(collider.GetComponentInParent<Character>() != null))
			{
				Vector3 size = collider.bounds.size;
				if (!(Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 1.2f))
				{
					return false;
				}
			}
		}
		return true;
	}

	private static bool Blocked(Vector3 p, Container chest)
	{
		int num = Physics.OverlapSphereNonAlloc(p, 0.35f, s_small, Mask());
		for (int i = 0; i < num; i++)
		{
			Collider collider = s_small[i];
			if (!(collider == null) && !IsChestPart(collider, chest) && !(collider.GetComponentInParent<Character>() != null))
			{
				return true;
			}
		}
		return false;
	}
}
