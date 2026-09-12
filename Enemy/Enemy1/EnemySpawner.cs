using Godot;
using System.Collections.Generic;

public partial class EnemySpawner : Node2D
{
	[Export] public PackedScene EnemyScene;
	[Export] public float SpawnInterval = 3f;
	[Export] public int MaxEnemies = 15;

	[ExportGroup("Placement")]
	[Export] public TileMapLayer FloorLayer;          // drag your floor layer here
	[Export] public float MinDistanceFromPlayer = 350f;
	[Export] public float MaxDistanceFromPlayer = 800f;
	[Export] public float ClearRadius = 16f;          // roughly the enemy's body radius
	[Export(PropertyHint.Layers2DPhysics)] public uint BlockedMask = 4; // walls, layer 3
	[Export] public int PlacementAttempts = 30;

	private readonly List<Vector2I> _floorCells = new();
	private float _timer;

	public override void _Ready()
	{
		if (FloorLayer == null)
		{
			GD.PrintErr("Spawner: FloorLayer is not assigned");
			return;
		}

		_floorCells.AddRange(FloorLayer.GetUsedCells());
		GD.Print($"Spawner: ready, {_floorCells.Count} floor cells");
	}

	public override void _Process(double delta)
	{
		_timer -= (float)delta;
		if (_timer > 0)
			return;

		_timer = SpawnInterval;

		if (GetTree().GetNodesInGroup("enemies").Count >= MaxEnemies)
			return;

		SpawnEnemy();
	}

	private void SpawnEnemy()
	{
		if (EnemyScene == null || _floorCells.Count == 0)
			return;

		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null)
			return;

		if (!TryFindSpawnPoint(player.GlobalPosition, out Vector2 spawnPoint))
		{
			GD.Print("Spawner: no valid floor position found this tick");
			return;
		}

		var enemy = EnemyScene.Instantiate<Node2D>();
		AddChild(enemy);
		enemy.GlobalPosition = spawnPoint;
	}

	private bool TryFindSpawnPoint(Vector2 playerPos, out Vector2 result)
	{
		float minSq = MinDistanceFromPlayer * MinDistanceFromPlayer;
		float maxSq = MaxDistanceFromPlayer * MaxDistanceFromPlayer;

		for (int i = 0; i < PlacementAttempts; i++)
		{
			Vector2I cell = _floorCells[(int)(GD.Randi() % (uint)_floorCells.Count)];
			Vector2 candidate = FloorLayer.ToGlobal(FloorLayer.MapToLocal(cell));

			float distSq = candidate.DistanceSquaredTo(playerPos);
			if (distSq < minSq || distSq > maxSq)
				continue;

			if (!IsClear(candidate))
				continue;

			result = candidate;
			return true;
		}

		result = Vector2.Zero;
		return false;
	}

	// Is there room for a body here, or is a wall in the way?
	private bool IsClear(Vector2 point)
	{
		var shape = new CircleShape2D { Radius = ClearRadius };

		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = shape,
			Transform = new Transform2D(0f, point),
			CollisionMask = BlockedMask,
			CollideWithBodies = true,
			CollideWithAreas = false,
		};

		return GetWorld2D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
	}
}
