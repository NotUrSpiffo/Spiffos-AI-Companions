using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>Resume lo que el compañero sabe en este momento. Solo incluye datos reales del juego.</summary>
    public static class GameContext
    {
        private const float EnemyRadius = 30f;
        private const int MaxItems = 20;

        public static string Describe(Player player, Component companion, LookTarget look)
        {
            Character self = companion.GetComponent<Character>();
            var sb = new StringBuilder();

            sb.AppendLine(ModLang.Pick("Tu nombre: ", "Your name: ") + CompanionBridge.Name(companion));
            sb.AppendLine(ModLang.Pick(
                "Jugador: " + player.GetPlayerName() + ", a " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position)) + " m de ti",
                "Player: " + player.GetPlayerName() + ", " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position)) + " m from you"));
            float playerHealth = player.GetHealthPercentage();
            sb.AppendLine(ModLang.Pick(
                "Salud del jugador: " + Mathf.RoundToInt(player.GetHealth()) + "/" + Mathf.RoundToInt(player.GetMaxHealth()) + (playerHealth < Initiative.BadlyHurtShare ? " (malherido)" : "") + ", aguante " + Mathf.RoundToInt(player.GetStaminaPercentage() * 100f) + " % (coméntalo solo si viene a cuento)",
                "Player health: " + Mathf.RoundToInt(player.GetHealth()) + "/" + Mathf.RoundToInt(player.GetMaxHealth()) + (playerHealth < Initiative.BadlyHurtShare ? " (badly hurt)" : "") + ", stamina " + Mathf.RoundToInt(player.GetStaminaPercentage() * 100f) + " % (mention only if relevant)"));
            if (player.InPlaceMode()) sb.AppendLine(ModLang.Pick("El jugador está construyendo.", "The player is building."));
            sb.AppendLine(ModLang.Pick("Lo que mira el jugador: ", "What the player is looking at: ") + look.Describe());
            sb.AppendLine(ModLang.Pick(
                "Lo que lleva equipado el jugador: " + Equipped(player.GetInventory()) + " (coméntalo solo si viene a cuento)",
                "What the player has equipped: " + Equipped(player.GetInventory()) + " (mention only if relevant)"));
            if (self != null)
            {
                sb.AppendLine(ModLang.Pick("Tu salud: ", "Your health: ") + Mathf.RoundToInt(self.GetHealth()) + "/" + Mathf.RoundToInt(self.GetMaxHealth()));
                sb.AppendLine(ModLang.Pick("Tu aguante: ", "Your stamina: ") + Mathf.RoundToInt(CompanionBridge.StaminaShare(companion) * 100f) + " %");
            }

            string status = CompanionBridge.Status(companion);
            if (!string.IsNullOrEmpty(status)) sb.AppendLine(ModLang.Pick("Lo que haces ahora: ", "What you are doing right now: ") + status);
            if (CompanionBridge.CombatStance(companion) == CompanionActions.StancePassive)
            {
                sb.AppendLine(ModLang.Pick(
                    "Estás en postura pasiva: no atacas a los enemigos. Si el jugador quiere que pelees, cámbiala con set_stance.",
                    "You are in passive stance: you do not attack enemies. If the player wants you to fight, change it with set_stance."));
            }

            sb.AppendLine(ModLang.Pick("Bioma: ", "Biome: ") + player.GetCurrentBiome());
            if (EnvMan.instance != null)
            {
                sb.AppendLine(ModLang.Pick(
                    "Momento: día " + EnvMan.instance.GetDay() + ", " + TimeOfDay.Name(EnvMan.instance.GetDayFraction()) + (EnvMan.IsWet() ? ", lloviendo" : "") + (EnvMan.IsCold() ? ", hace frío" : ""),
                    "Time: day " + EnvMan.instance.GetDay() + ", " + TimeOfDay.Name(EnvMan.instance.GetDayFraction()) + (EnvMan.IsWet() ? ", raining" : "") + (EnvMan.IsCold() ? ", cold" : "")));
            }

            string raid = CompanionBridge.ActiveRaid();
            if (raid != null) sb.AppendLine(ModLang.Pick("Incursión en marcha: ", "Active raid: ") + raid);

            string enemies = NearbyEnemies(companion);
            sb.AppendLine(ModLang.Pick(
                "Enemigos cerca: " + (enemies.Length == 0 ? "ninguno" : enemies),
                "Nearby enemies: " + (enemies.Length == 0 ? "none" : enemies)));

            sb.AppendLine(ModLang.Pick(
                "Tus herramientas (" + CompanionBridge.ToolSummary(companion) + "). Sin hacha no puedes talar árboles y sin pico no puedes picar rocas; solo recoger lo suelto y romper plantas pequeñas. Tenlo en cuenta, pero menciónalo solo si te preguntan.",
                "Your tools (" + CompanionBridge.ToolSummary(companion) + "). Without an axe you cannot chop trees and without a pickaxe you cannot mine rocks; only pick up loose items and break small plants. Keep this in mind, but mention it only if asked."));

            string ranged = CompanionBridge.RangedSummary(companion);
            if (ranged != null)
            {
                sb.AppendLine(ModLang.Pick(
                    "Tu arma a distancia: " + ranged + ". Si te faltan flechas, búscalas con take_from_chest; sin ellas peleas cuerpo a cuerpo.",
                    "Your ranged weapon: " + ranged + ". If you need arrows or bolts, get them with take_from_chest; without them you fight in melee."));
            }

            Humanoid humanoid = companion.GetComponent<Humanoid>();
            if (humanoid != null && humanoid.GetInventory() != null)
            {
                sb.AppendLine(ModLang.Pick("Tu inventario: ", "Your inventory: ") + Inventory(humanoid.GetInventory()));
            }
            return sb.ToString().TrimEnd();
        }

        private static string NearbyEnemies(Component companion)
        {
            Character self = companion.GetComponent<Character>();
            var counts = new Dictionary<string, int>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c == self || c.IsPlayer() || c.GetHealth() <= 0f || self == null || !CompanionBridge.IsThreat(self, c)) continue;
                if (Vector3.Distance(companion.transform.position, c.transform.position) > EnemyRadius) continue;
                string name = Localized(c.m_name);
                counts.TryGetValue(name, out int n);
                counts[name] = n + 1;
            }
            string tooStrongNote = ModLang.Pick(
                " (demasiado fuertes para vuestro equipo: no vas a por ellos salvo que ataquen de cerca o el jugador lo ordene)",
                " (too strong for your gear: you do not go after them unless they attack up close or the player orders it)");
            return string.Join(", ", counts.Select(kv => kv.Value + " " + kv.Key + (CompanionBridge.IsTooStrong(companion, kv.Key) ? tooStrongNote : "")).ToArray());
        }

        private static string Equipped(Inventory inventory)
        {
            if (inventory == null) return ModLang.Pick("no se sabe", "unknown");
            var worn = new List<string>();
            string levelLabel = ModLang.Pick(" (nivel ", " (level ");
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_equipped) worn.Add(Localized(item.m_shared.m_name) + (item.m_quality > 1 ? levelLabel + item.m_quality + ")" : ""));
            }
            return worn.Count == 0 ? ModLang.Pick("nada", "nothing") : string.Join(", ", worn.ToArray());
        }

        private static string Inventory(Inventory inventory)
        {
            var totals = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                string name = Localized(item.m_shared.m_name);
                totals.TryGetValue(name, out int n);
                totals[name] = n + item.m_stack;
            }
            if (totals.Count == 0) return ModLang.Pick("vacío", "empty");
            IEnumerable<string> parts = totals.OrderBy(kv => kv.Key).Take(MaxItems).Select(kv => kv.Value + " " + kv.Key);
            string text = string.Join(", ", parts.ToArray());
            return totals.Count > MaxItems ? text + ModLang.Pick(" y más cosas", " and more items") : text;
        }

        private static string Localized(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
