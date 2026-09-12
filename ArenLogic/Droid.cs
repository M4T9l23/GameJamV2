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

	private enum DroidState { Following, MovingToPark, Parked }

	private DroidState _state = DroidState.Following;
	private Vector2 _parkPoint;
	private Node2D _player;

	// ArenaLogic podle tohohle pozná, že už může zablokovat vchod.
	public bool IsParked => _state == DroidState.Parked;

	public override void _Ready()
	{
		AddToGroup("droid");

		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (ScanBar != null)
		{
			ScanBar.MinValue = 0;
			ScanBar.MaxValue = 100;
			ScanBar.Value = 0;
			ScanBar.Hide();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

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
