;-----------------------------------------------------------------------------
; {NAME} - example ROM for a tile set exported by MSX Game Tools
;-----------------------------------------------------------------------------
; Written for {ASSEMBLER}. Assemble it with:
;
;     {COMMAND}
;
; with the exported files next to it:
;
{FILES}
;
; What it does:
;   - Sets {MODE} ({SCREEN} in MSX Basic).
;   - Copies the pattern table and the colour table into VRAM. In GRAPHIC 2
;     every screen third has its own pair of tables, so the single set of 256
;     tiles the editor exports has to be copied three times: that is the part
;     the exported bytes cannot say by themselves. In GRAPHIC 1 there is one
;     table of each and the colour one is 32 bytes, one per group of 8 tiles.
;   - Fills the name table with 0..255 repeated. A third is exactly 32x8 = 256
;     cells, so each third shows the whole set laid out like the grid in the
;     editor. If the copies were wrong, the second and third thirds would come
;     out as garbage.
;   - Keys 0-9 and A-F change the border colour.
;
; The palette travels inside this file, as 32 bytes further down. It is only
; loaded on an MSX2 and up: on an MSX1 the 16 colours are fixed.
;-----------------------------------------------------------------------------

; --- VDP ports ---------------------------------------------------------------
VDP_DATA        {EQU} 0x98       ; VRAM data
VDP_ADDR        {EQU} 0x99       ; VRAM address and registers
VDP_PALETTE     {EQU} 0x9A       ; the port the colours go in through

; --- BIOS --------------------------------------------------------------------
SNSMAT          {EQU} 0x0141     ; A = keyboard matrix row -> A, bit at 0 = pressed
MSX_VERSION     {EQU} 0x002D     ; 0 = MSX1, 1 = MSX2, 2 = MSX2+, 3 = TurboR

; --- VRAM layout, the usual one ----------------------------------------------
PATTERN_TABLE   {EQU} 0x0000
NAME_TABLE      {EQU} 0x1800     ; 768: 32x24 cells
COLOR_TABLE     {EQU} 0x2000

; --- Sizes -------------------------------------------------------------------
PATTERN_BYTES   {EQU} 2048       ; 256 tiles of 8 lines
COLOR_BYTES     {EQU} {COLOR_BYTES}
TABLE_COPIES    {EQU} {TABLE_COPIES}          ; one per screen third in GRAPHIC 2
NAME_BYTES      {EQU} 768
PALETTE_BYTES   {EQU} 32         ; 16 colours of two bytes

;-----------------------------------------------------------------------------
; Cartridge header
;-----------------------------------------------------------------------------
                {ORG} 0x4000

                {DB} "AB"
                {DW} Start       ; INIT
                {DW} 0           ; STATEMENT
                {DW} 0           ; DEVICE
                {DW} 0           ; TEXT
                {DB} 0,0,0,0,0,0 ; reserved

;-----------------------------------------------------------------------------
; Start
;-----------------------------------------------------------------------------
Start:
                call SetupVdp
                call LoadPalette
                call LoadTables
                call FillNames

MainLoop:
                call ScanColorKeys
                jr MainLoop

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

; The classic values for this screen mode. In GRAPHIC 2 the ones for R#3 and
; R#4 are not plain addresses: those two registers also carry the mask that
; splits the tables into the three screen thirds.
VdpSetup:
                {DB}  0, {R0}    ; screen mode
                {DB}  1, 0xE0    ; 16K, screen on, VDP interrupt
                {DB}  2, 0x06    ; name table         0x1800
                {DB}  3, {R3}    ; colour table       0x2000
                {DB}  4, {R4}    ; pattern table      0x0000
                {DB}  5, 0x36    ; sprite attributes  0x1B00
                {DB}  6, 0x07    ; sprite patterns    0x3800
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
; is not here but in the caller, which is the one that knows where the transfer
; ends.
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
; The two tables
;-----------------------------------------------------------------------------
LoadTables:
                ld hl,PatternsData
                ld de,PATTERN_TABLE
                ld bc,PATTERN_BYTES
                call CopyTables

                ld hl,ColorsData
                ld de,COLOR_TABLE
                ld bc,COLOR_BYTES
                call CopyTables
                ret

