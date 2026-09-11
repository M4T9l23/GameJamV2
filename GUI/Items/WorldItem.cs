using Godot;

public partial class WorldItem : Area2D
{
	private Sprite2D _sprite;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		InputPickable = true;
		InputEvent += OnInputEvent;
	}

	public void SetTexture(Texture2D texture)
	{
		if (_sprite == null)
			_sprite = GetNode<Sprite2D>("Sprite2D");

		_sprite.Texture = texture;
	}

	private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (!PickupMode.Instance.Active)
			return;

		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			TryPickUp();
		}
	}

	private void TryPickUp()
	{
		foreach (Node node in GetTree().GetNodesInGroup("item_slots"))
		{
			if (node is ItemSlot slot && slot.IsEmpty())
			{
				slot.SetTexture(_sprite.Texture);
				QueueFree();
				return;
			}
		}

		// Inventář je plný – item zůstává ležet ve světě
		GD.Print("Inventář je plný, item nelze sebrat.");
	}
}
