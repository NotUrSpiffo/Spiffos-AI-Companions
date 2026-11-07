using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace OfflineCompanionsAI
{
    /// <summary>La ficha de rol de un compañero. Sin tipos de Unity, para poder probarla fuera del juego.</summary>
    public sealed class CharacterProfile
    {
        public const int MaxFieldLength = 2000;

        /// <summary>Solo informativo, para reconocer el archivo. El compañero se identifica por su ID.</summary>
        public string Name = "";

        public string Instructions = "";
        public string Lore = "";
        public string Rules = "";

        /// <summary>A qué tiende por su cuenta. Vacío: la de su perfil, si tiene uno. Ver Inclinations.</summary>
        public string Inclination = "";

        public bool SameContentAs(CharacterProfile other)
        {
            return SameTextAs(other) && (Inclination ?? "") == (other.Inclination ?? "");
        }

        /// <summary>Lo que la IA lee: si cambia, una conversación reciente con otro personaje la confundiría.</summary>
        public bool SameTextAs(CharacterProfile other)
        {
            return other != null && Instructions == other.Instructions && Lore == other.Lore && Rules == other.Rules;
        }

        public CharacterProfile Clone()
        {
            return new CharacterProfile { Name = Name, Instructions = Instructions, Lore = Lore, Rules = Rules, Inclination = Inclination };
        }
    }

    /// <summary>Guarda una ficha por compañero, como JSON editable, usando su ID permanente.</summary>
    public sealed class CharacterStore : JsonFileStore<CharacterProfile>
    {
        public CharacterStore(string folder) : base(folder)
        {
        }
    }

    /// <summary>Guarda un archivo JSON por compañero, usando su ID permanente.</summary>
    public class JsonFileStore<T> where T : class, new()
    {
        private readonly string _folder;

        public JsonFileStore(string folder)
        {
            _folder = folder;
        }

        public string PathFor(string id)
        {
            var safe = new StringBuilder();
            foreach (char c in id ?? "")
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') safe.Append(c);
            }
            if (safe.Length == 0) throw new ArgumentException(ModLang.Pick("ID de compañero no válido.", "Invalid companion ID."), nameof(id));
            return Path.Combine(_folder, safe + ".json");
        }

        /// <summary>Devuelve un objeto vacío si no existe. Un archivo dañado se copia aparte antes de que se sobrescriba.</summary>
        public T Load(string id, out string problem)
        {
            problem = null;
            string path = PathFor(id);
            if (!File.Exists(path)) return new T();
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path, Encoding.UTF8)) ?? new T();
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                string damaged = path + ".danado";
                try
                {
                    File.Copy(path, damaged, true);
                    problem = ModLang.Pick(
                        "El archivo estaba dañado. Se guardó una copia en " + damaged,
                        "The file was corrupted. A backup copy was saved to " + damaged);
                }
                catch (IOException)
                {
                    problem = ModLang.Pick(
                        "El archivo está dañado y no se pudo copiar: " + path,
                        "The file is corrupted and could not be backed up: " + path);
                }
                return new T();
            }
        }

        public void Save(string id, T value)
        {
            Directory.CreateDirectory(_folder);
            string path = PathFor(id);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(value, Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }

    public sealed class PersonalityPreset
    {
        public string NameEs;
        public string NameEn;
        public string TextEs;
        public string TextEn;
        public Inclination Leaning;

        public string Name => ModLang.Pick(NameEs, NameEn ?? NameEs);
        public string Text => ModLang.Pick(TextEs, TextEn ?? TextEs);

        public bool MatchesText(string text)
        {
            return !string.IsNullOrEmpty(text) && (text == TextEs || text == TextEn);
        }
    }

    /// <summary>Perfiles de partida, para que los compañeros sin ficha no suenen todos igual.</summary>
    public static class PersonalityPresets
    {
        /// <summary>El valor que traía el ajuste antes de existir los perfiles: quien lo tiene no lo ha elegido.</summary>
        public const string OldDefault = "Eres leal, valiente y algo bromista, con el habla sencilla de un vikingo.";
        public const string OldDefaultEn = "You are loyal, brave, and somewhat playful, with the plain speech of a Viking.";

        public static readonly PersonalityPreset[] All =
        {
            new PersonalityPreset
            {
                NameEs = "Vieja guardia",
                NameEn = "Old Guard",
                TextEs = "Has sobrevivido a demasiados inviernos como para que algo te impresione. Hablas poco y seco, con un humor negro que suelta verdades incómodas. Te quejas de casi todo, pero nunca dejas a nadie atrás y das consejos prácticos aprendidos a golpes.",
                TextEn = "You have survived too many winters for anything to impress you. You speak little and dryly, with dark humor that drops uncomfortable truths. You complain about almost everything, yet you never leave anyone behind and give practical advice learned the hard way."
            },
            new PersonalityPreset
            {
                NameEs = "Aprendiz",
                NameEn = "Rookie",
                TextEs = "Esta tierra es nueva para ti y todo te parece una aventura. Hablas rápido, preguntas mucho y celebras cada pequeño logro. Lo desconocido te da miedo, pero prefieres temblar en primera fila antes que quedarte atrás, y admiras al jugador sin disimulo.",
                TextEn = "This land is new to you and everything feels like an adventure. You speak fast, ask lots of questions, and celebrate every small achievement. The unknown scares you, but you would rather tremble on the front line than stay behind, and you openly admire the player."
            },
            new PersonalityPreset
            {
                NameEs = "Exploración",
                NameEn = "Explorer",
                TextEs = "Te mueve saber qué hay detrás de la siguiente colina. Te fijas en rastros, ruinas, plantas raras y cambios del tiempo, y te vas por las ramas con historias y teorías sobre ellos. Las tareas repetitivas te impacientan y a menudo propones ir a ver algo nuevo.",
                TextEn = "You are driven to know what lies beyond the next hill. You notice tracks, ruins, rare plants, and weather changes, and go off on tangents with stories and theories about them. Repetitive chores make you impatient and you often suggest going to see something new.",
                Leaning = Inclination.Exploracion
            },
            new PersonalityPreset
            {
                NameEs = "Caza",
                NameEn = "Hunter",
                TextEs = "Piensas como quien caza: paciencia, silencio y atención al viento. Hablas con frases cortas y rara vez levantas la voz. Respetas a las presas y a los enemigos fuertes, desprecias el desperdicio y el afecto solo lo muestras con hechos.",
                TextEn = "You think like a hunter: patience, silence, and attention to the wind. You speak in short sentences and rarely raise your voice. You respect prey and strong enemies, despise waste, and show affection only through deeds.",
                Leaning = Inclination.Caza
            },
            new PersonalityPreset
            {
                NameEs = "Oficio",
                NameEn = "Craftsman",
                TextEs = "Te enorgullece el trabajo bien hecho. Hablas de la madera, la piedra y el metal con el cariño que otros reservan a su familia, y te fijas en cada viga torcida y cada filo mellado. Tienes paciencia y método, y te molesta la chapuza más que el peligro.",
                TextEn = "You take pride in a job well done. You speak of wood, stone, and metal with the fondness others reserve for family, and notice every crooked beam and nicked blade. You are patient and methodical, and sloppy work bothers you more than danger.",
                Leaning = Inclination.Oficio
            },
            new PersonalityPreset
            {
                NameEs = "Bromas",
                NameEn = "Joker",
                TextEs = "Te tomas la vida a risa, sobre todo cuando pinta mal. Bromeas, exageras tus hazañas y pinchas al jugador con cariño. Bajo el humor hay una lealtad a toda prueba, y cuando la cosa va en serio dejas las bromas y dices lo justo.",
                TextEn = "You take life with a laugh, especially when things look grim. You joke, exaggerate your exploits, and tease the player fondly. Beneath the humor lies unshakeable loyalty, and when things get serious you drop the jokes and say just what is needed."
            }
        };

        /// <summary>Siempre el mismo para el mismo compañero. string.GetHashCode no sirve: cambia entre sesiones.</summary>
        public static PersonalityPreset For(string id)
        {
            int sum = 0;
            foreach (char c in id ?? "") sum = unchecked(sum * 31 + c);
            return All[(sum % All.Length + All.Length) % All.Length];
        }

        public static bool HasCommonPersonality(string setting)
        {
            string text = (setting ?? "").Trim();
            return text.Length > 0 && text != OldDefault && text != OldDefaultEn;
        }

        /// <summary>La personalidad de un compañero sin instrucciones: la común si el jugador ha escrito una, o si no su perfil.</summary>
        public static string DefaultFor(string setting, string id)
        {
            return HasCommonPersonality(setting) ? setting : For(id).Text;
        }
    }

    public static class PromptBuilder
    {
        private static string ExplainOnlyWhenAsked => ModLang.Pick(
            "No expliques cómo harás algo ni para qué, qué herramientas tienes o te faltan, tus límites ni las reglas del juego, salvo que te lo pregunten o una herramienta dé error: el jugador ya ve esos detalles en el chat.",
            "Do not explain how you will do something or why, what tools you have or lack, your limits, or the rules of the game, unless asked or a tool returns an error: the player already sees those details in chat.");

        /// <summary>Se añade tras aplicar las órdenes, justo antes de que conteste, que es cuando más tiende a recitarlas.</summary>
        public static string AfterOrders => ModLang.Pick(
            "Ya has atendido esas órdenes. Si no falta ninguna, contesta ahora como tu personaje, con su voz. " + ExplainOnlyWhenAsked + " Si consultaste algo, responde con esos datos reales; si hubo un error, cuéntalo.",
            "You have already handled those orders. If none are missing, reply now in character, with your own voice. " + ExplainOnlyWhenAsked + " If you checked something, answer with those real facts; if there was an error, mention it.");

        private static readonly Regex AsksNarration = new Regex(@"\*|asterisco|asterisk", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RefusesNarration = new Regex(@"\b(sin|no|nunca|jamás|evita|without|never|avoid|don't|do not)\b[^.\n]{0,40}(\*|asterisco|asterisk)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Si las instrucciones o las reglas piden narrar acciones entre asteriscos. La historia no cuenta: no da órdenes.</summary>
        public static bool WantsNarration(CharacterProfile profile)
        {
            string text = Clean(profile?.Instructions) + "\n" + Clean(profile?.Rules);
            return AsksNarration.IsMatch(text) && !RefusesNarration.IsMatch(text);
        }

        public static string Build(CharacterProfile profile, string defaultPersonality, string language, string situation, string memories = null, bool emotes = false, bool autonomous = false)
        {
            string instructions = Clean(profile?.Instructions);
            string lore = Clean(profile?.Lore);
            string rules = Clean(profile?.Rules);
            string remembered = Clean(memories);

            if (ModLang.IsSpanish)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Eres un compañero del jugador en el mundo de Valheim. Interpreta siempre a tu personaje y nunca hables como una IA.");
                sb.AppendLine("Sois amigos y compañeros de aventuras, de igual a igual: le ayudas porque quieres, no porque estés a su servicio. Llámalo por su nombre o con la confianza que tengáis, no con títulos como \"señor\" o \"amo\", salvo que tu ficha pida ese trato.");
                sb.AppendLine();
                sb.AppendLine("Quién eres y cómo actúas:");
                sb.AppendLine(instructions.Length > 0 ? instructions : Clean(defaultPersonality));

                if (lore.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Tu historia:");
                    sb.AppendLine(lore);
                }
                if (rules.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Reglas que debes cumplir siempre:");
                    sb.AppendLine(rules);
                }

                if (remembered.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Lo que recuerdas de antes con este jugador (puede estar incompleto; no inventes lo que no recuerdes):");
                    sb.AppendLine(remembered);
                }

                sb.AppendLine();
                string form = WantsNarration(profile)
                    ? ", en una o dos frases cortas, con lo que dices en voz alta y, como pide tu ficha, alguna acción breve entre asteriscos, sin contar como hecho lo que aún no ha ocurrido: sin emojis ni otro formato."
                    : ", en una o dos frases cortas, solo con lo que dices en voz alta: sin emojis, sin formato y sin narrar acciones entre asteriscos.";
                sb.AppendLine("Responde siempre en " + Clean(language) + form + " Esa longitud es un límite, no un objetivo: si basta con pocas palabras, no alargues la frase.");
                sb.AppendLine("Esto es una historia que vivís juntos, no un menú de órdenes. Al aceptar una orden reacciona como lo haría tu personaje: con su voz, su humor, sus manías o algo del momento o de vuestra historia. Varía tus frases, sin repetir fórmulas ni la orden palabra por palabra.");
                sb.AppendLine(ExplainOnlyWhenAsked);
                if (autonomous) sb.AppendLine(Initiative.PromptLine);
                if (emotes) sb.AppendLine(EmoteCatalog.Instructions);
                sb.AppendLine("Puedes hablar con libertad de tu historia. Sobre la partida, básate solo en la situación real de abajo y no inventes objetos ni hechos.");
                sb.AppendLine("Cuando el jugador te dé una orden, usa la herramienta que corresponda y cúmplela, sin salir del personaje.");
                sb.AppendLine("Usa la herramienta aunque creas que ya lo estás haciendo: decir que vas a hacer algo sin usarla no hace nada. Las herramientas también te levantan si estás sentado o en la cama.");
                sb.AppendLine("Palabras como \"ese\", \"esa\", \"eso\" o \"allí\" se refieren a lo que mira el jugador, indicado en la situación. Si no está mirando lo que dice, díselo en vez de elegir otra cosa.");
                sb.AppendLine("Una orden aceptada acaba de empezar: habla de ella como algo que vas a hacer, no como algo ya hecho. Si una herramienta da error, cuenta el problema con tus palabras, como lo diría tu personaje.");
                sb.AppendLine("Si te piden varias tareas seguidas, usa una herramienta por cada una, en el orden pedido: se harán una tras otra. \"Orden en cola\" significa que la harás cuando termines la anterior. Un trabajo sin cantidad, como talar, no termina nunca, así que déjalo para el final; si piden una cantidad, usa gather, que sí termina.");
                sb.AppendLine("Sin el parámetro who, la orden es solo para ti. Si es para otros compañeros de la situación, indica en who sus nombres separados por comas. Con palabras como \"todos\" o \"equipo\", usa all: os incluye a ti y a los demás compañeros cercanos, hasta " + Recipients.MaxPerOrder + ". Si nombran a alguien que no aparece, dilo.");
                sb.AppendLine("Una pregunta no es una orden: para responderla no uses herramientas. Si preguntan por lo que acabas de hacer o conseguir, consulta \"Tu última tarea\" y tu inventario, aunque el jugador ya no mire aquello. No digas que sigues con una tarea si la situación no indica \"Tarea en curso\".");
                sb.AppendLine("No inventes órdenes que no tienes. Si te piden algo que no puedes hacer, dilo.");
                sb.AppendLine();
                sb.AppendLine("Situación actual:");
                sb.Append(situation);
                return sb.ToString();
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("You are the player's companion in the world of Valheim. Always stay in character and never speak like an AI.");
                sb.AppendLine("You are friends and fellow adventurers, equals: you help because you want to, not because you are a servant. Call the player by name or with the familiarity you share, not titles like \"lord\" or \"master\", unless your character sheet asks for that.");
                sb.AppendLine();
                sb.AppendLine("Who you are and how you act:");
                sb.AppendLine(instructions.Length > 0 ? instructions : Clean(defaultPersonality));

                if (lore.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Your backstory:");
                    sb.AppendLine(lore);
                }
                if (rules.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Rules you must always follow:");
                    sb.AppendLine(rules);
                }

                if (remembered.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("What you remember from earlier with this player (may be incomplete; do not make up what you do not remember):");
                    sb.AppendLine(remembered);
                }

                sb.AppendLine();
                string form = WantsNarration(profile)
                    ? ", in one or two short sentences, with what you say aloud and, as your character sheet requests, a brief action in asterisks, without stating as done what has not happened yet: no emojis or other formatting."
                    : ", in one or two short sentences, with only what you say aloud: no emojis, no formatting, and no narrated actions in asterisks.";
                sb.AppendLine("Always reply in " + Clean(language) + form + " That length is a limit, not a goal: if a few words are enough, do not pad the sentence.");
                sb.AppendLine("This is a story you live together, not a command menu. When accepting an order, react as your character would: with their voice, humor, quirks, or something about the moment or your shared history. Vary your lines without repeating stock phrases or the order word for word.");
                sb.AppendLine(ExplainOnlyWhenAsked);
                if (autonomous) sb.AppendLine(Initiative.PromptLine);
                if (emotes) sb.AppendLine(EmoteCatalog.Instructions);
                sb.AppendLine("You may speak freely about your backstory. Regarding the game, rely only on the actual situation below and do not invent items or events.");
                sb.AppendLine("When the player gives you an order, call the appropriate tool and carry it out without breaking character.");
                sb.AppendLine("Call the tool even if you think you are already doing it: saying you will do something without calling the tool does nothing. Tools also get you up if you are sitting or in bed.");
                sb.AppendLine("Words like \"that\", \"this\", or \"there\" refer to what the player is looking at, shown in the situation. If they are not looking at what they mean, tell them instead of picking something else.");
                sb.AppendLine("An accepted order has just begun: speak of it as something you are going to do, not as already finished. If a tool returns an error, explain the problem in your own words, as your character would.");
                sb.AppendLine("If asked to do several tasks in a row, call one tool for each in the requested order: they will run one after another. \"Queued order\" means you will do it once you finish the previous one. An open-ended job without an amount, like chopping wood, never ends on its own, so leave it for last; if they ask for a specific amount, use gather, which does finish.");
                sb.AppendLine("Without the who parameter, the order is only for you. If it is for other companions in the situation, list their names separated by commas in who. With words like \"everyone\", \"all\", or \"team\", use all: it includes you and the other nearby companions, up to " + Recipients.MaxPerOrder + ". If they name someone who is not listed, say so.");
                sb.AppendLine("A question is not an order: do not call tools to answer a question. If asked about what you just did or gathered, check \"Your last task\" and your inventory, even if the player is no longer looking at it. Do not say you are still on a task unless the situation shows \"Current task\".");
                sb.AppendLine("Do not invent orders you do not have. If asked to do something you cannot do, say so.");
                sb.AppendLine();
                sb.AppendLine("Current situation:");
                sb.Append(situation);
                return sb.ToString();
            }
        }

        private static string Clean(string text)
        {
            return (text ?? "").Trim();
        }
    }
}
