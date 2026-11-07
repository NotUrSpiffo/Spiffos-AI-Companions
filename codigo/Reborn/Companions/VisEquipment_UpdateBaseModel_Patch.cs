using HarmonyLib;
using UnityEngine;

namespace Companions;

[HarmonyPatch(typeof(VisEquipment), "UpdateBaseModel")]
internal static class VisEquipment_UpdateBaseModel_Patch
{
	private static readonly int ModelIndexHash = "ModelIndex".GetStableHashCode();

	private static void Prefix(VisEquipment __instance)
	{
		if (!CompanionSetup.IsCompanionVisEquip(__instance) || __instance.m_models == null || __instance.m_models.Length == 0 || __instance.m_bodyModel == null)
		{
			return;
		}
		ZNetView component = __instance.GetComponent<ZNetView>();
		int num = 0;
		if (component?.GetZDO() != null)
		{
			num = component.GetZDO().GetInt(ModelIndexHash);
		}
		if (num < 0 || num >= __instance.m_models.Length)
		{
			return;
		}
		VisEquipment.PlayerModel playerModel = __instance.m_models[num];
		if (playerModel != null)
		{
			Mesh mesh = playerModel.m_mesh;
			if (!(mesh == null) && __instance.m_bodyModel.sharedMesh != null && __instance.m_bodyModel.sharedMesh != mesh)
			{
				__instance.m_bodyModel.sharedMesh = null;
			}
		}
	}
}
