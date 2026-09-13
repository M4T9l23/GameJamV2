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

	// Kolik stupňů otočit sprite, aby mířil ve směru letu.
	// Fireball art míří doprava (0), waterball doleva (180).
	[Export] public float FireballSpriteAngleOffset = 0f;
	[Export] public float WaterballSpriteAngleOffset = 180f;
	
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
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");

		if (_sprite == null)
		{
			GD.Print("Attack1: chybi potomek 'AnimatedSprite2D', strela poleti bez grafiky.");
		}
		else
		{
			bool isFire = Kind == AttackKind.Fireball;
			string anim = isFire ? "fireball" : "waterball";

			// Chybejici animace by jinak shodila celou strelu i strelbu.
			if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(anim))
				_sprite.Play(anim);
			else
				GD.Print($"Attack1: SpriteFrames nema animaci '{anim}'. " +
					$"Dostupne: {(_sprite.SpriteFrames == null ? "zadne SpriteFrames" : string.Join(", ", _sprite.SpriteFrames.GetAnimationNames()))}");

			_sprite.RotationDegrees = isFire
				? FireballSpriteAngleOffset
				: WaterballSpriteAngleOffset;
		}

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

		// Spinac se neposkozuje - ohen ho zapali, voda uhasi.
		if (body is FireSwitch fireSwitch)
		{
			if (Kind == AttackKind.Fireball)
				fireSwitch.Ignite();
			else
				fireSwitch.Douse();

			QueueFree();
			return;
		}

		// Dynamically call TakeDamage if the hit body supports it
		if (body.HasMethod("TakeDamage"))
		{
			body.Call("TakeDamage", Damage);
		}

		QueueFree(); // walls, enemies, anything solid
	} 
}
