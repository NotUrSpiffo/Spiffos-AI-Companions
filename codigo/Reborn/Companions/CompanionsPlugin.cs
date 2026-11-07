using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Companions;

[BepInPlugin("Maddhattr.OfflineCompanionsReborn", "Offline Companions Reborn", "1.5.8")]
[BepInDependency("com.profmags.traderoverhaul", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.haldor.bounties", BepInDependency.DependencyFlags.SoftDependency)]
public class CompanionsPlugin : BaseUnityPlugin
{
	public const string PluginGUID = "Maddhattr.OfflineCompanionsReborn";

	public const string PluginName = "Offline Companions Reborn";

	public const string PluginVersion = "1.5.8";

	private static Harmony _harmony;

	internal static ManualLogSource Log;

	internal static bool LogNormal = true;

	internal static bool LogDetailed;

	private bool _fontFixWarned;

	private const string OriginalGUID = "com.profmags.companions";

	private bool _started;

	internal static void ApplyLogLevel()
	{
		string text = ((ModConfig.LogLevel != null) ? ModConfig.LogLevel.Value : "Normal");
		LogNormal = Log != null && text != "Off";
		LogDetailed = Log != null && text == "Detailed";
	}

	private ConfigFile MHR_GetConfigFile()
	{
		try
		{
			MHR_MigrateOldConfig();
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Config] Couldn't copy settings from the old config file: " + ex.ToString());
		}
		return Config;
	}

	private void MHR_MigrateOldConfig()
	{
		ConfigFile config = Config;
		string configFilePath = config.ConfigFilePath;
		string text = Path.Combine(Paths.ConfigPath, "com.profmags.companions.cfg");
		if (!File.Exists(configFilePath) && File.Exists(text))
		{
			File.Copy(text, configFilePath);
			config.Reload();
			if (LogNormal)
			{
				Log.LogInfo("[Config] Settings copied to Maddhattr.OfflineCompanionsReborn.cfg from com.profmags.companions.cfg (the old file is no longer used and can be deleted)");
			}
		}
	}

	private static bool OriginalInstalled()
	{
		return Chainloader.PluginInfos.ContainsKey("com.profmags.companions");
	}

	private static void LogOriginalInstalled()
	{
		Log.LogError("The original Offline Companions by ProfMags (com.profmags.companions) is also installed. It doesn't work on Valheim 1.0 and can't run alongside Offline Companions Reborn. Remove the original Offline Companions to use Offline Companions Reborn. Offline Companions Reborn has not started.");
	}

	private void Awake()
	{
		Log = Logger;
		if (LogNormal)
		{
			Log.LogInfo("[BUILD] Companions.dll built from C# source");
		}
		if (LogNormal)
		{
			Log.LogInfo("Offline Companions Reborn v1.5.8 loading...");
		}
		if (OriginalInstalled())
		{
			LogOriginalInstalled();
			enabled = false;
			return;
		}
		_started = true;
		ModConfig.Init(MHR_GetConfigFile());
		ApplyLogLevel();
		ModConfig.LogLevel.SettingChanged += delegate
		{
			ApplyLogLevel();
		};
		_harmony = new Harmony("Maddhattr.OfflineCompanionsReborn");
		try
		{
			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
			{
				if (type.Namespace == "Companions" && type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length != 0)
				{
					_harmony.CreateClassProcessor(type).Patch();
				}
			}
			int num = 0;
			foreach (MethodBase patchedMethod in _harmony.GetPatchedMethods())
			{
				_ = patchedMethod;
				num++;
			}
			if (LogNormal)
			{
				Log.LogInfo(string.Format("{0} loaded successfully! ({1} methods patched)", "Offline Companions Reborn", num));
			}
		}
		catch (Exception arg)
		{
			Log.LogError($"[Companions] Harmony PatchAll failed: {arg}");
		}
		EquipDiag.Install(_harmony);
		ModLocalization.Init();
		ModLocalization.EnsureDefaultFile();
		SceneManager.sceneLoaded += OnSceneLoaded;
		Game.m_playerInitialSpawn += CompanionManager.MHR_StarterOnce;
		ConfigPanel.Create();
		FarmZonePlacer.EnsureInstance();
	}

	private void Start()
	{
		if (_started && OriginalInstalled())
		{
			LogOriginalInstalled();
			Shutdown();
			enabled = false;
		}
	}

	private void Shutdown()
	{
		if (_started)
		{
			_started = false;
			SceneManager.sceneLoaded -= OnSceneLoaded;
			Game.m_playerInitialSpawn -= CompanionManager.MHR_StarterOnce;
			_harmony?.UnpatchSelf();
			if (ConfigPanel.Instance != null)
			{
				UnityEngine.Object.Destroy(ConfigPanel.Instance.gameObject);
			}
			if (FarmZonePlacer.Instance != null)
			{
				UnityEngine.Object.Destroy(FarmZonePlacer.Instance.gameObject);
			}
		}
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		FontFix.Ensure("scene:" + scene.name);
	}

	private void OnDestroy()
	{
		Shutdown();
		if (LogNormal)
		{
			Log?.LogInfo("Offline Companions unloaded.");
		}
	}

	private void Update()
	{
		try
		{
			MHR_RespawnTracker.Tick();
			MHR_ToolKeeper.Tick();
		}
		catch (Exception ex)
		{
			Log.LogWarning("[Respawn] Tracker error: " + ex.ToString());
		}
		try
		{
			PlayerCombatLog.Tick();
		}
		catch (Exception ex2)
		{
			Log.LogWarning("[You] Player combat log error: " + ex2.Message);
		}
		try
		{
			MHR_Roster.Tick();
		}
		catch (Exception ex3)
		{
			Log.LogWarning("[Roster] Error: " + ex3.ToString());
		}
	}

	private static bool IsBrokenTmpFont(TMP_FontAsset font)
	{
		if (!(font == null))
		{
			return font.name.IndexOf("LiberationSans", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	private static TMP_FontAsset FindReplacementTmpFont()
	{
		TMP_Text[] array = Resources.FindObjectsOfTypeAll<TMP_Text>();
		foreach (TMP_Text tMP_Text in array)
		{
			if (!(tMP_Text == null))
			{
				TMP_FontAsset font = tMP_Text.font;
				if (!IsBrokenTmpFont(font))
				{
					return font;
				}
			}
		}
		TMP_FontAsset[] array2 = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
		TMP_FontAsset tMP_FontAsset = null;
		TMP_FontAsset[] array3 = array2;
		foreach (TMP_FontAsset tMP_FontAsset2 in array3)
		{
			if (!IsBrokenTmpFont(tMP_FontAsset2))
			{
				string text = tMP_FontAsset2.name.ToLowerInvariant();
				if (text.Contains("averia") || text.Contains("norse") || text.Contains("valheim"))
				{
					return tMP_FontAsset2;
				}
				if (tMP_FontAsset == null)
				{
					tMP_FontAsset = tMP_FontAsset2;
				}
			}
		}
		return tMP_FontAsset;
	}

	private void EnsureTmpDefaultFont(string source)
	{
		if (!IsBrokenTmpFont(TMP_Settings.defaultFontAsset))
		{
			return;
		}
		TMP_FontAsset tMP_FontAsset = FindReplacementTmpFont();
		if (tMP_FontAsset == null)
		{
			if (!_fontFixWarned)
			{
				_fontFixWarned = true;
				if (LogDetailed)
				{
					Log?.LogDebug("[Fonts] No replacement TMP font found yet (" + source + ") — will retry on next scene load.");
				}
			}
			return;
		}
		TMP_Settings.defaultFontAsset = tMP_FontAsset;
		_fontFixWarned = false;
		int num = 0;
		TMP_Text[] array = Resources.FindObjectsOfTypeAll<TMP_Text>();
		foreach (TMP_Text tMP_Text in array)
		{
			if (!(tMP_Text == null) && IsBrokenTmpFont(tMP_Text.font))
			{
				tMP_Text.font = tMP_FontAsset;
				num++;
			}
		}
		if (LogNormal)
		{
			Log?.LogInfo($"[Fonts] TMP default font repaired to '{tMP_FontAsset.name}' ({source}), reassigned={num}.");
		}
	}
}
