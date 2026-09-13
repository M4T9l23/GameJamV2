using Godot;

// Neviditelná čára nebo zóna, která při překročení přepíše Location ve
// StatsPanelu (ten text v inventáři, typu "E-404").
//
// Kromě názvu lokace umí dvě věci navíc: nechat droida donést item
// a zakázat mu do té lokace chodit.
//
// Ve hře není vidět - CollisionShape2D se kreslí jen v editoru.
//
// DVA REŽIMY:
//
// 1) ČÁRA (vyplníš LocationAhead i LocationBehind)
//    Pozná, ze které strany Jane přišla a kterou odešla. Každá strana má
//    vlastní název i vlastní nastavení droida.
//
// 2) ZÓNA (LocationBehind necháš prázdné)
//    Sepne při vstupu, rozepne při odchodu. Platí nastavení "Ahead".
//
// SETUP:
//   LocationLine (Area2D, tenhle skript)
//     +-- CollisionShape2D
//     +-- WaitPoint (Marker2D, volitelně - kam si droid stoupne)
public partial class LocationLine : Area2D
{
	public enum Axis { Y, X }

	[ExportGroup("Nazvy")]
	[Export] public string LocationAhead = "";
	// Prázdné = režim zóny.
	[Export] public string LocationBehind = "";

	[ExportGroup("Droid - lokace Ahead")]
	// Do téhle lokace droid za Jane nepůjde. Počká na WaitPointAhead.
	[Export] public bool DroidWaitsAhead = false;
	// Kam si stoupne. Nechej prázdné a hledá se potomek "WaitPoint",
	// jinak se použije pozice téhle čáry.
	[Export] public Marker2D WaitPointAhead;
	[Export(PropertyHint.MultilineText)] public string WaitMessageAhead = "";

	[ExportGroup("Droid - lokace Behind")]
	[Export] public bool DroidWaitsBehind = false;
	[Export] public Marker2D WaitPointBehind;
	[Export(PropertyHint.MultilineText)] public string WaitMessageBehind = "";

	[ExportGroup("Predani itemu")]
	// Droid k Jane doletí a item jí položí na zem. Prázdné = nic.
	[Export] public Item GiveItem;
	[Export(PropertyHint.MultilineText)] public string GiveMessage = "";
	[Export] public bool GiveOnce = true;

	[ExportGroup("Jednorazovy spawn")]
	// Nepřátelé, kteří se objeví při vstupu do lokace. Prázdné = nic.
	[Export] public Godot.Collections.Array<PackedScene> SpawnEnemies = new();
	// Kde se objeví. Nechej prázdné a vezmou se potomci "Spawn1", "Spawn2"...
	[Export] public Godot.Collections.Array<Marker2D> SpawnPoints = new();
	// Kolik jich celkem přijde. 0 nebo míň = jeden na každý spawn point.
	[Export] public int SpawnCount = 0;
	// Spustit jen jednou za běh hry.
	[Export] public bool SpawnOnce = true;
	// Spawnovat jen při vstupu do lokace Ahead, ne při návratu zpátky.
	[Export] public bool SpawnOnAheadOnly = true;

	[ExportGroup("Chovani")]
	// Která lokální osa dělí prostor na "před" a "za". U vodorovné čáry
	// nech Y, u svislé přepni na X. V režimu zóny se neuplatní.
	[Export] public Axis CrossAxis = Axis.Y;

	private float _entrySide;
	private bool _given;
	private bool _spawned;

	private bool ZoneMode => string.IsNullOrEmpty(LocationBehind);

	public override void _Ready()
	{
		WaitPointAhead ??= GetNodeOrNull<Marker2D>("WaitPoint");

		if (SpawnPoints.Count == 0)
		{
			foreach (Node child in GetChildren())
			{
				if (child is Marker2D marker && marker.Name.ToString().StartsWith("Spawn"))
					SpawnPoints.Add(marker);
			}
		}

		Monitoring = true;
		CollisionMask = 7;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (ZoneMode)
			Defer(true);
		else
			_entrySide = SideOf(body);
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (ZoneMode)
		{
			// Odchod ze zóny droida zase rozjede. Název lokace neměníme,
			// ten přepíše až další čára.
			if (DroidWaitsAhead)
				Callable.From(() => GetDroid()?.StopWaiting()).CallDeferred();

			return;
		}

		float exitSide = SideOf(body);

		// Vyšla stejnou stranou, kterou vešla - neprošla, nic neměníme.
		if (exitSide == 0f || exitSide == _entrySide)
			return;

		Defer(exitSide > 0f);
	}

