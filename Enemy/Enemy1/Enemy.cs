using Godot;

public partial class Enemy : CharacterBody2D, IDamageable
{
	[Export] public float HitRadius = 24f;

	[Export] public int Health = 3;
	[Export] public int ContactDamage = 1;
	[Export] public float Speed = 80f;
	[Export] public Godot.Collections.Array<DropEntry> Drops = new();

	[ExportGroup("Contact")]
	// Melee enemies explode on the player. Ranged ones should leave this off.
	[Export] public bool KamikazeOnContact = true;

	[ExportGroup("Ranged Attack")]
	[Export] public PackedScene ProjectileScene;
	[Export] public int RangedDamage = 1;
	[Export] public float AttackRange = 260f;    // starts shooting inside this
	[Export] public float KeepDistance = 160f;   // backs off if the player gets closer
	[Export] public float FireCooldown = 1.5f;
	[Export] public float AimSpread = 4f;        // degrees of random wobble
	[Export] public float MuzzleOffset = 20f;    // spawn distance from the body
	[Export] public bool RequireLineOfSight = true;

	[ExportGroup("Facing")]
	[Export] public Node2D Visual;
	[Export] public bool FlipInsteadOfRotate = false;
	[Export] public float RotationOffset = 0f;

	[ExportGroup("Steering")]
	[Export] public float SeparationDistance = 48f;
	[Export] public float SeparationWeight = 1.5f;
	[Export] public float WallDistance = 40f;
	[Export] public float WallWeight = 2f;
	[Export(PropertyHint.Layers2DPhysics)] public uint WallMask = 4; // layer 3

	private Node2D _player;
	private bool _dead;
	private float _fireTimer;
	private float _strafeDir = 1f;

	public override void _Ready()
	{
		AddToGroup("enemies");
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		// Stagger the first shot so a pack doesn't fire in one volley.
		_fireTimer = (float)GD.RandRange(0.0, FireCooldown);
		_strafeDir = GD.Randf() < 0.5f ? -1f : 1f;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _player == null || !IsInstanceValid(_player))
			return;

		Vector2 toPlayer = GlobalPosition.DirectionTo(_player.GlobalPosition);
		float dist = GlobalPosition.DistanceTo(_player.GlobalPosition);

		Vector2 move;
		bool isRanged = ProjectileScene != null;

		if (!isRanged || dist > AttackRange)
			move = toPlayer;                        // close the gap
		else if (dist < KeepDistance)
			move = -toPlayer;                       // too close, back up
		else
			move = toPlayer.Orthogonal() * _strafeDir; // circle while shooting

		Vector2 desired = move
			+ GetSeparation() * SeparationWeight
			+ GetObstacleAvoidance() * WallWeight;

		Vector2 direction = desired.LengthSquared() > 0.001f
			? desired.Normalized()
			: toPlayer;

		FaceDirection(toPlayer); // always look at the player, even while dodging

		Velocity = direction * Speed;
		MoveAndSlide();

		// --- shooting ---
		_fireTimer -= (float)delta;

		if (isRanged && dist <= AttackRange && _fireTimer <= 0f && HasLineOfSight())
			Shoot(toPlayer);

		// --- contact ---
		if (dist <= HitRadius && _player is Player player)
		{
			if (KamikazeOnContact)
			{
				_dead = true;
				player.TakeDamage(ContactDamage);
				QueueFree();
				return;
			}
		}
	}

	private void Shoot(Vector2 toPlayer)
	{
		_fireTimer = FireCooldown;

		float spread = Mathf.DegToRad((float)GD.RandRange(-AimSpread, AimSpread));
		Vector2 dir = toPlayer.Rotated(spread);

		var instance = ProjectileScene.Instantiate<Node2D>();
		instance.Position = GlobalPosition + dir * MuzzleOffset;

		if (instance is EnemyProjectile projectile)
		{
			projectile.Direction = dir;
			projectile.Shooter = this;
			projectile.Damage = RangedDamage;
		}

		GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, instance);
	}

	// Don't fire through walls.
	private bool HasLineOfSight()
	{
		if (!RequireLineOfSight)
			return true;

		var query = PhysicsRayQueryParameters2D.Create(
			GlobalPosition,
			_player.GlobalPosition,
			WallMask);
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	// Push away from any enemy that's too close. The closer it is, the harder the push.
	private Vector2 GetSeparation()
	{
		Vector2 push = Vector2.Zero;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node == this || node is not Node2D other || other.IsQueuedForDeletion())
				continue;

			float dist = GlobalPosition.DistanceTo(other.GlobalPosition);
			if (dist > SeparationDistance || dist <= 0.01f)
				continue;

			float strength = 1f - (dist / SeparationDistance);
			push += other.GlobalPosition.DirectionTo(GlobalPosition) * strength;
		}

		return push;
	}

	// Feel ahead with three rays and steer away from whatever they hit.
	private Vector2 GetObstacleAvoidance()
	{
		Vector2 forward = Velocity.LengthSquared() > 0.01f
			? Velocity.Normalized()
			: GlobalPosition.DirectionTo(_player.GlobalPosition);

		var space = GetWorld2D().DirectSpaceState;
		Vector2 push = Vector2.Zero;

		float[] angles = { 0f, 35f, -35f };

		foreach (float angle in angles)
		{
			Vector2 dir = forward.Rotated(Mathf.DegToRad(angle));

			var query = PhysicsRayQueryParameters2D.Create(
				GlobalPosition,
				GlobalPosition + dir * WallDistance,
				WallMask);
			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

			var hit = space.IntersectRay(query);
			if (hit.Count == 0)
				continue;

			Vector2 point = (Vector2)hit["position"];
			Vector2 normal = (Vector2)hit["normal"];

			float dist = GlobalPosition.DistanceTo(point);
			float strength = 1f - (dist / WallDistance);

			push += normal * strength;
		}

		return push;
	}

	private void FaceDirection(Vector2 direction)
	{
		if (Visual == null || direction == Vector2.Zero)
			return;

		if (FlipInsteadOfRotate)
		{
			float x = Mathf.Abs(Visual.Scale.X);
			Visual.Scale = new Vector2(direction.X < 0 ? -x : x, Visual.Scale.Y);
		}
		else
		{
			Visual.Rotation = direction.Angle() + Mathf.DegToRad(RotationOffset);
		}
	}

	public void TakeDamage(int amount)
	{
		if (_dead) return;

		Health -= amount;
		if (Health <= 0)
			Die();
	}

	private void Die()
	{
		_dead = true;
		DropItems();
		QueueFree();
	}

	private void DropItems()
	{
		foreach (DropEntry drop in Drops)
		{
			if (drop == null || drop.Scene == null)
				continue;

			if (GD.Randf() > drop.Chance)
				continue;

			var instance = drop.Scene.Instantiate<Node2D>();
			Vector2 offset = new Vector2((float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8));
			instance.Position = GlobalPosition + offset;

			if (drop.ItemData != null && instance is WorldItem worldItem)
				worldItem.SetItem(drop.ItemData);

			GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, instance);
		}
	}
}