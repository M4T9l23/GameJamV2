using Godot;

public partial class WorldItem : Area2D
{
	private Sprite2D _sprite;
	private bool _isDragging = false;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");
		InputPickable = true;
		InputEvent += OnInputEvent;
		AddToGroup("world_items");
	}

	// Nouzové vyčištění stavu, kdyby item zůstal "zaseknutý" uprostřed tažení
	// (např. kvůli chybějící/pozdní DragEnd notifikaci). Volá se při zapnutí PickupMode.
	public void ResetState()
	{
		_isDragging = false;
		InputPickable = true;
		if (_sprite != null)
			_sprite.Show();
	}

	public Texture2D GetTexture()
	{
		return _sprite.Texture;
	}

	public void SetTexture(Texture2D texture)
	{
		if (_sprite == null)
			_sprite = GetNode<Sprite2D>("Sprite2D");

		_sprite.Texture = texture;
	}

	private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
	{
		if (_isDragging || !PickupMode.Instance.Active || GetViewport().GuiIsDragging())
			return;

		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			StartDrag();
		}
	}

	private void StartDrag()
	{
		if (_sprite.Texture == null || Inventory.Instance == null)
			return;

		_isDragging = true;
		InputPickable = false; // dokud táhneme, nejde to chytit znovu
		_sprite.Hide();

		var previewIcon = new TextureRect
		{
			Texture = _sprite.Texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(96, 96),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = -new Vector2(48, 48),
			Modulate = new Color(1f, 1f, 1f, 0.7f)
		};

		var previewWrapper = new Control
		{
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		previewWrapper.AddChild(previewIcon);

		Inventory.Instance.ForceDrag(this, previewWrapper);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd && _isDragging)
		{
			_isDragging = false;

			if (!GetViewport().GuiIsDragSuccessful() && IsInstanceValid(this) && _sprite != null)
			{
				// Drag se nepovedl (puštěno mimo prázdný slot) -> item zůstává na místě
				_sprite.Show();
				InputPickable = true;
			}
			// Pokud byl drag úspěšný, tento uzel je stejně zničen v ItemSlot._DropData,
			// takže není potřeba nic vracet zpět.
		}
	}
}
