using HarmonyLib;

namespace Companions;

public static class SkillPatches
{
	[HarmonyPatch(typeof(Character), "RaiseSkill")]
	private static class RaiseSkill_Patch
	{
		private static bool Prefix(Character __instance, Skills.SkillType skill, float value)
		{
			CompanionSkills component = __instance.GetComponent<CompanionSkills>();
			if (component == null)
			{
				return true;
			}
			component.RaiseSkill(skill, value);
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetSkillFactor")]
	private static class GetSkillFactor_Patch
	{
		private static bool Prefix(Character __instance, Skills.SkillType skill, ref float __result)
		{
			CompanionSkills component = __instance.GetComponent<CompanionSkills>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetSkillFactor(skill);
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetRandomSkillFactor")]
	private static class GetRandomSkillFactor_Patch
	{
		private static bool Prefix(Character __instance, Skills.SkillType skill, ref float __result)
		{
			CompanionSkills component = __instance.GetComponent<CompanionSkills>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetRandomSkillFactor(skill);
			return false;
		}
	}
}
