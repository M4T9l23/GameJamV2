using Godot;

[Tool]
public partial class Text : Control
{
    private string _message = "Press tab to drag items to your inv.";

    [Export(PropertyHint.MultilineText)]
    public string Message
    {
        get => _message;
        set { _message = value; UpdateLabel(); }
    }

    public override void _Ready() => UpdateLabel();

    private void UpdateLabel()
    {
        var label = GetNodeOrNull<Label>("PanelContainer/Label");
        if (label == null)
        {
            GD.PushWarning($"Label not found from {Name}");
            return;
        }
        label.Text = _message;
    }
}