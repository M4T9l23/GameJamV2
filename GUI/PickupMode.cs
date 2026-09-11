using Godot;

// Přidej tento skript do Project Settings -> Autoload jako singleton
// s názvem "PickupMode" (přesný název je důležitý kvůli PickupMode.Instance).
public partial class PickupMode : Node
{
	public static PickupMode Instance { get; private set; }

	// Soubory nahraj do res://GUI/Cursors/ pod přesně těmito názvy.
	// Pokud je dáš jinam, uprav cesty tady:
	private const string ClosedCursorPath = "res://Assets/Sprites/Hand_closed.png";
	private const string OpenCursorPath = "res://Assets/Sprites/Hand_open.png";

	private Texture2D _closedCursor;
	private Texture2D _openCursor;
	private Vector2 _closedHotspot;
	private Vector2 _openHotspot;

	public bool Active { get; private set; } = false;

	public override void _Ready()
	{
		Instance = this;
		_closedCursor = GD.Load<Texture2D>(ClosedCursorPath);
		_openCursor = GD.Load<Texture2D>(OpenCursorPath);

		_closedHotspot = _closedCursor.GetSize() / 2f;
		_openHotspot = _openCursor.GetSize() / 2f;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Tab)
		{
			Toggle();
		}

		if (Active && @event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed)
				Input.SetCustomMouseCursor(_closedCursor, Input.CursorShape.Arrow, _closedHotspot);
			else
				Input.SetCustomMouseCursor(_openCursor, Input.CursorShape.Arrow, _openHotspot);
		}
	}

	private void Toggle()
	{
		Active = !Active;
		if (Active)
			Input.SetCustomMouseCursor(_openCursor, Input.CursorShape.Arrow, _openHotspot);
		else
			Input.SetCustomMouseCursor(null, Input.CursorShape.Arrow);
	}
}
