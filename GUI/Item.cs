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
}
