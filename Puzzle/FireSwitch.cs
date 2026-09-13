using Godot;

// Spínač, který se zapálí zásahem fireballu (E) a zhasne zásahem
// waterballu (Q). Dveře se pak dívají, jestli hoří.
//
// Volitelně po chvíli vyhasne sám - pak musí Jane zapálit víc spínačů
// dřív, než první dohoří.
//
// SETUP VE SCÉNĚ:
//   FireSwitch (StaticBody2D, tenhle skript)
//     +-- CollisionShape2D
//     +-- Sprite2D / ColorRect  (vizuál, obarvuje se přes Modulate)
//
// Collision layer nech na 1, jinak do něj střela netrefí.
public partial class FireSwitch : StaticBody2D
{
	[ExportGroup("Stav")]
	// Hoří už od začátku?
	[Export] public bool StartsLit = false;
	// Po kolika sekundách vyhasne sám. 0 = hoří napořád.
	[Export] public float BurnSeconds = 0f;
	// Může ho waterball zhasnout?
	[Export] public bool WaterExtinguishes = true;

	[ExportGroup("Vzhled")]
	// Vidět jen když hoří - typicky AnimatedSprite2D s plamenem.
	[Export] public CanvasItem LitVisual;
	// Vidět jen když nehoří. Nechej prázdné, když má jáma zůstat pořád.
	[Export] public CanvasItem UnlitVisual;
	// Zůstává vidět vždycky - samotná jáma. Obarvuje se podle stavu.
	[Export] public CanvasItem Visual;
	[Export] public Color LitColor = new Color(1f, 1f, 1f);
	[Export] public Color UnlitColor = new Color(0.75f, 0.75f, 0.8f);

	[Signal] public delegate void StateChangedEventHandler(bool lit);

	public bool IsLit { get; private set; }

	private float _burnLeft;

	public override void _Ready()
	{
		AddToGroup("fire_switch");

		IsLit = StartsLit;
		_burnLeft = IsLit ? BurnSeconds : 0f;

		Repaint();
	}

	public override void _Process(double delta)
	{
		if (!IsLit || BurnSeconds <= 0f)
			return;

		_burnLeft -= (float)delta;

		if (_burnLeft <= 0f)
		{
			GD.Print($"FireSwitch '{Name}': dohorel.");
			SetLit(false);
		}
	}

	// --- volá Attack1 -----------------------------------------------------

	public void Ignite()
	{
		_burnLeft = BurnSeconds;

		if (IsLit)
			return;   // uz hori, jen se obnovil odpocet

		GD.Print($"FireSwitch '{Name}': ZAPALEN.");
		SetLit(true);
	}

	public void Douse()
	{
		if (!WaterExtinguishes || !IsLit)
			return;

		GD.Print($"FireSwitch '{Name}': uhasen.");
		SetLit(false);
	}

	// --- vnitřní ----------------------------------------------------------

	private void SetLit(bool lit)
	{
		IsLit = lit;
		Repaint();
		EmitSignal(SignalName.StateChanged, lit);
	}

	private void Repaint()
	{
		if (LitVisual != null)
			LitVisual.Visible = IsLit;

		if (UnlitVisual != null)
			UnlitVisual.Visible = !IsLit;

		if (Visual != null)
			Visual.Modulate = IsLit ? LitColor : UnlitColor;
	}
}
