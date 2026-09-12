using Godot;

// Svítící obrys arény. Obkresluje tvar z Region, takže se sám přizpůsobí
// tomu, co máš nakreslené - obdélník i kruh.
//
// Řídí ho ArenaLogic: rozsvítí se při vstupu Jane, pulzuje rychleji, když
// nepřátelé žerou sken, a na konci zeleně blikne a zhasne.
//
// SETUP: přidej do arény Node2D pojmenovaný "Border" a dej mu tenhle
// skript. Víc nic - ArenaLogic si ho najde podle jména a tvar si vezme
// z Region sám.
public partial class ArenaBorder : Node2D
{
	[ExportGroup("Tvar")]
	// Nechej prázdné - vezme se CollisionShape2D z Region vedle.
	[Export] public CollisionShape2D SourceShape;
	// Kolik bodů na kružnici, když je Region kulatý.
	[Export] public int CircleSegments = 64;

	[ExportGroup("Vzhled")]
	[Export] public Color ActiveColor = new Color(1f, 0.15f, 0.15f);
	[Export] public Color CompleteColor = new Color(0.35f, 1f, 0.45f);
	[Export] public float LineWidth = 3f;
	// Falešná záře - kolik širších a průhlednějších čar se přikreslí pod
	// hlavní. Funguje bez WorldEnvironment, takže nemusíš nic nastavovat.
	[Export] public int GlowLayers = 4;
	[Export] public float GlowSpread = 3f;
	[Export] public int Layer = 10;

	[ExportGroup("Pulzování")]
	[Export] public float PulseSpeed = 2.5f;
	// Rychlost pulzu, když nepřátelé stahují sken dolů.
	[Export] public float AlertPulseSpeed = 9f;
	// Jak hluboko pulz stahuje jas. 0 = bez pulzu, 1 = úplně zhasíná.
	[Export] public float PulseDepth = 0.3f;

	[ExportGroup("Náběh")]
	// Jak rychle se rozsvítí a zhasne.
	[Export] public float FadeSpeed = 4f;
	// Jak dlouho svítí zelený záblesk po dokončení skenu.
	[Export] public float CompleteFlashTime = 1.2f;

	private Vector2[] _points = System.Array.Empty<Vector2>();
	private float _intensity;      // aktuální jas 0..1
	private float _targetIntensity;
	private float _phase;
	private bool _alert;
	private Color _color;
	private float _flashTimer;

	public override void _Ready()
	{
		ZIndex = Layer;
		_color = ActiveColor;

		ResolveShape();
		SetProcess(true);
		QueueRedraw();
	}

	private void ResolveShape()
	{
		if (SourceShape != null)
			return;

		// Region je sourozenec - hledáme jeho CollisionShape2D.
		Node parent = GetParent();
		Node region = parent?.GetNodeOrNull("Region");

		if (region == null)
		{
			GD.PushWarning($"ArenaBorder '{Name}': nenasel jsem Region, obrys se nevykresli.");
			return;
		}

		foreach (Node child in region.GetChildren())
		{
			if (child is CollisionShape2D shape)
			{
				SourceShape = shape;
				return;
			}
		}

		GD.PushWarning($"ArenaBorder '{Name}': Region nema CollisionShape2D.");
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_flashTimer > 0f)
		{
			_flashTimer -= dt;
			if (_flashTimer <= 0f)
			{
				_color = ActiveColor;
				_targetIntensity = 0f;
			}
		}

		// Exponenciální náběh - nezávislý na framerate.
		_intensity = Mathf.Lerp(_intensity, _targetIntensity, 1f - Mathf.Exp(-FadeSpeed * dt));

		if (_intensity < 0.01f && _targetIntensity <= 0f)
		{
			if (Visible)
			{
				Hide();
				_intensity = 0f;
			}
			return;
		}

		if (!Visible)
			Show();

		_phase += dt * (_alert ? AlertPulseSpeed : PulseSpeed);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_intensity <= 0.01f)
			return;

		RebuildPoints();

		if (_points.Length < 2)
			return;

		// Pulz stahuje jas nahoru a dolů kolem plné hodnoty.
		float pulse = 1f - PulseDepth * (0.5f - 0.5f * Mathf.Cos(_phase));
		float alpha = _intensity * pulse;

		// Nejdřív široké a průhledné vrstvy, nahoru pak ostrá linka.
		for (int i = GlowLayers; i >= 1; i--)
		{
			var glow = new Color(_color, alpha * 0.18f / i);
			DrawPolyline(_points, glow, LineWidth + i * GlowSpread, true);
		}

		DrawPolyline(_points, new Color(_color, alpha), LineWidth, true);
	}

	// Body počítáme v lokálním prostoru shapu a převádíme do prostoru
	// tohohle nodu, takže sedí i když je Region posunutý nebo otočený.
	private void RebuildPoints()
	{
		if (SourceShape?.Shape == null)
		{
			_points = System.Array.Empty<Vector2>();
			return;
		}

		Transform2D t = GlobalTransform.AffineInverse() * SourceShape.GlobalTransform;

		switch (SourceShape.Shape)
		{
			case RectangleShape2D rect:
			{
				Vector2 h = rect.Size * 0.5f;
				_points = new[]
				{
					t * new Vector2(-h.X, -h.Y),
					t * new Vector2(h.X, -h.Y),
					t * new Vector2(h.X, h.Y),
					t * new Vector2(-h.X, h.Y),
					t * new Vector2(-h.X, -h.Y),
				};
				break;
			}

			case CircleShape2D circle:
			{
				int steps = Mathf.Max(8, CircleSegments);
				var pts = new Vector2[steps + 1];

				for (int i = 0; i <= steps; i++)
				{
					float a = Mathf.Tau * i / steps;
					pts[i] = t * new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * circle.Radius;
				}

				_points = pts;
				break;
			}

			case CapsuleShape2D capsule:
			{
				// Aproximace obdélníkem - pro obrys arény to stačí.
				Vector2 h = new Vector2(capsule.Radius, capsule.Height * 0.5f);
				_points = new[]
				{
					t * new Vector2(-h.X, -h.Y),
					t * new Vector2(h.X, -h.Y),
					t * new Vector2(h.X, h.Y),
					t * new Vector2(-h.X, h.Y),
					t * new Vector2(-h.X, -h.Y),
				};
				break;
			}

			default:
				_points = System.Array.Empty<Vector2>();
				break;
		}
	}

	// --- volá ArenaLogic -------------------------------------------------

	public void SetActive(bool active)
	{
		_flashTimer = 0f;
		_color = ActiveColor;
		_targetIntensity = active ? 1f : 0f;

		if (active)
			Show();
	}

	// Zrychlí pulz, když nepřátelé stahují sken dolů.
	public void SetAlert(bool alert)
	{
		_alert = alert;
	}

	// Zelený záblesk po dokončení, pak zhasne.
	public void FlashComplete()
	{
		_color = CompleteColor;
		_alert = false;
		_targetIntensity = 1f;
		_flashTimer = CompleteFlashTime;
	}
}
