using Godot;
using System;

// InventoryManager bude Autoload (singleton) - viz návod níže.
// Díky tomu ho zavoláš odkudkoliv v projektu přes GetNode<InventoryManager>("/root/InventoryManager").
public partial class InventoryManager : Node
{
    public const int TotalSlots = 7;
    public const int TrackedSlots = 5; // sloty 0-4 = "sledované" (např. výbava postavy), 5-6 = obyčejné

    private InventorySlotData[] _slots = new InventorySlotData[TotalSlots];

    // Vyšle se při JAKÉKOLIV změně libovolného slotu - UI se na to napojí a překreslí se.
    [Signal] public delegate void SlotChangedEventHandler(int slotIndex);

    // Vyšle se jen když se změní jeden z prvních 5 sledovaných slotů.
    // Ostatní systémy hry (statistiky, combat, questy...) se můžou napojit jen na tohle
    // a nemusí procházet celý inventář.
    [Signal] public delegate void TrackedSlotChangedEventHandler(int slotIndex, ItemData item, int quantity);

    public override void _Ready()
    {
        for (int i = 0; i < TotalSlots; i++)
        {
            _slots[i] = new InventorySlotData
            {
                IsTracked = i < TrackedSlots // první 4 (0-4) budou tracked, zbytek (5-6) ne
            };
        }
    }

    public InventorySlotData GetSlot(int index) => _slots[index];

    // Nastaví obsah konkrétního slotu (přepíše, co v něm je) a rozešle signály.
    public void SetSlot(int index, ItemData item, int quantity)
    {
        if (index < 0 || index >= TotalSlots) return;

        _slots[index].Item = item;
        _slots[index].Quantity = quantity;

        EmitSignal(SignalName.SlotChanged, index);

        if (_slots[index].IsTracked)
            EmitSignal(SignalName.TrackedSlotChanged, index, item, quantity);
    }

    public void ClearSlot(int index) => SetSlot(index, null, 0);

    // Zkusí přidat předmět - nejdřív doplní existující stack, pak hledá první volný slot.
    // Vrací true, pokud se předmět povedlo umístit celý.
    public bool AddItem(ItemData item, int quantity = 1)
    {
        if (item.Stackable)
        {
            for (int i = 0; i < TotalSlots; i++)
            {
                if (_slots[i].Item == item && _slots[i].Quantity < item.MaxStack)
                {
                    int space = item.MaxStack - _slots[i].Quantity;
                    int toAdd = Math.Min(space, quantity);
                    SetSlot(i, item, _slots[i].Quantity + toAdd);
                    quantity -= toAdd;
                    if (quantity <= 0) return true;
                }
            }
        }

        for (int i = 0; i < TotalSlots; i++)
        {
            if (_slots[i].IsEmpty)
            {
                SetSlot(i, item, quantity);
                return true;
            }
        }

        return false; // inventář plný
    }
}
