using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_Follow
{
	private class S
	{
		public int TargetId;

		public bool HasSpot;

		public bool Trail;

		public float Angle;

		public float Radius;

		public float SpotDy;

		public float NextPick;

		public float NextCheck;

		public Vector3 Spot;

		public bool Moving;

		public float LastUpdate;

		public bool HasIdle;

		public bool IdleMoving;

		public Vector3 IdleAnchor;

		public Vector3 IdleGoal;

		public float NextIdle;

		public bool Regrouped;

		public float AimLog;

		public float SettleDelay;

		public bool UnloadAsked;

		public float UnloadAskedAt;

		public Chair Chair;

		public bool GoingToChair;

		public float ChairUntil;

		public float ChairFailUntil;

		public float NextChairLook;

		public bool SitChair;

		public bool SitFloor;

		public Vector3 SitPlayerPos;

		public string ChairAnim;

		public float OutOfSightSince;

		public Collider[] ChairCols;

		public bool IdleSeat;

		public bool OnPath;

		public bool PathApproach;

		public int PathIdx;

		public float PathProgressAt;

		public float NoPathUntil;

		public float PathJumpAt;

		public float NextPathLog;
	}

	private class T
	{
		public readonly List<Vector3> Trail = new List<Vector3>();

		public readonly List<Vector3> Steps = new List<Vector3>();

		public Vector3 Travel;

		public Vector3 LastPos;

		public float Speed;

		public float StillSince;

		public float MovingSince;

		public float NextIndoorCheck;

		public bool Indoors;

		public int Frame = -1;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static readonly Dictionary<int, T> s_t = new Dictionary<int, T>();

	private static readonly List<Piece> s_pieces = new List<Piece>();

	private static int s_ground = -1;

	private static int s_los = -1;

	private static float s_nextErr;

	private static string s_noChairWhy;

	private static int GroundMask()
	{
		if (s_ground == -1)
		{
			s_ground = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
		}
		return s_ground;
	}

	private static int LosMask()
	{
		if (s_los == -1)
		{
			s_los = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
		}
		return s_los;
	}

	private static void Dbg(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Follow] " + msg);
		}
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	private static float MaxDist(T t)
	{
		if (t.Indoors)
		{
			if (ModConfig.FollowIndoorDistance == null)
			{
				return 4f;
			}
			return ModConfig.FollowIndoorDistance.Value;
		}
		if (ModConfig.FollowMaxDistance == null)
		{
			return 10f;
		}
		return ModConfig.FollowMaxDistance.Value;
	}

	private static bool YouAimAt(Player player, Character c, float distP)
	{
		try
		{
			if (player == null || player != Player.m_localPlayer || distP > 10f)
			{
				return false;
			}
			Camera mainCamera = Utils.GetMainCamera();
			if (mainCamera == null)
			{
				return false;
			}
			Vector3 position = mainCamera.transform.position;
			Vector3 to = c.GetCenterPoint() - position;
			float magnitude = to.magnitude;
			if (magnitude < 0.1f || magnitude > 14f)
			{
				return false;
			}
			float num = Mathf.Atan2(0.6f, magnitude) * 57.29578f + 3f;
			if (Vector3.Angle(mainCamera.transform.forward, to) > num)
			{
				return false;
			}
			return !Physics.Linecast(position, c.GetCenterPoint(), LosMask());
		}
		catch
		{
			return false;
		}
	}

	private static S Get(Component c)
	{
		int instanceID = c.gameObject.GetInstanceID();
		if (!s_s.TryGetValue(instanceID, out var value))
		{
			value = new S();
			s_s[instanceID] = value;
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

	public static bool Owns(Component c)
	{
		S s = Peek(c);
		if (s != null)
		{
			return Time.time - s.LastUpdate < 0.5f;
		}
		return false;
	}

	private static T Track(GameObject target)
	{
		int instanceID = target.GetInstanceID();
		float time = Time.time;
		if (!s_t.TryGetValue(instanceID, out var value))
		{
			value = new T
			{
				StillSince = time,
				LastPos = target.transform.position
			};
			Vector3 forward = target.transform.forward;
			forward.y = 0f;
			value.Travel = ((forward.sqrMagnitude > 0.01f) ? forward.normalized : Vector3.forward);
			s_t[instanceID] = value;
		}
		if (value.Frame == Time.frameCount)
		{
			return value;
		}
		value.Frame = Time.frameCount;
		Vector3 position = target.transform.position;
		Vector3 vector = position - value.LastPos;
		vector.y = 0f;
		value.LastPos = position;
		if (vector.magnitude > 8f)
		{
			value.Trail.Clear();
			value.Steps.Clear();
			vector = Vector3.zero;
		}
		float num = Mathf.Max(Time.deltaTime, 0.001f);
		vector /= num;
		value.Speed = Mathf.Lerp(value.Speed, vector.magnitude, Mathf.Clamp01(num * 8f));
		if (value.Speed > 0.6f)
		{
			if (time - value.StillSince > 1f)
			{
				value.MovingSince = time;
			}
			value.StillSince = time;
			Vector3 vector2 = Vector3.Slerp(value.Travel, (vector.sqrMagnitude > 0.0001f) ? vector.normalized : value.Travel, Mathf.Clamp01(num * 2.5f));
			vector2.y = 0f;
			if (vector2.sqrMagnitude > 0.0001f)
			{
				value.Travel = vector2.normalized;
			}
		}
		if (value.Steps.Count == 0 || Vector3.Distance(position, value.Steps[value.Steps.Count - 1]) > 0.35f)
		{
			value.Steps.Add(position);
			if (value.Steps.Count > 400)
			{
				value.Steps.RemoveAt(0);
			}
		}
		if (value.Trail.Count == 0 || Flat(position, value.Trail[value.Trail.Count - 1]) > 0.75f)
		{
			value.Trail.Add(position);
			if (value.Trail.Count > 40)
			{
				value.Trail.RemoveAt(0);
			}
		}
		if (time >= value.NextIndoorCheck)
		{
			value.NextIndoorCheck = time + 0.5f;
			Player component = target.GetComponent<Player>();
			value.Indoors = component != null && component == Player.m_localPlayer && component.InShelter();
		}
		return value;
	}

	public static bool Update(CompanionAI ai, GameObject target, float dt)
	{
		if (ai == null || target == null || target.GetComponent<Player>() == null)
		{
			return false;
		}
		try
		{
			UpdateInner(ai, target, dt);
			return true;
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null && Time.time >= s_nextErr)
			{
				s_nextErr = Time.time + 30f;
				CompanionsPlugin.Log.LogWarning("[Follow] " + ex.Message);
			}
			return false;
		}
	}

	private static void UpdateInner(CompanionAI ai, GameObject target, float dt)
	{
		float time = Time.time;
		S s = Get(ai);
		T t = Track(target);
		Player component = target.GetComponent<Player>();
		Character component2 = ai.GetComponent<Character>();
		Vector3 position = ai.transform.position;
		Vector3 position2 = target.transform.position;
		float num = MaxDist(t);
		CompanionSetup component3 = ai.GetComponent<CompanionSetup>();
		bool flag = component3 == null || component3.GetWander();
		float num2 = ((Mathf.Abs(position.y - position2.y) > 3f) ? Vector3.Distance(position, position2) : Flat(position, position2));
		int instanceID = target.GetInstanceID();
		bool flag2 = Mathf.Abs(position.y - position2.y) > 2.5f && !component2.IsSwimming() && !component.IsSwimming();
		bool flag3 = MHR_Mover.OnTheWayUp(ai);
		if (time - s.LastUpdate > 0.5f || s.TargetId != instanceID)
		{
			s.HasSpot = false;
			s.Moving = num2 > num;
			s.HasIdle = false;
			s.IdleMoving = false;
			s.Regrouped = false;
			s.UnloadAsked = false;
			s.GoingToChair = false;
			s.Chair = null;
			s.SettleDelay = UnityEngine.Random.Range(5f, 10f);
		}
		s.LastUpdate = time;
		s.TargetId = instanceID;
		if (s.SitChair || s.SitFloor)
		{
			ai.StopMoving();
			if (s.SitChair)
			{
				Pin(ai, s);
			}
		}
		else
		{
			if (WalkPlayerPath(ai, s, t, component2, position, position2, time, dt, flag2))
			{
				return;
			}
			float num3 = time - t.StillSince;
			if (num3 < 1f)
			{
				s.Regrouped = false;
				s.UnloadAsked = false;
				s.HasIdle = false;
				s.IdleMoving = false;
				if (s.GoingToChair)
				{
					ReleaseChair(s);
				}
				if (!s.HasSpot || (time >= s.NextPick && (flag || s.Trail)))
				{
					PickSpot(ai, s, t, position2, num, regroup: false, 0f);
				}
				else if (time >= s.NextCheck)
				{
					s.NextCheck = time + 1f;
					Vector3 g;
					if (s.Trail)
					{
						PickSpot(ai, s, t, position2, num, regroup: false, 0f);
					}
					else if (!Ground(SpotXZ(s, t, position2), position2, out g) || !Los(position2, g))
					{
						PickSpot(ai, s, t, position2, num, regroup: false, 0f);
					}
					else
					{
						s.SpotDy = g.y - position2.y;
					}
				}
				Vector3 vector = Goal(ai, s, t, position2, instanceID);
				float num4 = Flat(position, vector);
				bool flag4 = time - t.MovingSince < 1.5f || (num2 < 2.5f && !component2.IsSwimming());
				if (!flag4 && time - t.MovingSince < 3.5f && !s.Moving && !component2.IsSwimming() && YouAimAt(component, component2, num2))
				{
					flag4 = true;
					if (time - s.AimLog > 5f)
					{
						s.AimLog = time;
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo("[Follow] " + component2.m_name + " stays put - your crosshair is on it");
						}
					}
				}
				if (!s.Moving && ((!flag4 && ((num4 > (t.Indoors ? 2.5f : 4f) || num2 > num || component2.IsSwimming()) | flag2)) | flag3))
				{
					s.Moving = true;
				}
				if (s.Moving)
				{
					Vector3 vector2 = (s.Trail ? vector : (vector + t.Travel * (t.Indoors ? 1f : Mathf.Max(1.5f, Mathf.Min(t.Speed, 8f)))));
					if (!s.Trail && !Los(vector, vector2))
					{
						vector2 = vector;
					}
					int num5 = Pace(component, num4, num2, num);
					if (!s.Trail && num4 < 2f && Vector3.Dot(position - vector, t.Travel) > 0.5f && num5 > 1)
					{
						num5--;
					}
					MHR_Mover.Go(ai, dt, vector2, 0.3f, allowRun: true, "Follow", num5);
					if (!s.Trail && MHR_Mover.RoutePartial(ai) && num2 > 3f)
					{
						s.Trail = true;
						s.NextPick = time + 4f;
						s.NextCheck = time + 4f;
					}
					if (!s.Trail && !component.IsSwimming() && !component2.IsSwimming() && MHR_Mover.RouteSwim(ai) > 1f)
					{
						s.Trail = true;
						s.NextPick = time + 6f;
						s.NextCheck = time + 6f;
					}
					if (!s.Trail && num2 > 3f && MHR_Mover.RouteLength(ai) > Flat(position, vector2) * 1.8f + 8f)
					{
						s.Trail = true;
						s.NextPick = time + 6f;
						s.NextCheck = time + 6f;
					}
					if (MHR_Mover.GaveUp(ai))
					{
						ai.MHR_FollowFallback(target, dt);
					}
				}
				else
				{
					ai.StopMoving();
				}
				ai.MHR_SetCrouch(component.IsCrouching());
				return;
			}
			ai.MHR_SetCrouch(component.IsCrouching());
			if ((num2 > num || component2.IsSwimming()) | flag2 | flag3)
			{
				if (!s.HasSpot || s.Radius > num * 0.7f)
				{
					PickSpot(ai, s, t, position2, num, regroup: false, num * 0.6f);
				}
				Vector3 goal = Goal(ai, s, t, position2, instanceID);
				s.Moving = true;
				s.HasIdle = false;
				s.IdleMoving = false;
				MHR_Mover.Go(ai, dt, goal, 1f, allowRun: true, "Follow", (num2 > num + 6f) ? 3 : 2);
				if (MHR_Mover.GaveUp(ai))
				{
					if (num2 > num || component2.IsSwimming())
					{
						ai.MHR_FollowFallback(target, dt);
					}
					else
					{
						ai.StopMoving();
					}
				}
				return;
			}
			if (s.Moving || !s.HasIdle)
			{
				s.Moving = false;
				ai.StopMoving();
				s.HasIdle = true;
				s.IdleAnchor = position;
				s.IdleMoving = false;
				s.NextIdle = time + UnityEngine.Random.Range(3f, 6f);
			}
			if (ModConfig.FollowRegroup != null && ModConfig.FollowRegroup.Value && !s.Regrouped && num3 >= ModConfig.FollowRegroupTime.Value)
			{
				s.Regrouped = true;
				PickSpot(ai, s, t, position2, num, regroup: true, 0f);
				s.IdleGoal = Goal(ai, s, t, position2, instanceID);
				s.IdleAnchor = s.IdleGoal;
				s.IdleMoving = true;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg(component2.m_name + " regrouping after " + num3.ToString("F0") + "s");
				}
			}
			if (t.Indoors && !s.IdleMoving && ModConfig.FollowSitIndoors != null && ModConfig.FollowSitIndoors.Value && num3 >= s.SettleDelay && component2.IsOnGround() && !component2.IsSwimming() && Settle(ai, s, component2, position2, num, dt, time))
			{
				return;
			}
			if (!flag && !s.IdleMoving)
			{
				ai.StopMoving();
				return;
			}
			if (s.IdleMoving)
			{
				if (MHR_Mover.Go(ai, dt, s.IdleGoal, 0.6f, allowRun: false, "Follow", 1) || MHR_Mover.GaveUp(ai))
				{
					s.IdleMoving = false;
					s.NextIdle = time + UnityEngine.Random.Range(6f, 14f);
					MHR_Mover.Release(ai);
					ai.LookAtPoint(position2 + Vector3.up * 1.5f);
				}
				return;
			}
			ai.StopMoving();
			if (time >= s.NextIdle)
			{
				s.NextIdle = time + UnityEngine.Random.Range(6f, 14f);
				if (UnityEngine.Random.value < 0.35f || !IdlePoint(ai, s, position2, num, t.Indoors ? 1f : 2f, instanceID, out var g2))
				{
					ai.LookAtPoint(position2 + Vector3.up * 1.5f);
					return;
				}
				s.IdleGoal = g2;
				s.IdleMoving = true;
			}
		}
	}

	private static bool WalkPlayerPath(CompanionAI ai, S s, T t, Character c, Vector3 me, Vector3 pp, float now, float dt, bool offLevel)
	{
		List<Vector3> steps = t.Steps;
		if (s.OnPath && !c.IsSwimming() && Mathf.Abs(me.y - pp.y) > 1.2f)
		{
			offLevel = true;
		}
		if (!offLevel || c.IsSwimming() || steps.Count < 3 || Mathf.Abs(steps[steps.Count - 1].y - pp.y) > 1.5f)
		{
			if (s.OnPath)
			{
				EndPath(s, "");
			}
			return false;
		}
		if (!s.OnPath)
		{
			if (now < s.NoPathUntil)
			{
				return false;
			}
			if (!MHR_Mover.RoutePartial(ai) && !MHR_Mover.GaveUp(ai) && !MHR_Mover.OnTheWayUp(ai))
			{
				return false;
			}
			if (MHR_Mover.OnLadderChain(ai))
			{
				return false;
			}
			int num = -1;
			float num2 = 0f;
			for (int num3 = steps.Count - 2; num3 >= 0; num3--)
			{
				if (Mathf.Abs(steps[num3].y - me.y) <= 0.5f && Flat(me, steps[num3]) <= 1.5f)
				{
					num = num3;
					num2 = Flat(me, steps[num3]);
					break;
				}
			}
			for (int num4 = ((num >= 0) ? (-1) : (steps.Count - 2)); num4 >= 0; num4--)
			{
				if (!(Mathf.Abs(steps[num4].y - me.y) > 0.5f))
				{
					float num5 = Flat(me, steps[num4]);
					if (!(num5 > 12f))
					{
						num = num4;
						num2 = num5;
						break;
					}
				}
			}
			if (num < 0)
			{
				if (now >= s.NextPathLog)
				{
					s.NextPathLog = now + 15f;
					if (CompanionsPlugin.LogDetailed)
					{
						Dbg(c.m_name + " can't follow your path: none of your last " + steps.Count + " steps is on its level within 12 m (came by portal, or the path is older than that)");
					}
				}
				return false;
			}
			s.OnPath = true;
			s.PathIdx = num;
			s.PathProgressAt = now;
			s.PathApproach = num2 > 1f;
			MHR_Mover.Release(ai);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Follow] " + c.m_name + " is " + Mathf.Abs(pp.y - me.y).ToString("F1") + "m " + ((pp.y > me.y) ? "below" : "above") + " you - following your path (" + (steps.Count - num) + " steps, starting " + num2.ToString("F1") + "m away)");
			}
		}
		if (s.PathIdx >= steps.Count)
		{
			s.PathIdx = steps.Count - 1;
		}
		if (s.PathApproach)
		{
			Vector3 vector = steps[s.PathIdx];
			if (!(Flat(me, vector) < 0.6f) || !(Mathf.Abs(me.y - vector.y) < 1f))
			{
				MHR_Mover.Go(ai, dt, vector, 0.4f, allowRun: true, "Follow", 2);
				if (MHR_Mover.GaveUp(ai) || now - s.PathProgressAt > 15f)
				{
					EndPath(s, "couldn't get to where your path starts");
					s.NoPathUntil = now + 10f;
					MHR_Mover.Release(ai);
					return false;
				}
				return true;
			}
			s.PathApproach = false;
			s.PathProgressAt = now;
			MHR_Mover.Release(ai);
		}
		while (s.PathIdx < steps.Count - 1 && Flat(me, steps[s.PathIdx]) < 0.5f && me.y > steps[s.PathIdx].y - 1f)
		{
			s.PathIdx++;
			s.PathProgressAt = now;
		}
		for (int num6 = Mathf.Min(s.PathIdx + 10, steps.Count - 1); num6 > s.PathIdx; num6--)
		{
			if (Flat(me, steps[num6]) < 0.6f && Mathf.Abs(me.y - steps[num6].y) < 0.8f)
			{
				s.PathIdx = num6;
				s.PathProgressAt = now;
				break;
			}
		}
		Vector3 vector2 = steps[s.PathIdx];
		if (me.y < vector2.y - 2.5f && c.IsOnGround())
		{
			EndPath(s, "fell off your path");
			s.NoPathUntil = now + 3f;
			return false;
		}
		if (now - s.PathProgressAt > 6f)
		{
			EndPath(s, "stuck on your path at " + vector2.ToString("F1"));
			s.NoPathUntil = now + 15f;
			return false;
		}
		Vector3 vector3 = vector2 - me;
		vector3.y = 0f;
		if (vector3.sqrMagnitude < 0.04f && s.PathIdx < steps.Count - 1)
		{
			vector3 = steps[s.PathIdx + 1] - vector2;
			vector3.y = 0f;
		}
		if (vector3.sqrMagnitude < 0.0001f)
		{
			vector3 = ai.transform.forward;
		}
		vector3.Normalize();
		if (c.IsOnGround() && vector2.y < me.y - 1f)
		{
			float num7 = CompanionFallDamagePatch.SafeDrop(c, 0.3f);
			if (!Physics.Raycast(me + vector3 * 0.7f + Vector3.up * 1f, Vector3.down, 1f + num7, GroundMask()))
			{
				ai.StopMoving();
				EndPath(s, "you went down a drop of more than " + num7.ToString("F1") + "m there - too far for me");
				s.NoPathUntil = now + 15f;
				return false;
			}
		}
		ai.transform.rotation = Quaternion.LookRotation(vector3);
		c.SetWalk(walk: false);
		ai.MoveTowards(vector3, run: false);
		if (vector2.y > me.y + 0.6f && c.IsOnGround() && now >= s.PathJumpAt && now - s.PathProgressAt > 1f)
		{
			Vector3 vector4 = me + vector3 * 0.6f;
			if (!Physics.Raycast(new Vector3(vector4.x, me.y + 2.5f, vector4.z), Vector3.down, out var hitInfo, 3f, GroundMask()) || !(hitInfo.point.y > vector2.y + 0.5f))
			{
				c.Jump();
			}
			s.PathJumpAt = now + 1f;
		}
		if (now >= s.NextPathLog)
		{
			s.NextPathLog = now + 3f;
			if (CompanionsPlugin.LogDetailed)
			{
				Dbg(c.m_name + " on your path: step " + s.PathIdx + "/" + (steps.Count - 1) + " at " + vector2.ToString("F1") + ", me " + me.ToString("F1"));
			}
		}
		return true;
	}

	internal static bool StepsOnto(GameObject player, Ship ship, out Vector3 from, out Vector3 to)
	{
		from = (to = Vector3.zero);
		if (player == null || ship == null || !s_t.TryGetValue(player.GetInstanceID(), out var value) || value.Steps.Count < 2)
		{
			return false;
		}
		int mask = LayerMask.GetMask("vehicle");
		List<Vector3> steps = value.Steps;
		bool flag = OnShip(steps[steps.Count - 1], ship, mask);
		for (int num = steps.Count - 2; num >= 0; num--)
		{
			bool flag2 = OnShip(steps[num], ship, mask);
			if (flag && !flag2)
			{
				from = steps[num];
				to = steps[num + 1];
				return true;
			}
			flag = flag2;
		}
		return false;
	}

	private static bool OnShip(Vector3 p, Ship ship, int vehicle)
	{
		if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 1.5f, vehicle))
		{
			return false;
		}
		return ((hitInfo.collider.attachedRigidbody != null) ? hitInfo.collider.attachedRigidbody.GetComponent<Ship>() : hitInfo.collider.GetComponentInParent<Ship>()) == ship;
	}

	private static void EndPath(S s, string why)
	{
		s.OnPath = false;
		s.PathApproach = false;
		if (why.Length > 0 && CompanionsPlugin.Log != null && CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Follow] stopped following your path: " + why);
		}
	}

	private static int Pace(Player p, float dSpot, float distP, float maxD)
	{
		if (distP > maxD + 4f || dSpot > 8f)
		{
			return 3;
		}
		int num = (p.IsRunning() ? 3 : ((p.IsCrouching() || p.GetWalk()) ? 1 : 2));
		if (dSpot > 3f && num < 3)
		{
			num++;
		}
		return num;
	}

	private static Vector3 SpotXZ(S s, T t, Vector3 pp)
	{
		Vector3 result = pp + Quaternion.Euler(0f, s.Angle, 0f) * t.Travel * s.Radius;
		result.y = pp.y + s.SpotDy;
		return result;
	}

	private static Vector3 Goal(CompanionAI ai, S s, T t, Vector3 pp, int tid)
	{
		if (s.Trail)
		{
			return TrailPoint(t, pp, 2f + (float)Rank(ai, tid) * 1.5f);
		}
		s.Spot = SpotXZ(s, t, pp);
		return s.Spot;
	}

	private static float RandomAngle(bool anywhere)
	{
		float num = ((UnityEngine.Random.value < 0.5f) ? (-1f) : 1f);
		if (anywhere)
		{
			return UnityEngine.Random.Range(0f, 360f);
		}
		float value = UnityEngine.Random.value;
		if (value < 0.15f)
		{
			return num * UnityEngine.Random.Range(20f, 60f);
		}
		if (value < 0.45f)
		{
			return num * UnityEngine.Random.Range(60f, 100f);
		}
		return num * UnityEngine.Random.Range(100f, 170f);
	}

	private static bool PickSpot(CompanionAI ai, S s, T t, Vector3 pp, float maxD, bool regroup, float maxRadius)
	{
		float time = Time.time;
		float num;
		float num2;
		if (regroup)
		{
			num = (t.Indoors ? 1.5f : 2f);
			num2 = (t.Indoors ? 2.5f : 4f);
		}
		else
		{
			num = (t.Indoors ? 1.5f : 3f);
			num2 = Mathf.Max(num + 0.5f, maxD * 0.7f);
		}
		if (maxRadius > 0f)
		{
			num2 = Mathf.Max(num, Mathf.Min(num2, maxRadius));
		}
		float spacing = (t.Indoors ? 1.5f : 3f);
		int targetId = s.TargetId;
		for (int i = 0; i < 10; i++)
		{
			float num3 = RandomAngle(regroup);
			float num4 = UnityEngine.Random.Range(num, num2);
			if (Ground(pp + Quaternion.Euler(0f, num3, 0f) * t.Travel * num4, pp, out var g) && !Crowded(ai, targetId, g, spacing) && Los(pp, g))
			{
				s.Angle = num3;
				s.Radius = num4;
				s.SpotDy = g.y - pp.y;
				s.Spot = g;
				s.HasSpot = true;
				s.Trail = false;
				s.NextPick = time + UnityEngine.Random.Range(10f, 25f);
				s.NextCheck = time + 1f;
				return true;
			}
		}
		s.HasSpot = true;
		s.Trail = true;
		s.NextPick = time + 3f;
		s.NextCheck = time + 1f;
		return false;
	}

	private static bool Ground(Vector3 w, Vector3 pp, out Vector3 g)
	{
		g = w;
		if (!Physics.Raycast(new Vector3(w.x, pp.y + 2.5f, w.z), Vector3.down, out var hitInfo, 6f, GroundMask()))
		{
			return false;
		}
		if (hitInfo.normal.y < 0.6f || Mathf.Abs(hitInfo.point.y - pp.y) > 2.5f)
		{
			return false;
		}
		if (MHR_Water.DepthAt(hitInfo.point) > 1f)
		{
			return false;
		}
		if (MHR_Mover.NearFire(hitInfo.point, 1.2f))
		{
			return false;
		}
		g = hitInfo.point;
		return true;
	}

	private static bool Los(Vector3 from, Vector3 to)
	{
		return !Physics.Linecast(from + Vector3.up * 1.6f, to + Vector3.up * 1f, LosMask());
	}

	private static bool Crowded(CompanionAI ai, int tid, Vector3 g, float spacing)
	{
		int instanceID = ai.gameObject.GetInstanceID();
		float time = Time.time;
		foreach (KeyValuePair<int, S> s_ in s_s)
		{
			S value = s_.Value;
			if (s_.Key != instanceID && value.TargetId == tid && !(time - value.LastUpdate > 2f) && value.HasSpot && !value.Trail && Flat(g, value.Spot) < spacing)
			{
				return true;
			}
		}
		return false;
	}

	private static int Rank(CompanionAI ai, int tid)
	{
		int instanceID = ai.gameObject.GetInstanceID();
		int num = 0;
		float time = Time.time;
		foreach (KeyValuePair<int, S> s_ in s_s)
		{
			if (s_.Key != instanceID && s_.Key < instanceID && s_.Value.TargetId == tid && s_.Value.Trail && time - s_.Value.LastUpdate < 2f)
			{
				num++;
			}
		}
		return num;
	}

	private static Vector3 TrailPoint(T t, Vector3 pp, float back)
	{
		float num = 0f;
		Vector3 a = pp;
		for (int num2 = t.Trail.Count - 1; num2 >= 0; num2--)
		{
			num += Vector3.Distance(a, t.Trail[num2]);
			a = t.Trail[num2];
			if (num >= back)
			{
				return t.Trail[num2];
			}
		}
		if (t.Trail.Count <= 0)
		{
			return pp - t.Travel * back;
		}
		return t.Trail[0];
	}

	private static bool IdlePoint(CompanionAI ai, S s, Vector3 pp, float maxD, float range, int tid, out Vector3 g)
	{
		g = Vector3.zero;
		for (int i = 0; i < 6; i++)
		{
			Vector2 vector = UnityEngine.Random.insideUnitCircle * range;
			if (Ground(s.IdleAnchor + new Vector3(vector.x, 0f, vector.y), pp, out g))
			{
				float num = Flat(g, pp);
				if (!(num > maxD) && !(num < 1.2f) && !Crowded(ai, tid, g, 1.2f) && Los(s.IdleAnchor, g))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool Settle(CompanionAI ai, S s, Character c, Vector3 pp, float maxD, float dt, float now)
	{
		CompanionSetup component = ai.GetComponent<CompanionSetup>();
		if (component == null || !component.CanWearArmor())
		{
			return false;
		}
		if (!s.UnloadAsked)
		{
			s.UnloadAsked = true;
			s.UnloadAskedAt = now;
			MHR_ToolKeeper.RequestUnload(component);
			return false;
		}
		if (now - s.UnloadAskedAt < 2.5f || MHR_ToolKeeper.IsBusy(component))
		{
			return false;
		}
		RepairController component2 = ai.GetComponent<RepairController>();
		if (component2 != null && component2.IsActive)
		{
			return false;
		}
		if (s.GoingToChair)
		{
			if (!(s.Chair == null) && !ChairTaken(s.Chair, ai) && !(now > s.ChairUntil) && !MHR_Mover.GaveUp(ai))
			{
				Vector3 position = s.Chair.m_attachPoint.position;
				if (Vector3.Distance(ai.transform.position, position) < 1.3f)
				{
					SitInChair(ai, s, s.Chair, pp);
				}
				else
				{
					MHR_Mover.Go(ai, dt, position, 0.9f, allowRun: false, "Follow", 1);
				}
				return true;
			}
			ReleaseChair(s);
			s.ChairFailUntil = now + 20f;
			MHR_Mover.Release(ai);
		}
		if (now >= s.ChairFailUntil && now >= s.NextChairLook)
		{
			s.NextChairLook = now + 3f;
			Chair chair = FindChair(ai, pp, Mathf.Max(maxD + 1.5f, ModConfig.FollowChairRange?.Value ?? 8f));
			if (chair != null)
			{
				s.Chair = chair;
				s.GoingToChair = true;
				s.ChairUntil = now + 15f;
				if (CompanionsPlugin.LogDetailed)
				{
					Dbg(c.m_name + " heading for " + chair.m_name + " at " + MHR_Mover.Fmt(chair.m_attachPoint.position));
				}
				return true;
			}
		}
		if (Flat(ai.transform.position, pp) <= maxD)
		{
			SitOnFloor(ai, s, pp);
			return true;
		}
		return false;
	}

	private static bool ChairTaken(Chair ch, CompanionAI ai)
	{
		if (ch == null || ch.m_attachPoint == null)
		{
			return true;
		}
		try
		{
			if (ch.IsInUse())
			{
				return true;
			}
		}
		catch
		{
		}
		int instanceID = ai.gameObject.GetInstanceID();
		float time = Time.time;
		foreach (KeyValuePair<int, S> s_ in s_s)
		{
			if (s_.Key != instanceID && s_.Value.Chair == ch && (s_.Value.SitChair || s_.Value.GoingToChair) && time - s_.Value.LastUpdate < 5f)
			{
				return true;
			}
		}
		return false;
	}

	private static Chair FindChair(CompanionAI ai, Vector3 pp, float range)
	{
		s_pieces.Clear();
		Piece.GetAllPiecesInRadius(pp, range, s_pieces);
		Vector3 position = ai.transform.position;
		Chair chair = null;
		float num = float.MaxValue;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		for (int i = 0; i < s_pieces.Count; i++)
		{
			Piece piece = s_pieces[i];
			if (piece == null)
			{
				continue;
			}
			Chair[] componentsInChildren = piece.GetComponentsInChildren<Chair>();
			foreach (Chair chair2 in componentsInChildren)
			{
				if (chair2 == null || chair2.m_inShip || chair2.m_attachPoint == null)
				{
					continue;
				}
				Vector3 position2 = chair2.m_attachPoint.position;
				if (Flat(position2, pp) > range)
				{
					continue;
				}
				num2++;
				if (Mathf.Abs(position2.y - pp.y) > 2f)
				{
					num3++;
					continue;
				}
				if (ChairTaken(chair2, ai))
				{
					num4++;
					continue;
				}
				if (Physics.Linecast(pp + Vector3.up * 1.6f, position2 + Vector3.up * 1.2f, out var hitInfo, LosMask()) && hitInfo.collider.GetComponentInParent<Piece>() != piece)
				{
					num5++;
					continue;
				}
				float num6 = Vector3.Distance(position, position2);
				if (num6 < num)
				{
					num = num6;
					chair = chair2;
				}
			}
		}
		string text;
		if (chair != null)
		{
			text = null;
		}
		else
		{
			text = ((num2 == 0) ? $"no chair within {range:F0} m of you" : $"{num2} chair(s) within {range:F0} m: {num3} on another level, {num4} in use, {num5} out of your sight");
		}
		s_noChairWhy = text;
		return chair;
	}

	private static void SitInChair(CompanionAI ai, S s, Chair ch, Vector3 pp)
	{
		MHR_Mover.Release(ai);
		ai.StopMoving();
		s.GoingToChair = false;
		s.SitChair = true;
		s.SitPlayerPos = pp;
		s.ChairAnim = ch.m_attachAnimation;
		s.OutOfSightSince = 0f;
		Piece componentInParent = ch.GetComponentInParent<Piece>();
		s.ChairCols = ((componentInParent != null) ? componentInParent.GetComponentsInChildren<Collider>() : ch.GetComponentsInChildren<Collider>());
		IgnoreChair(ai, s, ignore: true);
		Pin(ai, s);
		ZSyncAnimation component = ai.GetComponent<ZSyncAnimation>();
		if (component != null && !string.IsNullOrEmpty(s.ChairAnim))
		{
			component.SetBool(s.ChairAnim, value: true);
		}
		SetResting(ai, on: true);
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg(ai.GetComponent<Character>().m_name + " sat down on " + ch.m_name);
		}
	}

	private static void Pin(CompanionAI ai, S s)
	{
		if (!(s.Chair == null) && !(s.Chair.m_attachPoint == null))
		{
			Transform attachPoint = s.Chair.m_attachPoint;
			ai.transform.position = attachPoint.position;
			ai.transform.rotation = attachPoint.rotation;
			Rigidbody component = ai.GetComponent<Rigidbody>();
			if (component != null && !component.isKinematic)
			{
				component.position = attachPoint.position;
				component.linearVelocity = Vector3.zero;
				component.useGravity = false;
			}
		}
	}

	private static void IgnoreChair(CompanionAI ai, S s, bool ignore)
	{
		Collider component = ai.GetComponent<CapsuleCollider>();
		if (component == null || s.ChairCols == null)
		{
			return;
		}
		for (int i = 0; i < s.ChairCols.Length; i++)
		{
			if (s.ChairCols[i] != null)
			{
				Physics.IgnoreCollision(component, s.ChairCols[i], ignore);
			}
		}
		if (!ignore)
		{
			s.ChairCols = null;
		}
	}

	private static void SitOnFloor(CompanionAI ai, S s, Vector3 pp)
	{
		MHR_Mover.Release(ai);
		ai.StopMoving();
		Vector3 vector = pp - ai.transform.position;
		vector.y = 0f;
		if (vector.sqrMagnitude > 0.01f)
		{
			ai.transform.rotation = Quaternion.LookRotation(vector.normalized);
		}
		s.SitFloor = true;
		s.SitPlayerPos = pp;
		s.OutOfSightSince = 0f;
		ZSyncAnimation component = ai.GetComponent<ZSyncAnimation>();
		if (component != null)
		{
			component.SetTrigger("emote_sit");
			component.SetBool("emote_sit", value: true);
		}
		SetResting(ai, on: true);
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg(ai.GetComponent<Character>().m_name + " sat down on the floor" + ((s_noChairWhy != null) ? (" (" + s_noChairWhy + ")") : ""));
		}
	}

	private static void SetResting(CompanionAI ai, bool on)
	{
		CompanionStamina component = ai.GetComponent<CompanionStamina>();
		if (component != null)
		{
			component.IsResting = on;
		}
	}

	private static void ReleaseChair(S s)
	{
		s.GoingToChair = false;
		s.Chair = null;
	}

	internal static bool StandSpot(Vector3 seat, Vector3 fwd, Vector3 right, out Vector3 spot)
	{
		fwd.y = 0f;
		right.y = 0f;
		Vector3[] array = new Vector3[6]
		{
			fwd,
			right,
			-right,
			(fwd + right).normalized,
			(fwd - right).normalized,
			-fwd
		};
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].sqrMagnitude < 0.01f)
			{
				continue;
			}
			for (float num = 0.8f; num <= 1.6f; num += 0.4f)
			{
				if (Physics.Raycast(seat + array[i].normalized * num + Vector3.up * 1f, Vector3.down, out var hitInfo, 2.5f, GroundMask()) && !(hitInfo.normal.y <= 0.6f) && !(hitInfo.point.y > seat.y + 0.3f) && !(hitInfo.collider.GetComponentInParent<Fireplace>() != null) && !MHR_Chests.IsFurniture(hitInfo.collider) && !MHR_Mover.NearFire(hitInfo.point, 1.2f) && !Physics.CheckCapsule(hitInfo.point + Vector3.up * 0.6f, hitInfo.point + Vector3.up * 1.6f, 0.3f, GroundMask()))
				{
					spot = hitInfo.point + Vector3.up * 0.05f;
					return true;
				}
			}
		}
		spot = seat;
		return false;
	}

	private static void StandUp(CompanionAI ai, S s, bool keepAnim, string why)
	{
		ZSyncAnimation component = ai.GetComponent<ZSyncAnimation>();
		if (s.SitChair)
		{
			if (component != null && !string.IsNullOrEmpty(s.ChairAnim))
			{
				component.SetBool(s.ChairAnim, value: false);
			}
			Rigidbody component2 = ai.GetComponent<Rigidbody>();
			Vector3 position = ai.transform.position;
			if (s.Chair != null && s.Chair.m_attachPoint != null)
			{
				Transform attachPoint = s.Chair.m_attachPoint;
				position = ((!StandSpot(attachPoint.position, attachPoint.forward, attachPoint.right, out var spot)) ? attachPoint.TransformPoint(s.Chair.m_detachOffset) : spot);
			}
			ai.transform.position = position;
			IgnoreChair(ai, s, ignore: false);
			if (component2 != null && !component2.isKinematic)
			{
				component2.useGravity = true;
				component2.position = position;
				component2.linearVelocity = Vector3.zero;
			}
		}
		if (s.SitFloor && !keepAnim && component != null)
		{
			component.SetTrigger("emote_stop");
			component.SetBool("emote_sit", value: false);
		}
		if (!keepAnim)
		{
			SetResting(ai, on: false);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			Dbg(ai.GetComponent<Character>().m_name + " got up (" + why + ")");
		}
		s.SitChair = false;
		s.SitFloor = false;
		s.Chair = null;
		s.GoingToChair = false;
		s.IdleSeat = false;
		s.UnloadAsked = false;
		s.HasIdle = false;
		s.IdleMoving = false;
		s.SettleDelay = UnityEngine.Random.Range(5f, 10f);
	}

	internal static Chair IdleFindChair(CompanionAI ai, Vector3 center, float range)
	{
		return FindChair(ai, center, range);
	}

	internal static void IdleClaimChair(CompanionAI ai, Chair ch)
	{
		S s = Get(ai);
		s.Chair = ch;
		s.GoingToChair = true;
		s.LastUpdate = Time.time;
	}

	internal static void IdleSit(CompanionAI ai, Chair ch)
	{
		S s = Get(ai);
		s.Chair = ch;
		s.LastUpdate = Time.time;
		SitInChair(ai, s, ch, ai.transform.position);
		s.IdleSeat = true;
	}

	internal static void IdleSitFloor(CompanionAI ai, Vector3 face)
	{
		S s = Get(ai);
		SitOnFloor(ai, s, face);
		s.IdleSeat = true;
	}

	internal static bool IdleSeated(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.IdleSeat)
		{
			if (!s.SitChair)
			{
				return s.SitFloor;
			}
			return true;
		}
		return false;
	}

	internal static void IdleStand(CompanionAI ai, string why)
	{
		S s = Peek(ai);
		if (s != null)
		{
			if (s.IdleSeat && (s.SitChair || s.SitFloor))
			{
				StandUp(ai, s, keepAnim: false, why);
			}
			else if (s.GoingToChair)
			{
				ReleaseChair(s);
			}
			s.IdleSeat = false;
		}
	}

	public static void Tick(CompanionSetup cs, CompanionAI ai)
	{
		if (cs == null || ai == null)
		{
			return;
		}
		try
		{
			S s = Peek(ai);
			if (s == null)
			{
				return;
			}
			if (s.GoingToChair && Time.time - s.LastUpdate > 0.5f)
			{
				ReleaseChair(s);
			}
			if (!s.SitChair && !s.SitFloor)
			{
				return;
			}
			string text = null;
			bool keepAnim = false;
			if (s.IdleSeat)
			{
				CompanionRest component = cs.GetComponent<CompanionRest>();
				if (ai.IsInCombat)
				{
					StandUp(ai, s, keepAnim: false, "fight");
				}
				else if (component != null && (component.IsSleeping || component.IsNavigating))
				{
					StandUp(ai, s, s.SitFloor, "resting");
				}
				else if (s.SitChair)
				{
					Pin(ai, s);
					s.LastUpdate = Time.time;
				}
				return;
			}
			GameObject followTarget = ai.GetFollowTarget();
			CompanionRest component2 = cs.GetComponent<CompanionRest>();
			RepairController component3 = cs.GetComponent<RepairController>();
			ZNetView component4 = cs.GetComponent<ZNetView>();
			int num = ((component4 != null && component4.GetZDO() != null) ? component4.GetZDO().GetInt(CompanionSetup.ActionModeHash) : 0);
			if (component2 != null && (component2.IsResting || component2.IsSleeping || component2.IsNavigating))
			{
				text = "resting";
				keepAnim = s.SitFloor;
			}
			else if (followTarget == null || followTarget.GetInstanceID() != s.TargetId)
			{
				text = "not following";
			}
			else if (ai.IsInCombat)
			{
				text = "fight";
			}
			else if (num != 0 && !MHR_JobRules.KeepsFollow(num))
			{
				text = "new orders";
			}
			else if (MHR_ToolKeeper.IsBusy(cs))
			{
				text = "trip";
			}
			else if (component3 != null && component3.IsActive)
			{
				text = "repairs";
			}
			else if (Time.time - s.LastUpdate > 1f)
			{
				text = "busy";
			}
			else
			{
				bool num2 = s_t.TryGetValue(s.TargetId, out var value) && value.Indoors;
				float num3 = Flat(followTarget.transform.position, s.SitPlayerPos);
				if (num2)
				{
					float num4 = ((ModConfig.FollowSitStayRange != null) ? ModConfig.FollowSitStayRange.Value : 10f);
					if (Vector3.Distance(followTarget.transform.position, ai.transform.position) > num4)
					{
						text = "player moved off";
					}
					else if (Character.InInterior(ai.transform.position) && !Los(ai.transform.position, followTarget.transform.position))
					{
						if (s.OutOfSightSince <= 0f)
						{
							s.OutOfSightSince = Time.time;
						}
						else if (Time.time - s.OutOfSightSince > 2f)
						{
							text = "player out of sight";
						}
					}
					else
					{
						s.OutOfSightSince = 0f;
					}
				}
				else if (num3 > 1f)
				{
					text = "player moved";
				}
			}
			if (text != null)
			{
				StandUp(ai, s, keepAnim, text);
			}
			else if (s.SitChair)
			{
				Pin(ai, s);
			}
		}
		catch (Exception ex)
		{
			if (CompanionsPlugin.Log != null && Time.time >= s_nextErr)
			{
				s_nextErr = Time.time + 30f;
				CompanionsPlugin.Log.LogWarning("[Follow] " + ex.Message);
			}
		}
	}
}
