using System.Collections.Generic;
using System.Reflection;
using Companions;
using HarmonyLib;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Offline Companions guarda lo que sobra (la miel, por ejemplo), lo recolectado y la cosecha en el cofre más
    /// cercano con sitio cuando ningún cofre tiene ese mismo objeto. Aquí se prefiere antes el cofre que ya guarda más
    /// cosas del mismo tipo (comida, mineral, madera, piedra, equipo, trofeos). Si ninguno tiene nada parecido, sigue
    /// el cofre de Offline Companions.
    /// </summary>
    internal static class StorageSortPatches
    {
        private static MethodInfo _upkeepChests;
        private static MethodInfo _upkeepRoomIn;
        private static MethodInfo _upkeepCountKey;
        private static MethodInfo _upkeepKey;
        private static FieldInfo _farmChests;
        private static MethodInfo _farmSkipChest;

        internal static void Apply(Harmony harmony)
        {
            System.Type upkeep = typeof(UpkeepController);
            MethodInfo stow = AccessTools.Method(upkeep, "StowChest");
            _upkeepChests = AccessTools.Method(upkeep, "Chests");
            _upkeepRoomIn = AccessTools.Method(upkeep, "RoomIn");
            _upkeepCountKey = AccessTools.Method(upkeep, "CountKey");
            _upkeepKey = AccessTools.Method(upkeep, "Key");
            if (stow != null && _upkeepChests != null && _upkeepRoomIn != null && _upkeepCountKey != null && _upkeepKey != null)
                harmony.Patch(stow, postfix: new HarmonyMethod(typeof(StorageSortPatches), nameof(StowByKind)));
            else
                Plugin.Log.LogWarning("Guardar lo que sobra junto a cosas del mismo tipo no está disponible en esta versión de Offline Companions Reborn.");

            MethodInfo dropOff = AccessTools.Method(typeof(MHR_GatherRules), "DropOff");
            if (dropOff != null)
                harmony.Patch(dropOff, postfix: new HarmonyMethod(typeof(StorageSortPatches), nameof(GatherByKind)));
            else
                Plugin.Log.LogWarning("Guardar lo recolectado junto a cosas del mismo tipo no está disponible en esta versión de Offline Companions Reborn.");

            MethodInfo farmChest = AccessTools.Method(typeof(FarmController), "FindChestWithSpace");
            _farmChests = AccessTools.Field(typeof(FarmController), "_nearbyChests");
            _farmSkipChest = AccessTools.Method(typeof(FarmController), "SkipChest");
            if (farmChest != null && _farmChests != null && _farmSkipChest != null)
                harmony.Patch(farmChest, postfix: new HarmonyMethod(typeof(StorageSortPatches), nameof(HarvestByKind)));
            else
                Plugin.Log.LogWarning("Guardar la cosecha junto a cosas del mismo tipo no está disponible en esta versión de Offline Companions Reborn.");
        }

        private static void StowByKind(UpkeepController __instance, string key, ref Container __result)
        {
            try
            {
                // Si ya eligió un cofre que tiene lo mismo, se queda ese.
                if (__result != null && (int)_upkeepCountKey.Invoke(null, new object[] { __result.GetInventory(), key }) > 0) return;
                Inventory pack = __instance.GetComponent<Humanoid>()?.GetInventory();
                if (pack == null) return;
                var carried = new List<ItemDrop.ItemData>();
                foreach (ItemDrop.ItemData item in pack.GetAllItems())
                {
                    if (item?.m_shared != null && (string)_upkeepKey.Invoke(null, new object[] { item }) == key) carried.Add(item);
                }
                if (carried.Count == 0)
                {
                    foreach (ItemDrop.ItemData item in pack.GetAllItems())
                    {
                        if (item?.m_shared != null && item.m_shared.m_name == key) carried.Add(item);
                    }
                }
                var chests = _upkeepChests.Invoke(__instance, null) as List<Container>;
                Container pick = StorageSort.Pick(chests, carried, __instance.transform.position,
                    chest => (bool)_upkeepRoomIn.Invoke(null, new object[] { chest, key }));
                if (pick != null) __result = pick;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Guardar por tipo: " + ex.Message);
            }
        }

        private static void GatherByKind(HarvestController h, ref Container __result)
        {
            try
            {
                if (__result == null || h == null) return;
                Humanoid humanoid = h.GetComponent<Humanoid>();
                Inventory pack = humanoid?.GetInventory();
                if (pack == null) return;
                var carried = new List<ItemDrop.ItemData>();
                foreach (ItemDrop.ItemData item in pack.GetAllItems())
                {
                    if (item?.m_shared != null && !CompanionInteract.ShouldKeep(item, humanoid)) carried.Add(item);
                }
                CompanionSetup setup = h.GetComponent<CompanionSetup>();
                Vector3 position = h.transform.position;
                bool home = setup != null && setup.HasHomePosition() && MHR_ToolKeeper.NearHome(setup);
                List<Container> near = home ? MHR_Chests.Near(setup.GetHomePosition(), 50f) : MHR_Chests.Near(position, 30f);
                Container pick = StorageSort.Pick(near, carried, position, chest =>
                    chest.GetInventory().HaveEmptySlot() && MHR_Chests.Usable(chest, h) && MHR_Chests.CanReach(position, chest));
                if (pick != null) __result = pick;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Guardar lo recolectado por tipo: " + ex.Message);
            }
        }

        private static void HarvestByKind(FarmController __instance, ref Container __result)
        {
            try
            {
                if (__result == null) return;
                Inventory pack = __instance.GetComponent<Humanoid>()?.GetInventory();
                var chests = _farmChests.GetValue(__instance) as List<Container>;
                if (pack == null || chests == null) return;
                var carried = new List<ItemDrop.ItemData>();
                foreach (ItemDrop.ItemData item in pack.GetAllItems())
                {
                    if (__instance.IsCropItem(item)) carried.Add(item);
                }
                Container pick = StorageSort.Pick(chests, carried, __instance.transform.position, chest =>
                    !(bool)_farmSkipChest.Invoke(__instance, new object[] { chest }) && chest.GetInventory().GetEmptySlots() > 0);
                if (pick != null) __result = pick;
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning("Guardar la cosecha por tipo: " + ex.Message);
            }
        }
    }
}
