using Godot;

public partial class WorldItem : Area2D
{
	// Nastav v Inspectoru u konkrétní instance WorldItem.tscn v levelu -
	// přetáhni sem .tres soubor s Item resourcem. Při _Ready se podle něj
	// item automaticky nastaví (texturu, Id i tagy).
	[Export] public Item InitialItem;

	private Sprite2D _sprite;
	private DragHandle _handle;
	private TextureRect _dragIcon;
	private Item _item;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite2D>("Sprite2D");

		// Area2D._InputEvent neumí Godot drag&drop - potřebujeme Control.
		InputPickable = false;

		BuildDragHandle();

		if (InitialItem != null)
			SetItem(InitialItem);
	}

	// Aby si ArenaLogic mohl overit, jestli uz odmena neco lezi ve svete.
	public Item GetItem() => _item;

	public void SetItem(Item item)
	{
		_item = item;

		if (_sprite == null)
			_sprite = GetNode<Sprite2D>("Sprite2D");

		_sprite.Texture = item?.Texture;

		if (_dragIcon != null)
			_dragIcon.Texture = item?.Texture;

		UpdateHandleSize();
	}

	// Zpětná kompatibilita pro ruční umístění pickupu bez plných dat itemu
	// (Id zůstane prázdné -> kontrola duplicit se pro něj přeskočí).
	public void SetTexture(Texture2D texture) => SetItem(new Item { Texture = texture });

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

	// Volá DragHandle po skončení tažení (deferred). payload.Item je zdroj
	// pravdy o tom, co (pokud něco) se má vrátit zpátky do světa - buď ho
	// nikdo nezměnil (drop se nepovedl -> item zůstává), nebo ho nastavil
	// ItemSlot/Inventory (výměna, nebo null = item byl odebraný do inventáře).
	internal void OnDragFinished(ItemDragPayload payload)
	{
		_dragIcon.Visible = false;

		Item resultItem = payload.Item;

		if (resultItem == null)
		{
			// Item si vzal inventář (nebo se ztratil) -> zmizet ze světa.
			QueueFree();
			return;
		}

		SetItem(resultItem);
		_sprite.Show();
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
		private ItemDragPayload _payload;

		public override Variant _GetDragData(Vector2 atPosition)
		{
			if (PickupMode.Instance == null || !PickupMode.Instance.Active)
				return default;

			if (Owner2D?._item == null || Owner2D._dragIcon?.Texture == null)
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

			_payload = new ItemDragPayload
			{
				Item = Owner2D._item,
				Icon = Owner2D._dragIcon,
				SourceSlot = null,
			};

			return _payload;
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
			if (Owner2D != null && IsInstanceValid(Owner2D) && _payload != null)
				Owner2D.OnDragFinished(_payload);

			_payload = null;
		}
	}
}
