# Inventář – co se změnilo

## Struktura scény

```
CanvasLayer
└─ Inventory (PanelContainer)            ← rámeček, barva, rohy
   └─ MarginContainer                    ← vnitřní odsazení
      └─ HBoxContainer                   ← mezera mezi statsy a sloty
         ├─ StatsPanel (Label)           ← %StatsPanel
         └─ GridContainer                ← %GridContainer, Columns + mezery
            └─ Itemslot … Itemslot7      ← instance GUI/ItemSlot.tscn
```

## Kde co nastavit (všechno přímo v Inspectoru)

| Co chci změnit          | Kde                                                              |
|-------------------------|------------------------------------------------------------------|
| velikost slotu          | `GUI/ItemSlot.tscn` → root → **Custom Minimum Size** (72×72)      |
| okraj ikony ve slotu    | `GUI/ItemSlot.tscn` → `Icon` → offsety (8 px)                     |
| barva / tloušťka rámečku slotu | `GUI/ItemSlot.tscn` → Theme Overrides → Styles → panel     |
| počet sloupců           | `GridContainer` → **Columns**                                     |
| mezery mezi sloty       | `GridContainer` → Theme Overrides → Constants → h/v separation     |
| odsazení uvnitř panelu  | `MarginContainer` → Theme Overrides → Constants → margin_*         |
| vzhled panelu           | `Inventory` → Theme Overrides → Styles → panel                     |
| pozice/šířka panelu     | `Inventory` → Layout → offsety (anchor je Top Wide)                |

Změna v `ItemSlot.tscn` se hned projeví na všech 7 slotech.

## Přidání dalšího slotu

Pravý klik na `GridContainer` → Instantiate Child Scene → `GUI/ItemSlot.tscn`.
Prvních 5 slotů má v Inspectoru `IsEquipmentSlot = true` (equipment bonusy).

## StatsPanel

- `ShowFrame` je defaultně **vypnutý** – ASCII rámeček (`|`, `-`) funguje jen
  s opravdovým monospace fontem. Zapni ho, až ověříš, že `MonospaceBold.ttf`
  monospace opravdu je.
- Hezčí rámeček: StatsPanel → Theme Overrides → Styles → **normal** → New StyleBoxFlat.
- Text už se neořezává (`clip_text` je pryč, min. velikost se počítá z textu).

## Skripty

- `Inventory.cs` – už neřeší layout (žádné `AddThemeConstantOverride` za běhu),
  jen drag & drop. Root je `PanelContainer`, takže se sám roztáhne podle obsahu.
  Nově má `GetSlots()` a `TryAddItem(Item)`.
- `ItemSlot.cs` – odolnější (chybějící `Icon` už nezhodí scénu), sám si nastaví
  `mouse_filter`.
