using Godot;
using System.Collections.Generic;

public partial class EnemySpawner : Node2D
{
	[Export] public Godot.Collections.Array<SpawnEntry> Entries = new();

	[ExportGroup("Placement")]
	[Export] public TileMap FloorMap;          // drag your floor layer here
	[Export] public int FloorLayerIndex = 0;
	[Export] public float ClearRadius = 16f;
	[Export(PropertyHint.Layers2DPhysics)] public uint BlockedMask = 4; // walls, layer 3
	[Export] public int PlacementAttempts = 30;

	private readonly List<Vector2I> _floorCells = new();
	private float[] _timers;
	private List<Node2D>[] _alive;

	public override void _Ready()
	{
		if (FloorMap == null)
		{
			GD.PrintErr("Spawner: FloorMap is not assigned");
			return;
		}

		_floorCells.AddRange(FloorMap.GetUsedCells(FloorLayerIndex));

		_timers = new float[Entries.Count];
		_alive = new List<Node2D>[Entries.Count];
		for (int i = 0; i < Entries.Count; i++)
			_alive[i] = new List<Node2D>();

		GD.Print($"Spawner: ready, {_floorCells.Count} floor cells, {Entries.Count} entries");
	}

	// Physics process, not _Process — IsClear() queries the physics space,
	// which is only safe to read during the physics step.
	public override void _PhysicsProcess(double delta)
	{
		if (_timers == null || _floorCells.Count == 0)
			return;

		var player = GetTree().GetFirstNodeInGroup("player") as Node2D;
		if (player == null)
			return;

		for (int i = 0; i < Entries.Count; i++)
		{
			SpawnEntry entry = Entries[i];
			if (entry?.Scene == null)
				continue;

			_timers[i] -= (float)delta;
			if (_timers[i] > 0)
				continue;

			_timers[i] = entry.Interval;

			// forget anything that died since last tick
			_alive[i].RemoveAll(n => !IsInstanceValid(n) || n.IsQueuedForDeletion());

			if (_alive[i].Count >= entry.MaxAlive)
				continue;

			if (!TryFindSpawnPoint(player.GlobalPosition, entry, out Vector2 point))
			{
				GD.Print($"Spawner: no valid spot for {entry.Scene.ResourcePath} this tick");
				continue;
			}

			var enemy = entry.Scene.Instantiate<Node2D>();
			AddChild(enemy);
			enemy.GlobalPosition = point;
			_alive[i].Add(enemy);
		}
	}

	private bool TryFindSpawnPoint(Vector2 playerPos, SpawnEntry entry, out Vector2 result)
	{
		float minSq = entry.MinDistanceFromPlayer * entry.MinDistanceFromPlayer;
		float maxSq = entry.MaxDistanceFromPlayer * entry.MaxDistanceFromPlayer;

		for (int i = 0; i < PlacementAttempts; i++)
		{
			Vector2I cell = _floorCells[(int)(GD.Randi() % (uint)_floorCells.Count)];
			Vector2 candidate = FloorMap.ToGlobal(FloorMap.MapToLocal(cell));

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