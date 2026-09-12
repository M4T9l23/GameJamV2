using Godot;

public partial class Inventory : Panel
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

	// Inventář přijme cokoliv, ale nic s tím nedělá - ikonu vrátí zpět
	// do původního slotu (viz _Notification níže).
	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.As<ItemDragPayload>() != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.As<ItemDragPayload>() is ItemDragPayload payload)
			payload.Icon.Show();
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
