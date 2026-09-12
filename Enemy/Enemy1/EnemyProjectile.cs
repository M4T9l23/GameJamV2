using Godot;

public partial class EnemyProjectile : Area2D
{
    [Export] public float Speed = 220f;
    [Export] public int Damage = 1;
    [Export] public float Lifetime = 4f;
    // "physical", "fire" nebo "water" - Jane na to aplikuje odolnosti.
    [Export] public string DamageType = "physical";

    // Set by the enemy right before it spawns us.
    public Vector2 Direction = Vector2.Right;
    public Node Shooter;

    private bool _hasHit;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        Rotation = Direction.Angle();

        GetTree().CreateTimer(Lifetime).Timeout += () =>
        {
            if (IsInstanceValid(this)) QueueFree();
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        GlobalPosition += Direction * Speed * (float)delta;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hasHit || body == Shooter) return;

        // Don't let enemies shoot each other in the back.
        if (body.IsInGroup("enemies")) return;

        _hasHit = true;

        if (body is Player player)
            player.TakeTypedDamage(Damage, DamageType);
        else if (body is IDamageable damageable)
            damageable.TakeDamage(Damage);
        else if (body.HasMethod("TakeDamage"))
            body.Call("TakeDamage", Damage);

        QueueFree(); // also dies on walls, which is what we want
    }
}