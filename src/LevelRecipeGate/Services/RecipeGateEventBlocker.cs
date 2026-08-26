using System;
using System.Collections.Generic;
using System.Linq;
using BlackVeinCore;
using BlackVeinCore.Data;
using BlackVeinCore.Events;
using BlackVeinCore.Tools;
using BlackVeinCore.Utilities;
using LevelRecipeGate.Config;
using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace LevelRecipeGate.Services
{
	internal static class RecipeGateEventBlocker
	{
		private static readonly AssetGuid BlockedCraftSctAssetGuid = AssetGuid.FromString("4f3a2b28-77e7-4f7a-9fc2-b2e19cddf9e1");
		private static readonly PrefabGUID BlockedCraftSctType = new PrefabGUID(311215354);
		private static readonly TimeSpan BlockedFeedbackCooldown = TimeSpan.FromSeconds(2);
		private static readonly Dictionary<Entity, DateTime> LastBlockedFeedbackAtByUser = new Dictionary<Entity, DateTime>();

		private enum BlockedAction
		{
			Equip,
			Use
		}

		internal static void Initialize()
		{
			GameEvents.OnEquipItem += TryBlockEquippableEquipEvent;
			GameEvents.OnEquipJewel += TryBlockJewelEquipEvent;
			GameEvents.OnBeforeTryAddItem += TryDisableBlockedEquippableAutoEquip;
			GameEvents.OnPlayerUsedConsumable += TryBlockConsumableUseEvent;
			GameEvents.OnPlayerInventoryChanged += TryUnequipBlockedObtainedEquippable;
		}

		internal static void Dispose()
		{
			GameEvents.OnEquipItem -= TryBlockEquippableEquipEvent;
			GameEvents.OnEquipJewel -= TryBlockJewelEquipEvent;
			GameEvents.OnBeforeTryAddItem -= TryDisableBlockedEquippableAutoEquip;
			GameEvents.OnPlayerUsedConsumable -= TryBlockConsumableUseEvent;
			GameEvents.OnPlayerInventoryChanged -= TryUnequipBlockedObtainedEquippable;
			LastBlockedFeedbackAtByUser.Clear();
		}

		internal static void TryBlockEquippableEquipEvent(Entity eventEntity, Entity itemEntity, PrefabGUID itemGuid)
		{
			if (!TryGetEquippableGate(itemEntity, itemGuid, out PrefabGUID gateGuid, out int requiredLevel, out _))
				return;

			TryBlockEvent(eventEntity, gateGuid, requiredLevel, BlockedAction.Equip);
		}

		internal static void TryBlockJewelEquipEvent(Entity eventEntity, Entity itemEntity, PrefabGUID itemGuid, int tier)
		{
			if (!ConfigStore.TryGetRequiredJewelTierLevel(tier, out int requiredLevel))
				return;

			int playerCurrentGearLevel = TryGetPlayerCurrentGearLevel(eventEntity, out User user, out Entity userEntity, out Entity characterEntity);

			if (playerCurrentGearLevel < 0)
			{
				eventEntity.Destroy();
				TrySendBlockedFeedback(userEntity, characterEntity, () => TrySendSystemMessage(user, "Unable to determine your gear level. Jewel equip blocked."));
				Plugin.Logger.LogWarning($"[{Plugin.Name}] BLOCKED jewel tier {tier} equip: could not read player gear level.");
				return;
			}

			if (playerCurrentGearLevel >= requiredLevel)
				return;

			eventEntity.Destroy();
			TrySendBlockedFeedback(userEntity, characterEntity, () => TrySendBlockedJewelMessage(userEntity, tier, requiredLevel, playerCurrentGearLevel));
		}

		private static void TryDisableBlockedEquippableAutoEquip(Entity target, PrefabGUID itemGuid, ref AddItemSettings addItemSettings)
		{
			if (!addItemSettings.EquipIfPossible ||
			    itemGuid == PrefabGUID.Empty ||
			    !ConfigStore.TryGetRequiredEquippableLevel(itemGuid.GuidHash, out int requiredLevel) ||
			    !TryGetAddItemTargetPlayer(target, out Player player))
				return;

			int playerCurrentGearLevel = TryGetPlayerCurrentGearLevel(player);
			if (playerCurrentGearLevel >= requiredLevel)
				return;

			addItemSettings.EquipIfPossible = false;
		}

		private static void TryBlockConsumableUseEvent(Player player, Entity eventEntity, UseItemEvent useItemEvent, InventoryBuffer item)
		{
			if (IsSecondaryBloodPotion(item) &&
			    ConfigStore.TryGetRequiredSecondaryBloodPotionLevel(out int secondaryBloodPotionRequiredLevel))
			{
				int playerHighestGearLevelForSecondaryBloodPotion = TryGetPlayerHighestGearLevel(player);
				if (playerHighestGearLevelForSecondaryBloodPotion < secondaryBloodPotionRequiredLevel)
				{
					eventEntity.Destroy();
					TrySendBlockedFeedback(player.User, player.Character, () => TrySendSecondaryBloodPotionBlockedMessage(player.User, secondaryBloodPotionRequiredLevel, playerHighestGearLevelForSecondaryBloodPotion));
					return;
				}
			}

			if (!ConfigStore.TryGetRequiredConsumableLevel(item.ItemType.GuidHash, out int requiredLevel))
				return;

			int playerHighestGearLevel = TryGetPlayerHighestGearLevel(player);

			if (playerHighestGearLevel >= requiredLevel)
				return;

			eventEntity.Destroy();
			TrySendBlockedFeedback(player.User, player.Character, () => TrySendBlockedMessage(player.User, item.ItemType, requiredLevel, playerHighestGearLevel, BlockedAction.Use));
		}

		private static void TryUnequipBlockedObtainedEquippable(Player player, Entity eventEntity, InventoryChangedEvent inventoryChangedEvent)
		{
			if (inventoryChangedEvent.ChangeType != InventoryChangedEventType.Obtained ||
			    inventoryChangedEvent.Item == PrefabGUID.Empty ||
			    inventoryChangedEvent.Amount <= 0 ||
			    !inventoryChangedEvent.ItemEntity.Exists())
				return;

			if (!TryGetEquippableGate(inventoryChangedEvent.ItemEntity, inventoryChangedEvent.Item, out PrefabGUID gateGuid, out int requiredLevel, out EquipmentType equipmentType))
				return;

			int playerCurrentGearLevel = TryGetPlayerCurrentGearLevel(player);
			if (playerCurrentGearLevel >= requiredLevel || !IsEquippedBy(player.Character, inventoryChangedEvent.ItemEntity))
				return;

			TryQueueUnequipItem(player, inventoryChangedEvent.InventoryEntity, equipmentType);
			TrySendBlockedFeedback(player.User, player.Character, () => TrySendBlockedMessage(player.User, gateGuid, requiredLevel, playerCurrentGearLevel, BlockedAction.Equip));
		}

		private static void TryBlockEvent(Entity eventEntity, PrefabGUID gateGuid, int requiredLevel, BlockedAction action)
		{
			int playerCurrentGearLevel = TryGetPlayerCurrentGearLevel(eventEntity, out User user, out Entity userEntity, out Entity characterEntity);

			if (playerCurrentGearLevel < 0)
			{
				eventEntity.Destroy();
				TrySendBlockedFeedback(userEntity, characterEntity, () => TrySendSystemMessage(user, "Unable to determine your gear level. Equipping blocked."));
				Plugin.Logger.LogWarning($"[{Plugin.Name}] BLOCKED equipping gate={gateGuid.GuidHash}: could not read player gear level.");
				return;
			}

			if (playerCurrentGearLevel >= requiredLevel)
				return;

			eventEntity.Destroy();
			TrySendBlockedFeedback(userEntity, characterEntity, () => TrySendBlockedMessage(userEntity, gateGuid, requiredLevel, playerCurrentGearLevel, action));
		}

		private static bool TryGetEquippableGate(
			Entity itemEntity,
			PrefabGUID itemGuid,
			out PrefabGUID gateGuid,
			out int requiredLevel,
			out EquipmentType equipmentType)
		{
			gateGuid = itemGuid;
			requiredLevel = 0;
			equipmentType = EquipmentType.None;

			if (!itemEntity.Exists() || !itemEntity.Has<EquippableData>())
				return false;

			equipmentType = itemEntity.Read<EquippableData>().EquipmentType;
			if (ConfigStore.TryGetRequiredEquippableLevel(itemGuid.GuidHash, out requiredLevel))
				return true;

			return false;
		}

		private static bool IsSecondaryBloodPotion(InventoryBuffer item)
		{
			Entity itemEntity = item.ItemEntity._Entity;
			if (!itemEntity.Exists() || !itemEntity.Has<StoredBlood>())
				return false;

			var storedBlood = itemEntity.Read<StoredBlood>();
			return storedBlood.SecondaryBlood.Type != PrefabGUID.Empty &&
			       storedBlood.SecondaryBlood.Type != Prefabs.BloodType_None;
		}

		private static bool TryGetAddItemTargetPlayer(Entity target, out Player player)
		{
			if (PlayerService.TryFromCharacter(target, out player))
				return true;

			if (PlayerService.TryFromUser(target, out player))
				return true;

			if (target.Exists() && target.Has<InventoryConnection>())
			{
				Entity owner = target.Read<InventoryConnection>().InventoryOwner;
				if (PlayerService.TryFromCharacter(owner, out player))
					return true;
			}

			player = null!;
			return false;
		}

		private static bool IsEquippedBy(Entity characterEntity, Entity itemEntity)
		{
			return characterEntity.Exists() &&
			       itemEntity.Exists() &&
			       itemEntity.Has<Equipped>() &&
			       itemEntity.Read<Equipped>().Target == characterEntity;
		}

		private static void TryQueueUnequipItem(Player player, Entity targetInventory, EquipmentType equipmentType)
		{
			try
			{
				if (!player.Character.Exists())
					return;

				Entity destination = targetInventory.Exists() && targetInventory.Has<NetworkId>()
					? targetInventory
					: player.Character;

				if (!destination.Exists() || !destination.Has<NetworkId>())
					return;

				int targetSlot = FindEmptyInventorySlot(targetInventory);
				Entity unequipEventEntity = Helper.CreateEntityWithComponents<FromCharacter, UnequipItemEvent>();
				unequipEventEntity.Write(player.ToFromCharacter());
				unequipEventEntity.Write(new UnequipItemEvent
				{
					EquipmentType = equipmentType,
					ToInventory = destination.Read<NetworkId>(),
					ToSlotIndex = targetSlot
				});
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Could not queue item unequip: {ex}");
			}
		}

		private static int FindEmptyInventorySlot(Entity inventoryEntity)
		{
			if (!inventoryEntity.TryGetInventoryBuffer(out var buffer))
				return -1;

			for (int index = 0; index < buffer.Length; index++)
			{
				if (buffer[index].ItemType == PrefabGUID.Empty)
					return index;
			}

			return -1;
		}

		private static int TryGetPlayerHighestGearLevel(Player player)
		{
			if (player == null || !player.User.Exists() || !player.User.Has<User>())
				return -1;

			try
			{
				return PlayerLevelService.UpdateAndGetHighestGearLevel(
					Core.EntityManager,
					player.User.Read<User>(),
					player.Character);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Failed reading highest player gear level: {ex.Message}");
				return -1;
			}
		}

		private static int TryGetPlayerCurrentGearLevel(Player player)
		{
			if (player == null || !player.Character.Exists())
				return -1;

			try
			{
				return PlayerLevelService.GetCurrentGearLevel(Core.EntityManager, player.Character);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Failed reading current player gear level: {ex.Message}");
				return -1;
			}
		}

		private static int TryGetPlayerCurrentGearLevel(Entity eventEntity, out User user, out Entity userEntity, out Entity characterEntity)
		{
			user = default(User);
			userEntity = Entity.Null;
			characterEntity = Entity.Null;

			try
			{
				if (!eventEntity.Has<FromCharacter>())
					return -1;

				FromCharacter fromCharacter = eventEntity.Read<FromCharacter>();

				userEntity = fromCharacter.User;
				characterEntity = fromCharacter.Character;
				if (!userEntity.Exists() || !userEntity.Has<User>())
					return -1;

				user = userEntity.Read<User>();
				return PlayerLevelService.GetCurrentGearLevel(Core.EntityManager, characterEntity);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Failed reading current player gear level: {ex.Message}");
				return -1;
			}
		}

		private static void TrySendBlockedFeedback(Entity userEntity, Entity characterEntity, Action sendMessage)
		{
			if (!TryBeginBlockedFeedbackCooldown(userEntity))
				return;

			TrySendBlockedCraftSct(userEntity, characterEntity);
			sendMessage();
		}

		private static bool TryBeginBlockedFeedbackCooldown(Entity userEntity)
		{
			if (!userEntity.Exists())
				return true;

			DateTime now = DateTime.UtcNow;
			PruneBlockedFeedbackCooldowns(now);

			if (LastBlockedFeedbackAtByUser.TryGetValue(userEntity, out DateTime lastSentAt) &&
			    now - lastSentAt < BlockedFeedbackCooldown)
				return false;

			LastBlockedFeedbackAtByUser[userEntity] = now;
			return true;
		}

		private static void PruneBlockedFeedbackCooldowns(DateTime now)
		{
			TimeSpan retention = TimeSpan.FromTicks(BlockedFeedbackCooldown.Ticks * 10);
			foreach (var staleUser in LastBlockedFeedbackAtByUser
				         .Where(kvp => now - kvp.Value > retention)
				         .Select(kvp => kvp.Key)
				         .ToList())
			{
				LastBlockedFeedbackAtByUser.Remove(staleUser);
			}
		}

		private static void TrySendBlockedCraftSct(Entity userEntity, Entity characterEntity)
		{
			try
			{
				if (!userEntity.Exists() || !characterEntity.Exists())
					return;

				ScrollingCombatTextMessage.Create(
					Core.EntityManager,
					Core.EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
					BlockedCraftSctAssetGuid,
					characterEntity.Read<Translation>().Value,
					new float3(),
					characterEntity,
					0f,
					BlockedCraftSctType,
					userEntity);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Could not send blocked craft SCT: {ex}");
			}
		}

		private static void TrySendBlockedMessage(Entity userEntity, PrefabGUID gateGuid, int requiredLevel, int playerGateLevel, BlockedAction action)
		{
			if (!userEntity.Exists() || !userEntity.Has<User>())
				return;

			string message = RichTextMessageFormatter.Format(
				GetBlockedMessageTemplate(action),
				new Dictionary<string, object>
				{
					{ "level", requiredLevel },
					{ "current", playerGateLevel },
					{ "recipe", gateGuid.GuidHash }
				}).Error();

			TrySendSystemMessage(userEntity.Read<User>(), message);
		}

		private static void TrySendBlockedJewelMessage(Entity userEntity, int tier, int requiredLevel, int playerGateLevel)
		{
			if (!userEntity.Exists() || !userEntity.Has<User>())
				return;

			string message = RichTextMessageFormatter.Format(
				"You cannot equip tier {tier.Warning()} jewels yet. Required gear level: {level.Warning()}.",
				new Dictionary<string, object>
				{
					{ "tier", tier },
					{ "level", requiredLevel },
					{ "current", playerGateLevel }
				}).Error();

			TrySendSystemMessage(userEntity.Read<User>(), message);
		}

		private static void TrySendSecondaryBloodPotionBlockedMessage(Entity userEntity, int requiredLevel, int playerGateLevel)
		{
			if (!userEntity.Exists() || !userEntity.Has<User>())
				return;

			string message = RichTextMessageFormatter.Format(
				"You cannot drink mixed blood potions yet. Required gear level: {level.Warning()}.",
				new Dictionary<string, object>
				{
					{ "level", requiredLevel },
					{ "current", playerGateLevel }
				}).Error();

			TrySendSystemMessage(userEntity.Read<User>(), message);
		}

		private static string GetBlockedMessageTemplate(BlockedAction action)
		{
			return action switch
			{
				BlockedAction.Equip => "You cannot equip this yet. Required gear level: {level.Warning()}.",
				BlockedAction.Use => "You cannot use this yet. Required gear level: {level.Warning()}.",
				_ => "You cannot do that yet. Required gear level: {level.Warning()}."
			};
		}

		private static void TrySendSystemMessage(User user, string message)
		{
			try
			{
				FixedString512Bytes fixedMessage = message;
				ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref fixedMessage);
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning($"[{Plugin.Name}] Could not send system message: {ex}");
			}
		}
	}
}
