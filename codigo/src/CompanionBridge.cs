using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OfflineCompanionsAI
{
    /// <summary>Enemigos de una misma especie cerca de un compañero y cómo pinta pelear contra ellos.</summary>
    public sealed class EnemyGroup
    {
        public string Label;
        public int Count;
        public float Nearest;
        public float Ratio;
        public bool TooStrong;
        public string Details;
    }

    /// <summary>
    /// Única parte del mod que conoce los detalles internos de Offline Companions Reborn.
    /// Si una actualización los cambia, el mod se desactiva y explica el motivo en el registro.
    /// </summary>
    public static class CompanionBridge
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private const float SpeechDistance = 30f;
        // Igual que SurroundingsRange: si no, le dice a la IA que hay un cofre a 35 m y luego no puede usarlo.
        private const float SearchRange = 40f;
        private const float MaxWorkRange = 150f;
        private const string RebornGuid = "Maddhattr.OfflineCompanionsReborn";
        private const string OwnTag = "HC_Own";
        private const float TombstoneRange = 60f;

        private static Type _setupType;
        private static Type _talkType;
        private static PropertyInfo _allCompanions;
        private static MethodInfo _isMine;
        private static MethodInfo _describe;
        private static MethodInfo _permanentId;
        private static FieldInfo _lastSayTime;
        private static MethodInfo _takeControl;
        private static Type _staminaType;
        private static MethodInfo _staminaShare;
        private static FieldInfo _swimTiredShare;

        private const float DefaultSwimTiredShare = 0.25f;
        private const float DrySpotStep = 2f;
        private const float DrySpotRange = 60f;
        private const int DrySpotDirections = 16;
        private const float DryAboveWater = 0.5f;
        private const int SolidSearchHeight = 1000;
        private const float MaxSolidOnGround = 0.5f;
        private const float SlopeSampleDistance = 1.2f;
        private const float MaxSlopeRise = 1.2f;
        private const float PlacedAboveGround = 0.2f;
        private const float BesidePlayerDistance = 1.5f;
        private const float SurroundingsDistance = 5f;
        // Lo que cruza a nado de camino a un lugar sin buscar otro paso, algo menos de lo que Offline Companions acepta para un trabajo.
        private const float ShortSwimMeters = 10f;
        private const float MaxSearchClimb = 15f;
        private const float SwimSampleMeters = 2f;
        private const float DetourAngleStep = 30f;
        private const float MinLoadedReach = 40f;
        private const float LoadedReachMargin = 0.8f;
        private static PropertyInfo _isEngaged;
        private static FieldInfo _commandAnywhere;
        private static MethodInfo _isCommandable;
        private static int _actionModeHash;
        private static int _tombCompanionIdHash;
        private static MethodInfo _onJobSet;
        private static MethodInfo _followSetByPlayer;
        private static MethodInfo _hasJob;
        private static MethodInfo _keepsFollow;
        private static MethodInfo _getFollow;
        private static MethodInfo _setFollow;
        private static MethodInfo _applyFollowMode;
        private static MethodInfo _setCombatStance;
        private static int _combatStanceHash;
        private static MethodInfo _getHome;
        private static MethodInfo _setMoveTarget;
        private static MethodInfo _inScope;
        private static MethodInfo _getHarvestable;
        private static MethodInfo _stayAwake;
        private static MethodInfo _holdForOrder;
        private static Type _harvestType;
        private static Type _huntType;
        private static FieldInfo _huntState;
        private static FieldInfo _huntAnchor;
        private static FieldInfo _huntHasAnchor;
        private static readonly MethodInfo _minimapExplore = typeof(Minimap).GetMethod("Explore", Any, null, new[] { typeof(Vector3), typeof(float) }, null);
        private static readonly MethodInfo _minimapIsExplored = typeof(Minimap).GetMethod("IsExplored", Any, null, new[] { typeof(Vector3) }, null);
        private static readonly MethodInfo _minimapHasPin = typeof(Minimap).GetMethod("HaveSimilarPin", Any, null, new[] { typeof(Vector3), typeof(Minimap.PinType), typeof(string), typeof(bool) }, null);
        private static PropertyInfo _inGatherMode;
        private static Type _restType;
        private static MethodInfo _cancelRest;
        private static PropertyInfo _isResting;
        private static PropertyInfo _isNavigating;
        private static Type _aiType;
        private static FieldInfo _pendingDeposit;
        private static MethodInfo _setPendingDeposit;
        private static MethodInfo _cancelDeposit;
        private static MethodInfo _shouldKeep;
        private static Type _combatType;
        private static FieldInfo _ignoreUntil;
        private static FieldInfo _orderedTarget;
        private static FieldInfo _combatTarget;
        private static MethodInfo _letGo;
        private static readonly Dictionary<string, float> EnemyHits = new Dictionary<string, float>();
        private static readonly Dictionary<Component, HashSet<string>> StrongKinds = new Dictionary<Component, HashSet<string>>();
        private const float StrongEnemyRange = 40f;
        private const float AllyRange = 25f;
        private const float CloseFightRange = 6f;
        private const float IgnoreSeconds = 2f;
        private static readonly List<KeyValuePair<Type, MethodInfo>> JobControllers = new List<KeyValuePair<Type, MethodInfo>>();
        private static readonly Dictionary<string, MethodInfo> Direct = new Dictionary<string, MethodInfo>();

        private static Type _cookType;
        private static Type _upkeepType;
        private static MethodInfo _cookEnsure;
        private static MethodInfo _cookFinal;
        private static MethodInfo _cookValue;
        private static MethodInfo _cookIsMeadBase;
        private static MethodInfo _cookIsMead;
        private static MethodInfo _cookCount;
        private static FieldInfo _cookRecipes;
        private static FieldInfo _cookConversions;
        private static PropertyInfo _cookActive;
        private static PropertyInfo _upkeepActive;
        private static Type _foodType;
        private static MethodInfo _eatNow;
        private static MethodInfo _eatItem;
        private static MethodInfo _drinkPotion;
        private static MethodInfo _chestUsable;
        private static MethodInfo _setDirected;
        private static PropertyInfo _harvestActive;
        private static MethodInfo _toolsBusy;
        private static MethodInfo _toolsFindBest;
        private const int HammerKind = 8;
        private static PropertyInfo _hasOrder;
        private static PropertyInfo _isInCombat;
        private static Type _repairType;
        private static PropertyInfo _repairActive;
        private static FieldInfo _directedTarget;
        private static MethodInfo _setHome;
        private static MethodInfo _hasHome;
        private static MethodInfo _setWander;
        private static MethodInfo _setCommandable;
        private static int _autoPickupHash;
        private static int _nameHash;
        private const float EmoteDangerRange = 25f;
        private const float EmoteTurnSpeed = 360f;
        private const float SmeltRange = 25f;
        private const float SurroundingsRange = 40f;
        private const float BuriedMargin = 0.5f;
        private const float BesideDistance = 1.5f;
        private const float LookedItemMargin = 3f;
        private static MethodInfo _rosterGet;
        private static MethodInfo _rosterWhere;
        private static FieldInfo _rosterId;
        private static FieldInfo _rosterName;
        private static FieldInfo _rosterLastPos;
        private static FieldInfo _rosterDead;
        private static FieldInfo _rosterPositions;
        private static MethodInfo _rosterIsLoaded;
        private static MethodInfo _upkeepHammer;
        private static Type _smeltType;
        private static FieldInfo _smeltPhase;
        private static MethodInfo _superRecall;
        private static Type _skillsType;
        private static MethodInfo _skillFactor;
        private static MethodInfo _setHomeRadius;
        private static MethodInfo _isHungry;
        private static MethodInfo _needsRepair;
        private static MethodInfo _isOverweight;

        public static Type CompanionType => _setupType;

        /// <summary>Devuelve null si todo está disponible; si no, el nombre de lo que falta.</summary>
        public static string Initialize()
        {
            Assembly asm = typeof(Companions.CompanionsPlugin).Assembly;

            _setupType = asm.GetType("Companions.CompanionSetup");
            _talkType = asm.GetType("Companions.CompanionTalk");
            Type owner = asm.GetType("Companions.MHR_Owner");
            Type status = asm.GetType("Companions.CompanionStatus");
            if (_setupType == null) return "CompanionSetup";
            if (_talkType == null) return "CompanionTalk";
            if (owner == null) return "MHR_Owner";
            if (status == null) return "CompanionStatus";

            _allCompanions = _setupType.GetProperty("AllCompanions", Any);
            _isMine = owner.GetMethod("IsMine", Any, null, new[] { typeof(Component) }, null);
            _describe = status.GetMethod("Describe", Any, null, new[] { _setupType }, null);
            _permanentId = _setupType.GetMethod("MHR_EnsureCompanionId", Any, null, Type.EmptyTypes, null);
            _lastSayTime = _talkType.GetField("_lastSayTime", Any);
            if (_allCompanions == null) return "CompanionSetup.AllCompanions";
            if (_isMine == null) return "MHR_Owner.IsMine";
            if (_describe == null) return "CompanionStatus.Describe";
            if (_permanentId == null) return "CompanionSetup.MHR_EnsureCompanionId";
            if (_lastSayTime == null) return "CompanionTalk._lastSayTime";
            return InitializeActions(asm, owner) ?? InitializeOrders(asm) ?? InitializePlanning(asm);
        }

        private static string InitializePlanning(Assembly asm)
        {
            _repairType = asm.GetType("Companions.RepairController");
            if (_repairType == null) return "RepairController";

            FieldInfo pickup = _setupType.GetField("AutoPickupHash", Any);
            _hasOrder = _aiType.GetProperty("HasOrder", Any);
            _isInCombat = _aiType.GetProperty("IsInCombat", Any);
            _repairActive = _repairType.GetProperty("IsActive", Any);
            _directedTarget = _harvestType.GetField("_directedTarget", Any);
            _setHome = _setupType.GetMethod("SetHomePosition", Any, null, new[] { typeof(Vector3) }, null);
            _hasHome = _setupType.GetMethod("HasHomePosition", Any, null, Type.EmptyTypes, null);
            _setWander = _setupType.GetMethod("SetWander", Any, null, new[] { typeof(bool) }, null);
            _setCommandable = _setupType.GetMethod("SetIsCommandable", Any, null, new[] { typeof(bool) }, null);
            _harvestActive = _harvestType.GetProperty("IsActive", Any);
            _toolsBusy = asm.GetType("Companions.MHR_ToolKeeper")?.GetMethod("IsBusy", Any, null, new[] { typeof(Component) }, null);
            _toolsFindBest = asm.GetType("Companions.MHR_ToolKeeper")?.GetMethod("FindBestInChests", Any);
            Type catalog = asm.GetType("Companions.CookCatalog");
            _cookType = asm.GetType("Companions.CookController");
            _upkeepType = asm.GetType("Companions.UpkeepController");
            if (catalog == null) return "CookCatalog";
            if (_cookType == null) return "CookController";
            if (_upkeepType == null) return "UpkeepController";
            _cookEnsure = catalog.GetMethod("Ensure", Any, null, Type.EmptyTypes, null);
            _cookFinal = catalog.GetMethod("FinalFood", Any, null, new[] { typeof(string) }, null);
            _cookValue = catalog.GetMethod("Value", Any, null, new[] { typeof(string) }, null);
            _cookIsMeadBase = catalog.GetMethod("IsMeadBase", Any, null, new[] { typeof(string) }, null);
            _cookIsMead = catalog.GetMethod("IsMead", Any, null, new[] { typeof(string) }, null);
            _cookRecipes = catalog.GetField("Recipes", Any);
            _cookConversions = catalog.GetField("s_convTo", Any);
            _cookCount = _cookType.GetMethod("Count", Any, null, new[] { typeof(string) }, null);
            _cookActive = _cookType.GetProperty("IsActive", Any);
            _upkeepActive = _upkeepType.GetProperty("IsActive", Any);
            if (_cookEnsure == null) return "CookCatalog.Ensure";
            if (_cookFinal == null) return "CookCatalog.FinalFood";
            if (_cookValue == null) return "CookCatalog.Value";
            if (_cookIsMeadBase == null) return "CookCatalog.IsMeadBase";
            if (_cookIsMead == null) return "CookCatalog.IsMead";
            if (_cookRecipes == null) return "CookCatalog.Recipes";
            if (_cookConversions == null) return "CookCatalog.s_convTo";
            if (_cookCount == null) return "CookController.Count";
            if (_cookActive == null) return "CookController.IsActive";
            if (_upkeepActive == null) return "UpkeepController.IsActive";

            _foodType = asm.GetType("Companions.CompanionFood");
            _eatNow = _foodType?.GetMethod("EatNow", Any, null, Type.EmptyTypes, null);
            _eatItem = _foodType?.GetMethod("TryConsumeItem", Any, null, new[] { typeof(ItemDrop.ItemData) }, null);
            _drinkPotion = _foodType?.GetMethod("TryConsumePotion", Any, null, new[] { typeof(ItemDrop.ItemData) }, null);
            _chestUsable = asm.GetType("Companions.MHR_Chests")?.GetMethod("Usable", Any, null, new[] { typeof(Container), typeof(Component) }, null);
            if (_eatNow == null) return "CompanionFood.EatNow";
            if (_eatItem == null) return "CompanionFood.TryConsumeItem";
            if (_drinkPotion == null) return "CompanionFood.TryConsumePotion";
            if (_chestUsable == null) return "MHR_Chests.Usable";
            _setDirected = _harvestType.GetMethod("SetDirectedTarget", Any, null, new[] { typeof(GameObject) }, null);
            if (_setDirected == null) return "HarvestController.SetDirectedTarget";
            if (_harvestActive == null) return "HarvestController.IsActive";
            if (_toolsBusy == null) return "MHR_ToolKeeper.IsBusy";
            if (_toolsFindBest == null) return "MHR_ToolKeeper.FindBestInChests";
            if (pickup == null) return "CompanionSetup.AutoPickupHash";
            if (_hasOrder == null) return "CompanionAI.HasOrder";
            if (_isInCombat == null) return "CompanionAI.IsInCombat";
            if (_repairActive == null) return "RepairController.IsActive";
            if (_directedTarget == null) return "HarvestController._directedTarget";
            if (_setHome == null) return "CompanionSetup.SetHomePosition";
            if (_hasHome == null) return "CompanionSetup.HasHomePosition";
            if (_setWander == null) return "CompanionSetup.SetWander";
            if (_setCommandable == null) return "CompanionSetup.SetIsCommandable";
            Type roster = asm.GetType("Companions.MHR_Roster");
            Type entry = asm.GetType("Companions.MHR_Roster+Entry");
            Type interact = asm.GetType("Companions.CompanionInteract");
            _skillsType = asm.GetType("Companions.CompanionSkills");
            if (roster == null || entry == null) return "MHR_Roster";
            if (_skillsType == null) return "CompanionSkills";
            _rosterGet = roster.GetMethod("GetRoster", Any, null, Type.EmptyTypes, null);
            _rosterWhere = roster.GetMethod("WhereText", Any, null, new[] { typeof(Vector3) }, null);
            _rosterId = entry.GetField("Id", Any);
            _rosterName = entry.GetField("Name", Any);
            _rosterLastPos = entry.GetField("LastPos", Any);
            _rosterDead = entry.GetField("Dead", Any);
            _rosterPositions = roster.GetField("s_where", Any);
            _rosterIsLoaded = roster.GetMethod("IsLoaded", Any, null, new[] { typeof(string) }, null);
            _upkeepHammer = _upkeepType.GetMethod("Hammer", Any, null, Type.EmptyTypes, null);
            _smeltType = asm.GetType("Companions.SmeltController");
            _smeltPhase = _smeltType?.GetField("_phase", Any);
            if (_rosterDead == null || _rosterPositions == null || _rosterIsLoaded == null) return "MHR_Roster (estado)";
            if (_upkeepHammer == null) return "UpkeepController.Hammer";
            if (_smeltPhase == null) return "SmeltController._phase";
            _superRecall = interact?.GetMethod("MHR_FireSuperRecall", Any, null, new[] { typeof(Player) }, null);
            _skillFactor = _skillsType.GetMethod("GetSkillFactor", Any, null, new[] { typeof(Skills.SkillType) }, null);
            _setHomeRadius = _setupType.GetMethod("SetHomeRadius", Any, null, new[] { typeof(float) }, null);
            if (_rosterGet == null) return "MHR_Roster.GetRoster";
            if (_rosterWhere == null) return "MHR_Roster.WhereText";
            if (_rosterId == null || _rosterName == null || _rosterLastPos == null) return "MHR_Roster.Entry";
            if (_superRecall == null) return "CompanionInteract.MHR_FireSuperRecall";
            if (_skillFactor == null) return "CompanionSkills.GetSkillFactor";
            if (_setHomeRadius == null) return "CompanionSetup.SetHomeRadius";

            _isHungry = _talkType.GetMethod("IsHungry", Any, null, Type.EmptyTypes, null);
            _needsRepair = _talkType.GetMethod("NeedsRepair", Any, null, Type.EmptyTypes, null);
            _isOverweight = _talkType.GetMethod("IsOverweight", Any, null, Type.EmptyTypes, null);
            if (_isHungry == null) return "CompanionTalk.IsHungry";
            if (_needsRepair == null) return "CompanionTalk.NeedsRepair";
            if (_isOverweight == null) return "CompanionTalk.IsOverweight";
            FieldInfo nameHash = _setupType.GetField("NameHash", Any);
            if (nameHash == null) return "CompanionSetup.NameHash";
            _nameHash = (int)nameHash.GetValue(null);
            _autoPickupHash = (int)pickup.GetValue(null);
            return null;
        }

        /// <summary>Cuántas unidades lleva de cada objeto, sin contar lo equipado.</summary>
        public static Dictionary<string, int> PackContents(Component companion)
        {
            var contents = new Dictionary<string, int>();
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory == null) return contents;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_equipped || item.m_dropPrefab == null) continue;
                contents.TryGetValue(item.m_dropPrefab.name, out int count);
                contents[item.m_dropPrefab.name] = count + item.m_stack;
            }
            return contents;
        }

        /// <summary>Texto legible para el jugador y la IA, como "3 Madera, 1 Resina".</summary>
        public static string DescribeItems(IDictionary<string, int> items)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> item in items) parts.Add(item.Value + " " + ItemLabel(item.Key));
            return string.Join(", ", parts.ToArray());
        }

        private static string ItemLabel(string prefab)
        {
            string token = ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
            if (token == null) return prefab;
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }

        /// <summary>Qué objetos de la mochila coinciden con un nombre: primero exacto y, si no, por parte del nombre.</summary>
        public static Dictionary<string, int> FindInPack(Component companion, string wanted, int amount)
        {
            return FindIn(PackContents(companion), wanted, amount);
        }

        private static Dictionary<string, int> ContentsOf(Inventory inventory)
        {
            var contents = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab == null) continue;
                contents.TryGetValue(item.m_dropPrefab.name, out int count);
                contents[item.m_dropPrefab.name] = count + item.m_stack;
            }
            return contents;
        }

        /// <summary>El cofre más cercano que el compañero pueda usar y que contenga el objeto. Respeta cofres ajenos, abiertos y protegidos.</summary>
        public static Container FindChestWith(Component companion, string wanted, int amount, out Dictionary<string, int> items)
        {
            items = null;
            Dictionary<string, int> best = null;
            Container chest = Nearest<Container>(companion, SearchRange, c =>
            {
                if (!(bool)_chestUsable.Invoke(null, new object[] { c, companion })) return false;
                Dictionary<string, int> found = FindIn(ContentsOf(c.GetInventory()), wanted, amount);
                if (found.Count == 0) return false;
                best = found;
                return true;
            });
            if (chest != null) items = best;
            return chest;
        }

        private static bool IsFood(ItemDrop.ItemData item)
        {
            return item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f);
        }

        private static List<Container> UsableChests(Component companion)
        {
            var chests = new List<Container>();
            foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(companion.transform.position, chest.transform.position) <= SearchRange && (bool)_chestUsable.Invoke(null, new object[] { chest, companion })) chests.Add(chest);
            }
            chests.Sort((a, b) => Vector3.Distance(companion.transform.position, a.transform.position).CompareTo(Vector3.Distance(companion.transform.position, b.transform.position)));
            return chests;
        }

        private static Dictionary<string, FoodStock> FoodStocks(Component companion, List<Container> chests)
        {
            var stocks = new Dictionary<string, FoodStock>();
            FoodStock StockOf(ItemDrop.ItemData item)
            {
                string prefab = item.m_dropPrefab.name;
                if (!stocks.TryGetValue(prefab, out FoodStock stock))
                {
                    stocks[prefab] = stock = new FoodStock { Item = prefab, Value = item.m_shared.m_food + item.m_shared.m_foodStamina + item.m_shared.m_foodEitr };
                }
                return stock;
            }

            foreach (Container chest in chests)
            {
                foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
                {
                    if (item.m_dropPrefab != null && IsFood(item)) StockOf(item).InChests += item.m_stack;
                }
            }
            Inventory pack = companion.GetComponent<Humanoid>()?.GetInventory();
            if (pack != null)
            {
                foreach (ItemDrop.ItemData item in pack.GetAllItems())
                {
                    if (item.m_dropPrefab != null && IsFood(item)) StockOf(item).Carried += item.m_stack;
                }
            }
            return stocks;
        }

        /// <summary>Por qué cofres pasar y qué coger de cada uno para llevar comida variada, del más cercano al más lejano.</summary>
        public static List<KeyValuePair<Container, Dictionary<string, int>>> PlanFoodTrip(Component companion, int total)
        {
            List<Container> chests = UsableChests(companion);
            Dictionary<string, int> left = FoodPlan.Choose(FoodStocks(companion, chests).Values, total);

            var stops = new List<KeyValuePair<Container, Dictionary<string, int>>>();
            foreach (Container chest in chests)
            {
                var items = new Dictionary<string, int>();
                foreach (KeyValuePair<string, int> item in ContentsOf(chest.GetInventory()))
                {
                    if (!left.TryGetValue(item.Key, out int wanted) || wanted <= 0) continue;
                    items[item.Key] = Math.Min(wanted, item.Value);
                    left[item.Key] = wanted - items[item.Key];
                }
                if (items.Count > 0) stops.Add(new KeyValuePair<Container, Dictionary<string, int>>(chest, items));
            }
            return stops;
        }

        /// <summary>Sin una cantidad pedida, de la comida solo coge su parte: el resto se queda para el jugador.</summary>
        public static void LimitFoodToShare(Component companion, Dictionary<string, int> items)
        {
            Dictionary<string, FoodStock> stocks = FoodStocks(companion, UsableChests(companion));
            foreach (string prefab in new List<string>(items.Keys))
            {
                if (stocks.TryGetValue(prefab, out FoodStock stock)) items[prefab] = Math.Min(items[prefab], FoodPlan.ShareOf(stock.InChests));
            }
        }

        public static bool CarriesFood(Component companion)
        {
            Inventory pack = companion.GetComponent<Humanoid>()?.GetInventory();
            return pack != null && pack.GetAllItems().Exists(IsFood);
        }

        public static bool HungryWithoutFood(Component companion)
        {
            Component talk = companion.GetComponent(_talkType);
            return talk != null && !CarriesFood(companion) && (bool)_isHungry.Invoke(talk, null);
        }

        /// <summary>Coge los objetos del cofre si aún se puede usar. Devuelve lo cogido.</summary>
        public static Dictionary<string, int> TakeFromChest(Component companion, Container chest, IDictionary<string, int> wanted)
        {
            var empty = new Dictionary<string, int>();
            if (chest == null || !(bool)_chestUsable.Invoke(null, new object[] { chest, companion })) return empty;
            Inventory to = companion.GetComponent<Humanoid>()?.GetInventory();
            if (to == null) return empty;

            _takeControl.Invoke(null, new object[] { companion });
            ZNetView view = chest.GetComponent<ZNetView>();
            if (view != null && !view.IsOwner()) view.ClaimOwnership();
            return Transfer(chest.GetInventory(), to, wanted);
        }

        /// <summary>Todos los compañeros de este mundo, según la lista que guarda Offline Companions, aunque estén lejos o muertos.</summary>
        public static List<string> RosterLines(Func<string, string> stayOf = null)
        {
            return ReadRoster().ConvertAll(c =>
            {
                string stay = stayOf != null && !string.IsNullOrEmpty(c.Id) ? stayOf(c.Id) : null;
                return c.Name + ": " + c.Description + (string.IsNullOrEmpty(stay) ? "" : ", " + stay);
            });
        }

        /// <summary>Quienes siguen vivos pero en una zona que el juego tiene congelada: ninguna llamada puede moverlos.</summary>
        public static List<string> OutOfReach()
        {
            List<RosterCompanion> frozen = ReadRoster().FindAll(c => c.Frozen);
            return frozen.ConvertAll(c => c.Name + " (" + c.Where + ")");
        }

        private sealed class RosterCompanion
        {
            public string Id;
            public string Name;
            public string Description;
            public string Where;
            public bool Frozen;
        }

        private static List<RosterCompanion> ReadRoster()
        {
            var found = new List<RosterCompanion>();
            try
            {
                // Se lee después de pedir la lista, que es lo que actualiza estas posiciones.
                IEnumerable roster = (IEnumerable)_rosterGet.Invoke(null, null);
                var positions = (Dictionary<string, Vector3>)_rosterPositions.GetValue(null);
                foreach (object pair in roster)
                {
                    object entry = pair.GetType().GetProperty("Key").GetValue(pair, null);
                    string status = pair.GetType().GetProperty("Value").GetValue(pair, null).ToString();
                    string id = _rosterId.GetValue(entry) as string;
                    string name = _rosterName.GetValue(entry) as string;
                    bool loaded = (bool)_rosterIsLoaded.Invoke(null, new object[] { id });
                    bool died = (bool)_rosterDead.GetValue(entry);

                    Vector3 position = positions.TryGetValue(id, out Vector3 current) ? current : (Vector3)_rosterLastPos.GetValue(entry);
                    string where = _rosterWhere.Invoke(null, new object[] { position }) as string;
                    found.Add(new RosterCompanion
                    {
                        Id = id,
                        Name = string.IsNullOrEmpty(name) ? ModLang.Pick("Compañero", "Companion") : name,
                        Where = where,
                        Description = DescribeRosterStatus(status, where, loaded, died),
                        Frozen = !loaded && (status == "Alive" || (status == "Unknown" && !died))
                    });
                }
            }
            catch (TargetInvocationException ex)
            {
                Plugin.Log.LogError("[Lista] No se pudo leer: " + ex.InnerException);
            }
            return found;
        }

        private static string DescribeRosterStatus(string status, string where, bool loaded, bool died)
        {
            switch (status)
            {
                case "Alive":
                    return ModLang.Pick(
                        "con vida, " + where + (loaded ? "" : ", en una zona sin cargar"),
                        "alive, " + where + (loaded ? "" : ", in an unloaded zone"));
                case "Dead":
                    return ModLang.Pick(
                        "muerto, su tumba lo traerá de vuelta",
                        "dead, their gravestone will bring them back");
                case "Lost":
                    return ModLang.Pick(
                        "perdido, no queda rastro de él; se puede recuperar en el Santuario de Odín",
                        "lost, no trace left; can be recovered at Odin's Shrine");
                case "Unknown":
                    return ModLang.Pick(
                        "sin confirmar, " + (died ? "murió cerca de: " : "visto por última vez en: ") + where,
                        "unconfirmed, " + (died ? "died near: " : "last seen at: ") + where);
                case "Unsummoned":
                    return ModLang.Pick(
                        "despedido; el Santuario de Odín puede traerlo sin su equipo hasta que recargues el mundo",
                        "dismissed; Odin's Shrine can bring them back without gear until you reload the world");
                default: return status;
            }
        }

        /// <summary>Llama a los que están en zonas cargadas, a cualquier distancia. Devuelve a quiénes no puede alcanzar.</summary>
        public static List<string> RecallEveryone(Player player)
        {
            _superRecall.Invoke(null, new object[] { player });
            return OutOfReach();
        }

        /// <summary>Si lleva un martillo utilizable, sin el que no repara ni apila.</summary>
        public static bool HasHammer(Component companion)
        {
            Component upkeep = companion.GetComponent(_upkeepType);
            return upkeep == null || _upkeepHammer.Invoke(upkeep, null) != null;
        }

        /// <summary>
        /// Por qué no conseguiría un martillo, o null si lo lleva o puede ir por él.
        /// Repite lo que decide MHR_ToolKeeper con el trabajo de reparar: solo va cerca de su hogar y mira los cofres a 30 m de él.
        /// </summary>
        public static string HammerProblem(Component companion)
        {
            if (HasHammer(companion)) return null;
            var setup = companion.GetComponent<Companions.CompanionSetup>();
            if (setup == null) return ModLang.Pick("No lleva ningún martillo utilizable en la mochila.", "Not carrying any usable hammer in the pack.");
            if (!Companions.MHR_ToolKeeper.NearHome(setup)) return ModLang.Pick("No lleva martillo y está demasiado lejos de su hogar para ir por uno.", "Not carrying a hammer and too far from their home point to fetch one.");

            Vector3 from = companion.transform.position;
            object[] search = { setup.HasHomePosition() ? setup.GetHomePosition() : from, from, HammerKind, null, null };
            if (_toolsFindBest.Invoke(null, search) == null) return ModLang.Pick("No lleva martillo, ni hay uno en los cofres que puede usar cerca de su hogar.", "Not carrying a hammer, and there is none in the chests they can use near their home point.");
            Inventory pack = companion.GetComponent<Humanoid>()?.GetInventory();
            if (pack != null && !pack.HaveEmptySlot()) return ModLang.Pick("No lleva martillo y tiene la mochila demasiado llena para ir por uno.", "Not carrying a hammer, and the pack is too full to fetch one.");
            return null;
        }

        /// <summary>Si está llevando material o recogiendo, y no solo vigilando los hornos.</summary>
        public static bool IsSmeltBusy(Component companion)
        {
            Component smelt = companion.GetComponent(_smeltType);
            if (smelt == null) return false;
            string phase = _smeltPhase.GetValue(smelt).ToString();
            return phase != "Idle" && phase != "Monitoring";
        }

        /// <summary>Las habilidades del juego y las que añaden otros mods, como la de lanzar de Warfare.</summary>
        private static IEnumerable<Skills.SkillType> KnownSkillTypes()
        {
            var types = new HashSet<Skills.SkillType>();
            foreach (Skills.SkillType type in Enum.GetValues(typeof(Skills.SkillType))) types.Add(type);
            Skills own = Player.m_localPlayer != null ? Player.m_localPlayer.GetComponent<Skills>() : null;
            if (own != null && own.m_skills != null)
            {
                foreach (Skills.SkillDef def in own.m_skills)
                {
                    if (def != null) types.Add(def.m_skill);
                }
            }
            return types;
        }

        /// <summary>Sus habilidades con algún progreso, de mayor a menor.</summary>
        public static string DescribeSkills(Component companion)
        {
            Component skills = companion.GetComponent(_skillsType);
            if (skills == null) return ModLang.Pick("No se pueden consultar sus habilidades.", "Cannot check their skills.");

            var found = new List<KeyValuePair<float, string>>();
            foreach (Skills.SkillType type in KnownSkillTypes())
            {
                if (type == Skills.SkillType.None || type == Skills.SkillType.All) continue;
                float level = (float)_skillFactor.Invoke(skills, new object[] { type }) * 100f;
                if (level < 1f) continue;
                // Las de otros mods no tienen nombre en la lista del juego: su texto es "$skill_" y su número.
                string token = "$skill_" + type.ToString().ToLowerInvariant();
                string label = Localization.instance != null ? Localization.instance.Localize(token) : type.ToString();
                if (label.StartsWith("[") && !Enum.IsDefined(typeof(Skills.SkillType), type)) continue;
                found.Add(new KeyValuePair<float, string>(level, label + " " + Mathf.FloorToInt(level)));
            }
            if (found.Count == 0) return ModLang.Pick("Todavía no ha desarrollado ninguna habilidad.", "Hasn't developed any skills yet.");
            found.Sort((a, b) => b.Key.CompareTo(a.Key));
            return ModLang.Pick("Habilidades: ", "Skills: ") + string.Join(", ", found.ConvertAll(f => f.Value).ToArray()) + ".";
        }

        /// <summary>Lo que se puede fundir en los hornos cercanos: nombre interno y nombre visible.</summary>
        public static Dictionary<string, string> SmeltableNearby(Component companion)
        {
            var items = new Dictionary<string, string>();
            foreach (Smelter smelter in Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(companion.transform.position, smelter.transform.position) > SmeltRange) continue;
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                {
                    if (conversion?.m_from == null) continue;
                    string prefab = conversion.m_from.gameObject.name;
                    if (!Loads(smelter, prefab)) continue;
                    if (!items.ContainsKey(prefab)) items[prefab] = ItemLabel(prefab);
                }
            }
            return items;
        }

        // Sin combustible, Offline Companions solo carga madera: en la práctica el horno de carbón, y nunca el molino ni la rueca.
        private static bool Loads(Smelter smelter, string prefab)
        {
            return smelter.m_maxFuel != 0 || prefab == "Wood";
        }

        private static bool Accepts(Smelter smelter, string prefab)
        {
            foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
            {
                if (conversion?.m_from != null && conversion.m_from.gameObject.name == prefab) return Loads(smelter, prefab);
            }
            return false;
        }

        /// <summary>Lo que queda por hacer: el material en su mochila y cofres, lo que hay en los hornos y lo que falta por recoger.</summary>
        public static int SmeltWorkLeft(Component companion, string item)
        {
            int left = CountInPack(companion, item);
            foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(companion.transform.position, chest.transform.position) > SmeltRange) continue;
                if (!(bool)_chestUsable.Invoke(null, new object[] { chest, companion })) continue;
                ContentsOf(chest.GetInventory()).TryGetValue(item, out int count);
                left += count;
            }
            foreach (Smelter smelter in Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(companion.transform.position, smelter.transform.position) > SmeltRange || !Accepts(smelter, item)) continue;
                ZDO zdo = smelter.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null) left += zdo.GetInt(ZDOVars.s_queued) + zdo.GetInt(ZDOVars.s_spawnAmount);
            }
            return left;
        }

        /// <summary>
        /// Hasta dónde va a trabajar por su cuenta, según el ajuste de Offline Companions ("Gathering" o "Hunting"),
        /// para proponerle lo que de verdad alcanza. Se lee cada vez porque se puede cambiar en la partida con F8.
        /// </summary>
        public static float WorkRange(string section)
        {
            if (Chainloader.PluginInfos.TryGetValue(RebornGuid, out PluginInfo info) && info.Instance != null
                && info.Instance.Config.TryGetEntry(section, "Range", out ConfigEntry<float> range))
            {
                return Mathf.Clamp(range.Value, SurroundingsRange, MaxWorkRange);
            }
            return SurroundingsRange;
        }

        /// <summary>Lo que hay alrededor del compañero, con datos reales, para que no prometa sin saber.</summary>
        public static string DescribeSurroundings(Component companion)
        {
            const float range = SurroundingsRange;
            Vector3 from = companion.transform.position;
            bool Near(Component c) => Vector3.Distance(from, c.transform.position) <= range;
            int Count<T>(Func<T, bool> accept = null) where T : Component
            {
                int n = 0;
                foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (Near(c) && (accept == null || accept(c))) n++;
                return n;
            }

            float gatherRange = WorkRange("Gathering");
            float huntRange = WorkRange("Hunting");
            bool Beyond(Component c, float limit)
            {
                float distance = Vector3.Distance(from, c.transform.position);
                return distance > range && distance <= limit;
            }
            int CountBeyond<T>() where T : Component
            {
                int n = 0;
                foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (Beyond(c, gatherRange)) n++;
                return n;
            }
            var farther = new List<string>();
            void AddFar(string label, int n)
            {
                if (n > 0) farther.Add(n + " " + label);
            }
            AddFar(ModLang.Pick("árboles en pie", "standing trees"), CountBeyond<TreeBase>());
            AddFar(ModLang.Pick("rocas o vetas", "rocks or ore veins"), CountBeyond<MineRock5>() + CountBeyond<MineRock>());

            var parts = new List<string>();
            void Add(string label, int n)
            {
                if (n > 0) parts.Add(n + " " + label);
            }

            Add(ModLang.Pick("árboles en pie", "standing trees"), Count<TreeBase>());
            Add(ModLang.Pick("troncos caídos", "fallen logs"), Count<TreeLog>());
            Add(ModLang.Pick("rocas o vetas", "rocks or ore veins"), Count<MineRock5>() + Count<MineRock>());
            void AddChests(Func<Container, bool> accept, string one, string many)
            {
                int chests = Count(accept);
                if (chests == 0) return;
                Vector3 chest = Nearest(companion, range, accept).transform.position;
                parts.Add(chests + " " + (chests == 1 ? one : many)
                    + ModLang.Pick(", el más cercano a ", ", nearest at ")
                    + Mathf.RoundToInt(Vector3.Distance(from, chest))
                    + ModLang.Pick(" m al ", " m to the ")
                    + Compass.Direction(chest.x - from.x, chest.z - from.z));
            }
            // Sin distinguirlos, la IA presenta como un hallazgo un cofre de la base.
            AddChests(c => IsVisibleChest(c) && IsBuilt(c),
                ModLang.Pick("cofre construido por jugadores", "player-built chest"),
                ModLang.Pick("cofres construidos por jugadores", "player-built chests"));
            AddChests(c => IsVisibleChest(c) && !IsBuilt(c),
                ModLang.Pick("cofre encontrado, sin construir", "unbuilt world chest"),
                ModLang.Pick("cofres encontrados, sin construir", "unbuilt world chests"));
            Add(ModLang.Pick("tumbas", "gravestones"), Count<TombStone>());
            Add(ModLang.Pick("camas", "beds"), Count<Bed>());
            Add(ModLang.Pick("colmenas", "beehives"), Count<Beehive>());
            Add(ModLang.Pick("barcos", "ships"), Count<Ship>());

            var groups = new Dictionary<string, int>();
            var farGroups = new Dictionary<string, int>();
            foreach (Pickable pickable in Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))
            {
                // Un arbusto ya recogido sigue ahí mientras vuelve a crecer, pero no hay nada que coger.
                if (pickable.m_itemPrefab == null || !pickable.CanBePicked()) continue;
                Dictionary<string, int> group = Near(pickable) ? groups : Beyond(pickable, gatherRange) ? farGroups : null;
                if (group == null) continue;
                string prefab = pickable.m_itemPrefab.name;
                group.TryGetValue(prefab, out int n);
                group[prefab] = n + 1;
            }
            if (groups.Count > 0) parts.Add(ModLang.Pick("para recoger: ", "pickable: ") + DescribeItems(groups));
            if (farGroups.Count > 0) farther.Add(ModLang.Pick("para recoger: ", "pickable: ") + DescribeItems(farGroups));

            var loose = new Dictionary<string, int>();
            foreach (ItemDrop drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (!Near(drop)) continue;
                string prefab = Utils.GetPrefabName(drop.gameObject);
                loose.TryGetValue(prefab, out int n);
                loose[prefab] = n + drop.m_itemData.m_stack;
            }
            if (loose.Count > 0) parts.Add(ModLang.Pick("objetos sueltos: ", "loose items: ") + DescribeItems(loose));

            var stations = new List<string>();
            foreach (CraftingStation station in Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None))
            {
                if (!Near(station)) continue;
                string label = Localization.instance != null ? Localization.instance.Localize(station.m_name) : station.m_name;
                if (!stations.Contains(label)) stations.Add(label);
            }
            if (stations.Count > 0) parts.Add(ModLang.Pick("estaciones: ", "stations: ") + string.Join(", ", stations.ToArray()));

            var enemies = new Dictionary<string, int>();
            var animals = new Dictionary<string, int>();
            var farAnimals = new Dictionary<string, int>();
            Character self = companion.GetComponent<Character>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == self || c.IsPlayer() || c.GetHealth() <= 0f || self == null || !BaseAI.IsEnemy(self, c)) continue;
                bool threat = IsThreat(self, c);
                Dictionary<string, int> kind = Near(c) ? (threat ? enemies : animals) : !threat && Beyond(c, huntRange) ? farAnimals : null;
                if (kind == null) continue;
                string name = Localization.instance != null ? Localization.instance.Localize(c.m_name) : c.m_name;
                kind.TryGetValue(name, out int n);
                kind[name] = n + 1;
            }
            foreach (KeyValuePair<string, int> enemy in enemies) parts.Add(enemy.Value + " " + enemy.Key + ModLang.Pick(" (enemigo)", " (enemy)"));
            foreach (KeyValuePair<string, int> animal in animals) parts.Add(animal.Value + " " + animal.Key + ModLang.Pick(" (animal que huye)", " (fleeing animal)"));
            foreach (KeyValuePair<string, int> animal in farAnimals) farther.Add(animal.Value + " " + animal.Key + ModLang.Pick(" (animal que huye)", " (fleeing animal)"));

            string near = parts.Count == 0
                ? ModLang.Pick("No hay nada destacable a menos de " + range + " m.", "Nothing notable within " + range + " m.")
                : ModLang.Pick("A menos de " + range + " m: ", "Within " + range + " m: ") + string.Join("; ", parts.ToArray()) + ".";
            return farther.Count == 0
                ? near
                : near + ModLang.Pick(" Más lejos, hasta donde puedes ir a trabajar: ", " Farther out, within your work range: ") + string.Join("; ", farther.ToArray()) + ".";
        }

        /// <summary>
        /// Solo lo que un jugador reconocería como cofre. En el juego también son contenedores los propios compañeros,
        /// las tumbas, los carros y los barcos, y los tesoros enterrados no se ven.
        /// </summary>
        private static bool IsVisibleChest(Container chest)
        {
            if (chest.GetComponentInParent<Character>() != null || chest.GetComponent<TombStone>() != null
                || chest.GetComponent<Vagon>() != null || chest.GetComponentInParent<Ship>() != null) return false;
            if (chest.GetComponent<Piece>()?.IsPlacedByPlayer() == true || ZoneSystem.instance == null) return true;
            Vector3 position = chest.transform.position;
            return position.y >= ZoneSystem.instance.GetGroundHeight(position) - BuriedMargin;
        }

        private static bool IsBuilt(Component thing)
        {
            return thing.GetComponent<Piece>()?.IsPlacedByPlayer() == true;
        }

        /// <summary>Lo que puede atacarle. Un ciervo o una liebre cuentan como enemigos para el juego, pero solo huyen.</summary>
        public static bool IsThreat(Character self, Character other)
        {
            return BaseAI.IsEnemy(self, other) && other.GetComponent<AnimalAI>() == null;
        }

        /// <summary>El aviso de la incursión en la que está el jugador, o null si no hay ninguna.</summary>
        public static string ActiveRaid()
        {
            RandomEvent raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetActiveEvent() : null;
            if (raid == null) return null;
            string text = Localization.instance != null ? Localization.instance.Localize(raid.m_startMessage ?? "") : raid.m_startMessage;
            return string.IsNullOrEmpty(text) ? raid.m_name : text;
        }

        public static bool StrongEnemyFilterReady => _ignoreUntil != null && _orderedTarget != null && _combatTarget != null && _letGo != null;

        /// <summary>El daño de un objeto sin contar lo que solo sirve contra árboles y rocas.</summary>
        private static float CombatDamage(ItemDrop.ItemData item)
        {
            HitData.DamageTypes damage = item.GetDamage();
            return damage.GetTotalDamage() - damage.m_chop - damage.m_pickaxe;
        }

        private static float BestDamage(Inventory inventory, bool weaponsOnly)
        {
            float best = 0f;
            if (inventory == null) return best;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (!weaponsOnly || item.IsWeapon()) best = Mathf.Max(best, CombatDamage(item));
            }
            return best;
        }

        // Los monstruos llevan sus ataques como objetos en el inventario. Es por especie, y no cambia en toda la partida.
        private static float RawHitOf(Character enemy)
        {
            string kind = Utils.GetPrefabName(enemy.gameObject);
            if (EnemyHits.TryGetValue(kind, out float known)) return known;
            float hit = BestDamage((enemy as Humanoid)?.GetInventory(), false);
            if (hit > 0f) EnemyHits[kind] = hit;
            return hit;
        }

        private static bool IsOnOurSide(Character character)
        {
            return character != null && (character.IsPlayer() || character.GetComponent(_setupType) != null);
        }

        /// <summary>
        /// Valora los enemigos que tiene cerca y, con los demasiado fuertes, le hace pasar de ellos con la misma lista que usa
        /// Offline Companions para ignorar a un enemigo: de ahí vuelve solo si le ataca a él o al jugador. No toca lo que se
        /// le ha ordenado atacar ni una pelea de cerca que ya esté en marcha, donde huir de algo más rápido solo es peor.
        /// </summary>
        public static List<EnemyGroup> AssessEnemies(Component companion, Player player, float limit)
        {
            var groups = new List<EnemyGroup>();
            Character self = companion.GetComponent<Character>();
            Component combat = companion.GetComponent(_combatType);
            if (self == null || combat == null || !StrongEnemyFilterReady) return groups;

            Vector3 from = companion.transform.position;
            int allies = 1;
            var byKind = new Dictionary<string, List<Character>>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == self || c.GetHealth() <= 0f) continue;
                if (IsOnOurSide(c))
                {
                    if (Vector3.Distance(from, c.transform.position) <= AllyRange) allies++;
                    continue;
                }
                if (c.IsBoss() || !IsThreat(self, c) || Vector3.Distance(from, c.transform.position) > StrongEnemyRange) continue;
                if (!byKind.TryGetValue(c.m_name, out List<Character> kind)) byKind[c.m_name] = kind = new List<Character>();
                kind.Add(c);
            }

            float armor = self.GetBodyArmor();
            float ownHit = BestDamage((self as Humanoid)?.GetInventory(), true);
            float ownHealth = self.GetMaxHealth();
            var strong = new HashSet<string>();
            foreach (KeyValuePair<string, List<Character>> kind in byKind)
            {
                float health = 0f;
                float damage = 0f;
                float highestHit = 0f;
                float highestRaw = 0f;
                float nearest = float.PositiveInfinity;
                foreach (Character enemy in kind.Value)
                {
                    float raw = RawHitOf(enemy);
                    float hit = ThreatAssessment.HitOn(raw, enemy.GetLevel(), armor);
                    highestRaw = Mathf.Max(highestRaw, raw);
                    highestHit = Mathf.Max(highestHit, hit);
                    health += enemy.GetHealth();
                    damage += hit;
                    nearest = Mathf.Min(nearest, Vector3.Distance(from, enemy.transform.position));
                }
                float ratio = ThreatAssessment.Ratio(health, damage, ownHealth, ownHit, allies);
                bool tooStrong = ThreatAssessment.TooStrong(health, damage, highestHit, ownHealth, ownHit, allies, limit);
                string label = Localization.instance != null ? Localization.instance.Localize(kind.Key) : kind.Key;
                groups.Add(new EnemyGroup
                {
                    Label = label,
                    Count = kind.Value.Count,
                    Nearest = nearest,
                    Ratio = ratio,
                    TooStrong = tooStrong,
                    Details = "salud total " + Mathf.RoundToInt(health) + ", golpe base " + Mathf.RoundToInt(highestRaw) + " y tras armadura " + Mathf.RoundToInt(highestHit) + "; compañero: salud " + Mathf.RoundToInt(ownHealth) + ", armadura " + Mathf.RoundToInt(armor) + ", golpe " + Mathf.RoundToInt(ownHit) + "; aliados " + allies
                });
                if (!tooStrong) continue;

                strong.Add(label);
                foreach (Character enemy in kind.Value) KeepAwayFrom(combat, enemy);
            }
            StrongKinds[companion] = strong;
            return groups;
        }

        private static void KeepAwayFrom(Component combat, Character enemy)
        {
            if (_orderedTarget.GetValue(combat) as Character == enemy) return;
            Character prey = enemy.GetBaseAI()?.GetTargetCreature();
            if (IsOnOurSide(prey) && Vector3.Distance(enemy.transform.position, prey.transform.position) <= CloseFightRange) return;

            if (_combatTarget.GetValue(combat) as Character == enemy) _letGo.Invoke(combat, new object[] { enemy, IgnoreSeconds });
            else ((IDictionary)_ignoreUntil.GetValue(combat))[enemy] = Time.time + IgnoreSeconds;
        }

        public static bool IsTooStrong(Component companion, string label)
        {
            return StrongKinds.TryGetValue(companion, out HashSet<string> kinds) && kinds.Contains(label);
        }

        private static bool StrongEnemyNear(Component companion)
        {
            return StrongKinds.TryGetValue(companion, out HashSet<string> kinds) && kinds.Count > 0;
        }

        public static void ForgetStrongEnemies()
        {
            StrongKinds.Clear();
        }

        /// <summary>Lo que le impide alejarse, sin mirar nada más: se consulta a menudo.</summary>
        public static InitiativeFacts DangerFacts(Component companion, Player player)
        {
            Character self = companion.GetComponent<Character>();
            return new InitiativeFacts
            {
                Raid = ActiveRaid() != null,
                EnemiesClose = EnemyNear(companion, Initiative.AlertRange) || StrongEnemyNear(companion),
                PlayerHurt = player.GetHealthPercentage() < Initiative.BadlyHurtShare,
                SelfHurt = self != null && self.GetHealthPercentage() < Initiative.BadlyHurtShare
            };
        }

        /// <summary>Lo que hay en los cofres y alrededor que indica qué hace falta o qué no se puede hacer.</summary>
        private static void AddStockFacts(InitiativeFacts facts, Component companion, Func<Component, bool> near)
        {
            List<Container> chests = UsableChests(companion);
            Inventory pack = companion.GetComponent<Humanoid>()?.GetInventory();
            bool axe = HasTool(companion, true);
            bool pickaxe = HasTool(companion, false);
            var available = new HashSet<string>();

            if (chests.Count > 0)
            {
                facts.WoodStored = 0;
                facts.StoneStored = 0;
                facts.FoodStored = 0;
            }
            foreach (Container chest in chests)
            {
                foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
                {
                    if (item.m_dropPrefab == null) continue;
                    string prefab = item.m_dropPrefab.name;
                    available.Add(prefab);
                    if (prefab == "Wood") facts.WoodStored += item.m_stack;
                    else if (prefab == "Stone") facts.StoneStored += item.m_stack;
                    else if (IsFood(item)) facts.FoodStored += item.m_stack;

                    if (item.m_shared.m_useDurability && item.m_durability <= 0f) continue;
                    HitData.DamageTypes damage = item.GetDamage();
                    axe = axe || damage.m_chop > 0f;
                    pickaxe = pickaxe || damage.m_pickaxe > 0f;
                }
            }
            if (pack != null)
            {
                foreach (ItemDrop.ItemData item in pack.GetAllItems())
                {
                    if (item.m_dropPrefab != null) available.Add(item.m_dropPrefab.name);
                }
            }
            facts.NoAxe = !axe;
            facts.NoPickaxe = !pickaxe;

            Dictionary<string, string> foods = CookableFoods();
            foreach (CookingStation station in Object.FindObjectsByType<CookingStation>(FindObjectsSortMode.None))
            {
                if (facts.CookableDish != null || !near(station)) continue;
                foreach (CookingStation.ItemConversion conversion in station.m_conversion)
                {
                    if (conversion?.m_from == null || conversion.m_to == null || !available.Contains(conversion.m_from.gameObject.name)) continue;
                    if (foods.TryGetValue(conversion.m_to.gameObject.name, out string dish))
                    {
                        facts.CookableDish = dish;
                        break;
                    }
                }
            }
        }

        /// <summary>Lo que hay a su alrededor que puede dar pie a una tarea por su cuenta.</summary>
        public static InitiativeFacts Facts(Component companion, Player player)
        {
            const float range = SurroundingsRange;
            const float lowFuel = 0.5f;
            const float damaged = 0.75f;
            Vector3 from = companion.transform.position;
            bool Near(Component c) => Vector3.Distance(from, c.transform.position) <= range;
            bool Any<T>(Func<T, bool> accept) where T : Component
            {
                foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (Near(c) && accept(c)) return true;
                return false;
            }
            // Los recursos cuentan hasta donde el trabajo puede llevarlo; lo demás, solo si está a mano.
            float gatherRange = WorkRange("Gathering");
            float huntRange = WorkRange("Hunting");
            bool Within(Component c, float limit) => Vector3.Distance(from, c.transform.position) <= limit;
            bool AnyToGather<T>(Func<T, bool> accept) where T : Component
            {
                foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (Within(c, gatherRange) && accept(c)) return true;
                return false;
            }

            InitiativeFacts facts = DangerFacts(companion, player);
            facts.LooseItems = Any<ItemDrop>(IsLoose);
            facts.FiresLow = Any<Fireplace>(f => IsBuilt(f) && (f.GetComponent<ZNetView>()?.GetZDO()?.GetFloat(ZDOVars.s_fuel) ?? f.m_maxFuel) < f.m_maxFuel * lowFuel);
            facts.DamagedPieces = Any<WearNTear>(w => IsBuilt(w) && w.GetHealthPercentage() < damaged);
            facts.CanGetHammer = facts.DamagedPieces && HammerProblem(companion) == null;
            facts.HoneyReady = Any<Beehive>(b => (b.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level) ?? 0) > 0);
            facts.CookingSpot = Any<CookingStation>(_ => true);
            facts.Trees = AnyToGather<TreeBase>(_ => true);
            facts.Rocks = AnyToGather<MineRock5>(_ => true) || AnyToGather<MineRock>(_ => true);
            facts.Ripe = AnyToGather<Pickable>(p => p.m_itemPrefab != null && p.CanBePicked());
            facts.StationNear = Nearest<CraftingStation>(companion, SearchRange, null) != null;
            facts.FoundChest = Any<Container>(c => IsVisibleChest(c) && !IsBuilt(c));
            facts.Night = EnvMan.IsNight();
            facts.AtBase = EffectArea.IsPointInsideArea(from, EffectArea.Type.PlayerBase) != null;
            facts.PlayerBuilding = player.InPlaceMode();
            AddStockFacts(facts, companion, Near);

            Component talk = companion.GetComponent(_talkType);
            Inventory carried = companion.GetComponent<Humanoid>()?.GetInventory();
            facts.PackFull = carried != null && (carried.GetEmptySlots() == 0 || (talk != null && (bool)_isOverweight.Invoke(talk, null)));

            foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (Near(chest) && IsVisibleChest(chest) && IsBuilt(chest)) facts.BuiltChests++;
            }
            Character self = companion.GetComponent<Character>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c != null && c != self && self != null && !c.IsPlayer() && c.GetHealth() > 0f && Within(c, huntRange) && BaseAI.IsEnemy(self, c) && !IsThreat(self, c)) facts.Animals = true;
            }
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory != null)
            {
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material) facts.GatheredInPack = true;
                }
            }
            return facts;
        }

        private static bool IsAmmo(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            return type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
        }

        /// <summary>Su arco o ballesta en condiciones de usarse, o null. Una caña de pescar también usa cebo, pero no es un arma.</summary>
        private static ItemDrop.ItemData Launcher(Inventory inventory)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (IsAmmo(item) || string.IsNullOrEmpty(item.m_shared.m_ammoType)) continue;
                if (item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod) continue;
                if (!item.m_shared.m_useDurability || item.m_durability > 0f) return item;
            }
            return null;
        }

        private static int AmmoCount(Inventory inventory, string ammoType)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (IsAmmo(item) && item.m_shared.m_ammoType == ammoType) count += item.m_stack;
            }
            return count;
        }

        private static bool HasMelee(Inventory inventory)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
                bool weapon = type == ItemDrop.ItemData.ItemType.OneHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeapon || type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
                if (weapon && string.IsNullOrEmpty(item.m_shared.m_ammoType)) return true;
            }
            return false;
        }

        private static string AmmoWord(string ammoType)
        {
            return ammoType == "bolt" ? ModLang.Pick("virotes", "bolts") : ModLang.Pick("flechas", "arrows");
        }

        /// <summary>El tipo de munición que necesita y no tiene, o null si no le falta.</summary>
        public static string MissingAmmoType(Component companion)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            ItemDrop.ItemData launcher = inventory != null ? Launcher(inventory) : null;
            return launcher != null && AmmoCount(inventory, launcher.m_shared.m_ammoType) == 0 ? launcher.m_shared.m_ammoType : null;
        }

        /// <summary>El tipo de munición de lo que lleva, o null si no lleva nada que la use.</summary>
        public static string LauncherAmmoType(Component companion)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            return inventory != null ? Launcher(inventory)?.m_shared.m_ammoType : null;
        }

        /// <summary>Para la IA: con qué dispara, y si le queda con qué. Nada si no lleva un arma a distancia.</summary>
        public static string RangedSummary(Component companion)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            ItemDrop.ItemData launcher = inventory != null ? Launcher(inventory) : null;
            if (launcher == null) return null;
            string name = Localization.instance != null ? Localization.instance.Localize(launcher.m_shared.m_name) : launcher.m_shared.m_name;
            int ammo = AmmoCount(inventory, launcher.m_shared.m_ammoType);
            return name + ", " + (ammo > 0 ? ammo + " " + AmmoWord(launcher.m_shared.m_ammoType) : ModLang.Pick("sin ", "no ") + AmmoWord(launcher.m_shared.m_ammoType))
                + ModLang.Pick("; arma cuerpo a cuerpo: ", "; melee weapon: ") + (HasMelee(inventory) ? ModLang.Pick("sí", "yes") : "no");
        }

        /// <summary>La munición de un cofre, empezando por la que más abunda. amount 0 es toda.</summary>
        private static Dictionary<string, int> AmmoIn(Inventory inventory, string ammoType, int amount)
        {
            var stacks = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab == null || !IsAmmo(item) || (ammoType != null && item.m_shared.m_ammoType != ammoType)) continue;
                stacks.TryGetValue(item.m_dropPrefab.name, out int n);
                stacks[item.m_dropPrefab.name] = n + item.m_stack;
            }
            if (amount <= 0) return stacks;

            var ordered = new List<KeyValuePair<string, int>>(stacks);
            ordered.Sort((a, b) => b.Value.CompareTo(a.Value));
            var chosen = new Dictionary<string, int>();
            int left = amount;
            foreach (KeyValuePair<string, int> stack in ordered)
            {
                if (left <= 0) break;
                chosen[stack.Key] = Math.Min(stack.Value, left);
                left -= chosen[stack.Key];
            }
            return chosen;
        }

        /// <summary>El cofre más cercano que pueda usar con esa munición. ammoType null es cualquiera.</summary>
        public static Container FindChestWithAmmo(Component companion, string ammoType, int amount, out Dictionary<string, int> items)
        {
            items = null;
            Dictionary<string, int> best = null;
            Container chest = Nearest<Container>(companion, SearchRange, c =>
            {
                if (!(bool)_chestUsable.Invoke(null, new object[] { c, companion })) return false;
                Dictionary<string, int> found = AmmoIn(c.GetInventory(), ammoType, amount);
                if (found.Count == 0) return false;
                best = found;
                return true;
            });
            if (chest != null) items = best;
            return chest;
        }

        /// <summary>
        /// Offline Companions guarda en un cofre lo que lleva sin marcar como suyo, y solo marca lo que le da el jugador.
        /// Lo que el jugador le manda coger o decide coger él mismo debe quedárselo. Los materiales no: esos sí se guardan.
        /// </summary>
        public static void KeepGear(Component companion, ICollection<string> prefabs)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory == null) return;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab == null || !prefabs.Contains(item.m_dropPrefab.name)) continue;
                if (!item.IsEquipable() && !IsAmmo(item) && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) continue;
                if (item.m_customData == null) item.m_customData = new Dictionary<string, string>();
                item.m_customData[OwnTag] = "1";
            }
        }

        /// <summary>Necesidades que el compañero no puede resolver solo: textos para que se lo diga al jugador.</summary>
        public static List<KeyValuePair<string, string>> UnmetNeeds(Component companion)
        {
            var needs = new List<KeyValuePair<string, string>>();
            Component talk = companion.GetComponent(_talkType);
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (talk == null || inventory == null) return needs;

            if (!CarriesFood(companion) && (bool)_isHungry.Invoke(talk, null))
            {
                needs.Add(new KeyValuePair<string, string>("food", ModLang.Pick(
                    "Tienes hambre y no llevas nada de comer. Si puede haber en los cofres, usa take_from_chest con item comida.",
                    "You are hungry and carrying no food. If there may be food in the chests, use take_from_chest with item food.")));
            }
            if ((bool)_needsRepair.Invoke(talk, null)) needs.Add(new KeyValuePair<string, string>("repair", GearCheck.RepairNeed(Nearest<CraftingStation>(companion, SearchRange, null) != null)));
            if ((bool)_isOverweight.Invoke(talk, null) || inventory.GetEmptySlots() == 0)
            {
                needs.Add(new KeyValuePair<string, string>("weight", ModLang.Pick(
                    "Llevas la mochila llena o demasiado peso.",
                    "Your pack is full or you are carrying too much weight.")));
            }

            string ammoType = MissingAmmoType(companion);
            if (ammoType != null)
            {
                string launcher = Localization.instance != null ? Localization.instance.Localize(Launcher(inventory).m_shared.m_name) : Launcher(inventory).m_shared.m_name;
                bool inChest = FindChestWithAmmo(companion, ammoType, 1, out Dictionary<string, int> _) != null;
                needs.Add(new KeyValuePair<string, string>("ammo", GearCheck.AmmoNeed(launcher, AmmoWord(ammoType), inChest, HasMelee(inventory))));
            }
            return needs;
        }

        /// <summary>Resumen de los cofres cercanos que puede usar, para responder sin moverse.</summary>
        public static string DescribeChests(Component companion)
        {
            const int maxChests = 8;
            var found = new List<KeyValuePair<float, string>>();
            foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(companion.transform.position, chest.transform.position);
                if (distance > SearchRange || !(bool)_chestUsable.Invoke(null, new object[] { chest, companion })) continue;
                Dictionary<string, int> contents = ContentsOf(chest.GetInventory());
                string text = contents.Count == 0 ? ModLang.Pick("vacío", "empty") : DescribeItems(contents);
                found.Add(new KeyValuePair<float, string>(distance, ModLang.Pick("cofre a ", "chest at ") + Mathf.RoundToInt(distance) + " m: " + text));
            }
            if (found.Count == 0) return ModLang.Pick("No hay cofres utilizables a menos de " + SearchRange + " m.", "No usable chests within " + SearchRange + " m.");

            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            var lines = new List<string>();
            for (int i = 0; i < found.Count && i < maxChests; i++) lines.Add(found[i].Value);
            string more = found.Count > maxChests
                ? ModLang.Pick(" Hay " + (found.Count - maxChests) + " cofres más.", " There are " + (found.Count - maxChests) + " more chests.")
                : "";
            return string.Join("; ", lines.ToArray()) + "." + more;
        }

        /// <summary>Las comidas que Offline Companions sabe cocinar: nombre interno y nombre visible.</summary>
        public static Dictionary<string, string> CookableFoods()
        {
            var foods = new Dictionary<string, string>();
            _cookEnsure.Invoke(null, null);
            foreach (object entry in (IEnumerable)_cookRecipes.GetValue(null))
            {
                ItemDrop output = (entry as Recipe)?.m_item;
                if (output == null) continue;
                AddFood(foods, _cookFinal.Invoke(null, new object[] { output.gameObject.name }) as string);
            }
            foreach (string cooked in new List<string>(((IDictionary<string, string>)_cookConversions.GetValue(null)).Values)) AddFood(foods, cooked);
            return foods;
        }

        private static void AddFood(Dictionary<string, string> foods, string item)
        {
            if (string.IsNullOrEmpty(item) || foods.ContainsKey(item)) return;
            if ((bool)_cookIsMeadBase.Invoke(null, new object[] { item }) || (bool)_cookIsMead.Invoke(null, new object[] { item })) return;
            if ((float)_cookValue.Invoke(null, new object[] { item }) <= 0f) return;
            foods[item] = ItemLabel(item);
        }

        /// <summary>Resuelve el nombre pedido a una comida; primero exacto y, si no, por parte del nombre o sinónimo regional (ES Latino / ES España / EN).</summary>
        public static string FindFood(string wanted, Dictionary<string, string> foods)
        {
            string partial = null;
            foreach (KeyValuePair<string, string> food in foods)
            {
                if (string.Equals(food.Key, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(food.Value, wanted, StringComparison.OrdinalIgnoreCase)) return food.Key;
                // También por el nombre interno o sinónimos (ES Latino / ES España / EN).
                if (partial == null && (food.Value.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0
                    || wanted.IndexOf(food.Value, StringComparison.OrdinalIgnoreCase) >= 0
                    || food.Key.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0
                    || ItemNames.Matches(food.Value, wanted)
                    || ItemNames.Matches(food.Key, wanted))) partial = food.Key;
            }
            return partial;
        }

        public static string LabelOf(string prefab)
        {
            return ItemLabel(prefab);
        }

        /// <summary>Cuánto hay de una comida en los cofres cercanos y en su mochila, según el propio trabajo de cocina.</summary>
        public static int CookStock(Component companion, string item)
        {
            Component cook = companion.GetComponent(_cookType);
            return cook == null ? 0 : (int)_cookCount.Invoke(cook, new object[] { item });
        }

        public static bool IsCooking(Component companion)
        {
            Component cook = companion.GetComponent(_cookType);
            return (cook != null && (bool)_cookActive.GetValue(cook, null)) || IsBusy(companion);
        }

        public static bool IsOrganizing(Component companion)
        {
            Component upkeep = companion.GetComponent(_upkeepType);
            return (upkeep != null && (bool)_upkeepActive.GetValue(upkeep, null)) || IsBusy(companion);
        }

        /// <summary>Hace que coma o beba. Devuelve el error, o null si lo ha consumido.</summary>
        public static string Consume(Component companion, string wanted)
        {
            Component food = companion.GetComponent(_foodType);
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (food == null || inventory == null) return ModLang.Pick("No puede comer ahora.", "Cannot eat right now.");

            if (wanted == CompanionActions.AnyFood)
            {
                int before = inventory.GetAllItems().Count;
                int stackBefore = TotalStack(inventory);
                _eatNow.Invoke(food, null);
                return TotalStack(inventory) < stackBefore || inventory.GetAllItems().Count < before
                    ? null
                    : ModLang.Pick("No tiene hambre o no lleva comida.", "Not hungry or carrying no food.");
            }

            foreach (string prefab in FindInPack(companion, wanted, 0).Keys)
            {
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
                {
                    if (item.m_dropPrefab == null || item.m_dropPrefab.name != prefab) continue;
                    if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) continue;
                    bool potion = item.m_shared.m_food <= 0f && item.m_shared.m_foodStamina <= 0f && item.m_shared.m_foodEitr <= 0f;
                    bool done = (bool)(potion ? _drinkPotion : _eatItem).Invoke(food, new object[] { item });
                    return done ? null : ModLang.Pick("Ahora no puede consumir eso: ya tiene ese efecto o no tiene hueco.", "Cannot consume that right now: already has that effect or has no stomach room.");
                }
            }
            return ModLang.Pick("No lleva " + wanted + " en la mochila, o no se puede consumir.", "Not carrying " + wanted + " in the pack, or it cannot be consumed.");
        }

        private static int TotalStack(Inventory inventory)
        {
            int total = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems()) total += item.m_stack;
            return total;
        }

        /// <summary>Equipa un objeto. Devuelve el error, o null si lo ha equipado.</summary>
        public static string Equip(Component companion, string wanted)
        {
            Humanoid humanoid = companion.GetComponent<Humanoid>();
            Inventory inventory = humanoid?.GetInventory();
            if (inventory == null) return ModLang.Pick("No puede equiparse ahora.", "Cannot equip items right now.");

            foreach (string prefab in FindInPack(companion, wanted, 0).Keys)
            {
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
                {
                    if (item.m_dropPrefab == null || item.m_dropPrefab.name != prefab || !item.IsEquipable()) continue;
                    return humanoid.EquipItem(item) ? null : ModLang.Pick("No ha podido equiparse eso.", "Could not equip that.");
                }
            }
            return ModLang.Pick(
                "No lleva " + wanted + " en la mochila, ya lo tiene puesto o no se puede equipar. Si está en un cofre cercano, puede sacarlo primero con take_from_chest.",
                "Not carrying " + wanted + " in the pack, already wearing it, or it cannot be equipped. If it is in a nearby chest, take it first with take_from_chest.");
        }

        public static void WalkTo(Component companion, Player player, Vector3 point)
        {
            Component harvest = companion.GetComponent(_harvestType);
            bool gathering = harvest != null && (bool)_inGatherMode.GetValue(harvest, null);
            Invoke("DirectGround", companion, player, point);
            if (gathering) Invoke("DirectGround", companion, player, point);
        }

        private static Dictionary<string, int> FindIn(Dictionary<string, int> contents, string wanted, int amount)
        {
            var exact = new Dictionary<string, int>();
            var partial = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> item in contents)
            {
                string label = ItemLabel(item.Key);
                if (string.Equals(item.Key, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(label, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    exact[item.Key] = item.Value;
                }
                else if (ItemNames.Matches(label, wanted) || ItemNames.Matches(item.Key, wanted))
                {
                    partial[item.Key] = item.Value;
                }
            }

            Dictionary<string, int> found = exact.Count > 0 ? exact : partial;
            if (amount <= 0) return found;
            var limited = new Dictionary<string, int>();
            int left = amount;
            foreach (KeyValuePair<string, int> item in found)
            {
                if (left <= 0) break;
                limited[item.Key] = Math.Min(item.Value, left);
                left -= limited[item.Key];
            }
            return limited;
        }

        /// <summary>Pasa objetos a la mochila del jugador hasta donde quepan. Devuelve lo entregado.</summary>
        public static Dictionary<string, int> HandOver(Component companion, Player player, IDictionary<string, int> wanted)
        {
            Inventory from = companion.GetComponent<Humanoid>()?.GetInventory();
            Inventory to = player.GetInventory();
            if (from == null || to == null) return new Dictionary<string, int>();

            _takeControl.Invoke(null, new object[] { companion });
            return Transfer(from, to, wanted);
        }

        private static Dictionary<string, int> Transfer(Inventory from, Inventory to, IDictionary<string, int> wanted)
        {
            var given = new Dictionary<string, int>();
            var left = new Dictionary<string, int>(wanted);
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (item.m_equipped || item.m_dropPrefab == null) continue;
                string prefab = item.m_dropPrefab.name;
                if (!left.TryGetValue(prefab, out int remaining) || remaining <= 0) continue;

                int amount = Math.Min(remaining, item.m_stack);
                ItemDrop.ItemData copy = item.Clone();
                copy.m_stack = amount;
                // Primero se añade: si no cabe, el objeto sigue con el compañero y nada se pierde.
                if (!to.CanAddItem(copy) || !to.AddItem(copy)) continue;
                from.RemoveItem(item, amount);

                left[prefab] = remaining - amount;
                given.TryGetValue(prefab, out int total);
                given[prefab] = total + amount;
            }
            return given;
        }

        /// <summary>Suelta objetos delante del compañero. Devuelve lo soltado.</summary>
        public static Dictionary<string, int> DropToGround(Component companion, IDictionary<string, int> wanted)
        {
            var dropped = new Dictionary<string, int>();
            Inventory from = companion.GetComponent<Humanoid>()?.GetInventory();
            if (from == null) return dropped;

            _takeControl.Invoke(null, new object[] { companion });
            Vector3 spot = companion.transform.position + companion.transform.forward + Vector3.up;
            var left = new Dictionary<string, int>(wanted);
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(from.GetAllItems()))
            {
                if (item.m_equipped || item.m_dropPrefab == null) continue;
                string prefab = item.m_dropPrefab.name;
                if (!left.TryGetValue(prefab, out int remaining) || remaining <= 0) continue;

                int amount = Math.Min(remaining, item.m_stack);
                ItemDrop drop = ItemDrop.DropItem(item, amount, spot, companion.transform.rotation);
                if (drop == null) continue;
                // El juego solo lo marca así si quien suelta es un jugador; sin esto se recogería automáticamente al instante.
                drop.OnPlayerDrop();
                from.RemoveItem(item, amount);

                left[prefab] = remaining - amount;
                dropped.TryGetValue(prefab, out int total);
                dropped[prefab] = total + amount;
            }
            return dropped;
        }

        /// <summary>El objeto suelto más cercano de ese tipo, incluido lo que haya soltado un jugador.</summary>
        public static ItemDrop FindDropped(Component companion, string item, float range)
        {
            return Nearest<ItemDrop>(companion, range, d =>
            {
                ZNetView view = d.GetComponent<ZNetView>();
                return view != null && view.IsValid() && Utils.GetPrefabName(d.gameObject) == item;
            });
        }

        /// <summary>
        /// El objeto suelto al que se refiere el jugador: el que mira si coincide con el nombre, o si no da ninguno;
        /// si no, el más cercano con ese nombre.
        /// </summary>
        public static ItemDrop FindLooseItem(Component companion, LookTarget look, string wanted, float range)
        {
            bool named = !string.IsNullOrEmpty(wanted);
            ItemDrop looked = look?.Collider != null ? look.Collider.GetComponentInParent<ItemDrop>() : null;
            if (!named)
            {
                if (looked != null || look?.Collider == null) return looked;
                // Es fácil apuntar al suelo justo al lado de un objeto pequeño.
                Vector3 point = look.Point;
                return Nearest<ItemDrop>(companion, range, d => IsLoose(d) && Vector3.Distance(d.transform.position, point) <= LookedItemMargin);
            }

            var names = new Dictionary<string, int>();
            foreach (ItemDrop drop in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (IsLoose(drop)) names[Utils.GetPrefabName(drop.gameObject)] = 1;
            }
            Dictionary<string, int> matches = FindIn(names, wanted, 0);
            if (looked != null && matches.ContainsKey(Utils.GetPrefabName(looked.gameObject))) return looked;
            return Nearest<ItemDrop>(companion, range, d => IsLoose(d) && matches.ContainsKey(Utils.GetPrefabName(d.gameObject)));
        }

        private static bool IsLoose(ItemDrop drop)
        {
            ZNetView view = drop.GetComponent<ZNetView>();
            return view != null && view.IsValid();
        }

        public static string DroppedLabel(ItemDrop drop)
        {
            return ItemLabel(Utils.GetPrefabName(drop.gameObject));
        }

        public static bool IsBroken(ItemDrop drop)
        {
            ItemDrop.ItemData data = drop.m_itemData;
            return data.m_shared.m_useDurability && data.m_durability <= 0f;
        }

        /// <summary>Sin herramientas, armas, armaduras ni antorchas: guardarlas o sacarlas de la mano no es haberlas conseguido.</summary>
        public static Dictionary<string, int> WithoutGear(IDictionary<string, int> items)
        {
            var kept = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> item in items)
            {
                ItemDrop drop = ObjectDB.instance?.GetItemPrefab(item.Key)?.GetComponent<ItemDrop>();
                if (drop == null || !drop.m_itemData.IsEquipable()) kept[item.Key] = item.Value;
            }
            return kept;
        }

        public static bool PickUp(Component companion, ItemDrop drop)
        {
            Humanoid humanoid = companion.GetComponent<Humanoid>();
            if (humanoid == null || drop == null) return false;
            if (!drop.CanPickup())
            {
                drop.RequestOwn();
                return false;
            }
            return humanoid.Pickup(drop.gameObject, false, false);
        }

        public static bool EnemyNear(Component companion, float range)
        {
            Character self = companion.GetComponent<Character>();
            if (self == null) return false;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == self || c.IsPlayer() || c.GetHealth() <= 0f || !IsThreat(self, c)) continue;
                if (Vector3.Distance(companion.transform.position, c.transform.position) <= range) return true;
            }
            return false;
        }

        public static bool InCombat(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            return ai != null && (bool)_isInCombat.GetValue(ai, null);
        }

        /// <summary>La herramienta que le falta para un trabajo, o null si la lleva o no necesita ninguna.</summary>
        public static string MissingTool(Component companion, int job)
        {
            bool needsAxe = job == CompanionActions.JobWood;
            bool needsPick = job == CompanionActions.JobStone || job == CompanionActions.JobOre;
            if (!needsAxe && !needsPick) return null;
            if (HasTool(companion, needsAxe)) return null;
            return needsAxe ? ModLang.Pick("un hacha", "an axe") : ModLang.Pick("un pico", "a pickaxe");
        }

        private static bool HasTool(Component companion, bool axe)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory == null) return false;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_shared.m_useDurability && item.m_durability <= 0f) continue;
                HitData.DamageTypes damage = item.GetDamage();
                if ((axe ? damage.m_chop : damage.m_pickaxe) > 0f) return true;
            }
            return false;
        }

        /// <summary>Lo que el compañero necesita saber sobre sus herramientas para no prometer lo que no puede hacer.</summary>
        public static string ToolSummary(Component companion)
        {
            return ModLang.Pick("hacha: ", "axe: ") + (HasTool(companion, true) ? ModLang.Pick("sí", "yes") : "no")
                + ModLang.Pick(", pico: ", ", pickaxe: ") + (HasTool(companion, false) ? ModLang.Pick("sí", "yes") : "no");
        }

        /// <summary>Una planta pequeña que se rompe a golpes, o null. Nunca una construcción.</summary>
        public static Destructible SmallPlantLookedAt(LookTarget look)
        {
            Collider c = look?.Collider;
            if (c == null || look.Enemy != null) return null;
            Destructible plant = c.GetComponentInParent<Destructible>();
            if (plant == null || plant.GetComponent<Piece>() != null) return null;
            if ((GameObject)_getHarvestable.Invoke(null, new object[] { c }) != null) return null;
            bool isPlant = plant.m_destructibleType == DestructibleType.Tree
                || plant.gameObject.name.IndexOf("small", StringComparison.OrdinalIgnoreCase) >= 0
                || plant.gameObject.name.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0
                || plant.gameObject.name.IndexOf("shrub", StringComparison.OrdinalIgnoreCase) >= 0
                || plant.gameObject.name.IndexOf("sapling", StringComparison.OrdinalIgnoreCase) >= 0;
            return isPlant ? plant : null;
        }

        private static Vector3 AimPoint(Component target)
        {
            Collider collider = target.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : target.transform.position;
        }

        /// <summary>Distancia en horizontal hasta el borde de la planta, no hasta su centro.</summary>
        public static float DistanceTo(Component companion, Component target)
        {
            Collider collider = target.GetComponentInChildren<Collider>();
            Vector3 from = companion.transform.position;
            Vector3 to = collider != null ? collider.bounds.ClosestPoint(from) : target.transform.position;
            to.y = from.y;
            return Vector3.Distance(from, to);
        }

        /// <summary>El golpe sale según la dirección de la mirada, no la del cuerpo, así que hay que ajustar ambas.</summary>
        public static void AimAt(Component companion, Component target)
        {
            Character character = companion.GetComponent<Character>();
            if (character == null || target == null) return;

            Vector3 flat = AimPoint(target) - companion.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            companion.transform.rotation = Quaternion.LookRotation(flat.normalized);

            Vector3 aim = AimPoint(target) - character.GetEyePoint();
            character.SetLookDir(aim.normalized);
        }

        public static void Swing(Component companion)
        {
            Humanoid humanoid = companion.GetComponent<Humanoid>();
            if (humanoid != null && !humanoid.InAttack()) humanoid.StartAttack(null, false);
        }

        /// <summary>El árbol o tronco que mira el jugador, o null si mira otra cosa.</summary>
        public static GameObject TreeLookedAt(LookTarget look)
        {
            Collider c = look?.Collider;
            if (c == null || look.Enemy != null) return null;
            Component tree = c.GetComponentInParent<TreeBase>();
            if (tree == null) tree = c.GetComponentInParent<TreeLog>();
            return tree != null ? tree.gameObject : null;
        }

        /// <summary>Deja de seguir al jugador, porque con Seguir activo no alcanzaría los troncos que rueden lejos de él.</summary>
        public static void WorkHere(Component companion)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            if (zdo != null) SetFollow(companion, zdo, false);
        }

        /// <summary>Troncos y tocones que quedan junto a donde estaba el árbol.</summary>
        public static int TreePiecesNear(Vector3 position, float radius)
        {
            int count = 0;
            foreach (TreeLog log in Object.FindObjectsByType<TreeLog>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(log.transform.position, position) <= radius) count++;
            }
            foreach (Destructible piece in Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None))
            {
                if (piece.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0
                    && Vector3.Distance(piece.transform.position, position) <= radius) count++;
            }
            return count;
        }

        /// <summary>
        /// El viaje de vaciar la mochila al cambiar de trabajo borra el objetivo, así que se le vuelve a asignar.
        /// Devuelve true si tuvo que hacerlo.
        /// </summary>
        public static bool EnsureTreeTarget(Component companion, GameObject tree)
        {
            if (tree == null || IsOnErrand(companion)) return false;
            Component harvest = companion.GetComponent(_harvestType);
            if (harvest == null || (Object)_directedTarget.GetValue(harvest) != null) return false;
            _setDirected.Invoke(harvest, new object[] { tree });
            return true;
        }

        private static bool IsOnErrand(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            return (bool)_toolsBusy.Invoke(null, new object[] { companion })
                || (ai != null && ((bool)_hasOrder.GetValue(ai, null) || (bool)_isInCombat.GetValue(ai, null)));
        }

        /// <summary>Cuántas unidades de un objeto lleva en la mochila.</summary>
        public static int CountInPack(Component companion, string item)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory == null) return 0;
            int total = 0;
            foreach (ItemDrop.ItemData data in inventory.GetAllItems())
            {
                if (data.m_dropPrefab != null && data.m_dropPrefab.name == item) total += data.m_stack;
            }
            return total;
        }

        /// <summary>Si está buscando, recolectando o yendo a por una herramienta, aunque no gane nada todavía.</summary>
        public static bool IsWorking(Component companion)
        {
            Component harvest = companion.GetComponent(_harvestType);
            return (harvest != null && (bool)_harvestActive.GetValue(harvest, null))
                || (bool)_toolsBusy.Invoke(null, new object[] { companion })
                || IsBusy(companion);
        }

        /// <summary>Termina una meta de recolección: deja el trabajo y vuelve a seguir al jugador.</summary>
        public static void StopGathering(Component companion)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) return;
            _takeControl.Invoke(null, new object[] { companion });
            SetJob(companion, zdo, 0);
            SetFollow(companion, zdo, true);
        }

        /// <summary>Si sigue ocupado con una orden, para saber cuándo empezar la siguiente de la fila.</summary>
        public static bool IsBusy(Component companion)
        {
            if (IsOnErrand(companion)) return true;

            Component repair = companion.GetComponent(_repairType);
            if (repair != null && (bool)_repairActive.GetValue(repair, null)) return true;

            Component rest = companion.GetComponent(_restType);
            if (rest != null && (bool)_isNavigating.GetValue(rest, null)) return true;

            Component harvest = companion.GetComponent(_harvestType);
            return harvest != null && (Object)_directedTarget.GetValue(harvest) != null;
        }

        /// <summary>Los compañeros propios cercanos, con primary primero y el resto por distancia.</summary>
        public static List<Component> Nearby(Player player, Component primary, float range)
        {
            var found = new List<Component>();
            foreach (Component c in MyCompanions())
            {
                if (c == primary || Vector3.Distance(player.transform.position, c.transform.position) <= range) found.Add(c);
            }
            found.Sort((a, b) =>
            {
                if (a == b) return 0;
                if (a == primary) return -1;
                if (b == primary) return 1;
                return Vector3.Distance(player.transform.position, a.transform.position).CompareTo(Vector3.Distance(player.transform.position, b.transform.position));
            });
            return found;
        }

        private static string InitializeActions(Assembly asm, Type owner)
        {
            Type jobRules = asm.GetType("Companions.MHR_JobRules");
            if (jobRules == null) return "MHR_JobRules";

            FieldInfo hash = _setupType.GetField("ActionModeHash", Any);
            _takeControl = owner.GetMethod("TakeControl", Any, null, new[] { typeof(Component) }, null);
            _onJobSet = jobRules.GetMethod("OnJobSet", Any, null, new[] { _setupType, typeof(int) }, null);
            _followSetByPlayer = jobRules.GetMethod("FollowSetByPlayer", Any, null, new[] { _setupType }, null);
            _hasJob = jobRules.GetMethod("HasJob", Any, null, new[] { _setupType }, null);
            _keepsFollow = jobRules.GetMethod("KeepsFollow", Any, null, new[] { typeof(int) }, null);
            _getFollow = _setupType.GetMethod("GetFollow", Any, null, Type.EmptyTypes, null);
            _setFollow = _setupType.GetMethod("SetFollow", Any, null, new[] { typeof(bool) }, null);
            _applyFollowMode = _setupType.GetMethod("ApplyFollowMode", Any, null, new[] { typeof(int), typeof(bool) }, null);
            _setCombatStance = _setupType.GetMethod("SetCombatStance", Any, null, new[] { typeof(int) }, null);

            if (hash == null) return "CompanionSetup.ActionModeHash";
            if (_takeControl == null) return "MHR_Owner.TakeControl";
            if (_onJobSet == null) return "MHR_JobRules.OnJobSet";
            if (_followSetByPlayer == null) return "MHR_JobRules.FollowSetByPlayer";
            if (_hasJob == null) return "MHR_JobRules.HasJob";
            if (_keepsFollow == null) return "MHR_JobRules.KeepsFollow";
            if (_getFollow == null) return "CompanionSetup.GetFollow";
            if (_setFollow == null) return "CompanionSetup.SetFollow";
            if (_applyFollowMode == null) return "CompanionSetup.ApplyFollowMode";
            if (_setCombatStance == null) return "CompanionSetup.SetCombatStance";
            FieldInfo stanceHash = _setupType.GetField("CombatStanceHash", Any);
            if (stanceHash == null) return "CompanionSetup.CombatStanceHash";
            _combatStanceHash = (int)stanceHash.GetValue(null);
            _getHome = _setupType.GetMethod("GetHomePosition", Any, null, Type.EmptyTypes, null);
            _setMoveTarget = _setupType.Assembly.GetType("Companions.CompanionAI")?.GetMethod("SetMoveTarget", Any, null, new[] { typeof(Vector3) }, null);
            if (_getHome == null) return "CompanionSetup.GetHomePosition";
            if (_setMoveTarget == null) return "CompanionAI.SetMoveTarget";
            _actionModeHash = (int)hash.GetValue(null);

            JobControllers.Clear();
            foreach (string name in new[] { "HarvestController", "SmeltController", "FarmController", "FishController", "CookController", "HuntController", "UpkeepController" })
            {
                Type type = asm.GetType("Companions." + name);
                MethodInfo notify = type?.GetMethod("NotifyActionModeChanged", Any, null, Type.EmptyTypes, null);
                if (notify == null) return name + ".NotifyActionModeChanged";
                JobControllers.Add(new KeyValuePair<Type, MethodInfo>(type, notify));
            }
            return null;
        }

        private static string InitializeOrders(Assembly asm)
        {
            Type interact = asm.GetType("Companions.CompanionInteract");
            _harvestType = asm.GetType("Companions.HarvestController");
            _huntType = asm.GetType("Companions.HuntController");
            _huntState = _huntType?.GetField("_state", Any);
            _huntAnchor = _huntType?.GetField("_anchor", Any);
            _huntHasAnchor = _huntType?.GetField("_hasAnchor", Any);
            _restType = asm.GetType("Companions.CompanionRest");
            _aiType = asm.GetType("Companions.CompanionAI");
            _combatType = asm.GetType("Companions.CompanionCombatAI");
            if (interact == null) return "CompanionInteract";
            if (_restType == null) return "CompanionRest";
            if (_aiType == null) return "CompanionAI";
            if (_combatType == null) return "CompanionCombatAI";
            _ignoreUntil = _combatType.GetField("_ignoreUntil", Any);
            _orderedTarget = _combatType.GetField("_ordered", Any);
            _combatTarget = _combatType.GetField("_targetCreature", Any);
            _letGo = _combatType.GetMethod("LetGo", Any, null, new[] { typeof(Character), typeof(float) }, null);

            FieldInfo tombHash = _setupType.GetField("MHR_TombCompanionIdHash", Any);
            _inScope = interact.GetMethod("MHR_InScope", Any, null, new[] { _setupType }, null);
            _getHarvestable = interact.GetMethod("GetHarvestable", Any, null, new[] { typeof(Collider) }, null);
            _stayAwake = _restType.GetMethod("MHR_StayAwakeForRecall", Any, null, new[] { _setupType }, null);
            _holdForOrder = _combatType.GetMethod("HoldForOrder", Any);
            _inGatherMode = _harvestType.GetProperty("IsInGatherMode", Any);
            _cancelRest = _restType.GetMethod("CancelDirected", Any, null, Type.EmptyTypes, null);
            _isResting = _restType.GetProperty("IsResting", Any);
            _isNavigating = _restType.GetProperty("IsNavigating", Any);
            _pendingDeposit = _aiType.GetField("PendingDepositContainer", Any);
            _setPendingDeposit = _aiType.GetMethod("SetPendingDeposit", Any);
            _cancelDeposit = _aiType.GetMethod("CancelPendingDeposit", Any);
            _shouldKeep = interact.GetMethod("ShouldKeep", Any);
            if (tombHash == null) return "CompanionSetup.MHR_TombCompanionIdHash";
            if (_inScope == null) return "CompanionInteract.MHR_InScope";
            if (_getHarvestable == null) return "CompanionInteract.GetHarvestable";
            if (_stayAwake == null) return "CompanionRest.MHR_StayAwakeForRecall";
            if (_holdForOrder == null || _holdForOrder.GetParameters().Length != 1) return "CompanionCombatAI.HoldForOrder";
            if (_inGatherMode == null) return "HarvestController.IsInGatherMode";
            if (_cancelRest == null) return "CompanionRest.CancelDirected";
            if (_isResting == null) return "CompanionRest.IsResting";
            if (_isNavigating == null) return "CompanionRest.IsNavigating";
            if (_pendingDeposit == null) return "CompanionAI.PendingDepositContainer";
            if (_setPendingDeposit == null) return "CompanionAI.SetPendingDeposit";
            if (_cancelDeposit == null) return "CompanionAI.CancelPendingDeposit";
            if (_shouldKeep == null) return "CompanionInteract.ShouldKeep";
            _tombCompanionIdHash = (int)tombHash.GetValue(null);

            Direct.Clear();
            // Opcionales: sin ellos, solo se pierde poder mandar más allá del alcance de Offline Companions.
            _commandAnywhere = interact.GetField("_mhrSuperRecall", Any);
            _isCommandable = _setupType.GetMethod("GetIsCommandable", Any, null, Type.EmptyTypes, null);

            // Opcionales: sin ellos, solo se pierde saber su aguante y si sigue en la pelea.
            _staminaType = asm.GetType("Companions.CompanionStamina");
            _staminaShare = _staminaType?.GetMethod("GetStaminaPercentage", Any, null, Type.EmptyTypes, null);
            _isEngaged = _combatType.GetProperty("IsEngaged", Any);
            _swimTiredShare = asm.GetType("Companions.ModConfig")?.GetField("WaterStaminaFleeThreshold", Any);

            foreach (string name in new[] { "DirectAttack", "DirectCart", "DirectDoor", "DirectSit", "DirectSleep", "DirectRepair", "DirectSmelt", "DirectHive", "DirectBoard", "DirectTombstoneRecovery", "DirectDeposit", "DirectGatherMode", "DirectGround", "CancelAll" })
            {
                MethodInfo method = interact.GetMethod(name, Any);
                if (method == null) return "CompanionInteract." + name;
                Direct[name] = method;
            }
            return null;
        }

        /// <summary>El compañero propio al que mira el jugador o, si no, el más cercano dentro del alcance.</summary>
        public static Component FindTarget(Player player, float range)
        {
            GameObject hover = player.GetHoverObject();
            if (hover != null)
            {
                Component looked = hover.GetComponentInParent(_setupType);
                if (looked != null && IsMine(looked) && IsAlive(looked)) return looked;
            }

            Component best = null;
            float bestDistance = range;
            foreach (object item in (IEnumerable)_allCompanions.GetValue(null, null))
            {
                Component c = item as Component;
                if (c == null || !IsMine(c) || !IsAlive(c)) continue;
                float d = Vector3.Distance(player.transform.position, c.transform.position);
                if (d <= bestDistance)
                {
                    best = c;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Los compañeros vivos de este jugador que están cargados en el mundo.</summary>
        public static IEnumerable<Component> MyCompanions()
        {
            var result = new List<Component>();
            foreach (object item in (IEnumerable)_allCompanions.GetValue(null, null))
            {
                Component c = item as Component;
                if (c != null && IsMine(c) && IsAlive(c)) result.Add(c);
            }
            return result;
        }

        /// <summary>Todos los compañeros vivos cargados en el mundo, también los de otros jugadores.</summary>
        public static IEnumerable<Component> AllCompanions()
        {
            var result = new List<Component>();
            foreach (object item in (IEnumerable)_allCompanions.GetValue(null, null))
            {
                Component c = item as Component;
                if (c != null && IsAlive(c)) result.Add(c);
            }
            return result;
        }

        public static string Name(Component companion)
        {
            Character character = companion.GetComponent<Character>();
            string name = character != null ? character.GetHoverName() : null;
            return string.IsNullOrEmpty(name) ? ModLang.Pick("Compañero", "Companion") : name;
        }

        public static string Status(Component companion)
        {
            return _describe.Invoke(null, new object[] { companion }) as string;
        }

        /// <summary>Por qué no debería hacer un gesto ahora mismo, o null si puede. Se mira en el juego, no se deja a juicio de la IA.</summary>
        public static string WhyNoEmote(Component companion)
        {
            Character character = companion.GetComponent<Character>();
            if (character == null || character.GetHealth() <= 0f) return ModLang.Pick("no está disponible", "not available");
            if (InCombat(companion) || EnemyNear(companion, EmoteDangerRange)) return ModLang.Pick("hay peligro cerca", "danger nearby");
            if (!character.IsOnGround() || character.IsSwimming() || character.IsAttached() || character.InAttack() || character.IsStaggering()) return ModLang.Pick("no está en condiciones", "not in position");
            Component rest = companion.GetComponent(_restType);
            if (rest != null && IsResting(rest)) return ModLang.Pick("está descansando", "is resting");
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null || zdo.GetInt(_actionModeHash) != 0 || IsWorking(companion)) return ModLang.Pick("está trabajando", "is working");
            return null;
        }

        /// <summary>Si sigue al jugador. Si no, le han dicho que se quede en algún sitio, y no debe irse por su cuenta.</summary>
        public static bool IsFollowing(Component companion)
        {
            return (bool)_getFollow.Invoke(companion, null);
        }

        public static void PlayEmote(Component companion, string trigger)
        {
            _takeControl.Invoke(null, new object[] { companion });
            // Un aviso de parada que quedara pendiente, como al levantarse, cortaría el gesto nada más empezar.
            companion.GetComponentInChildren<Animator>()?.ResetTrigger("emote_stop");
            companion.GetComponent<ZSyncAnimation>()?.SetTrigger("emote_" + trigger);
        }

        public static bool IsPlayingEmote(Component companion)
        {
            Animator animator = companion.GetComponentInChildren<Animator>();
            if (animator == null) return false;
            AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            return state.IsTag("emote");
        }

        public static void StopEmote(Component companion)
        {
            if (IsPlayingEmote(companion)) companion.GetComponent<ZSyncAnimation>()?.SetTrigger("emote_stop");
        }

        /// <summary>Gira poco a poco, para que no parezca un salto al empezar el gesto.</summary>
        public static void Face(Component companion, Component target, float deltaTime)
        {
            Character character = companion.GetComponent<Character>();
            if (character == null || target == null) return;
            Vector3 flat = target.transform.position - companion.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            companion.transform.rotation = Quaternion.RotateTowards(companion.transform.rotation, Quaternion.LookRotation(flat.normalized), EmoteTurnSpeed * deltaTime);
            character.SetLookDir((AimPoint(target) - character.GetEyePoint()).normalized);
        }

        public static bool IsCompanion(GameObject target)
        {
            return target.GetComponent(_setupType) != null;
        }

        /// <summary>Muestra el texto el tiempo indicado, sin que los comentarios casuales lo tapen mientras tanto.</summary>
        public static void Say(Component companion, string text, float seconds)
        {
            if (Chat.instance == null) return;
            Chat.instance.SetNpcText(companion.gameObject, Vector3.up * 2f, SpeechDistance, seconds, "", text, false);

            // CompanionTalk no habla mientras no haya pasado su pausa desde esta marca de tiempo.
            Component talk = companion.GetComponent(_talkType);
            if (talk != null) _lastSayTime.SetValue(talk, Time.time + seconds);
        }

        /// <summary>Aplica una acción ya validada. Devuelve el error, o null si se aplicó; note añade detalles del resultado.</summary>
        public static string Execute(Component companion, IList<Component> group, Player player, LookTarget look, ActionRequest action, out string note)
        {
            note = null;
            if (companion == null || !IsAlive(companion) || player == null) return ModLang.Pick("El compañero ya no está disponible.", "The companion is no longer available.");
            try
            {
                _takeControl.Invoke(null, new object[] { companion });
                ZNetView view = companion.GetComponent<ZNetView>();
                ZDO zdo = view != null ? view.GetZDO() : null;
                if (zdo == null) return ModLang.Pick("El compañero ya no está disponible.", "The companion is no longer available.");

                switch (action.Kind)
                {
                    case ActionKind.SetJob:
                        if (action.Value != 0) GetUp(companion);
                        // Con Seguir activo solo atendería lo que está a unos 10 m del jugador.
                        if (CompanionActions.UsesWorkArea(action.Value) && !action.KeepFollowing) SetFollow(companion, zdo, false);
                        SetJob(companion, zdo, action.Value);
                        note = ToolNote(companion, action.Value);
                        return null;
                    case ActionKind.Gather:
                        GetUp(companion);
                        SetFollow(companion, zdo, false);
                        SetJob(companion, zdo, action.Value);
                        note = CompanionActions.OnlyOnGround(action.Item)
                            ? ModLang.Pick("Lo busca por el suelo, sin picar: suele estar cerca del agua.", "Searches for it on the ground without mining: usually near water.")
                            : ToolNote(companion, action.Value);
                        return null;
                    case ActionKind.SetFollow:
                        if (action.Value != 0)
                        {
                            GetUp(companion);
                            // Si no, de noche volvería a acostarse en cuanto se levantara.
                            _stayAwake.Invoke(null, new object[] { companion });
                        }
                        SetFollow(companion, zdo, action.Value != 0);
                        return null;
                    case ActionKind.SetStance:
                        _setCombatStance.Invoke(companion, new object[] { action.Value });
                        return null;
                    case ActionKind.SetHomeRadius:
                        if (!(bool)_hasHome.Invoke(companion, null)) return ModLang.Pick("Todavía no tiene un hogar fijado.", "Has no home point set yet.");
                        _setHomeRadius.Invoke(companion, new object[] { (float)action.Value });
                        return null;
                    case ActionKind.Rename:
                        zdo.Set(_nameHash, action.Item);
                        Character character = companion.GetComponent<Character>();
                        if (character != null) character.m_name = action.Item;
                        return null;
                    case ActionKind.Consume:
                        return Consume(companion, action.Item);
                    case ActionKind.Equip:
                        return Equip(companion, action.Item);
                    case ActionKind.SetHome:
                        _setHome.Invoke(companion, new object[] { companion.transform.position });
                        return null;
                    case ActionKind.SetOption:
                        SetOption(companion, zdo, action.Value, action.Enabled);
                        return null;
                }

                // Estas órdenes pasan por las mismas funciones que la tecla de mando, y esta no atiende si está desactivada.
                if (!WillObey(companion))
                {
                    return ModLang.Pick(
                        "Está demasiado lejos del jugador o tiene desactivada la opción Obedecer (set_option obey la vuelve a activar).",
                        "Too far from the player or has Obey disabled (set_option obey re-enables it).");
                }

                switch (action.Kind)
                {
                    case ActionKind.Attack:
                        return Attack(companion, player, look, action.Value);
                    case ActionKind.GoTo:
                        if (action.Stay)
                        {
                            // En este orden: quitar el trabajo puede volver a activar el seguimiento.
                            if (zdo.GetInt(_actionModeHash) != 0) SetJob(companion, zdo, 0);
                            SetFollow(companion, zdo, false);
                        }
                        return GoTo(companion, player, look, action.Value);
                    case ActionKind.UseLookedAt:
                        return UseLookedAt(group ?? new[] { companion }, player, look);
                    case ActionKind.Rest:
                        return Rest(companion, player, action.Value, out note);
                    case ActionKind.StoreItems:
                        return StoreItems(companion, player, out note);
                    case ActionKind.RecoverGear:
                        return RecoverGear(companion, player);
                    case ActionKind.RepairGear:
                        CraftingStation station = Nearest<CraftingStation>(companion, SearchRange, null);
                        if (station == null) return ModLang.Pick("No hay ninguna estación de trabajo a menos de " + SearchRange + " m.", "No crafting station within " + SearchRange + " m.");
                        Invoke("DirectRepair", companion, player, station);
                        return null;
                    case ActionKind.ComeHere:
                        return ComeHere(companion, player, zdo);
                    case ActionKind.CancelOrders:
                        Invoke("CancelAll", companion, player);
                        return null;
                    default:
                        return ModLang.Pick("Acción no disponible.", "Action not available.");
                }
            }
            catch (TargetInvocationException ex)
            {
                Plugin.Log.LogError("[Acciones] Falló " + action.Kind + ": " + ex.InnerException);
                return ModLang.Pick("No se pudo aplicar la orden.", "Could not apply the order.");
            }
        }

        /// <summary>A quién atacaría con esa orden, o null si no hay nadie.</summary>
        public static Character AttackTarget(Component companion, Player player, LookTarget look, int target)
        {
            Character enemy = target == CompanionActions.TargetLookedAt ? look?.Enemy : NearestEnemy(companion, player);
            return enemy != null && enemy.GetHealth() > 0f ? enemy : null;
        }

        /// <summary>Su aguante, de 0 a 1. Si no se puede saber, 1, para no alarmar sin motivo.</summary>
        public static float StaminaShare(Component companion)
        {
            Component stamina = _staminaType != null ? companion.GetComponent(_staminaType) : null;
            return stamina != null && _staminaShare != null ? (float)_staminaShare.Invoke(stamina, null) : 1f;
        }

        /// <summary>Por debajo de este aguante, Offline Companions lo manda nadando a la orilla.</summary>
        public static float SwimTiredShare()
        {
            return (_swimTiredShare?.GetValue(null) as ConfigEntry<float>)?.Value ?? DefaultSwimTiredShare;
        }

        /// <summary>
        /// Lo pone en el suelo seco más cercano o, si no hay, junto al jugador. Devuelve qué se ha hecho, para el registro,
        /// o null si no hay dónde ponerlo o su IA corre en otro equipo.
        /// </summary>
        public static string LeaveWater(Component companion, Player player)
        {
            ZNetView view = companion.GetComponent<ZNetView>();
            if (view == null || !view.IsOwner()) return null;

            Vector3 from = companion.transform.position;
            bool foundSpot = FindDrySpot(from, out Vector3 spot);
            if (!foundSpot)
            {
                if (player.IsSwimming() || !player.IsOnGround()) return null;
                spot = player.transform.position - player.transform.forward * BesidePlayerDistance;
            }

            string report = "estaba en " + from.ToString("F1") + ", " + WaterAround(from);
            spot.y += PlacedAboveGround;
            PlaceAt(companion, spot);
            return report + "; sale a " + spot.ToString("F1") + (foundSpot ? ", a " + Vector3.Distance(from, spot).ToString("F1") + " m" : ", junto al jugador");
        }

        /// <summary>Lo pone en ese punto. Si la zona no está cargada, el juego lo guarda ahí y lo vuelve a cargar cuando haga falta.</summary>
        public static void PlaceAt(Component companion, Vector3 spot)
        {
            companion.transform.position = spot;
            Rigidbody body = companion.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = spot;
                body.linearVelocity = Vector3.zero;
            }
            companion.GetComponent<ZNetView>()?.GetZDO()?.SetPosition(spot);
        }

        /// <summary>La postura de combate, con los valores de la rueda de órdenes.</summary>
        public static int CombatStance(Component companion)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            return zdo == null ? 0 : zdo.GetInt(_combatStanceHash);
        }

        public static bool HomePosition(Component companion, out Vector3 home)
        {
            home = Vector3.zero;
            if (_getHome == null || !(bool)_hasHome.Invoke(companion, null)) return false;
            home = (Vector3)_getHome.Invoke(companion, null);
            return true;
        }

        /// <summary>Hasta dónde puede alejarse del jugador sin que el juego lo descargue, según la distancia de simulación.</summary>
        public static float LoadedReach()
        {
            int zones = ZNet.instance != null ? ZNet.instance.GetSyncedSimulationDistance().NearSimulationDistance : SimulationDistance.OriginalNear;
            return Mathf.Max(MinLoadedReach, zones * ZoneSystem.instance.m_zoneSize * LoadedReachMargin);
        }

        /// <summary>Deja su trabajo y de seguir al jugador, y da el primer paso del camino. Devuelve por qué no puede, o null.</summary>
        public static string StartJourney(Component companion, Player player, Vector3 firstStep)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) return ModLang.Pick("El compañero ya no está disponible.", "The companion is no longer available.");
            if (!WillObey(companion))
            {
                return ModLang.Pick(
                    "Está demasiado lejos del jugador o tiene desactivada la opción Obedecer (set_option obey la vuelve a activar).",
                    "Too far from the player or has Obey disabled (set_option obey re-enables it).");
            }
            GetUp(companion);
            // En este orden: quitar el trabajo puede volver a activar el seguimiento.
            if (zdo.GetInt(_actionModeHash) != 0) SetJob(companion, zdo, 0);
            SetFollow(companion, zdo, false);
            WalkTo(companion, player, firstStep);
            return null;
        }

        /// <summary>Le pone a trabajar donde está, sin seguir al jugador. En este orden: cambiar de trabajo puede volver a activar el seguimiento.</summary>
        public static void WorkInPlace(Component companion, int job)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) return;
            _takeControl.Invoke(null, new object[] { companion });
            GetUp(companion);
            SetJob(companion, zdo, job);
            SetFollow(companion, zdo, false);
        }

        public static bool OnJob(Component companion, int job)
        {
            ZDO zdo = companion.GetComponent<ZNetView>()?.GetZDO();
            return zdo != null && zdo.GetInt(_actionModeHash) == job;
        }

        /// <summary>Lo que hay para recoger alrededor de un punto. De las plantas, solo las silvestres: lo sembrado es del jugador.</summary>
        public static void ResourcesNear(Vector3 point, float radius, out bool trees, out bool rocks, out string plant)
        {
            bool Close(Component c)
            {
                Vector3 at = c.transform.position;
                at.y = point.y;
                return Vector3.Distance(at, point) <= radius;
            }
            bool Any<T>() where T : Component
            {
                foreach (T c in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (Close(c)) return true;
                return false;
            }
            trees = Any<TreeBase>();
            rocks = Any<MineRock5>() || Any<MineRock>();
            plant = null;
            foreach (Pickable pickable in Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None))
            {
                if (pickable.m_itemPrefab == null || pickable.m_respawnTimeMinutes <= 0f || !Close(pickable) || !pickable.CanBePicked()) continue;
                if (CompanionActions.JobForItem(pickable.m_itemPrefab.name) != CompanionActions.JobForage) continue;
                plant = pickable.m_itemPrefab.name;
                return;
            }
        }

        /// <summary>Lo que suelta el animal más cercano a un punto, o null. Lo saca de lo que suelta de verdad, no de una lista.</summary>
        public static string PreyNear(Component companion, Vector3 point, float radius)
        {
            Character self = companion.GetComponent<Character>();
            string best = null;
            float bestDistance = radius;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == self || c.IsPlayer() || c.GetHealth() <= 0f || self == null || !BaseAI.IsEnemy(self, c) || IsThreat(self, c)) continue;
                Vector3 at = c.transform.position;
                at.y = point.y;
                float distance = Vector3.Distance(at, point);
                if (distance > bestDistance) continue;
                CharacterDrop drops = c.GetComponent<CharacterDrop>();
                if (drops == null) continue;
                foreach (CharacterDrop.Drop drop in drops.m_drops)
                {
                    if (drop.m_prefab == null || CompanionActions.JobForItem(drop.m_prefab.name) != CompanionActions.JobHunt) continue;
                    best = drop.m_prefab.name;
                    bestDistance = distance;
                    break;
                }
            }
            return best;
        }

        /// <summary>Reborn solo mira a su alrededor y se queda quieto si no ve presas: esto es lo que hace falta para moverle a otro sitio.</summary>
        public static bool HuntSearchAvailable => _huntType != null && _huntState != null && _huntAnchor != null && _huntHasAnchor != null;

        private const int HuntIdle = 0;
        private const int HuntReturning = 2;

        /// <summary>Si está parado sin presa, listo para buscar en otro sitio.</summary>
        public static bool IsHuntIdle(Component companion)
        {
            Component hunt = HuntSearchAvailable ? companion.GetComponent(_huntType) : null;
            return hunt != null && (bool)_huntHasAnchor.GetValue(hunt) && Convert.ToInt32(_huntState.GetValue(hunt)) == HuntIdle;
        }

        /// <summary>Si persigue una presa o va de camino al sitio donde busca.</summary>
        public static bool IsHuntMoving(Component companion)
        {
            Component hunt = HuntSearchAvailable ? companion.GetComponent(_huntType) : null;
            return hunt != null && Convert.ToInt32(_huntState.GetValue(hunt)) != HuntIdle;
        }

        /// <summary>
        /// Le manda a buscar presas desde otro punto. Reborn lo trata como si volviera a su sitio de caza: rodea el agua
        /// a su manera, sigue mirando por el camino y, si ve una presa, va a por ella.
        /// </summary>
        public static void SearchPreyAt(Component companion, Vector3 point)
        {
            Component hunt = HuntSearchAvailable ? companion.GetComponent(_huntType) : null;
            if (hunt == null) return;
            // Reborn mide también la altura para saber si ha llegado.
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(point, out float ground)) point.y = ground;
            _huntAnchor.SetValue(hunt, point);
            _huntState.SetValue(hunt, Enum.ToObject(_huntState.FieldType, HuntReturning));
        }

        /// <summary>Si puede andar en línea recta hasta ahí, sin nadar más que un trozo corto ni trepar un barranco.</summary>
        public static bool CanWalkStraight(Component companion, Vector3 to)
        {
            Vector3 from = companion.transform.position;
            Vector3 way = to - from;
            way.y = 0f;
            float length = way.magnitude;
            if (length < 1f) return true;
            return StepAlong(companion, from, way / length, length, ShortSwimMeters, out Vector3 step) && Mathf.Abs(step.y - from.y) <= MaxSearchClimb;
        }

        /// <summary>Si ese punto ya está descubierto en el mapa del jugador.</summary>
        public static bool IsMapped(Vector3 point)
        {
            return Minimap.instance == null || _minimapIsExplored == null || (bool)_minimapIsExplored.Invoke(Minimap.instance, new object[] { point });
        }

        /// <summary>Descubre el mapa alrededor de él, con el mismo alcance que el jugador.</summary>
        public static void RevealMap(Vector3 point)
        {
            if (Minimap.instance != null) _minimapExplore?.Invoke(Minimap.instance, new object[] { point, MapRevealRadius });
        }

        public static float MapRevealRadius => Minimap.instance != null ? Minimap.instance.m_exploreRadius : 0f;

        /// <summary>Si ya hay en el mapa una marca igual en ese sitio.</summary>
        public static bool HasPin(Vector3 point, Minimap.PinType type, string name)
        {
            return Minimap.instance != null && _minimapHasPin != null && (bool)_minimapHasPin.Invoke(Minimap.instance, new object[] { point, type, name, true });
        }

        /// <summary>Lo que un explorador contaría al volver: lugares del mundo, cofres sin construir y vetas. Solo existe lo que está cargado.</summary>
        public static List<KeyValuePair<string, Vector3>> FindingsNear(Vector3 from, float radius)
        {
            var found = new List<KeyValuePair<string, Vector3>>();
            bool Near(Component c)
            {
                Vector3 at = c.transform.position;
                at.y = from.y;
                return Vector3.Distance(at, from) <= radius;
            }
            void Add(Component c, string label)
            {
                if (label != null) found.Add(new KeyValuePair<string, Vector3>(label, c.transform.position));
            }

            foreach (Location place in Object.FindObjectsByType<Location>(FindObjectsSortMode.None))
            {
                if (Near(place)) Add(place, Landmarks.Label(Utils.GetPrefabName(place.gameObject)));
            }
            foreach (Container chest in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                if (Near(chest) && IsVisibleChest(chest) && !IsBuilt(chest)) Add(chest, ModLang.Pick("un cofre", "a chest"));
            }
            foreach (MineRock5 rock in Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None))
            {
                if (Near(rock)) Add(rock, Landmarks.Ore(Utils.GetPrefabName(rock.gameObject)));
            }
            foreach (MineRock rock in Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None))
            {
                if (Near(rock)) Add(rock, Landmarks.Ore(Utils.GetPrefabName(rock.gameObject)));
            }
            return found;
        }

        /// <summary>Para que de noche no vuelva a la cama nada más empezar.</summary>
        public static void StayAwake(Component companion)
        {
            _stayAwake.Invoke(null, new object[] { companion });
        }

        /// <summary>Sigue andando. No pasa por la orden de la tecla de mando, que deja de atenderle en cuanto se aleja del jugador.</summary>
        public static void KeepWalking(Component companion, Vector3 step)
        {
            Component ai = companion.GetComponent(_aiType);
            if (ai != null) _setMoveTarget.Invoke(ai, new object[] { step });
        }

        /// <summary>
        /// Hasta dónde andar ahora de camino a ese lugar: de frente si como mucho cruza un trozo corto a nado, y si no, por donde
        /// rodee el agua, manteniendo el lado (side) para no ir y venir. False, sin moverse, si no hay por dónde seguir andando.
        /// </summary>
        public static bool NextStep(Component companion, Vector3 place, float length, ref int side, out Vector3 step, out bool detour)
        {
            Vector3 from = companion.transform.position;
            Vector3 way = place - from;
            way.y = 0f;
            float reach = Mathf.Min(length, way.magnitude);
            Vector3 ahead = way.normalized;
            detour = false;
            if (StepAlong(companion, from, ahead, reach, ShortSwimMeters, out step)) return true;

            detour = true;
            if (companion.GetComponent<Character>().IsSwimming())
            {
                if (FindDrySpot(from, out step)) return true;
                step = from;
                return false;
            }

            for (float angle = DetourAngleStep; angle < 180f; angle += DetourAngleStep)
            {
                foreach (int turn in new[] { side, -side })
                {
                    if (!StepAlong(companion, from, Quaternion.Euler(0f, angle * turn, 0f) * ahead, reach, 0f, out step)) continue;
                    side = turn;
                    return true;
                }
            }
            step = from;
            return false;
        }

        private static bool StepAlong(Component companion, Vector3 from, Vector3 direction, float length, float swimAllowed, out Vector3 step)
        {
            step = from + direction * length;
            if (ZoneSystem.instance == null || !ZoneSystem.instance.GetGroundHeight(step, out float ground)) return false;
            step.y = ground;
            if (BehaviorOverrides.WouldSwimAt(companion, step)) return false;

            float swim = 0f;
            for (float along = SwimSampleMeters; along < length; along += SwimSampleMeters)
            {
                Vector3 point = Vector3.Lerp(from, step, along / length);
                if (BehaviorOverrides.WouldSwimAt(companion, point)) swim += SwimSampleMeters;
                if (swim > swimAllowed) return false;
            }
            return true;
        }

        private static bool FindDrySpot(Vector3 from, out Vector3 spot)
        {
            spot = from;
            for (float radius = DrySpotStep; radius <= DrySpotRange; radius += DrySpotStep)
            {
                float nearestClimb = float.PositiveInfinity;
                for (int i = 0; i < DrySpotDirections; i++)
                {
                    float angle = i * Mathf.PI * 2f / DrySpotDirections;
                    Vector3 point = from + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                    if (!IsDryGround(point, out float ground) || Mathf.Abs(ground - from.y) >= nearestClimb) continue;
                    nearestClimb = Mathf.Abs(ground - from.y);
                    spot = new Vector3(point.x, ground, point.z);
                }
                if (!float.IsInfinity(nearestClimb)) return true;
            }
            return false;
        }

        // Tierra sobre el agua, sin una roca o una construcción encima, y sin mucha pendiente para que pueda andar desde ahí.
        private static bool IsDryGround(Vector3 point, out float ground)
        {
            ZoneSystem zones = ZoneSystem.instance;
            ground = 0f;
            if (zones == null || !zones.GetGroundHeight(point, out ground)) return false;
            if (ground < zones.m_waterLevel + DryAboveWater) return false;
            if (zones.GetSolidHeight(point, out float solid, SolidSearchHeight) && solid - ground > MaxSolidOnGround) return false;
            foreach (Vector3 side in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                if (!zones.GetGroundHeight(point + side * SlopeSampleDistance, out float near) || Mathf.Abs(near - ground) > MaxSlopeRise) return false;
            }
            return true;
        }

        // Para saber luego si era un hoyo o una orilla empinada: cada dirección es la altura del suelo a unos metros.
        private static string WaterAround(Vector3 from)
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (zones == null) return "sin datos del terreno";
            var heights = new List<string>();
            foreach (Vector3 side in new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left })
            {
                heights.Add(zones.GetGroundHeight(from + side * SurroundingsDistance).ToString("F1"));
            }
            return "suelo " + zones.GetGroundHeight(from).ToString("F1") + ", agua " + zones.m_waterLevel.ToString("F1") + ", a " + SurroundingsDistance.ToString("F0") + " m N/E/S/O: " + string.Join(" ", heights);
        }

        public static bool IsEngaged(Component companion)
        {
            Component combat = companion.GetComponent(_combatType);
            return combat != null && _isEngaged != null ? (bool)_isEngaged.GetValue(combat, null) : InCombat(companion);
        }

        /// <summary>Toda la munición que lleva. Si cambia, es que está disparando.</summary>
        public static int AmmoCarried(Component companion)
        {
            Inventory inventory = companion.GetComponent<Humanoid>()?.GetInventory();
            if (inventory == null) return 0;
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (IsAmmo(item)) count += item.m_stack;
            }
            return count;
        }

        public static string WeaponInHand(Component companion)
        {
            string token = companion.GetComponent<Humanoid>()?.GetCurrentWeapon()?.m_shared?.m_name;
            if (token == null) return ModLang.Pick("ninguna", "none");
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }

        /// <summary>Deja la pelea sin moverse de donde le corresponde: sigue al jugador si lo seguía, o se queda donde estaba.</summary>
        public static void StopFighting(Component companion, Player player)
        {
            Invoke("CancelAll", companion, player);
            Component combat = companion.GetComponent(_combatType);
            if (combat != null) _holdForOrder.Invoke(combat, new object[] { "Stopped" });
        }

        private static string Attack(Component companion, Player player, LookTarget look, int target)
        {
            Character enemy;
            if (target == CompanionActions.TargetLookedAt)
            {
                enemy = look?.Enemy;
                if (enemy == null || enemy.GetHealth() <= 0f) return ModLang.Pick("El jugador no está mirando a ningún enemigo.", "The player is not looking at any enemy.");
            }
            else
            {
                enemy = NearestEnemy(companion, player);
                if (enemy == null) return ModLang.Pick("No hay enemigos a menos de " + SearchRange + " m.", "No enemies within " + SearchRange + " m.");
            }
            Invoke("DirectAttack", companion, player, enemy);
            return null;
        }

        private static string GoTo(Component companion, Player player, LookTarget look, int place)
        {
            Vector3 point;
            if (place == CompanionActions.PlacePlayer)
            {
                point = player.transform.position;
            }
            else if (place == CompanionActions.PlaceLookedAt)
            {
                if (look?.Collider == null) return ModLang.Pick("El jugador no está mirando a ningún lugar concreto.", "The player is not looking at any specific place.");
                point = look.Point;
            }
            else
            {
                Component thing = NearestThing(companion, place, out string missing);
                if (thing == null) return ModLang.Pick("No hay " + missing + " a menos de " + Mathf.RoundToInt(SurroundingsRange) + " m.", "There is " + missing + " within " + Mathf.RoundToInt(SurroundingsRange) + " m.");
                // Se queda a su lado: el objeto en sí le cierra el paso.
                Vector3 toCompanion = companion.transform.position - thing.transform.position;
                toCompanion.y = 0f;
                point = thing.transform.position + toCompanion.normalized * BesideDistance;
            }

            // Con un trabajo de recolección activo, la función de mover solo termina ese trabajo; hay que repetirla para moverse.
            Component harvest = companion.GetComponent(_harvestType);
            bool gathering = harvest != null && (bool)_inGatherMode.GetValue(harvest, null);
            Invoke("DirectGround", companion, player, point);
            if (gathering) Invoke("DirectGround", companion, player, point);
            return null;
        }

        private static Component NearestThing(Component companion, int place, out string missing)
        {
            const float range = SurroundingsRange;
            switch (place)
            {
                case CompanionActions.PlaceChest:
                    missing = ModLang.Pick("ningún cofre", "no chest");
                    return Nearest<Container>(companion, range, IsVisibleChest);
                case CompanionActions.PlaceFoundChest:
                    missing = ModLang.Pick("ningún cofre encontrado, sin construir,", "no unbuilt world chest");
                    return Nearest<Container>(companion, range, c => IsVisibleChest(c) && !IsBuilt(c));
                case CompanionActions.PlaceBed:
                    missing = ModLang.Pick("ninguna cama", "no bed");
                    return Nearest<Bed>(companion, range, null);
                case CompanionActions.PlaceStation:
                    missing = ModLang.Pick("ninguna estación de trabajo", "no crafting station");
                    return Nearest<CraftingStation>(companion, range, null);
                case CompanionActions.PlaceFire:
                    missing = ModLang.Pick("ningún fuego", "no fire");
                    return Nearest<Fireplace>(companion, range, null);
                case CompanionActions.PlaceShip:
                    missing = ModLang.Pick("ningún barco", "no ship");
                    return Nearest<Ship>(companion, range, null);
                case CompanionActions.PlaceTree:
                    missing = ModLang.Pick("ningún árbol", "no tree");
                    return Nearest<TreeBase>(companion, range, null);
                case CompanionActions.PlaceRock:
                    missing = ModLang.Pick("ninguna roca", "no rock");
                    Component big = Nearest<MineRock5>(companion, range, null);
                    Component small = Nearest<MineRock>(companion, range, null);
                    if (big == null || small == null) return big ?? small;
                    Vector3 from = companion.transform.position;
                    return Vector3.Distance(from, big.transform.position) <= Vector3.Distance(from, small.transform.position) ? big : small;
                default:
                    missing = ModLang.Pick("ese lugar", "that place");
                    return null;
            }
        }

        /// <summary>
        /// Recibe al grupo entero: así se conserva lo que ya hace la tecla de mando con varios compañeros,
        /// como repartir asientos o mandar solo al más cercano a una puerta.
        /// </summary>
        private static string UseLookedAt(IList<Component> group, Player player, LookTarget look)
        {
            Collider c = look?.Collider;
            if (c == null) return ModLang.Pick("El jugador no está mirando nada útil.", "The player is not looking at anything useful.");

            if (look.Enemy != null)
            {
                if (look.Enemy.GetHealth() <= 0f) return ModLang.Pick("Ese enemigo ya está muerto.", "That enemy is already dead.");
                InvokeGroup("DirectAttack", group, player, look.Enemy);
                return null;
            }

            // El mismo orden que la tecla de mando.
            Vagon cart = c.GetComponentInParent<Vagon>();
            if (cart != null) { InvokeGroup("DirectCart", group, player, cart); return null; }
            Door door = c.GetComponentInParent<Door>();
            if (door != null) { InvokeGroup("DirectDoor", group, player, door); return null; }
            Fireplace fire = c.GetComponentInParent<Fireplace>();
            if (fire != null) { InvokeGroup("DirectSit", group, player, fire); return null; }
            Bed bed = c.GetComponentInParent<Bed>();
            if (bed != null) { InvokeGroup("DirectSleep", group, player, bed); return null; }
            CraftingStation station = c.GetComponentInParent<CraftingStation>();
            if (station != null) { InvokeGroup("DirectRepair", group, player, station); return null; }
            if (c.GetComponentInParent<Smelter>() != null) { InvokeGroup("DirectSmelt", group, player); return null; }
            Beehive hive = c.GetComponentInParent<Beehive>();
            if (hive != null) { InvokeGroup("DirectHive", group, player, hive); return null; }
            Ship ship = c.GetComponentInParent<Ship>();
            if (ship != null)
            {
                Chair seat = c.GetComponentInParent<Chair>();
                InvokeGroup("DirectBoard", group, player, ship, seat != null && seat.m_inShip ? seat : null);
                return null;
            }
            TombStone tomb = c.GetComponentInParent<TombStone>();
            if (tomb != null) { InvokeGroup("DirectTombstoneRecovery", group, player, tomb); return null; }
            Container chest = c.GetComponentInParent<Container>();
            if (chest != null && chest.GetComponent(_setupType) == null)
            {
                // Si no, se acercaría a dejar las cosas y volvería con ellas sin decir nada.
                List<CarriedStack> carried = StorableStacks(group[0]);
                if (carried.Count > 0 && chest.GetInventory() != null && StorePlanner.WhatFits(carried, SpaceOf(chest, 0f), false).Count == 0)
                {
                    return ModLang.Pick("Ese cofre está lleno: no cabe nada de lo que lleva.", "That chest is full: none of what they carry fits.");
                }
                InvokeGroup("DirectDeposit", group, player, chest);
                return null;
            }

            GameObject resource = _getHarvestable.Invoke(null, new object[] { c }) as GameObject;
            if (resource != null)
            {
                if (resource.GetComponent<MineRock5>() == null)
                {
                    // Una roca grande puede estar detrás de otra pieza en la misma línea de visión.
                    foreach (Collider behind in look.Behind)
                    {
                        MineRock5 rock = behind != null ? behind.GetComponentInParent<MineRock5>() : null;
                        if (rock != null)
                        {
                            resource = rock.gameObject;
                            break;
                        }
                    }
                }
                InvokeGroup("DirectGatherMode", group, player, resource);
                return null;
            }

            foreach (Component companion in group)
            {
                string failure = GoTo(companion, player, look, CompanionActions.PlaceLookedAt);
                if (failure != null) return failure;
            }
            return null;
        }

        private static void SetOption(Component companion, ZDO zdo, int option, bool enabled)
        {
            switch (option)
            {
                case CompanionActions.OptionWander:
                    _setWander.Invoke(companion, new object[] { enabled });
                    // Igual que en la rueda: deambular necesita un punto de referencia.
                    if (enabled && !(bool)_hasHome.Invoke(companion, null))
                    {
                        _setHome.Invoke(companion, new object[] { companion.transform.position });
                    }
                    break;
                case CompanionActions.OptionPickup:
                    zdo.Set(_autoPickupHash, enabled);
                    break;
                case CompanionActions.OptionObey:
                    _setCommandable.Invoke(companion, new object[] { enabled });
                    break;
            }
        }

        private static string Rest(Component companion, Player player, int kind, out string note)
        {
            note = null;
            if (kind == CompanionActions.RestSit)
            {
                Fireplace fire = Nearest<Fireplace>(companion, SearchRange, f => f.IsBurning());
                if (fire == null) return ModLang.Pick("No hay ningún fuego encendido a menos de " + SearchRange + " m.", "No lit fire within " + SearchRange + " m.");
                Invoke("DirectSit", companion, player, fire);
                return null;
            }

            Bed bed = Nearest<Bed>(companion, SearchRange, null);
            if (bed == null) return ModLang.Pick("No hay ninguna cama a menos de " + SearchRange + " m.", "No bed within " + SearchRange + " m.");

            Component rest = companion.GetComponent(_restType);
            bool wasResting = rest != null && IsResting(rest);
            Invoke("DirectSleep", companion, player, bed);
            bool nowResting = rest != null && IsResting(rest);
            if (wasResting && !nowResting)
            {
                note = ModLang.Pick("Se ha levantado de la cama.", "Got out of bed.");
            }
            else if (!nowResting)
            {
                return ModLang.Pick("No ha podido usar la cama más cercana: no es suya o está ocupada.", "Could not use the nearest bed: it belongs to someone else or is occupied.");
            }
            return null;
        }

        private static string ToolNote(Component companion, int job)
        {
            string missing = MissingTool(companion, job);
            return missing == null ? null : ModLang.Pick(
                "No lleva " + missing + ": si no hay en los cofres de casa, solo recogerá lo que esté suelto por el suelo.",
                "Not carrying " + missing + ": unless there is one in the home chests, will only pick up loose items from the ground.");
        }

        /// <summary>Mientras está sentado o en la cama ignora el resto de sus tareas, así que una orden debe levantarlo.</summary>
        private static void GetUp(Component companion)
        {
            Component rest = companion.GetComponent(_restType);
            if (rest != null && IsResting(rest)) _cancelRest.Invoke(rest, null);
        }

        private static bool IsResting(Component rest)
        {
            return (bool)_isResting.GetValue(rest, null) || (bool)_isNavigating.GetValue(rest, null);
        }

        private static string StoreItems(Component companion, Player player, out string note)
        {
            note = null;
            if (StorableStacks(companion).Count == 0)
            {
                note = ModLang.Pick("No llevaba nada que guardar.", "Was not carrying anything to store.");
                return null;
            }

            StoreTrip trip = PlanStoreTrip(companion, player, null, out Container chest, out int usable);
            if (trip == null)
            {
                return usable == 0
                    ? ModLang.Pick("No hay ningún cofre utilizable a menos de " + SearchRange + " m.", "No usable chest within " + SearchRange + " m.")
                    : ModLang.Pick("Los cofres a menos de " + SearchRange + " m están llenos: no cabe nada de lo que lleva.", "Chests within " + SearchRange + " m are full: none of what they carry fits.");
            }

            SendToChest(companion, player, chest, trip.Items, true);
            if (IsDepositing(companion)) return null;
            BehaviorOverrides.StoreOnly.Remove(companion.gameObject);
            return ModLang.Pick("No ha podido ir a guardar sus cosas.", "Could not go store their items.");
        }

        /// <summary>Lo que dejaría en un cofre, según las mismas reglas de Offline Companions. No cuenta lo que esté repartiendo ahora.</summary>
        public static List<CarriedStack> StorableStacks(Component companion)
        {
            BehaviorOverrides.StoreOnly.Remove(companion.gameObject);
            var stacks = new List<CarriedStack>();
            Humanoid humanoid = companion.GetComponent<Humanoid>();
            Inventory inventory = humanoid != null ? humanoid.GetInventory() : null;
            if (inventory == null) return stacks;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab == null || (bool)_shouldKeep.Invoke(null, new object[] { item, humanoid })) continue;
                stacks.Add(new CarriedStack { Item = item.m_dropPrefab.name, Count = item.m_stack, MaxStack = item.m_shared.m_maxStackSize });
            }
            return stacks;
        }

        public static StoreTrip PlanStoreTrip(Component companion, Player player, ICollection<int> refused, out Container chest, out int usable)
        {
            var found = new Dictionary<int, Container>();
            var spaces = new List<ChestSpace>();
            long playerId = player.GetPlayerID();
            foreach (Container candidate in Object.FindObjectsByType<Container>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(companion.transform.position, candidate.transform.position);
                if (distance > SearchRange || !CanStoreIn(candidate, playerId)) continue;
                found[candidate.GetInstanceID()] = candidate;
                spaces.Add(SpaceOf(candidate, distance));
            }
            usable = spaces.Count;
            StoreTrip trip = StorePlanner.Next(StorableStacks(companion), spaces, refused);
            chest = trip == null ? null : found[trip.Chest];
            return trip;
        }

        private static bool CanStoreIn(Container c, long playerId)
        {
            Piece piece = c.GetComponent<Piece>();
            return c.GetComponent(_setupType) == null
                && c.GetComponentInParent<TombStone>() == null
                && c.GetComponentInParent<Vagon>() == null
                && c.GetComponentInParent<Ship>() == null
                && piece != null
                && piece.IsPlacedByPlayer()
                && !c.IsInUse()
                && c.GetInventory() != null
                && PrivateArea.CheckAccess(c.transform.position, 0f, false)
                && (c.m_privacy != Container.PrivacySetting.Private || piece.GetCreator() == playerId);
        }

        private static ChestSpace SpaceOf(Container chest, float distance)
        {
            Inventory inventory = chest.GetInventory();
            var space = new ChestSpace { Id = chest.GetInstanceID(), Distance = distance, FreeSlots = inventory.GetEmptySlots() };
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_dropPrefab == null) continue;
                string name = item.m_dropPrefab.name;
                space.Holds.Add(name);
                int room = item.m_shared.m_maxStackSize - item.m_stack;
                if (room <= 0) continue;
                space.RoomInStacks.TryGetValue(name, out int known);
                space.RoomInStacks[name] = known + room;
            }
            return space;
        }

        /// <summary>El primer viaje pasa por la orden normal, que deja lo que estuviera haciendo y lo dice; los siguientes siguen sin más.</summary>
        public static void SendToChest(Component companion, Player player, Container chest, ICollection<string> items, bool first)
        {
            BehaviorOverrides.StoreOnly[companion.gameObject] = new HashSet<string>(items);
            if (first)
            {
                Invoke("DirectDeposit", companion, player, chest);
                return;
            }
            Component ai = companion.GetComponent(_aiType);
            if (ai != null) _setPendingDeposit.Invoke(ai, new object[] { chest, companion.GetComponent<Humanoid>() });
        }

        public static bool IsDepositing(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            return ai != null && (Object)_pendingDeposit.GetValue(ai) != null;
        }

        /// <summary>El cofre al que va ahora, para saber luego si ha servido.</summary>
        public static int DepositChest(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            Object chest = ai != null ? (Object)_pendingDeposit.GetValue(ai) : null;
            return chest != null ? chest.GetInstanceID() : 0;
        }

        public static string DepositChestPosition(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            Container chest = ai != null ? _pendingDeposit.GetValue(ai) as Container : null;
            return chest != null ? chest.transform.position.ToString("F1") : "?";
        }

        public static void CancelDeposit(Component companion)
        {
            Component ai = companion.GetComponent(_aiType);
            if (ai != null) _cancelDeposit.Invoke(ai, null);
        }

        public static string DescribeStacks(IList<CarriedStack> stacks)
        {
            var items = new Dictionary<string, int>();
            foreach (CarriedStack stack in stacks)
            {
                items.TryGetValue(stack.Item, out int count);
                items[stack.Item] = count + stack.Count;
            }
            return DescribeItems(items);
        }

        private static string RecoverGear(Component companion, Player player)
        {
            string id = PermanentId(companion);
            TombStone tomb = Nearest<TombStone>(companion, TombstoneRange, t =>
            {
                ZDO zdo = t.GetComponent<ZNetView>()?.GetZDO();
                return zdo != null && zdo.GetString(_tombCompanionIdHash) == id;
            });
            if (tomb == null) return ModLang.Pick("No hay ninguna tumba suya a menos de " + TombstoneRange + " m.", "None of their gravestones within " + TombstoneRange + " m.");
            Invoke("DirectTombstoneRecovery", companion, player, tomb);
            return null;
        }

        public static bool HasTombstone(string companionId)
        {
            foreach (TombStone tomb in Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None))
            {
                ZDO zdo = tomb.GetComponent<ZNetView>()?.GetZDO();
                if (zdo != null && zdo.GetString(_tombCompanionIdHash) == companionId) return true;
            }
            return false;
        }

        private static string ComeHere(Component companion, Player player, ZDO zdo)
        {
            Invoke("CancelAll", companion, player);
            _stayAwake.Invoke(null, new object[] { companion });
            SetFollow(companion, zdo, true);

            // Igual que al llamarlo con la tecla de mando: la orden tiene prioridad sobre una pelea.
            Component combat = companion.GetComponent(_combatType);
            if (combat != null) _holdForOrder.Invoke(combat, new object[] { "Recalled" });
            return null;
        }

        private static Character NearestEnemy(Component companion, Player player)
        {
            Character best = null;
            float bestDistance = SearchRange;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || c.GetHealth() <= 0f || !BaseAI.IsEnemy(player, c)) continue;
                float d = Vector3.Distance(companion.transform.position, c.transform.position);
                if (d <= bestDistance)
                {
                    best = c;
                    bestDistance = d;
                }
            }
            return best;
        }

        private static T Nearest<T>(Component from, float range, Func<T, bool> accept) where T : Component
        {
            T best = null;
            float bestDistance = range;
            foreach (T candidate in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(from.transform.position, candidate.transform.position);
                if (d <= bestDistance && (accept == null || accept(candidate)))
                {
                    best = candidate;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>Llama a una orden de la tecla de mando pasándole solo este compañero, para que no afecte a los demás.</summary>
        private static void Invoke(string name, Component companion, Player player, params object[] extra)
        {
            InvokeGroup(name, new[] { companion }, player, extra);
        }

        private static void InvokeGroup(string name, IList<Component> group, Player player, params object[] extra)
        {
            Array setups = Array.CreateInstance(_setupType, group.Count);
            for (int i = 0; i < group.Count; i++) setups.SetValue(group[i], i);

            var args = new object[2 + (extra?.Length ?? 0)];
            args[0] = setups;
            args[1] = player.GetPlayerID().ToString();
            extra?.CopyTo(args, 2);

            // Offline Companions rechaza las órdenes más allá de su alcance de mando, menor que el de un grito.
            // Esa misma comprobación también mira si obedece, así que solo se salta con quien tiene "Obedecer" activo.
            bool ignoreRange = _commandAnywhere != null && AllObey(group);
            object before = ignoreRange ? _commandAnywhere.GetValue(null) : null;
            if (ignoreRange) _commandAnywhere.SetValue(null, true);
            try
            {
                Direct[name].Invoke(null, args);
            }
            finally
            {
                if (ignoreRange) _commandAnywhere.SetValue(null, before);
            }
        }

        /// <summary>El alcance lo decide quien recibe el mensaje (hablar o gritar), no el alcance de mando de Offline Companions.</summary>
        private static bool WillObey(Component companion)
        {
            if (_commandAnywhere != null && AllObey(new[] { companion }))
            {
                _takeControl.Invoke(null, new object[] { companion });
                return true;
            }
            return (bool)_inScope.Invoke(null, new object[] { companion });
        }

        private static bool AllObey(IList<Component> group)
        {
            if (_isCommandable == null) return false;
            foreach (Component companion in group)
            {
                if (!(bool)_isCommandable.Invoke(companion, null)) return false;
            }
            return true;
        }

        private static void SetJob(Component companion, ZDO zdo, int mode)
        {
            zdo.Set(_actionModeHash, mode);
            _onJobSet.Invoke(null, new object[] { companion, mode });
            foreach (KeyValuePair<Type, MethodInfo> controller in JobControllers)
            {
                Component c = companion.GetComponent(controller.Key);
                if (c != null) controller.Value.Invoke(c, null);
            }
            _applyFollowMode.Invoke(companion, new object[] { mode, false });
        }

        private static void SetFollow(Component companion, ZDO zdo, bool follow)
        {
            _setFollow.Invoke(companion, new object[] { follow });
            _followSetByPlayer.Invoke(null, new object[] { companion });

            int mode = zdo.GetInt(_actionModeHash);
            // Igual que en la rueda: seguir al jugador termina los trabajos que no son compatibles con seguirlo.
            if (follow && (bool)_hasJob.Invoke(null, new object[] { companion }) && !(bool)_keepsFollow.Invoke(null, new object[] { mode }))
            {
                SetJob(companion, zdo, 0);
                return;
            }
            _applyFollowMode.Invoke(companion, new object[] { mode, false });
        }

        /// <summary>El ID que Offline Companions conserva aunque el compañero muera y reaparezca.</summary>
        public static string PermanentId(Component companion)
        {
            return _permanentId.Invoke(companion, null) as string;
        }

        private static bool IsMine(Component c)
        {
            return (bool)_isMine.Invoke(null, new object[] { c });
        }

        private static bool IsAlive(Component c)
        {
            Character character = c.GetComponent<Character>();
            return character != null && character.GetHealth() > 0f;
        }
    }
}
