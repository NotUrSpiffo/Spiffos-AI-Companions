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
        private const float OtherPlayerRadius = 60f;
        private const int MaxOtherPlayers = 5;

        public static string Describe(Player player, Component companion, LookTarget look)
        {
            Character self = companion.GetComponent<Character>();
            var sb = new StringBuilder();

            sb.AppendLine(ModLang.Pick("Tu nombre: ", "Your name: ", "Твоё имя: ") + CompanionBridge.Name(companion));
            sb.AppendLine(ModLang.Pick(
                "Jugador: " + player.GetPlayerName() + ", a " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position)) + " m de ti",
                "Player: " + player.GetPlayerName() + ", " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position)) + " m from you",
                "Игрок: " + player.GetPlayerName() + ", в " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, companion.transform.position)) + " м от тебя"));
            float playerHealth = player.GetHealthPercentage();
            sb.AppendLine(ModLang.Pick(
                "Salud del jugador: " + Mathf.RoundToInt(player.GetHealth()) + "/" + Mathf.RoundToInt(player.GetMaxHealth()) + (playerHealth < Initiative.BadlyHurtShare ? " (malherido)" : "") + ", aguante " + Mathf.RoundToInt(player.GetStaminaPercentage() * 100f) + " % (coméntalo solo si viene a cuento)",
                "Player health: " + Mathf.RoundToInt(player.GetHealth()) + "/" + Mathf.RoundToInt(player.GetMaxHealth()) + (playerHealth < Initiative.BadlyHurtShare ? " (badly hurt)" : "") + ", stamina " + Mathf.RoundToInt(player.GetStaminaPercentage() * 100f) + " % (mention only if relevant)",
                "Здоровье игрока: " + Mathf.RoundToInt(player.GetHealth()) + "/" + Mathf.RoundToInt(player.GetMaxHealth()) + (playerHealth < Initiative.BadlyHurtShare ? " (тяжело ранен)" : "") + ", выносливость " + Mathf.RoundToInt(player.GetStaminaPercentage() * 100f) + " % (упоминай только если это к месту)"));
            if (player.InPlaceMode()) sb.AppendLine(ModLang.Pick("El jugador está construyendo.", "The player is building.", "Игрок строит."));
            sb.AppendLine(ModLang.Pick("Lo que mira el jugador: ", "What the player is looking at: ", "На что смотрит игрок: ") + look.Describe());
            sb.AppendLine(ModLang.Pick(
                "Lo que lleva equipado el jugador: " + Equipped(player.GetInventory()) + " (coméntalo solo si viene a cuento)",
                "What the player has equipped: " + Equipped(player.GetInventory()) + " (mention only if relevant)",
                "Снаряжение игрока: " + Equipped(player.GetInventory()) + " (упоминай только если это к месту)"));
            string others = OtherPlayers(player, companion);
            if (others.Length > 0) sb.AppendLine(ModLang.Pick(
                "Otros jugadores cerca: " + others + ". Los reconoces y puedes hablar de ellos, pero no son tu jugador: solo obedeces a " + player.GetPlayerName() + ".",
                "Other players nearby: " + others + ". You recognize them and can talk about them, but they are not your player: you only take orders from " + player.GetPlayerName() + ".",
                "Другие игроки поблизости: " + others + ". Ты узнаёшь их и можешь говорить о них, но они не твой командир: ты подчиняешься только " + player.GetPlayerName() + "."));
            if (self != null)
            {
                sb.AppendLine(ModLang.Pick("Tu salud: ", "Your health: ", "Твоё здоровье: ") + Mathf.RoundToInt(self.GetHealth()) + "/" + Mathf.RoundToInt(self.GetMaxHealth()));
                sb.AppendLine(ModLang.Pick("Tu aguante: ", "Your stamina: ", "Твоя выносливость: ") + Mathf.RoundToInt(CompanionBridge.StaminaShare(companion) * 100f) + " %");
            }

            string status = CompanionBridge.Status(companion);
            if (!string.IsNullOrEmpty(status)) sb.AppendLine(ModLang.Pick("Lo que haces ahora: ", "What you are doing right now: ", "Что ты делаешь сейчас: ") + status);
            if (CompanionBridge.CombatStance(companion) == CompanionActions.StancePassive)
            {
                sb.AppendLine(ModLang.Pick(
                    "Estás en postura pasiva: no atacas a los enemigos. Si el jugador quiere que pelees, cámbiala con set_stance.",
                    "You are in passive stance: you do not attack enemies. If the player wants you to fight, change it with set_stance.",
                    "Ты в пассивной стойке: ты не атакуешь врагов. Если игрок хочет, чтобы ты сражался, смени её с помощью set_stance."));
            }

            sb.AppendLine(ModLang.Pick("Bioma: ", "Biome: ", "Биом: ") + player.GetCurrentBiome());
            if (EnvMan.instance != null)
            {
                sb.AppendLine(ModLang.Pick(
                    "Momento: día " + EnvMan.instance.GetDay() + ", " + TimeOfDay.Name(EnvMan.instance.GetDayFraction()) + (EnvMan.IsWet() ? ", lloviendo" : "") + (EnvMan.IsCold() ? ", hace frío" : ""),
                    "Time: day " + EnvMan.instance.GetDay() + ", " + TimeOfDay.Name(EnvMan.instance.GetDayFraction()) + (EnvMan.IsWet() ? ", raining" : "") + (EnvMan.IsCold() ? ", cold" : ""),
                    "Время: день " + EnvMan.instance.GetDay() + ", " + TimeOfDay.Name(EnvMan.instance.GetDayFraction()) + (EnvMan.IsWet() ? ", идёт дождь" : "") + (EnvMan.IsCold() ? ", холодно" : "")));
            }

            string raid = CompanionBridge.ActiveRaid();
            if (raid != null) sb.AppendLine(ModLang.Pick("Incursión en marcha: ", "Active raid: ", "Идёт рейд: ") + raid);

            string enemies = NearbyEnemies(companion);
            sb.AppendLine(ModLang.Pick(
                "Enemigos cerca: " + (enemies.Length == 0 ? "ninguno" : enemies),
                "Nearby enemies: " + (enemies.Length == 0 ? "none" : enemies),
                "Враги поблизости: " + (enemies.Length == 0 ? "никого" : enemies)));

            sb.AppendLine(ModLang.Pick(
                "Tus herramientas (" + CompanionBridge.ToolSummary(companion) + "). Sin hacha no puedes talar árboles y sin pico no puedes picar rocas; solo recoger lo suelto y romper plantas pequeñas. Tenlo en cuenta, pero menciónalo solo si te preguntan.",
                "Your tools (" + CompanionBridge.ToolSummary(companion) + "). Without an axe you cannot chop trees and without a pickaxe you cannot mine rocks; only pick up loose items and break small plants. Keep this in mind, but mention it only if asked.",
                "Твои инструменты (" + CompanionBridge.ToolSummary(companion) + "). Без топора ты не можешь рубить деревья, а без кирки — добывать камень; только подбирать предметы с земли и срывать небольшие растения. Помни об этом, но упоминай только если спросят."));

            string ranged = CompanionBridge.RangedSummary(companion);
            if (ranged != null)
            {
                sb.AppendLine(ModLang.Pick(
                    "Tu arma a distancia: " + ranged + ". Si te faltan flechas, búscalas con take_from_chest; sin ellas peleas cuerpo a cuerpo.",
                    "Your ranged weapon: " + ranged + ". If you need arrows or bolts, get them with take_from_chest; without them you fight in melee.",
                    "Твоё оружие дальнего боя: " + ranged + ". Если нужны стрелы или болты, возьми их через take_from_chest; без них ты сражаешься в ближнем бою."));
            }

            Humanoid humanoid = companion.GetComponent<Humanoid>();
            if (humanoid != null && humanoid.GetInventory() != null)
            {
                sb.AppendLine(ModLang.Pick("Tu inventario: ", "Your inventory: ", "Твой инвентарь: ") + Inventory(humanoid.GetInventory()));
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Los demás jugadores cercanos, del más cercano al más lejano: nombre, distancia, salud y lo que llevan.</summary>
        private static string OtherPlayers(Player player, Component companion)
        {
            var near = new List<KeyValuePair<float, Player>>();
            foreach (Player other in Player.GetAllPlayers())
            {
                if (other == null || other == player || other.IsDead()) continue;
                float distance = Vector3.Distance(companion.transform.position, other.transform.position);
                if (distance <= OtherPlayerRadius) near.Add(new KeyValuePair<float, Player>(distance, other));
            }
            IEnumerable<string> parts = near.OrderBy(kv => kv.Key).Take(MaxOtherPlayers).Select(kv => Describe(kv.Value, kv.Key));
            return string.Join("; ", parts.ToArray());
        }

        private static string Describe(Player other, float distance)
        {
            string text = other.GetPlayerName() + ModLang.Pick(", a " + Mathf.RoundToInt(distance) + " m de ti", ", " + Mathf.RoundToInt(distance) + " m from you", ", в " + Mathf.RoundToInt(distance) + " м от тебя");
            if (other.GetMaxHealth() > 0f && other.GetHealthPercentage() < Initiative.BadlyHurtShare) text += ModLang.Pick(", malherido", ", badly hurt", ", тяжело ранен");
            // De otro jugador solo se sabe lo que se le ve en las manos: su inventario está en su juego.
            var hands = new List<string>();
            ZNetView view = other.GetComponent<ZNetView>();
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            if (zdo != null)
            {
                string right = ItemName(zdo.GetInt(ZDOVars.s_rightItem));
                string left = ItemName(zdo.GetInt(ZDOVars.s_leftItem));
                if (right != null) hands.Add(right);
                if (left != null && left != right) hands.Add(left);
            }
            if (hands.Count > 0) text += ModLang.Pick(", lleva en las manos " + string.Join(" y ", hands.ToArray()), ", holding " + string.Join(" and ", hands.ToArray()), ", в руках держит " + string.Join(" и ", hands.ToArray()));
            return text;
        }

        private static string ItemName(int hash)
        {
            if (hash == 0 || ObjectDB.instance == null) return null;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(hash);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop != null ? Localized(drop.m_itemData.m_shared.m_name) : null;
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
                " (demasiado fuertes para ustedes: no vas a por ellos salvo que ataquen de cerca o el jugador lo ordene)",
                " (too strong for your gear: you do not go after them unless they attack up close or the player orders it)",
                " (слишком сильны для вашего снаряжения: ты не нападаешь на них, если только они не атакуют в упор или игрок не прикажет)");
            return string.Join(", ", counts.Select(kv => kv.Value + " " + kv.Key + (CompanionBridge.IsTooStrong(companion, kv.Key) ? tooStrongNote : "")).ToArray());
        }

        private static string Equipped(Inventory inventory)
        {
            if (inventory == null) return ModLang.Pick("no se sabe", "unknown", "неизвестно");
            var worn = new List<string>();
            string levelLabel = ModLang.Pick(" (nivel ", " (level ", " (уровень ");
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_equipped) worn.Add(Localized(item.m_shared.m_name) + (item.m_quality > 1 ? levelLabel + item.m_quality + ")" : ""));
            }
            return worn.Count == 0 ? ModLang.Pick("nada", "nothing", "ничего") : string.Join(", ", worn.ToArray());
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
            if (totals.Count == 0) return ModLang.Pick("vacío", "empty", "пусто");
            IEnumerable<string> parts = totals.OrderBy(kv => kv.Key).Take(MaxItems).Select(kv => kv.Value + " " + kv.Key);
            string text = string.Join(", ", parts.ToArray());
            return totals.Count > MaxItems ? text + ModLang.Pick(" y más cosas", " and more items", " и другие предметы") : text;
        }

        private static string Localized(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }
    }
}
