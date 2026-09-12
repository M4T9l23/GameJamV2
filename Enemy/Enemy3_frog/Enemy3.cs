using Godot;

public partial class Enemy3 : CharacterBody2D, IDamageable
{
    [Export] public float HitRadius = 24f;

    [Export] public int Health = 3;
    [Export] public int ContactDamage = 1;
    [Export] public Godot.Collections.Array<DropEntry> Drops = new();

    [ExportGroup("Leaping Movement")]
    [Export] public float LeapForce = 400f;
    [Export] public float Friction = 400f;
    [Export] public double LeapCooldown = 2.0;

    [ExportGroup("Facing")]
    [Export] public Node2D Visual;
    [Export] public AnimatedSprite2D Sprite;      // drag the AnimatedSprite2D here
    [Export] public bool FlipInsteadOfRotate = false;
    [Export] public float RotationOffset = 0f;

    [ExportGroup("Steering")]
    [Export] public float SeparationDistance = 48f;
    [Export] public float SeparationWeight = 1.5f;
    [Export] public float WallDistance = 40f;
    [Export] public float WallWeight = 2f;
    [Export(PropertyHint.Layers2DPhysics)] public uint WallMask = 4;

    private Node2D _player;
    private bool _dead;
    private bool _inAir;
    private double _leapTimer;
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;

        // Offset the timer slightly so all frogs don't jump on the exact same frame
        _leapTimer = GD.RandRange(0, LeapCooldown);

        if (Sprite != null)
        {
            Sprite.Stop();
            Sprite.Frame = 0;   // crouched
        }
        // Grab the AnimatedSprite2D reference
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead || _player == null || !IsInstanceValid(_player))
            return;

        Vector2 toPlayer = GlobalPosition.DirectionTo(_player.GlobalPosition);

        // Only turn while grounded — the leap locks in the direction
        if (!_inAir)
            FaceDirection(toPlayer);

        _leapTimer += delta;

        if (_leapTimer >= LeapCooldown)
        {
            // Calculate steering direction right before the leap
            Vector2 desired = toPlayer
                              + GetSeparation() * SeparationWeight
                              + GetObstacleAvoidance() * WallWeight;

            Vector2 leapDirection = desired.LengthSquared() > 0.001f
                ? desired.Normalized()
                : toPlayer;

            // Snap to the actual leap direction on the frame it launches
            FaceDirection(leapDirection);

            Velocity = leapDirection * LeapForce;
            _leapTimer = 0.0;
            _inAir = true;

            if (Sprite != null)
                Sprite.Frame = 1;   // stretched, mid-air
        }
        else
        {
            // Apply friction to slide to a halt between leaps
            if (Velocity.LengthSquared() > 0)
            {
                Velocity = Velocity.MoveToward(Vector2.Zero, Friction * (float)delta);
            }

            // Land once the slide has mostly stopped
            if (Velocity.Length() < LeapForce * 0.25f)
            {
                _inAir = false;
                if (Sprite != null)
                    Sprite.Frame = 0;
            }
        }

        MoveAndSlide();

        if (GlobalPosition.DistanceTo(_player.GlobalPosition) <= HitRadius)
        {
            if (_player is Player player)
            {
                _dead = true;
                player.TakeDamage(ContactDamage);
                QueueFree();
                return;
            }
        }
    }

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

    private Vector2 GetObstacleAvoidance()
    {
        // Use the intended direction rather than velocity since velocity might be zero between leaps
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

    public async void TakeDamage(int amount)
    {
        if (_dead) return;

        Health -= amount;
        if (Health <= 0)
        {
            _dead = true;
            DropItems();
            
            // Disable collision so it doesn't hit the player or projectiles while dying
            GetNode<CollisionShape2D>("CollisionShape2D").SetDeferred("disabled", true);
        
            // Play the animation and wait for it to finish
            _animatedSprite.Play("death_animation");
            await ToSignal(_animatedSprite, AnimatedSprite2D.SignalName.AnimationFinished);
            
            QueueFree();
        }
    }

    private void DropItems()
    {
        foreach (DropEntry drop in Drops)
        {
            if (drop == null || drop.Scene == null)
                continue;

            if (GD.Randf() > drop.Chance)
                continue;

            var item = drop.Scene.Instantiate<Node2D>();
            Vector2 offset = new Vector2((float)GD.RandRange(-8, 8), (float)GD.RandRange(-8, 8));
            item.Position = GlobalPosition + offset;

            if (drop.ItemData != null && item is WorldItem worldItem)
                worldItem.SetItem(drop.ItemData);

            GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, item);
        }
    }
}