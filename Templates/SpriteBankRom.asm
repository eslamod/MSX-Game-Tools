;-----------------------------------------------------------------------------
; {NAME} - example ROM for a sprite bank exported by MSX Game Tools
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
;   - GRAPHIC 3 (SCREEN 4) with mode 2 sprites, 16x16.
;   - The whole screen in colour 0, that is transparent: what shows through is
;     the backdrop (R#7).
;   - Dumps the exported patterns into the sprite pattern generator.
;   - Walks the groups handing out planes in order, spending them as it finds
;     them. Once the 32 planes are gone it stops putting sprites down.
;   - The groups go in rows, filling each row while they fit: the VDP only draws
;     SPRITES_PER_LINE sprites per scan line, so as long as what the row carries
;     plus what the group asks for stays inside that quota, the group goes
;     beside it and not below. When it does not fit -or the columns across run
;     out- another row starts lower down, as far down as the tallest group of
;     the bank measures.
;     The offsets of each sprite of the group are added to the position of the
;     row and the column.
;   - One look then shows what really matters when laying out a screen: how many
;     of these characters fit together at the same height.
;   - Keys 0-9 and A-F change the backdrop colour.
;   - The left and right cursor keys change animation, if there is more than one.
;   - F1 toggles the magnification bit (sprites at x2) and rebuilds the
;     attribute table: doubling the size doubles the distances to the centre as
;     well, and whatever falls off the screen is parked outside.
;
; The palette travels inside this file, as 32 bytes further down. It is only
; loaded on an MSX2 and up: on an MSX1 the 16 colours are fixed.
;
; The bank has to be an MSX2 one, which is the one that exports the 16 colour
; bytes per sprite that mode 2 asks for.
;-----------------------------------------------------------------------------

; --- VDP ports ---------------------------------------------------------------
VDP_DATA        {EQU} 0x98       ; VRAM data
VDP_ADDR        {EQU} 0x99       ; VRAM address and registers

; --- BIOS --------------------------------------------------------------------
SNSMAT          {EQU} 0x0141     ; A = keyboard matrix row -> A, bit at 0 = pressed
MSX_VERSION     {EQU} 0x002D     ; 0 = MSX1, 1 = MSX2, 2 = MSX2+, 3 = TurboR

; --- Palette -----------------------------------------------------------------
VDP_PALETTE     {EQU} 0x9A       ; the port the colours go in through
PALETTE_BYTES   {EQU} 32         ; 16 colours of two bytes

; --- VRAM layout in GRAPHIC 3, page 0 ----------------------------------------
; The V9938 manual puts the sprite generator at 1C00H, but only 32 patterns of
; 16x16 fit there. It moves up to 1800H so that the 64 of the bank fit.
PATTERN_GEN     {EQU} 0x0000     ; 6144  screen patterns
SPRITE_GEN      {EQU} 0x1800     ; 2048  sprite patterns (64 of 16x16)
COLOR_TABLE     {EQU} 0x2000     ; 6144  screen colours
NAME_TABLE      {EQU} 0x3800     ;  768  names
SPRITE_COLOR    {EQU} 0x3C00     ;  512  sprite colour table (32 x 16)
SPRITE_ATTR     {EQU} 0x3E00     ;  128  attribute table (32 x 4)

; --- Constants ---------------------------------------------------------------
MAX_PLANES      {EQU} 32
Sprite_Rows     {EQU} 16         ; how tall a sprite is
SPRITE_END_Y    {EQU} 0xD8       ; 216: in mode 2 it cuts off the rest of the planes

; Sprites the VDP draws per scan line. Eight in mode 2, which is the only one
; this ROM runs: it asks for GRAPHIC 3 and the 16 colour bytes per sprite. In
; mode 1 there would be four, and half the groups would fit per row.
SPRITES_PER_LINE {EQU} 8

COLUMN_X        {EQU} 56         ; first column
COLUMN_X_STEP   {EQU} 72         ; and the next ones, to the right
; Three columns: 56 + 2*72 = 200, and the sprite ends at 216 even with MAG on.
; Past this a new row starts even if there is quota left on the scan line.
ROW_X_LIMIT     {EQU} 216

; First row, then it goes down. Right at the top and not half way: with 30 two
; rows were lost for nothing. With 10 nine rows fit in 192 lines -the last one
; starts at 170 and the sprite ends at 186-, which is more than the 32 planes
; give however many groups of one there are.
CENTRE_Y        {EQU} 10
; How far each row drops is measured from the groups the bank brings, it is not
; fixed: a figure two sprites tall measures 32, and with a step of 20 the rows
; overlap by twelve pixels. Only the margin left above it is here.
GROUP_MARGIN    {EQU} 4
GROUP_STEP      {EQU} 0xC00C     ; and here what comes out of measuring them
; Screen on, 16x16 sprites, MAG=0, and bit 5 set: IE0, the scan interrupt.
; Without it the VDP never interrupts and the halt of the main loop sits
; waiting for something that never comes, with the ROM hung and answering no
; key at all. It did not matter before, because nothing waited for anything.
REG1_BASE       {EQU} 0x62
SCREEN_LINES    {EQU} 192
HIDDEN_Y        {EQU} 200        ; below the screen, and it is not 208 or 216

; --- State -------------------------------------------------------------------
; One byte of RAM for the current value of R#1, which is where the MAG bit lives.
; Page 3 is RAM on any MSX2 and nobody else touches it here: the INIT of the
; cartridge never goes back to BASIC. It has to be in memory and not in a
; register because both the keyboard loop and the building of the attribute
; table read it, and that table is rebuilt whole every time magnification changes.
REG1_VALUE      {EQU} 0xC000

; And two more for laying the groups out in rows. In RAM and not in registers
; because between IX -the file-, IY -the plane-, B -the members left- and C
; -how far the row drops- there is none left that survives WriteMember.
GROUP_DX        {EQU} 0xC001     ; which column the group in hand goes to
ROW_USED        {EQU} 0xC002     ; sprites already put in the open row

;-----------------------------------------------------------------------------
; Cartridge header
;-----------------------------------------------------------------------------
{HEADER}

;-----------------------------------------------------------------------------
; Start
;-----------------------------------------------------------------------------
{START}:
                ld a,REG1_BASE
                ld (REG1_VALUE),a

                call SetupVdp
                call LoadPalette
                call ClearScreen
                call LoadPatterns

                ; Before laying out the grid, which is what decides the plane it
                ; starts from: the animation keeps the first ones.
                call AnimInit

                call BuildSprites
                call AnimShow

; The halt is what gives the animation its clock: its waits are counted in
; interrupts. It is good without animation too: the keyboard scan stops being
; a busy wait at full speed.
MainLoop:
                halt
                call AnimTick
                call ScanColorKeys
                call ScanF1
                call ScanAnimationKeys
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

VdpSetup:
                {DB}  0, 0x04    ; M4=1, M3=0 -> GRAPHIC 3
                {DB}  1, REG1_BASE
                {DB}  2, 0x0E    ; name table             0x3800
                {DB}  3, 0xFF    ; colour table           0x2000 (with R#10)
                {DB} 10, 0x00
                {DB}  4, 0x03    ; pattern generator      0x0000
                {DB}  5, 0x7F    ; sprite attributes      0x3E00 (with R#11)
                {DB} 11, 0x00
                {DB}  6, 0x03    ; sprite generator       0x1800
                {DB}  7, 0x00    ; initial backdrop colour
                {DB}  8, 0x08    ; VR=1, sprites visible
                {DB}  9, 0x00    ; 192 lines, NTSC
                {DB} 23, 0x00    ; no vertical scroll
                {DB} 14, 0x00    ; VRAM page 0
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
; HL = VRAM address (14 bits; it never goes past 0x3FFF here)
SetVramWrite:
                ld a,l
                out (VDP_ADDR),a
                ld a,h
                and 0x3F
                or 0x40         ; write bit
                out (VDP_ADDR),a
                ret

; HL = destination in VRAM, BC = length, A = value
FillVram:
                di
                push af
                call SetVramWrite
                pop af
FillNext:
                out (VDP_DATA),a
                dec bc
                ld d,a
                ld a,b
                or c
                ld a,d
                jr nz,FillNext
                ei
                ret

; HL = source in RAM/ROM, DE = destination in VRAM, BC = length
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
; The whole screen in colour 0
;-----------------------------------------------------------------------------
; With the three of them at zero every pixel uses colour 0, which is
; transparent, so the backdrop of R#7 shows through and can be changed at will.
ClearScreen:
                xor a
                ld hl,PATTERN_GEN
                ld bc,6144
                call FillVram

                xor a
                ld hl,COLOR_TABLE
                ld bc,6144
                call FillVram

                xor a
                ld hl,NAME_TABLE
                ld bc,768
                call FillVram
                ret

;-----------------------------------------------------------------------------
; Sprite patterns
;-----------------------------------------------------------------------------
LoadPatterns:
                ld hl,PatternsData
                ld de,SPRITE_GEN
                ld bc,PatternsEnd-PatternsData
                call CopyToVram
                ret

;-----------------------------------------------------------------------------
; Attributes and colours out of the groups
;-----------------------------------------------------------------------------
; The format of the groups file (MSX2), just as the editor exports it:
;   per group:  1 byte with how many sprites it has
;   per sprite: 16 colour bytes + offset Y + offset X + pattern (already x4)
;
; IX walks the file, IY is the plane in hand, C the vertical offset the current
; group has piled up.
BuildSprites:
                call MeasureGroups

                ld a,SPRITE_END_Y
                ld hl,SPRITE_ATTR
                ld bc,128
                call FillVram   ; every plane hidden to start with

                ld ix,GroupsData

                ; And not always from plane 0: with animation the first ones are
                ; its. IY through the stack because ld iyl,a is not documented Z80.
                ld a,(ANIM_FIRST)
                ld l,a
                ld h,0
                push hl
                pop iy

                ld c,0

                xor a
                ld (GROUP_DX),a     ; first column
                ld (ROW_USED),a     ; and the row, empty

NextGroup:
                push ix
                pop hl
                ld de,GroupsEnd
                or a
                sbc hl,de
                jr nc,SpritesDone   ; the file is over

                ld b,(ix+0)         ; sprites in this group
                inc ix
                ld a,b
                or a
                jr z,GroupDone      ; an empty group spends no planes

                ; Are there planes left for the WHOLE group? Checked here and
                ; not when writing each member: per member, a group that only
                ; half fits gets started anyway and comes out missing the
                ; planes it ran out of, which looks like a bug in the editor.
                ; With eleven groups of three that is 33 planes, and the last
                push iy
                pop hl
                ld a,l
                add a,b
                cp MAX_PLANES + 1
                jr nc,SpritesDone   ; it does not fit whole: it stops here

                ; Does it fit in the open row? This is the question the whole
                ; test is about: while the quota of the scan line allows more,
                ; the group goes beside the last one and not below it.
                ld a,(ROW_USED)
                or a
                jr z,GroupFits      ; empty row: it fits even if the group goes past the quota

                add a,b
                cp SPRITES_PER_LINE + 1
                jr nc,NextRow       ; it does not fit: new row

                ld a,(GROUP_DX)
                cp ROW_X_LIMIT
                jr c,GroupFits      ; and there is width left as well

NextRow:
                ld hl,GROUP_STEP
                ld a,c
                add a,(hl)
                ld c,a

                xor a
                ld (GROUP_DX),a
                ld (ROW_USED),a

GroupFits:
                ld a,(ROW_USED)
                add a,b
                ld (ROW_USED),a     ; what this row has spent

; No plane check here: if the whole group fitted, all of its members fit.
NextMember:
                call WriteMember
                inc iy
                djnz NextMember

; The next group goes one column to the right. If it does not fit, the check
; above takes care of dropping it a row.
GroupDone:
                ld a,(GROUP_DX)
                add a,COLUMN_X_STEP
                ld (GROUP_DX),a
                jr NextGroup

; The first free plane carries Y=216, which in mode 2 cuts off the rest.
SpritesDone:
                push iy
                pop hl
                ld a,l
                cp MAX_PLANES
                ret nc
                add hl,hl
                add hl,hl
                ld de,SPRITE_ATTR
                add hl,de
                di
                call SetVramWrite
                ld a,SPRITE_END_Y
                out (VDP_DATA),a
                ei
                ret

; How tall the tallest group of the bank is, plus a margin, into GROUP_STEP.
;
; It walks the Y offsets of every member: the figure reaches from the highest
; to the lowest plus the 16 a sprite measures. With groups of a single sprite
; it comes out 20, which is what used to be written by hand; with two-tall
; ones, 36, and the rows stop treading on each other.
;
; The offsets are signed and are compared here with a bias of 128 so it can be
; done unsigned: comparing signed on a Z80 means looking at the overflow flag.
; The bias cancels itself out when one is subtracted from the other.
MeasureGroups:
                ld ix,GroupsData
                ld d,128        ; the lowest seen, and to start with zero itself
                ld e,128        ; the highest

MeasureGroup:
                push ix
                pop hl
                ld bc,GroupsEnd
                or a
                sbc hl,bc
                jr nc,MeasureDone

                ld b,(ix+0)
                inc ix
                ld a,b
                or a
                jr z,MeasureGroup       ; an empty group measures nothing

MeasureMember:
                ld a,(ix+16)
                add a,128

                cp d
                jr c,MeasureNotLower
                ld d,a                  ; it goes lower than any

MeasureNotLower:
                cp e
                jr nc,MeasureNotHigher
                ld e,a                  ; or higher

MeasureNotHigher:
                push de
                ld de,19
                add ix,de
                pop de
                djnz MeasureMember
                jr MeasureGroup

MeasureDone:
                ld a,d
                sub e                   ; the span from the highest to the lowest
                add a,Sprite_Rows       ; and what the bottom sprite measures
                add a,GROUP_MARGIN
                ld (GROUP_STEP),a
                ret

; IX = member, IY = plane, C = offset of the group, B = members left
WriteMember:
                push bc         ; the member counter and the offset

                ; The coordinates first, which need C and the free registers;
                ; after that the transfers eat HL, BC and A.
                ; They end up in D (Y) and E (X).
                call MemberCoords

                ; --- 16 colour bytes at SPRITE_COLOR + plane*16
                push iy
                pop hl
                add hl,hl
                add hl,hl
                add hl,hl
                add hl,hl
                ld bc,SPRITE_COLOR
                add hl,bc

                di              ; the two transfers, in one piece
                call SetVramWrite

                push ix
                pop hl
                ld b,16
ColorNext:
                ld a,(hl)
                out (VDP_DATA),a
                inc hl
                djnz ColorNext

                ; --- 4 attribute bytes at SPRITE_ATTR + plane*4
                push iy
                pop hl
                add hl,hl
                add hl,hl
                ld bc,SPRITE_ATTR
                add hl,bc
                call SetVramWrite

                ld a,d          ; Y
                out (VDP_DATA),a

                ld a,e          ; X
                out (VDP_DATA),a

                ld a,(ix+18)    ; pattern, already multiplied by 4
                out (VDP_DATA),a

                xor a           ; in mode 2 the fourth byte carries no colour
                out (VDP_DATA),a
                ei

                pop bc          ; gets the counter and the offset back

                ld de,19
                add ix,de
                ret

; The coordinates of the sprite at the scale in force.
;
; What gets scaled are the distances to the centre, not the position: the group
; stays where it was and grows from there. Multiplying the 128 as well would
; send the whole thing to 256, off the screen.
;
; Worked out in signed 16 bits on purpose. A large group offset -how far each
; row drops times 32 planes- doubled goes past 8 bits before it goes off the
; screen, and in 8 bits the overflow would wrap round and put the sprite at the
; top instead of getting it out of the way.
;
; IX = member, C = offset of the group -> D = Y, E = X
MemberCoords:
                ; ---- Y = CENTRE_Y + (offsetY + offset) * scale
                ld a,(ix+16)
                call SignExtend ; HL = signed offset Y
                ld b,0          ; B is saved already, it can be used
                add hl,bc       ; + how far the group drops
                call ScaleIfMag
                ld bc,CENTRE_Y
                add hl,bc

                ld a,h
                or a
                jr nz,HideMember        ; negative, or past 255
                ld a,l
                cp SCREEN_LINES
                jr nc,HideMember
                ld d,a

                ; ---- X = COLUMN_X + column of the group + offsetX * scale
                ; The column is not scaled, just as the centre is not: what
                ; grows with magnification is the sprite against its group,
                ; not the gap between groups.
                ld a,(ix+17)
                call SignExtend
                call ScaleIfMag
                ld bc,COLUMN_X
                add hl,bc

                ld a,(GROUP_DX)
                ld c,a
                ld b,0
                add hl,bc

                ld a,h
                or a
                jr nz,HideMember        ; it does not fit in the X byte
                ld e,l
                ret

; With magnification, a sprite that fitted before can end up outside. It is
; parked below the screen instead of clipped: 200 is not 216, so it does not
; cut off the planes that come after it.
HideMember:
                ld d,HIDDEN_Y
                ld e,0
                ret

; signed A -> HL
SignExtend:
                ld l,a
                rla             ; the sign bit into the carry
                sbc a,a         ; 0x00 or 0xFF
                ld h,a
                ret

; HL x2 if the MAG bit of R#1 is set
ScaleIfMag:
                ld a,(REG1_VALUE)
                rrca            ; MAG is bit 0
                ret nc
                add hl,hl
                ret

;-----------------------------------------------------------------------------
; Keyboard
;-----------------------------------------------------------------------------
; Matrix rows: 0 = "0".."7", 1 = "8","9" in bits 0 and 1,
; 2 = "A" and "B" in bits 6 and 7, 3 = "C".."F" in bits 0..3.
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
                call SetBackdrop
                pop af
NoKeyA:
                bit 7,a
                jr nz,NoKeyB
                ld a,11
                call SetBackdrop
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
                call SetBackdrop
                pop bc
ScanNext:
                pop af
                inc c
                djnz ScanBit
                ret

; A = colour 0..15
SetBackdrop:
                and 0x0F
                ld b,7
                jp WriteVdp

; F1 is in row 6, bit 5. It waits for the release so it does not toggle forever.
ScanF1:
                ld a,6
                call KeyRow
                bit 5,a
                ret nz

                ld a,(REG1_VALUE)
                xor 1           ; MAG, bit 0 of R#1
                ld (REG1_VALUE),a
                ld b,1
                call WriteVdp

                ; The sprites are twice the size, so the distances between them
                ; are too: the attribute table is rebuilt at the new scale.
                call BuildSprites
                call AnimShow

WaitRelease:
                ld a,6
                call KeyRow
                bit 5,a
                jr z,WaitRelease
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
; The palette, only if the machine has one
;-----------------------------------------------------------------------------
; Byte 0x002D of the BIOS says the version: 0 is MSX1, 1 is MSX2, 2 is MSX2+ and
; 3 is TurboR. On an MSX1 the 16 colours are fixed and there is nothing to load.
;
; Checked at run time instead of with conditional assembly on purpose: that way
; there is one ROM that works on both machines, rather than two to build and hand
; out separately. The 32 bytes of the palette always travel and nobody notices.
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
; Between labels of its own because the groups file does not carry how many
; groups there are: it is walked until the end. With them here the ROM does not
; depend on the name of the bank, which is where the exporter's labels come from.
;
; The commented line next to each one is the other output of the editor: moving
; the comment tries the other one, and gives the same ROM byte for byte.
PaletteData:
{PALETTE}
PaletteEnd:

PatternsData:
{PATTERNS}
{PATTERNS_ALT}
PatternsEnd:

GroupsData:
{GROUPS}
{GROUPS_ALT}
GroupsEnd:

; The animations of the bank, and this block can stay empty: with no data the
; ROM behaves as it did before -the groups laid out in the grid, spending no
; planes on anything else- and the player never even starts. It is checked at
; run time by comparing the two labels, so there is nothing else to touch.
AnimationsData:
{ANIMATIONS}
{ANIMATIONS_ALT}
AnimationsEnd:

; The animation player, apart: it is the routine that gets copied into a real
; game, so it travels in one piece and not spread through here.
{PLAYER}

{TAIL}
