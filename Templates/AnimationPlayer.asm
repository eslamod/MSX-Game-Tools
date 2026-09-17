;-----------------------------------------------------------------------------
; The player for the animation format of MSX Game Tools
;-----------------------------------------------------------------------------
; Included from the ROM that brings the groups, because an animation points at
; the groups by the place they take in the table, so it can only be checked
; where that table exists and is known to be right.
;
; The data block can be empty. AnimInit then finds nothing, the ROM behaves as
; it always did -the groups laid out in the grid- and not one plane is spent
; here.
;
; --- The format ---------------------------------------------------------------
; Per animation: one byte with what to do at the end, the steps, and a 0x00.
;
;   0x00                                          that is the end
;   0x01, what it points at, how long it waits   show it and wait
;   0x02, points, waits, Y, X, event            the same, offset
;   0x03, times                                  a loop starts
;   0x04                                          and here it ends
;   0x05, colour                                 the colour from here on
;
; The waits are counted in interrupts and are never zero. The offsets are in
; two's complement and absolute: each one replaces the last, they do not add up.
;
; Only pattern animations carry the colour: a group already carries the colour
; of each of its sprites. It goes apart and not inside the frame so that it is
; paid for only when it changes. It is a colour index and nothing else: where it
; gets written depends on the sprite mode, and ANIM_COLOR_TABLE below decides.
;
; --- Why it is resolved into a table --------------------------------------------
; The strip is walked once at start-up and a flat list of frames is left in RAM,
; six bytes each. The colour goes inside each frame and not as a state apart,
; which is what makes ping-pong come out right: going backwards each frame
; carries the colour it was due. A real game would save that RAM by reading the
; strip as it goes, and for loops that works just as well; but ping-pong asks to
; walk it backwards, and a strip of steps of varying size is not walked backwards
; without keeping track of where it has been. Which is this table. On top of
; that it is the same thing the editor does, so if the two agree then the format
; is being read the same way from both sides.
;-----------------------------------------------------------------------------

; --- The steps ---------------------------------------------------------------
ANIM_END        {EQU} 0x00
ANIM_FRAME      {EQU} 0x01
ANIM_MOVED      {EQU} 0x02
ANIM_LOOP       {EQU} 0x03
ANIM_LOOP_END   {EQU} 0x04
ANIM_PAINT      {EQU} 0x05

; --- What the targets are made of --------------------------------------------
; The 38 of a pattern animation and the 38 of a group one are two different
; things, and they look the same in the strip: that is why the header says which.
ANIM_OF_PATTERNS {EQU} 0x00
ANIM_OF_GROUPS   {EQU} 0x01

; The one a pattern comes out in if the strip carries no 0x05 at all. The
; exporter always puts one, so this is for a strip written by hand.
ANIM_PATTERN_COLOR {EQU} 1       ; black

; Where the colour of a sprite goes, which is not the same place on both machines:
;
;   1 = mode 2, the 16 bytes of the colour table of the plane (what this ROM sets)
;   0 = mode 1, the fourth byte of the attribute
;
; Chosen at assembly time and not looked at while running. It could be: the
; version byte of the Main ROM is at 0x002D and a cartridge reads it straight.
; But that would be the wrong question: what rules is the sprite mode the game
; has set -an MSX2 runs in mode 1 perfectly well- and the game knows that
; already, since it set it.
;
; It only rules the pattern animations. The groups of this ROM are mode 2 and
; that is that: an MSX1 group carries four bytes per member instead of nineteen,
; which is another format and not an if.
ANIM_COLOR_TABLE {EQU} {ANIM_COLOR_TABLE}

; --- What to do at the end ---------------------------------------------------
ANIM_ONCE       {EQU} 0x00
ANIM_REPEAT     {EQU} 0x01
ANIM_PINGPONG   {EQU} 0x02

; --- How much fits -----------------------------------------------------------
; Eight planes: it is what this ROM sets aside for the animation, not the
; ceiling of a group -the editor allows thirty two-. Of a bigger one the first
; eight are painted. They are taken from the front and the grid of groups starts
; behind them, so with animation eight groups fewer show: that is the price of
; having both things on the same screen.
ANIM_PLANES     {EQU} 8

; If the animation brings more frames it is cut and what fits is shown. 64 is
; plenty for what is tried here and it is 320 bytes of RAM.
ANIM_MAX_FRAMES {EQU} 64
ANIM_MAX_DEPTH  {EQU} 4

