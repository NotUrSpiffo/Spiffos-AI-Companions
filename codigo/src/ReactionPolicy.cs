using System;
using System.Collections.Generic;

namespace OfflineCompanionsAI
{
    public enum ReactionKind
    {
        BossDefeated,
        NewBiome,
        PlayerDied,
        CompanionReturned,
        CompanionLowHealth,
        GoalFinished,
        CompanionNeed,
        RaidStarted,
        RaidEnded,
        StrongEnemy
    }

    public sealed class Reaction
    {
        public ReactionKind Kind;
        public string Description;

        /// <summary>Compañero concreto que debe hablar, o null para el más cercano. Sin tipos de Unity: Plugin le da sentido.</summary>
        public object Speaker;

        /// <summary>Si en modo autónomo puede intentar resolverlo por su cuenta. Solo problemas: con todo lo demás se encadenaría sin fin.</summary>
        public bool CanResolve;

        internal float AddedAt;
    }

    /// <summary>
    /// Decide cuándo una reacción puede gastar una petición a la IA.
    /// Sin tipos de Unity, para poder probarla fuera del juego.
    /// </summary>
    public sealed class ReactionPolicy
    {
        private const float PendingLifetime = 60f;
        private const float WindowSeconds = 3600f;

        private readonly List<Reaction> _pending = new List<Reaction>();
        private readonly Queue<float> _recent = new Queue<float>();
        private float _lastSpokeAt = float.NegativeInfinity;

        public void Add(Reaction reaction, float now)
        {
            reaction.AddedAt = now;
            _pending.Add(reaction);
        }

        /// <summary>Una conversación normal también cuenta como "acaba de hablar", para que no se interrumpa con un comentario.</summary>
        public void NotifySpoke(float now)
        {
            _lastSpokeAt = now;
        }

        /// <summary>Devuelve la reacción más importante que pueda decirse ahora, o null.</summary>
        public Reaction TryTake(float now, float cooldownSeconds, int maxPerHour)
        {
            _pending.RemoveAll(r => now - r.AddedAt > PendingLifetime);
            while (_recent.Count > 0 && now - _recent.Peek() >= WindowSeconds) _recent.Dequeue();
            if (_pending.Count == 0 || _recent.Count >= maxPerHour) return null;

            Reaction best = null;
            foreach (Reaction candidate in _pending)
            {
                if (best == null || Priority(candidate.Kind) > Priority(best.Kind)) best = candidate;
            }

            // Derrotar a un jefe, terminar un encargo o que empiece una incursión es poco frecuente y merece comentario aunque acabe de hablar.
            // Un enemigo demasiado fuerte, además, no puede esperar: el aviso ya tiene su propia pausa.
            bool ignoresCooldown = best.Kind == ReactionKind.BossDefeated || best.Kind == ReactionKind.GoalFinished || best.Kind == ReactionKind.RaidStarted || best.Kind == ReactionKind.StrongEnemy;
            if (!ignoresCooldown && now - _lastSpokeAt < cooldownSeconds) return null;

            _pending.Clear();
            _recent.Enqueue(now);
            _lastSpokeAt = now;
            return best;
        }

        public void Reset()
        {
            _pending.Clear();
        }

        private static int Priority(ReactionKind kind)
        {
            switch (kind)
            {
                case ReactionKind.BossDefeated: return 4;
                case ReactionKind.RaidStarted: return 4;
                case ReactionKind.PlayerDied: return 3;
                case ReactionKind.CompanionReturned: return 3;
                case ReactionKind.GoalFinished: return 3;
                case ReactionKind.StrongEnemy: return 3;
                case ReactionKind.CompanionLowHealth: return 2;
                case ReactionKind.CompanionNeed: return 2;
                default: return 1;
            }
        }

        /// <summary>El mensaje que recibe la IA. Deja claro que es un suceso y no algo que haya dicho el jugador.</summary>
        public static string EventMessage(Reaction reaction)
        {
            return ModLang.Pick(
                "(Suceso de la partida; el jugador no ha dicho nada: " + reaction.Description
                + " Coméntalo en voz alta con una sola frase corta y natural, sin dar órdenes ni ofrecer ayuda. No hagas como si se hubiera resuelto.)",
                "(In-game event; the player said nothing: " + reaction.Description
                + " Comment on it aloud in a single short, natural sentence, without giving orders or offering help. Do not act as if it has been resolved.)");
        }

        /// <summary>Lo que se guarda en la memoria, sin las instrucciones para la IA.</summary>
        public static string MemoryLine(Reaction reaction)
        {
            return ModLang.Pick(
                "(Suceso de la partida: " + reaction.Description + ")",
                "(In-game event: " + reaction.Description + ")");
        }

        public static string BossName(string globalKey)
        {
            switch ((globalKey ?? "").ToLowerInvariant())
            {
                case "defeated_eikthyr": return "Eikthyr";
                case "defeated_gdking": return ModLang.Pick("el Anciano", "The Elder");
                case "defeated_bonemass": return "Bonemass";
                case "defeated_dragon": return "Moder";
                case "defeated_goblinking": return "Yagluth";
                case "defeated_queen": return ModLang.Pick("la Reina", "The Queen");
                case "defeated_fader": return "Fader";
                default: return null;
            }
        }

        public static string BiomeName(string biome)
        {
            switch (biome)
            {
                case "Meadows": return ModLang.Pick("las Praderas", "the Meadows");
                case "BlackForest": return ModLang.Pick("el Bosque Negro", "the Black Forest");
                case "Swamp": return ModLang.Pick("el Pantano", "the Swamp");
                case "Mountain": return ModLang.Pick("las Montañas", "the Mountains");
                case "Plains": return ModLang.Pick("las Llanuras", "the Plains");
                case "Mistlands": return ModLang.Pick("las Tierras Brumosas", "the Mistlands");
                case "AshLands": return ModLang.Pick("las Tierras de Ceniza", "the Ashlands");
                case "DeepNorth": return ModLang.Pick("el Norte Profundo", "the Deep North");
                case "Ocean": return ModLang.Pick("el océano", "the Ocean");
                default: return null;
            }
        }
    }
}
