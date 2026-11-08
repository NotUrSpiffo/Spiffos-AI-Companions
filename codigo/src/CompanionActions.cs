using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OfflineCompanionsAI
{
    public static class RebornCopy
    {
        /// <summary>
        /// Reborn va dentro de este DLL, pero BepInEx solo carga una copia por GUID. Si elige un Companions.dll suelto,
        /// la copia integrada no arranca: el mod hablaría con ella y los compañeros reales serían los de la otra.
        /// </summary>
        public static bool IsForeign(string loadedFrom, string ownFile)
        {
            if (string.IsNullOrEmpty(loadedFrom) || string.IsNullOrEmpty(ownFile)) return false;
            return !string.Equals(System.IO.Path.GetFullPath(loadedFrom), System.IO.Path.GetFullPath(ownFile), System.StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Lo que se queda en la rueda de órdenes de Reborn: seguir, porque funciona sin IA; fijar el hogar, porque es exacto y no
    /// admite malentendidos; y despedir, que es definitivo y por eso la IA no puede hacerlo. Lo demás se pide por chat.
    /// Los números son los de Reborn. El anillo interior de posturas no pasa por aquí.
    /// </summary>
    public static class WheelOptions
    {
        public const int Follow = 0;
        public const int SetHome = 11;
        public const int Despawn = 30;

        public static bool Keeps(int actionId)
        {
            return actionId == Follow || actionId == SetHome || actionId == Despawn;
        }
    }

    public enum ActionKind
    {
        SetJob,
        SetFollow,
        SetStance,
        Attack,
        GoTo,
        UseLookedAt,
        Rest,
        StoreItems,
        RecoverGear,
        RepairGear,
        ComeHere,
        CancelOrders,
        SetHome,
        SetOption,
        Gather,
        GiveItems,
        DropItems,
        Consume,
        Equip,
        TakeFromChest,
        Guard,
        SetRetreat,
        Cook,
        OrganizeChests,
        CheckChests,
        Rename,
        Remember,
        LookAround,
        CompanionsStatus,
        RecallAll,
        CheckSkills,
        SetHomeRadius,
        SmeltItem,
        Maintain,
        PickUpItem,
        MarkPlace,
        ForgetPlace,
        GoToPlace,
        Explore
    }

    public sealed class ActionRequest
    {
        public ActionKind Kind;
        public int Value;
        public bool Stay;
        public bool Enabled;
        public bool KeepFollowing;
        public bool Tend;
        public string Item;
        public string ItemLabel;
        public int Amount;
        public string Who;
        public string Description;
    }

    /// <summary>
    /// La lista cerrada de acciones que la IA puede pedir. Nada fuera de esta lista llega al juego.
    /// Sin tipos de Unity, para poder probarla fuera del juego.
    /// </summary>
    public static class CompanionActions
    {
        public const int TargetLookedAt = 0;
        public const int TargetNearest = 1;
        public const int PlaceLookedAt = 0;
        public const int PlacePlayer = 1;
        public const int PlaceChest = 2;
        public const int PlaceBed = 3;
        public const int PlaceStation = 4;
        public const int PlaceFire = 5;
        public const int PlaceShip = 6;
        public const int PlaceTree = 7;
        public const int PlaceRock = 8;
        public const int PlaceFoundChest = 9;
        public const int RestSit = 0;
        public const int RestSleep = 1;
        public const int OptionWander = 0;
        public const int OptionPickup = 1;
        public const int OptionObey = 2;

        private const string SetJobTool = "set_job";
        private const string SetFollowTool = "set_follow";
        private const string SetStanceTool = "set_stance";
        private const string AttackTool = "attack";
        private const string GoToTool = "go_to";
        private const string UseLookedAtTool = "use_looked_at";
        private const string RestTool = "rest";
        private const string StoreItemsTool = "store_items";
        private const string RecoverGearTool = "recover_gear";
        private const string RepairGearTool = "repair_gear";
        private const string ComeHereTool = "come_here";
        private const string CancelOrdersTool = "cancel_orders";
        private const string SetHomeTool = "set_home";
        private const string SetOptionTool = "set_option";
        private const string GatherTool = "gather";
        private const string GiveItemsTool = "give_items";
        private const string DropItemsTool = "drop_items";
        private const string ConsumeTool = "consume_item";
        private const string EquipTool = "equip_item";
        private const string TakeTool = "take_from_chest";
        private const string GuardTool = "guard_area";
        private const string RetreatTool = "set_retreat";
        private const string CookTool = "cook_food";
        private const string OrganizeTool = "organize_chests";
        private const string CheckChestsTool = "check_chests";
        private const string RenameTool = "rename_companion";
        private const string RememberTool = "remember_fact";
        private const string LookAroundTool = "look_around";
        private const string CompanionsStatusTool = "companions_status";
        private const string RecallAllTool = "recall_all";
        private const string CheckSkillsTool = "check_skills";
        private const string HomeRadiusTool = "set_home_radius";
        private const string SmeltTool = "smelt_item";
        private const string MaintainTool = "maintain";
        private const string PickUpTool = "pick_up_item";
        private const string MarkPlaceTool = "mark_place";
        private const string ForgetPlaceTool = "forget_place";
        private const string GoToPlaceTool = "go_to_place";
        private const string ExploreTool = "explore";
        public const int MarkAtPlayer = 0;
        public const int MarkLookedAt = 1;
        public const int StancePassive = 1;
        public const int MinHomeRadius = 5;
        public const int MaxHomeRadius = 200;
        public const int JobSmelt = 6;

        private sealed class LocalizedOption
        {
            public int Value;
            public string Es;
            public string En;
            public string Text => ModLang.Pick(Es, En);
        }

        private static LocalizedOption Entry(int value, string es, string en)
        {
            return new LocalizedOption { Value = value, Es = es, En = en };
        }

        /// <summary>Las tareas de mantenimiento sueltas y el método de Offline Companions que planifica cada una.</summary>
        private static readonly Dictionary<string, string> MaintainPlans = new Dictionary<string, string>
        {
            { "repair", "PlanRepair" },
            { "fuel", "PlanFuel" },
            { "honey", "PlanHoney" },
            { "piles", "PlanPiles" }
        };

        private static readonly Dictionary<string, LocalizedOption> MaintainTasks = new Dictionary<string, LocalizedOption>
        {
            { "repair", Entry(0, "reparar las construcciones dañadas", "repair damaged structures") },
            { "fuel", Entry(1, "reavivar las hogueras y antorchas", "refuel the campfires and torches") },
            { "honey", Entry(2, "recoger la miel de las colmenas", "collect honey from the beehives") },
            { "piles", Entry(3, "apilar los materiales sobrantes", "stack leftover materials") }
        };

        public const string SortPlan = "PlanSort";

        public static bool NeedsHammer(string plan)
        {
            return plan == MaintainPlans["repair"] || plan == MaintainPlans["piles"];
        }

        public static string PlanForTask(string task)
        {
            return task != null && MaintainPlans.TryGetValue(task, out string plan) ? plan : null;
        }

        private static readonly string[] FurnaceWords = { "horno", "fundi", "furnace", "smelter", "kiln" };
        private static readonly string[] FireWords = { "fuego", "hoguera", "fogata", "antorcha", "brasero", "fire", "torch", "brazier", "sconce" };

        /// <summary>
        /// Reavivar fuegos no toca los hornos, y aun así parece lo más parecido a "rellena los hornos".
        /// Devuelve la corrección para la IA si el jugador pidió los hornos y no nombró ningún fuego; si no, null.
        /// </summary>
        public static string FurnaceMixup(ActionRequest action, string playerText)
        {
            if (action.Kind != ActionKind.Maintain || action.Item != MaintainPlans["fuel"] || string.IsNullOrEmpty(playerText)) return null;
            string text = playerText.ToLowerInvariant();
            if (!System.Array.Exists(FurnaceWords, text.Contains) || System.Array.Exists(FireWords, text.Contains)) return null;
            return ModLang.Pick(
                "maintain fuel solo reaviva hogueras y antorchas, nunca hornos. Para rellenar los hornos usa set_job con smelt, o smelt_item si piden un material concreto.",
                "maintain fuel only refuels campfires and torches, never smelters. To fill the smelters use set_job with smelt, or smelt_item if they ask for a specific material.");
        }
        public const int MaxNameLength = 24;
        public const int MaxCookAmount = 100;
        public const int DefaultCookAmount = 5;
        public const int JobCook = 17;
        public const int JobUpkeep = 15;

        /// <summary>Para consume_item: cualquier comida, a su elección.</summary>
        public const string AnyFood = "food";
        public const int MinRetreatPercent = 10;
        public const int MaxRetreatPercent = 90;

        private static readonly Dictionary<string, LocalizedOption> GuardPlaces = new Dictionary<string, LocalizedOption>
        {
            { "here", Entry(PlacePlayer, "proteger la zona donde está", "guard the area where they are") },
            { "looked_at", Entry(PlaceLookedAt, "proteger la zona que mira el jugador", "guard the area the player is looking at") }
        };

        /// <summary>Para give_items: lo que ganó en su última tarea, en vez de un objeto concreto.</summary>
        public const string LastTaskItems = "last_task";
        public const int MaxGiveAmount = 9999;

        public const int MaxGatherAmount = 500;
        public const int JobWood = 1;
        public const int JobStone = 2;
        public const int JobOre = 3;
        private const int ModeWood = JobWood;
        private const int ModeStone = JobStone;
        private const int ModeOre = JobOre;
        private const int ModeForage = JobForage;
        public const int JobHunt = 7;
        public const int JobForage = 5;
        private const int ModeHunt = JobHunt;
        private const int ModeFish = 9;

        // Los nombres coinciden con los objetos del juego; el valor es el trabajo que los consigue.
        private static readonly Dictionary<string, LocalizedOption> Items = new Dictionary<string, LocalizedOption>
        {
            { "Wood", Entry(ModeWood, "madera", "wood") },
            { "FineWood", Entry(ModeWood, "madera noble", "fine wood") },
            { "RoundLog", Entry(ModeWood, "madera resistente", "core wood") },
            { "ElderBark", Entry(ModeWood, "corteza ancestral", "ancient bark") },
            { "YggdrasilWood", Entry(ModeWood, "madera de Yggdrasil", "Yggdrasil wood") },
            { "Blackwood", Entry(ModeWood, "madera de ceniza", "ashwood") },
            { "Resin", Entry(ModeWood, "resina", "resin") },
            { "Stone", Entry(ModeStone, "piedra", "stone") },
            { "Flint", Entry(ModeStone, "sílex", "flint") },
            { "BlackMarble", Entry(ModeStone, "mármol negro", "black marble") },
            { "Grausten", Entry(ModeStone, "piedra cenicienta", "grausten") },
            { "CopperOre", Entry(ModeOre, "mineral de cobre", "copper ore") },
            { "TinOre", Entry(ModeOre, "mineral de estaño", "tin ore") },
            { "IronScrap", Entry(ModeOre, "chatarra", "scrap iron") },
            { "SilverOre", Entry(ModeOre, "mena de plata", "silver ore") },
            { "BlackMetalScrap", Entry(ModeOre, "trozos de metal negro", "black metal scrap") },
            { "CopperScrap", Entry(ModeOre, "chatarra de cobre", "copper scrap") },
            { "FlametalOreNew", Entry(ModeOre, "mineral de llametal", "flametal ore") },
            { "Obsidian", Entry(ModeOre, "obsidiana", "obsidian") },
            { "Raspberry", Entry(ModeForage, "frambuesas", "raspberries") },
            { "Blueberries", Entry(ModeForage, "arándanos", "blueberries") },
            { "Cloudberry", Entry(ModeForage, "moras de los pantanos", "cloudberries") },
            { "Mushroom", Entry(ModeForage, "setas", "mushrooms") },
            { "MushroomYellow", Entry(ModeForage, "setas amarillas", "yellow mushrooms") },
            { "MushroomBlue", Entry(ModeForage, "setas azules", "blue mushrooms") },
            { "Thistle", Entry(ModeForage, "cardos", "thistle") },
            { "Dandelion", Entry(ModeForage, "dientes de león", "dandelions") },
            { "Carrot", Entry(ModeForage, "zanahorias", "carrots") },
            { "Turnip", Entry(ModeForage, "nabos", "turnips") },
            { "Onion", Entry(ModeForage, "cebollas", "onions") },
            { "Barley", Entry(ModeForage, "cebada", "barley") },
            { "Flax", Entry(ModeForage, "lino", "flax") },
            { "Honey", Entry(ModeForage, "miel", "honey") },
            { "RawMeat", Entry(ModeHunt, "carne de jabalí", "boar meat") },
            { "DeerMeat", Entry(ModeHunt, "carne de ciervo", "deer meat") },
            { "NeckTail", Entry(ModeHunt, "colas de nixe", "neck tails") },
            { "WolfMeat", Entry(ModeHunt, "carne de lobo", "wolf meat") },
            { "LoxMeat", Entry(ModeHunt, "carne de luey", "lox meat") },
            { "HareMeat", Entry(ModeHunt, "carne de liebre", "hare meat") },
            { "ChickenMeat", Entry(ModeHunt, "carne de pollo", "chicken meat") },
            { "DeerHide", Entry(ModeHunt, "piel de ciervo", "deer hide") },
            { "LeatherScraps", Entry(ModeHunt, "trozos de cuero", "leather scraps") },
            { "Feathers", Entry(ModeHunt, "plumas", "feathers") },
            { "Fish1", Entry(ModeFish, "percas", "perch") },
            { "Fish2", Entry(ModeFish, "lucios", "pike") },
            { "Fish3", Entry(ModeFish, "atunes", "tuna") },
            { "Fish5", Entry(ModeFish, "peces troll", "trollfish") },
            { "Fish6", Entry(ModeFish, "arenques gigantes", "giant herring") }
        };

        public static List<string> ItemsForJobs(ICollection<int> jobs)
        {
            var items = new List<string>();
            foreach (KeyValuePair<string, LocalizedOption> item in Items)
            {
                if (jobs.Contains(item.Value.Value)) items.Add(item.Key);
            }
            return items;
        }

        /// <summary>Lo que no sale de picar ni talar: solo se encuentra suelto por el terreno.</summary>
        public static bool OnlyOnGround(string item)
        {
            return item == "Flint";
        }

        /// <summary>Lo que también se recoge suelto por el suelo: el sílex y la piedra.</summary>
        public static bool FoundOnGround(string item)
        {
            return item == "Flint" || item == "Stone";
        }

        public static int JobForItem(string item)
        {
            return item != null && Items.TryGetValue(item, out LocalizedOption entry) ? entry.Value : 0;
        }

        /// <summary>Lo que solo dan algunos árboles. La madera normal la da cualquiera, así que no hace falta elegir.</summary>
        public static bool NeedsChosenTrees(string item)
        {
            return JobForItem(item) == ModeWood && item != "Wood";
        }

        /// <summary>Los trabajos que, con Seguir activo, solo atienden lo que está muy cerca del jugador.</summary>
        public static bool UsesWorkArea(int job)
        {
            return job == ModeWood || job == ModeStone || job == ModeOre || job == ModeForage || job == ModeHunt;
        }

        private static readonly Dictionary<string, LocalizedOption> Options = new Dictionary<string, LocalizedOption>
        {
            { "wander", Entry(OptionWander, "moverse y mirar alrededor mientras te sigue", "wander and look around while following you") },
            { "pickup", Entry(OptionPickup, "recoger objetos del suelo", "pick up items from the ground") },
            { "obey", Entry(OptionObey, "atender la tecla de mando", "obey the command key") }
        };

        // Los valores de trabajo y postura coinciden con los de la rueda de órdenes de Offline Companions Reborn.
        private static readonly Dictionary<string, LocalizedOption> Jobs = new Dictionary<string, LocalizedOption>
        {
            { "none", Entry(0, "dejar su trabajo", "stop working") },
            { "wood", Entry(1, "talar madera", "chop wood") },
            { "stone", Entry(2, "picar piedra", "mine stone") },
            { "ore", Entry(3, "picar mineral", "mine ore") },
            { "forage", Entry(5, "recolectar plantas y bayas", "forage plants and berries") },
            { "smelt", Entry(6, "encargarse de los hornos", "tend the smelters") },
            { "hunt", Entry(7, "cazar", "hunt") },
            { "farm", Entry(8, "cultivar", "farm") },
            { "fish", Entry(9, "pescar", "fish") },
            { "repair", Entry(15, "reparar y reabastecer la base", "repair and resupply the base") },
            { "cook", Entry(17, "cocinar", "cook") }
        };

        private static readonly Dictionary<string, LocalizedOption> Stances = new Dictionary<string, LocalizedOption>
        {
            { "balanced", Entry(0, "postura equilibrada", "balanced stance") },
            { "passive", Entry(1, "postura pasiva", "passive stance") },
            { "ranged", Entry(2, "postura a distancia", "ranged stance") },
            { "melee", Entry(3, "postura cuerpo a cuerpo", "melee stance") }
        };

        public static string StanceName(int stance)
        {
            switch (stance)
            {
                case 0: return ModLang.Pick("equilibrada", "balanced");
                case StancePassive: return ModLang.Pick("pasiva", "passive");
                case 2: return ModLang.Pick("a distancia", "ranged");
                case 3: return ModLang.Pick("cuerpo a cuerpo", "melee");
                default: return ModLang.Pick("desconocida", "unknown");
            }
        }

        private static readonly Dictionary<string, LocalizedOption> MarkTargets = new Dictionary<string, LocalizedOption>
        {
            { "player", Entry(MarkAtPlayer, "donde está el jugador", "where the player is") },
            { "looked_at", Entry(MarkLookedAt, "donde mira el jugador", "where the player is looking") }
        };

        private static readonly Dictionary<string, LocalizedOption> AttackTargets = new Dictionary<string, LocalizedOption>
        {
            { "looked_at", Entry(TargetLookedAt, "atacar al enemigo que mira el jugador", "attack the enemy the player is looking at") },
            { "nearest_enemy", Entry(TargetNearest, "atacar al enemigo más cercano", "attack the nearest enemy") }
        };

        private static readonly Dictionary<string, LocalizedOption> Places = new Dictionary<string, LocalizedOption>
        {
            { "looked_at", Entry(PlaceLookedAt, "ir al lugar que mira el jugador", "go to the spot the player is looking at") },
            { "player", Entry(PlacePlayer, "ir hasta donde está el jugador", "go to the player") },
            { "chest", Entry(PlaceChest, "ir al cofre más cercano", "go to the nearest chest") },
            { "found_chest", Entry(PlaceFoundChest, "ir al cofre encontrado, no construido, más cercano", "go to the nearest discovered, non-player chest") },
            { "bed", Entry(PlaceBed, "ir a la cama más cercana", "go to the nearest bed") },
            { "station", Entry(PlaceStation, "ir a la estación de trabajo más cercana", "go to the nearest crafting station") },
            { "fire", Entry(PlaceFire, "ir al fuego más cercano", "go to the nearest fire") },
            { "ship", Entry(PlaceShip, "ir al barco más cercano", "go to the nearest ship") },
            { "tree", Entry(PlaceTree, "ir al árbol más cercano", "go to the nearest tree") },
            { "rock", Entry(PlaceRock, "ir a la roca más cercana", "go to the nearest rock") }
        };

        private static readonly Dictionary<string, LocalizedOption> RestKinds = new Dictionary<string, LocalizedOption>
        {
            { "sit_by_fire", Entry(RestSit, "sentarse junto al fuego más cercano", "sit by the nearest fire") },
            { "sleep", Entry(RestSleep, "ir a dormir a la cama más cercana", "sleep in the nearest bed") }
        };

        public static JArray ToolDefinitions()
        {
            return new JArray
            {
                Tool(ExploreTool, ModLang.Pick(
                    "Explora los alrededores un rato: anda por terreno que aún no ha pisado, descubre el mapa, marca lo que encuentra (cofres, ruinas, cuevas, vetas) y vuelve con el jugador. Úsala si piden explorar o ver qué hay por ahí, no para ir a un sitio concreto.",
                    "Explores the surroundings for a while: walks through unvisited terrain, reveals the map, marks discoveries (chests, ruins, caves, ore veins), and returns to the player. Use when asked to explore or see what is out there, not to go to a specific place."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject()
                }),
                Tool(SetJobTool, ModLang.Pick(
                    "Cambia el trabajo del compañero sin un límite de cantidad; deja de seguir al jugador y trabaja por la zona. Usa none para que deje de trabajar. Con smelt rellena de mineral, madera y combustible los hornos y hornos de carbón cercanos, recoge lo fundido y sigue atento a ellos. keep_following solo si piden que recoja lo que encuentre de camino mientras sigue al jugador; para cazar no lo uses, porque sale a buscar presas solo si no sigue al jugador.",
                    "Changes the companion's ongoing job without an amount limit; stops following the player and works around the area. Use none to stop working. With smelt they fill nearby smelters and kilns with ore, wood and fuel, collect the output, and keep tending them. Set keep_following only if asked to gather along the way while following the player; do not use it for hunt, as they only seek prey when not following."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["job"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Jobs.Keys) },
                        ["keep_following"] = new JObject { ["type"] = "boolean" }
                    },
                    ["required"] = new JArray("job")
                }),
                Tool(PickUpTool, ModLang.Pick(
                    "Va hasta un objeto suelto en el suelo y lo recoge, también si lo soltó el jugador. item es su nombre; sin item, lo que mira el jugador. Úsalo con \"recoge eso\" o \"coge el hacha del suelo\", y no uses set_option ni go_to para esto.",
                    "Walks to a loose item on the ground and picks it up, including items dropped by the player. item is its name; without item, picks up what the player is looking at. Use for \"pick that up\" or \"grab the axe on the ground\", and do not use set_option or go_to for this."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["item"] = new JObject { ["type"] = "string" } }
                }),
                Tool(GiveItemsTool, ModLang.Pick(
                    "Va hasta el jugador y le entrega objetos de su mochila. Con item last_task entrega lo que ganó en su última tarea; úsalo con \"dame lo que soltó\". Si no, item es el nombre del objeto. Nunca entrega lo que lleva equipado. Si también piden talar antes, pide primero esa tarea y después esta.",
                    "Walks to the player and hands over items from their backpack. With item last_task, hands over what they gained in their last task; use for \"give me what it dropped\". Otherwise item is the item name. Never gives equipped gear. If asked to chop or gather first, call that tool first and this one second."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["item"] = new JObject { ["type"] = "string" },
                        ["amount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = ModLang.Pick("Opcional. Si se omite, todo lo que tenga de ese objeto.", "Optional. If omitted, gives all they have of that item.") }
                    },
                    ["required"] = new JArray("item")
                }),
                Tool(DropItemsTool, ModLang.Pick(
                    "Suelta objetos al suelo donde está. Usa el mismo item que give_items, incluido last_task. Nunca suelta lo que lleva equipado. Para soltarlos en otro lugar, pide antes go_to con stay true y después esta orden. Úsalo solo si piden soltar o tirar algo al suelo; si piden que se lo den, usa give_items.",
                    "Drops items on the ground where they stand. Uses the same item as give_items, including last_task. Never drops equipped gear. To drop them somewhere else, call go_to with stay true first and then this order. Use only when asked to drop or throw something on the ground; if asked to give it to the player, use give_items."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["item"] = new JObject { ["type"] = "string" },
                        ["amount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = ModLang.Pick("Opcional. Si se omite, todo lo que tenga de ese objeto.", "Optional. If omitted, drops all they have of that item.") }
                    },
                    ["required"] = new JArray("item")
                }),
                Tool(CookTool, ModLang.Pick(
                    "Cocina una comida concreta con las estaciones y los ingredientes cercanos, también los que suele dejar de reserva, y al terminar vuelve con el jugador. No sirve para hidromiel. item es el nombre de la comida.",
                    "Cooks a specific food using nearby cooking stations and ingredients, including reserves, and returns to the player when done. Not for mead. item is the food name."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["item"] = new JObject { ["type"] = "string" },
                        ["amount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxCookAmount, ["description"] = ModLang.Pick("Opcional. Si se omite, " + DefaultCookAmount + ".", "Optional. If omitted, " + DefaultCookAmount + ".") }
                    },
                    ["required"] = new JArray("item")
                }),
                Tool(OrganizeTool, ModLang.Pick(
                    "Organiza los cofres cercanos: lleva cada objeto al cofre que ya tiene más de ese objeto, sin reparar ni hacer otras tareas, y al terminar vuelve con el jugador.",
                    "Organizes nearby chests: moves each item to the chest that already holds the most of that item, without repairing or doing other chores, and returns to the player when done."), NoParameters()),
                Tool(CheckChestsTool, ModLang.Pick(
                    "Consulta qué hay en los cofres cercanos, sin moverse. Úsalo si preguntan qué hay en los cofres; luego responde con lo que devuelva.",
                    "Checks what is inside nearby chests without moving. Use when asked what is in the chests; then reply with what it returns."), NoParameters()),
                Tool(CompanionsStatusTool, ModLang.Pick(
                    "Consulta dónde están y cómo están todos los compañeros del jugador en este mundo, aunque estén lejos o muertos. Úsalo con \"¿dónde está Bjorn?\" o \"¿quién sigue vivo?\", sin who; luego responde con lo que devuelva.",
                    "Checks where and how all of the player's companions are in this world, even if far away or dead. Use for \"where is Bjorn?\" or \"who is still alive?\", without who; then reply with what it returns."), NoParameters()),
                Tool(RecallAllTool, ModLang.Pick(
                    "Llama a todos los compañeros del jugador, a cualquier distancia, sin who; dejan lo que estén haciendo. No alcanza a los que estén en una zona sin cargar: el resultado dice quiénes son, así que cuéntaselo al jugador. Úsalo solo si piden que vengan todos estén donde estén; para los cercanos usa come_here con who all.",
                    "Calls all of the player's companions from any distance, without who; they stop what they are doing. Cannot reach those in unloaded zones: the result lists them, so tell the player. Use only when asked to summon everyone wherever they are; for nearby companions use come_here with who all."), NoParameters()),
                Tool(CheckSkillsTool, ModLang.Pick(
                    "Consulta las habilidades del compañero, de 0 a 100. Úsalo con \"¿qué tal se te da talar?\" o, con who all, \"¿quién pesca mejor?\"; luego responde con lo que devuelva.",
                    "Checks the companion's skills, from 0 to 100. Use for \"how good are you at chopping wood?\" or, with who all, \"who is best at fishing?\"; then reply with what it returns."), NoParameters()),
                Tool(HomeRadiusTool, ModLang.Pick(
                    "Cambia el tamaño de su zona de hogar, donde busca cama y cofres. Solo si ya tiene hogar.",
                    "Changes the size of their home area, where they look for a bed and chests. Only works if they already have a home set."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["meters"] = new JObject { ["type"] = "integer", ["minimum"] = MinHomeRadius, ["maximum"] = MaxHomeRadius } },
                    ["required"] = new JArray("meters")
                }),
                Tool(SmeltTool, ModLang.Pick(
                    "Funde en los hornos cercanos solo un material de los cofres cercanos, como cobre o madera para hacer carbón. Sigue hasta acabarlo, recoge el resultado y vuelve con el jugador. item es el nombre del material; el carbón no lo es, porque es combustible. Para rellenar los hornos sin nombrar un material o cargarles carbón, usa set_job con smelt.",
                    "Smelts a single material from nearby chests in nearby smelters or kilns, such as copper or wood for coal. Continues until finished, collects the output, and returns to the player. item is the material name; coal is not one, because it is fuel. To fill the smelters without naming a material, or to load them with coal, use set_job with smelt."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["item"] = new JObject { ["type"] = "string" } },
                    ["required"] = new JArray("item")
                }),
                ChoiceTool(MaintainTool, ModLang.Pick(
                    "Hace una sola tarea de mantenimiento cercana hasta acabarla y vuelve con el jugador: repair (reparar construcciones; si no lleva martillo, va por uno a un cofre), fuel (reavivar hogueras y antorchas, nunca hornos), honey (recoger miel) o piles (sacar de los cofres los materiales sobrantes y apilarlos; también va por el martillo). Para hacerlas todas sin parar, usa set_job con repair. Para rellenar hornos, usa set_job con smelt.",
                    "Performs a single nearby upkeep task until finished and returns to the player: repair (repair structures; if not carrying a hammer, they fetch one from a chest), fuel (refuel campfires and torches, never smelters), honey (collect honey), or piles (take excess materials from chests and stack them; also fetches the hammer). To do all upkeep continuously, use set_job with repair. To fill smelters, use set_job with smelt."), "task", MaintainTasks),
                Tool(RememberTool, ModLang.Pick(
                    "Guarda para siempre algo que el jugador pide expresamente recordar. fact es una frase breve, escrita desde tu punto de vista, como \"La base está al norte\".",
                    "Saves permanently something the player explicitly asks you to remember. fact is a brief sentence written from your perspective, such as \"The base is to the north\"."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["fact"] = new JObject { ["type"] = "string" } },
                    ["required"] = new JArray("fact")
                }),
                Tool(LookAroundTool, ModLang.Pick(
                    "Comprueba qué hay a tu alrededor, hasta unos 40 m: árboles, rocas, plantas, objetos sueltos, enemigos y construcciones. No ve lo que hay dentro de los cofres: para eso, check_chests. Úsalo si preguntan qué hay cerca o si dudas de si hay algún recurso; luego responde con lo que devuelva.",
                    "Checks what is around you within about 40 m: trees, rocks, plants, loose items, enemies, and structures. Does not see inside chests: use check_chests for that. Use when asked what is nearby or if unsure whether a resource is present; then reply with what it returns."), NoParameters()),
                Tool(RenameTool, ModLang.Pick(
                    "Cambia el nombre con el que aparece el compañero en el juego. Úsalo solo si el jugador le da un nombre nuevo.",
                    "Changes the companion's in-game display name. Use only if the player gives them a new name."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["name"] = new JObject { ["type"] = "string", ["maxLength"] = MaxNameLength } },
                    ["required"] = new JArray("name")
                }),
                Tool(ConsumeTool, ModLang.Pick(
                    "Come o bebe algo de su mochila. item es el nombre del alimento o hidromiel; usa food si solo piden que coma algo.",
                    "Eats or drinks something from their backpack. item is the food or mead name; use food if simply asked to eat something."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["item"] = new JObject { ["type"] = "string" } },
                    ["required"] = new JArray("item")
                }),
                Tool(EquipTool, ModLang.Pick(
                    "Equipa un arma, escudo o armadura de su mochila. Después puede cambiarla por otra que considere mejor, sobre todo en combate.",
                    "Equips a weapon, shield, or armor from their backpack. They may later switch to another they consider better, especially in combat."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["item"] = new JObject { ["type"] = "string" } },
                    ["required"] = new JArray("item")
                }),
                Tool(TakeTool, ModLang.Pick(
                    "Va al cofre, hasta unos 30 m, que contenga un objeto y lo coge; si ninguno lo tiene, te lo dice, así que úsalo directamente sin mirar antes. Úsalo con \"coge madera del cofre\" o \"agarra flechas\"; item flechas sirve para cualquier munición. Para buscar qué comer, usa item comida sin amount: coge una parte de hasta " + FoodPlan.Kinds + " comidas distintas, aunque estén en varios cofres. Conserva el equipo, la munición y la comida que coja. Después puede dárselo al jugador con give_items y last_task.",
                    "Walks to a chest within about 30 m that contains an item and takes it; if none has it, it tells you, so call it directly without checking first. Use for \"take wood from the chest\" or \"grab arrows\"; item arrows works for any ammo. To get food to eat, use item food without amount: takes a share of up to " + FoodPlan.Kinds + " different foods, even across multiple chests. Keeps gear, ammo, and food taken. Can then hand it to the player with give_items and last_task."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["item"] = new JObject { ["type"] = "string" },
                        ["amount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["description"] = ModLang.Pick("Opcional. Si se omite, de una comida coge solo una parte, el " + FoodPlan.SharePercent + " %, para dejar al jugador; de lo demás, todo lo que haya. Si piden expresamente toda una comida, indica " + MaxGiveAmount + ".", "Optional. If omitted, for food takes only a " + FoodPlan.SharePercent + " % share so some is left for the player; for other items, takes all available. If explicitly asked for all of a food, pass " + MaxGiveAmount + ".") }
                    },
                    ["required"] = new JArray("item")
                }),
                ChoiceTool(GuardTool, ModLang.Pick(
                    "Se queda protegiendo una zona: deja su trabajo, deja de seguir al jugador, pelea con lo que aparezca y luego vuelve a ese punto.",
                    "Stays guarding an area: stops working, stops following the player, fights anything that shows up, and returns to that spot."), "place", GuardPlaces),
                Tool(RetreatTool, ModLang.Pick(
                    "Fija con cuánta salud abandona una pelea y vuelve con el jugador. health_percent entre " + MinRetreatPercent + " y " + MaxRetreatPercent + ", o 0 para desactivarlo.",
                    "Sets the health percentage at which they retreat from a fight and return to the player. health_percent between " + MinRetreatPercent + " and " + MaxRetreatPercent + ", or 0 to disable."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["health_percent"] = new JObject { ["type"] = "integer" } },
                    ["required"] = new JArray("health_percent")
                }),
                Tool(GatherTool, ModLang.Pick(
                    "Consigue una cantidad concreta de un recurso, buscando por la zona hasta 40 m. Al terminar deja el trabajo y vuelve a seguir al jugador. Úsalo con \"consigue 10 piedras\". El sílex (Flint) no se pica: lo recoge del suelo sin necesitar pico, y suele estar cerca del agua. Si no dicen cuántas, usa una cantidad pequeña, como 5. Si no encuentra todas, termina con lo que tenga y sigue con la tarea siguiente. No la uses si solo preguntan por un encargo, como \"¿y la madera?\": responde según tu última tarea, y si piden lo que conseguiste, usa give_items.",
                    "Gathers a specific amount of a resource, searching the area within 40 m. When done, stops working and resumes following the player. Use for \"get 10 stones\". Flint is not mined: they pick it up off the ground without needing a pickaxe, usually near water. If no amount is given, use a small number like 5. If they cannot find it all, they finish with what they got and move to the next task. Do not call this if the player is only asking about an order, like \"what about the wood?\": answer from your last task, and if they ask for what you gathered, use give_items."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["item"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Items.Keys) },
                        ["amount"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = MaxGatherAmount }
                    },
                    ["required"] = new JArray("item", "amount")
                }),
                ChoiceTool(SetStanceTool, ModLang.Pick(
                    "Cambia la postura de combate del compañero.",
                    "Changes the companion's combat stance."), "stance", Stances),
                Tool(SetFollowTool, ModLang.Pick(
                    "Hace que el compañero siga al jugador o que se quede donde está. Seguir al jugador termina los trabajos que se hacen en un lugar fijo.",
                    "Makes the companion follow the player or stay where they are. Following the player ends jobs tied to a fixed spot."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["follow"] = new JObject { ["type"] = "boolean" } },
                    ["required"] = new JArray("follow")
                }),
                ChoiceTool(AttackTool, ModLang.Pick(
                    "Ataca a un enemigo concreto. looked_at es el enemigo que mira el jugador; úsalo con frases como \"ese jabalí\". nearest_enemy es el más cercano.",
                    "Attacks a specific enemy. looked_at is the enemy the player is looking at; use for phrases like \"that boar\". nearest_enemy is the closest one."), "target", AttackTargets),
                Tool(GoToTool, ModLang.Pick(
                    "Camina a un lugar. looked_at es el punto que mira el jugador: úsalo solo con \"ve allí\" o si mira justo lo que nombra. player es donde está el jugador. chest, bed, station, fire, ship, tree y rock son lo más cercano de ese tipo, hasta unos 40 m: úsalos si piden ir a una de esas cosas sin estar mirándola. found_chest es el cofre más cercano que nadie ha construido: úsalo para ir a un cofre encontrado, porque chest iría al más cercano, que suele ser uno de la base. Para ir a otra cosa que el jugador no mira, dilo en vez de caminar a otro sitio. Al llegar retoma su trabajo o vuelve a seguir al jugador, salvo que stay sea true.",
                    "Walks to a spot. looked_at is where the player is looking: use only for \"go there\" or if they are looking right at what they name. player is where the player stands. chest, bed, station, fire, ship, tree, and rock are the nearest of that kind within about 40 m: use when asked to go to one of those without looking at it. found_chest is the nearest chest not built by players: use to go to a discovered chest, since chest goes to the closest one, usually at base. To go to something else the player is not looking at, say so instead of walking elsewhere. Upon arrival, resumes work or following unless stay is true."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["place"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Places.Keys) },
                        ["stay"] = new JObject { ["type"] = "boolean", ["description"] = ModLang.Pick("true si le piden quedarse allí: deja su trabajo y no sigue al jugador.", "true if asked to stay there: stops working and does not follow the player.") }
                    },
                    ["required"] = new JArray("place")
                }),
                Tool(MarkPlaceTool, ModLang.Pick(
                    "Marca un lugar con un nombre para poder volver después, como la base o una mina, y lo señala en el mapa. Si el nombre ya existe, lo cambia de sitio. Úsalo con \"marca aquí la base\".",
                    "Marks a place with a name so you can return later, such as the base or a mine, and pins it on the map. If the name already exists, moves it. Use for \"mark the base here\"."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["name"] = new JObject { ["type"] = "string" },
                        ["at"] = new JObject { ["type"] = "string", ["enum"] = new JArray(MarkTargets.Keys), ["description"] = ModLang.Pick("player si no se indica.", "player if omitted.") }
                    },
                    ["required"] = new JArray("name")
                }),
                Tool(ForgetPlaceTool, ModLang.Pick(
                    "Borra un lugar marcado.",
                    "Deletes a marked place."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["name"] = new JObject { ["type"] = "string" } },
                    ["required"] = new JArray("name")
                }),
                Tool(GoToPlaceTool, ModLang.Pick(
                    "Va a un lugar marcado, por lejos que esté, deja su trabajo y se queda allí esperando. Úsalo con \"vuelve a la base\" o \"ve a la mina\". name es uno de los lugares marcados. Solo si además le piden que se encargue, se ocupe o cuide de ese sitio, pon tend en true: al llegar lo mantiene por su cuenta sin irse. Para encargarse de donde ya está, usa name \"aquí\" y tend true.",
                    "Goes to a marked place, no matter how far, stops working, and waits there. Use for \"go back to base\" or \"go to the mine\". name is one of the marked places. Only if also asked to tend, look after, or take care of that place, set tend to true: upon arrival they maintain it on their own without leaving. To tend where they already are, use name \"here\" and tend true."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject { ["name"] = new JObject { ["type"] = "string" }, ["tend"] = new JObject { ["type"] = "boolean" } },
                    ["required"] = new JArray("name")
                }),
                Tool(UseLookedAtTool, ModLang.Pick(
                    "Hace lo que corresponda con lo que mira el jugador: talar ese árbol por completo, con su tronco, y luego volver con el jugador; romper a golpes esa planta pequeña o arbusto, sin herramienta; picar esa roca, abrir o cerrar esa puerta, tirar de ese carro o soltarlo, subir a ese barco, sentarse en ese fuego, dormir en esa cama, guardar en ese cofre, reparar en esa estación, vaciar esa colmena o recuperar esa tumba.",
                    "Interacts appropriately with what the player is looking at: chop down that tree completely, including its log, then return to the player; smash that small plant or bush without a tool; mine that rock, open or close that door, pull or release that cart, board that ship, sit by that fire, sleep in that bed, store items in that chest, repair at that station, empty that beehive, or recover that tombstone."), NoParameters()),
                ChoiceTool(RestTool, ModLang.Pick(
                    "Se sienta junto al fuego encendido más cercano o va a dormir a la cama más cercana. Si ya está en ello, se levanta.",
                    "Sits by the nearest lit fire or goes to sleep in the nearest bed. If already resting, gets up."), "kind", RestKinds),
                Tool(StoreItemsTool, ModLang.Pick(
                    "Guarda lo que lleva de sobra en los cofres cercanos, repartiéndolo entre los que tengan sitio y prefiriendo donde ya hay de lo mismo. Conserva lo que lleva puesto, la comida, las armas, las armaduras y las antorchas.",
                    "Stores spare items in nearby chests, distributing them among chests with room and preferring those that already hold the same item. Keeps equipped gear, food, weapons, armor, and torches."), NoParameters()),
                Tool(RecoverGearTool, ModLang.Pick(
                    "Va a su propia tumba cercana a recuperar sus cosas.",
                    "Goes to their own nearby tombstone to recover their gear."), NoParameters()),
                Tool(RepairGearTool, ModLang.Pick(
                    "Repara su equipo en la estación de trabajo más cercana.",
                    "Repairs their gear at the nearest crafting station."), NoParameters()),
                Tool(ComeHereTool, ModLang.Pick(
                    "Deja lo que está haciendo y vuelve a seguir al jugador. Úsalo con \"ven aquí\".",
                    "Stops what they are doing and resumes following the player. Use for \"come here\"."), NoParameters()),
                Tool(CancelOrdersTool, ModLang.Pick(
                    "Cancela lo que está haciendo, sus tareas pendientes y su trabajo, sin moverse a ningún sitio.",
                    "Cancels what they are doing, their queued tasks, and their job, without moving anywhere."), NoParameters()),
                Tool(SetHomeTool, ModLang.Pick(
                    "Fija como hogar del compañero el lugar donde está ahora. Allí busca cama, deja cosas y trabaja.",
                    "Sets the companion's home at their current position. They look for a bed, store items, and work there."), NoParameters()),
                Tool(SetOptionTool, ModLang.Pick(
                    "Activa o desactiva una opción del compañero: wander (moverse y mirar alrededor mientras sigue al jugador), pickup (recoger por su cuenta lo que encuentre; para un objeto concreto usa pick_up_item), obey (atender la tecla de mando).",
                    "Enables or disables a companion option: wander (move and look around while following the player), pickup (autonomously pick up items found; for a specific item use pick_up_item), obey (respond to the command key)."), new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject
                    {
                        ["option"] = new JObject { ["type"] = "string", ["enum"] = new JArray(Options.Keys) },
                        ["enabled"] = new JObject { ["type"] = "boolean" }
                    },
                    ["required"] = new JArray("option", "enabled")
                })
            };
        }

        private static JObject NoParameters()
        {
            return new JObject { ["type"] = "object", ["properties"] = new JObject() };
        }

        private static JObject ChoiceTool(string name, string description, string parameter, Dictionary<string, LocalizedOption> options)
        {
            return Tool(name, description, new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { [parameter] = new JObject { ["type"] = "string", ["enum"] = new JArray(options.Keys) } },
                ["required"] = new JArray(parameter)
            });
        }

        private static JObject Tool(string name, string description, JObject parameters)
        {
            ((JObject)parameters["properties"])["who"] = new JObject
            {
                ["type"] = "string",
                // Se explica una sola vez en el prompt: repetirlo en las 35 herramientas gastaba saldo en cada petición.
                ["description"] = ModLang.Pick("Opcional: a quién se aplica.", "Optional: who this applies to.")
            };
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject { ["name"] = name, ["description"] = description, ["parameters"] = parameters }
            };
        }

        /// <summary>Valida una petición de la IA. Devuelve null y explica el error si no es válida.</summary>
        public static ActionRequest Parse(string tool, string arguments, out string error)
        {
            error = null;
            JObject args;
            try
            {
                args = JObject.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            }
            catch (JsonException)
            {
                error = ModLang.Pick("Argumentos no válidos.", "Invalid arguments.");
                return null;
            }

            ActionRequest request = ParseTool(tool, args, out error);
            JToken who = args["who"];
            if (request != null && who != null && who.Type == JTokenType.String) request.Who = who.Value<string>();
            return request;
        }

        private static ActionRequest ParseTool(string tool, JObject args, out string error)
        {
            error = null;
            switch (tool)
            {
                case MarkPlaceTool:
                    string markName = PlaceNameFrom(args, out error);
                    if (markName == null) return null;
                    int markAt = MarkAtPlayer;
                    if (args["at"] != null)
                    {
                        ActionRequest markTarget = FromTable(ActionKind.MarkPlace, MarkTargets, args, "at", out error);
                        if (markTarget == null) return null;
                        markAt = markTarget.Value;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.MarkPlace,
                        Item = markName,
                        Value = markAt,
                        Description = ModLang.Pick("marcar el lugar " + markName, "mark place " + markName)
                    };
                case ForgetPlaceTool:
                    string forgetName = PlaceNameFrom(args, out error);
                    return forgetName == null
                        ? null
                        : new ActionRequest
                        {
                            Kind = ActionKind.ForgetPlace,
                            Item = forgetName,
                            Description = ModLang.Pick("borrar el lugar " + forgetName, "forget place " + forgetName)
                        };
                case ExploreTool:
                    return Simple(ActionKind.Explore, ModLang.Pick("explorar los alrededores", "explore the surroundings"));
                case GoToPlaceTool:
                    JToken tendToken = args["tend"];
                    bool tend = tendToken != null && tendToken.Type == JTokenType.Boolean && tendToken.Value<bool>();
                    if (tend && IsHereWord(args["name"]))
                    {
                        return new ActionRequest
                        {
                            Kind = ActionKind.GoToPlace,
                            Item = "",
                            Tend = true,
                            Description = ModLang.Pick("encargarse de este sitio", "tend this place")
                        };
                    }
                    string placeName = PlaceNameFrom(args, out error);
                    return placeName == null
                        ? null
                        : new ActionRequest
                        {
                            Kind = ActionKind.GoToPlace,
                            Item = placeName,
                            Tend = tend,
                            Description = ModLang.Pick(
                                "ir a " + placeName + (tend ? " y encargarse de ese sitio" : " y quedarse allí"),
                                "go to " + placeName + (tend ? " and tend that place" : " and stay there"))
                        };
                case SetHomeTool:
                    return Simple(ActionKind.SetHome, ModLang.Pick("fijar su hogar aquí", "set their home here"));
                case SetOptionTool:
                    ActionRequest option = FromTable(ActionKind.SetOption, Options, args, "option", out error);
                    if (option == null) return null;
                    JToken enabled = args["enabled"];
                    if (enabled == null || enabled.Type != JTokenType.Boolean)
                    {
                        error = ModLang.Pick("Falta indicar si se activa o se desactiva.", "Missing whether to enable or disable it.");
                        return null;
                    }
                    option.Enabled = enabled.Value<bool>();
                    option.Description = (option.Enabled
                        ? ModLang.Pick("activar: ", "enable: ")
                        : ModLang.Pick("desactivar: ", "disable: ")) + option.Description;
                    return option;
                case SetJobTool:
                    ActionRequest job = FromTable(ActionKind.SetJob, Jobs, args, "job", out error);
                    JToken keep = args["keep_following"];
                    if (job != null) job.KeepFollowing = keep != null && keep.Type == JTokenType.Boolean && keep.Value<bool>();
                    return job;
                case CompanionsStatusTool:
                    return Simple(ActionKind.CompanionsStatus, ModLang.Pick("consultar a los compañeros", "check on companions"));
                case RecallAllTool:
                    return Simple(ActionKind.RecallAll, ModLang.Pick("llamar a todos los compañeros", "recall all companions"));
                case CheckSkillsTool:
                    return Simple(ActionKind.CheckSkills, ModLang.Pick("consultar sus habilidades", "check their skills"));
                case HomeRadiusTool:
                    JToken meters = args["meters"];
                    if (meters == null || meters.Type != JTokenType.Integer || meters.Value<long>() < MinHomeRadius || meters.Value<long>() > MaxHomeRadius)
                    {
                        error = ModLang.Pick(
                            "El tamaño debe estar entre " + MinHomeRadius + " y " + MaxHomeRadius + " metros.",
                            "The radius must be between " + MinHomeRadius + " and " + MaxHomeRadius + " meters.");
                        return null;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.SetHomeRadius,
                        Value = meters.Value<int>(),
                        Description = ModLang.Pick(
                            "cambiar su zona de hogar a " + meters.Value<int>() + " m",
                            "change their home area to " + meters.Value<int>() + " m")
                    };
                case SmeltTool:
                    JToken material = args["item"];
                    string materialName = material != null && material.Type == JTokenType.String ? material.Value<string>().Trim() : "";
                    if (materialName.Length == 0)
                    {
                        error = ModLang.Pick("Falta indicar qué material.", "Missing which material to smelt.");
                        return null;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.SmeltItem,
                        Item = materialName,
                        Description = ModLang.Pick("fundir: ", "smelt: ") + materialName
                    };
                case MaintainTool:
                    ActionRequest maintain = FromTable(ActionKind.Maintain, MaintainTasks, args, "task", out error);
                    if (maintain != null) maintain.Item = PlanForTask(args.Value<string>("task"));
                    return maintain;
                case LookAroundTool:
                    return Simple(ActionKind.LookAround, ModLang.Pick("mirar alrededor", "look around"));
                case PickUpTool:
                    JToken loose = args["item"];
                    string looseName = loose != null && loose.Type == JTokenType.String ? loose.Value<string>().Trim() : "";
                    ActionRequest pick = Simple(
                        ActionKind.PickUpItem,
                        looseName.Length > 0
                            ? ModLang.Pick("recoger del suelo: ", "pick up from ground: ") + looseName
                            : ModLang.Pick("recoger del suelo lo que mira el jugador", "pick up what the player is looking at"));
                    pick.Item = looseName;
                    return pick;
                case RememberTool:
                    JToken fact = args["fact"];
                    string factText = fact != null && fact.Type == JTokenType.String ? fact.Value<string>().Trim() : "";
                    if (factText.Length == 0)
                    {
                        error = ModLang.Pick("Falta indicar qué debe recordar.", "Missing what to remember.");
                        return null;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.Remember,
                        Item = factText,
                        Description = ModLang.Pick("recordar: ", "remember: ") + factText
                    };
                case RenameTool:
                    JToken newName = args["name"];
                    string cleanName = newName != null && newName.Type == JTokenType.String ? newName.Value<string>().Trim() : "";
                    if (!IsValidName(cleanName))
                    {
                        error = ModLang.Pick(
                            "El nombre debe tener entre 1 y " + MaxNameLength + " letras, números o espacios.",
                            "The name must be between 1 and " + MaxNameLength + " letters, numbers, or spaces.");
                        return null;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.Rename,
                        Item = cleanName,
                        Description = ModLang.Pick("llamarse " + cleanName, "be named " + cleanName)
                    };
                case OrganizeTool:
                    return Simple(ActionKind.OrganizeChests, ModLang.Pick("organizar los cofres cercanos", "organize nearby chests"));
                case CheckChestsTool:
                    return Simple(ActionKind.CheckChests, ModLang.Pick("consultar los cofres cercanos", "check nearby chests"));
                case CookTool:
                    JToken food = args["item"];
                    string dish = food != null && food.Type == JTokenType.String ? food.Value<string>().Trim() : "";
                    if (dish.Length == 0)
                    {
                        error = ModLang.Pick("Falta indicar qué comida.", "Missing which food to cook.");
                        return null;
                    }
                    int portions = DefaultCookAmount;
                    JToken requested = args["amount"];
                    if (requested != null)
                    {
                        if (requested.Type != JTokenType.Integer || requested.Value<long>() < 1 || requested.Value<long>() > MaxCookAmount)
                        {
                            error = ModLang.Pick(
                                "La cantidad debe estar entre 1 y " + MaxCookAmount + ".",
                                "The amount must be between 1 and " + MaxCookAmount + ".");
                            return null;
                        }
                        portions = requested.Value<int>();
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.Cook,
                        Item = dish,
                        Amount = portions,
                        Description = ModLang.Pick("cocinar " + portions + " de " + dish, "cook " + portions + " of " + dish)
                    };
                case GuardTool:
                    return FromTable(ActionKind.Guard, GuardPlaces, args, "place", out error);
                case RetreatTool:
                    JToken percent = args["health_percent"];
                    long value = percent != null && percent.Type == JTokenType.Integer ? percent.Value<long>() : -1;
                    if (value != 0 && (value < MinRetreatPercent || value > MaxRetreatPercent))
                    {
                        error = ModLang.Pick(
                            "health_percent debe ser 0 o estar entre " + MinRetreatPercent + " y " + MaxRetreatPercent + ".",
                            "health_percent must be 0 or between " + MinRetreatPercent + " and " + MaxRetreatPercent + ".");
                        return null;
                    }
                    return new ActionRequest
                    {
                        Kind = ActionKind.SetRetreat,
                        Value = (int)value,
                        Description = value == 0
                            ? ModLang.Pick("no retirarse por sí mismo", "do not retreat on their own")
                            : ModLang.Pick("retirarse con menos del " + value + " % de salud", "retreat below " + value + "% health")
                    };
                case ConsumeTool:
                case EquipTool:
                    JToken named = args["item"];
                    string itemName = named != null && named.Type == JTokenType.String ? named.Value<string>().Trim() : "";
                    if (itemName.Length == 0)
                    {
                        error = ModLang.Pick("Falta indicar qué objeto.", "Missing which item.");
                        return null;
                    }
                    bool consume = tool == ConsumeTool;
                    return new ActionRequest
                    {
                        Kind = consume ? ActionKind.Consume : ActionKind.Equip,
                        Item = itemName,
                        Description = (consume ? ModLang.Pick("consumir: ", "consume: ") : ModLang.Pick("equipar: ", "equip: "))
                            + (itemName == AnyFood ? ModLang.Pick("algo de comida", "some food") : itemName)
                    };
                case GiveItemsTool:
                case DropItemsTool:
                case TakeTool:
                    bool drop = tool == DropItemsTool;
                    bool take = tool == TakeTool;
                    JToken what = args["item"];
                    string wanted = what != null && what.Type == JTokenType.String ? what.Value<string>().Trim() : "";
                    if (wanted.Length == 0)
                    {
                        error = ModLang.Pick("Falta indicar qué objeto.", "Missing which item.");
                        return null;
                    }
                    int count = 0;
                    JToken howMany = args["amount"];
                    if (howMany != null)
                    {
                        if (howMany.Type != JTokenType.Integer || howMany.Value<long>() < 1 || howMany.Value<long>() > MaxGiveAmount)
                        {
                            error = ModLang.Pick("La cantidad no es válida.", "Invalid amount.");
                            return null;
                        }
                        count = howMany.Value<int>();
                    }
                    return new ActionRequest
                    {
                        Kind = take ? ActionKind.TakeFromChest : drop ? ActionKind.DropItems : ActionKind.GiveItems,
                        Item = wanted,
                        Amount = count,
                        Description = (take
                                ? ModLang.Pick("coger de un cofre: ", "take from chest: ")
                                : drop
                                    ? ModLang.Pick("soltar al suelo: ", "drop on ground: ")
                                    : ModLang.Pick("darle al jugador: ", "give to player: "))
                            + (wanted == LastTaskItems ? ModLang.Pick("lo que ganó en su última tarea", "what they gained in their last task") : wanted)
                    };
                case GatherTool:
                    ActionRequest gather = FromTable(ActionKind.Gather, Items, args, "item", out error);
                    if (gather == null) return null;
                    JToken amount = args["amount"];
                    if (amount == null || amount.Type != JTokenType.Integer || amount.Value<long>() < 1 || amount.Value<long>() > MaxGatherAmount)
                    {
                        error = ModLang.Pick(
                            "La cantidad debe estar entre 1 y " + MaxGatherAmount + ".",
                            "The amount must be between 1 and " + MaxGatherAmount + ".");
                        return null;
                    }
                    gather.Item = args.Value<string>("item");
                    gather.ItemLabel = Items[gather.Item].Text;
                    gather.Amount = amount.Value<int>();
                    gather.Description = ModLang.Pick(
                        "conseguir " + gather.Amount + " de " + gather.ItemLabel,
                        "gather " + gather.Amount + " " + gather.ItemLabel);
                    return gather;
                case SetStanceTool:
                    return FromTable(ActionKind.SetStance, Stances, args, "stance", out error);
                case AttackTool:
                    return FromTable(ActionKind.Attack, AttackTargets, args, "target", out error);
                case GoToTool:
                    ActionRequest move = FromTable(ActionKind.GoTo, Places, args, "place", out error);
                    JToken stay = args["stay"];
                    if (move != null && stay != null && stay.Type == JTokenType.Boolean && stay.Value<bool>())
                    {
                        move.Stay = true;
                        move.Description += ModLang.Pick(" y quedarse allí sin trabajar", " and stay there without working");
                    }
                    return move;
                case RestTool:
                    return FromTable(ActionKind.Rest, RestKinds, args, "kind", out error);
                case UseLookedAtTool:
                    return Simple(ActionKind.UseLookedAt, ModLang.Pick("usar lo que mira el jugador", "use what the player is looking at"));
                case StoreItemsTool:
                    return Simple(ActionKind.StoreItems, ModLang.Pick("guardar sus cosas en los cofres cercanos", "store their items in nearby chests"));
                case RecoverGearTool:
                    return Simple(ActionKind.RecoverGear, ModLang.Pick("recuperar sus cosas de su tumba", "recover their gear from their tombstone"));
                case RepairGearTool:
                    return Simple(ActionKind.RepairGear, ModLang.Pick("reparar su equipo", "repair their gear"));
                case ComeHereTool:
                    return Simple(ActionKind.ComeHere, ModLang.Pick("volver con el jugador", "return to the player"));
                case CancelOrdersTool:
                    return Simple(ActionKind.CancelOrders, ModLang.Pick("cancelar lo que estaba haciendo", "cancel what they were doing"));
                case SetFollowTool:
                    JToken follow = args["follow"];
                    if (follow == null || follow.Type != JTokenType.Boolean)
                    {
                        error = ModLang.Pick("Falta indicar si debe seguir al jugador.", "Missing whether they should follow the player.");
                        return null;
                    }
                    bool on = follow.Value<bool>();
                    return new ActionRequest
                    {
                        Kind = ActionKind.SetFollow,
                        Value = on ? 1 : 0,
                        Description = on ? ModLang.Pick("seguir al jugador", "follow the player") : ModLang.Pick("quedarse donde está", "stay where they are")
                    };
                default:
                    error = ModLang.Pick("Acción desconocida.", "Unknown action.");
                    return null;
            }
        }

        private static bool IsHereWord(JToken name)
        {
            if (name == null || name.Type == JTokenType.Null) return true;
            if (name.Type != JTokenType.String) return false;
            string text = OrderIntent.Normalize(name.Value<string>());
            return text.Length == 0 || text == "aqui" || text == "here" || text == "este sitio" || text == "este lugar" || text == "this place" || text == "this spot";
        }

        /// <summary>Un encargo de recoger que nace del propio mod, con las mismas comprobaciones que los de la IA. Null si no es un objeto conocido.</summary>
        public static ActionRequest GatherOrder(string item, int amount)
        {
            return Parse(GatherTool, new JObject { ["item"] = item, ["amount"] = amount }.ToString(), out string _);
        }

        private static string PlaceNameFrom(JObject args, out string error)
        {
            error = null;
            JToken token = args["name"];
            string name = token != null && token.Type == JTokenType.String ? token.Value<string>().Trim() : null;
            if (IsValidName(name)) return name;
            error = ModLang.Pick(
                "El lugar necesita un nombre corto, solo con letras, números y espacios.",
                "The place needs a short name using only letters, numbers, and spaces.");
            return null;
        }

        // Evita que un nombre con etiquetas de formato o saltos de línea rompa los textos del juego.
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength) return false;
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '\'') return false;
            }
            return true;
        }

        /// <summary>Las tareas se cancelan entre sí en Offline Companions Reborn, así que van en fila, una tras otra.</summary>
        public static bool IsExclusiveTask(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.SetJob:
                case ActionKind.SetFollow:
                case ActionKind.SetStance:
                case ActionKind.SetHome:
                case ActionKind.SetOption:
                case ActionKind.Consume:
                case ActionKind.Equip:
                case ActionKind.SetRetreat:
                case ActionKind.CheckChests:
                case ActionKind.Rename:
                case ActionKind.Remember:
                case ActionKind.LookAround:
                case ActionKind.CompanionsStatus:
                case ActionKind.RecallAll:
                case ActionKind.CheckSkills:
                case ActionKind.SetHomeRadius:
                case ActionKind.MarkPlace:
                case ActionKind.ForgetPlace:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Si la IA necesita el detalle del resultado para contestar bien: datos consultados o un desenlace distinto de lo pedido.
        /// El resto de detalles son avisos para el jugador, que se muestran en el chat para que el personaje no los recite.
        /// </summary>
        public static bool ReplyNeedsNote(ActionKind kind)
        {
            switch (kind)
            {
                case ActionKind.CheckChests:
                case ActionKind.LookAround:
                case ActionKind.CompanionsStatus:
                case ActionKind.CheckSkills:
                case ActionKind.RecallAll:
                case ActionKind.DropItems:
                case ActionKind.Rest:
                case ActionKind.StoreItems:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Las que tratan de todos los compañeros: las atiende quien escucha, aunque la IA nombre a alguien que está lejos.</summary>
        public static bool IsWorldWide(ActionKind kind)
        {
            return kind == ActionKind.CompanionsStatus || kind == ActionKind.RecallAll || kind == ActionKind.MarkPlace || kind == ActionKind.ForgetPlace;
        }

        public static bool ClearsQueue(ActionKind kind)
        {
            return kind == ActionKind.ComeHere || kind == ActionKind.CancelOrders;
        }

        private static ActionRequest Simple(ActionKind kind, string description)
        {
            return new ActionRequest { Kind = kind, Description = description };
        }

        private static ActionRequest FromTable(ActionKind kind, Dictionary<string, LocalizedOption> table, JObject args, string parameter, out string error)
        {
            error = null;
            JToken value = args[parameter];
            string key = value != null && value.Type == JTokenType.String ? value.Value<string>() : null;
            if (key == null || !table.TryGetValue(key, out LocalizedOption entry))
            {
                error = ModLang.Pick("Valor no válido para " + parameter + ".", "Invalid value for " + parameter + ".");
                return null;
            }
            return new ActionRequest { Kind = kind, Value = entry.Value, Description = entry.Text };
        }
    }
}
