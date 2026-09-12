using Godot;
using System.Text;

// [Tool] = text se vykreslí i v editoru, ne až po spuštění hry.
//
// Rámeček kolem textu (ShowFrame) je poskládaný ze znaků '-' a '|', takže
// dává smysl JEN s opravdovým monospace fontem. Když font monospace není,
// okraje se rozjedou a vypadá to jako "roztřepené čárky" - proto je
// ShowFrame defaultně vypnutý. Hezčí a spolehlivější rámeček uděláš přes
// Inspector: StatsPanel -> Theme Overrides -> Styles -> normal (StyleBoxFlat).
[Tool]
public partial class StatsPanel : Label
{
	private string _playerName = "Jane Steeler";
	private int _maxHp = 10;
	private int _currentHp = 5;
	private string _location = "E-404";
	private int _barLength = 20;
	private bool _showFrame = false;

	[Export]
	public string PlayerName
	{
		get => _playerName;
		set { _playerName = value; Refresh(); }
	}

	[Export]
	public int MaxHp
	{
		get => _maxHp;
		set { _maxHp = Mathf.Max(0, value); Refresh(); }
	}

	[Export]
	public int CurrentHp
	{
		get => _currentHp;
		set { _currentHp = value; Refresh(); }
	}

	[Export]
	public string Location
	{
		get => _location;
		set { _location = value; Refresh(); }
	}

	[Export(PropertyHint.Range, "4,60,1")]
	public int BarLength
	{
		get => _barLength;
		set { _barLength = Mathf.Max(1, value); Refresh(); }
	}

	// Zapni jen když máš na Labelu skutečný monospace font.
	[Export]
	public bool ShowFrame
	{
		get => _showFrame;
		set { _showFrame = value; Refresh(); }
	}

	public override void _Ready()
	{
		// Tohle byly hlavní důvody, proč se text ve hře ořezával:
		// clip_text = true + pevná custom_minimum_size + zalamování.
		ClipText = false;
		AutowrapMode = TextServer.AutowrapMode.Off;
		TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming;
		// Deferred, protože Inventory/CanvasLayer se může načíst dřív než hráč.
		CallDeferred(nameof(HookPlayer));
	}

	private void HookPlayer()
	{
		if (GetTree().GetFirstNodeInGroup("player") is not Player player)
		{
			GD.Print("StatsPanel: hráč není ve skupině 'player' (zatím).");
			return;
		}

		player.HealthChanged += OnHealthChanged;
		OnHealthChanged(player.Health, player.MaxHealth);
	}

	private void OnHealthChanged(int current, int max)
	{
		CurrentHp = current;
		MaxHp = max;

		Refresh();
	}

	// Zavolej z Player/GameState skriptu při změně staty, např.:
	// statsPanel.SetStats("Jane Steeler", player.CurrentHp, player.MaxHp, currentRoomName);
	public void SetStats(string playerName, int currentHp, int maxHp, string location)
	{
		_playerName = playerName;
		_currentHp = currentHp;
		_maxHp = Mathf.Max(0, maxHp);
		_location = location;
		Refresh();
	}

	public void SetHp(int currentHp, int maxHp)
	{
		_currentHp = currentHp;
		_maxHp = Mathf.Max(0, maxHp);
		Refresh();
	}

	private void Refresh()
	{
		int filled = _maxHp > 0 && _currentHp > 0
			? Mathf.Clamp(Mathf.CeilToInt((float)_currentHp / _maxHp * _barLength), 1, _barLength)
			: 0;

		string bar = new string('#', filled) + new string('-', _barLength - filled);

		string[] lines =
		{
			$"NAME: {_playerName}",
			$"HP[{bar}]",
			$"LOCATION: {_location}",
		};

		if (!_showFrame)
		{
			Text = string.Join('\n', lines);
			return;
		}

		int width = 0;
		foreach (string line in lines)
			width = Mathf.Max(width, line.Length);

		var sb = new StringBuilder();
		sb.Append('+').Append('-', width).Append("+\n");
		foreach (string line in lines)
			sb.Append('|').Append(line.PadRight(width)).Append("|\n");
		sb.Append('+').Append('-', width).Append('+');

		Text = sb.ToString();
	}
}
