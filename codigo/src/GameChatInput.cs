using System;
using System.Reflection;
using HarmonyLib;

namespace OfflineCompanionsAI
{
    /// <summary>Permite hablar con los compañeros desde el chat normal del juego.</summary>
    public static class GameChatInput
    {
        /// <summary>Recibe lo escrito. Devuelve true si el juego no debe procesarlo.</summary>
        public static Func<string, bool> Handler;

        public static void Apply(Harmony harmony)
        {
            MethodInfo input = AccessTools.Method(typeof(Chat), "InputText");
            if (input == null)
            {
                Plugin.Log.LogWarning("Cannot talk to companions from in-game chat: missing Chat.InputText.");
                return;
            }
            harmony.Patch(input, prefix: new HarmonyMethod(typeof(GameChatInput), nameof(BeforeInput)));
        }

        private static bool BeforeInput(Chat __instance)
        {
            try
            {
                return Handler == null || !Handler(__instance.m_input.text);
            }
            catch (Exception ex)
            {
                // Pase lo que pase, el jugador debe poder seguir usando el chat.
                Plugin.Log.LogError("[Chat] " + ex);
                return true;
            }
        }
    }
}
