using Godot;
using System.Collections.Generic;

public partial class DeathScreen : CanvasLayer
{
	private Label _title;
	private readonly List<Label> _options = new();
	private readonly List<string> _labels = new();
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

		var box = GetNodeOrNull<VBoxContainer>("Panel/VBoxContainer");
		if (box == null)
		{
			GD.PushError($"DeathScreen '{Name}': chybi Panel/VBoxContainer. " +
				"Ukazuje PauseOnEsc -> Menu Scene na spravnou scenu?");
			return;
		}

		_title = box.GetNodeOrNull<Label>("Label");

		// Posbirat kolik Option* labelu ve scene opravdu je. Driv se ctyri
		// cetly natvrdo a kdyz jeden chybel, _Ready spadl a od te chvile
		// kazdy stisk klavesy hodil NullReferenceException.
		foreach (Node child in box.GetChildren())
		{
			if (child is Label label && label.Name.ToString().StartsWith("Option"))
				_options.Add(label);
		}

		if (_options.Count == 0)
		{
			GD.PushError($"DeathScreen '{Name}': ve VBoxContainer nejsou zadne Labely 'Option*'.");
			return;
		}

		if (_isDeathMode)
		{
			if (_title != null) _title.Text = "[JaneSteel ~]$ Status: Dead";
			_labels.AddRange(new[] { "Restart level", "Unstack", "Credits", "Exit" });
		}
		else
		{
			if (_title != null) _title.Text = "[JaneSteel ~]$ Status: Paused";
			_labels.AddRange(new[] { "Resume", "Restart level", "Unstack", "Exit" });
			GetTree().Paused = true;
		}

		// Vic voleb nez labelu = prebytecne zahodit, min = prebytecne skryt.
		while (_labels.Count > _options.Count)
			_labels.RemoveAt(_labels.Count - 1);

		for (int i = _labels.Count; i < _options.Count; i++)
			_options[i].Hide();

		Refresh();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Kdyz _Ready neprosel, nesmime se tvarit, ze menu funguje.
		if (_labels.Count == 0)
			return;

		if (@event.IsActionPressed("ui_down"))
		{
			_index = (_index + 1) % _labels.Count;
			Refresh();
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("ui_up"))
		{
			_index = (_index - 1 + _labels.Count) % _labels.Count;
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
		for (int i = 0; i < _labels.Count; i++)
		{
			_options[i].Show();
			_options[i].Text = (i == _index ? "> " : "  ") + _labels[i];
		}
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

			case "Unstack":
				if (GetTree().GetFirstNodeInGroup("player") is Player p)
					p.Unstack();
				else
					GD.PushWarning("DeathScreen: zadny node ve skupine 'player'.");
				Resume();
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
