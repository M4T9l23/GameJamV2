using Godot;
using System.Text;

// ASCII ukazatel skenu do inventáře, ve stejném stylu jako StatsPanel.
// Sám se schová, když Jane není v aktivní aréně.
//
// Patří do Inventory.tscn mezi StatsPanel a GridContainer.
public partial class ScanBar : Label
{
	[Export] public int BarLength = 14;
	[Export] public bool ShowFrame = true;
	// Ukázat řádek s vlnou.
	[Export] public bool ShowWave = true;
	// Ukázat počet nepřátel uvnitř.
	[Export] public bool ShowHostiles = true;

	private ArenaLogic _arena;
	private string _last = "";

	public override void _Ready()
	{
		Hide();
	}

	public override void _Process(double delta)
	{
		_arena = GetTree().GetFirstNodeInGroup("active_arena") as ArenaLogic;

		if (_arena == null || !IsInstanceValid(_arena))
		{
			if (Visible)
				Hide();

			return;
		}

		if (!Visible)
			Show();

		string text = Build();

		// Label prekresluj jen pri zmene, ne kazdy frame.
		if (text == _last)
			return;

		_last = text;
		Text = text;
	}

	private string Build()
	{
		int filled = Mathf.Clamp(
			Mathf.FloorToInt(_arena.ScanProgress / 100f * BarLength), 0, BarLength);

		string bar = new string('#', filled) + new string('-', BarLength - filled);

		var lines = new System.Collections.Generic.List<string>
		{
			$"SCAN[{bar}]",
			$"{Mathf.FloorToInt(_arena.ScanProgress),3}%",
		};

		if (ShowWave)
			lines.Add($"WAVE: {_arena.CurrentWave}/{_arena.WaveCount}");

		if (ShowHostiles)
			lines.Add($"HOSTILES: {_arena.EnemiesInside}");

		if (!ShowFrame)
			return string.Join('\n', lines);

		int width = 0;
		foreach (string line in lines)
			width = Mathf.Max(width, line.Length);

		var sb = new StringBuilder();
		sb.Append('+').Append('-', width).Append("+\n");

		foreach (string line in lines)
			sb.Append('|').Append(line.PadRight(width)).Append("|\n");

		sb.Append('+').Append('-', width).Append('+');

		return sb.ToString();
	}
}
