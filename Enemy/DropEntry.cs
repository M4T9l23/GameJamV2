using Godot;

[GlobalClass]
public partial class DropEntry : Resource
{
	[Export] public PackedScene Scene;
	[Export] public Item ItemData;
	[Export(PropertyHint.Range, "0,1,0.01")] public float Chance = 0.5f;
}