; Bottom left, and not up top: the grid fills the lines above, and the VDP only
; draws eight sprites per scan line. Sharing lines with it would eat from the
; very quota this ROM is measuring.
ANIM_X          {EQU} 8
ANIM_Y          {EQU} 150

; --- RAM ---------------------------------------------------------------------
; Page 3 is RAM on any MSX and nobody else uses it here. The first three bytes
; belong to the ROM that brings this in.
ANIM_FIRST      {EQU} 0xC003     ; first plane left for the grid
ANIM_ENDING     {EQU} 0xC004     ; what to do at the end
ANIM_COUNT      {EQU} 0xC005     ; frames resolved
ANIM_INDEX      {EQU} 0xC006     ; which one is showing
ANIM_WAIT       {EQU} 0xC007     ; interrupts it has left
ANIM_DEPTH      {EQU} 0xC008     ; loops open while it is being resolved
ANIM_TARGET     {EQU} 0xC009     ; the group of the current frame
ANIM_OFF_Y      {EQU} 0xC00A     ; and its offset
ANIM_OFF_X      {EQU} 0xC00B
; the 0xC00C belongs to the grid
ANIM_TOTAL      {EQU} 0xC00D     ; how many animations the block brings
ANIM_CURRENT    {EQU} 0xC00E     ; which one is being shown
ANIM_MADE       {EQU} 0xC00F     ; what its targets are made of

ANIM_STACK      {EQU} 0xC010     ; 3 bytes per loop: where to go back to and how many times
ANIM_INK        {EQU} 0xC030     ; the colour the 0x05 sets while resolving
ANIM_COLOR      {EQU} 0xC031     ; and the one of the frame being painted
ANIM_TIMELINE   {EQU} 0xC040     ; 6 per frame: points, waits, Y, X, event, colour

;-----------------------------------------------------------------------------
; Gets the first animation of the block ready.
;
; On the way back, A = frames resolved. Zero if the block is empty, and then
; everything else in here does nothing.
;-----------------------------------------------------------------------------
AnimInit:
                xor a
                ld (ANIM_COUNT),a
                ld (ANIM_INDEX),a
                ld (ANIM_DEPTH),a
                ld (ANIM_TOTAL),a
                ld (ANIM_FIRST),a       ; with no animation, the grid from 0

                ld hl,AnimationsEnd
                ld de,AnimationsData
                or a
                sbc hl,de
                ret z                   ; empty block: there is nothing to play

                ld a,ANIM_PLANES
                ld (ANIM_FIRST),a       ; the grid starts behind its own

                call AnimCountAll

                xor a
                ; and falls into AnimSelect, which gets the first one ready

;-----------------------------------------------------------------------------
; A = which one to show. Leaves it resolved and ready to play.
;-----------------------------------------------------------------------------
AnimSelect:
                ld (ANIM_CURRENT),a

                ld hl,AnimationsData
                or a
                jr z,AnimSelected

                ld b,a
AnimSelectSkip:
                call AnimSkip
                djnz AnimSelectSkip

AnimSelected:
                xor a
                ld (ANIM_COUNT),a
                ld (ANIM_INDEX),a
                ld (ANIM_DEPTH),a

                ; Every animation starts with no inherited colour: this one can
                ; have been jumped to from any other.
                ld a,ANIM_PATTERN_COLOR
                ld (ANIM_INK),a

                ld a,(hl)               ; what it is made of
                ld (ANIM_MADE),a
                inc hl

                ld a,(hl)               ; and what it does at the end
                ld (ANIM_ENDING),a
                inc hl

                ld de,ANIM_TIMELINE     ; where the next frame lands

; HL = where the strip is up to, DE = where the table is up to
AnimStep:
                ld a,(hl)
                inc hl
                cp ANIM_FRAME
                jr z,AnimStepFrame
                cp ANIM_MOVED
                jr z,AnimStepMoved
                cp ANIM_LOOP
                jr z,AnimStepLoop
                cp ANIM_LOOP_END
                jr z,AnimStepLoopEnd
                cp ANIM_PAINT
                jr z,AnimStepPaint
                jr AnimResolved         ; the 0x00, or a byte nobody understands

