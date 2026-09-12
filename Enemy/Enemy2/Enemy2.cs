using Godot;

// The bare minimum an enemy needs to work with EnemySpawner:
//   - be a Node2D (CharacterBody2D counts)
//   - AddToGroup("enemies") in _Ready, so the spawner can count it
public partial class Enemy2 : CharacterBody2D
{
    [Export] public int Health = 3;
    [Export] public int ContactDamage = 1;
    [Export] public float Speed = 80f;

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

        Velocity = GlobalPosition.DirectionTo(_player.GlobalPosition) * Speed;
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

    public void TakeDamage(int amount)
    {
        if (_dead) return;

        Health -= amount;
        if (Health <= 0)
        {
            _dead = true;
            QueueFree();
        }
    }
}