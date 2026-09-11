using Godot;

public partial class Inventory : Panel
{
	private Variant _dataBk;

	public override void _Process(double delta)
	{
		if (Input.GetCurrentCursorShape() == Input.CursorShape.Forbidden)
		{
			DisplayServer.CursorSetShape(DisplayServer.CursorShape.Arrow);
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragBegin)
		{
			_dataBk = GetViewport().GuiGetDragData();
		}

		if (what == NotificationDragEnd)
		{
			if (!GetViewport().GuiIsDragSuccessful())
			{
				if (_dataBk.VariantType != Variant.Type.Nil)
				{
					if (_dataBk.AsGodotObject() is TextureRect icon)
					{
						Vector2 mousePos = GetGlobalMousePosition();

						if (GetGlobalRect().HasPoint(mousePos))
						{
							// Puštěno v rámci panelu inventáře, ale mimo platný slot -> zrušit tažení
							icon.Show();
						}
						else
						{
							// Puštěno mimo inventář -> item se objeví ve světě jako pickup
							SpawnPickup(icon.Texture);
							icon.Texture = null;
							icon.Show();
						}
					}
					_dataBk = default;
				}
			}
		}
	}

	private void SpawnPickup(Texture2D texture)
	{
		if (texture == null)
			return;

		var scene = GD.Load<PackedScene>("res://GUI/Items/WorldItem.tscn");
		var pickup = scene.Instantiate<WorldItem>();

		GetTree().CurrentScene.AddChild(pickup);
		pickup.SetTexture(texture);
		pickup.GlobalPosition = pickup.GetGlobalMousePosition();
	}
}
