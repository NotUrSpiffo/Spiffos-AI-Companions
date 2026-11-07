using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Mantiene el texto de los compañeros por encima de su nombre y sus barras. Se calcula en pantalla y cada fotograma,
    /// porque depende de lo largo que sea el texto y de la distancia a la cámara.
    /// </summary>
    public static class SpeechBubble
    {
        private const float Gap = 10f;
        private const float MaxLabelHeight = 100f;
        private const int MaxDiagnostics = 3;

        private static readonly List<RectTransform> Rects = new List<RectTransform>();
        private static readonly Vector3[] Corners = new Vector3[4];

        private static AccessTools.FieldRef<Chat, List<Chat.NpcText>> _texts;
        private static FieldInfo _huds;
        private static FieldInfo _hudGui;
        private static bool _failed;
        private static int _diagnostics;

        public static void Apply(Harmony harmony)
        {
            MethodInfo update = AccessTools.Method(typeof(Chat), "UpdateNpcTexts");
            FieldInfo texts = AccessTools.Field(typeof(Chat), "m_npcTexts");
            _huds = AccessTools.Field(typeof(EnemyHud), "m_huds");
            Type hudData = AccessTools.Inner(typeof(EnemyHud), "HudData");
            _hudGui = hudData == null ? null : AccessTools.Field(hudData, "m_gui");
            if (update == null || texts == null || _huds == null || _hudGui == null)
            {
                Plugin.Log.LogWarning("Companion speech bubbles may overlap their name: this game version is missing the required fields.");
                return;
            }
            _texts = AccessTools.FieldRefAccess<Chat, List<Chat.NpcText>>(texts);
            harmony.Patch(update, postfix: new HarmonyMethod(typeof(SpeechBubble), nameof(AfterUpdate)));
        }

        private static void AfterUpdate(Chat __instance)
        {
            if (_failed) return;
            try
            {
                List<Chat.NpcText> texts = _texts(__instance);
                if (texts.Count == 0) return;
                Camera camera = Utils.GetMainCamera();
                if (camera == null) return;
                foreach (Chat.NpcText npc in texts) Place(npc, camera);
            }
            catch (Exception ex)
            {
                // Se ejecuta cada fotograma dentro del chat del juego: un fallo no debe repetirse ni afectarle.
                _failed = true;
                Plugin.Log.LogError("[Bubble] Stopped adjusting speech bubble position: " + ex);
            }
        }

        private static void Place(Chat.NpcText npc, Camera camera)
        {
            if (npc.m_go == null || npc.m_gui == null || npc.m_textField == null || !npc.IsVisible()) return;
            if (!CompanionBridge.IsCompanion(npc.m_go)) return;
            Character character = npc.m_go.GetComponent<Character>();
            if (character == null) return;

            TextMeshProUGUI text = npc.m_textField;
            if (text.havePropertiesChanged) text.ForceMeshUpdate();
            if (text.textInfo == null || text.textInfo.characterCount == 0) return;
            Bounds bounds = text.textBounds;
            if (bounds.size.y <= 0f) return;

            Vector3 anchor = Utils.WorldToScreenPointScaled(camera, character.GetTopPoint());
            if (anchor.z < 0f) return;

            float scale = text.canvas != null ? text.canvas.scaleFactor : 1f;
            float bottom = text.transform.TransformPoint(bounds.min).y;
            float top = text.transform.TransformPoint(bounds.max).y;
            float label = LabelHeight(character, scale);
            float lift = BubbleLayout.Lift(bottom, top, anchor.y + label, Gap * scale, Screen.height);
            if (lift <= 0f) return;

            npc.m_gui.transform.position += Vector3.up * lift;
            if (_diagnostics < MaxDiagnostics && bounds.size.y > 0f)
            {
                _diagnostics++;
                Plugin.Log.LogInfo("[Bubble] Text height " + Mathf.RoundToInt(top - bottom) + ", name & bars " + Mathf.RoundToInt(label) + ", lift " + Mathf.RoundToInt(lift) + ", screen " + Screen.height + ".");
            }
        }

        // Cuánto ocupa lo que el juego dibuja sobre ese punto. Puede ser menos si la etiqueta aún no se muestra.
        private static float LabelHeight(Character character, float scale)
        {
            if (EnemyHud.instance == null || !(_huds.GetValue(EnemyHud.instance) is IDictionary huds) || !huds.Contains(character)) return 0f;
            GameObject gui = _hudGui.GetValue(huds[character]) as GameObject;
            if (gui == null || !gui.activeInHierarchy) return 0f;

            float origin = gui.transform.position.y;
            float highest = origin;
            gui.GetComponentsInChildren(false, Rects);
            foreach (RectTransform rect in Rects)
            {
                if (rect.gameObject == gui) continue;
                rect.GetWorldCorners(Corners);
                highest = Mathf.Max(highest, Corners[1].y);
            }
            return Mathf.Min(highest - origin, MaxLabelHeight * scale);
        }
    }
}
