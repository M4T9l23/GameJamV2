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

	[ExportGroup("Doruceni")]
	// Jak blizko k Jane doletí, než item pustí.
	[Export] public float DeliverDistance = 70f;
	[Export] public float DeliverSpeed = 420f;
	// Jak dlouho zůstane viset hláška nad droidem.
	[Export] public float MessageSeconds = 4f;
	// Nechej prázdné, vezme se res://GUI/Items/WorldItem.tscn.
	[Export] public PackedScene WorldItemScene;
	// Label na hlášky. Nechej prázdné, vyrobí se za běhu.
	[Export] public Label SpeechLabel;

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

	private enum DroidState { Following, MovingToPark, Parked, Delivering, Waiting }

	private DroidState _state = DroidState.Following;
	private Vector2 _parkPoint;
	private Node2D _player;
	private Vector2 _facing = Vector2.Right;
	private Item _pendingItem;
	private string _pendingMessage;
	private Vector2 _waitPoint;
	private bool _waiting;
	private float _messageTimer;

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
		SetupSpeech();

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

			case DroidState.Delivering:
				moveDir = MoveToDeliver(dt);
				break;

			case DroidState.Waiting:
				moveDir = MoveTowards(_waitPoint, FollowSpeed * dt, 4f);
				break;
		}

		TickMessage(dt);
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

	// Obecny posun k bodu. Vraci smer letu, nebo Zero kdyz uz je na miste.
	private Vector2 MoveTowards(Vector2 point, float step, float tolerance)
	{
		Vector2 delta = point - GlobalPosition;

		if (delta.Length() <= tolerance)
			return Vector2.Zero;

		GlobalPosition = GlobalPosition.MoveToward(point, step);
		return delta.Normalized();
	}

	// --- doruceni itemu --------------------------------------------------

	// Droid doleti k Jane, polozi item a rekne hlasku.
	public void DeliverItem(Item item, string message)
	{
		if (item == null)
			return;

		// Behem skenovani arenu neopousti - doruci az potom.
		if (_state == DroidState.MovingToPark || _state == DroidState.Parked)
		{
			GD.Print("Droid: doruceni odlozeno, zrovna drzi vchod.");
			return;
		}

		_pendingItem = item;
		_pendingMessage = message;

		// Kdyz ma cekat venku, nikam za Jane nelitame - to by cely wait
		// zrusilo. Item pustime tady. Jane prave preslapla caru, takze
		// stoji hned vedle.
		if (_waiting)
		{
			DropPendingItem();
			_state = DroidState.Waiting;
			return;
		}

		_state = DroidState.Delivering;
	}

	private Vector2 MoveToDeliver(float dt)
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
			if (_player == null)
			{
				_state = DroidState.Following;
				return Vector2.Zero;
			}
		}

		Vector2 dir = MoveTowards(_player.GlobalPosition, DeliverSpeed * dt, DeliverDistance);

		// Jeste nedoletel.
		if (dir != Vector2.Zero)
			return dir;

		DropPendingItem();
		_state = _waiting ? DroidState.Waiting : DroidState.Following;
		return Vector2.Zero;
	}

	private void DropPendingItem()
	{
		if (_pendingItem == null)
			return;

		PackedScene scene = WorldItemScene ?? GD.Load<PackedScene>("res://GUI/Items/WorldItem.tscn");

		if (scene == null)
		{
			GD.PushError("Droid: nepodarilo se nacist WorldItem.tscn");
			return;
		}

		var pickup = scene.Instantiate<WorldItem>();
		Vector2 where = GlobalPosition;
		Item dropped = _pendingItem;

		GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, pickup);

		Callable.From(() =>
		{
			if (!IsInstanceValid(pickup)) return;
			pickup.GlobalPosition = where;
			pickup.SetItem(dropped);
		}).CallDeferred();

		GD.Print($"Droid: predal '{_pendingItem.DisplayName}'.");

		if (!string.IsNullOrEmpty(_pendingMessage))
			Say(_pendingMessage);

		_pendingItem = null;
		_pendingMessage = null;
	}

	// --- hlasky ----------------------------------------------------------

	private void SetupSpeech()
	{
		if (SpeechLabel != null)
		{
			SpeechLabel.Hide();
			return;
		}

		SpeechLabel = new Label
		{
			Name = "SpeechLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			Size = new Vector2(220, 48),
			Position = new Vector2(-110, -110),
		};

		AddChild(SpeechLabel);
		SpeechLabel.Hide();
	}

	public void Say(string message)
	{
		if (string.IsNullOrEmpty(message))
		{
			GD.Print("Droid.Say: prazdna hlaska, nic se nezobrazi (vypln GiveMessage na LocationLine).");
			return;
		}

		if (SpeechLabel == null)
		{
			GD.PushError("Droid.Say: SpeechLabel je null.");
			return;
		}

		// Label s nulovou velikosti text orizne na nic. Kdyz si ho pridal
		// rucne a nenatahl, dorovnat na neco rozumneho.
		if (SpeechLabel.Size.X < 40f || SpeechLabel.Size.Y < 14f)
		{
			SpeechLabel.Size = new Vector2(220, 48);
			SpeechLabel.Position = new Vector2(-110, -110);
			GD.Print("Droid.Say: SpeechLabel mel nulovou velikost, dorovnano na 220x48.");
		}

		SpeechLabel.Text = message;
		SpeechLabel.Show();
		_messageTimer = MessageSeconds;

		GD.Print($"Droid rika: \"{message}\" na {MessageSeconds}s, " +
			$"label size {SpeechLabel.Size}, pozice {SpeechLabel.GlobalPosition}, visible {SpeechLabel.Visible}");
	}

	private void TickMessage(float dt)
	{
		if (_messageTimer <= 0f)
			return;

		_messageTimer -= dt;

		if (_messageTimer <= 0f)
			SpeechLabel?.Hide();
	}

	// --- cekaci zony -----------------------------------------------------

	// Jane vesla do lokace, kam droid nechodi. Zaparkuje a ceka.
	public void WaitAt(Vector2 point)
	{
		_waiting = true;
		_waitPoint = point;

		// Arenu ani doruceni neprerusujeme.
		if (_state == DroidState.Following)
			_state = DroidState.Waiting;
	}

	// Jane z te lokace vysla, droid se zase rozjede za ni.
	public void StopWaiting()
	{
		_waiting = false;

		if (_state == DroidState.Waiting)
			_state = DroidState.Following;
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
		_state = _waiting ? DroidState.Waiting : DroidState.Following;
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
