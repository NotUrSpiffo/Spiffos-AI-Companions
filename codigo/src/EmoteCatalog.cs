using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Los gestos que la IA puede añadir a su respuesta con una etiqueta. Va en el mismo texto y no como herramienta
    /// para no gastar una segunda petición, y porque así también sirve en los comentarios automáticos.
    /// </summary>
    public static class EmoteCatalog
    {
        private static readonly Regex Tag = new Regex(@"[\[\(]\s*(?:gesto|emote)\s*[:=]\s*([^\]\)]*)[\]\)]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Los valores son los nombres del juego en minúsculas. Sentarse y tumbarse quedan fuera porque no terminan solos.
        private static readonly Dictionary<string, string> Triggers = new Dictionary<string, string>
        {
            // Español
            { "beso", "blowkiss" },
            { "tequiero", "loveyou" },
            { "saludar", "wave" },
            { "reir", "laugh" },
            { "celebrar", "cheer" },
            { "aprobar", "thumbsup" },
            { "negar", "nonono" },
            { "encogerse", "shrug" },
            { "llorar", "cry" },
            { "desesperado", "despair" },
            { "asustado", "cower" },
            { "reverencia", "bow" },
            { "arrodillarse", "kneel" },
            { "presumir", "flex" },
            { "rugir", "roar" },
            { "desafiar", "challenge" },
            { "bailar", "dance" },
            { "brindar", "toast" },
            { "llamar", "comehere" },
            { "senalar", "point" },
            { "cabezazos", "headbang" },
            // English
            { "kiss", "blowkiss" },
            { "blowkiss", "blowkiss" },
            { "loveyou", "loveyou" },
            { "love", "loveyou" },
            { "wave", "wave" },
            { "greet", "wave" },
            { "laugh", "laugh" },
            { "cheer", "cheer" },
            { "celebrate", "cheer" },
            { "thumbsup", "thumbsup" },
            { "approve", "thumbsup" },
            { "no", "nonono" },
            { "nonono", "nonono" },
            { "deny", "nonono" },
            { "shrug", "shrug" },
            { "cry", "cry" },
            { "despair", "despair" },
            { "cower", "cower" },
            { "scared", "cower" },
            { "bow", "bow" },
            { "kneel", "kneel" },
            { "flex", "flex" },
            { "brag", "flex" },
            { "roar", "roar" },
            { "challenge", "challenge" },
            { "dance", "dance" },
            { "toast", "toast" },
            { "comehere", "comehere" },
            { "call", "comehere" },
            { "point", "point" },
            { "headbang", "headbang" }
        };

        public static string Instructions => ModLang.Pick(
            "Puedes acompañar lo que dices con un gesto de tu cuerpo si encaja con lo que siente tu personaje: empieza la respuesta con la etiqueta, como en \"[gesto:reir] ¡Buena esa!\". La mayoría de tus frases no lo necesitan: guárdalo para cuando de verdad aporte. Si te piden un gesto, como bailar o saludar, hazlo así. Gestos: beso y tequiero (cariño), saludar, reir, celebrar, aprobar, negar, encogerse (duda o desinterés), llorar, desesperado, asustado, reverencia, arrodillarse, presumir, rugir, desafiar, bailar, brindar, llamar, señalar, cabezazos.",
            "You can accompany what you say with a body emote if it fits what your character feels: start your reply with the tag, such as \"[emote:laugh] Good one!\". Most of your lines do not need one: save it for when it truly adds something. If asked to perform a gesture, like dancing or waving, do it this way. Emotes: kiss and loveyou (affection), wave, laugh, cheer, thumbsup, deny, shrug (doubt or disinterest), cry, despair, cower, bow, kneel, flex, roar, challenge, dance, toast, call, point, headbang.");

        /// <summary>Devuelve el texto sin etiquetas, que nunca deben verse. trigger es el primer gesto conocido, o null.</summary>
        public static string Extract(string reply, out string trigger)
        {
            trigger = null;
            if (string.IsNullOrEmpty(reply)) return reply;
            string found = null;
            string clean = Tag.Replace(reply, match =>
            {
                if (found == null && Triggers.TryGetValue(Normalize(match.Groups[1].Value), out string value)) found = value;
                return " ";
            });
            trigger = found;
            return Regex.Replace(clean, @"\s+", " ").Trim();
        }

        private static string Normalize(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && c != ' ') sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>Cuánto debe estar quieto para un gesto: depende de la animación, que el juego no permite conocer de antemano.</summary>
    public sealed class EmoteGoal
    {
        public const float EnterSeconds = 2.5f;
        public const float MaxSeconds = 6f;

        private readonly float _startedAt;

        public bool Entered { get; private set; }

        public EmoteGoal(float now)
        {
            _startedAt = now;
        }

        public bool Finished(bool playing, float now)
        {
            if (playing) Entered = true;
            float elapsed = now - _startedAt;
            // Bailar y otros se repiten hasta que alguien los para, por eso hay un máximo.
            return Entered ? !playing || elapsed >= MaxSeconds : elapsed >= EnterSeconds;
        }
    }
}
