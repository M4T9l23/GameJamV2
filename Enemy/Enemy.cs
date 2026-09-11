using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int Health = 3;
    [Export] public int ContactDamage = 1;
    [Export] public float Speed = 80f;
    [Export] public Godot.Collections.Array<DropEntry> Drops = new();

    [ExportGroup("Facing")]
    [Export] public Node2D Visual;                 // the sprite/polygon to turn
    [Export] public bool FlipInsteadOfRotate = false;
    [Export] public float RotationOffset = 0f;     // degrees, use if your art doesn't face right

    private Node2D _player;
    private bool _dead;

    public override void _Ready()
    {
        AddToGroup("enemies");
        _player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        GD.Print($"Enemy ready. Player found: {_player != null}, Visual set: {Visual != null}");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_dead || _player == null || !IsInstanceValid(_player))
            return;

        Vector2 direction = GlobalPosition.DirectionTo(_player.GlobalPosition);
        FaceDirection(direction);

        Velocity = direction * Speed;
        MoveAndSlide();

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            if (GetSlideCollision(i).GetCollider() is Player player)
            {
                player.TakeDamage(ContactDamage);
                QueueFree(); // no drops when it crashes into the player
                return;
            }
        }
    }

    private void FaceDirection(Vector2 direction)
    {
        if (Visual == null || direction == Vector2.Zero)
            return;

        if (FlipInsteadOfRotate)
        {
            // Mirror left/right, keep the sprite upright
            float x = Mathf.Abs(Visual.Scale.X);
            Visual.Scale = new Vector2(direction.X < 0 ? -x : x, Visual.Scale.Y);
        }
        else
        {
            // Point the sprite at the player
            Visual.Rotation = direction.Angle() + Mathf.DegToRad(RotationOffset);
        }
    }

    public void TakeDamage(int amount)
    {
        if (_dead) return;

        Health -= amount;
        GD.Print($"Enemy HP: {Health}");

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