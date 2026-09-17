;-----------------------------------------------------------------------------
; {NAME} - example ROM for a map exported by MSX Game Tools
;-----------------------------------------------------------------------------
; Written for {ASSEMBLER}. Assemble it with:
;
;     {COMMAND}
;
; with the exported files next to it:
;
{FILES}
;
; The tile set files are not exported by the map: export the tile set too, and
; keep the names this ROM asks for. Without the tile set a map is nothing, it is
; only indices.
;
; With the assembler output, export it for this same assembler as well -tick the
; example ROM there too, or set the directive in the preferences-. An include of
; a file written for another one stops on its first line. With the binary output
; it makes no difference: bytes are bytes.
;
; What it does:
;   - Sets {MODE} ({SCREEN} in MSX Basic) and loads the tile set into VRAM.
;   - Reads the size of the map out of its header, which is the first four bytes
;     of the file: two of width and two of height, low byte first.
;   - Draws the 32x24 cells that fit, from wherever the camera says.
;   - Cursor keys to move, if the map is bigger than the screen. If it fits
;     whole it does not move, and what is left over stays as tile 0.
;
; The cartridge is 32K and takes pages 1 and 2. The BIOS switches page 1 in,
; where it finds the "AB", but leaves page 2 as it was, which is RAM: the first
; thing this ROM does is hook itself in there with ENASLT, and that is what
; EnablePage2 is about.
;
; The palette travels inside this file, as 32 bytes further down. It is only
; loaded on an MSX2 and up: on an MSX1 the 16 colours are fixed.
;-----------------------------------------------------------------------------

; --- VDP ports ---------------------------------------------------------------
VDP_DATA        {EQU} 0x98       ; VRAM data
VDP_ADDR        {EQU} 0x99       ; VRAM address and registers

; --- BIOS --------------------------------------------------------------------
SNSMAT          {EQU} 0x0141     ; A = keyboard matrix row -> A, bit at 0 = pressed
MSX_VERSION     {EQU} 0x002D     ; 0 = MSX1, 1 = MSX2, 2 = MSX2+, 3 = TurboR
JIFFY           {EQU} 0xFC9E     ; the VDP interrupt counter, in RAM
ENASLT          {EQU} 0x0024     ; A = slot, H = page -> switches it in
RSLREG          {EQU} 0x0138     ; -> A = the primary slot register
EXPTBL          {EQU} 0xFCC1     ; one byte per slot: bit 7 if it is expanded
SLTTBL          {EQU} 0xFCC5     ; one byte per slot: which subslot is in

; --- Palette -----------------------------------------------------------------
VDP_PALETTE     {EQU} 0x9A       ; the port the colours go in through
PALETTE_BYTES   {EQU} 32         ; 16 colours of two bytes

; --- VRAM layout, the usual one ----------------------------------------------
PATTERN_TABLE   {EQU} 0x0000     ; 6144: three thirds of 2048
NAME_TABLE      {EQU} 0x1800     ;  768: 32x24 cells
COLOR_TABLE     {EQU} 0x2000     ; 6144: three thirds of 2048

; --- Constants ---------------------------------------------------------------
TABLE_LOADS     {EQU} {TABLE_LOADS}          ; blocks that go into VRAM at start-up
SCREEN_COLS     {EQU} 32
SCREEN_ROWS     {EQU} 24
KEY_ROW_CURSOR  {EQU} 8          ; the row with the four cursor keys
FRAME_MASK      {EQU} 0x03       ; moves one cell every four frames
PAD_TILE        {EQU} 0          ; what gets painted outside the map

