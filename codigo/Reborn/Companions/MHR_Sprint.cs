using System.Collections.Generic;

namespace Companions;

public static class MHR_Sprint
{
	private static readonly Dictionary<int, bool> s_isCompanion = new Dictionary<int, bool>();

	private static readonly Dictionary<int, bool> s_resting = new Dictionary<int, bool>();

	public static void Filter(Character c, ref bool run)
	{
		if (!run || c == null)
		{
			return;
		}
		try
		{
			int instanceID = c.GetInstanceID();
			if (!s_isCompanion.TryGetValue(instanceID, out var value))
			{
				value = c.GetComponent<CompanionSetup>() != null;
				if (s_isCompanion.Count > 2000)
				{
					s_isCompanion.Clear();
				}
				s_isCompanion[instanceID] = value;
			}
			if (!value)
			{
				return;
			}
			CompanionStamina component = c.GetComponent<CompanionStamina>();
			if (component == null || component.MaxStamina <= 0f)
			{
				return;
			}
			float num = component.Stamina / component.MaxStamina;
			CompanionAI component2 = c.GetComponent<CompanionAI>();
			bool flag = component2 != null && component2.IsInCombat;
			s_resting.TryGetValue(instanceID, out var value2);
			if (value2)
			{
				if (num >= 0.9f)
				{
					s_resting[instanceID] = false;
				}
				else if (!flag || !(num > 0.3f))
				{
					run = false;
				}
			}
			else if (num <= (flag ? 0.3f : 0.5f))
			{
				s_resting[instanceID] = true;
				run = false;
			}
		}
		catch
		{
		}
	}
}
