using Godot;

public partial class ItemSlot : Panel
{
	private TextureRect _icon;

	public override void _Ready()
	{
		_icon = GetNode<TextureRect>("Icon");

		// Ikona nesmí krást myš slotu.
		_icon.MouseFilter = MouseFilterEnum.Ignore;

		// Tab by jinak skákal po focusu a nedostal se do PickupMode.
		FocusMode = FocusModeEnum.None;

		AddToGroup("item_slots");
	}

	public bool IsEmpty()
	{
		return _icon.Texture == null;
	}

	public Texture2D GetTexture()
	{
		return _icon.Texture;
	}

	public void SetTexture(Texture2D texture)
	{
		_icon.Texture = texture;
		_icon.Show();
	}

	public void Clear()
	{
		_icon.Texture = null;
		_icon.Show();
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (_icon == null || _icon.Texture == null)
			return default;

		// POZOR: nepoužívat Duplicate() - duplikát by měl taky ItemSlot skript,
		// zaregistroval by se do skupiny "item_slots" a blokoval by myš.
		var preview = new TextureRect
		{
			Texture = _icon.Texture,
			CustomMinimumSize = _icon.Size,
			Size = _icon.Size,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Position = -_icon.Size / 2f,
			MouseFilter = MouseFilterEnum.Ignore,
		};

		var wrapper = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1f, 1f, 1f, 0.5f),
		};
		wrapper.AddChild(preview);

		SetDragPreview(wrapper);
		_icon.Hide();

		return _icon;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType != Variant.Type.Nil && data.As<TextureRect>() != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.As<TextureRect>() is not TextureRect otherIcon)
			return;

		if (otherIcon == _icon)
		{
			// Puštěno na stejný slot - jen vrátit viditelnost.
			_icon.Show();
			return;
		}

		Texture2D tmp = _icon.Texture;
		_icon.Texture = otherIcon.Texture;
		otherIcon.Texture = tmp;

		_icon.Show();
		otherIcon.Show();
	}
}