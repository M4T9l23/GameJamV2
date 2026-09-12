using Godot;

// Genom jedné oblasti. Vytváří se jako .tres (New Resource -> Genome).
//
// Stages je pole Itemů seřazené od nejnižší stage po nejvyšší. Délka je
// libovolná - genom s jednou stagí je prostě pole o jednom prvku.
//
// DŮLEŽITÉ: každý Item ve Stages musí mít:
//   - GenomeFamily = stejná hodnota jako FamilyId zde
//   - Stage        = 1, 2, 3, ... podle pořadí v poli
//   - Id           = unikátní pro každou stage ("fire_1", "fire_2", ...)
//
// Všech pět arén jedné oblasti ukazuje na ten samý .tres.
[GlobalClass]
public partial class Genome : Resource
{
	[Export] public string FamilyId = "";

	[Export] public Godot.Collections.Array<Item> Stages = new();

	public int MaxStage => Stages.Count;

	// Vrátí Item pro danou stage (1-based). Null, když je mimo rozsah.
	public Item GetStage(int stage)
	{
		if (stage < 1 || stage > Stages.Count)
			return null;

		return Stages[stage - 1];
	}
}
