using HarmonyLib;
using MGSC;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Quasimorph_Refill_Button
{
    public static class RefillService
    {
        public const int RefillCommandValue = 200000;

        public const string RefillCaptionKey = "mod.refillbutton.caption";

        public const int RefillUsagesCommandValue = 200001;

        public const string RefillUsagesCaptionKey = "mod.refillbutton.usagescaption";

        public static KeyCode RefillHotkeyKey => Plugin.Config?.RefillHotkeyKey ?? KeyCode.F;

        public static KeyCode RefillUsagesHotkeyKey => Plugin.Config?.RefillUsagesHotkeyKey ?? KeyCode.U;

        public static string RefillCaptionWithHotkey => RefillHotkeyPrefix(RefillHotkeyKey, Plugin.Config?.HideRefillKeybindHighlight ?? false) + (Localization.HasKey(RefillCaptionKey) ? Localization.Get(RefillCaptionKey) : "Refill");

        public static string RefillUsagesCaptionWithHotkey => RefillHotkeyPrefix(RefillUsagesHotkeyKey, Plugin.Config?.HideRefillUsagesKeybindHighlight ?? false) + (Localization.HasKey(RefillUsagesCaptionKey) ? Localization.Get(RefillUsagesCaptionKey) : "Refill usages");

        private static string RefillHotkeyPrefix(KeyCode key, bool hide)
        {
            if (hide || key == KeyCode.None)
            {
                return "";
            }
            return (key.ToString() + " ").WrapInColor(Colors.Yellow);
        }

        internal static bool RefillUsagesHiddenInRaid;

        public static bool IsRaid()
        {
            return SingletonMonoBehaviour<DungeonGameMode>.Instance != null;
        }

        public static bool CanShowRefill(BasePickupItem item)
        {
            return item != null
                && !item.Locked
                && !item.IsImplicit
                && ((item.IsStackable && !item.IsFullStack) || (item.IsUsable && !item.HasFullUsages));
        }

        public static bool CanShowRefillUsages(BasePickupItem item)
        {
            return item != null
                && !item.Locked
                && !item.IsImplicit
                && item.IsUsable
                && !item.HasFullUsages;
        }

        public static BasePickupItem GetContextMenuItem(object screen)
        {
            return Traverse.Create(screen).Field("_contextMenuItemSlot").GetValue<ItemSlot>()?.Item;
        }

        public static void HandleShipRefill(ScreenWithShipCargo screen, bool usagesOnly = false)
        {
            BasePickupItem item = GetContextMenuItem(screen);
            if (item == null)
            {
                return;
            }
            if (usagesOnly ? !CanShowRefillUsages(item) : !CanShowRefill(item))
            {
                PlayFeedback(false);
                return;
            }
            if (!IsValidShipRefillStorage(screen, item))
            {
                return;
            }

            List<ItemStorage> sources = GetShipRefillSources(screen);
            Inventory operatorInventory = GetOperatorInventory(screen);
            if (operatorInventory != null && operatorInventory.AllContainers.Contains(item.Storage))
            {
                sources.AddRange(operatorInventory.AllContainers);
            }

            SpaceTime spaceTime = SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow();
            bool any = usagesOnly ? TryRefillUsages(item, sources, spaceTime) : TryRefill(item, sources, spaceTime);
            PlayFeedback(any);
            screen.RefreshView();
        }

        private static Inventory GetOperatorInventory(ScreenWithShipCargo screen)
        {
            Mercenary merc = Traverse.Create(screen).Field("_merc").GetValue<Mercenary>();
            return merc?.CreatureData?.Inventory;
        }

        public static bool IsValidShipRefillStorage(ScreenWithShipCargo screen, BasePickupItem item)
        {
            if (IsShipCargoItem(item))
            {
                return true;
            }
            if (screen is FastTradeScreen)
            {
                Station station = Traverse.Create(screen).Field("_station").GetValue<Station>();
                return station?.Stash == item.Storage;
            }
            if (screen is TradeShuttleScreen)
            {
                return IsTradeShuttleTarget(screen, item);
            }
            Mercenary merc = Traverse.Create(screen).Field("_merc").GetValue<Mercenary>();
            return merc?.CreatureData?.Inventory?.AllContainers.Contains(item.Storage) == true;
        }

        public static bool IsShipCargoItem(BasePickupItem item)
        {
            if (item?.Storage == null)
            {
                return false;
            }
            MagnumCargo cargo = SingletonMonoBehaviour<SpaceGameMode>.Instance?.Get<MagnumCargo>();
            return cargo != null && (cargo.ShipCargo.Contains(item.Storage) || item.Storage == cargo.FridgeStorage);
        }

        private static bool IsTradeShuttleTarget(ScreenWithShipCargo screen, BasePickupItem item)
        {
            TradeShuttleDepartment department = Traverse.Create(screen).Field("_tradeShuttleDepartment").GetValue<TradeShuttleDepartment>();
            return department != null && department.TradeShuttleStorage == item.Storage;
        }

        private static List<ItemStorage> GetShipRefillSources()
        {
            MagnumCargo cargo = SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<MagnumCargo>();
            MagnumProgression ship = SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<MagnumProgression>();
            List<ItemStorage> sources = new List<ItemStorage>();
            foreach (ItemStorage storage in cargo.ShipCargo)
            {
                MagnumCargoTab tab = cargo.GetTab(storage);
                if (tab != null && tab.IncludeToSort)
                {
                    sources.Add(storage);
                }
            }
            if (ship != null && ship.HasStoreFridge && cargo.FridgeTab.IncludeToSort)
            {
                sources.Add(cargo.FridgeStorage);
            }
            return sources;
        }

        private static List<ItemStorage> GetShipRefillSources(ScreenWithShipCargo screen)
        {
            List<ItemStorage> sources = GetShipRefillSources();
            if (screen is FastTradeScreen)
            {
                Station station = Traverse.Create(screen).Field("_station").GetValue<Station>();
                if (station?.Stash != null)
                {
                    sources.Add(station.Stash);
                }
            }
            return sources;
        }

        public static void HandleRaidRefill(InventoryScreen screen, bool usagesOnly = false)
        {
            Traverse t = Traverse.Create(screen);
            BasePickupItem item = GetContextMenuItem(screen);
            if (item == null || (usagesOnly ? !CanShowRefillUsages(item) : !CanShowRefill(item)))
            {
                PlayFeedback(false);
                return;
            }

            SpaceTime spaceTime = SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow();
            if (IsShipCargoItem(item))
            {
                bool anyCargo = usagesOnly
                    ? TryRefillUsages(item, GetShipRefillSources(), spaceTime)
                    : TryRefill(item, GetShipRefillSources(), spaceTime);
                PlayFeedback(anyCargo);
                screen.RefreshItemsList();
                return;
            }

            Creatures creatures = t.Field("_creatures").GetValue<Creatures>();
            TurnController turnController = t.Field("_turnController").GetValue<TurnController>();
            TurnMetadata turnMetadata = t.Field("_turnMetadata").GetValue<TurnMetadata>();
            if (creatures == null || !TurnSystem.CanProcessPlayerTurn(turnController, turnMetadata, creatures))
            {
                PlayFeedback(false);
                return;
            }

            Inventory inventory = creatures.Player?.CreatureData?.Inventory;
            if (inventory == null || !inventory.AllContainers.Contains(item.Storage))
            {
                return;
            }

            List<ItemStorage> sources = CollectRaidSources(inventory, t.Field("_tabsView").GetValue<ItemTabsView>());
            if (sources.Count == 0)
            {
                PlayFeedback(false);
                return;
            }

            bool any = usagesOnly ? TryRefillUsages(item, sources, spaceTime) : TryRefill(item, sources, spaceTime);
            if (!any)
            {
                PlayFeedback(false);
                return;
            }

            PlayFeedback(true);
            screen.RefreshItemsList();
            creatures.Player.CreatureData.EffectsController.PropagateAction(PlayerActionHappened.HandAction);
            PlayerInteractionSystem.EndPlayerTurn(creatures, PlayerEndTurnContext.InventoryInteraction);
        }

        private static List<ItemStorage> CollectRaidSources(Inventory inventory, ItemTabsView tabsView)
        {
            List<ItemStorage> result = new List<ItemStorage>();
            object content = tabsView?.FirstSelectedTab()?.Content;
            if (content is ItemStorage storage)
            {
                if (!inventory.AllContainers.Contains(storage))
                {
                    result.Add(storage);
                }
            }
            else if (content is CorpseStorage corpse)
            {
                foreach (ItemStorage container in corpse.CreatureData?.Inventory?.AllContainers ?? new List<ItemStorage>())
                {
                    if (!inventory.AllContainers.Contains(container))
                    {
                        result.Add(container);
                    }
                }
            }
            result.AddRange(inventory.AllContainers);
            return result;
        }

        private static bool TryRefill(BasePickupItem target, IEnumerable<ItemStorage> sources, SpaceTime spaceTime)
        {
            bool any = false;
            foreach (ItemStorage storage in sources)
            {
                if (storage == null)
                {
                    continue;
                }
                foreach (BasePickupItem source in storage.Items.ToList())
                {
                    if (source == null || ReferenceEquals(source, target) || source.Id != target.Id)
                    {
                        continue;
                    }
                    if (target.IsUsable && target.IsFullStack && !target.HasFullUsages && source.IsUsable)
                    {
                        if (TryTransferUsages(target, source, target.MaxStack, spaceTime))
                        {
                            any = true;
                            if (target.HasFullUsages)
                            {
                                return any;
                            }
                        }
                        continue;
                    }
                    if (!ItemInteractionSystem.CanMerge(source, target))
                    {
                        continue;
                    }
                    bool emptyAfterMerge = false;
                    if (ItemInteractionSystem.Merge(spaceTime, source, target, ref emptyAfterMerge))
                    {
                        any = true;
                        if (target.IsFullStack && target.HasFullUsages)
                        {
                            return any;
                        }
                    }
                }
            }
            return any;
        }

        private static bool TryRefillUsages(BasePickupItem target, IEnumerable<ItemStorage> sources, SpaceTime spaceTime)
        {
            if (target == null || !target.IsUsable || target.HasFullUsages)
            {
                return false;
            }
            bool any = false;
            short stackCount = target.StackCount;
            foreach (ItemStorage storage in sources)
            {
                if (storage == null)
                {
                    continue;
                }
                foreach (BasePickupItem source in storage.Items.ToList())
                {
                    if (source == null || ReferenceEquals(source, target) || source.Id != target.Id || !source.IsUsable)
                    {
                        continue;
                    }
                    if (TryTransferUsages(target, source, stackCount, spaceTime))
                    {
                        any = true;
                        target.StackCount = stackCount;
                        if (target.HasFullUsages)
                        {
                            return any;
                        }
                    }
                }
            }
            target.StackCount = stackCount;
            return any;
        }

        private static bool TryTransferUsages(BasePickupItem target, BasePickupItem source, int usageStackCap, SpaceTime spaceTime)
        {
            UsableItemComponent targetComp = target.Comp<UsableItemComponent>();
            UsableItemComponent sourceComp = source.Comp<UsableItemComponent>();
            if (targetComp == null || sourceComp == null || sourceComp.CurrentUsageValue <= 0)
            {
                return false;
            }
            int before = targetComp.CurrentUsageValue;
            targetComp.MergeUsages(sourceComp, usageStackCap);
            if (targetComp.CurrentUsageValue == before)
            {
                return false;
            }
            target.StackCount = (short)targetComp.GetStackCount();
            source.StackCount = (short)sourceComp.GetStackCount();
            target.UpdateExpireAtRestack(spaceTime, source);
            source.UpdateExpireAtRestack(spaceTime, target);
            if (sourceComp.CurrentUsageValue <= 0)
            {
                source.Storage?.Remove(source);
            }
            return true;
        }

        private static void PlayFeedback(bool any)
        {
            SoundsStorage sounds = SingletonMonoBehaviour<SoundsStorage>.Instance;
            SoundController controller = SingletonMonoBehaviour<SoundController>.Instance;
            controller.PlayUiSound(any ? sounds.AmmoReceived : sounds.EmptyAttack);
        }
    }
}