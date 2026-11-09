using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>Observa la partida una vez por segundo y avisa de los sucesos que merecen un comentario.</summary>
    public sealed class WorldEvents
    {
        private const float BiomeSettleSeconds = 10f;
        private const float LowHealthFraction = 0.25f;
        private const float LowHealthRecovered = 0.5f;
        // Hasta donde puede estar trabajando. Solo es la condición para buscar su tumba, que es lo que confirma la muerte.
        private const float NearForDeathCheck = 100f;
        private const float FirstNeedDelay = 60f;
        private const float NeedCheckInterval = 20f;
        private const float NeedCooldown = 600f;

        private sealed class Tracked
        {
            public Component Companion;
            public float LastDistance;
            public bool LowHealthSaid;
            public bool MissingAfterNearby;
            public bool DeathChecked;
            public string LastPlace;
            public string ScarePlace;

            // No nada más entrar en el mundo: el compañero acaba de cargarse y quizá aún no ha comido.
            public float NextNeedCheckAt = Time.time + FirstNeedDelay;
            public readonly Dictionary<string, float> NeedSaidAt = new Dictionary<string, float>();
        }

        private readonly ModSettings _settings;
        private readonly Action<Reaction> _emit;
        private readonly Action<Component, int, string, bool> _remember;
        private readonly Dictionary<string, Tracked> _companions = new Dictionary<string, Tracked>();
        private readonly Func<Vector3, string> _placeAt;
        private bool _playerScared;
        private string _playerScarePlace;
        private readonly HashSet<string> _knownBossKeys = new HashSet<string>();
        // Los minijefes del mod Monstrum de Therzie no dejan marca en el mundo al morir, como los jefes del juego.
        private static readonly HashSet<string> MiniBossPrefabs = new HashSet<string> { "BossAsmodeus_TW", "BossSvalt_TW", "BossVrykolathas_TW" };
        private const float MiniBossRange = 60f;
        private const float MiniBossNearlyDead = 0.35f;
        private readonly Dictionary<Character, KeyValuePair<string, float>> _miniBosses = new Dictionary<Character, KeyValuePair<string, float>>();
        private readonly HashSet<string> _visitedBiomes = new HashSet<string>();
        private ZoneSystem _world;
        private bool _bossBaselineDone;
        private Heightmap.Biome _candidateBiome;
        private float _candidateSince;
        private bool _playerWasDead;
        private string _raid;

        /// <summary>
        /// remember anota un suceso en la memoria, aunque los comentarios estén desactivados: el compañero concreto
        /// o, si es null, los que estén cerca; su importancia; el texto; y si solo cuenta la primera vez.
        /// placeAt dice en qué lugar de expedición cae una posición, o null, para que el recuerdo diga dónde pasó.
        /// </summary>
        public WorldEvents(ModSettings settings, Action<Reaction> emit, Action<Component, int, string, bool> remember, Func<Vector3, string> placeAt)
        {
            _settings = settings;
            _emit = emit;
            _remember = remember;
            _placeAt = placeAt;
        }

        public void Tick(Player player)
        {
            if (_world != ZoneSystem.instance) ResetForNewWorld();
            if (_world == null || player == null) return;

            CheckPlayerDeath(player);
            if (player.IsDead()) return;

            CheckPlayerScare(player);
            CheckBosses();
            CheckMiniBosses(player);
            CheckRaid();
            CheckBiome(player);
            CheckCompanions(player);
        }

        private void ResetForNewWorld()
        {
            _world = ZoneSystem.instance;
            _companions.Clear();
            _knownBossKeys.Clear();
            _visitedBiomes.Clear();
            _bossBaselineDone = false;
            _candidateBiome = Heightmap.Biome.None;
            _playerWasDead = false;
            _playerScared = false;
            _raid = null;
            _miniBosses.Clear();
        }

        private void CheckPlayerDeath(Player player)
        {
            bool dead = player.IsDead();
            if (_playerWasDead && !dead)
            {
                // Reaparece lejos de donde estaban: se enteran todos, no solo los que queden cerca.
                foreach (Component companion in CompanionBridge.MyCompanions())
                {
                    _remember(companion, MemoryWeight.Death, player.GetPlayerName() + ModLang.Pick(" murió y volvió a aparecer.", " died and respawned.", " погиб и возродился."), false);
                }
                if (_settings.ReactPlayerDeath.Value)
                {
                    _emit(new Reaction
                    {
                        Kind = ReactionKind.PlayerDied,
                        Description = ModLang.Pick("El jugador acaba de morir y ha reaparecido.", "The player just died and respawned.", "Игрок только что погиб и возродился.")
                    });
                }
            }
            if (dead) _playerScared = false;
            _playerWasDead = dead;
        }

        private void CheckPlayerScare(Player player)
        {
            float share = player.GetHealthPercentage();
            if (share <= LowHealthFraction)
            {
                if (!_playerScared) _playerScarePlace = _placeAt(player.transform.position);
                _playerScared = true;
            }
            else if (share >= LowHealthRecovered && _playerScared)
            {
                _playerScared = false;
                _remember(null, MemoryWeight.Notable, ExpeditionText.PlayerNearDeath(player.GetPlayerName(), _playerScarePlace), false);
            }
        }

        private void CheckBosses()
        {
            foreach (string key in _world.GetGlobalKeys())
            {
                string boss = ReactionPolicy.BossName(key);
                if (boss == null || !_knownBossKeys.Add(key)) continue;
                // Los jefes ya derrotados al cargar el mundo no son noticia.
                if (_bossBaselineDone)
                {
                    _remember(null, MemoryWeight.Boss, ModLang.Pick("Derrotamos a " + boss + ".", "We defeated " + boss + ".", "Мы победили босса: " + boss + "."), true);
                    if (_settings.ReactBoss.Value)
                    {
                        _emit(new Reaction
                        {
                            Kind = ReactionKind.BossDefeated,
                            Description = ModLang.Pick("Acaban de derrotar a " + boss + ".", "You just defeated " + boss + ".", "Вы только что победили босса: " + boss + ".")
                        });
                    }
                }
            }
            _bossBaselineDone = true;
        }

        /// <summary>
        /// Un minijefe que estaba cerca y casi muerto, y que ya no está o ha muerto, es que lo derrotaron. Si se aleja
        /// o se descarga con salud, no cuenta.
        /// </summary>
        private void CheckMiniBosses(Player player)
        {
            Vector3 at = player.transform.position;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || _miniBosses.ContainsKey(c) || c.GetHealth() <= 0f) continue;
                if (!MiniBossPrefabs.Contains(Utils.GetPrefabName(c.gameObject)) || Vector3.Distance(at, c.transform.position) > MiniBossRange) continue;
                string name = Localization.instance != null ? Localization.instance.Localize(c.m_name) : c.m_name;
                _miniBosses[c] = new KeyValuePair<string, float>(name, 1f);
            }
            if (_miniBosses.Count == 0) return;

            var gone = new List<Character>();
            var defeated = new List<string>();
            foreach (KeyValuePair<Character, KeyValuePair<string, float>> boss in _miniBosses)
            {
                Character c = boss.Key;
                bool dead = c == null || c.IsDead() || c.GetHealth() <= 0f;
                if (dead)
                {
                    gone.Add(c);
                    if (boss.Value.Value <= MiniBossNearlyDead) defeated.Add(boss.Value.Key);
                }
                else if (Vector3.Distance(at, c.transform.position) > MiniBossRange * 1.5f) gone.Add(c);
            }
            foreach (Character c in gone) _miniBosses.Remove(c);
            foreach (Character c in new List<Character>(_miniBosses.Keys))
            {
                if (c.GetMaxHealth() > 0f) _miniBosses[c] = new KeyValuePair<string, float>(_miniBosses[c].Key, c.GetHealth() / c.GetMaxHealth());
            }
            foreach (string name in defeated)
            {
                _remember(null, MemoryWeight.Boss, ModLang.Pick("Derrotamos a " + name + ".", "We defeated " + name + ".", "Мы победили: " + name + "."), false);
                if (_settings.ReactBoss.Value)
                {
                    _emit(new Reaction
                    {
                        Kind = ReactionKind.BossDefeated,
                        Description = ModLang.Pick("Acaban de derrotar a " + name + ", un jefe menor.", "You just defeated " + name + ", a lesser boss.", "Вы только что победили " + name + ", мини-босса.")
                    });
                }
            }
        }

        private void CheckRaid()
        {
            string raid = CompanionBridge.ActiveRaid();
            if (raid == _raid) return;
            bool ended = raid == null;
            string previous = _raid;
            _raid = raid;
            if (ended && previous != null)
            {
                Player player = Player.m_localPlayer;
                _remember(null, MemoryWeight.Notable, ExpeditionText.Raid(player != null ? _placeAt(player.transform.position) : null), false);
            }
            if (!_settings.ReactRaid.Value) return;
            if (!ended)
            {
                _emit(new Reaction
                {
                    Kind = ReactionKind.RaidStarted,
                    Description = ModLang.Pick("Empieza una incursión de enemigos: ", "An enemy raid has started: ", "Начался рейд врагов: ") + raid
                });
            }
            else if (previous != null)
            {
                _emit(new Reaction
                {
                    Kind = ReactionKind.RaidEnded,
                    Description = ModLang.Pick("La incursión de enemigos ha terminado.", "The enemy raid has ended.", "Рейд врагов закончился.")
                });
            }
        }

        private void CheckBiome(Player player)
        {
            Heightmap.Biome biome = player.GetCurrentBiome();
            if (biome != _candidateBiome)
            {
                _candidateBiome = biome;
                _candidateSince = Time.time;
                return;
            }
            // Evita comentar los cruces breves por la frontera entre biomas.
            if (Time.time - _candidateSince < BiomeSettleSeconds) return;

            string key = biome.ToString();
            string name = ReactionPolicy.BiomeName(key);
            if (name == null) return;
            bool firstOfSession = _visitedBiomes.Count == 0;
            if (_visitedBiomes.Add(key) && !firstOfSession)
            {
                string visitedLine = ModLang.IsRussian
                    ? "Мы прибыли в " + name + "."
                    : (ModLang.IsSpanish
                        ? ("Llegamos a " + name + ".").Replace(" a el ", " al ")
                        : "We arrived at " + name + ".");
                _remember(null, MemoryWeight.Notable, visitedLine, true);
                if (_settings.ReactBiome.Value)
                {
                    _emit(new Reaction
                    {
                        Kind = ReactionKind.NewBiome,
                        Description = ModLang.Pick(
                            "Acabáis de entrar en " + name + " por primera vez en esta sesión.",
                            "You have just entered " + name + " for the first time this session.",
                            "Вы только что впервые вошли в " + name + " за эту сессию.")
                    });
                }
            }
        }

        private void CheckCompanions(Player player)
        {
            var seen = new HashSet<string>();
            foreach (Component companion in CompanionBridge.MyCompanions())
            {
                string id = CompanionBridge.PermanentId(companion);
                if (string.IsNullOrEmpty(id)) continue;
                seen.Add(id);

                float distance = Vector3.Distance(player.transform.position, companion.transform.position);
                if (!_companions.TryGetValue(id, out Tracked tracked))
                {
                    tracked = new Tracked();
                    _companions[id] = tracked;
                }
                else if (tracked.MissingAfterNearby)
                {
                    // Si no, al volver con salud contaría que escapó por poco de la misma muerte.
                    tracked.LowHealthSaid = false;
                    _remember(companion, MemoryWeight.Death, ExpeditionText.Death(tracked.LastPlace), false);
                    if (_settings.ReactCompanionReturn.Value)
                    {
                        _emit(new Reaction
                        {
                            Kind = ReactionKind.CompanionReturned,
                            Speaker = companion,
                            Description = ModLang.Pick("Acabas de volver a la vida tras morir.", "You just came back to life after dying.", "Ты только что вернулся к жизни после гибели.")
                        });
                    }
                }
                tracked.MissingAfterNearby = false;
                tracked.Companion = companion;
                tracked.LastDistance = distance;
                tracked.LastPlace = _placeAt(companion.transform.position);
                CheckLowHealth(companion, tracked);
                CheckNeeds(companion, tracked);
            }

            foreach (KeyValuePair<string, Tracked> entry in _companions)
            {
                Tracked tracked = entry.Value;
                if (seen.Contains(entry.Key))
                {
                    tracked.DeathChecked = false;
                    continue;
                }
                if (tracked.DeathChecked) continue;
                tracked.DeathChecked = true;
                // También desaparece al descargarse la zona o al cruzar un portal: solo una tumba suya confirma la muerte.
                tracked.MissingAfterNearby = tracked.LastDistance <= NearForDeathCheck && CompanionBridge.HasTombstone(entry.Key);
            }
        }

        private void CheckNeeds(Component companion, Tracked tracked)
        {
            if (!_settings.ReactNeeds.Value || CompanionBridge.InCombat(companion) || Time.time < tracked.NextNeedCheckAt) return;
            tracked.NextNeedCheckAt = Time.time + NeedCheckInterval;

            foreach (KeyValuePair<string, string> need in CompanionBridge.UnmetNeeds(companion))
            {
                if (tracked.NeedSaidAt.TryGetValue(need.Key, out float said) && Time.time - said < NeedCooldown) continue;
                tracked.NeedSaidAt[need.Key] = Time.time;
                _emit(new Reaction { Kind = ReactionKind.CompanionNeed, Speaker = companion, CanResolve = true, Description = need.Value });
                return;
            }
        }

        private void CheckLowHealth(Component companion, Tracked tracked)
        {
            Character character = companion.GetComponent<Character>();
            if (character == null || character.GetMaxHealth() <= 0f) return;

            float fraction = character.GetHealth() / character.GetMaxHealth();
            if (fraction >= LowHealthRecovered)
            {
                if (tracked.LowHealthSaid) _remember(companion, MemoryWeight.Notable, ExpeditionText.NearDeath(tracked.ScarePlace), false);
                tracked.LowHealthSaid = false;
            }
            else if (fraction <= LowHealthFraction && !tracked.LowHealthSaid)
            {
                tracked.LowHealthSaid = true;
                tracked.ScarePlace = tracked.LastPlace;
                if (_settings.ReactLowHealth.Value)
                {
                    _emit(new Reaction
                    {
                        Kind = ReactionKind.CompanionLowHealth,
                        Speaker = companion,
                        Description = ModLang.Pick("Te queda muy poca salud.", "You are very low on health.", "У тебя осталось очень мало здоровья.")
                    });
                }
            }
        }
    }
}
