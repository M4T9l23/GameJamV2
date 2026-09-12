using Godot;
using System.Collections.Generic;

// POZOR: kořenový node je teď PanelContainer (ne Panel).
// PanelContainer se sám roztáhne podle obsahu, takže sloty už nikdy
// nemůžou "vylézt" ven z rámečku - ani v editoru, ani ve hře.
//
// Tenhle skript UŽ NEŘEŠÍ LAYOUT. Velikost slotů, mezery, počet sloupců
// a odsazení se nastavují normálně v editoru:
//   - velikost slotu + ikona ....... GUI/ItemSlot.tscn (Custom Minimum Size)
//   - počet sloupců + mezery ....... GridContainer (Columns / Theme Overrides)
//   - vnitřní odsazení panelu ...... MarginContainer (Theme Overrides -> Constants)
//   - rámeček, barva, rohy ......... Inventory -> Theme Overrides -> Styles -> panel
public partial class Inventory : PanelContainer
{
	private const string WorldItemScenePath = "res://GUI/Items/WorldItem.tscn";

	private ItemDragPayload _draggedPayload;

	public override void _Ready()
	{
		// Panel inventáře musí umět přijmout drop, jinak Godot ukazuje
		// kurzor "Forbidden" a NotificationDragEnd chodí s divným stavem.
		MouseFilter = MouseFilterEnum.Stop;
		FocusMode = FocusModeEnum.None;
	}

	// --- veřejné API pro zbytek hry -------------------------------------

	private GridContainer GetGrid() =>
		GetNodeOrNull<GridContainer>("MarginContainer/HBoxContainer/GridContainer");

	// Sloty v pořadí, v jakém jsou ve scéně (prvních 5 = equipment).
	public IEnumerable<ItemSlot> GetSlots()
	{
		GridContainer grid = GetGrid();
		if (grid == null)
			yield break;

		foreach (Node child in grid.GetChildren())
		{
			if (child is ItemSlot slot)
				yield return slot;
		}
	}

	// Vloží item do prvního volného slotu. Vrací false, když je plno
	// nebo už stejný item (podle Id) v inventáři je.
	public bool TryAddItem(Item item)
	{
		if (item == null)
			return false;

		if (!string.IsNullOrEmpty(item.Id))
		{
			foreach (ItemSlot slot in GetSlots())
			{
				Item existing = slot.GetItem();
				if (existing != null && existing.Id == item.Id)
					return false;
			}
		}

		foreach (ItemSlot slot in GetSlots())
		{
			if (slot.IsEmpty())
			{
				slot.SetItem(item);
				return true;
			}
		}

		return false;
	}

	// --- drag & drop ----------------------------------------------------

	// Inventář přijme cokoliv, ale nic s tím nedělá - ikonu vrátí zpět
	// do původního slotu (viz _Notification níže).
	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.As<ItemDragPayload>() != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.As<ItemDragPayload>() is ItemDragPayload payload)
			payload.Icon?.Show();
	}

	// POZOR: NotificationDragBegin/End jsou v C# bindings typu long,
	// takže je nelze použít jako case labely u switch(int what).
	public override void _Notification(int what)
	{
		if (what == NotificationDragBegin)
		{
			Variant data = GetViewport().GuiGetDragData();
			_draggedPayload = data.As<ItemDragPayload>();
			return;
		}

		if (what == NotificationDragEnd)
		{
			ItemDragPayload payload = _draggedPayload;
			_draggedPayload = null;

			if (payload == null || !IsInstanceValid(payload.Icon))
				return;

			if (GetViewport().GuiIsDragSuccessful())
				return;

			// Nesahat na nody uprostřed rušení dragu - odložit o jeden frame.
			CallDeferred(nameof(HandleFailedDrop), payload);
		}
	}

	private void HandleFailedDrop(ItemDragPayload payload)
	{
		if (payload == null || !IsInstanceValid(payload.Icon))
			return;

		Vector2 mousePos = GetGlobalMousePosition();

		if (GetGlobalRect().HasPoint(mousePos))
		{
			// Puštěno v rámci panelu inventáře, ale mimo platný slot (nebo by
			// vznikl duplikát) -> zrušit tažení, nic se neděje.
			payload.Icon.Show();
			return;
		}

		// Puštěno mimo inventář -> item se objeví ve světě jako pickup.
		Item item = payload.Item;

		if (payload.SourceSlot != null)
		{
			// Zdroj byl slot v inventáři - vyprázdnit ho (a odebrat případné
			// equipment efekty).
			payload.SourceSlot.SetItem(null);
		}
		else
		{
			// Zdroj byl WorldItem - dát mu vědět, že o item přišel.
			payload.Item = null;
			payload.Icon.Texture = null;
			payload.Icon.Show();
		}

		SpawnPickup(item);
	}

	private void SpawnPickup(Item item)
	{
		if (item == null)
			return;

		var scene = GD.Load<PackedScene>(WorldItemScenePath);
		if (scene == null)
		{
			GD.PushError($"Inventory: nepodarilo se nacist {WorldItemScenePath}");
			return;
		}

		Node parent = GetTree().CurrentScene;
		if (parent == null)
			return;

		var pickup = scene.Instantiate<WorldItem>();
		parent.AddChild(pickup);
		pickup.SetItem(item);

		// Pozice myši ve světě (ne v GUI vrstvě).
		pickup.GlobalPosition = pickup.GetGlobalMousePosition();
	}
}
