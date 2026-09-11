using Godot;

// ItemData je Resource (Godotí datový typ pro "data bez logiky").
// Výhoda: v editoru si vyrobíš .tres soubor pro každý předmět (meč, lektvar...)
// a pak ho jen přetáhneš do slotu - nemusíš nic psát v kódu.
[GlobalClass]
public partial class ItemData : Resource
{
    [Export] public string Id { get; set; } = "";        // unikátní ID předmětu (hodí se pro ukládání hry)
    [Export] public string ItemName { get; set; } = "";  // jméno zobrazené hráči
    [Export] public Texture2D Icon { get; set; }          // ikonka do UI slotu
    [Export] public bool Stackable { get; set; } = false; // true = víc kusů v jednom slotu (např. lektvary)
    [Export] public int MaxStack { get; set; } = 1;       // max. počet kusů na slot, pokud je Stackable
}
