using Godot;

public partial class DeathScreen : CanvasLayer
{
	private Label _title;
	private Label[] _options;
	private string[] _labels;
	private int _index = 0;
	private bool _isDeathMode = true;

	// Call right after Instantiate(), before AddChild().
	// true  = death screen (no pause)
	// false = pause menu (pauses the tree)
	public void Setup(bool deathMode)
	{
		_isDeathMode = deathMode;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_title = GetNode<Label>("Panel/VBoxContainer/Label");
		_options = new Label[]
		{
			GetNode<Label>("Panel/VBoxContainer/Option0"),
			GetNode<Label>("Panel/VBoxContainer/Option1"),
			GetNode<Label>("Panel/VBoxContainer/Option2"),
			GetNode<Label>("Panel/VBoxContainer/Option3"),
		};

		if (_isDeathMode)
		{
			_title.Text = "[JaneSteel ~]$ Status: Dead";
			_labels = new string[] { "Restart level", "Leave arena", "Credits", "Exit" };
		}
		else
		{
			_title.Text = "[JaneSteel ~]$ Status: Paused";
			_labels = new string[] { "Resume", "Restart level", "Leave arena", "Exit" };
			GetTree().Paused = true;
		}

		Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_down"))
		{
			_index = (_index + 1) % _options.Length;
			Refresh();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_up"))
		{
			_index = (_index - 1 + _options.Length) % _options.Length;
			Refresh();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_accept"))
		{
			Select();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_cancel") && !_isDeathMode)
		{
			// Escape closes the pause menu, but never the death screen
			Resume();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Refresh()
	{
		for (int i = 0; i < _options.Length; i++)
			_options[i].Text = (i == _index ? "> " : "  ") + _labels[i];
	}

	private void Resume()
	{
		GetTree().Paused = false;
		QueueFree();
	}

	private void Select()
	{
		switch (_labels[_index])
		{
			case "Resume":
				Resume();
				break;

			case "Restart level":
				GetTree().Paused = false;
				GetTree().ReloadCurrentScene();
				break;

			case "Leave arena":
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
				break;

			case "Credits":
				GetTree().Paused = false;
				GetTree().ChangeSceneToFile("res://Scenes/Credits.tscn");
				break;

			case "Exit":
				GetTree().Quit();
				break;
		}
	}
}