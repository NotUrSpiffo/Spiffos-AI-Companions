using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class ModConfig
{
	internal static readonly Dictionary<string, List<ConfigEntryBase>> Categories = new Dictionary<string, List<ConfigEntryBase>>();

	internal static readonly List<string> CategoryOrder = new List<string>();

	internal static ConfigEntry<int> CompanionPrice;

	internal static ConfigEntry<bool> RecoverUnsummoned;

	internal static ConfigEntry<string> PortalItems;

	internal static ConfigEntry<bool> SpawnStarterCompanion;

	internal static ConfigEntry<bool> ResetStarterFlag;

	internal static ConfigEntry<bool> ShowCompanionRoster;

	internal static ConfigEntry<bool> RecoverLostCompanion;

	internal static ConfigEntry<float> RosterSaveInterval;

	internal static ConfigEntry<bool> ShrineBuildable;

	internal static ConfigEntry<string> ShrineRecipe;

	internal static ConfigEntry<string> ShrineModel;

	internal static ConfigEntry<float> MaxCarryWeight;

	internal static ConfigEntry<float> BaseHealth;

	internal static ConfigEntry<float> BaseStamina;

	internal static ConfigEntry<int> MaxFoodSlots;

	internal static ConfigEntry<float> FollowTeleportDistance;

	internal static ConfigEntry<bool> ShowMinimapHudPanel;

	internal static ConfigEntry<float> HealthRetreatPct;

	internal static ConfigEntry<float> FleeHealthPct;

	internal static ConfigEntry<float> WeaponSwitchMargin;

	internal static ConfigEntry<bool> UseTorchOpener;

	internal static ConfigEntry<bool> TorchBurnsDown;

	internal static ConfigEntry<string> LogLevel;

	internal static ConfigEntry<float> TorchDarkThreshold;

	internal static ConfigEntry<bool> EngageOnNotice;

	internal static ConfigEntry<float> TorchOpenerTimeout;

	internal static ConfigEntry<float> TorchOpenerCooldown;

	internal static ConfigEntry<float> TorchOpenerMinTargetHealth;

	internal static ConfigEntry<float> WaterStaminaFleeThreshold;

	internal static ConfigEntry<bool> ShowOverheadBars;

	internal static ConfigEntry<bool> ShowStatus;

	internal static ConfigEntry<int> ShieldMultiAttackerCount;

	internal static ConfigEntry<float> ParryChance;

	internal static ConfigEntry<float> ParryChanceMax;

	internal static ConfigEntry<float> BlockReactionTime;

	internal static ConfigEntry<bool> LogPlayerCombat;

	internal static ConfigEntry<bool> FightLikePlayer;

	internal static ConfigEntry<int> CombatComboHits;

	internal static ConfigEntry<float> CombatComboPause;

	internal static ConfigEntry<float> CombatStaminaReserve;

	internal static ConfigEntry<float> CombatStaminaResume;

	internal static ConfigEntry<float> CombatRecoverTime;

	internal static ConfigEntry<float> StepAwayChance;

	internal static ConfigEntry<int> CombatGroupSize;

	internal static ConfigEntry<float> CombatGroupRange;

	internal static ConfigEntry<float> CombatFlankAngle;

	internal static ConfigEntry<int> CombatFinishHits;

	internal static ConfigEntry<bool> CombatSpacing;

	internal static ConfigEntry<bool> CombatFlank;

	internal static ConfigEntry<float> CombatSpacingMargin;

	internal static ConfigEntry<float> CombatSpacingStepChance;

	internal static ConfigEntry<float> AttackCooldown;

	internal static ConfigEntry<float> MeleeTooClose;

	internal static ConfigEntry<float> MeleeReachMargin;

	internal static ConfigEntry<float> BowSwitchDistance;

	internal static ConfigEntry<float> MeleeSwitchDistance;

	internal static ConfigEntry<float> FormationOffset;

	internal static ConfigEntry<float> FormationCatchupDist;

	internal static ConfigEntry<float> AutoPickupRange;

	internal static ConfigEntry<float> TombstoneScanInterval;

	internal static ConfigEntry<float> TombstoneNavTimeout;

	internal static ConfigEntry<bool> AutoRespawnEnabled;

	internal static ConfigEntry<float> AutoRespawnDelaySeconds;

	internal static ConfigEntry<bool> AutoRespawnAtWorldSpawnWhenNoBed;

	internal static ConfigEntry<bool> AutoRespawnAtPlayer;

	internal static ConfigEntry<int> DeathItemRule;

	internal static ConfigEntry<float> TombstoneRecoveryRange;

	internal static ConfigEntry<string> RespawnStance;

	internal static ConfigEntry<float> RecoverThreatRange;

	internal static ConfigEntry<int> RecoverMaxMobs;

	internal static ConfigEntry<bool> SleepAtNight;

	internal static ConfigEntry<bool> SleepWhenPlayerSleeps;

	internal static ConfigEntry<float> BedRange;

	internal static ConfigEntry<float> RecallRange;

	internal static ConfigEntry<float> SuperRecallHoldTime;

	internal static ConfigEntry<KeyCode> PassiveKey;

	internal static ConfigEntry<KeyCode> BalancedKey;

	internal static ConfigEntry<KeyCode> RangedKey;

	internal static ConfigEntry<KeyCode> MeleeKey;

	internal static ConfigEntry<bool> ProtectCompanions;

	internal static ConfigEntry<int> SeedReserve;

	internal static ConfigEntry<int> SeedTarget;

	internal static ConfigEntry<int> SeedCarry;

	internal static ConfigEntry<float> WorkRunDistance;

	internal static ConfigEntry<float> HomeTripRange;

	internal static ConfigEntry<bool> FollowStuckTeleport;

	internal static ConfigEntry<bool> CompanionNavMesh;

	internal static ConfigEntry<float> CookRange;

	internal static ConfigEntry<int> CookIngredientReserve;

	internal static ConfigEntry<int> CookFoodTarget;

	internal static ConfigEntry<int> CookBatchSize;

	internal static ConfigEntry<string> CookFoods;

	internal static ConfigEntry<bool> CookStoreWithCookedFood;

	internal static ConfigEntry<int> CookMeadBaseTarget;

	internal static ConfigEntry<int> CookMeadTarget;

	internal static ConfigEntry<string> CookMeadFocus;

	internal static ConfigEntry<bool> CookCauldron;

	internal static ConfigEntry<bool> CookPrepTable;

	internal static ConfigEntry<bool> CookMeadKetill;

	internal static ConfigEntry<bool> CookCookingStation;

	internal static ConfigEntry<bool> CookIronCookingStation;

	internal static ConfigEntry<bool> CookOven;

	internal static ConfigEntry<bool> CookFermenter;

	internal static ConfigEntry<bool> CookBeehives;

	internal static ConfigEntry<bool> CookOtherStations;

	internal static ConfigEntry<bool> StatusEffectsOnRegen;

	internal static ConfigEntry<float> DryDetour;

	internal static ConfigEntry<float> JobSwim;

	internal static ConfigEntry<float> MaxDrop;

	internal static ConfigEntry<float> StuckDrop;

	internal static ConfigEntry<float> JumpGap;

	internal static ConfigEntry<float> JumpWaterDepth;

	internal static ConfigEntry<float> JumpWaterWidth;

	internal static ConfigEntry<string> HuntPrey;

	internal static ConfigEntry<bool> HuntSneak;

	internal static ConfigEntry<bool> HuntThrowSpear;

	internal static ConfigEntry<float> HuntThrowRange;

	internal static ConfigEntry<float> HuntThrowStandoff;

	internal static ConfigEntry<float> HuntSneakRestStamina;

	internal static ConfigEntry<float> HuntSneakFrom;

	internal static ConfigEntry<float> HuntSwitchCloserBy;

	internal static ConfigEntry<float> HuntSwitchShare;

	internal static ConfigEntry<float> HuntSneakResumeStamina;

	internal static ConfigEntry<float> HuntChaseRunStamina;

	internal static ConfigEntry<bool> HuntBirds;

	internal static ConfigEntry<int> HuntFeatherTarget;

	internal static ConfigEntry<int> HuntMeatTarget;

	internal static ConfigEntry<float> HuntRange;

	internal static ConfigEntry<float> HuntFollowRange;

	internal static ConfigEntry<float> HuntChaseExtra;

	internal static ConfigEntry<float> HuntBagFull;

	internal static ConfigEntry<bool> LootKills;

	internal static ConfigEntry<float> LootRadius;

	internal static ConfigEntry<float> LootWait;

	internal static ConfigEntry<float> LootMaxDistance;

	internal static ConfigEntry<float> FollowMaxDistance;

	internal static ConfigEntry<bool> FollowAutoBoard;

	internal static ConfigEntry<float> FollowBoardRange;

	internal static ConfigEntry<float> FollowIndoorDistance;

	internal static ConfigEntry<bool> FollowRegroup;

	internal static ConfigEntry<float> FollowRegroupTime;

	internal static ConfigEntry<bool> FollowSitIndoors;

	internal static ConfigEntry<float> FollowChairRange;

	internal static ConfigEntry<float> FollowSitStayRange;

	internal static ConfigEntry<int> PlantGroupSize;

	internal static ConfigEntry<float> FarmNextCropRange;

	internal static ConfigEntry<string> PlantAny;

	internal static ConfigEntry<int> WoodLimit;

	internal static ConfigEntry<int> StoneLimit;

	internal static ConfigEntry<int> OreLimit;

	internal static ConfigEntry<string> GatherWood;

	internal static ConfigEntry<string> GatherStone;

	internal static ConfigEntry<string> GatherOre;

	internal static ConfigEntry<string> WoodSkipTrees;

	internal static ConfigEntry<float> BaseProtectRadius;

	internal static ConfigEntry<bool> JunkCleanup;

	internal static ConfigEntry<string> NeverDiscard;

	internal static ConfigEntry<float> WalkSpeed;

	internal static ConfigEntry<float> RunSpeed;

	internal static ConfigEntry<float> SwimSpeed;

	internal static ConfigEntry<float> TurnSpeed;

	internal static ConfigEntry<float> ViewRange;

	internal static ConfigEntry<float> ViewAngle;

	internal static ConfigEntry<float> FoodRegenInterval;

	internal static ConfigEntry<float> MeadCooldown;

	internal static ConfigEntry<float> HealthMeadThreshold;

	internal static ConfigEntry<float> StaminaMeadThreshold;

	internal static ConfigEntry<float> ConsumeCheckInterval;

	internal static ConfigEntry<float> StaminaRegenRate;

	internal static ConfigEntry<float> StaminaRunDrain;

	internal static ConfigEntry<float> StaminaSneakDrain;

	internal static ConfigEntry<float> StaminaSwimDrain;

	internal static ConfigEntry<float> StaminaRegenDelay;

	internal static ConfigEntry<float> HarvestScanRadius;

	internal static ConfigEntry<float> HarvestFollowRange;

	internal static ConfigEntry<float> HarvestFollowLogRange;

	internal static ConfigEntry<float> CombatBowMinDraw;

	internal static ConfigEntry<float> CombatBowAimTurnSpeed;

	internal static ConfigEntry<float> CombatRangedMeleeRange;

	internal static ConfigEntry<float> FoodEatAgainMinutes;

	internal static ConfigEntry<bool> CombatIndoorAggroOnly;

	internal static ConfigEntry<bool> CombatDungeonDefensive;

	internal static ConfigEntry<float> CombatDefendRange;

	internal static ConfigEntry<float> CombatOrderHoldTime;

	internal static ConfigEntry<float> CombatLeashFromYou;

	internal static ConfigEntry<float> CombatGiveUpTime;

	internal static ConfigEntry<float> CombatBowEdgeMargin;

	internal static ConfigEntry<float> CombatBowFullDrawRange;

	internal static ConfigEntry<float> GatherRestStamina;

	internal static ConfigEntry<float> GatherResumeStamina;

	internal static ConfigEntry<float> GatherPowerChopLogBelow;

	internal static ConfigEntry<float> ForageFollowRange;

	internal static ConfigEntry<float> HarvestRange;

	internal static ConfigEntry<bool> HarvestPickUpLoose;

	internal static ConfigEntry<float> HarvestScanInterval;

	internal static ConfigEntry<float> HarvestOverweightThreshold;

	internal static ConfigEntry<float> HarvestDropScanRadius;

	internal static ConfigEntry<float> HarvestBlacklistDuration;

	internal static ConfigEntry<string> ForageItems;

	internal static ConfigEntry<float> HomeZoneRadius;

	internal static ConfigEntry<float> SmeltScanRadius;

	internal static ConfigEntry<float> SmeltScanInterval;

	internal static ConfigEntry<int> SmeltMaxCarryOre;

	internal static ConfigEntry<int> SmeltMaxCarryFuel;

	internal static ConfigEntry<float> SmeltUseDistance;

	internal static ConfigEntry<float> FarmScanRadius;

	internal static ConfigEntry<bool> FarmPlantTilled;

	internal static ConfigEntry<float> FarmUnreachableForget;

	internal static ConfigEntry<float> FarmScanInterval;

	internal static ConfigEntry<float> FarmPlantSpacing;

	internal static ConfigEntry<float> FarmUseDistance;

	internal static ConfigEntry<float> FishScanRadius;

	internal static ConfigEntry<string> FishList;

	internal static ConfigEntry<int> FishTarget;

	internal static ConfigEntry<float> FishCastWait;

	internal static ConfigEntry<float> FishBagFull;

	internal static ConfigEntry<float> RepairDurabilityThreshold;

	internal static ConfigEntry<float> RepairHomeThreshold;

	internal static ConfigEntry<float> RepairNoStationWait;

	internal static ConfigEntry<float> RepairScanRadius;

	internal static ConfigEntry<float> RepairScanInterval;

	internal static ConfigEntry<float> RepairTickInterval;

	internal static ConfigEntry<float> RepairUseDistance;

	internal static ConfigEntry<bool> UpkeepRepair;

	internal static ConfigEntry<float> UpkeepRepairBelow;

	internal static ConfigEntry<float> UpkeepReach;

	internal static ConfigEntry<bool> UpkeepRestock;

	internal static ConfigEntry<bool> RefuelFires;

	internal static ConfigEntry<bool> RefuelHearths;

	internal static ConfigEntry<bool> RefuelBraziers;

	internal static ConfigEntry<bool> RefuelStandingTorches;

	internal static ConfigEntry<bool> RefuelWallTorches;

	internal static ConfigEntry<bool> RefuelHotTubs;

	internal static ConfigEntry<bool> RefuelOther;

	internal static ConfigEntry<float> UpkeepFuelBelow;

	internal static ConfigEntry<bool> UpkeepSort;

	internal static ConfigEntry<string> UpkeepSortItems;

	internal static ConfigEntry<bool> UpkeepSortGear;

	internal static ConfigEntry<float> UpkeepAwayRadius;

	internal static ConfigEntry<float> UpkeepScanInterval;

	internal static ConfigEntry<float> UpkeepWorkTime;

	internal static ConfigEntry<float> UpkeepGiveUp;

	internal static ConfigEntry<bool> UpkeepPiles;

	internal static ConfigEntry<bool> UpkeepHoney;

	internal static ConfigEntry<float> UpkeepHoneyAt;

	internal static ConfigEntry<float> UpkeepOrderWait;

	internal static ConfigEntry<bool> SmeltUsePiles;

	internal static ConfigEntry<float> CookFuelLevel;

	internal static ConfigEntry<bool> CookUsePiles;

	internal static ConfigEntry<bool> CookMakeMead;

	internal static ConfigEntry<bool> IdleEnabled;

	internal static ConfigEntry<float> IdleStartAfter;

	internal static ConfigEntry<float> IdleRadius;

	internal static ConfigEntry<bool> IdleChores;

	internal static ConfigEntry<float> IdleChoreRadius;

	internal static ConfigEntry<bool> IdleSit;

	internal static ConfigEntry<bool> IdleWander;

	internal static ConfigEntry<bool> IdleSocialize;

	internal static ConfigEntry<float> IdleSpeechInterval;

	internal static ConfigEntry<int> UpkeepPileKeep;

	internal static ConfigEntry<string> UpkeepPileItems;

	internal static ConfigEntry<float> RestHealRate;

	internal static ConfigEntry<float> RestPlayerRange;

	internal static ConfigEntry<float> RestFireRange;

	internal static ConfigEntry<float> RestWarmupTime;

	internal static ConfigEntry<float> RestDirectedTimeout;

	internal static ConfigEntry<bool> SpeechEnabled;

	internal static ConfigEntry<string> SpeechChatter;

	internal static ConfigEntry<bool> SpeechFarMessages;

	internal static ConfigEntry<bool> SpeechAnnounceDeath;

	internal static ConfigEntry<float> SpeechCullDistance;

	internal static ConfigEntry<float> SpeechDuration;

	internal static ConfigEntry<float> SpeechSayCooldown;

	internal static ConfigEntry<float> SkillDeathLossFactor;

	internal static ConfigEntry<KeyCode> DirectTargetKey;

	internal static ConfigEntry<KeyCode> RadialMenuKey;

	internal static ConfigEntry<KeyCode> ConfigPanelKey;

	internal static ConfigEntry<KeyCode> FarmZoneKey;

	internal static ConfigEntry<KeyCode> FarmZoneModifier;

	internal static ConfigEntry<string> PadCommand;

	internal static ConfigEntry<string> PadCommandModifier;

	internal static ConfigEntry<string> PadWheel;

	internal static ConfigEntry<string> PadWheelModifier;

	internal static ConfigEntry<string> PadSettings;

	internal static ConfigEntry<string> PadSettingsModifier;

	internal static ConfigEntry<string> PadFarmZone;

	internal static ConfigEntry<string> PadFarmZoneModifier;

	internal static ConfigEntry<string> PadPassive;

	internal static ConfigEntry<string> PadPassiveModifier;

	internal static ConfigEntry<string> PadBalanced;

	internal static ConfigEntry<string> PadBalancedModifier;

	internal static ConfigEntry<string> PadRanged;

	internal static ConfigEntry<string> PadRangedModifier;

	internal static ConfigEntry<string> PadMelee;

	internal static ConfigEntry<string> PadMeleeModifier;

	private static readonly string[][] s_moved = new string[70][]
	{
		new string[4] { "General", "ShowMinimapHudPanel", "General", "ShowMinimapPanel" },
		new string[4] { "Overhead", "ShowOverheadBars", "General", "ShowOverheadBars" },
		new string[4] { "StayHome", "HomeZoneRadius", "General", "HomeRadius" },
		new string[4] { "General", "FollowTeleportDistance", "Follow", "TeleportDistance" },
		new string[4] { "General", "FollowStuckTeleport", "Follow", "StuckTeleport" },
		new string[4] { "AI", "FormationOffset", "Follow", "FormationSpacing" },
		new string[4] { "AI", "FormationCatchupDist", "Follow", "FormationCatchUp" },
		new string[4] { "General", "CompanionPathing", "Movement", "Pathing" },
		new string[4] { "General", "WorkRunDistance", "Movement", "WorkRunDistance" },
		new string[4] { "General", "DryDetour", "Movement", "DryDetour" },
		new string[4] { "General", "JobSwim", "Movement", "JobSwim" },
		new string[4] { "General", "MaxDrop", "Movement", "MaxDrop" },
		new string[4] { "General", "JumpGap", "Movement", "JumpGap" },
		new string[4] { "General", "JumpWaterDepth", "Movement", "JumpWaterDepth" },
		new string[4] { "General", "JumpWaterWidth", "Movement", "JumpWaterWidth" },
		new string[4] { "Combat", "WaterStaminaFleeThreshold", "Movement", "SwimStaminaShore" },
		new string[4] { "Sleep", "SleepAtNight", "Rest", "SleepAtNight" },
		new string[4] { "Sleep", "SleepWhenPlayerSleeps", "Rest", "SleepWhenPlayerSleeps" },
		new string[4] { "Sleep", "BedRange", "Rest", "BedRange" },
		new string[4] { "Respawn", "AutoRespawnEnabled", "Death", "AutoRespawn" },
		new string[4] { "Respawn", "AutoRespawnDelaySeconds", "Death", "RespawnDelay" },
		new string[4] { "Respawn", "AutoRespawnAtWorldSpawnWhenNoBed", "Death", "RespawnAtWorldSpawn" },
		new string[4] { "Respawn", "DeathItemRule", "Death", "DeathItemRule" },
		new string[4] { "Respawn", "TombstoneRecoveryRange", "Death", "TombstoneRecoveryRange" },
		new string[4] { "Skills", "DeathLossFactor", "Death", "SkillLoss" },
		new string[4] { "AI", "AutoPickupRange", "Looting", "AutoPickupRange" },
		new string[4] { "Repair", "DurabilityThreshold", "GearRepair", "DurabilityThreshold" },
		new string[4] { "Repair", "HomeThreshold", "GearRepair", "HomeThreshold" },
		new string[4] { "Repair", "NoStationWait", "GearRepair", "NoStationWait" },
		new string[4] { "Repair", "ScanRadius", "GearRepair", "StationRange" },
		new string[4] { "Harvest", "Range", "Gathering", "Range" },
		new string[4] { "Harvest", "FollowRange", "Gathering", "FollowRange" },
		new string[4] { "Harvest", "PickUpLoose", "Gathering", "PickUpLoose" },
		new string[4] { "Harvest", "WoodLimit", "Gathering", "WoodLimit" },
		new string[4] { "Harvest", "StoneLimit", "Gathering", "StoneLimit" },
		new string[4] { "Harvest", "OreLimit", "Gathering", "OreLimit" },
		new string[4] { "Harvest", "GatherWood", "Gathering", "GatherWood" },
		new string[4] { "Harvest", "GatherStone", "Gathering", "GatherStone" },
		new string[4] { "Harvest", "GatherOre", "Gathering", "GatherOre" },
		new string[4] { "Harvest", "ForageItems", "Gathering", "ForageItems" },
		new string[4] { "Harvest", "WoodSkipTrees", "Gathering", "WoodSkipTrees" },
		new string[4] { "Harvest", "BaseProtectRadius", "Gathering", "BaseProtectRadius" },
		new string[4] { "Harvest", "JunkCleanup", "Gathering", "JunkCleanup" },
		new string[4] { "Harvest", "NeverDiscard", "Gathering", "NeverDiscard" },
		new string[4] { "Harvest", "OverweightThreshold", "Gathering", "OverweightThreshold" },
		new string[4] { "Farming", "ScanRadius", "Farming", "Range" },
		new string[4] { "Farm", "SeedMinimum", "Farming", "SeedMinimum" },
		new string[4] { "Farm", "SeedTarget", "Farming", "SeedTarget" },
		new string[4] { "Farm", "SeedCarry", "Farming", "SeedCarry" },
		new string[4] { "Farm", "PlantGroupSize", "Farming", "PlantGroupSize" },
		new string[4] { "Farm", "NextCropRange", "Farming", "NextCropRange" },
		new string[4] { "Farm", "PlantAny", "Farming", "PlantAny" },
		new string[4] { "Smelting", "ScanRadius", "Smelting", "Range" },
		new string[4] { "Harvest", "ScanRadius", "Advanced", "GatheringSearchRadius" },
		new string[4] { "Harvest", "ScanInterval", "Advanced", "GatheringScanInterval" },
		new string[4] { "Harvest", "DropScanRadius", "Advanced", "GatheringDropRadius" },
		new string[4] { "Harvest", "BlacklistDuration", "Advanced", "GatheringSkipTime" },
		new string[4] { "Farming", "ScanInterval", "Advanced", "FarmScanInterval" },
		new string[4] { "Farming", "UseDistance", "Advanced", "FarmUseDistance" },
		new string[4] { "Smelting", "ScanInterval", "Advanced", "SmeltScanInterval" },
		new string[4] { "Smelting", "UseDistance", "Advanced", "SmeltUseDistance" },
		new string[4] { "Repair", "ScanInterval", "Advanced", "GearRepairScanInterval" },
		new string[4] { "Repair", "RepairTickInterval", "Advanced", "GearRepairTickInterval" },
		new string[4] { "Repair", "UseDistance", "Advanced", "GearRepairUseDistance" },
		new string[4] { "RepairAndRestock", "ScanInterval", "Advanced", "RestockScanInterval" },
		new string[4] { "Combat", "AttackCooldown", "Advanced", "AttackCooldown" },
		new string[4] { "Food", "FoodRegenInterval", "Advanced", "FoodRegenInterval" },
		new string[4] { "Food", "ConsumeCheckInterval", "Advanced", "FoodCheckInterval" },
		new string[4] { "AI", "TombstoneScanInterval", "Advanced", "TombstoneScanInterval" },
		new string[4] { "AI", "TombstoneNavTimeout", "Advanced", "TombstoneNavTimeout" }
	};

	internal static void Init(ConfigFile cfg)
	{
		CompanionPrice = Bind(cfg, "General", "CompanionPrice", 1000, "The offering in coins to summon another companion at Odin's Shrine (Summon companion), paid from your pack.", new AcceptableValueRange<int>(0, 50000));
		PortalItems = Bind(cfg, "General", "PortalItems", "Game", "What a following companion may carry through a portal with you. Game: the same rules as you - ore, metals and the like only through a portal that allows everything or with the world's portal setting on Casual, boss items never; it first puts what can't go through in a chest within 15 m of it, otherwise it stays behind and says why. AllowAll: it comes through with anything.", new AcceptableValueList<string>("Game", "AllowAll"));
		RecoverUnsummoned = Bind(cfg, "General", "RecoverUnsummoned", defaultValue: true, "An unsummoned (despawned) companion can be brought back at Odin's Shrine (Recover companions) - with its name, looks, skills and settings, but not its gear, which stays in the tombstone where it was unsummoned - until you next load this world. Off: unsummoning is permanent.");
		if (CompanionPrice.Value == 2000)
		{
			CompanionPrice.Value = 1000;
		}
		SpawnStarterCompanion = Bind(cfg, "General", "SpawnStarterCompanion", defaultValue: true, "The first time you enter a world with a character, you're offered one companion for free.");
		ResetStarterFlag = Bind(cfg, "General", "ResetStarterFlag", defaultValue: false, "Set to true to be offered the free starter companion again in the current world (on the next world load). It turns itself back to false.");
		ShowCompanionRoster = Bind(cfg, "General", "ShowCompanionRoster", defaultValue: false, "Set to true to list your companions in this world and their status: Alive, Dead (its tombstone will bring it back), Lost (nothing left of it - it can be recovered) or Unknown (on a dedicated server, a companion that isn't nearby can't be checked). It turns itself back to false.");
		RecoverLostCompanion = Bind(cfg, "General", "RecoverLostCompanion", defaultValue: false, "Set to true to bring back a Lost companion next to you, with its gear and settings from the last roster save. An Unknown companion needs it set a second time within 60 s to confirm (it may still be alive somewhere). If the old companion turns up later, it's removed. It turns itself back to false.");
		ProtectCompanions = Bind(cfg, "General", "ProtectCompanions", defaultValue: true, "Stops other mods from deleting a companion outright (for example mods that clear creatures out of warded areas). Companions can still die, leave a tombstone and respawn as normal, and the Despawn button still works. When something is blocked, the log says what tried. Turn off only if another mod needs to remove companions.");
		ShowMinimapHudPanel = Bind(cfg, "General", "ShowMinimapPanel", defaultValue: true, "Show the companion status panel under the minimap.");
		ShowOverheadBars = Bind(cfg, "General", "ShowOverheadBars", defaultValue: true, "Show health and stamina bars above each companion's head.");
		ShowStatus = Bind(cfg, "General", "ShowStatus", defaultValue: true, "When you look at a companion, show what it is doing under its name - and why not, when its job is stuck (e.g. \"Hunt - No game around.\").");
		MaxCarryWeight = Bind(cfg, "General", "MaxCarryWeight", 300f, "How much weight a companion can carry (a player carries 300). Gathering and hunting companions head home to unload before this.", new AcceptableValueRange<float>(50f, 2000f));
		BaseHealth = Bind(cfg, "General", "BaseHealth", 25f, "A companion's health before food (a player has 25).", new AcceptableValueRange<float>(1f, 500f));
		BaseStamina = Bind(cfg, "General", "BaseStamina", 50f, "A companion's stamina before food (a player has 50).", new AcceptableValueRange<float>(1f, 500f));
		MaxFoodSlots = Bind(cfg, "General", "MaxFoodSlots", 3, "How many different foods a companion can have eaten at the same time (a player has 3).", new AcceptableValueRange<int>(1, 5));
		StatusEffectsOnRegen = Bind(cfg, "General", "StatusEffectsOnRegen", defaultValue: true, "Status effects change a companion's health and stamina regeneration like they do for a player: Wet and Cold slow it, meads and other buffs change it. Off = companions regenerate at their normal rate whatever the effects.");
		HomeTripRange = Bind(cfg, "General", "HomeTripRange", 60f, "How far from home (meters) a companion will still walk back for chest trips: fetching or putting back tools, repairing a broken tool, emptying its bags. Further away than this, a companion missing its job tool says so and follows you until you are both back near home.", new AcceptableValueRange<float>(10f, 500f));
		HomeZoneRadius = Bind(cfg, "General", "HomeRadius", 50f, "Starting size (meters) of a companion's home area around its home spot - where it looks for a bed to claim. Each companion's own size can be changed later in its home panel.", new AcceptableValueRange<float>(5f, 200f));
		DirectTargetKey = Bind(cfg, "Controls", "DirectTargetKey", KeyCode.Z, "The command key. Tap it while looking at something to send your companions to it (move there, attack it, open the door, sit by the fire, repair at the station...). Hold it to call them to you.");
		RadialMenuKey = Bind(cfg, "Controls", "RadialMenuKey", KeyCode.E, "Hold this while looking at a companion to open its command wheel.");
		ConfigPanelKey = Bind(cfg, "Controls", "ConfigPanelKey", KeyCode.F8, "Opens the in-game settings panel, where all of these settings can be changed.");
		FarmZoneKey = Bind(cfg, "Controls", "FarmZoneKey", KeyCode.Z, "Pressed together with FarmZoneModifier: opens the farm zone editor.");
		FarmZoneModifier = Bind(cfg, "Controls", "FarmZoneModifier", KeyCode.LeftAlt, "Hold this and press FarmZoneKey to open the farm zone editor.");
		RecallRange = Bind(cfg, "Controls", "RecallRange", 40f, "How close (meters) a companion has to be to you to answer a recall (holding the command key) or a point-and-command order. Companions with Command switched off on the wheel ignore both. 0 = no limit.", new AcceptableValueRange<float>(0f, 500f));
		SuperRecallHoldTime = Bind(cfg, "Controls", "SuperRecallHoldTime", 2f, "Keep holding the command key this many seconds to recall every companion at any distance, including ones with Command switched off.", new AcceptableValueRange<float>(0.5f, 10f));
		PassiveKey = Bind(cfg, "Controls", "PassiveKey", KeyCode.None, "Sets the Passive stance. Looking at one of your companions: just that one; otherwise every companion within RecallRange that has Command on. None = no key.");
		BalancedKey = Bind(cfg, "Controls", "BalancedKey", KeyCode.None, "Sets the Balanced stance, like PassiveKey.");
		RangedKey = Bind(cfg, "Controls", "RangedKey", KeyCode.None, "Sets the Ranged stance, like PassiveKey.");
		MeleeKey = Bind(cfg, "Controls", "MeleeKey", KeyCode.None, "Sets the Melee stance, like PassiveKey.");
		AcceptableValueList<string> range = new AcceptableValueList<string>(MHR_Pad.Choices);
		PadCommand = Bind(cfg, "Controller", "Command", "Y", "Controller command button: tap while looking at something to send your companions to it, hold to call them to you. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadCommandModifier = Bind(cfg, "Controller", "CommandModifier", "Alt", "Held with Command. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadWheel = Bind(cfg, "Controller", "Wheel", "X", "Controller button for a companion's command wheel, while looking at it. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadWheelModifier = Bind(cfg, "Controller", "WheelModifier", "Alt", "Held with Wheel. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadSettings = Bind(cfg, "Controller", "Settings", "None", "Controller button for this settings panel. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadSettingsModifier = Bind(cfg, "Controller", "SettingsModifier", "None", "Held with Settings. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadFarmZone = Bind(cfg, "Controller", "FarmZone", "None", "Controller button for the farm zone editor. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadFarmZoneModifier = Bind(cfg, "Controller", "FarmZoneModifier", "None", "Held with FarmZone. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadPassive = Bind(cfg, "Controller", "Passive", "None", "Controller button for the Passive stance (like PassiveKey). None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadPassiveModifier = Bind(cfg, "Controller", "PassiveModifier", "None", "Held with Passive. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadBalanced = Bind(cfg, "Controller", "Balanced", "None", "Controller button for the Balanced stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadBalancedModifier = Bind(cfg, "Controller", "BalancedModifier", "None", "Held with Balanced. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadRanged = Bind(cfg, "Controller", "Ranged", "None", "Controller button for the Ranged stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadRangedModifier = Bind(cfg, "Controller", "RangedModifier", "None", "Held with Ranged. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		PadMelee = Bind(cfg, "Controller", "Melee", "None", "Controller button for the Melee stance. None = off. Use and Alt follow the game's controller layout (Alt is the game's own modifier - your block button); the rest are fixed buttons.", range);
		PadMeleeModifier = Bind(cfg, "Controller", "MeleeModifier", "None", "Held with Melee. Held while pressing the button. While it's held, that button's press is for the companions only (the game doesn't see it); on its own the button works as normal. None = the button alone.", range);
		FollowMaxDistance = Bind(cfg, "Follow", "MaxDistance", 10f, "How far a following companion lets you get before it moves to keep up. It picks its own spot within this range (beside, behind, sometimes ahead) and changes it now and then.", new AcceptableValueRange<float>(4f, 30f));
		FollowAutoBoard = Bind(cfg, "Follow", "AutoBoard", defaultValue: true, "When you're aboard a ship close by, a following companion gets on too, the way you would: it jumps aboard from a dock or the shore, uses the ship's ladder (swimming to it if it has the stamina), or follows your own steps aboard. If it can't, it waits on the shore. A companion that took a seat gets up again when you go ashore.");
		FollowBoardRange = Bind(cfg, "Follow", "BoardRange", 20f, "AutoBoard: how close (meters) the ship has to be for a following companion to board it on its own. The ship also has to be nearly still.", new AcceptableValueRange<float>(5f, 60f));
		FollowIndoorDistance = Bind(cfg, "Follow", "IndoorDistance", 4f, "The same range while you're indoors (under a roof), so companions stay in the room with you.", new AcceptableValueRange<float>(2f, 10f));
		FollowRegroup = Bind(cfg, "Follow", "Regroup", defaultValue: true, "When you stand still for RegroupTime seconds, companions come back and wait close to you. Off = they stay wherever they stopped.");
		FollowRegroupTime = Bind(cfg, "Follow", "RegroupTime", 45f, "Seconds you stand still before companions regroup around you (see Regroup).", new AcceptableValueRange<float>(5f, 600f));
		FollowSitIndoors = Bind(cfg, "Follow", "SitIndoors", defaultValue: true, "When you stop indoors, companions sit down near you: on a free chair or bench if there is one, otherwise on the floor. Near home they empty their bags first.");
		FollowChairRange = Bind(cfg, "Follow", "ChairRange", 8f, "How far (meters) from you a companion looks for a free chair or bench to sit on when you stop indoors - one in sight of you, on your level. Only when there's none does it sit on the floor.", new AcceptableValueRange<float>(2f, 30f));
		FollowSitStayRange = Bind(cfg, "Follow", "SitStayRange", 10f, "Indoors, a companion that sat down stays seated while you move about within this many meters of it, and gets up to follow once you go further (or step outside).", new AcceptableValueRange<float>(2f, 30f));
		FollowTeleportDistance = Bind(cfg, "Follow", "TeleportDistance", 50f, "A following companion that falls this many meters behind appears next to you: right away beyond 1.5x this distance, otherwise once it can't see you and isn't getting any closer.", new AcceptableValueRange<float>(20f, 200f));
		FollowStuckTeleport = Bind(cfg, "Follow", "StuckTeleport", defaultValue: false, "When following you, a companion that has been stuck for 15 seconds somewhere it can't see you appears next to you. Off = it keeps trying to find its own way.");
		FormationOffset = Bind(cfg, "Follow", "FormationSpacing", 3f, "Space (meters) between companions walking side by side in formation (when they follow something other than you).", new AcceptableValueRange<float>(1f, 10f));
		FormationCatchupDist = Bind(cfg, "Follow", "FormationCatchUp", 15f, "In formation, a companion this many meters away from its place runs to catch up.", new AcceptableValueRange<float>(5f, 50f));
		WalkSpeed = Bind(cfg, "Movement", "WalkSpeed", 2f, "Walking speed (a player walks at about 2). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(0.5f, 5f));
		RunSpeed = Bind(cfg, "Movement", "RunSpeed", 7f, "Running speed (a player runs at about 7). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(2f, 15f));
		SwimSpeed = Bind(cfg, "Movement", "SwimSpeed", 2f, "Swimming speed (a player swims at about 2). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(0.5f, 5f));
		TurnSpeed = Bind(cfg, "Movement", "TurnSpeed", 300f, "How fast a companion turns (degrees per second). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(50f, 600f));
		ViewRange = Bind(cfg, "Movement", "ViewRange", 20f, "How far (meters) a companion can see enemies and other creatures. Takes effect after the world is reloaded.", new AcceptableValueRange<float>(5f, 100f));
		ViewAngle = Bind(cfg, "Movement", "ViewAngle", 120f, "A companion's field of view (degrees). Takes effect after the world is reloaded.", new AcceptableValueRange<float>(30f, 360f));
		CompanionNavMesh = Bind(cfg, "Movement", "Pathing", defaultValue: true, "Companions find their way with their own map of the world, where doors don't block the way (they open them as they get there) and steps up to 0.6 m are climbable (they hop them). Off = the same map creatures use, where every closed door is a wall and most stairs don't connect. Takes effect after a restart.");
		WorkRunDistance = Bind(cfg, "Movement", "WorkRunDistance", 25f, "Companions walk while doing jobs (farming, chest trips, smelting, cooking, chores, repairs, gathering) and only run when what they are heading to is further away than this many meters. 0 = never run for work. Following you, fighting and hunting are not affected.", new AcceptableValueRange<float>(0f, 500f));
		DryDetour = Bind(cfg, "Movement", "DryDetour", 30f, "Companions prefer staying dry, like a player: when the way somewhere crosses water they go round it on land, as long as the dry way is no longer than 1.5x the swimming way plus this many meters. Otherwise they swim.", new AcceptableValueRange<float>(0f, 500f));
		JobSwim = Bind(cfg, "Movement", "JobSwim", 20f, "The most swimming (meters) a job accepts to reach something: loot from a kill, prey, a bird, a fishing spot. Further than that and it leaves it.", new AcceptableValueRange<float>(0f, 200f));
		MaxDrop = Bind(cfg, "Movement", "MaxDrop", 3f, "When a straight line is much shorter than the way round (down the stairs and back, round a wall), companions take it like a player: hopping steps, dropping off ledges up to this many meters (players take no fall damage below 4 m) and jumping small gaps (JumpGap).", new AcceptableValueRange<float>(1f, 4f));
		StuckDrop = Bind(cfg, "Movement", "StuckDrop", 8f, "When a companion is stuck above where it needs to be with no other way down (off the top of a ladder, a platform, a ledge), it jumps down a drop up to this many meters, like a player who takes a bit of fall damage rather than stay put. A ladder down comes first, and it never takes a drop that would cost more than 30% of its health right now (companions take fall damage like a player: none up to 4 m).", new AcceptableValueRange<float>(3f, 20f));
		JumpGap = Bind(cfg, "Movement", "JumpGap", 1.5f, "The widest gap (meters) a companion leaps across on such a shortcut. 0.5 = no gaps.", new AcceptableValueRange<float>(0.5f, 3f));
		JumpWaterDepth = Bind(cfg, "Movement", "JumpWaterDepth", 0.3f, "Water at least this deep (meters) in a companion's way is jumped over when it's narrow enough (JumpWaterWidth), like a player jumps a small stream instead of running through it. Wider shallow water is waded through.", new AcceptableValueRange<float>(0.05f, 2f));
		JumpWaterWidth = Bind(cfg, "Movement", "JumpWaterWidth", 3f, "The widest stretch of water (meters) a companion jumps over.", new AcceptableValueRange<float>(1f, 4f));
		WaterStaminaFleeThreshold = Bind(cfg, "Movement", "SwimStaminaShore", 0.25f, "When a swimming companion's stamina drops below this share (0.25 = 25%), it heads for the nearest shore, whatever it was doing.", new AcceptableValueRange<float>(0f, 1f));
		EngageOnNotice = Bind(cfg, "Combat", "EngageOnNotice", defaultValue: true, "On: companions attack a creature as soon as it has noticed you or a companion. Off: they wait until it is actually coming for you (alerted). Anything that attacks is always fought back, and Hunt goes after its prey either way.");
		HealthRetreatPct = Bind(cfg, "Combat", "HealthRetreatPct", 0.3f, "Below this share of its health (0.3 = 30%) a companion fights defensively: it takes out its best blocking shield and doesn't open with a torch.", new AcceptableValueRange<float>(0.05f, 0.9f));
		FleeHealthPct = Bind(cfg, "Combat", "FleeHealthPct", 0.15f, "Below this share of its health (0.15 = 15%) a companion runs away from the enemy.", new AcceptableValueRange<float>(0.05f, 0.6f));
		WeaponSwitchMargin = Bind(cfg, "Combat", "WeaponSwitchMargin", 0.1f, "A different weapon has to be this much better against the current enemy (0.1 = 10%) before a companion switches to it, so it doesn't swap back and forth.", new AcceptableValueRange<float>(0f, 0.5f));
		ShieldMultiAttackerCount = Bind(cfg, "Combat", "ShieldMultiAttackerCount", 3, "When this many enemies attack a companion at once, it switches to its shield with the best block.", new AcceptableValueRange<int>(1, 10));
		ParryChance = Bind(cfg, "Combat", "ParryChance", 0.3f, "Chance (0.3 = 30%) that a companion with no Blocking skill times a block as a parry (more damage stopped, less stamina, and many attackers are staggered). It rises with Blocking skill up to ParryChanceMax. Shields without a parry bonus never parry.", new AcceptableValueRange<float>(0f, 1f));
		ParryChanceMax = Bind(cfg, "Combat", "ParryChanceMax", 0.7f, "Chance (0.7 = 70%) that a companion times a block as a parry at Blocking skill 100.", new AcceptableValueRange<float>(0f, 1f));
		FightLikePlayer = Bind(cfg, "Combat", "FightLikePlayer", defaultValue: true, "With a melee weapon, companions fight like a player: short combos when there's an opening, guarding in between, stepping out of swings, getting clear when several enemies are on them, and backing off to catch their breath when low on stamina. Off: the older way (one swing at a time on the weapon's timer).");
		CombatComboHits = Bind(cfg, "Combat", "ComboHits", 3, "Most swings in one combo. Companions follow their weapon's own combo (3 swings on melee weapons, the last doing double damage) up to this many; now and then one short, and shorter when stamina is low.", new AcceptableValueRange<int>(1, 5));
		CombatComboPause = Bind(cfg, "Combat", "ComboPause", 0.5f, "Seconds (roughly - it varies) between combos, guarding or stepping back out of reach.", new AcceptableValueRange<float>(0.2f, 5f));
		CombatStaminaReserve = Bind(cfg, "Combat", "StaminaReserve", 0.3f, "Share of its stamina (0.3 = 30%) a companion keeps for blocking. A combo is only started above it and may dip to half of it; below it, the companion backs out of reach to get its breath back.", new AcceptableValueRange<float>(0f, 0.9f));
		CombatStaminaResume = Bind(cfg, "Combat", "StaminaResume", 0.5f, "Share of its stamina (0.5 = 50%) it waits for before going back in after backing off.", new AcceptableValueRange<float>(0f, 1f));
		CombatRecoverTime = Bind(cfg, "Combat", "BackOffTime", 8f, "Seconds a companion keeps trying to back off from an enemy that stays on it before it fights on with what stamina it has.", new AcceptableValueRange<float>(2f, 30f));
		StepAwayChance = Bind(cfg, "Combat", "StepAwayChance", 0.3f, "Chance (0.3 = 30%) a companion steps back or aside out of an enemy's swing instead of blocking it, when there's room and it can get out of reach in time. Doubled when it's low on stamina.", new AcceptableValueRange<float>(0f, 1f));
		CombatGroupSize = Bind(cfg, "Combat", "GroupSize", 2, "With this many enemies close enough to hit a companion, from different sides, it gets clear first - moves for a couple of seconds until they're all on one side - then fights from there.", new AcceptableValueRange<int>(2, 10));
		CombatGroupRange = Bind(cfg, "Combat", "GroupRange", 6f, "How close (meters) an enemy has to be to count as one of the group on a companion.", new AcceptableValueRange<float>(2f, 20f));
		CombatFlankAngle = Bind(cfg, "Combat", "FlankAngle", 100f, "Two enemies more than this many degrees apart, seen from the companion, count as on different sides.", new AcceptableValueRange<float>(30f, 180f));
		CombatBowAimTurnSpeed = Bind(cfg, "Combat", "BowAimTurnSpeed", 600f, "Bows: how fast a companion turns to keep its aim on the target while the bow is drawn (degrees per second), like you swinging the camera round. Movement > TurnSpeed is used the rest of the time.", new AcceptableValueRange<float>(100f, 1440f));
		CombatBowMinDraw = Bind(cfg, "Combat", "BowMinDraw", 0.5f, "Bows: the least a companion draws before letting go (0.5 = half drawn). A partial draw is quicker and cheaper but does less damage and spreads more, like yours. It takes a partial draw when the target is close enough that the spread still hits it, when it's low on stamina (below StaminaReserve), or when the target is rushing in; otherwise it draws fully.", new AcceptableValueRange<float>(0.1f, 1f));
		CombatIndoorAggroOnly = Bind(cfg, "Combat", "IndoorAggroOnly", defaultValue: true, "Indoors - in a dungeon, mine or cave, or under a roof - a companion only fights enemies that are actually coming for you or it (alerted), as if EngageOnNotice were off. Outdoors EngageOnNotice decides.");
		CombatDungeonDefensive = Bind(cfg, "Combat", "DungeonDefensive", defaultValue: true, "In a dungeon, mine or cave a companion fights defensively: it stays with you and fights what comes within DefendRange of you (and anything hitting it), instead of running off after enemies further down the corridor.");
		CombatDefendRange = Bind(cfg, "Combat", "DefendRange", 8f, "Fighting defensively (DungeonDefensive): enemies within this many meters of you are fought; further off they're left to come to you.", new AcceptableValueRange<float>(3f, 30f));
		CombatOrderHoldTime = Bind(cfg, "Combat", "OrderHoldTime", 10f, "After you recall a companion or send it somewhere, it leaves its fight and starts no new one for this many seconds - it still fights back against something hitting it up close. Companions only ever fight enemies they can see (not ones behind walls or closed doors), and only open a door to get at one when you order the attack.", new AcceptableValueRange<float>(0f, 60f));
		CombatLeashFromYou = Bind(cfg, "Combat", "LeashFromYou", 35f, "A companion lets an enemy go once it is further than this many meters from you, and starts no fight with one that far from you - it comes back to you instead. An attack order, or something hitting it up close, still keeps the fight going.", new AcceptableValueRange<float>(10f, 60f));
		CombatGiveUpTime = Bind(cfg, "Combat", "GiveUpTime", 10f, "An enemy a companion can't get at with its melee weapon (out in deep water, no way to walk there) and that isn't right on top of it is let go after this many seconds without being hurt, and left alone for 20 s - so it doesn't stay stuck on it.", new AcceptableValueRange<float>(3f, 60f));
		CombatBowEdgeMargin = Bind(cfg, "Combat", "BowEdgeMargin", 3f, "Bows: a draw is only started this many meters inside the bow's 30 m reach, and a draw already started is kept while the target is up to twice this far past the reach (walking closer, not running), so a target at the edge doesn't cost the draw.", new AcceptableValueRange<float>(0f, 10f));
		CombatRangedMeleeRange = Bind(cfg, "Combat", "RangedMeleeRange", 4f, "Ranged stance: the bow is out (also when not fighting); the melee weapon and shield only come out when an enemy gets within this many meters - or twice that while it's catching its breath - and go away again once it's past twice the distance.", new AcceptableValueRange<float>(1f, 15f));
		CombatBowFullDrawRange = Bind(cfg, "Combat", "BowFullDrawRange", 15f, "Bows: from this many meters away a companion always draws fully - a weaker arrow is slower and drops short at range.", new AcceptableValueRange<float>(3f, 50f));
		CombatFinishHits = Bind(cfg, "Combat", "FinishHits", 2, "An enemy this many hits or fewer from dead (judged from its own hits so far) is finished off with all the stamina it takes, as long as nothing else is close enough to hit the companion afterwards. 0 = never spend the stamina kept back for blocking.", new AcceptableValueRange<int>(0, 5));
		CombatSpacing = Bind(cfg, "Combat", "Spacing", defaultValue: true, "One on one, a companion keeps just outside the enemy's reach, goes in for a combo when the enemy can't swing again in time, then steps back out - and steps out of swings at the edge of its reach rather than blocking them.");
		CombatFlank = Bind(cfg, "Combat", "Flank", defaultValue: true, "When an enemy is busy with you (or someone else), a companion works its way round behind it and attacks from there, out of its swings. (The game's backstab bonus only counts on enemies that haven't noticed anyone.)");
		CombatSpacingMargin = Bind(cfg, "Combat", "SpacingMargin", 0.4f, "How far (meters) outside the enemy's reach a companion waits when spacing.", new AcceptableValueRange<float>(0f, 2f));
		CombatSpacingStepChance = Bind(cfg, "Combat", "SpacingStepChance", 0.6f, "Chance (0.6 = 60%) that a spacing companion steps back out of a swing it's only just inside the reach of, instead of blocking it (the rest are blocks, some of them parries).", new AcceptableValueRange<float>(0f, 1f));
		BowSwitchDistance = Bind(cfg, "Combat", "BowSwitchDistance", 15f, "A companion with a bow or crossbow switches to it when its target is further away than this (meters).", new AcceptableValueRange<float>(5f, 60f));
		MeleeSwitchDistance = Bind(cfg, "Combat", "MeleeSwitchDistance", 12f, "It switches back to its melee weapon when the target comes closer than this (meters).", new AcceptableValueRange<float>(3f, 40f));
		UseTorchOpener = Bind(cfg, "Combat", "UseTorchOpener", defaultValue: true, "Against enemies that are weak to fire, open with a torch to set them alight, then switch back to the best weapon.");
		TorchBurnsDown = Bind(cfg, "Torch", "BurnsDown", defaultValue: true, "A torch in a companion's hand burns down like yours and is gone at 0 durability. Off: companions' torches last forever (the game itself only burns down players' torches).");
		TorchDarkThreshold = Bind(cfg, "Torch", "DarkLevel", 0.4f, "In a dungeon or cave, a companion lights a torch when the light there is below this (0 = pitch black, 1 = bright day). The log shows the light level each time you go into one ([Torch] lines), to tune this. Outdoors it's simply night.", new AcceptableValueRange<float>(0f, 1f));
		TorchOpenerTimeout = Bind(cfg, "Combat", "TorchOpenerTimeout", 4f, "Seconds to keep trying to set an enemy alight before giving up and switching back.", new AcceptableValueRange<float>(1f, 10f));
		TorchOpenerCooldown = Bind(cfg, "Combat", "TorchOpenerCooldown", 10f, "Seconds before the same enemy can be opened on with a torch again.", new AcceptableValueRange<float>(0f, 60f));
		TorchOpenerMinTargetHealth = Bind(cfg, "Combat", "TorchOpenerMinTargetHealth", 40f, "No torch opener on enemies with less maximum health than this - not worth it.", new AcceptableValueRange<float>(0f, 500f));
		HealthMeadThreshold = Bind(cfg, "Food", "HealthMeadThreshold", 0.5f, "A companion drinks a healing mead from its pack when its health drops below this share (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		StaminaMeadThreshold = Bind(cfg, "Food", "StaminaMeadThreshold", 0.25f, "It drinks a stamina mead when its stamina drops below this share (0.25 = 25%).", new AcceptableValueRange<float>(0.05f, 0.9f));
		FoodEatAgainMinutes = Bind(cfg, "Food", "EatAgainMinutes", 3f, "A companion eats the same food again once it has this many minutes left, like a player topping up before it runs out (the game only allows that once a food is under half its time). It needs that food in its pack; otherwise it eats something else when the slot empties. 0 = wait until it runs out.", new AcceptableValueRange<float>(0f, 30f));
		MeadCooldown = Bind(cfg, "Food", "MeadCooldown", 10f, "Seconds after drinking a mead before it drinks another.", new AcceptableValueRange<float>(1f, 60f));
		StaminaRegenRate = Bind(cfg, "Stamina", "RegenRate", 6f, "Stamina a companion gets back per second when it isn't using any.", new AcceptableValueRange<float>(1f, 30f));
		StaminaRunDrain = Bind(cfg, "Stamina", "RunDrainRate", 10f, "Stamina used per second while running.", new AcceptableValueRange<float>(1f, 30f));
		StaminaSneakDrain = Bind(cfg, "Stamina", "SneakDrainRate", 5f, "Stamina used per second while sneaking.", new AcceptableValueRange<float>(1f, 30f));
		StaminaSwimDrain = Bind(cfg, "Stamina", "SwimDrainAtSkill0", 5f, "Stamina used per second while swimming (and moving), at Swim skill 0 - it drops to 40% of this at Swim 100, like the player (the game's rate is 5).", new AcceptableValueRange<float>(1f, 30f));
		StaminaRegenDelay = Bind(cfg, "Stamina", "RegenDelay", 1f, "Seconds after using stamina before it starts coming back.", new AcceptableValueRange<float>(0.1f, 5f));
		SleepAtNight = Bind(cfg, "Rest", "SleepAtNight", defaultValue: true, "At night, a companion within BedRange of its claimed bed stops what it's doing and goes to bed, including when it's working. It goes back to its job in the morning.");
		SleepWhenPlayerSleeps = Bind(cfg, "Rest", "SleepWhenPlayerSleeps", defaultValue: true, "When you get into a bed, your companions within BedRange of their claimed beds go to bed too, even in the evening. The night skip waits up to 30 seconds for them.");
		BedRange = Bind(cfg, "Rest", "BedRange", 50f, "How close (meters) a companion has to be to its claimed bed for the two settings above to send it to bed.", new AcceptableValueRange<float>(10f, 200f));
		RestHealRate = Bind(cfg, "Rest", "HealRate", 2f, "Health a companion gets back per second while sitting by a fire or sleeping.", new AcceptableValueRange<float>(0.5f, 20f));
		RestPlayerRange = Bind(cfg, "Rest", "PlayerRange", 5f, "When you sit down near a fire (the sit emote), companions without a job within this many meters of you sit down by it too.", new AcceptableValueRange<float>(2f, 20f));
		RestFireRange = Bind(cfg, "Rest", "FireRange", 5f, "How close (meters) a fire has to be for a companion to sit down by it.", new AcceptableValueRange<float>(2f, 20f));
		RestWarmupTime = Bind(cfg, "Rest", "WarmupTime", 20f, "Seconds of sitting by a fire before a companion becomes Rested, like a player.", new AcceptableValueRange<float>(5f, 120f));
		RestDirectedTimeout = Bind(cfg, "Rest", "DirectedTimeout", 300f, "When you tell a companion to sit by a fire, it gets up again after this many seconds.", new AcceptableValueRange<float>(30f, 1800f));
		AutoRespawnEnabled = Bind(cfg, "Death", "AutoRespawn", defaultValue: true, "A dead companion comes back by itself after RespawnDelay seconds if nobody brings it back from its tombstone first.");
		AutoRespawnDelaySeconds = Bind(cfg, "Death", "RespawnDelay", 300f, "Seconds after death before a companion comes back by itself.", new AcceptableValueRange<float>(30f, 3600f));
		AutoRespawnAtWorldSpawnWhenNoBed = Bind(cfg, "Death", "RespawnAtWorldSpawn", defaultValue: false, "A companion that comes back by itself and has no claimed bed appears at the world spawn stones. Off = next to you.");
		AutoRespawnAtPlayer = Bind(cfg, "Death", "RespawnAtPlayer", defaultValue: false, "A companion that comes back by itself always appears next to you, even when it has a bed (wins over RespawnAtWorldSpawn). Off = at its bed, and without one as RespawnAtWorldSpawn says.");
		DeathItemRule = Bind(cfg, "Death", "DeathItemRule", 0, "What a companion loses when it dies. 0 = the world's death penalty setting, same as a player. 1 = keep everything. 2 = keep equipped gear, the rest goes in the tombstone. 3 = everything goes in the tombstone. The owner's setting is used even if another player's game handles the death.", new AcceptableValueRange<int>(0, 3));
		TombstoneRecoveryRange = Bind(cfg, "Death", "TombstoneRecoveryRange", 50f, "A companion walks over and empties its own leftover tombstone when it's within this many meters of it.", new AcceptableValueRange<float>(5f, 100f));
		RespawnStance = Bind(cfg, "Death", "RespawnStance", "RecoverFirst", "The stance a companion comes back in after it died. RecoverFirst: Passive, auto-pickup off, it gets its gear back from its tombstone when it's safe (fighting back only if attacked with a weapon in hand), puts it on, eats, then goes back to the stance it died with. Passive: Passive until you change it. Keep: the stance it died with. Setting a stance yourself always wins.", new AcceptableValueList<string>("RecoverFirst", "Passive", "Keep"));
		RecoverThreatRange = Bind(cfg, "Death", "RecoverThreatRange", 15f, "RecoverFirst: enemies within this many meters of the companion or its tombstone count when it decides whether it's safe to go for its gear.", new AcceptableValueRange<float>(5f, 50f));
		RecoverMaxMobs = Bind(cfg, "Death", "RecoverMaxMobs", 3, "RecoverFirst: with more enemies than this busy fighting near its tombstone it waits by you instead of going for its gear.", new AcceptableValueRange<int>(0, 20));
		ShrineBuildable = Bind(cfg, "Death", "ShrineBuildable", defaultValue: true, "Odin's Shrine can be built with the hammer (Misc tab, needs a workbench). Use it to see your companions and bring back a lost one. Takes effect on the next world load; shrines already built keep working.");
		ShrineRecipe = Bind(cfg, "Death", "ShrineRecipe", "Stone:10,Wood:5,GreydwarfEye:3", "What Odin's Shrine costs to build, as Item:Amount pairs separated by commas (item names as in the game's spawn command). Takes effect on the next world load.");
		ShrineModel = Bind(cfg, "Death", "ShrineModel", "Tombstone", "What Odin's Shrine looks like: Tombstone (a player's gravestone) or Ward (the ward stone). Takes effect on the next world load, for shrines already built too.");
		SkillDeathLossFactor = Bind(cfg, "Death", "SkillLoss", 0.25f, "Share of its skills a companion loses when it dies (0.25 = 25%, 0 = none).", new AcceptableValueRange<float>(0f, 1f));
		LootKills = Bind(cfg, "Looting", "PickUpKills", defaultValue: true, "After a companion kills something it goes over and picks up what the creature dropped (things you'd auto-pickup yourself), like a player would. Needs Auto Pickup on in the wheel.");
		LootRadius = Bind(cfg, "Looting", "Radius", 6f, "How far around the kill (meters) the drops are looked for.", new AcceptableValueRange<float>(2f, 20f));
		LootWait = Bind(cfg, "Looting", "WaitSeconds", 10f, "Longest wait (seconds) by the body for its drops to come out.", new AcceptableValueRange<float>(2f, 60f));
		LootMaxDistance = Bind(cfg, "Looting", "MaxDistance", 30f, "Kills further away than this (meters), or that you've moved this far away from while it follows you, are left.", new AcceptableValueRange<float>(5f, 100f));
		AutoPickupRange = Bind(cfg, "Looting", "AutoPickupRange", 2f, "With Auto Pickup on in the wheel, a companion picks up items within this many meters of itself as it goes, like a player's auto-pickup.", new AcceptableValueRange<float>(0.5f, 10f));
		RepairDurabilityThreshold = Bind(cfg, "GearRepair", "DurabilityThreshold", 0.5f, "A companion takes its gear to a crafting station to repair it when something drops below this share of durability (0.5 = 50%).", new AcceptableValueRange<float>(0.1f, 0.9f));
		RepairHomeThreshold = Bind(cfg, "GearRepair", "HomeThreshold", 0.8f, "At home (or, without a home set, inside a base - a workbench's area), gear is repaired a bit sooner: below this share (0.8 = 80%). 1 = at any wear. Away from home DurabilityThreshold applies.", new AcceptableValueRange<float>(0.1f, 1f));
		RepairNoStationWait = Bind(cfg, "GearRepair", "NoStationWait", 600f, "When a worn item can't be repaired at any station within reach (say it needs a black forge and there isn't one nearby), or the way to the station failed, that item is left alone for this many seconds before it looks again.", new AcceptableValueRange<float>(30f, 3600f));
		RepairScanRadius = Bind(cfg, "GearRepair", "StationRange", 20f, "How far (meters) a companion looks for a crafting station to repair at. Near home, any station near home counts.", new AcceptableValueRange<float>(5f, 60f));
		HarvestRange = Bind(cfg, "Gathering", "Range", 40f, "Wood, Stone, Ore and Forage with Follow off: how far (meters) from where it's working it goes for trees, rocks and plants - around home when the job is started near home, otherwise around where it was started. It keeps working there when you leave.", new AcceptableValueRange<float>(10f, 150f));
		HarvestFollowRange = Bind(cfg, "Gathering", "FollowRange", 10f, "Wood, Stone and Ore with Follow on: a tree or rock has to be within this many meters of you. With nothing that close the companion just follows you. (With Follow off see Range; Forage has ForageFollowRange.)", new AcceptableValueRange<float>(5f, 60f));
		HarvestFollowLogRange = Bind(cfg, "Gathering", "FollowLogRange", 20f, "Wood with Follow on: fallen logs and stumps count up to this many meters from you (further than FollowRange), so it finishes the trees it fells before starting a new one - a log can roll well away from where you stand.", new AcceptableValueRange<float>(5f, 60f));
		GatherRestStamina = Bind(cfg, "Gathering", "RestStamina", 0.15f, "Chopping and mining: swing after swing (the weapon's combo) until stamina would drop below this share of the maximum, then stop and get its breath back.", new AcceptableValueRange<float>(0f, 0.9f));
		GatherResumeStamina = Bind(cfg, "Gathering", "ResumeStamina", 0.6f, "Chopping and mining: after stopping for breath, start swinging again once stamina is back to this share of the maximum.", new AcceptableValueRange<float>(0.1f, 1f));
		GatherPowerChopLogBelow = Bind(cfg, "Gathering", "PowerChopLogBelow", 0.3f, "Wood: the power chop (the axe's downward swing) is used on stumps, and on a log only when its top is less than this many meters above the companion's feet - too low for the sideways swing. Everything else gets normal swings.", new AcceptableValueRange<float>(-1f, 2f));
		ForageFollowRange = Bind(cfg, "Gathering", "ForageFollowRange", 10f, "Forage with Follow on: a plant has to be within this many meters of you - the berries you happen to walk past, not everything around. With nothing that close the companion just follows you. (With Follow off see Range.)", new AcceptableValueRange<float>(3f, 60f));
		HarvestPickUpLoose = Bind(cfg, "Gathering", "PickUpLoose", defaultValue: true, "Wood and Stone jobs, early in the game only: also pick up what's lying on the ground by hand - branches (Wood job), loose stones and flint (Stone job) - as long as they're on GatherWood / GatherStone and under their amount. Early game = there's no bronze-or-better axe in the companion's pack, your pack or the chests near home, and no forge at home to make one. With no tool for the job (no axe for Wood, no pickaxe for Stone) picking things up is all it does.");
		WoodLimit = Bind(cfg, "Gathering", "WoodLimit", 500, "How many of each wood material (counting the companion's pack and chests near its home) the Wood job gathers before stopping. 0 = no limit. Per-item numbers in GatherWood override this.", new AcceptableValueRange<int>(0, 100000));
		StoneLimit = Bind(cfg, "Gathering", "StoneLimit", 500, "Same as WoodLimit, for the Stone job and GatherStone.", new AcceptableValueRange<int>(0, 100000));
		OreLimit = Bind(cfg, "Gathering", "OreLimit", 500, "Same as WoodLimit, for the Ore job and GatherOre.", new AcceptableValueRange<int>(0, 100000));
		GatherWood = Bind(cfg, "Gathering", "GatherWood", "Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin", "What the Wood job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses WoodLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		GatherStone = Bind(cfg, "Gathering", "GatherStone", "Stone, Flint, BlackMarble, Grausten", "What the Stone job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses StoneLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		GatherOre = Bind(cfg, "Gathering", "GatherOre", "CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, FlametalOreNew, Obsidian", "What the Ore job collects. Targets are only worked if they drop something on this list that is still under its amount; drops not on the list are left on the ground. A name with no number uses OreLimit. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		ForageItems = Bind(cfg, "Gathering", "ForageItems", "Raspberry,Blueberries,Cloudberry,Mushroom,MushroomYellow,MushroomBlue,MushroomMagecap,MushroomJotunPuffs,MushroomSmokePuff,Thistle,Dandelion,Carrot,Turnip,Onion,Barley,Flax,RoyalJelly,Honey", "What the Forage job picks up. Name:Number stops picking that item once there are that many (pack + chests near home). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are prefab or in-game names.");
		WoodSkipTrees = Bind(cfg, "Gathering", "WoodSkipTrees", "Oak", "Standing trees the Wood job never fells, matched on part of the tree name (Oak, Birch, Beech, Pine, Fir, SwampTree, YggaShoot...). Logs and stumps are still cleaned up.");
		BaseProtectRadius = Bind(cfg, "Gathering", "BaseProtectRadius", 10f, "The Wood job won't fell a standing tree with anything players built within this many meters of its trunk. 0 = off.", new AcceptableValueRange<float>(0f, 50f));
		JunkCleanup = Bind(cfg, "Gathering", "JunkCleanup", defaultValue: true, "When a gathering companion's pack is full and it has no home chest to unload into, it drops junk (materials and trophies that aren't on any gather list) to make room. Gear, tools, ammo, food, potions, seeds and anything on NeverDiscard are always kept.");
		NeverDiscard = Bind(cfg, "Gathering", "NeverDiscard", "Trophy", "Items the junk clean-up never drops, matched on part of the item name.");
		HarvestOverweightThreshold = Bind(cfg, "Gathering", "OverweightThreshold", 298f, "A gathering companion stops picking things up at this carry weight.", new AcceptableValueRange<float>(50f, 500f));
		HuntPrey = Bind(cfg, "Hunting", "Prey", "*", "What the Hunt job goes after: creatures that drop meat (and birds, see Birds). Name:Number with a creature or a meat name gives that meat its own target instead of MeatTarget / FeatherTarget. Tamed animals, bosses and companions are never hunted. The names to use are listed in OfflineCompanionsReborn.Hunting.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		HuntMeatTarget = Bind(cfg, "Hunting", "MeatTarget", 100, "Hunt a creature only while one of the meats it drops is under this many (counting the companion's pack and the chests near its home). Leather, hides and other drops are picked up as a bonus. 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		HuntRange = Bind(cfg, "Hunting", "Range", 40f, "With Follow off: how far (meters) from where it is hunting (home if it's near home, otherwise where the job was started) a companion looks for prey.", new AcceptableValueRange<float>(10f, 200f));
		HuntFollowRange = Bind(cfg, "Hunting", "FollowRange", 40f, "With Follow on: prey has to be within this many meters of you. With nothing that close the companion just follows you.", new AcceptableValueRange<float>(5f, 100f));
		HuntChaseExtra = Bind(cfg, "Hunting", "ChaseExtra", 15f, "How much further than Range / FollowRange a companion chases prey that runs off before letting it go.", new AcceptableValueRange<float>(0f, 100f));
		HuntBagFull = Bind(cfg, "Hunting", "BagFull", 0.9f, "With Follow off and home set: when the pack is this full (by weight, or out of free slots) the hunter walks home and puts its catch away, then goes back out.", new AcceptableValueRange<float>(0.3f, 1f));
		HuntBirds = Bind(cfg, "Hunting", "Birds", defaultValue: true, "Also shoot birds (crows, gulls, modded birds) for their feathers: when one lands within range, the hunter sneaks up and shoots it with a bow or crossbow (only if it has one with ammo), then picks up the drops. Birds on the Prey list's exclusions (-Crow, -Seagal) are left alone.");
		HuntFeatherTarget = Bind(cfg, "Hunting", "FeatherTarget", 100, "Shoot birds only while what they drop (feathers) is under this many, counting the pack and the chests near home. 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		HuntSneak = Bind(cfg, "Hunting", "Sneak", defaultValue: true, "Sneak up on skittish game (deer, hares...) that hasn't noticed anyone yet, like a player: crouched and not running, so it doesn't bolt. Crouching companions are quieter and harder to spot, like a sneaking player (light and Sneak skill count).");
		HuntThrowSpear = Bind(cfg, "Hunting", "ThrowSpear", defaultValue: true, "A hunter holding a spear throws it at skittish game (deer, hares...) from a distance, like a player, then walks over and picks it back up. Only when it has another weapon in its pack to fight on with.");
		HuntThrowRange = Bind(cfg, "Hunting", "ThrowRange", 12f, "How far (meters) a hunter throws its spear from. Further throws drop short more often.", new AcceptableValueRange<float>(4f, 25f));
		HuntThrowStandoff = Bind(cfg, "Hunting", "ThrowStandoff", 10f, "With a spear to throw at skittish game, a hunter sneaks only this close (meters), stops and throws, instead of walking right up to stab it. Never more than ThrowRange less 1 m.", new AcceptableValueRange<float>(3f, 25f));
		HuntSneakFrom = Bind(cfg, "Hunting", "SneakFrom", 32f, "Further than this (meters) from prey that hasn't noticed it, a hunter walks (no running) instead of sneaking, like a player - sneaking all the way costs a lot of stamina. Closer, it crouches and sneaks.", new AcceptableValueRange<float>(5f, 60f));
		HuntSwitchCloserBy = Bind(cfg, "Hunting", "SwitchCloserBy", 5f, "Before its first strike, a hunter switches to another animal that is at least this many meters closer than its prey (and see SwitchShare). 0 = never switches.", new AcceptableValueRange<float>(0f, 50f));
		HuntSwitchShare = Bind(cfg, "Hunting", "SwitchShare", 0.6f, "...and that is at most this share of the distance to its prey (0.6 = 60%).", new AcceptableValueRange<float>(0.1f, 1f));
		HuntSneakRestStamina = Bind(cfg, "Hunting", "SneakRestStamina", 0.3f, "Sneaking costs stamina, like for a player (standing still doesn't). When a hunter sneaking up on prey drops below this share of its stamina, it stops where it is, stays crouched and gets its breath back. 0 = never stops.", new AcceptableValueRange<float>(0f, 0.9f));
		HuntSneakResumeStamina = Bind(cfg, "Hunting", "SneakResumeStamina", 0.6f, "A hunter resting while sneaking carries on once its stamina is back above this share.", new AcceptableValueRange<float>(0.1f, 1f));
		HuntChaseRunStamina = Bind(cfg, "Hunting", "ChaseRunStamina", 0.3f, "Chasing prey that has bolted, a hunter only runs while its stamina is above this share; below it, it jogs and lets its stamina come back, like a player. 0 = always runs.", new AcceptableValueRange<float>(0f, 1f));
		FishScanRadius = Bind(cfg, "Fishing", "ScanRadius", 30f, "How far (meters) from where the Fish job was started the companion looks for fish to catch. It stays around there and fishes the water nearby.", new AcceptableValueRange<float>(5f, 100f));
		FishList = Bind(cfg, "Fishing", "Fish", "*", "Which fish to catch. Name:Number gives that fish its own target instead of FishTarget. The fish names are listed in OfflineCompanionsReborn.Fishing.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit).");
		FishTarget = Bind(cfg, "Fishing", "FishTarget", 100, "Catch a kind of fish only while there are fewer than this many (pack + chests near home). 0 = no limit.", new AcceptableValueRange<int>(0, 10000));
		FishCastWait = Bind(cfg, "Fishing", "CastWait", 60f, "Seconds to wait for a bite before reeling in and trying again (a fish that swam off, the wrong spot).", new AcceptableValueRange<float>(10f, 600f));
		FishBagFull = Bind(cfg, "Fishing", "BagFull", 0.9f, "Near home: when the pack is this full (by weight, or out of free slots) the companion walks home and puts the catch away, then goes back to fishing.", new AcceptableValueRange<float>(0.3f, 1f));
		FarmScanRadius = Bind(cfg, "Farming", "Range", 30f, "How far (meters) around itself a farming companion looks for crops to pick, farm zones to plant and chests.", new AcceptableValueRange<float>(5f, 100f));
		FarmPlantTilled = Bind(cfg, "Farming", "PlantTilledGround", defaultValue: true, "Ground you've tilled with a cultivator (outside your farm zones) counts as an Any zone, but only for a companion with no farm zones (its own or another companion's): it then plants whatever seeds it has on the tilled ground only. Off = it only plants in the farm zones you set.");
		FarmUnreachableForget = Bind(cfg, "Farming", "UnreachableSpotMemory", 600f, "How long (seconds) a farmer leaves a planting spot alone after finding it can't walk there (e.g. tilled ground on the other side of a wall). It tries it again after this.", new AcceptableValueRange<float>(30f, 3600f));
		FarmPlantSpacing = Bind(cfg, "Farming", "PlantSpacing", 0.75f, "The least space (meters) between plants. A plant that needs more room to grow gets that instead.", new AcceptableValueRange<float>(0.25f, 3f));
		SeedReserve = Bind(cfg, "Farming", "SeedMinimum", 20, "The fewest of each plantable item (carrot, turnip, onion seeds, barley, flax and so on) the farmer keeps, counting its pack and chests near its home. It never plants seeds if that would take them below this. Barley, flax and other crops that grow from themselves are still planted, and the farmer keeps this many in its pack instead of storing them.", new AcceptableValueRange<int>(0, 1000));
		SeedTarget = Bind(cfg, "Farming", "SeedTarget", 100, "While a seed is under this number, the farmer grows seed plants (planting carrots, turnips, onions...) alongside its regular crops to build the stock back up. Below SeedMinimum it grows only seed plants for that crop. At or above this number it grows only crops.", new AcceptableValueRange<int>(0, 5000));
		SeedCarry = Bind(cfg, "Farming", "SeedCarry", 40, "How many of each plantable item the farmer carries. When it opens a chest for seeds it takes up to this many of every type on the PlantAny list (and vegetables for seed plants while that seed is under SeedTarget), so it can fill the fields without walking back for each type. It keeps up to this many in its pack instead of storing them.", new AcceptableValueRange<int>(1, 200));
		PlantGroupSize = Bind(cfg, "Farming", "PlantGroupSize", 4, "How many of one plant the farmer puts in before switching to the next type, so a mixed field ends up in small blocks instead of one crop. 1 = fully mixed.", new AcceptableValueRange<int>(1, 100));
		FarmNextCropRange = Bind(cfg, "Farming", "NextCropRange", 3f, "After picking a crop, the farmer goes straight to the next ripe crop within this many meters, like working down a row, instead of pausing and scanning the whole field. 0 = always pause and scan.", new AcceptableValueRange<float>(0f, 10f));
		PlantAny = Bind(cfg, "Farming", "PlantAny", "CarrotSeeds, TurnipSeeds, OnionSeeds, Barley, Flax, MushroomJotunPuffs, MushroomMagecap, VineberrySeeds", "What the farmer plants in farm zones set to Any crop. * = every crop that grows on cultivated ground (tree saplings are never planted in an Any zone unless named). Name:Number stops planting that crop once there are that many of what it gives (carrots for CarrotSeeds, barley for Barley...), counting pack and chests near home. Items added by other mods work too. List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		SmeltScanRadius = Bind(cfg, "Smelting", "Range", 25f, "How far (meters) around itself a smelting companion looks for smelters, kilns, furnaces and chests. Near home it also uses the chests around home ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 100f));
		SmeltMaxCarryOre = Bind(cfg, "Smelting", "MaxCarryOre", 20, "The most ore (or other input) a companion carries to a smelter in one trip.", new AcceptableValueRange<int>(1, 100));
		SmeltMaxCarryFuel = Bind(cfg, "Smelting", "MaxCarryFuel", 40, "The most fuel (coal, wood) a companion carries to a smelter in one trip.", new AcceptableValueRange<int>(1, 100));
		SmeltUsePiles = Bind(cfg, "Smelting", "UsePiles", defaultValue: true, "When the chests have none of what a smelter or kiln needs (wood for the kiln, fuel...), the Smelt job takes down a pile of it (Stack of Wood...) with a hammer, like a player, and uses that. Needs a hammer in its pack.");
		CookRange = Bind(cfg, "Cooking", "Range", 25f, "How far around itself (meters) a cooking companion looks for cooking stations, fermenters and chests. Near home it also uses the chests around home ([General] HomeTripRange).", new AcceptableValueRange<float>(5f, 60f));
		CookIngredientReserve = Bind(cfg, "Cooking", "IngredientReserve", 20, "A raw ingredient is only used while the chests in range (plus the companion's bag) hold more than this many, so there is always some left for you to cook something yourself.", new AcceptableValueRange<int>(0, 1000));
		CookFoodTarget = Bind(cfg, "Cooking", "FoodTarget", 20, "Cook a food until the chests in range hold this many of it.", new AcceptableValueRange<int>(1, 1000));
		CookBatchSize = Bind(cfg, "Cooking", "BatchSize", 5, "How many of a food (or mead base) to make in one go, like a player cooking a batch. Still stops at FoodTarget / MeadBaseTarget and never goes into the ingredient reserve. On a cooking station or oven it's also limited by the free slots.", new AcceptableValueRange<int>(1, 50));
		CookFoods = Bind(cfg, "Cooking", "Foods", "*", "What may be cooked. Name:Number is that food's own target instead of FoodTarget. The names to use are listed in OfflineCompanionsReborn.Cooking.txt in the config folder (written at startup). List format (same for every list): * = everything, Name = this one, -Name = never this one, Name:Number = this one with its own amount (0 = no limit). Names are item/creature names (prefab or in-game name).");
		CookStoreWithCookedFood = Bind(cfg, "Cooking", "StoreWithCookedFood", defaultValue: true, "Put cooked food in a chest that already holds that food; if there is none, one that holds other cooked food; otherwise the chest nearest the cooking area. Off = the nearest chest with room.");
		CookMakeMead = Bind(cfg, "Cooking", "MakeMead", defaultValue: true, "Brew mead: make mead bases and fill fermenters with them. Off = food only (mead that has already finished in a fermenter is still tapped).");
		CookMeadBaseTarget = Bind(cfg, "Cooking", "MeadBaseTarget", 10, "Brew a mead base (one in MeadFocus) until the chests in range hold this many of it.", new AcceptableValueRange<int>(0, 1000));
		CookMeadTarget = Bind(cfg, "Cooking", "MeadTarget", 20, "Keep fermenters going with a mead base until the chests in range hold this many of the finished mead.", new AcceptableValueRange<int>(0, 1000));
		CookMeadFocus = Bind(cfg, "Cooking", "MeadFocus", "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor; bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot; queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor; fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor; kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor", "Mead bases to brew FIRST, by the last boss beaten, to get ready for the next one (other mead bases you know are still brewed after food, within MeadBaseTarget / MeadTarget). Format: boss: Base, Base; boss: ... in the order you beat them. A boss is its name or part of it (eikthyr, gdking = The Elder, bonemass, dragon = Moder, goblinking = Yagluth, queen, fader, kall = Kall Fimbulbringer, or a modded boss); start = none beaten yet. The bosses your game has, and the mead base names, are listed in OfflineCompanionsReborn.Cooking.txt in the config folder.");
		CookFuelLevel = Bind(cfg, "Cooking", "FuelLevel", 0.5f, "Cook job: keeps the fires under the cooking spots it uses (spit, iron cooking station, cauldron) and the oven at this share of full (0.5 = 50%). When one drops below half of that it tops it back up. Only enough to cook with - Repair and Restock fills fires further if it's on.", new AcceptableValueRange<float>(0.1f, 1f));
		CookUsePiles = Bind(cfg, "Cooking", "UsePiles", defaultValue: true, "Cook job: with no fuel in the chests, take down a pile of it (Stack of Wood...) with a hammer, like a player. Needs a hammer in its pack.");
		CookCauldron = Bind(cfg, "Cooking", "UseCauldron", defaultValue: true, "Cook job: use the cauldron (soups, stews and other crafted food).");
		CookPrepTable = Bind(cfg, "Cooking", "UsePrepTable", defaultValue: true, "Cook job: use the food preparation table (dough, raw pies and the like).");
		CookMeadKetill = Bind(cfg, "Cooking", "UseMeadKetill", defaultValue: true, "Cook job: use the mead ketill (mead bases).");
		CookCookingStation = Bind(cfg, "Cooking", "UseCookingStation", defaultValue: true, "Cook job: use the cooking station / spit over a fire.");
		CookIronCookingStation = Bind(cfg, "Cooking", "UseIronCookingStation", defaultValue: true, "Cook job: use the iron cooking station.");
		CookOven = Bind(cfg, "Cooking", "UseOven", defaultValue: true, "Cook job: use the stone oven (bread, pies; it also gets wood).");
		CookFermenter = Bind(cfg, "Cooking", "UseFermenter", defaultValue: true, "Cook job: use fermenters (filling them with mead bases and tapping the mead).");
		CookBeehives = Bind(cfg, "Cooking", "UseBeehives", defaultValue: true, "Cook job: use beehives (collecting honey).");
		CookOtherStations = Bind(cfg, "Cooking", "UseOther", defaultValue: true, "Cook job: use any other cooking station, e.g. one added by another mod.");
		UpkeepRepair = Bind(cfg, "RepairAndRestock", "Repair", defaultValue: true, "Repair and Restock: repair damaged building pieces near home with a hammer, like a player (one swing per piece; the piece's crafting station has to be in range and a ward has to allow it; costs stamina and hammer wear). A hammer is fetched from a chest if it has none.");
		UpkeepRepairBelow = Bind(cfg, "RepairAndRestock", "RepairBelow", 0.75f, "Repair pieces below this much health (0.75 = 75% or less; 1 = any damage at all). Each repair is one swing that fixes the piece fully, like a player's.", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepReach = Bind(cfg, "RepairAndRestock", "Reach", 4.5f, "How far (meters, from the eyes) the companion can reach a piece to repair it. A player reaches about 5 m.", new AcceptableValueRange<float>(2f, 6f));
		UpkeepRestock = Bind(cfg, "RepairAndRestock", "Restock", defaultValue: true, "Repair and Restock: keep fires, torches and braziers near home fuelled, one piece of fuel at a time like a player. Fuel comes from the pack or is fetched from a chest; what's left over goes back in a chest. The Refuel... settings below pick which kinds.");
		UpkeepFuelBelow = Bind(cfg, "RepairAndRestock", "FuelBelow", 0.8f, "Top up a fire when its fuel is below this share of full (0.8 = 80%). It's filled all the way up.", new AcceptableValueRange<float>(0.1f, 1f));
		RefuelFires = Bind(cfg, "RepairAndRestock", "RefuelFires", defaultValue: true, "Restock: campfires, fire pits and bonfires.");
		RefuelHearths = Bind(cfg, "RepairAndRestock", "RefuelHearths", defaultValue: true, "Restock: hearths.");
		RefuelBraziers = Bind(cfg, "RepairAndRestock", "RefuelBraziers", defaultValue: true, "Restock: braziers (standing and hanging).");
		RefuelStandingTorches = Bind(cfg, "RepairAndRestock", "RefuelStandingTorches", defaultValue: true, "Restock: standing torches (wood, iron, green, blue, mist).");
		RefuelWallTorches = Bind(cfg, "RepairAndRestock", "RefuelWallTorches", defaultValue: true, "Restock: sconces and wall torches.");
		RefuelHotTubs = Bind(cfg, "RepairAndRestock", "RefuelHotTubs", defaultValue: true, "Restock: hot tubs.");
		RefuelOther = Bind(cfg, "RepairAndRestock", "RefuelOther", defaultValue: true, "Restock: anything else that burns fuel and isn't one of the kinds above (lanterns, the jack-o-turnip, things added by other mods).");
		UpkeepHoney = Bind(cfg, "RepairAndRestock", "Honey", defaultValue: true, "Repair and Restock: empty the beehives in its area and put the honey away in the chest that already holds the most honey.");
		UpkeepHoneyAt = Bind(cfg, "RepairAndRestock", "HoneyAt", 0.5f, "Empty a beehive once it's at least this full (0.5 = half full, 1 = only when full).", new AcceptableValueRange<float>(0.1f, 1f));
		UpkeepSort = Bind(cfg, "RepairAndRestock", "Sort", defaultValue: true, "Repair and Restock: sort the chests in its area (around home, or around where the job was started), like with like: each kind of item is carried to the chest that already holds the most of it, and part-stacks are merged.");
		UpkeepSortItems = Bind(cfg, "RepairAndRestock", "SortItems", "*", "Which items get sorted. Comma-separated: * = everything, Name = this one, -Name = never this one (e.g. *, -Coins, -Wood keeps coins and wood where they are). Prefab or in-game names.");
		UpkeepSortGear = Bind(cfg, "RepairAndRestock", "SortGear", defaultValue: false, "Also sort things that don't stack (weapons, armor, tools). Off: only stackable items are moved.");
		UpkeepPiles = Bind(cfg, "RepairAndRestock", "Piles", defaultValue: true, "Repair and Restock: stack wood, stone and the like from the chests into piles (the game's Stack of Wood, Stone Pile, Fine Wood Stack...), built with a hammer like a player. Only next to piles of the same kind you already built - it never starts a new pile somewhere on its own.");
		UpkeepPileKeep = Bind(cfg, "RepairAndRestock", "PileKeep", 100, "How many of each material stay in the chests for building: what's above this goes into piles, and when the chests drop below it a pile is taken down (with the hammer, like a player) and the material put back in the chests.", new AcceptableValueRange<int>(0, 10000));
		UpkeepPileItems = Bind(cfg, "RepairAndRestock", "PileItems", "*", "Which materials go into piles. Comma-separated: * = every material you have piles of, Name = this one, -Name = never this one, Name:Number = keep this many of it in the chests instead of PileKeep. Prefab or in-game names (Wood, Stone, FineWood, RoundLog...).");
		UpkeepAwayRadius = Bind(cfg, "RepairAndRestock", "AwayRadius", 30f, "Started away from home (or with no home set): how far (meters) from where the job was started it repairs, restocks and sorts. Started near home, the range is [General] HomeTripRange around home.", new AcceptableValueRange<float>(5f, 100f));
		UpkeepWorkTime = Bind(cfg, "RepairAndRestock", "WorkTime", 1f, "Seconds per action: a repair swing, opening a chest. Adding fuel takes a bit over half of this per piece. (The game won't repair the same piece more than once a second.)", new AcceptableValueRange<float>(0.3f, 5f));
		UpkeepGiveUp = Bind(cfg, "RepairAndRestock", "GiveUp", 90f, "Seconds it keeps trying to get within reach of a piece, fire or chest before leaving it for a few minutes.", new AcceptableValueRange<float>(20f, 600f));
		UpkeepOrderWait = Bind(cfg, "RepairAndRestock", "OrderWait", 30f, "After you give the companion an order (send it somewhere, sit by a fire...), it waits this many seconds once the order is done before going back to its chores.", new AcceptableValueRange<float>(0f, 600f));
		IdleEnabled = Bind(cfg, "Idle", "Enabled", defaultValue: true, "When its job has had nothing to do for a while (and it isn't following you), a companion idles near its job instead of standing still: small chores, sitting down, wandering about, chatting. It stops the moment the job has work again.");
		IdleStartAfter = Bind(cfg, "Idle", "StartAfter", 10f, "Seconds with nothing to do before it starts idling.", new AcceptableValueRange<float>(1f, 300f));
		IdleRadius = Bind(cfg, "Idle", "Radius", 8f, "How far (meters) from where its job left it a companion wanders, sits or looks for company while idle.", new AcceptableValueRange<float>(2f, 40f));
		IdleChores = Bind(cfg, "Idle", "Chores", defaultValue: true, "While idle, do small chores nearby: repair damaged pieces, top up fires, collect honey, put away what it carries (uses the Repair and Restock settings).");
		IdleChoreRadius = Bind(cfg, "Idle", "ChoreRadius", 15f, "How far (meters) it looks for idle chores.", new AcceptableValueRange<float>(2f, 60f));
		IdleSit = Bind(cfg, "Idle", "Sit", defaultValue: true, "While idle, sit down for a while - on a free chair or bench nearby, else on the ground.");
		IdleWander = Bind(cfg, "Idle", "Wander", defaultValue: true, "While idle, stroll about nearby and look over stations and chests.");
		IdleSocialize = Bind(cfg, "Idle", "Socialize", defaultValue: true, "While idle, go over to you or another companion nearby and chat now and then.");
		IdleSpeechInterval = Bind(cfg, "Idle", "SpeechInterval", 300f, "At most every this many seconds it says why it's idle (and chats while socializing about half as often). 0 = never.", new AcceptableValueRange<float>(0f, 3600f));
		SpeechEnabled = Bind(cfg, "Speech", "Enabled", defaultValue: true, "Companions say things at all (replies to your orders, what they're doing, problems, chatter). Off = silent.");
		SpeechChatter = Bind(cfg, "Speech", "Chatter", "Normal", "How often a companion makes small talk that isn't about anything in particular: Off, Rare (every 4-8 minutes), Normal (every 1.5-3 minutes) or Often (every 30-60 seconds). Replies to your orders and problems it runs into are always said.", new AcceptableValueList<string>("Off", "Rare", "Normal", "Often"));
		SpeechFarMessages = Bind(cfg, "Speech", "FarAwayMessages", defaultValue: true, "When a companion runs into a problem (needs a tool, can't get somewhere...) while you're out of earshot (further than CullDistance), it shows as a message in the top left corner with its name.");
		SpeechAnnounceDeath = Bind(cfg, "Speech", "AnnounceDeath", defaultValue: true, "When one of your companions dies, say so in the middle of the screen with where it happened (biome, how far from you), wherever you are.");
		SpeechDuration = Bind(cfg, "Speech", "Duration", 5f, "Seconds a line of speech stays on screen.", new AcceptableValueRange<float>(1f, 15f));
		SpeechSayCooldown = Bind(cfg, "Speech", "SayCooldown", 3f, "Least time (seconds) between two lines from any companion, so they don't all talk at once.", new AcceptableValueRange<float>(1f, 30f));
		SpeechCullDistance = Bind(cfg, "Speech", "CullDistance", 20f, "Speech from companions further away than this (meters) isn't shown.", new AcceptableValueRange<float>(5f, 60f));
		RosterSaveInterval = Bind(cfg, "Advanced", "RosterSaveInterval", 60f, "Seconds between saves of your companions to the roster (their gear and settings), used to recover a lost companion.", new AcceptableValueRange<float>(10f, 600f));
		HarvestScanRadius = Bind(cfg, "Advanced", "GatheringSearchRadius", 30f, "Gathering: how far (meters) around itself a companion looks for trees, rocks and plants when it's following you or sent to one with the command key (with Follow off it's [Gathering] Range).", new AcceptableValueRange<float>(5f, 100f));
		HarvestScanInterval = Bind(cfg, "Advanced", "GatheringScanInterval", 4f, "Gathering: seconds between looks for the next tree, rock or plant.", new AcceptableValueRange<float>(1f, 30f));
		HarvestDropScanRadius = Bind(cfg, "Advanced", "GatheringDropRadius", 8f, "Gathering: how far (meters) around a felled tree or broken rock the drops are picked up.", new AcceptableValueRange<float>(2f, 20f));
		HarvestBlacklistDuration = Bind(cfg, "Advanced", "GatheringSkipTime", 60f, "Gathering: seconds a tree, rock or plant it couldn't get to is skipped.", new AcceptableValueRange<float>(5f, 300f));
		FarmScanInterval = Bind(cfg, "Advanced", "FarmScanInterval", 4f, "Farming: seconds between looks for crops to pick and ground to plant.", new AcceptableValueRange<float>(1f, 30f));
		FarmUseDistance = Bind(cfg, "Advanced", "FarmUseDistance", 2f, "Farming: how close (meters) a companion has to be to a crop or chest to use it.", new AcceptableValueRange<float>(1f, 5f));
		SmeltScanInterval = Bind(cfg, "Advanced", "SmeltScanInterval", 3f, "Smelting: seconds between looks at the smelters and chests.", new AcceptableValueRange<float>(1f, 30f));
		SmeltUseDistance = Bind(cfg, "Advanced", "SmeltUseDistance", 2.5f, "Smelting: how close (meters) a companion has to be to a smelter to put things in or take them out.", new AcceptableValueRange<float>(1f, 5f));
		RepairScanInterval = Bind(cfg, "Advanced", "GearRepairScanInterval", 5f, "Gear repair: seconds between checks of a companion's gear for wear.", new AcceptableValueRange<float>(1f, 30f));
		RepairTickInterval = Bind(cfg, "Advanced", "GearRepairTickInterval", 0.8f, "Gear repair: seconds per item repaired at the station.", new AcceptableValueRange<float>(0.1f, 5f));
		RepairUseDistance = Bind(cfg, "Advanced", "GearRepairUseDistance", 2.5f, "Gear repair: how close (meters) a companion has to be to the station to repair.", new AcceptableValueRange<float>(1f, 5f));
		UpkeepScanInterval = Bind(cfg, "Advanced", "RestockScanInterval", 5f, "Repair and Restock: seconds between looks for something to repair, refuel or sort.", new AcceptableValueRange<float>(1f, 60f));
		AttackCooldown = Bind(cfg, "Advanced", "AttackCooldown", 0.225f, "Combat: least time (seconds) between a companion's attacks, on top of each weapon's own attack timing.", new AcceptableValueRange<float>(0.1f, 2f));
		MeleeTooClose = Bind(cfg, "Advanced", "MeleeTooClose", 0.3f, "Combat: closer than this (meters, from the edge of the target) a companion steps back before swinging a melee weapon - a stab or swing that starts inside the target misses, like a player standing on top of a boar.", new AcceptableValueRange<float>(0f, 1.5f));
		MeleeReachMargin = Bind(cfg, "Advanced", "MeleeReachMargin", 0.2f, "Combat: a companion swings a melee weapon only once it is at least this much (meters) inside the weapon's reach, not right at the edge of it, where hits are hit-or-miss.", new AcceptableValueRange<float>(0f, 1f));
		BlockReactionTime = Bind(cfg, "Advanced", "BlockReactionTime", 0.2f, "Combat: seconds between an enemy starting a swing at a companion and the companion raising its guard.", new AcceptableValueRange<float>(0f, 1f));
		LogPlayerCombat = Bind(cfg, "Advanced", "LogPlayerCombat", defaultValue: true, "Testing, with LogLevel on Detailed: write your own fighting to the log (lines starting [You]) - swings, guard, blocks and parries, enemy swings at you and how far you were - to help tune how companions fight. Will be removed once combat is tuned.");
		FoodRegenInterval = Bind(cfg, "Advanced", "FoodRegenInterval", 10f, "Food: seconds between health regeneration ticks from food (a player's is 10).", new AcceptableValueRange<float>(1f, 60f));
		ConsumeCheckInterval = Bind(cfg, "Advanced", "FoodCheckInterval", 1f, "Food: seconds between checks whether a companion should eat or drink.", new AcceptableValueRange<float>(0.5f, 10f));
		TombstoneScanInterval = Bind(cfg, "Advanced", "TombstoneScanInterval", 5f, "Death: seconds between looks for its own leftover tombstone nearby.", new AcceptableValueRange<float>(1f, 30f));
		LogLevel = Bind(cfg, "Advanced", "LogLevel", "Normal", "How much the mod writes to the BepInEx log. Off: warnings and errors only. Normal: the main events (orders, deaths, portals, jobs starting and stopping, problems). Detailed: everything the companions think and do, plus the diagnostics - turn this on when sending a log with a bug report. Lower levels cost nothing: the lines aren't even put together.", new AcceptableValueList<string>("Off", "Normal", "Detailed"));
		TombstoneNavTimeout = Bind(cfg, "Advanced", "TombstoneNavTimeout", 120f, "Death: seconds a companion keeps trying to get to its tombstone before giving up.", new AcceptableValueRange<float>(10f, 600f));
		bool flag = MigrateAndClean(cfg);
		if (flag && Mathf.Approximately(ViewRange.Value, 40f) && Mathf.Approximately(ViewAngle.Value, 90f))
		{
			ViewRange.Value = 20f;
			ViewAngle.Value = 120f;
		}
		if (flag && ForageItems.Value.Trim() == "*")
		{
			ForageItems.Value = (string)ForageItems.DefaultValue;
		}
		if (CookMeadFocus.Value == "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; bonemass: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseFrostResist; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseFrostResist, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, BarleyWineBase; queen: MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseEitrMinor, BarleyWineBase; fader: MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor, BarleyWineBase")
		{
			CookMeadFocus.Value = "start: MeadBaseHealthMinor, MeadBaseStaminaMinor; eikthyr: MeadBaseHealthMinor, MeadBaseStaminaMinor, MeadBasePoisonResist; gdking: MeadBasePoisonResist, MeadBaseHealthMedium, MeadBaseStaminaMinor; bonemass: MeadBaseFrostResist, MeadBaseHealthMedium, MeadBaseStaminaMedium; dragon: MeadBaseHealthMedium, MeadBaseStaminaMedium, MeadBaseTasty, BarleyWineBase; goblinking: MeadBaseHealthMajor, MeadBaseStaminaMedium, MeadBaseEitrMinor, MeadBaseLightfoot; queen: BarleyWineBase, MeadBaseHealthMajor, MeadBaseStaminaLingering, MeadBaseHealthLingering, MeadBaseEitrMinor; fader: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor; kall: MeadBaseFrostResist, MeadBaseHealthMajor, MeadBaseHealthLingering, MeadBaseStaminaLingering, MeadBaseEitrMinor";
		}
	}

	private static bool MigrateAndClean(ConfigFile cfg)
	{
		try
		{
			if (!(AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(cfg, null) is Dictionary<ConfigDefinition, string> { Count: not 0 } dictionary))
			{
				return false;
			}
			int num = 0;
			string[][] array = s_moved;
			foreach (string[] array2 in array)
			{
				ConfigDefinition key = new ConfigDefinition(array2[0], array2[1]);
				if (dictionary.TryGetValue(key, out var value))
				{
					ConfigDefinition key2 = new ConfigDefinition(array2[2], array2[3]);
					if (cfg.ContainsKey(key2))
					{
						cfg[key2].SetSerializedValue(value);
						num++;
					}
				}
			}
			int count = dictionary.Count;
			dictionary.Clear();
			cfg.Save();
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log?.LogInfo($"[Config] Cleaned up the config file: {num} setting(s) moved to their new names, {count} old line(s) removed");
			}
			return true;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log?.LogWarning("[Config] Couldn't clean up old settings: " + ex.Message);
			return false;
		}
	}

	private static ConfigEntry<T> Bind<T>(ConfigFile cfg, string category, string key, T defaultValue, string desc, AcceptableValueBase range = null)
	{
		ConfigEntry<T> configEntry = cfg.Bind(category, key, defaultValue, new ConfigDescription(desc, range));
		if (!Categories.TryGetValue(category, out var value))
		{
			value = new List<ConfigEntryBase>();
			Categories[category] = value;
			CategoryOrder.Add(category);
		}
		value.Add(configEntry);
		return configEntry;
	}
}
