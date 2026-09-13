using Godot;
using System.Collections.Generic;

// Jedna aréna. Když do ní Jane vejde, droid zaparkuje ve vstupu a začne
// skenovat. Aréna mezitím průběžně spawnuje nepřátele. Bar skenu roste,
// když je aréna prázdná, a klesá úměrně počtu nepřátel uvnitř. Na 100 %
// droid uvolní vstup a hodí na zem item pro tuhle oblast.
//
// SETUP VE SCÉNĚ:
//   ArenaLogic (Node2D)
//     +-- Region (Area2D + CollisionShape2D přes celou místnost)
//     +-- DroidPark (Marker2D)   ... vstup, kam si stoupne droid
//     +-- Respawn (Marker2D)     ... kam se Jane respawne po smrti
//     +-- Exit (Marker2D)        ... kam Jane skončí, když arénu opustí
//     +-- RewardPoint (Marker2D) ... kam spadne odmena
//     +-- Border (Node2D + ArenaBorder) ... svítící obrys, volitelný
//     +-- Spawn1..N (Marker2D)   ... spawn pointy nepřátel
//
// Exit MUSÍ ležet mimo Region, jinak se aréna hned znovu aktivuje.
//
// Aréna se dá projít opakovaně: spustí se pokaždé, když do ní Jane vejde
// a odměna nikde neexistuje - nemá ji v inventáři ani neleží na zemi.
// Když ji ztratí nebo zahodí, aréna se odemkne sama.
public partial class ArenaLogic : Node2D
{
	public enum ArenaState { Idle, Scanning, Done }

	[ExportGroup("Odměna")]
	// Item, který spadne po dokončení skenu. Každá aréna má svůj.
	[Export] public Item Reward;
	[Export] public Marker2D RewardPoint;
	[Export] public PackedScene WorldItemScene;
	// Když už Jane tenhle item má, defaultně nepadne nic.
	[Export] public bool DropEvenIfOwned = false;
	// Počítat i odměnu ležící na zemi jako "Jane ji má". Vypni, jestli
	// se má aréna dát projít znovu i když jen zapomněla item sebrat.
	[Export] public bool CheckWorldItems = true;

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
	// Rozestup mezi jednotlivými nepřáteli UVNITŘ vlny, ať se nevysypou
	// všichni v jednom framu.
	[Export] public float SpawnInterval = 0.4f;
	// Pojistka - víc než tolik živých naráz aréna nepustí.
	[Export] public int MaxAlive = 12;

