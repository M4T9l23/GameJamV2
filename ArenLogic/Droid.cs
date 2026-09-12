using Godot;

// Companion droid. Jeden na celou hru - patří do main.tscn, ne do arény.
//
// Sám nic neblokuje. Když ArenaLogic zavolá ParkAt(), doletí na místo
// a nahlásí IsParked - aréna pak zapne svůj vlastní Entrance blocker,
// který ucpe vyznačený průchod. Droid je tedy jen vizuál a nosič baru.
//
// SETUP VE SCÉNĚ:
//   Droid (Node2D, tenhle skript)
//     +-- Sprite2D / AnimatedSprite2D
//     +-- ScanBar (ProgressBar, Min 0 / Max 100)
public partial class Droid : Node2D
{
	[ExportGroup("Následování")]
	[Export] public float FollowDistance = 90f;
	[Export] public float FollowSpeed = 260f;

	[ExportGroup("Parkování")]
	[Export] public float ParkSpeed = 400f;
	// Jak blízko parkovacímu bodu se počítá za "dorazil".
	[Export] public float ParkTolerance = 2f;

	[ExportGroup("Bar skenu")]
	[Export] public ProgressBar ScanBar;

	[ExportGroup("Otáčení")]
	// Node, který se otáčí. Nechej prázdné - najde si první AnimatedSprite2D
	// nebo Sprite2D mezi dětmi. Otáčí se jen tenhle node, ne celý droid,
	// aby se s ním neotáčel i ScanBar.
	[Export] public Node2D Visual;
	// O kolik je sprite pootočený oproti "doprava". Sprite mířící nahoru
	// potřebuje 90, mířící doleva 180.
	[Export] public float RotationOffset = 0f;
	// Místo otáčení jen překlápět doleva/doprava (pro sprity z boku).
	[Export] public bool FlipInsteadOfRotate = false;
	// Vyšší číslo = ostřejší zatáčení. Kolem 20 je to skoro okamžité.
	[Export] public float TurnSpeed = 10f;
	// Pod touhle rychlostí (px/s) se směr nepřepočítává, ať sprite
	// nepoškubává, když droid jen doťukává na místo.
	[Export] public float MinSpeedToTurn = 5f;

	private enum DroidState { Following, MovingToPark, Parked }

	private DroidState _state = DroidState.Following;
	private Vector2 _parkPoint;
	private Node2D _player;
	private Vector2 _facing = Vector2.Right;

	// ArenaLogic podle tohohle pozná, že už může zablokovat vchod.
	public bool IsParked => _state == DroidState.Parked;

	public override void _Ready()
	{
		AddToGroup("droid");

		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		AutoWireVisual();

		if (ScanBar != null)
		{
			ScanBar.MinValue = 0;
			ScanBar.MaxValue = 100;
			ScanBar.Value = 0;
			ScanBar.Hide();
		}
	}

	private void AutoWireVisual()
	{
		if (Visual != null)
			return;

		foreach (Node child in GetChildren())
		{
			if (child is AnimatedSprite2D or Sprite2D)
			{
				Visual = (Node2D)child;
				return;
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		// Směr bereme z toho, o kolik se droid opravdu posunul - platí to
		// pro následování i pro let do vchodu, bez duplikování logiky.
		Vector2 before = GlobalPosition;

		switch (_state)
		{
			case DroidState.Following:
				FollowPlayer(dt);
				break;

			case DroidState.MovingToPark:
				GlobalPosition = GlobalPosition.MoveToward(_parkPoint, ParkSpeed * dt);

				if (GlobalPosition.DistanceTo(_parkPoint) <= ParkTolerance)
				{
					GlobalPosition = _parkPoint;
					_state = DroidState.Parked;
				}
				break;

			case DroidState.Parked:
				// Stojí ve vchodu a skenuje.
				break;
		}

		UpdateFacing(GlobalPosition - before, dt);
	}

	// Otáčí jen Visual, ne celý droid - jinak by se s ním otáčel i ScanBar.
	// Když droid stojí, drží se poslední směr.
	private void UpdateFacing(Vector2 motion, float dt)
	{
		if (Visual == null || dt <= 0f)
			return;

		if (motion.Length() / dt >= MinSpeedToTurn)
			_facing = motion.Normalized();

		if (FlipInsteadOfRotate)
		{
			float x = Mathf.Abs(Visual.Scale.X);
			Visual.Scale = new Vector2(_facing.X < 0 ? -x : x, Visual.Scale.Y);
			return;
		}

		float target = _facing.Angle() + Mathf.DegToRad(RotationOffset);

		// Exponenciální doběh - nezávislý na framerate a nepřetáčí se.
		Visual.Rotation = Mathf.LerpAngle(Visual.Rotation, target, 1f - Mathf.Exp(-TurnSpeed * dt));
	}

	// --- volá ArenaLogic -------------------------------------------------

	public void ParkAt(Vector2 point)
	{
		_parkPoint = point;
		_state = DroidState.MovingToPark;

		if (ScanBar != null)
		{
			ScanBar.Value = 0;
			ScanBar.Show();
		}
	}

	public void Release()
	{
		_state = DroidState.Following;
		ScanBar?.Hide();
	}

	public void SetScanProgress(float percent)
	{
		if (ScanBar != null)
			ScanBar.Value = percent;
	}

	// --- vnitřní ---------------------------------------------------------

	private void FollowPlayer(float dt)
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (_player == null)
				return;
		}

		Vector2 target = _player.GlobalPosition;

		if (GlobalPosition.DistanceTo(target) <= FollowDistance)
			return;

		// Míříme na bod ve FollowDistance od Jane, ne přímo na ni, ať jí
		// neleze do zad.
		Vector2 stopPoint = target + target.DirectionTo(GlobalPosition) * FollowDistance;
		GlobalPosition = GlobalPosition.MoveToward(stopPoint, FollowSpeed * dt);
	}
}