; Two bytes: the colour from here on. It is not a frame, so it does not count
; nor take room in the table; it is noted down and the ones that come after
; carry it. On the second time round a loop it comes through here again and
; sets the same one, which is exactly what is needed.
AnimStepPaint:
                ld a,(hl)
                inc hl
                ld (ANIM_INK),a
                jr AnimStep

; Three bytes: what it points at and how long it waits. The rest, at zero.
AnimStepFrame:
                call AnimRoom
                jr nc,AnimResolved

                ld a,(hl)
                inc hl
                ld (de),a
                inc de
                ld a,(hl)
                inc hl
                ld (de),a
                inc de
                xor a
                ld (de),a               ; no Y offset
                inc de
                ld (de),a               ; no X either
                inc de
                ld (de),a               ; and no event
                inc de

                call AnimInk
                call AnimCounted
                jr AnimStep

; Six: the same plus the offset and the event, which come in the same order.
AnimStepMoved:
                call AnimRoom
                jr nc,AnimResolved

                ld b,5
AnimMovedByte:
                ld a,(hl)
                inc hl
                ld (de),a
                inc de
                djnz AnimMovedByte

                call AnimInk
                call AnimCounted
                jr AnimStep

; Seals the current colour into the frame just written. DE is left after it.
AnimInk:
                ld a,(ANIM_INK)
                ld (de),a
                inc de
                ret

AnimStepLoop:
                ld a,(ANIM_DEPTH)
                cp ANIM_MAX_DEPTH
                jr nc,AnimTooDeep

                push de
                call AnimPush
                pop de
                jr AnimStep

; Nested deeper than the count goes: the loop is ignored and its steps happen
; once. It looks odd, but it shows, which beats hanging.
AnimTooDeep:
                inc hl                  ; the number of times
                jr AnimStep

AnimStepLoopEnd:
                ld a,(ANIM_DEPTH)
                or a
                jr z,AnimStep           ; a stray close: there is nothing to close

                push hl                 ; where to carry on if the loop ends
                push de                 ; and where the table is up to

                dec a
                ld (ANIM_DEPTH),a
                call AnimSlot           ; DE = the top of the stack

                ex de,hl
                ld e,(hl)
                inc hl
                ld d,(hl)               ; DE = where to go back to
                inc hl                  ; HL = the times left

                dec (hl)
                ld a,(hl)

                pop hl                  ; the table
                ex de,hl                ; DE = table, HL = where to go back to

                or a
                jr z,AnimLoopOver

                ld a,(ANIM_DEPTH)       ; still open: it goes back on the stack
                inc a
                ld (ANIM_DEPTH),a
                pop bc                  ; and the "where to carry on" is thrown away
                jp AnimStep             ; jp: a jr does not reach from here

AnimLoopOver:
                pop hl                  ; where to carry on
                jp AnimStep

;-----------------------------------------------------------------------------
; The strip is over. If it is ping-pong, it turns round.
;-----------------------------------------------------------------------------
AnimResolved:
                ld a,(ANIM_ENDING)
                cp ANIM_PINGPONG
                call z,AnimMirror

                ld a,(ANIM_COUNT)
                or a
                ret z

                xor a
                ld (ANIM_INDEX),a
                call AnimLoadWait

                ld a,(ANIM_COUNT)
                ret

; The turn of the ping-pong: from the last but one to the second. The ends are
; not repeated, or the frame it turns on would show for twice as long.
AnimMirror:
                ld a,(ANIM_COUNT)
                cp 3
                ret c                   ; with two or fewer there is no turn to make

                sub 2
                ld b,a                  ; how many get copied
                ld c,a                  ; and which one it starts from: the last but one

AnimMirrorNext:
                ld a,c
                push bc

                call AnimAt             ; HL = that frame
                ld bc,6
                ldir                    ; onto the tail, and DE is left after it by itself

                call AnimCounted
                pop bc

                dec c                   ; the one before
                djnz AnimMirrorNext
                ret

;-----------------------------------------------------------------------------
; How many animations the block brings, into ANIM_TOTAL.
;
; By walking the steps and not by counting zero bytes: the 0x00 that closes an
; animation is the same value a frame can carry inside -group 0, an offset of
; zero, an event of zero-, so they have to be walked.
;-----------------------------------------------------------------------------
AnimCountAll:
                ld hl,AnimationsData
                ld c,0

