using Godot;

// One of these per enemy type. [GlobalClass] makes it appear in the
// "New Resource" dropdown in the inspector.
[GlobalClass]
public partial class SpawnEntry : Resource
{
    [Export] public PackedScene Scene;

    [ExportGroup("How often")]
    [Export] public float Interval = 3f;      // seconds between spawns
    [Export] public int MaxAlive = 15;        // cap for this type only

    [ExportGroup("How far")]
    [Export] public float MinDistanceFromPlayer = 350f;
    [Export] public float MaxDistanceFromPlayer = 800f;
}