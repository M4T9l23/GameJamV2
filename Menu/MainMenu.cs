using Godot;
using System;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        Button startButton = GetNode<Button>("VBoxContainer/Button");
        startButton.Pressed += OnStartButtonPressed;

        Button creditsButton = GetNode<Button>("VBoxContainer/Button2");
        creditsButton.Pressed += OnCreditsButtonPressed;
    }

    private void OnStartButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    private void OnCreditsButtonPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Credits.tscn");
    }
}