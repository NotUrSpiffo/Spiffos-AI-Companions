using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Companions;

internal static class MHR_Board
{
	private class S
	{
		public Ship Ship;

		public Chair Chair;

		public bool Auto;

		public float Since;

		public float ProgressAt;

		public float Best = float.MaxValue;

		public float NextJump;

		public float NextLog;

		public bool Footsteps;

		public bool FootstepsTried;

		public Vector3 StepFrom;

		public Vector3 StepTo;

		public float StepBest = float.MaxValue;

		public bool HasShore;

		public Vector3 Shore;

		public float TreadSince = -1f;

		public float WadeBest = float.MaxValue;

		public bool TakeoffTried;

		public bool HasTakeoff;

		public Vector3 Takeoff;

		public int TakeoffJumps;

		public bool JumpLogged;

		public float WadeAt = -1f;

		public bool WadeGaveUp;

		public float HoldUntil;

		public Ship HoldShip;

		public Ship SatOn;

		public float OwnerOffSince = -1f;

		public float SeatSince = -1f;

		public bool LaunchTried;

		public bool HasLaunch;

		public Vector3 Launch;

		public Ship LeaveShip;

		public float LeaveSince;

		public bool HasLeaveSpot;

		public Vector3 LeaveSpot;

		public float LeapAt = -10f;

		public Vector3 LeapDir;
	}

	private static readonly Dictionary<int, S> s_s = new Dictionary<int, S>();

	private static int s_vehicle = -1;

	private static int s_solid = -1;

	private const float GiveUpAfter = 30f;

	private const float StuckFor = 8f;

	private const float HoldFor = 20f;

	private const float LadderReach = 1.7f;

	private static int Vehicle()
	{
		if (s_vehicle == -1)
		{
			s_vehicle = LayerMask.GetMask("vehicle");
		}
		return s_vehicle;
	}

	private static int Solid()
	{
		if (s_solid == -1)
		{
			s_solid = LayerMask.GetMask("Default", "static_solid", "piece", "terrain");
		}
		return s_solid;
	}

	private static S Get(CompanionAI ai)
	{
		int instanceID = ai.GetInstanceID();
		if (!s_s.TryGetValue(instanceID, out var value))
		{
			value = new S();
			s_s[instanceID] = value;
		}
		return value;
	}

	private static S Peek(CompanionAI ai)
	{
		if (!(ai != null) || !s_s.TryGetValue(ai.GetInstanceID(), out var value))
		{
			return null;
		}
		return value;
	}

