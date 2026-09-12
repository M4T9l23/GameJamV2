using Godot;

// Companion droid. Jeden na celou hru - patří do main.tscn, ne do arény.
//
// Sám nic neblokuje. Když ArenaLogic zavolá ParkAt(), doletí na místo
// a nahlásí IsParked - aréna pak zapne svůj vlastní Entrance blocker,
// který ucpe vyznačený průchod. Droid je tedy jen vizuál a nosič baru.
//
// Je pevný - Jane ani nepřátelé jím neprojdou. Sám ale nekoliduje
// s ničím (maska 0), takže se nemůže zaseknout o zeď ani o Jane;
// prostě letí, kam má, a ostatní odstrčí.
//
// SETUP VE SCÉNĚ:
//   Droid (AnimatableBody2D, tenhle skript)
//     +-- Sprite2D / AnimatedSprite2D
//     +-- CollisionShape2D
//     +-- ScanBar (ProgressBar, Min 0 / Max 100)
public partial class Droid : AnimatableBody2D
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

	[ExportGroup("Kolize")]
	// Vrstva, na které je droid pevný. 3 = vrstvy 1 a 2, do kterých koukají
	// masky Jane i nepřátel.
	//
	// Vrstvu 3 sem radši nedávej - na tu se dívá raycast vyhýbání nepřátel
	// a protože droid pořád lítá vedle Jane, začali by místo přímého náběhu
	// kroužit kolem ní.
	[Export(PropertyHint.Layers2DPhysics)] public uint BodyLayer = 3;
	// Vypni, kdyby Jane u droida poskakovala. Odstrkávání pak bude míň
	// přesné, ale klidnější.
	[Export] public bool PhysicsSync = true;

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

		// Droid je pevný pro ostatní, ale sám neřeší nic - díky nulové masce
		// se nezasekne o geometrii a vždycky dorazí na parkovací bod.
		CollisionLayer = BodyLayer;
		CollisionMask = 0;

		// Posouváme ho ručně v _PhysicsProcess, tohle zajistí, že to fyzika
		// zaregistruje správně a Jane se v něm neutopí.
		SyncToPhysics = PhysicsSync;

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

		// Směr bereme z toho, KAM droid míří, ne z toho, o kolik se posunul.
		// Naměřený posun je u fyzikálního tělesa nespolehlivý - SyncToPhysics
		// přepisuje transform ze serveru, takže odečet pozic vyjde nula
		// a sprite by se neotáčel.
		Vector2 moveDir = Vector2.Zero;

		switch (_state)
		{
			case DroidState.Following:
				moveDir = FollowPlayer(dt);
				break;

			case DroidState.MovingToPark:
				moveDir = MoveToPark(dt);
				break;

			case DroidState.Parked:
				// Stojí ve vchodu a skenuje, drží si poslední směr.
				break;
		}

		UpdateFacing(moveDir, dt);
	}

	// Vrací směr letu, nebo Vector2.Zero když už je na místě.
	private Vector2 MoveToPark(float dt)
	{
		Vector2 toPark = _parkPoint - GlobalPosition;

		if (toPark.Length() <= ParkTolerance)
		{
			GlobalPosition = _parkPoint;
			_state = DroidState.Parked;
			return Vector2.Zero;
		}

		GlobalPosition = GlobalPosition.MoveToward(_parkPoint, ParkSpeed * dt);
		return toPark.Normalized();
	}

	// Otáčí jen Visual, ne celý droid - jinak by se s ním otáčel i ScanBar.
	// Když droid stojí, drží se poslední směr.
	private void UpdateFacing(Vector2 direction, float dt)
	{
		if (Visual == null || dt <= 0f)
			return;

		if (direction.LengthSquared() > 0.0001f)
			_facing = direction.Normalized();

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

	// Vrací směr letu, nebo Vector2.Zero když je dost blízko a stojí.
	private Vector2 FollowPlayer(float dt)
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (_player == null)
				return Vector2.Zero;
		}

		Vector2 target = _player.GlobalPosition;

		if (GlobalPosition.DistanceTo(target) <= FollowDistance)
			return Vector2.Zero;

		// Míříme na bod ve FollowDistance od Jane, ne přímo na ni, ať jí
		// neleze do zad.
		Vector2 stopPoint = target + target.DirectionTo(GlobalPosition) * FollowDistance;
		Vector2 toStop = stopPoint - GlobalPosition;

		GlobalPosition = GlobalPosition.MoveToward(stopPoint, FollowSpeed * dt);

		return toStop.LengthSquared() > 0.0001f ? toStop.Normalized() : Vector2.Zero;
	}
}
