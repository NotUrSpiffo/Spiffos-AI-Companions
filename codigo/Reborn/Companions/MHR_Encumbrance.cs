using System.Collections.Generic;

namespace Companions;

internal static class MHR_Encumbrance
{
	private static readonly Dictionary<int, bool> s_isCompanion = new Dictionary<int, bool>();

	internal static bool IsCompanion(Character c)
	{
		int instanceID = c.GetInstanceID();
		if (!s_isCompanion.TryGetValue(instanceID, out var value))
		{
			if (s_isCompanion.Count > 1000)
			{
				s_isCompanion.Clear();
			}
			value = c.GetComponent<CompanionSetup>() != null;
			s_isCompanion[instanceID] = value;
		}
		return value;
	}

	internal static bool Over(Character c)
	{
		Inventory inventory = (c as Humanoid)?.GetInventory();
		if (inventory != null)
		{
			return inventory.GetTotalWeight() > CompanionTierData.CarryLimit(c);
		}
		return false;
	}
}
