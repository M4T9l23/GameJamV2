using Godot;

// Autoload, který řeší přepínání mezi:
//  - herním módem (myš zachycená/schovaná, hráč reaguje na WASD apod.)
//  - inventářovým módem (myš volná a viditelná, hráč se nehýbe, dá se klikat v UI)
public partial class InventoryModeController : Node
{
    [Export] public NodePath InventoryUIPath; // cesta na kořenový Control tvého inventáře (nastavíš v editoru)
    [Export] public NodePath PlayerPath;      // cesta na node hráče (nastavíš v editoru)

    private Control _inventoryUI;
    private Node _player;
    private bool _inventoryOpen = false;

    public override void _Ready()
    {
        _inventoryUI = GetNode<Control>(InventoryUIPath);
        _player = GetNode<Node>(PlayerPath);

        SetInventoryMode(false); // hra začíná v herním módu, inventář je zavřený
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // "toggle_inventory" je vlastní Input Action, kterou si vytvoříš v Project Settings (viz návod)
        if (@event.IsActionPressed("toggle_inventory"))
        {
            SetInventoryMode(!_inventoryOpen);
        }
    }

    private void SetInventoryMode(bool open)
    {
        _inventoryOpen = open;
        _inventoryUI.Visible = open; // schová/zobrazí celé UI inventáře

        // v inventáři chceme volnou myš, ve hře třeba zachycenou - uprav podle typu tvé hry
        // (pokud u tebe hráč ovládá myší kameru/směr, tohle je klíčové)
        Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

        // vypneme zpracování vstupu a fyziky u hráče, aby se při otevřeném inventáři nehýbal
        _player.SetProcessUnhandledInput(!open);
        _player.SetPhysicsProcess(!open);

        // UI musí "brát" klikání myší jen když je vidět, jinak by blokovalo hru na pozadí
        _inventoryUI.MouseFilter = open ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
    }
}
