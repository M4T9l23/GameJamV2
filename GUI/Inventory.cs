using Godot;

public partial class Inventory : Panel
{
	private const string WorldItemScenePath = "res://GUI/Items/WorldItem.tscn";

	private TextureRect _draggedIcon;

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
		return data.VariantType != Variant.Type.Nil && data.As<TextureRect>() != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.As<TextureRect>() is TextureRect icon)
			icon.Show();
	}

	// POZOR: NotificationDragBegin/End jsou v C# bindings typu long,
	// takže je nelze použít jako case labely u switch(int what).
	public override void _Notification(int what)
	{
		if (what == NotificationDragBegin)
		{
			Variant data = GetViewport().GuiGetDragData();
			_draggedIcon = data.VariantType != Variant.Type.Nil
				? data.As<TextureRect>()
				: null;
			return;
		}

		if (what == NotificationDragEnd)
		{
			TextureRect icon = _draggedIcon;
			_draggedIcon = null;

			if (icon == null || !IsInstanceValid(icon))
				return;

			if (GetViewport().GuiIsDragSuccessful())
				return;

			// Nesahat na nody uprostřed rušení dragu - odložit o jeden frame.
			CallDeferred(nameof(HandleFailedDrop), icon);
		}
	}

	private void HandleFailedDrop(TextureRect icon)
	{
		if (icon == null || !IsInstanceValid(icon))
			return;

		Vector2 mousePos = GetGlobalMousePosition();

		if (GetGlobalRect().HasPoint(mousePos))
		{
			// Puštěno v rámci panelu inventáře, ale mimo platný slot -> zrušit tažení.
			icon.Show();
			return;
		}

		// Puštěno mimo inventář -> item se objeví ve světě jako pickup.
		Texture2D texture = icon.Texture;
		icon.Texture = null;
		icon.Show();

		SpawnPickup(texture);
	}

	private void SpawnPickup(Texture2D texture)
	{
		if (texture == null)
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
		pickup.SetTexture(texture);

		// Pozice myši ve světě (ne v GUI vrstvě).
		pickup.GlobalPosition = pickup.GetGlobalMousePosition();
	}
}