; HL = source, DE = first destination, BC = length of one table
CopyTables:
                ld a,TABLE_COPIES
CopyNextTable:
                push af
                push hl
                push bc

                call CopyToVram

                pop bc
                pop hl

                ; DE is still the destination: step it on by one table.
                push hl
                ld h,d
                ld l,e
                add hl,bc
                ex de,hl
                pop hl

                pop af
                dec a
                jr nz,CopyNextTable
                ret

;-----------------------------------------------------------------------------
; The name table: 0..255 repeated
;-----------------------------------------------------------------------------
; A third is exactly 32x8 = 256 cells, so every third shows the whole set laid
; out like the grid in the editor. The inc a only wraps round past 255.
FillNames:
                di
                ld hl,NAME_TABLE
                call SetVramWrite

                ld bc,NAME_BYTES
                xor a
NameNext:
                out (VDP_DATA),a
                inc a
                dec bc
                ld d,a
                ld a,b
                or c
                ld a,d
                jr nz,NameNext
                ei
                ret

;-----------------------------------------------------------------------------
; Keyboard
;-----------------------------------------------------------------------------
; Matrix rows: 0 = "0".."7", 1 = "8","9" in bits 0 and 1, 2 = "A" and "B" in
; bits 6 and 7, 3 = "C".."F" in bits 0..3.
ScanColorKeys:
                xor a
                call KeyRow
                ld c,0
                call ScanRow

                ld a,1
                call KeyRow
                or 0xFC         ; the rest of the row is of no interest
                ld c,8
                call ScanRow

                ld a,3
                call KeyRow
                or 0xF0
                ld c,12
                call ScanRow

                ld a,2
                call KeyRow
                bit 6,a
                jr nz,NoKeyA
                push af
                ld a,10
                call SetBorder
                pop af
NoKeyA:
                bit 7,a
                jr nz,NoKeyB
                ld a,11
                call SetBorder
NoKeyB:
                ret

; A = state of the row (bit at 0 = pressed), C = colour of bit 0
ScanRow:
                ld b,8
ScanBit:
                rrca
                push af
                jr c,ScanNext   ; at 1 it is not pressed
                push bc
                ld a,c
                call SetBorder
                pop bc
ScanNext:
                pop af
                inc c
                djnz ScanBit
                ret

; A = colour 0..15
SetBorder:
                and 0x0F
                ld b,7
                jp WriteVdp

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
; The palette, only if the machine has one
;-----------------------------------------------------------------------------
; Byte 0x002D of the BIOS says the version: 0 is MSX1, 1 is MSX2, 2 is MSX2+ and
; 3 is TurboR. On an MSX1 the 16 colours are fixed and there is nothing to load.
;
; Checked at run time instead of with conditional assembly on purpose: that way
; there is one ROM that works on both machines, rather than two to build and
; hand out separately. The 32 bytes of the palette always travel and nobody
; notices them.
LoadPalette:
                ld a,(MSX_VERSION)
                or a
                ret z                   ; MSX1: no palette

                di

                ; R#16 = 0: the index of the colour about to be written. It
                ; moves on by itself with each pair of bytes, so setting it once
                ; is enough.
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

; 16 colours of two bytes: red in the high nibble of the first one, blue in the
; low one, green in the second.
PaletteData:
{PALETTE}
PaletteEnd:

;-----------------------------------------------------------------------------
; The data exported by the editor
;-----------------------------------------------------------------------------
; Between labels of its own so that the ROM does not depend on the name of the
; tile set, which is where the labels of the exported files come from. The
; commented line next to each one is the other output of the editor: swap the
; two to try it.
PatternsData:
{PATTERNS}
{PATTERNS_ALT}
PatternsEnd:

ColorsData:
{COLORS}
{COLORS_ALT}
ColorsEnd:

; Padding up to 16K, the size a cartridge in page 1 is expected to be. With a
; label and not with $, because in sasSX a $ inside an expression does not give
; the PC: it stays at zero and a 32K ROM comes out.
RomEnd:
                {DS} 0x8000 - RomEnd, 0xFF
