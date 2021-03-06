﻿using Barotrauma.Items.Components;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class AIObjectiveContainItem: AIObjective
    {
        public override string DebugTag => "contain item";

        public Func<Item, float> GetItemPriority;

        public int targetItemCount = 1;
        public string[] ignoredContainerIdentifiers;
        public bool checkInventory = true;

        //can either be a tag or an identifier
        public readonly string[] itemIdentifiers;
        public readonly ItemContainer container;
        public readonly Item item;

        private AIObjectiveGetItem getItemObjective;
        private AIObjectiveGoTo goToObjective;

        private readonly HashSet<Item> containedItems = new HashSet<Item>();

        public bool AllowToFindDivingGear { get; set; } = true;
        public float ConditionLevel { get; set; }

        public AIObjectiveContainItem(Character character, Item item, ItemContainer container, AIObjectiveManager objectiveManager, float priorityModifier = 1)
            : base(character, objectiveManager, priorityModifier)
        {
            this.container = container;
            this.item = item;
        }

        public AIObjectiveContainItem(Character character, string itemIdentifier, ItemContainer container, AIObjectiveManager objectiveManager, float priorityModifier = 1)
            : this(character, new string[] { itemIdentifier }, container, objectiveManager, priorityModifier) { }

        public AIObjectiveContainItem(Character character, string[] itemIdentifiers, ItemContainer container, AIObjectiveManager objectiveManager, float priorityModifier = 1) 
            : base(character, objectiveManager, priorityModifier)
        {
            this.itemIdentifiers = itemIdentifiers;
            for (int i = 0; i < itemIdentifiers.Length; i++)
            {
                itemIdentifiers[i] = itemIdentifiers[i].ToLowerInvariant();
            }

            this.container = container;
        }

        protected override bool Check()
        {
            if (IsCompleted) { return true; }
            if (item != null)
            {
                return container.Inventory.Items.Contains(item);
            }
            else
            {
                int containedItemCount = 0;
                foreach (Item i in container.Inventory.Items)
                {
                    if (i != null && itemIdentifiers.Any(id => i.Prefab.Identifier == id || i.HasTag(id)))
                    {
                        containedItemCount++;
                    }
                }
                return containedItemCount >= targetItemCount;
            }
        }

        public override float GetPriority()
        {
            if (objectiveManager.CurrentOrder == this)
            {
                return AIObjectiveManager.OrderPriority;
            }
            return 1.0f;
        }

        private bool CheckItem(Item i) => itemIdentifiers.Any(id => i.Prefab.Identifier == id || i.HasTag(id)) && i.ConditionPercentage > ConditionLevel;

        protected override void Act(float deltaTime)
        {
            Item itemToContain = item ?? character.Inventory.FindItem(i => CheckItem(i) && i.Container != container.Item, recursive: true);
            if (itemToContain != null)
            {
                // Contain the item
                if (itemToContain.ParentInventory == character.Inventory)
                {
                    character.Inventory.RemoveItem(itemToContain);
                    if (!container.Inventory.TryPutItem(itemToContain, null))
                    {
                        itemToContain.Drop(character);
                        Abandon = true;
                    }
                }
                else
                {
                    if (character.CanInteractWith(container.Item, out _, checkLinked: false))
                    {
                        if (container.Combine(itemToContain, character))
                        {
                            IsCompleted = true;
                        }
                        else
                        {
                            Abandon = true;
                        }
                    }
                    else
                    {
                        TryAddSubObjective(ref goToObjective, () => new AIObjectiveGoTo(container.Item, character, objectiveManager, getDivingGearIfNeeded: AllowToFindDivingGear),
                            onAbandon: () => Abandon = true,
                            onCompleted: () => RemoveSubObjective(ref goToObjective));
                    }
                }
            }
            else
            {
                // No matching items in the inventory, try to get an item
                TryAddSubObjective(ref getItemObjective, () =>
                    new AIObjectiveGetItem(character, itemIdentifiers, objectiveManager, equip: false, checkInventory: checkInventory)
                    {
                        GetItemPriority = GetItemPriority,
                        ignoredContainerIdentifiers = ignoredContainerIdentifiers,
                        ignoredItems = containedItems,
                        AllowToFindDivingGear = this.AllowToFindDivingGear
                    }, onAbandon: () =>
                    {
                        Abandon = true;
                    }, onCompleted: () =>
                    {
                        if (getItemObjective.TargetItem != null)
                        {
                            containedItems.Add(getItemObjective.TargetItem);
                        }
                        else
                        {
                            if (container.Inventory.FindItem(i => CheckItem(i), recursive: false) != null)
                            {
                                IsCompleted = true;
                            }
                            else
                            {
                                Abandon = true;
                            }
                        }
                        RemoveSubObjective(ref getItemObjective);
                    });
            }
        }  
    }
}
