using System;
using Newtonsoft.Json.Linq;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Reconocimiento de voz de la app local de Player2: ella escucha el micrófono entre stt/start y stt/stop.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public static class VoiceProtocol
    {
        public const float MinSeconds = 0.3f;
        public const int MinTimeout = 3;
        public const int MaxTimeout = 60;

        // La app descarta lo escuchado al agotarse su plazo, así que se termina un poco antes.
        public const float StopMargin = 0.5f;

        public static string StartBody(int seconds)
        {
            int timeout = Math.Max(MinTimeout, Math.Min(MaxTimeout, seconds));
            return new JObject { ["timeout"] = timeout }.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static bool ShouldStop(float elapsed, int maxSeconds)
        {
            return elapsed >= maxSeconds - StopMargin;
        }

        /// <summary>Devuelve el texto reconocido, o null si no se entendió nada.</summary>
        public static string ParseText(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                string text = JObject.Parse(json)["text"]?.ToString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        public static string DescribeError(long status, string body)
        {
            if (status == 0) return ModLang.Pick(
                "No se pudo conectar con Player2 para reconocer tu voz. Comprueba que la app esté abierta.",
                "Could not connect to Player2 for voice recognition. Check that the app is open.",
                "Не удалось подключиться к Player2 для распознавания голоса. Убедись, что приложение открыто.");
            if (status == 404) return ModLang.Pick(
                "Se agotó el tiempo de escucha. Inténtalo de nuevo.",
                "Listening time expired. Please try again.",
                "Время ожидания голосового ввода истекло. Попробуй ещё раз.");
            if (status == 409) return ModLang.Pick(
                "Player2 ya está escuchando. Inténtalo de nuevo en un momento.",
                "Player2 is already listening. Try again in a moment.",
                "Player2 уже ведёт запись. Попробуй ещё раз через секунду.");
            if (status == 503) return ModLang.Pick(
                "El reconocimiento de voz de Player2 no está disponible. Revisa el micrófono en la app.",
                "Player2 voice recognition is unavailable. Check the microphone in the app.",
                "Распознавание голоса Player2 недоступно. Проверь микрофон в приложении.");
            return LlmProtocol.DescribeError(status, null, body);
        }
    }
}
