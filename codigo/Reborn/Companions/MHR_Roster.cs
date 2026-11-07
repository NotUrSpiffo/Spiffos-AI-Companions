using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

internal static class MHR_Roster
{
	internal enum Status
	{
		Alive,
		Dead,
		Lost,
		Unknown,
		Unsummoned
	}

	internal class Entry
	{
		public string Id = "";

		public int Gen;

		public string Name = "";

		public string Prefab = "";

		public string Appearance = "";

		public string Owner = "";

		public int ActionMode;

		public int Schema;

		public bool Follow = true;

		public bool FollowPaused;

		public bool StayHome;

		public bool AutoPickup = true;

		public bool Wander;

		public int Commandable = 1;

		public int Formation = -1;

		public string Skills = "";

		public bool HasHome;

		public Vector3 HomePos;

		public int CombatStance;

		public bool HasBed;

		public Vector3 BedPos;

		public string Inventory = "";

		public Vector3 LastPos;

		public double LastSeen;

		public bool Dead;

		public string FarmZones;

		public bool Unsummoned;
	}

	internal static readonly int GenHash = "HC_Gen".GetStableHashCode();

	internal static readonly int RespawnGenHash = "HC_Respawn_Gen".GetStableHashCode();

	private const int Version = 1;

	private const float ConfirmSeconds = 60f;

	private const float LoadSettleSeconds = 10f;

	private static bool s_saving;

	private static readonly HashSet<string> s_sessionUnsummoned = new HashSet<string>();

	private static ZNet s_sessionNet;

	private static readonly List<Entry> s_entries = new List<Entry>();

	private static string s_loadedKey;

	private static Player s_loadedFor;

	private static float s_nextSnapshot;

	private static string s_confirmId;

	private static float s_confirmUntil;

	private static readonly FieldInfo s_objectsById = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");

	private static readonly Dictionary<string, Vector3> s_where = new Dictionary<string, Vector3>();

	private static readonly Dictionary<string, KeyValuePair<bool, float>> s_goneCache = new Dictionary<string, KeyValuePair<bool, float>>();

	private static readonly HashSet<string> s_goneLogged = new HashSet<string>();

	private static string Key()
	{
		ZNet instance = ZNet.instance;
		if (instance == null)
		{
			return null;
		}
		return $"HC_Roster_{instance.GetWorldUID()}";
	}

	private static string MyId()
	{
		return Player.m_localPlayer?.GetPlayerID().ToString() ?? "";
	}

