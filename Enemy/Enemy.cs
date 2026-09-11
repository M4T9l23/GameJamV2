using Godot;

public partial class Enemy : StaticBody2D
{
    [Export] public int Health = 3;

    public void TakeDamage(int amount)
    {
        Health -= amount;
        if (Health <= 0)
            QueueFree();
    }
}