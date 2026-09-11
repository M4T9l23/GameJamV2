// Obyčejná (ne-Godot) datová třída - reprezentuje obsah jednoho slotu inventáře.
// Nedědí z Node ani Resource, protože ji nepotřebujeme ukládat do scény,
// jen ji držíme v paměti uvnitř InventoryManageru.
public class InventorySlotData
{
    public ItemData Item;   // jaký předmět je ve slotu (null = prázdný slot)
    public int Quantity;    // počet kusů daného předmětu
    public bool IsTracked;  // true u prvních 5 slotů (0-4), které chceš sledovat i jinde ve hře

    public bool IsEmpty => Item == null || Quantity <= 0;
}
