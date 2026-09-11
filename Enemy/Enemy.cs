using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int Health = 3;
    [Export] public int ContactDamage = 1;
    [Export] public float Speed = 80f;

    private Node2D _player;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player == null || !IsInstanceValid(_player))
            return;

        Velocity = GlobalPosition.DirectionTo(_player.GlobalPosition) * Speed;
        MoveAndSlide(); // stops at walls and slides along them

        // Check if we bumped into the player
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

    public void TakeDamage(int amount)
    {
        Health -= amount;
        GD.Print($"Enemy HP: {Health}");

        if (Health <= 0)
            QueueFree();
    }
}