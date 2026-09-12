using Godot;

public partial class Player : CharacterBody2D
{
    [Export] public int MaxHealth = 5;
    [Export] public PackedScene BulletScene;
    [Export] public float FireRate = 1.00f;
    [Export] public float SpriteAngleOffsetDegrees = 180f;
    
    public int Health;
    private const float Speed = 200f;
    private Vector2 _facing = Vector2.Right;
    private bool _canShoot = true;
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        Health = MaxHealth;
        AddToGroup("player");
    }
    
    public void TakeDamage(int amount)
    {
        Health -= amount;
        GD.Print($"Player HP: {Health}/{MaxHealth}");

        if (Health <= 0)
            Die();
    }
    
    private void Die()
    {
        GD.Print("Player died");
        GetTree().ReloadCurrentScene(); // restart the level for now
    }
    
    public override void _PhysicsProcess(double delta)
    {
        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Velocity = input * Speed;

        if (input != Vector2.Zero)
        {
            _animatedSprite.Play("move_animation");
            _facing = input.Normalized();
            
            _animatedSprite.Rotation = _facing.Angle() + Mathf.DegToRad(SpriteAngleOffsetDegrees);
        }
        else
        {
            _animatedSprite.Play("idle_animation");
        }

        MoveAndSlide();

        if (Input.IsActionPressed("shoot") && _canShoot)
            Shoot();
    }

    private async void Shoot()
    {
        _canShoot = false;

        var bullet = BulletScene.Instantiate<Attack1>();
        bullet.Direction = _facing;
        bullet.Rotation = _facing.Angle();
        bullet.Shooter = this;
        GetTree().CurrentScene.AddChild(bullet);
        bullet.GlobalPosition = GlobalPosition;

        await ToSignal(GetTree().CreateTimer(FireRate), SceneTreeTimer.SignalName.Timeout);
        _canShoot = true;
    }
}