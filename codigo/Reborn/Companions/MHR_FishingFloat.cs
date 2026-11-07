using System;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public static class MHR_FishingFloat
{
	[HarmonyPatch(typeof(FishingFloat), "Setup")]
	private static class Setup_Patch
	{
		private static bool Prefix(FishingFloat __instance, Character owner, ItemDrop.ItemData ammo)
		{
			try
			{
				if (owner == null || owner.GetComponent<CompanionSetup>() == null)
				{
					return true;
				}
				ZNetView zNetView = NView(__instance);
				if (zNetView == null || !zNetView.IsValid())
				{
					return false;
				}
				FishingFloat fishingFloat = Of(owner);
				if (fishingFloat != null && fishingFloat != __instance && ZNetScene.instance != null)
				{
					ZNetScene.instance.Destroy(fishingFloat.gameObject);
				}
				ZDO zDO = zNetView.GetZDO();
				zDO.Set("HC_FloatOwner", owner.GetZDOID());
				zDO.Set(ZDOVars.s_rodOwner, owner.GetZDOID().UserID);
				string text = ((ammo != null && ammo.m_dropPrefab != null) ? ammo.m_dropPrefab.name : null);
				if (text == null)
				{
					Humanoid humanoid = owner as Humanoid;
					ItemDrop.ItemData itemData = ((humanoid != null) ? humanoid.GetAmmoItem() : null);
					text = ((itemData != null && itemData.m_dropPrefab != null) ? itemData.m_dropPrefab.name : FishController.LastBait(owner));
				}
				if (text != null)
				{
					zDO.Set(ZDOVars.s_bait, text);
				}
				Transform transform = FindChild(owner.transform, "_RodTop");
				if (transform == null)
				{
					Warn("no rod tip (_RodTop) on " + owner.m_name + " - the float can't be used");
					zNetView.Destroy();
					return false;
				}
				if (__instance.m_rodLine != null)
				{
					__instance.m_rodLine.SetPeer(owner.GetZDOID());
				}
				LineLength(__instance) = Vector3.Distance(transform.position, __instance.transform.position);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log?.LogDebug("[Fish] Float out " + LineLength(__instance).ToString("F0") + " m, bait " + (text ?? "?") + ((ammo == null) ? " (the cast came without its ammo)" : ""));
				}
				return false;
			}
			catch (Exception ex)
			{
				Warn("float setup: " + ex.GetType().Name + ": " + ex.Message);
				try
				{
					ZNetView zNetView2 = NView(__instance);
					if (zNetView2 != null && zNetView2.IsValid())
					{
						zNetView2.Destroy();
					}
				}
				catch
				{
				}
				return false;
			}
		}
	}

	[HarmonyPatch(typeof(FishingFloat), "GetOwner")]
	private static class GetOwner_Patch
	{
		private static bool Prefix(FishingFloat __instance, ref Character __result)
		{
			try
			{
				ZNetView zNetView = NView(__instance);
				if (zNetView == null || !zNetView.IsValid() || zNetView.GetZDO().GetZDOID("HC_FloatOwner").IsNone())
				{
					return true;
				}
				__result = CompanionOwner(__instance);
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(FishingFloat), "ReturnBait")]
	private static class ReturnBait_Patch
	{
		private static bool Prefix(FishingFloat __instance, bool ___m_baitConsumed)
		{
			try
			{
				Character character = CompanionOwner(__instance);
				if (character == null)
				{
					return true;
				}
				if (!___m_baitConsumed)
				{
					Humanoid humanoid = character as Humanoid;
					GameObject gameObject = ((ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(__instance.GetBait()) : null);
					if (humanoid != null && gameObject != null)
					{
						humanoid.GetInventory().AddItem(gameObject, 1);
					}
				}
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	internal const string OwnerHash = "HC_FloatOwner";

	internal static readonly AccessTools.FieldRef<FishingFloat, Fish> Nibbler = AccessTools.FieldRefAccess<FishingFloat, Fish>("m_nibbler");

	internal static readonly AccessTools.FieldRef<FishingFloat, float> NibbleTime = AccessTools.FieldRefAccess<FishingFloat, float>("m_nibbleTime");

	internal static readonly AccessTools.FieldRef<FishingFloat, float> LineLength = AccessTools.FieldRefAccess<FishingFloat, float>("m_lineLength");

	internal static readonly AccessTools.FieldRef<FishingFloat, ZNetView> NView = AccessTools.FieldRefAccess<FishingFloat, ZNetView>("m_nview");

	private static float s_nextWarn;

	public static FishingFloat Of(Character c)
	{
		if (c == null)
		{
			return null;
		}
		ZDOID zDOID = c.GetZDOID();
		foreach (FishingFloat allInstance in FishingFloat.GetAllInstances())
		{
			if (!(allInstance == null))
			{
				ZNetView zNetView = NView(allInstance);
				if (!(zNetView == null) && zNetView.IsValid() && zNetView.GetZDO().GetZDOID("HC_FloatOwner") == zDOID)
				{
					return allInstance;
				}
			}
		}
		return null;
	}

	internal static Character CompanionOwner(FishingFloat f)
	{
		ZNetView zNetView = NView(f);
		if (zNetView == null || !zNetView.IsValid())
		{
			return null;
		}
		ZDOID zDOID = zNetView.GetZDO().GetZDOID("HC_FloatOwner");
		if (zDOID.IsNone() || ZNetScene.instance == null)
		{
			return null;
		}
		GameObject gameObject = ZNetScene.instance.FindInstance(zDOID);
		if (!(gameObject != null))
		{
			return null;
		}
		return gameObject.GetComponent<Character>();
	}

	private static void Warn(string msg)
	{
		if (!(Time.time < s_nextWarn))
		{
			s_nextWarn = Time.time + 10f;
			CompanionsPlugin.Log?.LogWarning("[Fish] " + msg);
		}
	}

	private static Transform FindChild(Transform t, string name)
	{
		if (t.name == name)
		{
			return t;
		}
		for (int i = 0; i < t.childCount; i++)
		{
			Transform transform = FindChild(t.GetChild(i), name);
			if (transform != null)
			{
				return transform;
			}
		}
		return null;
	}
}
