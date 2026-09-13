using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
	// Vyletí, když Jane zkusí použít schopnost, kterou nemá odemčenou.
	// Napoj si na to zvuk nebo hlášku v HUD.
	[Signal] public delegate void AbilityBlockedEventHandler(string ability);
	// Vyletí po úspěšném unstacku - navěš si na to zvuk / fade / hlášku.
	[Signal] public delegate void UnstuckEventHandler(Vector2 position);
	[Export] public int MaxHealth = 5;
	[Export] public PackedScene BulletScene;
	[Export] public float FireRate = 1.00f;
	[Export] public float SpriteAngleOffsetDegrees = 180f;
	[Export] private PackedScene _deathScreenScene;

	// Názvy animací v SpriteFrames. Attack animace musí mít vypnutý Loop,
	// jinak se AnimationFinished nikdy nezavolá.
	[Export] public string IdleAnim = "idle_animation";
	[Export] public string MoveAnim = "move_animation";
	[Export] public string AttackAnim = "attack_animation";

	// Základní hodnoty statů bez vybavení. Efektivní hodnota = tohle +
	// bonus z tagů itemů v equipment slotech (PlayerEquipmentBonuses).
	[Export] public float BaseSpeed = 200f;
	[Export] public int BaseDamage = 1;

	// Sekundární útok (Q = waterball). Vlastní cooldown a vlastní damage,
	// takže může mít úplně jiný rytmus/sílu než primární útok (E = fireball).
	[Export] public float FireRateSecondary = 1.00f;
	[Export] public int BaseDamageSecondary = 1;

	// Volitelné přebití rychlosti/homingu pro sekundární útok. Pokud chceš,
	// aby waterball měl stejné hodnoty jako je nastaveno přímo na Attack1.tscn,
	// prostě nech tyto hodnoty stejné jako tam (Speed=400, Homing=true).
	[Export] public float SecondarySpeed = 300f;
	[Export] public bool SecondaryHoming = false;

	[ExportGroup("Sprint")]
	// Základní násobič rychlosti při držení sprintu. Itemy s tagem
	// "sprint:<procenta>" ho zvyšují: sprint:20 = +0.20 k násobiči.
	[Export] public float SprintMultiplier = 1.6f;
	// Sekundy sprintu na plnou výdrž. Tag "stamina:<sekundy>" přidává.
	[Export] public float BaseStamina = 2.0f;
	// Za jak dlouho se plná výdrž doplní, když Jane nesprintuje.
	[Export] public float StaminaRecoverySeconds = 3.0f;
	// Prodleva, než se doplňování rozjede.
	[Export] public float StaminaRecoveryDelay = 0.6f;

	[ExportGroup("Healing")]
	// Kolik HP vrátí jedno použití (klávesa R).
	[Export] public int HealAmount = 2;
	// Prodleva mezi použitími v sekundách.
	[Export] public float HealCooldown = 8f;

	[ExportGroup("Unstack")]
	// Jak často se ukládá bezpečná pozice (v sekundách).
	[Export] public float UnstackSampleInterval = 0.25f;
	// Kolik vzorků se drží. 12 * 0.25 = tři sekundy historie. Vic = skok
	// dal dozadu, ale taky vetsi sance, ze to Jane hodi pres pul areny.
	[Export] public int UnstackHistorySize = 12;
	// Jak daleko od sebe musi byt dva vzorky, aby se ten novy ulozil.
	// Brani tomu, aby se buffer zaplnil dvanacti skoro shodnymi body,
	// kdyz se Jane zasekne a jen se tam vrti o par pixelu.
	[Export] public float UnstackMinSampleDistance = 24f;
	// Prodleva mezi pouzitimi. Bez ni je unstack volny unik z obklici.
	[Export] public float UnstackCooldown = 15f;

	[ExportGroup("Zamky schopnosti")]
	// Když je zapnuto, schopnost jde použít jen s odpovídajícím itemem
	// v equipment slotu (tag "ability_fire" / "ability_water" /
	// "ability_sprint" se součtem větším než nula).
	[Export] public bool RequireFireItem = true;
	[Export] public bool RequireWaterItem = true;
	[Export] public bool RequireSprintItem = true;
	[Export] public bool RequireHealItem = true;

	[ExportGroup("Odolnosti")]
	// Strop pro součet odolností, aby se Jane nestala nesmrtelnou.
	// 0.8 = maximálně 80 % pohlceného poškození.
	[Export] public float MaxResistance = 0.8f;

	public float Stamina { get; private set; }
	public float StaminaMax => BaseStamina + Bonus("stamina");
	public bool IsSprinting { get; private set; }

	// Pro HUD: kdyz je false, unstack je na cooldownu.
	public bool CanUnstack => _canUnstack;

	private float _staminaIdle;

	public int Health;
	private Vector2 _facing = Vector2.Right;
	private bool _canShoot = true;
	private bool _canShootSecondary = true;
	private bool _canHeal = true;
	private bool _canUnstack = true;
	// Managed flag - da se cist i kdyz uz je nativni objekt uvolneny,
	// na rozdil od cehokoliv, co sahne na strom nebo na nody.
	private bool _exiting;
	private bool _isDead;
	private bool _isAttacking;
	private AnimatedSprite2D _animatedSprite;

	// Kruhova historie bezpecnych pozic. Ukladaji se jen body, ve kterych
	// se Jane opravdu hybala a do niceho nenarazila - takze pozice z
	// okamziku, kdy uz byla zaseknuta, se sem nikdy nedostane.
	private readonly Queue<Vector2> _safeSpots = new();
	private Vector2 _spawnPoint;
	private float _sampleTimer;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_animatedSprite.AnimationFinished += OnAnimationFinished;
		AddToGroup("player");

		Health = GetEffectiveMaxHealth();
		Stamina = StaminaMax;

		// Zachranna brzda pro pripad, ze je historie prazdna (Jane se
		// zasekla driv, nez se stihl ulozit prvni vzorek).
		_spawnPoint = GlobalPosition;

		// Když si Jane sundá item s "maxhp", musíme HP doříznout na nové
		// maximum - jinak by jí zůstalo víc, než smí mít.
		if (PlayerEquipmentBonuses.Instance != null)
			PlayerEquipmentBonuses.Instance.BonusesChanged += OnBonusesChanged;

		EmitSignal(SignalName.HealthChanged, Health, GetEffectiveMaxHealth());

		// Kontrolní výpis: když tenhle řádek v konzoli NENÍ, běží stará
		// zkompilovaná assembly a žádná z těchhle změn se neuplatnila.
		GD.Print($"Player: zamky schopnosti -> fire={CanCastFire}, water={CanCastWater}, " +
			$"sprint={CanSprint}, heal={CanHeal} (Require: {RequireFireItem}/{RequireWaterItem}/" +
			$"{RequireSprintItem}/{RequireHealItem})");
	}

	// Autoload prezije reload sceny, Jane ne. Bez odhlaseni by si drzel
	// odkaz na kazdou mrtvou Jane a pri kazde zmene bonusu je vsechny
	// zavolal -> ObjectDisposedException.
	public override void _ExitTree()
	{
		_exiting = true;

		if (PlayerEquipmentBonuses.Instance != null)
			PlayerEquipmentBonuses.Instance.BonusesChanged -= OnBonusesChanged;
	}

	private void OnBonusesChanged()
	{
		if (_exiting)
			return;

		int max = GetEffectiveMaxHealth();

		if (Health > max)
			Health = max;

		if (Stamina > StaminaMax)
			Stamina = StaminaMax;

		EmitSignal(SignalName.HealthChanged, Mathf.Max(Health, 0), max);
	}

	// Beztypové poškození - bere se jako fyzické. Nechávám to jako
	// samostatnou metodu, protože ji přes Call("TakeDamage", x) volá
	// Attack1 i EnemyProjectile duck typingem.
	public void TakeDamage(int amount)
	{
		TakeTypedDamage(amount, "physical");
	}

	// Poškození se známým typem ("fire", "water", "physical"). Odolnosti
	// z vybavení pohltí část podle GetResistance().
	public void TakeTypedDamage(int amount, string damageType)
	{
		if (_isDead) return;

		float resistance = GetResistance(damageType);
		int final = Mathf.Max(0, Mathf.RoundToInt(amount * (1f - resistance)));

		// Odolnost nikdy nesmí poškození umazat úplně - jinak by se dala
		// arena vyfarmit stáním na místě.
		if (amount > 0 && final == 0)
			final = 1;

		Health -= final;

		int max = GetEffectiveMaxHealth();
		EmitSignal(SignalName.HealthChanged, Mathf.Max(Health, 0), max);

		if (resistance > 0f)
			GD.Print($"Player HP: {Health}/{max} (dmg {amount} -> {final}, {damageType}, res {resistance:P0})");
		else
			GD.Print($"Player HP: {Health}/{max}");

		if (Health <= 0)
			Die();
	}

	private void OnAnimationFinished()
	{
		if (_animatedSprite.Animation == AttackAnim)
			_isAttacking = false;
	}


	private void Die()
	{
		if (_isDead) return;
		_isDead = true;
		_isAttacking = false;

		SetPhysicsProcess(false);   // stop moving and shooting
		_animatedSprite.Play(IdleAnim);

		GD.Print("Player died");
		var screen = _deathScreenScene.Instantiate<DeathScreen>();
		screen.Setup(true);
		GetTree().CurrentScene.AddChild(screen);
	}

	// --- unstack ---------------------------------------------------------

	// Ulozi aktualni pozici jako bezpecnou, pokud splnuje podminky.
	// Vola se z _PhysicsProcess az PO MoveAndSlide(), aby uz byly zname
	// kolize z tohohle framu.
	private void SampleSafeSpot(float dt, Vector2 input)
	{
		_sampleTimer += dt;

		if (_sampleTimer < UnstackSampleInterval)
			return;

		_sampleTimer = 0f;

		// Zadny vstup = Jane stoji, nic zajimaveho k ulozeni.
		if (input == Vector2.Zero)
			return;

		// Tohle je jadro cele veci: kdyz se Jane o neco otira, pozice je
		// podezrela a neulozi se. Zaseknuty stav tim padem nikdy neskonci
		// v historii a unstack ma vzdycky kam skocit.
		if (GetSlideCollisionCount() > 0)
			return;

		// Drzela klavesu, ale nehnula se? Taky zasek (nebo naraz do zdi
		// presne v ose, kde MoveAndSlide nenahlasi skluz).
		if (Velocity.Length() < 1f)
			return;

		if (_safeSpots.Count > 0)
		{
			Vector2 last = LastSafeSpot();

			if (GlobalPosition.DistanceTo(last) < UnstackMinSampleDistance)
				return;
		}

		_safeSpots.Enqueue(GlobalPosition);

		while (_safeSpots.Count > Mathf.Max(1, UnstackHistorySize))
			_safeSpots.Dequeue();
	}

	private Vector2 LastSafeSpot()
	{
		Vector2 last = _spawnPoint;

		foreach (Vector2 spot in _safeSpots)
			last = spot;

		return last;
	}

	// Vytahne Jane ze zaseku: skoci na nejstarsi zapamatovanou bezpecnou
	// pozici (tedy cca UnstackHistorySize * UnstackSampleInterval sekund
	// zpatky), vynuluje rychlost a vycisti historii, aby se nedalo
	// spamovat porad do stejneho spatneho framu.
	//
	// Kdyz je Jane mrtva, unstack ji zaroven oziví - v death menu je to
	// alternativa k restartu celeho levelu.
	public void Unstack()
	{
		if (_exiting || !IsInstanceValid(this))
			return;

		if (!_canUnstack && !_isDead)
		{
			GD.Print("Player: unstack je na cooldownu.");
			return;
		}

		Vector2 target = _safeSpots.Count > 0 ? _safeSpots.Peek() : _spawnPoint;

		_safeSpots.Clear();
		_sampleTimer = 0f;

		if (_isDead)
		{
			// RespawnAt resetuje HP, staminu i _isDead a zase zapne
			// fyziku, kterou Die() vypnul.
			RespawnAt(target, 0);
		}
		else
		{
			GlobalPosition = target;
			Velocity = Vector2.Zero;
			StartUnstackCooldown();
		}

		GD.Print($"Player unstuck -> {target}");
		EmitSignal(SignalName.Unstuck, target);
	}

	private async void StartUnstackCooldown()
	{
		if (UnstackCooldown <= 0f)
			return;

		_canUnstack = false;

		await ToSignal(GetTree().CreateTimer(UnstackCooldown), SceneTreeTimer.SignalName.Timeout);

		if (!_exiting && IsInstanceValid(this))
			_canUnstack = true;
	}

	// ---------------------------------------------------------------------

	// Volá ArenaLogic při respawnu nebo opuštění arény.
	// health <= 0 znamená plné HP.
	public void RespawnAt(Vector2 position, int health)
	{
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		GlobalPosition = position;
		Velocity = Vector2.Zero;
		int max = GetEffectiveMaxHealth();
		Health = health > 0 ? Mathf.Min(health, max) : max;
		Stamina = StaminaMax;
		_isDead = false;
		_canShoot = true;
		_canShootSecondary = true;
		_canHeal = true;
		_canUnstack = true;
		_isAttacking = false;

		// Historie z minuleho zivota uz neplati - nova pozice je novy
		// zachytny bod.
		_safeSpots.Clear();
		_sampleTimer = 0f;
		_spawnPoint = position;

		SetPhysicsProcess(true);   // Die() ho vypnul

		GD.Print($"Player respawned, HP: {Health}/{MaxHealth}");
	}

	// Zkratka pro čtení sečteného tagu z vybavených itemů.
	private static float Bonus(string tag) =>
		PlayerEquipmentBonuses.Instance?.GetBonus(tag) ?? 0f;

	// Rychlost chůze včetně tagu "speed". Sprint se násobí až v pohybu.
	public float GetEffectiveSpeed() => BaseSpeed + Bonus("speed");

	// Násobič sprintu včetně tagu "sprint:<procenta>".
	public float GetSprintMultiplier() => SprintMultiplier + Bonus("sprint") / 100f;

	// Maximum HP včetně tagu "maxhp".
	public int GetEffectiveMaxHealth() =>
		Mathf.Max(1, MaxHealth + Mathf.RoundToInt(Bonus("maxhp")));

	// Poškození fireballu: base + "strength" (obě střely) + "firepower" (jen oheň).
	public int GetAttackDamage() =>
		Mathf.Max(0, BaseDamage + Mathf.RoundToInt(Bonus("strength") + Bonus("firepower")));

	// Poškození waterballu: base + "strength" + "waterpower" (jen voda).
	public int GetAttackDamageSecondary() =>
		Mathf.Max(0, BaseDamageSecondary + Mathf.RoundToInt(Bonus("strength") + Bonus("waterpower")));

	// Prodleva mezi výstřely. Tagy "firerate"/"waterrate" jsou PROCENTA
	// zrychlení, ne sekundy - firerate:50 zkrátí prodlevu na dvě třetiny.
	// Dělením se to nikdy nedostane na nulu ani do záporu.
	public float GetEffectiveFireRate() => FireRate / (1f + Bonus("firerate") / 100f);

	public float GetEffectiveFireRateSecondary() =>
		FireRateSecondary / (1f + Bonus("waterrate") / 100f);

	// --- zámky schopnosti ------------------------------------------------

	public bool CanCastFire => !RequireFireItem || Bonus("ability_fire") > 0f;
	public bool CanCastWater => !RequireWaterItem || Bonus("ability_water") > 0f;
	public bool CanSprint => !RequireSprintItem || Bonus("ability_sprint") > 0f;
	public bool CanHeal => !RequireHealItem || Bonus("ability_heal") > 0f;

	// Podíl pohlceného poškození daného typu, 0 az MaxResistance.
	// Tagy "fireres", "waterres", "armor" jsou procenta.
	public float GetResistance(string damageType)
	{
		string tag = damageType switch
		{
			"fire" => "fireres",
			"water" => "waterres",
			_ => "armor",
		};

		return Mathf.Clamp(Bonus(tag) / 100f, 0f, MaxResistance);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		Velocity = input * GetEffectiveSpeed() * TickSprint((float)delta, input);

		if (input != Vector2.Zero)
		{
			_facing = input.Normalized();
			_animatedSprite.Rotation = _facing.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);
		}

		// Attack animace má přednost před idle/move
		if (!_isAttacking)
			_animatedSprite.Play(input != Vector2.Zero ? MoveAnim : IdleAnim);

		MoveAndSlide();

		// Az tady - GetSlideCollisionCount() ma smysl jen po MoveAndSlide().
		SampleSafeSpot((float)delta, input);

		// Zámky se kontrolují až tady, ne uvnitř Shoot() - jinak by se
		// spustila attack animace a spálil cooldown i pro zablokovaný útok.
		if (Input.IsActionJustPressed("shoot") && !CanCastFire)
		{
			GD.Print($"BLOK fire: ability_fire={Bonus("ability_fire")}, Require={RequireFireItem}");
			DumpInventory();
			EmitSignal(SignalName.AbilityBlocked, "fire");
		}
		else if (Input.IsActionPressed("shoot") && _canShoot && CanCastFire)
			Shoot(Attack1.AttackKind.Fireball);

		if (Input.IsActionJustPressed("shoot_secondary") && !CanCastWater)
		{
			GD.Print($"BLOK water: ability_water={Bonus("ability_water")}, Require={RequireWaterItem}");
			DumpInventory();
			EmitSignal(SignalName.AbilityBlocked, "water");
		}
		else if (Input.IsActionPressed("shoot_secondary") && _canShootSecondary && CanCastWater)
			Shoot(Attack1.AttackKind.Waterball);
	}

	// Vrací násobič rychlosti pro tenhle frame a stará se o výdrž.
	// Sprint jede, jen když Jane drží klávesu, hýbe se a má co utratit.
	private float TickSprint(float dt, Vector2 input)
	{
		if (Input.IsActionJustPressed("heal"))
		{
			if (!CanHeal)
				EmitSignal(SignalName.AbilityBlocked, "heal");
			else if (_canHeal)
				Heal();
		}

		if (Input.IsActionJustPressed("sprint") && !CanSprint)
			EmitSignal(SignalName.AbilityBlocked, "sprint");

		bool wants = Input.IsActionPressed("sprint") && input != Vector2.Zero && CanSprint;

		IsSprinting = wants && Stamina > 0f;

		if (IsSprinting)
		{
			Stamina = Mathf.Max(0f, Stamina - dt);
			_staminaIdle = 0f;
			return GetSprintMultiplier();
		}

		// Doplňování se rozjede až po krátké prodlevě, aby nešlo
		// sprintovat trhaně pořád dokola.
		_staminaIdle += dt;

		if (_staminaIdle >= StaminaRecoveryDelay && StaminaRecoverySeconds > 0f)
			Stamina = Mathf.Min(StaminaMax, Stamina + dt * StaminaMax / StaminaRecoverySeconds);

		return 1f;
	}

	// Doplní HP a nastartuje cooldown. Když je Jane na plných, použití
	// se nespotřebuje - jinak by se dal lék omylem vyplýtvat.
	// Ladici vypis: co je opravdu ve slotech a jake to ma tagy.
	private void DumpInventory()
	{
		if (GetTree().GetFirstNodeInGroup("inventory") is not Inventory inv)
		{
			GD.Print("  inventar: NENALEZEN (grupa 'inventory')");
			return;
		}

		int i = 0;
		foreach (ItemSlot slot in inv.GetSlots())
		{
			Item it = slot.GetItem();
			string tags = it?.Tags == null || it.Tags.Length == 0
				? "ZADNE TAGY"
				: string.Join(" | ", it.Tags);

			GD.Print($"  slot {i++}: {(it == null ? "prazdny" : $"'{it.DisplayName}' [{tags}]")}");
		}
	}

	private async void Heal()
	{
		int max = GetEffectiveMaxHealth();

		if (Health >= max)
		{
			GD.Print("Player: plne HP, healing se nepouzil.");
			return;
		}

		_canHeal = false;

		Health = Mathf.Min(max, Health + HealAmount);
		EmitSignal(SignalName.HealthChanged, Health, max);
		GD.Print($"Player healed: {Health}/{max}");

		await ToSignal(GetTree().CreateTimer(HealCooldown), SceneTreeTimer.SignalName.Timeout);

		if (!_exiting && IsInstanceValid(this))
			_canHeal = true;
	}

	private async void Shoot(Attack1.AttackKind kind)
	{
		bool isSecondary = kind == Attack1.AttackKind.Waterball;

		if (isSecondary)
			_canShootSecondary = false;
		else
			_canShoot = false;

		_isAttacking = true;
		_animatedSprite.Frame = 0;
		_animatedSprite.Play(AttackAnim);

		// Kdyz tady cokoliv spadne, _canShoot uz by se nikdy nevratilo na
		// true a strelba by tise umrela. Radsi to chytit a nahlasit.
		try
		{
			SpawnBullet(kind, isSecondary);
		}
		catch (System.Exception e)
		{
			GD.PushError($"Player.Shoot selhal: {e.Message}");
			GD.Print($"Player.Shoot SELHAL: {e}");
		}

		float rate = isSecondary ? GetEffectiveFireRateSecondary() : GetEffectiveFireRate();
		await ToSignal(GetTree().CreateTimer(rate), SceneTreeTimer.SignalName.Timeout);

		if (_exiting || !IsInstanceValid(this)) return;

		if (isSecondary)
			_canShootSecondary = true;
		else
			_canShoot = true;
	}

	private void SpawnBullet(Attack1.AttackKind kind, bool isSecondary)
	{
		if (BulletScene == null)
			throw new System.InvalidOperationException("BulletScene neni prirazena v inspektoru hrace.");

		var bullet = BulletScene.Instantiate<Attack1>();
		bullet.Kind = kind; // picks fireball/waterball animation in Attack1._Ready()
		bullet.Direction = _facing;
		bullet.Shooter = this;
		bullet.Damage = isSecondary ? GetAttackDamageSecondary() : GetAttackDamage();

		if (isSecondary)
		{
			bullet.Speed = SecondarySpeed;
			bullet.Homing = SecondaryHoming;
		}
		// else: leave Speed/Homing at whatever Attack1.tscn has them set to

		GetTree().CurrentScene.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;

		GD.Print($"Strela vyrobena: {kind}, dmg {bullet.Damage}");
	}
};
