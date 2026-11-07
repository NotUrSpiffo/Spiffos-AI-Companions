using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Mover
{
	private class S
	{
		public Vector3 Goal;

		public Vector3 Dest;

		public float Dist;

		public string Tag = "";

		public bool Quiet;

		public bool FireReplan;

		public readonly List<Vector3> Path = new List<Vector3>();

		public int Corner;

		public float NextPlan;

		public bool PathOk;

		public bool Partial;

		public float Best;

		public float ProgressTime;

		public float AnchorTime;

		public Vector3 Anchor;

		public int Ladder;

		public float StepUntil;

		public Vector3 StepDir;

		public bool StepRun;

		public bool HasDetour;

		public Vector3 Detour;

		public float DetourUntil;

		public bool GaveUp;

		public float GaveUpAt;

		public bool DoorBlocked;

		public float LastGo;

		public float NextLog;

		public float NextDoorSearch;

		public float NextDoorPeek;

		public float NextJump;

		public float NextDive;

		public float NextWaterPeek;

		public float NextLineLog;

		public float NextFireLog;

		public Door RouteDoor;

		public Vector3 RouteDoorSpot;

		public bool WaitStamina;

		public bool TooTired;

		public float NeedStamina;

		public float WaitStart;

		public float NoWaitUntil;

		public float MainBest;

		public int Passes;

		public Vector3 Shore;

		public bool HasShore;

		public float NextShore;

		public bool Walking;

		public bool NewLeg;

		public float NextStartSearch;

		public Door LastRouteDoor;

		public float LastRouteDoorAt;

		public float RouteSwim;

		public Vector3 LastWaterJumpDir;

		public float LastWaterJumpAt = -99f;

		public int HopUpTries;

		public bool DescendLadder;

		public float DescendUntil;

		public Vector3 DescendTop;

		public bool ClimbLadder;

		public bool ClimbAuto;

		public bool LadderPending;

		public Vector3 ClimbDir;

		public Vector3 ClimbBase;

		public float ClimbStartY;

		public float ClimbStartAt;

		public float ClimbBottomY;

		public float NextEdgeLog;

		public float ClimbViaUntil;

		public float ClimbFootBest;

		public float ClimbFootAt;

		public float ClimbFaceAt;

		public Vector3 ClimbFace;

		public List<LadderStep> Chain;

		public List<LadderStep> PendingChain;

		public bool OnChain;

		public bool ChainDown;

		public bool PendingDown;

		public int ChainIdx;

		public int ChainJumps;

		public AutoJumpLedge[] ClimbLedges;

		public float ChainProgressAt;

		public float ChainBestY;

		public float ChainJumpAt;

		public float ChainPushAt;

		public float ChainRestUntil;

		public float ChainFootBest;

		public float ChainFootAt;

		public float ChainViaUntil;

		public Vector3 ChainVia;

		public Vector3 ClimbVia;

		public int ClimbJumps;

		public int ClimbLadderId;

		public float NextLadderClimbSearch;

		public float NextLadderLog;

		public readonly HashSet<int> LadderClimbsTried = new HashSet<int>();

		public readonly Dictionary<int, Vector2> LadderUses = new Dictionary<int, Vector2>();

		public Ladder UseLadder;

		public float LadderUntil;

		public float NextLadderSearch;

		public readonly HashSet<Ladder> LaddersUsed = new HashSet<Ladder>();

		public bool MainPartial;

		public Vector3 GoodPos;

		public float GoodAt;

		public bool HasGood;

		public float ClimbUntil;

		public float ClimbBestY;

		public float ClimbProgressAt;

		public float NextClimbJump;

		public float ClimbRestUntil;

		public Vector3 ClimbTop;

		public int ClimbTries;

		public readonly List<Vector3> ClimbTried = new List<Vector3>();

		public bool Direct;

		public float NextHop;

		public float NoDirectUntil;

		public Door PassDoor;

		public float PassSide;

		public float NextDoorwayCheck;
	}

	private class Opened
	{
		public Door Door;

		public float At;
	}

	internal class LadderStep
	{
		public Vector3 Foot;

		public Vector3 Dir;

		public Vector3 TopSpot;

		public float Bottom;

		public float Top;

		public float Run;

		public int Id;

		public bool LinedUp;
	}

	private class KnownChain
	{
		public List<LadderStep> Steps;

		public float Until;
	}

	private class FireZone
	{
		public Fireplace Fp;

		public EffectArea Area;

		public Aoe Aoe;

		public Collider[] Triggers;
	}

	private class DoorNode
	{
		public int Door;

		public int Side;

		public int Depth;

		public int First;

		public Vector3 FirstSpot;

		public float Len;
	}

	private class F
	{
		public Vector3 Anchor;

		public float AnchorTime;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static readonly Dictionary<int, List<Opened>> s_opened = new Dictionary<int, List<Opened>>();

	private static readonly List<Vector3> s_tmp = new List<Vector3>();

	private static readonly List<Vector3> s_tmp2 = new List<Vector3>();

	private static readonly List<Piece> s_pieces = new List<Piece>();

	private static readonly Collider[] s_cols = new Collider[16];

	private static readonly Collider[] s_doorCols = new Collider[512];

	private static int s_mask = -1;

	private static float s_nextErr;

	public const float StillTime = 2.5f;

	public const float NoProgressTime = 8f;

	public const string CombatTag = "Combat";

	public const int SpeedAuto = 0;

	public const int SpeedWalk = 1;

	public const int SpeedJog = 2;

	public const int SpeedRun = 3;

	public const string FollowTag = "Follow";

	private static readonly List<Vector3> s_walkPath = new List<Vector3>();

	private static bool s_swimming;

	private const int ClimbMaxTries = 3;

	private const float ClimbMaxRise = 10f;

	private static readonly int s_terrainLayer = LayerMask.NameToLayer("terrain");

	private const int LadderJumpTries = 4;

	private static readonly List<Vector3> s_ladderPath = new List<Vector3>();

	private static readonly Dictionary<int, float> s_badLadders = new Dictionary<int, float>();

	private static readonly List<KnownChain> s_knownChains = new List<KnownChain>();

	private static string s_hopWhy;

	private static readonly Collider[] s_spikeBuf = new Collider[32];

	private static readonly Dictionary<int, float> s_lavaSince = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_lavaLog = new Dictionary<int, float>();

	private static readonly List<FireZone> s_zones = new List<FireZone>();

	private static float s_zonesAt = -10f;

	private static readonly Dictionary<int, float> s_fireLog = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_fireSince = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_fireJump = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_leafClosed = new Dictionary<int, float>();

	private static readonly List<Door> s_doors = new List<Door>();

	private static readonly List<Vector3> s_reachPath = new List<Vector3>();

	private static readonly Dictionary<int, F> s_follow = new Dictionary<int, F>();

	private static int Mask()
	{
		if (s_mask == -1)
		{
			s_mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
		}
		return s_mask;
	}

	private static S Get(Component c)
	{
		if (c == null)
		{
			return null;
		}
		if (!s_s.TryGetValue(c.gameObject.GetInstanceID(), out var value))
		{
			value = new S();
			s_s[c.gameObject.GetInstanceID()] = value;
		}
		return value;
	}

	private static S Peek(Component c)
	{
		if (!(c != null) || !s_s.TryGetValue(c.gameObject.GetInstanceID(), out var value))
		{
			return null;
		}
		return value;
	}

	private static void Dbg(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug(msg);
		}
	}

	public static string Fmt(Vector3 v)
	{
		return "(" + v.x.ToString("F1") + ", " + v.y.ToString("F1") + ", " + v.z.ToString("F1") + ")";
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	public static bool OwnsMovement(Component c)
	{
		S s = Peek(c);
		if (s != null)
		{
			return Time.time - s.LastGo < 0.3f;
		}
		return false;
	}

	public static bool GaveUp(CompanionAI ai)
	{
		return Peek(ai)?.GaveUp ?? false;
	}

	public static bool DoorBlocked(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.GaveUp)
		{
			return s.DoorBlocked;
		}
		return false;
	}

	public static bool RoutePartial(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && Time.time - s.LastGo < 0.5f && s.Partial)
		{
			return !s.HasDetour;
		}
		return false;
	}

	public static float JobSwim()
	{
		if (ModConfig.JobSwim == null)
		{
			return 20f;
		}
		return ModConfig.JobSwim.Value;
	}

	public static float RouteSwim(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s == null || !(Time.time - s.LastGo < 0.5f))
		{
			return 0f;
		}
		return s.RouteSwim;
	}

	public static bool RestingForSwim(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.WaitStamina)
		{
			return Time.time - s.LastGo < 0.5f;
		}
		return false;
	}

	public static float RouteLength(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s == null || Time.time - s.LastGo >= 0.5f || !s.PathOk || s.Path.Count == 0)
		{
			return 0f;
		}
		return Remaining(s, ai.transform.position);
	}

	public static bool OnTheWayUp(CompanionAI ai)
	{
		S s = Peek(ai);
		float time = Time.time;
		if (s != null && time - s.LastGo < 1f)
		{
			if (!(time < s.ClimbUntil) && !s.LadderPending && !s.DescendLadder && !(time < s.StepUntil))
			{
				return s.OnChain;
			}
			return true;
		}
		return false;
	}

	public static bool OnLadderChain(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.OnChain)
		{
			return Time.time - s.LastGo < 1f;
		}
		return false;
	}

	public static bool Progressing(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && !s.GaveUp)
		{
			return Time.time - s.ProgressTime < 10f;
		}
		return false;
	}

	public static float StuckSeconds(CompanionAI ai)
	{
		if (!GaveUp(ai))
		{
			return 0f;
		}
		return 11f;
	}

	public static bool KeepAlive(Component c)
	{
		S s = Peek(c);
		if (s != null && !s.GaveUp && Time.time - s.LastGo < 0.5f)
		{
			return Time.time - s.ProgressTime < 10f;
		}
		return false;
	}

	public static void Release(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null)
		{
			s.LastGo = 0f;
			s.GaveUp = false;
			s.Path.Clear();
			s.HasDetour = false;
			s.RouteDoor = null;
			Character component = ai.GetComponent<Character>();
			if (component != null && s.Walking)
			{
				component.SetWalk(walk: false);
				s.Walking = false;
			}
		}
	}

	public static bool Go(CompanionAI ai, float dt, Vector3 goal, float dist, bool allowRun, string tag)
	{
		return Go(ai, dt, goal, dist, allowRun, tag, 0);
	}

	public static bool Go(CompanionAI ai, float dt, Vector3 goal, float dist, bool allowRun, string tag, int speed)
	{
		if (ai == null)
		{
			return false;
		}
		try
		{
			return GoInner(ai, goal, dist, allowRun, tag ?? "", speed);
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null && Time.time >= s_nextErr)
			{
				s_nextErr = Time.time + 30f;
				CompanionsPlugin.Log.LogWarning("[Move] " + ex.Message);
			}
			return false;
		}
	}

	private static bool GoInner(CompanionAI ai, Vector3 goal, float dist, bool allowRun, string tag, int speed)
	{
		float time = Time.time;
		S s = Get(ai);
		Character component = ai.GetComponent<Character>();
		Vector3 position = ai.transform.position;
		float num = Vector3.Distance(goal, s.Goal);
		bool flag = num > 4f || time - s.LastGo > 2f || s.Tag != tag;
		if (s.GaveUp && time - s.GaveUpAt > 15f)
		{
			flag = true;
		}
		if (flag)
		{
			Reset(s, goal, position, time, tag);
		}
		else if (time - s.LastGo > 0.5f || (num > 1.5f && tag != "Follow"))
		{
			s.AnchorTime = time;
			s.Anchor = position;
			s.ProgressTime = time;
			s.Best = float.MaxValue;
			s.StepUntil = 0f;
			if (num > 1.5f)
			{
				s.MainBest = float.MaxValue;
				s.Ladder = 0;
				s.Passes = 0;
				s.GaveUp = false;
			}
		}
		else if (num > 1.5f && !s.HasDetour && s.RouteDoor == null)
		{
			s.NextPlan = 0f;
		}
		else if (!s.HasDetour && s.RouteDoor == null && Vector3.Distance(goal, s.Dest) > 1.5f && s.NextPlan > time + 0.7f)
		{
			s.NextPlan = Mathf.Min(s.NextPlan, time + 0.3f);
		}
		s.LastGo = time;
		s.Goal = goal;
		s.Dist = dist;
		CompanionStamina component2 = ai.GetComponent<CompanionStamina>();
		s.Quiet = component2 != null && component2.IsCrouching;
		if (Flat(position, goal) <= dist && Mathf.Abs(position.y - goal.y) <= 1.5f)
		{
			Halt(ai, component, s);
			return true;
		}
		if (s.GaveUp)
		{
			Halt(ai, component, s);
			return false;
		}
		if (component == null || component.InAttack())
		{
			return false;
		}
		if (time >= s.NextPlan || s.Path.Count == 0)
		{
			Plan(ai, component, s, position, time);
		}
		s_swimming = component.IsSwimming();
		AdvanceCorner(s, position);
		float num2 = Remaining(s, position);
		if (s.NewLeg)
		{
			s.NewLeg = false;
			s.Best = num2;
		}
		if (num2 < s.Best - 0.75f)
		{
			s.Best = num2;
			s.ProgressTime = time;
		}
		if (!s.HasDetour && s.RouteDoor == null && num2 < s.MainBest - 1.5f)
		{
			s.MainBest = num2;
			s.Ladder = 0;
			s.Passes = 0;
		}
		if (Vector3.Distance(position, s.Anchor) > 0.6f)
		{
			s.Anchor = position;
			s.AnchorTime = time;
		}
		Log(s, position, num2, time, component);
		if (s.OnChain)
		{
			RunChain(ai, component, s, position, time, tag);
			if (s.OnChain)
			{
				return false;
			}
		}
		if (time < s.ClimbUntil)
		{
			Climb(ai, component, s, position, time, tag);
			return false;
		}
		if (s.DescendLadder && !s.LadderPending)
		{
			if (time < s.DescendUntil)
			{
				Descend(ai, component, s, position, time, tag);
				return false;
			}
			MarkBadLadder(s.ClimbLadderId, time, 300f);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get down the ladder (got to " + Fmt(position) + ")");
			}
			s.DescendLadder = false;
		}
		if (time < s.StepUntil)
		{
			if (!s.StepRun && component.IsOnGround() && !component.IsSwimming() && (DropAhead(position, s.StepDir, 0.8f, 1.2f) || FireArea(position + s.StepDir * 0.8f, 0.3f) != null || SpikeNear(position + s.StepDir * 0.8f, 0.6f) != null))
			{
				s.StepUntil = 0f;
				Halt(ai, component, s);
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] stopped a step - a drop, fire or spikes ahead");
				}
				return false;
			}
			Steer(ai, component, s, s.StepDir, s.StepRun, walk: false);
			return false;
		}
		if (EscapeFire(ai))
		{
			s.FireReplan = true;
			return false;
		}
		if (EscapeLava(ai))
		{
			s.FireReplan = true;
			return false;
		}
		if (s.FireReplan)
		{
			s.FireReplan = false;
			s.HasDetour = false;
			s.NextPlan = 0f;
		}
		if (s.WaitStamina)
		{
			if (Stamina(ai) >= s.NeedStamina)
			{
				s.WaitStamina = false;
				s.TooTired = false;
				s.NoWaitUntil = time + 30f;
				s.NextPlan = 0f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] rested enough to swim across");
				}
			}
			else
			{
				if (!(time - s.WaitStart > 40f))
				{
					Halt(ai, component, s);
					s.ProgressTime = time;
					s.AnchorTime = time;
					return false;
				}
				s.WaitStamina = false;
				s.TooTired = false;
				s.NoWaitUntil = time + 60f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] waited long enough - swimming across");
				}
			}
		}
		if (s.UseLadder != null || (object)s.UseLadder != null)
		{
			if (UseLadderNow(ai, component, s, position, time, tag))
			{
				return false;
			}
			if (s.UseLadder == null)
			{
				s.HasDetour = false;
				Plan(ai, component, s, position, time);
			}
		}
		else if (s.MainPartial && !s.HasDetour && s.RouteDoor == null && !component.IsSwimming() && time >= s.NextLadderSearch && s.Goal.y > position.y + 2f)
		{
			if (!TryLadder(ai, s, position, time, tag) && time >= s.NextLadderClimbSearch && TryStartLadderClimb(ai, component, s, position, time, tag))
			{
				return false;
			}
		}
		else if (s.MainPartial && !s.HasDetour && !s.DescendLadder && s.RouteDoor == null && !component.IsSwimming() && time >= s.NextLadderClimbSearch && s.Goal.y < position.y - 2f && TryLadderDown(ai, component, s, position, time, tag))
		{
			return false;
		}
		if (s.LadderPending)
		{
			if (s.DescendLadder && Flat(position, s.DescendTop) < 1f && Mathf.Abs(position.y - s.DescendTop.y) < 1.5f)
			{
				s.LadderPending = false;
				s.HasDetour = false;
				s.DescendUntil = time + 10f;
				if (s.PendingChain != null && s.PendingDown && s.PendingChain.Count >= 2)
				{
					StartChain(s, s.PendingChain, 0, position, time, tag, down: true);
					s.PendingChain = null;
					return false;
				}
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] at the top of the ladder - going down");
				}
				return false;
			}
			if (!s.DescendLadder && Flat(position, s.ClimbBase) < 1f && Mathf.Abs(position.y - s.ClimbBase.y) < 1.5f)
			{
				s.LadderPending = false;
				s.HasDetour = false;
				if (s.PendingChain != null && !s.PendingDown && s.PendingChain.Count >= 2)
				{
					StartChain(s, s.PendingChain, 0, position, time, tag);
					s.PendingChain = null;
					return false;
				}
				s.ClimbLadder = true;
				s.ClimbStartY = position.y;
				s.ClimbStartAt = time;
				s.ClimbJumps = 0;
				s.ClimbViaUntil = 0f;
				s.ClimbFootBest = float.MaxValue;
				s.ClimbFootAt = time;
				s.ClimbUntil = time + 20f;
				s.ClimbBestY = position.y;
				s.ClimbProgressAt = time;
				s.NextClimbJump = 0f;
				NoteLadderClimb(s, time, tag);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] at the foot of the ladder - climbing");
				}
				return false;
			}
			if (!s.DescendLadder && position.y < s.ClimbBase.y - 1.5f && component.IsOnGround())
			{
				s.LadderPending = false;
				s.HasDetour = false;
				s.NextLadderClimbSearch = 0f;
				s.NextPlan = 0f;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off on the way to the ladder (now at " + Fmt(position) + ") - looking again");
				}
				Plan(ai, component, s, position, time);
			}
			else if (time > s.DetourUntil || !s.HasDetour)
			{
				s.LadderPending = false;
				s.HasDetour = false;
				MarkBadLadder(s.ClimbLadderId, time, 120f);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get to the " + (s.DescendLadder ? ("top of the ladder at " + Fmt(s.DescendTop)) : ("foot of the ladder at " + Fmt(s.ClimbBase))));
				}
				s.DescendLadder = false;
				Plan(ai, component, s, position, time);
			}
		}
		else if (s.HasDetour && s.UseLadder == null && (Flat(position, s.Detour) < 1f || time > s.DetourUntil))
		{
			s.HasDetour = false;
			Plan(ai, component, s, position, time);
		}
		if (s.RouteDoor != null)
		{
			if (!DoorClosed(s.RouteDoor))
			{
				s.RouteDoor = null;
				Plan(ai, component, s, position, time);
			}
			else if (MHR_Nav.DoorDist(position, s.RouteDoor.transform.position) <= 2.2f)
			{
				Vector3 position2 = s.RouteDoor.transform.position;
				Vector3 vector = position2 - s.RouteDoorSpot;
				vector.y = 0f;
				OpenDoor(ai, component, s.RouteDoor, tag);
				s.LastRouteDoor = s.RouteDoor;
				s.LastRouteDoorAt = time;
				if (vector.sqrMagnitude > 0.01f)
				{
					s.HasDetour = true;
					s.Detour = position2 + vector.normalized * 1.8f;
					s.DetourUntil = time + 5f;
				}
				s.RouteDoor = null;
				s.NextPlan = 0f;
				Halt(ai, component, s);
				return false;
			}
		}
		Vector3 vector2 = NextCorner(s, position);
		if (component.IsSwimming() && (!s.PathOk || s.Partial))
		{
			if (time >= s.NextShore)
			{
				s.NextShore = time + 0.5f;
				s.HasShore = FindShore(component, position, goal, out var best);
				s.Shore = best;
			}
			if (s.HasShore)
			{
				vector2 = s.Shore;
			}
		}
		if (!component.IsSwimming())
		{
			vector2 = DoorwayLine(s, position, vector2, time, tag);
		}
		if (time >= s.NextDoorPeek)
		{
			s.NextDoorPeek = time + 0.4f;
			Door door = DoorAhead(position, vector2);
			if (door != null)
			{
				OpenDoor(ai, component, door, tag);
				if (DoorClosed(door))
				{
					s.NextDoorPeek = time + 0.15f;
				}
				Halt(ai, component, s);
				return false;
			}
			Door door2 = OpenLeafAhead(position, vector2);
			if (door2 == null && time - s.AnchorTime > 1f)
			{
				door2 = OpenDoorNear(position, 2f);
			}
			if (door2 != null && tag != "Combat")
			{
				CloseDoor(ai, component, door2, tag);
				s.NextDoorPeek = time + 0.15f;
				Halt(ai, component, s);
				return false;
			}
		}
		Vector3 vector3 = vector2 - position;
		vector3.y = 0f;
		if (vector3.sqrMagnitude < 0.04f && s.Corner < s.Path.Count - 1)
		{
			s.Corner++;
			vector2 = NextCorner(s, position);
			vector3 = vector2 - position;
			vector3.y = 0f;
		}
		if (vector3.sqrMagnitude < 0.0001f)
		{
			Halt(ai, component, s);
			if (time - s.AnchorTime >= 2.5f)
			{
				Unstick(ai, component, s, position, s.Goal, time, tag, time - s.AnchorTime, time - s.ProgressTime);
			}
			return false;
		}
		vector3.Normalize();
		if (!component.IsSwimming() && component.IsOnGround() && time >= s.NextJump && time >= s.NextWaterPeek)
		{
			s.NextWaterPeek = time + 0.2f;
			if (TryWaterJump(ai, component, s, position, vector3, time, tag))
			{
				return false;
			}
		}
		if (s.Direct && !s.Quiet && !component.IsSwimming() && component.IsOnGround() && time >= s.NextJump && GapAhead(component, position, vector3) && !LandingHazard(position, vector3))
		{
			Leap(ai, component, vector3, 6.5f);
			s.NextJump = time + 1.2f;
			s.StepDir = vector3;
			s.StepRun = true;
			s.StepUntil = time + 0.6f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] jumping the gap at " + Fmt(position + vector3 * 0.8f));
			}
			return false;
		}
		EffectArea effectArea = FireArea(position + vector3 * 0.8f, 0.3f);
		if (effectArea != null && !InFire(goal))
		{
			Collider component3 = effectArea.GetComponent<Collider>();
			if (component3 != null && Mathf.Max(component3.bounds.extents.x, component3.bounds.extents.z) < 0.9f && component.IsOnGround() && time >= s.NextHop && !NearFire(position + vector3 * 2.5f, 0.3f) && !DropAhead(position, vector3, 2.5f, 1.2f))
			{
				s.NextHop = time + 1f;
				ai.transform.rotation = Quaternion.LookRotation(vector3);
				component.Jump();
				Rigidbody component4 = ai.GetComponent<Rigidbody>();
				if (component4 != null)
				{
					Vector3 linearVelocity = component4.linearVelocity;
					linearVelocity.x = vector3.x * 6f;
					linearVelocity.z = vector3.z * 6f;
					component4.linearVelocity = linearVelocity;
				}
				s.StepDir = vector3;
				s.StepRun = true;
				s.StepUntil = time + 0.6f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] jumping over a fire at " + Fmt(position + vector3 * 1.2f));
				}
				return false;
			}
			if (FireExit(position, vector3, out var dir, wholeLeg: true))
			{
				vector3 = dir;
			}
		}
		if (!component.IsSwimming() && WorldGenerator.IsAshlands(position.x, position.z) && (Lava(position + vector3 * 0.9f) || Lava(position + vector3 * 1.6f)) && !Lava(goal))
		{
			if (!LavaExit(position + vector3 * 0f, vector3, out var dir2, out var _) || !(Vector3.Dot(dir2, vector3) > -0.2f) || !LegClearOfLava(position, position + dir2 * 1.5f))
			{
				Halt(ai, component, s);
				if (time >= s.NextFireLog)
				{
					s.NextFireLog = time + 10f;
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stopping at the edge of the lava at " + Fmt(position + vector3 * 0.9f));
					}
				}
				if (time - s.AnchorTime >= 2.5f)
				{
					Unstick(ai, component, s, position, s.Goal, time, tag, time - s.AnchorTime, time - s.ProgressTime);
				}
				return false;
			}
			vector3 = dir2;
		}
		Collider collider = SpikeNear(position, component.GetRadius() + 0.35f);
		bool flag2 = collider != null;
		if (collider == null)
		{
			collider = SpikeNear(position + vector3 * 1.2f, 0.8f);
		}
		if (collider != null)
		{
			Vector3 vector4 = collider.ClosestPoint(position);
			Vector3 vector5 = position - vector4;
			vector5.y = 0f;
			if (vector5.sqrMagnitude > 0.0001f)
			{
				vector5.Normalize();
				Vector3 vector6 = Vector3.Cross(Vector3.up, vector5);
				if (Vector3.Dot(vector6, vector3) < 0f)
				{
					vector6 = -vector6;
				}
				vector3 = (vector6 + vector5 * (flag2 ? 1.5f : 0.6f)).normalized;
				if (time >= s.NextFireLog)
				{
					s.NextFireLog = time + 10f;
					if (CompanionsPlugin.LogDetailed)
					{
						Dbg("[Move|" + tag + "] keeping off the spikes at " + Fmt(vector4));
					}
				}
			}
		}
		if (!component.IsSwimming() && component.IsOnGround() && LedgeAhead(position, vector3, 0.7f, out var drop))
		{
			Halt(ai, component, s);
			if (time >= s.NextEdgeLog)
			{
				s.NextEdgeLog = time + 5f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] stopping at the edge at " + Fmt(position + vector3 * 0.7f) + " (" + drop.ToString("F1") + "m down)");
				}
			}
			if (time - s.AnchorTime >= 2.5f)
			{
				Unstick(ai, component, s, position, s.Goal, time, tag, time - s.AnchorTime, time - s.ProgressTime);
			}
			return false;
		}
		float num3 = ((ModConfig.WorkRunDistance != null) ? ModConfig.WorkRunDistance.Value : 25f);
		bool run = allowRun && num3 > 0f && num2 > num3;
		bool walk = num2 < 2.5f;
		switch (speed)
		{
		case 1:
			run = false;
			walk = true;
			break;
		case 2:
			run = false;
			walk = false;
			break;
		case 3:
			run = allowRun;
			walk = false;
			break;
		}
		Steer(ai, component, s, vector3, run, walk);
		if (time >= s.NextHop && component.IsOnGround() && !component.InAttack() && !component.IsSwimming())
		{
			bool num4;
			if (!s.Quiet)
			{
				if (s.Direct)
				{
					goto IL_1392;
				}
				num4 = time - s.AnchorTime > 0.5f;
			}
			else
			{
				num4 = time - s.AnchorTime > 1.5f;
			}
			if (num4)
			{
				goto IL_1392;
			}
		}
		goto IL_1450;
		IL_1392:
		if (StepAhead(position, vector3) && !NearFire(position + vector3 * 0.8f, 0.4f) && !NearFire(position + vector3 * 1.5f, 0.4f))
		{
			s.NextHop = time + 0.6f;
			ai.transform.rotation = Quaternion.LookRotation(vector3);
			component.Jump();
			Rigidbody component5 = ai.GetComponent<Rigidbody>();
			if (component5 != null)
			{
				Vector3 linearVelocity2 = component5.linearVelocity;
				linearVelocity2.x = vector3.x * 4f;
				linearVelocity2.z = vector3.z * 4f;
				component5.linearVelocity = linearVelocity2;
			}
		}
		goto IL_1450;
		IL_1450:
		float num5 = time - s.AnchorTime;
		float num6 = time - s.ProgressTime;
		if (tag == "Follow")
		{
			num6 = 0f;
		}
		if (num5 >= 2.5f || num6 >= 8f)
		{
			Unstick(ai, component, s, position, vector2, time, tag, num5, num6);
		}
		return false;
	}

	private static void Reset(S s, Vector3 goal, Vector3 pos, float now, string tag)
	{
		bool num = s.UseLadder != null && s.Tag == tag && now <= s.LadderUntil && goal.y > pos.y + 1.5f;
		bool num2 = s.ClimbLadder && s.Tag == tag && now < s.ClimbUntil && goal.y > pos.y + 1.5f;
		bool flag = s.LadderPending && s.Tag == tag && now <= s.DetourUntil && (s.DescendLadder ? (goal.y < pos.y - 1.5f) : (goal.y > pos.y + 1.5f));
		bool flag2 = s.DescendLadder && s.Tag == tag && (flag || now < s.DescendUntil) && goal.y < pos.y - 1f;
		bool num3 = s.OnChain && s.Tag == tag && (s.ChainDown ? (goal.y < pos.y - 1f) : (goal.y > pos.y + 1f));
		float descendUntil = s.DescendUntil;
		Vector3 descendTop = s.DescendTop;
		float climbUntil = s.ClimbUntil;
		float climbBestY = s.ClimbBestY;
		float climbProgressAt = s.ClimbProgressAt;
		float climbStartY = s.ClimbStartY;
		float climbStartAt = s.ClimbStartAt;
		float climbBottomY = s.ClimbBottomY;
		float detourUntil = s.DetourUntil;
		Vector3 climbTop = s.ClimbTop;
		Vector3 climbDir = s.ClimbDir;
		Vector3 climbBase = s.ClimbBase;
		bool climbAuto = s.ClimbAuto;
		int climbLadderId = s.ClimbLadderId;
		Ladder useLadder = s.UseLadder;
		float ladderUntil = s.LadderUntil;
		s.Goal = goal;
		s.Dest = goal;
		s.Tag = tag;
		s.Path.Clear();
		s.Corner = 0;
		s.NextPlan = 0f;
		s.PathOk = false;
		s.Partial = false;
		s.Best = float.MaxValue;
		s.ProgressTime = now;
		s.AnchorTime = now;
		s.Anchor = pos;
		s.Ladder = 0;
		s.StepUntil = 0f;
		s.HasDetour = false;
		s.GaveUp = false;
		s.DoorBlocked = false;
		s.RouteDoor = null;
		s.WaitStamina = false;
		s.PassDoor = null;
		s.TooTired = false;
		s.MainBest = float.MaxValue;
		s.Passes = 0;
		s.HasShore = false;
		s.NextShore = 0f;
		s.NextDoorSearch = 0f;
		s.NextLog = now + 1f;
		s.NextStartSearch = 0f;
		s.ClimbUntil = 0f;
		s.ClimbTries = 0;
		s.ClimbTried.Clear();
		s.ClimbRestUntil = 0f;
		s.Direct = false;
		s.UseLadder = null;
		s.LadderUntil = 0f;
		s.NextLadderSearch = 0f;
		s.ClimbLadder = false;
		s.NextLadderClimbSearch = 0f;
		s.LadderClimbsTried.Clear();
		s.HopUpTries = 0;
		s.LadderPending = false;
		if (!num3)
		{
			s.OnChain = false;
		}
		if (!flag)
		{
			s.PendingChain = null;
		}
		if (num2 | flag)
		{
			s.ClimbTop = climbTop;
			s.ClimbDir = climbDir;
			s.ClimbBase = climbBase;
			s.ClimbAuto = climbAuto;
			s.ClimbLadderId = climbLadderId;
			s.ClimbStartY = climbStartY;
			s.ClimbStartAt = climbStartAt;
			s.ClimbBottomY = climbBottomY;
		}
		if (num2)
		{
			s.ClimbLadder = true;
			s.ClimbUntil = climbUntil;
			s.ClimbBestY = climbBestY;
			s.ClimbProgressAt = climbProgressAt;
		}
		s.DescendLadder = false;
		s.DescendUntil = 0f;
		if (flag2)
		{
			s.DescendLadder = true;
			s.DescendUntil = descendUntil;
			s.DescendTop = descendTop;
			s.ClimbBase = climbBase;
			s.ClimbDir = climbDir;
			s.ClimbLadderId = climbLadderId;
		}
		if (flag)
		{
			s.LadderPending = true;
			s.HasDetour = true;
			s.Detour = (s.DescendLadder ? descendTop : climbBase);
			s.DetourUntil = detourUntil;
		}
		if (num)
		{
			s.UseLadder = useLadder;
			s.LadderUntil = ladderUntil;
			s.HasDetour = true;
			s.Detour = useLadder.transform.position;
			s.DetourUntil = ladderUntil;
		}
		else
		{
			s.LaddersUsed.Clear();
		}
	}

	private static bool TryLadder(CompanionAI ai, S s, Vector3 pos, float now, string tag)
	{
		s.NextLadderSearch = now + 2f;
		if (s.UseLadder != null || s.Goal.y < pos.y + 1.5f)
		{
			return false;
		}
		Ladder ladder = null;
		float num = float.MaxValue;
		float num2 = Vector3.Distance(pos, s.Goal);
		Ladder[] array = UnityEngine.Object.FindObjectsByType<Ladder>(FindObjectsSortMode.None);
		foreach (Ladder ladder2 in array)
		{
			if (ladder2 == null || ladder2.m_targetPos == null || s.LaddersUsed.Contains(ladder2))
			{
				continue;
			}
			Vector3 position = ladder2.transform.position;
			Vector3 position2 = ladder2.m_targetPos.position;
			if (Flat(pos, position) > 25f || Mathf.Abs(position.y - pos.y) > 3f || position2.y < pos.y + 1.5f)
			{
				continue;
			}
			float num3 = Vector3.Distance(position2, s.Goal);
			if (!(num3 > num2 - 2f))
			{
				float num4 = Vector3.Distance(pos, position) + num3;
				if (num4 < num)
				{
					num = num4;
					ladder = ladder2;
				}
			}
		}
		if (ladder == null)
		{
			return false;
		}
		s.UseLadder = ladder;
		s.LadderUntil = now + 20f;
		s.HasDetour = true;
		s.Detour = ladder.transform.position;
		s.DetourUntil = s.LadderUntil;
		s.NextPlan = 0f;
		s.StepUntil = 0f;
		s.ClimbUntil = 0f;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (s.Goal.y - pos.y).ToString("F1") + "m below where it needs to be - going up the ladder at " + Fmt(ladder.transform.position));
		}
		return true;
	}

	private static bool UseLadderNow(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		Ladder useLadder = s.UseLadder;
		if (useLadder == null || now > s.LadderUntil)
		{
			if ((s.UseLadder != null || (object)s.UseLadder != null) && CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] couldn't get to the ladder");
			}
			s.UseLadder = null;
			return false;
		}
		if (Vector3.Distance(pos, useLadder.transform.position) >= useLadder.m_useDistance - 0.1f)
		{
			return false;
		}
		Humanoid humanoid = c as Humanoid;
		if (humanoid == null)
		{
			s.UseLadder = null;
			return false;
		}
		useLadder.Interact(humanoid, hold: false, alt: false);
		s.LaddersUsed.Add(useLadder);
		s.UseLadder = null;
		s.HasDetour = false;
		s.NextPlan = 0f;
		s.Path.Clear();
		s.Ladder = 0;
		s.Passes = 0;
		s.MainBest = float.MaxValue;
		s.Best = float.MaxValue;
		s.ProgressTime = now;
		s.AnchorTime = now;
		s.Anchor = ai.transform.position;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] went up the ladder to " + Fmt(ai.transform.position));
		}
		return true;
	}

	private static void Halt(CompanionAI ai, Character c, S s)
	{
		ai.StopMoving();
		if (c != null && s.Walking)
		{
			c.SetWalk(walk: false);
			s.Walking = false;
		}
	}

	private static void Steer(CompanionAI ai, Character c, S s, Vector3 dir, bool run, bool walk)
	{
		if (walk != s.Walking)
		{
			c.SetWalk(walk);
			s.Walking = walk;
		}
		ai.MoveTowards(dir, run && !walk);
	}

	private static void Plan(CompanionAI ai, Character c, S s, Vector3 pos, float now)
	{
		s.NextPlan = now + 1f;
		s.Path.Clear();
		s.Corner = 0;
		Vector3 vector;
		if (s.HasDetour)
		{
			vector = s.Detour;
		}
		else
		{
			vector = ((s.RouteDoor != null) ? s.RouteDoorSpot : s.Goal);
		}
		if (Vector3.Distance(vector, s.Dest) > 1.5f)
		{
			s.NewLeg = true;
			s.ProgressTime = now;
		}
		s.Dest = vector;
		s.RouteSwim = 0f;
		if (!GetPath(pos, vector, s.Path, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: false) || s.Path.Count == 0)
		{
			s.PathOk = false;
			s.Partial = true;
			s.Path.Clear();
			s.Path.Add(vector);
			if (!s.HasDetour && s.RouteDoor == null)
			{
				s.MainPartial = true;
			}
			if (!s.HasDetour && s.RouteDoor == null && !c.IsSwimming() && now >= s.NextStartSearch)
			{
				s.NextStartSearch = now + 2f;
				if (FindRouteStart(pos, vector, out var start))
				{
					s.HasDetour = true;
					s.Detour = start;
					s.DetourUntil = now + 3f;
					s.Path.Clear();
					s.Path.Add(start);
					if (CompanionsPlugin.LogDetailed)
					{
						Dbg("[Move|" + s.Tag + "] no route from " + Fmt(pos) + " - stepping to " + Fmt(start) + " to find one");
					}
				}
			}
		}
		else
		{
			s.PathOk = true;
			Vector3 a = s.Path[s.Path.Count - 1];
			s.Partial = Flat(a, vector) > 1.5f || Mathf.Abs(a.y - vector.y) > 2.2f;
			if (!s.HasDetour && s.RouteDoor == null)
			{
				s.MainPartial = s.Partial;
				if (!s.Partial && c.IsOnGround() && !c.IsSwimming())
				{
					s.GoodPos = pos;
					s.GoodAt = now;
					s.HasGood = true;
				}
			}
			s.TooTired = false;
			if (!c.IsSwimming())
			{
				float num = (s.RouteSwim = MHR_Water.SwimMeters(c, pos, s.Path, 60f));
				if (num > 0.5f)
				{
					s_tmp.Clear();
					float num2 = PathLength(pos, s.Path);
					if (GetPath(pos, vector, s_tmp, Pathfinding.AgentType.HumanoidAvoidWater, requireFull: true))
					{
						float num3 = PathLength(pos, s_tmp);
						float num4 = ((ModConfig.DryDetour != null) ? ModConfig.DryDetour.Value : 30f);
						if (num3 <= num2 * 1.5f + num4)
						{
							s.Path.Clear();
							s.Path.AddRange(s_tmp);
							s.Partial = false;
							num = 0f;
							s.RouteSwim = 0f;
							if (now >= s.NextLog && CompanionsPlugin.LogDetailed)
							{
								Dbg("[Move|" + s.Tag + "] going round the water (" + num3.ToString("F0") + "m) instead of swimming (" + num2.ToString("F0") + "m)");
							}
						}
						else if (now >= s.NextLog && CompanionsPlugin.LogDetailed)
						{
							Dbg("[Move|" + s.Tag + "] swimming: the dry way is " + num3.ToString("F0") + "m against " + num2.ToString("F0") + "m");
						}
					}
				}
				if (num > 0.5f)
				{
					float num5 = MHR_Water.SwimStamina(c, num) * 1.25f + 5f;
					float num6 = MaxStamina(ai);
					if (num6 > 0f)
					{
						num5 = Mathf.Min(num5, num6 * 0.9f);
					}
					if (now >= s.NoWaitUntil && Stamina(ai) < num5)
					{
						s_tmp.Clear();
						if (GetPath(pos, vector, s_tmp, Pathfinding.AgentType.HumanoidAvoidWater, requireFull: true))
						{
							s.Path.Clear();
							s.Path.AddRange(s_tmp);
							s.Partial = false;
							if (now >= s.NextLog && CompanionsPlugin.LogDetailed)
							{
								Dbg("[Move|" + s.Tag + "] too tired to swim " + num.ToString("F0") + "m - going around the water");
							}
						}
						else
						{
							s.TooTired = true;
							s.NeedStamina = num5;
						}
					}
				}
			}
		}
		bool direct = s.Direct;
		s.Direct = false;
		if (!s.HasDetour && s.RouteDoor == null && !c.IsSwimming() && now >= s.NoDirectUntil)
		{
			float num7 = Vector3.Distance(pos, vector);
			float num8 = Remaining(s, pos);
			int hops = 0;
			s_hopWhy = null;
			bool flag = s.Partial || num8 > num7 * 1.6f + 6f;
			bool flag2 = num7 < 25f && (flag || num8 > num7 + 2f) && HopLine(pos, vector, out hops, c);
			if (((!flag2 && num7 < 25f) & flag) && s_hopWhy != null && now >= s.NextLineLog)
			{
				s.NextLineLog = now + 15f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + s.Tag + "] taking the " + num8.ToString("F0") + "m way round, not straight (" + num7.ToString("F0") + "m): " + s_hopWhy);
				}
			}
			if (flag2 && (flag || hops > 0))
			{
				if (!direct && CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + s.Tag + "] going straight to " + Fmt(vector) + " (" + hops + " step(s) to hop) instead of " + (s.Partial ? "no route" : ("a " + num8.ToString("F0") + "m way round")));
				}
				s.Path.Clear();
				s.Path.Add(vector);
				s.Corner = 0;
				s.PathOk = true;
				s.Partial = false;
				s.MainPartial = false;
				s.Direct = true;
				s.RouteSwim = 0f;
			}
		}
		if (s.Partial && s.Tag != "Follow" && s.Tag != "Combat" && s.RouteDoor == null && !s.HasDetour && now >= s.NextDoorSearch)
		{
			s.NextDoorSearch = now + 4f;
			Door skip = ((now - s.LastRouteDoorAt < 30f) ? s.LastRouteDoor : null);
			Door door = RouteDoor(pos, vector, out var spot, skip);
			Vector3 bridge;
			if (door != null)
			{
				s.RouteDoor = door;
				s.RouteDoorSpot = spot;
				s.NextPlan = 0f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + s.Tag + "] no way through - heading for the closed " + door.m_name + " at " + Fmt(door.transform.position));
				}
			}
			else if (!c.IsSwimming() && FindHopBridge(pos, vector, out bridge) && Flat(pos, bridge) > 1f)
			{
				s.HasDetour = true;
				s.Detour = bridge;
				s.DetourUntil = now + 10f + Flat(pos, bridge) * 0.5f;
				s.NextPlan = 0f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + s.Tag + "] no route to " + Fmt(vector) + " - going to " + Fmt(bridge) + " to hop the rest");
				}
			}
		}
		AvoidFire(s, pos, vector, now);
		AvoidLava(s, pos, vector, now);
	}

	private static void AvoidLava(S s, Vector3 pos, Vector3 goal, float now)
	{
		if (s.Path.Count == 0 || !WorldGenerator.IsAshlands(pos.x, pos.z) || Lava(goal))
		{
			return;
		}
		try
		{
			Vector3 vector = pos;
			for (int i = 0; i < s.Path.Count && i < 40; i++)
			{
				Vector3 vector2 = s.Path[i];
				Vector3 vector3 = vector2 - vector;
				vector3.y = 0f;
				float magnitude = vector3.magnitude;
				if (magnitude > 0.05f)
				{
					Vector3 vector4 = vector3 / magnitude;
					for (float num = 0.3f; num < magnitude; num += 0.5f)
					{
						Vector3 vector5 = vector + vector4 * num;
						if (!Lava(vector5))
						{
							continue;
						}
						if (LavaRing(vector5, vector, vector2, i == 0, out var way))
						{
							s.Path.InsertRange(i, way);
							if (now >= s.NextFireLog)
							{
								s.NextFireLog = now + 10f;
								if (CompanionsPlugin.LogNormal)
								{
									CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] going round the lava at " + Fmt(vector5) + " by " + way.Count + " spots");
								}
							}
						}
						else if (s.Tag != "Follow")
						{
							s.GaveUp = true;
							s.GaveUpAt = now;
							if (CompanionsPlugin.LogNormal)
							{
								CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] the only way to " + Fmt(goal) + " is over lava at " + Fmt(vector5) + " - not going");
							}
						}
						else if (now >= s.NextFireLog)
						{
							s.NextFireLog = now + 10f;
							if (CompanionsPlugin.LogNormal)
							{
								CompanionsPlugin.Log.LogInfo("[Move|" + s.Tag + "] the route crosses lava at " + Fmt(vector5) + " and there's no clear way round - keeping to its edge");
							}
						}
						return;
					}
				}
				vector = vector2;
			}
		}
		catch
		{
		}
	}

	private static bool LavaRing(Vector3 c, Vector3 from, Vector3 next, bool fromIsHere, out List<Vector3> way)
	{
		way = null;
		float[] array = new float[3] { 3f, 6f, 10f };
		int num = 16 * array.Length;
		Vector3[] pt = new Vector3[num];
		bool[] array2 = new bool[num];
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				float f = (float)j * Mathf.PI * 2f / 16f;
				Vector3 p = c + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * array[i];
				int num2 = i * 16 + j;
				array2[num2] = GroundAt(p, from.y, out var ground) && !Lava(ground) && MHR_Water.DepthAt(ground) < 0.5f;
				pt[num2] = ground;
			}
		}
		int S0 = num;
		int T = num + 1;
		float[] array3 = new float[num + 2];
		int[] array4 = new int[num + 2];
		bool[] array5 = new bool[num + 2];
		for (int k = 0; k < array3.Length; k++)
		{
			array3[k] = float.MaxValue;
			array4[k] = -1;
		}
		array3[S0] = 0f;
		for (int l = 0; l < num + 2; l++)
		{
			int num3 = -1;
			float num4 = float.MaxValue;
			for (int m = 0; m < array3.Length; m++)
			{
				if (!array5[m] && array3[m] < num4)
				{
					num4 = array3[m];
					num3 = m;
				}
			}
			if (num3 < 0 || num3 == T)
			{
				break;
			}
			array5[num3] = true;
			for (int n = 0; n < num + 2; n++)
			{
				if (array5[n] || n == S0 || (n < num && !array2[n]))
				{
					continue;
				}
				if (num3 < num && n < num)
				{
					int num5 = Mathf.Abs(num3 % 16 - n % 16);
					num5 = Mathf.Min(num5, 16 - num5);
					if (num5 > 1)
					{
						continue;
					}
				}
				if (num3 != S0 || n != T)
				{
					Vector3 vector = P(num3);
					Vector3 vector2 = P(n);
					float num6 = Vector3.Distance(vector, vector2);
					if (!(array3[num3] + num6 >= array3[n]) && CanSee(vector, vector2) && LegClearOfLava(vector, vector2, ((num3 == S0) & fromIsHere) ? 0.6f : 0.3f))
					{
						array3[n] = array3[num3] + num6;
						array4[n] = num3;
					}
				}
			}
		}
		if (array4[T] < 0)
		{
			return false;
		}
		way = new List<Vector3>();
		int num7 = array4[T];
		while (num7 >= 0 && num7 != S0)
		{
			way.Insert(0, pt[num7]);
			num7 = array4[num7];
		}
		return way.Count > 0;
		Vector3 P(int num8)
		{
			if (num8 != S0)
			{
				if (num8 != T)
				{
					return pt[num8];
				}
				return next;
			}
			return from;
		}
	}

	private static void AvoidFire(S s, Vector3 pos, Vector3 goal, float now)
	{
		if (s.Path.Count == 0 || InFire(goal) || InFire(pos))
		{
			return;
		}
		try
		{
			Vector3 vector = pos;
			for (int i = 0; i < s.Path.Count && i < 40; i++)
			{
				Vector3 vector2 = s.Path[i];
				Vector3 vector3 = vector2 - vector;
				vector3.y = 0f;
				float magnitude = vector3.magnitude;
				if (magnitude > 0.05f)
				{
					Vector3 vector4 = vector3 / magnitude;
					for (float num = 0.2f; num < magnitude; num += 0.4f)
					{
						Vector3 p = vector + vector4 * num;
						p.y = Mathf.Lerp(vector.y, vector2.y, num / magnitude);
						EffectArea effectArea = FireArea(p, 0.5f);
						if (effectArea == null)
						{
							continue;
						}
						List<Vector3> way;
						if (FireBypass(effectArea, vector, vector4, vector2, out var a, out var b))
						{
							s.Path.Insert(i, b);
							s.Path.Insert(i, a);
							if (now >= s.NextFireLog)
							{
								s.NextFireLog = now + 10f;
								if (CompanionsPlugin.LogDetailed)
								{
									Dbg("[Move|" + s.Tag + "] going round the fire at " + Fmt(effectArea.transform.position) + " via " + Fmt(a) + " and " + Fmt(b));
								}
							}
						}
						else if (FireRing(effectArea, vector, vector2, i == 0, out way))
						{
							s.Path.InsertRange(i, way);
							if (now >= s.NextFireLog)
							{
								s.NextFireLog = now + 10f;
								if (CompanionsPlugin.LogDetailed)
								{
									Dbg("[Move|" + s.Tag + "] going round the fire at " + Fmt(effectArea.transform.position) + " by " + way.Count + " spots round it");
								}
							}
						}
						else if (s.Tag != "Follow")
						{
							s.GaveUp = true;
							s.GaveUpAt = now;
							if (CompanionsPlugin.LogDetailed)
							{
								Dbg("[Move|" + s.Tag + "] the only way to " + Fmt(goal) + " is through the fire at " + Fmt(effectArea.transform.position) + " - not going");
							}
						}
						else if (now >= s.NextFireLog)
						{
							s.NextFireLog = now + 10f;
							if (CompanionsPlugin.LogDetailed)
							{
								Dbg("[Move|" + s.Tag + "] the route passes the fire at " + Fmt(effectArea.transform.position) + " and there's no clear way round it - steering round step by step");
							}
						}
						return;
					}
				}
				vector = vector2;
			}
		}
		catch
		{
		}
	}

	private static bool FireBypass(EffectArea fire, Vector3 from, Vector3 dir, Vector3 next, out Vector3 a, out Vector3 b)
	{
		a = (b = Vector3.zero);
		float num = FireReach(fire, out var c);
		Vector3 vector = new Vector3(0f - dir.z, 0f, dir.x);
		for (float num2 = 0.8f; num2 <= 3.3f; num2 += 0.5f)
		{
			float num3 = num + num2;
			float num4 = float.MaxValue;
			bool flag = false;
			for (int i = -1; i <= 1; i += 2)
			{
				Vector3 p = c + vector * ((float)i * num3) - dir * num3 * 0.75f;
				Vector3 p2 = c + vector * ((float)i * num3) + dir * num3 * 0.75f;
				if (GroundAt(p, from.y, out var ground) && GroundAt(p2, from.y, out var ground2) && !NearFire(ground, 0.5f) && !NearFire(ground2, 0.5f) && CanSee(from, ground) && CanSee(ground, ground2) && LegClearOfFire(from, ground) && LegClearOfFire(ground, ground2) && LegClearOfFire(ground2, next))
				{
					float num5 = Vector3.Distance(from, ground);
					if (num5 < num4)
					{
						num4 = num5;
						a = ground;
						b = ground2;
						flag = true;
					}
				}
			}
			if (flag)
			{
				return true;
			}
		}
		return false;
	}

	private static float FireReach(EffectArea fire, out Vector3 c)
	{
		Collider component = fire.GetComponent<Collider>();
		c = ((component != null) ? component.bounds.center : fire.transform.position);
		float a = ((component != null) ? Mathf.Max(component.bounds.extents.x, component.bounds.extents.z) : 2f);
		Piece componentInParent = fire.GetComponentInParent<Piece>();
		if (componentInParent != null)
		{
			Collider[] componentsInChildren = componentInParent.GetComponentsInChildren<Collider>();
			foreach (Collider collider in componentsInChildren)
			{
				if (!(collider == null))
				{
					Vector3 vector = collider.bounds.center - c;
					vector.y = 0f;
					a = Mathf.Max(a, vector.magnitude + Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z));
				}
			}
		}
		return Mathf.Min(a, 4f);
	}

	private static bool FireRing(EffectArea fire, Vector3 from, Vector3 next, bool fromIsHere, out List<Vector3> way)
	{
		way = null;
		float num = FireReach(fire, out var c);
		float[] array = new float[2]
		{
			num + 0.9f,
			num + 1.7f
		};
		int num2 = 12 * array.Length;
		Vector3[] pt = new Vector3[num2];
		bool[] array2 = new bool[num2];
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < 12; j++)
			{
				float f = (float)j * Mathf.PI * 2f / 12f;
				Vector3 p = c + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * array[i];
				int num3 = i * 12 + j;
				array2[num3] = GroundAt(p, from.y, out var ground) && !NearFire(ground, 0.5f);
				pt[num3] = ground;
			}
		}
		int S0 = num2;
		int T = num2 + 1;
		float[] array3 = new float[num2 + 2];
		int[] array4 = new int[num2 + 2];
		bool[] array5 = new bool[num2 + 2];
		for (int k = 0; k < array3.Length; k++)
		{
			array3[k] = float.MaxValue;
			array4[k] = -1;
		}
		array3[S0] = 0f;
		for (int l = 0; l < num2 + 2; l++)
		{
			int num4 = -1;
			float num5 = float.MaxValue;
			for (int m = 0; m < array3.Length; m++)
			{
				if (!array5[m] && array3[m] < num5)
				{
					num5 = array3[m];
					num4 = m;
				}
			}
			if (num4 < 0 || num4 == T)
			{
				break;
			}
			array5[num4] = true;
			for (int n = 0; n < num2 + 2; n++)
			{
				if (array5[n] || n == S0 || (n < num2 && !array2[n]))
				{
					continue;
				}
				if (num4 < num2 && n < num2)
				{
					int num6 = Mathf.Abs(num4 % 12 - n % 12);
					num6 = Mathf.Min(num6, 12 - num6);
					if (num6 > 1)
					{
						continue;
					}
				}
				if (num4 != S0 || n != T)
				{
					Vector3 vector = P(num4);
					Vector3 vector2 = P(n);
					float num7 = Vector3.Distance(vector, vector2);
					if (!(array3[num4] + num7 >= array3[n]) && CanSee(vector, vector2) && LegClearOfFire(vector, vector2, ((num4 == S0) & fromIsHere) ? 0.6f : 0.2f))
					{
						array3[n] = array3[num4] + num7;
						array4[n] = num4;
					}
				}
			}
		}
		if (array4[T] < 0)
		{
			return false;
		}
		way = new List<Vector3>();
		int num8 = array4[T];
		while (num8 >= 0 && num8 != S0)
		{
			way.Insert(0, pt[num8]);
			num8 = array4[num8];
		}
		return way.Count > 0;
		Vector3 P(int num9)
		{
			if (num9 != S0)
			{
				if (num9 != T)
				{
					return pt[num9];
				}
				return next;
			}
			return from;
		}
	}

	private static bool LegClearOfFire(Vector3 p0, Vector3 p1, float skipStart = 0.2f)
	{
		Vector3 vector = p1 - p0;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 0.05f)
		{
			return FireArea(p0, 0.5f) == null;
		}
		Vector3 vector2 = vector / magnitude;
		for (float num = skipStart; num < magnitude; num += 0.4f)
		{
			Vector3 p2 = p0 + vector2 * num;
			p2.y = Mathf.Lerp(p0.y, p1.y, num / magnitude);
			if (FireArea(p2, 0.5f) != null)
			{
				return false;
			}
		}
		return FireArea(p1, 0.5f) == null;
	}

	private static bool GroundAt(Vector3 p, float nearY, out Vector3 ground)
	{
		ground = p;
		if (!Physics.Raycast(new Vector3(p.x, nearY + 1.5f, p.z), Vector3.down, out var hitInfo, 4f, Mask()))
		{
			return false;
		}
		if (Mathf.Abs(hitInfo.point.y - nearY) > 1.2f)
		{
			return false;
		}
		ground = hitInfo.point;
		return true;
	}

	private static float PathLength(Vector3 from, List<Vector3> path)
	{
		float num = 0f;
		Vector3 a = from;
		for (int i = 0; i < path.Count; i++)
		{
			num += Vector3.Distance(a, path[i]);
			a = path[i];
		}
		return num;
	}

	public static bool WalkRoute(Vector3 from, Vector3 to, out float len)
	{
		len = 0f;
		if (!GetPath(from, to, s_walkPath, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: false))
		{
			return false;
		}
		Vector3 a = s_walkPath[s_walkPath.Count - 1];
		if (Flat(a, to) > 1.5f || Mathf.Abs(a.y - to.y) > 2.2f)
		{
			return false;
		}
		len = PathLength(from, s_walkPath);
		return true;
	}

	private static bool GetPath(Vector3 from, Vector3 to, List<Vector3> path, Pathfinding.AgentType agent, bool requireFull)
	{
		path.Clear();
		if (Pathfinding.instance != null && Pathfinding.instance.GetPath(from, to, path, agent, requireFull))
		{
			return path.Count > 0;
		}
		return false;
	}

	private static void AdvanceCorner(S s, Vector3 pos)
	{
		while (s.Corner < s.Path.Count - 1)
		{
			Vector3 b = s.Path[s.Corner];
			if (Flat(pos, b) < 0.6f && (s_swimming || Mathf.Abs(pos.y - b.y) < 1.6f))
			{
				s.Corner++;
				continue;
			}
			break;
		}
	}

	private static Vector3 NextCorner(S s, Vector3 pos)
	{
		if (s.Path.Count == 0)
		{
			return s.Dest;
		}
		int num = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1);
		if (num == s.Path.Count - 1 && Flat(pos, s.Path[num]) < 0.8f)
		{
			return s.Dest;
		}
		return s.Path[num];
	}

	private static float Remaining(S s, Vector3 pos)
	{
		if (s.Path.Count == 0)
		{
			return Vector3.Distance(pos, s.Dest);
		}
		float num = 0f;
		Vector3 a = pos;
		for (int i = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1); i < s.Path.Count; i++)
		{
			num += Vector3.Distance(a, s.Path[i]);
			a = s.Path[i];
		}
		if (s.Partial)
		{
			num += Vector3.Distance(a, s.Dest);
		}
		return num;
	}

	private static void Unstick(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 target, float now, string tag, float still, float noCloser)
	{
		string text = ((still >= 2.5f) ? ("standing still " + still.ToString("F0") + "s") : ("no closer for " + noCloser.ToString("F0") + "s"));
		s.AnchorTime = now;
		s.Anchor = pos;
		s.ProgressTime = now - 4f;
		if (s.Direct)
		{
			s.Direct = false;
			s.NoDirectUntil = now + 20f;
			s.NextPlan = 0f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] couldn't get through going straight at " + Fmt(pos) + " - taking the route");
			}
			return;
		}
		int num = s.Ladder++;
		Vector3 dir = target - pos;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.01f)
		{
			dir = ai.transform.forward;
		}
		dir.Normalize();
		if (num == 0 && TryLadder(ai, s, pos, now, tag))
		{
			s.Ladder = 0;
			return;
		}
		if (num == 0 && TryStartLadderClimb(ai, c, s, pos, now, tag))
		{
			s.Ladder = 0;
			return;
		}
		if (num == 0 && TryHopUp(ai, c, s, pos, now, tag))
		{
			s.Ladder = 0;
			return;
		}
		if (num == 0 && TryLadderDown(ai, c, s, pos, now, tag))
		{
			s.Ladder = 0;
			return;
		}
		if (num == 0 && TryDropDown(ai, c, s, pos, now, tag))
		{
			s.Ladder = 0;
			return;
		}
		if (num == 0 && TryStartClimb(ai, c, s, pos, now, tag, text))
		{
			s.Ladder = 0;
			return;
		}
		switch (num)
		{
		case 0:
		{
			Door door = DoorNear(pos, 2.6f);
			if (door != null)
			{
				OpenDoor(ai, c, door, tag);
				s.NextPlan = now + 0.6f;
				return;
			}
			Door door2 = OpenLeafAhead(pos, target) ?? OpenDoorNear(pos, 2.5f);
			if (door2 != null && tag != "Combat")
			{
				CloseDoor(ai, c, door2, tag);
				s.NextPlan = now + 0.6f;
			}
			else
			{
				Sidestep(ai, c, s, pos, dir, now, tag, text, leftFirst: true);
			}
			return;
		}
		case 1:
			Hop(ai, c, s, pos, dir, now, tag, text);
			return;
		case 2:
			Sidestep(ai, c, s, pos, dir, now, tag, text, leftFirst: false);
			return;
		case 3:
			Hop(ai, c, s, pos, dir, now, tag, text);
			return;
		case 4:
		{
			Vector3 det;
			if (++s.Passes > 2)
			{
				s.Ladder = 99;
				Unstick(ai, c, s, pos, target, now, tag, still, noCloser);
			}
			else if (FindDetour(pos, s.Goal, dir, out det))
			{
				s.HasDetour = true;
				s.Detour = det;
				s.DetourUntil = now + 6f;
				s.NextPlan = 0f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] stuck (" + text + ") at " + Fmt(pos) + " - trying another way via " + Fmt(det));
				}
			}
			else
			{
				Hop(ai, c, s, pos, dir, now, tag, text);
			}
			return;
		}
		case 5:
			Hop(ai, c, s, pos, dir, now, tag, text);
			s.Ladder = ((s.Passes >= 2) ? 6 : 0);
			return;
		}
		s.GaveUp = true;
		s.GaveUpAt = now;
		Halt(ai, c, s);
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get to " + Fmt(s.Goal) + " from " + Fmt(pos) + " - giving up");
		}
		int num2;
		switch (tag)
		{
		default:
			num2 = ((tag == "Hunt") ? 1 : 0);
			break;
		case "Follow":
		case "Command":
		case "Combat":
			num2 = 1;
			break;
		}
		bool speak = (byte)num2 != 0;
		CompanionStatus.Why(ai, (s.Goal.y - pos.y > 2f) ? "hc_why_cant_reach_up" : "hc_why_no_way", speak);
		Rescue(ai, c, s, pos, now, tag);
	}

	private static void Sidestep(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag, string why, bool leftFirst)
	{
		Vector3 vector = -dir;
		Vector3 vector2 = Quaternion.Euler(0f, -45f, 0f) * vector;
		Vector3 vector3 = Quaternion.Euler(0f, 45f, 0f) * vector;
		Vector3 vector4 = (leftFirst ? vector2 : vector3);
		Vector3 vector5 = (leftFirst ? vector3 : vector2);
		Vector3 vector6 = Quaternion.Euler(0f, 90f, 0f) * dir;
		if (c.IsSwimming())
		{
			Vector3 stepDir;
			if (Clear(pos, vector4))
			{
				stepDir = vector4;
			}
			else if (Clear(pos, vector5))
			{
				stepDir = vector5;
			}
			else
			{
				stepDir = (Clear(pos, vector6) ? vector6 : (-vector6));
			}
			s.StepDir = stepDir;
			s.StepRun = false;
			s.StepUntil = now + 1.2f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] stuck in the water (" + why + ") at " + Fmt(pos) + " - swimming to the side");
			}
			return;
		}
		if (DropAhead(pos, vector6, 1.5f, 1.2f))
		{
			vector6 = (DropAhead(pos, -vector6, 1.5f, 1.2f) ? Vector3.zero : (-vector6));
		}
		Vector3 stepDir2;
		if (SafeClear(pos, vector4))
		{
			stepDir2 = vector4;
		}
		else if (SafeClear(pos, vector5))
		{
			stepDir2 = vector5;
		}
		else
		{
			stepDir2 = (SafeClear(pos, vector) ? vector : vector6);
		}
		s.StepDir = stepDir2;
		s.StepRun = false;
		s.StepUntil = now + 1.2f;
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg("[Move|" + tag + "] stuck (" + why + ") at " + Fmt(pos) + " - stepping back to the side");
		}
	}

	public static bool Climbing(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && Time.time < s.ClimbUntil)
		{
			return Time.time - s.LastGo < 0.5f;
		}
		return false;
	}

	private static void NoteLadderClimb(S s, float now, string tag)
	{
		if (!s.LadderUses.TryGetValue(s.ClimbLadderId, out var value) || now - value.y > 120f)
		{
			value = new Vector2(0f, now);
		}
		value.x++;
		s.LadderUses[s.ClimbLadderId] = value;
		if (value.x >= 4f)
		{
			MarkBadLadder(s.ClimbLadderId, now, 180f);
			s.LadderUses.Remove(s.ClimbLadderId);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] this ladder keeps ending up back where it started - leaving it for a few minutes");
			}
		}
	}

	private static void MarkBadLadder(int id, float now, float secs)
	{
		if (id != 0)
		{
			s_badLadders[id] = now + secs;
		}
	}

	private static bool BadLadder(int id, float now)
	{
		if (s_badLadders.TryGetValue(id, out var value))
		{
			return now < value;
		}
		return false;
	}

	private static GameObject LadderRoot(Collider col, out Piece piece)
	{
		piece = col.GetComponentInParent<Piece>();
		if (piece != null)
		{
			return piece.gameObject;
		}
		Transform transform = col.transform;
		int num = 0;
		while (num < 4 && transform != null)
		{
			if (transform.name.ToLowerInvariant().Contains("ladder") || !(transform.GetComponentInChildren<AutoJumpLedge>(includeInactive: true) == null))
			{
				if (!OwnBounds(transform.gameObject, out var b) || b.size.y > 14f || (b.size.x > 4f && b.size.z > 4f))
				{
					return null;
				}
				return transform.gameObject;
			}
			num++;
			transform = transform.parent;
		}
		return null;
	}

	private static bool IsLadder(GameObject root, Piece piece, Collider col)
	{
		if (!(root.name + " " + ((col != null) ? col.gameObject.name : "") + " " + ((piece != null) ? piece.m_name : "")).ToLowerInvariant().Contains("ladder"))
		{
			return root.GetComponentInChildren<AutoJumpLedge>(includeInactive: true) != null;
		}
		return true;
	}

	private static bool OwnBounds(GameObject root, out Bounds b)
	{
		b = default;
		bool flag = false;
		Collider[] componentsInChildren = root.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!(collider == null) && collider.enabled && (!collider.isTrigger || !(collider.GetComponentInParent<AutoJumpLedge>() == null)))
			{
				if (!flag)
				{
					b = collider.bounds;
					flag = true;
				}
				else
				{
					b.Encapsulate(collider.bounds);
				}
			}
		}
		return flag;
	}

	private static Bounds LadderBounds(GameObject root, out int id)
	{
		id = root.GetInstanceID();
		if (!OwnBounds(root, out var b))
		{
			return b;
		}
		HashSet<int> hashSet = new HashSet<int> { id };
		for (int i = 0; i < 2; i++)
		{
			Bounds bounds = b;
			GameObject gameObject = root;
			for (int j = 0; j < 8; j++)
			{
				float y = ((i == 0) ? bounds.max.y : bounds.min.y);
				GameObject gameObject2 = null;
				Bounds bounds2 = default;
				Collider[] array = Physics.OverlapSphere(new Vector3(bounds.center.x, y, bounds.center.z), 1f, Mask(), QueryTriggerInteraction.Collide);
				foreach (Collider collider in array)
				{
					if (collider == null)
					{
						continue;
					}
					GameObject gameObject3 = LadderRoot(collider, out var piece);
					if (gameObject3 == null || hashSet.Contains(gameObject3.GetInstanceID()) || !IsLadder(gameObject3, piece, collider) || !OwnBounds(gameObject3, out var b2) || Flat(b2.center, bounds.center) > 0.8f)
					{
						continue;
					}
					bool num;
					if (i != 0)
					{
						if (!(b2.max.y < bounds.min.y + 1.5f))
						{
							continue;
						}
						num = b2.max.y > bounds.min.y - 0.8f;
					}
					else
					{
						if (!(b2.min.y > bounds.max.y - 1.5f))
						{
							continue;
						}
						num = b2.min.y < bounds.max.y + 0.8f;
					}
					if (num)
					{
						gameObject2 = gameObject3;
						bounds2 = b2;
						break;
					}
				}
				if (gameObject2 == null)
				{
					break;
				}
				hashSet.Add(gameObject2.GetInstanceID());
				b.Encapsulate(bounds2);
				bounds = bounds2;
				gameObject = gameObject2;
			}
			if (i == 1)
			{
				id = gameObject.GetInstanceID();
			}
		}
		return b;
	}

	private static bool LadderFoot(Bounds b, Vector3 f, out Vector3 spot, out bool room)
	{
		float num = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.z) * b.extents.z;
		Vector3 vector = new Vector3(b.center.x, b.min.y, b.center.z);
		spot = vector - f * (num + 0.7f);
		room = true;
		bool flag = false;
		float[] array = new float[4] { 0.7f, 0.4f, 1f, 1.3f };
		RaycastHit hitInfo;
		foreach (float num2 in array)
		{
			if (flag)
			{
				break;
			}
			Vector3 vector2 = vector - f * (num + num2);
			if (Physics.Raycast(new Vector3(vector2.x, b.min.y + 0.6f, vector2.z), Vector3.down, out hitInfo, 1.2f, Mask()))
			{
				spot = new Vector3(vector2.x, hitInfo.point.y, vector2.z);
				flag = true;
			}
		}
		if (!flag)
		{
			if (!Physics.Raycast(spot + Vector3.up * 0.6f, Vector3.down, out hitInfo, 5f, Mask()))
			{
				return false;
			}
			spot.y = hitInfo.point.y;
		}
		for (float num3 = 0f; (num3 <= 0.31f) & room; num3 += 0.3f)
		{
			RaycastHit[] array2 = Physics.RaycastAll(spot + f * num3 + Vector3.up * 0.3f, Vector3.up, 2f, Mask(), QueryTriggerInteraction.Ignore);
			for (int i = 0; i < array2.Length; i++)
			{
				RaycastHit raycastHit = array2[i];
				GameObject gameObject = LadderRoot(raycastHit.collider, out var piece);
				if (!(gameObject != null) || !IsLadder(gameObject, piece, raycastHit.collider))
				{
					room = false;
					break;
				}
			}
		}
		return true;
	}

	private static bool FloorAlong(Vector3 a, Vector3 b)
	{
		Vector3 vector = b - a;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 0.05f)
		{
			return true;
		}
		vector /= magnitude;
		int num = 0;
		for (float num2 = 0.2f; num2 <= magnitude + 0.01f; num2 += 0.2f)
		{
			Vector3 vector2 = a + vector * Mathf.Min(num2, magnitude);
			float num3 = Mathf.Max(a.y, b.y) + 0.6f;
			if (Physics.SphereCast(new Vector3(vector2.x, num3, vector2.z), 0.25f, Vector3.down, out var _, num3 - (Mathf.Min(a.y, b.y) - 1f), Mask()))
			{
				num = 0;
			}
			else if (++num >= 2)
			{
				return false;
			}
		}
		return true;
	}

	private static bool FindFloorVia(Vector3 pos, Vector3 target, out Vector3 via)
	{
		via = Vector3.zero;
		float num = float.MaxValue;
		for (float num2 = 0.6f; num2 <= 1.51f; num2 += 0.45f)
		{
			for (int i = 0; i < 16; i++)
			{
				Vector3 vector = pos + Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward * num2;
				if (Physics.SphereCast(new Vector3(vector.x, pos.y + 1f, vector.z), 0.25f, Vector3.down, out var hitInfo, 1.8f, Mask()) && !(Mathf.Abs(hitInfo.point.y - pos.y) > 0.6f))
				{
					vector.y = hitInfo.point.y;
					float num3 = Flat(pos, vector) + Flat(vector, target);
					if (!(num3 >= num) && FloorAlong(pos, vector) && FloorAlong(vector, target))
					{
						num = num3;
						via = vector;
					}
				}
			}
		}
		return num < float.MaxValue;
	}

	private static LadderStep MakeStep(Bounds b, Vector3 foot, Vector3 dir, int id, GameObject root)
	{
		float num = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
		Vector3 vector = new Vector3(b.center.x, foot.y, b.center.z) + dir * num;
		float num2 = Vector3.Dot(new Vector3(vector.x - foot.x, 0f, vector.z - foot.z), dir);
		if (LiftZoneEdge((root != null) ? root.GetComponentsInChildren<AutoJumpLedge>(includeInactive: true) : null, foot, dir, out var zone))
		{
			num2 -= Vector3.Dot(new Vector3(zone.x - foot.x, 0f, zone.z - foot.z), dir);
			foot = zone;
		}
		return new LadderStep
		{
			Foot = foot,
			Dir = dir,
			Bottom = b.min.y,
			Top = b.max.y,
			Run = Mathf.Max(num2, 0.5f),
			Id = id
		};
	}

	private static List<LadderStep> PlanChain(GameObject first, Bounds firstB, int firstId, Vector3 firstFoot, Vector3 firstDir, float needY)
	{
		List<LadderStep> list = new List<LadderStep> { MakeStep(firstB, firstFoot, firstDir, firstId, first) };
		HashSet<int> hashSet = new HashSet<int> { firstId };
		Bounds bounds = firstB;
		Vector3 vector = firstDir;
		for (int i = 0; i < 12; i++)
		{
			if (!(bounds.max.y < needY - 0.8f))
			{
				break;
			}
			float num = Mathf.Abs(vector.x) * bounds.extents.x + Mathf.Abs(vector.z) * bounds.extents.z;
			Vector3 vector2 = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + vector * num;
			LadderStep ladderStep = null;
			Bounds bounds2 = default;
			float num2 = float.MaxValue;
			HashSet<int> hashSet2 = new HashSet<int>();
			Collider[] array = Physics.OverlapSphere(vector2, 5f, Mask());
			foreach (Collider collider in array)
			{
				if (collider == null || collider.isTrigger)
				{
					continue;
				}
				GameObject gameObject = LadderRoot(collider, out var piece);
				if (gameObject == null || !hashSet2.Add(gameObject.GetInstanceID()) || !IsLadder(gameObject, piece, collider))
				{
					continue;
				}
				AutoJumpLedge componentInChildren = gameObject.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
				if (componentInChildren == null)
				{
					continue;
				}
				Bounds bounds3 = LadderBounds(gameObject, out var id);
				if (hashSet.Contains(id) || bounds3.max.y < bounds.max.y + 1f)
				{
					continue;
				}
				Vector3 forward = componentInChildren.transform.forward;
				forward.y = 0f;
				if (forward.sqrMagnitude < 0.01f)
				{
					continue;
				}
				forward.Normalize();
				if (LadderFoot(bounds3, forward, out var spot, out var _) && !(Mathf.Abs(spot.y - bounds.max.y) > 1f) && !(Flat(spot, vector2) > 4.5f))
				{
					float num3 = Flat(spot, vector2);
					if (num3 < num2)
					{
						num2 = num3;
						ladderStep = MakeStep(bounds3, spot, forward, id, gameObject);
						bounds2 = bounds3;
					}
				}
			}
			if (ladderStep == null)
			{
				break;
			}
			list.Add(ladderStep);
			hashSet.Add(ladderStep.Id);
			bounds = bounds2;
			vector = ladderStep.Dir;
		}
		return list;
	}

	private static bool HeadRoom(Vector3 spot, float height)
	{
		RaycastHit[] array = Physics.RaycastAll(spot + Vector3.up * 0.3f, Vector3.up, height - 0.3f, Mask(), QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			RaycastHit raycastHit = array[i];
			GameObject gameObject = LadderRoot(raycastHit.collider, out var piece);
			if (!(gameObject != null) || !IsLadder(gameObject, piece, raycastHit.collider))
			{
				return false;
			}
		}
		return true;
	}

	private static bool LiftZoneEdge(AutoJumpLedge[] ledges, Vector3 foot, Vector3 dir, out Vector3 zone)
	{
		zone = foot;
		if (ledges == null)
		{
			return false;
		}
		Vector3 vector = foot + Vector3.up * 0.5f;
		float num = float.MaxValue;
		bool result = false;
		foreach (AutoJumpLedge autoJumpLedge in ledges)
		{
			if (autoJumpLedge == null)
			{
				continue;
			}
			Collider[] componentsInChildren = autoJumpLedge.GetComponentsInChildren<Collider>(includeInactive: true);
			foreach (Collider collider in componentsInChildren)
			{
				if (collider == null || !collider.isTrigger)
				{
					continue;
				}
				Vector3 a = collider.ClosestPoint(vector);
				float num2 = Vector3.Distance(a, vector);
				if (num2 >= num || num2 > 3f)
				{
					continue;
				}
				for (float num3 = -0.2f; num3 <= 1.21f; num3 += 0.2f)
				{
					Vector3 vector2 = new Vector3(a.x, foot.y, a.z) + dir * num3;
					if (Physics.SphereCast(vector2 + Vector3.up * 1f, 0.25f, Vector3.down, out var hitInfo, 2f, Mask()) && !(Mathf.Abs(hitInfo.point.y - foot.y) > 0.6f))
					{
						vector2.y = hitInfo.point.y;
						if (HeadRoom(vector2, 2.2f))
						{
							num = num2;
							zone = vector2;
							result = true;
							break;
						}
					}
				}
			}
		}
		return result;
	}

	private static void StartChain(S s, List<LadderStep> chain, int idx, Vector3 pos, float now, string tag, bool down = false)
	{
		s.Chain = chain;
		s.ChainIdx = idx;
		s.OnChain = true;
		s.ChainDown = down;
		foreach (LadderStep item in chain)
		{
			item.LinedUp = false;
		}
		s.DescendLadder = false;
		s.DescendUntil = 0f;
		s.ClimbLadder = false;
		s.ClimbUntil = 0f;
		s.LadderPending = false;
		s.HasDetour = false;
		s.StepUntil = 0f;
		ChainStepReset(s, pos, now);
		if (down)
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] going down a chain of " + (chain.Count - idx) + " ladders to " + chain[chain.Count - 1].Bottom.ToString("F1") + " (first top " + Fmt(chain[idx].TopSpot) + ")");
			}
		}
		else if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] climbing a chain of " + (chain.Count - idx) + " ladders up to " + chain[chain.Count - 1].Top.ToString("F1") + " (first foot " + Fmt(chain[idx].Foot) + ")");
		}
	}

	private static void ChainStepReset(S s, Vector3 pos, float now)
	{
		s.ChainProgressAt = now;
		s.ChainBestY = pos.y;
		s.ChainJumps = 0;
		s.ChainJumpAt = 0f;
		s.ChainPushAt = 0f;
		s.ChainFootBest = float.MaxValue;
		s.ChainFootAt = now;
		s.ChainViaUntil = 0f;
		s.ChainRestUntil = 0f;
	}

	private static void EndChain(S s, float now)
	{
		s.LadderClimbsTried.Clear();
		s.OnChain = false;
		s.NextPlan = 0f;
		s.Ladder = 0;
		s.Passes = 0;
		s.MainBest = float.MaxValue;
		s.ProgressTime = now;
		s.AnchorTime = now;
		s.NextLadderClimbSearch = 0f;
		s.NextLadderSearch = 0f;
	}

	private static bool UseKnownChain(S s, Vector3 pos, float needY, float now, string tag)
	{
		s_knownChains.RemoveAll((KnownChain k) => now > k.Until);
		foreach (KnownChain s_knownChain in s_knownChains)
		{
			for (int num = 0; num < s_knownChain.Steps.Count; num++)
			{
				LadderStep ladderStep = s_knownChain.Steps[num];
				if (!BadLadder(ladderStep.Id, now) && !(Mathf.Abs(ladderStep.Foot.y - pos.y) > 1f) && !(Flat(pos, ladderStep.Foot) > 3f) && !(s_knownChain.Steps[s_knownChain.Steps.Count - 1].Top < pos.y + 1.5f))
				{
					StartChain(s, s_knownChain.Steps, num, pos, now, tag);
					return true;
				}
			}
		}
		return false;
	}

	private static void RememberChain(List<LadderStep> chain, float now)
	{
		foreach (KnownChain s_knownChain in s_knownChains)
		{
			if (s_knownChain.Steps == chain)
			{
				s_knownChain.Until = now + 900f;
				return;
			}
		}
		s_knownChains.Add(new KnownChain
		{
			Steps = chain,
			Until = now + 900f
		});
		if (s_knownChains.Count > 20)
		{
			s_knownChains.RemoveAt(0);
		}
	}

	private static void RunChain(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		if (s.Chain == null || s.ChainIdx >= s.Chain.Count)
		{
			EndChain(s, now);
			return;
		}
		if (s.ChainDown)
		{
			RunChainDown(ai, c, s, pos, now, tag);
			return;
		}
		LadderStep ladderStep = s.Chain[s.ChainIdx];
		Vector3 lhs = pos - ladderStep.Foot;
		lhs.y = 0f;
		bool flag = s.ChainFootBest <= 0.6f && pos.y > ladderStep.Top - 1.2f && (Vector3.Dot(lhs, ladderStep.Dir) > ladderStep.Run + 0.3f || LedgeAhead(pos, ladderStep.Dir, 0.6f, out var _));
		if (flag && !c.IsOnGround())
		{
			Halt(ai, c, s);
			return;
		}
		if (c.IsOnGround() && (((pos.y >= ladderStep.Top - 0.3f) | flag) || (pos.y >= ladderStep.Top - 0.8f && now - s.ChainProgressAt > 1.2f)))
		{
			s.ChainIdx++;
			ChainStepReset(s, pos, now);
			if (s.ChainIdx >= s.Chain.Count)
			{
				RememberChain(s.Chain, now);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] up the ladders at " + Fmt(pos));
				}
				EndChain(s, now);
				if (!LedgeAhead(pos, ladderStep.Dir, 0.7f, out var _))
				{
					s.StepDir = ladderStep.Dir;
					s.StepRun = false;
					s.StepUntil = now + 0.5f;
				}
				return;
			}
			ladderStep = s.Chain[s.ChainIdx];
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] next ladder (" + (s.ChainIdx + 1) + "/" + s.Chain.Count + "), foot at " + Fmt(ladderStep.Foot));
			}
		}
		if (c.IsOnGround() && pos.y < ladderStep.Foot.y - 1f)
		{
			for (int i = 0; i < s.Chain.Count; i++)
			{
				if (Mathf.Abs(s.Chain[i].Foot.y - pos.y) < 1f && Flat(pos, s.Chain[i].Foot) < 6f)
				{
					s.ChainIdx = i;
					ChainStepReset(s, pos, now);
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off the ladders - back on at ladder " + (i + 1) + "/" + s.Chain.Count);
					}
					return;
				}
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off the ladders (now at " + Fmt(pos) + ") - looking again");
			}
			EndChain(s, now);
			return;
		}
		if (pos.y > s.ChainBestY + 0.1f)
		{
			s.ChainBestY = pos.y;
			s.ChainProgressAt = now;
			s.ChainJumps = 0;
		}
		float num = Flat(pos, ladderStep.Foot);
		if (pos.y < ladderStep.Foot.y + 1f && num < s.ChainFootBest - 0.15f)
		{
			s.ChainFootBest = num;
			s.ChainFootAt = now;
			s.ChainProgressAt = Mathf.Max(s.ChainProgressAt, now - 2f);
		}
		bool flag2 = (s.ChainFootBest <= 0.6f || (now - s.ChainFootAt > 1f && num < 1.2f)) && pos.y > ladderStep.Foot.y + 0.25f;
		Vector3 vector = ladderStep.Dir;
		float num2 = MaxStamina(ai);
		if (!flag2 && num2 > 0f && c.IsOnGround())
		{
			float num3 = Stamina(ai);
			if (now >= s.ChainRestUntil && num3 < num2 * 0.15f)
			{
				s.ChainRestUntil = now + 8f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] getting my breath back on the landing (" + (num3 / num2 * 100f).ToString("F0") + "% stamina)");
				}
			}
			if (now < s.ChainRestUntil)
			{
				if (!(num3 >= num2 * 0.4f))
				{
					Halt(ai, c, s);
					s.ChainProgressAt = now;
					s.ChainFootAt = now;
					return;
				}
				s.ChainRestUntil = 0f;
			}
		}
		if (!flag2)
		{
			bool flag3 = now - s.ChainFootAt > 1f && num < 1.2f;
			if (num > 0.45f && !flag3)
			{
				Vector3 vector2 = ladderStep.Foot;
				Vector3 vector3 = ladderStep.Foot - ladderStep.Dir * 0.8f;
				Vector3 vector4 = ladderStep.Foot - pos;
				vector4.y = 0f;
				if (!ladderStep.LinedUp)
				{
					bool num4 = vector4.sqrMagnitude < 0.01f || Vector3.Dot(vector4.normalized, ladderStep.Dir) > 0.8f;
					bool flag4 = Physics.SphereCast(vector3 + Vector3.up * 1f, 0.25f, Vector3.down, out var hitInfo, 2f, Mask()) && Mathf.Abs(hitInfo.point.y - ladderStep.Foot.y) < 0.6f;
					if (flag4)
					{
						GameObject gameObject = LadderRoot(hitInfo.collider, out var piece);
						if (gameObject != null && IsLadder(gameObject, piece, hitInfo.collider))
						{
							flag4 = false;
						}
					}
					if (num4 || !flag4 || Flat(pos, vector3) < 0.4f)
					{
						ladderStep.LinedUp = true;
					}
					else
					{
						vector2 = vector3;
					}
				}
				Vector3 via;
				if (now < s.ChainViaUntil && Flat(pos, s.ChainVia) > 0.35f)
				{
					vector2 = s.ChainVia;
				}
				else if (!FloorAlong(pos, vector2) && FindFloorVia(pos, vector2, out via))
				{
					s.ChainVia = via;
					s.ChainViaUntil = now + 3f;
					vector2 = via;
				}
				vector = vector2 - pos;
				vector.y = 0f;
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = ladderStep.Dir;
				}
			}
		}
		vector.Normalize();
		if (now - s.ChainProgressAt > 6f)
		{
			MarkBadLadder(ladderStep.Id, now, 120f);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck on ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " at " + Fmt(pos) + " - looking again");
			}
			EndChain(s, now);
			return;
		}
		if (!flag2 && c.IsOnGround() && LedgeAhead(pos, vector, 0.5f, out var drop3))
		{
			Halt(ai, c, s);
			if (now >= s.NextEdgeLog)
			{
				s.NextEdgeLog = now + 5f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] edge ahead on the ladders at " + Fmt(pos + vector * 0.5f) + " (" + drop3.ToString("F1") + "m down) - not stepping off");
				}
			}
			return;
		}
		ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(vector), 540f * Time.deltaTime);
		Steer(ai, c, s, vector, run: false, walk: false);
		if (!((!flag2 && (num <= 0.45f || (now - s.ChainFootAt > 1f && num < 1.2f))) | flag2))
		{
			return;
		}
		if (s.ChainPushAt <= 0f)
		{
			s.ChainPushAt = now;
		}
		float num5 = now - Mathf.Max(s.ChainPushAt, s.ChainProgressAt);
		bool flag5 = num2 > 0f && Stamina(ai) < num2 * 0.2f;
		bool flag6 = LedgeAhead(pos, vector, 0.8f, out var drop4) || LedgeAhead(pos, vector, 1.6f, out drop4);
		float num6 = (flag2 ? 2.5f : 0.8f);
		if (!(num5 > num6) || flag5 || flag6 || !c.IsOnGround() || !(now >= s.ChainJumpAt))
		{
			return;
		}
		if (++s.ChainJumps > 6)
		{
			MarkBadLadder(ladderStep.Id, now, 120f);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get up ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " - looking again");
			}
			EndChain(s, now);
			return;
		}
		c.Jump();
		Rigidbody component = ai.GetComponent<Rigidbody>();
		if (component != null)
		{
			Vector3 linearVelocity = component.linearVelocity;
			linearVelocity.x = vector.x * 2.5f;
			linearVelocity.z = vector.z * 2.5f;
			component.linearVelocity = linearVelocity;
		}
		s.ChainJumpAt = now + 1f;
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg("[Move|" + tag + "] stuck at ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " - hopping (" + s.ChainJumps + ")");
		}
	}

	private static LadderStep DownStep(Bounds b, Vector3 f, int id)
	{
		float num = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.z) * b.extents.z;
		Vector3 vector = new Vector3(b.center.x, b.max.y, b.center.z) + f * (num + 0.5f);
		if (Physics.SphereCast(vector + Vector3.up * 1f, 0.25f, Vector3.down, out var hitInfo, 2.2f, Mask()) && Mathf.Abs(hitInfo.point.y - b.max.y) < 1f)
		{
			vector.y = hitInfo.point.y;
		}
		if (!LadderFoot(b, f, out var spot, out var _))
		{
			spot = new Vector3(b.center.x, b.min.y, b.center.z) - f * (num + 0.7f);
		}
		return new LadderStep
		{
			Foot = spot,
			TopSpot = vector,
			Dir = f,
			Bottom = b.min.y,
			Top = b.max.y,
			Id = id
		};
	}

	private static List<LadderStep> PlanChainDown(Bounds firstB, int firstId, Vector3 firstF, float needY)
	{
		List<LadderStep> list = new List<LadderStep> { DownStep(firstB, firstF, firstId) };
		HashSet<int> hashSet = new HashSet<int> { firstId };
		Bounds bounds = firstB;
		for (int i = 0; i < 12; i++)
		{
			if (!(bounds.min.y > needY + 0.8f))
			{
				break;
			}
			Vector3 foot = list[list.Count - 1].Foot;
			LadderStep ladderStep = null;
			Bounds bounds2 = default;
			float num = float.MaxValue;
			HashSet<int> hashSet2 = new HashSet<int>();
			Collider[] array = Physics.OverlapSphere(foot, 5f, Mask());
			foreach (Collider collider in array)
			{
				if (collider == null || collider.isTrigger)
				{
					continue;
				}
				GameObject gameObject = LadderRoot(collider, out var piece);
				if (gameObject == null || !hashSet2.Add(gameObject.GetInstanceID()) || !IsLadder(gameObject, piece, collider))
				{
					continue;
				}
				AutoJumpLedge componentInChildren = gameObject.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
				if (componentInChildren == null)
				{
					continue;
				}
				Bounds bounds3 = LadderBounds(gameObject, out var id);
				if (hashSet.Contains(id) || bounds3.min.y > bounds.min.y - 1f || Mathf.Abs(bounds3.max.y - bounds.min.y) > 1.2f)
				{
					continue;
				}
				Vector3 forward = componentInChildren.transform.forward;
				forward.y = 0f;
				if (!(forward.sqrMagnitude < 0.01f))
				{
					LadderStep ladderStep2 = DownStep(bounds3, forward.normalized, id);
					float num2 = Flat(ladderStep2.TopSpot, foot);
					if (!(num2 > 4.5f) && !(num2 >= num))
					{
						num = num2;
						ladderStep = ladderStep2;
						bounds2 = bounds3;
					}
				}
			}
			if (ladderStep == null)
			{
				break;
			}
			list.Add(ladderStep);
			hashSet.Add(ladderStep.Id);
			bounds = bounds2;
		}
		return list;
	}

	private static void RunChainDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		LadderStep ladderStep = s.Chain[s.ChainIdx];
		if (pos.y <= ladderStep.Bottom + 0.6f && c.IsOnGround())
		{
			s.ChainIdx++;
			ChainStepReset(s, pos, now);
			if (s.ChainIdx >= s.Chain.Count)
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] down the ladders at " + Fmt(pos));
				}
				EndChain(s, now);
				return;
			}
			ladderStep = s.Chain[s.ChainIdx];
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] next ladder down (" + (s.ChainIdx + 1) + "/" + s.Chain.Count + "), top at " + Fmt(ladderStep.TopSpot));
			}
		}
		if (c.IsOnGround() && pos.y < ladderStep.Bottom - 1f)
		{
			for (int i = s.ChainIdx + 1; i < s.Chain.Count; i++)
			{
				if (Mathf.Abs(s.Chain[i].Top - pos.y) < 1.2f)
				{
					s.ChainIdx = i;
					ChainStepReset(s, pos, now);
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] slipped down the ladders - carrying on from ladder " + (i + 1) + "/" + s.Chain.Count);
					}
					return;
				}
			}
			EndChain(s, now);
			return;
		}
		if (pos.y < s.ChainBestY - 0.3f)
		{
			s.ChainBestY = pos.y;
			s.ChainProgressAt = now;
		}
		Vector3 vector = ladderStep.Foot;
		if (pos.y > ladderStep.Top - 0.5f)
		{
			float num = Flat(pos, ladderStep.TopSpot);
			if (num < s.ChainFootBest - 0.15f)
			{
				s.ChainFootBest = num;
				s.ChainFootAt = now;
				s.ChainProgressAt = Mathf.Max(s.ChainProgressAt, now - 2f);
			}
			if (!(s.ChainFootBest <= 0.45f) && !(now - s.ChainFootAt > 1f))
			{
				vector = ladderStep.TopSpot;
				Vector3 via;
				if (now < s.ChainViaUntil && Flat(pos, s.ChainVia) > 0.35f)
				{
					vector = s.ChainVia;
				}
				else if (!FloorAlong(pos, ladderStep.TopSpot) && FindFloorVia(pos, ladderStep.TopSpot, out via))
				{
					s.ChainVia = via;
					s.ChainViaUntil = now + 3f;
					vector = via;
				}
			}
		}
		Vector3 vector2 = vector - pos;
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.04f)
		{
			vector2 = -ladderStep.Dir;
		}
		vector2.Normalize();
		if (now - s.ChainProgressAt > 6f)
		{
			MarkBadLadder(ladderStep.Id, now, 120f);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck going down ladder " + (s.ChainIdx + 1) + "/" + s.Chain.Count + " at " + Fmt(pos) + " - looking again");
			}
			EndChain(s, now);
		}
		else
		{
			ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(vector2), 540f * Time.deltaTime);
			Steer(ai, c, s, vector2, run: false, walk: false);
		}
	}

	private static Vector3 NextFootDir(S s, Vector3 pos)
	{
		Vector3 vector = s.Goal - pos;
		vector.y = 0f;
		float num = float.MaxValue;
		HashSet<int> hashSet = new HashSet<int>();
		Collider[] array = Physics.OverlapSphere(pos, 3.5f, Mask());
		foreach (Collider collider in array)
		{
			if (collider == null || collider.isTrigger)
			{
				continue;
			}
			GameObject gameObject = LadderRoot(collider, out var piece);
			if (gameObject == null || !hashSet.Add(gameObject.GetInstanceID()) || !IsLadder(gameObject, piece, collider))
			{
				continue;
			}
			AutoJumpLedge componentInChildren = gameObject.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
			if (componentInChildren == null)
			{
				continue;
			}
			Bounds b = LadderBounds(gameObject, out var id);
			if (id == s.ClimbLadderId || b.max.y < pos.y + 1f)
			{
				continue;
			}
			Vector3 forward = componentInChildren.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.01f || !LadderFoot(b, forward.normalized, out var spot, out var _) || Mathf.Abs(spot.y - pos.y) > 1f)
			{
				continue;
			}
			float num2 = Flat(pos, spot);
			if (num2 < num)
			{
				num = num2;
				vector = spot - pos;
				vector.y = 0f;
				if (vector.sqrMagnitude < 0.04f)
				{
					vector = forward;
				}
			}
		}
		if (!(vector.sqrMagnitude > 0.01f))
		{
			return Vector3.zero;
		}
		return vector.normalized;
	}

	private static bool TryStartLadderClimb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		s.NextLadderClimbSearch = now + 2f;
		if (c.IsSwimming() || !c.IsOnGround() || now < s.ClimbUntil)
		{
			return false;
		}
		float num = ((s.HasGood && s.GoodPos.y > pos.y + 1.5f && Vector3.Distance(pos, s.GoodPos) < 40f) ? s.GoodPos.y : s.Goal.y);
		if (num < pos.y + 1.5f)
		{
			return false;
		}
		if (UseKnownChain(s, pos, num, now, tag))
		{
			return true;
		}
		Collider[] array = Physics.OverlapSphere(pos + Vector3.up * 2f, 14f, Mask());
		GameObject gameObject = null;
		Bounds bounds = default;
		float num2 = float.MaxValue;
		int num3 = 0;
		HashSet<int> hashSet = new HashSet<int>();
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			if (collider == null || collider.isTrigger)
			{
				continue;
			}
			GameObject gameObject2 = LadderRoot(collider, out var piece);
			if (gameObject2 == null || !hashSet.Add(gameObject2.GetInstanceID()) || !IsLadder(gameObject2, piece, collider))
			{
				continue;
			}
			Bounds bounds2 = LadderBounds(gameObject2, out var id);
			if ((!hashSet.Add(id) && id != gameObject2.GetInstanceID()) || s.LadderClimbsTried.Contains(id) || BadLadder(id, now) || bounds2.max.y < pos.y + 1f)
			{
				continue;
			}
			float num4 = Flat(new Vector3(bounds2.center.x, bounds2.max.y, bounds2.center.z), s.Goal) + 2f * Mathf.Abs(num - bounds2.max.y);
			AutoJumpLedge componentInChildren = gameObject2.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
			Vector3 vector = ((componentInChildren != null) ? componentInChildren.transform.forward : Vector3.zero);
			vector.y = 0f;
			float num7;
			if (vector.sqrMagnitude > 0.01f && LadderFoot(bounds2, vector.normalized, out var spot, out var room))
			{
				if (Mathf.Abs(spot.y - pos.y) > 1f)
				{
					continue;
				}
				float num5 = bounds2.min.y - spot.y;
				if (num5 > 0.4f && (!room || num5 > 2f))
				{
					continue;
				}
				float num6 = Flat(pos, spot);
				if (num6 > 3f)
				{
					if (!GetPath(pos, spot, s_ladderPath, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: false))
					{
						continue;
					}
					Vector3 a = s_ladderPath[s_ladderPath.Count - 1];
					if (Flat(a, spot) > 1f || Mathf.Abs(a.y - spot.y) > 1f)
					{
						continue;
					}
					num6 = PathLength(pos, s_ladderPath);
				}
				num7 = num6 + num4 + 2f * Mathf.Max(0f, num5 - 0.4f);
			}
			else
			{
				if (bounds2.min.y > pos.y + 2.2f)
				{
					continue;
				}
				num7 = Flat(pos, bounds2.center) + num4 + 5f * Mathf.Max(0f, bounds2.min.y - pos.y - 0.5f);
			}
			if (num7 < num2)
			{
				num2 = num7;
				gameObject = gameObject2;
				bounds = bounds2;
				num3 = id;
			}
		}
		if (gameObject == null)
		{
			if (now >= s.NextLadderLog)
			{
				s.NextLadderLog = now + 30f;
				HashSet<string> hashSet2 = new HashSet<string>();
				array2 = array;
				foreach (Collider collider2 in array2)
				{
					if (!(collider2 == null) && !(collider2.bounds.max.y <= pos.y + 2f) && !(Flat(pos, collider2.bounds.center) >= 6f))
					{
						Piece componentInParent = collider2.GetComponentInParent<Piece>();
						string text = ((componentInParent != null) ? componentInParent.gameObject.name : (((collider2.transform.parent != null) ? (collider2.transform.parent.name + "/") : "") + collider2.gameObject.name));
						hashSet2.Add(text.Replace("(Clone)", ""));
						if (hashSet2.Count >= 8)
						{
							break;
						}
					}
				}
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] " + (num - pos.y).ToString("F1") + "m below where it needs to be and no ladder found (tall things close by: " + string.Join(", ", hashSet2) + ")");
				}
			}
			return false;
		}
		s.LadderClimbsTried.Add(num3);
		Vector3 vector2 = bounds.center - pos;
		vector2.y = 0f;
		if (vector2.sqrMagnitude < 0.04f)
		{
			vector2 = s.Goal - pos;
			vector2.y = 0f;
		}
		if (vector2.sqrMagnitude < 0.04f)
		{
			vector2 = ai.transform.forward;
		}
		AutoJumpLedge componentInChildren2 = gameObject.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
		s.ClimbAuto = false;
		if (componentInChildren2 != null)
		{
			Vector3 forward = componentInChildren2.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude > 0.01f)
			{
				forward.Normalize();
				vector2 = forward;
				s.ClimbAuto = true;
				if (LadderFoot(bounds, forward, out var spot2, out var _))
				{
					s.ClimbBase = spot2;
				}
				else
				{
					float num8 = Mathf.Abs(forward.x) * bounds.extents.x + Mathf.Abs(forward.z) * bounds.extents.z;
					s.ClimbBase = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - forward * (num8 + 0.7f);
				}
			}
		}
		s.ClimbDir = vector2.normalized;
		s.ClimbLedges = gameObject.GetComponentsInChildren<AutoJumpLedge>(includeInactive: true);
		if (s.ClimbAuto && LiftZoneEdge(s.ClimbLedges, s.ClimbBase, s.ClimbDir, out var zone))
		{
			s.ClimbBase = zone;
		}
		s.ClimbLadderId = num3;
		s.ClimbBottomY = bounds.min.y;
		s.ClimbTop = new Vector3(bounds.center.x, Mathf.Min(bounds.max.y, num + 0.5f), bounds.center.z);
		List<LadderStep> list = (s.ClimbAuto ? PlanChain(gameObject, bounds, num3, s.ClimbBase, s.ClimbDir, num) : null);
		if (list != null && list.Count >= 2 && !(Flat(pos, s.ClimbBase) > 3f) && !(Mathf.Abs(pos.y - s.ClimbBase.y) > 1f))
		{
			StartChain(s, list, 0, pos, now, tag);
			return true;
		}
		s.PendingChain = ((list != null && list.Count >= 2) ? list : null);
		s.PendingDown = false;
		if (s.ClimbAuto && (Flat(pos, s.ClimbBase) > 3f || Mathf.Abs(pos.y - s.ClimbBase.y) > 1f))
		{
			s.LadderPending = true;
			s.HasDetour = true;
			s.Detour = s.ClimbBase;
			s.DetourUntil = now + 15f;
			s.NextPlan = 0f;
			s.StepUntil = 0f;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (num - pos.y).ToString("F1") + "m below where it needs to be - heading for the ladder " + gameObject.name.Replace("(Clone)", "") + ", foot at " + Fmt(s.ClimbBase) + ", top at " + bounds.max.y.ToString("F1"));
			}
			return true;
		}
		s.ClimbLadder = true;
		s.ClimbStartY = pos.y;
		s.ClimbStartAt = now;
		s.ClimbJumps = 0;
		s.ClimbViaUntil = 0f;
		s.ClimbFootBest = float.MaxValue;
		s.ClimbFootAt = now;
		NoteLadderClimb(s, now, tag);
		s.ClimbTop = new Vector3(bounds.center.x, Mathf.Min(bounds.max.y, num + 0.5f), bounds.center.z);
		s.ClimbUntil = now + 20f;
		s.ClimbBestY = pos.y;
		s.ClimbProgressAt = now;
		s.NextClimbJump = 0f;
		s.StepUntil = 0f;
		s.HasDetour = false;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + (num - pos.y).ToString("F1") + "m below where it needs to be - climbing the ladder " + gameObject.name.Replace("(Clone)", "") + " at " + Fmt(bounds.center) + " (" + Flat(pos, bounds.center).ToString("F1") + "m away)");
		}
		return true;
	}

	private static void LadderTopHop(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		Vector3 vector = ((s.HasGood && s.GoodPos.y > pos.y - 1f && Vector3.Distance(pos, s.GoodPos) < 40f) ? s.GoodPos : s.Goal);
		Vector3 vector2 = vector - pos;
		vector2.y = 0f;
		Vector3[] array = ((!(vector2.sqrMagnitude > 0.04f)) ? new Vector3[1] { s.ClimbDir } : new Vector3[2] { s.ClimbDir, vector2.normalized });
		float num = c.m_jumpForce * c.m_jumpForce / (2f * Mathf.Max(1f, 0f - Physics.gravity.y)) * 0.85f;
		Vector3[] array2 = array;
		foreach (Vector3 vector3 in array2)
		{
			for (float num2 = 0.8f; num2 <= 3.01f; num2 += 0.4f)
			{
				Vector3 vector4 = pos + vector3 * num2;
				if (!Physics.Raycast(new Vector3(vector4.x, pos.y + num + 1.5f, vector4.z), Vector3.down, out var hitInfo, num + 3f, Mask()))
				{
					continue;
				}
				float num3 = hitInfo.point.y - pos.y;
				if (num3 < -0.5f || num3 > num || (num3 > 0.4f && vector.y < pos.y + 1f) || Physics.Raycast(new Vector3(vector4.x, hitInfo.point.y + 0.1f, vector4.z), Vector3.up, 1.6f, Mask()))
				{
					continue;
				}
				Vector3 vector5 = vector4 + vector3 * 0.5f;
				if (!Physics.Raycast(new Vector3(vector5.x, hitInfo.point.y + 1f, vector5.z), Vector3.down, out var hitInfo2, 1.5f, Mask()) || Mathf.Abs(hitInfo2.point.y - hitInfo.point.y) > 0.4f || MHR_Jump.Reach(c, vector3, num3) < num2)
				{
					continue;
				}
				if (num3 < 0.4f && num2 <= 1.3f)
				{
					s.StepDir = vector3;
					s.StepRun = false;
					s.StepUntil = now + 0.5f;
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stepping off the top of the ladder onto " + Fmt(hitInfo.point) + " (" + num2.ToString("F1") + "m on)");
					}
					return;
				}
				if (!LedgeAhead(hitInfo.point, vector3, 1f, out var _))
				{
					MHR_Jump.Leap(ai, c, vector3);
					s.StepDir = vector3;
					s.StepRun = true;
					s.StepUntil = now + 0.6f;
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] jumping from the top of the ladder onto " + Fmt(hitInfo.point) + " (" + num3.ToString("F1") + "m up, " + num2.ToString("F1") + "m on)");
					}
					return;
				}
			}
		}
		if (!TryHopUp(ai, c, s, pos, now, tag))
		{
			if (LedgeAhead(pos, s.ClimbDir, 0.7f, out var _))
			{
				Halt(ai, c, s);
				return;
			}
			s.StepDir = s.ClimbDir;
			s.StepRun = false;
			s.StepUntil = now + 0.5f;
		}
	}

	private static bool TryLadderDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		s.NextLadderClimbSearch = now + 2f;
		if (c.IsSwimming() || !c.IsOnGround() || s.DescendLadder || s.LadderPending)
		{
			return false;
		}
		float num = pos.y - s.Goal.y;
		if (num < 1.5f)
		{
			return false;
		}
		Collider[] array = Physics.OverlapSphere(pos, 14f, Mask());
		HashSet<int> hashSet = new HashSet<int>();
		float num2 = float.MaxValue;
		GameObject gameObject = null;
		int num3 = 0;
		Vector3 vector = Vector3.zero;
		Vector3 climbBase = Vector3.zero;
		Vector3 vector2 = Vector3.zero;
		Bounds firstB = default;
		bool flag = false;
		Collider[] array2 = array;
		foreach (Collider collider in array2)
		{
			if (collider == null || collider.isTrigger)
			{
				continue;
			}
			GameObject gameObject2 = LadderRoot(collider, out var piece);
			if (gameObject2 == null || !hashSet.Add(gameObject2.GetInstanceID()) || !IsLadder(gameObject2, piece, collider))
			{
				continue;
			}
			AutoJumpLedge componentInChildren = gameObject2.GetComponentInChildren<AutoJumpLedge>(includeInactive: true);
			Bounds bounds = LadderBounds(gameObject2, out var id);
			if ((!hashSet.Add(id) && id != gameObject2.GetInstanceID()) || BadLadder(id, now) || bounds.max.y < pos.y - 1.5f || bounds.max.y > pos.y + 1.2f || bounds.min.y > pos.y - 1.5f || Mathf.Abs(bounds.min.y - s.Goal.y) > num - 1f)
			{
				continue;
			}
			Vector3 vector3 = ((componentInChildren != null) ? componentInChildren.transform.forward : (bounds.center - pos));
			vector3.y = 0f;
			if (!(vector3.sqrMagnitude < 0.01f))
			{
				vector3.Normalize();
				float num4 = Mathf.Abs(vector3.x) * bounds.extents.x + Mathf.Abs(vector3.z) * bounds.extents.z;
				Vector3 vector4 = new Vector3(bounds.center.x, pos.y, bounds.center.z) + vector3 * (num4 + 0.6f);
				Vector3 vector5 = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - vector3 * (num4 + 0.7f);
				float num5 = Flat(pos, vector4) + Mathf.Abs(bounds.min.y - s.Goal.y);
				if (num5 < num2)
				{
					num2 = num5;
					gameObject = gameObject2;
					num3 = id;
					vector = vector4;
					climbBase = vector5;
					vector2 = vector3;
					firstB = bounds;
					flag = componentInChildren != null;
				}
			}
		}
		if (gameObject == null)
		{
			return false;
		}
		List<LadderStep> list = (flag ? PlanChainDown(firstB, num3, vector2, s.Goal.y) : null);
		if (list != null && list.Count >= 2)
		{
			if (Flat(pos, list[0].TopSpot) <= 3f && Mathf.Abs(pos.y - list[0].TopSpot.y) < 1f)
			{
				StartChain(s, list, 0, pos, now, tag, down: true);
				return true;
			}
			s.PendingChain = list;
			s.PendingDown = true;
		}
		else
		{
			s.PendingChain = null;
			s.PendingDown = false;
		}
		s.DescendLadder = true;
		s.DescendTop = vector;
		s.ClimbBase = climbBase;
		s.ClimbDir = vector2;
		s.ClimbLadderId = num3;
		s.StepUntil = 0f;
		s.ClimbUntil = 0f;
		if (Flat(pos, vector) > 1f)
		{
			s.LadderPending = true;
			s.HasDetour = true;
			s.Detour = vector;
			s.DetourUntil = now + 15f;
			s.NextPlan = 0f;
		}
		else
		{
			s.DescendUntil = now + 10f;
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + num.ToString("F1") + "m above where it needs to be - going down the ladder " + gameObject.name.Replace("(Clone)", "") + " (top at " + Fmt(vector) + ")");
		}
		return true;
	}

	private static void Descend(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		if (pos.y <= s.ClimbBase.y + 1f)
		{
			s.DescendLadder = false;
			s.DescendUntil = 0f;
			s.NextPlan = 0f;
			s.Ladder = 0;
			s.Passes = 0;
			s.MainBest = float.MaxValue;
			s.ProgressTime = now;
			s.AnchorTime = now;
			s.Anchor = pos;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] went down the ladder to " + Fmt(pos));
			}
		}
		else
		{
			Vector3 vector = s.ClimbBase - pos;
			vector.y = 0f;
			if (vector.sqrMagnitude < 0.09f)
			{
				vector = -s.ClimbDir;
			}
			vector.Normalize();
			ai.transform.rotation = Quaternion.LookRotation(vector);
			Steer(ai, c, s, vector, run: false, walk: false);
		}
	}

	private static bool TryStartClimb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag, string why)
	{
		if (!s.MainPartial || c.IsSwimming() || !c.IsOnGround() || s.ClimbTries >= 3)
		{
			return false;
		}
		float num = ((s.HasGood && s.GoodPos.y > pos.y + 1.5f && Vector3.Distance(pos, s.GoodPos) < 40f) ? s.GoodPos.y : s.Goal.y);
		if (num < pos.y + 1.5f)
		{
			return false;
		}
		num = Mathf.Min(num, pos.y + 10f);
		if (!FindClimb(s, pos, num, out var top, out var steep))
		{
			return false;
		}
		s.ClimbTries++;
		s.ClimbTried.Add(top);
		s.ClimbTop = top;
		s.ClimbUntil = now + 15f;
		s.ClimbBestY = pos.y;
		s.ClimbProgressAt = now;
		s.NextClimbJump = 0f;
		s.StepUntil = 0f;
		s.HasDetour = false;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] stuck (" + why + ") " + (num - pos.y).ToString("F1") + "m below where it needs to be at " + Fmt(pos) + " - climbing out at " + Fmt(top) + " (steepest step " + steep.ToString("F1") + "m, try " + s.ClimbTries + ")");
		}
		return true;
	}

	private static bool FindClimb(S s, Vector3 pos, float needY, out Vector3 top, out float steep)
	{
		top = Vector3.zero;
		steep = 0f;
		float num = float.MaxValue;
		int mask = LayerMask.GetMask("terrain", "static_solid", "piece", "Default");
		for (int i = 0; i < 16; i++)
		{
			Vector3 vector = Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward;
			float y = pos.y;
			float num2 = 0f;
			for (int j = 1; j <= 16; j++)
			{
				Vector3 vector2 = pos + vector * j;
				if (!Physics.Raycast(new Vector3(vector2.x, needY + 4f, vector2.z), Vector3.down, out var hitInfo, needY + 4f - (pos.y - 3f), mask) || MHR_Water.DepthAt(hitInfo.point) >= 1f)
				{
					break;
				}
				float num3 = hitInfo.point.y - y;
				if (num3 < -1.5f || (num3 > 1f && hitInfo.collider.gameObject.layer != s_terrainLayer) || num3 > 6f)
				{
					break;
				}
				if (num3 > num2)
				{
					num2 = num3;
				}
				y = hitInfo.point.y;
				if (!(hitInfo.point.y >= needY - 0.5f))
				{
					continue;
				}
				Vector3 vector3 = hitInfo.point + vector * 0.75f;
				bool flag = false;
				foreach (Vector3 item in s.ClimbTried)
				{
					if (Flat(item, vector3) < 3f)
					{
						flag = true;
						break;
					}
				}
				float num4 = num2 * 3f + (float)j * 0.1f;
				if (!flag && num4 < num)
				{
					num = num4;
					top = vector3;
					steep = num2;
				}
				break;
			}
		}
		return num < float.MaxValue;
	}

	private static void Climb(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		Vector3 vector = s.ClimbTop - pos;
		vector.y = 0f;
		if (pos.y > s.ClimbBestY + 0.3f)
		{
			s.ClimbBestY = pos.y;
			s.ClimbProgressAt = now;
			s.ClimbJumps = 0;
		}
		bool flag = pos.y >= s.ClimbTop.y - 0.6f;
		if (flag && s.ClimbLadder && s.ClimbAuto && pos.y < s.ClimbTop.y + 0.3f && now - s.ClimbProgressAt < 0.5f)
		{
			if (c.IsOnGround())
			{
				Halt(ai, c, s);
				if (s.ClimbFaceAt < s.ClimbStartAt)
				{
					s.ClimbFaceAt = now;
					s.ClimbFace = NextFootDir(s, pos);
				}
				if (s.ClimbFace.sqrMagnitude > 0.01f)
				{
					ai.transform.rotation = Quaternion.RotateTowards(ai.transform.rotation, Quaternion.LookRotation(s.ClimbFace), 360f * Time.deltaTime);
				}
				return;
			}
			flag = false;
		}
		bool flag2 = false;
		if (s.ClimbLadder && pos.y < s.ClimbStartY - 1f && c.IsOnGround())
		{
			s.ClimbUntil = 0f;
			s.ClimbLadder = false;
			s.NextPlan = 0f;
			s.Ladder = 0;
			s.NextLadderClimbSearch = 0f;
			s.ProgressTime = now;
			s.AnchorTime = now;
			s.Anchor = pos;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] fell off on the way to the ladder (now at " + Fmt(pos) + ") - looking again");
			}
			return;
		}
		if (s.ClimbLadder && !flag)
		{
			vector = s.ClimbDir;
			float num = Flat(pos, s.ClimbBase);
			if (num < s.ClimbFootBest - 0.15f)
			{
				s.ClimbFootBest = num;
				s.ClimbFootAt = now;
			}
			bool flag3 = now - s.ClimbFootAt > 1.5f;
			if (s.ClimbAuto && !flag3 && pos.y < s.ClimbBase.y + 0.8f && (num > 1.2f || (num > 0.6f && now - s.ClimbStartAt < 6f)))
			{
				Vector3 vector2 = s.ClimbBase - pos;
				vector2.y = 0f;
				vector = vector2;
				flag2 = true;
				if (now < s.ClimbViaUntil)
				{
					Vector3 vector3 = s.ClimbVia - pos;
					vector3.y = 0f;
					if (vector3.magnitude < 0.35f)
					{
						s.ClimbViaUntil = 0f;
					}
					else
					{
						vector = vector3;
					}
				}
				else if (!FloorAlong(pos, s.ClimbBase))
				{
					if (!FindFloorVia(pos, s.ClimbBase, out var via))
					{
						s.ClimbUntil = 0f;
						s.ClimbLadder = false;
						s.NextPlan = 0f;
						s.Ladder = 0;
						Halt(ai, c, s);
						s.ProgressTime = now;
						s.AnchorTime = now;
						s.Anchor = pos;
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] no floor all the way to the foot of the ladder at " + Fmt(s.ClimbBase) + " - trying another");
						}
						return;
					}
					s.ClimbVia = via;
					s.ClimbViaUntil = now + 3f;
					Vector3 vector4 = via - pos;
					vector4.y = 0f;
					vector = vector4;
					if (CompanionsPlugin.LogDetailed)
					{
						Dbg("[Move|" + tag + "] going round by " + Fmt(via) + " to the foot of the ladder, keeping to the floor");
					}
				}
				if (now - s.ClimbStartAt < 8f)
				{
					s.ClimbProgressAt = Mathf.Max(s.ClimbProgressAt, now - 4f);
				}
			}
		}
		if (flag || vector.sqrMagnitude < 0.25f)
		{
			s.ClimbUntil = 0f;
			s.NextPlan = 0f;
			s.Ladder = 0;
			s.Passes = 0;
			s.MainBest = float.MaxValue;
			s.ProgressTime = now;
			s.AnchorTime = now;
			s.Anchor = pos;
			bool climbLadder = s.ClimbLadder;
			if ((climbLadder & flag) && pos.y - s.ClimbStartY < 1f)
			{
				MarkBadLadder(s.ClimbLadderId, now, 300f);
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + ((!flag) ? "reached the climb spot" : (climbLadder ? "climbed the ladder" : "climbed out")) + " at " + Fmt(pos));
			}
			s.ClimbLadder = false;
			if (climbLadder & flag)
			{
				s.LadderClimbsTried.Clear();
			}
			if ((climbLadder & flag) && (!(s.Goal.y > pos.y + 1.5f) || !c.IsOnGround() || !TryStartLadderClimb(ai, c, s, pos, now, tag)))
			{
				LadderTopHop(ai, c, s, pos, now, tag);
				s.NextLadderClimbSearch = 0f;
				s.NextLadderSearch = 0f;
			}
			return;
		}
		vector.Normalize();
		float num2 = MaxStamina(ai);
		if (num2 > 0f && (now < s.ClimbRestUntil || Stamina(ai) < num2 * 0.15f) && c.IsOnGround())
		{
			if (now >= s.ClimbRestUntil)
			{
				s.ClimbRestUntil = now + 5f;
			}
			if (Stamina(ai) >= num2 * 0.6f)
			{
				s.ClimbRestUntil = 0f;
			}
			Halt(ai, c, s);
			s.ClimbProgressAt = now;
			s.ClimbUntil = Mathf.Max(s.ClimbUntil, now + 6f);
			return;
		}
		if (now - s.ClimbProgressAt > 5f)
		{
			s.ClimbUntil = 0f;
			s.AnchorTime = now - 2.5f;
			s.Ladder = 0;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't " + (s.ClimbLadder ? "get up the ladder" : "climb out") + " at " + Fmt(s.ClimbTop) + " (got to " + Fmt(pos) + ")");
			}
			if (s.ClimbLadder)
			{
				MarkBadLadder(s.ClimbLadderId, now, 300f);
			}
			s.ClimbLadder = false;
			return;
		}
		ai.transform.rotation = Quaternion.LookRotation(vector);
		Steer(ai, c, s, vector, !s.ClimbLadder, walk: false);
		if (s.ClimbLadder && s.ClimbAuto)
		{
			bool flag4 = s.ClimbBottomY > pos.y + 0.4f;
			float num3 = 2f;
			if (flag2 || (!flag4 && now - s.ClimbProgressAt < num3) || !c.IsOnGround() || !(now >= s.NextClimbJump))
			{
				return;
			}
			bool flag5 = pos.y > s.ClimbStartY + 0.8f;
			if (s.ClimbJumps >= 4)
			{
				s.ClimbUntil = 0f;
				s.AnchorTime = now - 2.5f;
				s.Ladder = 0;
				s.NextPlan = 0f;
				MarkBadLadder(s.ClimbLadderId, now, 120f);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] couldn't get " + (flag5 ? "further up" : "onto") + " the ladder at " + Fmt(s.ClimbBase) + " after " + s.ClimbJumps + " jumps (its bottom is " + (s.ClimbBottomY - s.ClimbStartY).ToString("F1") + "m up, best " + (s.ClimbBestY - s.ClimbStartY).ToString("F1") + "m) - trying another way");
				}
				s.ClimbLadder = false;
				return;
			}
			c.Jump();
			Rigidbody component = ai.GetComponent<Rigidbody>();
			if (component != null)
			{
				Vector3 linearVelocity = component.linearVelocity;
				linearVelocity.x = vector.x * 3f;
				linearVelocity.z = vector.z * 3f;
				component.linearVelocity = linearVelocity;
			}
			s.NextClimbJump = now + 1.2f;
			s.ClimbJumps++;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] jumping " + (flag5 ? "up the ladder" : "onto the ladder") + " (try " + s.ClimbJumps + "/" + 4 + (flag4 ? (", its bottom is " + (s.ClimbBottomY - pos.y).ToString("F1") + "m up") : "") + ")");
			}
		}
		else
		{
			if (!c.IsOnGround() || !(now >= s.NextClimbJump))
			{
				return;
			}
			Vector3 velocity = c.GetVelocity();
			velocity.y = 0f;
			if ((Physics.Raycast(pos + vector * 0.9f + Vector3.up * 3f, Vector3.down, out var hitInfo, 4f, Mask()) && hitInfo.point.y > pos.y + 0.4f) || velocity.magnitude < 1f)
			{
				c.Jump();
				Rigidbody component2 = ai.GetComponent<Rigidbody>();
				if (component2 != null)
				{
					Vector3 linearVelocity2 = component2.linearVelocity;
					linearVelocity2.x = vector.x * 5f;
					linearVelocity2.z = vector.z * 5f;
					component2.linearVelocity = linearVelocity2;
				}
				s.NextClimbJump = now + 0.3f;
			}
		}
	}

	private static void Rescue(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		if (s.HasGood && s.MainPartial && !c.IsSwimming() && !ai.IsInCombat && !(pos.y > s.GoodPos.y - 2f) && !(Vector3.Distance(pos, s.GoodPos) > 40f) && !(now - s.GoodAt > 300f))
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] trapped at " + Fmt(pos) + " (" + (s.GoodPos.y - pos.y).ToString("F1") + "m below the last good spot) - moving back to " + Fmt(s.GoodPos));
			}
			ai.MHR_PlaceAt(s.GoodPos);
			s.HasGood = false;
			Reset(s, s.Goal, s.GoodPos, now, tag);
		}
	}

	private static bool HopLine(Vector3 from, Vector3 to, out int hops)
	{
		return HopLine(from, to, out hops, null);
	}

	private static bool HopLine(Vector3 from, Vector3 to, out int hops, Character c)
	{
		hops = 0;
		s_hopWhy = null;
		float num = ((ModConfig.MaxDrop != null) ? ModConfig.MaxDrop.Value : 3f);
		float num2 = ((ModConfig.JumpGap != null) ? ModConfig.JumpGap.Value : 1.5f);
		Vector3 vector = to - from;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 1.5f)
		{
			return false;
		}
		vector /= magnitude;
		Vector3 vector2 = from;
		float num3 = 0f;
		for (float num4 = 0.5f; num4 <= magnitude + 0.01f; num4 += 0.5f)
		{
			Vector3 v = from + vector * Mathf.Min(num4, magnitude);
			bool flag = Physics.Raycast(new Vector3(v.x, vector2.y + 1.5f, v.z), Vector3.down, out var hitInfo, 2f + num, Mask());
			float num5 = (flag ? (hitInfo.point.y - vector2.y) : (-100f));
			if (!flag || num5 < 0f - num)
			{
				num3 += 0.5f;
				if (num3 > num2)
				{
					s_hopWhy = "a drop or gap at " + Fmt(v);
					return false;
				}
				continue;
			}
			if (num3 > 0f)
			{
				if (num5 > 0.3f)
				{
					s_hopWhy = "the far side of the gap at " + Fmt(v) + " is too high";
					return false;
				}
				if (c != null && (!MHR_Jump.CanAfford(c) || num3 + 0.25f > MHR_Jump.Reach(c, vector, num5)))
				{
					s_hopWhy = "the gap at " + Fmt(v) + " is too wide to jump" + (MHR_Jump.CanAfford(c) ? "" : " (out of stamina)");
					return false;
				}
				hops++;
				num3 = 0f;
			}
			if (num5 > 1.1f)
			{
				s_hopWhy = "a step too high at " + Fmt(v);
				return false;
			}
			if (NearFire(hitInfo.point, 0.4f) && !InFire(to))
			{
				s_hopWhy = "a fire at " + Fmt(hitInfo.point);
				return false;
			}
			if ((c != null) ? MHR_Water.WouldSwim(c, hitInfo.point) : (MHR_Water.DepthAt(hitInfo.point) > 0.8f))
			{
				s_hopWhy = "deep water at " + Fmt(v);
				return false;
			}
			if (Physics.Linecast(vector2 + Vector3.up * 1.4f, hitInfo.point + Vector3.up * 1.4f, Mask()))
			{
				s_hopWhy = "something in the way at " + Fmt(v);
				return false;
			}
			if (num5 > 0.35f)
			{
				hops++;
			}
			vector2 = hitInfo.point;
		}
		if (num3 > 0f)
		{
			s_hopWhy = "it ends over a drop";
			return false;
		}
		if (Mathf.Abs(vector2.y - to.y) > 1f)
		{
			s_hopWhy = "it doesn't end up at the target's height";
			return false;
		}
		return hops <= 10;
	}

	private static bool GapAhead(Character c, Vector3 pos, Vector3 dir)
	{
		float num = ((ModConfig.MaxDrop != null) ? ModConfig.MaxDrop.Value : 3f);
		float num2 = ((ModConfig.JumpGap != null) ? ModConfig.JumpGap.Value : 1.5f);
		if (Physics.Raycast(pos + dir * 0.6f + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 1.5f + num, Mask()))
		{
			return false;
		}
		for (float num3 = 1f; num3 <= num2 + 1f; num3 += 0.5f)
		{
			if (Physics.Raycast(pos + dir * num3 + Vector3.up * 1.5f, Vector3.down, out hitInfo, 1.5f + num, Mask()))
			{
				float num4 = MHR_Jump.Reach(c, dir, hitInfo.point.y - pos.y);
				if (LedgeAhead(pos, dir, 0.6f, out var _))
				{
					num4 *= 0.8f;
				}
				if (hitInfo.point.y < pos.y + 0.3f && MHR_Water.DepthAt(hitInfo.point) < 0.8f && MHR_Jump.CanAfford(c))
				{
					return num3 <= num4;
				}
				return false;
			}
		}
		return false;
	}

	private static bool StepAhead(Vector3 pos, Vector3 dir)
	{
		if (Physics.Raycast(pos + dir * 0.7f + Vector3.up * 2f, Vector3.down, out var hitInfo, 2.5f, Mask()) && hitInfo.point.y > pos.y + 0.35f && hitInfo.point.y < pos.y + 1.3f)
		{
			return true;
		}
		if (Physics.Raycast(pos + Vector3.up * 0.4f, dir, 0.7f, Mask()))
		{
			return !Physics.Raycast(pos + Vector3.up * 1.4f, dir, 1f, Mask());
		}
		return false;
	}

	public static Collider SpikeNear(Vector3 p, float r)
	{
		try
		{
			int num = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.6f, r, s_spikeBuf, -1, QueryTriggerInteraction.Collide);
			for (int i = 0; i < num; i++)
			{
				Collider collider = s_spikeBuf[i];
				if (!(collider == null))
				{
					Aoe componentInParent = collider.GetComponentInParent<Aoe>();
					if (!(componentInParent == null) && !(componentInParent.GetComponentInParent<Piece>() == null) && !(componentInParent.GetComponentInParent<Fireplace>() != null) && !(componentInParent.m_damage.GetTotalDamage() <= 0f))
					{
						return collider;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool HazardAhead(Vector3 pos, Vector3 dir, float dist)
	{
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.0001f)
		{
			return true;
		}
		dir.Normalize();
		Vector3 p = pos + dir * dist;
		if (!DropAhead(pos, dir, dist, 1.2f) && !(FireArea(p, 0.3f) != null))
		{
			return SpikeNear(p, 0.6f) != null;
		}
		return true;
	}

	private static bool LandingHazard(Vector3 pos, Vector3 dir)
	{
		for (float num = 2f; num <= 4f; num++)
		{
			Vector3 p = pos + dir * num;
			if (FireArea(p, 0.5f) != null || SpikeNear(p, 0.7f) != null || Lava(p))
			{
				return true;
			}
		}
		return false;
	}

	public static bool NearFire(Vector3 p, float margin)
	{
		return FireArea(p, margin) != null;
	}

	public static bool Lava(Vector3 p)
	{
		try
		{
			if (ZoneSystem.instance == null || !WorldGenerator.IsAshlands(p.x, p.z))
			{
				return false;
			}
			if (!ZoneSystem.instance.IsLava(p))
			{
				return false;
			}
			if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 4f, Mask()) && hitInfo.collider.GetComponent<Heightmap>() == null)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static bool LegClearOfLava(Vector3 p0, Vector3 p1, float skipStart = 0.3f)
	{
		Vector3 vector = p1 - p0;
		vector.y = 0f;
		float magnitude = vector.magnitude;
		if (magnitude < 0.05f)
		{
			return !Lava(p0);
		}
		Vector3 vector2 = vector / magnitude;
		for (float num = skipStart; num < magnitude; num += 0.5f)
		{
			if (Lava(p0 + vector2 * num))
			{
				return false;
			}
		}
		return !Lava(p1);
	}

	private static bool LavaExit(Vector3 pos, Vector3 prefer, out Vector3 dir, out float dist)
	{
		dir = Vector3.zero;
		dist = 0f;
		prefer.y = 0f;
		if (prefer.sqrMagnitude < 0.01f)
		{
			prefer = Vector3.forward;
		}
		prefer.Normalize();
		for (float num = 1.5f; num <= 8f; num += 1.5f)
		{
			for (int i = 0; i < 16; i++)
			{
				float y = (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 22.5f;
				Vector3 vector = Quaternion.Euler(0f, y, 0f) * prefer;
				if (!Lava(pos + vector * num) && !DropAhead(pos, vector, 1f, 1.2f))
				{
					dir = vector;
					dist = num;
					return true;
				}
			}
		}
		return false;
	}

	public static bool EscapeLava(CompanionAI ai)
	{
		if (ai == null)
		{
			return false;
		}
		Character component = ai.GetComponent<Character>();
		if (component == null || component.IsAttached() || component.IsDead() || component.IsSwimming())
		{
			return false;
		}
		int instanceID = ai.gameObject.GetInstanceID();
		Vector3 position = ai.transform.position;
		if (!WorldGenerator.IsAshlands(position.x, position.z) || (!component.InLava() && !Lava(position)))
		{
			s_lavaSince.Remove(instanceID);
			return false;
		}
		if (!component.IsOnGround())
		{
			return true;
		}
		float time = Time.time;
		if (!s_lavaSince.TryGetValue(instanceID, out var value))
		{
			value = time;
			s_lavaSince[instanceID] = time;
		}
		bool flag = LavaExit(position, ai.transform.forward, out var dir, out var dist);
		if (!s_lavaLog.TryGetValue(instanceID, out var value2) || time - value2 > 5f)
		{
			s_lavaLog[instanceID] = time;
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Move] \"" + component.m_name + "\" standing in lava at " + Fmt(position) + " - " + (flag ? ("getting out toward " + Fmt(position + dir * dist)) : "no clear ground close by"));
			}
		}
		if (!flag)
		{
			return false;
		}
		if (time - value > 1f && MHR_Jump.CanAfford(component))
		{
			s_lavaSince[instanceID] = time;
			MHR_Jump.Leap(ai, component, dir);
			return true;
		}
		ai.MoveTowards(dir, run: true);
		return true;
	}

	internal static EffectArea FireArea(Vector3 p, float radius)
	{
		try
		{
			for (float num = 0.3f; num < 2f; num += 0.6f)
			{
				EffectArea effectArea = EffectArea.IsPointInsideArea(p + Vector3.up * num, EffectArea.Type.Burning, radius);
				if (effectArea != null)
				{
					return effectArea;
				}
			}
			return FireZoneAt(p, radius);
		}
		catch
		{
		}
		return null;
	}

	public static bool InFire(Vector3 p)
	{
		try
		{
			return EffectArea.IsPointInsideArea(p + Vector3.up * 0.3f, EffectArea.Type.Burning, 0.3f) != null || FireZoneAt(p, 0.2f) != null;
		}
		catch
		{
			return false;
		}
	}

	private static EffectArea FireZoneAt(Vector3 p, float margin)
	{
		float time = Time.time;
		if (time - s_zonesAt > 2f)
		{
			s_zonesAt = time;
			s_zones.Clear();
			Fireplace[] array = UnityEngine.Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None);
			foreach (Fireplace fireplace in array)
			{
				if (fireplace == null || !fireplace.IsBurning())
				{
					continue;
				}
				EffectArea componentInChildren = fireplace.GetComponentInChildren<EffectArea>();
				Aoe[] componentsInChildren = fireplace.GetComponentsInChildren<Aoe>(includeInactive: false);
				foreach (Aoe aoe in componentsInChildren)
				{
					if (aoe == null || (aoe.m_damage.m_fire <= 0f && aoe.m_damageSelf <= 0f && aoe.m_damage.GetTotalDamage() <= 0f))
					{
						continue;
					}
					List<Collider> list = new List<Collider>();
					Collider[] components = aoe.GetComponents<Collider>();
					foreach (Collider collider in components)
					{
						if (collider != null && collider.isTrigger)
						{
							list.Add(collider);
						}
					}
					s_zones.Add(new FireZone
					{
						Fp = fireplace,
						Area = componentInChildren,
						Aoe = aoe,
						Triggers = list.ToArray()
					});
				}
			}
		}
		for (int l = 0; l < s_zones.Count; l++)
		{
			FireZone fireZone = s_zones[l];
			if (fireZone.Aoe == null || !fireZone.Aoe.isActiveAndEnabled)
			{
				continue;
			}
			Vector3 position = fireZone.Aoe.transform.position;
			if ((position - p).sqrMagnitude > 64f)
			{
				continue;
			}
			for (float num = 0.3f; num < 2f; num += 0.6f)
			{
				Vector3 vector = p + Vector3.up * num;
				bool flag;
				if (fireZone.Aoe.m_useTriggers && fireZone.Triggers.Length != 0)
				{
					flag = false;
					Collider[] components = fireZone.Triggers;
					foreach (Collider collider2 in components)
					{
						if (collider2 != null && Vector3.Distance(collider2.ClosestPoint(vector), vector) <= margin)
						{
							flag = true;
							break;
						}
					}
				}
				else if (fireZone.Aoe.m_useCollider != null)
				{
					Vector3 vector2 = Quaternion.Inverse(fireZone.Aoe.transform.rotation) * (vector - (position + fireZone.Aoe.m_useCollider.center));
					Vector3 vector3 = fireZone.Aoe.m_useCollider.size * 0.5f + Vector3.one * margin;
					flag = Mathf.Abs(vector2.x) <= vector3.x && Mathf.Abs(vector2.y) <= vector3.y && Mathf.Abs(vector2.z) <= vector3.z;
				}
				else
				{
					flag = Vector3.Distance(vector, position) <= fireZone.Aoe.m_radius + margin;
				}
				if (flag)
				{
					return fireZone.Area ?? fireZone.Fp.GetComponentInChildren<EffectArea>();
				}
			}
		}
		return null;
	}

	private static bool FireExit(Vector3 pos, Vector3 prefer, out Vector3 dir, bool wholeLeg = false)
	{
		dir = Vector3.zero;
		prefer.y = 0f;
		if (prefer.sqrMagnitude < 0.01f)
		{
			prefer = Vector3.forward;
		}
		prefer.Normalize();
		for (float num = 1.5f; num <= 3.6f; num++)
		{
			for (int i = 0; i < 8; i++)
			{
				float y = (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 45f;
				Vector3 vector = Quaternion.Euler(0f, y, 0f) * prefer;
				if (!NearFire(pos + vector * num, 0.3f) && !DropAhead(pos, vector, 1f, 1.2f) && (!wholeLeg || LegClearOfFire(pos, pos + vector * num, 0.6f)))
				{
					dir = vector;
					return true;
				}
			}
		}
		return false;
	}

	private static bool SafeSpot(Vector3 pos, out Vector3 spot)
	{
		spot = pos;
		for (float num = 1.5f; num <= 5f; num += 0.75f)
		{
			for (int i = 0; i < 12; i++)
			{
				if (Physics.Raycast(pos + Quaternion.Euler(0f, (float)i * 30f, 0f) * Vector3.forward * num + Vector3.up * 2f, Vector3.down, out var hitInfo, 4f, Mask()) && !(Mathf.Abs(hitInfo.point.y - pos.y) > 1.2f) && !InFire(hitInfo.point) && !Lava(hitInfo.point) && !MHR_Chests.IsFurniture(hitInfo.collider) && !(MHR_Water.DepthAt(hitInfo.point) > 0.5f) && !Physics.CheckCapsule(hitInfo.point + Vector3.up * 0.5f, hitInfo.point + Vector3.up * 1.6f, 0.3f, Mask()))
				{
					spot = hitInfo.point;
					return true;
				}
			}
		}
		return false;
	}

	public static bool EscapeFire(CompanionAI ai)
	{
		if (ai == null)
		{
			return false;
		}
		Character component = ai.GetComponent<Character>();
		int instanceID = ai.gameObject.GetInstanceID();
		if (component == null || component.IsAttached() || component.IsDead())
		{
			return false;
		}
		Vector3 position = ai.transform.position;
		if (!InFire(position))
		{
			s_fireSince.Remove(instanceID);
			return false;
		}
		if (!component.IsOnGround())
		{
			return false;
		}
		float time = Time.time;
		if (!s_fireSince.TryGetValue(instanceID, out var value))
		{
			value = time;
			s_fireSince[instanceID] = time;
		}
		bool flag = FireExit(position, ai.transform.forward, out var dir);
		if (!s_fireLog.TryGetValue(instanceID, out var value2) || time - value2 > 10f)
		{
			s_fireLog[instanceID] = time;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move] standing in a fire at " + Fmt(position) + " - " + (flag ? "stepping out" : "no clear way out"));
			}
		}
		if (time - value > 3f && SafeSpot(position, out var spot))
		{
			ai.MHR_PlaceAt(spot);
			s_fireSince.Remove(instanceID);
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move] still in the fire after 3s (stuck in it?) - moved to " + Fmt(spot));
			}
			return true;
		}
		if (flag && time - value > 1f && (!s_fireJump.TryGetValue(instanceID, out var value3) || time - value3 > 1.2f))
		{
			s_fireJump[instanceID] = time;
			MHR_Jump.Leap(ai, component, dir);
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move] jumping out of the fire");
			}
			return true;
		}
		if (flag)
		{
			ai.MoveTowards(dir, run: true);
		}
		return true;
	}

	private static bool LedgeAhead(Vector3 pos, Vector3 dir, float dist, out float drop)
	{
		drop = 0f;
		float num = ((ModConfig.MaxDrop != null) ? ModConfig.MaxDrop.Value : 3f);
		Vector3 vector = pos + dir.normalized * dist;
		if (Physics.SphereCast(vector + Vector3.up * 1f, 0.25f, Vector3.down, out var hitInfo, 1f + num, Mask()))
		{
			return false;
		}
		if (!Physics.Raycast(vector + Vector3.up * 1f, Vector3.down, out hitInfo, 60f, Mask()))
		{
			drop = 60f;
			return true;
		}
		if (MHR_Water.DepthAt(hitInfo.point) >= 1f)
		{
			return false;
		}
		drop = pos.y - hitInfo.point.y;
		return drop > num;
	}

	public static bool DropAhead(Vector3 pos, Vector3 dir, float dist, float maxDrop)
	{
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.0001f)
		{
			return false;
		}
		if (!Physics.Raycast(pos + dir.normalized * dist + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 1.5f + maxDrop, Mask()))
		{
			return true;
		}
		return MHR_Water.DepthAt(hitInfo.point) >= 1f;
	}

	private static void Hop(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag, string why)
	{
		if (c.IsSwimming())
		{
			ai.transform.rotation = Quaternion.LookRotation(dir);
			c.Jump();
			Rigidbody component = ai.GetComponent<Rigidbody>();
			if (component != null)
			{
				Vector3 linearVelocity = component.linearVelocity;
				linearVelocity.x = dir.x * 4f;
				linearVelocity.z = dir.z * 4f;
				component.linearVelocity = linearVelocity;
			}
			s.StepDir = dir;
			s.StepRun = true;
			s.StepUntil = now + 0.7f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] stuck in the water (" + why + ") at " + Fmt(pos) + " - jumping out toward " + Fmt(pos + dir * 2f));
			}
		}
		else if (!c.IsOnGround())
		{
			Sidestep(ai, c, s, pos, dir, now, tag, why, leftFirst: true);
		}
		else if (DropAhead(pos, dir, 1.5f, 1.2f) || NearFire(pos + dir * 1f, 0.4f) || NearFire(pos + dir * 2f, 0.4f) || Lava(pos + dir * 1.5f) || Lava(pos + dir * 3f))
		{
			Sidestep(ai, c, s, pos, dir, now, tag, why, leftFirst: true);
		}
		else
		{
			ai.transform.rotation = Quaternion.LookRotation(dir);
			c.Jump();
			Rigidbody component2 = ai.GetComponent<Rigidbody>();
			if (component2 != null)
			{
				Vector3 linearVelocity2 = component2.linearVelocity;
				linearVelocity2.x = dir.x * 4f;
				linearVelocity2.z = dir.z * 4f;
				component2.linearVelocity = linearVelocity2;
			}
			s.StepDir = dir;
			s.StepRun = true;
			s.StepUntil = now + 0.7f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] stuck (" + why + ") at " + Fmt(pos) + " - jumping toward " + Fmt(pos + dir * 2f));
			}
		}
	}

	private static bool Clear(Vector3 pos, Vector3 dir)
	{
		return Clear(pos, dir, 1.5f);
	}

	private static bool SafeClear(Vector3 pos, Vector3 dir)
	{
		if (Clear(pos, dir))
		{
			return !DropAhead(pos, dir, 1.5f, 1.2f);
		}
		return false;
	}

	private static bool TryDropDown(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		if (c.IsSwimming() || !c.IsOnGround())
		{
			return false;
		}
		Vector3 goal = s.Goal;
		float num = pos.y - goal.y;
		if (num < 1.5f)
		{
			return false;
		}
		float num2 = Mathf.Min((ModConfig.StuckDrop != null) ? ModConfig.StuckDrop.Value : 8f, CompanionFallDamagePatch.SafeDrop(c, 0.3f));
		float num3 = Flat(pos, goal);
		float num4 = float.MaxValue;
		Vector3 vector = Vector3.zero;
		Vector3 v = Vector3.zero;
		float num5 = 0f;
		for (int i = 0; i < 16; i++)
		{
			Vector3 vector2 = Quaternion.Euler(0f, (float)i * 22.5f, 0f) * Vector3.forward;
			if (!Clear(pos, vector2, 1f))
			{
				continue;
			}
			for (float num6 = 0.6f; num6 <= 3.01f; num6 += 0.3f)
			{
				Vector3 vector3 = pos + vector2 * num6;
				if (!Physics.Raycast(new Vector3(vector3.x, pos.y + 1f, vector3.z), Vector3.down, out var hitInfo, num2 + 1.5f, Mask()))
				{
					if (num6 > 0.6f)
					{
						break;
					}
					continue;
				}
				float num7 = pos.y - hitInfo.point.y;
				if (num7 < 1.2f)
				{
					continue;
				}
				if (num7 > num2 || MHR_Water.DepthAt(hitInfo.point) > 1f)
				{
					break;
				}
				float num8 = Flat(hitInfo.point, goal);
				if (!(num8 > num3 + 4f))
				{
					float num9 = Mathf.Abs(hitInfo.point.y - goal.y) * 2f + num8 * 0.3f + num7 * 0.2f;
					if (num9 < num4)
					{
						num4 = num9;
						vector = vector2;
						v = hitInfo.point;
						num5 = num6;
					}
				}
				break;
			}
		}
		if (vector == Vector3.zero || Mathf.Abs(v.y - goal.y) > num - 1f)
		{
			return false;
		}
		ai.transform.rotation = Quaternion.LookRotation(vector);
		Rigidbody component = ai.GetComponent<Rigidbody>();
		if (component != null)
		{
			Vector3 linearVelocity = component.linearVelocity;
			linearVelocity.x = vector.x * 3f;
			linearVelocity.z = vector.z * 3f;
			component.linearVelocity = linearVelocity;
		}
		s.StepDir = vector;
		s.StepRun = false;
		s.StepUntil = now + 0.5f + num5 * 0.3f;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + num.ToString("F1") + "m above where it needs to be - dropping down to " + Fmt(v) + " (" + (pos.y - v.y).ToString("F1") + "m)");
		}
		return true;
	}

	private static bool TryHopUp(CompanionAI ai, Character c, S s, Vector3 pos, float now, string tag)
	{
		if (c.IsSwimming() || !c.IsOnGround() || s.HopUpTries >= 6)
		{
			return false;
		}
		Vector3 vector = ((s.HasGood && s.GoodPos.y > pos.y + 0.5f && Vector3.Distance(pos, s.GoodPos) < 40f) ? s.GoodPos : s.Goal);
		float num = vector.y - pos.y;
		if (num < 0.5f || num > 6f || Flat(pos, vector) > 15f)
		{
			return false;
		}
		float num2 = c.m_jumpForce * c.m_jumpForce / (2f * Mathf.Max(1f, 0f - Physics.gravity.y)) * 0.85f;
		Vector3 vector2 = vector - pos;
		vector2.y = 0f;
		Vector3 vector3 = ((vector2.sqrMagnitude > 0.04f) ? vector2.normalized : ai.transform.forward);
		float num3 = float.MinValue;
		float num4 = 0f;
		Vector3 vector4 = Vector3.zero;
		Vector3 v = Vector3.zero;
		for (int i = 0; i < 12; i++)
		{
			Vector3 vector5 = Quaternion.Euler(0f, (float)i * 30f, 0f) * vector3;
			for (float num5 = 0.6f; num5 <= 2.61f; num5 += 0.4f)
			{
				Vector3 vector6 = pos + vector5 * num5;
				if (!Physics.Raycast(new Vector3(vector6.x, pos.y + num2 + 1f, vector6.z), Vector3.down, out var hitInfo, num2 + 1.5f, Mask()))
				{
					continue;
				}
				float num6 = hitInfo.point.y - pos.y;
				if (!(num6 < 0.4f) && !(num6 > num2) && !(hitInfo.normal.y < 0.6f) && !Physics.Raycast(new Vector3(vector6.x, hitInfo.point.y + 0.1f, vector6.z), Vector3.up, 1.2f, Mask()) && !(MHR_Jump.Reach(c, vector5, num6) < num5))
				{
					float num7 = hitInfo.point.y - Vector3.Angle(vector5, vector3) * 0.004f;
					if (num7 > num3)
					{
						num3 = num7;
						vector4 = vector5;
						v = hitInfo.point;
						num4 = num5;
					}
					break;
				}
			}
		}
		if (vector4 == Vector3.zero)
		{
			return false;
		}
		s.HopUpTries++;
		MHR_Jump.Leap(ai, c, vector4);
		s.StepDir = vector4;
		s.StepRun = true;
		s.StepUntil = now + 0.6f;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Move|" + tag + "] " + num.ToString("F1") + "m below where it needs to be - jumping up onto " + Fmt(v) + " (" + (v.y - pos.y).ToString("F1") + "m up, " + num4.ToString("F1") + "m on)");
		}
		return true;
	}

	public static bool SafeStep(Vector3 pos, Vector3 dir, float dist)
	{
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.0001f || dist <= 0f)
		{
			return false;
		}
		dir.Normalize();
		if (Physics.Raycast(pos + Vector3.up * 0.5f, dir, dist + 0.3f, Mask()))
		{
			return false;
		}
		float num = Mathf.Min(1f, Mathf.Max(0.4f, MHR_Water.DepthAt(pos) + 0.15f));
		for (float num2 = 0.5f; num2 < dist + 0.49f; num2 += 0.5f)
		{
			if (!Physics.SphereCast(pos + dir * Mathf.Min(num2, dist) + Vector3.up * 1.5f, 0.2f, Vector3.down, out var hitInfo, 2.7f, Mask()))
			{
				return false;
			}
			if (hitInfo.point.y < pos.y - 1f || hitInfo.point.y > pos.y + 0.8f)
			{
				return false;
			}
			if (MHR_Water.DepthAt(hitInfo.point) > num)
			{
				return false;
			}
			if (FireArea(hitInfo.point, 0.5f) != null)
			{
				return false;
			}
			if (SpikeNear(hitInfo.point, 0.7f) != null)
			{
				return false;
			}
			if (Lava(hitInfo.point))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Clear(Vector3 pos, Vector3 dir, float dist)
	{
		if (Physics.Raycast(pos + Vector3.up * 0.5f, dir, dist, Mask()))
		{
			return false;
		}
		if (!Physics.Raycast(pos + dir * dist + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 4f, Mask()))
		{
			return false;
		}
		if (hitInfo.point.y < pos.y - 1.2f)
		{
			return false;
		}
		if (SpikeNear(hitInfo.point, 0.7f) != null || FireArea(hitInfo.point, 0.4f) != null || Lava(hitInfo.point))
		{
			return false;
		}
		return MHR_Water.DepthAt(hitInfo.point) < 1f;
	}

	private static bool FindRouteStart(Vector3 pos, Vector3 goal, out Vector3 start)
	{
		start = Vector3.zero;
		float num = float.MaxValue;
		bool flag = false;
		for (int i = 1; i <= 2; i++)
		{
			float num2 = ((i == 1) ? 1.5f : 2.5f);
			for (int j = 0; j < 8; j++)
			{
				Vector3 vector = Quaternion.Euler(0f, (float)j * 45f, 0f) * Vector3.forward;
				if (!Clear(pos, vector, num2))
				{
					continue;
				}
				Vector3 vector2 = pos + vector * num2;
				if (!Physics.Raycast(vector2 + Vector3.up * 1.5f, Vector3.down, out var hitInfo, 4f, Mask()))
				{
					continue;
				}
				vector2 = hitInfo.point;
				s_tmp.Clear();
				if (GetPath(vector2, goal, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: false))
				{
					Vector3 a = s_tmp[s_tmp.Count - 1];
					bool flag2 = Flat(a, goal) <= 1.5f && Mathf.Abs(a.y - goal.y) <= 2.2f;
					float num3 = num2;
					Vector3 a2 = vector2;
					for (int k = 0; k < s_tmp.Count; k++)
					{
						num3 += Vector3.Distance(a2, s_tmp[k]);
						a2 = s_tmp[k];
					}
					if (!flag2)
					{
						num3 += Vector3.Distance(a2, goal);
					}
					if ((flag2 && !flag) || (flag2 == flag && num3 < num))
					{
						num = num3;
						flag = flag2;
						start = vector2;
					}
				}
			}
			if (flag)
			{
				break;
			}
		}
		return num < float.MaxValue;
	}

	private static bool FindDetour(Vector3 pos, Vector3 goal, Vector3 dir, out Vector3 det)
	{
		det = Vector3.zero;
		Vector3 normalized = Vector3.Cross(Vector3.up, dir).normalized;
		for (int i = 0; i < 4; i++)
		{
			float num = ((i < 2) ? 4f : 7f) * ((i % 2 == 0) ? 1f : (-1f));
			Vector3 to = pos + normalized * num - dir * 1.5f;
			s_tmp.Clear();
			if (GetPath(pos, to, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: true))
			{
				det = s_tmp[s_tmp.Count - 1];
				if (!(Flat(det, pos) < 2f))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool TryWaterJump(CompanionAI ai, Character c, S s, Vector3 pos, Vector3 dir, float now, string tag)
	{
		float num = MHR_Water.SwimLimit(c);
		float num2 = ((ModConfig.JumpWaterDepth != null) ? ModConfig.JumpWaterDepth.Value : 0.3f);
		float num3 = ((ModConfig.JumpWaterWidth != null) ? ModConfig.JumpWaterWidth.Value : 3f);
		if (c.InInterior())
		{
			return false;
		}
		float num4 = -1f;
		float num5 = -1f;
		bool flag = false;
		for (float num6 = 0.5f; num6 <= 6f; num6 += 0.5f)
		{
			Vector3 p = pos + dir * num6;
			float num7 = MHR_Water.DepthAt(p);
			if (MHR_Water.GroundAt(p) + num7 < pos.y - 1.5f)
			{
				return false;
			}
			if (num4 < 0f)
			{
				if (num7 > num2)
				{
					num4 = num6;
				}
				else if (num6 > 1.5f)
				{
					return false;
				}
			}
			else if (num7 < Mathf.Min(num2, 0.3f))
			{
				num5 = num6;
				break;
			}
			if (num4 >= 0f && num7 > num)
			{
				flag = true;
			}
		}
		if (num4 < 0f)
		{
			return false;
		}
		if (now - s.LastWaterJumpAt < 6f && Vector3.Dot(dir, s.LastWaterJumpDir) < -0.3f)
		{
			s.NextWaterPeek = now + 1f;
			return false;
		}
		float num8 = ((num5 > 0f) ? MHR_Water.GroundAt(pos + dir * num5) : pos.y);
		bool flag2 = num5 > 0f && MHR_Water.DepthAt(pos + dir * num5) < 0.05f && MHR_Water.DepthAt(pos + dir * (num5 + 1f)) < 0.05f && MHR_Water.DepthAt(pos + dir * (num5 + 2f)) < 0.05f;
		if (((!s.Quiet && num5 > 0f) & flag2) && num5 - num4 <= num3 && num8 < pos.y + 1f && MHR_Jump.CanAfford(c) && num5 <= MHR_Jump.Reach(c, dir, num8 - pos.y) && !LandingHazard(pos, dir))
		{
			Leap(ai, c, dir, 6.5f);
			s.NextJump = now + 0.8f;
			s.StepDir = dir;
			s.StepRun = true;
			s.StepUntil = now + 0.6f;
			s.LastWaterJumpDir = dir;
			s.LastWaterJumpAt = now;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] jumping the " + (flag ? "stream" : "shallow water") + " at " + Fmt(pos + dir * num4));
			}
			return true;
		}
		if ((!s.Quiet & flag) && num4 <= 1.5f && MHR_Jump.HasSpare(c, 0.25f))
		{
			float num9 = MHR_Jump.Reach(c, dir, 0f);
			if (num9 >= 2f)
			{
				Vector3 vector = pos + dir * Mathf.Min(num9, 6f);
				float num10 = MHR_Water.GroundAt(vector);
				if (!MHR_Water.WouldSwim(c, vector) && num10 < pos.y + 0.5f && num10 > pos.y - ((ModConfig.MaxDrop != null) ? ModConfig.MaxDrop.Value : 3f) && !LandingHazard(pos, dir))
				{
					Leap(ai, c, dir, 6.5f);
					s.NextJump = now + 0.8f;
					s.StepDir = dir;
					s.StepRun = true;
					s.StepUntil = now + 0.6f;
					s.LastWaterJumpDir = dir;
					s.LastWaterJumpAt = now;
					if (CompanionsPlugin.LogDetailed)
					{
						Dbg("[Move|" + tag + "] jumping as far across the water as it can (lands at " + Fmt(vector) + ")");
					}
					return true;
				}
			}
		}
		if (!flag)
		{
			s.NextWaterPeek = now + 1f;
			return false;
		}
		if (s.TooTired && num4 <= 1.5f)
		{
			s.WaitStamina = true;
			s.WaitStart = now;
			Halt(ai, c, s);
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] too tired to swim across - waiting on the bank");
			}
			return true;
		}
		if (((num5 < 0f) & flag) && now >= s.NextDive && num4 <= 1f && !s.WaitStamina && s.RouteSwim > 3f && tag != "Loot")
		{
			Leap(ai, c, dir, 5f);
			s.NextJump = now + 1.5f;
			s.NextDive = now + 20f;
			s.StepDir = dir;
			s.StepRun = true;
			s.StepUntil = now + 0.6f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] diving in at " + Fmt(pos + dir * num4));
			}
			return true;
		}
		return false;
	}

	private static void Leap(CompanionAI ai, Character c, Vector3 dir, float speed)
	{
		MHR_Jump.Leap(ai, c, dir);
	}

	private static bool FindShore(Character c, Vector3 pos, Vector3 goal, out Vector3 best)
	{
		best = Vector3.zero;
		float num = float.MaxValue;
		bool result = false;
		float num2 = MHR_Water.SwimLimit(c);
		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < 16; j++)
			{
				float num3 = i switch
				{
					2 => 8f, 
					1 => 5f, 
					0 => 3f, 
					_ => 12f, 
				};
				float f = (float)j * 22.5f * (Mathf.PI / 180f);
				Vector3 p = pos + new Vector3(Mathf.Cos(f), 0f, Mathf.Sin(f)) * num3;
				float num4 = MHR_Water.WaterLevel(p);
				if (num4 < -1000f)
				{
					num4 = pos.y;
				}
				float num5 = MHR_Water.GroundAt(new Vector3(p.x, num4 + 0.5f, p.z));
				if (!(num4 - num5 > num2 - 0.3f) && !(num5 - num4 > 1f))
				{
					float num6 = num3 + Vector3.Distance(new Vector3(p.x, num5, p.z), goal) * 0.6f;
					if (num6 < num)
					{
						num = num6;
						best = new Vector3(p.x, num5, p.z);
						result = true;
					}
				}
			}
		}
		return result;
	}

	private static float MaxStamina(Component c)
	{
		CompanionStamina component = c.GetComponent<CompanionStamina>();
		if (!(component != null))
		{
			return 0f;
		}
		return component.MaxStamina;
	}

	private static float Stamina(Component c)
	{
		CompanionStamina component = c.GetComponent<CompanionStamina>();
		if (!(component != null) || !(component.MaxStamina > 0f))
		{
			return 999f;
		}
		return component.Stamina;
	}

	private static bool DoorClosed(Door d)
	{
		if (d == null)
		{
			return false;
		}
		ZNetView component = d.GetComponent<ZNetView>();
		if (component != null && component.IsValid())
		{
			return component.GetZDO().GetInt("state") == 0;
		}
		return false;
	}

	private static bool Usable(Door d)
	{
		try
		{
			return d != null && DoorHandler.IsValidClosedDoor(d);
		}
		catch
		{
			return false;
		}
	}

	private static Door DoorNear(Vector3 pos, float range)
	{
		int num = Physics.OverlapSphereNonAlloc(pos + Vector3.up, range, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
		Door result = null;
		float num2 = float.MaxValue;
		for (int i = 0; i < num; i++)
		{
			Door door = ((s_doorCols[i] != null) ? s_doorCols[i].GetComponentInParent<Door>() : null);
			if (Usable(door))
			{
				float num3 = MHR_Nav.DoorDist(pos, door.transform.position);
				if (num3 < num2)
				{
					num2 = num3;
					result = door;
				}
			}
		}
		if (!(num2 <= range + 0.5f))
		{
			return null;
		}
		return result;
	}

	private static Door DoorAhead(Vector3 pos, Vector3 target)
	{
		Vector3 vector = pos + Vector3.up * 1f;
		Vector3 vector2 = new Vector3(target.x, Mathf.Max(target.y, pos.y) + 1f, target.z) - vector;
		float num = Mathf.Min(vector2.magnitude, 2.5f);
		if (num < 0.2f)
		{
			return null;
		}
		if (!Physics.Raycast(vector, vector2.normalized, out var hitInfo, num, LayerMask.GetMask("piece", "Default")))
		{
			return null;
		}
		Door door = ((hitInfo.collider != null) ? hitInfo.collider.GetComponentInParent<Door>() : null);
		if (!Usable(door))
		{
			return null;
		}
		return door;
	}

	private static Vector3 DoorwayLine(S s, Vector3 pos, Vector3 target, float now, string tag)
	{
		if (s.PassDoor == null && now >= s.NextDoorwayCheck)
		{
			s.NextDoorwayCheck = now + 0.4f;
			Door door = DoorwayOnRoute(s, pos, out var side);
			if (door != null)
			{
				s.PassDoor = door;
				s.PassSide = side;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg("[Move|" + tag + "] lining up with the " + door.m_name + " at " + Fmt(door.transform.position));
				}
			}
		}
		Door passDoor = s.PassDoor;
		if (passDoor == null)
		{
			return target;
		}
		if (!passDoor || passDoor.GetComponent<ZNetView>() == null || !passDoor.GetComponent<ZNetView>().IsValid())
		{
			s.PassDoor = null;
			return target;
		}
		Vector3 position = passDoor.transform.position;
		Vector3 forward = passDoor.transform.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < 0.01f)
		{
			s.PassDoor = null;
			return target;
		}
		forward.Normalize();
		Vector3 lhs = pos - position;
		lhs.y = 0f;
		float num = Vector3.Dot(lhs, forward) * s.PassSide;
		float f = Vector3.Dot(lhs, new Vector3(forward.z, 0f, 0f - forward.x));
		if (num < -0.9f || lhs.magnitude > 5f || Mathf.Abs(pos.y - position.y) > 2.5f)
		{
			s.PassDoor = null;
			s.NextDoorwayCheck = now + 1f;
			s.NextPlan = 0f;
			return target;
		}
		Vector3 vector = forward * s.PassSide;
		if (num > 0.35f && Mathf.Abs(f) > 0.25f)
		{
			Vector3 vector2 = position + vector * Mathf.Clamp(num, 0.8f, 1.4f);
			return new Vector3(vector2.x, pos.y, vector2.z);
		}
		Vector3 vector3 = position - vector * 1.4f;
		return new Vector3(vector3.x, pos.y, vector3.z);
	}

	private static Door DoorwayOnRoute(S s, Vector3 pos, out float side)
	{
		side = 0f;
		if (s.Path.Count == 0)
		{
			return null;
		}
		int num = Physics.OverlapSphereNonAlloc(pos + Vector3.up, 5f, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
		Door result = null;
		float num2 = float.MaxValue;
		HashSet<int> hashSet = new HashSet<int>();
		for (int i = 0; i < num; i++)
		{
			Door door = ((s_doorCols[i] != null) ? s_doorCols[i].GetComponentInParent<Door>() : null);
			if (door == null || !hashSet.Add(door.GetInstanceID()))
			{
				continue;
			}
			Vector3 position = door.transform.position;
			if (Mathf.Abs(position.y - pos.y) > 2.5f)
			{
				continue;
			}
			Vector3 forward = door.transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.01f)
			{
				continue;
			}
			forward.Normalize();
			Vector3 a = pos;
			float num3 = 0f;
			for (int j = Mathf.Clamp(s.Corner, 0, s.Path.Count - 1); j < s.Path.Count; j++)
			{
				if (!(num3 < 6f))
				{
					break;
				}
				Vector3 vector = s.Path[j];
				float num4 = Vector3.Dot(new Vector3(a.x - position.x, 0f, a.z - position.z), forward);
				float num5 = Vector3.Dot(new Vector3(vector.x - position.x, 0f, vector.z - position.z), forward);
				if (num4 * num5 < 0f || Mathf.Abs(num5) < 0.05f)
				{
					float t = Mathf.Abs(num4) / Mathf.Max(0.001f, Mathf.Abs(num4) + Mathf.Abs(num5));
					Vector3 vector2 = Vector3.Lerp(a, vector, t);
					Vector3 vector3 = vector2 - position;
					vector3.y = 0f;
					if (vector3.magnitude <= 1.3f && Mathf.Abs(num4) > 0.5f)
					{
						float num6 = num3 + Vector3.Distance(a, vector2);
						if (num6 < num2)
						{
							num2 = num6;
							result = door;
							side = Mathf.Sign(num4);
						}
					}
					break;
				}
				num3 += Vector3.Distance(a, vector);
				a = vector;
			}
		}
		return result;
	}

	private static Door OpenLeafAhead(Vector3 pos, Vector3 target)
	{
		Vector3 vector = target - pos;
		vector.y = 0f;
		float num = Mathf.Min(vector.magnitude, 1.6f);
		if (num < 0.2f)
		{
			return null;
		}
		Vector3 normalized = vector.normalized;
		float[] array = new float[3] { 0.5f, 1f, 1.5f };
		for (int i = 0; i < array.Length; i++)
		{
			if (Physics.Raycast(pos + Vector3.up * array[i], normalized, out var hitInfo, num, LayerMask.GetMask("piece", "Default", "piece_nonsolid")))
			{
				Door door = ((hitInfo.collider != null) ? hitInfo.collider.GetComponentInParent<Door>() : null);
				if (!(door == null) && !DoorClosed(door) && !(door.m_keyItem != null) && (!door.m_checkGuardStone || PrivateArea.CheckAccess(door.transform.position, 0f, flash: false)) && (!s_leafClosed.TryGetValue(door.GetInstanceID(), out var value) || !(Time.time < value)))
				{
					return door;
				}
			}
		}
		return null;
	}

	private static Door OpenDoorNear(Vector3 pos, float range)
	{
		int num = Physics.OverlapSphereNonAlloc(pos + Vector3.up, range, s_doorCols, LayerMask.GetMask("piece", "piece_nonsolid", "Default"));
		Door result = null;
		float num2 = float.MaxValue;
		for (int i = 0; i < num; i++)
		{
			Collider collider = s_doorCols[i];
			Door door = ((collider != null) ? collider.GetComponentInParent<Door>() : null);
			if (!(door == null) && !DoorClosed(door) && !(door.m_keyItem != null) && (!door.m_checkGuardStone || PrivateArea.CheckAccess(door.transform.position, 0f, flash: false)) && (!s_leafClosed.TryGetValue(door.GetInstanceID(), out var value) || !(Time.time < value)))
			{
				float num3 = Vector3.Distance(pos + Vector3.up, collider.ClosestPoint(pos + Vector3.up));
				if (num3 < num2)
				{
					num2 = num3;
					result = door;
				}
			}
		}
		return result;
	}

	private static void CloseDoor(CompanionAI ai, Character c, Door d, string tag)
	{
		if (!(d == null) && !DoorClosed(d))
		{
			s_leafClosed[d.GetInstanceID()] = Time.time + 8f;
			if (s_leafClosed.Count > 200)
			{
				s_leafClosed.Clear();
			}
			Vector3 vector = d.transform.position - ai.transform.position;
			vector.y = 0f;
			if (vector.sqrMagnitude > 0.01f)
			{
				ai.transform.rotation = Quaternion.LookRotation(vector.normalized);
			}
			Humanoid humanoid = c as Humanoid;
			bool flag = false;
			try
			{
				flag = humanoid != null && d.Interact(humanoid, hold: false, alt: false);
			}
			catch
			{
				flag = false;
			}
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] " + (flag ? "closed" : "couldn't close") + " the " + d.m_name + " at " + Fmt(d.transform.position) + " - it was open towards us and in the way");
			}
		}
	}

	public static bool ReachableViaDoor(Vector3 pos, Vector3 goal)
	{
		return CanGetTo(pos, goal);
	}

	private static Door RouteDoor(Vector3 pos, Vector3 goal, out Vector3 spot, Door skip)
	{
		spot = Vector3.zero;
		s_doors.Clear();
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(goal, 35f, s_pieces);
		if (Flat(pos, goal) > 20f)
		{
			Piece.GetAllPiecesInRadius(pos, 20f, s_pieces);
		}
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece piece = s_pieces[i];
			Door door = ((piece != null) ? piece.GetComponentInChildren<Door>() : null);
			if (Usable(door) && !(door == skip) && !s_doors.Contains(door))
			{
				s_doors.Add(door);
			}
		}
		if (s_doors.Count == 0)
		{
			return null;
		}
		Vector3 mid = (pos + goal) * 0.5f;
		s_doors.Sort((Door door3, Door b) => Vector3.Distance(door3.transform.position, mid).CompareTo(Vector3.Distance(b.transform.position, mid)));
		if (s_doors.Count > 8)
		{
			s_doors.RemoveRange(8, s_doors.Count - 8);
		}
		int num = 120;
		bool[] array = new bool[s_doors.Count * 2];
		Queue<DoorNode> queue = new Queue<DoorNode>();
		for (int num2 = 0; num2 < s_doors.Count; num2++)
		{
			for (int num3 = -1; num3 <= 1; num3 += 2)
			{
				if (num-- <= 0)
				{
					break;
				}
				if (ToDoor(pos, s_doors[num2], num3, out var end, out var len))
				{
					array[num2 * 2 + ((num3 > 0) ? 1 : 0)] = true;
					queue.Enqueue(new DoorNode
					{
						Door = num2,
						Side = num3,
						Depth = 1,
						First = num2,
						FirstSpot = end,
						Len = len
					});
				}
			}
		}
		Door result = null;
		float num4 = float.MaxValue;
		int num5 = 99;
		while (queue.Count > 0 && num > 0)
		{
			DoorNode doorNode = queue.Dequeue();
			if (doorNode.Depth > num5)
			{
				break;
			}
			Door door2 = s_doors[doorNode.Door];
			Vector3 vector = door2.transform.position - door2.transform.forward * (1.4f * (float)doorNode.Side);
			s_tmp2.Clear();
			num--;
			if (GetPath(vector, goal, s_tmp2, Pathfinding.AgentType.Humanoid, requireFull: false))
			{
				Vector3 a = s_tmp2[s_tmp2.Count - 1];
				if (Flat(a, goal) <= 2f && Mathf.Abs(a.y - goal.y) <= 2.2f)
				{
					float num6 = doorNode.Len + Vector3.Distance(door2.transform.position, goal);
					if (num6 < num4)
					{
						num4 = num6;
						result = s_doors[doorNode.First];
						spot = doorNode.FirstSpot;
						num5 = doorNode.Depth;
					}
					continue;
				}
			}
			if (doorNode.Depth >= 3)
			{
				continue;
			}
			for (int num7 = 0; num7 < s_doors.Count; num7++)
			{
				if (num <= 0)
				{
					break;
				}
				if (num7 == doorNode.Door)
				{
					continue;
				}
				for (int num8 = -1; num8 <= 1; num8 += 2)
				{
					int num9 = num7 * 2 + ((num8 > 0) ? 1 : 0);
					if (!array[num9] && num-- > 0 && ToDoor(vector, s_doors[num7], num8, out var _, out var len2))
					{
						array[num9] = true;
						queue.Enqueue(new DoorNode
						{
							Door = num7,
							Side = num8,
							Depth = doorNode.Depth + 1,
							First = doorNode.First,
							FirstSpot = doorNode.FirstSpot,
							Len = doorNode.Len + len2
						});
					}
				}
			}
		}
		return result;
	}

	private static bool ToDoor(Vector3 from, Door d, int side, out Vector3 end, out float len)
	{
		end = Vector3.zero;
		len = 0f;
		Vector3 position = d.transform.position;
		Vector3 to = position + d.transform.forward * (1.4f * (float)side);
		s_tmp.Clear();
		if (!GetPath(from, to, s_tmp, Pathfinding.AgentType.Humanoid, requireFull: true))
		{
			return false;
		}
		end = s_tmp[s_tmp.Count - 1];
		if (MHR_Nav.DoorDist(end, position) > 2.2f)
		{
			return false;
		}
		Vector3 a = from;
		for (int i = 0; i < s_tmp.Count; i++)
		{
			len += Vector3.Distance(a, s_tmp[i]);
			a = s_tmp[i];
		}
		return true;
	}

	public static bool FindHopBridge(Vector3 pos, Vector3 goal, out Vector3 bridge)
	{
		bridge = Vector3.zero;
		float num = float.MaxValue;
		int num2 = 0;
		float[] array = new float[5] { 3f, 6f, 10f, 15f, 20f };
		for (int i = 0; i < array.Length; i++)
		{
			for (int j = 0; j < 12; j++)
			{
				Vector3 vector = Quaternion.Euler(0f, (float)j * 30f, 0f) * Vector3.forward;
				Vector3 vector2 = goal + vector * array[i];
				if (!Physics.Raycast(new Vector3(vector2.x, goal.y + 1.5f, vector2.z), Vector3.down, out var hitInfo, 12f, Mask()) || MHR_Water.DepthAt(hitInfo.point) > 0.8f)
				{
					continue;
				}
				Vector3 point = hitInfo.point;
				if (!HopLine(point, goal, out var _))
				{
					continue;
				}
				if (num2++ >= 24)
				{
					break;
				}
				s_tmp.Clear();
				if (!GetPath(pos, point, s_tmp, MHR_CompanionNavMesh.AgentOrHumanoid, requireFull: true))
				{
					continue;
				}
				Vector3 a = s_tmp[s_tmp.Count - 1];
				if (!(Flat(a, point) > 1f) && !(Mathf.Abs(a.y - point.y) > 1.2f))
				{
					float num3 = 0f;
					Vector3 a2 = pos;
					for (int k = 0; k < s_tmp.Count; k++)
					{
						num3 += Vector3.Distance(a2, s_tmp[k]);
						a2 = s_tmp[k];
					}
					num3 += array[i];
					if (num3 < num)
					{
						num = num3;
						bridge = point;
					}
				}
			}
			if (num < float.MaxValue)
			{
				break;
			}
		}
		return num < float.MaxValue;
	}

	public static bool Reachable(Vector3 pos, Vector3 goal, float near)
	{
		try
		{
			if (GetPath(pos, goal, s_reachPath, Pathfinding.AgentType.Humanoid, requireFull: true) && Vector3.Distance(s_reachPath[s_reachPath.Count - 1], goal) <= near)
			{
				return true;
			}
			return CanGetTo(pos, goal);
		}
		catch
		{
			return false;
		}
	}

	public static bool CanGetTo(Vector3 pos, Vector3 goal)
	{
		try
		{
			if (Flat(pos, goal) < 25f && HopLine(pos, goal, out var _))
			{
				return true;
			}
			if (FindHopBridge(pos, goal, out var bridge))
			{
				return true;
			}
			return RouteDoor(pos, goal, out bridge, null) != null;
		}
		catch
		{
			return false;
		}
	}

	private static bool YouWentThrough(CompanionAI ai, Door d)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || d == null || Vector3.Distance(localPlayer.transform.position, d.transform.position) > 25f)
		{
			return false;
		}
		Vector3 forward = d.transform.forward;
		forward.y = 0f;
		if (forward.sqrMagnitude < 0.01f)
		{
			return false;
		}
		float f = Vector3.Dot(ai.transform.position - d.transform.position, forward);
		float f2 = Vector3.Dot(localPlayer.transform.position - d.transform.position, forward);
		if (Mathf.Abs(f2) > 0.5f)
		{
			return Mathf.Sign(f) != Mathf.Sign(f2);
		}
		return false;
	}

	private static void OpenDoor(CompanionAI ai, Character c, Door d, string tag)
	{
		if (d == null || !DoorClosed(d))
		{
			return;
		}
		if (tag == "Combat" && !YouWentThrough(ai, d))
		{
			S s = Get(ai);
			if (!s.DoorBlocked && CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] closed " + d.m_name + " in the way at " + Fmt(d.transform.position) + " - not opening it in a fight");
			}
			s.DoorBlocked = true;
			s.GaveUp = true;
			s.GaveUpAt = Time.time;
			return;
		}
		Vector3 vector = d.transform.position - ai.transform.position;
		vector.y = 0f;
		if (vector.sqrMagnitude > 0.01f)
		{
			ai.transform.rotation = Quaternion.LookRotation(vector.normalized);
		}
		Humanoid humanoid = c as Humanoid;
		bool flag = false;
		try
		{
			flag = humanoid != null && d.Interact(humanoid, hold: false, alt: false);
		}
		catch
		{
			flag = false;
		}
		if (!flag)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg("[Move|" + tag + "] couldn't open " + d.m_name);
			}
			return;
		}
		int instanceID = ai.gameObject.GetInstanceID();
		if (!s_opened.TryGetValue(instanceID, out var value))
		{
			value = new List<Opened>();
			s_opened[instanceID] = value;
		}
		value.Add(new Opened
		{
			Door = d,
			At = Time.time
		});
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg("[Move|" + tag + "] opened " + d.m_name + " at " + Fmt(d.transform.position));
		}
	}

	public static void Tick(Component cs, CompanionAI ai)
	{
		if (cs == null || ai == null)
		{
			return;
		}
		try
		{
			S s = Peek(ai);
			if (s != null && s.Walking && Time.time - s.LastGo > 0.5f)
			{
				Character component = ai.GetComponent<Character>();
				if (component != null)
				{
					component.SetWalk(walk: false);
				}
				s.Walking = false;
			}
			if (!s_opened.TryGetValue(ai.gameObject.GetInstanceID(), out var value) || value.Count == 0)
			{
				return;
			}
			Vector3 position = ai.transform.position;
			for (int num = value.Count - 1; num >= 0; num--)
			{
				Opened opened = value[num];
				if (opened.Door == null || DoorClosed(opened.Door) || Time.time - opened.At > 60f)
				{
					value.RemoveAt(num);
				}
				else if (!(Time.time - opened.At < 1.5f))
				{
					Vector3 position2 = opened.Door.transform.position;
					if ((!(Flat(position, position2) < 3.5f) || !(Mathf.Abs(position.y - position2.y) < 3f)) && (s == null || !(Time.time - s.LastGo < 0.5f) || (s.Ladder <= 0 && !s.HasDetour) || !(Flat(position, position2) < 8f)) && (!(Player.m_localPlayer != null) || !(Vector3.Distance(Player.m_localPlayer.transform.position, position2) < 2.5f)) && !AnyCharacterIn(position2, 1.2f))
					{
						Humanoid component2 = ai.GetComponent<Humanoid>();
						try
						{
							if (component2 != null)
							{
								opened.Door.Interact(component2, hold: false, alt: false);
							}
						}
						catch
						{
						}
						if (CompanionsPlugin.LogDetailed)
						{
							Dbg("[Move] closed " + opened.Door.m_name + " behind us");
						}
						value.RemoveAt(num);
					}
				}
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null && Time.time >= s_nextErr)
			{
				s_nextErr = Time.time + 30f;
				CompanionsPlugin.Log.LogWarning("[Move] " + ex.Message);
			}
		}
	}

	private static bool AnyCharacterIn(Vector3 p, float r)
	{
		int num = Physics.OverlapSphereNonAlloc(p, r, s_cols, LayerMask.GetMask("character", "character_net", "character_ghost"));
		for (int i = 0; i < num; i++)
		{
			if (s_cols[i] != null && s_cols[i].GetComponentInParent<Character>() != null)
			{
				return true;
			}
		}
		return false;
	}

	public static void FollowWatch(CompanionAI ai, GameObject target)
	{
		try
		{
			if (ai == null || target == null || ModConfig.FollowStuckTeleport == null || !ModConfig.FollowStuckTeleport.Value)
			{
				return;
			}
			float time = Time.time;
			int instanceID = ai.gameObject.GetInstanceID();
			if (!s_follow.TryGetValue(instanceID, out var value))
			{
				value = new F
				{
					Anchor = ai.transform.position,
					AnchorTime = time
				};
				s_follow[instanceID] = value;
			}
			Vector3 position = ai.transform.position;
			Vector3 position2 = target.transform.position;
			if (Vector3.Distance(position, position2) < 10f || Vector3.Distance(position, value.Anchor) > 1.5f || ai.IsInCombat)
			{
				value.Anchor = position;
				value.AnchorTime = time;
			}
			else
			{
				if (time - value.AnchorTime < 15f)
				{
					return;
				}
				value.Anchor = position;
				value.AnchorTime = time;
				if (Physics.Linecast(position + Vector3.up * 1.6f, position2 + Vector3.up * 1.6f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
				{
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Move|Follow] stuck for 15s out of sight at " + Fmt(position) + " - catching up");
					}
					ai.TeleportToFollowTarget();
				}
			}
		}
		catch
		{
		}
	}

	public static bool CanSee(Vector3 a, Vector3 b)
	{
		return !Physics.Linecast(a + Vector3.up * 1.6f, b + Vector3.up * 1.6f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain"));
	}

	private static void Log(S s, Vector3 pos, float remain, float now, Character c)
	{
		if (!(now < s.NextLog) && CompanionsPlugin.Log != null)
		{
			s.NextLog = now + 3f;
			float num = now - s.AnchorTime;
			float num2 = now - s.ProgressTime;
			string text = ((!s.PathOk) ? "none" : ((s.Partial ? "partial" : "full") + " (" + s.Path.Count + " corners)"));
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Move|" + s.Tag + "] at " + Fmt(pos) + " -> " + Fmt(s.Goal) + " " + remain.ToString("F1") + "m left, route=" + text + ((s.RouteDoor != null) ? ", via door" : "") + (s.HasDetour ? ", detour" : "") + (c.IsSwimming() ? ", swimming" : "") + ((num2 > 3f) ? (", no closer for " + num2.ToString("F0") + "s") : "") + ((num > 2f) ? (", standing still " + num.ToString("F0") + "s") : "") + ((s.Ladder > 0) ? (", unstuck step " + s.Ladder) : ""));
			}
		}
	}
}
