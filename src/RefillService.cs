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

        public const string RefillCaption = "Refill";

        public const KeyCode RefillHotkeyKey = KeyCode.F;

        public static string RefillCaptionWithHotkey => ("F ").WrapInColor(Colors.Yellow) + RefillCaption;

        public static bool CanShowRefill(BasePickupItem item)
        {
            return item != null
                && item.IsStackable
                && item.StackCount < item.MaxStack
                && !item.Locked
                && !item.IsImplicit;
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

            bool any = TryRefill(item, GetShipRefillSources(), SingletonMonoBehaviour<ItemFactory>.Instance.GetGameTimeNow());
            PlayFeedback(any);
            screen.RefreshView();
        }

        public static bool IsValidShipRefillStorage(ScreenWithShipCargo screen, BasePickupItem item)
        {
            if (screen is TradeShuttleScreen)
            {
                return IsTradeShuttleTarget(screen, item);
            }
            Mercenary merc = Traverse.Create(screen).Field("_merc").GetValue<Mercenary>();
            return merc?.CreatureData?.Inventory?.AllContainers.Contains(item.Storage) == true;
        }

        private static bool IsTradeShuttleTarget(ScreenWithShipCargo screen, BasePickupItem item)
        {
            MagnumCargo cargo = SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<MagnumCargo>();
            if (cargo.ShipCargo.Contains(item.Storage) || item.Storage == cargo.FridgeStorage)
            {
                return true;
            }
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

        public static void HandleRaidRefill(InventoryScreen screen)
        {
            Traverse t = Traverse.Create(screen);
            Creatures creatures = t.Field("_creatures").GetValue<Creatures>();
            TurnController turnController = t.Field("_turnController").GetValue<TurnController>();
            TurnMetadata turnMetadata = t.Field("_turnMetadata").GetValue<TurnMetadata>();
            if (creatures == null || !TurnSystem.CanProcessPlayerTurn(turnController, turnMetadata, creatures))
            {
                PlayFeedback(false);
                return;
            }

            BasePickupItem item = GetContextMenuItem(screen);
            Inventory inventory = creatures.Player?.CreatureData?.Inventory;
            if (item == null || inventory == null || !inventory.AllContainers.Contains(item.Storage))
            {
                return;
            }
            if (!CanShowRefill(item))
            {
                PlayFeedback(false);
                return;
            }

            List<ItemStorage> sources = CollectRaidSources(t.Field("_tabsView").GetValue<ItemTabsView>());
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

        private static List<ItemStorage> CollectRaidSources(ItemTabsView tabsView)
        {
            List<ItemStorage> result = new List<ItemStorage>();
            object content = tabsView?.FirstSelectedTab()?.Content;
            if (content is ItemStorage storage)
            {
                result.Add(storage);
            }
            else if (content is CorpseStorage corpse)
            {
                result.AddRange(corpse.CreatureData?.Inventory?.AllContainers ?? new List<ItemStorage>());
            }
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
                    if (!ItemInteractionSystem.CanMerge(source, target))
                    {
                        continue;
                    }
                    bool emptyAfterMerge = false;
                    if (ItemInteractionSystem.Merge(spaceTime, source, target, ref emptyAfterMerge))
                    {
                        any = true;
                        if (target.IsFullStack)
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