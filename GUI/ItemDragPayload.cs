using Godot;

// Data přenášená během drag & drop mezi ItemSlot <-> ItemSlot a ItemSlot <-> WorldItem.
// Dřív se takhle posílala přímo TextureRect ikona - to ale neslo jen texturu,
// ne skutečná data itemu (Id, tagy), takže po jednom průchodu přes svět se
// item "rozpadl" na pouhý obrázek. Teď nese celý Item resource.
public partial class ItemDragPayload : RefCounted
{
	// Item, který se právě táhne (po zpracování dropu se přepíše na to, co se
	// má vrátit zpátky do zdroje - null pokud si ho zdroj "odevzdal" úplně).
	public Item Item;

	// Ikona zdroje - buď ItemSlot.Icon, nebo WorldItem._dragIcon.
	// Používá se pro preview a jako signál viditelnosti po skončení dragu.
	public TextureRect Icon;

	// Zdrojový slot, pokud item táhneme z inventáře. Null = zdroj je WorldItem
	// (item ve světě).
	public ItemSlot SourceSlot;
}