	private static float Flat(Vector3 a, Vector3 b)
	{
		return new Vector2(a.x - b.x, a.z - b.z).magnitude;
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log?.LogInfo("[Board] " + msg);
		}
	}

	internal static bool Active(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null)
		{
			return s.Ship != null;
		}
		return false;
	}

	internal static bool KeepSwimming(Character c)
	{
		S s = Peek((c != null) ? c.GetComponent<CompanionAI>() : null);
		if (s == null || s.Ship == null)
		{
			return false;
		}
		if (NearestLadder(s.Ship, c.transform.position, out var flat) == null || flat > 8f)
		{
			return false;
		}
		CompanionStamina component = c.GetComponent<CompanionStamina>();
		if (!(flat < 3.5f) && !(component == null))
		{
			return component.Stamina >= MHR_Water.SwimStamina(c, Mathf.Max(0f, flat - 1.7f)) * 1.1f;
		}
		return true;
	}

	private static Ladder NearestLadder(Ship ship, Vector3 me, out float flat)
	{
		flat = float.MaxValue;
		Ladder result = null;
		Ladder[] componentsInChildren = ship.GetComponentsInChildren<Ladder>();
		foreach (Ladder ladder in componentsInChildren)
		{
			if (!(ladder == null) && !(ladder.m_targetPos == null))
			{
				float num = Flat(me, ladder.transform.position);
				if (num < flat)
				{
					flat = num;
					result = ladder;
				}
			}
		}
		return result;
	}

	internal static void Start(CompanionAI ai, Ship ship, Chair chair, bool auto)
	{
		if (!(ai == null) && !(ship == null))
		{
			S s = Get(ai);
			s.Ship = ship;
			s.Chair = chair;
			s.Auto = auto;
			s.Since = Time.time;
			s.ProgressAt = Time.time;
			s.Best = float.MaxValue;
			s.Footsteps = false;
			s.FootstepsTried = false;
			s.HoldUntil = 0f;
			s.TreadSince = -1f;
			s.SeatSince = -1f;
			s.LaunchTried = false;
			s.HasLaunch = false;
			s.WadeBest = float.MaxValue;
			s.WadeAt = -1f;
			s.WadeGaveUp = false;
			s.TakeoffTried = false;
			s.HasTakeoff = false;
			s.TakeoffJumps = 0;
			s.JumpLogged = false;
			MHR_Mover.Release(ai);
			MHR_Deck.Release(ai);
			if (CompanionsPlugin.LogNormal)
			{
				Log(ai.GetComponent<Character>()?.m_name + (auto ? " is following you aboard" : " is boarding the ship"));
			}
		}
	}

	internal static void Cancel(CompanionAI ai)
	{
		S s = Peek(ai);
		if (s != null && s.Ship != null)
		{
			s.Ship = null;
			s.Chair = null;
			s.Footsteps = false;
			s.SeatSince = -1f;
			MHR_Mover.Release(ai);
			MHR_Deck.Release(ai);
		}
	}

	private static bool Aboard(Player p, Ship ship)
	{
		if (p != null && ship != null)
		{
			if (!(ShipUnder(p) == ship))
			{
				return ship.IsPlayerInBoat(p);
			}
			return true;
		}
		return false;
	}

	internal static Ship ShipUnder(Character c)
	{
		if (c == null)
		{
			return null;
		}
		Ship standingOnShip = c.GetStandingOnShip();
		if (standingOnShip != null)
		{
			return standingOnShip;
		}
		if (Physics.Raycast(c.transform.position + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 1.2f, Vehicle()))
		{
			if (!(hitInfo.collider.attachedRigidbody != null))
			{
				return hitInfo.collider.GetComponentInParent<Ship>();
			}
			return hitInfo.collider.attachedRigidbody.GetComponent<Ship>();
		}
		return null;
	}

	private static string SeatPath(Transform t, Transform root)
	{
		string text = t.name;
		while (t.parent != null && t.parent != root)
		{
			t = t.parent;
			text = t.name + "/" + text;
		}
		return text;
	}

	private static Chair FreeChair(Ship ship, Chair prefer, Vector3 near, Character self)
	{
		if (Free(prefer, self))
		{
			return prefer;
		}
		Chair result = null;
		float num = float.MaxValue;
		Chair[] componentsInChildren = ship.GetComponentsInChildren<Chair>();
		foreach (Chair chair in componentsInChildren)
		{
			if (Free(chair, self))
			{
				float num2 = Vector3.Distance(near, chair.m_attachPoint.position);
				if (num2 < num)
				{
					num = num2;
					result = chair;
				}
			}
		}
		return result;
	}

	internal static bool IsMast(Chair ch)
	{
		if (ch != null)
		{
			return ch.gameObject.name.IndexOf("mast", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	internal static bool Free(Chair ch, Character self = null)
	{
		if (ch == null || !ch.m_inShip || ch.m_attachPoint == null || IsMast(ch))
		{
			return false;
		}
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (allCharacter != null && allCharacter != self && Vector3.Distance(allCharacter.transform.position, ch.m_attachPoint.position) < 0.5f)
			{
				return false;
			}
		}
		return true;
	}

	private static bool DeckSpot(Ship ship, Vector3 me, out Vector3 spot, Collider[] cols = null)
	{
		spot = Vector3.zero;
		Vector3 position = ship.transform.position;
		Collider collider = null;
		float num = float.MaxValue;
		Vector3 vector = position;
		Collider[] array = cols ?? ship.GetComponentsInChildren<Collider>();
		foreach (Collider collider2 in array)
		{
			if (!(collider2 == null) && !collider2.isTrigger && ((1 << collider2.gameObject.layer) & Vehicle()) != 0)
			{
				Vector3 vector2;
				try
				{
					vector2 = collider2.ClosestPoint(me);
				}
				catch
				{
					continue;
				}
				float num2 = Flat(vector2, me);
				if (num2 < num)
				{
					num = num2;
					collider = collider2;
					vector = vector2;
				}
			}
		}
		if (collider == null)
		{
			return false;
		}
		Vector3 vector3 = position - vector;
		vector3.y = 0f;
		Vector3 vector4 = vector + ((vector3.sqrMagnitude > 0.01f) ? vector3.normalized : Vector3.zero) * 1f;
		if (!Physics.Raycast(new Vector3(vector4.x, Mathf.Max(me.y, position.y) + 4f, vector4.z), Vector3.down, out var hitInfo, 10f, Vehicle()))
		{
			return false;
		}
		if (((hitInfo.collider.attachedRigidbody != null) ? hitInfo.collider.attachedRigidbody.GetComponent<Ship>() : hitInfo.collider.GetComponentInParent<Ship>()) != ship)
		{
			return false;
		}
		spot = hitInfo.point;
		return true;
	}

	internal static bool FollowHook(CompanionAI ai, GameObject target, float dt)
	{
		if (!(ai == null) && !(target == null) && !ai.IsOnShip)
		{
			ConfigEntry<bool> followAutoBoard = ModConfig.FollowAutoBoard;
			if (followAutoBoard == null || followAutoBoard.Value)
			{
				Player component = target.GetComponent<Player>();
				Character component2 = ai.GetComponent<Character>();
				if (component == null || component2 == null)
				{
					return false;
				}
				Ship ship = ShipUnder(component);
				Ship ship2 = ShipUnder(component2);
				S s = Get(ai);
				if (ship2 != null && ship != ship2 && Aboard(component, ship2))
				{
					ship = ship2;
				}
				if (ship2 != null && ship2 != ship)
				{
					return Leave(ai, s, component2, ship2, component, dt);
				}
				if (ship2 == null && s.LeaveShip != null && !component2.IsOnGround() && !component2.IsSwimming() && Time.time - s.LeapAt < 3f)
				{
					ai.MoveTowards(s.LeapDir, run: true);
					return true;
				}
				s.LeaveShip = null;
				if (ship == null)
				{
					s.HoldUntil = 0f;
					return false;
				}
				if (ship2 == ship)
				{
					if (Flat(ai.transform.position, component.transform.position) > 2.5f && MHR_Deck.SpotNear(ship, component.transform.position, out var local))
					{
						MHR_Deck.Walk(ai, ship, local, 1.5f, out var _, 0f);
					}
					else
					{
						MHR_Deck.Release(ai);
						ai.StopMoving();
						ai.LookAtPoint(component.transform.position);
					}
					return true;
				}
				if (Time.time < s.HoldUntil && s.HoldShip == ship)
				{
					if (s.HasShore && Flat(ai.transform.position, s.Shore) > 1f)
					{
						MHR_Mover.Go(ai, dt, s.Shore, 0.8f, allowRun: false, "Board", 2);
						if (MHR_Mover.GaveUp(ai))
						{
							s.HasShore = false;
						}
						return true;
					}
					s.HasShore = false;
					ai.StopMoving();
					ai.LookAtPoint(ship.transform.position);
					return true;
				}
				float num = ModConfig.FollowBoardRange?.Value ?? 20f;
				Rigidbody component3 = ship.GetComponent<Rigidbody>();
				if (Flat(ai.transform.position, ship.transform.position) > num || (component3 != null && component3.linearVelocity.magnitude > 3f) || ai.IsInCombat)
				{
					return false;
				}
				Start(ai, ship, null, auto: true);
				return Update(ai, dt);
			}
		}
		return false;
	}

	private static bool Leave(CompanionAI ai, S s, Character c, Ship ship, Player p, float dt)
	{
		float time = Time.time;
		if (s.LeaveShip != ship)
		{
			if (p.IsSwimming() || !p.IsOnGround())
			{
				MHR_Deck.Release(ai);
				ai.StopMoving();
				return true;
			}
			s.LeaveShip = ship;
			s.LeaveSince = time;
			s.NextJump = 0f;
			s.LeapAt = -10f;
			s.HasLeaveSpot = MHR_Deck.SideSpot(ship, ai.transform.position, p.transform.position, out s.LeaveSpot) || MHR_Deck.SpotNear(ship, p.transform.position, out s.LeaveSpot);
			if (CompanionsPlugin.LogNormal)
			{
				Log(c.m_name + " getting off the ship");
			}
		}
		if (time - s.LeaveSince > 15f)
		{
			MHR_Deck.Release(ai);
			return false;
		}
		Vector3 position = ai.transform.position;
		Vector3 position2 = p.transform.position;
		if (s.HasLeaveSpot && time - s.LeapAt > 1.5f && !MHR_Deck.Walk(ai, ship, s.LeaveSpot, 0.5f, out var stuck, 0f, 3f) && !stuck)
		{
			return true;
		}
		MHR_Deck.Release(ai);
		Vector3 vector = position2 - position;
		vector.y = 0f;
		if (vector.sqrMagnitude < 0.01f)
		{
			return false;
		}
		vector.Normalize();
		ai.transform.rotation = Quaternion.LookRotation(vector);
		ai.MoveTowards(vector, run: true);
		if (c.IsOnGround() && time >= s.NextJump)
		{
			s.NextJump = time + 0.5f;
			s.LeapAt = time;
			s.LeapDir = vector;
			MHR_Jump.Leap(ai, c, vector);
			if (CompanionsPlugin.LogNormal)
			{
				Log(c.m_name + " jumping off the ship");
			}
		}
		return true;
	}

	private static bool LaunchSpot(Ladder ladder, Vector3 me, out Vector3 spot)
	{
		spot = me;
		Vector3 position = ladder.transform.position;
		int layerMask = Solid() | Vehicle();
		for (int i = 1; i <= 15; i++)
		{
			float num = float.MaxValue;
			bool flag = false;
			for (int j = 0; j < 24; j++)
			{
				Vector3 vector = Quaternion.Euler(0f, (float)j * 15f, 0f) * Vector3.forward;
				Vector3 vector2 = position + vector * i;
				if (!Physics.Raycast(new Vector3(vector2.x, position.y + 8f, vector2.z), Vector3.down, out var hitInfo, 30f, layerMask) || ((1 << hitInfo.collider.gameObject.layer) & Vehicle()) != 0 || hitInfo.point.y > position.y + 4f)
				{
					continue;
				}
				vector2 = hitInfo.point;
				if (!(MHR_Water.DepthAt(vector2) >= 0.1f) && !(Flat(vector2, me) > 40f))
				{
					float num2 = Flat(vector2, me);
					if (num2 < num)
					{
						num = num2;
						spot = vector2;
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

	internal static bool ShouldLeaveSeat(CompanionAI ai, Ship ship)
	{
		S s = Peek(ai);
		if (s == null || s.SatOn == null || s.SatOn != ship)
		{
			return false;
		}
		CompanionSetup component = ai.GetComponent<CompanionSetup>();
		if (component == null || !component.GetFollow())
		{
			s.OwnerOffSince = -1f;
			return false;
		}
		Player player = MHR_Owner.Of(ai);
		if (player == null)
		{
			return false;
		}
		if (Aboard(player, ship) || !player.IsOnGround() || Vector3.Distance(player.transform.position, ai.transform.position) > 40f)
		{
			s.OwnerOffSince = -1f;
			return false;
		}
		if (s.OwnerOffSince < 0f)
		{
			s.OwnerOffSince = Time.time;
		}
		if (Time.time - s.OwnerOffSince < 2f)
		{
			return false;
		}
		s.SatOn = null;
		s.OwnerOffSince = -1f;
		if (CompanionsPlugin.LogNormal)
		{
			Log(ai.GetComponent<Character>()?.m_name + " - you've gone ashore, getting off the ship");
		}
		return true;
	}

	internal static bool Update(CompanionAI ai, float dt)
	{
		S s = Peek(ai);
		Character component = ai.GetComponent<Character>();
		Humanoid humanoid = component as Humanoid;
		if (s == null || s.Ship == null || component == null || humanoid == null)
		{
			return false;
		}
		Ship ship = s.Ship;
		float time = Time.time;
		Vector3 position = ai.transform.position;
		string name = component.m_name;
		if (ShipUnder(component) == ship)
		{
			Chair chair = FreeChair(ship, s.Chair, position, component);
			bool auto = s.Auto;
			if (chair != null)
			{
				s.Chair = chair;
				Vector3 vector = ship.transform.InverseTransformPoint(chair.m_attachPoint.position);
				if (s.SeatSince < 0f)
				{
					s.SeatSince = time;
					s.ProgressAt = time;
					if (CompanionsPlugin.LogNormal)
					{
						Log($"{name} is aboard - walking to a seat: '{SeatPath(chair.transform, ship.transform)}' at {vector:F2} on the ship");
					}
				}
				Vector3 goalLocal = (MHR_Deck.SpotNear(ship, chair.m_attachPoint.position, out var local) ? local : vector);
				bool flag = MHR_Deck.Walk(ai, ship, goalLocal, 0.6f, out var stuck);
				if (!flag && !stuck && time - s.SeatSince < 20f)
				{
					return true;
				}
				if (!flag && CompanionsPlugin.LogNormal)
				{
					Log(name + " can't get any closer to the seat - sitting down anyway");
				}
				Cancel(ai);
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} sits down: '{SeatPath(chair.transform, ship.transform)}' (world {chair.m_attachPoint.position:F1})");
				}
				ai.AttachToShip(ship, chair);
				Get(ai).SatOn = ship;
			}
			else
			{
				Cancel(ai);
				if (CompanionsPlugin.LogNormal)
				{
					Log(name + " is aboard - no free seat, standing on deck");
				}
				if (!auto)
				{
					ai.BoardingEnded();
				}
			}
			return false;
		}
		Ladder ladder = null;
		float num = float.MaxValue;
		Ladder[] componentsInChildren = ship.GetComponentsInChildren<Ladder>();
		foreach (Ladder ladder2 in componentsInChildren)
		{
			if (ladder2 == null || ladder2.m_targetPos == null)
			{
				continue;
			}
			float num2 = Vector3.Distance(position, ladder2.transform.position);
			if (num2 < ladder2.m_useDistance - 0.1f && (component.IsSwimming() || component.IsOnGround()))
			{
				ladder2.Interact(humanoid, hold: false, alt: false);
				if (CompanionsPlugin.LogNormal)
				{
					Log(name + " used the ship's ladder");
				}
				s.ProgressAt = time;
				return true;
			}
			if (num2 < num)
			{
				num = num2;
				ladder = ladder2;
			}
		}
		CompanionStamina component2 = ai.GetComponent<CompanionStamina>();
		if (MHR_Mover.RestingForSwim(ai))
		{
			s.ProgressAt = time;
			if (time >= s.NextLog)
			{
				s.NextLog = time + 5f;
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} resting before the swim ({((component2 != null) ? component2.Stamina : 0f):F0}/{((component2 != null) ? component2.MaxStamina : 0f):F0} stamina)");
				}
			}
		}
		float num3 = Flat(position, ship.transform.position);
		if (num3 < s.Best - 0.5f)
		{
			s.Best = num3;
			s.ProgressAt = time;
		}
		if (TryJumpAboard(ai, s, component, position, ship, time, name))
		{
			return true;
		}
		if (!s.TakeoffTried && !component.IsSwimming() && MHR_Water.DepthAt(position) < 0.3f)
		{
			s.TakeoffTried = true;
			Player player = MHR_Owner.Of(ai);
			Vector3 spot;
			if (player != null && MHR_Follow.StepsOnto(player.gameObject, ship, out var from, out var to) && MHR_Water.DepthAt(from) < 0.1f && Flat(from, position) < 30f && JumpCheck(component, ship, from, null, out to, out var _, out var _, out var _, out var _))
			{
				s.HasTakeoff = true;
				s.Takeoff = from;
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} going to jump aboard where you did, from {from:F1}");
				}
			}
			else if (JumpSpot(component, ship, position, out spot))
			{
				s.HasTakeoff = true;
				s.Takeoff = spot;
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} going to jump aboard from {spot:F1} ({Flat(spot, position):F1} m away)");
				}
			}
		}
		if (s.HasTakeoff && !component.IsSwimming())
		{
			if (Flat(position, s.Takeoff) > 0.6f)
			{
				MHR_Mover.Go(ai, dt, s.Takeoff, 0.4f, num3 > 8f, "Board", (num3 > 8f) ? 3 : 2);
				if (MHR_Mover.Progressing(ai))
				{
					s.ProgressAt = time;
				}
				if (MHR_Mover.GaveUp(ai))
				{
					s.HasTakeoff = false;
					MHR_Mover.Release(ai);
					if (CompanionsPlugin.LogNormal)
					{
						Log(name + " can't get to the spot to jump aboard from - the ladder instead");
					}
				}
				return true;
			}
			if (s.TakeoffJumps < 3 && !(time - s.ProgressAt > 6f))
			{
				ai.StopMoving();
				ai.LookAtPoint(ship.transform.position);
				return true;
			}
			s.HasTakeoff = false;
			if (CompanionsPlugin.LogNormal)
			{
				Log(name + " couldn't jump aboard from there - the ladder instead");
			}
		}
		Ladder ladder3 = NearestLadder(ship, position, out var flat);
		CompanionStamina component3 = ai.GetComponent<CompanionStamina>();
		if (ladder3 != null && !s.LaunchTried && !component.IsSwimming() && flat > 2f && flat <= 15f)
		{
			s.LaunchTried = true;
			s.HasLaunch = LaunchSpot(ladder3, position, out s.Launch);
			if (s.HasLaunch && CompanionsPlugin.LogNormal)
			{
				Log($"{name} heading for the water's edge nearest the ladder ({Flat(s.Launch, ladder3.transform.position):F1} m from it)");
			}
		}
		float num4;
		if (s.HasLaunch && ladder3 != null)
		{
			num4 = Mathf.Max(0f, Flat(s.Launch, ladder3.transform.position) - 1.7f);
		}
		else
		{
			num4 = ((ladder3 != null && MHR_Water.DepthAt(position) > 0.3f) ? Mathf.Max(0f, flat - 1.7f) : MHR_Mover.RouteSwim(ai));
		}
		if (num4 > 0.5f && component2 != null && component2.MaxStamina > 0f && !component.IsSwimming())
		{
			float num5 = MHR_Water.SwimStamina(component, num4) * 1.25f + 5f;
			if (num5 > component2.MaxStamina)
			{
				return GiveUp(ai, s, component, $"the swim to the ladder ({num4:F0} m) needs about {num5:F0} stamina, it has {component2.MaxStamina:F0} at most");
			}
		}
		bool flag2 = s.HasLaunch && Flat(position, s.Launch) < 1.2f;
		if (ladder3 != null && ((flat <= 8f) | flag2))
		{
			Vector3 vector2 = ladder3.transform.position - position;
			vector2.y = 0f;
			Vector3 vector3 = ((vector2.sqrMagnitude > 0.01f) ? vector2.normalized : ai.transform.forward);
			if (component.IsSwimming() && flat < 3.5f)
			{
				if (s.TreadSince < 0f)
				{
					s.TreadSince = time;
					if (CompanionsPlugin.LogNormal)
					{
						Log(name + " treading water by the ladder until it's in reach");
					}
				}
				s.ProgressAt = time;
				if (flat > 0.8f)
				{
					ai.transform.rotation = Quaternion.LookRotation(vector3);
					ai.MoveTowards(vector3, run: false);
				}
				else
				{
					ai.StopMoving();
				}
				if (time - s.TreadSince > 20f)
				{
					return GiveUp(ai, s, component, "the ladder never came within reach");
				}
				return true;
			}
			s.TreadSince = -1f;
			if (!component.IsSwimming() && component.IsOnGround() && !Physics.Raycast(position + Vector3.up * 0.5f, vector3, 1.5f, Solid()))
			{
				Vector3 p = position + vector3 * 1.2f;
				p.y = MHR_Water.GroundAt(p);
				if (MHR_Water.DepthAt(p) > 0.8f)
				{
					float num6 = MHR_Water.SwimStamina(component, Mathf.Max(0f, flat - 1.7f)) * 1.25f + 5f;
					if (component3 != null && component3.Stamina < num6)
					{
						ai.StopMoving();
						s.ProgressAt = time;
						if (time >= s.NextLog)
						{
							s.NextLog = time + 5f;
							if (CompanionsPlugin.LogNormal)
							{
								Log($"{name} resting before the swim to the ladder ({component3.Stamina:F0}/{num6:F0} stamina)");
							}
						}
						return true;
					}
					if (time >= s.NextJump)
					{
						s.NextJump = time + 2f;
						s.ProgressAt = time;
						MHR_Jump.Leap(ai, component, vector3);
						s.HasLaunch = false;
						if (CompanionsPlugin.LogNormal)
						{
							Log($"{name} diving in towards the ladder ({flat:F1} m)");
						}
					}
					return true;
				}
				if (((MHR_Water.DepthAt(p) < 0.4f) | flag2) && p.y > position.y - 1.5f)
				{
					ai.transform.rotation = Quaternion.LookRotation(vector3);
					ai.MoveTowards(vector3, run: false);
					s.ProgressAt = time;
					return true;
				}
			}
		}
		if (ladder3 != null && !s.WadeGaveUp && !component.IsSwimming() && flat <= 15f && MHR_Water.DepthAt(position) > 0.3f)
		{
			if (s.WadeAt < 0f)
			{
				s.WadeAt = time;
				s.WadeBest = flat;
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} wading to the ladder ({flat:F1} m)");
				}
			}
			if (flat < s.WadeBest - 0.3f)
			{
				s.WadeBest = flat;
				s.WadeAt = time;
				s.ProgressAt = time;
			}
			if (!(time - s.WadeAt > 4f))
			{
				Vector3 vector4 = ladder3.transform.position - position;
				vector4.y = 0f;
				if (vector4.sqrMagnitude > 0.01f)
				{
					vector4.Normalize();
					ai.transform.rotation = Quaternion.LookRotation(vector4);
					ai.MoveTowards(vector4, run: false);
				}
				return true;
			}
			s.WadeGaveUp = true;
			if (CompanionsPlugin.LogNormal)
			{
				Log(name + " can't wade any closer to the ladder - going round");
			}
		}
		if (!s.FootstepsTried && time - s.ProgressAt > 8f)
		{
			s.FootstepsTried = true;
			Player player2 = MHR_Owner.Of(ai);
			if (player2 != null && MHR_Follow.StepsOnto(player2.gameObject, ship, out s.StepFrom, out s.StepTo))
			{
				s.Footsteps = true;
				s.StepBest = float.MaxValue;
				s.ProgressAt = time;
				if (CompanionsPlugin.LogNormal)
				{
					Log($"{name} can't find its own way aboard - following your steps (you got on at {s.StepFrom:F1})");
				}
			}
		}
		if (s.Footsteps)
		{
			float num7 = Flat(position, s.StepFrom);
			if (num7 < s.StepBest - 0.3f)
			{
				s.StepBest = num7;
				s.ProgressAt = time;
			}
			if (num7 > 0.6f)
			{
				MHR_Mover.Go(ai, dt, s.StepFrom, 0.4f, allowRun: true, "Board", 2);
			}
			else
			{
				Vector3 vector5 = s.StepTo - position;
				vector5.y = 0f;
				if (vector5.sqrMagnitude > 0.01f)
				{
					vector5.Normalize();
					if (s.StepTo.y > position.y + 0.4f && component.IsOnGround() && time >= s.NextJump)
					{
						s.NextJump = time + 1.5f;
						MHR_Jump.Leap(ai, component, vector5);
						if (CompanionsPlugin.LogNormal)
						{
							Log(name + " jumping where you did");
						}
					}
					else
					{
						ai.transform.rotation = Quaternion.LookRotation(vector5);
						ai.MoveTowards(vector5, run: false);
					}
				}
			}
		}
		else
		{
			Vector3 goal;
			if (ladder != null)
			{
				goal = ladder.transform.position;
			}
			else
			{
				goal = (DeckSpot(ship, position, out var spot2) ? spot2 : ship.transform.position);
			}
			bool flag3 = s.HasLaunch && !component.IsSwimming() && !flag2;
			if (flag3)
			{
				goal = s.Launch;
			}
			MHR_Mover.Go(ai, dt, goal, flag3 ? 0.6f : 0.5f, num3 > 8f, "Board", (num3 > 8f) ? 3 : 2);
			if (flag3 && MHR_Mover.Progressing(ai))
			{
				s.ProgressAt = time;
			}
			if (flag3 && MHR_Mover.GaveUp(ai))
			{
				s.HasLaunch = false;
				MHR_Mover.Release(ai);
			}
		}
		if (time >= s.NextLog)
		{
			s.NextLog = time + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log?.LogDebug(string.Format("[Board] {0}: {1:F1} m from the ship, nearest ladder {2}{3}", name, num3, (ladder != null) ? (num.ToString("F1") + " m") : "none", s.Footsteps ? ", on your steps" : ""));
			}
		}
		if (time - s.ProgressAt > 16f || time - s.Since > 60f || (s.Footsteps && time - s.ProgressAt > 8f))
		{
			return GiveUp(ai, s, component, "no way found");
		}
		return true;
	}

	private static bool JumpCheck(Character c, Ship ship, Vector3 from, Collider[] cols, out Vector3 dir, out float gap, out float dy, out float reach, out bool clear)
	{
		dir = Vector3.zero;
		gap = (dy = (reach = 0f));
		clear = false;
		if (!DeckSpot(ship, from, out var spot, cols))
		{
			return false;
		}
		gap = Flat(from, spot);
		dy = spot.y - from.y;
		dir = spot - from;
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.01f)
		{
			return false;
		}
		dir.Normalize();
		reach = MHR_Jump.Reach(c, dir, dy);
		clear = !Physics.Linecast(from + Vector3.up * 1.2f, spot + Vector3.up * 0.8f, Solid());
		return (gap > 0.3f && gap <= 4f && dy <= 1.6f && dy >= -3f && reach >= gap + 0.3f) & clear;
	}

	private static bool TryJumpAboard(CompanionAI ai, S s, Character c, Vector3 me, Ship ship, float now, string name)
	{
		if (!c.IsOnGround() || c.IsSwimming() || now < s.NextJump)
		{
			return false;
		}
		if (!JumpCheck(c, ship, me, null, out var dir, out var gap, out var dy, out var reach, out var clear))
		{
			if (!s.JumpLogged && gap > 0f && gap <= 6f)
			{
				s.JumpLogged = true;
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log?.LogInfo(string.Format("[Board] {0} can't jump aboard from {1:F1}: {2:F1} m across, {3:+0.0;-0.0} m up, its jump reaches {4:F1} m, {5}", name, me, gap, dy, reach, clear ? "clear" : "something in the way"));
				}
			}
			return false;
		}
		s.NextJump = now + 1.5f;
		s.ProgressAt = now;
		if (s.HasTakeoff)
		{
			s.TakeoffJumps++;
		}
		MHR_Jump.Leap(ai, c, dir);
		if (CompanionsPlugin.LogNormal)
		{
			Log($"{name} jumping aboard ({gap:F1} m across, {dy:+0.0;-0.0} m up)");
		}
		return true;
	}

	private static bool JumpSpot(Character c, Ship ship, Vector3 me, out Vector3 spot)
	{
		spot = me;
		Collider[] componentsInChildren = ship.GetComponentsInChildren<Collider>();
		List<Collider> list = new List<Collider>();
		Collider[] array = componentsInChildren;
		foreach (Collider collider in array)
		{
			if (collider != null && !collider.isTrigger && ((1 << collider.gameObject.layer) & Vehicle()) != 0 && !(collider is MeshCollider { convex: false }))
			{
				list.Add(collider);
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		Collider[] array2 = list.ToArray();
		int layerMask = Solid() | Vehicle();
		float num = float.MaxValue;
		for (int j = -15; j <= 15; j++)
		{
			for (int k = -15; k <= 15; k++)
			{
				if (j * j + k * k > 225)
				{
					continue;
				}
				Vector3 vector = me + new Vector3(j, 0f, k);
				float num2 = Flat(vector, me);
				if (num2 >= num)
				{
					continue;
				}
				float num3 = float.MaxValue;
				array = array2;
				foreach (Collider collider2 in array)
				{
					Vector3 a;
					try
					{
						a = collider2.ClosestPoint(vector);
					}
					catch
					{
						continue;
					}
					num3 = Mathf.Min(num3, Flat(a, vector));
				}
				if (!(num3 > 3.5f) && Physics.Raycast(new Vector3(vector.x, me.y + 6f, vector.z), Vector3.down, out var hitInfo, 15f, layerMask) && ((1 << hitInfo.collider.gameObject.layer) & Vehicle()) == 0)
				{
					vector = hitInfo.point;
					if (!(MHR_Water.DepthAt(vector) >= 0.1f) && !(Mathf.Abs(vector.y - me.y) > 4f) && JumpCheck(c, ship, vector, array2, out var _, out var _, out var _, out var _, out var _))
					{
						num = num2;
						spot = vector;
					}
				}
			}
		}
		return num < float.MaxValue;
	}

	private static bool GiveUp(CompanionAI ai, S s, Character c, string why)
	{
		Ship ship = s.Ship;
		bool auto = s.Auto;
		if (CompanionsPlugin.LogNormal)
		{
			Log(c.m_name + " can't get aboard (" + why + ") - waiting on the shore");
		}
		Cancel(ai);
		s.HoldShip = ship;
		s.HoldUntil = Time.time + 20f;
		s.HasShore = DryShore(ai.transform.position, (ship != null) ? ship.transform.position : ai.transform.position, out s.Shore);
		if (!s.HasShore)
		{
			ai.StopMoving();
		}
		if (!auto)
		{
			ai.BoardingEnded();
		}
		return false;
	}

	private static bool DryShore(Vector3 me, Vector3 ship, out Vector3 spot)
	{
		spot = me;
		if (MHR_Water.DepthAt(me) < 0.1f)
		{
			return false;
		}
		float num = float.MaxValue;
		bool flag = false;
		Vector3 vector = ship - me;
		vector.y = 0f;
		float[] array = new float[6] { 2f, 4f, 6f, 9f, 12f, 15f };
		foreach (float num2 in array)
		{
			for (int j = 0; j < 12; j++)
			{
				Vector3 vector2 = Quaternion.Euler(0f, (float)j * 30f, 0f) * Vector3.forward;
				Vector3 vector3 = me + vector2 * num2;
				vector3.y = MHR_Water.GroundAt(vector3);
				if (!(MHR_Water.DepthAt(vector3) >= 0.1f) && !(vector3.y > me.y + 3f))
				{
					float num3 = num2 - ((vector.sqrMagnitude > 0.01f) ? Vector3.Dot(vector2, vector.normalized) : 0f);
					if (num3 < num)
					{
						num = num3;
						spot = vector3;
						flag = true;
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
}
