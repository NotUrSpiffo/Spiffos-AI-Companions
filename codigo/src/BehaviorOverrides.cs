using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Cambios temporales en lo que deciden los trabajos de Offline Companions, sin tocar la configuración del jugador.
    /// Solo actúan mientras hay un encargo activo.
    /// </summary>
    public static class BehaviorOverrides
    {
        /// <summary>La comida que se está encargando, o null si no hay encargo de cocina.</summary>
        public static string CookFood;
        public static int CookTarget;

        /// <summary>El material que se está encargando fundir, o null si no hay encargo.</summary>
        public static string SmeltItem;

        /// <summary>Compañeros cuyo trabajo de mantenimiento se limita a una tarea, y cuál (el nombre de su método Plan).</summary>
        public static readonly Dictionary<GameObject, string> UpkeepFocus = new Dictionary<GameObject, string>();

        private const float SpacingPatienceSeconds = 3f;
        private const float SpacingDipSeconds = 2f;
        private const float CookDropRadius = 4f;
        private const float RockStrikeReach = 3f;
        private const float SectionAboveGround = 0.1f;
        private const float SectionSampleSpread = 0.5f;
        private const float SectionCheckSeconds = 5f;
        private const int MaxSectionsRemembered = 2000;
        private const int MaxBuriedReports = 5;
        private static readonly Vector2[] SectionSamples = { Vector2.zero, Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        private static readonly Dictionary<Collider, KeyValuePair<float, bool>> _sectionVisible = new Dictionary<Collider, KeyValuePair<float, bool>>();
        private static int _buriedReports;

        // Estado de cada hueco de una estación de cocina en sus datos de red.
        private const int SlotCooking = 0;
        private const int SlotDone = 1;
        private const int SlotBurnt = 2;

        private static bool _inRefill;
        private static MethodInfo _unclaimSmelter;
        private static int _cookingNow;

        private static MethodInfo _pickTask;
        private static MethodInfo _finishTask;
        private static MethodInfo _pickUpDrops;
        private static MethodInfo _dropPoint;
        private static FieldInfo _cookTask;
        private static FieldInfo _cookTimer;
        private static FieldInfo _cookers;
        private static FieldInfo _taskKind;
        private static FieldInfo _taskCooker;

        private static MethodInfo _wouldSwim;
        private static MethodInfo _applyRockDamage;
        private static MethodInfo _sectionExposed;
        private static FieldInfo _harvestTarget;
        private static FieldInfo _noDamageSwings;
        private static FieldInfo _lastHitAt;
        private static FieldInfo _hitCheckFor;
        private static FieldInfo _harvestBlacklist;
        private static readonly HashSet<Component> _rockReported = new HashSet<Component>();

        private static int _stanceHash;
        private static int _pickupHash;
        private static int _recoveringHash;
        private static int _savedStanceHash;
        private static int _savedPickupHash;

        private static FieldInfo _spacingHold;
        private static FieldInfo _dipping;
        private static FieldInfo _dipUntil;
        private static FieldInfo _fightTarget;
        private static readonly Dictionary<Component, float> _holdingSince = new Dictionary<Component, float>();

        public static bool CookAvailable { get; private set; }
        public static bool UpkeepAvailable { get; private set; }
        public static bool SmeltAvailable { get; private set; }

        public static void Apply(Harmony harmony)
        {
            ApplyCooking(harmony);
            ApplyCookSpread(harmony);
            ApplySpacingPatience(harmony);
            ApplyToolInFight(harmony);
            ApplyUpkeep(harmony);
            ApplySmelting(harmony);
            StorageSortPatches.Apply(harmony);
            ApplySpeechFilter(harmony);
            ApplyRockStrikes(harmony);
            ApplyStuckGather(harmony);
            ApplyCompanionLimit(harmony);
            ApplyStoreFilter(harmony);
            ApplyGroundItems(harmony);
            ApplyChopping(harmony);
            ApplyDeathWhileRecovering(harmony);
            ApplyStayDry(harmony);
            ApplyNameTyping(harmony);
            ApplyTendArea(harmony);
            ApplySimpleWheel(harmony);
        }

        private static FieldInfo _wheelSegments;
        private static FieldInfo _wheelSegmentAction;

        /// <summary>La rueda calcula sus huecos según cuántas opciones haya, así que basta con quitarlas de la lista al crearla.</summary>
        private static void ApplySimpleWheel(Harmony harmony)
        {
            System.Type wheel = AccessTools.TypeByName("Companions.CompanionRadialMenu");
            MethodInfo build = wheel == null ? null : AccessTools.Method(wheel, "BuildSegments");
            _wheelSegments = wheel == null ? null : AccessTools.Field(wheel, "_segments");
            System.Type segment = wheel?.GetNestedType("Segment", BindingFlags.Public | BindingFlags.NonPublic);
            _wheelSegmentAction = segment == null ? null : AccessTools.Field(segment, "ActionId");
            if (build == null || _wheelSegments == null || _wheelSegmentAction == null)
            {
                Plugin.Log.LogWarning("Reducir la rueda de órdenes no está disponible en esta versión de Offline Companions Reborn: se verá completa.");
                return;
            }
            harmony.Patch(build, postfix: Hook(nameof(SimpleWheelPostfix)));
        }

        private static void SimpleWheelPostfix(Component __instance)
        {
            if (!(_wheelSegments.GetValue(__instance) is System.Collections.IList segments)) return;
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (!WheelOptions.Keeps((int)_wheelSegmentAction.GetValue(segments[i]))) segments.RemoveAt(i);
            }
        }

        /// <summary>Quienes se encargan de un sitio, y dónde está: solo van a por recursos alrededor de ese punto.</summary>
        public static readonly Dictionary<GameObject, Vector3> TendArea = new Dictionary<GameObject, Vector3>();

        public static bool TendAreaAvailable { get; private set; }

        public static bool TendHuntAvailable { get; private set; }

        /// <summary>
        /// El alcance de recolección de Reborn es el mismo para todos, y puede ser de 80 m o más; además, cerca del hogar mide
        /// desde el hogar. Esto lo acorta solo para quien se encarga de un sitio, sin tocar el ajuste del jugador.
        /// </summary>
        private static void ApplyTendArea(Harmony harmony)
        {
            System.Type harvest = AccessTools.TypeByName("Companions.HarvestController");
            MethodInfo inArea = harvest == null ? null : AccessTools.Method(harvest, "InWorkArea");
            MethodInfo center = harvest == null ? null : AccessTools.Method(harvest, "ScanCenter");
            MethodInfo radius = harvest == null ? null : AccessTools.Method(harvest, "ScanRad");
            if (inArea == null || center == null || radius == null)
            {
                Plugin.Log.LogWarning("Limitar la recolección alrededor de un sitio no está disponible en esta versión de Offline Companions Reborn: al encargarse de un sitio solo hará mantenimiento.");
                return;
            }
            harmony.Patch(inArea, postfix: Hook(nameof(TendInAreaPostfix)));
            harmony.Patch(center, postfix: Hook(nameof(TendCenterPostfix)));
            harmony.Patch(radius, postfix: Hook(nameof(TendRadiusPostfix)));
            TendAreaAvailable = true;

            System.Type hunt = AccessTools.TypeByName("Companions.HuntController");
            MethodInfo huntCenter = hunt == null ? null : AccessTools.Method(hunt, "Center");
            MethodInfo huntRange = hunt == null ? null : AccessTools.Method(hunt, "Range");
            if (huntCenter == null || huntRange == null)
            {
                Plugin.Log.LogWarning("Limitar la caza alrededor de un sitio no está disponible en esta versión de Offline Companions Reborn: al encargarse de un sitio no cazará.");
                return;
            }
            harmony.Patch(huntCenter, postfix: Hook(nameof(TendCenterPostfix)));
            harmony.Patch(huntRange, postfix: Hook(nameof(TendRadiusLimitPostfix)));
            TendHuntAvailable = true;
        }

        private static void TendInAreaPostfix(Component __instance, Vector3 p, ref bool __result)
        {
            if (TendArea.Count == 0 || !TendArea.TryGetValue(__instance.gameObject, out Vector3 point)) return;
            p.y = point.y;
            __result = Vector3.Distance(p, point) <= TendPlan.AreaMeters;
        }

        private static void TendCenterPostfix(Component __instance, ref Vector3 __result)
        {
            if (TendArea.Count > 0 && TendArea.TryGetValue(__instance.gameObject, out Vector3 point)) __result = point;
        }

        // Sin esto buscaría solo en lo que diga "GatheringSearchRadius", que suele ser menos que el sitio entero.
        private static void TendRadiusPostfix(Component __instance, ref float __result)
        {
            if (TendArea.Count > 0 && TendArea.ContainsKey(__instance.gameObject)) __result = TendPlan.AreaMeters;
        }

        private static void TendRadiusLimitPostfix(Component __instance, ref float __result)
        {
            if (TendArea.Count > 0 && TendArea.ContainsKey(__instance.gameObject)) __result = Mathf.Min(__result, TendPlan.AreaMeters);
        }

        private static MethodInfo _panelInstance;
        private static MethodInfo _nameFocused;

        /// <summary>
        /// Con el inventario abierto, el juego lo cierra al pulsar Usar o Inventario aunque se esté escribiendo. Offline Companions
        /// solo bloquea el control del personaje, así que una E en el nombre cerraba la ventana y al soltarla la volvía a abrir.
        /// </summary>
        private static void ApplyNameTyping(Harmony harmony)
        {
            System.Type panel = AccessTools.TypeByName("Companions.CompanionInteractPanel");
            _panelInstance = panel == null ? null : AccessTools.PropertyGetter(panel, "Instance");
            _nameFocused = panel == null ? null : AccessTools.PropertyGetter(panel, "IsNameInputFocused");
            MethodInfo update = AccessTools.Method(typeof(InventoryGui), "Update");
            if (_panelInstance == null || _nameFocused == null || update == null)
            {
                Plugin.Log.LogWarning("Escribir el nombre sin que se cierre la ventana no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(update, prefix: Hook(nameof(IgnoreKeysWhileNaming)));
        }

        private static void IgnoreKeysWhileNaming()
        {
            if (!InventoryGui.IsVisible()) return;
            Component panel = _panelInstance.Invoke(null, null) as Component;
            if (panel == null || !(bool)_nameFocused.Invoke(panel, null)) return;
            ZInput.ResetButtonStatus("Use");
            ZInput.ResetButtonStatus("Inventory");
        }

        /// <summary>Compañeros que no deben meterse a nadar por su cuenta hasta recuperar aguante.</summary>
        public static readonly HashSet<GameObject> KeepDry = new HashSet<GameObject>();

        // Volver a la orilla, huir o subir al barco del jugador no pueden esperar a que descanse.
        private static readonly HashSet<string> TiredMoves = new HashSet<string> { "Shore", "Flee", "Board" };
        private const float WaterLookAhead = 20f;
        private static MethodInfo _swimOnLine;

        /// <summary>
        /// Todos sus desplazamientos pasan por el mismo método, así que aquí se corta solo lo que le llevaría al agua: puede
        /// seguir peleando o andando por tierra mientras descansa.
        /// </summary>
        private static void ApplyStayDry(Harmony harmony)
        {
            System.Type mover = AccessTools.TypeByName("Companions.MHR_Mover");
            MethodInfo go = mover == null ? null : AccessTools.GetDeclaredMethods(mover).Find(method => method.Name == "Go" && method.GetParameters().Length == 7);
            _swimOnLine = AccessTools.Method("Companions.MHR_Water:SwimOnLine");
            if (go == null || _swimOnLine == null)
            {
                Plugin.Log.LogWarning("Descansar en la orilla antes de volver a nadar no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(go, prefix: Hook(nameof(HoldAshore)));
        }

        private static bool HoldAshore(BaseAI ai, Vector3 goal, string tag, ref bool __result)
        {
            if (KeepDry.Count == 0 || ai == null || !KeepDry.Contains(ai.gameObject) || (tag != null && TiredMoves.Contains(tag))) return true;
            Character character = ai.GetComponent<Character>();
            if (character == null || character.IsSwimming()) return true;
            if (!(bool)_swimOnLine.Invoke(null, new object[] { character, ai.transform.position, goal, WaterLookAhead })) return true;
            ai.StopMoving();
            __result = false;
            return false;
        }

        /// <summary>
        /// Al revivir, Offline Companions lo deja en postura pasiva hasta recuperar sus cosas y guarda la postura de antes.
        /// Si muere en ese rato, toma la pasiva por la suya y se queda sin pelear para siempre; igual con recoger por su cuenta.
        /// </summary>
        private static void ApplyDeathWhileRecovering(Harmony harmony)
        {
            System.Type setup = AccessTools.TypeByName("Companions.CompanionSetup");
            System.Type recover = AccessTools.TypeByName("Companions.MHR_Recover");
            MethodInfo death = setup == null ? null : AccessTools.Method(setup, "OnCompanionDeath");
            FieldInfo stance = setup == null ? null : AccessTools.Field(setup, "CombatStanceHash");
            FieldInfo pickup = setup == null ? null : AccessTools.Field(setup, "AutoPickupHash");
            FieldInfo recovering = recover == null ? null : AccessTools.Field(recover, "RecoveringHash");
            FieldInfo savedStance = recover == null ? null : AccessTools.Field(recover, "RecoverStanceHash");
            FieldInfo savedPickup = recover == null ? null : AccessTools.Field(recover, "RecoverPickupHash");
            if (death == null || stance == null || pickup == null || recovering == null || savedStance == null || savedPickup == null)
            {
                Plugin.Log.LogWarning("Conservar la postura al morir recién revivido no está disponible en esta versión de Offline Companions Reborn: podría quedarse en postura pasiva.");
                return;
            }
            _stanceHash = (int)stance.GetValue(null);
            _pickupHash = (int)pickup.GetValue(null);
            _recoveringHash = (int)recovering.GetValue(null);
            _savedStanceHash = (int)savedStance.GetValue(null);
            _savedPickupHash = (int)savedPickup.GetValue(null);
            harmony.Patch(death, prefix: Hook(nameof(RestoreBeforeDeath)));
        }

        private static void RestoreBeforeDeath(Component __instance)
        {
            ZNetView view = __instance.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner()) return;
            ZDO zdo = view.GetZDO();
            if (zdo == null || !zdo.GetBool(_recoveringHash)) return;
            int stance = zdo.GetInt(_savedStanceHash);
            zdo.Set(_stanceHash, stance);
            zdo.Set(_pickupHash, zdo.GetBool(_savedPickupHash, true));
            Plugin.Log.LogInfo("[Postura] " + CompanionBridge.Name(__instance) + " ha muerto recién revivido: conserva la postura " + CompanionActions.StanceName(stance) + ".");
        }

        /// <summary>Si en ese punto tendría que nadar. Sin el dato de Offline Companions, se da por hecho que no.</summary>
        public static bool WouldSwimAt(Component companion, Vector3 point)
        {
            return _wouldSwim != null && (bool)_wouldSwim.Invoke(null, new object[] { companion.GetComponent<Character>(), point });
        }

        /// <summary>Lo único que puede dejar en el cofre al que va ahora. Lo demás es para otro cofre.</summary>
        public static readonly Dictionary<GameObject, HashSet<string>> StoreOnly = new Dictionary<GameObject, HashSet<string>>();

        /// <summary>
        /// El objeto que un compañero puede buscar por el suelo (sílex, o piedra cuando no tiene pico), o null si no tiene ese encargo.
        /// </summary>
        public static System.Func<GameObject, string> GroundItemFor;

        private static void ApplyStoreFilter(Harmony harmony)
        {
            MethodInfo keep = AccessTools.Method(AccessTools.TypeByName("Companions.CompanionInteract"), "ShouldKeep", new[] { typeof(ItemDrop.ItemData), typeof(Humanoid) });
            if (keep == null)
            {
                Plugin.Log.LogWarning("Repartir lo guardado según lo que hay en cada cofre no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(keep, postfix: Hook(nameof(KeepPostfix)));
        }

        private static void KeepPostfix(ItemDrop.ItemData item, Humanoid humanoid, ref bool __result)
        {
            if (__result || humanoid == null || !StoreOnly.TryGetValue(humanoid.gameObject, out HashSet<string> items)) return;
            __result = item.m_dropPrefab == null || !items.Contains(item.m_dropPrefab.name);
        }

        /// <summary>
        /// Con pico, Offline Companions va a lo que tenga más cerca, roca o no, y las rocas no dan sílex: nunca terminaría.
        /// Sin herramienta solo recoge lo suelto, que es justo lo que hace falta.
        /// </summary>
        private static void ApplyGroundItems(Harmony harmony)
        {
            MethodInfo looseOn = AccessTools.Method(AccessTools.TypeByName("Companions.HarvestController"), "LooseOn");
            MethodInfo allowLoose = AccessTools.Method("Companions.MHR_GatherRules:AllowLoose");
            if (looseOn == null || allowLoose == null)
            {
                Plugin.Log.LogWarning("Buscar sílex solo por el suelo no está disponible en esta versión de Offline Companions Reborn: con pico se pondrá a picar rocas.");
                return;
            }
            _wouldSwim = AccessTools.Method("Companions.MHR_Water:WouldSwim");
            if (_wouldSwim == null) Plugin.Log.LogWarning("Evitar el sílex en aguas hondas no está disponible en esta versión de Offline Companions Reborn: puede ponerse a nadar para buscarlo.");
            harmony.Patch(looseOn, postfix: Hook(nameof(LooseOnPostfix)));
            harmony.Patch(allowLoose, postfix: Hook(nameof(AllowLoosePostfix)));
        }

        // Offline Companions deja de recoger piedras sueltas en cuanto alguien tiene un hacha de bronce o hay una forja,
        // aunque el compañero no lleve pico: con un encargo de piedra se quedaba quieto sin poder conseguir ninguna.
        // Con pico, la piedra se saca picando rocas como siempre.
        private static void LooseOnPostfix(Component __instance, ref bool noTool, ref bool __result)
        {
            string item = GroundItemFor?.Invoke(__instance.gameObject);
            if (item == null || item == StoneItem && !noTool) return;
            noTool = true;
            __result = true;
        }

        // Tampoco piedras sueltas, ni lo limitan las listas y cantidades de la configuración: lo ha pedido el jugador.
        // Lo que está donde no hace pie se deja: nadar hasta allí le agota, y hay más por la orilla.
        private static void AllowLoosePostfix(Component h, Pickable pick, int mode, ref bool __result)
        {
            string item = h == null ? null : GroundItemFor?.Invoke(h.gameObject);
            if (item == null)
            {
                // Las ramas sueltas solo dan madera normal.
                string wood = h == null || mode != WoodMode ? null : ChopItemFor?.Invoke(h.gameObject);
                if (wood != null && __result) __result = pick != null && pick.m_itemPrefab != null && pick.m_itemPrefab.name == wood;
                return;
            }
            __result = pick != null && pick.m_itemPrefab != null && pick.m_itemPrefab.name == item && !NeedsSwimming(h, pick);
        }

        private static bool NeedsSwimming(Component h, Pickable pick)
        {
            return _wouldSwim != null && (bool)_wouldSwim.Invoke(null, new object[] { h.GetComponent<Character>(), pick.transform.position });
        }

        /// <summary>La madera que un compañero debe sacar solo de los árboles que la dan, o null si vale cualquiera.</summary>
        public static System.Func<GameObject, string> ChopItemFor;

        private const int WoodMode = 1;
        private const string StoneItem = "Stone";
        private static MethodInfo _dropsOf;
        private static MethodInfo _isOwnPiece;
        private static FieldInfo _bestTree;

        /// <summary>
        /// Offline Companions tala lo que tenga a mano sin mirar qué madera suelta, y va antes a cualquier tronco o tocón
        /// del suelo, aunque esté lejos, que a un árbol en pie cercano.
        /// </summary>
        private static void ApplyChopping(Harmony harmony)
        {
            System.Type harvest = AccessTools.TypeByName("Companions.HarvestController");
            MethodInfo allow = AccessTools.Method("Companions.MHR_GatherRules:AllowTarget");
            MethodInfo scan = AccessTools.Method(harvest, "ScanForTarget");
            _dropsOf = AccessTools.Method("Companions.MHR_GatherRules:DropsOf");
            _isOwnPiece = AccessTools.Method(harvest, "IsOwnPiece");
            _bestTree = AccessTools.Field(harvest, "_bestTree");
            if (allow == null || scan == null || _dropsOf == null || _isOwnPiece == null || _bestTree == null)
            {
                Plugin.Log.LogWarning("Elegir los árboles según la madera pedida no está disponible en esta versión de Offline Companions Reborn: talará cualquiera.");
                return;
            }
            harmony.Patch(allow, postfix: Hook(nameof(ChopOnlyForItem)));
            harmony.Patch(scan, postfix: Hook(nameof(NearestWood)));
        }

        // Según lo que el juego dice que suelta el árbol, sus troncos o el tocón: así sirve para cualquier madera.
        private static void ChopOnlyForItem(Component h, GameObject go, int mode, ref bool __result)
        {
            if (!__result || mode != WoodMode || h == null || go == null) return;
            string item = ChopItemFor?.Invoke(h.gameObject);
            if (item == null) return;
            var drops = _dropsOf.Invoke(null, new object[] { go }) as List<string>;
            __result = drops != null && drops.Contains(item);
        }

        // Rematar el tronco del árbol que acaba de tirar sigue yendo primero. Lo demás, lo que esté más cerca.
        private static void NearestWood(Component __instance, int mode, ref GameObject __result)
        {
            if (mode != WoodMode || __result == null || __result.GetComponent<TreeBase>() != null) return;
            var tree = _bestTree.GetValue(__instance) as GameObject;
            if (tree == null) return;
            string type = __result.GetComponent<TreeLog>() != null ? "TreeLog" : "Stump";
            if ((bool)_isOwnPiece.Invoke(__instance, new object[] { __result, type })) return;
            Vector3 from = __instance.transform.position;
            if (Vector3.Distance(from, tree.transform.position) < Vector3.Distance(from, __result.transform.position)) __result = tree;
        }

        /// <summary>
        /// Offline Companions abandona una roca grande si dos golpes seguidos no la tocan físicamente, cosa fácil en vetas
        /// medio enterradas como las de cobre, donde el pico se queda en el suelo. Y no olvida esos fallos al repetir la
        /// orden, así que después se rinde al primer golpe.
        /// </summary>
        private static void ApplyRockStrikes(Harmony harmony)
        {
            System.Type harvest = AccessTools.TypeByName("Companions.HarvestController");
            MethodInfo swings = harvest == null ? null : AccessTools.Method(harvest, "SwingsNotLanding");
            MethodInfo directed = harvest == null ? null : AccessTools.Method(harvest, "SetDirectedTarget", new[] { typeof(GameObject) });
            _applyRockDamage = harvest == null ? null : AccessTools.Method(harvest, "ApplyDirectRock5Damage");
            _sectionExposed = harvest == null ? null : AccessTools.Method(harvest, "IsSegmentExposed", new[] { typeof(Collider) });
            _harvestTarget = harvest == null ? null : AccessTools.Field(harvest, "_target");
            _noDamageSwings = harvest == null ? null : AccessTools.Field(harvest, "_noDamageSwings");
            _lastHitAt = harvest == null ? null : AccessTools.Field(harvest, "_lastHitAt");
            _hitCheckFor = harvest == null ? null : AccessTools.Field(harvest, "_hitCheckFor");
            _harvestBlacklist = harvest == null ? null : AccessTools.Field(harvest, "_blacklist");
            if (swings == null || directed == null || _applyRockDamage == null || _sectionExposed == null || _harvestTarget == null
                || _noDamageSwings == null || _lastHitAt == null || _hitCheckFor == null || _harvestBlacklist == null)
            {
                Plugin.Log.LogWarning("Picar vetas medio enterradas puede fallar: esta versión de Offline Companions Reborn no tiene lo necesario para ayudarle.");
                return;
            }
            harmony.Patch(swings, postfix: Hook(nameof(SwingsPostfix)));
            harmony.Patch(directed, prefix: Hook(nameof(DirectedPrefix)));
            harmony.Patch(_sectionExposed, postfix: Hook(nameof(SectionExposedPostfix)));

            _wouldSwim = AccessTools.Method("Companions.MHR_Water:WouldSwim");
            MethodInfo allow = AccessTools.Method("Companions.MHR_GatherRules:AllowTarget");
            if (_wouldSwim == null || allow == null)
            {
                Plugin.Log.LogWarning("Dejar las rocas a las que solo se llega nadando no está disponible en esta versión de Offline Companions Reborn: puede nadar hasta ellas y agotarse.");
                return;
            }
            harmony.Patch(allow, postfix: Hook(nameof(RockReachableDry)));
        }

        private const int StoneMode = 2;
        private const int OreMode = 3;
        private const float DryStandRing = 1f;
        private const int DryStandSamples = 8;
        private const int MaxWetReports = 5;
        private static int _wetReports;

        /// <summary>
        /// Offline Companions no mira el agua al elegir roca. Una veta de cobre o de estaño metida en un lago tiene secciones
        /// sobre el fondo que da por buenas, y va nadando hasta ellas: nadando no puede picar, y se agota una y otra vez.
        /// </summary>
        private static void RockReachableDry(GameObject go, int mode, ref bool __result)
        {
            if (!__result || go == null || (mode != StoneMode && mode != OreMode)) return;
            try
            {
                bool dry;
                if (go.GetComponent<MineRock5>() != null)
                {
                    // Las secciones que valen ya descartan las que están bajo el agua.
                    dry = false;
                    foreach (Transform section in go.transform)
                    {
                        if (!section.gameObject.activeSelf) continue;
                        Collider collider = section.GetComponent<Collider>();
                        if (collider != null && (bool)_sectionExposed.Invoke(null, new object[] { collider })) { dry = true; break; }
                    }
                }
                else
                {
                    dry = !SolidBounds(go, out Bounds bounds) || StandsDryBeside(bounds);
                }
                if (dry) return;
                __result = false;
                if (_wetReports < MaxWetReports)
                {
                    _wetReports++;
                    Plugin.Log.LogInfo("[Picar] Se deja " + go.name + " en " + go.transform.position.ToString("F1") + ": solo se llega nadando.");
                }
            }
            catch (System.Exception ex)
            {
                // Dentro de la búsqueda de Offline Companions: ante la duda, se queda con lo que él decidió.
                Plugin.Log.LogError("[Picar] " + ex);
            }
        }

        private static bool SolidBounds(GameObject go, out Bounds bounds)
        {
            bounds = default(Bounds);
            bool any = false;
            foreach (Collider collider in go.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || !collider.enabled) continue;
                if (any) bounds.Encapsulate(collider.bounds);
                else { bounds = collider.bounds; any = true; }
            }
            return any;
        }

        // Se mira alrededor, justo por fuera, si hay algún sitio donde se haga pie. Sin forma de saberlo, no se descarta.
        private static bool StandsDryBeside(Bounds bounds)
        {
            if (_wouldSwim == null) return true;
            float ring = Mathf.Max(bounds.extents.x, bounds.extents.z) + DryStandRing;
            for (int i = 0; i < DryStandSamples; i++)
            {
                float angle = i * 2f * Mathf.PI / DryStandSamples;
                var spot = new Vector3(bounds.center.x + Mathf.Cos(angle) * ring, bounds.center.y, bounds.center.z + Mathf.Sin(angle) * ring);
                if (!(bool)_wouldSwim.Invoke(null, new object[] { null, spot })) return true;
            }
            return false;
        }

        /// <summary>
        /// Offline Companions da por expuesta una sección con el centro hasta 1 m bajo tierra, y va a la más cercana de
        /// esas. Una veta sigue bajo el suelo mucho más allá de lo que se ve, así que acababa picando la tierra de encima.
        /// Se pide mucho (por sección y por fotograma), de ahí que se recuerde un rato.
        /// </summary>
        private static void SectionExposedPostfix(Collider __0, ref bool __result)
        {
            if (!__result || __0 == null) return;
            try
            {
                float now = Time.time;
                if (_sectionVisible.TryGetValue(__0, out KeyValuePair<float, bool> known) && now - known.Key < SectionCheckSeconds)
                {
                    __result = known.Value;
                    return;
                }
                bool visible = SectionShowsAboveGround(__0) && SectionReachableDry(__0);
                if (_sectionVisible.Count > MaxSectionsRemembered) _sectionVisible.Clear();
                _sectionVisible[__0] = new KeyValuePair<float, bool>(now, visible);
                __result = visible;
            }
            catch (System.Exception ex)
            {
                // Dentro de cada fotograma de Offline Companions: ante la duda, se queda con lo que él decidió.
                Plugin.Log.LogError("[Picar] " + ex);
            }
        }

        // Se mira desde arriba dónde queda su superficie frente al suelo. Si los rayos no la encuentran, no se sabe, y no se descarta.
        private static bool SectionShowsAboveGround(Collider section)
        {
            if (ZoneSystem.instance == null) return true;
            Bounds bounds = section.bounds;
            float top = bounds.max.y + 1f;
            float length = bounds.size.y + 2f;
            Vector3 spread = bounds.extents * SectionSampleSpread;
            bool found = false;
            float highest = float.NegativeInfinity;
            foreach (Vector2 offset in SectionSamples)
            {
                var ray = new Ray(new Vector3(bounds.center.x + offset.x * spread.x, top, bounds.center.z + offset.y * spread.z), Vector3.down);
                if (!section.Raycast(ray, out RaycastHit hit, length)) continue;
                found = true;
                highest = Mathf.Max(highest, hit.point.y - ZoneSystem.instance.GetGroundHeight(hit.point));
            }
            if (!found || highest >= SectionAboveGround) return true;
            if (_buriedReports < MaxBuriedReports)
            {
                _buriedReports++;
                Plugin.Log.LogInfo("[Picar] Sección enterrada descartada en " + bounds.center.ToString("F1") + ": asoma " + highest.ToString("F1") + " m sobre el suelo.");
            }
            return false;
        }

        // Nadando no puede picar: una sección rodeada de agua honda no sirve aunque asome.
        private static bool SectionReachableDry(Collider section)
        {
            if (StandsDryBeside(section.bounds)) return true;
            if (_wetReports < MaxWetReports)
            {
                _wetReports++;
                Plugin.Log.LogInfo("[Picar] Sección en el agua descartada en " + section.bounds.center.ToString("F1") + ": no hay dónde ponerse sin nadar.");
            }
            return false;
        }

        /// <summary>Una orden nueva empieza de cero, aunque antes no pudiera con esa misma roca.</summary>
        private static void DirectedPrefix(Component __instance, GameObject __0)
        {
            if (__0 == null) return;
            _hitCheckFor.SetValue(__instance, null);
            (_harvestBlacklist.GetValue(__instance) as System.Collections.IDictionary)?.Remove(__0.GetInstanceID());
            _rockReported.Remove(__instance);
        }

        /// <summary>Si el golpe no ha tocado la roca pero tiene una sección a su alcance, cuenta como si la hubiera tocado.</summary>
        private static void SwingsPostfix(Component __instance, ref bool __result)
        {
            try
            {
                if ((int)_noDamageSwings.GetValue(__instance) < 1) return;
                GameObject target = _harvestTarget.GetValue(__instance) as GameObject;
                if (target == null || target.GetComponent<MineRock5>() == null) return;

                float distance = NearestRockSection(__instance.transform.position, target);
                string name = CompanionBridge.Name(__instance);
                if (distance > RockStrikeReach)
                {
                    if (!__result) return;
                    Plugin.Log.LogInfo("[Picar] " + name + ": no llega a golpear " + target.name + "; la sección más cercana está a " + (float.IsInfinity(distance) ? "ninguna parte" : distance.ToString("F1") + " m"));
                    MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, name + ModLang.Pick(
                        " no consigue golpear esa roca desde donde está.",
                        " cannot hit that rock from there.",
                        " не может достать до этого камня оттуда, где стоит."));
                    return;
                }

                _applyRockDamage.Invoke(__instance, null);
                _noDamageSwings.SetValue(__instance, 0);
                // Así el golpe siguiente vuelve a contar si tampoco toca: si no, solo haría daño uno de cada dos.
                _lastHitAt.SetValue(__instance, float.NegativeInfinity);
                __result = false;
                if (_rockReported.Add(__instance)) Plugin.Log.LogInfo("[Picar] " + name + ": sus golpes no tocan " + target.name + "; se aplican a la sección a " + distance.ToString("F1") + " m.");
            }
            catch (System.Exception ex)
            {
                // Se ejecuta en cada golpe, dentro de Offline Companions: mejor que siga con su propia decisión.
                Plugin.Log.LogError("[Picar] " + ex);
            }
        }

        // Solo las secciones que no están bajo tierra: las mismas que Offline Companions acepta golpear.
        private static float NearestRockSection(Vector3 from, GameObject rock)
        {
            float nearest = float.PositiveInfinity;
            foreach (Transform section in rock.transform)
            {
                if (!section.gameObject.activeSelf) continue;
                Collider collider = section.GetComponent<Collider>();
                if (collider == null || !(bool)_sectionExposed.Invoke(null, new object[] { collider })) continue;
                nearest = Mathf.Min(nearest, Vector3.Distance(from, collider.bounds.ClosestPoint(from)));
            }
            return nearest;
        }

        private const float StuckGatherSeconds = 20f;
        private const float StuckCheckSeconds = 1f;
        private const float StuckMoveDistance = 1.5f;
        private static FieldInfo _swingCount;
        private static FieldInfo _catchingBreath;

        private sealed class GatherWatch
        {
            public GameObject Target;
            public float Since;
            public float NextCheck;
            public Vector3 Anchor;
            public int Swings;
            public float LastHit;
            public Vector3 StepDir;
            public float StepUntil;
            public GameObject StepTarget;
            public int StepCount;
        }

        private const float StepOffAfterSeconds = 4f;
        private const float StepOffSeconds = 4f;
        private const int StepOffTries = 2;
        // Valheim hace daño por caída a partir de 4 m.
        private const float StepOffMaxDrop = 3.8f;

        private static readonly Dictionary<Companions.HarvestController, GatherWatch> _gatherWatch = new Dictionary<Companions.HarvestController, GatherWatch>();

        /// <summary>
        /// Offline Companions puede quedarse parado con un objetivo sin dar ningún golpe ni moverse, sin escribir nada en el
        /// registro: pasa al romperse una veta de cobre entera mientras está subido encima, y se queda sin el pico y quieto.
        /// Si pasa un rato así (también talando), se anota cómo estaba y se le hace dejar ese objetivo para que busque otro.
        /// </summary>
        private static void ApplyStuckGather(Harmony harmony)
        {
            MethodInfo update = AccessTools.Method(typeof(Companions.HarvestController), "Update");
            _swingCount = AccessTools.Field(typeof(Companions.HarvestController), "_swingCount");
            _catchingBreath = AccessTools.Field(typeof(Companions.HarvestController), "_catchingBreath");
            if (update == null || _swingCount == null || _catchingBreath == null || _lastHitAt == null)
            {
                Plugin.Log.LogWarning("Sacarle de un trabajo atascado no está disponible en esta versión de Offline Companions Reborn: puede quedarse quieto junto a una roca.");
                return;
            }
            harmony.Patch(update, postfix: Hook(nameof(StuckGatherPostfix)));
            MethodInfo allowTarget = AccessTools.Method(typeof(Companions.MHR_GatherRules), "AllowTarget");
            if (allowTarget != null) harmony.Patch(allowTarget, postfix: Hook(nameof(GatherFocusPostfix)));
        }

        /// <summary>
        /// Datos para averiguar por qué no golpea: a qué distancia está el jugador, si va cargado, qué herramienta de
        /// talar o picar lleva y si se puede equipar, y si el juego está animando al compañero.
        /// </summary>
        private static string StuckDetails(Companions.HarvestController harvest, Humanoid humanoid, Vector3 position)
        {
            try
            {
                Player player = Player.m_localPlayer;
                string text = "jugador a " + (player == null ? "?" : Vector3.Distance(player.transform.position, position).ToString("F0") + " m")
                    + ", cargado " + harvest.IsOverweight();
                Inventory inventory = humanoid?.GetInventory();
                if (inventory != null)
                {
                    bool chop = harvest._targetType == "TreeLog" || harvest._targetType == "TreeBase";
                    var tools = new System.Collections.Generic.List<string>();
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                    {
                        if (item?.m_shared == null) continue;
                        HitData.DamageTypes damage = item.GetDamage();
                        if (chop ? damage.m_chop <= 0f : damage.m_pickaxe <= 0f) continue;
                        if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow) continue;
                        tools.Add(item.m_shared.m_name + " (durabilidad " + item.m_durability.ToString("F0")
                            + ", equipada " + humanoid.IsItemEquiped(item) + ")");
                    }
                    text += ", herramientas " + (tools.Count == 0 ? "ninguna" : string.Join("; ", tools))
                        + ", espacios libres " + inventory.GetEmptySlots();
                }
                Animator animator = harvest.GetComponentInChildren<Animator>();
                if (animator != null) text += ", animación " + animator.cullingMode + (animator.enabled ? "" : " apagada");
                Renderer renderer = harvest.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null) text += ", visible " + renderer.isVisible;
                return text;
            }
            catch (System.Exception ex)
            {
                return "sin más datos (" + ex.Message + ")";
            }
        }

        private static readonly HashSet<string> _focusLogged = new HashSet<string>();

        /// <summary>
        /// Con un encargo de un recurso concreto ("madera resistente"), solo vale lo que suelta ese recurso: Reborn talaba
        /// cualquier árbol y el encargo acababa con madera corriente.
        /// </summary>
        private static void GatherFocusPostfix(Companions.HarvestController h, GameObject go, int mode, ref bool __result)
        {
            try
            {
                if (!__result || h == null || go == null) return;
                // Lo que queda al borde de la zona que el juego simula no se toca: allí se quedaría congelado.
                if (!Companions.MHR_Loaded.Safe(go.transform.position, 8f)) { __result = false; return; }
                string item = Plugin.GatherFocus(h);
                if (item == null || !CompanionActions.GatherNeedsSource(item, mode)) return;
                List<string> drops = Companions.MHR_GatherRules.DropsOf(go);
                if (drops == null || drops.Count == 0 || drops.Contains(item)) return;
                __result = false;
                if (_focusLogged.Count > 200) _focusLogged.Clear();
                if (_focusLogged.Add(item + "|" + go.name))
                    Plugin.Log.LogInfo("[Recoger] " + CompanionBridge.Name(h) + " deja " + go.name + ": no da " + item + ".");
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError("[Recoger] " + ex);
            }
        }

        private static void StuckGatherPostfix(Companions.HarvestController __instance)
        {
            try
            {
                float now = Time.time;
                if (!_gatherWatch.TryGetValue(__instance, out GatherWatch watch))
                {
                    if (_gatherWatch.Count > 50) _gatherWatch.Clear();
                    watch = new GatherWatch();
                    _gatherWatch[__instance] = watch;
                }
                if (watch.StepUntil > now)
                {
                    var stepAi = __instance.GetComponent<Companions.CompanionAI>();
                    if (stepAi != null && __instance._target != null && StandingOn(__instance.transform.position, __instance._target))
                    {
                        stepAi.MoveTowards(watch.StepDir, run: false);
                        return;
                    }
                    watch.StepUntil = 0f;
                }
                if (now < watch.NextCheck) return;
                watch.NextCheck = now + StuckCheckSeconds;

                GameObject target = __instance._target;
                var ai = __instance.GetComponent<Companions.CompanionAI>();
                bool working = (__instance.IsMoving || __instance.IsAttacking) && target != null;
                bool waiting = (ai != null && ai.IsInCombat) || Companions.CompanionRest.MHR_ShouldPauseWork(__instance)
                    || CompanionBridge.FarFromPlayer(__instance) > 0f
                    || (bool)_catchingBreath.GetValue(__instance);
                int swings = (int)_swingCount.GetValue(__instance);
                float lastHit = (float)_lastHitAt.GetValue(__instance);
                Vector3 position = __instance.transform.position;
                bool progress = target != watch.Target || swings != watch.Swings || lastHit != watch.LastHit
                    || Vector3.Distance(position, watch.Anchor) > StuckMoveDistance;
                if (!working || waiting || progress)
                {
                    watch.Target = working ? target : null;
                    watch.Since = now;
                    watch.Anchor = position;
                    watch.Swings = swings;
                    watch.LastHit = lastHit;
                    return;
                }
                if (now - watch.Since >= StepOffAfterSeconds && now - watch.Since < StuckGatherSeconds
                    && (__instance._targetType == "MineRock5" || __instance._targetType == "MineRock")
                    && ai != null && StandingOn(position, target) && TryStepOff(__instance, watch, target, position, now))
                {
                    return;
                }
                if (now - watch.Since < StuckGatherSeconds) return;

                watch.Target = null;
                watch.Since = now;
                var humanoid = __instance.GetComponent<Humanoid>();
                ItemDrop.ItemData hand = humanoid == null ? null : Companions.ReflectionHelper.GetRightItem(humanoid);
                bool seated = ai != null && Companions.MHR_Follow.IdleSeated(ai);
                float aboveGround = ZoneSystem.instance == null ? 0f : position.y - ZoneSystem.instance.GetGroundHeight(position);
                Plugin.Log.LogInfo("[Recoger] " + CompanionBridge.Name(__instance) + " lleva " + StuckGatherSeconds.ToString("F0")
                    + " s sin golpear ni moverse con " + target.name + " (" + __instance._targetType + ", " + __instance.CurrentStateName
                    + "): en la mano " + (hand?.m_shared?.m_name ?? "nada") + ", atacando " + (humanoid != null && humanoid.InAttack())
                    + ", en el suelo " + (humanoid != null && humanoid.IsOnGround()) + ", nadando " + (humanoid != null && humanoid.IsSwimming())
                    + ", sentado " + seated + ", a " + aboveGround.ToString("F1") + " m sobre el terreno en " + position.ToString("F1")
                    + ". " + StuckDetails(__instance, humanoid, position) + ". Lo deja un rato y busca otro.");
                if (seated) Companions.MHR_Follow.IdleStand(ai, "stuck at work");
                __instance._blacklist[target.GetInstanceID()] = now;
                __instance.ResetToIdle();
            }
            catch (System.Exception ex)
            {
                // Se ejecuta en cada fotograma de Offline Companions: mejor que siga con su propia decisión.
                Plugin.Log.LogError("[Recoger] " + ex);
            }
        }

        /// <summary>
        /// Al ir hacia el trozo más cercano de una mena puede acabar subido encima y sin golpear nada. Offline Companions
        /// no lo baja, porque cualquier escalón de más de 1,2 m le parece una caída. Aquí baja por el lado donde la caída
        /// no hace daño y no hay agua, y vuelve a picar desde el suelo. A la tercera vez con la misma roca la deja un rato.
        /// </summary>
        private static bool TryStepOff(Companions.HarvestController harvest, GatherWatch watch, GameObject target, Vector3 position, float now)
        {
            if (watch.StepTarget != target)
            {
                watch.StepTarget = target;
                watch.StepCount = 0;
            }
            string name = CompanionBridge.Name(harvest);
            if (watch.StepCount >= StepOffTries)
            {
                Plugin.Log.LogInfo("[Recoger] " + name + " se vuelve a subir a " + target.name + "; la deja un rato y busca otra.");
                watch.StepTarget = null;
                watch.Target = null;
                watch.Since = now;
                harvest._blacklist[target.GetInstanceID()] = now;
                harvest.ResetToIdle();
                return true;
            }
            if (!StepOffDirection(position, target, out Vector3 dir)) return false;
            watch.StepCount++;
            watch.StepDir = dir;
            watch.StepUntil = now + StepOffSeconds;
            watch.Since = now;
            watch.Anchor = position;
            Plugin.Log.LogInfo("[Recoger] " + name + " está subido a " + target.name + " sin poder picar; baja para golpearla desde el suelo.");
            return true;
        }

        private static bool StandingOn(Vector3 position, GameObject target)
        {
            RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 0.5f, Vector3.down, 1.5f, -1, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && hit.collider.transform.IsChildOf(target.transform)) return true;
            }
            return false;
        }

        private static bool StepOffDirection(Vector3 position, GameObject target, out Vector3 best)
        {
            best = Vector3.zero;
            if (ZoneSystem.instance == null) return false;
            Vector3 outward = position - target.transform.position;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
            outward.Normalize();
            float bestDistance = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * outward;
                for (float d = 1f; d <= 8f; d += 0.5f)
                {
                    Vector3 p = position + dir * d;
                    if (StandingOn(new Vector3(p.x, position.y, p.z), target)) continue;
                    float ground = ZoneSystem.instance.GetGroundHeight(p);
                    float beyond = ZoneSystem.instance.GetGroundHeight(p + dir * 1.5f);
                    bool safe = position.y - ground <= StepOffMaxDrop
                        && ground > ZoneSystem.instance.m_waterLevel + 0.3f
                        && ground - beyond < 2f;
                    // Cerca del lado de fuera gana por poco: así no cruza la roca entera.
                    float score = d + Mathf.Min(i, 16 - i) * 0.05f;
                    if (safe && score < bestDistance)
                    {
                        bestDistance = score;
                        best = dir;
                    }
                    break;
                }
            }
            return bestDistance < float.MaxValue;
        }

        /// <summary>Cuántos compañeros puede haber en un mundo, contando los muertos que van a revivir.</summary>
        public const int MaxCompanionsPerWorld = 5;
        private static FieldInfo _zdosById;

        /// <summary>
        /// Todo compañero nuevo (el inicial, el que invoca Odín por monedas y el que se compra al comerciante) sale de
        /// SpawnCompanion. Los que reviven no pasan por ahí, así que nunca se pierde uno que ya se tenía.
        /// </summary>
        private static void ApplyCompanionLimit(Harmony harmony)
        {
            MethodInfo spawn = AccessTools.Method(typeof(Companions.CompanionManager), "SpawnCompanion");
            _zdosById = AccessTools.Field(typeof(ZDOMan), "m_objectsByID");
            if (spawn == null)
            {
                Plugin.Log.LogWarning("El límite de " + MaxCompanionsPerWorld + " compañeros por mundo no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(spawn, prefix: Hook(nameof(CompanionLimitPrefix)));
        }

        private static bool CompanionLimitPrefix(ref bool __result)
        {
            try
            {
                int count = CompanionsInWorld();
                if (count < MaxCompanionsPerWorld) return true;
                Plugin.Log.LogInfo("[Compañeros] No se crea otro: ya hay " + count + " en este mundo (máximo " + MaxCompanionsPerWorld + ").");
                MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, ModLang.Pick(
                    "Ya tienes " + MaxCompanionsPerWorld + " compañeros en este mundo, el máximo.",
                    "You already have " + MaxCompanionsPerWorld + " companions in this world, the maximum.",
                    "У тебя уже есть максимум спутников в этом мире: " + MaxCompanionsPerWorld + "."));
                __result = false;
                return false;
            }
            catch (System.Exception ex)
            {
                // Ante la duda, se deja a Offline Companions hacer lo suyo.
                Plugin.Log.LogError("[Compañeros] " + ex);
                return true;
            }
        }

        // Quien tiene el mundo (la partida en solitario, o el anfitrión) ve todos sus objetos: también los compañeros
        // lejanos y las tumbas de los que van a revivir. Un invitado solo puede contar los que tiene cerca.
        private static int CompanionsInWorld()
        {
            var zdos = ZNet.instance != null && ZNet.instance.IsServer() && ZDOMan.instance != null
                ? _zdosById?.GetValue(ZDOMan.instance) as System.Collections.IDictionary : null;
            if (zdos == null) return Companions.CompanionSetup.AllCompanions.Count;

            int human = "HC_Companion".GetStableHashCode();
            int living = 0;
            var alive = new HashSet<string>();
            var waiting = new HashSet<string>();
            foreach (object value in zdos.Values)
            {
                if (!(value is ZDO zdo)) continue;
                int prefab = zdo.GetPrefab();
                if (prefab == human || Companions.CompanionTierData.IsDvergerVariant(prefab))
                {
                    living++;
                    string id = zdo.GetString(Companions.CompanionSetup.MHR_CompanionIdHash);
                    if (id.Length > 0) alive.Add(id);
                    continue;
                }
                string tomb = zdo.GetString(Companions.CompanionSetup.MHR_TombCompanionIdHash);
                if (tomb.Length > 0 && zdo.GetString(Companions.CompanionTombstoneResurrect.RespawnOwnerHash).Length > 0) waiting.Add(tomb);
            }
            waiting.ExceptWith(alive);
            return living + waiting.Count + Unsummoned();
        }

        // Los que se han despedido siguen en la lista y se pueden volver a llamar sin pasar por SpawnCompanion.
        private static int Unsummoned()
        {
            int count = 0;
            foreach (var entry in Companions.MHR_Roster.GetRoster())
                if (entry.Value == Companions.MHR_Roster.Status.Unsummoned) count++;
            return count;
        }

        /// <summary>Qué frases fijas se dejan ver. Lo decide el ajuste, que se puede cambiar en la partida.</summary>
        public static System.Func<RebornSpeechMode> SpeechMode;

        /// <summary>Las demás formas de hablar de CompanionTalk acaban aquí, y la pausa entre frases no cambia porque esta no se llega a decir.</summary>
        private static void ApplySpeechFilter(Harmony harmony)
        {
            System.Type talk = AccessTools.TypeByName("Companions.CompanionTalk");
            System.Type priority = talk == null ? null : AccessTools.Inner(talk, "Priority");
            MethodInfo speak = priority == null ? null : AccessTools.Method(talk, "Speak", new[] { typeof(string), priority, typeof(string), typeof(float) });
            if (speak == null)
            {
                Plugin.Log.LogWarning("Las frases fijas no se pueden filtrar en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(speak, prefix: Hook(nameof(SpeakPrefix)));
        }

        private static bool SpeakPrefix(object[] __args)
        {
            return SpeechMode == null || RebornSpeech.Allows(SpeechMode(), System.Convert.ToInt32(__args[1]));
        }

        /// <summary>Con un encargo de cocina no espera junto a una sola estación si puede ir llenando las demás.</summary>
        private static void ApplyCookSpread(Harmony harmony)
        {
            System.Type cook = AccessTools.TypeByName("Companions.CookController");
            System.Type task = cook == null ? null : AccessTools.Inner(cook, "Task");
            MethodInfo updateCooking = AccessTools.Method(cook, "UpdateCooking");
            MethodInfo foodTask = AccessTools.Method(cook, "FoodTask");
            MethodInfo restoreFollow = AccessTools.Method(cook, "RestoreFollow");
            _pickTask = AccessTools.Method(cook, "PickTask");
            _finishTask = AccessTools.Method(cook, "Finish");
            _pickUpDrops = AccessTools.Method(cook, "PickUpDrops");
            _dropPoint = AccessTools.Method(cook, "DropPoint");
            _cookTask = AccessTools.Field(cook, "_task");
            _cookTimer = AccessTools.Field(cook, "_timer");
            _cookers = AccessTools.Field(cook, "_cookers");
            _taskKind = task == null ? null : AccessTools.Field(task, "Kind");
            _taskCooker = task == null ? null : AccessTools.Field(task, "Cooker");
            if (!CookAvailable || updateCooking == null || foodTask == null || restoreFollow == null || _pickTask == null || _finishTask == null || _pickUpDrops == null
                || _dropPoint == null || _cookTask == null || _cookTimer == null || _cookers == null || _taskKind == null || _taskCooker == null)
            {
                Plugin.Log.LogWarning("Cocinar en varias estaciones a la vez no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(updateCooking, prefix: Hook(nameof(SpreadCooking)));
            harmony.Patch(foodTask, prefix: Hook(nameof(CountCookingNow)), finalizer: Hook(nameof(ForgetCookingNow)));
            harmony.Patch(restoreFollow, prefix: Hook(nameof(StayWhileCooking)));
        }

        /// <summary>
        /// Al pelear uno contra uno, Offline Companions espera a que el enemigo ataque para entrar. Si el enemigo no llega
        /// por el desnivel, ninguno de los dos hace nada: la espera tiene que acabarse.
        /// </summary>
        private static void ApplySpacingPatience(Harmony harmony)
        {
            System.Type fight = AccessTools.TypeByName("Companions.CompanionCombatAI");
            MethodInfo spacing = AccessTools.Method(fight, "Spacing");
            _spacingHold = AccessTools.Field(fight, "_spacingHold");
            _dipping = AccessTools.Field(fight, "_dipping");
            _dipUntil = AccessTools.Field(fight, "_dipUntil");
            _fightTarget = AccessTools.Field(fight, "_targetCreature");
            if (spacing == null || _spacingHold == null || _dipping == null || _dipUntil == null || _fightTarget == null)
            {
                Plugin.Log.LogWarning("El límite de espera en combate no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(spacing, postfix: Hook(nameof(SpacingPostfix)));
        }

        private static MethodInfo _forceMelee;
        private static MethodInfo _forceRanged;
        private static FieldInfo _fightHumanoid;
        private static FieldInfo _fightCreature;
        private const float ToolSwapLogSeconds = 30f;
        private static readonly Dictionary<Component, float> _toolSwapLogged = new Dictionary<Component, float>();

        /// <summary>
        /// En la postura de pelear a distancia, con el enemigo cerca, Offline Companions solo saca el arma de cuerpo a cuerpo
        /// si tiene las manos vacías o un arco. Con el pico de picar en la mano no cambia nada, y con el pico no le deja
        /// atacar: se queda bloqueando sin dar un golpe.
        /// </summary>
        private static void ApplyToolInFight(Harmony harmony)
        {
            System.Type fight = AccessTools.TypeByName("Companions.CompanionCombatAI");
            MethodInfo select = fight == null ? null : AccessTools.Method(fight, "SelectBestAttack");
            _forceMelee = fight == null ? null : AccessTools.Method(fight, "TryForceMeleeWeapon");
            _forceRanged = fight == null ? null : AccessTools.Method(fight, "TryForceRangedWeapon");
            _fightHumanoid = fight == null ? null : AccessTools.Field(fight, "_humanoid");
            _fightCreature = fight == null ? null : AccessTools.Field(fight, "_targetCreature");
            if (select == null || _forceMelee == null || _forceRanged == null || _fightHumanoid == null || _fightCreature == null)
            {
                Plugin.Log.LogWarning("Cambiar la herramienta por un arma al pelear no está disponible en esta versión de Offline Companions Reborn: puede quedarse con el pico en la mano.");
                return;
            }
            harmony.Patch(select, postfix: Hook(nameof(WeaponOutForFight)));
        }

        // Las mismas cosas con las que Offline Companions no deja atacar.
        private static bool CannotFightWith(ItemDrop.ItemData item)
        {
            return item.m_shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod
                || item.GetDamage().m_pickaxe > 0f
                || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Tool;
        }

        private static void WeaponOutForFight(Component __instance, ref ItemDrop.ItemData __result)
        {
            try
            {
                if (__result == null || !CannotFightWith(__result) || _fightCreature.GetValue(__instance) == null) return;
                var humanoid = _fightHumanoid.GetValue(__instance) as Humanoid;
                if (humanoid == null || humanoid.InAttack()) return;
                string tool = __result.m_shared.m_name;
                // Su arma de cuerpo a cuerpo, o el arco, o los puños: cualquier cosa antes que no poder atacar.
                if (!(bool)_forceMelee.Invoke(__instance, null) && !(bool)_forceRanged.Invoke(__instance, null))
                    humanoid.UnequipItem(__result);
                __result = humanoid.GetCurrentWeapon();
                float now = Time.time;
                if (_toolSwapLogged.TryGetValue(__instance, out float last) && now - last < ToolSwapLogSeconds) return;
                _toolSwapLogged[__instance] = now;
                Plugin.Log.LogInfo("[Combate] " + CompanionBridge.Name(__instance) + ": deja " + tool + " y pelea con " + (__result?.m_shared?.m_name ?? "los puños") + ".");
            }
            catch (System.Exception ex)
            {
                // Se pide cada segundo durante la pelea: mejor que siga con lo que él eligió.
                Plugin.Log.LogError("[Combate] " + ex);
            }
        }

        private static void ApplyCooking(Harmony harmony)
        {
            MethodInfo allowed = AccessTools.Method("Companions.CookCatalog:Allowed");
            MethodInfo target = AccessTools.Method("Companions.CookCatalog:Target");
            MethodInfo focus = AccessTools.Method("Companions.CookCatalog:Focus");
            // Se intercepta Usable y no la propiedad Reserve, que es tan pequeña que el motor puede incrustarla en quien la llama.
            MethodInfo usable = AccessTools.Method(AccessTools.TypeByName("Companions.CookController"), "Usable", new[] { typeof(string) });
            if (allowed == null || target == null || focus == null || usable == null)
            {
                Plugin.Log.LogWarning("Cocinar una comida concreta no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(allowed, postfix: Hook(nameof(AllowedPostfix)));
            harmony.Patch(target, postfix: Hook(nameof(TargetPostfix)));
            harmony.Patch(focus, postfix: Hook(nameof(FocusPostfix)));
            harmony.Patch(usable, postfix: Hook(nameof(UsablePostfix)));
            CookAvailable = true;
        }

        private static void ApplyUpkeep(Harmony harmony)
        {
            System.Type upkeep = AccessTools.TypeByName("Companions.UpkeepController");
            MethodInfo[] plans =
            {
                AccessTools.Method(upkeep, "PlanRepair"),
                AccessTools.Method(upkeep, "PlanFuel"),
                AccessTools.Method(upkeep, "PlanHoney"),
                AccessTools.Method(upkeep, "PlanPiles"),
                AccessTools.Method(upkeep, "PlanSort")
            };
            if (!System.Array.TrueForAll(plans, m => m != null))
            {
                Plugin.Log.LogWarning("Las tareas sueltas de mantenimiento no están disponibles en esta versión de Offline Companions Reborn.");
                return;
            }
            foreach (MethodInfo plan in plans) harmony.Patch(plan, prefix: Hook(nameof(OnlyFocusedPlan)));
            UpkeepAvailable = true;
        }

        private static void ApplySmelting(Harmony harmony)
        {
            System.Type smelt = AccessTools.TypeByName("Companions.SmeltController");
            MethodInfo refill = AccessTools.Method(smelt, "TryPlanRefill");
            MethodInfo count = AccessTools.Method(smelt, "CountItemByPrefab");
            MethodInfo pile = AccessTools.Method(smelt, "TryStartPile");
            MethodInfo scan = AccessTools.Method(smelt, "UpdateScanning");
            _unclaimSmelter = AccessTools.Method(smelt, "UnclaimSmelter");
            if (refill == null || count == null || pile == null || scan == null || _unclaimSmelter == null)
            {
                Plugin.Log.LogWarning("Fundir un material concreto no está disponible en esta versión de Offline Companions Reborn.");
                return;
            }
            harmony.Patch(scan, prefix: Hook(nameof(FreeOwnSmelter)));
            harmony.Patch(refill, prefix: Hook(nameof(RefillStart)), finalizer: Hook(nameof(RefillEnd)));
            harmony.Patch(count, postfix: Hook(nameof(CountPostfix)));
            harmony.Patch(pile, prefix: Hook(nameof(PileOnlyForItem)));
            SmeltAvailable = true;

            MethodInfo output = AccessTools.Method(smelt, "FindChestWithSpace");
            _smeltChests = AccessTools.Field(smelt, "_nearbyChests");
            _smeltSkipChest = AccessTools.Method(smelt, "SkipChest");
            if (output == null || _smeltChests == null || _smeltSkipChest == null)
            {
                Plugin.Log.LogWarning("Guardar lo fundido junto a lo mismo no está disponible en esta versión de Offline Companions Reborn: irá al cofre más cercano con sitio.");
                return;
            }
            harmony.Patch(output, postfix: Hook(nameof(OutputWithSame)));
        }

        private static FieldInfo _smeltChests;
        private static MethodInfo _smeltSkipChest;

        private const int MatchSame = 3;
        private const int MatchFamily = 2;
        private const int MatchSource = 1;

        /// <summary>
        /// Offline Companions lo deja en el cofre más cercano con un hueco libre, tenga lo que tenga. Se prefiere, por
        /// orden: un cofre con lo mismo; uno con otra cosa del mismo horno (el estaño junto al cobre o al hierro); uno
        /// con su mineral. Lo que da cada horno sale de todos los hornos del juego, no del que tiene asignado ahora: al
        /// recoger el carbón del horno de carbón, el asignado puede ser ya la fundición.
        /// </summary>
        private static void OutputWithSame(Component __instance, ref Container __result)
        {
            Inventory pack = __instance.GetComponent<Humanoid>()?.GetInventory();
            var chests = _smeltChests.GetValue(__instance) as List<Container>;
            if (pack == null || chests == null || !SmeltFamiliesReady()) return;

            Container best = null;
            int bestMatch = 0;
            int bestAmount = 0;
            float bestDistance = float.MaxValue;
            foreach (Container chest in chests)
            {
                if (chest == null || (bool)_smeltSkipChest.Invoke(__instance, new object[] { chest })) continue;
                Inventory inventory = chest.GetInventory();
                int amount = 0;
                int match = inventory == null ? 0 : OutputMatch(pack, inventory, out amount);
                if (match == 0) continue;
                float distance = Vector3.Distance(__instance.transform.position, chest.transform.position);
                // Entre dos cofres que encajan igual gana el que guarda más de esa familia: si una vez cayó estaño en
                // otro cofre, el más cercano no se queda con todo lo que sigue.
                bool better = match > bestMatch
                    || match == bestMatch && amount > bestAmount
                    || match == bestMatch && amount == bestAmount && distance < bestDistance;
                if (better)
                {
                    best = chest;
                    bestMatch = match;
                    bestAmount = amount;
                    bestDistance = distance;
                }
            }
            if (best != null) __result = best;
        }

        // Por cada cosa que sale de un horno: lo demás que sale de esos hornos, y de qué se hace.
        private static Dictionary<string, HashSet<string>> _smeltFamily;
        private static Dictionary<string, HashSet<string>> _smeltSource;

        private static bool SmeltFamiliesReady()
        {
            if (_smeltFamily != null) return true;
            if (ZNetScene.instance == null) return false;
            var family = new Dictionary<string, HashSet<string>>();
            var source = new Dictionary<string, HashSet<string>>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Smelter smelter = prefab != null ? prefab.GetComponent<Smelter>() : null;
                if (smelter?.m_conversion == null) continue;
                var outputs = new List<string>();
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                {
                    if (conversion?.m_to == null) continue;
                    string output = conversion.m_to.gameObject.name;
                    outputs.Add(output);
                    if (!source.TryGetValue(output, out HashSet<string> from)) source[output] = from = new HashSet<string>();
                    if (conversion.m_from != null) from.Add(conversion.m_from.gameObject.name);
                }
                foreach (string output in outputs)
                {
                    if (!family.TryGetValue(output, out HashSet<string> siblings)) family[output] = siblings = new HashSet<string>();
                    foreach (string other in outputs) siblings.Add(other);
                }
            }
            if (family.Count == 0) return false;
            _smeltFamily = family;
            _smeltSource = source;
            return true;
        }

        /// <summary>Lo bien que encaja el cofre con lo fundido que lleva: 0 si no le cabe o no tiene nada parecido.
        /// <paramref name="amount"/> es cuánto guarda ya de esa familia, para desempatar.</summary>
        private static int OutputMatch(Inventory pack, Inventory chest, out int amount)
        {
            var inChest = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData stored in chest.GetAllItems())
            {
                string storedName = stored?.m_dropPrefab?.name;
                if (storedName == null) continue;
                inChest.TryGetValue(storedName, out int count);
                inChest[storedName] = count + stored.m_stack;
            }
            int best = 0;
            amount = 0;
            foreach (ItemDrop.ItemData item in pack.GetAllItems())
            {
                string name = item?.m_dropPrefab?.name;
                if (name == null || item.m_shared == null || !_smeltFamily.TryGetValue(name, out HashSet<string> siblings)) continue;
                if (!chest.CanAddItem(item, item.m_stack)) continue;
                int family = 0;
                foreach (string sibling in siblings)
                {
                    if (inChest.TryGetValue(sibling, out int count)) family += count;
                }
                int match = inChest.ContainsKey(name) ? MatchSame
                    : family > 0 ? MatchFamily
                    : _smeltSource.TryGetValue(name, out HashSet<string> from) && HasAny(inChest, from) ? MatchSource
                    : 0;
                if (match > best || match == best && family > amount)
                {
                    best = match;
                    amount = family;
                }
            }
            return best;
        }

        private static bool HasAny(Dictionary<string, int> inChest, HashSet<string> names)
        {
            foreach (string name in names)
            {
                if (inChest.ContainsKey(name)) return true;
            }
            return false;
        }

        private static HarmonyMethod Hook(string name)
        {
            return new HarmonyMethod(typeof(BehaviorOverrides), name);
        }

        private static void AllowedPostfix(string item, ref bool __result)
        {
            if (CookFood != null) __result = item == CookFood;
        }

        private static void TargetPostfix(string item, ref int __result)
        {
            // Lo que ya está al fuego cuenta, o llenaría cada estación y cocinaría de más.
            if (CookFood != null && item == CookFood) __result = CookTarget - _cookingNow;
        }

        private static void CountCookingNow(Component __instance)
        {
            _cookingNow = CookFood == null ? 0 : CookingNow(__instance);
        }

        private static void ForgetCookingNow()
        {
            _cookingNow = 0;
        }

        private static int CookingNow(Component cook)
        {
            int count = 0;
            foreach (CookingStation station in (List<CookingStation>)_cookers.GetValue(cook))
            {
                ZDO zdo = station == null ? null : station.GetComponent<ZNetView>()?.GetZDO();
                if (zdo == null || station.m_slots == null) continue;
                for (int i = 0; i < station.m_slots.Length; i++)
                {
                    string item = zdo.GetString("slot" + i);
                    if (item.Length == 0) continue;
                    int status = zdo.GetInt("slotstatus" + i);
                    if (status == SlotCooking ? CooksInto(station, item, CookFood) : status == SlotDone && item == CookFood) count++;
                }
            }
            return count;
        }

        private static bool CooksInto(CookingStation station, string from, string to)
        {
            foreach (CookingStation.ItemConversion conversion in station.m_conversion)
            {
                if (conversion?.m_from != null && conversion.m_to != null && conversion.m_from.gameObject.name == from && conversion.m_to.gameObject.name == to) return true;
            }
            return false;
        }

        private static int SlotsWith(CookingStation station, int status)
        {
            ZDO zdo = station.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null || station.m_slots == null) return 0;
            int count = 0;
            for (int i = 0; i < station.m_slots.Length; i++)
            {
                if (zdo.GetString("slot" + i).Length > 0 && zdo.GetInt("slotstatus" + i) == status) count++;
            }
            return count;
        }

        /// <summary>
        /// Mientras espera a que algo se haga, deja de esperar si Offline Companions ve otra cosa que hacer con las estaciones:
        /// sacar lo que ya está hecho en otra o poner más en alguna libre.
        /// </summary>
        private static bool SpreadCooking(Component __instance)
        {
            if (CookFood == null || (float)_cookTimer.GetValue(__instance) > Time.deltaTime) return true;

            object task = _cookTask.GetValue(__instance);
            if (task == null || _taskKind.GetValue(task).ToString() != "Grill") return true;
            var station = _taskCooker.GetValue(task) as CookingStation;
            // Lo terminado en esta, y acabar cuando no quede nada, lo sigue llevando Offline Companions.
            if (station == null || SlotsWith(station, SlotDone) + SlotsWith(station, SlotBurnt) > 0 || SlotsWith(station, SlotCooking) == 0) return true;

            object next = _pickTask.Invoke(__instance, null);
            if (next == null) return true;
            string kind = _taskKind.GetValue(next).ToString();
            bool sameStation = (_taskCooker.GetValue(next) as CookingStation) == station;
            if (kind != "Grill" && (kind != "Rescue" || sameStation)) return true;

            object point = _dropPoint.Invoke(null, new object[] { station });
            _pickUpDrops.Invoke(__instance, new object[] { point, CookDropRadius, true });
            _finishTask.Invoke(__instance, null);
            return false;
        }

        // Si cree que no hay nada que hacer pero hay comida al fuego, seguir al jugador lo alejaría de ella justo antes de que se haga.
        private static bool StayWhileCooking(Component __instance)
        {
            return CookFood == null || CookingNow(__instance) == 0;
        }

        private static void SpacingPostfix(Component __instance, ref int __result)
        {
            if (__result != 2 || !(bool)_spacingHold.GetValue(__instance))
            {
                _holdingSince.Remove(__instance);
                return;
            }
            if (!_holdingSince.TryGetValue(__instance, out float since))
            {
                _holdingSince[__instance] = Time.time;
                return;
            }
            if (Time.time - since < SpacingPatienceSeconds) return;

            var target = _fightTarget.GetValue(__instance) as Character;
            if (target == null || target.InAttack()) return;

            _holdingSince.Remove(__instance);
            _spacingHold.SetValue(__instance, false);
            _dipping.SetValue(__instance, true);
            _dipUntil.SetValue(__instance, Time.time + SpacingDipSeconds);
            __result = 0;

            Vector3 gap = target.transform.position - __instance.transform.position;
            Plugin.Log.LogInfo("[Combate] " + CompanionBridge.Name(__instance) + ": deja de esperar a " + target.m_name + " y entra a atacar (a " + gap.magnitude.ToString("F1") + " m, desnivel " + gap.y.ToString("+0.0;-0.0") + " m)");
        }

        // Mientras cocina un encargo no se pone a preparar hidromiel.
        private static void FocusPostfix(ref List<string> __result)
        {
            if (CookFood != null) __result = new List<string>();
        }

        // El jugador ha pedido esa comida expresamente: puede usar los ingredientes que normalmente deja de reserva.
        private static void UsablePostfix(Component __instance, string item, ref int __result)
        {
            if (CookFood != null) __result = CompanionBridge.CookStock(__instance, item);
        }

        private static bool OnlyFocusedPlan(Component __instance, MethodBase __originalMethod, ref bool __result)
        {
            if (!UpkeepFocus.TryGetValue(__instance.gameObject, out string plan) || plan == __originalMethod.Name) return true;
            __result = false;
            return false;
        }

        // Offline Companions reserva el horno al trabajar con él y solo lo libera si se interrumpe. Al revisar de nuevo,
        // descartaría el suyo por estar reservado y, con un solo horno, creería que no hay nada que hacer.
        private static void FreeOwnSmelter(Component __instance)
        {
            _unclaimSmelter.Invoke(__instance, null);
        }

        // Solo durante esta decisión: el combustible se cuenta con la misma función fuera de ella y no debe verse afectado.
        private static void RefillStart()
        {
            _inRefill = true;
        }

        private static void RefillEnd()
        {
            _inRefill = false;
        }

        private static void CountPostfix(string __1, ref int __result)
        {
            if (_inRefill && SmeltItem != null && __1 != SmeltItem) __result = 0;
        }

        private static bool PileOnlyForItem(string __0, ref bool __result)
        {
            if (!_inRefill || SmeltItem == null || __0 == SmeltItem) return true;
            __result = false;
            return false;
        }
    }
}