	private static bool EnsureLoaded()
	{
		Player localPlayer = Player.m_localPlayer;
		string text = Key();
		if (localPlayer == null || text == null)
		{
			return false;
		}
		if ((object)s_loadedFor == localPlayer && s_loadedKey == text)
		{
			return true;
		}
		s_loadedFor = localPlayer;
		s_loadedKey = text;
		s_entries.Clear();
		s_confirmId = null;
		s_nextSnapshot = Time.unscaledTime + 10f + 2f;
		if (localPlayer.m_customData.TryGetValue(text, out var value) && !string.IsNullOrEmpty(value))
		{
			string[] array = value.Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < array.Length; i++)
			{
				Entry entry = Read(array[i]);
				if (entry != null)
				{
					s_entries.Add(entry);
				}
			}
		}
		if ((object)s_sessionNet != ZNet.instance)
		{
			s_sessionNet = ZNet.instance;
			s_sessionUnsummoned.Clear();
		}
		int num = s_entries.RemoveAll((Entry e) => e.Unsummoned && !s_sessionUnsummoned.Contains(e.Id));
		if (num > 0)
		{
			Save(toDisk: false);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Roster] Dropped {num} companion(s) unsummoned in an earlier session");
			}
		}
		return true;
	}

	private static void Save(bool toDisk)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || s_loadedKey == null)
		{
			return;
		}
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Entry s_entry in s_entries)
		{
			stringBuilder.Append(Write(s_entry)).Append('\n');
		}
		localPlayer.m_customData[s_loadedKey] = stringBuilder.ToString();
		if (!toDisk || !(Game.instance != null))
		{
			return;
		}
		s_saving = true;
		try
		{
			Game.instance.SavePlayerProfile(setLogoutPoint: false);
		}
		finally
		{
			s_saving = false;
		}
	}

	private static string Write(Entry e)
	{
		ZPackage zPackage = new ZPackage();
		zPackage.Write(1);
		zPackage.Write(e.Id);
		zPackage.Write(e.Gen);
		zPackage.Write(e.Name);
		zPackage.Write(e.Prefab);
		zPackage.Write(e.Appearance);
		zPackage.Write(e.Owner);
		zPackage.Write(e.ActionMode);
		zPackage.Write(e.Schema);
		zPackage.Write(e.Follow);
		zPackage.Write(e.FollowPaused);
		zPackage.Write(e.StayHome);
		zPackage.Write(e.AutoPickup);
		zPackage.Write(e.Wander);
		zPackage.Write(e.Commandable);
		zPackage.Write(e.Formation);
		zPackage.Write(e.Skills);
		zPackage.Write(e.HasHome);
		zPackage.Write(e.HomePos);
		zPackage.Write(e.CombatStance);
		zPackage.Write(e.HasBed);
		zPackage.Write(e.BedPos);
		zPackage.Write(e.Inventory);
		zPackage.Write(e.LastPos);
		zPackage.Write(e.LastSeen);
		zPackage.Write(e.Dead);
		zPackage.Write(e.FarmZones ?? "");
		zPackage.Write(e.Unsummoned);
		return zPackage.GetBase64();
	}

	private static Entry Read(string data)
	{
		try
		{
			ZPackage zPackage = new ZPackage(data);
			if (zPackage.ReadInt() != 1)
			{
				return null;
			}
			return new Entry
			{
				Id = zPackage.ReadString(),
				Gen = zPackage.ReadInt(),
				Name = zPackage.ReadString(),
				Prefab = zPackage.ReadString(),
				Appearance = zPackage.ReadString(),
				Owner = zPackage.ReadString(),
				ActionMode = zPackage.ReadInt(),
				Schema = zPackage.ReadInt(),
				Follow = zPackage.ReadBool(),
				FollowPaused = zPackage.ReadBool(),
				StayHome = zPackage.ReadBool(),
				AutoPickup = zPackage.ReadBool(),
				Wander = zPackage.ReadBool(),
				Commandable = zPackage.ReadInt(),
				Formation = zPackage.ReadInt(),
				Skills = zPackage.ReadString(),
				HasHome = zPackage.ReadBool(),
				HomePos = zPackage.ReadVector3(),
				CombatStance = zPackage.ReadInt(),
				HasBed = zPackage.ReadBool(),
				BedPos = zPackage.ReadVector3(),
				Inventory = zPackage.ReadString(),
				LastPos = zPackage.ReadVector3(),
				LastSeen = zPackage.ReadDouble(),
				Dead = zPackage.ReadBool(),
				FarmZones = ReadOptionalString(zPackage),
				Unsummoned = ReadOptionalBool(zPackage)
			};
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Roster] Skipped an unreadable roster entry: " + ex.Message);
			return null;
		}
	}

	private static bool ReadOptionalBool(ZPackage p)
	{
		try
		{
			return p.GetPos() < p.Size() && p.ReadBool();
		}
		catch
		{
			return false;
		}
	}

	private static string ReadOptionalString(ZPackage p)
	{
		try
		{
			return (p.GetPos() < p.Size()) ? p.ReadString() : "";
		}
		catch
		{
			return "";
		}
	}

	private static Entry Find(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		foreach (Entry s_entry in s_entries)
		{
			if (s_entry.Id == id)
			{
				return s_entry;
			}
		}
		return null;
	}

	internal static int GenOf(string id)
	{
		if (!EnsureLoaded())
		{
			return 0;
		}
		return Find(id)?.Gen ?? 0;
	}

	private static bool IsMine(CompanionSetup c, out ZNetView nview, out ZDO zdo)
	{
		nview = c?.GetComponent<ZNetView>();
		zdo = nview?.GetZDO();
		if (zdo == null || !nview.IsOwner())
		{
			return false;
		}
		string text = MyId();
		if (text.Length > 0)
		{
			return zdo.GetString(CompanionSetup.OwnerHash) == text;
		}
		return false;
	}

	internal static void Tick()
	{
		if (ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded())
		{
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (!localPlayer.IsDead() && !localPlayer.InIntro())
		{
			RemoveStaleCopies();
			if (Time.unscaledTime >= s_nextSnapshot)
			{
				s_nextSnapshot = Time.unscaledTime + ModConfig.RosterSaveInterval.Value;
				SnapshotAll();
			}
			if (ModConfig.ShowCompanionRoster.Value)
			{
				ModConfig.ShowCompanionRoster.Value = false;
				ShowRoster();
			}
			if (ModConfig.RecoverLostCompanion.Value)
			{
				ModConfig.RecoverLostCompanion.Value = false;
				RecoverNext();
			}
		}
	}

	private static void RemoveStaleCopies()
	{
		List<CompanionSetup> list = null;
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (!IsMine(allCompanion, out var _, out var zdo))
			{
				continue;
			}
			Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
			if (entry != null && entry.Gen > zdo.GetInt(GenHash))
			{
				Character component = allCompanion.GetComponent<Character>();
				if (component != null && component.GetHealth() > 0f && !allCompanion._mhrStale)
				{
					(list ?? (list = new List<CompanionSetup>())).Add(allCompanion);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (CompanionSetup item in list)
		{
			ZDO zDO = item.GetComponent<ZNetView>().GetZDO();
			string text = zDO.GetString(CompanionSetup.NameHash, "Companion");
			CompanionsPlugin.Log.LogWarning($"[Roster] Found an old copy of \"{text}\" at {item.transform.position:F1} (copy {zDO.GetInt(GenHash)}, the recovered one is copy {Find(zDO.GetString(CompanionSetup.MHR_CompanionIdHash)).Gen}) - removing it");
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, "An old copy of " + text + " was found and removed (" + text + " was recovered).");
			item._mhrStale = true;
			item.PrepareForDespawn();
			Humanoid component2 = item.GetComponent<Humanoid>();
			if (component2 != null)
			{
				component2.m_onDeath = null;
			}
			item.GetComponent<ZNetView>().Destroy();
		}
	}

	internal static void OnProfileSave()
	{
		if (s_saving)
		{
			return;
		}
		try
		{
			if (ZNet.instance != null && ZNetScene.instance != null && EnsureLoaded())
			{
				SnapshotAll();
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Roster] Couldn't save the roster with the character: " + ex.Message);
		}
	}

	private static void SnapshotAll()
	{
		bool flag = false;
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (allCompanion._mhrStale || allCompanion._mhrDespawning || allCompanion._mhrDying || Time.time - allCompanion._mhrAwakeTime < 10f || !IsMine(allCompanion, out var _, out var zdo))
			{
				continue;
			}
			Character component = allCompanion.GetComponent<Character>();
			if (!(component == null) && !(component.GetHealth() <= 0f))
			{
				string text = allCompanion.MHR_EnsureCompanionId();
				if (!string.IsNullOrEmpty(text) && (Find(text)?.Gen ?? 0) <= zdo.GetInt(GenHash))
				{
					Snapshot(allCompanion, zdo, text);
					flag = true;
				}
			}
		}
		if (flag)
		{
			Save(toDisk: false);
		}
	}

	private static void Snapshot(CompanionSetup c, ZDO zdo, string id)
	{
		Entry entry = Find(id);
		if (entry == null)
		{
			entry = new Entry
			{
				Id = id
			};
			s_entries.Add(entry);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo(string.Format("[Roster] Added \"{0}\" to your companion roster for this world ({1} item(s))", zdo.GetString(CompanionSetup.NameHash, "Companion"), (c.GetComponent<Humanoid>()?.GetInventory()?.NrOfItems()).GetValueOrDefault()));
			}
		}
		entry.Gen = zdo.GetInt(GenHash);
		entry.Name = zdo.GetString(CompanionSetup.NameHash, "Companion");
		GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
		entry.Prefab = ((prefab != null) ? prefab.name : CompanionTierData.Companion.PrefabName);
		entry.Appearance = zdo.GetString(CompanionSetup.AppearanceHash);
		entry.Owner = zdo.GetString(CompanionSetup.OwnerHash);
		entry.ActionMode = zdo.GetInt(CompanionSetup.ActionModeHash);
		entry.Schema = zdo.GetInt(CompanionSetup.ActionModeSchemaHash);
		entry.Follow = zdo.GetBool(CompanionSetup.FollowHash, defaultValue: true);
		entry.FollowPaused = zdo.GetBool(CompanionTombstoneResurrect.FollowPausedByJobHash);
		entry.StayHome = zdo.GetBool(CompanionSetup.StayHomeHash);
		entry.AutoPickup = zdo.GetBool(CompanionSetup.AutoPickupHash, defaultValue: true);
		entry.Wander = zdo.GetBool(CompanionSetup.WanderHash);
		entry.Commandable = zdo.GetInt(CompanionSetup.IsCommandableHash, 1);
		entry.Formation = zdo.GetInt(CompanionSetup.FormationSlotHash, -1);
		CompanionSkills component = c.GetComponent<CompanionSkills>();
		entry.Skills = ((component != null) ? component.SerializeSkills() : zdo.GetString(CompanionSkills.SkillsHash));
		entry.HasHome = c.HasHomePosition();
		entry.HomePos = (entry.HasHome ? c.GetHomePosition() : Vector3.zero);
		entry.CombatStance = zdo.GetInt(CompanionSetup.CombatStanceHash);
		entry.HasBed = zdo.GetBool(CompanionSetup.HasHomeBedHash);
		entry.BedPos = zdo.GetVec3(CompanionSetup.HomeBedPosHash, Vector3.zero);
		entry.FarmZones = zdo.GetString(FarmZoneSerializer.FarmZonesHash);
		Inventory inventory = c.GetComponent<Humanoid>()?.GetInventory();
		if (inventory != null)
		{
			ZPackage zPackage = new ZPackage();
			inventory.Save(zPackage);
			entry.Inventory = zPackage.GetBase64();
		}
		entry.LastPos = c.transform.position;
		entry.LastSeen = ZNet.instance.GetTimeSeconds();
		entry.Dead = false;
	}

	internal static void OnDied(CompanionSetup c)
	{
		if (EnsureLoaded() && IsMine(c, out var _, out var zdo))
		{
			Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
			if (entry != null)
			{
				entry.Dead = true;
				entry.LastPos = c.transform.position;
				entry.LastSeen = ZNet.instance.GetTimeSeconds();
				Save(toDisk: false);
			}
		}
	}

	internal static void Forget(CompanionSetup c)
	{
		if (c._mhrStale || !EnsureLoaded() || !IsMine(c, out var _, out var zdo))
		{
			return;
		}
		Entry entry = Find(zdo.GetString(CompanionSetup.MHR_CompanionIdHash));
		if (entry == null)
		{
			return;
		}
		ConfigEntry<bool> recoverUnsummoned = ModConfig.RecoverUnsummoned;
		if (recoverUnsummoned == null || recoverUnsummoned.Value)
		{
			entry.Unsummoned = true;
			entry.Inventory = "";
			entry.Gen++;
			s_sessionUnsummoned.Add(entry.Id);
			Save(toDisk: true);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Roster] \"" + entry.Name + "\" was unsummoned - Odin's Shrine can bring it back (without gear) until you next load this world");
			}
		}
		else
		{
			s_entries.Remove(entry);
			Save(toDisk: true);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Roster] \"" + entry.Name + "\" was despawned - removed from your companion roster");
			}
		}
	}

	private static bool IsLoaded(string id)
	{
		foreach (CompanionSetup allCompanion in CompanionSetup.AllCompanions)
		{
			if (allCompanion._mhrStale)
			{
				continue;
			}
			ZDO zDO = allCompanion.GetComponent<ZNetView>()?.GetZDO();
			if (zDO != null && zDO.GetString(CompanionSetup.MHR_CompanionIdHash) == id)
			{
				Character component = allCompanion.GetComponent<Character>();
				s_where[id] = allCompanion.transform.position;
				if (component != null && component.GetHealth() > 0f)
				{
					return true;
				}
			}
		}
		return false;
	}

	internal static string WhereText(Vector3 pos)
	{
		string text = "";
		try
		{
			if (WorldGenerator.instance != null)
			{
				Heightmap.Biome biome = WorldGenerator.instance.GetBiome(pos);
				text = Localization.instance?.Localize("$biome_" + biome.ToString().ToLowerInvariant()) ?? biome.ToString();
			}
		}
		catch
		{
		}
		float num = ((Player.m_localPlayer != null) ? Vector3.Distance(Player.m_localPlayer.transform.position, pos) : 0f);
		return ModLocalization.LocFmt("hc_where", text, num.ToString("F0"));
	}

	internal static Dictionary<string, Status> GetStatuses()
	{
		Dictionary<string, Status> dictionary = new Dictionary<string, Status>();
		s_where.Clear();
		foreach (Entry s_entry in s_entries)
		{
			dictionary[s_entry.Id] = (s_entry.Unsummoned ? Status.Unsummoned : ((!IsLoaded(s_entry.Id)) ? Status.Unknown : Status.Alive));
		}
		if (!ZNet.instance.IsServer() || ZDOMan.instance == null || !(s_objectsById?.GetValue(ZDOMan.instance) is IDictionary dictionary2))
		{
			return dictionary;
		}
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> hashSet2 = new HashSet<string>();
		foreach (object value in dictionary2.Values)
		{
			if (!(value is ZDO zDO))
			{
				continue;
			}
			string text = zDO.GetString(CompanionSetup.MHR_CompanionIdHash);
			if (text.Length > 0 && dictionary.ContainsKey(text))
			{
				if (zDO.GetInt(GenHash) >= Find(text).Gen)
				{
					hashSet.Add(text);
					s_where[text] = zDO.GetPosition();
				}
			}
			else
			{
				text = zDO.GetString(CompanionSetup.MHR_TombCompanionIdHash);
				if (text.Length > 0 && dictionary.ContainsKey(text) && zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash).Length > 0 && zDO.GetInt(RespawnGenHash) >= Find(text).Gen)
				{
					hashSet2.Add(text);
				}
			}
		}
		foreach (Entry s_entry2 in s_entries)
		{
			if (dictionary[s_entry2.Id] != Status.Alive && dictionary[s_entry2.Id] != Status.Unsummoned)
			{
				dictionary[s_entry2.Id] = ((!hashSet.Contains(s_entry2.Id)) ? (hashSet2.Contains(s_entry2.Id) ? Status.Dead : Status.Lost) : Status.Alive);
			}
		}
		return dictionary;
	}

	internal static bool CompanionGone(string id)
	{
		if (string.IsNullOrEmpty(id) || ZNet.instance == null || !ZNet.instance.IsServer() || ZDOMan.instance == null)
		{
			return false;
		}
		if (s_goneCache.TryGetValue(id, out var value) && Time.unscaledTime - value.Value < 120f)
		{
			return value.Key;
		}
		bool flag = true;
		if (s_objectsById?.GetValue(ZDOMan.instance) is IDictionary dictionary)
		{
			foreach (object value2 in dictionary.Values)
			{
				if (value2 is ZDO zDO && (zDO.GetString(CompanionSetup.MHR_CompanionIdHash) == id || (zDO.GetString(CompanionSetup.MHR_TombCompanionIdHash) == id && zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash).Length > 0)))
				{
					flag = false;
					break;
				}
			}
		}
		else
		{
			flag = false;
		}
		s_goneCache[id] = new KeyValuePair<bool, float>(flag, Time.unscaledTime);
		if (flag && s_goneLogged.Add(id) && CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Rest] A bed is claimed by a companion that no longer exists (ID " + id + ") - the claim is ignored");
		}
		return flag;
	}

	internal static string Describe(Entry e, Status s)
	{
		string text = $"{e.Name}: {s}";
		switch (s)
		{
		case Status.Alive:
		{
			if (s_where.TryGetValue(e.Id, out var value))
			{
				text = text + " (" + WhereText(value) + ")";
			}
			break;
		}
		case Status.Dead:
			text += " (its tombstone will bring it back)";
			break;
		case Status.Lost:
			text += " (nothing left of it in the world - it can be recovered)";
			break;
		case Status.Unknown:
			text = text + (e.Dead ? " (last known: died" : " (last seen") + $" near {e.LastPos.x:F0}, {e.LastPos.z:F0})";
			break;
		case Status.Unsummoned:
			text += " (unsummoned - Odin's Shrine can bring it back without its gear until you next load this world)";
			break;
		}
		return text;
	}

	private static void ShowRoster()
	{
		if (s_entries.Count == 0)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, "No companions on your roster in this world yet.");
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Roster] No companions on the roster for this character in this world yet");
			}
			return;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		StringBuilder stringBuilder = new StringBuilder();
		foreach (Entry s_entry in s_entries)
		{
			string text = Describe(s_entry, statuses[s_entry.Id]);
			stringBuilder.Append(text).Append('\n');
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Roster] " + text);
			}
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, stringBuilder.ToString().TrimEnd(new char[1] { '\n' }));
	}

	private static void RecoverNext()
	{
		if (s_entries.Count == 0)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, "No companions on your roster in this world yet.");
			return;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		Entry entry = null;
		foreach (Entry s_entry in s_entries)
		{
			if (statuses[s_entry.Id] == Status.Lost)
			{
				entry = s_entry;
				break;
			}
		}
		if (entry == null)
		{
			foreach (Entry s_entry2 in s_entries)
			{
				if (statuses[s_entry2.Id] == Status.Unknown)
				{
					if (s_confirmId != s_entry2.Id || Time.unscaledTime > s_confirmUntil)
					{
						s_confirmId = s_entry2.Id;
						s_confirmUntil = Time.unscaledTime + 60f;
						string text = $"{s_entry2.Name}'s status is Unknown - it may still be alive somewhere that isn't loaded. Turn RecoverLostCompanion on again within {60f:F0} s to recover it anyway. If the old {s_entry2.Name} turns up later, it will be removed.";
						MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text);
						if (CompanionsPlugin.LogNormal)
						{
							CompanionsPlugin.Log.LogInfo("[Roster] " + text);
						}
						return;
					}
					entry = s_entry2;
					break;
				}
			}
		}
		s_confirmId = null;
		if (entry == null)
		{
			string text2 = "No lost companions to recover.";
			foreach (Entry s_entry3 in s_entries)
			{
				text2 = text2 + "\n" + Describe(s_entry3, statuses[s_entry3.Id]);
			}
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text2);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Roster] Recover: nothing to recover");
			}
		}
		else
		{
			Recover(entry, statuses[entry.Id]);
		}
	}

	internal static List<KeyValuePair<Entry, Status>> GetRoster()
	{
		List<KeyValuePair<Entry, Status>> list = new List<KeyValuePair<Entry, Status>>();
		if (ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded())
		{
			return list;
		}
		Dictionary<string, Status> statuses = GetStatuses();
		foreach (Entry s_entry in s_entries)
		{
			list.Add(new KeyValuePair<Entry, Status>(s_entry, statuses[s_entry.Id]));
		}
		return list;
	}

	internal static bool RecoverAt(Entry e, Vector3 near)
	{
		if (e == null || ZNet.instance == null || ZNetScene.instance == null || !EnsureLoaded() || !s_entries.Contains(e))
		{
			return false;
		}
		Status status = GetStatuses()[e.Id];
		if (status != Status.Lost && status != Status.Unknown && status != Status.Unsummoned)
		{
			MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, Describe(e, status));
			return false;
		}
		Recover(e, status, near);
		return true;
	}

	private static void Recover(Entry e, Status status, Vector3? near = null)
	{
		Player localPlayer = Player.m_localPlayer;
		GameObject prefab = ZNetScene.instance.GetPrefab(e.Prefab);
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogError("[Roster] Recover: prefab not found: " + e.Prefab);
			return;
		}
		Vector3 vector = CompanionManager.FindSpawnPosition(near ?? localPlayer.transform.position, 3f);
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, vector, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
		ZDO zDO = gameObject.GetComponent<ZNetView>()?.GetZDO();
		if (zDO == null)
		{
			CompanionsPlugin.Log.LogError("[Roster] Recover: spawn failed - no ZDO");
			UnityEngine.Object.Destroy(gameObject);
			return;
		}
		int num = e.Gen + 1;
		int num2 = 0;
		zDO.Set(CompanionSetup.AppearanceHash, e.Appearance);
		zDO.Set(CompanionSetup.OwnerHash, e.Owner);
		zDO.Set(CompanionSetup.NameHash, e.Name);
		zDO.Set(CompanionSetup.MHR_CompanionIdHash, e.Id);
		zDO.Set(GenHash, num);
		zDO.Set(CompanionSetup.FollowHash, e.Follow);
		zDO.Set(CompanionTombstoneResurrect.FollowPausedByJobHash, e.FollowPaused);
		zDO.Set(CompanionSetup.ActionModeHash, e.ActionMode);
		zDO.Set(CompanionSetup.ActionModeSchemaHash, e.Schema);
		zDO.Set(CompanionSetup.StayHomeHash, e.StayHome);
		zDO.Set(CompanionSetup.AutoPickupHash, e.AutoPickup);
		zDO.Set(CompanionSetup.WanderHash, e.Wander);
		zDO.Set(CompanionSetup.IsCommandableHash, e.Commandable);
		zDO.Set(CompanionSetup.FormationSlotHash, e.Formation);
		zDO.Set(CompanionSetup.CombatStanceHash, e.CombatStance);
		if (e.HasHome)
		{
			zDO.Set(CompanionSetup.HomePosHash, e.HomePos);
			zDO.Set(CompanionSetup.HomePosSetHash, value: true);
		}
		if (!string.IsNullOrEmpty(e.Skills))
		{
			zDO.Set(CompanionSkills.SkillsHash, e.Skills);
		}
		zDO.Set(CompanionSetup.HasHomeBedHash, e.HasBed);
		zDO.Set(CompanionSetup.HomeBedPosHash, e.BedPos);
		if (!string.IsNullOrEmpty(e.FarmZones))
		{
			zDO.Set(FarmZoneSerializer.FarmZonesHash, e.FarmZones);
		}
		zDO.Persistent = true;
		if (!string.IsNullOrEmpty(e.Inventory))
		{
			Inventory inventory = gameObject.GetComponent<Humanoid>()?.GetInventory();
			if (inventory != null)
			{
				inventory.Load(new ZPackage(e.Inventory));
				num2 = inventory.NrOfItems();
				foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
				{
					allItem.m_equipped = false;
				}
				inventory.m_onChanged?.Invoke();
			}
		}
		gameObject.GetComponent<CompanionSetup>()?.ApplyAppearance(CompanionAppearance.Deserialize(e.Appearance));
		Character component = gameObject.GetComponent<Character>();
		if (component != null)
		{
			component.m_name = e.Name;
		}
		CompanionAI component2 = gameObject.GetComponent<CompanionAI>();
		if (component2 != null)
		{
			if (e.HasHome)
			{
				component2.SetPatrolPointAt(e.HomePos);
			}
			if (e.StayHome && e.HasHome)
			{
				component2.SetFollowTarget(null);
			}
			else if (e.Follow)
			{
				component2.SetFollowTarget(localPlayer.gameObject);
			}
		}
		e.Gen = num;
		e.Dead = false;
		e.Unsummoned = false;
		s_sessionUnsummoned.Remove(e.Id);
		e.LastPos = vector;
		e.LastSeen = ZNet.instance.GetTimeSeconds();
		Save(toDisk: true);
		CompanionTombstoneResurrect.PlayResurrectEffects(vector);
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, e.Name + " has been recovered!");
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo($"[Roster] Recovered \"{e.Name}\" ({status}) at {vector:F1} as copy {num}, with {num2} item(s) from the last roster save");
		}
	}
}
