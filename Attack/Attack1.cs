using Godot;

public partial class Attack1 : Area2D
{
	// Which projectile this instance represents. The Player script sets this
	// right after Instantiate(), before adding the bullet to the tree, so
	// _Ready() below picks the correct animation.
	public enum AttackKind
	{
		Fireball,
		Waterball
	}

	[Export] public AttackKind Kind = AttackKind.Fireball;

	[Export] public float Speed = 400f;
	[Export] public int Damage = 1;
	[Export] public bool Homing = true; // true = follows the enemy, false = aims once
	public Vector2 Direction = Vector2.Right;
	public Node Shooter; // so the bullet doesn't hit whoever fired it

	private bool _hasHit;
	private bool _targetChosen;
	private Node2D _target;
	private AnimatedSprite2D _sprite;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;

		// Both "fireball" and "waterball" animations live in the same
		// SpriteFrames on this node, switch to whichever one this shot is.
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_sprite.Play(Kind == AttackKind.Fireball ? "fireball" : "waterball");

		// clean up missed shots after 3 seconds
		GetTree().CreateTimer(3.0).Timeout += () =>
		{
			if (IsInstanceValid(this)) QueueFree();
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		// Pick the target on the first frame, after the player has set our position
		if (!_targetChosen)
		{
			_targetChosen = true;
			_target = FindClosestEnemy();

			if (_target != null)
				Direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
		}
		else if (Homing && _target != null && IsInstanceValid(_target))
		{
			Direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
		}

		Rotation = Direction.Angle(); // point the bullet where it's going
		Position += Direction * Speed * (float)delta;
	}

	private Node2D FindClosestEnemy()
	{
		Node2D closest = null;
		float closestDist = float.MaxValue;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node2D enemy || enemy.IsQueuedForDeletion())
				continue;

			float dist = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
			if (dist < closestDist)
			{
				closestDist = dist;
				closest = enemy;
			}
		}

		return closest;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (_hasHit || body == Shooter) return;
		_hasHit = true;

		// Dynamically call TakeDamage if the hit body supports it
		if (body.HasMethod("TakeDamage"))
		{
			body.Call("TakeDamage", Damage);
		}

		QueueFree(); // walls, enemies, anything solid
	}
}
