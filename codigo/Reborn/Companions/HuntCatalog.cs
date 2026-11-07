using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

internal static class HuntCatalog
{
	private static readonly Dictionary<string, List<string>> s_meatsOf = new Dictionary<string, List<string>>();

	private static readonly HashSet<string> s_meats = new HashSet<string>();

	private static bool s_built;

	private static readonly HashSet<string> s_allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> s_deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> s_birds = new HashSet<string>();

	internal static void Ensure()
	{
		if (s_built || ZNetScene.instance == null || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null || ObjectDB.instance.m_recipes.Count == 0)
		{
			return;
		}
		s_built = true;
		try
		{
			CookCatalog.Ensure();
			foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
			{
				if (prefab == null)
				{
					continue;
				}
				Character component = prefab.GetComponent<Character>();
				CharacterDrop component2 = prefab.GetComponent<CharacterDrop>();
				if (component == null || component2 == null || component2.m_drops == null || component.IsPlayer() || component.m_boss || prefab.GetComponent<CompanionSetup>() != null)
				{
					continue;
				}
				List<string> list = null;
				foreach (CharacterDrop.Drop drop in component2.m_drops)
				{
					string text = drop?.m_prefab?.name;
					if (text != null && LooksLikeMeat(text))
					{
						if (list == null)
						{
							list = new List<string>();
						}
						if (!list.Contains(text))
						{
							list.Add(text);
						}
						s_meats.Add(text);
					}
				}
				if (list != null)
				{
					s_meatsOf[prefab.name] = list;
				}
			}
			foreach (GameObject prefab2 in ZNetScene.instance.m_prefabs)
			{
				if (prefab2 == null || prefab2.GetComponent<RandomFlyingBird>() == null)
				{
					continue;
				}
				DropOnDestroyed component3 = prefab2.GetComponent<DropOnDestroyed>();
				if (component3?.m_dropWhenDestroyed?.m_drops == null)
				{
					continue;
				}
				List<string> list2 = new List<string>();
				foreach (DropTable.DropData drop2 in component3.m_dropWhenDestroyed.m_drops)
				{
					if (drop2.m_item != null && !list2.Contains(drop2.m_item.name))
					{
						list2.Add(drop2.m_item.name);
						s_meats.Add(drop2.m_item.name);
					}
				}
				if (list2.Count > 0)
				{
					s_meatsOf[prefab2.name] = list2;
					s_birds.Add(prefab2.name);
				}
			}
			WriteReference();
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Hunt] {s_meatsOf.Count - s_birds.Count} creatures drop meat, {s_birds.Count} kinds of bird ({s_meats.Count} kinds of drops counted)");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Hunt] couldn't read the creatures: " + ex.Message);
		}
	}

	private static bool LooksLikeMeat(string item)
	{
		if (!CookCatalog.IsGrillInput(item))
		{
			return item.IndexOf("meat", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	internal static bool CanHuntBird(string prefab)
	{
		Ensure();
		if (s_birds.Contains(prefab))
		{
			return Allowed(prefab, prefab);
		}
		return false;
	}

	internal static bool IsMeat(string item)
	{
		Ensure();
		return s_meats.Contains(item);
	}

	internal static List<string> MeatsOf(string prefab)
	{
		Ensure();
		if (!s_meatsOf.TryGetValue(prefab, out var value))
		{
			return null;
		}
		return value;
	}

	internal static bool CanHunt(Character c, string prefab)
	{
		Ensure();
		if (c.IsPlayer() || c.IsTamed() || c.IsBoss() || c.GetComponent<CompanionSetup>() != null || !s_meatsOf.ContainsKey(prefab) || s_birds.Contains(prefab))
		{
			return false;
		}
		return Allowed(prefab, c.m_name);
	}

	private static bool Allowed(string prefab, string token)
	{
		return MHR_List.Get(ModConfig.HuntPrey?.Value ?? "*").Allows(prefab, MHR_List.Local(token));
	}

	internal static int Target(string prefab, string item, int dflt)
	{
		MHR_List mHR_List = MHR_List.Get(ModConfig.HuntPrey?.Value ?? "*");
		if (mHR_List.HasAmount(item))
		{
			return mHR_List.AmountFor(item, dflt);
		}
		return mHR_List.AmountFor(prefab, dflt);
	}

	private static void WriteReference()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Offline Companions Reborn - names for the [Hunting] settings. Written at every start from the game's own data, mods included.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("PREY (Hunting > Prey)  name = in-game name  (meat it drops)");
			stringBuilder.AppendLine("Tamed animals, bosses and companions are never hunted.");
			SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>();
			foreach (KeyValuePair<string, List<string>> item in s_meatsOf)
			{
				GameObject prefab = ZNetScene.instance.GetPrefab(item.Key);
				Character character = ((prefab != null) ? prefab.GetComponent<Character>() : null);
				bool flag = s_birds.Contains(item.Key);
				List<string> list = new List<string>();
				foreach (string item2 in item.Value)
				{
					list.Add(item2 + " = " + CookController.Name(item2));
				}
				sortedDictionary[item.Key] = item.Key + " = " + ((character != null) ? CookController.Loc(character.m_name) : item.Key) + (flag ? " (bird, needs Hunting > Birds)" : "") + "  (" + string.Join(", ", list.ToArray()) + ")";
			}
			foreach (string value in sortedDictionary.Values)
			{
				stringBuilder.AppendLine("  " + value);
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Hunting.txt"), stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Hunt] couldn't write OfflineCompanionsReborn.Hunting.txt: " + ex.Message);
		}
	}
}
