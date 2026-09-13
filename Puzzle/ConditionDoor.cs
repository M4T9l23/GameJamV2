using Godot;

// Dveře, které se otevřou, když hoří jejich spínače. Když spínač vyhasne,
// zase se zavřou - pokud nezapneš StayOpen.
//
// SETUP VE SCÉNĚ:
//   ConditionDoor (StaticBody2D, tenhle skript)
//     +-- CollisionShape2D  (přes celý průchod)
//     +-- Sprite2D / ColorRect  (vizuál)
//
// Collision layer nech na 7, stejnou jako Wall.tscn - tak do dveří narazí
// Jane i nepřátelé a zároveň se jim vyhýbají raycastem.
public partial class ConditionDoor : StaticBody2D
{
	[ExportGroup("Podminka")]
	// Spínače, na kterých dveře závisí. Přetáhni je sem z scény.
	[Export] public Godot.Collections.Array<FireSwitch> Switches = new();
	// Musí hořet všechny, nebo stačí jeden?
	[Export] public bool RequireAll = true;
	// Otevřít, když spínače NEhoří (past, nebo dveře zavírané ohněm).
	[Export] public bool Invert = false;
	// Jednou otevřené zůstanou otevřené, i když spínač vyhasne.
	[Export] public bool StayOpen = false;

	[ExportGroup("Vzhled")]
	// Co se schová, když jsou dveře otevřené. Nechej prázdné a schová se
	// celý node i s kolizí.
	[Export] public CanvasItem Visual;

	[Signal] public delegate void OpenedEventHandler();

	public bool IsOpen { get; private set; }

	private CollisionShape2D _shape;
	private bool _everOpened;

	public override void _Ready()
	{
		AddToGroup("condition_door");

		foreach (Node child in GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				_shape = shape;
				break;
			}
		}

		if (_shape == null)
			GD.PushError($"ConditionDoor '{Name}': chybi potomek CollisionShape2D.");

		if (Switches.Count == 0)
			GD.PushWarning($"ConditionDoor '{Name}': nema prirazeny zadny spinac, zustanou zavrene.");

		// Zavřené na začátku, pokud podmínka neříká jinak.
		Apply(Evaluate(), force: true);
	}

	public override void _Process(double delta)
	{
		if (StayOpen && _everOpened)
			return;

		bool open = Evaluate();

		if (open != IsOpen)
			Apply(open, force: false);
	}

	// Splňují spínače podmínku?
	private bool Evaluate()
	{
		if (Switches.Count == 0)
			return Invert;

		bool all = true;
		bool any = false;

		foreach (FireSwitch sw in Switches)
		{
			if (sw == null || !IsInstanceValid(sw))
				continue;

			if (sw.IsLit) any = true;
			else all = false;
		}

		bool result = RequireAll ? all : any;

		return Invert ? !result : result;
	}

	private void Apply(bool open, bool force)
	{
		IsOpen = open;

		if (open)
			_everOpened = true;

		// Kolize se musi menit odlozene - fyzika ji uprostred vyhodnocovani
		// prepsat nedovoli.
		_shape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, open);

		if (Visual != null)
			Visual.Visible = !open;
		else
			Visible = !open;

		if (!force)
			GD.Print($"ConditionDoor '{Name}': {(open ? "otevreny" : "zavreny")}.");

		if (open)
			EmitSignal(SignalName.Opened);
	}
}
