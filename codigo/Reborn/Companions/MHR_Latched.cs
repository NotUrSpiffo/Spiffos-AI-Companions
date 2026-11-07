using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class MHR_Latched
{
	private static readonly AccessTools.FieldRef<Humanoid, Attack> s_currentAttack = AccessTools.FieldRefAccess<Humanoid, Attack>("m_currentAttack");

	private static readonly AccessTools.FieldRef<Attack, bool> s_isAttached = AccessTools.FieldRefAccess<Attack, bool>("m_isAttached");

	private static readonly AccessTools.FieldRef<Attack, Transform> s_attachTarget = AccessTools.FieldRefAccess<Attack, Transform>("m_attachTarget");

	internal static Character LatchedOn(Character c)
	{
		if (c == null)
		{
			return null;
		}
		Vector3 position = c.transform.position;
		foreach (Character allCharacter in Character.GetAllCharacters())
		{
			if (!(allCharacter == null) && !(allCharacter == c) && !allCharacter.IsDead() && allCharacter is Humanoid instance && !((allCharacter.transform.position - position).sqrMagnitude > 16f))
			{
				Attack attack = s_currentAttack(instance);
				if (attack != null && s_isAttached(attack) && s_attachTarget(attack) == c.transform)
				{
					return allCharacter;
				}
			}
		}
		return null;
	}
}
