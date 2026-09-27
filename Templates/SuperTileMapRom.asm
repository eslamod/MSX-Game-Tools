;-----------------------------------------------------------------------------
; {NAME} - example ROM for a map of super tiles exported by MSX Game Tools
;-----------------------------------------------------------------------------
; SPDX-License-Identifier: MIT-0
; Yours to use in your own games as you like: under MIT-0, not even this
; notice has to stay.
;
; Written for {ASSEMBLER}. Assemble it with:
;
;     {COMMAND}
;
; with the exported files next to it:
;
{FILES}
;
; The tile set files are not exported by the map: export the tile set too, and
; keep the names this ROM asks for. Its super tile table comes from there as
; well, which is where it belongs: the table is the tile set's, and every map
; drawn with it shares the same one.
;
; With the assembler output, export it for this same assembler as well -tick the
; example ROM there too, or set the directive in the preferences-. An include of
; a file written for another one stops on its first line. With the binary output
; it makes no difference: bytes are bytes.
;
; What it does:
;   - The same as the ROM of a map up to loading the tile set into VRAM.
;   - Reads the header of the super tile table: width, height and how many, one
;     byte each, with the convention that a count of 0 means 256.
;   - Draws the map resolving every screen cell through the table: a cell of
;     this map is not a tile, it is a whole super tile, and the number it holds
;     is the number of the super tile.
;   - Cursor keys to move, one super tile at a time.
;
; What really gets tested here is the table: that the header tells the truth and
; that the tiles of each super tile are in the order the exporter says, left to
; right and top to bottom. If the order were swapped every super tile would come
; out transposed -and with square super tiles that is exactly what does not show
; at a glance, so it is worth trying with a rectangular one, 2x3 or 3x2, where a
; wrong order throws the whole drawing out.
;
; The cartridge is 32K and takes pages 1 and 2, with the same ENASLT as the ROM
; of a map: the super tile table can be large and 16K would not hold it.
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
NAME_BYTES      {EQU} 768        ; 32x24
KEY_ROW_CURSOR  {EQU} 8          ; the row with the four cursor keys
FRAME_MASK      {EQU} 0x03       ; moves one super tile every four frames
PAD_TILE        {EQU} 0          ; what gets painted outside the map
SUPER_HEADER    {EQU} 3          ; width, height and how many

; --- Variables ---------------------------------------------------------------
; In page 3, which is RAM on any MSX, and below 0xF380, where the work area of
; the work area of the BIOS starts.
CameraX         {EQU} 0xE000     ; map column, in super tiles
CameraY         {EQU} 0xE002
MapWidth        {EQU} 0xE004     ; in super tiles
MapHeight       {EQU} 0xE006
MaxCameraX      {EQU} 0xE008
MaxCameraY      {EQU} 0xE00A
MapCells        {EQU} 0xE00C     ; first cell, past the header of the map
SuperWidth      {EQU} 0xE00E     ; 1 byte: tiles a super tile measures
SuperHeight     {EQU} 0xE00F     ; 1 byte
SuperArea       {EQU} 0xE010     ; 1 byte: width times height
VisibleSupersX  {EQU} 0xE011     ; 1 byte: super tiles that fit across
VisibleSupersY  {EQU} 0xE012     ; 1 byte
FrameCount      {EQU} 0xE013     ; 1 byte
Moved           {EQU} 0xE014     ; 1 byte

; The name table is built here and dumped in one go. Building it in VRAM would mean
; writing each super tile in skipped rows, with an address change per row; this way
; it is one straight write of 768 bytes.
NameBuffer      {EQU} 0xE100     ; 768 bytes

; Where each super tile starts inside the table, worked out already. Done once at
; start-up, and it saves a multiplication for each of the 768 cells.
SuperAddr       {EQU} 0xE400     ; 256 entries of two bytes

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
                call ReadSuperHeader
                call BuildSuperAddr
                call ReadMapHeader
                call DrawMap

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
; The BIOS looks for the "AB" in page 1 and switches that one in, but leaves
; page 2 as it was, which is RAM. It is the routine from the Technical Handbook, the
; same one as in the ROM of a map.
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
                or c
                ld c,a

                inc hl                  ; SLTTBL is four bytes further on
                inc hl
                inc hl
                inc hl
                ld a,(hl)
                and 0x0C
                or c

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
; The di is not here but in the caller: setting the address is two OUTs that the VDP
; counts as a pair, and an interrupt in between resets the flip-flop of port 0x99.
;
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

