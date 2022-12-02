using System;
using System.Collections.Generic;

namespace Zoobop.InventorySystem
{
    public interface IInventory
    {
        public string Name { get; set; }
        
        public event Action<IInventory> OnInventoryChanged;
        public bool AddItem(in Item itemToAdd, int amount = 1);
        public bool AddItems(in IEnumerable<ItemSlot> items);
        public bool AddItems(in IInventory inventory);
        public bool RemoveItem(in Item itemToRemove, int amount = 1);
        public bool RemoveItems(in IEnumerable<ItemSlot> items);
        public bool RemoveItems(in IInventory inventory);
        public void Discard(in ItemSlot slot, int amount);
        public bool HasItem(in Item item, int amount = 1);
        public bool HasItemSlot(in ItemSlot itemSlot);
        public ItemSlot Transfer(in Item item);
        public IEnumerable<ItemSlot> TransferAll();
        public IList<ItemSlot> AsList();
        public IDictionary<Item, int> AsDictionary();
    }
}
