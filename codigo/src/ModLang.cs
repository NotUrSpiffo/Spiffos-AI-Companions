using System;

namespace OfflineCompanionsAI
{
    public enum ModLanguage
    {
        English,
        Spanish,
        Auto,
        Russian
    }

    /// <summary>
    /// Controls the active language of the mod (English, Spanish, Russian, or Auto based on Valheim's language).
    /// Defaults to Auto so it automatically adapts to English, Español (Latinoamérica), Español (España) or Русский.
    /// </summary>
    public static class ModLang
    {
        public static ModLanguage Current = ModLanguage.Auto;
        public static bool GameLanguageIsSpanish;
        public static bool GameLanguageIsRussian;

        public static void UpdateGameLanguage(string gameLang)
        {
            if (string.IsNullOrEmpty(gameLang)) return;
            GameLanguageIsSpanish = gameLang.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase)
                || gameLang.IndexOf("español", StringComparison.OrdinalIgnoreCase) >= 0
                || gameLang.IndexOf("espanol", StringComparison.OrdinalIgnoreCase) >= 0
                || gameLang.IndexOf("latam", StringComparison.OrdinalIgnoreCase) >= 0;
            GameLanguageIsRussian = !GameLanguageIsSpanish
                && (gameLang.StartsWith("Russian", StringComparison.OrdinalIgnoreCase)
                    || gameLang.IndexOf("русский", StringComparison.OrdinalIgnoreCase) >= 0
                    || string.Equals(gameLang.Trim(), "ru", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>El idioma que se usa de verdad: el elegido, o con Auto el del juego (inglés si no es español ni ruso).</summary>
        public static ModLanguage Effective
        {
            get
            {
                if (Current != ModLanguage.Auto) return Current;
                if (GameLanguageIsRussian) return ModLanguage.Russian;
                return GameLanguageIsSpanish ? ModLanguage.Spanish : ModLanguage.English;
            }
        }

        public static bool IsEnglish => Effective == ModLanguage.English;
        public static bool IsSpanish => Effective == ModLanguage.Spanish;
        public static bool IsRussian => Effective == ModLanguage.Russian;

        /// <summary>Texto en el idioma activo con fallback a inglés si no se aporta ruso.</summary>
        public static string Pick(string es, string en) => Pick(es, en, en);

        /// <summary>Texto en el idioma activo: español, inglés o ruso.</summary>
        public static string Pick(string es, string en, string ru)
        {
            switch (Effective)
            {
                case ModLanguage.Spanish: return es;
                case ModLanguage.Russian: return ru ?? en;
                default: return en;
            }
        }

        /// <summary>Nombre del idioma activo para mostrarlo: "English", "Español" o "Русский".</summary>
        public static string DisplayName(ModLanguage language)
        {
            switch (language)
            {
                case ModLanguage.Spanish: return "Español";
                case ModLanguage.Russian: return "Русский";
                case ModLanguage.English: return "English";
                default: return Pick("Automático", "Auto (Game)", "Авто (как в игре)");
            }
        }

        /// <summary>Lo que escribe el jugador en /language: en, es, ru, auto, en cualquiera de los tres idiomas. Null si no lo reconoce.</summary>
        public static ModLanguage? Parse(string text)
        {
            string a = (text ?? "").Trim().ToLowerInvariant();
            switch (a)
            {
                case "en": case "eng": case "english": case "ingles": case "inglés": case "английский": case "англ":
                    return ModLanguage.English;
                case "es": case "esp": case "spanish": case "espanol": case "español": case "испанский": case "исп":
                    return ModLanguage.Spanish;
                case "ru": case "rus": case "russian": case "ruso": case "русский": case "рус": case "ру":
                    return ModLanguage.Russian;
                case "auto": case "game": case "juego": case "авто": case "игра": case "автоматически":
                    return ModLanguage.Auto;
                default:
                    return null;
            }
        }

        private static string DefaultReplyLanguage => Pick("español", "English", "русский");

        public static string EffectiveReplyLanguage(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured)) return DefaultReplyLanguage;
            string trimmed = configured.Trim();
            if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "inglés", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "ingles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "español", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "espanol", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "spanish", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "russian", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "ruso", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "русский", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultReplyLanguage;
            }
            return trimmed;
        }
    }
}
