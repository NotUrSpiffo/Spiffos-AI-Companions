using UnityEngine;

namespace Companions;

internal static class MHR_Owner
{
	internal static string Id(Component c)
	{
		CompanionSetup companionSetup = ((c != null) ? c.GetComponent<CompanionSetup>() : null);
		ZNetView zNetView = ((companionSetup != null) ? companionSetup.GetComponent<ZNetView>() : null);
		return ((zNetView != null && zNetView.IsValid()) ? zNetView.GetZDO() : null)?.GetString(CompanionSetup.OwnerHash);
	}

	internal static Player Of(Component c)
	{
		Player localPlayer = Player.m_localPlayer;
		string text = Id(c);
		if (string.IsNullOrEmpty(text))
		{
			return localPlayer;
		}
		if (localPlayer != null && localPlayer.GetPlayerID().ToString() == text)
		{
			return localPlayer;
		}
		foreach (Player allPlayer in Player.GetAllPlayers())
		{
			if (allPlayer != null && allPlayer.GetPlayerID().ToString() == text)
			{
				return allPlayer;
			}
		}
		return null;
	}

	internal static GameObject Go(Component c)
	{
		Player player = Of(c);
		if (!(player != null))
		{
			return null;
		}
		return player.gameObject;
	}

	internal static bool IsMine(Component c)
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		string text = Id(c);
		if (!string.IsNullOrEmpty(text))
		{
			return text == localPlayer.GetPlayerID().ToString();
		}
		return true;
	}

	internal static long CreatorId(Component c)
	{
		string text = Id(c);
		if (!string.IsNullOrEmpty(text) && long.TryParse(text, out var result))
		{
			return result;
		}
		if (!(Player.m_localPlayer != null))
		{
			return 0L;
		}
		return Player.m_localPlayer.GetPlayerID();
	}

	internal static void TakeControl(Component c)
	{
		ZNetView zNetView = ((c != null) ? c.GetComponent<ZNetView>() : null);
		if (zNetView != null && zNetView.IsValid() && !zNetView.IsOwner())
		{
			zNetView.ClaimOwnership();
		}
	}
}
