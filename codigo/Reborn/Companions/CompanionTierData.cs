using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class CompanionTierData
{
	public static readonly CompanionTierDef Companion;

	public static readonly CompanionTierDef DvergerWarrior;

	public static readonly CompanionTierDef Dverger;

	public static readonly CompanionTierDef DvergerFire;

	public static readonly CompanionTierDef DvergerIce;

	public static readonly CompanionTierDef DvergerSupport;

	public static readonly CompanionTierDef[] DvergerVariants;

	private static readonly HashSet<int> _dvergerHashes;

	public static int Price => ModConfig.CompanionPrice.Value;

	public static float MaxCarryWeight => ModConfig.MaxCarryWeight.Value;

	public static float CarryLimit(Component c)
	{
		float limit = MaxCarryWeight;
		Character character = ((c != null) ? c.GetComponent<Character>() : null);
		((character != null) ? character.GetSEMan() : null)?.ModifyMaxCarryWeight(MaxCarryWeight, ref limit);
		return limit * Game.m_carryWeightRate;
	}

	public static float CarryBonus(Component c)
	{
		return Mathf.Max(0f, CarryLimit(c) - MaxCarryWeight);
	}

	public static bool IsDvergerVariant(int prefabHash)
	{
		return _dvergerHashes.Contains(prefabHash);
	}

	static CompanionTierData()
	{
		Companion = new CompanionTierDef
		{
			PrefabName = "HC_Companion",
			DisplayName = "Companion",
			NameKey = "hc_tier_companion",
			SourcePrefab = "Player",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = true,
			GiveStarterGear = true
		};
		DvergerWarrior = new CompanionTierDef
		{
			PrefabName = "HC_DvergerWarrior",
			DisplayName = "Warrior",
			NameKey = "hc_tier_warrior",
			SourcePrefab = "Dverger",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		Dverger = new CompanionTierDef
		{
			PrefabName = "HC_Dverger",
			DisplayName = "Mage",
			NameKey = "hc_tier_mage",
			SourcePrefab = "DvergerMage",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerFire = new CompanionTierDef
		{
			PrefabName = "HC_DvergerFire",
			DisplayName = "Fire Mage",
			NameKey = "hc_tier_fire_mage",
			SourcePrefab = "DvergerMageFire",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerIce = new CompanionTierDef
		{
			PrefabName = "HC_DvergerIce",
			DisplayName = "Ice Mage",
			NameKey = "hc_tier_ice_mage",
			SourcePrefab = "DvergerMageIce",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerSupport = new CompanionTierDef
		{
			PrefabName = "HC_DvergerSupport",
			DisplayName = "Support",
			NameKey = "hc_tier_support",
			SourcePrefab = "DvergerMageSupport",
			Health = 25f,
			WalkSpeed = 2f,
			RunSpeed = 7f,
			CanWearArmor = false,
			GiveStarterGear = false
		};
		DvergerVariants = new CompanionTierDef[4] { DvergerWarrior, DvergerFire, DvergerIce, DvergerSupport };
		_dvergerHashes = new HashSet<int>();
		CompanionTierDef[] dvergerVariants = DvergerVariants;
		foreach (CompanionTierDef companionTierDef in dvergerVariants)
		{
			_dvergerHashes.Add(companionTierDef.PrefabName.GetStableHashCode());
		}
		_dvergerHashes.Add(Dverger.PrefabName.GetStableHashCode());
	}
}
