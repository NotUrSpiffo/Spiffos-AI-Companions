using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class MHR_RiseWatch
{
	private struct Sample
	{
		public float At;

		public Vector3 Pos;

		public string Doing;

		public bool Jumped;
	}

	private const float Every = 0.25f;

	private const float Window = 2f;

	private static readonly Dictionary<int, List<Sample>> s_samples = new Dictionary<int, List<Sample>>();

	private static readonly Dictionary<int, float> s_next = new Dictionary<int, float>();

	private static readonly Dictionary<int, float> s_quietUntil = new Dictionary<int, float>();

	private static readonly AccessTools.FieldRef<Character, float> s_jumpTimer = AccessTools.FieldRefAccess<Character, float>("m_jumpTimer");

	internal static void Tick(CompanionSetup cs)
	{
		if (!CompanionsPlugin.LogDetailed)
		{
			return;
		}
		try
		{
			if (cs == null)
			{
				return;
			}
			int instanceID = cs.GetInstanceID();
			float time = Time.time;
			if (s_next.TryGetValue(instanceID, out var value) && time < value)
			{
				return;
			}
			s_next[instanceID] = time + 0.25f;
			Character component = cs.GetComponent<Character>();
			CompanionAI component2 = cs.GetComponent<CompanionAI>();
			if (component == null || component.IsDead() || component.IsAttached())
			{
				s_samples.Remove(instanceID);
				return;
			}
			if (!s_samples.TryGetValue(instanceID, out var value2))
			{
				value2 = new List<Sample>();
				s_samples[instanceID] = value2;
			}
			Vector3 position = cs.transform.position;
			bool flag = s_jumpTimer(component) < 1.2f || (component2 != null && MHR_Mover.Climbing(component2));
			Sample sample = ((value2.Count > 0) ? value2[value2.Count - 1] : default(Sample));
			bool flag2 = value2.Count > 0;
			value2.Add(new Sample
			{
				At = time,
				Pos = position,
				Doing = CompanionStatus.Describe(cs),
				Jumped = flag
			});
			while (value2.Count > 0 && time - value2[0].At > 2f)
			{
				value2.RemoveAt(0);
			}
			if ((!s_quietUntil.TryGetValue(instanceID, out var value3) || !(time < value3)) && !((!flag2 || time - sample.At > 0.45f) | flag) && !sample.Jumped)
			{
				Sample sample2 = sample;
				float num = position.y - sample2.Pos.y;
				float magnitude = new Vector2(position.x - sample2.Pos.x, position.z - sample2.Pos.z).magnitude;
				if (!(num < 1f) && !(num < magnitude * 1.5f) && !(num > 50f))
				{
					s_quietUntil[instanceID] = time + 5f;
					CompanionsPlugin.Log.LogWarning(string.Format("[Diag] \"{0}\" rose {1:F1} m in {2:F1} s from {3:F1} to {4:F1} ({5:F1} m across), no jump or ladder; doing: {6} -> {7}; pieces at the start: {8}; at the end: {9}", component.m_name, num, time - sample2.At, sample2.Pos, position, magnitude, sample2.Doing ?? "?", CompanionStatus.Describe(cs) ?? "?", PiecesNear(sample2.Pos), PiecesNear(position)));
				}
			}
		}
		catch
		{
		}
	}

	private static string PiecesNear(Vector3 p)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Collider[] array = Physics.OverlapSphere(p + Vector3.up * 0.9f, 1.2f, -1, QueryTriggerInteraction.Ignore);
		foreach (Collider collider in array)
		{
			Piece piece = ((collider != null) ? collider.GetComponentInParent<Piece>() : null);
			if (piece != null)
			{
				hashSet.Add(piece.m_name + " at " + piece.transform.position.ToString("F1"));
			}
			if (hashSet.Count >= 8)
			{
				break;
			}
		}
		if (hashSet.Count <= 0)
		{
			return "none";
		}
		return string.Join(", ", hashSet);
	}
}
