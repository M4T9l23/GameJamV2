using Godot;

public partial class Enemy : CharacterBody2D
{
	[Export] public int Health = 3;
	[Export] public int ContactDamage = 1;
	[Export] public float Speed = 80f;
	[Export] public Godot.Collections.Array<DropEntry> Drops = new();

	[ExportGroup("Facing")]
	[Export] public Node2D Visual;
	[Export] public bool FlipInsteadOfRotate = false;
	[Export] public float RotationOffset = 0f;

	[ExportGroup("Steering")]
	[Export] public float SeparationDistance = 48f;  // how far apart enemies stay
	[Export] public float SeparationWeight = 1.5f;   // how hard they push each other
	[Export] public float WallDistance = 40f;        // how far ahead they look for walls
	[Export] public float WallWeight = 2f;           // how hard they steer around walls
	[Export(PropertyHint.Layers2DPhysics)] public uint WallMask = 4; // layer 3

	private Node2D _player;
	private bool _dead;

	public override void _Ready()
	{
		AddToGroup("enemies");
		_player = GetTree().GetFirstNodeInGroup("player") as Node2D;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_dead || _player == null || !IsInstanceValid(_player))
			return;

		Vector2 toPlayer = GlobalPosition.DirectionTo(_player.GlobalPosition);

		Vector2 desired = toPlayer
			+ GetSeparation() * SeparationWeight
			+ GetObstacleAvoidance() * WallWeight;

		// If the forces cancel out completely, fall back to chasing
		Vector2 direction = desired.LengthSquared() > 0.001f
			? desired.Normalized()
			: toPlayer;

		FaceDirection(toPlayer); // always look at the player, even while dodging

		Velocity = direction * Speed;
		MoveAndSlide();

		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			if (GetSlideCollision(i).GetCollider() is Player player)
			{
				GD.Print("DMGGGG");
				player.TakeDamage(ContactDamage);
				QueueFree();
				return;
			}
		}
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

			float strength = 1f - (dist / SeparationDistance); // 0 at the edge, 1 when overlapping
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
			float strength = 1f - (dist / WallDistance); // stronger the closer we get

			push += normal * strength; // the normal points away from the wall surface
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
			if (drop == null || drop.Item == null)
				continue;

			if (GD.Randf() > drop.Chance)
				continue;

			var item = drop.Item.Instantiate<Node2D>();
			Vector2 offset = new Vector2((float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8));
			item.Position = GlobalPosition + offset;

			GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, item);
		}
	}
}
