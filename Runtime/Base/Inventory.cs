using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zoobop.InventorySystem
{
    public class Inventory : MonoBehaviour, IInventory
    {
        [Header("Properties")]
        [SerializeField] protected string _name = "Inventory";
        [SerializeField] protected List<ItemSlot> _items = new();

        protected IDictionary<Item, int> _itemsDictionary = new Dictionary<Item, int>();

        public string Name
        {
            get => _name;
            set => _name = value;
        }
        
        public event Action<IInventory> OnInventoryChanged = delegate { };

        #region UnityEvents

        protected virtual void Awake()
        {
            PopulateItemsDictionary();
        }

        protected virtual void OnValidate()
        {
            // Return if null
            if (_items is null) return;

            // Save old items
            var old = _items;
            _items = new List<ItemSlot>(old);

            // Populate dictionary
            PopulateItemsDictionary();
        }

        private void PopulateItemsDictionary()
        {
            // Reflect list data onto dictionary and total weight
            _itemsDictionary = new Dictionary<Item, int>();
            var validItems = _items.Where(slot => slot.Item is not null);
            foreach (var (item, amount) in validItems)
            {
                if (_itemsDictionary.ContainsKey(item))
                {
                    _itemsDictionary[item] += amount;
                    continue;
                }
                
                _itemsDictionary.Add(item, amount);
            }
        }

        #endregion

        #region InventoryUtility

        private void TryAddStackItem(Item item, int amount)
        {
            // Check stackability
            if (item.IsStackable)
            {
                // Find the first stackable item instance
                var itemIndex = _items.FindIndex(slot => slot.Amount < item.MaxStack && ReferenceEquals(slot.Item, item));
                if (itemIndex != -1)
                {
                    // Calculate total amount
                    var currentAmount = _items[itemIndex].Amount;
                    var calculatedAmount = currentAmount + amount;

                    // No stack overflow
                    if (calculatedAmount <= item.MaxStack)
                    {
                        _items[itemIndex].Amount += amount;
                        _itemsDictionary[item] += amount;
                        return;
                    }

                    // Overflow
                    var difference = item.MaxStack - currentAmount;
                    _items[itemIndex].Amount += difference;
                    _itemsDictionary[item] += difference;

                    TryAddStackItem(item, amount - difference);
                    return;
                }
            }

            // Non-stackable item
            _itemsDictionary[item] += amount;
            _items.Add(new ItemSlot(item, amount));
        }

        private void TryRemoveStackItem(Item item, int amount)
        {
            // Check stackability
            if (item.IsStackable)
            {
                // Find the first stackable item instance
                var itemIndex = _items.FindIndex(slot => ReferenceEquals(slot.Item, item) && slot.Amount > 0);
                if (itemIndex != -1)
                {
                    // Calculate total amount
                    var currentAmount = _items[itemIndex].Amount;
                    var calculatedAmount = currentAmount - amount;

                    // No stack overflow
                    if (calculatedAmount >= 0)
                    {
                        _items[itemIndex].Amount -= amount;
                        _itemsDictionary[item] -= amount;
                        return;
                    }

                    // Overflow
                    var difference = Mathf.Abs(calculatedAmount);
                    _items.RemoveAt(itemIndex);
                    _itemsDictionary[item] -= currentAmount;

                    TryRemoveStackItem(item, difference);
                    return;
                }
            }

            // Non-stackable item
            _itemsDictionary[item] -= amount;
            var index = _items.FindIndex(slot => slot.Item == item);
            _items.RemoveAt(index);
        }

        #endregion

        #region IInventory

        private bool AddItemSingle(in Item itemToAdd, int amount)
        {
            // Check if not null
            if (itemToAdd == null) return false;

            // Initialize item if new
            if (!_itemsDictionary.ContainsKey(itemToAdd))
            {
                _itemsDictionary.Add(itemToAdd, 0);
            }

            // Handle stacked item
            TryAddStackItem(itemToAdd, amount);
            return true;
        }

        public virtual bool AddItem(in Item itemToAdd, int amount = 1)
        {
            var result = AddItemSingle(itemToAdd, amount);
            // Invoke event
            OnInventoryChanged?.Invoke(this);
            return result;
        }

        public bool AddItems(in IEnumerable<ItemSlot> items)
        {
            var result = true;
            foreach (var (item, amount) in items)
            {
                // If item didn't get added, return false
                if (!AddItemSingle(item, amount))
                {
                    result = false;
                }
            }

            // Invoke event
            OnInventoryChanged?.Invoke(this);
            return result;
        }

        public bool AddItems(in IInventory inventory)
        {
            return AddItems(inventory.AsList());
        }

        private bool RemoveItemSingle(in Item itemToRemove, int amount)
        {
            // Check if not null
            if (itemToRemove == null) return false;

            // Handle stacked item
            TryRemoveStackItem(itemToRemove, amount);

            // Check item validity
            if (_itemsDictionary.ContainsKey(itemToRemove) && _itemsDictionary[itemToRemove] <= 0)
            {
                _itemsDictionary.Remove(itemToRemove);
            }
            
            return true;
        }

        public virtual bool RemoveItem(in Item itemToRemove, int amount = 1)
        {
            var result = RemoveItemSingle(itemToRemove, amount);
            // Invoke event
            OnInventoryChanged?.Invoke(this);
            return result;
        }

        public bool RemoveItems(in IEnumerable<ItemSlot> items)
        {
            var result = true;
            foreach (var (item, amount) in items)
            {
                // If item didn't get removed, return false
                if (!RemoveItemSingle(item, amount))
                {
                    result = false;
                }
            }

            // Invoke event
            OnInventoryChanged?.Invoke(this);
            return result;
        }

        public bool RemoveItems(in IInventory inventory)
        {
            return RemoveItems(inventory.AsList());
        }

        public void Discard(in ItemSlot slot, int amount)
        {
            // Find item slot
            var index = _items.IndexOf(slot);
            _items[index].Amount -= amount;

            // Remove slot if amount is 0
            if (_items[index].Amount <= 0)
            {
                _items.RemoveAt(index);
                _itemsDictionary[slot.Item] -= amount;

                // Remove from dictionary if item is no longer in inventory
                if (_itemsDictionary[slot.Item] <= 0)
                {
                    _itemsDictionary.Remove(slot.Item);
                }
            }

            // Invoke event
            OnInventoryChanged?.Invoke(this);
        }

        public bool HasItem(in Item item, int amount = 1)
        {
            return _itemsDictionary.ContainsKey(item) && _itemsDictionary[item] >= amount;
        }

        public bool HasItemSlot(in ItemSlot itemSlot)
        {
            return _items.Contains(itemSlot);
        }

        public ItemSlot Transfer(in Item item)
        {
            if (HasItem(item))
            {
                var itemToCheck = item;
                var itemSlot = _items.Find(slot => slot.Item == itemToCheck);
                _items.Remove(itemSlot);

                // Update dictionary
                var difference = _itemsDictionary[itemSlot.Item] - itemSlot.Amount;
                if (difference == 0)
                {
                    _itemsDictionary.Remove(itemSlot.Item);
                }

                // Invoke event
                OnInventoryChanged?.Invoke(this);
                return itemSlot;
            }

            return null;
        }

        public IEnumerable<ItemSlot> TransferAll()
        {
            // Store items
            var items = _items.ToList();

            // Clear items
            _items.Clear();
            _itemsDictionary.Clear();

            // Invoke event
            OnInventoryChanged?.Invoke(this);
            return items;
        }

        public IList<ItemSlot> AsList()
        {
            return _items;
        }

        public IDictionary<Item, int> AsDictionary()
        {
            return _itemsDictionary;
        }

        #endregion
    }
}
