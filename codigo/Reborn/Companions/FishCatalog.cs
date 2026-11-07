using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

internal static class FishCatalog
{
	private static readonly HashSet<string> s_fish = new HashSet<string>();

	private static bool s_built;

	internal static void Ensure()
	{
		if (s_built || ZNetScene.instance == null)
		{
			return;
		}
		s_built = true;
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Offline Companions Reborn - names for the [Fishing] settings. Written at every start from the game's own data, mods included.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("FISH (Fishing > Fish)  name = in-game name  (baits it takes, with the chance)");
			SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				Fish fish = ((prefab != null) ? prefab.GetComponent<Fish>() : null);
				if (!(fish == null))
				{
					s_fish.Add(prefab.name);
					sortedDictionary[prefab.name] = prefab.name + " = " + Local(fish) + "  (" + BaitsText(fish) + ")";
				}
			}
			foreach (string value in sortedDictionary.Values)
			{
				stringBuilder.AppendLine("  " + value);
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Fishing.txt"), stringBuilder.ToString());
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Fish] {s_fish.Count} kinds of fish");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Fish] couldn't read the fish: " + ex.Message);
		}
	}

	internal static bool IsFish(string prefab)
	{
		Ensure();
		return s_fish.Contains(prefab);
	}

	internal static string BaitNames(Fish fish)
	{
		List<string> list = new List<string>();
		foreach (Fish.BaitSetting bait in fish.m_baits)
		{
			if (bait?.m_bait != null)
			{
				list.Add(CookController.Name(bait.m_bait.gameObject.name));
			}
		}
		if (list.Count <= 0)
		{
			return "?";
		}
		return string.Join(" / ", list.ToArray());
	}

	internal static string BaitsText(Fish fish)
	{
		List<string> list = new List<string>();
		foreach (Fish.BaitSetting bait in fish.m_baits)
		{
			if (bait?.m_bait != null)
			{
				list.Add(CookController.Name(bait.m_bait.gameObject.name) + " " + (bait.m_chance * 100f).ToString("F0") + "%");
			}
		}
		if (list.Count <= 0)
		{
			return "no bait";
		}
		return string.Join(", ", list.ToArray());
	}

	internal static string Local(Fish fish)
	{
		string text = fish.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name ?? fish.m_name;
		return MHR_List.Local(text) ?? text;
	}

	internal static bool Allowed(string prefab, Fish fish)
	{
		return MHR_List.Get(ModConfig.FishList?.Value ?? "*").Allows(prefab, Local(fish));
	}

	internal static int Target(string prefab)
	{
		int dflt = ModConfig.FishTarget?.Value ?? 100;
		MHR_List mHR_List = MHR_List.Get(ModConfig.FishList?.Value ?? "*");
		if (mHR_List.HasAmount(prefab))
		{
			return mHR_List.AmountFor(prefab, dflt);
		}
		string name = CookController.Name(prefab);
		return mHR_List.AmountFor(name, dflt);
	}

	internal static string PrefabName(GameObject go)
	{
		ZNetView component = go.GetComponent<ZNetView>();
		ZDO zDO = ((component != null && component.IsValid()) ? component.GetZDO() : null);
		GameObject gameObject = ((zDO != null && ZNetScene.instance != null) ? ZNetScene.instance.GetPrefab(zDO.GetPrefab()) : null);
		if (gameObject != null)
		{
			return gameObject.name;
		}
		string name = go.name;
		int num = name.IndexOf("(Clone)", StringComparison.Ordinal);
		if (num < 0)
		{
			return name;
		}
		return name.Substring(0, num).Trim();
	}
}
