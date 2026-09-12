using Godot;

public partial class DeathScreen : CanvasLayer
{
    private Label[] _options;
    private string[] _labels = { "Restart level", "Main menu", "Credits" };
    private int _index = 0;

    public override void _Ready()
    {
        _options = new Label[]
        {
            GetNode<Label>("Panel/VBoxContainer/Option0"),
            GetNode<Label>("Panel/VBoxContainer/Option1"),
            GetNode<Label>("Panel/VBoxContainer/Option2"),
        };
        Refresh();
        GetTree().Paused = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_down"))
        {
            _index = (_index + 1) % _options.Length;
            Refresh();
        }
        else if (@event.IsActionPressed("ui_up"))
        {
            _index = (_index - 1 + _options.Length) % _options.Length;
            Refresh();
        }
        else if (@event.IsActionPressed("ui_accept"))
        {
            Select();
        }
    }

    private void Refresh()
    {
        for (int i = 0; i < _options.Length; i++)
            _options[i].Text = (i == _index ? "> " : "  ") + _labels[i];
    }

    private void Select()
    {
        GetTree().Paused = false; // always unpause before changing scene
        switch (_index)
        {
            case 0: GetTree().ReloadCurrentScene(); break;
            case 1: GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn"); break;
            case 2: GetTree().ChangeSceneToFile("res://scenes/Credits.tscn"); break;
        }
    }
}