using System.Collections.Generic;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// En multijugador, el juego que mueve a un compañero puede cambiar: el de su dueño o el de otro jugador que esté
    /// cerca. Si cambia mientras está sentado, el nuevo no sabe que lo estaba y nunca lo levanta, y los demás lo ven
    /// sentado mientras anda. Lo mismo pasa si a alguien no le llega el aviso de levantarse.
    /// Un compañero que anda nunca debe ir sentado: se le levanta. El juego que lo mueve se lo dice a todos; los demás,
    /// que no pueden, lo arreglan al menos en su pantalla.
    /// </summary>
    internal static class SeatSync
    {
        private const float CheckInterval = 0.5f;
        private const float MinSpeed = 0.8f;
        private const float MaxSpeed = 15f;
        private const int ChecksToFix = 3;

        private static readonly string[] SeatBools = { "emote_sit", "attach_chair", "attach_bed" };

        private sealed class Track
        {
            public Vector3 Position;
            public int Moving;
        }

        private static readonly Dictionary<Component, Track> Tracks = new Dictionary<Component, Track>();
        private static readonly Dictionary<Animator, HashSet<string>> Parameters = new Dictionary<Animator, HashSet<string>>();
        private static float _nextCheck;

        public static void Reset()
        {
            Tracks.Clear();
            Parameters.Clear();
        }

        public static void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            var seen = new HashSet<Component>();
            foreach (Component companion in CompanionBridge.AllCompanions())
            {
                seen.Add(companion);
                Check(companion);
            }
            var gone = new List<Component>();
            foreach (Component c in Tracks.Keys)
            {
                if (c == null || !seen.Contains(c)) gone.Add(c);
            }
            foreach (Component c in gone) Tracks.Remove(c);
            if (Parameters.Count > 64) Parameters.Clear();
        }

        private static void Check(Component companion)
        {
            Vector3 position = companion.transform.position;
            if (!Tracks.TryGetValue(companion, out Track track))
            {
                Tracks[companion] = new Track { Position = position };
                return;
            }
            Vector3 step = position - track.Position;
            step.y = 0f;
            track.Position = position;
            float speed = step.magnitude / CheckInterval;
            if (speed < MinSpeed || speed > MaxSpeed)
            {
                track.Moving = 0;
                return;
            }

            Animator animator = companion.GetComponentInChildren<Animator>();
            if (animator == null || !LooksSeated(animator))
            {
                track.Moving = 0;
                return;
            }
            if (++track.Moving < ChecksToFix) return;
            track.Moving = 0;
            // En un barco se mueve sentado con él, y eso sí está bien.
            if (OnShip(position)) return;

            ZNetView view = companion.GetComponent<ZNetView>();
            ZSyncAnimation sync = companion.GetComponent<ZSyncAnimation>();
            bool owner = view != null && view.IsValid() && view.IsOwner() && sync != null;
            HashSet<string> parameters = ParametersOf(animator);
            if (owner)
            {
                foreach (string name in SeatBools)
                {
                    if (parameters.Contains(name)) sync.SetBool(name, false);
                }
                sync.SetTrigger("emote_stop");
            }
            else
            {
                foreach (string name in SeatBools)
                {
                    if (parameters.Contains(name)) animator.SetBool(name, false);
                }
                if (parameters.Contains("emote_stop")) animator.SetTrigger("emote_stop");
            }
            Plugin.Log.LogInfo("[Sentado] " + CompanionBridge.Name(companion) + " andaba sentado (" + speed.ToString("0.0") + " m/s): se levanta "
                + (owner ? "para todos." : "en esta pantalla; lo mueve el juego de otro jugador."));
        }

        private static bool LooksSeated(Animator animator)
        {
            AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (state.IsTag("emote")) return true;
            HashSet<string> parameters = ParametersOf(animator);
            foreach (string name in SeatBools)
            {
                if (parameters.Contains(name) && animator.GetBool(name)) return true;
            }
            return false;
        }

        private static HashSet<string> ParametersOf(Animator animator)
        {
            if (Parameters.TryGetValue(animator, out HashSet<string> names)) return names;
            names = new HashSet<string>();
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool || parameter.name == "emote_stop") names.Add(parameter.name);
            }
            Parameters[animator] = names;
            return names;
        }

        private static bool OnShip(Vector3 position)
        {
            foreach (Collider collider in Physics.OverlapSphere(position, 2f))
            {
                if (collider != null && collider.GetComponentInParent<Ship>() != null) return true;
            }
            return false;
        }
    }
}