AnimCountNext:
                push hl
                ld de,AnimationsEnd
                or a
                sbc hl,de
                pop hl
                jr nc,AnimCountDone

                call AnimSkip
                inc c
                jr AnimCountNext

AnimCountDone:
                ld a,c
                ld (ANIM_TOTAL),a
                ret

; HL = the start of an animation -> HL = the start of the next one.
AnimSkip:
                inc hl                  ; what it is made of
                inc hl                  ; and what it does at the end

AnimSkipNext:
                ld a,(hl)
                inc hl
                cp ANIM_FRAME
                jr z,AnimSkipTwo
                cp ANIM_MOVED
                jr z,AnimSkipFive
                cp ANIM_LOOP
                jr z,AnimSkipOne
                cp ANIM_LOOP_END
                jr z,AnimSkipNext
                cp ANIM_PAINT
                jr z,AnimSkipOne
                ret                     ; the 0x00, and HL is past it already

AnimSkipOne:
                inc hl
                jr AnimSkipNext

AnimSkipTwo:
                inc hl
                inc hl
                jr AnimSkipNext

AnimSkipFive:
                push de
                ld de,5
                add hl,de
                pop de
                jr AnimSkipNext

;-----------------------------------------------------------------------------
; The cursor keys change animation, if the bank brings more than one.
;-----------------------------------------------------------------------------
ANIM_KEY_ROW    {EQU} 8          ; the keyboard row with the cursor keys
ANIM_KEY_LEFT   {EQU} 4
ANIM_KEY_RIGHT  {EQU} 7

; Both bits together, added up already. As a number and not as
; (1 << ANIM_KEY_LEFT) | (1 << ANIM_KEY_RIGHT) because the parentheses of an
; expression do not travel the same through the four assemblers: asMSX swaps
; parentheses for brackets with .zilog, so there the line does not assemble at
; all, and pasmo gets it right but warns about it on every build.
ANIM_KEY_BOTH   {EQU} 0x90

ScanAnimationKeys:
                ld a,(ANIM_TOTAL)
                cp 2
                ret c                   ; with only one there is nothing to choose

                ld a,ANIM_KEY_ROW
                call KeyRow
                bit ANIM_KEY_RIGHT,a
                jr z,AnimKeyNext
                bit ANIM_KEY_LEFT,a
                jr z,AnimKeyPrevious
                ret

AnimKeyNext:
                ld a,(ANIM_CURRENT)
                inc a
                ld hl,ANIM_TOTAL
                cp (hl)
                jr c,AnimKeyGo
                xor a                   ; from the last one, to the first
                jr AnimKeyGo

AnimKeyPrevious:
                ld a,(ANIM_CURRENT)
                or a
                jr nz,AnimKeyBack
                ld a,(ANIM_TOTAL)       ; from the first one, to the last
AnimKeyBack:
                dec a

AnimKeyGo:
                call AnimSelect
                call AnimShow

; Waiting for the release: without this one animation would go by per interrupt
; and there would be no way to stop on any.
AnimKeyRelease:
                ld a,ANIM_KEY_ROW
                call KeyRow
                and ANIM_KEY_BOTH
                cp ANIM_KEY_BOTH
                jr nz,AnimKeyRelease
                ret

;-----------------------------------------------------------------------------
; One interrupt less. When the wait is over, it moves on to the next one.
;-----------------------------------------------------------------------------
AnimTick:
                ld a,(ANIM_COUNT)
                or a
                ret z

                ld a,(ANIM_WAIT)
                dec a
                ld (ANIM_WAIT),a
                ret nz

                ld a,(ANIM_INDEX)
                inc a
                ld hl,ANIM_COUNT
                cp (hl)
                jr c,AnimTickShow       ; there is animation left

                ld a,(ANIM_ENDING)
                or a
                jr nz,AnimTickWrap      ; loop or ping-pong: back to the start

                ; Once through: it stays still on the last one. It comes back in
                ; every 255 interrupts and does nothing, which is cheaper than
                ; keeping a "stopped" state just for this.
                ld a,255
                ld (ANIM_WAIT),a
                ret

AnimTickWrap:
                xor a

AnimTickShow:
                ld (ANIM_INDEX),a
                call AnimLoadWait
                ; and falls into AnimShow

