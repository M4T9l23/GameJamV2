using Godot;

// Datový popis itemu. Vytváří se jako .tres resource (New Resource -> Item v editoru).
[GlobalClass]
public partial class Item : Resource
{
	// Unikátní identifikátor itemu - podle něj se hlídá, že item nemůže být
	// v inventáři dvakrát zároveň. Prázdné Id = kontrola duplicit se přeskočí
	// (hodí se pro "no-name" testovací itemy).
	[Export] public string Id = "";

	[Export] public string DisplayName = "";

	[Export] public Texture2D Texture;

	// Tagy ve formátu "klic:hodnota", např. "speed:15" nebo "strength:5".
	// Když je item v equipment slotu (prvních 5 slotů inventáře), hodnoty
	// všech jeho tagů se sečtou do PlayerEquipmentBonuses.
	[Export] public string[] Tags = System.Array.Empty<string>();

	// --- genomy ---------------------------------------------------------
	// Prázdná GenomeFamily = normální item, nic z tohohle se na něj nevztahuje.
	//
	// Když má item family vyplněnou, hlídá se, že Jane může mít v inventáři
	// vždycky jen jednu stage dané rodiny - vložení vyšší stage do slotu
	// automaticky smaže tu nižší (viz ItemSlot).
	[ExportGroup("Genom")]
	[Export] public string GenomeFamily = "";

	// 1-based, musí sedět na pořadí ve Genome.Stages.
	[Export] public int Stage = 1;

	public bool IsGenome => !string.IsNullOrEmpty(GenomeFamily);
}
