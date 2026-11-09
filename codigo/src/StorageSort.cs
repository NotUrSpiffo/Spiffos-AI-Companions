using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// A qué tipo pertenece un objeto al guardarlo en un cofre: comida, mineral, madera, piedra, equipo, trofeo u
    /// otro material. Los tipos salen de los datos del juego (hornos, estaciones de cocina, recetas), así que los
    /// objetos de otros mods caen en su tipo sin listas a mano.
    /// </summary>
    internal static class StorageSort
    {
        internal const string Food = "comida";
        internal const string Ore = "mineral";
        internal const string Wood = "madera";
        internal const string Stone = "piedra";
        internal const string Gear = "equipo";
        internal const string Trophy = "trofeo";
        internal const string Other = "otro";

        private const int TierSame = 2;
        private const int TierKind = 1;

        private static HashSet<string> _foodParts;
        private static HashSet<string> _oreParts;

        internal static string Category(ItemDrop.ItemData item)
        {
            if (item?.m_shared == null) return Other;
            string name = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            switch (item.m_shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Consumable:
                    return Food;
                case ItemDrop.ItemData.ItemType.Trophy:
                    return Trophy;
                case ItemDrop.ItemData.ItemType.Material:
                case ItemDrop.ItemData.ItemType.Misc:
                case ItemDrop.ItemData.ItemType.None:
                    break;
                default:
                    return Gear;
            }
            if (!Ready()) return Other;
            if (_oreParts.Contains(name)) return Ore;
            if (_foodParts.Contains(name)) return Food;
            if (IsWood(name)) return Wood;
            if (IsStone(name)) return Stone;
            return Other;
        }

        private static bool IsWood(string name) =>
            name.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Log", StringComparison.Ordinal) >= 0
            || name.IndexOf("Bark", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsStone(string name) =>
            name.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Flint", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Marble", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Obsidian", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("Grausten", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Junta una vez lo que entra y sale de los hornos con combustible y lo que se cocina.</summary>
        private static bool Ready()
        {
            if (_oreParts != null) return true;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return false;
            var ore = new HashSet<string>();
            var food = new HashSet<string>();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                Smelter smelter = prefab.GetComponent<Smelter>();
                // Solo los hornos que queman algo: el de carbón quema madera, y la madera no es mineral.
                if (smelter?.m_conversion != null && smelter.m_fuelItem != null && smelter.m_maxFuel > 0)
                {
                    ore.Add(smelter.m_fuelItem.gameObject.name);
                    foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                    {
                        if (conversion?.m_from != null) ore.Add(conversion.m_from.gameObject.name);
                        if (conversion?.m_to != null) ore.Add(conversion.m_to.gameObject.name);
                    }
                }
                CookingStation station = prefab.GetComponent<CookingStation>();
                if (station?.m_conversion != null)
                {
                    foreach (CookingStation.ItemConversion conversion in station.m_conversion)
                    {
                        if (conversion?.m_from != null) food.Add(conversion.m_from.gameObject.name);
                        if (conversion?.m_to != null) food.Add(conversion.m_to.gameObject.name);
                    }
                }
                Fermenter fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter?.m_conversion != null)
                {
                    foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                    {
                        if (conversion?.m_from != null) food.Add(conversion.m_from.gameObject.name);
                    }
                }
            }
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (recipe?.m_item?.m_itemData?.m_shared == null || recipe.m_resources == null) continue;
                if (recipe.m_item.m_itemData.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable) continue;
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (requirement?.m_resItem != null) food.Add(requirement.m_resItem.gameObject.name);
                }
            }
            if (ore.Count == 0 && food.Count == 0) return false;
            food.ExceptWith(ore);
            _oreParts = ore;
            _foodParts = food;
            return true;
        }

        /// <summary>
        /// El cofre que mejor encaja con lo que se va a guardar: primero uno que ya tenga lo mismo, luego uno con más
        /// cosas del mismo tipo, y entre iguales el más cercano. Null si ninguno tiene nada parecido.
        /// </summary>
        internal static Container Pick(IEnumerable<Container> chests, IList<ItemDrop.ItemData> carried, Vector3 from, Func<Container, bool> fits)
        {
            if (chests == null || carried == null || carried.Count == 0) return null;
            var wanted = new List<KeyValuePair<string, string>>();
            foreach (ItemDrop.ItemData item in carried)
            {
                if (item?.m_dropPrefab == null || item.m_shared == null) continue;
                wanted.Add(new KeyValuePair<string, string>(item.m_dropPrefab.name, Category(item)));
            }
            if (wanted.Count == 0) return null;

            Container best = null;
            int bestTier = 0, bestAmount = 0;
            float bestDistance = float.MaxValue;
            foreach (Container chest in chests)
            {
                Inventory inventory = chest != null ? chest.GetInventory() : null;
                if (inventory == null || !fits(chest)) continue;
                int tier = Score(inventory, wanted, out int amount);
                if (tier == 0) continue;
                float distance = Vector3.Distance(from, chest.transform.position);
                bool better = tier > bestTier
                    || tier == bestTier && amount > bestAmount
                    || tier == bestTier && amount == bestAmount && distance < bestDistance;
                if (!better) continue;
                best = chest;
                bestTier = tier;
                bestAmount = amount;
                bestDistance = distance;
            }
            return best;
        }

        private static int Score(Inventory chest, List<KeyValuePair<string, string>> wanted, out int amount)
        {
            var byName = new Dictionary<string, int>();
            var byKind = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData stored in chest.GetAllItems())
            {
                if (stored?.m_dropPrefab == null) continue;
                string name = stored.m_dropPrefab.name;
                byName.TryGetValue(name, out int count);
                byName[name] = count + stored.m_stack;
                string kind = Category(stored);
                byKind.TryGetValue(kind, out count);
                byKind[kind] = count + stored.m_stack;
            }
            int tier = 0;
            amount = 0;
            var counted = new HashSet<string>();
            foreach (KeyValuePair<string, string> item in wanted)
            {
                int itemTier = 0, itemAmount = 0;
                if (byName.TryGetValue(item.Key, out int same))
                {
                    itemTier = TierSame;
                    itemAmount = same;
                }
                // "Otro material" no es un tipo: dos cosas sin tipo no tienen por qué ir juntas.
                else if (item.Value != Other && byKind.TryGetValue(item.Value, out int kind))
                {
                    itemTier = TierKind;
                    itemAmount = kind;
                }
                if (itemTier > tier)
                {
                    tier = itemTier;
                    amount = 0;
                    counted.Clear();
                }
                if (itemTier == tier && itemTier > 0 && counted.Add(itemTier == TierSame ? item.Key : item.Value))
                {
                    amount += itemAmount;
                }
            }
            return tier;
        }
    }
}
