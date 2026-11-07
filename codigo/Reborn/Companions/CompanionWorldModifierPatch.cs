using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Companions;

[HarmonyPatch]
internal static class CompanionWorldModifierPatch
{
	private static readonly FieldInfo s_speedSize = AccessTools.Field(typeof(Game), "m_enemySpeedSize");

	private static readonly FieldInfo s_worldLevel = AccessTools.Field(typeof(Game), "m_worldLevel");

	private static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(Character), "Awake");
		yield return AccessTools.Method(typeof(Character), "UpdateWalking");
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
	{
		int replaced = 0;
		foreach (CodeInstruction instruction in instructions)
		{
			if (instruction.opcode == OpCodes.Ldsfld && (object.Equals(instruction.operand, s_speedSize) || object.Equals(instruction.operand, s_worldLevel)))
			{
				bool size = object.Equals(instruction.operand, s_speedSize);
				CodeInstruction codeInstruction = new CodeInstruction(OpCodes.Ldarg_0);
				codeInstruction.labels.AddRange(instruction.labels);
				codeInstruction.blocks.AddRange(instruction.blocks);
				yield return codeInstruction;
				yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompanionWorldModifierPatch), size ? "SpeedSize" : "WorldLevel"));
				replaced++;
			}
			else
			{
				yield return instruction;
			}
		}
		if (replaced == 0)
		{
			CompanionsPlugin.Log?.LogWarning("[WorldModifiers] " + __originalMethod?.Name + ": the enemy size/speed values weren't found - companions may grow with the Combat world modifier");
		}
	}

	private static bool IsCompanion(Character c)
	{
		if (c != null)
		{
			return c.GetBaseAI() is CompanionAI;
		}
		return false;
	}

	internal static float SpeedSize(Character c)
	{
		if (!IsCompanion(c))
		{
			return Game.m_enemySpeedSize;
		}
		return 1f;
	}

	internal static int WorldLevel(Character c)
	{
		if (!IsCompanion(c))
		{
			return Game.m_worldLevel;
		}
		return 0;
	}
}
