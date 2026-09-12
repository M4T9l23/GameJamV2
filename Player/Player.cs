using Godot;

public partial class Player : CharacterBody2D
{
	[Export] public int MaxHealth = 5;
	[Export] public PackedScene BulletScene;
	[Export] public float FireRate = 1.00f;
	[Export] public float SpriteAngleOffsetDegrees = 180f;
	[Export] private PackedScene _deathScreenScene;

	// Základní hodnoty statů bez vybavení. Efektivní hodnota = tohle +
	// bonus z tagů itemů v equipment slotech (PlayerEquipmentBonuses).
	[Export] public float BaseSpeed = 200f;
	[Export] public int BaseDamage = 1;

	public int Health;
	private Vector2 _facing = Vector2.Right;
	private bool _canShoot = true;
	private bool _isDead;
	private AnimatedSprite2D _animatedSprite;

	public override void _Ready()
	{
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		Health = MaxHealth;
		AddToGroup("player");
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

		GD.Print("Player died");
		var screen = _deathScreenScene.Instantiate();
		GetTree().CurrentScene.AddChild(screen);
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
		Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
		Velocity = input * GetEffectiveSpeed();

		if (input != Vector2.Zero)
		{
			_animatedSprite.Play("move_animation");
			_facing = input.Normalized();
			_animatedSprite.Rotation = _facing.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);
		}
		else
		{
			_animatedSprite.Play("idle_animation");
		}

		MoveAndSlide();

		if (Input.IsActionPressed("shoot") && _canShoot)
			Shoot();
	}

	private async void Shoot()
	{
		_canShoot = false;

		var bullet = BulletScene.Instantiate<Attack1>();
		bullet.Direction = _facing;
		bullet.Rotation = _facing.Angle();
		bullet.Shooter = this;
		GetTree().CurrentScene.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;

		await ToSignal(GetTree().CreateTimer(FireRate), SceneTreeTimer.SignalName.Timeout);
		_canShoot = true;
	}
}