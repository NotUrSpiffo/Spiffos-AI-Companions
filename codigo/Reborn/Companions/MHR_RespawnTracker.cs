using System.Collections.Generic;
using UnityEngine;

namespace Companions;

public static class MHR_RespawnTracker
{
	internal static readonly int RespawnAtHash = "HC_RespawnAt".GetStableHashCode();

	internal static readonly int CleanupHash = "HC_TombCleanup".GetStableHashCode();

	private static float s_nextTick;

	private static readonly List<string> s_deferred = new List<string>();

	internal static void Tick()
	{
		if (!(Time.unscaledTime >= s_nextTick))
		{
			return;
		}
		s_nextTick = Time.unscaledTime + 1f;
		CompanionTombstoneResurrect.MHR_SweepGravePins();
		CompanionManager.MHR_StarterFallbackTick();
		MHR_Diag.Tick();
		if (!ModConfig.AutoRespawnEnabled.Value)
		{
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || localPlayer.IsDead() || ZDOMan.instance == null || ZNetScene.instance == null)
		{
			return;
		}
		if (s_deferred.Count > 0)
		{
			string text = GetPending(localPlayer);
			foreach (string item in s_deferred)
			{
				if (!text.Contains(";" + item.Substring(0, item.LastIndexOf(',') + 1)))
				{
					text = text + ";" + item;
				}
			}
			SetPending(localPlayer, text);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Respawn] Tracking {s_deferred.Count} companion(s) that died while you were dead");
			}
			s_deferred.Clear();
		}
		string pending = GetPending(localPlayer);
		if (string.IsNullOrEmpty(pending))
		{
			return;
		}
		long num = NowSeconds();
		string[] array = pending.Split(new char[1] { ';' });
		foreach (string text2 in array)
		{
			if (string.IsNullOrEmpty(text2))
			{
				continue;
			}
			string[] array2 = text2.Split(new char[1] { ',' });
			if (array2.Length != 3 || !long.TryParse(array2[0], out var result) || !uint.TryParse(array2[1], out var result2) || !long.TryParse(array2[2], out var result3) || num < result3)
			{
				continue;
			}
			ZDOID zDOID = new ZDOID(result, result2);
			ZDO zDO = ZDOMan.instance.GetZDO(zDOID);
			if (zDO == null)
			{
				if (num > result3 + 10800)
				{
					RemovePending(zDOID);
				}
			}
			else if (string.IsNullOrEmpty(zDO.GetString(CompanionTombstoneResurrect.RespawnOwnerHash)))
			{
				RemovePending(zDOID);
			}
			else if (!(ZNetScene.instance.FindInstance(zDO) != null))
			{
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Respawn] Respawn time reached for a companion whose tombstone is far away; respawning it now");
				}
				CompanionTombstoneResurrect.MHR_ResurrectCore(zDO, null, isAutoRespawn: true);
				RemovePending(zDOID);
			}
		}
	}

	internal static void OnCompanionDied(ZDO tombZdo, string ownerId)
	{
		if (tombZdo == null)
		{
			return;
		}
		if (Player.m_localPlayer == null)
		{
			string text = tombZdo.GetString(CompanionTombstoneResurrect.RespawnOwnerHash);
			long valueOrDefault = (Game.instance?.GetPlayerProfile()?.GetPlayerID()).GetValueOrDefault();
			if (valueOrDefault != 0L && text == valueOrDefault.ToString())
			{
				s_deferred.Add(EntryPrefix(tombZdo.m_uid) + tombZdo.GetLong(RespawnAtHash, 0L));
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Respawn] Companion died while you were dead - it's added to the respawn list when you're back");
				}
			}
			return;
		}
		if (string.IsNullOrEmpty(tombZdo.GetString(CompanionTombstoneResurrect.RespawnOwnerHash)))
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Respawn] Tombstone with an unsummoned companion's gear - nothing to respawn");
			}
			return;
		}
		if (!IsLocalOwner(tombZdo))
		{
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Respawn] Companion died while another player's game was running it; its owner's game will respawn it when the owner is near the tombstone");
			}
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		string text2 = EntryPrefix(tombZdo.m_uid);
		string pending = GetPending(localPlayer);
		if (!pending.Contains(";" + text2))
		{
			long num = tombZdo.GetLong(RespawnAtHash, 0L);
			SetPending(localPlayer, pending + ";" + text2 + num);
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo($"[Respawn] Tracking dead companion (tombstone {text2}): respawns at world time {num}s, now {NowSeconds()}s");
			}
		}
	}

	internal static void RemovePending(ZDOID uid)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return;
		}
		string pending = GetPending(localPlayer);
		if (string.IsNullOrEmpty(pending))
		{
			return;
		}
		string text = EntryPrefix(uid);
		string[] array = pending.Split(new char[1] { ';' });
		string text2 = "";
		bool flag = false;
		string[] array2 = array;
		foreach (string text3 in array2)
		{
			if (!string.IsNullOrEmpty(text3))
			{
				if (text3.StartsWith(text))
				{
					flag = true;
				}
				else
				{
					text2 = text2 + ";" + text3;
				}
			}
		}
		if (flag)
		{
			SetPending(localPlayer, text2);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Respawn] Stopped tracking tombstone " + text);
			}
		}
	}

	internal static bool IsDueForLocalOwner(ZDO zdo)
	{
		if (IsLocalOwner(zdo) && !Player.m_localPlayer.IsDead())
		{
			long num = zdo.GetLong(RespawnAtHash, 0L);
			if (num != 0L)
			{
				return NowSeconds() >= num;
			}
			zdo.Set(RespawnAtHash, ComputeRespawnAt());
		}
		return false;
	}

	internal static long ComputeRespawnAt()
	{
		return NowSeconds() + (long)ModConfig.AutoRespawnDelaySeconds.Value;
	}

	internal static long NowSeconds()
	{
		if (!(ZNet.instance == null))
		{
			return (long)ZNet.instance.GetTimeSeconds();
		}
		return 0L;
	}

	internal static bool IsLocalOwner(ZDO zdo)
	{
		if (zdo != null)
		{
			Player localPlayer = Player.m_localPlayer;
			if (!(localPlayer == null))
			{
				long playerID = localPlayer.GetPlayerID();
				return zdo.GetString(CompanionTombstoneResurrect.RespawnOwnerHash) == playerID.ToString();
			}
		}
		return false;
	}

	private static string GetPending(Player p)
	{
		if (p.m_customData != null && p.m_customData.TryGetValue("HC_PendingRespawns", out var value) && value != null)
		{
			return value;
		}
		return "";
	}

	private static void SetPending(Player p, string val)
	{
		if (p.m_customData != null)
		{
			if (string.IsNullOrEmpty(val))
			{
				p.m_customData.Remove("HC_PendingRespawns");
			}
			else
			{
				p.m_customData["HC_PendingRespawns"] = val;
			}
		}
	}

	private static string EntryPrefix(ZDOID uid)
	{
		long userID = uid.UserID;
		uint iD = uid.ID;
		return string.Concat(userID + "," + iD, ",");
	}
}
