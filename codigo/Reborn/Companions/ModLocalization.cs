using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace Companions;

[HarmonyPatch]
public static class ModLocalization
{
	private static readonly FieldInfo TranslationsField = AccessTools.Field(typeof(Localization), "m_translations");

	private static Dictionary<string, string> _englishDefaults;

	private static string _translationsDir;

	private static bool _initialized;

	private static string _lastInjectedLanguage;

	private static readonly Regex EntryRegex = new Regex("\"key\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"[^\"]*\"value\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

	private static readonly FieldInfo InstanceField = AccessTools.Field(typeof(Localization), "m_instance");

	public static void Refresh()
	{
		_lastInjectedLanguage = null;
		if (_initialized && InstanceField?.GetValue(null) is Localization loc)
		{
			InjectTranslations(loc);
		}
		SpeechConfig.Reload();
	}

	public static string Loc(string key)
	{
		if (Localization.instance == null)
		{
			return key;
		}
		return Localization.instance.Localize("$" + key);
	}

	public static string LocFmt(string key, params object[] args)
	{
		string text = Loc(key);
		try
		{
			return string.Format(text, args);
		}
		catch
		{
			return text;
		}
	}

	public static void Init()
	{
		string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string subDir = Path.Combine(baseDir, "OfflineCompanionsReborn", "Translations");
		_translationsDir = Directory.Exists(subDir) ? subDir : Path.Combine(baseDir, "Translations");
		BuildEnglishDefaults();
		_initialized = true;
		if (InstanceField?.GetValue(null) is Localization loc)
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Localization] Localization already initialized — injecting retroactively");
			}
			InjectTranslations(loc);
		}
	}

	private static void BuildEnglishDefaults()
	{
		_englishDefaults = new Dictionary<string, string>
		{
			{ "hc_ui_title_companions", "Companions" },
			{ "hc_ui_title_choose", "Choose Your Companion" },
			{ "hc_ui_btn_spawn", "Summon Companion" },
			{ "hc_ui_title_hire", "Summon a Companion" },
			{ "hc_ui_btn_hire", "Summon Companion" },
			{ "hc_ui_desc_hire", "Odin will summon another companion if you make an offering of {0} coins. Customize their appearance on the left, then summon them.\n\nThey will follow you, fight by your side, and carry supplies. Equip them with gear and keep them fed to stay battle-ready." },
			{ "hc_shrine_use", "Your companions" },
			{ "hc_shrine_title", "Odin's Shrine" },
			{ "hc_shrine_flavor", "Here you can make an offering to Odin to receive a boon and check on those who are joining you on this adventure." },
			{ "hc_shrine_btn_recover", "Recover" },
			{ "hc_shrine_btn_recover_list", "Recover" },
			{ "hc_shrine_btn_summon", "Summon" },
			{ "hc_shrine_recover_title", "Recover companions" },
			{ "hc_shrine_offering", "Summoning a new companion asks an offering of {0} coins." },
			{ "hc_shrine_unsummoned", "Unsummoned - can be brought back without gear" },
			{ "hc_shrine_back", "Back" },
			{ "hc_shrine_none", "No companions on your roster in this world yet. A companion joins the roster once it has been near you for a few seconds." },
			{ "hc_shrine_alive", "Alive" },
			{ "hc_shrine_dead", "Dead - its tombstone will bring it back" },
			{ "hc_shrine_lost", "Lost - can be recovered" },
			{ "hc_shrine_unknown", "Unknown - not nearby, may still be alive" },
			{ "hc_msg_hire_need", "Odin asks for an offering of {0} coins to summon a companion - you have {1}, {2} short." },
			{ "hc_msg_hired", "Odin has summoned a new companion for you!" },
			{ "hc_ui_btn_buy", "Buy {0} ({1})" },
			{ "hc_ui_btn_need", "Need {0}" },
			{ "hc_ui_bank", "Bank: {0}" },
			{ "hc_ui_label_type", "Type" },
			{ "hc_ui_label_gender", "Gender" },
			{ "hc_ui_label_hair", "Hair Style" },
			{ "hc_ui_label_beard", "Beard Style" },
			{ "hc_ui_label_skin", "Skin Tone" },
			{ "hc_ui_label_hairtone", "Hair Tone" },
			{ "hc_ui_label_hairshade", "Hair Shade" },
			{ "hc_ui_label_hairdye", "Hair Dye (left = none)" },
			{ "hc_ui_label_subtype", "Sub-Type" },
			{ "hc_ui_btn_male", "Male" },
			{ "hc_ui_btn_female", "Female" },
			{ "hc_ui_btn_companion", "Companion" },
			{ "hc_ui_btn_dverger", "Dverger" },
			{ "hc_ui_beard_none", "None" },
			{ "hc_ui_desc_starter", "Odin has taken pity on you. He has sent a loyal companion to join you on your journey. Customize their appearance on the left, then summon them into the world.\n\nThey will follow you, fight by your side, and carry supplies. Equip them with gear and keep them fed to stay battle-ready.\n\nPress Escape to skip — you will be given another chance to summon a companion again next session." },
			{ "hc_ui_desc_trader", "Hire a loyal companion to join your journey. They will follow you across the world and fight by your side against any threat.\n\nEach companion has their own health, stamina and inventory. You will need to equip them with gear and keep them fed to stay battle-ready.\n\nInteract with your companion to open their panel, where you can manage their equipment and issue commands.\n\nCustomise their appearance on the left, then confirm your purchase." },
			{ "hc_ui_placeholder_name", "Enter name..." },
			{ "hc_ui_label_food", "Food" },
			{ "hc_radial_follow", "Follow" },
			{ "hc_radial_wood", "Wood" },
			{ "hc_radial_stone", "Stone" },
			{ "hc_radial_ore", "Ore" },
			{ "hc_radial_forage", "Forage" },
			{ "hc_radial_smelt", "Smelt" },
			{ "hc_radial_pending", "Pending" },
			{ "hc_radial_sethome", "Set Home" },
			{ "hc_radial_wander", "Wander" },
			{ "hc_radial_pickup", "Pickup" },
			{ "hc_radial_command", "Command" },
			{ "hc_radial_balanced", "Balanced" },
			{ "hc_radial_aggressive", "Aggressive" },
			{ "hc_radial_defensive", "Defensive" },
			{ "hc_radial_passive", "Passive" },
			{ "hc_radial_melee", "Melee" },
			{ "hc_radial_ranged", "Ranged" },
			{ "hc_radial_hunt", "Hunt" },
			{ "hc_radial_farm", "Farm" },
			{ "hc_radial_fish", "Fish" },
			{ "hc_radial_cook", "Cook" },
			{ "hc_radial_repair", "Repair and Restock" },
			{ "hc_radial_despawn", "Despawn" },
			{ "hc_despawn_popup_title", "Despawn Companion" },
			{ "hc_despawn_popup_text", "Permanently remove {0}? Anything they're carrying will be left in a tombstone." },
			{ "hc_despawn_popup_recover", "Changed your mind? Until you next load this world, Odin's Shrine can bring them back - without their gear, which stays in the tombstone." },
			{ "hc_radial_active", "ACTIVE" },
			{ "hc_radial_on", "ON" },
			{ "hc_radial_off", "OFF" },
			{ "hc_hover_inventory", "Inventory" },
			{ "hc_hover_commands", "Commands" },
			{ "hc_hover_hold", "Hold" },
			{ "hc_tomb_hold_resurrect", "Hold to resurrect" },
			{ "hc_tomb_resurrecting", "Resurrecting... {0}%" },
			{ "hc_tier_companion", "Companion" },
			{ "hc_tier_warrior", "Warrior" },
			{ "hc_tier_mage", "Mage" },
			{ "hc_tier_fire_mage", "Fire Mage" },
			{ "hc_tier_ice_mage", "Ice Mage" },
			{ "hc_tier_support", "Support" },
			{ "hc_speech_found_1", "I've been looking for you!" },
			{ "hc_speech_found_2", "There you are!" },
			{ "hc_speech_found_3", "Finally found you!" },
			{ "hc_msg_not_yours", "This is not your companion." },
			{ "hc_msg_need_coins", "Not enough coins in bank! Need {0}" },
			{ "hc_msg_arrived", "Your new {0} has arrived!" },
			{ "hc_msg_returned", "{0} has returned at {1}!" },
			{ "hc_msg_location_home", "home" },
			{ "hc_msg_location_spawn", "the world spawn" },
			{ "hc_msg_starter_joined", "A companion has joined you on your journey!" },
			{ "hc_msg_name_default", "Companion" },
			{ "hc_menu_mod_options", "Mod Options" },
			{ "hc_minimap_grave", "Grave" },
			{ "hc_msg_sleep_waiting", "Waiting for {0} to sleep..." },
			{ "hc_msg_sleep_waiting_generic", "Waiting for companions to sleep..." },
			{ "hc_msg_rested", "{0} is Rested (Comfort: {1})" },
			{ "hc_msg_rested_suffix", "Rested" },
			{ "hc_msg_tools_weak", "{0}'s tools are not strong enough" },
			{ "hc_msg_reposition_on", "UI Reposition Mode — drag to move, F7 to confirm" },
			{ "hc_msg_reposition_off", "UI position saved" },
			{ "hc_msg_commanded", "Moving to position." },
			{ "hc_speech_repair_start", "Time for repairs." },
			{ "hc_speech_repair_done", "All fixed up!" },
			{ "hc_speech_smelt_monitoring", "Everything's running. I'll keep watch." },
			{ "hc_speech_smelt_done", "All done smelting." },
			{ "hc_speech_smelt_fuel", "Fetching fuel." },
			{ "hc_speech_smelt_materials", "Fetching materials." },
			{ "hc_speech_homestead_refuel", "Time to refuel." },
			{ "hc_speech_homestead_stoked", "Fire's stoked." },
			{ "hc_speech_homestead_repair", "I'll patch that up." },
			{ "hc_speech_homestead_repaired", "Good as new." },
			{ "hc_speech_homestead_tidy", "Let me tidy up." },
			{ "hc_speech_homestead_tidied", "All tidied up." },
			{ "hc_speech_homestead_cooked", "Something tasty!" },
			{ "hc_speech_repair_buildings_done", "Everything's patched up." },
			{ "hc_speech_repair_nothing", "Nothing needs repair." },
			{ "hc_speech_repair_no_hammer", "I need a hammer for that." },
			{ "hc_speech_restock_done", "Fires are stoked." },
			{ "hc_speech_restock_nothing", "Nothing needs fuel." },
			{ "hc_speech_upkeep_nothing", "Everything's in order." },
			{ "hc_idle_why_0", "Nothing to do right now." },
			{ "hc_idle_why_1", "Nothing left to chop here." },
			{ "hc_idle_why_2", "Nothing left to mine around here." },
			{ "hc_idle_why_3", "No ore around here." },
			{ "hc_idle_why_5", "Nothing to pick right now." },
			{ "hc_idle_why_6", "The smelters are busy - I'll wait." },
			{ "hc_idle_why_7", "No game around." },
			{ "hc_idle_why_8", "Waiting for the crops to grow." },
			{ "hc_idle_why_9", "The fish aren't biting." },
			{ "hc_idle_why_15", "Everything's in order." },
			{ "hc_idle_why_17", "Nothing to cook right now." },
			{ "hc_idle_chat_1", "Fine day for it." },
			{ "hc_idle_chat_2", "Need anything?" },
			{ "hc_idle_chat_3", "I could use a break." },
			{ "hc_idle_chat_4", "Quiet around here." },
			{ "hc_idle_chat_5", "Smells like rain." },
			{ "hc_idle_chat_6", "Anything need doing?" },
			{ "hc_status_sleeping", "Sleeping" },
			{ "hc_status_resting", "Resting" },
			{ "hc_status_hunting", "Hunting a {0}" },
			{ "hc_status_fighting", "Fighting a {0}" },
			{ "hc_status_spear", "Fetching its spear" },
			{ "hc_status_order", "Carrying out your order" },
			{ "hc_status_nothing", "nothing to do" },
			{ "hc_status_following_look", "following you, looking out for work" },
			{ "hc_status_following", "Following you" },
			{ "hc_status_idle", "Idling" },
			{ "hc_status_staying", "Staying put" },
			{ "hc_status_waiting", "Waiting" },
			{ "hc_status_fetching", "Fetching {0}" },
			{ "hc_status_putting_back", "Putting back {0}" },
			{ "hc_status_unloading", "Emptying its bags" },
			{ "hc_status_repairing_gear", "Repairing its gear" },
			{ "hc_status_needs_home", "Needs {0} - waiting till we're home" },
			{ "hc_status_job_wood", "Wood" },
			{ "hc_status_job_stone", "Stone" },
			{ "hc_status_job_ore", "Ore" },
			{ "hc_status_job_forage", "Forage" },
			{ "hc_status_job_smelt", "Smelt" },
			{ "hc_status_job_hunt", "Hunt" },
			{ "hc_status_job_farm", "Farm" },
			{ "hc_status_job_fish", "Fish" },
			{ "hc_status_job_upkeep", "Repair and Restock" },
			{ "hc_status_job_cook", "Cook" },
			{ "hc_status_job_mead", "Mead" },
			{ "hc_doing_chop", "chopping {0}" },
			{ "hc_doing_mine", "mining {0}" },
			{ "hc_doing_pick", "picking up {0}" },
			{ "hc_doing_forage", "picking {0}" },
			{ "hc_doing_hunt", "hunting {0}" },
			{ "hc_doing_encumbered", "carrying too much - walking slowly" },
			{ "hc_doing_fish", "fishing for {0}" },
			{ "hc_doing_smelt_load", "loading the {0} with {1}" },
			{ "hc_doing_smelt_fuel", "fuelling the {0}" },
			{ "hc_doing_smelt_collect", "emptying the {0}" },
			{ "hc_doing_take_pile", "taking down the {0}" },
			{ "hc_doing_upkeep_repair", "repairing the {0}" },
			{ "hc_doing_upkeep_fuel", "fuelling the {0}" },
			{ "hc_doing_upkeep_sort", "sorting {0}" },
			{ "hc_doing_upkeep_tidy", "stacking up {0}" },
			{ "hc_doing_upkeep_stow", "putting back {0}" },
			{ "hc_doing_upkeep_pile", "piling up {0}" },
			{ "hc_doing_upkeep_honey", "collecting honey" },
			{ "hc_doing_home", "heading home" },
			{ "hc_doing_cook_make", "making {0}" },
			{ "hc_doing_cook_rescue", "taking food off the {0}" },
			{ "hc_doing_cook_store", "putting away {0}" },
			{ "hc_doing_mead_fill", "filling a fermenter with {0}" },
			{ "hc_doing_mead_tap", "tapping a fermenter" },
			{ "hc_why_gather_limit", "{0} {1}/{2} - at the limit." },
			{ "hc_why_gather_tier", "{0} needs a better {1}." },
			{ "hc_tool_axe", "axe" },
			{ "hc_tool_pickaxe", "pickaxe" },
			{ "hc_why_gather_near_base", "Leaving {0} trees within {1} m of buildings." },
			{ "hc_why_hunt_at_target", "{0} {1}/{2} - enough." },
			{ "hc_why_hunt_none_range", "No game within {0} m." },
			{ "hc_why_fish_none", "No fish within {0} m." },
			{ "hc_why_fish_at_target", "{0} {1}/{2} - enough." },
			{ "hc_why_fish_bait", "No bait for {0} ({1})." },
			{ "hc_why_fish_no_bank", "No spot to fish for {0} from." },
			{ "hc_why_smelt_no_ore", "No ore for the {0}." },
			{ "hc_why_smelt_no_wood", "No wood for the {0}." },
			{ "hc_why_smelt_no_fuel", "No {0} for the {1}." },
			{ "hc_why_smelt_pile_hammer", "Need a hammer to take down the {0}." },
			{ "hc_why_upkeep_no_fuel", "No {0} for the fires." },
			{ "hc_why_upkeep_no_station", "{0} damaged pieces have no station in range." },
			{ "hc_why_farm_seed_min", "Seeds at the minimum ({0}) - keeping them." },
			{ "hc_why_farm_zone_full", "Every spot is planted." },
			{ "hc_why_farm_no_seed", "No {0} to plant." },
			{ "hc_act_fetch_tool", "Fetching {0}." },
			{ "hc_act_return_tool", "Putting {0} back." },
			{ "hc_act_bed", "Off to bed." },
			{ "hc_status_farm_harvesting", "harvesting" },
			{ "hc_status_farm_pickup", "picking up the harvest" },
			{ "hc_status_farm_seeds", "fetching seeds" },
			{ "hc_status_farm_planting", "planting {0}" },
			{ "hc_status_farm_storing", "storing the harvest" },
			{ "hc_cmd_refused_off", "{0}: Command is off on the wheel." },
			{ "hc_cmd_refused_far", "{0} is {1} m away. Hold {2} s to call all." },
			{ "hc_why_spear_lost", "I lost my {0}." },
			{ "hc_msg_companion_died", "{0} has died ({1}, {2} m from you)" },
			{ "hc_where", "{0}, {1} m from you" },
			{ "hc_status_recover", "Getting its gear back" },
			{ "hc_status_recover_wait", "Getting its gear back - waiting for it to be safe" },
			{ "hc_status_recover_far", "Its gear is out of reach" },
			{ "hc_status_recover_equip", "Putting its gear back on" },
			{ "hc_speech_recover_wait", "Too many of them. I'll wait for my gear." },
			{ "hc_speech_recover_far", "My gear's out of reach. I'll stay out of fights." },
			{ "hc_speech_recover_done", "Got my gear back." },
			{ "hc_msg_stance_set", "{0}: {1}" },
			{ "hc_msg_stance_none", "No companion in range for {0}." },
			{ "hc_msg_resurrected", "{0} has been resurrected!" },
			{ "hc_msg_back_bed", "{0} is back - at its bed ({1})" },
			{ "hc_msg_back_spawn", "{0} is back - at the world spawn ({1})" },
			{ "hc_msg_back_here", "{0} is back - next to you" },
			{ "hc_why_hunt_none", "No game around." },
			{ "hc_why_hunt_enough", "We have enough meat." },
			{ "hc_why_hunt_reach", "The game around here is out of reach." },
			{ "hc_why_hunt_passive", "I won't hunt in the Passive stance." },
			{ "hc_why_hunt_spear", "Let me get my spear back first." },
			{ "hc_why_hunt_got_away", "It got away." },
			{ "hc_why_pack_full", "My pack is full." },
			{ "hc_why_pack_full_far", "My pack is full and home is too far." },
			{ "hc_why_gather_none", "Nothing left to gather around here." },
			{ "hc_why_no_way", "I can't find a way there." },
			{ "hc_why_cant_reach_up", "I can't get up there." },
			{ "hc_why_chests_full", "The chests near home are full." },
			{ "hc_why_chest_unreachable", "I can't get to that chest." },
			{ "hc_why_farm_no_zones", "Nothing ripe, and nowhere to plant." },
			{ "hc_why_farm_waiting", "Waiting for the crops to grow." },
			{ "hc_why_farm_no_chest", "No chest has room for the harvest." },
			{ "hc_why_smelt_no_chests", "There are no chests near the smelters." },
			{ "hc_why_smelt_nothing", "Nothing to smelt in the chests." },
			{ "hc_why_cook_nothing", "Nothing to cook right now." },
			{ "hc_why_mead_short", "{0} {1}/{2} for {3}." },
			{ "hc_why_cook_no_food", "No food ingredients in reach." },
			{ "hc_why_cook_stocked", "Food is stocked up." },
			{ "hc_why_cook_no_station", "Can't use the {0} (level, fire, roof)." },
			{ "hc_why_repair_no_station", "No station near home can fix my {0}." },
			{ "hc_why_tool_taken", "You took my {0} - I'll do without." },
			{ "hc_why_need_tool", "I need {0} for this job." },
			{ "hc_why_fish_no_rod", "I need a fishing rod." },
			{ "hc_speech_farm_planting", "Time to plant some seeds." },
			{ "hc_speech_farm_harvesting", "Crops are ready!" },
			{ "hc_speech_farm_done", "All done farming." },
			{ "hc_speech_farm_no_seeds", "I need seeds to plant." },
			{ "hc_speech_farm_no_cultivator", "I need a cultivator to plant." },
			{ "hc_speech_no_chest", "No chest nearby to unload!" },
			{ "hc_speech_no_bow", "I need a bow and arrows to shoot!" },
			{ "hc_speech_overweight", "My back is hurting from all this weight!" },
			{ "hc_speech_cook_cooking", "Let me cook something." },
			{ "hc_speech_cook_brewing", "Brewing a batch." },
			{ "hc_speech_cook_fermenter", "Into the fermenter." },
			{ "hc_speech_cook_tapping", "This batch is ready!" },
			{ "hc_speech_cook_storing", "Putting these away." },
			{ "hc_speech_fish_need_rod", "I need a fishing rod." },
			{ "hc_speech_broke_axe", "My axe broke. I can fix it when we get home." },
			{ "hc_speech_broke_pickaxe", "My pickaxe broke. I can fix it when we get home." },
			{ "hc_speech_broke_tool", "My tool broke. I can fix it when we get home." },
			{ "hc_speech_unload", "I need to empty my bags first." },
			{ "hc_speech_need_axe", "I need an axe for that." },
			{ "hc_speech_need_pickaxe", "I need a pickaxe for that." },
			{ "hc_speech_need_arrows", "I need arrows." },
			{ "hc_speech_plenty_wood", "We've got plenty of wood." },
			{ "hc_speech_plenty_stone", "We've got plenty of stone." },
			{ "hc_speech_plenty_ore", "We've got plenty of ore." },
			{ "hc_speech_fish_need_bait", "I need bait to fish." },
			{ "hc_speech_fish_no_water", "I can't find water nearby..." },
			{ "hc_speech_fish_got_away", "It got away!" },
			{ "hc_speech_fish_caught", "I caught a {0}!" },
			{ "hc_speech_fish_full", "My bags are full!" },
			{ "hc_speech_tomb_found", "My belongings!" },
			{ "hc_speech_tomb_recovered", "Got everything back." },
			{ "hc_speech_tomb_empty", "Nothing left to take." },
			{ "hc_speech_deposit_done", "All stowed away." },
			{ "hc_speech_deposit_empty", "Nothing to deposit." },
			{ "hc_cmd_comehere_1", "Coming!" },
			{ "hc_cmd_comehere_2", "On my way back!" },
			{ "hc_cmd_comehere_3", "Right behind you." },
			{ "hc_cmd_attack_1", "On it!" },
			{ "hc_cmd_attack_2", "Going in!" },
			{ "hc_cmd_attack_3", "I'll take them down!" },
			{ "hc_cmd_attack_4", "For Odin!" },
			{ "hc_cmd_cart_pull_1", "I'll haul this." },
			{ "hc_cmd_cart_pull_2", "Got the cart!" },
			{ "hc_cmd_cart_pull_3", "Let me pull." },
			{ "hc_cmd_cart_release_1", "Letting go." },
			{ "hc_cmd_cart_release_2", "Cart's free." },
			{ "hc_cmd_cart_release_3", "Released!" },
			{ "hc_cmd_door_1", "Getting the door." },
			{ "hc_cmd_door_2", "I'll get it." },
			{ "hc_cmd_door_3", "Door's open!" },
			{ "hc_cmd_doorclose_1", "Closing it." },
			{ "hc_cmd_doorclose_2", "I'll shut it." },
			{ "hc_cmd_doorclose_3", "Shutting the door." },
			{ "hc_cmd_door_locked", "It's locked." },
			{ "hc_cmd_sit_1", "Nice and warm." },
			{ "hc_cmd_sit_2", "Good spot to rest." },
			{ "hc_cmd_sit_3", "I'll sit here." },
			{ "hc_cmd_sleep_1", "Time for some rest." },
			{ "hc_cmd_sleep_2", "I could use some sleep." },
			{ "hc_cmd_sleep_3", "Wake me if you need me." },
			{ "hc_cmd_wake_1", "I'm up!" },
			{ "hc_cmd_wake_2", "Already?" },
			{ "hc_cmd_wake_3", "Right, let's go." },
			{ "hc_cmd_notmybed_1", "That's not my bed." },
			{ "hc_cmd_notmybed_2", "That one's already taken." },
			{ "hc_cmd_notmybed_3", "That's not mine to use." },
			{ "hc_cmd_deposit_1", "Dropping off my haul." },
			{ "hc_cmd_deposit_2", "Storing the goods." },
			{ "hc_cmd_deposit_3", "Lightening my load." },
			{ "hc_cmd_deposit_empty_1", "I've got nothing to drop off." },
			{ "hc_cmd_deposit_empty_2", "Already empty." },
			{ "hc_cmd_harvest_1", "I'll get that." },
			{ "hc_cmd_harvest_2", "On it!" },
			{ "hc_cmd_harvest_3", "Looks like good stuff." },
			{ "hc_cmd_cancel_1", "Standing by." },
			{ "hc_cmd_cancel_2", "Awaiting orders." },
			{ "hc_cmd_cancel_3", "Ready when you are." },
			{ "hc_cmd_move_1", "Heading over." },
			{ "hc_cmd_move_2", "On my way." },
			{ "hc_cmd_move_3", "Moving out." },
			{ "hc_cmd_repair_1", "I'll fix my gear up." },
			{ "hc_cmd_repair_2", "Time for repairs." },
			{ "hc_cmd_repair_3", "This needs some work." },
			{ "hc_cmd_board_1", "Coming aboard!" },
			{ "hc_cmd_board_2", "All aboard!" },
			{ "hc_cmd_board_3", "I'll hop on." },
			{ "hc_cmd_repair_nothing_1", "Nothing to fix here." },
			{ "hc_cmd_repair_nothing_2", "My gear's fine." },
			{ "hc_cmd_repair_nothing_3", "No repairs needed." },
			{ "hc_cmd_hive", "I'll fetch the honey." },
			{ "hc_cmd_hive_empty", "No honey in there yet." },
			{ "hc_say_encumbered", "This is too heavy - I can barely move." },
			{ "hc_say_no_torches", "My last torch burned out." },
			{ "hc_say_portal_stay", "I can't take {0} through the portal - I'll wait here." },
			{ "hc_say_portal_left", "Left {0} in the chest by the portal." },
			{ "hc_cmd_tombstone_1", "I'll grab my things." },
			{ "hc_cmd_tombstone_2", "Let me get that." },
			{ "hc_cmd_tombstone_3", "On my way to pick that up." },
			{ "hc_cmd_smelt_1", "I'll keep the fires burning." },
			{ "hc_cmd_smelt_2", "Time to smelt." },
			{ "hc_cmd_smelt_3", "I'll tend the furnace." },
			{ "hc_radial_farmzones", "Farm Zones" },
			{ "hc_farmzone_title", "Select Crop" },
			{ "hc_farmzone_any", "Any Crop" },
			{ "hc_farmzone_hint", "[LMB] Place  [RMB] Remove  [Scroll] Resize  [RMB+Scroll] Rotate  [Esc] Exit" },
			{ "hc_farmzone_hint_gamepad", "[{0}] Place  [{1}] Remove  [{2}/{3}] Resize  [{4}] Rotate  [{5}] Exit" },
			{ "hc_farmzone_full", "Maximum zones reached (4)" }
		};
	}

	private static void InjectTranslations(Localization loc)
	{
		if (loc == null)
		{
			return;
		}
		OfflineCompanionsAI.ModLang.UpdateGameLanguage(loc.GetSelectedLanguage());
		string selectedLanguage = OfflineCompanionsAI.ModLang.IsRussian ? "Russian" : (OfflineCompanionsAI.ModLang.IsSpanish ? "Spanish" : "English");
		if (selectedLanguage == _lastInjectedLanguage)
		{
			return;
		}
		_lastInjectedLanguage = selectedLanguage;
		if (!(TranslationsField?.GetValue(loc) is Dictionary<string, string> dictionary))
		{
			return;
		}
		if (_englishDefaults != null)
		{
			foreach (KeyValuePair<string, string> englishDefault in _englishDefaults)
			{
				dictionary[englishDefault.Key] = englishDefault.Value;
			}
		}
		Dictionary<string, string> dictionary2 = LoadTranslationFile((selectedLanguage != "English") ? selectedLanguage : null);
		if (dictionary2 == null && selectedLanguage != "English")
		{
			dictionary2 = LoadTranslationFile("English");
		}
		int num = 0;
		if (dictionary2 != null)
		{
			foreach (KeyValuePair<string, string> item in dictionary2)
			{
				dictionary[item.Key] = item.Value;
				num++;
			}
		}
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Localization] Injected {_englishDefaults?.Count ?? 0} defaults + {num} \"{selectedLanguage}\" overrides");
		}
	}

	public static void EnsureDefaultFile()
	{
		if (_englishDefaults == null)
		{
			return;
		}
		try
		{
			if (!Directory.Exists(_translationsDir))
			{
				Directory.CreateDirectory(_translationsDir);
			}
			string text = Path.Combine(_translationsDir, "English.json");
			if (File.Exists(text))
			{
				Dictionary<string, string> dictionary = ParseTranslationJson(File.ReadAllText(text));
				if (dictionary != null && dictionary.Count >= _englishDefaults.Count)
				{
					return;
				}
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo($"[Localization] English.json is stale ({dictionary?.Count ?? 0} vs {_englishDefaults.Count} keys) — regenerating.");
				}
			}
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>(_englishDefaults);
			list.Sort((KeyValuePair<string, string> a, KeyValuePair<string, string> b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("{");
			stringBuilder.AppendLine("    \"entries\": [");
			for (int num = 0; num < list.Count; num++)
			{
				string text2 = ((num < list.Count - 1) ? "," : "");
				stringBuilder.AppendLine("        { \"key\": \"" + EscapeJson(list[num].Key) + "\", \"value\": \"" + EscapeJson(list[num].Value) + "\" }" + text2);
			}
			stringBuilder.AppendLine("    ]");
			stringBuilder.AppendLine("}");
			File.WriteAllText(text, stringBuilder.ToString(), Encoding.UTF8);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Localization] Created default " + text);
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Localization] Failed to write English.json: " + ex.Message);
		}
	}

	private static Dictionary<string, string> ParseTranslationJson(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		MatchCollection matchCollection = EntryRegex.Matches(json);
		for (int i = 0; i < matchCollection.Count; i++)
		{
			Match match = matchCollection[i];
			string text = UnescapeJson(match.Groups[1].Value);
			string value = UnescapeJson(match.Groups[2].Value);
			if (!string.IsNullOrEmpty(text))
			{
				dictionary[text] = value;
			}
		}
		return dictionary;
	}

	private static string EscapeJson(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")
			.Replace("\r", "\\r")
			.Replace("\t", "\\t");
	}

	[HarmonyPatch(typeof(Localization), "SetupLanguage")]
	[HarmonyPostfix]
	private static void SetupLanguage_Postfix(Localization __instance)
	{
		if (_initialized)
		{
			InjectTranslations(__instance);
		}
	}

	private static Dictionary<string, string> LoadTranslationFile(string language)
	{
		if (string.IsNullOrEmpty(language) || string.IsNullOrEmpty(_translationsDir))
		{
			return null;
		}
		string text = Path.Combine(_translationsDir, language + ".json");
		if (!File.Exists(text))
		{
			return null;
		}
		try
		{
			Dictionary<string, string> dictionary = ParseTranslationJson(File.ReadAllText(text));
			if (dictionary == null || dictionary.Count == 0)
			{
				return null;
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Localization] Loaded {dictionary.Count} keys from {text}");
			}
			return dictionary;
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Localization] Failed to load " + text + ": " + ex.Message);
			return null;
		}
	}

	private static string UnescapeJson(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t")
			.Replace("\\\"", "\"")
			.Replace("\\\\", "\\");
	}
}
