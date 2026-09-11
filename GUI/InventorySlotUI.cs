using Godot;

// Tenhle skript dáš na Button node - jeden pro každý ze 7 slotů v UI.
// Button používáme kvůli vestavěné podpoře kliknutí, hoveru a fokusu.
public partial class InventorySlotUI : Button
{
    [Export] public int SlotIndex;        // v editoru nastavíš 0 až 6 - který slot v manageru tohle reprezentuje
    [Export] public TextureRect IconRect; // odkaz na TextureRect uvnitř tlačítka (ikonka předmětu)
    [Export] public Label QuantityLabel;  // odkaz na Label uvnitř tlačítka (počet kusů)

    private InventoryManager _manager;

    public override void _Ready()
    {
        _manager = GetNode<InventoryManager>("/root/InventoryManager");

        // napojíme se na signál - kdykoliv se změní JAKÝKOLIV slot, tahle metoda se zavolá
        _manager.SlotChanged += OnSlotChanged;

        // zobrazíme aktuální stav hned na startu
        Refresh(_manager.GetSlot(SlotIndex));

        // klik na tlačítko -> zavolá se OnPressed (dá se propojit i přes editor v záložce Node -> Signals)
        Pressed += OnPressed;
    }

    private void OnSlotChanged(int changedIndex)
    {
        if (changedIndex != SlotIndex) return; // zajímá nás jen náš vlastní slot
        Refresh(_manager.GetSlot(SlotIndex));
    }

    private void Refresh(InventorySlotData slot)
    {
        if (slot.IsEmpty)
        {
            IconRect.Texture = null;
            QuantityLabel.Text = "";
        }
        else
        {
            IconRect.Texture = slot.Item.Icon;
            // počet zobrazíme jen když je víc než 1 kus
            QuantityLabel.Text = slot.Quantity > 1 ? slot.Quantity.ToString() : "";
        }
    }

    private void OnPressed()
    {
        GD.Print($"Kliknuto na slot {SlotIndex}");
        // sem doplníš vlastní logiku: použít předmět, otevřít menu, drag&drop apod.
    }
}
