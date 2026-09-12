using Godot;

// Autoload: Project Settings -> Autoload, název "PickupMode".
public partial class PickupMode : Node
{
	public static PickupMode Instance { get; private set; }

	private const string ClosedCursorPath = "res://Assets/Sprites/Hand_closed.png";
	private const string OpenCursorPath = "res://Assets/Sprites/Hand_open.png";

	private Texture2D _closedCursor;
	private Texture2D _openCursor;
	private Vector2 _closedHotspot;
	private Vector2 _openHotspot;

	private bool _dragging;

	public bool Active { get; private set; }

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		_closedCursor = GD.Load<Texture2D>(ClosedCursorPath);
		_openCursor = GD.Load<Texture2D>(OpenCursorPath);

		if (_closedCursor != null)
			_closedHotspot = _closedCursor.GetSize() / 2f;

		if (_openCursor != null)
			_openHotspot = _openCursor.GetSize() / 2f;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
			Instance = null;
	}

	// _Input, ne _UnhandledInput: Tab je bindnutý na ui_focus_next a Controly
	// ho spolknou dřív, než se dostane k unhandled input.
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Tab)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!Active)
			return;

		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed == _dragging)
				return; // ignoruj opakované/duplicitní eventy

			_dragging = mb.Pressed;
			ApplyCursor();
		}
	}

	private void Toggle()
	{
		SetActive(!Active);
	}

	public void SetActive(bool active)
	{
		if (Active == active)
			return;

		Active = active;
		_dragging = false;

		if (Active)
			ApplyCursor();
		else
			Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
	}

	private void ApplyCursor()
	{
		Texture2D tex = _dragging ? _closedCursor : _openCursor;
		Vector2 hotspot = _dragging ? _closedHotspot : _openHotspot;

		if (tex == null)
			return;

		Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, hotspot);
	}
	// Ladici vypis toho, nad cim je mys. Bezel kazdy frame a zaplavil
	// konzoli tak, ze v ni nebylo videt nic jineho. Zapnout jen kdyz je
	// potreba ladit drag & drop.
	[Export] public bool DebugHover = false;

	public override void _Process(double delta)
	{
		if (!Active || !DebugHover)
			return;

		Control hovered = GetViewport().GuiGetHoveredControl();
		string path = hovered == null ? "<none>" : hovered.GetPath().ToString();

		// Vypsat jen pri zmene, ne kazdy frame.
		if (path == _lastHovered)
			return;

		_lastHovered = path;
		GD.Print($"hovered: {path}");
	}

	private string _lastHovered = "";
}
