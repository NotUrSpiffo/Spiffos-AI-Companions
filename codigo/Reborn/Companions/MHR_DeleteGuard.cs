using System.Diagnostics;
using UnityEngine;

namespace Companions;

public static class MHR_DeleteGuard
{
	private static float s_lastLog;

	internal static bool Allow(GameObject go)
	{
		if (!(go == null) && ModConfig.ProtectCompanions != null && ModConfig.ProtectCompanions.Value)
		{
			CompanionSetup component = go.GetComponent<CompanionSetup>();
			if (!(component == null) && !component._mhrDespawning && !component._mhrDying && !component._mhrStale)
			{
				Character component2 = go.GetComponent<Character>();
				if (!(component2 == null) && component2.GetHealth() > 0f)
				{
					if (Time.time - s_lastLog >= 5f)
					{
						s_lastLog = Time.time;
						string name = component2.m_name;
						string text = new StackTrace(2, fNeedFileInfo: false).ToString();
						text = text.Replace('\n', ' ');
						if (text.Length > 600)
						{
							text = text.Substring(0, 600);
						}
						CompanionsPlugin.Log.LogWarning("[Protect] Blocked another mod from deleting companion \"" + name + "\" (turn off General > ProtectCompanions to allow this). Called from: " + text);
					}
					return false;
				}
			}
		}
		return true;
	}
}
