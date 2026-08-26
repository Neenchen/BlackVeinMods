using BlackVeinCore.Events;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace BlackVeinCore.Patches;

[HarmonyPatch(typeof(InventoryUtilitiesServer), nameof(InventoryUtilitiesServer.TryAddItem), new[] { typeof(AddItemSettings), typeof(Entity), typeof(PrefabGUID), typeof(int) })]
public static class InventoryUtilitiesServerTryAddItemPrefabPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref AddItemSettings addItemSettings, Entity target, PrefabGUID itemType)
    {
        if (GameEvents.OnBeforeTryAddItem == null)
        {
            return;
        }

        try
        {
            GameEvents.OnBeforeTryAddItem?.Invoke(target, itemType, ref addItemSettings);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish before try add item event: {ex}");
        }
    }
}

[HarmonyPatch(typeof(InventoryUtilitiesServer), nameof(InventoryUtilitiesServer.TryAddItem), new[] { typeof(AddItemSettings), typeof(Entity), typeof(InventoryBuffer) })]
public static class InventoryUtilitiesServerTryAddItemBufferPatch
{
    [HarmonyPrefix]
    public static void Prefix(ref AddItemSettings addItemSettings, Entity target, InventoryBuffer inventoryItem)
    {
        if (GameEvents.OnBeforeTryAddItem == null)
        {
            return;
        }

        try
        {
            GameEvents.OnBeforeTryAddItem?.Invoke(target, inventoryItem.ItemType, ref addItemSettings);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[GameEvents] Failed to publish before try add inventory item event: {ex}");
        }
    }
}
