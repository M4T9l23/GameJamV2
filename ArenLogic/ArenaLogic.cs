using Godot;
using System.Collections.Generic;

// Jedna aréna. Když do ní Jane vejde, droid zaparkuje ve vstupu a začne
// skenovat. Aréna mezitím průběžně spawnuje nepřátele. Bar skenu roste,
// když je aréna prázdná, a klesá úměrně počtu nepřátel uvnitř. Na 100 %
// droid uvolní vstup a hodí na zem genom pro tuhle oblast.
//
// SETUP VE SCÉNĚ:
//   ArenaLogic (Node2D)
//     +-- Region (Area2D + CollisionShape2D přes celou místnost)
//     +-- DroidPark (Marker2D)   ... vstup, kam si stoupne droid
//     +-- Respawn (Marker2D)     ... kam se Jane respawne po smrti
//     +-- Exit (Marker2D)        ... kam Jane skončí, když arénu opustí
//     +-- RewardPoint (Marker2D) ... kam spadne genom
//     +-- Border (Node2D + ArenaBorder) ... svítící obrys, volitelný
//     +-- Spawn1..N (Marker2D)   ... spawn pointy nepřátel
//
// Exit MUSÍ ležet mimo Region, jinak se aréna hned znovu aktivuje.
public partial class ArenaLogic : Node2D
{
	public enum ArenaState { Idle, Scanning, Done }

	[ExportGroup("Odměna")]
	[Export] public Genome Reward;
	[Export] public Marker2D RewardPoint;
	[Export] public PackedScene WorldItemScene;
	// Když už má Jane maximální stage tohohle genomu, defaultně nepadne nic.
	[Export] public bool GiveRewardWhenMaxed = false;

	[ExportGroup("Prostor")]
	[Export] public Area2D Region;
	// Vyznačený průchod. StaticBody2D s CollisionShape2D přes celý vchod -
	// jakmile v něm droid zaparkuje, shape se zapne a neprojde tudy Jane
	// ani nepřátelé. Droid si sem zároveň stoupne, pokud není nastavený
	// DroidParkPoint.
	[Export] public StaticBody2D Entrance;
	[Export] public Marker2D DroidParkPoint;
	[Export] public Marker2D RespawnPoint;
	[Export] public Marker2D ExitPoint;
	// Svítící obrys arény. Nechej prázdné - najde se Node2D "Border".
	[Export] public ArenaBorder Border;

	[ExportGroup("Spawn")]
	[Export] public Godot.Collections.Array<PackedScene> EnemyScenes = new();
	[Export] public Godot.Collections.Array<Marker2D> SpawnPoints = new();
	[Export] public float SpawnInterval = 2.5f;
	[Export] public int MaxAlive = 8;

	[ExportGroup("Sken")]
	[Export] public float ScanRate = 8f;          // %/s v prázdné aréně
	[Export] public float DrainPerEnemy = 4f;     // %/s za každého nepřítele uvnitř

	[ExportGroup("Respawn")]
	// -1 = plné HP. Necháno jako export na doladění balancu.
	[Export] public int RespawnHealth = -1;

	[ExportGroup("Ostatní")]
	// Globální spawner v main.tscn - na dobu boje se vypne, ať do arény
	// nesype nepřátele navíc.
	[Export] public EnemySpawner GlobalSpawner;

	public ArenaState CurrentState { get; private set; } = ArenaState.Idle;
	public float ScanProgress { get; private set; }

	private float _spawnTimer;
	private Droid _droid;
	private CollisionShape2D _entranceShape;
	private bool _entranceBlocked;

	// Nepřátelé, které tahle aréna spawnla. Používá se jen na úklid - počítání
	// do skenu jede přes Area2D, aby se chytli i ti, co se dokodrcali odjinud.
	private readonly List<Node2D> _spawned = new();

