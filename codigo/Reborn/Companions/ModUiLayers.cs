using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class ModUiLayers
{
	internal const int Base = 1050;

	internal const int FarmZonePlacer = 1050;

	internal const int HomeZonePanel = 1051;

	internal const int RadialMenu = 1052;

	internal const int StarterCompanionPanel = 1053;

	internal const int FarmZoneCropPicker = 1054;

	internal const int ConfigPanel = 1055;

	private static bool _logged;

	internal static void LogSceneLayers(string panel)
	{
		if (_logged)
		{
			return;
		}
		_logged = true;
		try
		{
			List<string> list = new List<string>();
			Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			foreach (Canvas canvas in array)
			{
				if (canvas != null && (canvas.isRootCanvas || canvas.overrideSorting))
				{
					list.Add($"{canvas.name}={canvas.sortingOrder}" + (canvas.overrideSorting ? "(override)" : ""));
				}
			}
			list.Sort();
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[UiLayers] {panel} opened at order {1050}+ - canvases in the scene: " + string.Join(", ", list));
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[UiLayers] Couldn't list the canvases: " + ex.Message);
		}
	}
}
