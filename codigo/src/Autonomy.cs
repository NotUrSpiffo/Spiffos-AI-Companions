using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OfflineCompanionsAI
{
    public enum AutonomyMode
    {
        Normal,
        Autonomo
    }

    /// <summary>Qué frases fijas de Offline Companions se dejan ver, para que no contradigan al personaje.</summary>
    public enum RebornSpeechMode
    {
        Todas,
        Avisos,
        Ninguna
    }

    /// <summary>Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class RebornSpeech
    {
        // Los valores de CompanionTalk.Priority en Offline Companions.
        public const int Ambient = 0;
        public const int Event = 1;
        public const int Problem = 2;
        public const int Order = 3;

        /// <summary>
        /// Con Avisos solo pasan los sucesos y problemas, que informan de algo real. Las charlas y los "Como ordenes"
        /// son las que hablan por el personaje: de eso ya se encarga la IA.
        /// </summary>
        public static bool Allows(RebornSpeechMode mode, int priority)
        {
            switch (mode)
            {
                case RebornSpeechMode.Todas: return true;
                case RebornSpeechMode.Ninguna: return false;
                default: return priority == Event || priority == Problem;
            }
        }
    }

    /// <summary>
    /// Cuánto le costaría una pelea: cuántos intercambios de golpes haría falta para ganarla, frente a cuántos para perderla.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public static class ThreatAssessment
    {
        /// <summary>Por encima de esto, mejor no buscar la pelea. Se ajusta con StrongEnemyRatio.</summary>
        public const float DefaultLimit = 1.5f;

        // No todos los golpes llegan: el enemigo se mueve, y entre golpes se guarda la guardia y se recupera aguante.
        private const float ShareOfHitsLanding = 0.6f;
        private const float DamagePerExtraLevel = 0.5f;
        private const float LethalHitShare = 0.5f;
        private const float QuickKillRounds = 1.5f;
        private const float ShareOfAllyHealth = 0.5f;

        /// <summary>Igual que HitData.DamageTypes.ApplyArmor en el juego.</summary>
        public static float AfterArmor(float damage, float armor)
        {
            if (armor < damage / 2f) return damage - armor;
            if (armor <= 0f) return damage;
            return Math.Min(1f, damage / (armor * 4f)) * damage;
        }

        /// <summary>Lo que le quita de verdad un golpe. Las estrellas de un enemigo aumentan su daño.</summary>
        public static float HitOn(float rawHit, int enemyLevel, float armor)
        {
            return AfterArmor(rawHit * (1f + Math.Max(0, enemyLevel - 1) * DamagePerExtraLevel), armor);
        }

        private static float RoundsToWin(float enemyHealth, float ownHit, int allies)
        {
            return enemyHealth / Math.Max(1f, ownHit * Math.Max(1, allies) * ShareOfHitsLanding);
        }

        /// <summary>Más de 1 significa que perderían antes de ganar. allies cuenta al propio compañero y al jugador, si está cerca.</summary>
        public static float Ratio(float enemyHealth, float enemyDamagePerRound, float ownHealth, float ownHit, int allies)
        {
            // Los aliados aguantan menos de lo que suman: un enemigo se centra en uno, y al caer ese queda uno menos pegando.
            float sharedHealth = ownHealth * (1f + (Math.Max(1, allies) - 1) * ShareOfAllyHealth);
            float roundsToLose = sharedHealth / Math.Max(1f, enemyDamagePerRound);
            return RoundsToWin(enemyHealth, ownHit, allies) / Math.Max(0.01f, roundsToLose);
        }

        /// <summary>
        /// Además del índice, que reparte el daño entre todos: si dos golpes bastan para matarlo y él no puede tumbar pronto al
        /// enemigo, tampoco compensa, aunque sea uno solo y aunque vayan varios: ser más no le salva de esos dos golpes, y un
        /// lobo muerde tres veces seguidas. Sin saber cuánto pega, no hay motivo para evitarlo.
        /// </summary>
        public static bool TooStrong(float enemyHealth, float enemyDamagePerRound, float highestHit, float ownHealth, float ownHit, int allies, float limit)
        {
            if (highestHit <= 0f) return false;
            if (Ratio(enemyHealth, enemyDamagePerRound, ownHealth, ownHit, allies) >= limit) return true;
            return highestHit >= ownHealth * LethalHitShare && RoundsToWin(enemyHealth, ownHit, 1) >= QuickKillRounds;
        }
    }

    /// <summary>Lo que se ve de un vistazo a su alrededor, para proponerle cosas que de verdad se pueden hacer.</summary>
    public sealed class InitiativeFacts
    {
        public bool LooseItems;
        public bool FiresLow;
        public bool DamagedPieces;
        public bool CanGetHammer;
        public bool HoneyReady;
        public int BuiltChests;
        public bool GatheredInPack;
        public bool CookingSpot;
        public bool Animals;
        public bool Trees;
        public bool Rocks;
        public bool Ripe;
        public bool FoundChest;
        public bool StationNear = true;

        public bool Raid;
        public bool EnemiesClose;
        public bool PlayerHurt;
        public bool SelfHurt;
        public bool Night;
        public bool AtBase;
        public bool PlayerBuilding;
        public bool PackFull;

        // Sin herramienta encima ni en los cofres. Al revés, lo normal sería que no pudiera hacer nada hasta saberlo.
        public bool NoAxe;
        public bool NoPickaxe;

        /// <summary>Lo que hay en los cofres cercanos, o null si no hay ninguno a mano.</summary>
        public int? WoodStored;
        public int? StoneStored;
        public int? FoodStored;

        /// <summary>Una comida que puede cocinar con lo que hay, o null.</summary>
        public string CookableDish;

        public bool MustStayClose => Raid || EnemiesClose || PlayerHurt || SelfHurt;
        public bool NightOutside => Night && !AtBase;
    }

    /// <summary>A qué tiende por su cuenta. Nunca manda sobre las órdenes del jugador, lo urgente ni el peligro.</summary>
    public enum Inclination
    {
        Ninguna,
        Oficio,
        Exploracion,
        Caza,
        Recoleccion
    }

    /// <summary>De qué tipo es lo que podría hacer por su cuenta, para saber si le tira más o menos.</summary>
    public enum IdeaKind
    {
        Other,
        Upkeep,
        Storage,
        Discovery,
        Hunting,
        Foraging,
        Materials
    }

    /// <summary>Cómo pesa la inclinación de un compañero. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class Inclinations
    {
        /// <summary>El valor guardado para decir expresamente que no tiene ninguna, aunque su perfil tenga una.</summary>
        public const string NoneValue = "ninguna";

        private const float Favored = 2f;
        private const float Unfavored = 0.6f;

        /// <summary>Lo que se puede elegir en el editor. Automática deja que decida su perfil.</summary>
        public static KeyValuePair<string, string>[] Choices => new[]
        {
            new KeyValuePair<string, string>("", ModLang.Pick("Automática", "Automatic", "Авто")),
            new KeyValuePair<string, string>(NoneValue, ModLang.Pick("Ninguna", "None", "Нет")),
            new KeyValuePair<string, string>("oficio", ModLang.Pick("Oficio", "Craft", "Ремесло")),
            new KeyValuePair<string, string>("exploracion", ModLang.Pick("Exploración", "Exploration", "Разведка")),
            new KeyValuePair<string, string>("caza", ModLang.Pick("Caza", "Hunting", "Охота")),
            new KeyValuePair<string, string>("recoleccion", ModLang.Pick("Recolección", "Foraging", "Сбор"))
        };

        public static string Name(Inclination inclination)
        {
            switch (inclination)
            {
                case Inclination.Oficio: return ModLang.Pick("Oficio", "Craft", "Ремесло");
                case Inclination.Exploracion: return ModLang.Pick("Exploración", "Exploration", "Разведка");
                case Inclination.Caza: return ModLang.Pick("Caza", "Hunting", "Охота");
                case Inclination.Recoleccion: return ModLang.Pick("Recolección", "Foraging", "Сбор");
                default: return ModLang.Pick("Ninguna", "None", "Нет");
            }
        }

        /// <summary>La elegida en la ficha; si es Automática, la del perfil en uso. Una ficha escrita a mano no tiene perfil.</summary>
        public static Inclination Of(CharacterProfile profile, string commonPersonality, string id)
        {
            switch ((profile?.Inclination ?? "").Trim().ToLowerInvariant())
            {
                case NoneValue:
                case "none":
                    return Inclination.Ninguna;
                case "oficio":
                case "craft":
                    return Inclination.Oficio;
                case "exploracion":
                case "exploration":
                    return Inclination.Exploracion;
                case "caza":
                case "hunting":
                    return Inclination.Caza;
                case "recoleccion":
                case "foraging":
                    return Inclination.Recoleccion;
            }

            string instructions = (profile?.Instructions ?? "").Trim();
            if (instructions.Length == 0) return PersonalityPresets.HasCommonPersonality(commonPersonality) ? Inclination.Ninguna : PersonalityPresets.For(id).Leaning;
            foreach (PersonalityPreset preset in PersonalityPresets.All)
            {
                if (preset.MatchesText(instructions)) return preset.Leaning;
            }
            return Inclination.Ninguna;
        }

        /// <summary>Por cuánto multiplicar el peso de una idea: inclina la balanza, sin quitar ninguna posibilidad.</summary>
        public static float Factor(Inclination inclination, IdeaKind kind)
        {
            if (kind == IdeaKind.Other) return 1f;
            switch (inclination)
            {
                case Inclination.Oficio:
                    return kind == IdeaKind.Upkeep || kind == IdeaKind.Storage ? Favored : kind == IdeaKind.Materials ? 1f : Unfavored;
                case Inclination.Exploracion:
                    return kind == IdeaKind.Discovery || kind == IdeaKind.Foraging ? Favored : kind == IdeaKind.Hunting ? 1f : Unfavored;
                case Inclination.Caza:
                    return kind == IdeaKind.Hunting ? Favored : kind == IdeaKind.Discovery || kind == IdeaKind.Foraging ? 1f : Unfavored;
                case Inclination.Recoleccion:
                    return kind == IdeaKind.Materials || kind == IdeaKind.Foraging ? Favored : kind == IdeaKind.Storage ? 1f : Unfavored;
                default:
                    return 1f;
            }
        }
    }

    /// <summary>Lo que hay ahora alrededor del sitio del que se encarga, para decidir si sale a recoger algo.</summary>
    public sealed class TendSituation
    {
        public bool Night;
        public bool PackFull;
        public bool NoAxe;
        public bool NoPickaxe;
        public bool Trees;
        public bool Rocks;

        /// <summary>Una planta silvestre lista para recoger, o null. Lo sembrado por el jugador no cuenta.</summary>
        public string Plant;

        /// <summary>Lo que suelta un animal que hay cerca del sitio, o null.</summary>
        public string Prey;

        /// <summary>Lo que hay en los cofres cercanos, o null si no hay ninguno: sin cofres no tendría dónde dejarlo.</summary>
        public int? WoodStored;
        public int? StoneStored;
        public int? FoodStored;
    }

    /// <summary>
    /// Qué recoger mientras se encarga de un sitio, según su inclinación. Lo que encuentre, aunque sea menos de lo previsto,
    /// cuenta como bueno, y si no hay nada tarda en volver a probar: así no se pasa el rato empezando y fracasando.
    /// Sin tipos de Unity, para poder probarlo fuera del juego.
    /// </summary>
    public sealed class TendPlan
    {
        public const int GatherAmount = 10;
        public const int Reserve = 50;

        /// <summary>Hasta dónde va a por recursos o a cazar, midiendo desde el sitio. El mantenimiento de Reborn abarca menos, unos 30 m.</summary>
        public const float AreaMeters = 50f;

        public const float RestSeconds = 300f;
        public const float NothingFoundSeconds = 900f;

        private readonly Dictionary<string, float> _notBefore = new Dictionary<string, float>();

        /// <summary>El objeto que conviene ir a recoger ahora, o null si basta con el mantenimiento.</summary>
        public string NextGather(Inclination inclination, TendSituation situation, float now)
        {
            if (situation.Night || situation.PackFull) return null;

            string wood = situation.Trees && !situation.NoAxe && Wanted("Wood", situation.WoodStored, now) ? "Wood" : null;
            string stone = situation.Rocks && !situation.NoPickaxe && Wanted("Stone", situation.StoneStored, now) ? "Stone" : null;
            string plant = situation.Plant != null && Wanted(situation.Plant, situation.FoodStored, now) ? situation.Plant : null;
            string prey = situation.Prey != null && Wanted(situation.Prey, situation.FoodStored, now) ? situation.Prey : null;

            switch (inclination)
            {
                case Inclination.Oficio:
                    return wood;
                case Inclination.Exploracion:
                    return plant;
                case Inclination.Caza:
                    return prey;
                case Inclination.Recoleccion:
                    if (wood != null && stone != null) return situation.StoneStored < situation.WoodStored ? stone : wood;
                    return wood ?? stone ?? plant;
                default:
                    return null;
            }
        }

        private bool Wanted(string item, int? stored, float now)
        {
            if (stored == null || stored >= Reserve) return false;
            return !_notBefore.TryGetValue(item, out float next) || now >= next;
        }

        public void Finished(string item, int gained, float now)
        {
            _notBefore[item] = now + (gained > 0 ? RestSeconds : NothingFoundSeconds);
        }
    }

    /// <summary>Algo que podría hacer ahora. Job limita qué recursos puede pedir si es conseguir algo.</summary>
    public sealed class InitiativeIdea
    {
        public string Tool;
        public string Key;
        public string Text;
        public int Job;
        public IdeaKind Kind;

        /// <summary>Cuánto encaja con la situación: más peso, más probable que se le proponga.</summary>
        public float Weight = 1f;

        /// <summary>No puede esperar: se le propone esto y nada más.</summary>
        public bool Urgent;
    }

    /// <summary>La hora tal como la diría una persona.</summary>
    public static class TimeOfDay
    {
        public static string Name(float dayFraction)
        {
            if (dayFraction < 0.2f || dayFraction >= 0.78f) return ModLang.Pick("de noche", "at night", "ночью");
            if (dayFraction < 0.3f) return ModLang.Pick("amaneciendo", "at dawn", "на рассвете");
            if (dayFraction < 0.5f) return ModLang.Pick("por la mañana", "in the morning", "утром");
            return dayFraction < 0.68f ? ModLang.Pick("por la tarde", "in the afternoon", "днём") : ModLang.Pick("anocheciendo", "at dusk", "в сумерках");
        }
    }

    /// <summary>
    /// Lo que el compañero puede hacer sin que se lo pidan. Sin tipos de Unity, para poder probarlo fuera del juego.
    /// Solo cosas útiles y fáciles de deshacer, sin cambiar los ajustes del jugador.
    /// </summary>
    public static class Initiative
    {
        public const int MaxGatherAmount = 20;
        public const int MaxCookAmount = 5;

        // Siempre es posible, así que con más peso desplazaría a lo que de verdad hace falta.
        private const float ExploreWeight = 0.5f;

        /// <summary>Por debajo de esta parte de la salud, alguien está malherido.</summary>
        public const float BadlyHurtShare = 0.35f;

        /// <summary>A esta distancia, un enemigo aún no pelea con él pero no es momento de irse a trabajar. Más lejos, en el bosque casi nunca haría nada.</summary>
        public const float AlertRange = 20f;

        private const int LowWood = 20;
        private const int PlentyWood = 100;
        private const int LowFood = 10;
        private const int PlentyFood = 50;
        private const float BuildingFactor = 2f;
        private const float NightWorkFactor = 0.5f;

        public static string MemoryLine => ModLang.Pick(
            "(Momento tranquilo: actuaste por iniciativa propia.)",
            "(Quiet moment: you acted on your own initiative.)",
            "(Спокойный момент: ты действовал по собственной инициативе.)");

        private const string SilenceTagEs = "[nada]";
        private const string SilenceTagEn = "[nothing]";
        private const string SilenceTagRu = "[ничего]";
        private static string SilenceTag => ModLang.Pick(SilenceTagEs, SilenceTagEn, SilenceTagRu);

        public static string PromptLine => ModLang.Pick(
            "Tienes iniciativa propia: no eres una herramienta que espera órdenes. Opina, propón planes, pregunta al jugador por lo que hace, retoma temas de la historia que comparten y lleva la contraria si tu personaje lo haría.",
            "You have your own initiative: you are not a tool waiting for orders. Share opinions, suggest plans, ask the player what they are doing, revisit topics from your shared history, and push back if your character would.",
            "У тебя есть собственная инициатива: ты не инструмент, ждущий приказов. Высказывай мнение, предлагай планы, спрашивай игрока о его делах, возвращайся к темам из вашей общей истории и возражай, если твой персонаж так бы поступил.");

        public static string AfterActing => ModLang.Pick(
            "Ya has empezado lo que decidiste hacer. Si quieres, avísalo con tu voz en muy pocas palabras, unas seis como mucho, como quien dice de pasada a dónde va. No digas por qué ni para qué, ni adornes la frase. Si hubo un error, no insistas.",
            "You have already started what you decided to do. If you like, mention it in your own voice in very few words, about six at most, like someone casually saying where they are going. Do not explain why or what for, and do not embellish. If there was an error, do not insist.",
            "Ты уже начал делать то, что решил. Если хочешь, скажи об этом вслух очень кратко, не более шести слов, словно мимоходом бросая, куда идёшь. Не объясняй почему или зачем и не приукрашивай фразу. Если возникла ошибка, не настаивай.");

        private static readonly HashSet<string> AllowedTools = new HashSet<string>
        {
            "check_chests", "check_skills", "remember_fact", "gather", "pick_up_item",
            "take_from_chest", "consume_item", "repair_gear", "recover_gear", "maintain", "go_to",
            "organize_chests", "store_items", "equip_item", "cook_food", "explore"
        };

        // Consultar y atender sus propias necesidades no cuenta: pueden repetirse si hace falta.
        private static readonly HashSet<string> Repeatable = new HashSet<string>
        {
            "check_chests", "check_skills", "remember_fact", "consume_item", "repair_gear", "recover_gear", "equip_item"
        };

        private static readonly string[] AlwaysOffered = { "check_chests", "take_from_chest", "consume_item", "repair_gear", "recover_gear", "equip_item" };

        // Lo único que puede hacer sin moverse de donde está.
        private static readonly string[] OfferedWhenStaying = { "consume_item", "equip_item" };

        public static IEnumerable<string> AllowedToolNames => AllowedTools;

        public static JArray Tools()
        {
            var allowed = new JArray();
            foreach (JToken tool in CompanionActions.ToolDefinitions())
            {
                if (AllowedTools.Contains((string)tool["function"]["name"])) allowed.Add(tool);
            }
            return allowed;
        }

        /// <summary>Por qué no puede hacer esto por su cuenta, o null si puede. La IA podría pedir igualmente lo que no se le ofrece.</summary>
        public static string Refusal(string tool, ActionRequest action)
        {
            if (!AllowedTools.Contains(tool))
            {
                return ModLang.Pick(
                    "Esto no lo haces por tu cuenta: propónselo al jugador.",
                    "You do not do this on your own: suggest it to the player.",
                    "Ты не делаешь это сам по себе: предложи это игроку.");
            }
            if (action.Kind == ActionKind.GoTo && (action.Value == CompanionActions.PlaceLookedAt || action.Stay))
            {
                return ModLang.Pick(
                    "Por tu cuenta solo puedes ir un momento hasta algo cercano.",
                    "On your own you can only step over to something nearby for a moment.",
                    "По собственной инициативе ты можешь лишь ненадолго подойти к чему-то поблизости.");
            }
            if (action.Kind == ActionKind.Gather && action.Amount > MaxGatherAmount)
            {
                return ModLang.Pick(
                    "Por tu cuenta, como mucho " + MaxGatherAmount + " de una vez.",
                    "On your own, at most " + MaxGatherAmount + " at a time.",
                    "По собственной инициативе максимум " + MaxGatherAmount + " за раз.");
            }
            if (action.Kind == ActionKind.Cook && action.Amount > MaxCookAmount)
            {
                return ModLang.Pick(
                    "Por tu cuenta, como mucho " + MaxCookAmount + " raciones de una vez.",
                    "On your own, at most " + MaxCookAmount + " portions at a time.",
                    "По собственной инициативе максимум " + MaxCookAmount + " порций за раз.");
            }
            if (action.Kind == ActionKind.TakeFromChest && !ItemNames.IsFoodWord(action.Item) && !ItemNames.IsAmmoWord(action.Item) && (action.Amount <= 0 || action.Amount > MaxGatherAmount))
            {
                return ModLang.Pick(
                    "Por tu cuenta, indica cuánto coges, como mucho " + MaxGatherAmount + ": lo demás es del jugador. Para comer, usa item comida.",
                    "On your own, specify how much you take, at most " + MaxGatherAmount + ": the rest belongs to the player. To eat, use item food.",
                    "По собственной инициативе укажи, сколько берёшь, максимум " + MaxGatherAmount + ": остальное принадлежит игроку. Чтобы поесть, укажи item food.");
            }
            return null;
        }

        /// <summary>Identifica lo que hizo para no repetirlo enseguida, o null si puede repetirse.</summary>
        public static string VarietyKey(string tool, ActionRequest action)
        {
            if (Repeatable.Contains(tool)) return null;
            if (action.Kind == ActionKind.Gather || action.Kind == ActionKind.GoTo) return tool + ":" + action.Value;
            if (action.Kind == ActionKind.Cook || action.Kind == ActionKind.PickUpItem) return tool;
            return string.IsNullOrEmpty(action.Item) ? tool : tool + ":" + action.Item;
        }

        /// <summary>Cosas que tienen sentido ahora mismo, sin lo que ha hecho hace poco. Si no debe alejarse, ninguna.</summary>
        public static List<InitiativeIdea> Ideas(InitiativeFacts facts, Func<string, bool> doneRecently, Inclination inclination = Inclination.Ninguna)
        {
            var ideas = new List<InitiativeIdea>();
            if (facts.MustStayClose) return ideas;

            void Add(bool possible, IdeaKind kind, string tool, string key, string text, float weight = 1f, int job = 0, bool urgent = false)
            {
                if (possible && !doneRecently(key)) ideas.Add(new InitiativeIdea { Tool = tool, Key = key, Text = text, Job = job, Kind = kind, Weight = weight * Inclinations.Factor(inclination, kind), Urgent = urgent });
            }
            // De noche y lejos de la base no se separa del jugador, y con la mochila llena no le cabría nada más.
            void AddGather(bool possible, IdeaKind kind, int job, string text, float weight)
            {
                Add(possible && !facts.NightOutside && !facts.PackFull, kind, "gather", "gather:" + job, text, weight * (facts.Night ? NightWorkFactor : 1f), job);
            }

            float food = ByStock(facts.FoodStored, LowFood, PlentyFood);
            float materials = facts.PlayerBuilding ? BuildingFactor : 1f;

            Add(facts.LooseItems, IdeaKind.Discovery, "pick_up_item", "pick_up_item",
                ModLang.Pick("recoger lo que hay suelto (pick_up_item con su nombre)", "pick up loose items (pick_up_item with their name)", "подобрать то, что лежит на земле (pick_up_item с его именем)"));
            Add(facts.FiresLow, IdeaKind.Upkeep, "maintain", "maintain:" + CompanionActions.PlanForTask("fuel"),
                ModLang.Pick("reavivar los fuegos que se apagan (maintain fuel)", "refuel dying fires (maintain fuel)", "подбросить топлива в затухающие огни (maintain fuel)"), 3f, urgent: facts.Night);
            Add(facts.DamagedPieces && facts.CanGetHammer, IdeaKind.Upkeep, "maintain", "maintain:" + CompanionActions.PlanForTask("repair"),
                ModLang.Pick("reparar lo que está dañado (maintain repair)", "repair damaged structures (maintain repair)", "починить повреждённые постройки (maintain repair)"), 3f);
            Add(facts.HoneyReady, IdeaKind.Upkeep, "maintain", "maintain:" + CompanionActions.PlanForTask("honey"),
                ModLang.Pick("recoger la miel (maintain honey)", "collect honey (maintain honey)", "собрать мёд (maintain honey)"), 2f);
            Add(facts.GatheredInPack && facts.BuiltChests > 0, IdeaKind.Storage, "store_items", "store_items",
                ModLang.Pick("guardar en un cofre lo que has recogido (store_items)", "store what you gathered in a chest (store_items)", "сложить собранное в сундук (store_items)"), 1f, urgent: facts.PackFull);
            Add(facts.BuiltChests > 1, IdeaKind.Storage, "organize_chests", "organize_chests",
                ModLang.Pick("ordenar los cofres (organize_chests)", "organize the chests (organize_chests)", "навести порядок в сундуках (organize_chests)"), 0.5f);
            Add(facts.CookingSpot && facts.CookableDish != null, IdeaKind.Other, "cook_food", "cook_food",
                ModLang.Pick("cocinar hasta " + MaxCookAmount + " de " + facts.CookableDish + " (cook_food)", "cook up to " + MaxCookAmount + " of " + facts.CookableDish + " (cook_food)", "приготовить до " + MaxCookAmount + " " + facts.CookableDish + " (cook_food)"), food);
            Add(facts.FoundChest && !facts.NightOutside, IdeaKind.Discovery, "go_to", "go_to:" + CompanionActions.PlaceFoundChest,
                ModLang.Pick("ir a ver el cofre encontrado (go_to found_chest)", "go check the discovered chest (go_to found_chest)", "пойти проверить найденный сундук (go_to found_chest)"), 2f);
            Add(!facts.NightOutside && !facts.PackFull, IdeaKind.Discovery, "explore", "explore",
                ModLang.Pick("explorar los alrededores por si hay algo interesante (explore)", "explore the surroundings for anything interesting (explore)", "осмотреть окрестности в поисках чего-то интересного (explore)"), ExploreWeight);
            AddGather(facts.Animals, IdeaKind.Hunting, CompanionActions.JobHunt,
                ModLang.Pick("cazar los animales cercanos (gather con su carne o piel)", "hunt nearby animals (gather with their meat or hide)", "поохотиться на животных поблизости (gather с их мясом или шкурой)"), food);
            AddGather(facts.Ripe, IdeaKind.Foraging, CompanionActions.JobForage,
                ModLang.Pick("recoger plantas que estén listas (gather con su nombre)", "gather ripe plants (gather with their name)", "собрать созревшие растения (gather с их именем)"), food);
            AddGather(facts.Trees && !facts.NoAxe, IdeaKind.Materials, CompanionActions.JobWood,
                ModLang.Pick("conseguir madera (gather)", "gather wood (gather)", "нарубить древесины (gather)"), ByStock(facts.WoodStored, LowWood, PlentyWood) * materials);
            AddGather(facts.Rocks && !facts.NoPickaxe, IdeaKind.Materials, CompanionActions.JobStone,
                ModLang.Pick("conseguir piedra (gather)", "gather stone (gather)", "добыть камень (gather)"), ByStock(facts.StoneStored, LowWood, PlentyWood) * materials);
            return ideas;
        }

        /// <summary>Hace más falta lo que escasea que lo que sobra. Sin cofres a mano no se sabe.</summary>
        private static float ByStock(int? stored, int low, int plenty)
        {
            if (stored == null) return 1f;
            return stored < low ? 2f : stored >= plenty ? 0.3f : 1f;
        }

        /// <summary>
        /// Lo urgente, si lo hay, y si no unas pocas según su peso: si se le ofrecen todas, el modelo escoge siempre conseguir
        /// recursos, y si fuera siempre lo que más pesa, no habría variedad.
        /// </summary>
        public static List<InitiativeIdea> Pick(IList<InitiativeIdea> ideas, int count, Random random)
        {
            var pool = new List<InitiativeIdea>(ideas);
            List<InitiativeIdea> urgent = pool.FindAll(idea => idea.Urgent);
            if (urgent.Count > 0) pool = urgent;

            var picked = new List<InitiativeIdea>();
            while (pool.Count > 0 && picked.Count < count)
            {
                float total = 0f;
                foreach (InitiativeIdea idea in pool) total += idea.Weight;
                double point = random.NextDouble() * total;
                int index = 0;
                while (index < pool.Count - 1 && point >= pool[index].Weight)
                {
                    point -= pool[index].Weight;
                    index++;
                }
                picked.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return picked;
        }

        /// <summary>Lo que debe tener presente al decidir, o null si no hay nada especial.</summary>
        public static string Context(InitiativeFacts facts)
        {
            var lines = new List<string>();
            if (facts.Raid)
            {
                lines.Add(ModLang.Pick(
                    "Hay una incursión en marcha: no te alejes del jugador ni empieces ningún trabajo.",
                    "A raid is underway: stay close to the player and do not start any jobs.",
                    "Идёт рейд: держись рядом с игроком и не начинай никакой работы."));
            }
            else if (facts.EnemiesClose)
            {
                lines.Add(ModLang.Pick(
                    "Hay enemigos cerca: no te alejes del jugador ni empieces ningún trabajo.",
                    "Enemies are nearby: stay close to the player and do not start any jobs.",
                    "Враги поблизости: держись рядом с игроком и не начинай никакой работы."));
            }
            if (facts.PlayerHurt)
            {
                lines.Add(ModLang.Pick(
                    "El jugador está malherido: quédate a su lado y, si quieres, díselo. No puedes curarlo ni darle de comer, y comer tú no le ayuda.",
                    "The player is badly hurt: stay by their side and, if you want, mention it. You cannot heal or feed them, and eating yourself does not help them.",
                    "Игрок тяжело ранен: оставайся рядом с ним и, если хочешь, скажи об этом. Ты не можешь вылечить или накормить его, и твоя еда ему не поможет."));
            }
            if (facts.SelfHurt)
            {
                lines.Add(ModLang.Pick(
                    "Estás malherido: no te alejes, y come si llevas algo.",
                    "You are badly hurt: do not wander off, and eat if you are carrying food.",
                    "Ты тяжело ранен: не уходи далеко и поешь, если у тебя есть еда."));
            }
            if (facts.NightOutside)
            {
                lines.Add(ModLang.Pick(
                    "Es de noche y están lejos de la base: no te separes del jugador.",
                    "It is night and you are far from base: do not separate from the player.",
                    "Сейчас ночь, и вы далеко от базы: не отходи от игрока."));
            }
            if (facts.PlayerBuilding && !facts.MustStayClose)
            {
                lines.Add(ModLang.Pick(
                    "El jugador está construyendo: los materiales le vendrán bien.",
                    "The player is building: materials will come in handy.",
                    "Игрок строит: материалы придутся кстати."));
            }
            return lines.Count == 0 ? null : string.Join(" ", lines);
        }

        /// <summary>Las herramientas de esas ideas, más las de atender sus propias necesidades. Solo puede conseguir los recursos propuestos.</summary>
        public static JArray ToolsFor(IList<InitiativeIdea> picked, bool staying = false, bool stationNear = true)
        {
            var names = new HashSet<string>(staying ? OfferedWhenStaying : AlwaysOffered);
            // Sin estación a mano, la herramienta solo sirve para que la intente, gaste una petición y falle.
            if (!stationNear) names.Remove("repair_gear");
            var jobs = new HashSet<int>();
            foreach (InitiativeIdea idea in picked)
            {
                names.Add(idea.Tool);
                if (idea.Job != 0) jobs.Add(idea.Job);
            }

            var offered = new JArray();
            foreach (JToken tool in Tools())
            {
                string name = (string)tool["function"]["name"];
                if (!names.Contains(name)) continue;
                if (name == "gather") tool["function"]["parameters"]["properties"]["item"]["enum"] = new JArray(CompanionActions.ItemsForJobs(jobs));
                offered.Add(tool);
            }
            return offered;
        }

        public static string Message(string surroundings, IList<string> needs, bool canAct, IList<string> ideas = null, IList<string> recent = null, string context = null)
        {
            if (ModLang.IsRussian)
            {
                string text = "(Спокойный момент; игрок ничего не сказал. Вокруг тебя: " + surroundings;
                text += " Построенные игроками сундуки принадлежат базе: не представляй их как находку.";
                if (needs != null && needs.Count > 0) text += " Что тебе нужно" + (canAct ? ", и позаботься об этом прежде всего, если можешь" : "") + ": " + string.Join(" ", needs);
                if (context != null) text += " " + context;
                if (canAct)
                {
                    text += " Реши в характере своего персонажа, сделаешь ли что-то по собственной инициативе: скажи что-то новое, подходящее к моменту или развивающее вашу общую историю, используй инструмент для полезного дела, которое порадует игрока, или сделай и то и другое. Сам по себе собирай не более " + MaxGatherAmount + " каждого ресурса.";
                    if (ideas != null && ideas.Count > 0) text += " Идеи на текущий момент: " + string.Join("; ", ideas) + ".";
                    if (recent != null && recent.Count > 0) text += " Ты уже пробовал это недавно, так что выбери другое: " + string.Join("; ", recent) + ".";
                }
                else
                {
                    text += " Ты занят другим делом, поэтому не можешь сменить занятие: максимум скажи что-то новое, подходящее к моменту.";
                }
                return text + " Не повторяй то, что уже сказал или только что сделал, и не командуй игроком. Если сейчас не стоит ничего делать, ответь только " + SilenceTag + ".)";
            }
            else if (ModLang.IsSpanish)
            {
                string text = "(Momento tranquilo; el jugador no ha dicho nada. A tu alrededor: " + surroundings;
                text += " Los cofres construidos por jugadores son de la base: no los presentes como un hallazgo.";
                if (needs != null && needs.Count > 0) text += " Lo que necesitas" + (canAct ? ", y atiéndelo antes que lo demás si puedes" : "") + ": " + string.Join(" ", needs);
                if (context != null) text += " " + context;
                if (canAct)
                {
                    text += " Decide como tu personaje si haces algo por iniciativa propia: decir algo nuevo que venga a cuento o haga avanzar la historia que comparten, usar una herramienta para algo útil que el jugador agradecería, o ambas cosas. Por tu cuenta, como mucho " + MaxGatherAmount + " de cada recurso.";
                    if (ideas != null && ideas.Count > 0) text += " Ideas para ahora: " + string.Join("; ", ideas) + ".";
                    if (recent != null && recent.Count > 0) text += " Ya has intentado hace poco, así que elige otra cosa: " + string.Join("; ", recent) + ".";
                }
                else
                {
                    text += " Estás ocupado con otra cosa, así que no puedes cambiar lo que haces: como mucho, di algo nuevo que venga a cuento.";
                }
                return text + " No repitas lo que ya dijiste ni lo que acabas de hacer, y no des órdenes al jugador. Si ahora nada merece la pena, responde solo " + SilenceTag + ".)";
            }
            else
            {
                string text = "(Quiet moment; the player said nothing. Around you: " + surroundings;
                text += " Player-built chests belong to the base: do not present them as a discovery.";
                if (needs != null && needs.Count > 0) text += " What you need" + (canAct ? ", and take care of it before anything else if you can" : "") + ": " + string.Join(" ", needs);
                if (context != null) text += " " + context;
                if (canAct)
                {
                    text += " Decide in character whether to do something on your own initiative: say something new that fits the moment or advances your story, use a tool for something useful the player would appreciate, or both. On your own, gather at most " + MaxGatherAmount + " of each resource.";
                    if (ideas != null && ideas.Count > 0) text += " Ideas for right now: " + string.Join("; ", ideas) + ".";
                    if (recent != null && recent.Count > 0) text += " You already tried recently, so pick something else: " + string.Join("; ", recent) + ".";
                }
                else
                {
                    text += " You are busy with something else, so you cannot change what you are doing: at most, say something new that fits the moment.";
                }
                return text + " Do not repeat what you already said or just did, and do not give orders to the player. If nothing is worth doing right now, reply only " + SilenceTag + ".)";
            }
        }

        /// <summary>Un suceso que puede resolver por su cuenta, como tener hambre o no poder terminar un encargo.</summary>
        public static string EventMessage(Reaction reaction)
        {
            return ModLang.Pick(
                "(Suceso de la partida; el jugador no ha dicho nada: " + reaction.Description
                + " Coméntalo en voz alta con una frase corta. Si puedes resolverlo tú con una herramienta, como buscar lo que te falta en un cofre, hazlo sin esperar a que te lo pidan.)",
                "(In-game event; the player said nothing: " + reaction.Description
                + " Comment on it aloud in a short sentence. If you can solve it yourself with a tool, like looking for what you need in a chest, do so without waiting to be asked.)",
                "(Игровое событие; игрок ничего не сказал: " + reaction.Description
                + " Прокомментируй это вслух короткой фразой. Если ты можешь решить это сам с помощью инструмента, например поискать недостающее в сундуке, сделай это, не дожидаясь просьбы.)");
        }

        /// <summary>Quita la indicación de no decir nada. Si no queda texto, se queda callado sin que nada llegue a verse.</summary>
        public static string StripSilence(string reply)
        {
            return (reply ?? "").Replace(SilenceTagEs, "").Replace(SilenceTagEn, "").Replace(SilenceTagRu, "").Trim();
        }
    }

    /// <summary>
    /// Lo que ha intentado hace poco por su cuenta, haya salido bien o no, para que no insista siempre en lo mismo.
    /// </summary>
    public sealed class InitiativeHistory
    {
        public const float RepeatSeconds = 900f;
        public static string RepeatRefusal => ModLang.Pick(
            "Ya intentaste esto hace poco: elige otra cosa, o simplemente di algo.",
            "You already tried this recently: choose something else, or simply say something.",
            "Ты уже пробовал это недавно: выбери что-то другое или просто скажи что-нибудь.");

        private readonly Dictionary<string, KeyValuePair<float, string>> _done = new Dictionary<string, KeyValuePair<float, string>>();

        public bool WasRecent(string key, float now)
        {
            return _done.TryGetValue(key, out KeyValuePair<float, string> entry) && now - entry.Key < RepeatSeconds;
        }

        public void Add(string key, string description, float now)
        {
            _done[key] = new KeyValuePair<float, string>(now, description);
        }

        public List<string> Recent(float now)
        {
            var recent = new List<string>();
            foreach (KeyValuePair<float, string> entry in _done.Values)
            {
                if (now - entry.Key < RepeatSeconds) recent.Add(entry.Value);
            }
            return recent;
        }
    }

    /// <summary>Comprobaciones de su equipo. Sin tipos de Unity, para poder probarlo fuera del juego.</summary>
    public static class GearCheck
    {
        /// <summary>Cuánta munición coge por su cuenta: el resto se queda en el cofre para el jugador.</summary>
        public const int AmmoToTake = 30;

        /// <summary>Sin esto, la IA se inventa que necesita materiales y se pone a reunirlos.</summary>
        public static string RepairNeed(bool stationNear)
        {
            return ModLang.Pick(
                "Tu equipo está muy gastado. Se repara en una estación de trabajo, sin gastar materiales"
                + (stationNear ? " (repair_gear)." : ", y no hay ninguna cerca: no busques materiales para esto; como mucho, díselo al jugador."),
                "Your gear is very worn. It is repaired at a crafting station without spending materials"
                + (stationNear ? " (repair_gear)." : ", and there is none nearby: do not look for materials for this; at most, mention it to the player."),
                "Твоё снаряжение сильно изношено. Оно ремонтируется на верстаке или в кузнице без расхода материалов"
                + (stationNear ? " (repair_gear)." : ", но поблизости ничего нет: не ищи материалы для этого; максимум скажи об этом игроку."));
        }

        /// <summary>Qué decirle si lleva un arma a distancia sin munición.</summary>
        public static string AmmoNeed(string launcher, string ammo, bool inChest, bool hasMelee)
        {
            if (ModLang.IsRussian)
            {
                string text = "У тебя есть " + launcher + ", но нет " + ammo;
                if (inChest) return text + ", и они есть в сундуке поблизости (take_from_chest).";
                text += ", и в сундуках поблизости их тоже нет.";
                return text + (hasMelee
                    ? " Пока сражайся оружием ближнего боя. Ты не умеешь их создавать: попроси у игрока."
                    : " У тебя также нет оружия ближнего боя: поищи в сундуках или попроси у игрока.");
            }
            if (ModLang.IsSpanish)
            {
                string text = "Llevas " + launcher + " pero no tienes " + ammo;
                if (inChest) return text + ", y hay en un cofre cercano (take_from_chest).";
                text += ", ni hay en los cofres cercanos.";
                return text + (hasMelee
                    ? " Mientras tanto peleas con tu arma cuerpo a cuerpo. Tú no puedes fabricarlas: pídeselas al jugador."
                    : " Tampoco llevas arma cuerpo a cuerpo: busca una en los cofres o pídesela al jugador.");
            }
            else
            {
                string text = "You are carrying " + launcher + " but have no " + ammo;
                if (inChest) return text + ", and there are some in a nearby chest (take_from_chest).";
                text += ", nor are there any in nearby chests.";
                return text + (hasMelee
                    ? " Meanwhile you fight with your melee weapon. You cannot craft them: ask the player for some."
                    : " You also have no melee weapon: look for one in the chests or ask the player for one.");
            }
        }
    }

    /// <summary>Cuándo puede gastar una petición por iniciativa propia, aparte de los comentarios automáticos.</summary>
    public sealed class InitiativeBudget
    {
        private const float WindowSeconds = 3600f;

        private readonly Queue<float> _recent = new Queue<float>();
        private float _lastAt;

        /// <summary>Al entrar en el mundo espera un intervalo completo, y no cuenta lo gastado en otra partida.</summary>
        public void Reset(float now)
        {
            _recent.Clear();
            _lastAt = now;
        }

        /// <summary>Si acaba de hablar por cualquier motivo, se espera otro intervalo.</summary>
        public void NotifySpoke(float now)
        {
            _lastAt = now;
        }

        public bool TryTake(float now, float intervalSeconds, int maxPerHour)
        {
            while (_recent.Count > 0 && now - _recent.Peek() >= WindowSeconds) _recent.Dequeue();
            if (now - _lastAt < intervalSeconds || _recent.Count >= maxPerHour) return false;
            _recent.Enqueue(now);
            _lastAt = now;
            return true;
        }
    }
}
