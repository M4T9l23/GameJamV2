using Godot;

public partial class Player : CharacterBody2D
{
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

	public int Health;
	private Vector2 _facing = Vector2.Right;
	private bool _canShoot = true;
	private bool _isDead;
	private bool _isAttacking;
	private AnimatedSprite2D _animatedSprite;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_animatedSprite.AnimationFinished += OnAnimationFinished;
		Health = MaxHealth;
		AddToGroup("player");
	}

	private void OnAnimationFinished()
	{
		if (_animatedSprite.Animation == AttackAnim)
			_isAttacking = false;
	}

	public void TakeDamage(int amount)
	{
		if (_isDead) return;

		Health -= amount;
		GD.Print($"Player HP: {Health}/{MaxHealth}");

		if (Health <= 0)
			Die();
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
		GlobalPosition = position;
		Velocity = Vector2.Zero;
		Health = health > 0 ? Mathf.Min(health, MaxHealth) : MaxHealth;
		_isDead = false;
		_canShoot = true;
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

	// Poškození útoku včetně bonusu z tagu "strength:<číslo>".
	public int GetAttackDamage()
	{
		float bonus = PlayerEquipmentBonuses.Instance?.GetBonus("strength") ?? 0f;
		return BaseDamage + Mathf.RoundToInt(bonus);
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
			Shoot();
	}

	private async void Shoot()
	{
		_canShoot = false;

		_isAttacking = true;
		_animatedSprite.Frame = 0;
		_animatedSprite.Play(AttackAnim);

		var bullet = BulletScene.Instantiate<Attack1>();
		bullet.Direction = _facing;
		bullet.Shooter = this;
		bullet.Damage = GetAttackDamage();
		GetTree().CurrentScene.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;

		await ToSignal(GetTree().CreateTimer(FireRate), SceneTreeTimer.SignalName.Timeout);

		if (!IsInstanceValid(this)) return;
		_canShoot = true;
	}
}