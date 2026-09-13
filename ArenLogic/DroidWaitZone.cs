using Godot;

// Oblast, kam droid za Jane nechodí. Když do ní vejde, zaparkuje na
// WaitPoint (nebo na okraji zóny) a počká, až vyjde ven.
//
// Aréna má přednost - když zrovna drží vchod nebo doručuje item, čekání
// se uplatní až potom.
//
// SETUP VE SCÉNĚ:
//   DroidWaitZone (Area2D, tenhle skript)
//     +-- CollisionShape2D (přes celou oblast)
//     +-- WaitPoint (Marker2D, volitelně - kam si droid stoupne)
public partial class DroidWaitZone : Area2D
{
	// Kam droid doletí a počká. Nechej prázdné a použije se pozice
	// téhle zóny - hoď ji tedy ke vchodu, ne doprostřed místnosti.
	[Export] public Marker2D WaitPoint;

	// Hláška, kterou droid řekne, když zůstane stát.
	[Export(PropertyHint.MultilineText)] public string WaitMessage = "";

	public override void _Ready()
	{
		WaitPoint ??= GetNodeOrNull<Marker2D>("WaitPoint");

		Monitoring = true;
		CollisionMask = 7;

		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (GetTree().GetFirstNodeInGroup("droid") is not Droid droid)
			return;

		droid.WaitAt(WaitPoint?.GlobalPosition ?? GlobalPosition);

		if (!string.IsNullOrEmpty(WaitMessage))
			droid.Say(WaitMessage);

		GD.Print($"DroidWaitZone '{Name}': droid ceka venku.");
	}

	private void OnBodyExited(Node2D body)
	{
		if (!body.IsInGroup("player"))
			return;

		if (GetTree().GetFirstNodeInGroup("droid") is Droid droid)
			droid.StopWaiting();
	}
}
