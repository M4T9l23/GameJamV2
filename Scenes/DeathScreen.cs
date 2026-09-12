using Godot;
using System.Collections.Generic;

public partial class DeathScreen : CanvasLayer
{
	// Nastavuje Player.Die() těsně před AddChild. Null = Jane umřela mimo
	// arénu a menu vypadá jako dřív.
	public ArenaLogic Arena;

	private enum Choice
	{
		RespawnInArena,
		LeaveArena,
		RestartLevel,
		MainMenu,
		Credits,
	}

	private readonly List<Label> _pool = new();
	private readonly List<Choice> _choices = new();
	private int _index;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;   // ← must be before the pause

		CollectLabels();
		BuildChoices();
		Refresh();

		GetTree().Paused = true;
	}

	// Posbírá Labely pojmenované Option* z VBoxContaineru. Když jich je míň,
	// než potřebujeme, doduplikuje první (aby se zachovalo nastavení fontu).
	private void CollectLabels()
	{
		var box = GetNodeOrNull<VBoxContainer>("Panel/VBoxContainer");
		if (box == null)
		{
			GD.PushError("DeathScreen: chybi Panel/VBoxContainer.");
			return;
		}

		foreach (Node child in box.GetChildren())
		{
			if (child is Label label && label.Name.ToString().StartsWith("Option"))
				_pool.Add(label);
		}

		if (_pool.Count == 0)
			GD.PushError("DeathScreen: ve VBoxContaineru nejsou zadne Labely 'Option*'.");
	}

	private void BuildChoices()
	{
		_choices.Clear();

		if (Arena != null && IsInstanceValid(Arena))
		{
			_choices.Add(Choice.RespawnInArena);
			_choices.Add(Choice.LeaveArena);
		}

		_choices.Add(Choice.RestartLevel);
		_choices.Add(Choice.MainMenu);
		_choices.Add(Choice.Credits);

		EnsureLabelCount(_choices.Count);
	}

	private void EnsureLabelCount(int needed)
	{
		if (_pool.Count == 0)
			return;

		Label template = _pool[0];
		Node parent = template.GetParent();

		while (_pool.Count < needed)
		{
			var extra = (Label)template.Duplicate();
			extra.Name = $"Option{_pool.Count}";
			parent.AddChild(extra);
			_pool.Add(extra);
		}

		// Přebytečné schovat (mimo arénu jsou volby jen tři).
		for (int i = needed; i < _pool.Count; i++)
			_pool[i].Hide();
	}

	private static string LabelFor(Choice choice) => choice switch
	{
		Choice.RespawnInArena => "Respawn in arena",
		Choice.LeaveArena     => "Leave arena",
		Choice.RestartLevel   => "Restart level",
		Choice.MainMenu       => "Main menu",
		Choice.Credits        => "Credits",
		_ => "?",
	};

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_choices.Count == 0)
			return;

		if (@event.IsActionPressed("ui_down"))
		{
			_index = (_index + 1) % _choices.Count;
			Refresh();
		}
		else if (@event.IsActionPressed("ui_up"))
		{
			_index = (_index - 1 + _choices.Count) % _choices.Count;
			Refresh();
		}
		else if (@event.IsActionPressed("ui_accept"))
		{
			Select();
		}
	}

	private void Refresh()
	{
		for (int i = 0; i < _choices.Count && i < _pool.Count; i++)
		{
			_pool[i].Show();
			_pool[i].Text = (i == _index ? "> " : "  ") + LabelFor(_choices[i]);
		}
	}

	private void Select()
	{
		GetTree().Paused = false; // always unpause before changing scene

		switch (_choices[_index])
		{
			case Choice.RespawnInArena:
				Arena.RespawnPlayerHere();
				QueueFree();
				break;

			case Choice.LeaveArena:
				Arena.AbandonArena();
				QueueFree();
				break;

			case Choice.RestartLevel:
				GetTree().ReloadCurrentScene();
				break;

			case Choice.MainMenu:
				GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn");
				break;

			case Choice.Credits:
				GetTree().ChangeSceneToFile("res://scenes/Credits.tscn");
				break;
		}
	}
}
