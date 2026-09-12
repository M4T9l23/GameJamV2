using Godot;
using System;

public partial class MainMenu : Control
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		// The path must exactly match the hierarchy in your Scene tab
		Button startButton = GetNode<Button>("VBoxContainer/Button");
		startButton.Pressed += OnStartButtonPressed;
	}

	private void OnStartButtonPressed()
	{
		// Replace "res://MainScene.tscn" with the actual path to your main scene
		GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}