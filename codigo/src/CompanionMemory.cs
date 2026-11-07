using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace OfflineCompanionsAI
{
    public sealed class MemoryEntry
    {
        public string Role;
        public string Content;
    }

    /// <summary>Algo que ocurrió de verdad en la partida. Lo registra el mod, no la IA, así que no puede inventárselo.</summary>
    public sealed class MemoryEvent
    {
        public int Day;
        public int Weight;
        public string Text;
    }

    /// <summary>Cuánto importa un suceso: si no caben todos, primero se olvida lo menos importante.</summary>
    public static class MemoryWeight
    {
        public const int Job = 1;

        /// <summary>Haber llegado a un sitio pesa como un trabajo: nunca desplaza a un jefe, una muerte o un susto.</summary>
        public const int Visit = 1;

        public const int Notable = 2;
        public const int Death = 3;
        public const int Boss = 4;
    }

    /// <summary>Cómo cuenta lo vivido en un lugar marcado. Lo redacta el mod, sin gastar saldo. Sin lugar, el texto de siempre.</summary>
    public static class ExpeditionText
    {
        /// <summary>A esta distancia de un lugar marcado, ya ha llegado.</summary>
        public const float ArrivalMeters = 15f;

        /// <summary>A esta distancia, lo que pase ocurre "allí", y quien esté cerca estaba con él.</summary>
        public const float NearbyMeters = 40f;

        private const int MaxNames = 3;

        public static string Arrival(string place, IList<string> with)
        {
            string text = ModLang.Pick("Llegué por primera vez a ", "I arrived for the first time at ") + place;
            if (with == null || with.Count == 0) return text + ".";
            var names = new List<string>(with);
            bool more = names.Count > MaxNames;
            if (more) names.RemoveRange(MaxNames, names.Count - MaxNames);
            string last = more ? ModLang.Pick("otros", "others") : names[names.Count - 1];
            if (!more) names.RemoveAt(names.Count - 1);
            string joinWord = ModLang.Pick(" y ", " and ");
            string withWord = ModLang.Pick(" con ", " with ");
            return text + withWord + (names.Count > 0 ? string.Join(", ", names.ToArray()) + joinWord : "") + last + ".";
        }

        public static string NearDeath(string place)
        {
            return ModLang.Pick(
                "Estuve a punto de morir" + In(place) + " y salí con vida.",
                "I nearly died" + In(place) + " and made it out alive.");
        }

        public static string Death(string place)
        {
            return ModLang.Pick(
                "Morí" + In(place) + " y volví a la vida.",
                "I died" + In(place) + " and came back to life.");
        }

        public static string PlayerNearDeath(string player, string place)
        {
            return ModLang.Pick(
                player + " estuvo a punto de morir" + In(place) + " y salió adelante a mi lado.",
                player + " nearly died" + In(place) + " and pulled through by my side.");
        }

        public static string Raid(string place)
        {
            return ModLang.Pick(
                "Aguantamos una incursión de enemigos" + In(place) + ".",
                "We held off an enemy raid" + In(place) + ".");
        }

        private static string In(string place)
        {
            return string.IsNullOrEmpty(place) ? "" : ModLang.Pick(" en ", " at ") + place;
        }
    }

    /// <summary>Lo que el jugador ve y puede corregir en el editor.</summary>
    public sealed class MemoryTexts
    {
        public string Facts = "";
        public string Events = "";
        public string Summary = "";

        public bool SameAs(MemoryTexts other)
        {
            return other != null && Facts == other.Facts && Events == other.Events && Summary == other.Summary;
        }
    }

    /// <summary>
    /// Lo que un compañero recuerda entre sesiones: sus últimos mensajes, un resumen de lo anterior que la IA reescribe,
    /// y aparte lo que no debe perderse al reescribirlo: hechos firmes y sucesos de la partida.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class CompanionMemory
    {
        public const int MaxSummaryLength = 2000;
        public const int SummaryBatch = 8;
        public const int HardLimit = 80;
        public const int MaxFactLength = 200;

        // Van en todas las peticiones: 1500 caracteres son unos 400 tokens.
        public const int MaxFacts = 30;
        public const int MaxFactsLength = 1500;
        public const int MaxEvents = 12;
        public const int MaxEventLength = 120;

        private const int MaxFactsPerSummary = 3;
        private const string FactPrefixEs = "HECHO:";
        private const string FactPrefixEn = "FACT:";
        private const string DayPrefixEs = "Día ";
        private const string DayPrefixEn = "Day ";

        private static string FactPrefix => ModLang.Pick(FactPrefixEs, FactPrefixEn);
        private static string DayPrefix => ModLang.Pick(DayPrefixEs, DayPrefixEn);

        public string Summary = "";
        public List<MemoryEntry> Recent = new List<MemoryEntry>();
        public List<string> Facts = new List<string>();
        public List<MemoryEvent> Events = new List<MemoryEvent>();

        // Lo que debe sobrevivir al cerrar el juego, además de la conversación.
        public int RetreatPercent;
        public bool GuardActive;
        public float GuardX;
        public float GuardY;
        public float GuardZ;
        public string LastTaskDescription;
        public Dictionary<string, int> LastTaskGained = new Dictionary<string, int>();

        // Dónde se le dejó: se sabe también con el compañero fuera de la zona cargada.
        public string StayLabel;
        public float StayX;
        public float StayY;
        public float StayZ;
        public bool StayTend;

        /// <summary>Los lugares marcados donde ya ha estado. Aparte de las vivencias, que se olvidan: volver no es llegar.</summary>
        public List<string> VisitedPlaces = new List<string>();

        private const int MaxVisitedPlaces = 100;

        public void SetStay(string label, float x, float y, float z, bool tend)
        {
            StayLabel = label;
            StayX = x;
            StayY = y;
            StayZ = z;
            StayTend = tend;
        }

        /// <summary>Devuelve true si estaba en algún sitio y por tanto hay que guardarlo.</summary>
        public bool ClearStay()
        {
            if (StayLabel == null) return false;
            StayLabel = null;
            StayTend = false;
            return true;
        }

        /// <summary>Lo que hace donde se le dejó, como se lo cuenta al jugador, o null si no se le dejó en ningún sitio.</summary>
        public string DescribeStay()
        {
            return StayLabel == null
                ? null
                : (StayTend
                    ? ModLang.Pick("encargándose de ", "tending ")
                    : ModLang.Pick("esperando en ", "waiting at ")) + StayLabel;
        }

        /// <summary>True solo la primera vez que llega a ese lugar.</summary>
        public bool MarkVisited(string place)
        {
            if (VisitedPlaces.Exists(known => string.Equals(known, place, System.StringComparison.OrdinalIgnoreCase))) return false;
            VisitedPlaces.Add(place);
            if (VisitedPlaces.Count > MaxVisitedPlaces) VisitedPlaces.RemoveAt(0);
            return true;
        }

        /// <summary>Cambia cuando el jugador edita los recuerdos, para descartar un resumen que estuviera en curso.</summary>
        [JsonIgnore]
        public int Revision;

        /// <summary>Copia el estado actual. Devuelve true si ha cambiado y por tanto hay que guardarlo.</summary>
        public bool UpdateState(int retreatPercent, bool guardActive, float x, float y, float z, string lastTask, IDictionary<string, int> gained)
        {
            bool sameGained = gained == null ? LastTaskGained.Count == 0 : gained.Count == LastTaskGained.Count;
            if (sameGained && gained != null)
            {
                foreach (KeyValuePair<string, int> item in gained)
                {
                    if (!LastTaskGained.TryGetValue(item.Key, out int had) || had != item.Value) sameGained = false;
                }
            }
            bool same = sameGained
                && RetreatPercent == retreatPercent
                && GuardActive == guardActive
                && (!guardActive || (GuardX == x && GuardY == y && GuardZ == z))
                && LastTaskDescription == lastTask;
            if (same) return false;

            RetreatPercent = retreatPercent;
            GuardActive = guardActive;
            GuardX = x;
            GuardY = y;
            GuardZ = z;
            LastTaskDescription = lastTask;
            LastTaskGained = gained == null ? new Dictionary<string, int>() : new Dictionary<string, int>(gained);
            return true;
        }

        public void Add(string question, string reply)
        {
            Recent.Add(new MemoryEntry { Role = "user", Content = question });
            Recent.Add(new MemoryEntry { Role = "assistant", Content = reply });
            // Si resumir falla o está desactivado, el archivo no debe crecer sin límite.
            if (Recent.Count > HardLimit) Recent.RemoveRange(0, Recent.Count - HardLimit);
        }

        public List<ChatMessage> ToMessages(int keep)
        {
            var result = new List<ChatMessage>();
            for (int i = System.Math.Max(0, Recent.Count - keep); i < Recent.Count; i++)
            {
                result.Add(new ChatMessage(Recent[i].Role, Recent[i].Content));
            }
            return result;
        }

        public bool NeedsSummary(int keep)
        {
            return Recent.Count >= keep + SummaryBatch;
        }

        public int CountToSummarize(int keep)
        {
            return System.Math.Max(0, Recent.Count - keep);
        }

        /// <summary>Separa los hechos firmes del resumen. Si la IA solo ha devuelto hechos, se conserva el resumen anterior.</summary>
        public void ApplySummary(string summary, int summarizedCount)
        {
            var text = new StringBuilder();
            int added = 0;
            foreach (string raw in (summary ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(FactPrefixEs, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (added < MaxFactsPerSummary && AddFact(line.Substring(FactPrefixEs.Length))) added++;
                }
                else if (line.StartsWith(FactPrefixEn, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (added < MaxFactsPerSummary && AddFact(line.Substring(FactPrefixEn.Length))) added++;
                }
                else if (line.Length > 0)
                {
                    if (text.Length > 0) text.Append('\n');
                    text.Append(line);
                }
            }
            if (text.Length > 0) Summary = LlmProtocol.Shorten(text.ToString(), MaxSummaryLength);
            Recent.RemoveRange(0, System.Math.Min(summarizedCount, Recent.Count));
        }

        /// <summary>
        /// Añade algo que debe recordar siempre. Devuelve false si está vacío, repetido o no cabe.
        /// No cambia la revisión: un resumen en curso ya no lo pisa, porque se guarda aparte.
        /// </summary>
        public bool RememberFact(string fact)
        {
            return AddFact(fact);
        }

        private bool AddFact(string fact)
        {
            fact = LlmProtocol.Shorten(fact ?? "", MaxFactLength).Trim();
            if (fact.Length == 0 || Facts.Count >= MaxFacts) return false;
            int total = fact.Length;
            foreach (string known in Facts)
            {
                if (string.Equals(known, fact, System.StringComparison.OrdinalIgnoreCase)) return false;
                total += known.Length;
            }
            if (total > MaxFactsLength) return false;
            Facts.Add(fact);
            return true;
        }

        /// <summary>Anota un suceso. Con once, solo la primera vez; si no, solo una vez al día. Devuelve false si ya estaba.</summary>
        public bool AddEvent(int day, int weight, string text, bool once = false)
        {
            text = LlmProtocol.Shorten(text ?? "", MaxEventLength).Trim();
            if (text.Length == 0) return false;
            foreach (MemoryEvent known in Events)
            {
                if ((once || known.Day == day) && string.Equals(known.Text, text, System.StringComparison.OrdinalIgnoreCase)) return false;
            }
            Events.Add(new MemoryEvent { Day = day, Weight = weight, Text = text });
            TrimEvents();
            return true;
        }

        // De lo menos importante, lo más antiguo.
        private void TrimEvents()
        {
            while (Events.Count > MaxEvents)
            {
                int worst = 0;
                for (int i = 1; i < Events.Count; i++)
                {
                    if (Events[i].Weight < Events[worst].Weight) worst = i;
                }
                Events.RemoveAt(worst);
            }
        }

        private static string Describe(MemoryEvent happened)
        {
            return happened.Day > 0 ? DayPrefix + happened.Day + ": " + happened.Text : happened.Text;
        }

        private static void AppendList(StringBuilder text, string title, IEnumerable<string> lines)
        {
            if (text.Length > 0) text.AppendLine();
            text.AppendLine(title);
            foreach (string line in lines) text.AppendLine("- " + line);
        }

        private List<string> DescribeEvents()
        {
            return Events.ConvertAll(Describe);
        }

        /// <summary>Todo lo que recuerda a largo plazo, para la petición.</summary>
        public string LongTerm()
        {
            var text = new StringBuilder();
            if (Facts.Count > 0) AppendList(text, ModLang.Pick("Hechos firmes:", "Core facts:"), Facts);
            if (Events.Count > 0) AppendList(text, ModLang.Pick("Lo que habéis vivido:", "What you have lived through:"), DescribeEvents());
            if (text.Length == 0) return Summary;
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                text.AppendLine();
                text.AppendLine(ModLang.Pick("Otros recuerdos:", "Other memories:"));
                text.AppendLine(Summary);
            }
            return text.ToString().TrimEnd();
        }

        public MemoryTexts Texts()
        {
            return new MemoryTexts
            {
                Facts = string.Join("\n", Facts.ToArray()),
                Events = string.Join("\n", DescribeEvents().ToArray()),
                Summary = Summary
            };
        }

        public void ApplyEdit(MemoryTexts edited)
        {
            MemoryTexts current = Texts();
            if (edited.Summary != current.Summary) EditSummary(edited.Summary);
            if (edited.Facts != current.Facts) EditFacts(edited.Facts);
            if (edited.Events != current.Events) EditEvents(edited.Events);
        }

        public void EditSummary(string summary)
        {
            Summary = (summary ?? "").Trim();
            Revision++;
        }

        /// <summary>Un hecho por línea.</summary>
        public void EditFacts(string facts)
        {
            Facts.Clear();
            foreach (string line in (facts ?? "").Split('\n')) AddFact(line);
            Revision++;
        }

        /// <summary>Un suceso por línea, como se muestra. Los que siguen ahí conservan su importancia.</summary>
        public void EditEvents(string events)
        {
            List<MemoryEvent> before = Events;
            Events = new List<MemoryEvent>();
            foreach (string raw in (events ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                int day = 0;
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    if (line.StartsWith(DayPrefixEs, System.StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(line.Substring(DayPrefixEs.Length, colon - DayPrefixEs.Length).Trim(), out int parsedEs))
                    {
                        day = parsedEs;
                        line = line.Substring(colon + 1).Trim();
                    }
                    else if (line.StartsWith(DayPrefixEn, System.StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(line.Substring(DayPrefixEn.Length, colon - DayPrefixEn.Length).Trim(), out int parsedEn))
                    {
                        day = parsedEn;
                        line = line.Substring(colon + 1).Trim();
                    }
                }
                string text = line;
                MemoryEvent known = before.Find(e => string.Equals(e.Text, text, System.StringComparison.OrdinalIgnoreCase));
                AddEvent(day, known?.Weight ?? MemoryWeight.Notable, text);
            }
            Revision++;
        }

        public void ClearRecent()
        {
            Recent.Clear();
            Revision++;
        }

        public void Forget()
        {
            Summary = "";
            Facts.Clear();
            Events.Clear();
            VisitedPlaces.Clear();
            ClearRecent();
        }

        /// <summary>La petición para incorporar los mensajes más antiguos al resumen.</summary>
        public List<ChatMessage> BuildSummaryRequest(string companionName, int count)
        {
            var text = new StringBuilder();
            if (Facts.Count > 0 || Events.Count > 0)
            {
                var saved = new StringBuilder();
                if (Facts.Count > 0) AppendList(saved, ModLang.Pick("Hechos firmes:", "Core facts:"), Facts);
                if (Events.Count > 0) AppendList(saved, ModLang.Pick("Sucesos:", "Events:"), DescribeEvents());
                text.AppendLine(ModLang.Pick("Ya guardado aparte, no lo repitas:", "Already saved separately, do not repeat:"));
                text.AppendLine(saved.ToString().TrimEnd());
                text.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                text.AppendLine(ModLang.Pick("Recuerdos actuales:", "Current memories:"));
                text.AppendLine(Summary);
                text.AppendLine();
            }
            text.AppendLine(ModLang.Pick("Conversación nueva:", "New conversation:"));
            string playerLabel = ModLang.Pick("Jugador: ", "Player: ");
            for (int i = 0; i < count && i < Recent.Count; i++)
            {
                text.AppendLine((Recent[i].Role == "user" ? playerLabel : companionName + ": ") + Recent[i].Content);
            }

            string instructions = ModLang.IsSpanish
                ? "Actualizas los recuerdos de " + companionName + ", compañero del jugador en Valheim. "
                    + "Combina los recuerdos actuales y la conversación nueva en un único texto, en primera persona y en español. "
                    + "Conserva lo que importe más adelante: relaciones, planes y anécdotas. "
                    + "Olvida lo pasajero, como la salud, el inventario o las órdenes ya cumplidas, y no cuentes como presente lo que estaba ocurriendo en ese momento, como una pelea. "
                    + "No añadas nada que no aparezca en el texto. Las líneas entre paréntesis son sucesos de la partida, no cosas que haya dicho el jugador. "
                    + "Si en la conversación nueva aparece algo que debe recordarse siempre, como un nombre, cómo quiere que le traten, una promesa o una preferencia clara, ponlo además en una línea propia que empiece por \"" + FactPrefix + "\", como mucho " + MaxFactsPerSummary + " y solo si no está ya guardado. "
                    + "Devuelve solo eso, sin formato, en menos de " + (MaxSummaryLength / 2) + " caracteres."
                : "You update the memories of " + companionName + ", the player's companion in Valheim. "
                    + "Combine the current memories and the new conversation into a single text, in first person and in English. "
                    + "Keep what matters later on: relationships, plans, and anecdotes. "
                    + "Forget passing details like health, inventory, or completed orders, and do not describe as ongoing what was happening at that moment, such as a fight. "
                    + "Do not add anything that does not appear in the text. Lines in parentheses are in-game events, not things the player said. "
                    + "If something in the new conversation should always be remembered, such as a name, how they want to be addressed, a promise, or a clear preference, also put it on its own line starting with \"" + FactPrefix + "\", at most " + MaxFactsPerSummary + " and only if not already saved. "
                    + "Return only that, unformatted, in under " + (MaxSummaryLength / 2) + " characters.";

            return new List<ChatMessage>
            {
                new ChatMessage("system", instructions),
                new ChatMessage("user", text.ToString())
            };
        }
    }
}
