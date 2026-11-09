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
        private static readonly Regex Tag = new Regex(@"[\[\(]\s*(?:gesto|emote|жест|эмоция)\s*[:=]\s*([^\]\)]*)[\]\)]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Los valores son los nombres del juego en minúsculas. Sentarse y tumbarse quedan fuera porque no terminan solos.
        private static readonly Dictionary<string, string> Triggers = InitTriggers();

        private static Dictionary<string, string> InitTriggers()
        {
            var map = new Dictionary<string, string>();
            void Add(string key, string anim) { map[Normalize(key)] = anim; }

            // Español
            Add("beso", "blowkiss");
            Add("tequiero", "loveyou");
            Add("saludar", "wave");
            Add("reir", "laugh");
            Add("celebrar", "cheer");
            Add("aprobar", "thumbsup");
            Add("negar", "nonono");
            Add("encogerse", "shrug");
            Add("llorar", "cry");
            Add("desesperado", "despair");
            Add("asustado", "cower");
            Add("reverencia", "bow");
            Add("arrodillarse", "kneel");
            Add("presumir", "flex");
            Add("rugir", "roar");
            Add("desafiar", "challenge");
            Add("bailar", "dance");
            Add("brindar", "toast");
            Add("llamar", "comehere");
            Add("senalar", "point");
            Add("cabezazos", "headbang");

            // English
            Add("kiss", "blowkiss");
            Add("blowkiss", "blowkiss");
            Add("loveyou", "loveyou");
            Add("love", "loveyou");
            Add("wave", "wave");
            Add("greet", "wave");
            Add("laugh", "laugh");
            Add("cheer", "cheer");
            Add("celebrate", "cheer");
            Add("thumbsup", "thumbsup");
            Add("approve", "thumbsup");
            Add("no", "nonono");
            Add("nonono", "nonono");
            Add("deny", "nonono");
            Add("shrug", "shrug");
            Add("cry", "cry");
            Add("despair", "despair");
            Add("cower", "cower");
            Add("scared", "cower");
            Add("bow", "bow");
            Add("kneel", "kneel");
            Add("flex", "flex");
            Add("brag", "flex");
            Add("roar", "roar");
            Add("challenge", "challenge");
            Add("dance", "dance");
            Add("toast", "toast");
            Add("comehere", "comehere");
            Add("call", "comehere");
            Add("point", "point");
            Add("headbang", "headbang");

            // Russian
            Add("поцелуй", "blowkiss");
            Add("воздушныйпоцелуй", "blowkiss");
            Add("люблю", "loveyou");
            Add("любовь", "loveyou");
            Add("помахать", "wave");
            Add("махать", "wave");
            Add("привет", "wave");
            Add("поприветствовать", "wave");
            Add("смех", "laugh");
            Add("смеяться", "laugh");
            Add("радость", "cheer");
            Add("ура", "cheer");
            Add("праздновать", "cheer");
            Add("класс", "thumbsup");
            Add("одобрение", "thumbsup");
            Add("палецвверх", "thumbsup");
            Add("нет", "nonono");
            Add("несогласен", "nonono");
            Add("отказ", "nonono");
            Add("пожатьплечами", "shrug");
            Add("плечи", "shrug");
            Add("плач", "cry");
            Add("плакать", "cry");
            Add("отчаяние", "despair");
            Add("страх", "cower");
            Add("испуг", "cower");
            Add("дрожать", "cower");
            Add("поклон", "bow");
            Add("поклониться", "bow");
            Add("наколени", "kneel");
            Add("преклонитьколени", "kneel");
            Add("мускулы", "flex");
            Add("сила", "flex");
            Add("показатьсилу", "flex");
            Add("хвастовство", "flex");
            Add("рык", "roar");
            Add("рычать", "roar");
            Add("вызов", "challenge");
            Add("танец", "dance");
            Add("танцевать", "dance");
            Add("плясать", "dance");
            Add("тост", "toast");
            Add("выпить", "toast");
            Add("сюда", "comehere");
            Add("комне", "comehere");
            Add("позвать", "comehere");
            Add("указать", "point");
            Add("показать", "point");
            Add("трястиголовой", "headbang");
            Add("хэдбэнг", "headbang");

            return map;
        }

        public static string Instructions => ModLang.Pick(
            "Puedes acompañar lo que dices con un gesto de tu cuerpo si encaja con lo que siente tu personaje: empieza la respuesta con la etiqueta, como en \"[gesto:reir] ¡Buena esa!\". La mayoría de tus frases no lo necesitan: guárdalo para cuando de verdad aporte. Si te piden un gesto, como bailar o saludar, hazlo así. Gestos: beso y tequiero (cariño), saludar, reir, celebrar, aprobar, negar, encogerse (duda o desinterés), llorar, desesperado, asustado, reverencia, arrodillarse, presumir, rugir, desafiar, bailar, brindar, llamar, señalar, cabezazos.",
            "You can accompany what you say with a body emote if it fits what your character feels: start your reply with the tag, such as \"[emote:laugh] Good one!\". Most of your lines do not need one: save it for when it truly adds something. If asked to perform a gesture, like dancing or waving, do it this way. Emotes: kiss and loveyou (affection), wave, laugh, cheer, thumbsup, deny, shrug (doubt or disinterest), cry, despair, cower, bow, kneel, flex, roar, challenge, dance, toast, call, point, headbang.",
            "Ты можешь сопроводить свои слова жестом тела, если это подходит настроению твоего персонажа: начни ответ с метки, например \"[emote:laugh] Отличная шутка!\". Большинству фраз это не нужно: используй только тогда, когда это действительно к месту. Если тебя просят сделать жест, например станцевать или помахать, сделай это так. Жесты: kiss/поцелуй, wave/помахать, laugh/смех, cheer/радость, thumbsup/класс, nonono/нет, shrug/пожать плечами (сомнение), cry/плач, despair/отчаяние, cower/страх, bow/поклон, kneel/на колени, flex/сила, roar/рык, challenge/вызов, dance/танец, toast/тост, comehere/сюда, point/указать, headbang/трясти головой.");

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
