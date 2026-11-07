using System;

namespace OfflineCompanionsAI
{
    public enum ModLanguage
    {
        English,
        Spanish,
        Auto
    }

    /// <summary>
    /// Controls the active language of the mod (English, Spanish, or Auto based on Valheim's language).
    /// Defaults to Auto so it automatically adapts to English, Español (Latinoamérica), or Español (España).
    /// </summary>
    public static class ModLang
    {
        public static ModLanguage Current = ModLanguage.Auto;
        public static bool GameLanguageIsSpanish;

        public static void UpdateGameLanguage(string gameLang)
        {
            if (string.IsNullOrEmpty(gameLang)) return;
            GameLanguageIsSpanish = gameLang.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase)
                || gameLang.IndexOf("español", StringComparison.OrdinalIgnoreCase) >= 0
                || gameLang.IndexOf("espanol", StringComparison.OrdinalIgnoreCase) >= 0
                || gameLang.IndexOf("latam", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsEnglish => Current == ModLanguage.English || (Current == ModLanguage.Auto && !GameLanguageIsSpanish);
        public static bool IsSpanish => !IsEnglish;

        public static string Pick(string es, string en) => IsEnglish ? en : es;

        public static string EffectiveReplyLanguage(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured)) return IsEnglish ? "English" : "español";
            string trimmed = configured.Trim();
            if (string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "english", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "inglés", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "ingles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "español", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "espanol", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "spanish", StringComparison.OrdinalIgnoreCase))
            {
                return IsEnglish ? "English" : "español";
            }
            return trimmed;
        }
    }
}
