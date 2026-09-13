using Godot;

// Neviditelná čára, která při překročení přepíše Location ve StatsPanelu
// (ten text vpravo v inventáři, typu "E-404").
//
// Ve hře není vidět - CollisionShape2D se kreslí jen v editoru.
//
// DVA REŽIMY:
//
// 1) ČÁRA (vyplníš LocationAhead i LocationBehind)
//    Pozná, ze které strany Jane přišla a kterou stranou odešla. Přepíše
//    se jen když opravdu projde skrz, ne když se otře o kraj a vrátí se.
//
// 2) ZÓNA (LocationBehind necháš prázdné)
//    Přepíše Location hned při vstupu. Hodí se na místnost, do které se
//    dá přijít z víc stran.
//
// SETUP:
//   LocationLine (Area2D, tenhle skript)
//     +-- CollisionShape2D (RectangleShape2D, úzký obdélník přes průchod)
//
// U režimu čáry natoč celý node tak, aby průchod vedl napříč osou, kterou
// máš nastavenou v CrossAxis.
public partial class LocationLine : Area2D
{
	public enum Axis { Y, X }

	[ExportGroup("Nazvy")]
	// Kam se Jane dostane, když projde na kladnou stranu osy.
	[Export] public string LocationAhead = "";
	// Kam se dostane při průchodu opačným směrem. Prázdné = režim zóny.
	[Export] public string LocationBehind = "";

	[ExportGroup("Predani itemu")]
	// Když Jane vstoupí do téhle lokace, droid k ní doletí a item jí
	// položí na zem. Nechej prázdné, pokud se nic předávat nemá.
	[Export] public Item GiveItem;
	[Export(PropertyHint.MultilineText)] public string GiveMessage = "";
	// Předat jen jednou za běh hry.
	[Export] public bool GiveOnce = true;

	[ExportGroup("Chovani")]
	// Která lokální osa určuje "před" a "za". U vodorovné čáry nech Y,
	// u svislé přepni na X.
	[Export] public Axis CrossAxis = Axis.Y;

	private float _entrySide;
	private bool _given;

	public override void _Ready()
	{
		Monitoring = true;
		// Hráč je na vrstvě 2, bereme 1+2+3 pro jistotu.
		CollisionMask = 7;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		// Režim zóny - žádné řešení stran, rovnou přepsat.
		if (string.IsNullOrEmpty(LocationBehind))
		{
			Apply(LocationAhead);
			return;
		}

		_entrySide = SideOf(body);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player") || string.IsNullOrEmpty(LocationBehind))
			return;

		float exitSide = SideOf(body);

		// Vyšla stejnou stranou, kterou vešla - neprošla, nic neměníme.
		if (exitSide == 0f || exitSide == _entrySide)
			return;

		Apply(exitSide > 0f ? LocationAhead : LocationBehind);
	}

	// Na které straně čáry Jane je, v lokálním prostoru tohohle nodu.
	private float SideOf(Node2D body)
	{
		Vector2 local = ToLocal(body.GlobalPosition);
		float value = CrossAxis == Axis.Y ? local.Y : local.X;

		return Mathf.Sign(value);
	}

	private void Apply(string location)
	{
		if (string.IsNullOrEmpty(location))
			return;

		if (GetTree().GetFirstNodeInGroup("stats_panel") is not StatsPanel panel)
		{
			GD.Print($"LocationLine '{Name}': StatsPanel nenalezen (grupa 'stats_panel').");
			return;
		}

		if (panel.Location == location)
			return;

		panel.Location = location;
		GD.Print($"Location: {location}");

		TryGiveItem();
	}

	// Posle droida, aby Jane donesl item. Neopakuje se, kdyz uz ho ma.
	private void TryGiveItem()
	{
		if (GiveItem == null || (GiveOnce && _given))
			return;

		if (GetTree().GetFirstNodeInGroup("droid") is not Droid droid)
		{
			GD.Print($"LocationLine '{Name}': droid nenalezen (grupa 'droid').");
			return;
		}

		if (PlayerHasItem(GiveItem))
			return;

		_given = true;
		droid.DeliverItem(GiveItem, GiveMessage);
	}

	private bool PlayerHasItem(Item item)
	{
		if (GetTree().GetFirstNodeInGroup("inventory") is not Inventory inv)
			return false;

		foreach (ItemSlot slot in inv.GetSlots())
		{
			Item held = slot.GetItem();
			if (held != null && held.Id == item.Id)
				return true;
		}

		return false;
	}
}