;-----------------------------------------------------------------------------
; Paints the frame in hand into the planes set aside.
;-----------------------------------------------------------------------------
AnimShow:
                ld a,(ANIM_TOTAL)
                or a
                ret z                   ; with no animations the planes belong to the grid

                ; An animation with no steps does have to clear: it can be reached
                ; with the cursor keys, and without this the figure of the last
                ; one would stay on screen as if it were still there.
                ld a,(ANIM_COUNT)
                or a
                jp z,AnimShowNone       ; jp and not jr: the short jump does not reach from here to there

                ld a,(ANIM_INDEX)
                call AnimAt

                ld a,(hl)
                ld (ANIM_TARGET),a
                inc hl
                inc hl                  ; the wait has been read already
                ld a,(hl)
                ld (ANIM_OFF_Y),a
                inc hl
                ld a,(hl)
                ld (ANIM_OFF_X),a
                inc hl
                inc hl                  ; the event, which this ROM does not use
                ld a,(hl)
                ld (ANIM_COLOR),a

                ld a,(ANIM_MADE)
                cp ANIM_OF_GROUPS
                jr nz,AnimShowPattern

                ld a,(ANIM_TARGET)
                call AnimGroupAt        ; IX = its members, B = how many
                jr c,AnimShowNone       ; that group is not in the file

                ld a,b
                or a
                jr z,AnimShowNone       ; an empty group paints nothing

                ; And it does not go past the ones set aside: the grid of groups starts
                ; behind, and extra planes would tread on it.
                cp ANIM_PLANES
                jr c,AnimMemberCount
                ld a,ANIM_PLANES
AnimMemberCount:
                ld b,a

                ld iy,0
AnimMemberNext:
                push bc
                call AnimWriteMember
                pop bc
                inc iy
                djnz AnimMemberNext

                push iy
                pop hl
                ld a,l
                jr AnimHideFrom

; A lone pattern: one sprite and that is it, no members, no group offsets.
AnimShowPattern:
                di

    {IF} ANIM_COLOR_TABLE
                ; In mode 2 the colour of a sprite is 16 bytes, one per line. The
                ; animation brings just one, so all 16 are the same: the colour
                ; per line lives in the groups, not here.
                ld hl,SPRITE_COLOR      ; the 16 colour bytes of plane 0
                call SetVramWrite
                ld b,16
                ld a,(ANIM_COLOR)
AnimPatternColor:
                out (VDP_DATA),a
                djnz AnimPatternColor
    {ENDIF}

                ld hl,SPRITE_ATTR
                call SetVramWrite

                ld a,(ANIM_OFF_Y)
                call AnimScale
                add a,ANIM_Y
                out (VDP_DATA),a

                ld a,(ANIM_OFF_X)
                call AnimScale
                add a,ANIM_X
                out (VDP_DATA),a

                ; The pattern number is multiplied by four for the attribute table,
                ; because in 16x16 each pattern takes four of the 8x8 ones. The file
                ; brings it unmultiplied because a bank can hold more than 64 and
                ; then it would not fit in a byte.
                ld a,(ANIM_TARGET)
                add a,a
                add a,a
                out (VDP_DATA),a

    {IF} ANIM_COLOR_TABLE
                xor a                   ; in mode 2 the colour is in the table already
    {ELSE}
                ld a,(ANIM_COLOR)       ; and in mode 1 it goes here
    {ENDIF}
                out (VDP_DATA),a
                ei

                ld a,1                  ; plane 0 spent; the rest, out of the way
                jr AnimHideFrom

AnimShowNone:
                xor a

; A = the first plane set aside that this frame does not spend. They are parked
; below the screen: if not, the sprites of the last frame would stay put.
AnimHideFrom:
                cp ANIM_PLANES
                ret nc

                push af
                ld l,a
                ld h,0
                add hl,hl
                add hl,hl
                ld de,SPRITE_ATTR
                add hl,de

                di
                call SetVramWrite
                ld a,HIDDEN_Y
                out (VDP_DATA),a
                ei

                pop af
                inc a
                jr AnimHideFrom

;-----------------------------------------------------------------------------
; IX = member, IY = plane. Its 16 colour bytes and its 4 attribute ones.
;-----------------------------------------------------------------------------
AnimWriteMember:
                push iy
                pop hl
                add hl,hl
                add hl,hl
                add hl,hl
                add hl,hl
                ld bc,SPRITE_COLOR
                add hl,bc

                di
                call SetVramWrite

                push ix
                pop hl
                ld b,16