; HL = source in ROM or RAM, DE = destination in VRAM, BC = length. DE is kept.
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
; Out of a list and not worked out here, the same as in the ROM of a map: that way
; the program knows neither how many tables there are nor how big they are, which is
; what changes between GRAPHIC 2 and GRAPHIC 1. The editor writes the list.
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
; The header of the super tile table
;-----------------------------------------------------------------------------
; Three bytes: width, height and how many. The count is not needed for drawing -the
; map says which super tile goes in each cell- but the width and the height are: out
; of them comes how many tiles each one takes and how many fit on the screen.
ReadSuperHeader:
                ld hl,SuperData
                ld a,(hl)
                ld (SuperWidth),a
                inc hl
                ld a,(hl)
                ld (SuperHeight),a

                ; The area, which is what a super tile takes up in the table.
                ld a,(SuperWidth)
                ld b,a
                ld a,(SuperHeight)
                call MulBytes           ; A = B * A
                ld (SuperArea),a

                ; How many super tiles fit on the screen, across and down.
                ; Division by subtraction: it happens once and the divisor is
                ; eight at most.
                ld a,SCREEN_COLS
                ld hl,SuperWidth
                call DivideByte
                ld (VisibleSupersX),a

                ld a,SCREEN_ROWS
                ld hl,SuperHeight
                call DivideByte
                ld (VisibleSupersY),a
                ret

; A = B * A, with the result in A. Both fit in a byte and so does the product:
; eight by eight is 64.
MulBytes:
                ld c,a          ; C = what gets added up
                xor a           ; and the accumulator starts at zero

                ; If B is zero the product is zero, and djnz would go round 256
                ; times. Checked with inc/dec and not with an or b, which as well
                ; as testing for zero leaves A holding B: the product came out as
                ; b + b*c, and with 2x2 super tiles the area gave 6 instead of 4.
                inc b
                dec b
                ret z
MulNextByte:
                add a,c
                djnz MulNextByte
                ret

; A = A / (HL), by subtraction. It returns at least one: with a super tile wider than
; the screen it shows the one there is and not zero.
DivideByte:
                ld c,(hl)

                ; A side at zero cannot be, but a broken file would bring one
                ; and this would sit subtracting zero forever.
                ld b,a
                ld a,c
                or a
                ld a,b
                jr z,DivideOne

                ld b,0
DivideNext:
                sub c
                jr c,DivideDone
                inc b
                jr DivideNext
DivideDone:
                ld a,b
                or a
                ret nz
DivideOne:
                ld a,1
                ret

;-----------------------------------------------------------------------------
; Where each super tile starts inside the table
;-----------------------------------------------------------------------------
; Worked out once and kept: otherwise drawing the screen would be 768
; multiplications, one per cell. With this each cell is one table read.
BuildSuperAddr:
                ld hl,SuperData
                ld bc,SUPER_HEADER
                add hl,bc               ; HL = the first super tile

                ld de,SuperAddr

                ld a,(SuperArea)
                ld c,a
                ld b,0                  ; BC = what a super tile takes up

                ; 256 times round: the counter starts at zero and ends when it
                ; wraps, which is exactly the ceiling of super tiles a map can
                ; name.
                xor a
BuildNext:
                push af

                ld a,l
                ld (de),a
                inc de
                ld a,h
                ld (de),a
                inc de

                add hl,bc               ; on to the next super tile

                pop af
                inc a
                jr nz,BuildNext
                ret

;-----------------------------------------------------------------------------
; The header of the map
;-----------------------------------------------------------------------------
; The same four bytes as in an ordinary map: width and height, two bytes each with
; the low one first. What changes is the unit, which here is super tiles.
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

                ld hl,0
                ld (CameraX),hl
                ld (CameraY),hl

                ; How far the camera can go: what is left of the map after
                ; what shows. If it fits whole, it does not move.
                ld hl,(MapWidth)
                ld a,(VisibleSupersX)
                call MaxCamera
                ld (MaxCameraX),hl

                ld hl,(MapHeight)
                ld a,(VisibleSupersY)
                call MaxCamera
                ld (MaxCameraY),hl
                ret

; HL = side of the map, A = what shows -> HL = what is left over, or zero.
MaxCamera:
                ld e,a
                ld d,0
                or a
                sbc hl,de
                ret nc
                ld hl,0
                ret

;-----------------------------------------------------------------------------
; Drawing
;-----------------------------------------------------------------------------
; The whole name table is built in RAM and dumped in one go. Each screen cell comes
; out of two reads: the super tile number the map holds and, inside that super tile,
; the tile due by the row and the column it is on. The counters add up instead of
; dividing, which comes to the same thing and does not cost a division per cell.
;
DrawMap:
                ; HL = the first visible cell of the map
                ld de,(MapWidth)
                ld bc,(CameraY)
                call Mul16
                ld de,(CameraX)
                add hl,de
                ld de,(MapCells)
                add hl,de

                ld (RowStart),hl

                ld de,NameBuffer
                ld a,SCREEN_ROWS
                ld (RowsLeft),a
                xor a
                ld (RowInSuper),a

