using Godot;

public partial class Enemy2 : CharacterBody2D, IDamageable
{
	[Export] public int Health = 3;
	[Export] public int ContactDamage = 1;
	[Export] public float Speed = 110f;

	[ExportGroup("Interception")]
	[Export] public float MaxLeadTime = 1.2f;      // seconds of prediction, capped
	[Export] public float VelocitySmoothing = 6f;  // higher = reacts faster, twitchier
	[Export] public float TurnSpeed = 6f;          // how fast it can change heading

	private Node2D _player;
	private Vector2 _playerVelSmoothed;
	private Vector2 _lastPlayerPos;
	private Vector2 _heading;
	private bool _dead;
	
	[Export] public float SpriteAngleOffsetDegrees = 0f;
	private AnimatedSprite2D _animatedSprite;

	private Node2D _sprite;

	public override void _Ready()
	{
		_sprite = GetNodeOrNull<Node2D>("AnimatedSprite2D") ?? GetNodeOrNull<Node2D>("Sprite2D");
		AddToGroup("enemies");
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		if (_player != null)
		{
			_lastPlayerPos = _player.GlobalPosition;
			_heading = GlobalPosition.DirectionTo(_player.GlobalPosition);
		}
		// Grab the AnimatedSprite2D reference
		_animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _player == null || !IsInstanceValid(_player))
			return;

		float dt = (float)delta;
		Vector2 playerPos = _player.GlobalPosition;

		// Derive the player's velocity from position change. Works whether or not
		// the player is a CharacterBody2D, so no cast needed.
		Vector2 rawVel = (playerPos - _lastPlayerPos) / dt;
		_lastPlayerPos = playerPos;
		_playerVelSmoothed = _playerVelSmoothed.Lerp(rawVel, 1f - Mathf.Exp(-VelocitySmoothing * dt));

		// Aim where they'll be when we arrive, not where they are.
		float timeToReach = Mathf.Min(GlobalPosition.DistanceTo(playerPos) / Speed, MaxLeadTime);
		Vector2 aimPoint = playerPos + _playerVelSmoothed * timeToReach;

		Vector2 desired = GlobalPosition.DirectionTo(aimPoint);

		// Ease into the new heading so it banks instead of snapping.
		_heading = _heading.Lerp(desired, 1f - Mathf.Exp(-TurnSpeed * dt)).Normalized();

		Velocity = _heading * Speed;
		MoveAndSlide();

		_heading = _heading.Lerp(desired, 1f - Mathf.Exp(-TurnSpeed * dt)).Normalized();

		if (_sprite != null)
			_sprite.Rotation = _heading.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);

		Velocity = _heading * Speed;
		MoveAndSlide();
		
		_heading = _heading.Lerp(desired, 1f - Mathf.Exp(-TurnSpeed * dt)).Normalized();

		if (_sprite != null)
			_sprite.Rotation = _heading.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);

		Velocity = _heading * Speed;
		MoveAndSlide();

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is Player player)
			{
				player.TakeDamage(ContactDamage);
				QueueFree();
				return;
			}
		}
	}

	public async void TakeDamage(int amount)
	{
		if (_dead) return;

		Health -= amount;
		if (Health <= 0)
		{
			_dead = true;
        
			// Disable collision so it doesn't hit the player or projectiles while dying
			GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
        
			// Play the animation and wait for it to finish
			_animatedSprite.Play("death_animation");
			await ToSignal(_animatedSprite, AnimatedSprite2D.SignalName.AnimationFinished);
        
			QueueFree();
		}
	}
}