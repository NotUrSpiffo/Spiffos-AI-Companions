using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal static class MHR_Shrine
{
	internal const string PrefabName = "HC_CompanionShrine";

	internal const string DisplayName = "Odin's Shrine";

	private static GameObject s_holder;

	private static GameObject s_prefab;

	private static string s_model;

	internal static void Init(ZNetScene zs)
	{
		try
		{
			string text = ModelPrefabName();
			if (s_prefab != null && s_model != text)
			{
				UnityEngine.Object.Destroy(s_prefab);
				s_prefab = null;
			}
			if (s_prefab == null && !Create(zs, text))
			{
				return;
			}
			if (zs.GetPrefab("HC_CompanionShrine") == null)
			{
				CompanionPrefabs.RegisterPrefab(s_prefab, zs);
			}
			Piece component = s_prefab.GetComponent<Piece>();
			component.m_resources = ParseRecipe(zs, ModConfig.ShrineRecipe.Value);
			component.m_craftingStation = zs.GetPrefab("piece_workbench")?.GetComponent<CraftingStation>();
			PieceTable pieceTable = zs.GetPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
			if (pieceTable == null)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] Hammer build table not found - Odin's Shrine can't be built");
				return;
			}
			pieceTable.m_pieces.RemoveAll((GameObject p) => p == null || p.name == "HC_CompanionShrine");
			if (ModConfig.ShrineBuildable.Value)
			{
				pieceTable.m_pieces.Add(s_prefab);
				if (CompanionsPlugin.LogNormal)
				{
					CompanionsPlugin.Log.LogInfo("[Shrine] Odin's Shrine added to the hammer (Misc), recipe " + ModConfig.ShrineRecipe.Value + " at a workbench");
				}
			}
		}
		catch (Exception ex)
		{
			CompanionsPlugin.Log.LogWarning("[Shrine] Setup failed: " + ex);
		}
	}

	private static string ModelPrefabName()
	{
		if (!(ModConfig.ShrineModel.Value ?? "").Trim().Equals("Ward", StringComparison.OrdinalIgnoreCase))
		{
			return "Player_tombstone";
		}
		return "guard_stone";
	}

	private static bool Create(ZNetScene zs, string model)
	{
		GameObject prefab = zs.GetPrefab(model);
		if (prefab == null)
		{
			CompanionsPlugin.Log.LogWarning("[Shrine] Model " + model + " not found - no Odin's Shrine");
			return false;
		}
		if (s_holder == null)
		{
			s_holder = new GameObject("HC_ShrinePrefab");
			UnityEngine.Object.DontDestroyOnLoad(s_holder);
			s_holder.SetActive(value: false);
		}
		GameObject gameObject = UnityEngine.Object.Instantiate(prefab, s_holder.transform, worldPositionStays: false);
		gameObject.name = "HC_CompanionShrine";
		float hoverOffset = 1f;
		Piece piece = gameObject.GetComponent<Piece>();
		TombStone component = gameObject.GetComponent<TombStone>();
		if (component != null)
		{
			hoverOffset = component.m_hoverOffset;
			if (component.m_worldText != null)
			{
				component.m_worldText.text = "Odin's Shrine";
			}
			if (component.m_floater != null && component.m_floater != gameObject)
			{
				component.m_floater.SetActive(value: false);
			}
			Remove<TombStone>(gameObject);
			Remove<Floating>(gameObject);
			Remove<Container>(gameObject);
			Remove<ZSyncTransform>(gameObject);
			Remove<Rigidbody>(gameObject);
		}
		PrivateArea component2 = gameObject.GetComponent<PrivateArea>();
		if (component2 != null)
		{
			hoverOffset = component2.m_hoverOffset;
			if (component2.m_areaMarker != null)
			{
				UnityEngine.Object.DestroyImmediate(component2.m_areaMarker.gameObject);
			}
			if (component2.m_inRangeEffect != null && component2.m_inRangeEffect.transform.IsChildOf(gameObject.transform))
			{
				component2.m_inRangeEffect.SetActive(value: false);
			}
			if (component2.m_enabledEffect != null && component2.m_enabledEffect.transform.IsChildOf(gameObject.transform))
			{
				component2.m_enabledEffect.SetActive(value: true);
			}
			UnityEngine.Object.DestroyImmediate(component2);
		}
		CircleProjector[] componentsInChildren = gameObject.GetComponentsInChildren<CircleProjector>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren[i]);
		}
		Piece piece2 = zs.GetPrefab("guard_stone")?.GetComponent<Piece>();
		if (piece == null)
		{
			piece = gameObject.AddComponent<Piece>();
			if (piece2 != null)
			{
				piece.m_placeEffect = piece2.m_placeEffect;
			}
			piece.m_icon = zs.GetPrefab("TrophySkeleton")?.GetComponent<ItemDrop>()?.m_itemData?.GetIcon() ?? piece2?.m_icon;
		}
		int num = LayerMask.NameToLayer("piece");
		Collider[] componentsInChildren2 = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		foreach (Collider collider in componentsInChildren2)
		{
			if (num >= 0 && !collider.isTrigger)
			{
				collider.gameObject.layer = num;
			}
		}
		piece.m_name = "Odin's Shrine";
		piece.m_description = "Shows your companions and whether they're alive, and brings back one that's been lost.";
		piece.m_category = Piece.PieceCategory.Misc;
		piece.m_enabled = true;
		gameObject.AddComponent<CompanionShrine>().m_hoverOffset = hoverOffset;
		s_prefab = gameObject;
		s_model = model;
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Shrine] Odin's Shrine piece created (model: " + model + ")");
		}
		return true;
	}

	private static void Remove<T>(GameObject go) where T : Component
	{
		T[] components = go.GetComponents<T>();
		foreach (T obj in components)
		{
			try
			{
				UnityEngine.Object.DestroyImmediate(obj);
			}
			catch (Exception ex)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] Couldn't remove " + typeof(T).Name + ": " + ex.Message);
			}
		}
	}

	private static Piece.Requirement[] ParseRecipe(ZNetScene zs, string recipe)
	{
		List<Piece.Requirement> list = new List<Piece.Requirement>();
		string[] array = (recipe ?? "").Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { ':' });
			string text = array2[0].Trim();
			int result = 1;
			if (array2.Length > 1 && !int.TryParse(array2[1].Trim(), out result))
			{
				result = 1;
			}
			ItemDrop itemDrop = zs.GetPrefab(text)?.GetComponent<ItemDrop>();
			if (itemDrop == null)
			{
				CompanionsPlugin.Log.LogWarning("[Shrine] ShrineRecipe: unknown item \"" + text + "\" - skipped");
			}
			else if (result > 0)
			{
				list.Add(new Piece.Requirement
				{
					m_resItem = itemDrop,
					m_amount = result,
					m_recover = true
				});
			}
		}
		return list.ToArray();
	}
}