	public override void _Ready()
	{
		AutoWire();

		if (Region == null)
		{
			GD.PushError($"ArenaLogic '{Name}': neni prirazeny Region (Area2D).");
			SetPhysicsProcess(false);
			return;
		}

		// Musíme vidět hráče (vrstva 2) i nepřátele (vrstvy 1 a 2).
		Region.Monitoring = true;
		Region.CollisionMask = 7;

		SetupEntrance();
	}

	private void SetupEntrance()
	{
		if (Entrance == null)
			return;

		foreach (Node child in Entrance.GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				_entranceShape = shape;
				break;
			}
		}

		if (_entranceShape == null)
		{
			GD.PushError($"ArenaLogic '{Name}': Entrance nema potomka CollisionShape2D.");
			return;
		}

		// Vrstva 7 = stejná jako Wall.tscn, takže do toho narazí Jane
		// i nepřátelé a zároveň se tomu nepřátelé vyhýbají raycastem.
		Entrance.CollisionLayer = 7;

		SetEntranceBlocked(false);
	}

	// Co není vyplněné v inspektoru, se dohledá mezi dětmi podle jména.
	// Díky tomu stačí nakopírovat Arena.tscn a jen posunout markery.
	private void AutoWire()
	{
		Region ??= GetNodeOrNull<Area2D>("Region");
		Entrance ??= GetNodeOrNull<StaticBody2D>("Entrance");
		DroidParkPoint ??= GetNodeOrNull<Marker2D>("DroidPark");
		RespawnPoint ??= GetNodeOrNull<Marker2D>("Respawn");
		ExitPoint ??= GetNodeOrNull<Marker2D>("Exit");
		RewardPoint ??= GetNodeOrNull<Marker2D>("RewardPoint");
		Border ??= GetNodeOrNull<ArenaBorder>("Border");

		if (SpawnPoints.Count == 0)
		{
			foreach (Node child in GetChildren())
			{
				if (child is Marker2D marker && marker.Name.ToString().StartsWith("Spawn"))
					SpawnPoints.Add(marker);
			}
		}

		if (GlobalSpawner == null && GetTree().CurrentScene != null)
		{
			foreach (Node node in GetTree().CurrentScene.GetChildren())
			{
				if (node is EnemySpawner spawner)
				{
					GlobalSpawner = spawner;
					break;
				}
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Region == null)
			return;

		float dt = (float)delta;

		Godot.Collections.Array<Node2D> bodies = Region.GetOverlappingBodies();
		int enemyCount = 0;
		bool playerInside = false;

		foreach (Node2D body in bodies)
		{
			if (body.IsInGroup("enemies") && !body.IsQueuedForDeletion())
				enemyCount++;
			else if (body.IsInGroup("player"))
				playerInside = true;
		}

		switch (CurrentState)
		{
			case ArenaState.Idle:
				if (playerInside)
					Activate();
				break;

			case ArenaState.Scanning:
				TickEntrance();
				TickScan(dt, enemyCount);
				TickSpawn(dt, enemyCount);
				break;
		}
	}

	// --- aktivace a dokončení -------------------------------------------

	private void Activate()
	{
		CurrentState = ArenaState.Scanning;
		ScanProgress = 0f;
		_spawnTimer = 0f;
		_entranceBlocked = false;

		// Player.Die() si podle téhle grupy najde, ve které aréně zemřel.
		AddToGroup("active_arena");

		GlobalSpawner?.SetPhysicsProcess(false);

		Border?.SetActive(true);

		Droid droid = GetDroid();
		Vector2? park = DroidParkPoint?.GlobalPosition ?? Entrance?.GlobalPosition;

		if (droid != null && park.HasValue)
			droid.ParkAt(park.Value);

		GD.Print($"Arena '{Name}': sken zahajen.");
	}

	private void TickScan(float dt, int enemyCount)
	{
		// Obrys pulzuje rychleji, když sken padá dolů.
		Border?.SetAlert(enemyCount > 0);

		float change = enemyCount == 0
			? ScanRate
			: -DrainPerEnemy * enemyCount;

		ScanProgress = Mathf.Clamp(ScanProgress + change * dt, 0f, 100f);

		GetDroid()?.SetScanProgress(ScanProgress);

		if (ScanProgress >= 100f)
			Complete();
	}

	private void Complete()
	{
		CurrentState = ArenaState.Done;
		RemoveFromGroup("active_arena");

		SetEntranceBlocked(false);
		Border?.FlashComplete();

		// Spawn se zastaví, ale nepřátelé, co zbyli, zůstanou - jinak to
		// vypadá, jako bys je vypnul vypínačem.
		GlobalSpawner?.SetPhysicsProcess(true);

		Droid droid = GetDroid();
		droid?.Release();

		DropReward();

		GD.Print($"Arena '{Name}': sken dokoncen.");
	}

	// --- vchod -----------------------------------------------------------

	// Zapne blokaci, jakmile droid dorazí na místo. Čeká ale, dokud v
	// průchodu někdo stojí - jinak by se Jane nebo nepřítel zasekli uvnitř
	// statického tělesa.
	private void TickEntrance()
	{
		if (_entranceBlocked || _entranceShape == null)
			return;

		Droid droid = GetDroid();
		if (droid == null || !droid.IsParked)
			return;

		if (!IsEntranceClear())
			return;

		SetEntranceBlocked(true);
	}

	private void SetEntranceBlocked(bool blocked)
	{
		_entranceBlocked = blocked;

		_entranceShape?.SetDeferred(CollisionShape2D.PropertyName.Disabled, !blocked);
	}

	// Stojí někdo v průchodu? Ptáme se fyziky tvarem samotného vchodu,
	// takže to sedí přesně na to, co je nakreslené ve scéně.
	private bool IsEntranceClear()
	{
		if (_entranceShape?.Shape == null)
			return true;

		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = _entranceShape.Shape,
			Transform = _entranceShape.GlobalTransform,
			CollisionMask = 3,          // hráč (vrstva 2) i nepřátelé (1 a 2)
			CollideWithBodies = true,
			CollideWithAreas = false,
			Exclude = new Godot.Collections.Array<Rid> { Entrance.GetRid() },
		};

		foreach (Godot.Collections.Dictionary hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 16))
		{
			if (hit["collider"].As<GodotObject>() is Node node
				&& (node.IsInGroup("player") || node.IsInGroup("enemies")))
				return false;
		}

		return true;
	}

	// --- spawnování ------------------------------------------------------

	private void TickSpawn(float dt, int enemyCount)
	{
		if (EnemyScenes.Count == 0 || SpawnPoints.Count == 0)
			return;

		_spawnTimer -= dt;
		if (_spawnTimer > 0f)
			return;

		_spawnTimer = SpawnInterval;

		if (enemyCount >= MaxAlive)
			return;

		PackedScene scene = EnemyScenes[(int)(GD.Randi() % (uint)EnemyScenes.Count)];
		Marker2D point = SpawnPoints[(int)(GD.Randi() % (uint)SpawnPoints.Count)];

		if (scene == null || point == null)
			return;

		var enemy = scene.Instantiate<Node2D>();
		AddChild(enemy);
		enemy.GlobalPosition = point.GlobalPosition;

		_spawned.RemoveAll(n => !IsInstanceValid(n) || n.IsQueuedForDeletion());
		_spawned.Add(enemy);
	}

	// --- odměna ----------------------------------------------------------

	private void DropReward()
	{
		if (Reward == null || Reward.Stages.Count == 0)
		{
			GD.PushWarning($"Arena '{Name}': neni nastaveny Reward genom.");
			return;
		}

		int currentStage = FindCurrentStage();
		int nextStage = currentStage + 1;

		if (nextStage > Reward.MaxStage)
		{
			if (!GiveRewardWhenMaxed)
			{
				GD.Print($"Arena '{Name}': Jane uz ma max stage genomu '{Reward.FamilyId}', nic nepada.");
				return;
			}

			nextStage = Reward.MaxStage;
		}

		Item item = Reward.GetStage(nextStage);
		if (item == null)
			return;

		SpawnWorldItem(item);
		GD.Print($"Arena '{Name}': shozen genom '{Reward.FamilyId}' stage {nextStage}.");
	}

	// Projde inventář a najde nejvyšší stage tohohle genomu, kterou Jane drží.
	// 0 = žádnou nemá (dostane tedy stage 1).
	private int FindCurrentStage()
	{
		var inventory = GetTree().GetFirstNodeInGroup("inventory") as Inventory;
		if (inventory == null)
		{
			GD.PushWarning("ArenaLogic: inventar nenalezen, davam stage 1.");
			return 0;
		}

		int best = 0;

		foreach (ItemSlot slot in inventory.GetSlots())
		{
			Item held = slot.GetItem();
			if (held == null || held.GenomeFamily != Reward.FamilyId)
				continue;

			if (held.Stage > best)
				best = held.Stage;
		}

		return best;
	}

	private void SpawnWorldItem(Item item)
	{
		PackedScene scene = WorldItemScene ?? GD.Load<PackedScene>("res://GUI/Items/WorldItem.tscn");
		if (scene == null)
		{
			GD.PushError("ArenaLogic: nepodarilo se nacist WorldItem.tscn");
			return;
		}

		var pickup = scene.Instantiate<WorldItem>();
		GetTree().CurrentScene.AddChild(pickup);
		pickup.GlobalPosition = RewardPoint?.GlobalPosition ?? GlobalPosition;
		pickup.SetItem(item);
	}

	// --- smrt hráče ------------------------------------------------------

	// Volá DeathScreen: Jane se respawne v aréně, sken padá na nulu.
	public void RespawnPlayerHere()
	{
		ClearEnemies();
		ScanProgress = 0f;
		_spawnTimer = SpawnInterval;

		GetDroid()?.SetScanProgress(0f);

		MovePlayerTo(RespawnPoint?.GlobalPosition ?? GlobalPosition);
	}

	// Volá DeathScreen: Jane arénu vzdá. Droid uvolní vstup, aréna se vrátí
	// do Idle, takže při dalším vstupu začne znovu od nuly.
	public void AbandonArena()
	{
		ClearEnemies();
		ScanProgress = 0f;
		CurrentState = ArenaState.Idle;
		RemoveFromGroup("active_arena");

		SetEntranceBlocked(false);
		Border?.SetActive(false);

		GlobalSpawner?.SetPhysicsProcess(true);

		Droid droid = GetDroid();
		droid?.Release();
		droid?.SetScanProgress(0f);

		MovePlayerTo(ExitPoint?.GlobalPosition ?? GlobalPosition);
	}

	private void MovePlayerTo(Vector2 position)
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Player player)
			return;

		player.RespawnAt(position, RespawnHealth);
	}

	private void ClearEnemies()
	{
		if (Region == null)
			return;

		foreach (Node2D body in Region.GetOverlappingBodies())
		{
			if (body.IsInGroup("enemies"))
				body.QueueFree();
		}

		// Pojistka na ty, co se spawnli před chvílí a fyzika je ještě nestihla
		// zaregistrovat v Area2D.
		foreach (Node2D enemy in _spawned)
		{
			if (IsInstanceValid(enemy) && !enemy.IsQueuedForDeletion())
				enemy.QueueFree();
		}

		_spawned.Clear();
	}

	private Droid GetDroid()
	{
		if (_droid != null && IsInstanceValid(_droid))
			return _droid;

		_droid = GetTree().GetFirstNodeInGroup("droid") as Droid;
		return _droid;
	}
}