	// Signály z fyziky běží uprostřed vyhodnocování kolizí. Přidávat v tu
	// chvíli do scény nody s Area2D (nepřátelé, WorldItem) Godot nedovolí
	// a hlásí "Can't change this state while flushing queries".
	// Proto všechno odložíme o frame.
	private void Defer(bool ahead)
	{
		Callable.From(() => EnterLocation(ahead)).CallDeferred();
	}

	// Jane se ocitla v jedné z lokací téhle čáry.
	private void EnterLocation(bool ahead)
	{
		string location = ahead ? LocationAhead : LocationBehind;
		bool waits = ahead ? DroidWaitsAhead : DroidWaitsBehind;
		Marker2D point = ahead ? WaitPointAhead : WaitPointBehind;
		string waitMessage = ahead ? WaitMessageAhead : WaitMessageBehind;

		SetLocationText(location);
		ApplyDroidRule(waits, point, waitMessage);
		TryGiveItem();
		TrySpawnEnemies(ahead);
	}

	// Jednorázová várka nepřátel do místnosti.
	private void TrySpawnEnemies(bool ahead)
	{
		if (SpawnEnemies.Count == 0 || SpawnPoints.Count == 0)
			return;

		if (SpawnOnce && _spawned)
			return;

		if (SpawnOnAheadOnly && !ahead)
			return;

		_spawned = true;

		int count = SpawnCount > 0 ? SpawnCount : SpawnPoints.Count;
		Node parent = GetTree().CurrentScene ?? (Node)this;

		for (int i = 0; i < count; i++)
		{
			PackedScene scene = SpawnEnemies[(int)(GD.Randi() % (uint)SpawnEnemies.Count)];

			// Pri jednom na marker jdeme poporade, jinak nahodne.
			Marker2D point = SpawnCount > 0
				? SpawnPoints[(int)(GD.Randi() % (uint)SpawnPoints.Count)]
				: SpawnPoints[i % SpawnPoints.Count];

			if (scene == null || point == null)
				continue;

			var enemy = scene.Instantiate<Node2D>();
			parent.AddChild(enemy);

			// Jen pozice - scale si nepratele nesou z vlastni sceny.
			enemy.GlobalPosition = point.GlobalPosition;
		}

		GD.Print($"LocationLine '{Name}': naspawnovano {count} nepratel.");
	}

	private void SetLocationText(string location)
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
	}

	// Buď droida zastavíme, nebo ho zase pustíme za Jane.
	private void ApplyDroidRule(bool waits, Marker2D point, string message)
	{
		Droid droid = GetDroid();
		if (droid == null)
			return;

		if (!waits)
		{
			droid.StopWaiting();
			return;
		}

		droid.WaitAt(point?.GlobalPosition ?? GlobalPosition);

		if (!string.IsNullOrEmpty(message))
			droid.Say(message);

		GD.Print($"LocationLine '{Name}': droid ceka venku.");
	}

	// Pošle droida, aby Jane donesl item. Neopakuje se, když ho už má.
	private void TryGiveItem()
	{
		if (GiveItem == null || (GiveOnce && _given))
			return;

		Droid droid = GetDroid();
		if (droid == null)
			return;

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

	private Droid GetDroid()
	{
		if (GetTree().GetFirstNodeInGroup("droid") is Droid droid)
			return droid;

		GD.Print($"LocationLine '{Name}': droid nenalezen (grupa 'droid').");
		return null;
	}

	// Na které straně čáry Jane je, v lokálním prostoru tohohle nodu.
	private float SideOf(Node2D body)
	{
		Vector2 local = ToLocal(body.GlobalPosition);
		float value = CrossAxis == Axis.Y ? local.Y : local.X;

		return Mathf.Sign(value);
	}
}
