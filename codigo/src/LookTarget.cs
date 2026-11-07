using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Lo que mira el jugador al enviar el mensaje. Se guarda entonces porque la respuesta de la IA
    /// tarda unos segundos y mientras tanto la cámara puede moverse.
    /// </summary>
    public sealed class LookTarget
    {
        private const float MaxDistance = 50f;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[64];

        // Las mismas capas que usa la tecla de mando de Offline Companions Reborn.
        private static readonly string[] Layers = { "character", "character_net", "character_ghost", "character_noenv", "Default", "Default_small", "piece", "piece_nonsolid", "static_solid", "terrain", "vehicle", "item" };

        /// <summary>El objeto al que se apunta, descartando jugadores, compañeros y personajes no hostiles.</summary>
        public Collider Collider;

        /// <summary>Los objetos que siguen detrás en la misma línea de visión.</summary>
        public readonly List<Collider> Behind = new List<Collider>();

        public Vector3 Point;
        public Character Enemy;

        public static LookTarget Capture(Player player, Type companionType)
        {
            Camera camera = Utils.GetMainCamera();
            if (camera == null) return new LookTarget();

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, HitBuffer, MaxDistance, LayerMask.GetMask(Layers));
            Array.Sort(HitBuffer, 0, count, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            var look = new LookTarget();
            for (int i = 0; i < count; i++)
            {
                Collider collider = HitBuffer[i].collider;
                if (collider == null) continue;
                if (look.Collider != null)
                {
                    look.Behind.Add(collider);
                    continue;
                }
                if (collider.GetComponentInParent<Player>() != null || collider.GetComponentInParent(companionType) != null) continue;

                Character character = collider.GetComponentInParent<Character>();
                if (character != null)
                {
                    if (character.GetHealth() <= 0f || !BaseAI.IsEnemy(player, character)) continue;
                    look.Enemy = character;
                }
                look.Collider = collider;
                look.Point = HitBuffer[i].point;
            }
            return look;
        }

        /// <summary>Una descripción breve para que la IA sepa a qué se refiere el jugador.</summary>
        public string Describe()
        {
            if (Collider == null) return ModLang.Pick("nada en particular", "nothing in particular");
            if (Enemy != null) return Localized(Enemy.m_name) + ModLang.Pick(" (enemigo)", " (enemy)");

            string kind = KindOf(Collider);
            string name = Collider.GetComponentInParent<Hoverable>()?.GetHoverName();
            if (!string.IsNullOrEmpty(name)) return kind == null ? Localized(name) : Localized(name) + " (" + kind + ")";
            return kind ?? ModLang.Pick("el suelo o una construcción", "the ground or a structure");
        }

        private static string KindOf(Collider c)
        {
            if (c.GetComponentInParent<Vagon>() != null) return ModLang.Pick("carro", "cart");
            if (c.GetComponentInParent<Door>() != null) return ModLang.Pick("puerta", "door");
            if (c.GetComponentInParent<Fireplace>() != null) return ModLang.Pick("fuego", "fire");
            if (c.GetComponentInParent<Bed>() != null) return ModLang.Pick("cama", "bed");
            if (c.GetComponentInParent<CraftingStation>() != null) return ModLang.Pick("estación de trabajo", "crafting station");
            if (c.GetComponentInParent<Smelter>() != null) return ModLang.Pick("horno", "smelter");
            if (c.GetComponentInParent<Beehive>() != null) return ModLang.Pick("colmena", "beehive");
            if (c.GetComponentInParent<Ship>() != null) return ModLang.Pick("barco", "ship");
            if (c.GetComponentInParent<TombStone>() != null) return ModLang.Pick("tumba", "tombstone");
            if (c.GetComponentInParent<Container>() != null) return ModLang.Pick("cofre", "chest");
            if (c.GetComponentInParent<TreeBase>() != null || c.GetComponentInParent<TreeLog>() != null) return ModLang.Pick("árbol", "tree");
            if (c.GetComponentInParent<MineRock5>() != null || c.GetComponentInParent<MineRock>() != null) return ModLang.Pick("roca", "rock");
            if (c.GetComponentInParent<Destructible>() != null && c.GetComponentInParent<Piece>() == null) return ModLang.Pick("planta o resto pequeño", "plant or small debris");
            return null;
        }

        private static string Localized(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
