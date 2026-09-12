using Godot;

public partial class Credits : Button
{
    private void _on_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }
}