using Godot;

public partial class EnemySpawner : Node2D
{
    [Export] public PackedScene EnemyScene;
    [Export] public float SpawnInterval = 2f;
    [Export] public float SpawnDistance = 400f;

    private float _timer;

    public override void _Ready()
    {
        GD.Print("Spawner: ready");
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta;
        if (_timer > 0)
            return;

        _timer = SpawnInterval;
        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
        if (player == null)
        {
            GD.Print("Spawner: no player found in group 'player'");
            return;
        }

        if (EnemyScene == null)
        {
            GD.Print("Spawner: EnemyScene is not assigned");
            return;
        }

        var enemy = EnemyScene.Instantiate<Node2D>();
        AddChild(enemy);

        Vector2 offset = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * SpawnDistance;
        enemy.GlobalPosition = player.GlobalPosition + offset;

        GD.Print($"Spawner: spawned enemy at {enemy.GlobalPosition}");
    }
}