# MSX Game Tools

*[Leer en español](README.md)*

Graphics and map authoring tools for MSX games: sprites, tile sets, palettes, blocks and
maps, exporting to the formats the VDP expects.

Written in C# on Avalonia 11.3 and .NET 10. Runs on Windows, Linux and macOS.

## What it does

### Sprite banks

- 16x16 sprites in two flavours: MSX, with one colour for the whole sprite, and MSX2,
  with a colour per line and the V9938 CC bit for overlapping planes.
- **Groups**: several patterns placed with offsets to build a figure bigger than a single
  sprite plane allows.
- Reference images behind the canvas, with adjustable opacity, for tracing.
- Exports the pattern table and the attributes, as binary and as `.asm`.

### Tile sets

- 256 tiles of 8x8 for GRAPHIC 2 and 3, with their two colours per line.
- **One set, not three.** The VDP splits the screen into three thirds, each with its own
  tables; here you define one set and the export replicates it, so a tile looks the same
  wherever the map puts it.
- Imports and exports **png**, so the work can move to GIMP or Photoshop. On import it
  checks what the machine cannot do —more than two colours in a line of eight pixels—
  and says which tile and which line is at fault.
- **Blocks**: groups of tiles up to 16x16, arranged as they will look on the map: a 3x3
  tree, or a 2x2 supertile. They work as brushes in the map editor.

### Maps

- Any size up to 1024 per side, with **layers** that are flattened on export.
- Stamp single tiles, rectangles of tiles or whole blocks, with a translucent preview
  under the pointer.
- Select a rectangle and fill it, copy it or clear it.
- Resize with an anchor, and replace ranges of tiles with others.
- **Undo and redo**, twenty steps.
- Reads and writes json, csv (Tiled compatible) and binary; also writes `.asm`.

### The application

- In **Spanish, English and Catalan**, switched live from Preferences, which also stores
  the zoom each area starts at. Settings live in the user's folder, not in the project:
  sharing a project does not change anybody's language. Malformed-file messages stay in
  Spanish: they are diagnostics and only show up when something is broken.
- Menus are grouped **by thing** — Sprites, Tiles, Maps, Palette — like the tree, so what
  you export from tiles sits next to what you import into tiles.
- **Open** is a single entry: it looks at the file and knows whether it is a bank, a set, a
  map or a palette.

### The project and its files

- **Properties** on the tree node, or F2, to rename anything. The file is left alone: what
  a map is called belongs to the map, and where it lives is decided by whoever saves it.

- **Save** writes whatever document is in front — a bank, a tile set or a map — back to
  the file it came from, and only asks for a path the first time.
- Tabs with unsaved work carry an asterisk, and quitting warns about what would be lost,
  offering to save it first.
- The **project** (`.msxproj`) groups everything that is open. It is an index of paths,
  not a file with everything inside: each set, bank and map stays in its own `.json`, and
  anything without a file yet gets one named after it, next to the project. Saving it
  writes whatever has been touched in one go, and opening it brings it all back, each map
  attached to its tile set.

### Palettes

- The fifteen MSX1 colours plus the transparent index, and custom MSX2 palettes from the
  512 colours of the V9938.
- Every tile set and every sprite bank carries its own, stored inside its file: patterns
  are indices, not colours. It is chosen when you create it, in the same form as the name.
  After that, the bar at the top shows the palette of whichever document is in front, and
  picking another there changes that one only.
- Exported in the two-bytes-per-colour format that register 16 expects.

## Running it

```bash
dotnet run
```

The tests:

```bash
dotnet test
```

## Decisions worth knowing

These shape the code the most. They are explained in detail in the comments and in the
commit messages.

**Colour 0 is transparent, in sprites and in tiles.** It shows the border colour
(register 7); it is not just another palette entry. The editor draws it that way so you
see what the machine will show.

**An empty cell is not tile 0.** Tile 0 is a real tile, so blocks and maps tell "nothing
here" apart from "the first tile of the set". When stamping, an empty cell lets whatever
is underneath show through. In csv it is written as `-1`; in binary there is no room for
it, because the name table always draws something, so every map carries a filler tile.

**Layers do not exist on the machine.** They are an authoring aid: on export they are
merged into a single name table, the topmost non-empty cell winning.

**A block and a layer are the same structure.** A block is a small name table and a map
is the same table, bigger, so stamping a block onto a map needs no translation.

**Tiles are always shown in 32 columns.** That is the layout of the editor and of the
png, and the only one where picking a rectangle means anything: a tree drawn across three
rows is only a rectangle if the rows are as wide as they were when it was drawn.

## How it is checked

Around six hundred automated tests, run with the interface actually mounted (Avalonia
headless with Skia) whenever the interface is what is under test.

Two habits that have caught a fair number of bugs:

- **A test you have not seen fail is worth nothing.** Before accepting a fix, the bug is
  put back to check that the test falls. And it has to fall *through the path the user
  takes*: testing the ViewModel on its own has let more than one bug through that lived
  in the entry point.
- **Exporters are validated by actually assembling.** The `.asm` produced is run through
  the sasSX cross-assembler and compared byte for byte against the binary. If the two
  match, the file is good.

## Test ROMs

`msx/test_rom` holds two Z80 assembly ROMs that load the exported data and show it on a
real MSX or on openMSX:

- `sprites_test.asm` — GRAPHIC 3 and sprite mode 2, with the groups laid out. Keys 0-F
  change the border colour and F1 toggles magnification.
- `tileset_test.asm` — GRAPHIC 2 with the tile set replicated across the three thirds.

Both detect at runtime whether they are on an MSX1 or an MSX2, so the palette is only
loaded where it can be. Their README explains the VRAM maps and how to switch between
embedded data and your own files.

## Status

Under development. Sprite banks, tile sets with their blocks, palettes, the map editor and
the project that groups them all work. Animations, behaviours, sounds and music are still
to be done, though they already have their place in the tree.
