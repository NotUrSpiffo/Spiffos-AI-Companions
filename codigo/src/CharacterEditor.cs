using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>Ventana dentro del juego para editar la ficha de un compañero.</summary>
    public sealed class CharacterEditor : MonoBehaviour
    {
        private const float DesignHeight = 1080f;

        public static bool IsOpen { get; private set; }

        /// <summary>Callback opcional para cambiar el idioma del mod desde el editor y guardarlo en la configuración.</summary>
        public static Action<ModLanguage> OnLanguageChanged;

        /// <summary>Evita que la misma pulsación de Esc que cierra el editor abra el menú del juego.</summary>
        internal static int BlockMenuUntilFrame { get; private set; }

        private static Component _editing;
        private static float _healthAtOpen;

        private string _companionName;
        private CharacterProfile _original;
        private CharacterProfile _edit;
        public delegate void SaveHandler(CharacterProfile profile, bool profileChanged, MemoryTexts memories, bool memoriesChanged, bool forgetAll);

        private MemoryTexts _originalMemories;
        private MemoryTexts _memories;
        private string _emptyHint;
        private Func<string> _emptyHintProvider;
        private bool _forget;
        private SaveHandler _onSave;
        private Rect _window;
        private Vector2 _scroll;
        private GUIStyle _label;
        private GUIStyle _area;
        private GUIStyle _button;

        /// <summary>El compañero espera mientras se edita, salvo que esté en peligro.</summary>
        internal static bool ShouldHold(Component companion)
        {
            if (!IsOpen || _editing == null || companion == null || companion.gameObject != _editing.gameObject) return false;
            Character character = _editing.GetComponent<Character>();
            return character != null && !character.IsSwimming() && character.GetHealth() >= _healthAtOpen;
        }

        public void Open(Component companion, string companionName, CharacterProfile profile, MemoryTexts memories, string emptyHint, SaveHandler onSave, Func<string> emptyHintProvider = null)
        {
            _editing = companion;
            _companionName = companionName;
            _healthAtOpen = companion.GetComponent<Character>()?.GetHealth() ?? 0f;
            _original = profile;
            _edit = profile.Clone();
            _originalMemories = memories;
            _memories = new MemoryTexts { Facts = memories.Facts, Events = memories.Events, Summary = memories.Summary };
            _emptyHint = emptyHint;
            _emptyHintProvider = emptyHintProvider;
            _forget = false;
            _onSave = onSave;
            _scroll = Vector2.zero;
            _window = default;
            IsOpen = true;
        }

        private void Close()
        {
            IsOpen = false;
            BlockMenuUntilFrame = Time.frameCount + 1;
            _onSave = null;
            _editing = null;
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            KeepCursorFree();
        }

        private void LateUpdate()
        {
            if (IsOpen) KeepCursorFree();
        }

        private static void KeepCursorFree()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            if (Player.m_localPlayer == null)
            {
                Close();
                return;
            }

            EnsureStyles();
            float scale = Mathf.Max(0.5f, Screen.height / DesignHeight);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (_window.width <= 0f)
            {
                float w = Mathf.Min(980f, width - 40f);
                float h = Mathf.Min(900f, height - 40f);
                _window = new Rect((width - w) / 2f, (height - h) / 2f, w, h);
            }
            string title = ModLang.Pick("Personaje de " + _companionName, _companionName + "'s Character");
            _window = GUI.ModalWindow(GetInstanceID(), _window, DrawWindow, title);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            _area = new GUIStyle(GUI.skin.textArea) { fontSize = 18, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawLanguageSelector();
            string emptyHint = _emptyHintProvider != null ? _emptyHintProvider() : _emptyHint;
            _edit.Instructions = Field(
                ModLang.Pick("Instrucciones", "Instructions"),
                ModLang.Pick("Quién es y cómo se comporta. ", "Who they are and how they behave. ") + emptyHint,
                _edit.Instructions,
                170f);
            DrawPresets();
            DrawInclination();
            _edit.Lore = Field(
                ModLang.Pick("Historia", "Backstory"),
                ModLang.Pick("Su pasado, su familia, sus motivos...", "Their past, family, motives..."),
                _edit.Lore,
                200f);
            _edit.Rules = Field(
                ModLang.Pick("Reglas", "Rules"),
                ModLang.Pick("Lo que siempre debe cumplir. Ejemplo: \"Llámame capitán\".", "What they must always follow. Example: \"Call me captain\"."),
                _edit.Rules,
                130f);
            _memories.Facts = Field(
                ModLang.Pick("Hechos firmes", "Core Facts"),
                ModLang.Pick("Lo que nunca debe olvidar, uno por línea. Los resúmenes no los tocan.", "What they must never forget, one per line. Summaries do not alter these."),
                _memories.Facts,
                110f);
            _memories.Events = Field(
                ModLang.Pick("Vivencias", "Experiences"),
                ModLang.Pick("Lo que ha ocurrido de verdad en la partida, uno por línea. Se anota solo.", "What actually happened in the game, one per line. Recorded automatically."),
                _memories.Events,
                110f);
            _memories.Summary = Field(
                ModLang.Pick("Otros recuerdos", "Other Memories"),
                ModLang.Pick("El resumen de vuestras conversaciones. Se reescribe solo, pero puedes corregirlo.", "Summary of your conversations. Rewritten automatically, but you can edit it."),
                _memories.Summary,
                130f);
            _forget = GUILayout.Toggle(_forget, ModLang.Pick(
                " Al guardar, borrar todos sus recuerdos y conversaciones",
                " When saving, erase all memories and conversations"));
            GUILayout.Space(10f);
            GUILayout.EndScrollView();

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLang.Pick(
                "Si cambias la ficha, olvida la conversación reciente. Esc cancela.",
                "If you change the character sheet, recent conversation is forgotten. Esc cancels."), GUI.skin.label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(ModLang.Pick("Cancelar", "Cancel"), _button, GUILayout.Width(150f), GUILayout.Height(44f)))
            {
                Close();
                return;
            }
            if (GUILayout.Button(ModLang.Pick("Guardar", "Save"), _button, GUILayout.Width(150f), GUILayout.Height(44f)))
            {
                SaveHandler save = _onSave;
                CharacterProfile result = _edit;
                MemoryTexts memories = _memories;
                bool forget = _forget;
                bool profileChanged = !result.SameContentAs(_original);
                bool memoriesChanged = !memories.SameAs(_originalMemories);
                Close();
                if (profileChanged || memoriesChanged || forget) save(result, profileChanged, memories, memoriesChanged, forget);
                return;
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        private void DrawLanguageSelector()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLang.Pick("Idioma del mod:", "Mod Language:"), _label, GUILayout.Width(190f));
            ModLanguage current = ModLang.Current;
            if (GUILayout.Toggle(current == ModLanguage.English, "English", _button, GUILayout.Width(140f)) && current != ModLanguage.English)
            {
                SetLanguage(ModLanguage.English);
            }
            if (GUILayout.Toggle(current == ModLanguage.Spanish, "Español", _button, GUILayout.Width(140f)) && current != ModLanguage.Spanish)
            {
                SetLanguage(ModLanguage.Spanish);
            }
            if (GUILayout.Toggle(current == ModLanguage.Auto, ModLang.Pick("Automático", "Auto (Game)"), _button, GUILayout.Width(160f)) && current != ModLanguage.Auto)
            {
                SetLanguage(ModLanguage.Auto);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
        }

        private static void SetLanguage(ModLanguage lang)
        {
            ModLang.Current = lang;
            Companions.ModLocalization.Refresh();
            OnLanguageChanged?.Invoke(lang);
        }

        /// <summary>Solo con las instrucciones vacías: un clic de más no debe borrar una ficha escrita a mano.</summary>
        private void DrawPresets()
        {
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && string.IsNullOrWhiteSpace(_edit.Instructions);
            GUILayout.BeginHorizontal();
            GUILayout.Label(ModLang.Pick("Perfiles:", "Presets:"), _label, GUILayout.Width(100f));
            foreach (PersonalityPreset preset in PersonalityPresets.All)
            {
                if (GUILayout.Button(preset.Name, _button)) _edit.Instructions = preset.Text;
            }
            GUILayout.EndHorizontal();
            GUI.enabled = wasEnabled;
            GUILayout.Space(10f);
        }

        private void DrawInclination()
        {
            GUILayout.Label(ModLang.Pick("Inclinación", "Inclination"), _label);
            GUILayout.Label(ModLang.Pick(
                "A qué tiende por su cuenta: nunca pasa por encima de tus órdenes. Automática usa la de su perfil; una ficha escrita a mano no tiene.",
                "What they gravitate toward on their own: never overrides your orders. Automatic uses their preset's inclination; a custom-written sheet has none."));
            GUILayout.BeginHorizontal();
            string current = (_edit.Inclination ?? "").Trim();
            foreach (KeyValuePair<string, string> choice in Inclinations.Choices)
            {
                if (GUILayout.Toggle(current == choice.Key, choice.Value, _button)) _edit.Inclination = choice.Key;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10f);
        }

        private string Field(string title, string hint, string value, float height)
        {
            GUILayout.Label(title, _label);
            GUILayout.Label(hint + "  (" + (value ?? "").Length + "/" + CharacterProfile.MaxFieldLength + ")");
            string result = GUILayout.TextArea(value ?? "", CharacterProfile.MaxFieldLength, _area, GUILayout.Height(height));
            GUILayout.Space(10f);
            return result;
        }
    }

    /// <summary>Cubre al resto de sistemas: mientras se escribe, ningún botón del juego se activa.</summary>
    [HarmonyPatch]
    internal static class EditorButtonPatches
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButton), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetButtonUp), new[] { typeof(string) });
            yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetKey), new[] { typeof(KeyCode), typeof(bool) });
            yield return AccessTools.Method(typeof(ZInput), nameof(ZInput.GetKeyDown), new[] { typeof(KeyCode), typeof(bool) });
        }

        [HarmonyPostfix]
        private static void Postfix(ref bool __result)
        {
            if (CharacterEditor.IsOpen) __result = false;
        }
    }

    /// <summary>Mientras el editor está abierto, el teclado escribe en él y no mueve al personaje.</summary>
    [HarmonyPatch]
    internal static class EditorInputPatches
    {
        [HarmonyPatch(typeof(Player), "TakeInput")]
        [HarmonyPostfix]
        private static void PlayerTakeInput(ref bool __result)
        {
            if (CharacterEditor.IsOpen) __result = false;
        }

        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        [HarmonyPostfix]
        private static void ControllerTakeInput(ref bool __result)
        {
            if (CharacterEditor.IsOpen) __result = false;
        }

        // El movimiento, el mapa, el menú, el chat y el cursor consultan esto antes de leer el teclado.
        [HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
        [HarmonyPostfix]
        private static void TextInputVisible(ref bool __result)
        {
            if (CharacterEditor.IsOpen) __result = true;
        }

        /// <summary>Hace que el compañero espere con el mismo mecanismo que usa la rueda de órdenes.</summary>
        internal static void HoldCompanionPostfix(Component __0, ref bool __result)
        {
            if (CharacterEditor.ShouldHold(__0) || (ExtraHold != null && ExtraHold(__0))) __result = true;
        }

        /// <summary>Otras tareas del mod que necesitan pausar la IA del compañero, como golpear una planta.</summary>
        internal static Func<Component, bool> ExtraHold;

        [HarmonyPatch(typeof(Hud), "InRadial")]
        [HarmonyPostfix]
        private static void InRadial(ref bool __result)
        {
            if (CharacterEditor.IsOpen) __result = true;
        }

        [HarmonyPatch(typeof(Menu), "Show")]
        [HarmonyPrefix]
        private static bool MenuShow()
        {
            return !CharacterEditor.IsOpen && Time.frameCount > CharacterEditor.BlockMenuUntilFrame;
        }
    }
}
