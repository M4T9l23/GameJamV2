using Godot;

public partial class Attack1 : Area2D
{
    [Export] public float Speed = 400f;
    [Export] public int Damage = 1;
    public Vector2 Direction = Vector2.Right;
    public Node Shooter; // so the bullet doesn't hit whoever fired it

    private bool _hasHit;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;

        // clean up missed shots after 3 seconds
        GetTree().CreateTimer(3.0).Timeout += () =>
        {
            if (IsInstanceValid(this)) QueueFree();
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * Speed * (float)delta;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_hasHit || body == Shooter) return;
        _hasHit = true;

        if (body is Enemy enemy)
            enemy.TakeDamage(Damage);

        QueueFree(); // walls, enemies, anything solid
    }
}