using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OfflineCompanionsAI
{
    /// <summary>A qué compañeros se dirige una orden. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class Recipients
    {
        public const int MaxPerOrder = 4;
        public const string All = "all";
        private const string ShoutPrefix = "/s ";

        /// <summary>
        /// Devuelve los índices en names, que va ordenada con quien habla primero.
        /// who: vacío = solo quien habla; "all" = todos los cercanos; o nombres separados por comas.
        /// </summary>
        public static List<int> Resolve(string who, IList<string> names, out string unknown)
        {
            unknown = null;
            var result = new List<int>();
            if (names == null || names.Count == 0) return result;

            who = (who ?? "").Trim();
            if (who.Length == 0)
            {
                result.Add(0);
                return result;
            }
            if (string.Equals(who, All, StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < names.Count && i < MaxPerOrder; i++) result.Add(i);
                return result;
            }

            foreach (string part in who.Split(','))
            {
                string wanted = part.Trim();
                if (wanted.Length == 0) continue;
                bool found = false;
                for (int i = 0; i < names.Count; i++)
                {
                    if (!string.Equals(names[i], wanted, StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    if (!result.Contains(i) && result.Count < MaxPerOrder) result.Add(i);
                }
                if (!found && unknown == null) unknown = wanted;
            }
            return result;
        }

        /// <summary>El compañero nombrado primero en el mensaje, para que responda él y no el más cercano. -1 si no se nombra a ninguno.</summary>
        public static int FindAddressed(string text, IList<string> names)
        {
            int best = -1;
            int bestPosition = int.MaxValue;
            for (int i = 0; i < names.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(names[i])) continue;
                Match match = Regex.Match(text ?? "", @"(?<!\w)" + Regex.Escape(names[i]) + @"(?!\w)", RegexOptions.IgnoreCase);
                if (match.Success && match.Index < bestPosition)
                {
                    best = i;
                    bestPosition = match.Index;
                }
            }
            return best;
        }

        private static readonly HashSet<string> GroupWords = new HashSet<string>
        {
            "todos", "todas", "ustedes", "ambos", "ambas", "chicos", "chicas",
            "everyone", "everybody", "both", "guys", "yall",
            "все", "оба", "обе", "ребята", "вдвоем"
        };
        private static readonly string[] GroupPhrases = { "los dos", "las dos", "all of you", "you two", "you all", "вы оба", "вы обе", "вы двое" };
        // Palabras que acaban como un plural de "ustedes" sin serlo.
        private static readonly HashSet<string> NotPlural = new HashSet<string>
        {
            "tambien", "quien", "alguien", "bien", "recien", "orden", "joven", "imagen", "examen", "origen", "almacen", "margen", "resumen", "volumen"
        };

        /// <summary>
        /// Si el jugador habla a varios compañeros a la vez: nombra a dos o más, usa "todos", "ustedes", "both" o "все",
        /// o empieza con un verbo en plural, como "marquen", "síganme" o "отметьте".
        /// </summary>
        public static bool AddressesSeveral(string text, IList<string> names)
        {
            var nameOf = new Dictionary<string, int>();
            for (int n = 0; names != null && n < names.Count; n++)
            {
                if (string.IsNullOrWhiteSpace(names[n])) continue;
                foreach (string part in OrderIntent.Normalize(names[n]).Split(' ')) nameOf[part] = n;
            }

            string plain = " " + OrderIntent.Normalize(text) + " ";
            foreach (string phrase in GroupPhrases)
            {
                if (plain.Contains(" " + phrase + " ")) return true;
            }
            // Los nombres solo cuentan al principio, como en "Roxy y Darkness, vengan": "Darkness, ¿conoces a Roxy?" es para uno.
            var leading = new HashSet<int>();
            string first = null;
            foreach (string word in plain.Trim().Split(' '))
            {
                if (word.Length == 0) continue;
                if (GroupWords.Contains(word)) return true;
                if (first != null) continue;
                if (nameOf.TryGetValue(word, out int who)) leading.Add(who);
                else if (word != "y" && word != "e" && word != "and" && word != "и") first = word;
            }
            if (leading.Count >= 2) return true;
            if (first == null || first.Length < 5 || NotPlural.Contains(first)) return false;
            return first.EndsWith("en") || first.EndsWith("an") || first.EndsWith("nse") || first.EndsWith("nme") || first.EndsWith("те");
        }

        /// <summary>
        /// Si lo escrito en el chat del juego es para los compañeros. Con otros jugadores en la partida debe nombrarlos,
        /// para no enviar a la IA conversaciones entre personas.
        /// </summary>
        public static bool GameChatIsForCompanions(string text, IList<string> names, bool otherPlayersOnline)
        {
            return !otherPlayersOnline || FindAddressed(text, names) >= 0;
        }

        /// <summary>"/s mensaje" es como se grita en el chat de Valheim. Devuelve false si no es un grito o no dice nada.</summary>
        public static bool TryParseShout(string text, out string message)
        {
            message = null;
            string clean = (text ?? "").Trim();
            if (!clean.StartsWith(ShoutPrefix, StringComparison.OrdinalIgnoreCase)) return false;
            message = clean.Substring(ShoutPrefix.Length).Trim();
            return message.Length > 0;
        }
    }

    /// <summary>Compara nombres de objetos como los escribe una persona: "flechas" también es "Flecha de madera".</summary>
    public static class ItemNames
    {
        private static readonly HashSet<string> AmmoWords = new HashSet<string>
        {
            "flecha", "arrow", "municion", "municione", "ammo", "ammunition", "proyectil", "proyectile", "projectile", "virote", "saeta", "bolt", "dardo", "dart",
            "стрела", "стрелы", "стрел", "боеприпасы", "патроны", "снаряды", "болт", "болты", "дротик", "дротики"
        };

        private static readonly HashSet<string> FoodWords = new HashSet<string>
        {
            "comida", "food", "meal", "ration", "alimento", "comestible", "provisione", "vivere", "sustento",
            "algo de comer", "algo para comer", "de comer", "para comer", "something to eat",
            "еда", "пища", "провизия", "провиант", "съестное", "что нибудь поесть", "что то поесть", "поесть", "перекусить", "еды"
        };

        private static readonly string[][] SynonymGroups = new[]
        {
            new[] { "seta", "hongo", "mushroom", "гриб", "грибы" },
            new[] { "seta amarilla", "hongo amarillo", "yellow mushroom", "mushroomyellow", "желтый гриб", "желтые грибы" },
            new[] { "seta azul", "seta azule", "hongo azul", "hongo azule", "blue mushroom", "mushroomblue", "голубой гриб", "голубые грибы", "синий гриб" },
            new[] { "silex", "pedernal", "flint", "кремень", "кремня" },
            new[] { "madera noble", "madera fina", "fine wood", "finewood", "качественная древесина", "ценная древесина" },
            new[] { "madera resistente", "madera dura", "madera de nucleo", "tronco redondo", "core wood", "roundlog", "цельная древесина", "кругляк" },
            new[] { "chatarra", "chatarra de hierro", "mineral de hierro", "scrap iron", "ironscrap", "металлолом", "железный металлолом" },
            new[] { "trozo de cuero", "retazo de cuero", "tira de cuero", "recorte de cuero", "leather scrap", "leatherscrap", "кожаные обрывки", "обрывки кожи" },
            new[] { "mora de lo pantano", "mora del pantano", "camemoro", "zarzamora", "cloudberry", "cloudberrie", "морошка", "морошки" },
            new[] { "carne de luey", "carne de lox", "lox meat", "loxmeat", "мясо быкоящера", "быкоящер" },
            new[] { "cola de nixe", "cola de nix", "cola de neck", "neck tail", "necktail", "хвост никса", "хвосты никса" },
            new[] { "carne asada", "carne cocida", "carne cocinada", "cooked meat", "cookedmeat", "жареное мясо", "приготовленное мясо" },
            new[] { "carne de ciervo", "carne de venado", "deer meat", "deermeat", "оленина", "мясо оленя" },
            new[] { "piel de ciervo", "piel de venado", "cuero de ciervo", "cuero de venado", "deer hide", "deerhide", "шкура оленя" },
            new[] { "arandano", "mora azul", "mora azule", "blueberry", "blueberrie", "черника", "черники" },
            new[] { "carne de jabali", "carne de puerco", "carne de cerdo", "boar meat", "кабанина", "мясо кабана" }
        };

        public static bool Matches(string label, string wanted)
        {
            string a = Stem(label);
            string b = Stem(wanted);
            if (a.Length == 0 || b.Length == 0) return false;
            if (a.IndexOf(b, StringComparison.Ordinal) >= 0 || b.IndexOf(a, StringComparison.Ordinal) >= 0) return true;
            foreach (string[] group in SynonymGroups)
            {
                bool hasA = false;
                bool hasB = false;
                foreach (string raw in group)
                {
                    // Igual que el texto del jugador: sin acentos, y en ruso "й" pasa a "и" y "ё" a "е".
                    string term = OrderIntent.Normalize(raw);
                    if (a == term || a.IndexOf(term, StringComparison.Ordinal) >= 0) hasA = true;
                    if (b == term || b.IndexOf(term, StringComparison.Ordinal) >= 0) hasB = true;
                }
                if (hasA && hasB) return true;
            }
            return false;
        }

        /// <summary>Si piden munición en general, sin nombrar ninguna en concreto.</summary>
        public static bool IsAmmoWord(string wanted)
        {
            string stem = Stem(wanted);
            foreach (string word in new[] { " para el arco", " para la ballesta", " para arco", " para ballesta", " for the bow", " for the crossbow", " for bow", " for crossbow", " для лука", " для арбалета", " к луку", " к арбалету" })
            {
                if (stem.EndsWith(word, StringComparison.Ordinal)) stem = stem.Substring(0, stem.Length - word.Length);
            }
            return AmmoWords.Contains(stem);
        }

        /// <summary>Si piden comida en general, sin nombrar ninguna en concreto.</summary>
        public static bool IsFoodWord(string wanted)
        {
            return FoodWords.Contains(Stem(wanted));
        }

        /// <summary>
        /// El objeto que el jugador nombra con todas sus letras, entre los que se dan como nombre y sus etiquetas en los
        /// tres idiomas. Si un nombre contiene a otro ("madera resistente" y "madera"), gana el más largo. Null si no nombra
        /// ninguno o nombra varios distintos.
        /// </summary>
        public static string NamedIn(string playerText, IEnumerable<KeyValuePair<string, string[]>> items)
        {
            string text = " " + Stem(playerText) + " ";
            string best = null;
            string bestTerm = "";
            bool tie = false;
            var found = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string[]> item in items)
            {
                var terms = new List<string>();
                foreach (string label in item.Value) terms.Add(Stem(label));
                foreach (string[] group in SynonymGroups)
                {
                    bool mine = false;
                    foreach (string raw in group) if (terms.Contains(Stem(raw))) mine = true;
                    if (mine) foreach (string raw in group) terms.Add(Stem(raw));
                }
                foreach (string term in terms)
                {
                    if (term.Length > 0 && text.IndexOf(" " + term + " ", StringComparison.Ordinal) >= 0) found.Add(new KeyValuePair<string, string>(item.Key, term));
                }
            }
            foreach (KeyValuePair<string, string> hit in found)
            {
                // Un nombre que solo aparece dentro de otro más largo no cuenta por separado.
                bool inside = false;
                foreach (KeyValuePair<string, string> other in found)
                {
                    if (other.Key != hit.Key && other.Value.Length > hit.Value.Length && (" " + other.Value + " ").IndexOf(" " + hit.Value + " ", StringComparison.Ordinal) >= 0) inside = true;
                }
                if (inside) continue;
                if (best == null || best == hit.Key) { best = hit.Key; if (hit.Value.Length > bestTerm.Length) bestTerm = hit.Value; }
                else tie = true;
            }
            return tie ? null : best;
        }

        private static string Stem(string text)
        {
            string[] words = OrderIntent.Normalize(text).Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 3 && words[i].EndsWith("s", StringComparison.Ordinal)) words[i] = words[i].Substring(0, words[i].Length - 1);
            }
            return string.Join(" ", words);
        }
    }

    /// <summary>Una comida a su alcance: cuánta hay en los cofres cercanos y cuánta lleva encima.</summary>
    public sealed class FoodStock
    {
        public string Item;
        public int InChests;
        public int Carried;
        public float Value;
    }

    /// <summary>
    /// Cuánta comida coger de los cofres sin dejar al jugador sin ella. Se comen hasta tres comidas distintas a la vez,
    /// así que busca variedad y no una sola en cantidad.
    /// </summary>
    public static class FoodPlan
    {
        public const int Kinds = 3;
        public const int SharePercent = 20;

        /// <summary>Al menos una, para que pueda comer aunque quede poca.</summary>
        public static int ShareOf(int stock)
        {
            return stock <= 0 ? 0 : (stock * SharePercent + 99) / 100;
        }

        /// <summary>
        /// Repone las que ya lleva y completa con las mejores que le falten. Con una cantidad total, la reparte entre ellas
        /// en lugar de calcular su parte.
        /// </summary>
        public static Dictionary<string, int> Choose(IEnumerable<FoodStock> foods, int total = 0)
        {
            var all = new List<FoodStock>(foods);
            var picks = all.FindAll(food => food.Carried > 0 && food.InChests > 0);
            int carriedKinds = all.FindAll(food => food.Carried > 0).Count;
            var fresh = all.FindAll(food => food.Carried == 0 && food.InChests > 0);
            fresh.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < fresh.Count && carriedKinds + i < Kinds; i++) picks.Add(fresh[i]);

            var plan = new Dictionary<string, int>();
            int left = total;
            for (int i = 0; i < picks.Count; i++)
            {
                int amount = total > 0
                    ? Math.Min(picks[i].InChests, (left + picks.Count - i - 1) / (picks.Count - i))
                    : ShareOf(picks[i].InChests) - picks[i].Carried;
                left -= amount;
                if (amount > 0) plan[picks[i].Item] = amount;
            }
            return plan;
        }
    }

    /// <summary>Puntos cardinales como los del mapa del juego, donde el norte es +z y el este +x.</summary>
    public static class Compass
    {
        private static readonly string[] NamesEs = { "norte", "noreste", "este", "sureste", "sur", "suroeste", "oeste", "noroeste" };
        private static readonly string[] NamesEn = { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
        private static readonly string[] NamesRu = { "север", "северо-восток", "восток", "юго-восток", "юг", "юго-запад", "запад", "северо-запад" };

        public static string Direction(float dx, float dz)
        {
            string[] names = ModLang.IsSpanish ? NamesEs : (ModLang.IsRussian ? NamesRu : NamesEn);
            double degrees = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            int sector = (int)Math.Round((degrees + 360.0) / 45.0) % names.Length;
            return names[sector];
        }
    }

    /// <summary>
    /// Red de seguridad: órdenes tan claras y comunes que deben cumplirse aunque el modelo responda sin usar la herramienta.
    /// Solo reconoce mensajes que no son más que la orden, para no confundir una frase normal con una orden.
    /// </summary>
    public static class OrderIntent
    {
        private static readonly HashSet<string> ComeHere = BuildNormalizedSet(
            // Español
            "sigueme", "seguime", "sigueme por favor", "ven", "ven aqui", "ven aca", "ven conmigo", "vente", "vente conmigo", "vamonos", "vamos",
            "siguenme", "siganme", "vengan", "vengan aqui", "vengan aca", "vengan conmigo", "vuelve", "vuelve aqui", "vuelve conmigo", "regresa", "regresa aqui",
            // English
            "follow me", "follow me please", "come", "come here", "come with me", "let s go", "lets go", "come back", "come back here", "on me",
            // Русский
            "следуй за мной", "следуй за мною", "иди за мной", "иди сюда", "ко мне", "за мной", "пошли", "пойдем", "пойдём", "идем", "идём", "вернись", "вернись сюда", "возвращайся", "возвращайся сюда", "пожалуйста иди сюда", "за мной пожалуйста"
        );

        private static readonly HashSet<string> Stop = BuildNormalizedSet(
            // Español
            "para", "paren", "detente", "detenganse", "alto", "cancela", "cancela eso", "deja eso",
            // English
            "stop", "halt", "cancel", "cancel that", "drop it", "leave that", "stop that",
            // Русский
            "стой", "стоп", "остановись", "остановитесь", "хватит", "отмена", "отмени", "отмени это", "брось это", "прекрати", "оставь это", "хватит это делать"
        );

        private static readonly HashSet<string> Stay = BuildNormalizedSet(
            // Español
            "quedate", "quedate aqui", "quedate ahi", "quedate alli", "quieto", "quieta", "quedense", "quedense aqui", "esperame aqui", "espera aqui", "esperen aqui",
            // English
            "stay", "stay here", "stay there", "wait here", "wait for me here", "hold position", "hold here",
            // Русский
            "стой здесь", "останься", "останься здесь", "оставайся здесь", "жди здесь", "подожди здесь", "жди меня здесь", "держи позицию", "ни с места", "жди тут", "оставайся тут"
        );

        private static readonly HashSet<string> Everyone = BuildNormalizedSet(
            "todos", "todas", "equipo", "chicos",
            "everyone", "all", "team", "guys", "everybody",
            "все", "всё", "всем", "команда", "ребята", "народ", "бойцы", "друзья"
        );

        private static HashSet<string> BuildNormalizedSet(params string[] items)
        {
            var set = new HashSet<string>();
            foreach (string item in items)
            {
                set.Add(Normalize(item));
            }
            return set;
        }

        /// <summary>Devuelve la llamada equivalente, o null si el mensaje no es solo una de estas órdenes.</summary>
        public static ToolCall Detect(string text, IEnumerable<string> names)
        {
            string clean = Normalize(text);
            string who = null;
            foreach (string word in Everyone)
            {
                if (clean == word || clean.StartsWith(word + " ", StringComparison.Ordinal))
                {
                    clean = clean.Substring(word.Length).Trim();
                    who = Recipients.All;
                    break;
                }
            }
            if (who == null && names != null)
            {
                foreach (string name in names)
                {
                    string plain = Normalize(name);
                    if (plain.Length == 0) continue;
                    // Vale tanto "darkness ven" como "ven darkness".
                    if (clean.StartsWith(plain + " ", StringComparison.Ordinal)) clean = clean.Substring(plain.Length).Trim();
                    else if (clean.EndsWith(" " + plain, StringComparison.Ordinal)) clean = clean.Substring(0, clean.Length - plain.Length).Trim();
                    else continue;
                    who = name;
                    break;
                }
            }

            string tool = ComeHere.Contains(clean) ? "come_here" : Stop.Contains(clean) ? "cancel_orders" : Stay.Contains(clean) ? "set_follow" : null;
            if (tool == null) return null;
            var args = new Newtonsoft.Json.Linq.JObject();
            if (tool == "set_follow") args["follow"] = false;
            if (who != null) args["who"] = who;
            return new ToolCall { Id = "respaldo", Name = tool, Arguments = args.ToString(Newtonsoft.Json.Formatting.None) };
        }

        internal static string Normalize(string text)
        {
            string decomposed = (text ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            bool space = false;
            foreach (char c in decomposed)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c))
                {
                    if (space && sb.Length > 0) sb.Append(' ');
                    space = false;
                    sb.Append(c);
                }
                else
                {
                    space = true;
                }
            }
            return sb.ToString();
        }
    }

    public enum GoalState
    {
        Working,
        Reached,
        Stuck,
        TimedOut
    }

    /// <summary>
    /// Una meta como "consigue 10 piedras". Cuenta lo que entra en la mochila, sin restar lo que deje en un cofre,
    /// para que acercarse a otro cofre o vaciar la mochila no altere el progreso.
    /// </summary>
    public sealed class GatherGoal
    {
        public const float StuckSeconds = 60f;
        public const float StuckWithSomeSeconds = 15f;
        public const float TimeoutSeconds = 900f;

        private float _startedAt;
        private int _lastPack;
        private float _lastActiveAt;

        public readonly string Item;
        public readonly string Label;
        public readonly int Amount;
        public int Gained { get; private set; }

        private readonly float _stuckSeconds;
        private readonly float _stuckWithSomeSeconds;

        public GatherGoal(string item, string label, int amount, int packNow, float now, float stuckSeconds = StuckSeconds, float stuckWithSomeSeconds = StuckWithSomeSeconds)
        {
            _stuckSeconds = stuckSeconds;
            _stuckWithSomeSeconds = stuckWithSomeSeconds;
            Item = item;
            Label = label;
            Amount = amount;
            _lastPack = packNow;
            _startedAt = now;
            _lastActiveAt = now;
        }

        private float _lastCheckAt = -1f;

        /// <summary>Si sigue en pausa desde la última comprobación.</summary>
        public bool Paused { get; private set; }

        /// <summary>
        /// El tiempo con el jugador demasiado lejos no cuenta: el juego no mueve a un compañero fuera de la zona que simula.
        /// </summary>
        public void Pause(float now)
        {
            if (_lastCheckAt >= 0f && now > _lastCheckAt)
            {
                _startedAt += now - _lastCheckAt;
                _lastActiveAt += now - _lastCheckAt;
            }
            _lastCheckAt = now;
            Paused = true;
        }

        public GoalState Update(int packNow, bool working, float now)
        {
            _lastCheckAt = now;
            Paused = false;
            if (packNow > _lastPack)
            {
                Gained += packNow - _lastPack;
                _lastActiveAt = now;
            }
            _lastPack = packNow;

            if (Gained >= Amount) return GoalState.Reached;
            if (working) _lastActiveAt = now;
            if (now - _startedAt >= TimeoutSeconds) return GoalState.TimedOut;
            // Si ya tiene algo y no queda más, no merece la pena hacer esperar al jugador un minuto.
            float patience = Gained > 0 ? _stuckWithSomeSeconds : _stuckSeconds;
            return now - _lastActiveAt >= patience ? GoalState.Stuck : GoalState.Working;
        }
    }

    /// <summary>Una pila que lleva encima y que puede guardar.</summary>
    public sealed class CarriedStack
    {
        public string Item;
        public int Count;
        public int MaxStack = 1;
    }

    /// <summary>Lo que se sabe de un cofre al decidir adónde llevar las cosas.</summary>
    public sealed class ChestSpace
    {
        public int Id;
        public float Distance;
        public int FreeSlots;
        public readonly HashSet<string> Holds = new HashSet<string>();
        public readonly Dictionary<string, int> RoomInStacks = new Dictionary<string, int>();
    }

    /// <summary>Un viaje a un cofre, llevando solo esos objetos.</summary>
    public sealed class StoreTrip
    {
        public int Chest;
        public HashSet<string> Items;
    }

    /// <summary>
    /// Decide adónde lleva lo que tiene que guardar: primero al cofre donde ya hay más de lo que lleva, y lo que no tenga
    /// sitio en ninguno así, al cofre más cercano con huecos. El juego deja en la mochila lo que no cabe, sin avisar.
    /// </summary>
    public static class StorePlanner
    {
        public static StoreTrip Next(IList<CarriedStack> carried, IList<ChestSpace> chests, ICollection<int> refused)
        {
            ChestSpace best = null;
            HashSet<string> bestItems = null;
            foreach (ChestSpace chest in chests)
            {
                if (refused != null && refused.Contains(chest.Id)) continue;
                HashSet<string> items = WhatFits(carried, chest, true);
                if (items.Count == 0) continue;
                if (best == null || items.Count > bestItems.Count || (items.Count == bestItems.Count && chest.Distance < best.Distance))
                {
                    best = chest;
                    bestItems = items;
                }
            }
            if (best != null) return new StoreTrip { Chest = best.Id, Items = bestItems };

            foreach (ChestSpace chest in chests)
            {
                if (refused != null && refused.Contains(chest.Id) || chest.FreeSlots <= 0) continue;
                HashSet<string> items = WhatFits(carried, chest, false);
                if (items.Count == 0) continue;
                if (best == null || chest.Distance < best.Distance)
                {
                    best = chest;
                    bestItems = items;
                }
            }
            return best == null ? null : new StoreTrip { Chest = best.Id, Items = bestItems };
        }

        /// <summary>Qué le cabe, aunque sea en parte: lo que sobre de una pila se queda en la mochila para el viaje siguiente.</summary>
        public static HashSet<string> WhatFits(IList<CarriedStack> carried, ChestSpace chest, bool onlyWhatItHolds)
        {
            var items = new HashSet<string>();
            var room = new Dictionary<string, int>(chest.RoomInStacks);
            int free = chest.FreeSlots;
            foreach (CarriedStack stack in carried)
            {
                if (onlyWhatItHolds && !chest.Holds.Contains(stack.Item)) continue;
                room.TryGetValue(stack.Item, out int inStacks);
                int left = stack.Count - inStacks;
                room[stack.Item] = Math.Max(0, -left);
                if (left > 0 && free > 0)
                {
                    free--;
                    room[stack.Item] = Math.Max(0, stack.MaxStack - left);
                }
                else if (left > 0 && inStacks == 0)
                {
                    continue;
                }
                items.Add(stack.Item);
            }
            return items;
        }

        public static int Units(IList<CarriedStack> carried)
        {
            int total = 0;
            foreach (CarriedStack stack in carried) total += stack.Count;
            return total;
        }
    }

    /// <summary>"Guarda tus cosas": lleva la cuenta de los viajes y de los cofres que no han servido para no volver a ellos.</summary>
    public sealed class StoreGoal
    {
        public const int MaxTrips = 24;
        public const float TimeoutSeconds = 300f;

        private readonly float _startedAt;
        private readonly HashSet<int> _used = new HashSet<int>();
        private int _units;
        private int _current;

        public readonly int StartUnits;
        public readonly HashSet<int> Refused = new HashSet<int>();
        public int Trips { get; private set; }
        public int ChestsUsed { get { return _used.Count; } }

        public StoreGoal(int units, float now)
        {
            StartUnits = units;
            _units = units;
            _startedAt = now;
        }

        public void TripStarted(int chest)
        {
            _current = chest;
            Trips++;
        }

        /// <summary>Un viaje sin dejar nada, porque otro cogió el sitio o no pudo abrirlo, descarta ese cofre.</summary>
        public bool TripEnded(int unitsNow)
        {
            bool progress = unitsNow < _units;
            if (progress) _used.Add(_current);
            else Refused.Add(_current);
            _units = unitsNow;
            return progress;
        }

        public bool TimedOut(float now)
        {
            return now - _startedAt >= TimeoutSeconds;
        }

        public bool CanContinue(float now)
        {
            return _units > 0 && Trips < MaxTrips && !TimedOut(now);
        }
    }

    /// <summary>
    /// Nadando sin aguante, Offline Companions lo manda a la orilla por encima de cualquier orden. Si no llega, lo deja
    /// a los 25 s y vuelve a empezar sin fin, porque nadando no se recupera. A la orilla más lejana que busca se llega en
    /// ese tiempo, así que el doble largo ya es estar atrapado y no un nado largo.
    /// También lo olvida en cuanto hace pie, y su trabajo lo devuelve al agua sin haber recuperado nada: por eso aquí salir
    /// un momento no cuenta, y hasta recuperarse tiene que quedarse sin nadar.
    /// </summary>
    public sealed class WaterTrapWatch
    {
        public const float TrappedSeconds = 45f;
        public const float AshoreSeconds = 10f;
        public const int RetreatsToLongRest = 3;
        public const float RetreatWindowSeconds = 120f;
        public const float LongRestSeconds = 60f;
        private const float RecoveredFactor = 2f;
        private const float NotWatching = -1f;

        private float _since = NotWatching;
        private float _lastSwimAt;
        private bool _tired;
        private int _retreats;
        private float _firstRetreatAt;
        private float _longRestUntil;

        /// <summary>True si lleva tanto tiempo apurado que hay que sacarlo de ahí.</summary>
        public bool Update(bool swimming, float staminaShare, float tiredShare, float now)
        {
            bool recovered = staminaShare >= tiredShare * RecoveredFactor;
            if (recovered) _tired = false;
            else if (swimming && staminaShare < tiredShare && !_tired)
            {
                _tired = true;
                CountRetreat(now);
            }

            if (swimming) _lastSwimAt = now;
            if (recovered || (!swimming && now - _lastSwimAt >= AshoreSeconds))
            {
                _since = NotWatching;
                return false;
            }
            if (!swimming) return false;
            if (_since == NotWatching)
            {
                if (staminaShare >= tiredShare) return false;
                _since = now;
            }
            return now - _since >= TrappedSeconds;
        }

        /// <summary>Si no debe meterse a nadar por su cuenta: aún no se ha recuperado, o ya ha vuelto agotado varias veces.</summary>
        public bool StayDry(float now)
        {
            return _tired || OnLongRest(now);
        }

        /// <summary>Lo que intenta alcanzar queda demasiado lejos a nado, y volver a probar enseguida acabaría igual.</summary>
        public bool OnLongRest(float now)
        {
            return now < _longRestUntil;
        }

        public void Reset()
        {
            _since = NotWatching;
        }

        private void CountRetreat(float now)
        {
            if (_retreats == 0 || now - _firstRetreatAt > RetreatWindowSeconds)
            {
                _retreats = 0;
                _firstRetreatAt = now;
            }
            if (++_retreats < RetreatsToLongRest) return;
            _retreats = 0;
            _longRestUntil = now + LongRestSeconds;
        }
    }

    public sealed class PlaceMark
    {
        public string Name;
        public float X;
        public float Y;
        public float Z;
    }

    /// <summary>Los lugares marcados de un mundo. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public sealed class PlaceBook
    {
        public const int MaxPlaces = 30;

        private static readonly string[] Articles =
        {
            "el ", "la ", "los ", "las ", "mi ", "tu ", "nuestra ", "nuestro ",
            "the ", "my ", "your ", "our ",
            "наш ", "наша ", "наше ", "наши ", "мой ", "моя ", "мое ", "моё ", "мои "
        };
        private static readonly HashSet<string> HomeWords = new HashSet<string> {
            "base", "casa", "hogar", "home", "house", "camp",
            "база", "базу", "базе", "дом", "дома", "домой", "лагерь", "жилище"
        };

        public List<PlaceMark> Places = new List<PlaceMark>();

        /// <summary>Solo busca por nombre exacto: es lo que decide si marcar uno lo mueve o crea otro.</summary>
        public PlaceMark Exact(string name)
        {
            string wanted = Plain(name);
            return Places.Find(place => Plain(place.Name) == wanted);
        }

        /// <summary>Tolera "la base" o "mina" por "mina de cobre". Si dos encajan por igual, no adivina cuál es.</summary>
        public PlaceMark Find(string name)
        {
            string wanted = Plain(name);
            if (wanted.Length == 0) return null;
            PlaceMark exact = Exact(name);
            if (exact != null) return exact;

            List<PlaceMark> partial = Places.FindAll(place => Plain(place.Name).Contains(wanted) || wanted.Contains(Plain(place.Name)));
            if (partial.Count == 1) return partial[0];
            return partial.Count == 0 && IsHomeWord(name) ? Places.Find(place => IsHomeWord(place.Name)) : null;
        }

        /// <summary>False si ya no caben más.</summary>
        public bool Set(string name, float x, float y, float z)
        {
            PlaceMark place = Exact(name);
            if (place == null)
            {
                if (Places.Count >= MaxPlaces) return false;
                place = new PlaceMark();
                Places.Add(place);
            }
            place.Name = name;
            place.X = x;
            place.Y = y;
            place.Z = z;
            return true;
        }

        public static bool IsHomeWord(string name)
        {
            string plain = Plain(name);
            foreach (string word in HomeWords)
            {
                if (OrderIntent.Normalize(word) == plain) return true;
            }
            return false;
        }

        private static string Plain(string name)
        {
            string plain = OrderIntent.Normalize(name);
            foreach (string article in Articles)
            {
                if (plain.StartsWith(article, StringComparison.Ordinal)) return plain.Substring(article.Length);
            }
            return plain;
        }
    }

    public enum TravelStep
    {
        Wait,
        Walk,
        Arrived,
        Jump
    }

    /// <summary>
    /// Por dónde seguir buscando sin dar vueltas por lo ya pisado, tanto al explorar como al buscar presas. Solo propone sitios
    /// dentro de la zona que el juego mantiene cargada alrededor del jugador, así que si este camina, la búsqueda va con él.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class SearchPlan
    {
        public const int Bearings = 12;

        // Un paso más corto por si el largo se sale de la zona cargada: junto al borde aún queda sitio.
        private const float ShortStepShare = 0.6f;
        private const double NewGroundScore = 2.0;
        private const double KeepHeadingScore = 1.0;
        private const double RandomScore = 0.5;

        private readonly float _cellSize;
        private readonly HashSet<long> _seen = new HashSet<long>();
        private float _headingX;
        private float _headingZ;

        public SearchPlan(float cellSize)
        {
            _cellSize = cellSize;
        }

        public void MarkSeen(float x, float z)
        {
            _seen.Add(Cell(x, z));
        }

        public bool Seen(float x, float z)
        {
            return _seen.Contains(Cell(x, z));
        }

        private long Cell(float x, float z)
        {
            long cellX = (long)Math.Floor(x / _cellSize);
            long cellZ = (long)Math.Floor(z / _cellSize);
            return (cellX << 32) ^ (cellZ & 0xFFFFFFFFL);
        }

        /// <summary>
        /// El siguiente sitio al que ir, o false si no queda ninguno al alcance. Prefiere seguir en la misma dirección y, con
        /// isNew, lo que descubra algo: mira lookAhead metros más allá, porque desde allí verá más de lo que pisa.
        /// </summary>
        public bool Next(float fromX, float fromZ, float playerX, float playerZ, float step, float maxFromPlayer, Func<float, float, bool> canWalk, Random random, out float x, out float z, Func<float, float, bool> isNew = null, float lookAhead = 0f)
        {
            x = fromX;
            z = fromZ;
            foreach (float length in new[] { step, step * ShortStepShare })
            {
                double best = double.NegativeInfinity;
                double offset = random.NextDouble() * Math.PI * 2.0;
                for (int i = 0; i < Bearings; i++)
                {
                    double angle = offset + i * Math.PI * 2.0 / Bearings;
                    float dirX = (float)Math.Sin(angle);
                    float dirZ = (float)Math.Cos(angle);
                    float toX = fromX + dirX * length;
                    float toZ = fromZ + dirZ * length;
                    float awayX = toX - playerX;
                    float awayZ = toZ - playerZ;
                    if (awayX * awayX + awayZ * awayZ > maxFromPlayer * maxFromPlayer || Seen(toX, toZ) || !canWalk(toX, toZ)) continue;

                    double score = random.NextDouble() * RandomScore + (dirX * _headingX + dirZ * _headingZ) * KeepHeadingScore;
                    if (isNew != null && isNew(toX + dirX * lookAhead, toZ + dirZ * lookAhead)) score += NewGroundScore;
                    if (score <= best) continue;
                    best = score;
                    x = toX;
                    z = toZ;
                }
                if (double.IsNegativeInfinity(best)) continue;

                double total = Math.Sqrt((x - fromX) * (x - fromX) + (z - fromZ) * (z - fromZ));
                _headingX = (float)((x - fromX) / total);
                _headingZ = (float)((z - fromZ) / total);
                return true;
            }
            return false;
        }
    }

    public enum ExploreStep
    {
        Walk,
        PickNext,
        Blocked,
        OutOfTime
    }

    /// <summary>Cuándo elegir otro sitio y cuándo dejarlo. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public sealed class ExploreGoal
    {
        public const float TimeLimitSeconds = 240f;
        public const float CellSize = 30f;
        public const float StepDistance = 35f;
        public const float ArriveDistance = 6f;
        public const float StallSeconds = 10f;

        /// <summary>Hasta qué parte de la zona cargada se aleja del jugador, con margen para que no se congele.</summary>
        public const float ReachShare = 0.85f;

        /// <summary>Más allá, el jugador se ha ido sin esperarle y deja de explorar.</summary>
        public const float LeaveShare = 0.97f;

        private readonly float _startedAt;
        private float _bestDistance = float.PositiveInfinity;
        private float _progressAt;

        public ExploreGoal(float now)
        {
            _startedAt = now;
            _progressAt = now;
        }

        public void NewWaypoint(float now)
        {
            _bestDistance = float.PositiveInfinity;
            _progressAt = now;
        }

        /// <summary>Si está peleando no cuenta como atascado: al acabar sigue por donde iba.</summary>
        public void Hold(float now)
        {
            _progressAt = now;
        }

        public ExploreStep Update(float toWaypoint, float now)
        {
            if (now - _startedAt >= TimeLimitSeconds) return ExploreStep.OutOfTime;
            if (toWaypoint <= ArriveDistance) return ExploreStep.PickNext;
            if (toWaypoint < _bestDistance - 1f)
            {
                _bestDistance = toWaypoint;
                _progressAt = now;
            }
            return now - _progressAt >= StallSeconds ? ExploreStep.Blocked : ExploreStep.Walk;
        }
    }

    /// <summary>Cómo se llaman los lugares del juego. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class Landmarks
    {
        private sealed class LandmarkEntry
        {
            public string Prefab;
            public string Es;
            public string En;
            public string Ru;
        }

        private static readonly LandmarkEntry[] Names =
        {
            Entry("SunkenCrypt", "una cripta hundida", "a sunken crypt", "затонувший склеп"),
            Entry("Crypt", "una cripta", "a burial chamber", "погребальные комнаты"),
            Entry("TrollCave", "una cueva de trol", "a troll cave", "пещера тролля"),
            Entry("MountainCave", "una cueva de montaña", "a frost cave", "ледяная пещера"),
            Entry("Runestone", "una piedra rúnica", "a runestone", "рунический камень"),
            Entry("StoneCircle", "un círculo de piedras", "a stone circle", "круг камней"),
            Entry("StoneHenge", "un gran círculo de piedras", "a large stone circle", "большой круг камней"),
            Entry("StoneTower", "una torre de piedra en ruinas", "a ruined stone tower", "разрушенная каменная башня"),
            Entry("WoodHouse", "una casa abandonada", "an abandoned house", "заброшенный дом"),
            Entry("WoodFarm", "una granja abandonada", "an abandoned farm", "заброшенная ферма"),
            Entry("WoodVillage", "una aldea abandonada", "an abandoned village", "заброшенная деревня"),
            Entry("AbandonedLogCabin", "una cabaña abandonada", "an abandoned cabin", "заброшенная хижина"),
            Entry("Ruin", "unas ruinas", "some ruins", "руины"),
            Entry("Dolmen", "un dolmen", "a dolmen", "дольмен"),
            Entry("Grave", "una tumba antigua", "an ancient grave", "древняя могила"),
            Entry("Greydwarf_camp", "un campamento de enanillos grises", "a greydwarf nest", "гнездо грейдворфов"),
            Entry("GoblinCamp", "un campamento de fulings", "a fuling village", "деревня фулингов"),
            Entry("DrakeNest", "un nido de draco", "a drake nest", "гнездо дракона"),
            Entry("ShipWreck", "un naufragio", "a shipwreck", "кораблекрушение"),
            Entry("InfestedTree", "un árbol infestado", "an infested tree", "заражённое дерево"),
            Entry("SwampHut", "una cabaña del pantano", "a swamp hut", "хижина на болоте"),
            Entry("SwampRuin", "unas ruinas del pantano", "some swamp ruins", "руины на болоте"),
            Entry("SwampWell", "un pozo del pantano", "a swamp well", "болотный колодец"),
            Entry("FireHole", "un agujero de fuego", "a surtling geyser", "гейзер суртлингов"),
            Entry("TarPit", "un pozo de brea", "a tar pit", "смоляная яма"),
            Entry("Mistlands_DvergrTownEntrance", "la entrada de una ciudadela", "an infested mine", "заражённый рудник"),
            Entry("Mistlands", "unas ruinas de las Tierras Brumosas", "some Mistlands ruins", "руины Туманных земель"),
            Entry("Eikthyrnir", "el altar de Eikthyr", "Eikthyr's altar", "алтарь Эйктюра"),
            Entry("GDKing", "el altar del Anciano", "The Elder's altar", "алтарь Древнего"),
            Entry("Bonemass", "el altar de Masa Ósea", "Bonemass's altar", "алтарь Массы костей"),
            Entry("Dragonqueen", "el altar de Moder", "Moder's altar", "алтарь Матери"),
            Entry("GoblinKing", "el altar de Yagluth", "Yagluth's altar", "алтарь Яглута"),
            Entry("Vendor_BlackForest", "el campamento de Haldor", "Haldor's camp", "лагерь Хальдора"),
            Entry("Hildir_camp", "el campamento de Hildir", "Hildir's camp", "лагерь Хильдир"),
            Entry("Hildir", "una guarida de los encargos de Hildir", "a Hildir quest dungeon", "подземелье поручений Хильдир"),
            Entry("BogWitch", "el campamento de la Bruja del Pantano", "the Bog Witch's camp", "лагерь Болотной ведьмы"),
            // Del mod Monstrum de Therzie: solo aparecen en los mundos que lo tienen.
            Entry("AltarAsmodeus_TW", "el altar de Asmodeus", "Asmodeus's altar", "алтарь Асмодея"),
            Entry("AltarSvalt_TW", "la cámara de rituales de Svält", "Svält's ritual chamber", "ритуальная комната Свельта"),
            Entry("AltarVrykolathas_TW", "el brasero de Hel de Vrykolathas", "Vrykolathas's Hel brazier", "жаровня Хель Вриколатас"),
            Entry("Vegvisir_location_Asmodeus_TW", "un vegvísir que señala el altar de Asmodeus", "a vegvisir pointing to Asmodeus's altar", "вегвизир, указывающий на алтарь Асмодея"),
            Entry("Vegvisir_location_Svalt_TW", "un vegvísir que señala la cámara de Svält", "a vegvisir pointing to Svält's chamber", "вегвизир, указывающий на комнату Свельта"),
            Entry("Vegvisir_location_Vrykolathas_TW", "un vegvísir que señala el brasero de Vrykolathas", "a vegvisir pointing to Vrykolathas's brazier", "вегвизир, указывающий на жаровню Вриколатас")
        };

        // Están por todas partes, o son el punto de partida: no son un hallazgo.
        private static readonly string[] Ignored = { "StartTemple", "Waymarker" };

        private static LandmarkEntry Entry(string prefab, string es, string en, string ru)
        {
            return new LandmarkEntry { Prefab = prefab, Es = es, En = en, Ru = ru };
        }

        /// <summary>Con artículo, para contarlo en una frase. Null si no merece la pena contarlo.</summary>
        public static string Label(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            foreach (string ignored in Ignored)
            {
                if (prefab.StartsWith(ignored, StringComparison.OrdinalIgnoreCase)) return null;
            }
            foreach (LandmarkEntry name in Names)
            {
                if (prefab.StartsWith(name.Prefab, StringComparison.OrdinalIgnoreCase)) return ModLang.Pick(name.Es, name.En, name.Ru);
            }
            return ModLang.Pick("un lugar fuera de lo normal", "an unusual place", "необычное место");
        }

        /// <summary>Una veta que merece la pena señalar, o null si es una roca cualquiera.</summary>
        public static string Ore(string prefab)
        {
            string name = (prefab ?? "").ToLowerInvariant();
            if (name.Contains("copper")) return ModLang.Pick("una veta de cobre", "a copper vein", "медная жила");
            if (name.Contains("silver")) return ModLang.Pick("una veta de plata", "a silver vein", "серебряная жила");
            return name.EndsWith("_tin") ? ModLang.Pick("una veta de estaño", "a tin deposit", "месторождение олова") : null;
        }

        private static readonly string[] LeadingArticles = { "un ", "una ", "unos ", "unas ", "el ", "la ", "los ", "las ", "a ", "an ", "the ", "some " };

        /// <summary>Lo que se escribe en el mapa: sin artículo y con mayúscula.</summary>
        public static string PinName(string label)
        {
            if (string.IsNullOrEmpty(label)) return label;
            string name = label;
            foreach (string article in LeadingArticles)
            {
                if (name.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(article.Length);
                    break;
                }
            }
            return name.Length == 0 ? label : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
    }

    /// <summary>
    /// Fuera de la zona cargada alrededor del jugador, el juego congela al compañero. Así que camina mientras se le vea y,
    /// antes de que eso pase o si se atasca, se adelanta hasta el lugar. Con el jugador al lado aguanta más, porque rodear algo
    /// también aleja del destino y verlo desaparecer sorprende. Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class TravelGoal
    {
        public const float ArriveDistance = 6f;
        public const float StepDistance = 30f;
        public const float StallSeconds = 15f;
        public const float StallNearPlayerSeconds = 30f;
        public const float NearPlayerDistance = 40f;
        private const float WalkEverySeconds = 3f;
        private const float MinProgress = 2f;
        private const float LooksStalledSeconds = 6f;

        /// <summary>Cómo contar un viaje, también al propio compañero. Sin peticiones a la IA.</summary>
        public static string Describe(string label, bool tend, int meters, bool detouring, bool stalled)
        {
            if (ModLang.IsSpanish)
            {
                return "ir a " + label + (tend ? " y encargarte de ese sitio" : " y quedarte allí") + " (a " + meters + " m" + (stalled ? ", sin avanzar" : detouring ? ", rodeando agua" : ", caminando") + ")";
            }
            if (ModLang.IsRussian)
            {
                return "идти в " + label + (tend ? " и следить за этим местом" : " и оставаться там") + " (в " + meters + " м" + (stalled ? ", без движения" : detouring ? ", обходя воду" : ", пешком") + ")";
            }
            return "go to " + label + (tend ? " and tend that place" : " and stay there") + " (" + meters + " m away" + (stalled ? ", stalled" : detouring ? ", going around water" : ", walking") + ")";
        }

        /// <summary>Si lleva un rato sin acercarse, aunque aún no le toque adelantarse.</summary>
        public bool LooksStalled(float now)
        {
            return now - _progressAt >= LooksStalledSeconds;
        }

        private float _closest = float.PositiveInfinity;
        private float _progressAt;
        private float _nextWalkAt;

        public TravelGoal(float now)
        {
            _progressAt = now;
            _nextWalkAt = now + WalkEverySeconds;
        }

        public TravelStep Update(float toPlace, float toPlayer, float loadedReach, float now)
        {
            if (toPlace <= ArriveDistance) return TravelStep.Arrived;
            if (toPlayer >= loadedReach) return TravelStep.Jump;
            if (toPlace <= _closest - MinProgress)
            {
                _closest = toPlace;
                _progressAt = now;
            }
            if (now - _progressAt >= (toPlayer <= NearPlayerDistance ? StallNearPlayerSeconds : StallSeconds)) return TravelStep.Jump;
            if (now < _nextWalkAt) return TravelStep.Wait;
            _nextWalkAt = now + WalkEverySeconds;
            return TravelStep.Walk;
        }
    }

    /// <summary>
    /// "Tala ese árbol" no termina cuando cae: quedan el tronco y el tocón, de donde sale la madera.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class TreeGoal
    {
        public const float TimeoutSeconds = 300f;
        public const int MaxRetargets = 3;

        // Margen para que recoja lo que ha caído antes de darlo por terminado.
        public const float SettleSeconds = 3f;

        private float _startedAt;
        private float _clearSince = -1f;

        private readonly float _settleSeconds;
        private readonly float _timeoutSeconds;

        public int Retargets;

        public TreeGoal(float now, float settleSeconds = SettleSeconds, float timeoutSeconds = TimeoutSeconds)
        {
            _startedAt = now;
            _settleSeconds = settleSeconds;
            _timeoutSeconds = timeoutSeconds;
        }

        public void Postpone(float seconds)
        {
            _startedAt += seconds;
        }

        public GoalState Update(bool treeStanding, int piecesLeft, float now)
        {
            if (now - _startedAt >= _timeoutSeconds) return GoalState.TimedOut;
            if (treeStanding || piecesLeft > 0)
            {
                _clearSince = -1f;
                return GoalState.Working;
            }
            if (_clearSince < 0f) _clearSince = now;
            return now - _clearSince >= _settleSeconds ? GoalState.Reached : GoalState.Working;
        }
    }

    /// <summary>Lo que ha ganado un compañero entre dos momentos, para saber qué le dio una tarea.</summary>
    public static class InventoryDiff
    {
        public static Dictionary<string, int> Gained(IDictionary<string, int> before, IDictionary<string, int> after)
        {
            var gained = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> item in after)
            {
                before.TryGetValue(item.Key, out int had);
                if (item.Value > had) gained[item.Key] = item.Value - had;
            }
            return gained;
        }
    }

    public enum HuntState
    {
        Going,
        Done,
        Lost,
        Stalled
    }

    /// <summary>
    /// Una orden de atacar a un animal. Offline Companions se acerca agachado y, sin aguante, espera quieto a recuperarlo,
    /// lo que parece que se ha atascado. Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class HuntWatch
    {
        public const float StallSeconds = 30f;
        public const float LostSeconds = 5f;
        public const float TimeoutSeconds = 180f;
        public const float MoveMeters = 1.5f;

        private readonly float _startedAt;
        private float _x;
        private float _z;
        private float _health;
        private int _ammo;
        private float _lastProgressAt;
        private float _lastEngagedAt;

        public HuntWatch(float x, float z, float health, int ammo, float now)
        {
            _x = x;
            _z = z;
            _health = health;
            _ammo = ammo;
            _startedAt = now;
            _lastProgressAt = now;
            _lastEngagedAt = now;
        }

        public HuntState Update(bool preyAlive, float preyHealth, bool engaged, float x, float z, int ammo, float now)
        {
            if (!preyAlive) return HuntState.Done;

            float dx = x - _x;
            float dz = z - _z;
            bool moved = dx * dx + dz * dz >= MoveMeters * MoveMeters;
            if (moved)
            {
                _x = x;
                _z = z;
            }
            if (moved || preyHealth < _health || ammo != _ammo) _lastProgressAt = now;
            _health = preyHealth;
            _ammo = ammo;

            if (engaged) _lastEngagedAt = now;
            if (now - _lastEngagedAt >= LostSeconds) return HuntState.Lost;
            return now - _lastProgressAt >= StallSeconds || now - _startedAt >= TimeoutSeconds ? HuntState.Stalled : HuntState.Going;
        }
    }

    /// <summary>La entrega de objetos al jugador: se acerca, y si este se mueve, va tras él.</summary>
    public sealed class GiveGoal
    {
        public const float HandDistance = 3f;
        public const float RewalkSeconds = 3f;
        public const float TimeoutSeconds = 40f;

        private float _startedAt;
        private float _nextWalkAt;

        public GiveGoal(float now)
        {
            _startedAt = now;
            _nextWalkAt = now;
        }

        /// <summary>El tiempo peleando no cuenta para el plazo.</summary>
        public void Postpone(float seconds)
        {
            _startedAt += seconds;
        }

        public bool InReach(float distance)
        {
            return distance <= HandDistance;
        }

        public bool TimedOut(float now)
        {
            return now - _startedAt >= TimeoutSeconds;
        }

        public bool ShouldWalk(float now)
        {
            if (now < _nextWalkAt) return false;
            _nextWalkAt = now + RewalkSeconds;
            return true;
        }
    }

    /// <summary>Para encargos largos: cuánto hace que no cambia el trabajo pendiente.</summary>
    public sealed class ProgressWatch
    {
        private int _last;
        private float _changedAt;

        public ProgressWatch(int value, float now)
        {
            _last = value;
            _changedAt = now;
        }

        public float SecondsWithoutChange(int value, float now)
        {
            if (value != _last)
            {
                _last = value;
                _changedAt = now;
            }
            return now - _changedAt;
        }

        public void Postpone(float seconds)
        {
            _changedAt += seconds;
        }
    }

    public enum PlantStep
    {
        Wait,
        Swing,
        Done,
        TimedOut
    }

    /// <summary>
    /// Plantas pequeñas que se rompen a golpes, como las hayas jóvenes. Offline Companions no las reconoce como
    /// recurso, así que el mod lleva al compañero hasta ellas y lo hace golpear.
    /// </summary>
    public sealed class PlantGoal
    {
        public const float ReachDistance = 1.5f;
        public const float SwingInterval = 1.2f;
        public const float TimeoutSeconds = 40f;

        // Tiempo para que termine de girarse hacia la planta antes del primer golpe.
        public const float AimDelay = 0.4f;

        private float _startedAt;
        private float _reachedAt = -1f;
        private float _nextSwingAt;

        public PlantGoal(float now)
        {
            _startedAt = now;
        }

        public void Postpone(float seconds)
        {
            _startedAt += seconds;
        }

        /// <summary>Tras una interrupción debe volver a acercarse y apuntar.</summary>
        public void ReleaseReach()
        {
            _reachedAt = -1f;
        }

        /// <summary>Mientras está al alcance, la IA del compañero queda en pausa para que no lo aleje.</summary>
        public bool InReach => _reachedAt >= 0f;

        public PlantStep Next(bool plantExists, float distance, float now)
        {
            if (!plantExists) return PlantStep.Done;
            if (now - _startedAt >= TimeoutSeconds) return PlantStep.TimedOut;
            if (distance > ReachDistance && !InReach) return PlantStep.Wait;
            if (!InReach) _reachedAt = now;
            if (now - _reachedAt < AimDelay || now < _nextSwingAt) return PlantStep.Wait;
            _nextSwingAt = now + SwingInterval;
            return PlantStep.Swing;
        }
    }

    public enum SequenceStep
    {
        Wait,
        StartNext,
        Finished,
        TimedOut
    }

    /// <summary>
    /// Las tareas pendientes de un compañero. Decide cuándo pasar a la siguiente; no gasta peticiones a la IA.
    /// T es la tarea en cola. Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class OrderSequence<T>
    {
        public const int MaxQueued = 5;

        // Tras dar una orden, el compañero tarda un momento en constar como ocupado.
        public const float StartGrace = 1.5f;
        public const float StepTimeout = 180f;

        private readonly Queue<T> _queue = new Queue<T>();
        private float _stepStartedAt;

        public int Pending => _queue.Count;

        public void StepStarted(float now)
        {
            _stepStartedAt = now;
        }

        public bool Enqueue(T item)
        {
            if (_queue.Count >= MaxQueued) return false;
            _queue.Enqueue(item);
            return true;
        }

        public T TakeNext()
        {
            return _queue.Dequeue();
        }

        public void Clear()
        {
            _queue.Clear();
        }

        public SequenceStep Tick(float now, bool busy, float timeout = StepTimeout)
        {
            float elapsed = now - _stepStartedAt;
            if (elapsed < StartGrace) return SequenceStep.Wait;
            if (busy) return elapsed >= timeout ? SequenceStep.TimedOut : SequenceStep.Wait;
            return _queue.Count > 0 ? SequenceStep.StartNext : SequenceStep.Finished;
        }
    }
}
