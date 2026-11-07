using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Companions;

internal static class MHR_CompanionNavMesh
{
	[HarmonyPatch(typeof(Pathfinding), "SetupAgents")]
	private static class SetupAgents_Patch
	{
		private static void Postfix(Pathfinding __instance)
		{
			Ready = false;
			try
			{
				if (ModConfig.CompanionNavMesh == null || ModConfig.CompanionNavMesh.Value)
				{
					object obj = s_getSettings.Invoke(__instance, new object[1] { Pathfinding.AgentType.Humanoid });
					object obj2 = s_addAgent.Invoke(__instance, new object[2]
					{
						(Pathfinding.AgentType)40,
						obj
					});
					s_build = AccessTools.Field(obj2.GetType(), "m_build");
					NavMeshBuildSettings navMeshBuildSettings = (NavMeshBuildSettings)s_build.GetValue(obj2);
					navMeshBuildSettings.agentClimb = 0.6f;
					s_build.SetValue(obj2, navMeshBuildSettings);
					Ready = true;
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log?.LogInfo("[Move] Companion route map ready (doors don't block, steps up to 0.6 m)");
					}
				}
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log?.LogWarning("[Move] Couldn't set up the companion route map, using the normal one: " + ex.Message);
			}
		}
	}

	[HarmonyPatch(typeof(Pathfinding), "BuildTile")]
	private static class BuildTile_Patch
	{
		private static bool Prefix(Pathfinding __instance, object tile)
		{
			if (!Ready || tile == null)
			{
				return true;
			}
			try
			{
				if (s_tileId == null)
				{
					Type type = tile.GetType();
					s_tileId = AccessTools.Field(type, "m_tile");
					s_center = AccessTools.Field(type, "m_center");
					s_data = AccessTools.Field(type, "m_data");
				}
				if (((Vector3Int)s_tileId.GetValue(tile)).z != 40)
				{
					return true;
				}
				Vector3 vector = (Vector3)s_center.GetValue(tile);
				float num = (float)s_tileSize.GetValue(__instance);
				int value = ((LayerMask)s_layers.GetValue(__instance)).value;
				int value2 = ((LayerMask)s_waterLayers.GetValue(__instance)).value;
				object obj = s_getSettings.Invoke(__instance, new object[1] { (Pathfinding.AgentType)40 });
				NavMeshBuildSettings buildSettings = (NavMeshBuildSettings)s_build.GetValue(obj);
				Bounds includedWorldBounds = new Bounds(vector, new Vector3(num, 6000f, num));
				Bounds localBounds = new Bounds(Vector3.zero, new Vector3(num, 6000f, num));
				List<NavMeshBuildSource> list = new List<NavMeshBuildSource>();
				List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
				NavMeshBuilder.CollectSources(includedWorldBounds, value, NavMeshCollectGeometry.PhysicsColliders, 0, markups, list);
				list.RemoveAll((NavMeshBuildSource s) => s.component != null && s.component.GetComponentInParent<Door>() != null);
				List<NavMeshBuildSource> list2 = new List<NavMeshBuildSource>();
				NavMeshBuilder.CollectSources(includedWorldBounds, value2, NavMeshCollectGeometry.PhysicsColliders, 3, markups, list2);
				list.AddRange(list2);
				NavMeshData navMeshData = (NavMeshData)s_data.GetValue(tile);
				if (navMeshData == null)
				{
					navMeshData = new NavMeshData();
					navMeshData.position = vector;
					s_data.SetValue(tile, navMeshData);
				}
				s_buildOperation.SetValue(__instance, NavMeshBuilder.UpdateNavMeshDataAsync(navMeshData, buildSettings, list, localBounds));
				s_buildTile.SetValue(__instance, tile);
				return false;
			}
			catch (Exception ex)
			{
				Ready = false;
				CompanionsPlugin.Log?.LogWarning("[Move] Companion route map failed, using the normal one: " + ex.Message);
				return true;
			}
		}
	}

	internal const Pathfinding.AgentType Agent = (Pathfinding.AgentType)40;

	internal static bool Ready;

	private static readonly MethodInfo s_addAgent = AccessTools.Method(typeof(Pathfinding), "AddAgent");

	private static readonly MethodInfo s_getSettings = AccessTools.Method(typeof(Pathfinding), "GetSettings");

	private static readonly FieldInfo s_layers = AccessTools.Field(typeof(Pathfinding), "m_layers");

	private static readonly FieldInfo s_waterLayers = AccessTools.Field(typeof(Pathfinding), "m_waterLayers");

	private static readonly FieldInfo s_tileSize = AccessTools.Field(typeof(Pathfinding), "m_tileSize");

	private static readonly FieldInfo s_buildOperation = AccessTools.Field(typeof(Pathfinding), "m_buildOperation");

	private static readonly FieldInfo s_buildTile = AccessTools.Field(typeof(Pathfinding), "m_buildTile");

	private static FieldInfo s_build;

	private static FieldInfo s_tileId;

	private static FieldInfo s_center;

	private static FieldInfo s_data;

	internal static Pathfinding.AgentType AgentOrHumanoid
	{
		get
		{
			if (!Ready)
			{
				return Pathfinding.AgentType.Humanoid;
			}
			return (Pathfinding.AgentType)40;
		}
	}
}
