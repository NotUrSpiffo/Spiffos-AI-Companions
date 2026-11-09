using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace OfflineCompanionsAI
{
    public sealed class ChatMessage
    {
        public readonly string Role;
        public readonly string Content;
        public readonly JArray ToolCalls;
        public readonly string ToolCallId;

        public ChatMessage(string role, string content, JArray toolCalls = null, string toolCallId = null)
        {
            Role = role;
            Content = content;
            ToolCalls = toolCalls;
            ToolCallId = toolCallId;
        }
    }

    public sealed class ToolCall
    {
        public string Id;
        public string Name;
        public string Arguments;
    }

    public sealed class LlmResponse
    {
        public string Text;
        public JArray RawToolCalls;
        public readonly List<ToolCall> ToolCalls = new List<ToolCall>();
    }

    /// <summary>Formato compatible con OpenAI. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class LlmProtocol
    {
        private static readonly Regex ThinkBlock = new Regex("<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex Narration = new Regex(@"\*[^*]*(\*|$)", RegexOptions.Singleline | RegexOptions.Compiled);

        // Si no tiene herramientas disponibles, un modelo puede escribir su marcado interno de llamadas como si fuera texto.
        private static readonly Regex LeakedToolMarkup = new Regex(@"<[｜|]{1,2}.*$", RegexOptions.Singleline | RegexOptions.Compiled);

        public static string BuildRequest(IEnumerable<ChatMessage> messages, string model, int maxTokens, float temperature, JArray tools = null)
        {
            var list = new JArray();
            foreach (ChatMessage m in messages)
            {
                var item = new JObject { ["role"] = m.Role, ["content"] = m.Content ?? "" };
                if (m.ToolCalls != null) item["tool_calls"] = m.ToolCalls;
                if (m.ToolCallId != null) item["tool_call_id"] = m.ToolCallId;
                list.Add(item);
            }

            var body = new JObject { ["messages"] = list, ["temperature"] = temperature };
            // Player2 elige el modelo y no pide este campo.
            if (!string.IsNullOrWhiteSpace(model)) body["model"] = model.Trim();
            // Con un límite bajo, algunos modelos lo gastan razonando y devuelven una respuesta vacía.
            if (maxTokens > 0) body["max_tokens"] = maxTokens;
            if (tools != null && tools.Count > 0)
            {
                body["tools"] = tools;
                body["tool_choice"] = "auto";
            }
            return body.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>Devuelve la respuesta, o null si no contiene ni texto ni acciones.</summary>
        public static LlmResponse ParseResponse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                JToken message = JObject.Parse(json).SelectToken("choices[0].message");
                if (message == null) return null;

                var response = new LlmResponse();
                string content = message["content"]?.ToString();
                if (!string.IsNullOrEmpty(content))
                {
                    content = LeakedToolMarkup.Replace(ThinkBlock.Replace(content, ""), "").Trim();
                    if (content.Length > 0) response.Text = content;
                }

                if (message["tool_calls"] is JArray calls && calls.Count > 0)
                {
                    response.RawToolCalls = calls;
                    foreach (JToken call in calls)
                    {
                        response.ToolCalls.Add(new ToolCall
                        {
                            Id = call["id"]?.ToString(),
                            Name = call.SelectToken("function.name")?.ToString(),
                            Arguments = call.SelectToken("function.arguments")?.ToString()
                        });
                    }
                }
                return response.Text == null && response.ToolCalls.Count == 0 ? null : response;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Convierte un error HTTP en un mensaje claro para el jugador.</summary>
        public static string DescribeError(long status, string networkError, string body)
        {
            if (status == 0) return ModLang.Pick(
                "No se pudo conectar con la IA. Comprueba que el servicio esté abierto.",
                "Could not connect to the AI. Check that the service is running.",
                "Не удалось подключиться к ИИ. Проверь, запущен ли сервис.");
            bool noCredits = body != null && body.IndexOf("insufficient_credits", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (noCredits) return ModLang.Pick(
                "Tu cuenta de IA no tiene saldo suficiente.",
                "Your AI account does not have enough balance.",
                "На твоём аккаунте ИИ недостаточно средств.");
            if (status == 401 || status == 403) return ModLang.Pick(
                "La IA rechazó la autenticación. Revisa la clave o la sesión.",
                "The AI rejected authentication. Check your API key or session.",
                "ИИ отклонил авторизацию. Проверь API-ключ или сессию.");
            if (status == 429) return ModLang.Pick(
                "Demasiadas peticiones a la IA. Espera un momento.",
                "Too many requests to the AI. Wait a moment.",
                "Слишком много запросов к ИИ. Подожди немного.");
            if (status >= 500) return ModLang.Pick(
                "El servicio de IA ha fallado. Inténtalo de nuevo.",
                "The AI service failed. Please try again.",
                "Сбой в работе сервиса ИИ. Попробуй ещё раз.");
            return ModLang.Pick(
                "La IA rechazó la petición (HTTP " + status + ").",
                "The AI rejected the request (HTTP " + status + ").",
                "ИИ отклонил запрос (HTTP " + status + ").");
        }

        /// <summary>El saldo de la respuesta de Player2, o null si no se puede leer.</summary>
        public static long? ParseJoules(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                JToken joules = JObject.Parse(json)["joules"];
                return joules != null && joules.Type == JTokenType.Integer ? joules.Value<long>() : (long?)null;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Quita acciones narradas como *sonríe*, que sobre la cabeza se verían con asteriscos, salvo que la ficha
        /// del personaje las pida (keepNarration).
        /// </summary>
        public static string StripNarration(string text, bool keepNarration = false)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Sin herramientas disponibles, algunos modelos las escriben como texto, y no debe leerse sobre su cabeza.
            text = Regex.Replace(text, @"<tool[\w-]*\b[\s\S]*?(</tool[\w-]*>|/>)", " ");
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (keepNarration) return Regex.Replace(text, @"\s+", " ").Trim();
            string stripped = Regex.Replace(Narration.Replace(text, " "), @"\s+", " ").Trim();
            return stripped.Length > 0 ? stripped : text.Replace("*", "").Trim();
        }

        /// <summary>Segundos que debe permanecer un texto en pantalla según su longitud.</summary>
        public static float ReadingSeconds(string text, float charsPerSecond, float minSeconds, float maxSeconds)
        {
            float seconds = (text?.Length ?? 0) / System.Math.Max(1f, charsPerSecond);
            return System.Math.Max(minSeconds, System.Math.Min(maxSeconds, seconds));
        }

        /// <summary>Recorta una respuesta larga para que quepa sobre la cabeza del compañero.</summary>
        public static string Shorten(string text, int maxChars)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            if (maxChars <= 0 || text.Length <= maxChars) return text;

            string cut = text.Substring(0, maxChars);
            int end = cut.LastIndexOfAny(new[] { '.', '!', '?' });
            if (end >= maxChars / 2) return cut.Substring(0, end + 1);
            int space = cut.LastIndexOf(' ');
            return (space > 0 ? cut.Substring(0, space) : cut).TrimEnd(',', ';', ':') + "...";
        }

        /// <summary>Reparte un texto largo en tramos que caben sobre la cabeza, sin perder nada. Corta por frases si puede.</summary>
        public static List<string> Pages(string text, int maxChars)
        {
            char[] sentenceEnds = { '.', '!', '?', '…' };
            text = Shorten(text, 0);
            var pages = new List<string>();
            while (maxChars > 0 && text.Length > maxChars)
            {
                string cut = text.Substring(0, maxChars);
                int end = cut.LastIndexOfAny(sentenceEnds);
                int at = end >= maxChars / 2 ? end + 1 : cut.LastIndexOf(' ');
                if (at <= 0) at = maxChars;

                string page = text.Substring(0, at).Trim();
                pages.Add(end >= maxChars / 2 ? page : page.TrimEnd(',', ';', ':') + "...");
                text = text.Substring(at).Trim();
            }
            if (text.Length > 0) pages.Add(text);
            return pages;
        }
    }
}