; --- Variables ---------------------------------------------------------------
; In page 3, which is RAM on any MSX, and below 0xF380, where the work area of
; the BIOS starts. A cartridge cannot write to itself, so the camera has to live
; here.
CameraX         {EQU} 0xE000     ; map column against the left edge
CameraY         {EQU} 0xE002     ; map row against the top edge
MapWidth        {EQU} 0xE004
MapHeight       {EQU} 0xE006
MaxCameraX      {EQU} 0xE008     ; how far it can go without running off
MaxCameraY      {EQU} 0xE00A
MapCells        {EQU} 0xE00C     ; first cell, past the header
VisibleCols     {EQU} 0xE00E     ; 1 byte: map columns that show
VisibleRows     {EQU} 0xE00F     ; 1 byte
PadCols         {EQU} 0xE010     ; 1 byte: what is left over up to 32
PadRows         {EQU} 0xE011     ; 1 byte: what is left over up to 24
FrameCount      {EQU} 0xE012     ; 1 byte
Moved           {EQU} 0xE013     ; 1 byte: whether the camera has moved

;-----------------------------------------------------------------------------
; Cartridge header
;-----------------------------------------------------------------------------
; The "AB" and the four vectors the BIOS goes looking for in page 1.
{HEADER}

;-----------------------------------------------------------------------------
; Start
;-----------------------------------------------------------------------------
{START}:
                call EnablePage2
                call SetupVdp
                call LoadPalette
                call LoadTables
                call ReadMapHeader
                call DrawMap

; It only redraws when the camera has moved: that is 768 bytes through VRAM and
; there is no reason to write them sixty times a second if nothing changes. It
; also shows when the movement does not arrive, instead of hiding it.
MainLoop:
                call WaitFrame
                call MoveCamera
                or a
                jr z,MainLoop
                call DrawMap
                jr MainLoop

;-----------------------------------------------------------------------------
; The upper half of the cartridge
;-----------------------------------------------------------------------------
; The very first thing, because from here on there is data above 0x8000 and
; without this it would not be there.
;
; The BIOS looks for the "AB" in page 1 and switches that one in, but leaves
; page 2 as it was, which is RAM: a 32K cartridge does not see its own upper
; half until it hooks itself in. Here it works out which slot it is in -by
; looking at which slot is in page 1, which is where it is running- and puts
; that same one into page 2.
;
; It is the routine from the Technical Handbook, as it stands. The detour
; through EXPTBL and SLTTBL is because the slot can be expanded into subslots,
; and then the primary number is not enough to name it.
;
; If some machine or emulator had already switched page 2 in, this puts the same
; slot back and nothing happens.
EnablePage2:
                call RSLREG             ; the primary slots of the four pages
                rrca                    ; bits 2 and 3: the one of page 1
                rrca
                and 0x03
                ld c,a
                ld b,0

                ld hl,EXPTBL            ; is that slot expanded?
                add hl,bc
                ld c,a                  ; the primary one, saved
                ld a,(hl)
                and 0x80
                or c                    ; with the expanded bit if it is
                ld c,a

                inc hl                  ; SLTTBL is four bytes further on
                inc hl
                inc hl
                inc hl
                ld a,(hl)               ; which subslot is in right now
                and 0x0C
                or c                    ; that is the whole slot now

                ld h,0x80               ; an address in page 2
                jp ENASLT

;-----------------------------------------------------------------------------
; VDP registers
;-----------------------------------------------------------------------------
SetupVdp:
                ld hl,VdpSetup
SetupNext:
                ld a,(hl)
                cp 0xFF
                ret z
                ld b,a          ; register number
                inc hl
                ld a,(hl)       ; value
                inc hl
                push hl
                call WriteVdp
                pop hl
                jr SetupNext

; The same values as the ROM of the tile set, so that both set the machine up
; the same way and the only difference between the two is what gets written
; into the name table.
VdpSetup:
                {DB}  0, {R0}    ; screen mode
                {DB}  1, 0xE0    ; 16K, screen on, VDP interrupt
                {DB}  2, 0x06    ; name table             0x1800
                {DB}  3, {R3}    ; colour table           0x2000
                {DB}  4, {R4}    ; pattern table          0x0000
                {DB}  5, 0x36    ; sprite attributes      0x1B00
                {DB}  6, 0x07    ; sprite patterns        0x3800
                {DB}  7, 0x01    ; border colour
                {DB} 0xFF        ; end of table

