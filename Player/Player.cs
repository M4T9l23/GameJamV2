using Godot;

public partial class Player : CharacterBody2D
{
	[Signal] public delegate void HealthChangedEventHandler(int current, int max);
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

	public int Health;
	private Vector2 _facing = Vector2.Right;
	private bool _canShoot = true;
	private bool _canShootSecondary = true;
	private bool _isDead;
	private bool _isAttacking;
	private AnimatedSprite2D _animatedSprite;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_animatedSprite.AnimationFinished += OnAnimationFinished;
		Health = MaxHealth;
		AddToGroup("player");
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
	}

	public void TakeDamage(int amount)
	{
		if (_isDead) return;

		Health -= amount;
		EmitSignal(SignalName.HealthChanged, Mathf.Max(Health, 0), MaxHealth);
		GD.Print($"Player HP: {Health}/{MaxHealth}");

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

	// Volá ArenaLogic při respawnu nebo opuštění arény.
	// health <= 0 znamená plné HP.
	public void RespawnAt(Vector2 position, int health)
	{
		EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
		GlobalPosition = position;
		Velocity = Vector2.Zero;
		Health = health > 0 ? Mathf.Min(health, MaxHealth) : MaxHealth;
		_isDead = false;
		_canShoot = true;
		_canShootSecondary = true;
		_isAttacking = false;

		SetPhysicsProcess(true);   // Die() ho vypnul

		GD.Print($"Player respawned, HP: {Health}/{MaxHealth}");
	}

	// Aktuální rychlost hráče včetně bonusu z tagu "speed:<číslo>" na
	// vybavených itemech (prvních 5 slotů inventáře).
	public float GetEffectiveSpeed()
	{
		float bonus = PlayerEquipmentBonuses.Instance?.GetBonus("speed") ?? 0f;
		if (PlayerEquipmentBonuses.Instance == null)
			GD.Print("Player: PlayerEquipmentBonuses.Instance je null - autoload asi neni registrovany.");
		return BaseSpeed + bonus;
	}

	// Poškození primárního útoku (E = fireball) včetně bonusu z tagu "strength:<číslo>".
	public int GetAttackDamage()
	{
		float bonus = PlayerEquipmentBonuses.Instance?.GetBonus("strength") ?? 0f;
		return BaseDamage + Mathf.RoundToInt(bonus);
	}

	// Poškození sekundárního útoku (Q = waterball). Vlastní base hodnota,
	// ale sdílí stejný "strength" bonus z vybavení jako primární útok.
	public int GetAttackDamageSecondary()
	{
		float bonus = PlayerEquipmentBonuses.Instance?.GetBonus("strength") ?? 0f;
		return BaseDamageSecondary + Mathf.RoundToInt(bonus);
	}

	public override void _PhysicsProcess(double delta)
	{
		Vector2 input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = input * GetEffectiveSpeed();

		if (input != Vector2.Zero)
		{
			_facing = input.Normalized();
			_animatedSprite.Rotation = _facing.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);
		}

		// Attack animace má přednost před idle/move
		if (!_isAttacking)
			_animatedSprite.Play(input != Vector2.Zero ? MoveAnim : IdleAnim);
			
		MoveAndSlide();

		if (Input.IsActionPressed("shoot") && _canShoot)
			Shoot(Attack1.AttackKind.Fireball);

		if (Input.IsActionPressed("shoot_secondary") && _canShootSecondary)
			Shoot(Attack1.AttackKind.Waterball);
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

		float rate = isSecondary ? FireRateSecondary : FireRate;
		await ToSignal(GetTree().CreateTimer(rate), SceneTreeTimer.SignalName.Timeout);

		if (!IsInstanceValid(this)) return;

		if (isSecondary)
			_canShootSecondary = true;
		else
			_canShoot = true;
	}
}