	[ExportGroup("Vlny")]
	// Kolik vln proběhne, než aréna přestane spawnovat. Po poslední už
	// nic nepřijde a sken doběhne do 100 %.
	[Export] public int WaveCount = 3;
	// Velikost první vlny.
	[Export] public int EnemiesPerWave = 3;
	// O kolik je každá další vlna větší.
	[Export] public int ExtraEnemiesPerWave = 1;
	// Pauza po vyčištění vlny, než přijde další. Během ní sken stoupá.
	[Export] public float WaveDelay = 3f;
	// Pauza mezi vstupem Jane a první vlnou.
	[Export] public float FirstWaveDelay = 1.5f;

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
	private string _blockReason;
	private int _waveIndex;
	private int _toSpawnInWave;
	private bool _waveActive;
	private float _waveTimer;
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
		LogSetup();
	}

	// Souhrn zapojení do konzole. Chybějící věc se tu projeví hned, místo
	// aby se poznala až podle toho, že něco v logu chybí.
	private void LogSetup()
	{
		GD.Print($"--- Arena '{Name}' ---");
		GD.Print($"  Region:    {(Region != null ? "OK" : "CHYBI")}");
		GD.Print($"  Entrance:  {(_entranceShape != null ? "OK" : "CHYBI")}");
		GD.Print($"  Border:    {(Border != null ? "OK" : "CHYBI (pridej Node2D 'Border' se skriptem ArenaBorder)")}");
		GD.Print($"  Respawn:   {(RespawnPoint != null ? "OK" : "CHYBI")}");
		GD.Print($"  Exit:      {(ExitPoint != null ? "OK" : "CHYBI")}");
		GD.Print($"  Reward:    {(Reward != null ? $"'{Reward.DisplayName}'" : "CHYBI (nastav Item .tres)")}");
		GD.Print($"  Nepratele: {EnemyScenes.Count} scen, {SpawnPoints.Count} spawn pointu, {WaveCount} vln");

		if (Reward == null)
			GD.Print("  ! Po dokonceni skenu nic nespadne.");

		if (EnemyScenes.Count == 0 || SpawnPoints.Count == 0)
			GD.Print("  ! Arena nebude spawnovat nic, sken probehne bez odporu.");
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

		// Kdyby ve scéně nebyl (třeba u arény udělané přes Make Local),
		// vyrobíme ho za běhu. Nastavení z inspektoru tím pádem není
		// potřeba a nedá se to zapomenout.
		if (Border == null)
		{
			Border = new ArenaBorder { Name = "Border" };
			AddChild(Border);
			GD.Print($"Arena '{Name}': Border nebyl ve scene, vyrobil jsem ho za behu.");
		}

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
				if (playerInside && ShouldActivate())
					Activate();
				break;

			case ArenaState.Scanning:
				TickEntrance();
				TickScan(dt, enemyCount);
				TickSpawn(dt, enemyCount);
				break;

			case ArenaState.Done:
				// Zpet do Idle az kdyz Jane odejde. Bez toho by se arena
				// spustila znovu hned, protoze odmena lezi na zemi a ona
				// ji jeste nema v inventari.
				if (!playerInside)
				{
					CurrentState = ArenaState.Idle;
					GD.Print($"Arena '{Name}': Jane odesla, arena je pripravena.");
				}
				break;
		}
	}

	// --- aktivace a dokončení -------------------------------------------

	// Má smysl arénu (znovu) spustit? Ne, když odměna už existuje - buď
	// ji Jane drží, nebo leží někde ve světě. Když o ni přijde, aréna
	// se odemkne a dá se projít znovu.
	private bool ShouldActivate()
	{
		if (Reward == null || DropEvenIfOwned)
			return true;

		if (PlayerAlreadyHas(Reward))
			return LogBlocked($"Jane uz ma '{Reward.DisplayName}' v inventari");

		if (CheckWorldItems)
		{
			WorldItem lying = FindRewardInWorld();
			if (lying != null)
				return LogBlocked($"'{Reward.DisplayName}' lezi ve svete jako '{lying.Name}' " +
					$"na {lying.GlobalPosition}. Vypni CheckWorldItems, jestli to vadi.");
		}

		_blockReason = null;
		return true;
	}

	// Vypíše důvod jen při změně, ne každý frame.
	private bool LogBlocked(string reason)
	{
		if (_blockReason != reason)
		{
			_blockReason = reason;
			GD.Print($"Arena '{Name}': nespoustim se - {reason}.");
		}

		return false;
	}

	// Vrátí WorldItem s odměnou, pokud nějaký ve scéně leží.
	private WorldItem FindRewardInWorld()
	{
		Node scene = GetTree().CurrentScene;
		if (scene == null)
			return null;

		foreach (Node node in scene.GetChildren())
		{
			if (node is WorldItem pickup && pickup.GetItem()?.Id == Reward.Id)
				return pickup;
		}

		return null;
	}



	private void Activate()
	{
		CurrentState = ArenaState.Scanning;
		ScanProgress = 0f;
		_entranceBlocked = false;
		ResetWaves();

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

		// 1) Rozsypávám aktuální vlnu po jednom.
		if (_toSpawnInWave > 0)
		{
			_spawnTimer -= dt;
			if (_spawnTimer > 0f)
				return;

			_spawnTimer = SpawnInterval;

			if (enemyCount >= MaxAlive)
				return;

			SpawnOne();
			_toSpawnInWave--;
			return;
		}

		// 2) Vlna je venku celá - čekám, až ji Jane vybije.
		if (_waveActive)
		{
			if (enemyCount > 0)
				return;

			_waveActive = false;
			_waveTimer = WaveDelay;

			GD.Print($"Arena '{Name}': vlna {_waveIndex}/{WaveCount} vycistena.");
			return;
		}

		// 3) Všechny vlny doběhly - dál už nic nespawnuje a sken volně
		//    stoupá do 100 %.
		if (_waveIndex >= WaveCount)
			return;

		// 4) Pauza mezi vlnami.
		_waveTimer -= dt;
		if (_waveTimer > 0f)
			return;

		StartNextWave();
	}

	private void StartNextWave()
	{
		_toSpawnInWave = EnemiesPerWave + _waveIndex * ExtraEnemiesPerWave;
		_waveIndex++;
		_waveActive = true;
		_spawnTimer = 0f;

		GD.Print($"Arena '{Name}': vlna {_waveIndex}/{WaveCount}, {_toSpawnInWave} nepratel.");
	}

	private void ResetWaves()
	{
		_waveIndex = 0;
		_toSpawnInWave = 0;
		_waveActive = false;
		_waveTimer = FirstWaveDelay;
		_spawnTimer = 0f;
	}

	private void SpawnOne()
	{
		PackedScene scene = EnemyScenes[(int)(GD.Randi() % (uint)EnemyScenes.Count)];
		Marker2D point = SpawnPoints[(int)(GD.Randi() % (uint)SpawnPoints.Count)];

		if (scene == null || point == null)
			return;

		var enemy = scene.Instantiate<Node2D>();

		// Do scény, ne pod arénu. Jako dítě arény by nepřítel zdědil její
		// scale (a rotaci) a byl by jinak velký než ten samý nepřítel od
		// globálního spawneru - včetně kolizních tvarů.
		Node parent = GetTree().CurrentScene ?? (Node)this;
		parent.AddChild(enemy);

		enemy.GlobalPosition = point.GlobalPosition;
		enemy.Scale = Vector2.One;
		enemy.Rotation = 0f;

		_spawned.RemoveAll(n => !IsInstanceValid(n) || n.IsQueuedForDeletion());
		_spawned.Add(enemy);
	}

	// --- odměna ----------------------------------------------------------

	private void DropReward()
	{
		if (Reward == null)
		{
			GD.Print($"Arena '{Name}': neni nastavena odmena (Reward), nic nepada.");
			return;
		}

		if (!DropEvenIfOwned && PlayerAlreadyHas(Reward))
		{
			GD.Print($"Arena '{Name}': Jane uz ma '{Reward.DisplayName}', nic nepada.");
			return;
		}

		SpawnWorldItem(Reward);
		GD.Print($"Arena '{Name}': shozen item '{Reward.DisplayName}'.");
	}

	// Má Jane tenhle item v inventáři? Porovnává se podle Id.
	private bool PlayerAlreadyHas(Item item)
	{
		if (GetTree().GetFirstNodeInGroup("inventory") is not Inventory inventory)
		{
			GD.Print("ArenaLogic: inventar nenalezen.");
			return false;
		}

		foreach (ItemSlot slot in inventory.GetSlots())
		{
			Item held = slot.GetItem();
			if (held != null && held.Id == item.Id)
				return true;
		}

		return false;
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
		ResetWaves();

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
