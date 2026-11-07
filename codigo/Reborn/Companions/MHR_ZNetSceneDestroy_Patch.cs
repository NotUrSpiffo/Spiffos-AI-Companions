using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch]
public static class MHR_ZNetSceneDestroy_Patch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(ZNetScene), "Destroy", new Type[1] { typeof(GameObject) });
	}

	[HarmonyPrefix]
	private static bool Prefix(GameObject go)
	{
		try
		{
			return MHR_DeleteGuard.Allow(go);
		}
		catch (Exception)
		{
			return true;
		}
	}
}
