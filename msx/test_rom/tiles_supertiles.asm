; Supertile table - Tiles
; 8 supertiles of 2x3 tiles.
; Header: width, height and count, one byte each. A count of 0 means 256,
; which is the most a map can name because a map cell is one byte.
; Then the tile numbers of each supertile, left to right and top to bottom.
; Empty cells are written as tile 0: the name table always draws something.
; Size: tiles_supertiles_end - tiles_supertiles

tiles_supertiles:
    .db  0x02,0x03,0x08    ; size and count

tiles_supertile_0:    ; S0
    .db  0x00,0x01,0x02,0x03,0x04,0x05
tiles_supertile_1:    ; S1
    .db  0x06,0x07,0x08,0x09,0x0A,0x0B
tiles_supertile_2:    ; S2
    .db  0x0C,0x0D,0x0E,0x0F,0x10,0x11
tiles_supertile_3:    ; S3
    .db  0x12,0x13,0x14,0x15,0x16,0x17
tiles_supertile_4:    ; S4
    .db  0x18,0x19,0x1A,0x1B,0x1C,0x1D
tiles_supertile_5:    ; S5
    .db  0x1E,0x1F,0x20,0x21,0x22,0x23
tiles_supertile_6:    ; S6
    .db  0x24,0x25,0x26,0x27,0x28,0x29
tiles_supertile_7:    ; S7
    .db  0x2A,0x2B,0x2C,0x2D,0x2E,0x2F
tiles_supertiles_end:
