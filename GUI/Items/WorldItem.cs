using Godot;

public partial class WorldItem : Area2D
{
	private Sprite2D _sprite;
	private DragHandle _handle;
	private TextureRect _dragIcon;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");

		// Area2D._InputEvent neumí Godot drag&drop - potřebujeme Control.
		InputPickable = false;

		BuildDragHandle();
	}

	public void SetTexture(Texture2D texture)
	{
		if (_sprite == null)
			_sprite = GetNode<Sprite2D>("Sprite2D");

		_sprite.Texture = texture;

		if (_dragIcon != null)
			_dragIcon.Texture = texture;

		UpdateHandleSize();
	}

	private void BuildDragHandle()
	{
		_dragIcon = new TextureRect
		{
			Texture = _sprite.Texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false, // sprite kreslí item, tohle drží jen texturu pro drag
		};

		_handle = new DragHandle
		{
			Owner2D = this,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		_handle.AddChild(_dragIcon);
		AddChild(_handle);

		UpdateHandleSize();
	}

	private void UpdateHandleSize()
	{
		if (_handle == null || _sprite?.Texture == null)
			return;

		Vector2 size = _sprite.Texture.GetSize() * _sprite.Scale;

		_handle.Size = size;
		_handle.Position = -size / 2f; // sprite je centrovaný na originu
		_dragIcon.Size = size;
		_dragIcon.Position = Vector2.Zero;
	}

	// Volá DragHandle po skončení tažení (deferred).
	internal void OnDragFinished()
	{
		if (_dragIcon == null)
			return;

		_dragIcon.Visible = false;

		if (_dragIcon.Texture == null)
		{
			// Slot byl prázdný -> item si vzal inventář.
			QueueFree();
			return;
		}

		// Buď se drop nepovedl (textura zůstala stejná), nebo proběhla výměna
		// s obsazeným slotem a dostali jsme zpátky jeho starý item.
		_sprite.Texture = _dragIcon.Texture;
		_sprite.Show();
		UpdateHandleSize();
	}

	internal void OnDragStarted()
	{
		_sprite.Hide();
	}

	// Vnitřní Control, který obstarává samotné tažení.
	private partial class DragHandle : Control
	{
		public WorldItem Owner2D;

		private bool _dragging;

		public override Variant _GetDragData(Vector2 atPosition)
		{
			if (PickupMode.Instance == null || !PickupMode.Instance.Active)
				return default;

			if (Owner2D == null || Owner2D._dragIcon?.Texture == null)
				return default;

			var preview = new TextureRect
			{
				Texture = Owner2D._dragIcon.Texture,
				Size = Size,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Position = -Size / 2f,
				MouseFilter = MouseFilterEnum.Ignore,
			};

			var wrapper = new Control
			{
				MouseFilter = MouseFilterEnum.Ignore,
				Modulate = new Color(1f, 1f, 1f, 0.5f),
			};
			wrapper.AddChild(preview);
			SetDragPreview(wrapper);

			_dragging = true;
			Owner2D.OnDragStarted();

			return Owner2D._dragIcon;
		}

		public override void _Notification(int what)
		{
			if (what != NotificationDragEnd || !_dragging)
				return;

			_dragging = false;
			CallDeferred(nameof(FinishDrag));
		}

		private void FinishDrag()
		{
			if (Owner2D != null && IsInstanceValid(Owner2D))
				Owner2D.OnDragFinished();
		}
	}
}