RowLoop:
                ld hl,(RowStart)        ; the row of the map we are on

                ; Offset inside the super tile for the row: width * row.
                ld a,(SuperWidth)
                ld b,a
                ld a,(RowInSuper)
                call MulBytes
                ld (RowOffset),a

                ld a,SCREEN_COLS
                ld (ColsLeft),a
                xor a
                ld (ColInSuper),a

ColLoop:
                push hl                 ; pointer into the map

                ; The address of the super tile in this cell.
                ld l,(hl)
                ld h,0
                add hl,hl               ; two bytes per entry
                ld bc,SuperAddr
                add hl,bc
                ld a,(hl)
                inc hl
                ld h,(hl)
                ld l,a                  ; HL = the first tile of the super tile

                ; Plus the row and the column inside it.
                ld a,(RowOffset)
                ld c,a
                ld a,(ColInSuper)
                add a,c
                ld c,a
                ld b,0
                add hl,bc

                ld a,(hl)               ; the tile number
                ld (de),a               ; into the name table in RAM
                inc de

                pop hl                  ; pointer into the map again

                ; The next column of the super tile, and on to the next one
                ; when it runs out.
                ld a,(ColInSuper)
                inc a
                ld c,a
                ld a,(SuperWidth)
                cp c
                jr nz,ColSame
                xor a                   ; the super tile is over
                inc hl                  ; on to the next cell of the map
                jr ColStore
ColSame:
                ld a,c
ColStore:
                ld (ColInSuper),a

                ld a,(ColsLeft)
                dec a
                ld (ColsLeft),a
                jr nz,ColLoop

                ; The next row of the super tile, and on to the next row of
                ; the map when it runs out.
                ld a,(RowInSuper)
                inc a
                ld c,a
                ld a,(SuperHeight)
                cp c
                jr nz,RowSame

                xor a
                ld (RowInSuper),a

                push de
                ld hl,(RowStart)
                ld de,(MapWidth)
                add hl,de
                ld (RowStart),hl
                pop de
                jr RowNext
RowSame:
                ld a,c
                ld (RowInSuper),a
RowNext:
                ld a,(RowsLeft)
                dec a
                ld (RowsLeft),a
                jr nz,RowLoop

                ; And from RAM into the name table in one write.
                ld hl,NameBuffer
                ld de,NAME_TABLE
                ld bc,NAME_BYTES
                jp CopyToVram

; DE * BC -> HL. Anything past 16 bits is lost, which does not happen here.
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
; The camera, in super tiles
;-----------------------------------------------------------------------------
MoveCamera:
                ld a,(FrameCount)
                inc a
                ld (FrameCount),a
                and FRAME_MASK
                jr z,ReadCursors
                xor a
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
                ret z
                dec hl
                ld (CameraX),hl
                jr MarkMoved

CameraRight:
                ld hl,(CameraX)
                ld de,(MaxCameraX)
                or a
                sbc hl,de
                ret nc
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
; Through the counter of the BIOS: its interrupt routine reads the status register of
; the VDP sixty times a second and clears the flag by reading it, so whoever waits
; for it on their own misses it nearly always.
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
LoadPalette:
                ld a,(MSX_VERSION)
                or a
                ret z                   ; MSX1: no palette

                di

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
; Variables of the drawing
;-----------------------------------------------------------------------------
; ROM cannot be written to, so these live where the others do. Down here and not up
; top so that they sit next to what uses them.
RowStart        {EQU} 0xE020     ; pointer to the row of the map being painted
RowsLeft        {EQU} 0xE022     ; 1 byte
ColsLeft        {EQU} 0xE023     ; 1 byte
RowInSuper      {EQU} 0xE024     ; 1 byte: which row of the super tile we are on
ColInSuper      {EQU} 0xE025     ; 1 byte
RowOffset       {EQU} 0xE026     ; 1 byte: width * RowInSuper, worked out already

;-----------------------------------------------------------------------------
; The data exported by the editor
;-----------------------------------------------------------------------------
; Between labels of its own so that the ROM does not depend on the name given to the
; tile set or to the map. The commented line next to each one is the other output of
; the editor: moving the comment tries that one instead.
PaletteData:
{PALETTE}
PaletteEnd:

{TILESETS}

; The table: three bytes of header and the tiles of each super tile.
SuperData:
{SUPERTILES}
{SUPERTILES_ALT}
SuperEnd:

; The map, with its cells as super tile numbers. A map of super tiles and one of
; tiles cannot be swapped: there a cell is a tile and here it is a whole super
; tile.
MapData:
{MAP}
{MAP_ALT}
MapEnd:

{TAIL}
