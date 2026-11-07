using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Companions;

internal static class CookCatalog
{
	private class Boss
	{
		public string Key;

		public string Prefab;

		public string Name;
	}

	internal static readonly List<Recipe> Recipes = new List<Recipe>();

	private static readonly Dictionary<string, string> s_convTo = new Dictionary<string, string>();

	private static readonly Dictionary<string, string> s_meadOf = new Dictionary<string, string>();

	private static readonly HashSet<string> s_meads = new HashSet<string>();

	private static readonly HashSet<string> s_made = new HashSet<string>();

	private static readonly HashSet<string> s_stationNames = new HashSet<string>();

	private static readonly Dictionary<string, Recipe> s_recipeFor = new Dictionary<string, Recipe>();

	private static bool s_built;

	private static readonly HashSet<string> s_allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> s_deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	private static string s_focusKey;

	private static string s_focusStage;

	private static readonly List<string> s_focus = new List<string>();

	private static List<Boss> s_bosses;

	internal static List<string> MeadBases => new List<string>(s_meadOf.Keys);

	internal static void Ensure()
	{
		if (s_built || ObjectDB.instance == null || ObjectDB.instance.m_recipes == null || ObjectDB.instance.m_recipes.Count == 0)
		{
			return;
		}
		s_built = true;
		try
		{
			Fermenter[] array = Resources.FindObjectsOfTypeAll<Fermenter>();
			foreach (Fermenter fermenter in array)
			{
				if (fermenter?.m_conversion == null)
				{
					continue;
				}
				foreach (Fermenter.ItemConversion item in fermenter.m_conversion)
				{
					if (item?.m_from != null && item.m_to != null)
					{
						s_meadOf[item.m_from.gameObject.name] = item.m_to.gameObject.name;
						s_meads.Add(item.m_to.gameObject.name);
					}
				}
			}
			CookingStation[] array2 = Resources.FindObjectsOfTypeAll<CookingStation>();
			foreach (CookingStation cookingStation in array2)
			{
				if (cookingStation?.m_conversion == null)
				{
					continue;
				}
				foreach (CookingStation.ItemConversion item2 in cookingStation.m_conversion)
				{
					if (item2?.m_from != null && item2.m_to != null)
					{
						s_convTo[item2.m_from.gameObject.name] = item2.m_to.gameObject.name;
						s_made.Add(item2.m_to.gameObject.name);
					}
				}
			}
			foreach (Recipe recipe in ObjectDB.instance.m_recipes)
			{
				if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_craftingStation == null)
				{
					continue;
				}
				string name = recipe.m_item.gameObject.name;
				ItemDrop.ItemData.SharedData sharedData = recipe.m_item.m_itemData?.m_shared;
				if (sharedData != null && (Value(sharedData) > 0f || s_meadOf.ContainsKey(name) || (s_convTo.TryGetValue(name, out var value) && Value(value) > 0f)) && !s_meads.Contains(name))
				{
					Recipes.Add(recipe);
					s_made.Add(name);
					s_stationNames.Add(recipe.m_craftingStation.m_name);
					if (!s_recipeFor.ContainsKey(name))
					{
						s_recipeFor[name] = recipe;
					}
				}
			}
			WriteReference();
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Cook] {Recipes.Count} recipes, {s_convTo.Count} cooking station items, {s_meadOf.Count} mead bases");
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't read the cooking recipes: " + ex.Message);
		}
	}

	internal static bool IsCookingStation(string stationName)
	{
		return s_stationNames.Contains(stationName);
	}

	internal static bool IsMade(string item)
	{
		return s_made.Contains(item);
	}

	internal static bool IsGrillInput(string item)
	{
		Ensure();
		return s_convTo.ContainsKey(item);
	}

	internal static bool IsMeadBase(string item)
	{
		return s_meadOf.ContainsKey(item);
	}

	internal static bool IsMead(string item)
	{
		return s_meads.Contains(item);
	}

	internal static string MeadOf(string meadBase)
	{
		if (!s_meadOf.TryGetValue(meadBase, out var value))
		{
			return null;
		}
		return value;
	}

	internal static Recipe RecipeFor(string item)
	{
		if (!s_recipeFor.TryGetValue(item, out var value))
		{
			return null;
		}
		return value;
	}

	internal static string FinalFood(string item)
	{
		if (Value(item) > 0f && !IsMeadBase(item))
		{
			return item;
		}
		if (s_convTo.TryGetValue(item, out var value) && Value(value) > 0f)
		{
			return value;
		}
		return null;
	}

	internal static float Value(string item)
	{
		ItemDrop.ItemData.SharedData sharedData = ObjectDB.instance?.GetItemPrefab(item)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
		if (sharedData == null)
		{
			return 0f;
		}
		return Value(sharedData);
	}

	private static float Value(ItemDrop.ItemData.SharedData s)
	{
		if (s.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
		{
			return 0f;
		}
		return s.m_food + s.m_foodStamina + s.m_foodEitr;
	}

	internal static bool Allowed(string item)
	{
		return MHR_List.Get(ModConfig.CookFoods?.Value ?? "*").Allows(item, CookController.Name(item));
	}

	internal static int Target(string item, int dflt)
	{
		MHR_List mHR_List = MHR_List.Get(ModConfig.CookFoods?.Value ?? "*");
		int num = mHR_List.AmountFor(item, -1);
		if (num < 0)
		{
			num = mHR_List.AmountFor(CookController.Name(item), dflt);
		}
		if (num > 0)
		{
			return num;
		}
		return 536870911;
	}

	internal static List<string> Focus()
	{
		string text = ModConfig.CookMeadFocus?.Value ?? "";
		List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
		string[] array = text.Split(new char[1] { ';' });
		foreach (string text2 in array)
		{
			int num = text2.IndexOf(':');
			if (num > 0)
			{
				list.Add(new KeyValuePair<string, string>(text2.Substring(0, num).Trim(), text2.Substring(num + 1)));
			}
		}
		string text3 = null;
		string text4 = null;
		foreach (KeyValuePair<string, string> item in list)
		{
			if (item.Key.Equals("start", StringComparison.OrdinalIgnoreCase))
			{
				if (text3 == null)
				{
					text3 = item.Key;
					text4 = item.Value;
				}
			}
			else if (Beaten(item.Key))
			{
				text3 = item.Key;
				text4 = item.Value;
			}
		}
		if (text == s_focusKey && text3 == s_focusStage)
		{
			return s_focus;
		}
		s_focusKey = text;
		s_focusStage = text3;
		s_focus.Clear();
		if (text4 != null)
		{
			array = text4.Split(new char[1] { ',' });
			for (int i = 0; i < array.Length; i++)
			{
				string text5 = array[i].Trim();
				if (text5.Length > 0)
				{
					if (IsMeadBase(text5))
					{
						s_focus.Add(text5);
					}
					else
					{
						CompanionsPlugin.Log.LogWarning("[Cook] MeadFocus: \"" + text5 + "\" isn't a mead base (see OfflineCompanionsReborn.Cooking.txt)");
					}
				}
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Cook] Mead priority (" + (text3 ?? "no entry") + "): " + ((s_focus.Count > 0) ? string.Join(", ", s_focus) : "none"));
		}
		return s_focus;
	}

	private static List<Boss> Bosses()
	{
		if (s_bosses != null)
		{
			return s_bosses;
		}
		List<Boss> list = new List<Boss>();
		try
		{
			Character[] array = Resources.FindObjectsOfTypeAll<Character>();
			foreach (Character character in array)
			{
				if (!(character == null) && character.m_boss && !string.IsNullOrEmpty(character.m_defeatSetGlobalKey) && !list.Exists((Boss b) => b.Key == character.m_defeatSetGlobalKey))
				{
					list.Add(new Boss
					{
						Key = character.m_defeatSetGlobalKey,
						Prefab = character.gameObject.name,
						Name = CookController.Loc(character.m_name)
					});
				}
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't read the bosses: " + ex.Message);
		}
		if (list.Count > 0)
		{
			s_bosses = list;
		}
		return list;
	}

	private static string Norm(string s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string text = s ?? "";
		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(char.ToLowerInvariant(c));
			}
		}
		return stringBuilder.ToString();
	}

	private static bool Beaten(string label)
	{
		string text = Norm(label);
		if (text.Length == 0)
		{
			return false;
		}
		List<string> list = new List<string>();
		foreach (Boss item in Bosses())
		{
			string text2 = Norm(item.Key);
			if (text2 == text || text2 == "defeated" + text || text2.EndsWith(text) || Norm(item.Name).Contains(text) || Norm(item.Prefab).Contains(text))
			{
				list.Add(item.Key);
			}
		}
		if (list.Count == 0)
		{
			list.Add("defeated_" + label.Trim().ToLowerInvariant());
		}
		foreach (string item2 in list)
		{
			if ((ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(item2)) || (Player.m_localPlayer != null && Player.m_localPlayer.HaveUniqueKey(item2)))
			{
				return true;
			}
		}
		return false;
	}

	private static void WriteReference()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Offline Companions Reborn - names for the [Cooking] settings. Written at every start from the game's own data, mods included.");
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("FOODS (Cooking > Foods)  name = in-game name  (health + stamina + eitr, where it's made)");
			SortedDictionary<string, string> sortedDictionary = new SortedDictionary<string, string>();
			foreach (Recipe recipe in Recipes)
			{
				string name = recipe.m_item.gameObject.name;
				if (!IsMeadBase(name) && Value(name) > 0f)
				{
					sortedDictionary[name] = $"{name} = {CookController.Name(name)}  ({Value(name):F0}, {CookController.Loc(recipe.m_craftingStation.m_name)})";
				}
			}
			foreach (KeyValuePair<string, string> item in s_convTo)
			{
				if (Value(item.Value) > 0f)
				{
					sortedDictionary[item.Value] = $"{item.Value} = {CookController.Name(item.Value)}  ({Value(item.Value):F0}, from {CookController.Name(item.Key)} on a cooking station / oven)";
				}
			}
			foreach (string value in sortedDictionary.Values)
			{
				stringBuilder.AppendLine("  " + value);
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("MEAD BASES (Cooking > MeadFocus)  name = in-game name -> mead");
			foreach (KeyValuePair<string, string> item2 in s_meadOf)
			{
				stringBuilder.AppendLine("  " + item2.Key + " = " + CookController.Name(item2.Key) + " -> " + CookController.Name(item2.Value));
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("BOSSES (Cooking > MeadFocus: name a boss by any part of its name or key; start = none beaten yet)");
			foreach (Boss item3 in Bosses())
			{
				stringBuilder.AppendLine("  " + item3.Name + "  (key " + item3.Key + ", prefab " + item3.Prefab + ")");
			}
			File.WriteAllText(Path.Combine(Paths.ConfigPath, "OfflineCompanionsReborn.Cooking.txt"), stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Cook] couldn't write OfflineCompanionsReborn.Cooking.txt: " + ex.Message);
		}
	}
}
