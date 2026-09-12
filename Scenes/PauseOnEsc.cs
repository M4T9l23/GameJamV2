using Godot;

public partial class PauseOnEsc : Node
{
    [Export] private PackedScene _menuScene;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (GetTree().Paused) return;   // menu already open, it handles its own Escape

        var menu = _menuScene.Instantiate<DeathScreen>();
        menu.Setup(false);
        GetTree().CurrentScene.AddChild(menu);
        GetViewport().SetInputAsHandled();
    }
}