; A = value, B = register number
WriteVdp:
                di
                out (VDP_ADDR),a
                ld a,b
                or 0x80
                out (VDP_ADDR),a
                ei
                ret

;-----------------------------------------------------------------------------
; Getting to VRAM
;-----------------------------------------------------------------------------
; Setting the address is two OUTs that the VDP counts as a pair, and it leaves a
; transfer open on top of that. If an interrupt slips in between them, the BIOS
; routine reads the status register and resets the flip-flop of port 0x99: the
; second OUT then gets read as the first one of another pair. That is why the di
; is not here but in the caller, which is the one that knows where it ends.
;
; HL = VRAM address (14 bits)
SetVramWrite:
                ld a,l
                out (VDP_ADDR),a
                ld a,h
                and 0x3F
                or 0x40         ; write bit
                out (VDP_ADDR),a
                ret

; HL = source in ROM, DE = destination in VRAM, BC = length. DE is kept.
CopyToVram:
                di
                ex de,hl        ; HL = destination
                call SetVramWrite
                ex de,hl        ; HL = source again
CopyNext:
                ld a,(hl)
                out (VDP_DATA),a
                inc hl
                dec bc
                ld a,b
                or c
                jr nz,CopyNext
                ei
                ret

;-----------------------------------------------------------------------------
; The tables of the tile set, into the screen thirds
;-----------------------------------------------------------------------------
; Same as in the ROM of a tile set: in GRAPHIC 2 every screen third has its own
; pattern table and its own colour table. Here it matters even more, because the
; map uses the same tile at the top and at the bottom and it has to look the
; same in all three.
; Out of a list and not worked out here, because what goes into each third is
; not always the same: a map can carry a different tile set in each screen
; third, and then it is three pattern tables and three colour tables instead of
; the same one copied three times. The editor writes the list; this only walks
; it.
LoadTables:
                ld ix,TableList
                ld b,TABLE_LOADS
NextLoad:
                push bc

                ld l,(ix+0)     ; source
                ld h,(ix+1)
                ld e,(ix+2)     ; destination in VRAM
                ld d,(ix+3)
                ld c,(ix+4)     ; length
                ld b,(ix+5)

                push ix
                call CopyToVram
                pop ix

                ld bc,6         ; on to the next entry
                add ix,bc

                pop bc
                djnz NextLoad
                ret

; Source, destination and length of each block that goes into VRAM, six bytes
; per entry.
TableList:
{TABLES}

;-----------------------------------------------------------------------------
; The header of the map
;-----------------------------------------------------------------------------
; The first four bytes of the file: width and height, two bytes each with the
; low one first. They are read one after the other and HL is left pointing at
; the first cell, which is exactly what the format says: the header, then rows.
;
; On the way it works out how much map shows and how much is left over. From
; here and not from every redraw because it never changes, and because this way
; the drawing loop compares nothing per cell.
ReadMapHeader:
                ld hl,MapData
                ld e,(hl)
                inc hl
                ld d,(hl)
                inc hl
                ld (MapWidth),de

                ld e,(hl)
                inc hl
                ld d,(hl)
                inc hl
                ld (MapHeight),de

                ld (MapCells),hl

                ; The camera starts at the top left corner.
                ld hl,0
                ld (CameraX),hl
                ld (CameraY),hl

                ; Width: either it fits whole, or there is room to move about in.
                ld hl,(MapWidth)
                ld de,SCREEN_COLS
                or a                    ; no carry from before
                sbc hl,de
                jr nc,WideMap

                ld hl,0                 ; narrower than the screen
                ld (MaxCameraX),hl
                ld a,(MapWidth)         ; it fits in a byte: it is less than 32
                ld (VisibleCols),a
                ld b,a
                ld a,SCREEN_COLS
                sub b
                ld (PadCols),a
                jr HeaderRows

