using Godot;

public partial class ItemSlot : Panel
{
	// Nastav na true u prvních 5 slotů v Inventory.tscn - jejich itemy pak
	// přímo mění hráče přes PlayerEquipmentBonuses (podle Item.Tags).
	[Export] public bool IsEquipmentSlot = false;

	private TextureRect _icon;
	private Item _item;

	public override void _Ready()
	{
		_icon = GetNodeOrNull<TextureRect>("Icon");
		if (_icon == null)
		{
			GD.PushError($"ItemSlot '{Name}': chybi potomek 'Icon' (TextureRect).");
			return;
		}

		// Ikona nesmi krast mys slotu.
		_icon.MouseFilter = MouseFilterEnum.Ignore;

		// Slot musi prijimat mys, jinak nejde drag & drop.
		MouseFilter = MouseFilterEnum.Stop;

		// Tab by jinak skákal po focusu a nedostal se do PickupMode.
		FocusMode = FocusModeEnum.None;

		AddToGroup("item_slots");
	}

	public bool IsEmpty() => _item == null;

	public Item GetItem() => _item;

	public Texture2D GetTexture() => _icon?.Texture;

	public void SetItem(Item item)
	{
		// Equipment efekty: nejdřív dole odebrat starý item, teprve pak přidat nový.
		if (IsEquipmentSlot && _item != null)
			PlayerEquipmentBonuses.Instance?.RemoveItem(_item);

		_item = item;

		if (_icon != null)
		{
			_icon.Texture = item?.Texture;
			_icon.Show();
		}

		if (IsEquipmentSlot && _item != null)
			PlayerEquipmentBonuses.Instance?.ApplyItem(_item);
	}

	public void Clear() => SetItem(null);

	public override Variant _GetDragData(Vector2 atPosition)
	{
		// Bez pickup módu (Tab) se s itemy nedá hýbat.
		if (PickupMode.Instance == null || !PickupMode.Instance.Active)
			return default;

		if (_item == null || _icon == null)
			return default;

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

		return new ItemDragPayload
		{
			Item = _item,
			Icon = _icon,
			SourceSlot = this,
		};
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		if (data.As<ItemDragPayload>() is not ItemDragPayload payload)
			return false;

		// Puštěno zpátky na stejný slot - vždy OK, nic se nemění.
		if (payload.SourceSlot == this)
			return true;

		return !WouldCreateDuplicate(payload);
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.As<ItemDragPayload>() is not ItemDragPayload payload)
			return;

		if (payload.SourceSlot == this)
		{
			// Puštěno na stejný slot - jen vrátit viditelnost.
			_icon?.Show();
			return;
		}

		Item incoming = payload.Item;
		Item outgoing = _item;

		SetItem(incoming);

		if (payload.SourceSlot != null)
		{
			// Zdroj je jiný slot - výměna (outgoing může být null, to je OK).
			payload.SourceSlot.SetItem(outgoing);
		}
		else
		{
			// Zdroj je WorldItem ve světě - řekneme mu přes payload, co (pokud
			// něco) se má vrátit zpátky. WorldItem si to přečte deferred
			// v OnDragFinished.
			payload.Item = outgoing;
			payload.Icon.Texture = outgoing?.Texture;
		}

		payload.Icon?.Show();
	}

	// Projde všechny sloty v inventáři (equipment i normální) a zjistí, jestli
	// by přijetí tohoto payloadu vytvořilo duplicitní Id. Zdrojový slot (odkud
	// item táhneme) a tento cílový slot jsou z kontroly vyloučené - ty se
	// řeší přímo výměnou v _DropData.
	private bool WouldCreateDuplicate(ItemDragPayload payload)
	{
		if (payload.Item == null || string.IsNullOrEmpty(payload.Item.Id))
			return false;

		foreach (Node node in GetTree().GetNodesInGroup("item_slots"))
		{
			if (node is not ItemSlot slot || slot == this || slot == payload.SourceSlot)
				continue;

			if (slot._item != null && slot._item.Id == payload.Item.Id)
				return true;
		}

		return false;
	}
}
