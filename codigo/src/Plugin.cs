using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OfflineCompanionsAI
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("Maddhattr.OfflineCompanionsReborn", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin, TextReceiver
    {
        // Del GUID salen el .cfg y la carpeta de datos, con los ajustes, fichas y memorias. Una instalación que ya
        // existía conserva el antiguo para no perderlos; el paquete para instalar desde cero usa el nuevo.
#if NEW_INSTALL
        public const string PluginGuid = "SpiffoAiCompanions";
#else
        public const string PluginGuid = "OfflineCompanionsAI";
#endif
        public const string PluginName = "Spiffo's Valheim AI Companions";
        public const string PluginVersion = "0.26.3";

        private const float EmoteCooldown = 10f;

        private const float SmeltCheckInterval = 2f;
        private const float SmeltSettleSeconds = 15f;
        private const float SmeltStuckSeconds = 180f;
        private const float SmeltTimeoutSeconds = 1800f;

        private const string BalanceCommand = "/saldo";
        private const string BalanceCommandEn = "/balance";
        private const string BalanceCommandRu = "/баланс";
        private const string HelpCommand = "/ayuda";
        private const string HelpCommandEn = "/help";
        private const string HelpCommandRu = "/помощь";
        private const string StatusCommand = "/estado";
        private const string StatusCommandEn = "/status";
        private const string StatusCommandRu = "/статус";
        private const string RecallCommand = "/llamar";
        private const string RecallCommandEn = "/recall";
        private const string CallCommandEn = "/call";
        private const string RecallCommandRu = "/позвать";
        private const string PlacesCommand = "/lugares";
        private const string PlacesCommandEn = "/places";
        private const string PlacesCommandRu = "/места";
        private const string MarkCommand = "/marcar";
        private const string MarkCommandEn = "/mark";
        private const string MarkCommandRu = "/отметить";
        private const string ForgetCommand = "/olvidar";
        private const string ForgetCommandEn = "/forget";
        private const string ForgetCommandRu = "/забыть";
        private const string LanguageCommand = "/idioma";
        private const string LanguageCommandEn = "/language";
        private const string LangCommandEn = "/lang";
        private const string LanguageCommandRu = "/язык";
        private static string HomePlaceLabel => ModLang.Pick("su hogar", "their home", "свой дом");
        private const float MarkAboveGround = 0.2f;
        private const float PinRemoveRadius = 1f;
        private const float HintDelay = 8f;
        private const float PersistInterval = 5f;

        private const float CookStuckSeconds = 120f;
        private const float CookStoreSeconds = 60f;
        private const float OrganizeSettleSeconds = 20f;
        private const float OrganizeTimeoutSeconds = 600f;

        private const int BalancedStance = 0;
        private const float StayLeashMeters = 40f;
        private const float TendCheckSeconds = 20f;
        private const float TendFollowGraceSeconds = 3f;
        private const float TendLeashMargin = 10f;
        private const float TendWarnAgainShare = 0.8f;
        private static string TendHereLabel => ModLang.Pick("este sitio", "this place", "это место");
        private const float HuntSweepCell = 40f;
        private const float HuntSweepStep = 45f;
        private const float HuntSweepCheckSeconds = 2f;
        private const float HuntSweepWaitSeconds = 5f;
        private const float HuntSweepDangerSeconds = 30f;
        private static string ExploreStayLabel => ModLang.Pick("donde estaba explorando", "where they were exploring", "где шло исследование");
        private const float ExploreLookSeconds = 3f;
        private const float ExploreFindMeters = 50f;
        private const float ExploreSameFindMeters = 40f;
        private const float ExploreLookAheadShare = 0.8f;
        private const int MaxExploreFinds = 8;
        private const float NearHomeMeters = 50f;
        private const float ExpeditionCheckSeconds = 3f;
        private const float GuardRadius = 6f;
        private const float GuardEnemyRadius = 25f;
        private const float GuardCalmSeconds = 5f;
        private const float GatherSearchRange = 40f;
        private const float LoosePickupDistance = 2f;
        private const float LooseRewalkSeconds = 3f;
        private const float RetreatCooldown = 20f;

        // Espera tras una pelea antes de volver a una planta, por si llegan más enemigos.
        private const float PlantResumeDelay = 2f;

        // Margen para que recoja lo que ha soltado la planta antes de darla por terminada.
        private const float PlantPickupSeconds = 5f;
        private const float TreeCheckInterval = 1f;
        private const float TreeWorkRadius = 15f;
        private const float HealthInterval = 60f;
        private const float EventCheckInterval = 1f;
        private const float ChainCheckInterval = 0.5f;
        private const float SummaryRetryDelay = 300f;
        private const int MemorableTaskAmount = 5;
        private const float OrderRange = 54f;
        private const int InputLimit = 200;
        private const int MaxActionsPerMessage = 6;
        private const int MaxToolRounds = 3;
        private const string EditCommand = "/personaje";
        private const string EditCommandEn = "/character";
        private const string EditCommandRu = "/персонаж";
        private const string AutonomyCommand = "/autonomia";
        private const string AutonomyCommandAccented = "/autonomía";
        private const string AutonomyCommandEn = "/autonomy";
        private const string AutonomyCommandRu = "/автономия";
        private const float InitiativeCheckInterval = 5f;
        private const float InitiativeDangerRange = 15f;
        private const float StrongEnemyCheckInterval = 1f;
        private const float StrongEnemyWarnRange = 30f;
        private const float StrongEnemyWarnSeconds = 300f;
        private const float StrongEnemyAloneDistance = 15f;
        // Los mismos umbrales que usa Offline Companions: por debajo de uno se detiene, y no sigue hasta pasar el otro.
        private const float OutOfBreathShare = 0.3f;
        private const float OutOfBreathResume = 0.6f;
        private const float BusyChatFactor = 3f;
        private const int IdeasPerInitiative = 3;
        private const float GearCheckInterval = 15f;
        private const float GearTripCooldown = 120f;
        private const float PlayerLookedAngle = 2f;
        private const float PlayerMovedDistance = 1f;
        private const float PlayerIdleSeconds = 300f;
        private static readonly string[] Commands =
        {
            HelpCommand, HelpCommandEn, HelpCommandRu,
            StatusCommand, StatusCommandEn, StatusCommandRu,
            RecallCommand, RecallCommandEn, CallCommandEn, RecallCommandRu,
            BalanceCommand, BalanceCommandEn, BalanceCommandRu,
            EditCommand, EditCommandEn, EditCommandRu,
            AutonomyCommand, AutonomyCommandAccented, AutonomyCommandEn, AutonomyCommandRu,
            PlacesCommand, PlacesCommandEn, PlacesCommandRu,
            MarkCommand, MarkCommandEn, MarkCommandRu,
            ForgetCommand, ForgetCommandEn, ForgetCommandRu,
            LanguageCommand, LanguageCommandEn, LangCommandEn, LanguageCommandRu
        };
        private static readonly FieldInfo ChatHideTimer = AccessTools.Field(typeof(Chat), "m_hideTimer");

        internal static ManualLogSource Log;

        private readonly Dictionary<string, CompanionMemory> _memories = new Dictionary<string, CompanionMemory>();
        private readonly Dictionary<Component, OrderSequence<QueuedOrder>> _chains = new Dictionary<Component, OrderSequence<QueuedOrder>>();
        private readonly Dictionary<Component, GatherGoal> _goals = new Dictionary<Component, GatherGoal>();
        private readonly Dictionary<Component, TreeTask> _trees = new Dictionary<Component, TreeTask>();
        private readonly Dictionary<Component, PlantTask> _plants = new Dictionary<Component, PlantTask>();
        private readonly Dictionary<Component, GiveTask> _gives = new Dictionary<Component, GiveTask>();
        private readonly Dictionary<Component, TakeTask> _takes = new Dictionary<Component, TakeTask>();
        private readonly Dictionary<Component, PickupTask> _pickups = new Dictionary<Component, PickupTask>();
        private readonly Dictionary<Component, StoreGoal> _stores = new Dictionary<Component, StoreGoal>();
        private readonly Dictionary<Component, WaterTrapWatch> _waterTraps = new Dictionary<Component, WaterTrapWatch>();
        private readonly Dictionary<Component, HuntTask> _hunts = new Dictionary<Component, HuntTask>();
        private readonly Dictionary<Component, Vector3> _guards = new Dictionary<Component, Vector3>();
        private readonly Dictionary<Component, TravelTask> _travels = new Dictionary<Component, TravelTask>();
        private readonly Dictionary<Component, TendTask> _tends = new Dictionary<Component, TendTask>();
        private readonly Dictionary<Component, HuntSweep> _huntSweeps = new Dictionary<Component, HuntSweep>();
        private readonly Dictionary<Component, ExploreTask> _explores = new Dictionary<Component, ExploreTask>();
        private readonly HashSet<string> _foundThings = new HashSet<string>();
        private float _nextExpeditionCheckAt;
        private JsonFileStore<PlaceBook> _placeFiles;
        private PlaceBook _places;
        private string _placesWorld;
        private readonly Dictionary<Component, float> _guardCalmSince = new Dictionary<Component, float>();
        private readonly Dictionary<Component, float> _looseWalkAt = new Dictionary<Component, float>();
        private readonly Dictionary<string, Component> _restoredFor = new Dictionary<string, Component>();
        private ZoneSystem _world;
        private float _hintAt = -1f;
        private float _nextPersistAt;
        private readonly Dictionary<Component, GatherGoal> _cooks = new Dictionary<Component, GatherGoal>();
        private readonly Dictionary<Component, float> _cookStoreSince = new Dictionary<Component, float>();
        private readonly Dictionary<Component, UpkeepTask> _organizing = new Dictionary<Component, UpkeepTask>();
        private readonly Dictionary<Component, SmeltTask> _smelts = new Dictionary<Component, SmeltTask>();
        private readonly Dictionary<Component, EmoteGoal> _emotes = new Dictionary<Component, EmoteGoal>();
        private readonly Dictionary<Component, float> _nextEmoteAt = new Dictionary<Component, float>();
        private readonly Dictionary<Component, Queue<string>> _speech = new Dictionary<Component, Queue<string>>();
        private readonly Dictionary<Component, float> _nextPageAt = new Dictionary<Component, float>();
        private float _nextSmeltCheckAt;
        private readonly Dictionary<Component, int> _retreats = new Dictionary<Component, int>();
        private readonly Dictionary<Component, float> _retreatedAt = new Dictionary<Component, float>();
        private readonly Dictionary<Component, Dictionary<string, int>> _taskStartPack = new Dictionary<Component, Dictionary<string, int>>();
        private readonly Dictionary<string, TaskRecord> _lastTask = new Dictionary<string, TaskRecord>();
        private readonly Dictionary<Component, Worker> _workers = new Dictionary<Component, Worker>();
        private readonly HashSet<string> _diedAtWork = new HashSet<string>();
        private float _nextTreeCheckAt;
        private VoiceState _voiceState;
        private bool _voiceReleased;
        private bool _voiceCancelled;
        private float _voiceStartedAt;
        private float _nextChainCheckAt;
        private Harmony _harmony;
        private ModSettings _settings;
        private LlmClient _client;
        private CharacterStore _characters;
        private JsonFileStore<CompanionMemory> _memoryFiles;
        private CharacterEditor _editor;
        private ReactionPolicy _reactions;
        private WorldEvents _events;
        private float _nextEventCheckAt;
        private bool _ready;
        private bool _waiting;
        private bool _summarizing;
        private float _nextSummaryAt;
        private float _nextTalkAt;
        private float _nextHealthAt;
        private Component _pendingTarget;
        private string _chatMessage;
        private bool _chatShouted;
        private readonly InitiativeBudget _initiative = new InitiativeBudget();
        private readonly System.Random _random = new System.Random();
        private readonly Dictionary<string, InitiativeHistory> _initiativeHistory = new Dictionary<string, InitiativeHistory>();
        private readonly Dictionary<Component, float> _nextGearTripAt = new Dictionary<Component, float>();
        private float _nextInitiativeCheckAt;
        private float _nextGearCheckAt;
        private float _playerActiveAt;
        private Vector3 _lastPlayerPosition;
        private Vector3 _lastPlayerLook;
        private string _initiativeWait;
        private float _nextStrongCheckAt;
        private readonly HashSet<string> _threatsLogged = new HashSet<string>();
        private readonly Dictionary<string, float> _strongWarnedAt = new Dictionary<string, float>();

        private void Awake()
        {
            Log = Logger;
            _settings = new ModSettings(Config);
            CharacterEditor.OnLanguageChanged = lang => _settings.Language.Value = lang;
            _client = new LlmClient(_settings);
            string folder = Path.Combine(Paths.ConfigPath, PluginGuid);
            _characters = new CharacterStore(Path.Combine(folder, "personajes"));
            _memoryFiles = new JsonFileStore<CompanionMemory>(Path.Combine(folder, "memoria"));
            _placeFiles = new JsonFileStore<PlaceBook>(Path.Combine(folder, "lugares"));
            _editor = gameObject.AddComponent<CharacterEditor>();
            _reactions = new ReactionPolicy();
            _events = new WorldEvents(_settings, reaction => _reactions.Add(reaction, Time.time), RememberEvent, ExpeditionPlaceAt);
            _harmony = new Harmony(PluginGuid);
            PatchOwnTypes();
        }

        private void PatchOwnTypes()
        {
            foreach (System.Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Namespace == "OfflineCompanionsAI" && type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
            }
        }

        private void HoldCompanionWhileEditing()
        {
            MethodInfo isOpenFor = AccessTools.Method("Companions.CompanionRadialMenu:IsOpenFor");
            if (isOpenFor == null)
            {
                Log.LogWarning("El compañero no esperará mientras lo editas: falta CompanionRadialMenu.IsOpenFor.");
                return;
            }
            _harmony.Patch(isOpenFor, postfix: new HarmonyMethod(typeof(EditorInputPatches), nameof(EditorInputPatches.HoldCompanionPostfix)));
            EditorInputPatches.ExtraHold = companion => HoldForPlant(companion) || _emotes.ContainsKey(companion);
            try
            {
                BehaviorOverrides.SpeechMode = () => _settings.RebornSpeech.Value;
                BehaviorOverrides.GroundItemFor = GroundItemFor;
                SharedSpeech.ChatLine = AddToChat;
            BehaviorOverrides.ChopItemFor = ChopItemFor;
                BehaviorOverrides.Apply(_harmony);
                SpeechBubble.Apply(_harmony);
                GameChatInput.Handler = OnGameChat;
                GameChatInput.Apply(_harmony);
            }
            catch (System.Exception ex)
            {
                // Sin esto, un cambio en Offline Companions dejaría el mod sin arrancar por dos órdenes concretas.
                Log.LogError("Alguna función opcional no está disponible: " + ex.Message);
            }
        }

        private void Start()
        {
            if (Chainloader.PluginInfos.TryGetValue(Companions.CompanionsPlugin.PluginGUID, out PluginInfo reborn)
                && RebornCopy.IsForeign(reborn.Location, Assembly.GetExecutingAssembly().Location))
            {
                Log.LogError("Mod desactivado: hay otra copia de Offline Companions Reborn en " + reborn.Location + ". Este mod ya la lleva dentro: borra ese archivo con Valheim cerrado.");
                return;
            }
            // En Start, porque así Offline Companions Reborn ya ha cargado.
            string missing = CompanionBridge.Initialize();
            _ready = missing == null;
            if (_ready)
            {
                HoldCompanionWhileEditing();
                Log.LogInfo("Conectado con Offline Companions Reborn. Proveedor: " + _settings.Provider.Value);
                int patched = 0;
                foreach (MethodBase unused in _harmony.GetPatchedMethods()) patched++;
                Log.LogInfo("Métodos del juego parcheados: " + patched);
                if (!CompanionBridge.StrongEnemyFilterReady) Log.LogWarning("Evitar enemigos demasiado fuertes no está disponible en esta versión de Offline Companions Reborn.");
                Log.LogInfo("[Iniciativa] Alcance de trabajo: recursos a " + Mathf.RoundToInt(CompanionBridge.WorkRange("Gathering")) + " m, caza a " + Mathf.RoundToInt(CompanionBridge.WorkRange("Hunting")) + " m, conversación a " + Mathf.RoundToInt(_settings.TalkRange.Value) + " m.");
            }
            else
            {
                Log.LogError("Mod desactivado: esta versión de Offline Companions Reborn no incluye " + missing + ".");
            }
        }

        private void Update()
        {
            if (!_ready) return;
            if (Localization.instance != null) ModLang.UpdateGameLanguage(Localization.instance.GetSelectedLanguage());
            if (_world != ZoneSystem.instance)
            {
                _world = ZoneSystem.instance;
                ResetSession();
            }
            SharedSpeech.Update();
            AddModItemsOnce();
            if (Player.m_localPlayer == null) return;
            ShowHintOnce();

            if (_settings.Provider.Value == AiProvider.Player2 && Time.time >= _nextHealthAt)
            {
                _nextHealthAt = Time.time + HealthInterval;
                StartCoroutine(_client.PingHealth());
            }

            if (Input.GetKeyDown(_settings.TalkKey.Value) && CanOpenInput())
            {
                if (Input.GetKey(_settings.EditModifier.Value)) OpenEditor(Player.m_localPlayer);
                else OpenInput(Player.m_localPlayer);
            }

            UpdateGameChat(Player.m_localPlayer);
            UpdateVoice(Player.m_localPlayer);
            UpdateSpeech();
            UpdatePlants();
            UpdateEmotes(Player.m_localPlayer);
            UpdateGives(Player.m_localPlayer);
            UpdateTakes(Player.m_localPlayer);
            UpdateChains(Player.m_localPlayer);
            UpdateStrongEnemies(Player.m_localPlayer);
            UpdateReactions(Player.m_localPlayer);
            UpdateAutonomy(Player.m_localPlayer);
            SeatSync.Update();
        }

        /// <summary>
        /// Con autonomía, no busca peleas que no puede ganar. Avisa una vez cada cierto tiempo, y si estaba lejos del
        /// jugador con un encargo, lo deja: allí nadie le ayudaría.
        /// </summary>
        private void UpdateStrongEnemies(Player player)
        {
            if (Time.time < _nextStrongCheckAt) return;
            _nextStrongCheckAt = Time.time + StrongEnemyCheckInterval;
            if (!IsAutonomous || !_settings.AvoidStrongEnemies.Value || player.IsDead())
            {
                CompanionBridge.ForgetStrongEnemies();
                return;
            }

            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                string name = CompanionBridge.Name(companion);
                foreach (EnemyGroup group in CompanionBridge.AssessEnemies(companion, player, _settings.StrongEnemyRatio.Value))
                {
                    // Una línea por situación distinta, para poder ajustar el índice sin llenar el registro.
                    if (_threatsLogged.Add(name + group.Label + group.Count + group.TooStrong))
                    {
                        Log.LogInfo("[Amenaza] " + name + " ve " + group.Count + " " + group.Label + " a " + Mathf.RoundToInt(group.Nearest) + " m: índice " + group.Ratio.ToString("F1") + (group.TooStrong ? ", demasiado fuertes" : ", puede con ellos") + " (" + group.Details + ")");
                    }
                    if (group.TooStrong) AvoidStrongEnemy(player, companion, name, group);
                }
            }
        }

        private void AvoidStrongEnemy(Player player, Component companion, string name, EnemyGroup group)
        {
            // Quien se encarga de un sitio no vuelve con el jugador: solo deja de alejarse a recoger.
            bool tending = _tends.TryGetValue(companion, out TendTask tend);
            if (tending && tend.Chore != null && group.Nearest <= Initiative.AlertRange) AbandonTendChore(companion, tend);
            // Cazar sin cantidad no tiene una meta que abandonar, pero no debe seguir buscando presas hacia el peligro.
            if (group.Nearest <= Initiative.AlertRange && _huntSweeps.TryGetValue(companion, out HuntSweep sweep)) sweep.NextAt = Time.time + HuntSweepDangerSeconds;
            bool leftTask = !tending && group.Nearest <= Initiative.AlertRange && HasGoal(companion)
                && Vector3.Distance(player.transform.position, companion.transform.position) > StrongEnemyAloneDistance;
            if (leftTask)
            {
                ClearGoals(companion);
                _chains.Remove(companion);
                CompanionBridge.StopGathering(companion);
                string notice = ModLang.Pick(
                    name + " deja lo que hacía y vuelve contigo: hay " + group.Count + " " + group.Label + " cerca.",
                    name + " leaves what they were doing and returns to you: there are " + group.Count + " " + group.Label + " nearby.",
                    name + " бросает начатое и возвращается к тебе: рядом " + group.Count + " " + group.Label + ".");
                Notify(notice);
                AddToChat(name, notice);
                Log.LogInfo("[Amenaza] " + notice);
            }

            string key = name + group.Label;
            if (group.Nearest > StrongEnemyWarnRange || (_strongWarnedAt.TryGetValue(key, out float warned) && Time.time - warned < StrongEnemyWarnSeconds)) return;
            _strongWarnedAt[key] = Time.time;
            _reactions.Add(new Reaction
            {
                Kind = ReactionKind.StrongEnemy,
                Speaker = companion,
                Description = ModLang.Pick(
                    "Has visto " + group.Count + " " + group.Label + " y son demasiado fuertes para el equipo que llevan. No irás a por ellos salvo que ataquen de cerca o el jugador lo ordene." + (leftTask ? " Has dejado lo que hacías y vuelves con él." : ""),
                    "You saw " + group.Count + " " + group.Label + " and they are too strong for your current gear. You will not engage them unless they attack at close range or the player orders it." + (leftTask ? " You left what you were doing and are returning to the player." : ""),
                    "Ты видишь врагов (" + group.Count + " " + group.Label + "), и они слишком сильны для твоего снаряжения. Не атакуй их, если они не нападут в упор или игрок не прикажет." + (leftTask ? " Ты бросаешь начатое и возвращаешься к игроку." : ""))
            }, Time.time);
        }

        private enum VoiceState
        {
            Idle,
            Starting,
            Listening,
            Stopping
        }

        /// <summary>Al entrar en otro mundo o volver al menú, nada de lo que estaba en marcha sigue siendo válido.</summary>
        private void ResetSession()
        {
            StopAllCoroutines();
            CompanionBridge.ForgetStrongEnemies();
            SeatSync.Reset();
            _strongWarnedAt.Clear();
            _chains.Clear();
            _goals.Clear();
            _trees.Clear();
            _plants.Clear();
            _gives.Clear();
            _takes.Clear();
            _pickups.Clear();
            _stores.Clear();
            _waterTraps.Clear();
            BehaviorOverrides.KeepDry.Clear();
            BehaviorOverrides.StoreOnly.Clear();
            _hunts.Clear();
            _guards.Clear();
            _travels.Clear();
            _tends.Clear();
            BehaviorOverrides.TendArea.Clear();
            _huntSweeps.Clear();
            _explores.Clear();
            _foundThings.Clear();
            _nextExpeditionCheckAt = 0f;
            _places = null;
            _placesWorld = null;
            _guardCalmSince.Clear();
            _looseWalkAt.Clear();
            _retreats.Clear();
            _retreatedAt.Clear();
            _cooks.Clear();
            _cookStoreSince.Clear();
            _organizing.Clear();
            _taskStartPack.Clear();
            _lastTask.Clear();
            _workers.Clear();
            _diedAtWork.Clear();
            _initiativeHistory.Clear();
            _nextGearTripAt.Clear();
            _restoredFor.Clear();
            _memories.Clear();
            _reactions.Reset();
            _smelts.Clear();
            _emotes.Clear();
            _nextEmoteAt.Clear();
            _speech.Clear();
            _nextPageAt.Clear();
            _chatMessage = null;
            _initiative.Reset(Time.time);
            _playerActiveAt = Time.time;
            _nextInitiativeCheckAt = 0f;
            BehaviorOverrides.CookFood = null;
            BehaviorOverrides.SmeltItem = null;
            BehaviorOverrides.UpkeepFocus.Clear();
            _waiting = false;
            _summarizing = false;
            _voiceState = VoiceState.Idle;
            _hintAt = -1f;
            _nextTalkAt = 0f;
            _nextHealthAt = 0f;
            _nextEventCheckAt = 0f;
            _nextPersistAt = 0f;
        }

        private void ShowHintOnce()
        {
            if (_hintAt == float.MaxValue || !_settings.ShowHints.Value) return;
            if (_hintAt < 0f)
            {
                _hintAt = Time.time + HintDelay;
                return;
            }
            if (Time.time < _hintAt) return;
            _hintAt = float.MaxValue;
            Notify(ModLang.Pick(
                "Compañeros: " + TalkKeys() + " para hablar, " + _settings.VoiceKey.Value + " para voz. Escribe /ayuda para ver más.",
                "Companions: " + TalkKeys() + " to talk, " + _settings.VoiceKey.Value + " for voice. Type /help for more.",
                "Спутники: " + TalkKeys() + " — говорить, " + _settings.VoiceKey.Value + " — голос. Введи /помощь для справки."));
        }

        private string TalkKeys()
        {
            return (_settings.TalkInGameChat.Value ? ModLang.Pick("chat del juego o ", "in-game chat or ", "чат игры или ") : "") + _settings.TalkKey.Value;
        }

        private void ShowHelp()
        {
            if (ModLang.IsEnglish)
            {
                AddLine("Talk: " + TalkKeys() + " | Shout so they hear you from farther away: /s message | Voice: hold " + _settings.VoiceKey.Value + " | Character: " + _settings.EditModifier.Value + " + " + _settings.TalkKey.Value);
                AddLine("Commands: /help, /status, /recall, /balance, /character, /autonomy, /places, /mark name, /forget name, /language [en|es|ru|auto]");
                AddLine("Ask & remember: what's nearby?, what's in the chests?, remember my base is to the north");
                AddLine("Move: follow me, come here, go there, go to the chest, go there and stay, stay here, stop");
                AddLine("Places: mark base here, mark mine there, go back to base, go to base and tend it, forget mine");
                AddLine("Explore: explore around here, see what's out there (walks new ground, pins findings on your map and returns)");
                AddLine("Looking at something: attack that one, chop that tree, open that door, board that ship");
                AddLine("Tasks: get 10 stone, cook 3 cooked meat, smelt copper, hunt, fish, sort the chests");
                AddLine("Upkeep: repair buildings, refuel fires, harvest honey, stack materials");
                AddLine("Companion team: where is Bjorn?, who fishes best?, call everyone wherever they are");
                AddLine("Items: pick up that axe, give me what it dropped, give me 5 wood, drop the wood, take wood from the chest");
                AddLine("Gear: eat something, drink mead, equip the bow, repair your gear");
                AddLine("Combat: guard this area, switch to passive stance, retreat below half health");
                AddLine("Group: everyone, follow me | Bjorn, chop wood | Your name will be Saber");
                AddLine("Emotes: performs them automatically when fitting, or when you ask them to dance or wave");
                return;
            }
            if (ModLang.IsRussian)
            {
                AddLine("Разговор: " + TalkKeys() + " | Крикнуть дальше: /s сообщение | Голос: удерживай " + _settings.VoiceKey.Value + " | Анкета: " + _settings.EditModifier.Value + " + " + _settings.TalkKey.Value);
                AddLine("Команды: /помощь (/help), /статус (/status), /позвать (/recall), /баланс (/balance), /персонаж (/character), /автономия (/autonomy), /места (/places), /отметить имя (/mark), /забыть имя (/forget), /язык [en|es|ru|auto]");
                AddLine("Вопросы и память: что рядом?, что в сундуках?, помни: база на севере");
                AddLine("Движение: за мной, иди сюда, иди туда, иди к сундуку, оставайся там, стой здесь, стоп");
                AddLine("Места: отметь здесь базу, отметь там шахту, вернись на базу, иди на базу и следи за ней, забудь шахту");
                AddLine("Исследование: разведай окрестности, посмотри что вокруг (идёт по новой земле, ставит метки на карту и возвращается)");
                AddLine("По взгляду: атакуй его, сруби это дерево, открой эту дверь, садись в эту лодку");
                AddLine("Задания: добудь 10 камня, приготовь 3 жареного мяса, переплавь медь, охоться, лови рыбу, разложи по сундукам");
                AddLine("Уход за базой: почини постройки, добавь топлива в огни, собери мёд, сложи материалы в кучи");
                AddLine("Отряд спутников: где Бьёрн?, кто лучше ловит рыбу?, все ко мне где бы вы ни были");
                AddLine("Предметы: подними тот топор, дай то что выпало, дай 5 древесины, брось древесину, возьми древесину из сундука");
                AddLine("Снаряжение: поешь, выпей медовухи, возьми лук, почини снаряжение");
                AddLine("Бой: охраняй эту зону, перейди в пассивную стойку, отступай если меньше половины здоровья");
                AddLine("Группа: все, за мной | Бьёрн, руби дерево | Твоё имя будет Сейбер");
                AddLine("Эмоции: выражает автоматически к месту, или по просьбе станцевать или помахать рукой");
                return;
            }
            AddLine("Hablar: " + TalkKeys() + " | Gritar, para que te oiga de más lejos: /s mensaje | Voz: mantén " + _settings.VoiceKey.Value + " | Personaje: " + _settings.EditModifier.Value + " + " + _settings.TalkKey.Value);
            AddLine("Comandos: /ayuda, /estado, /llamar, /saldo, /personaje, /autonomia, /lugares, /marcar nombre, /olvidar nombre, /idioma [en|es|ru|auto]");
            AddLine("Saber y recordar: ¿qué hay cerca?, ¿qué hay en los cofres?, recuerda que mi base está al norte");
            AddLine("Moverse: sígueme, ven aquí, ve allí, ve al cofre, ve allí y quédate, quédate aquí, para");
            AddLine("Lugares: marca aquí la base, marca allí la mina, vuelve a la base, ve a la base y encárgate, olvida la mina");
            AddLine("Explorar: explora por ahí, a ver qué hay (anda por terreno nuevo, marca en el mapa lo que encuentra y vuelve)");
            AddLine("Mirando algo: ataca a ese, tala ese árbol, abre esa puerta, sube a ese barco");
            AddLine("Encargos: consigue 10 piedras, cocina 3 de carne asada, funde el cobre, caza, pesca, organiza los cofres");
            AddLine("Mantenimiento: repara las construcciones, reaviva los fuegos, recoge la miel, apila los materiales");
            AddLine("Equipo de compañeros: ¿dónde está Bjorn?, ¿quién pesca mejor?, que vengan todos estén donde estén");
            AddLine("Objetos: recoge esa hacha, dame lo que soltó, dame 5 de madera, suelta la madera, saca madera del cofre");
            AddLine("Equipo: come algo, bebe hidromiel, equípate el arco, repara tu equipo");
            AddLine("Combate: protege esta zona, ponte en postura pasiva, retírate con menos de la mitad de vida");
            AddLine("Grupo: todos, síganme | Bjorn, tala madera | Te llamarás Saber");
            AddLine("Gestos: los hace solo si encajan con lo que dice, o si le pides que baile o salude");
        }

        private void ShowStatus()
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            List<Component> all = new List<Component>(CompanionBridge.MyCompanions());
            if (all.Count == 0) AddLine(ModLang.Pick("No hay compañeros tuyos cargados cerca.", "None of your companions are loaded nearby.", "Поблизости нет загруженных спутников."));
            foreach (Component companion in all)
            {
                int distance = Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position));
                string status = CompanionBridge.Status(companion);
                string tasks = TaskSummary(companion, true).Replace("\n", " | ").Trim(' ', '|');
                string stance = CompanionActions.StanceName(CompanionBridge.CombatStance(companion));
                string id = CompanionBridge.PermanentId(companion);
                Inclination inclination = InclinationOf(id);
                string leaning = inclination == Inclination.Ninguna ? "" : ModLang.Pick(", inclinación ", ", inclination ", ", склонность ") + Inclinations.Name(inclination);
                AddLine(CompanionBridge.Name(companion)
                    + ModLang.Pick(" (a " + distance + " m, postura " + stance + leaning + "): ", " (" + distance + " m away, " + stance + " stance" + leaning + "): ", " (в " + distance + " м, стойка " + stance + leaning + "): ")
                    + (string.IsNullOrEmpty(status) ? ModLang.Pick("sin actividad", "idle", "без дела") : status)
                    + (tasks.Length > 0 ? " | " + tasks : ""));
            }

            List<string> roster = CompanionBridge.RosterLines(StayOf);
            if (roster.Count > 0) AddLine(ModLang.Pick("En este mundo: ", "In this world: ", "В этом мире: ") + string.Join("; ", roster.ToArray()));
            AddLine(ModLang.Pick(
                "Modo: " + (_settings.Autonomy.Value == AutonomyMode.Autonomo ? "autónomo" : "normal") + " (" + AutonomyCommand + " para cambiarlo)",
                "Mode: " + (_settings.Autonomy.Value == AutonomyMode.Autonomo ? "autonomous" : "normal") + " (" + AutonomyCommandEn + " to toggle)",
                "Режим: " + (_settings.Autonomy.Value == AutonomyMode.Autonomo ? "автономный" : "обычный") + " (" + AutonomyCommandRu + " для переключения)"));
        }

        private void ShowBalance()
        {
            AddLine(ModLang.Pick(
                "Peticiones de IA desde que abriste el juego: " + _client.RequestsThisRun,
                "AI requests since game launch: " + _client.RequestsThisRun,
                "Запросов к ИИ с запуска игры: " + _client.RequestsThisRun));
            if (_settings.Provider.Value != AiProvider.Player2 && !_settings.VoiceEnabled.Value) return;
            StartCoroutine(_client.FetchJoules(joules =>
            {
                AddLine(joules.HasValue
                    ? ModLang.Pick("Saldo de Player2: " + joules.Value + " joules", "Player2 balance: " + joules.Value + " joules", "Баланс Player2: " + joules.Value + " джоулей")
                    : ModLang.Pick("No se pudo consultar el saldo de Player2.", "Could not check Player2 balance.", "Не удалось проверить баланс Player2."));
            }));
        }

        private void AddLine(string text)
        {
            if (Chat.instance == null) return;
            Chat.instance.AddString("<color=#9fd0ff>" + text + "</color>");
            ChatHideTimer?.SetValue(Chat.instance, 0f);
        }

        private void AddOrderLine(string text)
        {
            if (_settings.ShowOrdersInChat.Value) AddLine(text);
        }

        /// <summary>Guarda lo que debe sobrevivir al cierre, y lo recupera cuando un compañero se carga o reaparece.</summary>
        private void UpdatePersistence()
        {
            if (Time.time < _nextPersistAt) return;
            _nextPersistAt = Time.time + PersistInterval;

            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                string id = CompanionBridge.PermanentId(companion);
                if (string.IsNullOrEmpty(id)) continue;
                CompanionMemory memory = MemoryOf(id);

                if (!_restoredFor.TryGetValue(id, out Component known) || known != companion)
                {
                    _restoredFor[id] = companion;
                    if (memory.RetreatPercent > 0) _retreats[companion] = memory.RetreatPercent;
                    if (memory.GuardActive) _guards[companion] = new Vector3(memory.GuardX, memory.GuardY, memory.GuardZ);
                    if (memory.StayTend && memory.StayLabel != null && !_tends.ContainsKey(companion)) AddTend(companion, new Vector3(memory.StayX, memory.StayY, memory.StayZ), memory.StayLabel);
                    if (!_lastTask.ContainsKey(id) && !string.IsNullOrEmpty(memory.LastTaskDescription))
                    {
                        _lastTask[id] = new TaskRecord { Description = memory.LastTaskDescription, Gained = new Dictionary<string, int>(memory.LastTaskGained) };
                    }
                    continue;
                }

                _retreats.TryGetValue(companion, out int retreat);
                bool guarding = _guards.TryGetValue(companion, out Vector3 point);
                _lastTask.TryGetValue(id, out TaskRecord last);
                if (memory.UpdateState(retreat, guarding, point.x, point.y, point.z, last?.Description, last?.Gained)) SaveMemory(id, memory);

                // Pudo mandarle otra cosa con la rueda de órdenes, sin pasar por aquí.
                if (memory.StayLabel != null && !memory.StayTend && !_travels.ContainsKey(companion) && !CompanionBridge.InCombat(companion)
                    && (CompanionBridge.IsFollowing(companion) || HasGoal(companion) || FlatDistance(companion.transform.position, new Vector3(memory.StayX, memory.StayY, memory.StayZ)) > StayLeashMeters))
                {
                    memory.ClearStay();
                    SaveMemory(id, memory);
                }
            }
        }

        private void UpdateVoice(Player player)
        {
            if (_voiceState == VoiceState.Idle)
            {
                if (_settings.VoiceEnabled.Value && Input.GetKeyDown(_settings.VoiceKey.Value) && CanOpenInput()) BeginVoice(player);
                return;
            }
            if (_voiceState == VoiceState.Stopping) return;

            // Si se abre un menú mientras escucha, no debe enviarse lo que se esté diciendo.
            if (!CanOpenInput() || player.IsDead()) _voiceCancelled = true;
            if (!Input.GetKey(_settings.VoiceKey.Value) || _voiceCancelled) _voiceReleased = true;

            if (_voiceState == VoiceState.Listening
                && (_voiceReleased || VoiceProtocol.ShouldStop(Time.unscaledTime - _voiceStartedAt, _settings.VoiceMaxSeconds.Value)))
            {
                EndVoice();
            }
        }

        private void BeginVoice(Player player)
        {
            if (_waiting)
            {
                Notify(ModLang.Pick("Tu compañero todavía está pensando...", "Your companion is still thinking...", "Твой спутник всё ещё думает..."));
                return;
            }
            if (Time.time < _nextTalkAt || FindTargetOrNotify(player) == null) return;

            // Evita que se cuele un comentario automático o un mensaje escrito mientras hablas.
            _waiting = true;
            _voiceState = VoiceState.Starting;
            _voiceReleased = false;
            _voiceCancelled = false;
            StartCoroutine(_client.StartListening(
                _settings.VoiceMaxSeconds.Value,
                () =>
                {
                    _voiceState = VoiceState.Listening;
                    _voiceStartedAt = Time.unscaledTime;
                    if (!_voiceReleased) Notify(ModLang.Pick("Escuchando... suelta la tecla para enviar.", "Listening... release the key to send.", "Слушаю... отпусти клавишу для отправки."));
                },
                error =>
                {
                    _voiceState = VoiceState.Idle;
                    _waiting = false;
                    Notify(error);
                }));
        }

        private void EndVoice()
        {
            // Una pulsación accidental también debe cerrar la escucha en la app, aunque se ignore el resultado.
            bool ignore = _voiceCancelled || Time.unscaledTime - _voiceStartedAt < VoiceProtocol.MinSeconds;
            _voiceState = VoiceState.Stopping;
            StartCoroutine(_client.StopListening(
                text =>
                {
                    _voiceState = VoiceState.Idle;
                    _waiting = false;
                    if (ignore) return;
                    if (text == null)
                    {
                        Notify(ModLang.Pick("No he entendido lo que has dicho.", "I couldn't understand what you said.", "Я не понял, что ты сказал."));
                        return;
                    }
                    // Se busca ahora: el jugador puede haberse movido mientras se reconocía.
                    Player current = Player.m_localPlayer;
                    Component target = current != null ? FindTargetOrNotify(current) : null;
                    if (target != null) Send(current, target, text);
                },
                error =>
                {
                    _voiceState = VoiceState.Idle;
                    _waiting = false;
                    if (!ignore) Notify(error);
                }));
        }

        private void UpdateReactions(Player player)
        {
            if (!_settings.ReactionsEnabled.Value || Time.time < _nextEventCheckAt) return;
            _nextEventCheckAt = Time.time + EventCheckInterval;
            _events.Tick(player);

            if (_waiting || player.IsDead() || !CanOpenInput()) return;
            Reaction reaction = _reactions.TryTake(Time.time, _settings.ReactionCooldown.Value, _settings.ReactionsPerHour.Value);
            if (reaction != null) React(player, reaction);
        }

        private void React(Player player, Reaction reaction)
        {
            Component target = reaction.Speaker as Component;
            if (target == null) target = CompanionBridge.FindTarget(player, _settings.TalkRange.Value);
            if (target == null || Vector3.Distance(player.transform.position, target.transform.position) > _settings.TalkRange.Value) return;

            string name = CompanionBridge.Name(target);
            string id = CompanionBridge.PermanentId(target);
            // Sin herramientas: un comentario nunca debe cambiar lo que hace el compañero, salvo que el jugador le dé autonomía.
            bool canAct = reaction.CanResolve && IsAutonomous && CanActOnOwn(target);
            List<ChatMessage> request = BuildRequest(player, target, id, canAct ? Initiative.EventMessage(reaction) : ReactionPolicy.EventMessage(reaction));

            _waiting = true;
            Log.LogInfo("[Reacciones] " + name + ": " + reaction.Kind);
            CompleteOnOwn(target, name, id, request, ReactionPolicy.MemoryLine(reaction), canAct);
        }

        private bool IsAutonomous => _settings.Autonomy.Value == AutonomyMode.Autonomo && _settings.AllowActions.Value;

        /// <summary>Solo si está libre y siguiendo al jugador: nunca interrumpe un encargo ni se va de donde le dijeron que se quedara.</summary>
        private bool CanActOnOwn(Component companion)
        {
            if (companion == null || HasGoal(companion) || _guards.ContainsKey(companion)) return false;
            if (_chains.TryGetValue(companion, out OrderSequence<QueuedOrder> chain) && chain.Pending > 0) return false;
            return CompanionBridge.WhyNoEmote(companion) == null && CompanionBridge.IsFollowing(companion);
        }

        /// <summary>Una petición que no ha hecho el jugador. Un fallo no merece un aviso en pantalla.</summary>
        private void CompleteOnOwn(Component target, string name, string id, List<ChatMessage> request, string memoryLine, bool canAct, JArray tools = null)
        {
            tools = tools ?? Initiative.Tools();
            StartCoroutine(_client.Complete(
                request,
                canAct ? tools : null,
                response =>
                {
                    // La situación puede haber cambiado mientras se esperaba la respuesta.
                    if (canAct && response.ToolCalls.Count > 0 && CanActOnOwn(target))
                    {
                        RunActions(target, null, name, id, request, memoryLine, response, new MessageActions { Initiative = true, OnOwnTools = tools });
                        return;
                    }
                    Finish(target, name, id, memoryLine, response.Text ?? "");
                },
                error =>
                {
                    _waiting = false;
                    Log.LogWarning("[Iniciativa] Sin respuesta: " + error);
                }));
        }

        private void UpdateAutonomy(Player player)
        {
            if (!IsAutonomous || Time.time < _nextInitiativeCheckAt) return;
            _nextInitiativeCheckAt = Time.time + InitiativeCheckInterval;

            // Construyendo apenas se camina: mirar alrededor también cuenta como estar jugando.
            Vector3 look = player.GetLookDir();
            if (Vector3.Distance(player.transform.position, _lastPlayerPosition) > PlayerMovedDistance || Vector3.Angle(look, _lastPlayerLook) > PlayerLookedAngle)
            {
                _lastPlayerPosition = player.transform.position;
                _lastPlayerLook = look;
                _playerActiveAt = Time.time;
            }
            // Si el jugador se ha ido del teclado, no tiene sentido seguir gastando su saldo.
            if (Time.time - _playerActiveAt > PlayerIdleSeconds)
            {
                NoteInitiativeWait("el jugador lleva un rato sin jugar");
                return;
            }
            if (_waiting || player.IsDead() || !CanOpenInput() || _voiceState != VoiceState.Idle) return;

            List<Component> near = CompanionBridge.Nearby(player, null, _settings.TalkRange.Value);
            if (near.Count == 0)
            {
                NoteInitiativeWait("no hay compañeros a " + Mathf.RoundToInt(_settings.TalkRange.Value) + " m");
                return;
            }
            Component target = near[Random.Range(0, near.Count)];
            if (CompanionBridge.InCombat(target) || CompanionBridge.EnemyNear(target, InitiativeDangerRange))
            {
                NoteInitiativeWait("hay peligro cerca");
                return;
            }
            NoteInitiativeWait(null);

            // Si está ocupado solo puede hablar: mejor guardar las peticiones para cuando pueda hacer algo.
            bool canAct = CanActOnOwn(target);
            bool staying = CompanionBridge.DangerFacts(target, player).MustStayClose;
            float interval = _settings.AutonomyInterval.Value * (canAct && !staying ? 1f : BusyChatFactor);
            if (!_initiative.TryTake(Time.time, interval, _settings.AutonomyPerHour.Value)) return;

            string name = CompanionBridge.Name(target);
            string id = CompanionBridge.PermanentId(target);
            List<string> needs = CompanionBridge.UnmetNeeds(target).ConvertAll(need => need.Value);
            InitiativeHistory history = HistoryOf(id);
            InitiativeFacts facts = CompanionBridge.Facts(target, player);
            string context = Initiative.Context(facts);
            List<InitiativeIdea> picked = canAct ? Initiative.Pick(Initiative.Ideas(facts, key => history.WasRecent(key, Time.time), InclinationOf(id)), IdeasPerInitiative, _random) : new List<InitiativeIdea>();
            List<string> ideas = picked.ConvertAll(idea => idea.Text);
            List<string> recent = canAct ? history.Recent(Time.time) : null;
            List<ChatMessage> request = BuildRequest(player, target, id, Initiative.Message(CompanionBridge.DescribeSurroundings(target), needs, canAct, ideas, recent, context));

            _waiting = true;
            Log.LogInfo("[Iniciativa] " + name + (canAct ? " (ideas: " + (ideas.Count == 0 ? "ninguna" : string.Join("; ", ideas.ToArray())) + ")" : " (ocupado, solo puede hablar)") + (context == null ? "" : " " + context));
            CompleteOnOwn(target, name, id, request, Initiative.MemoryLine, canAct, Initiative.ToolsFor(picked, facts.MustStayClose, facts.StationNear));
        }

        /// <summary>Solo cuando cambia, para poder ver en el registro por qué no toma la iniciativa.</summary>
        private void NoteInitiativeWait(string why)
        {
            if (why != null && why != _initiativeWait) Log.LogInfo("[Iniciativa] En espera: " + why);
            _initiativeWait = why;
        }

        private InitiativeHistory HistoryOf(string id)
        {
            if (!_initiativeHistory.TryGetValue(id, out InitiativeHistory history)) _initiativeHistory[id] = history = new InitiativeHistory();
            return history;
        }

        /// <summary>
        /// Sin pasar por la IA, porque no hay nada que decidir y así no gasta saldo. Si lleva un arma a distancia
        /// sin munición, va a por ella, y si tiene hambre sin nada que comer, a por comida. Si no hay, lo dirá como
        /// cualquier otra necesidad, y sin flechas Offline Companions pelea cuerpo a cuerpo.
        /// </summary>
        private void UpdateGearChecks(Player player)
        {
            if (!IsAutonomous || Time.time < _nextGearCheckAt) return;
            _nextGearCheckAt = Time.time + GearCheckInterval;

            foreach (Component companion in CompanionBridge.Nearby(player, null, _settings.TalkRange.Value))
            {
                if (_nextGearTripAt.TryGetValue(companion, out float next) && Time.time < next) continue;
                if (!CanActOnOwn(companion) || CompanionBridge.InCombat(companion) || CompanionBridge.DangerFacts(companion, player).MustStayClose) continue;

                string ammoType = CompanionBridge.MissingAmmoType(companion);
                Dictionary<string, int> items = null;
                Container chest = ammoType == null ? null : CompanionBridge.FindChestWithAmmo(companion, ammoType, GearCheck.AmmoToTake, out items);
                if (chest != null)
                {
                    _nextGearTripAt[companion] = Time.time + GearTripCooldown;
                    _takes[companion] = new TakeTask { Goal = new GiveGoal(Time.time), Chest = chest, Items = items };
                    string text = CompanionBridge.DescribeItems(items);
                    Log.LogInfo("[Equipo] " + CompanionBridge.Name(companion) + ": sin munición, va a por " + text);
                    AddOrderLine(ModLang.Pick(
                        "Por iniciativa propia, " + CompanionBridge.Name(companion) + " va a sacar de un cofre: " + text,
                        "On their own initiative, " + CompanionBridge.Name(companion) + " goes to take from a chest: " + text,
                        "По собственной инициативе " + CompanionBridge.Name(companion) + " идёт взять из сундука: " + text));
                }
                else if (CompanionBridge.HungryWithoutFood(companion) && StartTakingFood(companion, 0) == null)
                {
                    _nextGearTripAt[companion] = Time.time + GearTripCooldown;
                    Log.LogInfo("[Equipo] " + CompanionBridge.Name(companion) + ": tiene hambre y no lleva comida, va a por ella");
                    AddOrderLine(ModLang.Pick(
                        "Por iniciativa propia, " + CompanionBridge.Name(companion) + " va a buscar comida a los cofres.",
                        "On their own initiative, " + CompanionBridge.Name(companion) + " goes to look for food in the chests.",
                        "По собственной инициативе " + CompanionBridge.Name(companion) + " идёт искать еду в сундуках."));
                }
            }
        }

        private void ToggleAutonomy()
        {
            bool enable = _settings.Autonomy.Value != AutonomyMode.Autonomo;
            _settings.Autonomy.Value = enable ? AutonomyMode.Autonomo : AutonomyMode.Normal;
            _initiative.Reset(Time.time);
            _playerActiveAt = Time.time;
            Log.LogInfo("[Iniciativa] Modo " + _settings.Autonomy.Value);
            AddLine(enable
                ? ModLang.Pick(
                    "Modo autónomo: tus compañeros hablarán y harán cosas útiles por su cuenta, hasta " + _settings.AutonomyPerHour.Value + " veces por hora. Cada vez gasta saldo. Vuelve a escribir " + AutonomyCommand + " para desactivarlo.",
                    "Autonomous mode: your companions will talk and do useful tasks on their own, up to " + _settings.AutonomyPerHour.Value + " times per hour. Each time uses AI credits. Type " + AutonomyCommandEn + " again to disable it.",
                    "Автономный режим: спутники будут общаться и делать полезные дела сами до " + _settings.AutonomyPerHour.Value + " раз в час. Каждый раз тратится баланс. Напиши " + AutonomyCommandRu + " ещё раз, чтобы отключить.")
                : ModLang.Pick(
                    "Modo normal: tus compañeros solo harán lo que les pidas.",
                    "Normal mode: your companions will only do what you ask.",
                    "Обычный режим: спутники делают только то, о чём ты просишь."));
        }

        private static bool CanOpenInput()
        {
            return !CharacterEditor.IsOpen
                && !TextInput.IsVisible()
                && !InventoryGui.IsVisible()
                && !Menu.IsVisible()
                && !Minimap.IsOpen()
                && !Console.IsVisible()
                && !StoreGui.IsVisible()
                && !Hud.IsPieceSelectionVisible()
                && (Chat.instance == null || !Chat.instance.HasFocus());
        }

        private void OpenInput(Player player)
        {
            if (_waiting)
            {
                Notify(ModLang.Pick("Tu compañero todavía está pensando...", "Your companion is still thinking...", "Твой спутник всё ещё думает..."));
                return;
            }
            if (Time.time < _nextTalkAt) return;

            // Se abre también sin nadie cerca, porque es cuando más hacen falta /estado y /llamar.
            Component target = CompanionBridge.FindTarget(player, _settings.TalkRange.Value);
            _pendingTarget = target;
            string title = target != null
                ? ModLang.Pick("Hablar con ", "Talk to ", "Поговорить с ") + CompanionBridge.Name(target)
                : ModLang.Pick(
                    "Ningún compañero cerca. Comandos: " + StatusCommand + ", " + RecallCommand + ", " + HelpCommand,
                    "No companion nearby. Commands: " + StatusCommandEn + ", " + RecallCommandEn + ", " + HelpCommandEn,
                    "Нет спутников поблизости. Команды: " + StatusCommandRu + ", " + RecallCommandRu + ", " + HelpCommandRu);
            TextInput.instance.RequestText(this, title, InputLimit);
        }

        private void OpenEditor(Player player)
        {
            Component target = FindTargetOrNotify(player);
            if (target != null) OpenEditorFor(target);
        }

        private void OpenEditorFor(Component target)
        {
            string name = CompanionBridge.Name(target);
            string id = CompanionBridge.PermanentId(target);
            CompanionMemory memory = MemoryOf(id);
            string emptyHint = PersonalityPresets.HasCommonPersonality(_settings.Personality.Value)
                ? ModLang.Pick("Si lo dejas vacío, usa la personalidad común.", "If left empty, uses the shared personality.", "Если оставить пустым, используется общая личность.")
                : ModLang.Pick("Si lo dejas vacío, usa su perfil: ", "If left empty, uses their preset: ", "Если оставить пустым, используется шаблон: ") + PersonalityPresets.For(id).Name + ".";
            CharacterProfile before = LoadProfile(id);
            _editor.Open(target, name, before, memory.Texts(), emptyHint, (edited, profileChanged, memories, memoriesChanged, forgetAll) =>
            {
                edited.Name = name;
                try
                {
                    if (profileChanged) _characters.Save(id, edited);
                    if (forgetAll)
                    {
                        memory.Forget();
                    }
                    else
                    {
                        // Una conversación reciente con otro personaje confundiría a la IA; los recuerdos los ve y corrige el jugador.
                        if (!edited.SameTextAs(before)) memory.ClearRecent();
                        if (memoriesChanged) memory.ApplyEdit(memories);
                    }
                    if (_settings.MemoryEnabled.Value) _memoryFiles.Save(id, memory);
                }
                catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
                {
                    Log.LogError("[Personaje] No se pudo guardar: " + ex.Message);
                    Notify(ModLang.Pick("No se pudo guardar. Revisa el registro.", "Could not save. Check the log.", "Не удалось сохранить. Проверь журнал."));
                    return;
                }
                Log.LogInfo("[Personaje] Guardado " + name + ": ficha=" + profileChanged + ", recuerdos=" + memoriesChanged + ", olvidar=" + forgetAll);
                Notify(ModLang.Pick("Personaje de " + name + " guardado.", name + "'s character saved.", "Персонаж " + name + " сохранён."));
            });
        }

        private Component FindTargetOrNotify(Player player)
        {
            Component target = CompanionBridge.FindTarget(player, _settings.TalkRange.Value);
            if (target == null) Notify(ModLang.Pick("No hay ningún compañero tuyo cerca.", "None of your companions are nearby.", "Поблизости нет твоих спутников."));
            return target;
        }

        private Inclination InclinationOf(string id)
        {
            return string.IsNullOrEmpty(id) ? Inclination.Ninguna : Inclinations.Of(LoadProfile(id), _settings.Personality.Value, id);
        }

        private CharacterProfile LoadProfile(string id)
        {
            CharacterProfile profile = _characters.Load(id, out string problem);
            if (problem != null)
            {
                Log.LogWarning("[Personaje] " + problem);
                Notify(ModLang.Pick("La ficha de este compañero estaba dañada. Revisa el registro.", "This companion's profile was corrupted. Check the log.", "Файл персонажа этого спутника был повреждён. Проверь журнал."));
            }
            return profile;
        }

        private CompanionMemory MemoryOf(string id)
        {
            if (_memories.TryGetValue(id, out CompanionMemory memory)) return memory;

            memory = new CompanionMemory();
            if (_settings.MemoryEnabled.Value)
            {
                memory = _memoryFiles.Load(id, out string problem);
                if (problem != null)
                {
                    Log.LogWarning("[Memoria] " + problem);
                    Notify(ModLang.Pick("La memoria de este compañero estaba dañada. Revisa el registro.", "This companion's memory was corrupted. Check the log.", "Память этого спутника была повреждена. Проверь журнал."));
                }
            }
            _memories[id] = memory;
            return memory;
        }

        /// <summary>Anota un suceso real. Sin compañero concreto, lo viven los que estén cerca.</summary>
        private void RememberEvent(Component companion, int weight, string text, bool once)
        {
            Player player = Player.m_localPlayer;
            if (companion == null && player == null) return;
            int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
            List<Component> witnesses = companion != null ? new List<Component> { companion } : CompanionBridge.Nearby(player, null, _settings.TalkRange.Value);
            foreach (Component witness in witnesses)
            {
                string id = CompanionBridge.PermanentId(witness);
                if (string.IsNullOrEmpty(id)) continue;
                CompanionMemory memory = MemoryOf(id);
                if (!memory.AddEvent(day, weight, text, once)) continue;
                SaveMemory(id, memory);
                Log.LogInfo("[Memoria] " + CompanionBridge.Name(witness) + " recordará: " + text);
            }
        }

        private void SaveMemory(string id, CompanionMemory memory)
        {
            if (!_settings.MemoryEnabled.Value) return;
            try
            {
                _memoryFiles.Save(id, memory);
            }
            catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
            {
                Log.LogWarning("[Memoria] No se pudo guardar: " + ex.Message);
            }
        }

        public string GetText()
        {
            return "";
        }

        public void SetText(string text)
        {
            Component target = _pendingTarget;
            _pendingTarget = null;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text) || Player.m_localPlayer == null) return;

            if (string.Equals(text, "/help", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "/ayuda", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "/помощь", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                return;
            }
            if (RunCommand(text, target)) return;

            bool shouted = Recipients.TryParseShout(text, out string shout);
            float hearing = HearingRange(shouted);
            if (shouted)
            {
                text = shout;
                target = CompanionBridge.FindTarget(Player.m_localPlayer, hearing);
            }
            if (target == null)
            {
                Notify(OutOfEarshot(Player.m_localPlayer, shouted) ?? ModLang.Pick("No hay ningún compañero tuyo cerca.", "None of your companions are nearby.", "Рядом нет ни одного твоего спутника."));
                return;
            }
            Send(Player.m_localPlayer, target, text, hearing: hearing);
        }

        private float HearingRange(bool shouted)
        {
            return shouted ? Mathf.Max(_settings.ShoutRange.Value, _settings.TalkRange.Value) : _settings.TalkRange.Value;
        }

        /// <summary>Por qué nadie ha oído al jugador, para que no parezca que el mensaje se ha perdido. Null si no hay nadie a quien nombrar.</summary>
        private string OutOfEarshot(Player player, bool shouted)
        {
            Component nearest = CompanionBridge.FindTarget(player, float.MaxValue);
            if (nearest == null) return null;
            int dist = Mathf.RoundToInt(Vector3.Distance(player.transform.position, nearest.transform.position));
            string name = CompanionBridge.Name(nearest);
            if (ModLang.IsEnglish)
            {
                return shouted
                    ? name + " is " + dist + " m away and cannot hear you even shouting. To call them all, use " + RecallCommandEn + "."
                    : name + " is " + dist + " m away and cannot hear you. To reach farther, shout: /s message";
            }
            if (ModLang.IsRussian)
            {
                string whereRu = name + " находится в " + dist + " м";
                return shouted
                    ? whereRu + " и не слышит тебя даже с криком. Чтобы позвать всех, используй " + RecallCommandRu + "."
                    : whereRu + " и не слышит тебя. Чтобы крикнуть дальше: /s сообщение";
            }
            string where = name + " está a " + dist + " m";
            return shouted
                ? where + " y no te oye ni gritando. Para llamarlos a todos, usa " + RecallCommand + "."
                : where + " y no te oye. Para llegar más lejos, grita: /s mensaje";
        }

        private static bool IsCommand(string text)
        {
            return System.Array.Exists(Commands, command => string.Equals(text, command, System.StringComparison.OrdinalIgnoreCase))
                || HasArgument(text, MarkCommand) || HasArgument(text, MarkCommandEn) || HasArgument(text, MarkCommandRu)
                || HasArgument(text, ForgetCommand) || HasArgument(text, ForgetCommandEn) || HasArgument(text, ForgetCommandRu)
                || HasArgument(text, LanguageCommand) || HasArgument(text, LanguageCommandEn) || HasArgument(text, LangCommandEn) || HasArgument(text, LanguageCommandRu);
        }

        private static bool HasArgument(string text, string command)
        {
            return text.StartsWith(command + " ", System.StringComparison.OrdinalIgnoreCase);
        }

        private void ChangeLanguageFromCommand(string arg)
        {
            ModLanguage? parsed = ModLang.Parse(arg);
            ModLanguage next;
            if (parsed.HasValue)
            {
                next = parsed.Value;
            }
            else
            {
                ModLanguage eff = _settings.Language.Value == ModLanguage.Auto ? ModLang.Effective : _settings.Language.Value;
                switch (eff)
                {
                    case ModLanguage.English: next = ModLanguage.Spanish; break;
                    case ModLanguage.Spanish: next = ModLanguage.Russian; break;
                    default: next = ModLanguage.English; break;
                }
            }
            _settings.Language.Value = next;
            AddLine(ModLang.Pick(
                "Idioma del mod cambiado a: " + ModLang.DisplayName(next) + " (" + next + "). Usa /idioma [en|es|ru|auto] para cambiarlo.",
                "Mod language set to: " + ModLang.DisplayName(next) + " (" + next + "). Use /language [en|es|ru|auto] to change it.",
                "Язык мода изменён на: " + ModLang.DisplayName(next) + " (" + next + "). Введи /язык [en|es|ru|auto] для смены."));
        }

        /// <summary>Estos comandos no pasan por la IA, así que no gastan saldo.</summary>
        private bool RunCommand(string text, Component target)
        {
            if (!IsCommand(text)) return false;
            string command = text.ToLowerInvariant();
            if (command.StartsWith(MarkCommand + " ")) AddLine(MarkFromCommand(text.Substring(MarkCommand.Length).Trim()));
            else if (command.StartsWith(MarkCommandEn + " ")) AddLine(MarkFromCommand(text.Substring(MarkCommandEn.Length).Trim()));
            else if (command.StartsWith(MarkCommandRu + " ")) AddLine(MarkFromCommand(text.Substring(MarkCommandRu.Length).Trim()));
            else if (command == MarkCommand || command == MarkCommandEn || command == MarkCommandRu) AddLine(MarkFromCommand(""));
            else if (command.StartsWith(ForgetCommand + " ")) AddLine(ForgetFromCommand(text.Substring(ForgetCommand.Length).Trim()));
            else if (command.StartsWith(ForgetCommandEn + " ")) AddLine(ForgetFromCommand(text.Substring(ForgetCommandEn.Length).Trim()));
            else if (command.StartsWith(ForgetCommandRu + " ")) AddLine(ForgetFromCommand(text.Substring(ForgetCommandRu.Length).Trim()));
            else if (command == ForgetCommand || command == ForgetCommandEn || command == ForgetCommandRu) AddLine(ForgetFromCommand(""));
            else if (command == LanguageCommand || command == LanguageCommandEn || command == LangCommandEn || command == LanguageCommandRu) ChangeLanguageFromCommand("");
            else if (command.StartsWith(LanguageCommand + " ")) ChangeLanguageFromCommand(text.Substring(LanguageCommand.Length).Trim());
            else if (command.StartsWith(LanguageCommandEn + " ")) ChangeLanguageFromCommand(text.Substring(LanguageCommandEn.Length).Trim());
            else if (command.StartsWith(LangCommandEn + " ")) ChangeLanguageFromCommand(text.Substring(LangCommandEn.Length).Trim());
            else if (command.StartsWith(LanguageCommandRu + " ")) ChangeLanguageFromCommand(text.Substring(LanguageCommandRu.Length).Trim());
            else if (command == PlacesCommand || command == PlacesCommandEn || command == PlacesCommandRu) AddLine(ListPlaces(Player.m_localPlayer));
            else if (command == RecallCommand || command == RecallCommandEn || command == CallCommandEn || command == RecallCommandRu) AddLine(RecallEveryone());
            else if (command == HelpCommand || command == HelpCommandEn || command == HelpCommandRu) ShowHelp();
            else if (command == StatusCommand || command == StatusCommandEn || command == StatusCommandRu) ShowStatus();
            else if (command == BalanceCommand || command == BalanceCommandEn || command == BalanceCommandRu) ShowBalance();
            else if (command == AutonomyCommand || command == AutonomyCommandAccented || command == AutonomyCommandEn || command == AutonomyCommandRu) ToggleAutonomy();
            else if (target == null) Notify(ModLang.Pick("No hay ningún compañero tuyo cerca.", "None of your companions are nearby.", "Рядом нет ни одного твоего спутника."));
            else OpenEditorFor(target);
            return true;
        }

        /// <summary>
        /// Se llama mientras el juego procesa el chat. Solo se anota, para atenderlo con el chat ya cerrado.
        /// Lo que no es un comando sigue su curso, y se ve en el chat como cualquier mensaje.
        /// </summary>
        private bool OnGameChat(string text)
        {
            text = text?.Trim();
            if (!_settings.TalkInGameChat.Value || string.IsNullOrEmpty(text)) return false;
            bool command = IsCommand(text);
            // El grito sigue su curso en el juego, para que también lo oigan los demás jugadores.
            string shouted = null;
            _chatShouted = !command && Recipients.TryParseShout(text, out shouted);
            if (_chatShouted) _chatMessage = shouted;
            else if (command || text[0] != '/') _chatMessage = text;
            return command;
        }

        private void UpdateGameChat(Player player)
        {
            if (_chatMessage == null) return;
            string text = _chatMessage;
            bool shouted = _chatShouted;
            _chatMessage = null;

            float hearing = HearingRange(shouted);
            Component target = CompanionBridge.FindTarget(player, hearing);
            if ((!shouted && RunCommand(text, target)) || player.IsDead()) return;

            bool othersOnline = ZNet.instance != null && ZNet.instance.GetNrOfPlayers() > 1;
            var names = new List<string>();
            foreach (Component companion in CompanionBridge.MyCompanions()) names.Add(CompanionBridge.Name(companion));
            if (!Recipients.GameChatIsForCompanions(text, names, othersOnline))
            {
                Log.LogInfo("[Chat] Hay otros jugadores y el mensaje no nombra a ningún compañero: no se envía a la IA.");
                return;
            }
            if (target == null)
            {
                string reason = OutOfEarshot(player, shouted);
                if (reason != null) Notify(reason);
                return;
            }
            if (_waiting) Notify(ModLang.Pick("Tu compañero todavía está pensando...", "Your companion is still thinking...", "Твой спутник всё ещё думает..."));
            else if (Time.time < _nextTalkAt) Notify(ModLang.Pick("Espera un momento antes de volver a hablarle.", "Wait a moment before talking to them again.", "Подожди немного, прежде чем снова заговорить."));
            else Send(player, target, text, echo: false, hearing: hearing);
        }

        private List<ChatMessage> BuildRequest(Player player, Component target, string id, string message, LookTarget look = null, bool playerMessage = false, float reach = OrderRange)
        {
            CompanionMemory memory = MemoryOf(id);
            string system = PromptBuilder.Build(
                LoadProfile(id),
                PersonalityPresets.DefaultFor(_settings.Personality.Value, id),
                ModLang.EffectiveReplyLanguage(_settings.ReplyLanguage.Value),
                GameContext.Describe(player, target, look ?? LookTarget.Capture(player, CompanionBridge.CompanionType)) + PartySummary(player, target, reach) + TaskSummary(target) + PlacesSummary(target),
                memory.LongTerm(),
                _settings.AllowEmotes.Value && WhyNoEmote(target) == null,
                IsAutonomous);

            var request = new List<ChatMessage> { new ChatMessage("system", system) };
            request.AddRange(memory.ToMessages(_settings.HistoryMessages.Value));
            // El historial solo guarda frases, no acciones, y el modelo tiende a imitar eso y contestar sin actuar.
            // Solo con herramientas disponibles: un comentario automático no las tiene.
            if (playerMessage && _settings.AllowActions.Value)
            {
                request.Add(new ChatMessage("system", ModLang.Pick(
                    "Recordatorio: las órdenes de mensajes anteriores ya se atendieron; no las repitas. Atiende solo el siguiente mensaje: si es una orden, usa la herramienta de esa orden, porque tus frases no hacen nada en el juego. Si no es una orden, responde sin herramientas.",
                    "Reminder: orders from previous messages have already been handled; do not repeat them. Handle only the next message: if it is an order, call the tool for that order, because your words alone do nothing in the game. If it is not an order, reply without tools.",
                    "Напоминание: приказы из предыдущих сообщений уже выполнены, не повторяй их. Реагируй только на следующее сообщение: если это приказ, вызови соответствующий инструмент, так как одни лишь слова не выполняют действий в игре. Если это не приказ, ответь без инструментов.")));
            }
            request.Add(new ChatMessage("user", message));
            return request;
        }

        private void Send(Player player, Component target, string text, bool echo = true, float hearing = 0f)
        {
            // Si el mensaje nombra a otro compañero cercano, responde ese y no el que estaba más a mano.
            float reach = Mathf.Max(OrderRange, hearing);
            List<Component> nearby = CompanionBridge.Nearby(player, target, reach);
            int addressed = Recipients.FindAddressed(text, nearby.ConvertAll(c => CompanionBridge.Name(c)));
            if (addressed > 0) target = nearby[addressed];

            string name = CompanionBridge.Name(target);
            string id = CompanionBridge.PermanentId(target);
            LookTarget look = LookTarget.Capture(player, CompanionBridge.CompanionType);
            List<ChatMessage> request = BuildRequest(player, target, id, text, look, playerMessage: true, reach: reach);
            var state = new MessageActions { OrderRange = reach, PlayerText = text };

            _waiting = true;
            _nextTalkAt = Time.time + _settings.Cooldown.Value;
            if (echo) AddToChat(player.GetPlayerName(), text);
            Notify(name + ModLang.Pick(" está pensando...", " is thinking...", " думает..."));

            JArray tools = _settings.AllowActions.Value ? CompanionActions.ToolDefinitions() : null;
            StartCoroutine(_client.Complete(
                request,
                tools,
                response =>
                {
                    if (response.ToolCalls.Count > 0)
                    {
                        RunActions(target, look, name, id, request, text, response, state);
                        return;
                    }
                    ApplyClearOrder(target, look, name, text, state);
                    Finish(target, name, id, text, response.Text);
                },
                error =>
                {
                    _waiting = false;
                    Notify(error);
                    // Sin saldo o sin conexión, "ven", "para" y "quédate" siguen funcionando.
                    ApplyClearOrder(target, look, name, text, state);
                }));
        }

        /// <summary>Si el modelo contestó sin actuar a una orden inequívoca como "sígueme", o no contestó, la cumple igualmente.</summary>
        private void ApplyClearOrder(Component target, LookTarget look, string name, string text, MessageActions state)
        {
            if (!_settings.AllowActions.Value || target == null || Player.m_localPlayer == null) return;
            List<string> names = CompanionBridge.Nearby(Player.m_localPlayer, target, state.OrderRange).ConvertAll(c => CompanionBridge.Name(c));
            ToolCall call = OrderIntent.Detect(text, names);
            if (call == null) return;
            Log.LogInfo("[Acciones] " + name + ": el modelo no actuó o no respondió; se aplica " + call.Name + " directamente");
            ApplyCall(target, look, name, call, state);
        }

        /// <summary>Lo acumulado durante un mismo mensaje del jugador, que puede ocupar varias rondas con la IA.</summary>
        private sealed class MessageActions
        {
            public int Round;
            public int Executed;
            public bool Initiative;
            public float OrderRange = Plugin.OrderRange;
            public string PlayerText;
            public JArray OnOwnTools;
            public readonly List<string> Done = new List<string>();
            public readonly HashSet<Component> TaskStarted = new HashSet<Component>();
            public readonly HashSet<Component> OpenEndedJob = new HashSet<Component>();
        }

        private void RunActions(Component target, LookTarget look, string name, string id, List<ChatMessage> request, string question, LlmResponse response, MessageActions state = null)
        {
            state = state ?? new MessageActions();
            state.Round++;
            var followUp = new List<ChatMessage>(request) { new ChatMessage("assistant", response.Text, response.RawToolCalls) };

            foreach (ToolCall call in response.ToolCalls)
            {
                string result;
                if (state.Executed >= MaxActionsPerMessage)
                {
                    result = ModLang.Pick("No aplicada: demasiadas órdenes a la vez.", "Not applied: too many orders at once.", "Не принято: слишком много приказов одновременно.");
                }
                else
                {
                    state.Executed++;
                    result = ApplyCall(target, look, name, call, state);
                }
                followUp.Add(new ChatMessage("tool", result, null, call.Id));
            }
            followUp.Add(new ChatMessage("system", state.Initiative ? OfflineCompanionsAI.Initiative.AfterActing : PromptBuilder.AfterOrders));

            // Algunos modelos piden las órdenes de una en una: si quedan rondas, se le deja seguir pidiendo.
            JArray tools = state.Round >= MaxToolRounds ? null : state.Initiative ? state.OnOwnTools : CompanionActions.ToolDefinitions();

            // La orden ya se aplicó, así que un fallo de esta petición no debe parecer que no se hizo.
            string fallback = response.Text ?? (state.Initiative ? "" : state.Done.Count > 0 ? ModLang.Pick("Entendido.", "Understood.", "Понятно.") : ModLang.Pick("No he podido hacer eso.", "I couldn't do that.", "Не удалось это сделать."));
            StartCoroutine(_client.Complete(
                followUp,
                tools,
                next =>
                {
                    if (next.ToolCalls.Count > 0 && tools != null) RunActions(target, look, name, id, followUp, question, next, state);
                    else Finish(target, name, id, question, next.Text ?? fallback);
                },
                error =>
                {
                    Log.LogWarning("[IA] Sin respuesta tras la acción: " + error);
                    Finish(target, name, id, question, fallback);
                }));
        }

        private string ApplyCall(Component speaker, LookTarget look, string speakerName, ToolCall call, MessageActions state)
        {
            ActionRequest action = CompanionActions.Parse(call.Name, call.Arguments, out string invalid);
            if (action == null)
            {
                Log.LogWarning("[Acciones] " + speakerName + ": " + call.Name + " rechazada: " + invalid);
                return "Error: " + invalid;
            }

            string mixup = CompanionActions.FurnaceMixup(action, state.PlayerText);
            if (mixup != null)
            {
                Log.LogInfo("[Acciones] " + speakerName + ": " + call.Name + " rechazada: pedían los hornos");
                return "Error: " + mixup;
            }

            if (state.Initiative)
            {
                string refusal = OfflineCompanionsAI.Initiative.Refusal(call.Name, action);
                if (refusal != null)
                {
                    Log.LogInfo("[Iniciativa] " + speakerName + ": " + call.Name + " rechazada: " + refusal);
                    return "Error: " + refusal;
                }
                action.Who = null;
            }

            Player player = Player.m_localPlayer;
            if (player == null || speaker == null) return ModLang.Pick("Error: El compañero ya no está disponible.", "Error: The companion is no longer available.", "Ошибка: спутник больше недоступен.");

            if (state.Initiative)
            {
                string key = OfflineCompanionsAI.Initiative.VarietyKey(call.Name, action);
                if (key != null)
                {
                    InitiativeHistory history = HistoryOf(CompanionBridge.PermanentId(speaker));
                    if (history.WasRecent(key, Time.time))
                    {
                        Log.LogInfo("[Iniciativa] " + speakerName + ": " + call.Name + " rechazada: ya la intentó hace poco");
                        return "Error: " + InitiativeHistory.RepeatRefusal;
                    }
                    // Cuenta aunque luego falle: las frambuesas que no se pueden coger no deben intentarse otra vez enseguida.
                    history.Add(key, action.Description, Time.time);
                }
            }

            List<Component> nearby = CompanionBridge.Nearby(player, speaker, state.OrderRange);
            List<string> names = nearby.ConvertAll(c => CompanionBridge.Name(c));
            List<int> chosen = Recipients.Resolve(CompanionActions.IsWorldWide(action.Kind) ? null : action.Who, names, out string unknown);
            if (unknown != null) return ModLang.Pick("Error: No hay ningún compañero llamado " + unknown + " cerca.", "Error: No companion named " + unknown + " is nearby.", "Ошибка: поблизости нет спутника по имени " + unknown + ".");
            if (chosen.Count == 0) return ModLang.Pick("Error: No hay compañeros cerca.", "Error: No companions nearby.", "Ошибка: поблизости нет спутников.");

            List<Component> group = chosen.ConvertAll(i => nearby[i]);

            // Usar lo que se mira se aplica de una vez al grupo, porque la tecla de mando ya reparte asientos y objetos.
            if (action.Kind == ActionKind.UseLookedAt)
            {
                string shared = ApplyToCompanion(group[0], group, look, action, state);
                if (!shared.StartsWith("Error:")) state.Done.Add(action.Description);
                return ModLang.Pick("Para " + group.Count + " compañero(s): " + shared, "For " + group.Count + " companion(s): " + shared, "Для " + group.Count + " спутн.: " + shared);
            }

            var results = new List<string>();
            bool anyAccepted = false;
            foreach (Component companion in group)
            {
                string outcome = ApplyToCompanion(companion, null, look, action, state);
                anyAccepted |= !outcome.StartsWith("Error:");
                results.Add(group.Count > 1 ? CompanionBridge.Name(companion) + ": " + outcome : outcome);
            }
            if (anyAccepted) state.Done.Add(action.Description);
            return string.Join(" ", results.ToArray());
        }

        private string ApplyToCompanion(Component companion, IList<Component> group, LookTarget look, ActionRequest action, MessageActions state)
        {
            string name = CompanionBridge.Name(companion);
            string failure;
            string note = null;

            // Mientras hace un gesto se queda quieto: una orden nueva debe poder moverlo enseguida.
            if (_emotes.ContainsKey(companion)) EndEmote(companion);

            if (!CompanionActions.IsExclusiveTask(action.Kind))
            {
                // Un cambio de trabajo manual sustituye a una meta de recolección en curso.
                if (action.Kind == ActionKind.SetJob)
                {
                    ClearGoals(companion);
                    if (action.Value != 0) state.OpenEndedJob.Add(companion);
                }
                // Dejar de proteger una zona si le mandan seguir al jugador o trabajar.
                if (action.Kind == ActionKind.SetJob || (action.Kind == ActionKind.SetFollow && action.Value != 0)) _guards.Remove(companion);
                // Otro trabajo, "sígueme" o "quédate aquí" sustituyen a lo que se le encargó en ese sitio.
                if (action.Kind == ActionKind.SetJob || action.Kind == ActionKind.SetFollow)
                {
                    _explores.Remove(companion);
                    _huntSweeps.Remove(companion);
                    bool wasTending = _tends.ContainsKey(companion);
                    LeaveStay(companion);
                    if (wasTending && action.Kind == ActionKind.SetFollow) CompanionBridge.WorkInPlace(companion, 0);
                }
                if (action.Kind == ActionKind.SetRetreat)
                {
                    if (action.Value == 0) _retreats.Remove(companion);
                    else _retreats[companion] = action.Value;
                    failure = null;
                }
                else if (action.Kind == ActionKind.MarkPlace)
                {
                    failure = MarkOrdered(look, action);
                }
                else if (action.Kind == ActionKind.ForgetPlace)
                {
                    failure = ForgetPlace(action.Item);
                }
                else if (action.Kind == ActionKind.CheckChests)
                {
                    note = CompanionBridge.DescribeChests(companion);
                    failure = null;
                }
                else if (action.Kind == ActionKind.LookAround)
                {
                    note = CompanionBridge.DescribeSurroundings(companion);
                    failure = null;
                }
                else if (action.Kind == ActionKind.CompanionsStatus)
                {
                    List<string> roster = CompanionBridge.RosterLines(StayOf);
                    note = roster.Count == 0 ? ModLang.Pick("No se pudo consultar la lista de compañeros.", "Could not check the companion roster.", "Не удалось проверить список спутников.") : string.Join("; ", roster.ToArray()) + ".";
                    failure = null;
                }
                else if (action.Kind == ActionKind.CheckSkills)
                {
                    note = CompanionBridge.DescribeSkills(companion);
                    failure = null;
                }
                else if (action.Kind == ActionKind.RecallAll)
                {
                    note = RecallEveryone();
                    failure = null;
                }
                else if (action.Kind == ActionKind.Remember)
                {
                    string memoryId = CompanionBridge.PermanentId(companion);
                    CompanionMemory memory = MemoryOf(memoryId);
                    failure = memory.RememberFact(action.Item) ? null : ModLang.Pick("No cabe más en sus recuerdos, o ya lo recordaba.", "No more room in their memory, or they already remembered that.", "В памяти нет места или спутник уже помнит это.");
                    if (failure == null) SaveMemory(memoryId, memory);
                }
                else
                {
                    failure = CompanionBridge.Execute(companion, group, Player.m_localPlayer, look, action, out note);
                    if (failure == null) BeginHuntSweep(companion, action);
                }
            }
            else if (state.OpenEndedJob.Contains(companion) && !CompanionActions.ClearsQueue(action.Kind))
            {
                // Un trabajo sin cantidad nunca termina, así que la tarea se haría antes y no después.
                return ModLang.Pick(
                    "Error: Esta tarea no se ha iniciado porque el trabajo pedido antes no termina nunca. Para hacerlas en ese orden, usa gather con una cantidad y después esta tarea.",
                    "Error: This task was not started because the open-ended job requested earlier never finishes on its own. To run them in sequence, use gather with an amount first and then this task.",
                    "Ошибка: задача не начата, так как предыдущая бессрочная работа никогда не завершается сама. Чтобы выполнить их по очереди, сначала укажи количество для сбора, а затем эту задачу.");
            }
            else if (CompanionActions.ClearsQueue(action.Kind) || state.TaskStarted.Add(companion))
            {
                // La primera tarea de un mensaje nuevo sustituye a lo que tuviera pendiente de antes.
                state.TaskStarted.Add(companion);
                ChainOf(companion).Clear();
                // Sin esto, al acabar la tarea nueva seguiría con el trabajo del encargo anterior, ya sin límite.
                // "Ven" y "para" ya se lo quitan, y "para" no debe hacerle volver con el jugador.
                bool leavesJob = (HoldsJob(companion) || _tends.ContainsKey(companion)) && !CompanionActions.ClearsQueue(action.Kind);
                ClearGoals(companion);
                if (leavesJob) CompanionBridge.StopGathering(companion);
                failure = StartTask(companion, group, look, action, out note);
                if (failure == null) ChainOf(companion).StepStarted(Time.time);
            }
            else
            {
                OrderSequence<QueuedOrder> chain = ChainOf(companion);
                // Si la IA pidió esta tarea en otra ronda, la cadena pudo cerrarse: la anterior cuenta desde ahora.
                if (chain.Pending == 0) chain.StepStarted(Time.time);
                bool queued = chain.Enqueue(new QueuedOrder { Action = action, Look = look, Group = group });
                Log.LogInfo("[Cadena] " + name + ": " + action.Kind + (queued ? " en cola" : " rechazada: cola llena"));
                if (queued) AddOrderLine(ModLang.Pick("En cola para " + name + ": " + action.Description, "Queued for " + name + ": " + action.Description, "В очереди для " + name + ": " + action.Description));
                return queued
                    ? ModLang.Pick("Orden en cola: " + action.Description + ".", "Order queued: " + action.Description + ".", "Приказ добавлен в очередь: " + action.Description + ".")
                    : ModLang.Pick("Error: Tiene demasiadas tareas pendientes.", "Error: Has too many pending tasks.", "Ошибка: слишком много задач в очереди.");
            }

            if (failure != null)
            {
                Log.LogWarning("[Acciones] " + name + ": " + action.Kind + " rechazada: " + failure);
                AddOrderLine(ModLang.Pick("Orden rechazada para " + name + ": " + failure, "Order rejected for " + name + ": " + failure, "Приказ отклонён для " + name + ": " + failure));
                return "Error: " + failure;
            }
            Log.LogInfo("[Acciones] " + name + ": " + action.Kind + " = " + action.Value + (action.Amount > 0 ? ", cantidad " + action.Amount : "") + (note == null ? "" : " (" + note + ")"));
            bool forReply = CompanionActions.ReplyNeedsNote(action.Kind);
            AddOrderLine((state.Initiative
                ? ModLang.Pick("Por iniciativa propia, " + name + " va a ", "On their own initiative, " + name + " goes to ", "По собственной инициативе " + name + " собирается ")
                : ModLang.Pick("Orden para " + name + ": ", "Order for " + name + ": ", "Приказ для " + name + ": "))
                + action.Description + (note == null || forReply ? "" : ". " + note));
            return ModLang.Pick("Orden aceptada: ", "Order accepted: ", "Приказ принят: ") + action.Description + "." + (note == null || !forReply ? "" : " " + note);
        }

        private string StartTask(Component companion, IList<Component> group, LookTarget look, ActionRequest action, out string note)
        {
            foreach (Component worker in group ?? new[] { companion }) RememberWorker(worker);

            // Se mide antes de empezar, para contar solo lo que consiga a partir de ahora.
            int packBefore = action.Kind == ActionKind.Gather ? CompanionBridge.CountInPack(companion, action.Item) : 0;
            int storeBefore = action.Kind == ActionKind.StoreItems ? StorePlanner.Units(CompanionBridge.StorableStacks(companion)) : 0;
            if (action.Kind == ActionKind.GiveItems)
            {
                note = null;
                return StartGiving(companion, action);
            }
            if (action.Kind == ActionKind.DropItems) return DropItems(companion, action, out note);
            if (action.Kind == ActionKind.TakeFromChest)
            {
                note = null;
                return StartTaking(companion, action);
            }
            if (action.Kind == ActionKind.GoToPlace) return StartTravel(companion, action, out note);
            if (action.Kind == ActionKind.Explore) return StartExploring(companion, out note);
            if (action.Kind == ActionKind.Guard) return StartGuard(companion, look, action, out note);
            if (action.Kind == ActionKind.Cook) return StartCooking(companion, action, out note);
            if (action.Kind == ActionKind.OrganizeChests) return StartUpkeepTask(companion, CompanionActions.SortPlan, action.Description, out note);
            if (action.Kind == ActionKind.Maintain) return StartUpkeepTask(companion, action.Item, action.Description, out note);
            if (action.Kind == ActionKind.SmeltItem) return StartSmelting(companion, action, out note);
            if (action.Kind == ActionKind.PickUpItem)
            {
                note = null;
                return StartPickup(companion, look, action);
            }

            GameObject tree = action.Kind == ActionKind.UseLookedAt ? CompanionBridge.TreeLookedAt(look) : null;
            Destructible plant = action.Kind == ActionKind.UseLookedAt ? CompanionBridge.SmallPlantLookedAt(look) : null;

            if (tree != null || plant != null || action.Kind == ActionKind.Gather)
            {
                foreach (Component worker in group ?? new[] { companion }) _taskStartPack[worker] = CompanionBridge.PackContents(worker);
            }
            if (plant != null) return StartPlantTask(companion, group, look, plant, out note);

            Character prey = null;
            if (action.Kind == ActionKind.Attack) prey = CompanionBridge.AttackTarget(companion, Player.m_localPlayer, look, action.Value);
            else if (action.Kind == ActionKind.UseLookedAt) prey = look?.Enemy;

            string failure = CompanionBridge.Execute(companion, group, Player.m_localPlayer, look, action, out note);
            if (failure != null) return failure;

            if (action.Kind == ActionKind.StoreItems && CompanionBridge.IsDepositing(companion))
            {
                var store = new StoreGoal(storeBefore, Time.time);
                store.TripStarted(CompanionBridge.DepositChest(companion));
                _stores[companion] = store;
                Log.LogInfo("[Guardar] " + CompanionBridge.Name(companion) + ": empieza con " + store.StartUnits + " por guardar; va al cofre en " + CompanionBridge.DepositChestPosition(companion));
            }
            if (prey != null)
            {
                foreach (Component worker in group ?? new[] { companion }) note = WatchHunt(worker, prey) ?? note;
            }
            if (action.Kind == ActionKind.Gather)
            {
                _goals[companion] = new GatherGoal(action.Item, action.ItemLabel, action.Amount, packBefore, Time.time);
                BeginHuntSweep(companion, action);
            }
            if (tree != null)
            {
                foreach (Component worker in group ?? new[] { companion })
                {
                    CompanionBridge.WorkHere(worker);
                    _trees[worker] = new TreeTask { Goal = new TreeGoal(Time.time), Tree = tree, Position = tree.transform.position };
                }
            }
            return null;
        }

        /// <summary>Reborn mira a su alrededor desde un punto y, si no ve presas, se queda quieto. Con esto, por orden de la IA, sale a buscarlas.</summary>
        private sealed class HuntSweep
        {
            public readonly SearchPlan Plan = new SearchPlan(HuntSweepCell);
            public float NextAt;
            public float IdleSince = -1f;

            /// <summary>No queda nada sin mirar a su alcance. Si el jugador se mueve, puede volver a haberlo.</summary>
            public bool Exhausted;
        }

        private void BeginHuntSweep(Component companion, ActionRequest action)
        {
            bool hunting = (action.Kind == ActionKind.Gather || action.Kind == ActionKind.SetJob) && action.Value == CompanionActions.JobHunt;
            if (hunting && !action.KeepFollowing && CompanionBridge.HuntSearchAvailable) _huntSweeps[companion] = new HuntSweep();
        }

        /// <summary>Lo que no sea cazar sigue igual. Solo se mueve si está parado sin presa, y dentro de la zona que el juego mantiene cargada.</summary>
        private void UpdateHuntSweeps(Player player)
        {
            if (_huntSweeps.Count == 0) return;
            float reach = CompanionBridge.LoadedReach();
            foreach (Component companion in new List<Component>(_huntSweeps.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f || !CompanionBridge.OnJob(companion, CompanionActions.JobHunt) || CompanionBridge.IsFollowing(companion))
                {
                    _huntSweeps.Remove(companion);
                    continue;
                }

                HuntSweep sweep = _huntSweeps[companion];
                if (Time.time < sweep.NextAt) continue;
                sweep.NextAt = Time.time + HuntSweepCheckSeconds;

                Vector3 here = companion.transform.position;
                sweep.Plan.MarkSeen(here.x, here.z);
                if (!CompanionBridge.IsHuntIdle(companion) || CompanionBridge.InCombat(companion))
                {
                    sweep.IdleSince = -1f;
                    continue;
                }
                // Antes de irse, un momento para que mire desde donde ha llegado.
                if (sweep.IdleSince < 0f) sweep.IdleSince = Time.time;
                if (Time.time - sweep.IdleSince < HuntSweepWaitSeconds) continue;
                if (FlatDistance(here, player.transform.position) >= reach) continue;

                Vector3 at = player.transform.position;
                bool found = sweep.Plan.Next(here.x, here.z, at.x, at.z, HuntSweepStep, reach * ExploreGoal.ReachShare, (x, z) => CompanionBridge.CanWalkStraight(companion, new Vector3(x, here.y, z)), _random, out float nextX, out float nextZ);
                if (found)
                {
                    sweep.Exhausted = false;
                    sweep.IdleSince = -1f;
                    Vector3 next = new Vector3(nextX, here.y, nextZ);
                    CompanionBridge.SearchPreyAt(companion, next);
                    Log.LogInfo("[Caza] " + CompanionBridge.Name(companion) + ": sin presas a la vista, va a buscar a " + next.ToString("F1"));
                }
                else if (!sweep.Exhausted)
                {
                    sweep.Exhausted = true;
                    string name = CompanionBridge.Name(companion);
                    Log.LogInfo("[Caza] " + name + ": no queda sitio sin mirar a su alcance");
                    // Con una cantidad encargada, ya avisará la propia meta al darla por imposible.
                    if (!_goals.ContainsKey(companion))
                    {
                        AddToChat(name, ModLang.Pick(
                            name + " no encuentra presas a su alcance. Si te mueves, seguirá buscando contigo cerca.",
                            name + " cannot find any prey within reach. If you move, they will keep searching near you.",
                            name + " не находит добычи поблизости. Если пойдёшь дальше, продолжит поиск рядом с тобой."));
                    }
                }
            }
        }

        private sealed class ExploreTask
        {
            public ExploreGoal Goal;
            public readonly SearchPlan Plan = new SearchPlan(ExploreGoal.CellSize);
            public Vector3 Waypoint;
            public Vector3 LastSeenAt;
            public int Side = 1;
            public float NextLookAt;
            public string MemoryId;
            public readonly List<string> Found = new List<string>();
        }

        private bool PickExploreWaypoint(Component companion, Player player, ExploreTask task, float reach)
        {
            Vector3 here = companion.transform.position;
            Vector3 at = player.transform.position;
            bool found = task.Plan.Next(here.x, here.z, at.x, at.z, ExploreGoal.StepDistance, reach * ExploreGoal.ReachShare, (x, z) => CompanionBridge.CanWalkStraight(companion, new Vector3(x, here.y, z)), _random, out float x2, out float z2,
                (x, z) => !CompanionBridge.IsMapped(new Vector3(x, here.y, z)), CompanionBridge.MapRevealRadius * ExploreLookAheadShare);
            if (found) task.Waypoint = new Vector3(x2, here.y, z2);
            return found;
        }

        private string StartExploring(Component companion, out string note)
        {
            Player player = Player.m_localPlayer;
            float reach = CompanionBridge.LoadedReach();
            var task = new ExploreTask { Goal = new ExploreGoal(Time.time), MemoryId = CompanionBridge.PermanentId(companion) };
            note = ModLang.Pick(
                "Explorará hasta unos " + Mathf.RoundToInt(reach * ExploreGoal.ReachShare) + " m de ti: más lejos el juego lo congelaría. Si caminas, va contigo.",
                "Will explore up to about " + Mathf.RoundToInt(reach * ExploreGoal.ReachShare) + " m from you: farther away the game would freeze them. If you walk, they move with you.",
                "Разведка в радиусе около " + Mathf.RoundToInt(reach * ExploreGoal.ReachShare) + " м от тебя: дальше игра его заморозит. Если пойдёшь, он последует за тобой.");
            task.LastSeenAt = companion.transform.position;
            task.Plan.MarkSeen(task.LastSeenAt.x, task.LastSeenAt.z);
            if (!PickExploreWaypoint(companion, player, task, reach))
            {
                note = null;
                return ModLang.Pick(
                    "No ve por dónde explorar desde aquí: hay agua, un barranco o ya se ha alejado todo lo que puede.",
                    "Cannot see where to explore from here: blocked by water, a cliff, or already as far as they can go.",
                    "Не видит пути для разведки отсюда: мешает вода, обрыв или достигнут предел расстояния.");
            }

            CompanionBridge.NextStep(companion, task.Waypoint, TravelGoal.StepDistance, ref task.Side, out Vector3 step, out bool _);
            string failure = CompanionBridge.StartJourney(companion, player, step);
            if (failure != null)
            {
                note = null;
                return failure;
            }
            CompanionBridge.StayAwake(companion);
            _explores[companion] = task;
            Log.LogInfo("[Explorar] " + CompanionBridge.Name(companion) + ": empieza desde " + task.LastSeenAt.ToString("F1") + ", hasta " + Mathf.RoundToInt(reach * ExploreGoal.ReachShare) + " m del jugador");
            return null;
        }

        /// <summary>Camina por terreno nuevo sin hablar con la IA, salvo un comentario final.</summary>
        private void UpdateExplores(Player player)
        {
            if (_explores.Count == 0) return;
            float reach = CompanionBridge.LoadedReach();
            foreach (Component companion in new List<Component>(_explores.Keys))
            {
                ExploreTask task = _explores[companion];
                if (companion == null)
                {
                    // El jugador se ha ido de golpe, por ejemplo por un portal: se queda allí, y que conste dónde.
                    _explores.Remove(companion);
                    if (!string.IsNullOrEmpty(task.MemoryId))
                    {
                        CompanionMemory memory = MemoryOf(task.MemoryId);
                        memory.SetStay(ExploreStayLabel, task.LastSeenAt.x, task.LastSeenAt.y, task.LastSeenAt.z, false);
                        SaveMemory(task.MemoryId, memory);
                    }
                    Log.LogInfo("[Explorar] Se quedó sin cargar en " + task.LastSeenAt.ToString("F1"));
                    continue;
                }
                Character self = companion.GetComponent<Character>();
                if (self.GetHealth() <= 0f)
                {
                    _explores.Remove(companion);
                    continue;
                }
                // Le han mandado seguir con la rueda de órdenes: manda eso.
                if (CompanionBridge.IsFollowing(companion))
                {
                    _explores.Remove(companion);
                    continue;
                }

                Vector3 here = companion.transform.position;
                task.LastSeenAt = here;
                if (FlatDistance(here, player.transform.position) >= reach * ExploreGoal.LeaveShare)
                {
                    FinishExploring(companion, task, ModLang.Pick("te has alejado demasiado", "you moved too far away", "ты отошёл слишком далеко"));
                    continue;
                }
                if (self.GetHealthPercentage() < Initiative.BadlyHurtShare)
                {
                    FinishExploring(companion, task, ModLang.Pick("está malherido", "they are badly hurt", "тяжело ранен"));
                    continue;
                }
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Hold(Time.time);
                    continue;
                }

                if (Time.time >= task.NextLookAt)
                {
                    task.NextLookAt = Time.time + ExploreLookSeconds;
                    CompanionBridge.RevealMap(here);
                    NoteFindings(companion, task, player);
                }

                task.Plan.MarkSeen(here.x, here.z);
                ExploreStep step = task.Goal.Update(FlatDistance(here, task.Waypoint), Time.time);
                if (step == ExploreStep.OutOfTime)
                {
                    FinishExploring(companion, task, ModLang.Pick("ya lleva un buen rato", "they have been out for a while", "разведка длится уже долго"));
                    continue;
                }
                if (step == ExploreStep.Blocked) task.Plan.MarkSeen(task.Waypoint.x, task.Waypoint.z);
                if (step != ExploreStep.Walk)
                {
                    if (!PickExploreWaypoint(companion, player, task, reach))
                    {
                        FinishExploring(companion, task, ModLang.Pick("no queda terreno nuevo a su alcance", "no new ground left within reach", "поблизости не осталось неизведанных мест"));
                        continue;
                    }
                    task.Goal.NewWaypoint(Time.time);
                }
                // Sin paso no se le manda nada: si de verdad no hay por dónde, irá a otro sitio al no avanzar.
                if (CompanionBridge.NextStep(companion, task.Waypoint, TravelGoal.StepDistance, ref task.Side, out Vector3 next, out bool _)) CompanionBridge.KeepWalking(companion, next);
            }
        }

        private void NoteFindings(Component companion, ExploreTask task, Player player)
        {
            string name = CompanionBridge.Name(companion);
            foreach (KeyValuePair<string, Vector3> thing in CompanionBridge.FindingsNear(companion.transform.position, ExploreFindMeters))
            {
                if (task.Found.Count >= MaxExploreFinds) return;
                Vector3 at = thing.Value;
                if (!_foundThings.Add(thing.Key + Mathf.FloorToInt(at.x / ExploreSameFindMeters) + ":" + Mathf.FloorToInt(at.z / ExploreSameFindMeters))) continue;
                // De otra partida: ya lo encontró y sigue en el mapa.
                string pin = Landmarks.PinName(thing.Key);
                if (CompanionBridge.HasPin(at, Minimap.PinType.Icon3, pin)) continue;
                Minimap.instance?.AddPin(at, Minimap.PinType.Icon3, pin, true, false);

                task.Found.Add(thing.Key);
                Vector3 from = player.transform.position;
                string message = ModLang.Pick(
                    name + " ha encontrado " + thing.Key + " al " + Compass.Direction(at.x - from.x, at.z - from.z) + " de ti, a " + Mathf.RoundToInt(FlatDistance(at, from)) + " m. Lo marca en el mapa.",
                    name + " found " + thing.Key + " to the " + Compass.Direction(at.x - from.x, at.z - from.z) + " of you, " + Mathf.RoundToInt(FlatDistance(at, from)) + " m away. Marked on the map.",
                    name + " находит объект (" + thing.Key + ") в " + Mathf.RoundToInt(FlatDistance(at, from)) + " м от тебя, направление: " + Compass.Direction(at.x - from.x, at.z - from.z) + ". Отмечено на карте.");
                Log.LogInfo("[Explorar] " + message);
                Notify(message);
                AddToChat(name, message);
            }
        }

        private void FinishExploring(Component companion, ExploreTask task, string why)
        {
            _explores.Remove(companion);
            CompanionBridge.StopGathering(companion);
            string name = CompanionBridge.Name(companion);
            string found = string.Join(", ", task.Found.ToArray());
            string message = ModLang.Pick(
                name + " deja de explorar y vuelve contigo: " + why + "." + (task.Found.Count == 0 ? " No ha encontrado nada nuevo." : " Ha encontrado " + found + "."),
                name + " stops exploring and returns to you: " + why + "." + (task.Found.Count == 0 ? " Found nothing new." : " Found " + found + "."),
                name + " прекращает разведку и возвращается к тебе: " + why + "." + (task.Found.Count == 0 ? " Ничего нового не найдено." : " Найдено: " + found + ".") );
            Log.LogInfo("[Explorar] " + message);
            AddToChat(name, message);

            string id = CompanionBridge.PermanentId(companion);
            if (!string.IsNullOrEmpty(id))
            {
                _lastTask[id] = new TaskRecord
                {
                    Description = ModLang.Pick(
                        "explorar los alrededores" + (task.Found.Count == 0 ? ", sin encontrar nada nuevo" : ", donde encontraste " + found + " y lo marcaste en el mapa"),
                        "exploring the surroundings" + (task.Found.Count == 0 ? ", without finding anything new" : ", where you found " + found + " and marked it on the map"),
                        "разведка окрестностей" + (task.Found.Count == 0 ? ", без новых находок" : ", где удалось найти " + found + " и отметить на карте"))
                };
            }
            if (task.Found.Count > 0) RememberEvent(companion, MemoryWeight.Visit, ModLang.Pick("Explorando encontró ", "While exploring, found ", "На разведке найдено ") + found, false);
            FinishedCommentary(companion, ModLang.Pick(
                "Has terminado de explorar y vuelves con el jugador" + (task.Found.Count == 0 ? ": no has encontrado nada nuevo." : ". Has encontrado " + found + ", y lo has marcado en su mapa."),
                "You finished exploring and are returning to the player" + (task.Found.Count == 0 ? ": you found nothing new." : ". You found " + found + " and marked it on their map."),
                "Ты завершаешь разведку и возвращаешься к игроку" + (task.Found.Count == 0 ? ": ничего нового не найдено." : ". Удалось найти " + found + " и отметить на его карте.")));
        }

        private sealed class HuntTask
        {
            public HuntWatch Watch;
            public Character Prey;
            public string Label;
        }

        /// <summary>Solo con animales: en una pelea normal, Offline Companions ya sabe cuándo rendirse. Devuelve un aviso si lo hay.</summary>
        private string WatchHunt(Component companion, Character prey)
        {
            Character self = companion.GetComponent<Character>();
            if (self == null || CompanionBridge.IsThreat(self, prey)) return null;

            string label = Localization.instance != null ? Localization.instance.Localize(prey.m_name) : prey.m_name;
            Vector3 at = companion.transform.position;
            _hunts[companion] = new HuntTask { Watch = new HuntWatch(at.x, at.z, prey.GetHealth(), CompanionBridge.AmmoCarried(companion), Time.time), Prey = prey, Label = label };

            float stamina = CompanionBridge.StaminaShare(companion);
            return stamina < OutOfBreathShare
                ? ModLang.Pick(
                    "Está sin aliento (" + Mathf.RoundToInt(stamina * 100f) + " % de aguante), así que puede tardar en atacar.",
                    "Out of breath (" + Mathf.RoundToInt(stamina * 100f) + "% stamina), so they may take a moment to attack.",
                    "Спутник выбился из сил (" + Mathf.RoundToInt(stamina * 100f) + " % выносливости), атака может занять время.")
                : null;
        }

        private void UpdateHunts(Player player)
        {
            if (_hunts.Count == 0) return;
            foreach (Component companion in new List<Component>(_hunts.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _hunts.Remove(companion);
                    continue;
                }

                HuntTask task = _hunts[companion];
                bool alive = task.Prey != null && task.Prey.GetHealth() > 0f;
                Vector3 at = companion.transform.position;
                HuntState state = task.Watch.Update(alive, alive ? task.Prey.GetHealth() : 0f, CompanionBridge.IsEngaged(companion), at.x, at.z, CompanionBridge.AmmoCarried(companion), Time.time);
                if (state == HuntState.Going) continue;

                _hunts.Remove(companion);
                string name = CompanionBridge.Name(companion);
                string id = CompanionBridge.PermanentId(companion);
                if (state == HuntState.Done)
                {
                    _lastTask[id] = new TaskRecord { Description = ModLang.Pick("cazar ", "hunting ", "охота: ") + task.Label };
                    Log.LogInfo("[Caza] " + name + ": ha acabado con " + task.Label);
                    AddToChat(name, ModLang.Pick(name + " ha acabado con " + task.Label + ".", name + " took down " + task.Label + ".", name + " одолевает цель: " + task.Label + "."));
                    continue;
                }

                float stamina = CompanionBridge.StaminaShare(companion);
                float distance = alive ? Vector3.Distance(at, task.Prey.transform.position) : 0f;
                Log.LogInfo("[Caza] " + name + ": " + state + " con " + task.Label + "; aguante " + Mathf.RoundToInt(stamina * 100f) + " %, a " + Mathf.RoundToInt(distance) + " m, arma " + CompanionBridge.WeaponInHand(companion) + ", estado: " + CompanionBridge.Status(companion));
                _lastTask[id] = new TaskRecord { Description = ModLang.Pick("cazar " + task.Label + ", sin conseguirlo", "hunting " + task.Label + ", without success", "охота на " + task.Label + ", безуспешно") };

                bool tired = stamina < OutOfBreathResume;
                string reason = state == HuntState.Lost
                    ? ModLang.Pick("ha dejado de perseguirlo: seguramente ha huido o está demasiado lejos.", "stopped chasing it: it probably fled or is too far away.", "преследование прекращено: добыча сбежала или слишком далеко.")
                    : tired
                        ? ModLang.Pick("se ha quedado sin aliento (" + Mathf.RoundToInt(stamina * 100f) + " % de aguante) y lo deja por ahora.", "ran out of breath (" + Mathf.RoundToInt(stamina * 100f) + "% stamina) and gave up for now.", "не хватает выносливости (" + Mathf.RoundToInt(stamina * 100f) + " %) — охота пока прекращена.")
                        : ModLang.Pick("no consigue acercarse ni dispararle, así que lo deja.", "cannot get close or land a shot, so they gave up.", "не удаётся подойти или выстрелить — преследование прекращено.");
                string message = ModLang.Pick(name + " no ha podido con " + task.Label + ": " + reason, name + " could not hunt " + task.Label + ": " + reason, name + " не может одолеть " + task.Label + ": " + reason);
                if (state == HuntState.Stalled) CompanionBridge.StopFighting(companion, player);
                Notify(message);
                AddToChat(name, message);
                _chains.Remove(companion);
                FinishedCommentary(companion, ModLang.Pick("No has podido cazar a " + task.Label + ": " + reason, "You could not hunt " + task.Label + ": " + reason, "Не удалось поймать " + task.Label + ": " + reason));
            }
        }

        /// <summary>Al morir se destruye el compañero, así que hay que apuntar antes quién era.</summary>
        private sealed class Worker
        {
            public string Id;
            public string Name;
        }

        private void RememberWorker(Component companion)
        {
            if (companion != null) _workers[companion] = new Worker { Id = CompanionBridge.PermanentId(companion), Name = CompanionBridge.Name(companion) };
        }

        /// <summary>
        /// Offline Companions lo resucita con el mismo trabajo y sin seguir al jugador, y sin este encargo que le ponga
        /// límite trabajaría para siempre. Lo que lo mató puede seguir ahí, así que el encargo se da por terminado.
        /// </summary>
        private void AbandonOnDeath(Component companion, string task)
        {
            _taskStartPack.Remove(companion);
            if (!_workers.TryGetValue(companion, out Worker worker)) return;
            _workers.Remove(companion);
            _diedAtWork.Add(worker.Id);
            _lastTask[worker.Id] = new TaskRecord { Description = task + ModLang.Pick(", sin terminar porque moriste", ", unfinished because you died", ", не завершено из-за твоей гибели") };

            string message = ModLang.Pick(
                worker.Name + " ha muerto y no seguirá con su encargo: " + task + ".",
                worker.Name + " died and will not continue their task: " + task + ".",
                worker.Name + " погибает и не может продолжить поручение: " + task + ".");
            Log.LogInfo("[Meta] " + message);
            Notify(message);
            AddToChat(worker.Name, message);
        }

        private void UpdateRevived()
        {
            if (_diedAtWork.Count == 0) return;
            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                if (!_diedAtWork.Remove(CompanionBridge.PermanentId(companion))) continue;
                CompanionBridge.StopGathering(companion);
                Log.LogInfo("[Meta] " + CompanionBridge.Name(companion) + ": ha vuelto y deja el trabajo que tenía al morir.");
            }
        }

        /// <summary>Si su encargo actual consiste en un trabajo de Offline Companions, que no termina por sí solo.</summary>
        private bool HoldsJob(Component companion)
        {
            return _goals.ContainsKey(companion) || _trees.ContainsKey(companion) || _cooks.ContainsKey(companion) || _organizing.ContainsKey(companion) || _smelts.ContainsKey(companion);
        }

        private sealed class GiveTask
        {
            public GiveGoal Goal;
            public Dictionary<string, int> Items;
        }

        private sealed class TaskRecord
        {
            public string Description;
            public Dictionary<string, int> Gained = new Dictionary<string, int>();
        }

        private string StartCooking(Component companion, ActionRequest action, out string note)
        {
            note = null;
            if (!BehaviorOverrides.CookAvailable) return ModLang.Pick("Cocinar una comida concreta no está disponible.", "Cooking a specific food is not available.", "Приготовление конкретного блюда недоступно.");

            Dictionary<string, string> foods = CompanionBridge.CookableFoods();
            string food = CompanionBridge.FindFood(action.Item, foods);
            if (food == null)
            {
                return ModLang.Pick(
                    "No sabe cocinar " + action.Item + ". Comidas posibles: " + string.Join(", ", new List<string>(foods.Values).ToArray()) + ".",
                    "Does not know how to cook " + action.Item + ". Possible foods: " + string.Join(", ", new List<string>(foods.Values).ToArray()) + ".",
                    "Не умеет готовить " + action.Item + ". Возможные блюда: " + string.Join(", ", new List<string>(foods.Values).ToArray()) + ".");
            }
            if (BehaviorOverrides.CookFood != null && BehaviorOverrides.CookFood != food)
            {
                return ModLang.Pick(
                    "Otro compañero está con un encargo de cocina distinto. Debe esperar a que termine.",
                    "Another companion is working on a different cooking task. Must wait until they finish.",
                    "Другой спутник уже готовит другое блюдо. Нужно дождаться окончания.");
            }

            int stock = CompanionBridge.CookStock(companion, food);
            BehaviorOverrides.CookFood = food;
            BehaviorOverrides.CookTarget = stock + action.Amount;
            var job = new ActionRequest { Kind = ActionKind.SetJob, Value = CompanionActions.JobCook };
            string failure = CompanionBridge.Execute(companion, null, Player.m_localPlayer, null, job, out string _);
            if (failure != null)
            {
                if (_cooks.Count == 0) BehaviorOverrides.CookFood = null;
                return failure;
            }

            string label = CompanionBridge.LabelOf(food);
            _cooks[companion] = new GatherGoal(food, label, action.Amount, stock, Time.time, CookStuckSeconds, CookStuckSeconds);
            note = ModLang.Pick(
                "Necesita conocer la receta y tener cerca la estación y los ingredientes.",
                "Needs to know the recipe and have the station and ingredients nearby.",
                "Нужно знать рецепт, а также иметь рядом кулинарную стойку и ингредиенты.");
            return null;
        }

        private void UpdateCooks()
        {
            if (_cooks.Count == 0) return;
            foreach (Component companion in new List<Component>(_cooks.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, ModLang.Pick("cocinar " + _cooks[companion].Amount + " de " + _cooks[companion].Label, "cooking " + _cooks[companion].Amount + " " + _cooks[companion].Label, "готовка: " + _cooks[companion].Amount + " шт. " + _cooks[companion].Label));
                    EndCook(companion);
                    continue;
                }

                GatherGoal goal = _cooks[companion];
                GoalState state = goal.Update(CompanionBridge.CookStock(companion, goal.Item), CompanionBridge.IsCooking(companion), Time.time);
                if (state == GoalState.Working) continue;
                if (state == GoalState.Reached && WaitsToStoreCooked(companion, goal)) continue;

                string reason = state == GoalState.Stuck ? CompanionBridge.Status(companion) : null;
                EndCook(companion);
                CompanionBridge.StopGathering(companion);
                string name = CompanionBridge.Name(companion);
                string progress = ModLang.Pick(goal.Gained + " de " + goal.Amount + " de " + goal.Label, goal.Gained + " of " + goal.Amount + " " + goal.Label, goal.Gained + " из " + goal.Amount + " " + goal.Label);
                _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("cocinar ", "cooking ", "готовка: ") + progress };
                Log.LogInfo("[Meta] " + name + ": cocina " + state + " con " + progress + (reason == null ? "" : " (" + reason + ")"));

                string message = state == GoalState.Reached
                    ? ModLang.Pick(name + " ha cocinado " + progress + ".", name + " cooked " + progress + ".", name + " готовит " + progress + ".")
                    : ModLang.Pick(
                        name + " no ha podido terminar de cocinar: lleva " + progress + (string.IsNullOrEmpty(reason) ? "." : ". " + reason),
                        name + " could not finish cooking: has " + progress + (string.IsNullOrEmpty(reason) ? "." : ". " + reason),
                        name + " не удаётся закончить готовку: готово " + progress + (string.IsNullOrEmpty(reason) ? "." : ". " + reason));
                Notify(message);
                AddToChat(name, message);
                FinishedCommentary(companion, state == GoalState.Reached
                    ? ModLang.Pick("Has terminado de cocinar " + progress + ".", "You finished cooking " + progress + ".", "Ты завершаешь готовку: готово " + progress + ".")
                    : ModLang.Pick("No has podido terminar de cocinar: llevas " + progress + ".", "You could not finish cooking: you have " + progress + ".", "Не удалось завершить готовку: готово " + progress + "."));
                if (state != GoalState.Reached && goal.Gained == 0) _chains.Remove(companion);
            }
        }

        /// <summary>
        /// Offline Companions guarda lo cocinado en el cofre que ya tiene esa comida, pero solo cuando no le queda nada
        /// por cocinar. Quitarle el trabajo al llegar a la cantidad lo dejaría en su mochila.
        /// </summary>
        private bool WaitsToStoreCooked(Component companion, GatherGoal goal)
        {
            if (CompanionBridge.CountInPack(companion, goal.Item) == 0 || !CompanionBridge.IsCooking(companion)) return false;
            if (!_cookStoreSince.TryGetValue(companion, out float since))
            {
                _cookStoreSince[companion] = Time.time;
                Log.LogInfo("[Meta] " + CompanionBridge.Name(companion) + ": ya tiene " + goal.Gained + " de " + goal.Label + "; espera a que lo guarde");
                return true;
            }
            return Time.time - since < CookStoreSeconds;
        }

        private void EndCook(Component companion)
        {
            _cookStoreSince.Remove(companion);
            _cooks.Remove(companion);
            if (_cooks.Count == 0) BehaviorOverrides.CookFood = null;
        }

        private string RecallEveryone()
        {
            // Afecta a todos: nada de lo que estuvieran haciendo por encargo sigue siendo válido.
            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                _chains.Remove(companion);
                ClearGoals(companion);
            }
            List<string> outOfReach = CompanionBridge.RecallEveryone(Player.m_localPlayer);
            return outOfReach.Count == 0
                ? ModLang.Pick("Vienen todos los que están con vida.", "All living companions are on their way.", "Идут все живые спутники.")
                : ModLang.Pick(
                    "No pueden oír la llamada porque el juego tiene su zona sin cargar: " + string.Join("; ", outOfReach.ToArray()) + ". Hay que acercarse a ellos.",
                    "Cannot hear the call because the game has their zone unloaded: " + string.Join("; ", outOfReach.ToArray()) + ". You need to get closer to them.",
                    "Не слышат зов, так как их зона выгружена из памяти игры: " + string.Join("; ", outOfReach.ToArray()) + ". Нужно подойти ближе к ним.");
        }

        private sealed class SmeltTask
        {
            public TreeGoal Goal;
            public ProgressWatch Progress;
            public string Item;
            public string Label;
            public bool FoundWork;
        }

        private string StartSmelting(Component companion, ActionRequest action, out string note)
        {
            note = null;
            if (!BehaviorOverrides.SmeltAvailable) return ModLang.Pick("Fundir un material concreto no está disponible.", "Smelting a specific material is not available.", "Плавка конкретного материала недоступна.");

            Dictionary<string, string> items = CompanionBridge.SmeltableNearby(companion);
            if (items.Count == 0) return ModLang.Pick("No hay ningún horno a menos de 25 m.", "No smelter or kiln within 25 m.", "Поблизости нет плавильни или печи в радиусе 25 м.");
            string item = CompanionBridge.FindFood(action.Item, items);
            if (item == null)
            {
                return ModLang.Pick(
                    "Los hornos cercanos no aceptan " + action.Item + ". Aceptan: " + string.Join(", ", new List<string>(items.Values).ToArray()) + ". Para que los rellene con lo que haya, también de combustible, usa set_job con smelt.",
                    "Nearby smelters do not accept " + action.Item + ". They accept: " + string.Join(", ", new List<string>(items.Values).ToArray()) + ". To have them filled with whatever is available, fuel included, use set_job with smelt.",
                    "Печи поблизости не принимают " + action.Item + ". Доступно: " + string.Join(", ", new List<string>(items.Values).ToArray()) + ". Чтобы заполнить всем подходящим сырьём и топливом, используй set_job со smelt.");
            }
            if (BehaviorOverrides.SmeltItem != null && BehaviorOverrides.SmeltItem != item)
            {
                return ModLang.Pick(
                    "Otro compañero está fundiendo un material distinto. Debe esperar a que termine.",
                    "Another companion is smelting a different material. Must wait until they finish.",
                    "Другой спутник уже плавит другой материал. Нужно дождаться окончания.");
            }

            string label = items[item];
            int left = CompanionBridge.SmeltWorkLeft(companion, item);

            BehaviorOverrides.SmeltItem = item;
            var job = new ActionRequest { Kind = ActionKind.SetJob, Value = CompanionActions.JobSmelt };
            string error = CompanionBridge.Execute(companion, null, Player.m_localPlayer, null, job, out string _);
            if (error != null)
            {
                if (_smelts.Count == 0) BehaviorOverrides.SmeltItem = null;
                return error;
            }
            _smelts[companion] = new SmeltTask
            {
                Goal = new TreeGoal(Time.time, SmeltSettleSeconds, SmeltTimeoutSeconds),
                Progress = new ProgressWatch(left, Time.time),
                Item = item,
                Label = label
            };
            note = ModLang.Pick(
                "Usa lo que haya en su mochila, los cofres y las pilas cercanas, también el combustible. Puede tardar varios minutos.",
                "Uses what is in their pack, nearby chests, and stacks, including fuel. May take several minutes.",
                "Использует ресурсы из инвентаря, сундуков и ближайших куч, включая топливо. Может занять несколько минут.");
            return null;
        }

        private void UpdateSmelts()
        {
            if (_smelts.Count == 0 || Time.time < _nextSmeltCheckAt) return;
            _nextSmeltCheckAt = Time.time + SmeltCheckInterval;

            foreach (Component companion in new List<Component>(_smelts.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, ModLang.Pick("fundir ", "smelting ", "плавка: ") + _smelts[companion].Label);
                    EndSmelt(companion);
                    continue;
                }

                SmeltTask task = _smelts[companion];
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Postpone(SmeltCheckInterval);
                    task.Progress.Postpone(SmeltCheckInterval);
                    continue;
                }

                int left = CompanionBridge.SmeltWorkLeft(companion, task.Item);
                bool busy = CompanionBridge.IsSmeltBusy(companion);
                if (left > 0 || busy) task.FoundWork = true;
                // Un viaje largo, por ejemplo a una pila que no se cuenta aquí, también es avanzar.
                if (busy) task.Progress.Postpone(SmeltCheckInterval);
                bool stuck = task.Progress.SecondsWithoutChange(left, Time.time) >= SmeltStuckSeconds;
                GoalState state = stuck ? GoalState.Stuck : task.Goal.Update(busy, left, Time.time);
                if (state == GoalState.Working) continue;

                bool finished = state == GoalState.Reached && task.FoundWork;
                string reason = state == GoalState.Stuck ? CompanionBridge.Status(companion) : null;
                EndSmelt(companion);
                CompanionBridge.StopGathering(companion);
                string name = CompanionBridge.Name(companion);
                _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("fundir ", "smelting ", "плавка: ") + task.Label };
                Log.LogInfo("[Meta] " + name + ": fundir " + task.Label + " " + state + ", había material: " + task.FoundWork + (reason == null ? "" : " (" + reason + ")"));

                string message = finished
                    ? ModLang.Pick(name + " ha terminado de fundir " + task.Label + ".", name + " finished smelting " + task.Label + ".", name + " завершает плавку (" + task.Label + ").")
                    : !task.FoundWork
                        ? ModLang.Pick(
                            name + " no ha encontrado " + task.Label + " en su mochila, en los cofres ni en las pilas cercanas.",
                            name + " did not find any " + task.Label + " in their pack, chests, or nearby stacks.",
                            name + " не находит " + task.Label + " ни в инвентаре, ни в сундуках, ни в ближайших кучах.")
                        : ModLang.Pick(
                            name + " ha dejado de fundir " + task.Label + (string.IsNullOrEmpty(reason) ? "." : ". " + reason),
                            name + " stopped smelting " + task.Label + (string.IsNullOrEmpty(reason) ? "." : ". " + reason),
                            name + " прекращает плавку (" + task.Label + ")" + (string.IsNullOrEmpty(reason) ? "." : ". " + reason));
                Notify(message);
                AddToChat(name, message);
                FinishedCommentary(companion, finished
                    ? ModLang.Pick("Has terminado de fundir " + task.Label + ".", "You finished smelting " + task.Label + ".", "Ты завершаешь плавку (" + task.Label + ").")
                    : !task.FoundWork
                        ? ModLang.Pick("No has encontrado " + task.Label + " que fundir.", "You found no " + task.Label + " to smelt.", "Не найдено сырья для плавки: " + task.Label + ".")
                        : ModLang.Pick("Has dejado de fundir " + task.Label + " sin acabar.", "You stopped smelting " + task.Label + " before finishing.", "Ты прекращаешь плавку (" + task.Label + "), не закончив."));
                if (!finished) _chains.Remove(companion);
            }
        }

        private void EndSmelt(Component companion)
        {
            _smelts.Remove(companion);
            if (_smelts.Count == 0) BehaviorOverrides.SmeltItem = null;
        }

        private sealed class UpkeepTask
        {
            public TreeGoal Goal;
            public string Label;
            public bool NeedsHammer;
        }

        private string StartUpkeepTask(Component companion, string plan, string label, out string note)
        {
            note = null;
            if (!BehaviorOverrides.UpkeepAvailable) return ModLang.Pick("Las tareas de mantenimiento sueltas no están disponibles.", "Standalone upkeep tasks are not available.", "Отдельные задачи обслуживания недоступны.");
            // Offline Companions va solo por el martillo a un cofre con este trabajo. Si no puede, no encontraría nada que hacer y parecería que ya estaba todo hecho.
            bool needsHammer = CompanionActions.NeedsHammer(plan);
            if (needsHammer)
            {
                string hammer = CompanionBridge.HammerProblem(companion);
                if (hammer != null) return hammer;
            }
            bool fetchesHammer = needsHammer && !CompanionBridge.HasHammer(companion);

            BehaviorOverrides.UpkeepFocus[companion.gameObject] = plan;
            var job = new ActionRequest { Kind = ActionKind.SetJob, Value = CompanionActions.JobUpkeep };
            string failure = CompanionBridge.Execute(companion, null, Player.m_localPlayer, null, job, out string _);
            if (failure != null)
            {
                BehaviorOverrides.UpkeepFocus.Remove(companion.gameObject);
                return failure;
            }
            _organizing[companion] = new UpkeepTask { Goal = new TreeGoal(Time.time, OrganizeSettleSeconds, OrganizeTimeoutSeconds), Label = label, NeedsHammer = needsHammer };
            note = plan == CompanionActions.SortPlan
                ? ModLang.Pick(
                    "No mueve armas, armaduras ni herramientas, y solo usa los cofres que puede abrir.",
                    "Does not move weapons, armor, or tools, and only uses chests they can open.",
                    "Не трогает оружие, броню и инструменты, и использует только открываемые сундуки.")
                : (fetchesHammer ? ModLang.Pick("Primero irá por un martillo a un cofre y al acabar lo devolverá. ", "They will first fetch a hammer from a chest and put it back when done. ", "Сначала возьмёт молот из сундука, а закончив — вернёт его на место. ") : "") + ModLang.Pick(
                    "Trabaja alrededor de su hogar si está cerca o, si no, a unos 30 m. Si no hay nada que hacer, terminará enseguida.",
                    "Works around their home point if nearby, or within 30 m otherwise. If there is nothing to do, they will finish right away.",
                    "Работает около базы, если она рядом, или в радиусе 30 м. Если дел нет, быстро завершит работу.");
            return null;
        }

        private void UpdateOrganizing()
        {
            if (_organizing.Count == 0 || Time.time < _nextTreeCheckAt) return;
            foreach (Component companion in new List<Component>(_organizing.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, _organizing[companion].Label);
                    EndOrganizing(companion);
                    continue;
                }

                UpkeepTask task = _organizing[companion];
                GoalState state = task.Goal.Update(CompanionBridge.IsOrganizing(companion), 0, Time.time);
                if (state == GoalState.Working) continue;

                // Sin martillo no ha podido hacer nada: decir que ha terminado daría a entender que no había trabajo.
                bool noHammer = state == GoalState.Reached && task.NeedsHammer && !CompanionBridge.HasHammer(companion);
                EndOrganizing(companion);
                CompanionBridge.StopGathering(companion);
                string name = CompanionBridge.Name(companion);
                string id = CompanionBridge.PermanentId(companion);
                _lastTask[id] = new TaskRecord { Description = task.Label };
                Log.LogInfo("[Meta] " + name + ": " + task.Label + " " + state + (noHammer ? ", sin martillo" : ""));
                string message = noHammer
                    ? ModLang.Pick(name + " no ha podido " + task.Label + ": no consiguió un martillo.", name + " could not complete task (" + task.Label + "): could not get a hammer.", name + " не может выполнить задачу (" + task.Label + "): не удалось достать молот.")
                    : state == GoalState.Reached
                    ? ModLang.Pick(name + " ha terminado de " + task.Label + ".", name + " finished task: " + task.Label + ".", name + " завершает: " + task.Label + ".")
                    : ModLang.Pick(name + " ha dejado de " + task.Label + ": se agotó el tiempo.", name + " stopped task (" + task.Label + "): timed out.", name + " прекращает (" + task.Label + "): время вышло.");
                Notify(message);
                AddToChat(name, message);
                FinishedCommentary(companion, noHammer
                    ? ModLang.Pick("No has podido " + task.Label + " porque no conseguiste un martillo.", "You could not complete (" + task.Label + ") because you could not get a hammer.", "Не удалось выполнить (" + task.Label + "): не удалось достать молот.")
                    : state == GoalState.Reached
                    ? ModLang.Pick("Has terminado de " + task.Label + ".", "You finished: " + task.Label + ".", "Ты завершаешь: " + task.Label + ".")
                    : ModLang.Pick("Has dejado de " + task.Label + " porque se agotó el tiempo.", "You stopped (" + task.Label + ") because it timed out.", "Ты прекращаешь (" + task.Label + "): время вышло."));
                if (noHammer) _chains.Remove(companion);
            }
        }

        private void EndOrganizing(Component companion)
        {
            _organizing.Remove(companion);
            if (companion != null) BehaviorOverrides.UpkeepFocus.Remove(companion.gameObject);
        }

        /// <summary>Offline Companions solo sabe llevar las cosas a un cofre: aquí se encadenan los viajes hasta que no quede nada o no quepa más.</summary>
        private void UpdateStores(Player player)
        {
            if (_stores.Count == 0) return;
            foreach (Component companion in new List<Component>(_stores.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    EndStore(companion);
                    continue;
                }

                StoreGoal goal = _stores[companion];
                if (CompanionBridge.IsDepositing(companion))
                {
                    if (!goal.TimedOut(Time.time)) continue;
                    CompanionBridge.CancelDeposit(companion);
                }

                List<CarriedStack> left = CompanionBridge.StorableStacks(companion);
                int units = StorePlanner.Units(left);
                bool progress = goal.TripEnded(units);
                string name = CompanionBridge.Name(companion);
                Log.LogInfo("[Guardar] " + name + ": " + (progress ? "le quedan " + units + " por guardar" : "ese cofre no le ha servido; le quedan " + units));

                if (goal.CanContinue(Time.time))
                {
                    StoreTrip next = CompanionBridge.PlanStoreTrip(companion, player, goal.Refused, out Container chest, out int _);
                    if (next != null)
                    {
                        CompanionBridge.SendToChest(companion, player, chest, next.Items, false);
                        if (CompanionBridge.IsDepositing(companion))
                        {
                            StartStoreTrip(companion, goal, next);
                            continue;
                        }
                    }
                }
                FinishStoring(companion, goal, left);
            }
        }

        private void StartStoreTrip(Component companion, StoreGoal goal, StoreTrip trip)
        {
            goal.TripStarted(CompanionBridge.DepositChest(companion));
            Log.LogInfo("[Guardar] " + CompanionBridge.Name(companion) + ": lleva " + string.Join(", ", new List<string>(trip.Items).ToArray()) + " al cofre en " + CompanionBridge.DepositChestPosition(companion));
        }

        private void FinishStoring(Component companion, StoreGoal goal, List<CarriedStack> left)
        {
            EndStore(companion);
            string name = CompanionBridge.Name(companion);
            if (_tends.TryGetValue(companion, out TendTask tend))
            {
                Log.LogInfo("[Encargo] " + name + ": ha guardado lo recogido" + (left.Count == 0 ? "" : "; no le cabe " + CompanionBridge.DescribeStacks(left)));
                BackToUpkeep(companion, tend);
                return;
            }
            int stored = goal.StartUnits - StorePlanner.Units(left);
            string chests = goal.ChestsUsed == 1 ? ModLang.Pick("1 cofre", "1 chest", "1 сундук") : goal.ChestsUsed + ModLang.Pick(" cofres", " chests", " сундуков");
            string remaining = left.Count == 0 ? "" : CompanionBridge.DescribeStacks(left);

            string message;
            string commentary;
            if (stored <= 0)
            {
                message = ModLang.Pick(
                    name + " no ha podido guardar nada: " + remaining + " no cabe en los cofres cercanos o no puede abrirlos.",
                    name + " could not store anything: " + remaining + " does not fit in nearby chests or they cannot open them.",
                    name + " не может сложить вещи: " + remaining + " не помещается в сундуки рядом или их нельзя открыть.");
                commentary = ModLang.Pick(
                    "No has podido guardar nada: no cabe en los cofres cercanos o no puedes abrirlos. Sigues llevando " + remaining + ".",
                    "You could not store anything: it does not fit in nearby chests or you cannot open them. You are still carrying " + remaining + ".",
                    "Не удалось сложить вещи: они не помещаются в сундуки рядом или их нельзя открыть. У тебя остаётся " + remaining + ".");
                _chains.Remove(companion);
            }
            else if (left.Count > 0)
            {
                message = ModLang.Pick(
                    name + " ha guardado parte de sus cosas en " + chests + ", pero no hay sitio para el resto: " + remaining + ".",
                    name + " stored some of their items in " + chests + ", but there is no room for the rest: " + remaining + ".",
                    name + " складывает часть вещей в " + chests + ", но для остального нет места: " + remaining + ".");
                commentary = ModLang.Pick(
                    "Has guardado parte de tus cosas en " + chests + ", pero no hay sitio para el resto. Sigues llevando " + remaining + ".",
                    "You stored some of your items in " + chests + ", but there is no room for the rest. You are still carrying " + remaining + ".",
                    "Ты складываешь часть вещей в " + chests + ", но для остального нет места. У тебя остаётся " + remaining + ".");
            }
            else
            {
                message = ModLang.Pick(
                    name + " ha guardado sus cosas en " + chests + ".",
                    name + " stored their items in " + chests + ".",
                    name + " складывает вещи в " + chests + ".");
                commentary = ModLang.Pick(
                    "Has guardado tus cosas en " + chests + ".",
                    "You stored your items in " + chests + ".",
                    "Ты складываешь вещи в " + chests + ".");
            }

            _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("guardar sus cosas en los cofres", "storing their items in the chests", "складывание вещей в сундуки") };
            Log.LogInfo("[Meta] " + message);
            Notify(message);
            AddToChat(name, message);
            FinishedCommentary(companion, commentary, left.Count > 0);
        }

        private void EndStore(Component companion)
        {
            _stores.Remove(companion);
            if (companion != null) BehaviorOverrides.StoreOnly.Remove(companion.gameObject);
        }

        private string ChopItemFor(GameObject worker)
        {
            foreach (KeyValuePair<Component, GatherGoal> goal in _goals)
            {
                if (goal.Key != null && goal.Key.gameObject == worker && CompanionActions.NeedsChosenTrees(goal.Value.Item)) return goal.Value.Item;
            }
            return null;
        }

        private static bool _modItemsAdded;

        /// <summary>Con los mods de Therzie cargados, la IA también puede pedir sus pieles y carnes. Sin ellos no cambia nada.</summary>
        private static void AddModItemsOnce()
        {
            if (_modItemsAdded || Player.m_localPlayer == null || ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0) return;
            _modItemsAdded = true;
            int added = CompanionActions.AddModItems(prefab =>
            {
                string token = ObjectDB.instance.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name;
                if (token == null) return null;
                return Localization.instance != null ? Localization.instance.Localize(token) : "";
            });
            if (added > 0) Log.LogInfo("[Mods] " + added + " items from other mods can be requested with gather.");
        }

        private string GroundItemFor(GameObject worker)
        {
            foreach (KeyValuePair<Component, GatherGoal> goal in _goals)
            {
                if (goal.Key != null && goal.Key.gameObject == worker && CompanionActions.FoundOnGround(goal.Value.Item)) return goal.Value.Item;
            }
            return null;
        }

        private sealed class PickupTask
        {
            public GiveGoal Goal;
            public ItemDrop Drop;
            public string Prefab;
            public string Label;
            public bool Broken;
        }

        private string StartPickup(Component companion, LookTarget look, ActionRequest action)
        {
            ItemDrop drop = CompanionBridge.FindLooseItem(companion, look, action.Item, GatherSearchRange);
            if (drop == null)
            {
                return string.IsNullOrEmpty(action.Item)
                    ? ModLang.Pick("El jugador no está mirando ningún objeto suelto.", "The player is not looking at any loose item.", "Игрок не смотрит на лежащий предмет.")
                    : ModLang.Pick(
                        "No hay ningún objeto suelto llamado " + action.Item + " a menos de " + Mathf.RoundToInt(GatherSearchRange) + " m.",
                        "No loose item named " + action.Item + " within " + Mathf.RoundToInt(GatherSearchRange) + " m.",
                        "На земле нет предмета '" + action.Item + "' в радиусе " + Mathf.RoundToInt(GatherSearchRange) + " м.");
            }
            _pickups[companion] = new PickupTask { Goal = new GiveGoal(Time.time), Drop = drop, Prefab = Utils.GetPrefabName(drop.gameObject), Label = CompanionBridge.DroppedLabel(drop), Broken = CompanionBridge.IsBroken(drop) };
            return null;
        }

        /// <summary>Va hasta el objeto y lo recoge él mismo, porque Offline Companions ignora lo que suelta un jugador.</summary>
        private void UpdatePickups(Player player)
        {
            if (_pickups.Count == 0) return;
            foreach (Component companion in new List<Component>(_pickups.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _pickups.Remove(companion);
                    continue;
                }

                PickupTask task = _pickups[companion];
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Postpone(ChainCheckInterval);
                    continue;
                }

                string name = CompanionBridge.Name(companion);
                if (task.Drop == null)
                {
                    _pickups.Remove(companion);
                    _chains.Remove(companion);
                    AddToChat(name, ModLang.Pick(
                        name + " ya no encuentra " + task.Label + ": alguien lo tomó o desapareció.",
                        name + " can no longer find " + task.Label + ": someone picked it up or it disappeared.",
                        name + " больше не видит " + task.Label + ": предмет подобрали или он исчез."));
                }
                else if (task.Goal.InReach(Vector3.Distance(companion.transform.position, task.Drop.transform.position)) && CompanionBridge.PickUp(companion, task.Drop))
                {
                    _pickups.Remove(companion);
                    CompanionBridge.KeepGear(companion, new[] { task.Prefab });
                    _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("recoger del suelo ", "picking up ", "подбор с земли: ") + task.Label };
                    Log.LogInfo("[Meta] " + name + ": recoge del suelo " + task.Label + (task.Broken ? " (rota)" : ""));
                    string done = ModLang.Pick(
                        name + " ha recogido " + task.Label + "." + (task.Broken ? " Está rota: hay que repararla para poder usarla." : ""),
                        name + " picked up " + task.Label + "." + (task.Broken ? " It is broken: it must be repaired before use." : ""),
                        name + " подбирает " + task.Label + "." + (task.Broken ? " Предмет сломан: его нужно починить перед использованием." : ""));
                    Notify(done);
                    AddToChat(name, done);
                    FinishedCommentary(companion, ModLang.Pick(
                        "Has recogido del suelo " + task.Label + "." + (task.Broken ? " Está rota y no podrás usarla hasta repararla." : ""),
                        "You picked up " + task.Label + " from the ground." + (task.Broken ? " It is broken and cannot be used until repaired." : ""),
                        "Ты подбираешь с земли " + task.Label + "." + (task.Broken ? " Предмет сломан и не может использоваться до починки." : "")));
                }
                else if (task.Goal.TimedOut(Time.time))
                {
                    _pickups.Remove(companion);
                    _chains.Remove(companion);
                    string problem = ModLang.Pick(
                        name + " no ha podido recoger " + task.Label + ": no llega hasta allí o no le cabe.",
                        name + " could not pick up " + task.Label + ": cannot reach it or has no room.",
                        name + " не может подобрать " + task.Label + ": не достать или нет места в инвентаре.");
                    Log.LogWarning("[Meta] " + problem);
                    Notify(problem);
                    AddToChat(name, problem);
                }
                else if (task.Goal.ShouldWalk(Time.time))
                {
                    CompanionBridge.WalkTo(companion, player, task.Drop.transform.position);
                }
            }
        }

        private sealed class TakeTask
        {
            public GiveGoal Goal;
            public Container Chest;
            public Dictionary<string, int> Items;
            public readonly Queue<KeyValuePair<Container, Dictionary<string, int>>> Next = new Queue<KeyValuePair<Container, Dictionary<string, int>>>();
            public readonly Dictionary<string, int> Taken = new Dictionary<string, int>();
        }

        private string StartTaking(Component companion, ActionRequest action)
        {
            if (ItemNames.IsFoodWord(action.Item)) return StartTakingFood(companion, action.Amount);

            Dictionary<string, int> items;
            Container chest = ItemNames.IsAmmoWord(action.Item)
                ? CompanionBridge.FindChestWithAmmo(companion, CompanionBridge.LauncherAmmoType(companion), action.Amount, out items)
                : CompanionBridge.FindChestWith(companion, action.Item, action.Amount, out items);
            if (chest == null) return ModLang.Pick("No hay ningún cofre utilizable cerca con " + action.Item + ".", "No usable chest nearby with " + action.Item + ".", "Рядом нет доступного сундука с " + action.Item + ".");
            if (action.Amount <= 0) CompanionBridge.LimitFoodToShare(companion, items);
            _takes[companion] = new TakeTask { Goal = new GiveGoal(Time.time), Chest = chest, Items = items };
            return null;
        }

        private string StartTakingFood(Component companion, int amount)
        {
            List<KeyValuePair<Container, Dictionary<string, int>>> stops = CompanionBridge.PlanFoodTrip(companion, amount);
            if (stops.Count == 0)
            {
                return CompanionBridge.CarriesFood(companion)
                    ? ModLang.Pick("Ya lleva comida suficiente y no necesita tomar más.", "Already carrying enough food and does not need to take more.", "Еды с собой уже достаточно, брать ещё не требуется.")
                    : ModLang.Pick("No hay comida en ningún cofre utilizable cerca.", "No food in any usable chest nearby.", "В доступных сундуках поблизости нет еды.");
            }
            var task = new TakeTask { Goal = new GiveGoal(Time.time), Chest = stops[0].Key, Items = stops[0].Value };
            for (int i = 1; i < stops.Count; i++) task.Next.Enqueue(stops[i]);
            _takes[companion] = task;
            return null;
        }

        private void UpdateTakes(Player player)
        {
            if (_takes.Count == 0) return;
            foreach (Component companion in new List<Component>(_takes.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _takes.Remove(companion);
                    continue;
                }

                TakeTask task = _takes[companion];
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Postpone(Time.deltaTime);
                    continue;
                }

                string name = CompanionBridge.Name(companion);
                bool chestGone = task.Chest == null;
                float distance = chestGone ? 0f : Vector3.Distance(companion.transform.position, task.Chest.transform.position);
                bool arrived = chestGone || task.Goal.InReach(distance);
                if (arrived || task.Goal.TimedOut(Time.time))
                {
                    if (arrived && !chestGone)
                    {
                        Dictionary<string, int> got = CompanionBridge.TakeFromChest(companion, task.Chest, task.Items);
                        // Enseguida, porque Offline Companions podría guardarlo camino del siguiente cofre.
                        CompanionBridge.KeepGear(companion, got.Keys);
                        foreach (KeyValuePair<string, int> item in got)
                        {
                            task.Taken.TryGetValue(item.Key, out int before);
                            task.Taken[item.Key] = before + item.Value;
                        }
                    }
                    if (task.Next.Count > 0)
                    {
                        KeyValuePair<Container, Dictionary<string, int>> stop = task.Next.Dequeue();
                        task.Chest = stop.Key;
                        task.Items = stop.Value;
                        task.Goal = new GiveGoal(Time.time);
                        continue;
                    }

                    _takes.Remove(companion);
                    if (task.Taken.Count == 0)
                    {
                        _chains.Remove(companion);
                        string problem = arrived
                            ? ModLang.Pick(name + " no pudo sacar eso del cofre: está en uso, no le cabe o ya no está.", name + " could not take that from the chest: it is in use, does not fit, or is gone.", name + " не может взять это из сундука: сундук занят, нет места или предмет пропал.")
                            : ModLang.Pick(name + " no ha llegado hasta el cofre.", name + " did not reach the chest.", name + " не добирается до сундука.");
                        Log.LogWarning("[Meta] " + problem);
                        Notify(problem);
                        AddToChat(name, problem);
                        continue;
                    }
                    string text = CompanionBridge.DescribeItems(task.Taken);
                    _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("sacar cosas de un cofre", "taking items from a chest", "извлечение предметов из сундука"), Gained = task.Taken };
                    Log.LogInfo("[Meta] " + name + ": saca del cofre " + text);
                    Notify(ModLang.Pick(name + " sacó " + text + ".", name + " took " + text + ".", name + " достаёт: " + text + "."));
                    AddToChat(name, ModLang.Pick("sacó " + text + " del cofre.", "took " + text + " from the chest.", "достаёт " + text + " из сундука."));
                }
                else if (task.Goal.ShouldWalk(Time.time))
                {
                    CompanionBridge.WalkTo(companion, player, task.Chest.transform.position);
                }
            }
        }

        private string StartGuard(Component companion, LookTarget look, ActionRequest action, out string note)
        {
            note = ModLang.Pick(
                "Solo pelea con enemigos a unos 35 m de ti: si te alejas mucho, no defenderá la zona.",
                "Only fights enemies within about 35 m of you: if you move far away, they won't defend the area.",
                "Сражается только с врагами в пределах 35 м от тебя: если отойдёшь далеко, защита зоны прекратится.");
            Player player = Player.m_localPlayer;
            Vector3 anchor = companion.transform.position;
            if (action.Value == CompanionActions.PlaceLookedAt)
            {
                if (look?.Collider == null) return ModLang.Pick("El jugador no está mirando a ningún lugar concreto.", "The player is not looking at any specific place.", "Игрок не смотрит ни на какую точку.");
                anchor = look.Point;
            }

            var stay = new ActionRequest { Kind = ActionKind.GoTo, Value = action.Value, Stay = true };
            string failure = CompanionBridge.Execute(companion, null, player, look, stay, out string _);
            if (failure != null) return failure;
            CompanionBridge.Execute(companion, null, player, look, new ActionRequest { Kind = ActionKind.SetStance, Value = BalancedStance }, out string _);
            _guards[companion] = anchor;
            return null;
        }

        /// <summary>Tras una pelea vuelve al punto que protege. Nunca durante ella, porque la orden de caminar interrumpiría el combate.</summary>
        private void UpdateGuards(Player player)
        {
            foreach (Component companion in new List<Component>(_guards.Keys))
            {
                if (companion == null)
                {
                    AbandonOnDeath(companion, ModLang.Pick("proteger una zona", "guarding an area", "охрана территории"));
                    _guards.Remove(companion);
                    continue;
                }
                // La orden de caminar le hace dejar de pelear: solo tras un rato sin combate y sin enemigos cerca.
                if (companion.GetComponent<Character>().GetHealth() <= 0f || CompanionBridge.IsBusy(companion) || CompanionBridge.EnemyNear(companion, GuardEnemyRadius))
                {
                    _guardCalmSince.Remove(companion);
                    continue;
                }
                if (!_guardCalmSince.TryGetValue(companion, out float calmSince))
                {
                    _guardCalmSince[companion] = Time.time;
                    continue;
                }
                if (Time.time - calmSince < GuardCalmSeconds) continue;

                if (Vector3.Distance(companion.transform.position, _guards[companion]) > GuardRadius)
                {
                    _guardCalmSince.Remove(companion);
                    CompanionBridge.WalkTo(companion, player, _guards[companion]);
                }
            }
        }

        /// <summary>Abandona la pelea y vuelve con el jugador si baja de la salud fijada.</summary>
        private void UpdateRetreats(Player player)
        {
            foreach (Component companion in new List<Component>(_retreats.Keys))
            {
                Character character = companion != null ? companion.GetComponent<Character>() : null;
                if (character == null)
                {
                    _retreats.Remove(companion);
                    continue;
                }
                if (character.GetHealth() <= 0f || character.GetMaxHealth() <= 0f || !CompanionBridge.InCombat(companion)) continue;
                if (_retreatedAt.TryGetValue(companion, out float last) && Time.time - last < RetreatCooldown) continue;
                if (character.GetHealth() / character.GetMaxHealth() * 100f >= _retreats[companion]) continue;

                _retreatedAt[companion] = Time.time;
                ClearGoals(companion);
                _chains.Remove(companion);
                string failure = CompanionBridge.Execute(companion, null, player, null, new ActionRequest { Kind = ActionKind.ComeHere }, out string _);
                string name = CompanionBridge.Name(companion);
                Log.LogInfo("[Meta] " + name + ": se retira" + (failure == null ? "" : " (falló: " + failure + ")"));
                if (failure == null) Notify(ModLang.Pick(name + " se retira: tiene poca salud.", name + " retreats: low on health.", name + " отступает: мало здоровья."));
            }
        }

        private Dictionary<string, int> SelectItems(Component companion, ActionRequest action, out string failure)
        {
            failure = null;
            if (action.Item == CompanionActions.LastTaskItems)
            {
                if (_lastTask.TryGetValue(CompanionBridge.PermanentId(companion), out TaskRecord record) && record.Gained.Count > 0)
                {
                    return new Dictionary<string, int>(record.Gained);
                }
                failure = ModLang.Pick("No le queda nada ganado en su última tarea.", "Has nothing left from their last task.", "От прошлой задачи ничего не осталось.");
                return null;
            }

            Dictionary<string, int> items = CompanionBridge.FindInPack(companion, action.Item, action.Amount);
            if (items.Count == 0)
            {
                failure = ModLang.Pick("No lleva " + action.Item + " en la mochila, o lo lleva equipado.", "Not carrying " + action.Item + " in the pack, or wearing it equipped.", "В инвентаре нет '" + action.Item + "' или предмет надет.");
                return null;
            }
            return items;
        }

        private string StartGiving(Component companion, ActionRequest action)
        {
            Dictionary<string, int> items = SelectItems(companion, action, out string failure);
            if (items == null) return failure;
            _gives[companion] = new GiveTask { Goal = new GiveGoal(Time.time), Items = items };
            return null;
        }

        private string DropItems(Component companion, ActionRequest action, out string note)
        {
            note = null;
            Dictionary<string, int> items = SelectItems(companion, action, out string failure);
            if (items == null) return failure;

            Dictionary<string, int> dropped = CompanionBridge.DropToGround(companion, items);
            if (dropped.Count == 0) return ModLang.Pick("No ha podido soltar nada.", "Could not drop anything.", "Не удалось ничего выбросить.");

            string text = CompanionBridge.DescribeItems(dropped);
            string name = CompanionBridge.Name(companion);
            // Ya soltado, no debe poder entregarse otra vez como "lo de la última tarea".
            _lastTask[CompanionBridge.PermanentId(companion)] = new TaskRecord { Description = ModLang.Pick("soltaste al suelo " + text, "you dropped " + text + " on the ground", "ты сбрасываешь на землю: " + text) };
            Log.LogInfo("[Meta] " + name + ": suelta " + text);
            Notify(ModLang.Pick(name + " ha soltado " + text + ".", name + " dropped " + text + ".", name + " сбрасывает на землю: " + text + "."));
            AddToChat(name, ModLang.Pick("ha soltado " + text + ".", "dropped " + text + ".", "сбрасывает на землю: " + text + "."));
            note = ModLang.Pick("Soltaste " + text + ".", "You dropped " + text + ".", "Ты сбрасываешь на землю: " + text + ".");
            return null;
        }

        /// <summary>Cada fotograma: se acerca al jugador y le entrega los objetos en cuanto llega.</summary>
        private void UpdateGives(Player player)
        {
            if (_gives.Count == 0) return;
            foreach (Component companion in new List<Component>(_gives.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _gives.Remove(companion);
                    continue;
                }

                GiveTask task = _gives[companion];

                // La orden de caminar tiene prioridad sobre las peleas: mientras luche, no se envía y el plazo se detiene.
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Postpone(Time.deltaTime);
                    continue;
                }

                string name = CompanionBridge.Name(companion);
                float distance = Vector3.Distance(companion.transform.position, player.transform.position);

                if (task.Goal.InReach(distance))
                {
                    _gives.Remove(companion);
                    Dictionary<string, int> given = CompanionBridge.HandOver(companion, player, task.Items);
                    string id = CompanionBridge.PermanentId(companion);
                    if (given.Count == 0)
                    {
                        _chains.Remove(companion);
                        string full = ModLang.Pick(
                            name + " no te ha podido dar nada: no te cabe o ya no lo lleva.",
                            name + " could not give you anything: you have no room or they no longer have it.",
                            name + " не может передать тебе предметы: у тебя нет места или у спутника их уже нет.");
                        Log.LogWarning("[Meta] " + full);
                        Notify(full);
                        AddToChat(name, full);
                        FinishedCommentary(companion, ModLang.Pick(
                            "No has podido entregarle nada al jugador: no le cabe o ya no lo llevas.",
                            "You could not hand anything to the player: they have no room or you no longer carry it.",
                            "Не удалось передать вещи игроку: у него нет места или у тебя их уже нет."));
                        continue;
                    }
                    string text = CompanionBridge.DescribeItems(given);
                    // Ya entregado, no debe poder darse otra vez como "lo de la última tarea".
                    _lastTask[id] = new TaskRecord { Description = ModLang.Pick("le diste al jugador " + text, "you gave the player " + text, "ты передаёшь игроку: " + text) };
                    Log.LogInfo("[Meta] " + name + ": entrega " + text);
                    string done = ModLang.Pick(name + " te ha dado " + text + ".", name + " gave you " + text + ".", name + " передаёт тебе: " + text + ".");
                    Notify(done);
                    AddToChat(name, done);
                    FinishedCommentary(companion, ModLang.Pick("Acabas de darle al jugador " + text + ".", "You just gave the player " + text + ".", "Ты только что передаёшь игроку: " + text + "."));
                }
                else if (task.Goal.TimedOut(Time.time))
                {
                    _gives.Remove(companion);
                    _chains.Remove(companion);
                    string late = ModLang.Pick(
                        name + " no ha llegado hasta ti para darte las cosas.",
                        name + " did not reach you in time to hand over the items.",
                        name + " не успевает подойти к тебе, чтобы передать вещи.");
                    Log.LogWarning("[Meta] " + late);
                    Notify(late);
                    AddToChat(name, late);
                }
                else if (task.Goal.ShouldWalk(Time.time))
                {
                    var walk = new ActionRequest { Kind = ActionKind.GoTo, Value = CompanionActions.PlacePlayer };
                    string failure = CompanionBridge.Execute(companion, null, player, null, walk, out string _);
                    if (failure != null)
                    {
                        _gives.Remove(companion);
                        _chains.Remove(companion);
                        Notify(ModLang.Pick(name + " no ha podido ir hasta ti: " + failure, name + " could not come to you: " + failure, name + " не может подойти к тебе: " + failure));
                    }
                }
            }
        }

        /// <summary>Anota qué ganó en una tarea, para que pueda contarlo o entregarlo después.</summary>
        private string RecordTask(Component companion, string description)
        {
            var record = new TaskRecord { Description = description };
            if (_taskStartPack.TryGetValue(companion, out Dictionary<string, int> before))
            {
                record.Gained = CompanionBridge.WithoutGear(InventoryDiff.Gained(before, CompanionBridge.PackContents(companion)));
                _taskStartPack.Remove(companion);
            }
            _lastTask[CompanionBridge.PermanentId(companion)] = record;
            string gained = record.Gained.Count == 0 ? "" : CompanionBridge.DescribeItems(record.Gained);
            int total = 0;
            foreach (int amount in record.Gained.Values) total += amount;
            if (total >= MemorableTaskAmount)
            {
                RememberEvent(companion, MemoryWeight.Job, ModLang.Pick(
                    "Trabajé en " + description + " y conseguí " + gained + ".",
                    "Worked on " + description + " and got " + gained + ".",
                    "Работа над задачей '" + description + "', добыто: " + gained + "."), false);
            }
            return gained;
        }

        /// <summary>Para el compañero o, con forPlayer, para que lo lea el jugador, sin las indicaciones a la IA.</summary>
        private string TaskSummary(Component companion, bool forPlayer = false)
        {
            string text = "";
            string current = _travels.TryGetValue(companion, out TravelTask travel)
                ? TravelGoal.Describe(travel.Label, travel.Tend, Mathf.RoundToInt(FlatDistance(companion.transform.position, travel.Point)), travel.Detouring, travel.Goal.LooksStalled(Time.time))
                : _explores.TryGetValue(companion, out ExploreTask exploring)
                    ? ModLang.Pick(
                        "explorar los alrededores (" + (exploring.Found.Count == 0 ? "sin hallazgos por ahora" : "encontrado: " + string.Join(", ", exploring.Found.ToArray())) + ")",
                        "exploring the surroundings (" + (exploring.Found.Count == 0 ? "nothing found yet" : "found: " + string.Join(", ", exploring.Found.ToArray())) + ")",
                        "разведка окрестностей (" + (exploring.Found.Count == 0 ? "пока ничего не найдено" : "найдено: " + string.Join(", ", exploring.Found.ToArray())) + ")")
                : _gives.ContainsKey(companion) ? ModLang.Pick("ir a darle objetos al jugador", "going to hand items to the player", "подойти и передать вещи игроку")
                : _pickups.TryGetValue(companion, out PickupTask pickup) ? ModLang.Pick("ir a recoger del suelo ", "going to pick up ", "подойти и подобрать с земли: ") + pickup.Label
                : _stores.ContainsKey(companion) ? ModLang.Pick("guardar tus cosas en los cofres", "storing your items in the chests", "сложить вещи в сундуки")
                : _takes.ContainsKey(companion) ? ModLang.Pick("ir a sacar algo de un cofre", "going to take something from a chest", "подойти и взять вещи из сундука")
                : _hunts.TryGetValue(companion, out HuntTask hunt) ? ModLang.Pick("cazar ", "hunting ", "охота: ") + hunt.Label
                : _guards.ContainsKey(companion) ? ModLang.Pick("proteger una zona", "guarding an area", "охрана территории")
                : _organizing.TryGetValue(companion, out UpkeepTask upkeep) ? upkeep.Label
                : _smelts.TryGetValue(companion, out SmeltTask smelting) ? ModLang.Pick("fundir ", "smelting ", "плавка: ") + smelting.Label
                : _cooks.TryGetValue(companion, out GatherGoal cooking) ? ModLang.Pick("cocinar " + cooking.Amount + " de " + cooking.Label + " (llevas " + cooking.Gained + ")", "cooking " + cooking.Amount + " " + cooking.Label + " (have " + cooking.Gained + ")", "готовка: " + cooking.Amount + " шт. " + cooking.Label + " (готово " + cooking.Gained + ")")
                : _plants.ContainsKey(companion) ? ModLang.Pick("romper una planta", "breaking a plant", "сбор растения")
                : _trees.ContainsKey(companion) ? ModLang.Pick("talar un árbol hasta acabar el tronco", "chopping a tree and its logs", "рубка дерева вместе с пнями и брёвнами")
                : _goals.TryGetValue(companion, out GatherGoal goal) ? ModLang.Pick("conseguir " + goal.Amount + " de " + goal.Label + " (llevas " + goal.Gained + ")", "gathering " + goal.Amount + " " + goal.Label + " (have " + goal.Gained + ")", "добыча: " + goal.Amount + " шт. " + goal.Label + " (собрано " + goal.Gained + ")")
                : _huntSweeps.TryGetValue(companion, out HuntSweep sweep)
                    ? (sweep.Exhausted
                        ? ModLang.Pick("cazar, sin presas a tu alcance", "hunting, no prey within reach", "охота: добычи поблизости нет")
                        : ModLang.Pick("cazar, buscando presas por la zona", "hunting, searching for prey in the area", "охота: поиск добычи в окрестностях"))
                : null;
            if (current != null) text += ModLang.Pick("\nTarea en curso: ", "\nCurrent task: ", "\nТекущая задача: ") + current;
            if (_tends.TryGetValue(companion, out TendTask tend))
            {
                text += forPlayer
                    ? ModLang.Pick("\nSe encarga de " + tend.Label + ": ", "\nTending " + tend.Label + ": ", "\nПрисмотр за " + tend.Label + ": ") + TendActivity(companion, tend)
                    : ModLang.Pick(
                        "\nTe encargas de " + tend.Label + " por tu cuenta, sin irte de allí hasta nueva orden. Ahora: " + TendActivity(companion, tend),
                        "\nYou are tending " + tend.Label + " on your own, staying there until ordered otherwise. Right now: " + TendActivity(companion, tend),
                        "\nТы присматриваешь за " + tend.Label + " самостоятельно, оставаясь на месте до нового приказа. Сейчас: " + TendActivity(companion, tend));
            }
            else if (current == null && !_guards.ContainsKey(companion) && !string.IsNullOrEmpty(CompanionBridge.PermanentId(companion)) && MemoryOf(CompanionBridge.PermanentId(companion)).StayLabel is string waiting)
            {
                text += forPlayer
                    ? ModLang.Pick("\nEsperando en ", "\nWaiting at ", "\nОжидание в: ") + waiting
                    : ModLang.Pick("\nEstás esperando en " + waiting + ", donde te dijo el jugador", "\nYou are waiting at " + waiting + ", where the player told you to stay", "\nТы ждёшь в точке '" + waiting + "', как велел игрок");
            }
            if (_retreats.TryGetValue(companion, out int retreat))
            {
                text += ModLang.Pick(
                    "\nTe retiras de las peleas con menos del " + retreat + " % de salud",
                    "\nYou retreat from fights below " + retreat + "% health",
                    "\nТы отступаешь из боя, если здоровье ниже " + retreat + " %");
            }

            if (_lastTask.TryGetValue(CompanionBridge.PermanentId(companion), out TaskRecord last))
            {
                text += ModLang.Pick("\nTu última tarea, ya terminada: ", "\nYour last completed task: ", "\nТвоя последняя выполненная задача: ") + last.Description;
                if (last.Gained.Count > 0) text += ModLang.Pick(". Ganaste ", ". You got ", ". Добыто: ") + CompanionBridge.DescribeItems(last.Gained);
                if (current == null && !forPlayer)
                {
                    text += ModLang.Pick(
                        ". Si el jugador pregunta por ella, cuéntale cómo fue o dale lo que lleves, pero no la repitas salvo que te lo pida",
                        ". If the player asks about it, tell them how it went or hand over what you carry, but do not repeat it unless asked",
                        ". Если игрок спросит о ней, расскажи как прошло или отдай собранное, но не повторяй без его просьбы");
                }
            }
            return text;
        }

        /// <summary>Dónde se le dejó, para la lista de compañeros, que incluye a los que están fuera de la zona cargada.</summary>
        private string StayOf(string id)
        {
            return MemoryOf(id).DescribeStay();
        }

        private void RememberStay(Component companion, Vector3 point, string label, bool tend)
        {
            string id = CompanionBridge.PermanentId(companion);
            if (string.IsNullOrEmpty(id)) return;
            CompanionMemory memory = MemoryOf(id);
            memory.SetStay(label, point.x, point.y, point.z, tend);
            SaveMemory(id, memory);
        }

        /// <summary>Ya no se queda donde se le dejó ni se encarga de ese sitio.</summary>
        private void LeaveStay(Component companion)
        {
            if (_tends.TryGetValue(companion, out TendTask tend))
            {
                DropTend(companion, tend);
                Log.LogInfo("[Encargo] " + (companion != null ? CompanionBridge.Name(companion) : "Un compañero") + " deja de encargarse de " + tend.Label);
            }
            if (companion == null) return;
            string id = CompanionBridge.PermanentId(companion);
            if (string.IsNullOrEmpty(id)) return;
            CompanionMemory memory = MemoryOf(id);
            if (memory.ClearStay()) SaveMemory(id, memory);
        }

        private sealed class TendTask
        {
            public GameObject Worker;
            public Vector3 Point;
            public string Label;
            public readonly TendPlan Plan = new TendPlan();
            public float NextCheckAt;
            public float FollowingSince = -1f;
            public bool WarnedFar;
            public int Side = 1;

            /// <summary>El objeto que ha salido a recoger ahora mismo, o null.</summary>
            public string Chore;
        }

        private void AddTend(Component companion, Vector3 point, string label)
        {
            // Si ya está lejos, al dar la orden se le avisó: no hace falta repetirlo enseguida.
            bool alreadyFar = FlatDistance(Player.m_localPlayer.transform.position, point) >= CompanionBridge.LoadedReach();
            _tends[companion] = new TendTask { Worker = companion.gameObject, Point = point, Label = label, NextCheckAt = Time.time, WarnedFar = alreadyFar };
            BehaviorOverrides.TendArea[companion.gameObject] = point;
        }

        private void DropTend(Component companion, TendTask tend)
        {
            _tends.Remove(companion);
            BehaviorOverrides.TendArea.Remove(tend.Worker);
        }

        private void BeginTend(Component companion, Vector3 point, string label)
        {
            AddTend(companion, point, label);
            RememberStay(companion, point, label, true);
            CompanionBridge.WorkInPlace(companion, CompanionActions.JobUpkeep);
            Log.LogInfo("[Encargo] " + CompanionBridge.Name(companion) + " se encarga de " + label + " en " + point.ToString("F1") + " (inclinación " + Inclinations.Name(InclinationOf(CompanionBridge.PermanentId(companion))) + ")");
        }

        /// <summary>Lo que el jugador debe saber antes de irse, o null. El juego congela lo que queda fuera de la zona cargada.</summary>
        private string TendWarning(Component companion, Vector3 point, string label)
        {
            Player player = Player.m_localPlayer;
            float reach = CompanionBridge.LoadedReach();
            float away = FlatDistance(player.transform.position, point);
            var warnings = new List<string>();
            if (away >= reach)
            {
                warnings.Add(ModLang.Pick(
                    "Estás a " + Mathf.RoundToInt(away) + " m de " + label + ": solo trabajará allí mientras estés a menos de " + Mathf.RoundToInt(reach) + " m.",
                    "You are " + Mathf.RoundToInt(away) + " m from " + label + ": they will only work there while you are within " + Mathf.RoundToInt(reach) + " m.",
                    "Ты в " + Mathf.RoundToInt(away) + " м от точки '" + label + "': спутник работает там, только пока ты ближе " + Mathf.RoundToInt(reach) + " м."));
            }
            string hammer = CompanionBridge.HammerProblem(companion);
            if (hammer != null) warnings.Add(hammer + ModLang.Pick(" No reparará hasta tener uno.", " They will not repair until they have one.", " Ремонт не начнётся без молота."));
            return warnings.Count == 0 ? null : string.Join(" ", warnings.ToArray());
        }

        private string TendActivity(Component companion, TendTask tend)
        {
            float away = FlatDistance(Player.m_localPlayer.transform.position, tend.Point);
            if (away >= CompanionBridge.LoadedReach()) return ModLang.Pick("parado porque estás lejos, a " + Mathf.RoundToInt(away) + " m", "paused because you are far away, at " + Mathf.RoundToInt(away) + " m", "на паузе: ты слишком далеко (" + Mathf.RoundToInt(away) + " м)");
            if (tend.Chore != null)
            {
                return _stores.ContainsKey(companion)
                    ? ModLang.Pick("guardando lo recogido", "storing gathered items", "складывание собранного")
                    : CompanionActions.JobForItem(tend.Chore) == CompanionActions.JobHunt
                        ? ModLang.Pick("cazando cerca", "hunting nearby", "охота поблизости")
                        : ModLang.Pick("recogiendo cerca", "gathering nearby", "сбор поблизости");
            }
            return CompanionBridge.IsOrganizing(companion) ? ModLang.Pick("manteniendo el sitio", "tending the area", "присмотр за территорией") : ModLang.Pick("todo en orden", "all in order", "всё в порядке");
        }

        private void BackToUpkeep(Component companion, TendTask tend)
        {
            tend.Chore = null;
            tend.NextCheckAt = Time.time + TendCheckSeconds;
            CompanionBridge.WorkInPlace(companion, CompanionActions.JobUpkeep);
        }

        private void AbandonTendChore(Component companion, TendTask tend)
        {
            Log.LogInfo("[Encargo] " + CompanionBridge.Name(companion) + " deja de recoger " + tend.Chore + ": hay peligro cerca");
            tend.Plan.Finished(tend.Chore, 0, Time.time);
            _goals.Remove(companion);
            EndStore(companion);
            BackToUpkeep(companion, tend);
        }

        /// <summary>Lo que ha recogido, aunque sea poco, cuenta como bueno. Sin avisos ni comentarios, que gastarían saldo una y otra vez.</summary>
        private void FinishTendGather(Component companion, TendTask tend, GatherGoal goal, GoalState state)
        {
            string name = CompanionBridge.Name(companion);
            tend.Plan.Finished(goal.Item, goal.Gained, Time.time);
            Log.LogInfo("[Encargo] " + name + ": " + goal.Gained + " de " + goal.Amount + " de " + goal.Label + " (" + state + ")");

            int carried = StorePlanner.Units(CompanionBridge.StorableStacks(companion));
            if (goal.Gained > 0 && carried > 0)
            {
                string failure = CompanionBridge.Execute(companion, null, Player.m_localPlayer, null, new ActionRequest { Kind = ActionKind.StoreItems }, out string _);
                if (failure == null && CompanionBridge.IsDepositing(companion))
                {
                    var store = new StoreGoal(carried, Time.time);
                    store.TripStarted(CompanionBridge.DepositChest(companion));
                    _stores[companion] = store;
                    return;
                }
                Log.LogInfo("[Encargo] " + name + " no ha podido guardarlo: " + (failure ?? "no hay cofre disponible"));
            }
            BackToUpkeep(companion, tend);
        }

        /// <summary>Mantiene el sitio con el trabajo de Reborn y, según su inclinación, sale a recoger cerca. Sin peticiones a la IA.</summary>
        private void UpdateTends(Player player)
        {
            if (_tends.Count == 0) return;
            float reach = CompanionBridge.LoadedReach();
            foreach (Component companion in new List<Component>(_tends.Keys))
            {
                TendTask tend = _tends[companion];
                // Descargado o muerto: el encargo sigue en su memoria y se retoma cuando vuelva a cargarse.
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    DropTend(companion, tend);
                    continue;
                }

                string name = CompanionBridge.Name(companion);
                if (CompanionBridge.IsFollowing(companion))
                {
                    // Puede ser un instante al cambiar de trabajo; si sigue, se lo han pedido con la rueda de órdenes.
                    if (tend.FollowingSince < 0f) tend.FollowingSince = Time.time;
                    if (Time.time - tend.FollowingSince < TendFollowGraceSeconds) continue;
                    LeaveStay(companion);
                    _goals.Remove(companion);
                    EndStore(companion);
                    CompanionBridge.StopGathering(companion);
                    Notify(ModLang.Pick(
                        name + " deja de encargarse de " + tend.Label + " y te sigue.",
                        name + " stops tending " + tend.Label + " and follows you.",
                        name + " прекращает присматривать за " + tend.Label + " и следует за тобой."));
                    continue;
                }
                tend.FollowingSince = -1f;

                float away = FlatDistance(player.transform.position, tend.Point);
                if (away >= reach && !tend.WarnedFar)
                {
                    tend.WarnedFar = true;
                    string far = ModLang.Pick(
                        "Te has alejado de " + tend.Label + ": " + name + " no hará nada allí hasta que vuelvas.",
                        "You moved far from " + tend.Label + ": " + name + " will not do anything there until you return.",
                        "Ты отходишь далеко от " + tend.Label + ": " + name + " приостанавливает работу, пока ты не вернёшься.");
                    Notify(far);
                    AddToChat(name, far);
                }
                else if (away < reach * TendWarnAgainShare)
                {
                    tend.WarnedFar = false;
                }

                // Si algo ajeno le quitó lo que estaba recogiendo, no debe quedarse esperándolo para siempre.
                if (tend.Chore != null && !_goals.ContainsKey(companion) && !_stores.ContainsKey(companion)) BackToUpkeep(companion, tend);
                if (tend.Chore != null || Time.time < tend.NextCheckAt) continue;
                tend.NextCheckAt = Time.time + TendCheckSeconds;
                if (CompanionBridge.InCombat(companion) || CompanionBridge.EnemyNear(companion, Initiative.AlertRange)) continue;
                if (CompanionBridge.IsOrganizing(companion)) continue;

                if (FlatDistance(companion.transform.position, tend.Point) > TendPlan.AreaMeters + TendLeashMargin)
                {
                    if (CompanionBridge.NextStep(companion, tend.Point, TravelGoal.StepDistance, ref tend.Side, out Vector3 next, out bool _)) CompanionBridge.KeepWalking(companion, next);
                    continue;
                }

                string item = BehaviorOverrides.TendAreaAvailable ? TendGatherNow(companion, player, tend) : null;
                if (item == null)
                {
                    if (!CompanionBridge.OnJob(companion, CompanionActions.JobUpkeep)) CompanionBridge.WorkInPlace(companion, CompanionActions.JobUpkeep);
                    continue;
                }

                ActionRequest order = CompanionActions.GatherOrder(item, TendPlan.GatherAmount);
                if (order == null)
                {
                    tend.Plan.Finished(item, 0, Time.time);
                    continue;
                }
                int packBefore = CompanionBridge.CountInPack(companion, order.Item);
                string failure = CompanionBridge.Execute(companion, null, player, null, order, out string _);
                if (failure != null)
                {
                    tend.Plan.Finished(item, 0, Time.time);
                    Log.LogInfo("[Encargo] " + name + " no puede recoger " + order.ItemLabel + ": " + failure);
                    continue;
                }
                tend.Chore = item;
                _goals[companion] = new GatherGoal(order.Item, order.ItemLabel, order.Amount, packBefore, Time.time);
                Log.LogInfo("[Encargo] " + name + " sale a recoger " + order.Amount + " de " + order.ItemLabel + " cerca de " + tend.Label);
            }
        }

        private string TendGatherNow(Component companion, Player player, TendTask tend)
        {
            Inclination inclination = InclinationOf(CompanionBridge.PermanentId(companion));
            if (inclination == Inclination.Ninguna) return null;
            bool hunts = inclination == Inclination.Caza;
            if (hunts && !BehaviorOverrides.TendHuntAvailable) return null;

            InitiativeFacts facts = CompanionBridge.Facts(companion, player);
            CompanionBridge.ResourcesNear(tend.Point, TendPlan.AreaMeters, out bool trees, out bool rocks, out string plant);
            return tend.Plan.NextGather(inclination, new TendSituation
            {
                Night = facts.Night,
                PackFull = facts.PackFull,
                NoAxe = facts.NoAxe,
                NoPickaxe = facts.NoPickaxe,
                Trees = trees,
                Rocks = rocks,
                Plant = plant,
                Prey = hunts ? CompanionBridge.PreyNear(companion, tend.Point, TendPlan.AreaMeters) : null,
                WoodStored = facts.WoodStored,
                StoneStored = facts.StoneStored,
                FoodStored = facts.FoodStored
            }, Time.time);
        }

        /// <summary>El lugar de expedición que cae en esa posición, o null. La base y lo que queda junto al hogar no son una expedición.</summary>
        private string ExpeditionPlaceAt(Vector3 position)
        {
            if (ZNet.instance == null || Player.m_localPlayer == null) return null;
            PlaceMark nearest = null;
            float best = ExpeditionText.NearbyMeters;
            foreach (PlaceMark place in Places().Places)
            {
                float distance = FlatDistance(position, new Vector3(place.X, place.Y, place.Z));
                if (distance <= best && IsExpedition(place))
                {
                    best = distance;
                    nearest = place;
                }
            }
            return nearest?.Name;
        }

        private static bool IsExpedition(PlaceMark place)
        {
            if (PlaceBook.IsHomeWord(place.Name)) return false;
            Vector3 at = new Vector3(place.X, place.Y, place.Z);
            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                if (CompanionBridge.HomePosition(companion, out Vector3 home) && FlatDistance(at, home) <= NearHomeMeters) return false;
            }
            return true;
        }

        /// <summary>La primera llegada de cada compañero a cada lugar. Volver no deja recuerdo, y se redacta sin gastar saldo.</summary>
        private void UpdateExpeditions(Player player)
        {
            if (Time.time < _nextExpeditionCheckAt) return;
            _nextExpeditionCheckAt = Time.time + ExpeditionCheckSeconds;
            List<PlaceMark> places = Places().Places;
            if (places.Count == 0) return;

            List<Component> companions = new List<Component>(CompanionBridge.MyCompanions());
            foreach (Component companion in companions)
            {
                foreach (PlaceMark place in places)
                {
                    Vector3 at = new Vector3(place.X, place.Y, place.Z);
                    if (FlatDistance(companion.transform.position, at) > ExpeditionText.ArrivalMeters || !IsExpedition(place)) continue;
                    string id = CompanionBridge.PermanentId(companion);
                    if (string.IsNullOrEmpty(id) || !MemoryOf(id).MarkVisited(place.Name)) continue;

                    var with = new List<string>();
                    if (FlatDistance(player.transform.position, at) <= ExpeditionText.NearbyMeters) with.Add(player.GetPlayerName());
                    foreach (Component other in companions)
                    {
                        if (other != companion && FlatDistance(other.transform.position, at) <= ExpeditionText.NearbyMeters) with.Add(CompanionBridge.Name(other));
                    }
                    // Aunque ya estuviera en sus vivencias, hay que guardar que ha estado.
                    SaveMemory(id, MemoryOf(id));
                    RememberEvent(companion, MemoryWeight.Visit, ExpeditionText.Arrival(place.Name, with), true);
                }
            }
        }

        private string StartPlantTask(Component companion, IList<Component> group, LookTarget look, Destructible plant, out string note)
        {
            note = null;
            var walk = new ActionRequest { Kind = ActionKind.GoTo, Value = CompanionActions.PlaceLookedAt };
            foreach (Component worker in group ?? new[] { companion })
            {
                string failure = CompanionBridge.Execute(worker, null, Player.m_localPlayer, look, walk, out string _);
                if (failure != null) return failure;
                _plants[worker] = new PlantTask { Goal = new PlantGoal(Time.time), Plant = plant, Look = look, HealthAtStart = worker.GetComponent<Character>().GetHealth() };
            }
            return null;
        }

        private sealed class PlantTask
        {
            public PlantGoal Goal;
            public Destructible Plant;
            public float HealthAtStart;
            public float BrokenAt = -1f;
            public LookTarget Look;
            public float ResumeAt = -1f;
        }

        private bool HoldForPlant(Component companion)
        {
            // Una vez rota, debe poder moverse para recoger lo que ha soltado. Interrumpida, debe poder pelear.
            return _plants.TryGetValue(companion, out PlantTask task) && task.Goal.InReach && task.BrokenAt < 0f && task.ResumeAt < 0f;
        }

        /// <summary>Cada fotograma, porque la mirada debe seguir apuntando a la planta durante todo el golpe.</summary>
        private void UpdatePlants()
        {
            if (_plants.Count == 0) return;
            foreach (Component companion in new List<Component>(_plants.Keys))
            {
                Character character = companion != null ? companion.GetComponent<Character>() : null;
                if (character == null || character.GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, ModLang.Pick("romper una planta", "breaking a plant", "сбор растения"));
                    _plants.Remove(companion);
                    continue;
                }

                PlantTask task = _plants[companion];
                string name = CompanionBridge.Name(companion);

                if (task.BrokenAt >= 0f)
                {
                    if (Time.time - task.BrokenAt < PlantPickupSeconds) continue;
                    _plants.Remove(companion);
                    string gained = RecordTask(companion, ModLang.Pick("romper una planta", "breaking a plant", "сбор растения"));
                    Log.LogInfo("[Meta] " + name + ": planta rota" + (gained.Length > 0 ? ", gana " + gained : ""));
                    string summary = name + (gained.Length > 0
                        ? ModLang.Pick(" ha conseguido " + gained + ".", " got " + gained + ".", " добывает " + gained + ".")
                        : ModLang.Pick(" ha roto la planta, pero no ha recogido nada.", " broke the plant, but picked up nothing.", " срывает растение, но ничего не подобрано."));
                    Notify(summary);
                    AddToChat(name, summary);
                    FinishedCommentary(companion, gained.Length > 0
                        ? ModLang.Pick("Has roto la planta y has conseguido " + gained + ".", "You broke the plant and got " + gained + ".", "Ты срываешь растение и получаешь: " + gained + ".")
                        : ModLang.Pick("Has roto la planta, pero no has recogido nada.", "You broke the plant, but picked up nothing.", "Ты срываешь растение, но ничего не подобрано."));
                    continue;
                }

                if (character.IsSwimming())
                {
                    _plants.Remove(companion);
                    _chains.Remove(companion);
                    Log.LogInfo("[Meta] " + name + ": planta abandonada porque está nadando");
                    continue;
                }

                // Con la IA en pausa no se defendería: al recibir daño o entrar en combate le devuelve el control, y luego retoma.
                bool hurt = character.GetHealth() < task.HealthAtStart;
                task.HealthAtStart = character.GetHealth();
                if (hurt || CompanionBridge.InCombat(companion))
                {
                    task.ResumeAt = Time.time + PlantResumeDelay;
                    task.Goal.ReleaseReach();
                }
                if (task.ResumeAt >= 0f)
                {
                    task.Goal.Postpone(Time.deltaTime);
                    if (Time.time < task.ResumeAt) continue;

                    task.ResumeAt = -1f;
                    if (task.Plant != null)
                    {
                        var back = new ActionRequest { Kind = ActionKind.GoTo, Value = CompanionActions.PlaceLookedAt };
                        string problem = CompanionBridge.Execute(companion, null, Player.m_localPlayer, task.Look, back, out string _);
                        if (problem != null)
                        {
                            _plants.Remove(companion);
                            _chains.Remove(companion);
                            Notify(ModLang.Pick(name + " no ha podido volver a la planta: " + problem, name + " could not return to the plant: " + problem, name + " не может вернуться к растению: " + problem));
                            continue;
                        }
                        Log.LogInfo("[Meta] " + name + ": retoma la planta tras la interrupción");
                    }
                }

                float distance = task.Plant != null ? CompanionBridge.DistanceTo(companion, task.Plant) : 0f;
                PlantStep step = task.Goal.Next(task.Plant != null, distance, Time.time);
                if (task.Plant != null && task.Goal.InReach) CompanionBridge.AimAt(companion, task.Plant);

                if (step == PlantStep.Swing)
                {
                    CompanionBridge.Swing(companion);
                }
                else if (step == PlantStep.Done)
                {
                    task.BrokenAt = Time.time;
                }
                else if (step == PlantStep.TimedOut)
                {
                    _plants.Remove(companion);
                    _chains.Remove(companion);
                    Log.LogWarning("[Meta] " + name + ": no pudo romper la planta");
                    string problem = ModLang.Pick(name + " no ha podido romper esa planta.", name + " could not break that plant.", name + " не может сорвать это растение.");
                    Notify(problem);
                    AddToChat(name, problem);
                }
            }
        }

        private sealed class TreeTask
        {
            public TreeGoal Goal;
            public GameObject Tree;
            public Vector3 Position;
        }

        /// <summary>Sigue con el tronco y el tocón tras caer el árbol, y termina cuando no queda nada de él.</summary>
        private void UpdateTrees()
        {
            if (_trees.Count == 0 || Time.time < _nextTreeCheckAt) return;
            _nextTreeCheckAt = Time.time + TreeCheckInterval;

            foreach (Component companion in new List<Component>(_trees.Keys))
            {
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, ModLang.Pick("talar un árbol", "chopping a tree", "рубка дерева"));
                    _trees.Remove(companion);
                    continue;
                }

                TreeTask task = _trees[companion];
                if (CompanionBridge.InCombat(companion))
                {
                    task.Goal.Postpone(TreeCheckInterval);
                    continue;
                }

                bool standing = task.Tree != null;
                if (standing && CompanionBridge.EnsureTreeTarget(companion, task.Tree)) task.Goal.Retargets++;

                bool cannotReach = task.Goal.Retargets > TreeGoal.MaxRetargets;
                GoalState state = cannotReach
                    ? GoalState.Stuck
                    : task.Goal.Update(standing, CompanionBridge.TreePiecesNear(task.Position, TreeWorkRadius), Time.time);
                if (state == GoalState.Working) continue;

                _trees.Remove(companion);
                CompanionBridge.StopGathering(companion);
                string name = CompanionBridge.Name(companion);
                Log.LogInfo("[Meta] " + name + ": árbol " + state);
                string gained = RecordTask(companion, ModLang.Pick("talar un árbol", "chopping a tree", "рубка дерева"));
                if (state == GoalState.Reached)
                {
                    string summary = ModLang.Pick(
                        name + " ha terminado de talar" + (gained.Length > 0 ? " y ha conseguido " + gained + "." : "."),
                        name + " finished chopping" + (gained.Length > 0 ? " and got " + gained + "." : "."),
                        name + " завершает рубку" + (gained.Length > 0 ? " и получает: " + gained + "." : "."));
                    Notify(summary);
                    AddToChat(name, summary);
                    FinishedCommentary(companion, ModLang.Pick(
                        "Has terminado de talar el árbol, incluido el tronco, y vuelves con el jugador." + (gained.Length > 0 ? " Has conseguido " + gained + "." : ""),
                        "You finished chopping the tree, including the logs, and are returning to the player." + (gained.Length > 0 ? " You got " + gained + "." : ""),
                        "Ты срубаешь дерево вместе с брёвнами и возвращаешься к игроку." + (gained.Length > 0 ? " Добыто: " + gained + "." : "")));
                }
                else
                {
                    string problem = ModLang.Pick(name + " no ha podido terminar de talar ese árbol.", name + " could not finish chopping that tree.", name + " не может закончить рубку этого дерева.");
                    Notify(problem);
                    AddToChat(name, problem);
                    _chains.Remove(companion);
                    FinishedCommentary(companion, ModLang.Pick(
                        "No has podido terminar de talar el árbol y vuelves con el jugador.",
                        "You could not finish chopping the tree and are returning to the player.",
                        "Не удалось закончить рубку дерева, возвращаешься к игроку."));
                }
            }
        }

        private void FinishedCommentary(Component companion, string description, bool canResolve = false)
        {
            if (!_settings.ReactionsEnabled.Value || !_settings.ReactGoal.Value) return;
            _reactions.Add(new Reaction { Kind = ReactionKind.GoalFinished, Speaker = companion, CanResolve = canResolve, Description = description }, Time.time);
        }

        private void ClearGoals(Component companion)
        {
            _goals.Remove(companion);
            _trees.Remove(companion);
            _plants.Remove(companion);
            _gives.Remove(companion);
            _takes.Remove(companion);
            _pickups.Remove(companion);
            EndStore(companion);
            _hunts.Remove(companion);
            _guards.Remove(companion);
            _travels.Remove(companion);
            _explores.Remove(companion);
            _huntSweeps.Remove(companion);
            LeaveStay(companion);
            _taskStartPack.Remove(companion);
            EndCook(companion);
            EndOrganizing(companion);
            EndSmelt(companion);
        }

        /// <summary>
        /// Si no tiene otra cosa que hacer, va a por lo que haya suelto de ese objeto, también lo soltado por un jugador,
        /// que Offline Companions ignora. Devuelve true si hay algo a por lo que ir.
        /// </summary>
        private bool FetchLooseItem(Component companion, GatherGoal goal)
        {
            ItemDrop drop = CompanionBridge.FindDropped(companion, goal.Item, GatherSearchRange);
            if (drop == null) return false;
            if (_tends.TryGetValue(companion, out TendTask tend) && FlatDistance(drop.transform.position, tend.Point) > TendPlan.AreaMeters) return false;

            if (Vector3.Distance(companion.transform.position, drop.transform.position) <= LoosePickupDistance)
            {
                if (CompanionBridge.PickUp(companion, drop)) Log.LogInfo("[Meta] " + CompanionBridge.Name(companion) + ": recoge del suelo " + goal.Label);
                return true;
            }

            if (!_looseWalkAt.TryGetValue(companion, out float next) || Time.time >= next)
            {
                _looseWalkAt[companion] = Time.time + LooseRewalkSeconds;
                CompanionBridge.WalkTo(companion, Player.m_localPlayer, drop.transform.position);
            }
            return true;
        }

        /// <summary>Atrapado así no obedece ninguna orden, ni "ven aquí", y tampoco se teletransporta al jugador, que se queda sin él.</summary>
        private void UpdateWaterTraps(Player player)
        {
            float tired = CompanionBridge.SwimTiredShare();
            float now = Time.time;
            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                if (!_waterTraps.TryGetValue(companion, out WaterTrapWatch watch)) _waterTraps[companion] = watch = new WaterTrapWatch();
                float stamina = CompanionBridge.StaminaShare(companion);
                string name = CompanionBridge.Name(companion);
                bool wasDry = watch.StayDry(now);
                bool wasOnLongRest = watch.OnLongRest(now);
                bool trapped = watch.Update(companion.GetComponent<Character>().IsSwimming(), stamina, tired, now);

                bool stayDry = watch.StayDry(now);
                if (stayDry) BehaviorOverrides.KeepDry.Add(companion.gameObject);
                else BehaviorOverrides.KeepDry.Remove(companion.gameObject);
                if (stayDry != wasDry) Log.LogInfo("[Agua] " + name + (stayDry ? ": cansado nadando en " + companion.transform.position.ToString("F1") + ", aguante " + stamina.ToString("P0") + ": no vuelve al agua hasta recuperarse" : ": ya puede volver a nadar, aguante " + stamina.ToString("P0")));
                if (!wasOnLongRest && watch.OnLongRest(now))
                {
                    Log.LogInfo("[Agua] " + name + ": ha vuelto agotado " + WaterTrapWatch.RetreatsToLongRest + " veces, no nada durante " + WaterTrapWatch.LongRestSeconds.ToString("F0") + " s");
                    Notify(ModLang.Pick(
                        name + " está agotado de nadar y esperará un rato en la orilla.",
                        name + " is exhausted from swimming and will wait on shore for a while.",
                        name + " выбивается из сил от плавания и передохнёт на берегу."));
                }

                if (!trapped) continue;
                watch.Reset();
                // Nadando hacia un barco puede tardar lo que tarde el barco en parar; el jugador sabrá dónde lo ha dejado.
                if (player.GetStandingOnShip() != null) continue;

                string report = CompanionBridge.LeaveWater(companion, player);
                if (report == null)
                {
                    Log.LogInfo("[Agua] " + name + ": atrapado en el agua, pero no hay dónde sacarlo");
                    continue;
                }
                Log.LogInfo("[Agua] " + name + ": atrapado nadando, aguante " + stamina.ToString("P0") + "; " + report);
                Notify(ModLang.Pick(
                    name + " no conseguía salir del agua y ha aparecido en tierra.",
                    name + " could not get out of the water and appeared on dry land.",
                    name + " не мог выбраться из воды и перенесён на сушу."));
            }
        }

        private sealed class TravelTask
        {
            public TravelGoal Goal;
            public Vector3 Point;
            public string Label;
            public ZDOID Id;
            public int Side = 1;
            public bool Detouring;
            public bool Tend;
            public string MemoryId;
        }

        private PlaceBook Places()
        {
            string world = ZNet.instance.GetWorldName() + "_" + ZNet.instance.GetWorldUID();
            if (_places != null && _placesWorld == world) return _places;
            _places = _placeFiles.Load(world, out string problem);
            _placesWorld = world;
            if (problem != null) Log.LogWarning("[Lugares] " + problem);
            return _places;
        }

        private string SavePlaces()
        {
            try
            {
                _placeFiles.Save(_placesWorld, _places);
                return null;
            }
            catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
            {
                Log.LogWarning("[Lugares] No se pudieron guardar: " + ex.Message);
                return ModLang.Pick(
                    "No se ha podido guardar en el disco, así que se perderá al salir del mundo.",
                    "Could not save to disk, so it will be lost when leaving the world.",
                    "Не удалось сохранить на диск, изменения пропадут при выходе из мира.");
            }
        }

        private string MarkPlace(string name, Vector3 point)
        {
            PlaceBook book = Places();
            PlaceMark old = book.Exact(name);
            if (old != null) Minimap.instance?.RemovePin(new Vector3(old.X, old.Y, old.Z), PinRemoveRadius);
            if (!book.Set(name, point.x, point.y, point.z))
            {
                return ModLang.Pick(
                    "Ya hay " + PlaceBook.MaxPlaces + " lugares marcados: hay que borrar alguno antes.",
                    "There are already " + PlaceBook.MaxPlaces + " marked places: delete one first.",
                    "Уже отмечено максимум мест (" + PlaceBook.MaxPlaces + "): сначала удали ненужные.");
            }
            Minimap.instance?.AddPin(point, Minimap.PinType.Icon3, name, true, false);
            Log.LogInfo("[Lugares] Marcado " + name + " en " + point.ToString("F1"));
            return SavePlaces();
        }

        private string MarkOrdered(LookTarget look, ActionRequest action)
        {
            if (action.Value != CompanionActions.MarkLookedAt) return MarkPlace(action.Item, Player.m_localPlayer.transform.position);
            if (look?.Collider == null) return ModLang.Pick("El jugador no está mirando a ningún lugar concreto.", "The player is not looking at any specific place.", "Игрок не смотрит ни на какую точку.");
            return MarkPlace(action.Item, look.Point);
        }

        private string ForgetPlace(string name)
        {
            PlaceBook book = Places();
            PlaceMark place = book.Find(name);
            if (place == null) return ModLang.Pick("No hay ningún lugar marcado que se llame " + name + ".", "No marked place named " + name + ".", "Нет отмеченного места с именем '" + name + "'.");
            book.Places.Remove(place);
            Minimap.instance?.RemovePin(new Vector3(place.X, place.Y, place.Z), PinRemoveRadius);
            Log.LogInfo("[Lugares] Borrado " + place.Name);
            return SavePlaces();
        }

        private string MarkFromCommand(string name)
        {
            if (!CompanionActions.IsValidName(name))
            {
                return ModLang.Pick(
                    "Escribe " + MarkCommand + " y un nombre corto, solo con letras, números y espacios.",
                    "Type " + MarkCommandEn + " followed by a short name using only letters, numbers, and spaces.",
                    "Напиши " + MarkCommandRu + " и короткое имя (буквы, цифры и пробелы).");
            }
            return MarkPlace(name, Player.m_localPlayer.transform.position) ?? ModLang.Pick("Lugar marcado aquí: " + name, "Marked place here: " + name, "Место отмечено здесь: " + name);
        }

        private string ForgetFromCommand(string name)
        {
            if (name.Length == 0) return ModLang.Pick("Escribe " + ForgetCommand + " y el nombre del lugar.", "Type " + ForgetCommandEn + " followed by the place name.", "Напиши " + ForgetCommandRu + " и название места.");
            return ForgetPlace(name) ?? ModLang.Pick("Lugar borrado: " + name, "Removed place: " + name, "Место удалено: " + name);
        }

        private string ListPlaces(Player player)
        {
            List<PlaceMark> places = Places().Places;
            if (places.Count == 0)
            {
                return ModLang.Pick(
                    "No hay lugares marcados. Para marcar uno: " + MarkCommand + " nombre",
                    "No marked places. To mark one: " + MarkCommandEn + " name",
                    "Нет отмеченных мест. Чтобы отметить: " + MarkCommandRu + " имя");
            }
            return ModLang.Pick("Lugares marcados: ", "Marked places: ", "Отмеченные места: ")
                + string.Join(", ", places.ConvertAll(place => place.Name + ModLang.Pick(" (a " + DistanceTo(player, place) + " m)", " (" + DistanceTo(player, place) + " m away)", " (в " + DistanceTo(player, place) + " м)")).ToArray());
        }

        private static int DistanceTo(Player player, PlaceMark place)
        {
            return Mathf.RoundToInt(FlatDistance(player.transform.position, new Vector3(place.X, place.Y, place.Z)));
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y;
            return Vector3.Distance(a, b);
        }

        /// <summary>Solo aparece si hay algo a donde ir, para no gastar saldo en cada mensaje.</summary>
        private string PlacesSummary(Component companion)
        {
            var parts = Places().Places.ConvertAll(place => place.Name + ModLang.Pick(" (a " + DistanceTo(Player.m_localPlayer, place) + " m)", " (" + DistanceTo(Player.m_localPlayer, place) + " m away)", " (в " + DistanceTo(Player.m_localPlayer, place) + " м)"));
            if (CompanionBridge.HomePosition(companion, out Vector3 home))
            {
                int homeDist = Mathf.RoundToInt(FlatDistance(Player.m_localPlayer.transform.position, home));
                parts.Add(ModLang.Pick(
                    HomePlaceLabel + ", basta con decir hogar (a " + homeDist + " m)",
                    HomePlaceLabel + ", can just say home (" + homeDist + " m away)",
                    HomePlaceLabel + ", можно просто сказать дом (в " + homeDist + " м)"));
            }
            return parts.Count == 0 ? "" : ModLang.Pick("\nLugares marcados: ", "\nMarked places: ", "\nОтмеченные места: ") + string.Join(", ", parts.ToArray());
        }

        private bool FindPlace(Component companion, string name, out string label, out Vector3 point)
        {
            PlaceMark place = Places().Find(name);
            if (place != null)
            {
                label = place.Name;
                point = new Vector3(place.X, place.Y, place.Z);
                return true;
            }
            label = HomePlaceLabel;
            return CompanionBridge.HomePosition(companion, out point) && PlaceBook.IsHomeWord(name);
        }

        private string StartTravel(Component companion, ActionRequest action, out string note)
        {
            note = null;
            if (action.Tend && action.Item.Length == 0)
            {
                Vector3 here = companion.transform.position;
                note = TendWarning(companion, here, TendHereLabel);
                BeginTend(companion, here, TendHereLabel);
                if (note != null) Notify(note);
                return null;
            }
            if (!FindPlace(companion, action.Item, out string label, out Vector3 point))
            {
                return ModLang.Pick(
                    "No conoce ningún lugar llamado " + action.Item + ". " + ListPlaces(Player.m_localPlayer),
                    "Does not know any place named " + action.Item + ". " + ListPlaces(Player.m_localPlayer),
                    "Неизвестное место '" + action.Item + "'. " + ListPlaces(Player.m_localPlayer));
            }
            if (action.Tend)
            {
                note = TendWarning(companion, point, label);
                if (note != null) Notify(note);
            }
            int side = 1;
            CompanionBridge.NextStep(companion, point, TravelGoal.StepDistance, ref side, out Vector3 step, out bool detour);
            string failure = CompanionBridge.StartJourney(companion, Player.m_localPlayer, step);
            if (failure != null) return failure;
            _travels[companion] = new TravelTask { Goal = new TravelGoal(Time.time), Point = point, Label = label, Id = companion.GetComponent<ZNetView>().GetZDO().m_uid, Side = side, Detouring = detour, Tend = action.Tend, MemoryId = CompanionBridge.PermanentId(companion) };
            Log.LogInfo("[Viaje] " + CompanionBridge.Name(companion) + ": va a " + label + " en " + point.ToString("F1") + ", a " + Mathf.RoundToInt(FlatDistance(companion.transform.position, point)) + " m");
            return null;
        }

        /// <summary>Camina mientras el juego lo tenga cargado y, si no, se adelanta. Sin peticiones a la IA, salvo el comentario final.</summary>
        private void UpdateTravels(Player player)
        {
            if (_travels.Count == 0) return;
            float reach = CompanionBridge.LoadedReach();
            foreach (Component companion in new List<Component>(_travels.Keys))
            {
                TravelTask task = _travels[companion];
                if (companion == null)
                {
                    // El jugador se ha ido antes de tiempo, por ejemplo por un portal. Si estuviera muerto ya no existiría su registro.
                    _travels.Remove(companion);
                    ZDO left = ZDOMan.instance?.GetZDO(task.Id);
                    if (left != null) left.SetPosition(task.Point + Vector3.up * MarkAboveGround);
                    if (left != null && !string.IsNullOrEmpty(task.MemoryId))
                    {
                        CompanionMemory memory = MemoryOf(task.MemoryId);
                        memory.SetStay(task.Label, task.Point.x, task.Point.y, task.Point.z, task.Tend);
                        SaveMemory(task.MemoryId, memory);
                    }
                    Log.LogInfo("[Viaje] Se quedó sin cargar de camino a " + task.Label + (left != null ? ": ya está allí" : ": ya no existe"));
                    continue;
                }
                if (companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _travels.Remove(companion);
                    continue;
                }

                string name = CompanionBridge.Name(companion);
                Vector3 here = companion.transform.position;
                float toPlayer = FlatDistance(here, player.transform.position);
                TravelStep step = task.Goal.Update(FlatDistance(here, task.Point), toPlayer, reach, Time.time);
                if (step == TravelStep.Walk)
                {
                    // Sin paso no se le manda nada: si de verdad no hay por dónde, ya saltará por no avanzar.
                    bool canWalk = CompanionBridge.NextStep(companion, task.Point, TravelGoal.StepDistance, ref task.Side, out Vector3 next, out bool detour);
                    if (canWalk) CompanionBridge.KeepWalking(companion, next);
                    if (detour != task.Detouring)
                    {
                        task.Detouring = detour;
                        if (detour) Log.LogInfo("[Viaje] " + name + ": el agua corta el paso en " + here.ToString("F1") + (canWalk ? ", la rodea por " + next.ToString("F1") : " y no ve por dónde rodearla"));
                    }
                }

                if (step == TravelStep.Arrived)
                {
                    _travels.Remove(companion);
                    if (task.Tend) BeginTend(companion, task.Point, task.Label);
                    else RememberStay(companion, task.Point, task.Label, false);
                    string arrived = ModLang.Pick(
                        name + " ha llegado a " + task.Label + (task.Tend ? " y se encarga del sitio." : "."),
                        name + " arrived at " + task.Label + (task.Tend ? " and is tending the area." : "."),
                        name + " прибывает в точку '" + task.Label + "'" + (task.Tend ? " и присматривает за местом." : "."));
                    Log.LogInfo("[Viaje] " + arrived);
                    Notify(arrived);
                    AddToChat(name, arrived);
                    FinishedCommentary(companion, ModLang.Pick(
                        "Has llegado a " + task.Label + (task.Tend ? " y te encargas del sitio por tu cuenta." : " y te quedas allí."),
                        "You arrived at " + task.Label + (task.Tend ? " and are tending the area on your own." : " and are staying there."),
                        "Ты приходишь в точку '" + task.Label + "'" + (task.Tend ? " и самостоятельно присматриваешь за местом." : " и остаёшься там.")));
                }
                else if (step == TravelStep.Jump)
                {
                    _travels.Remove(companion);
                    bool staysLoaded = FlatDistance(task.Point, player.transform.position) < reach;
                    CompanionBridge.PlaceAt(companion, task.Point + Vector3.up * MarkAboveGround);
                    if (task.Tend) BeginTend(companion, task.Point, task.Label);
                    else RememberStay(companion, task.Point, task.Label, false);
                    Log.LogInfo("[Viaje] " + name + ": se adelanta a " + task.Label + " desde " + here.ToString("F1") + ": " + (toPlayer >= reach ? "el juego dejaría de verlo, a " + Mathf.RoundToInt(toPlayer) + " m del jugador" : "no avanza"));
                    bool pending = _chains.TryGetValue(companion, out OrderSequence<QueuedOrder> chain) && chain.Pending > 0;
                    string gone = ModLang.Pick(
                        name + " se adelanta" + (task.Tend ? " y se encargará de " + task.Label + (staysLoaded ? "." : " cuando estés cerca.") : " y te esperará en " + task.Label + ".") + (pending && !staysLoaded ? " Lo demás que le pediste se queda sin hacer." : ""),
                        name + " goes ahead" + (task.Tend ? " and will tend " + task.Label + (staysLoaded ? "." : " when you are nearby.") : " and will wait for you at " + task.Label + ".") + (pending && !staysLoaded ? " The rest of your queued orders were skipped." : ""),
                        name + " выдвигается вперёд" + (task.Tend ? " и будет присматривать за '" + task.Label + "'" + (staysLoaded ? "." : ", когда ты будешь рядом.") : " и подождёт тебя в точке '" + task.Label + "'.") + (pending && !staysLoaded ? " Остальные поручения из очереди пропущены." : ""));
                    if (!staysLoaded) _chains.Remove(companion);
                    Notify(gone);
                    AddToChat(name, gone);
                }
            }
        }

        /// <summary>Comprueba las metas de recolección. Sin peticiones a la IA, salvo el comentario final.</summary>
        private void UpdateGoals()
        {
            if (_goals.Count == 0) return;
            foreach (Component companion in new List<Component>(_goals.Keys))
            {
                GatherGoal goal = _goals[companion];
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    AbandonOnDeath(companion, ModLang.Pick(
                        "conseguir " + goal.Amount + " de " + goal.Label + " (llevaba " + goal.Gained + ")",
                        "gathering " + goal.Amount + " " + goal.Label + " (had " + goal.Gained + ")",
                        "добыча: " + goal.Amount + " шт. " + goal.Label + " (было " + goal.Gained + ")"));
                    _goals.Remove(companion);
                    continue;
                }

                int pack = CompanionBridge.CountInPack(companion, goal.Item);
                // Mientras caza o busca presas también está trabajando, aunque aún no haya conseguido nada.
                bool hunting = _huntSweeps.TryGetValue(companion, out HuntSweep sweep) && (!sweep.Exhausted || CompanionBridge.IsHuntMoving(companion));
                bool working = CompanionBridge.IsWorking(companion) || FetchLooseItem(companion, goal) || hunting;
                GoalState state = goal.Update(pack, working, Time.time);
                if (state == GoalState.Working) continue;

                _goals.Remove(companion);
                if (_tends.TryGetValue(companion, out TendTask tend))
                {
                    FinishTendGather(companion, tend, goal, state);
                    continue;
                }
                CompanionBridge.StopGathering(companion);
                string name = CompanionBridge.Name(companion);
                RecordTask(companion, ModLang.Pick("conseguir " + goal.Amount + " de " + goal.Label, "gathering " + goal.Amount + " " + goal.Label, "добыча: " + goal.Amount + " шт. " + goal.Label));
                string progress = ModLang.Pick(goal.Gained + " de " + goal.Amount + " de " + goal.Label, goal.Gained + " of " + goal.Amount + " " + goal.Label, goal.Gained + " из " + goal.Amount + " " + goal.Label);
                Log.LogInfo("[Meta] " + name + ": " + state + " con " + progress);
                bool onGround = CompanionActions.OnlyOnGround(goal.Item);
                string missingTool = onGround ? null : CompanionBridge.MissingTool(companion, CompanionActions.JobForItem(goal.Item));
                string stuckReason = missingTool != null
                    ? ModLang.Pick(" y le falta " + missingTool + ".", " and is missing " + missingTool + ".", " и не хватает инструмента: " + missingTool + ".")
                    : onGround
                        ? ModLang.Pick(" y no encuentra más por el suelo a su alcance: suele estar cerca del agua.", " and cannot find any more on the ground within reach: usually found near water.", " и больше не находит на земле поблизости: обычно встречается у воды.")
                        : ModLang.Pick(" y no encuentra más cerca.", " and cannot find any more nearby.", " и больше не находит поблизости.");
                string message = state == GoalState.Reached
                    ? ModLang.Pick(
                        name + " ha conseguido " + progress + "." + (missingTool != null ? " Solo pudo recoger lo que estaba suelto: le falta " + missingTool + "." : ""),
                        name + " gathered " + progress + "." + (missingTool != null ? " Could only pick up loose items: missing " + missingTool + "." : ""),
                        name + " собирает " + progress + "." + (missingTool != null ? " Удалось собрать только лежащее на земле: не хватает инструмента (" + missingTool + ")." : ""))
                    : ModLang.Pick(
                        name + " no ha podido terminar: lleva " + progress + (state == GoalState.Stuck ? stuckReason : " y se agotó el tiempo." + (missingTool != null ? " Le falta " + missingTool + "." : "")),
                        name + " could not finish: has " + progress + (state == GoalState.Stuck ? stuckReason : " and ran out of time." + (missingTool != null ? " Missing " + missingTool + "." : "")),
                        name + " не может завершить сбор: собрано " + progress + (state == GoalState.Stuck ? stuckReason : " и вышло время." + (missingTool != null ? " Не хватает инструмента (" + missingTool + ")." : "")));
                Notify(message);
                AddToChat(name, message);

                FinishedCommentary(companion, state == GoalState.Reached
                    ? ModLang.Pick("Has conseguido " + progress + " y vuelves con el jugador.", "You gathered " + progress + " and are returning to the player.", "Собрано " + progress + ", возвращаешься к игроку.")
                    : ModLang.Pick(
                        "No has podido terminar el encargo: llevas " + progress + (state != GoalState.Stuck ? " y se te ha agotado el tiempo." : missingTool != null ? " porque te falta " + missingTool + "." : onGround ? " porque no encuentras más por el suelo a tu alcance; suele estar cerca del agua." : " porque no encuentras más cerca."),
                        "You could not finish the task: you have " + progress + (state != GoalState.Stuck ? " and ran out of time." : missingTool != null ? " because you are missing " + missingTool + "." : onGround ? " because you cannot find any more on the ground within reach; usually near water." : " because you cannot find any more nearby."),
                        "Не удалось завершить поручение: собрано " + progress + (state != GoalState.Stuck ? " и вышло время." : missingTool != null ? ", так как не хватает инструмента (" + missingTool + ")." : onGround ? ", так как больше ничего не лежит на земле поблизости (обычно ищется у воды)." : ", так как больше ничего нет поблизости.")),
                    state != GoalState.Reached);

                // Sin haber conseguido nada, lo pendiente ("tráemelas") ya no tiene sentido; con algo, sigue con lo que tenga.
                if (state != GoalState.Reached && goal.Gained == 0) _chains.Remove(companion);
            }
        }

        private bool HasGoal(Component companion)
        {
            return _travels.ContainsKey(companion) || _explores.ContainsKey(companion) || _goals.ContainsKey(companion) || _trees.ContainsKey(companion) || _plants.ContainsKey(companion) || _gives.ContainsKey(companion) || _takes.ContainsKey(companion) || _pickups.ContainsKey(companion) || _stores.ContainsKey(companion) || _hunts.ContainsKey(companion) || _cooks.ContainsKey(companion) || _organizing.ContainsKey(companion) || _smelts.ContainsKey(companion);
        }

        private string WhyNoEmote(Component companion)
        {
            if (companion == null) return ModLang.Pick("no está disponible", "not available", "недоступно");
            if (_emotes.ContainsKey(companion)) return ModLang.Pick("ya está haciendo uno", "already doing one", "уже выполняет задачу");
            if (_nextEmoteAt.TryGetValue(companion, out float next) && Time.time < next) return ModLang.Pick("acaba de hacer otro", "just did another", "только что закончил другое дело");
            if (HasGoal(companion) || _guards.ContainsKey(companion)) return ModLang.Pick("tiene un encargo", "working on a task", "занят поручением");
            if (_chains.TryGetValue(companion, out OrderSequence<QueuedOrder> chain) && chain.Pending > 0) return ModLang.Pick("tiene tareas pendientes", "has pending tasks", "есть задачи в очереди");
            return CompanionBridge.WhyNoEmote(companion);
        }

        private void TryEmote(Component companion, string name, string trigger)
        {
            if (trigger == null || !_settings.AllowEmotes.Value) return;
            string reason = WhyNoEmote(companion);
            if (reason != null)
            {
                Log.LogInfo("[Gesto] " + name + ": " + trigger + " omitido, " + reason);
                return;
            }
            CompanionBridge.PlayEmote(companion, trigger);
            _emotes[companion] = new EmoteGoal(Time.time);
            _nextEmoteAt[companion] = Time.time + EmoteCooldown;
            Log.LogInfo("[Gesto] " + name + ": " + trigger);
        }

        /// <summary>Cada fotograma, porque debe seguir mirando al jugador y parar en cuanto acabe la animación o haya peligro.</summary>
        private void UpdateEmotes(Player player)
        {
            if (_emotes.Count == 0) return;
            foreach (Component companion in new List<Component>(_emotes.Keys))
            {
                if (companion == null)
                {
                    _emotes.Remove(companion);
                    continue;
                }
                CompanionBridge.Face(companion, player, Time.deltaTime);
                bool finished = _emotes[companion].Finished(CompanionBridge.IsPlayingEmote(companion), Time.time);
                if (finished || CompanionBridge.InCombat(companion) || companion.GetComponent<Character>().GetHealth() <= 0f) EndEmote(companion);
            }
        }

        private void EndEmote(Component companion)
        {
            EmoteGoal goal = _emotes[companion];
            _emotes.Remove(companion);
            CompanionBridge.StopEmote(companion);
            if (!goal.Entered) Log.LogWarning("[Gesto] La animación no llegó a empezar.");
        }

        private sealed class QueuedOrder
        {
            public ActionRequest Action;
            public LookTarget Look;
            public IList<Component> Group;
        }

        private OrderSequence<QueuedOrder> ChainOf(Component companion)
        {
            if (!_chains.TryGetValue(companion, out OrderSequence<QueuedOrder> chain))
            {
                chain = new OrderSequence<QueuedOrder>();
                _chains[companion] = chain;
            }
            return chain;
        }

        /// <summary>Avanza las cadenas sin hablar con la IA: cada paso solo comprueba si ha terminado el anterior.</summary>
        private void UpdateChains(Player player)
        {
            if (Time.time < _nextChainCheckAt) return;
            _nextChainCheckAt = Time.time + ChainCheckInterval;
            UpdatePersistence();
            UpdateRevived();
            UpdateGearChecks(player);
            UpdateWaterTraps(player);
            UpdateTravels(player);
            UpdateTends(player);
            UpdateExplores(player);
            UpdateExpeditions(player);
            UpdateHuntSweeps(player);
            UpdateHunts(player);
            UpdatePickups(player);
            UpdateStores(player);
            UpdateGoals();
            UpdateOrganizing();
            UpdateTrees();
            UpdateCooks();
            UpdateSmelts();
            UpdateGuards(player);
            UpdateRetreats(player);

            foreach (Component companion in new List<Component>(_chains.Keys))
            {
                OrderSequence<QueuedOrder> chain = _chains[companion];
                if (companion == null || companion.GetComponent<Character>().GetHealth() <= 0f)
                {
                    _chains.Remove(companion);
                    continue;
                }
                if (chain.Pending == 0)
                {
                    _chains.Remove(companion);
                    continue;
                }

                bool hasGoal = HasGoal(companion);
                SequenceStep step = chain.Tick(Time.time, hasGoal || CompanionBridge.IsBusy(companion), hasGoal ? GatherGoal.TimeoutSeconds + OrderSequence<QueuedOrder>.StepTimeout : OrderSequence<QueuedOrder>.StepTimeout);
                string name = CompanionBridge.Name(companion);
                if (step == SequenceStep.TimedOut)
                {
                    Log.LogWarning("[Cadena] " + name + ": tardó demasiado; se cancelan " + chain.Pending + " tareas.");
                    Notify(ModLang.Pick(name + " no ha podido seguir con sus tareas pendientes.", name + " could not continue with their pending tasks.", name + " не может продолжить задачи из очереди."));
                    _chains.Remove(companion);
                }
                else if (step == SequenceStep.StartNext)
                {
                    QueuedOrder next = chain.TakeNext();
                    string failure = StartTask(companion, next.Group, next.Look, next.Action, out string note);
                    if (failure == null)
                    {
                        chain.StepStarted(Time.time);
                        Log.LogInfo("[Cadena] " + name + ": empieza " + next.Action.Kind + (note == null ? "" : " (" + note + ")"));
                        AddOrderLine(ModLang.Pick("Siguiente tarea de " + name + ": " + next.Action.Description, "Next task for " + name + ": " + next.Action.Description, "Следующая задача для " + name + ": " + next.Action.Description));
                    }
                    else
                    {
                        Log.LogWarning("[Cadena] " + name + ": " + next.Action.Kind + " falló: " + failure);
                        Notify(ModLang.Pick(name + " no ha podido " + next.Action.Description + ": " + failure, name + " could not complete (" + next.Action.Description + "): " + failure, name + " не может выполнить (" + next.Action.Description + "): " + failure));
                        _chains.Remove(companion);
                    }
                }
            }
        }

        private string PartySummary(Player player, Component speaker, float reach)
        {
            var others = new List<string>();
            foreach (Component c in CompanionBridge.Nearby(player, speaker, reach))
            {
                if (c != speaker) others.Add(CompanionBridge.Name(c));
            }
            string text = ModLang.Pick("\nOtros compañeros cerca: ", "\nOther companions nearby: ", "\nДругие спутники рядом: ")
                + (others.Count == 0 ? ModLang.Pick("ninguno", "none", "нет") : string.Join(", ", others.ToArray()));
            if (_chains.TryGetValue(speaker, out OrderSequence<QueuedOrder> chain) && chain.Pending > 0)
            {
                text += ModLang.Pick("\nTareas pendientes en tu cola: ", "\nPending tasks in your queue: ", "\nЗадачи в твоей очереди: ") + chain.Pending;
            }
            return text;
        }

        private void Finish(Component target, string name, string id, string question, string reply)
        {
            _waiting = false;
            // Fuera del historial y de lo que se muestra: es una indicación para el juego, no algo que el personaje dice.
            string spoken = LlmProtocol.StripNarration(EmoteCatalog.Extract(Initiative.StripSilence(reply), out string emote), PromptBuilder.WantsNarration(LoadProfile(id)));
            if (target != null) TryEmote(target, name, emote);
            if (string.IsNullOrWhiteSpace(spoken)) return;
            _reactions.NotifySpoke(Time.time);
            _initiative.NotifySpoke(Time.time);

            CompanionMemory memory = MemoryOf(id);
            memory.Add(question, spoken);
            SaveMemory(id, memory);

            AddToChat(name, LlmProtocol.Shorten(spoken, 0));
            if (_settings.ShareSpeech.Value && target != null) SharedSpeech.SendChat(target, name, LlmProtocol.Shorten(spoken, 0));
            // El compañero puede haber desaparecido mientras se esperaba la respuesta.
            if (target != null)
            {
                var pages = new Queue<string>(LlmProtocol.Pages(spoken, _settings.MaxReplyChars.Value));
                _speech[target] = pages;
                ShowNextPage(target, pages);
            }

            SummarizeIfNeeded(name, id, memory);
        }

        private void ShowNextPage(Component target, Queue<string> pages)
        {
            string page = pages.Dequeue();
            float seconds = LlmProtocol.ReadingSeconds(page, _settings.ReadingSpeed.Value, _settings.MinReadingSeconds.Value, _settings.MaxReadingSeconds.Value);
            CompanionBridge.Say(target, page, seconds);
            if (_settings.ShareSpeech.Value) SharedSpeech.SendBubble(target, page, seconds);
            _nextPageAt[target] = Time.time + seconds;
        }

        private void UpdateSpeech()
        {
            if (_speech.Count == 0) return;
            foreach (Component target in new List<Component>(_speech.Keys))
            {
                Queue<string> pages = _speech[target];
                if (target == null || pages.Count == 0)
                {
                    _speech.Remove(target);
                    _nextPageAt.Remove(target);
                }
                else if (Time.time >= _nextPageAt[target])
                {
                    ShowNextPage(target, pages);
                }
            }
        }

        /// <summary>Se hace en segundo plano, después de responder, para que el jugador no tenga que esperarlo.</summary>
        private void SummarizeIfNeeded(string name, string id, CompanionMemory memory)
        {
            int keep = _settings.HistoryMessages.Value;
            if (!_settings.MemoryEnabled.Value || !_settings.SummarizeMemory.Value || _summarizing) return;
            if (Time.time < _nextSummaryAt || !memory.NeedsSummary(keep)) return;

            int count = memory.CountToSummarize(keep);
            int revision = memory.Revision;
            _summarizing = true;
            StartCoroutine(_client.Complete(
                memory.BuildSummaryRequest(name, count),
                null,
                response =>
                {
                    _summarizing = false;
                    if (string.IsNullOrWhiteSpace(response.Text)) return;
                    // Si el jugador editó los recuerdos mientras tanto, su versión manda.
                    if (memory.Revision != revision) return;
                    memory.ApplySummary(response.Text, count);
                    SaveMemory(id, memory);
                    Log.LogInfo("[Memoria] " + name + ": resumidos " + count + " mensajes.");
                },
                error =>
                {
                    _summarizing = false;
                    _nextSummaryAt = Time.time + SummaryRetryDelay;
                    Log.LogWarning("[Memoria] No se pudo resumir: " + error);
                }));
        }

        private void AddToChat(string who, string text)
        {
            if (_settings.ShowInChat.Value && Chat.instance != null)
            {
                Chat.instance.AddString("<color=orange>" + who + "</color>: " + text);
                // AddString solo lo guarda en el historial; esto hace que la ventana de chat se muestre.
                ChatHideTimer?.SetValue(Chat.instance, 0f);
            }
        }

        private static void Notify(string text)
        {
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, text);
        }
    }
}
