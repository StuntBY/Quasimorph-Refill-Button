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

        public const KeyCode RefillHotkeyKey = KeyCode.F;

        public static string RefillCaptionWithHotkey => ("F ").WrapInColor(Colors.Yellow) + (Localization.HasKey(RefillCaptionKey) ? Localization.Get(RefillCaptionKey) : "Refill");

        public static bool CanShowRefill(BasePickupItem item)
        {
            return item != null
                && !item.Locked
                && !item.IsImplicit
                && ((item.IsStackable && !item.IsFullStack) || (item.IsUsable && !item.HasFullUsages));
        }

        public static BasePickupItem GetContextMenuItem(object screen)
        {
            return Traverse.Create(screen).Field("_contextMenuItemSlot").GetValue<ItemSlot>()?.Item;
        }

        public static void HandleShipRefill(ScreenWithShipCargo screen)
        {
            BasePickupItem item = GetContextMenuItem(screen);
            if (item == null)
            {
                return;
            }
            if (!CanShowRefill(item))
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

            bool any = TryRefill(item, sources, SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow());
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

        public static void HandleRaidRefill(InventoryScreen screen)
        {
            Traverse t = Traverse.Create(screen);
            BasePickupItem item = GetContextMenuItem(screen);
            if (item == null || !CanShowRefill(item))
            {
                PlayFeedback(false);
                return;
            }

            if (IsShipCargoItem(item))
            {
                bool anyCargo = TryRefill(item, GetShipRefillSources(), SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow());
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

            bool any = TryRefill(item, sources, SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow());
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
                        UsableItemComponent targetComp = target.Comp<UsableItemComponent>();
                        UsableItemComponent sourceComp = source.Comp<UsableItemComponent>();
                        if (targetComp == null || sourceComp == null || sourceComp.CurrentUsageValue <= 0)
                        {
                            continue;
                        }
                        target.UpdateUsagesAtReStack(source);
                        target.StackCount = (short)targetComp.GetStackCount();
                        source.StackCount = (short)sourceComp.GetStackCount();
                        target.UpdateExpireAtRestack(spaceTime, source);
                        source.UpdateExpireAtRestack(spaceTime, target);
                        any = true;
                        if (sourceComp.CurrentUsageValue <= 0)
                        {
                            source.Storage?.Remove(source);
                        }
                        if (targetComp.IsMax)
                        {
                            return any;
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

        private static void PlayFeedback(bool any)
        {
            SoundsStorage sounds = SingletonMonoBehaviour<SoundsStorage>.Instance;
            SoundController controller = SingletonMonoBehaviour<SoundController>.Instance;
            controller.PlayUiSound(any ? sounds.AmmoReceived : sounds.EmptyAttack);
        }
    }
}