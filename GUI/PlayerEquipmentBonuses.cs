using Godot;
using System.Collections.Generic;

// Autoload: Project Settings -> Autoload, název "PlayerEquipmentBonuses".
//
// Drží sečtené bonusy ze všech itemů, které jsou aktuálně v equipment slotech
// inventáře (prvních 5 slotů). ItemSlot volá ApplyItem/RemoveItem při vložení
// nebo odebrání itemu z equipment slotu; Player.cs se pak dotazuje GetBonus().
public partial class PlayerEquipmentBonuses : Node
{
	public static PlayerEquipmentBonuses Instance { get; private set; }

	[Signal]
	public delegate void BonusesChangedEventHandler();

	private readonly Dictionary<string, float> _bonuses = new();

	public override void _Ready()
	{
		Instance = this;
		GD.Print("PlayerEquipmentBonuses: autoload je aktivni.");
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	// Vrátí sečtenou hodnotu daného tagu, např. GetBonus("speed").
	// Klíč není case-sensitive (ItemTagUtils ho normalizuje na lowercase).
	public float GetBonus(string tagKey)
	{
		if (string.IsNullOrEmpty(tagKey))
			return 0f;

		return _bonuses.TryGetValue(tagKey.ToLowerInvariant(), out float value) ? value : 0f;
	}

	public void ApplyItem(Item item) => ChangeItem(item, 1f);

	public void RemoveItem(Item item) => ChangeItem(item, -1f);

	private void ChangeItem(Item item, float sign)
	{
		if (item?.Tags == null || item.Tags.Length == 0)
		{
			GD.Print($"PlayerEquipmentBonuses: item '{item?.DisplayName}' nema zadne tagy.");
			return;
		}

		foreach (string rawTag in item.Tags)
		{
			if (!ItemTagUtils.TryParse(rawTag, out string key, out float value))
				continue;

			_bonuses.TryGetValue(key, out float current);
			_bonuses[key] = current + value * sign;
			GD.Print($"PlayerEquipmentBonuses: '{key}' = {_bonuses[key]} (zmena {value * sign:+0.##;-0.##})");
		}

		EmitSignal(SignalName.BonusesChanged);
	}
}
