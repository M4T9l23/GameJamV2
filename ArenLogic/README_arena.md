# Aréna, droid a genomy

## Co kam patří

| Soubor | Co to je |
|---|---|
| `ArenLogic/ArenaLogic.cs` | logika arény – sken, spawn, odměna, respawn |
| `ArenLogic/Droid.cs` | companion, jeden na celou hru |
| `ArenLogic/Genome.cs` | resource – rodina genomu a její stage |
| `ArenLogic/Arena.tscn` | šablona arény, nakopíruj pro každou z pěti |
| `ArenLogic/Droid.tscn` | droid s barem skenu |
| `GUI/Items/Genomes/` | hotové genomy Fire a Water ze spritů v `Assets/Sprites/genes/` |

Změněné oproti původnímu projektu: `GUI/Item.cs`, `GUI/ItemSlot.cs`,
`GUI/Inventory.cs`, `Player/Player.cs`, `Scenes/DeathScreen.cs`.

## Zprovoznění

1. Do `main.tscn` přidej **jednu** instanci `ArenLogic/Droid.tscn`.
2. Pro každou arénu instancuj `ArenLogic/Arena.tscn`, posuň markery
   tak, aby seděly na místnost, a **natáhni `Entrance` přes celý
   průchod** – viz níž.
3. V inspektoru arény nastav `Reward` na příslušný `.tres` z
   `GUI/Items/Genomes/` a naplň `EnemyScenes` scénami nepřátel.

Markery se propojují automaticky podle jména (`Region`, `Entrance`,
`Respawn`, `Exit`, `RewardPoint`, `Spawn*`), takže v inspektoru nic
přetahovat nemusíš. Cokoliv vyplníš ručně má přednost.

Volitelně můžeš přidat marker `DroidPark`, když má droid stát jinde
než uprostřed vchodu.

`GlobalSpawner` se taky dohledá sám – hledá první `EnemySpawner` mezi
dětmi aktuální scény.

## Jak to běží

Jane vejde do `Region` → droid doletí doprostřed `Entrance`. Jakmile
dorazí a v průchodu nikdo nestojí, `Entrance` se zapne jako pevná zeď
a neprojde tudy Jane ani nepřátelé. Aréna začne spawnovat po
`SpawnInterval` na náhodný `Spawn*` marker, strop je `MaxAlive`.

Bar nad droidem roste o `ScanRate` %/s, když je aréna prázdná, a klesá
o `DrainPerEnemy` %/s **za každého** nepřítele uvnitř. Na nule se
zastaví. Na 100 % droid uvolní vstup a na `RewardPoint` spadne genom.

Nepřátelé se počítají přes překryv s `Region`, ne podle toho, kdo je
spawnul – takže se započítá i ten, co se do arény připotácel zvenku.

## Vyznačení vchodu

`Entrance` je `StaticBody2D` s `CollisionShape2D`. Natáhni ten obdélník
přes celý průchod včetně rámu – co nakreslíš, to se zablokuje. Klidně
ho i otoč, pokud je vchod ze strany.

Shape nech v editoru **disabled** a vrstvu na 7 (stejná jako `Wall.tscn`,
takže se tomu nepřátelé vyhýbají stejně jako zdi). Skript si obojí
stejně nastaví sám.

Blokace se zapne až ve chvíli, kdy droid dorazí **a** v průchodu nikdo
nestojí – jinak by se Jane nebo nepřítel zasekli uvnitř statického
tělesa. Vypne se při dokončení skenu i při opuštění arény.

Droid sám žádnou kolizi nemá, je to jen vizuál s barem.

## Smrt v aréně

Death screen dostane dvě volby navíc: **Respawn in arena** (nepřátelé
se smažou, sken padá na nulu, Jane se objeví na `Respawn`) a **Leave
arena** (to samé, ale Jane skončí na `Exit`, droid uvolní vstup a aréna
se vrátí do výchozího stavu).

`RespawnHealth = -1` znamená plné HP.

**`Exit` musí ležet mimo `Region`**, jinak se aréna hned po opuštění
znovu aktivuje a Jane se zacyklí.

## Genomy

`Genome.tres` drží `FamilyId` a pole stagí. Každý `Item` v tom poli musí
mít `GenomeFamily` shodnou s `FamilyId`, `Stage` podle pořadí (1-based)
a unikátní `Id`.

Aréna při dokončení najde v inventáři nejvyšší stage své rodiny a shodí
tu následující. Když Jane nemá žádnou, dostane první. Když už má
maximální, nepadne nic (pokud nezapneš `GiveRewardWhenMaxed`).

V inventáři nikdy nemůžou být dvě stage jedné rodiny naráz – vložení
vyšší stage do slotu tu nižší automaticky smaže i s jejími bonusy.

Vygenerované genomy mají **4 stage**, protože tolik je spritů. Tagy
(`strength` u Fire, `speed` u Water) jsou placeholdery na doladění.
