using Godot;

public partial class ItemSlot : Panel
{
	private TextureRect _icon;

	public override void _Ready()
	{
		_icon = GetNode<TextureRect>("Icon");
		AddToGroup("item_slots");
	}

	public bool IsEmpty()
	{
		return _icon.Texture == null;
	}

	public void SetTexture(Texture2D texture)
	{
		_icon.Texture = texture;
		_icon.Show();
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (PickupMode.Instance == null || !PickupMode.Instance.Active)
			return default;

		if (_icon.Texture == null)
			return default;

		var preview = (Control)Duplicate();
		var c = new Control();
		c.AddChild(preview);
		preview.Position -= new Vector2(25, 25);
		preview.SelfModulate = Colors.Transparent;
		c.Modulate = new Color(c.Modulate, 0.5f);

		SetDragPreview(c);
		_icon.Hide();
		return _icon;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (PickupMode.Instance == null || !PickupMode.Instance.Active)
			return false;

		if (data.AsGodotObject() is WorldItem)
			return IsEmpty();

		return true;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.AsGodotObject() is WorldItem worldItem)
		{
			SetTexture(worldItem.GetTexture());
			worldItem.QueueFree();
			return;
		}

		var otherIcon = data.As<TextureRect>();
		var tmp = _icon.Texture;
		_icon.Texture = otherIcon.Texture;
		otherIcon.Texture = tmp;
		otherIcon.Show();
	}
}
