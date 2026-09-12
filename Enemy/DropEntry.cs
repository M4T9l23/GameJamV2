using Godot;

[GlobalClass]
public partial class DropEntry : Resource
{
	[Export] public PackedScene Item;
	[Export(PropertyHint.Range, "0,1,0.01")] public float Chance = 0.5f; // 0.5 = 50%
}