AnimColorNext:
                ld a,(hl)
                out (VDP_DATA),a
                inc hl
                djnz AnimColorNext

                push iy
                pop hl
                add hl,hl
                add hl,hl
                ld bc,SPRITE_ATTR
                add hl,bc
                call SetVramWrite

                ; Y = anchor + (what the member says + what the frame says) at the scale
                ld a,(ANIM_OFF_Y)
                ld c,a
                ld a,(ix+16)
                add a,c
                call AnimScale
                add a,ANIM_Y
                out (VDP_DATA),a

                ld a,(ANIM_OFF_X)
                ld c,a
                ld a,(ix+17)
                add a,c
                call AnimScale
                add a,ANIM_X
                out (VDP_DATA),a

                ld a,(ix+18)            ; pattern, already multiplied by 4
                out (VDP_DATA),a

                xor a                   ; in mode 2 the fourth byte carries no colour
                out (VDP_DATA),a
                ei

                ld de,19
                add ix,de
                ret

; What gets scaled are the distances inside the figure, not the anchor: with
; magnification the sprite grows from where it is and does not go twice as far.
AnimScale:
                ld c,a
                ld a,(REG1_VALUE)
                rrca                    ; MAG is bit 0
                ld a,c
                ret nc
                add a,a
                ret

;-----------------------------------------------------------------------------
; A = group number -> IX = its members, B = how many. Carry if it does not exist.
;
; By the place it takes in the file and not by any number stored inside: it is
; how the exporter addresses them, writing them in order.
;-----------------------------------------------------------------------------
AnimGroupAt:
                ld ix,GroupsData
                ld c,a

AnimGroupNext:
                push ix
                pop hl
                ld de,GroupsEnd
                or a
                sbc hl,de
                jr nc,AnimGroupNone     ; the file is over

                ld b,(ix+0)
                inc ix

                ld a,c
                or a
                ret z                   ; this is the one, and no carry

                dec c

                ld a,b
                or a
                jr z,AnimGroupNext      ; empty group, it takes up no members

AnimGroupSkip:
                push de
                ld de,19
                add ix,de
                pop de
                dec a
                jr nz,AnimGroupSkip
                jr AnimGroupNext

AnimGroupNone:
                scf
                ret

;-----------------------------------------------------------------------------
; Helpers for the frame table
;-----------------------------------------------------------------------------
; A = frame number -> HL = where it starts
AnimAt:
                ld l,a
                ld h,0
                add hl,hl               ; x2
                ld c,l
                ld b,h
                add hl,hl               ; x4
                add hl,bc               ; x6
                ld bc,ANIM_TIMELINE
                add hl,bc
                ret

; The wait of the current frame. A wait of zero would stop the clock, so it is
; counted as one: the editor does not allow them, but a file could bring one.
AnimLoadWait:
                push hl
                push de
                ld a,(ANIM_INDEX)
                call AnimAt
                inc hl
                ld a,(hl)
                or a
                jr nz,AnimWaitOk
                inc a
AnimWaitOk:
                ld (ANIM_WAIT),a
                pop de
                pop hl
                ret

; Carry if there is still room for frames in the table: cp leaves the carry set
; when what there is is less than the ceiling, which is exactly that.
AnimRoom:
                ld a,(ANIM_COUNT)
                cp ANIM_MAX_FRAMES
                ret

AnimCounted:
                ld a,(ANIM_COUNT)
                inc a
                ld (ANIM_COUNT),a
                ret

; DE = ANIM_STACK + depth*3
AnimSlot:
                ld a,(ANIM_DEPTH)
                ld l,a
                ld h,0
                add hl,hl               ; x2
                ld e,a
                ld d,0
                add hl,de               ; x3
                ld de,ANIM_STACK
                add hl,de
                ex de,hl
                ret

; Stacks the loop that is starting. HL points at the times and comes out after.
AnimPush:
                ld c,(hl)               ; times
                inc hl                  ; and here the body starts

                push hl
                call AnimSlot           ; DE = its slot
                pop hl

                ld a,l
                ld (de),a
                inc de
                ld a,h
                ld (de),a
                inc de
                ld a,c
                ld (de),a

                ld a,(ANIM_DEPTH)
                inc a
                ld (ANIM_DEPTH),a
                ret