WideMap:
                ld (MaxCameraX),hl      ; width - 32
                ld a,SCREEN_COLS
                ld (VisibleCols),a
                xor a
                ld (PadCols),a

HeaderRows:
                ld hl,(MapHeight)
                ld de,SCREEN_ROWS
                or a
                sbc hl,de
                jr nc,TallMap

                ld hl,0                 ; shorter than the screen
                ld (MaxCameraY),hl
                ld a,(MapHeight)        ; it fits in a byte: it is less than 24
                ld (VisibleRows),a
                ld b,a
                ld a,SCREEN_ROWS
                sub b
                ld (PadRows),a
                ret

TallMap:
                ld (MaxCameraY),hl      ; height - 24
                ld a,SCREEN_ROWS
                ld (VisibleRows),a
                xor a
                ld (PadRows),a
                ret

;-----------------------------------------------------------------------------
; Drawing the window that shows
;-----------------------------------------------------------------------------
; The whole name table in one go: 768 bytes is about three milliseconds and it
; happens right after the retrace, so the picture does not tear. Redrawing all
; of it instead of moving what is there is more work for the VDP, but it leaves
; the code with no state that can drift, which is worth more here.
DrawMap:
                ; first visible cell = cells + cameraY * width + cameraX
                ld de,(MapWidth)
                ld bc,(CameraY)
                call Mul16
                ld de,(CameraX)
                add hl,de
                ld de,(MapCells)
                add hl,de

                di
                ex de,hl                ; DE = source in ROM
                ld hl,NAME_TABLE
                call SetVramWrite
                ex de,hl                ; HL = source again

                ld a,(VisibleRows)
                ld b,a
RowNext:
                push bc
                push hl

                ld a,(VisibleCols)
                ld b,a
CellNext:
                ld a,(hl)
                out (VDP_DATA),a
                inc hl
                djnz CellNext

                ; Whatever is missing up to 32 columns, if the map is narrow.
                ; With a jump and not a plain djnz: djnz with B at zero goes
                ; round 256 times and would fill the screen with padding.
                ld a,(PadCols)
                or a
                jr z,RowDone
                ld b,a
                ld a,PAD_TILE
PadColNext:
                out (VDP_DATA),a
                djnz PadColNext

RowDone:
                pop hl
                ld de,(MapWidth)        ; on to the next row of the map
                add hl,de
                pop bc
                djnz RowNext

                ; And the rows below the map, if there is screen left over.
                ld a,(PadRows)
                or a
                jr z,DrawDone
                ld b,a
PadRowNext:
                push bc
                ld b,SCREEN_COLS
                ld a,PAD_TILE
PadRowCell:
                out (VDP_DATA),a
                djnz PadRowCell
                pop bc
                djnz PadRowNext

DrawDone:
                ei
                ret

; DE * BC -> HL. Anything past 16 bits is lost, which does not happen here: the
; whole map has to fit in the cartridge, so the largest possible offset is
; around 28000.
Mul16:
                ld hl,0
MulNext:
                ld a,b
                or c
                ret z
                srl b
                rr c
                jr nc,MulSkip
                add hl,de
MulSkip:
                sla e
                rl d
                jr MulNext

;-----------------------------------------------------------------------------
; The camera
;-----------------------------------------------------------------------------
; Returns A other than zero if it has moved, which is what decides whether to
; redraw.
;
; One cell every four frames: at one per frame thirty two columns go by in half
; a second and there is no stopping where you want.
MoveCamera:
                ld a,(FrameCount)
                inc a
                ld (FrameCount),a
                and FRAME_MASK
                jr z,ReadCursors
                xor a                   ; not this frame
                ret

; Row 8 of the matrix: bit 4 left, bit 5 up, bit 6 down and bit 7 right. A bit
; at zero is the key pressed.
ReadCursors:
                xor a
                ld (Moved),a

                ld a,KEY_ROW_CURSOR
                call KeyRow
                ld c,a

                bit 4,c
                jr nz,NoLeft
                call CameraLeft
NoLeft:
                bit 7,c
                jr nz,NoRight
                call CameraRight
NoRight:
                bit 5,c
                jr nz,NoUp
                call CameraUp
NoUp:
                bit 6,c
                jr nz,NoDown
                call CameraDown
NoDown:
                ld a,(Moved)
                ret

CameraLeft:
                ld hl,(CameraX)
                ld a,h
                or l
                ret z                   ; already against the edge
                dec hl
                ld (CameraX),hl
                jr MarkMoved

CameraRight:
                ld hl,(CameraX)
                ld de,(MaxCameraX)
                or a
                sbc hl,de
                ret nc                  ; already at the limit
                ld hl,(CameraX)
                inc hl
                ld (CameraX),hl
                jr MarkMoved

CameraUp:
                ld hl,(CameraY)
                ld a,h
                or l
                ret z
                dec hl
                ld (CameraY),hl
                jr MarkMoved

CameraDown:
                ld hl,(CameraY)
                ld de,(MaxCameraY)
                or a
                sbc hl,de
                ret nc
                ld hl,(CameraY)
                inc hl
                ld (CameraY),hl

MarkMoved:
                ld a,1
                ld (Moved),a
                ret

; A = row -> A = state. The BIOS is saved from whole, just in case.
KeyRow:
                push bc
                push de
                push hl
                push ix
                push iy
                call SNSMAT
                pop iy
                pop ix
                pop hl
                pop de
                pop bc
                ret

;-----------------------------------------------------------------------------
; Waiting for the next frame
;-----------------------------------------------------------------------------
; Through the counter of the BIOS and not by reading the status register of the
; VDP: the interrupt routine of the BIOS reads it sixty times a second and
; clears the flag by reading it, so whoever waits for it on their own misses it
; nearly always. The low byte of JIFFY changes on every interrupt, and that is
; all it takes.
WaitFrame:
                ld hl,JIFFY
                ld a,(hl)
FrameNext:
                cp (hl)
                jr z,FrameNext
                ret

;-----------------------------------------------------------------------------
; The palette, only if the machine has one
;-----------------------------------------------------------------------------
; Byte 0x002D of the BIOS says the version: 0 is MSX1, 1 is MSX2, 2 is MSX2+ and
; 3 is TurboR. On an MSX1 the 16 colours are fixed and there is nothing to load.
LoadPalette:
                ld a,(MSX_VERSION)
                or a
                ret z                   ; MSX1: no palette

                di

                ; R#16 = 0: the index of the colour about to be written. It moves
                ; on by itself with each pair of bytes, so setting it once is enough.
                xor a
                out (VDP_ADDR),a
                ld a,0x90               ; 0x80 + 16
                out (VDP_ADDR),a

                ld hl,PaletteData
                ld b,PALETTE_BYTES
PaletteNext:
                ld a,(hl)
                out (VDP_PALETTE),a
                inc hl
                djnz PaletteNext

                ei
                ret

;-----------------------------------------------------------------------------
; The data exported by the editor
;-----------------------------------------------------------------------------
; Between labels of its own so that the ROM does not depend on the name given to
; the map or to the tile set, which is where the exporter's labels come from.
; The commented line next to each one is the other output of the editor: moving
; the comment tries that one instead.
PaletteData:
{PALETTE}
PaletteEnd:

{TILESETS}

; The four byte header comes inside the file, so MapData points at the width and
; the cells start four bytes further on. Not worked out with a constant: it is
; ReadMapHeader walking past it, which is how a format with a header is read and
; is what keeps the two from drifting apart.
MapData:
{MAP}
{MAP_ALT}
MapEnd:

{TAIL}
