;-----------------------------------------------------------------------------
; sprites_test.asm - ROM de prueba para los grupos de sprites de MSX Game Tools
;-----------------------------------------------------------------------------
; Ensamblar con SirCmpwn's Assembler (sasSX):
;
;     sasSX.exe sprites_test.asm --output sprites_test.rom
;
; Necesita al lado los dos ficheros que exporta el editor, renombrados a
; bank_patterns y bank_groups. Vale la salida binaria o la de ensamblador: ver
; el bloque de datos del final y el README. El banco tiene que ser MSX2, que es
; el que exporta los 16 bytes de color por sprite.
;
; Que hace:
;   - GRAPHIC 3 (SCREEN 4) con sprites de modo 2, 16x16.
;   - Toda la pantalla a color 0, o sea transparente: se ve el borde (R#7).
;   - Vuelca los patrones exportados en el generador de patrones de sprites.
;   - Recorre los grupos y les va dando planos por orden, gastandolos segun
;     los encuentra. Cuando se acaban los 32 planos deja de poner sprites.
;   - Cada grupo se centra en x=128 y baja GROUP_Y_STEP pixeles respecto al
;     anterior; a eso se le suman los offsets propios de cada sprite.
;   - Teclas 0-9 y A-F cambian el color de fondo.
;   - F1 alterna el bit de ampliacion (sprites a x2) y rehace la tabla de
;     atributos: al doblar el tamano se doblan tambien las distancias al
;     centro, y el que se salga de la pantalla se aparca fuera.
;-----------------------------------------------------------------------------

; --- Puertos del VDP ---------------------------------------------------------
VDP_DATA        .equ 0x98       ; datos de VRAM
VDP_ADDR        .equ 0x99       ; direccion de VRAM y registros

; --- BIOS --------------------------------------------------------------------
SNSMAT          .equ 0x0141     ; A = fila de la matriz -> A, bit a 0 = pulsada
MSX_VERSION     .equ 0x002D     ; 0 = MSX1, 1 = MSX2, 2 = MSX2+, 3 = TurboR

; --- Paleta ------------------------------------------------------------------
VDP_PALETTE     .equ 0x9A       ; puerto por el que entran los colores
PALETTE_BYTES   .equ 32         ; 16 colores de dos bytes

; --- Mapa de VRAM en GRAPHIC 3, pagina 0 -------------------------------------
; El manual del V9938 propone el generador de sprites en 1C00H, pero ahi solo
; caben 32 patrones de 16x16. Se sube a 1800H para que quepan los 64 del banco.
PATTERN_GEN     .equ 0x0000     ; 6144  patrones de pantalla
SPRITE_GEN      .equ 0x1800     ; 2048  patrones de sprite (64 de 16x16)
COLOR_TABLE     .equ 0x2000     ; 6144  colores de pantalla
NAME_TABLE      .equ 0x3800     ;  768  nombres
SPRITE_COLOR    .equ 0x3C00     ;  512  tabla de color de sprites (32 x 16)
SPRITE_ATTR     .equ 0x3E00     ;  128  tabla de atributos (32 x 4)

; --- Constantes de la prueba -------------------------------------------------
MAX_PLANES      .equ 32
SPRITE_END_Y    .equ 0xD8       ; 216: en modo 2 corta el resto de planos
CENTRE_X        .equ 128
CENTRE_Y        .equ 30         ; primer grupo, luego va bajando
GROUP_Y_STEP    .equ 20
REG1_BASE       .equ 0x42       ; pantalla activa, sprites 16x16, MAG=0
SCREEN_LINES    .equ 192
HIDDEN_Y        .equ 200        ; debajo de la pantalla, y no es 208 ni 216

; --- Estado -----------------------------------------------------------------
; Un byte de RAM para el valor actual de R#1, que es donde vive el bit MAG.
; La pagina 3 es RAM en cualquier MSX2 y aqui nadie mas la toca: el INIT del
; cartucho no vuelve al BASIC. Hace falta en memoria y no en un registro porque
; lo consultan tanto el bucle de teclado como la construccion de la tabla de
; atributos, que se rehace entera cada vez que cambia la ampliacion.
REG1_VALUE      .equ 0xC000

;-----------------------------------------------------------------------------
; Cabecera de cartucho
;-----------------------------------------------------------------------------
                .org 0x4000

                .db "AB"
                .dw Start       ; INIT
                .dw 0           ; STATEMENT
                .dw 0           ; DEVICE
                .dw 0           ; TEXT
                .db 0,0,0,0,0,0 ; reservados

;-----------------------------------------------------------------------------
; Arranque
;-----------------------------------------------------------------------------
Start:
                ld a,REG1_BASE
                ld (REG1_VALUE),a

                call SetupVdp
                call LoadPalette
                call ClearScreen
                call LoadPatterns
                call BuildSprites

MainLoop:
                call ScanColorKeys
                call ScanF1
                jr MainLoop

;-----------------------------------------------------------------------------
; Registros del VDP
;-----------------------------------------------------------------------------
SetupVdp:
                ld hl,VdpSetup
SetupNext:
                ld a,(hl)
                cp 0xFF
                ret z
                ld b,a          ; numero de registro
                inc hl
                ld a,(hl)       ; valor
                inc hl
                push hl
                call WriteVdp
                pop hl
                jr SetupNext

VdpSetup:
                .db  0, 0x04    ; M4=1, M3=0 -> GRAPHIC 3
                .db  1, REG1_BASE
                .db  2, 0x0E    ; tabla de nombres        0x3800
                .db  3, 0xFF    ; tabla de color          0x2000 (con R#10)
                .db 10, 0x00
                .db  4, 0x03    ; generador de patrones   0x0000
                .db  5, 0x7F    ; atributos de sprite     0x3E00 (con R#11)
                .db 11, 0x00
                .db  6, 0x03    ; generador de sprites    0x1800
                .db  7, 0x00    ; color de fondo inicial
                .db  8, 0x08    ; VR=1, sprites visibles
                .db  9, 0x00    ; 192 lineas, NTSC
                .db 23, 0x00    ; sin desplazamiento vertical
                .db 14, 0x00    ; pagina de VRAM 0
                .db 0xFF        ; fin de tabla

; A = valor, B = numero de registro
WriteVdp:
                di
                out (VDP_ADDR),a
                ld a,b
                or 0x80
                out (VDP_ADDR),a
                ei
                ret

;-----------------------------------------------------------------------------
; Acceso a VRAM
;-----------------------------------------------------------------------------
; Poner la direccion son dos OUT que el VDP cuenta como un par, y ademas deja
; abierta una transferencia. Si una interrupcion se cuela en medio, la rutina de
; la BIOS lee el registro de estado y reinicia el biestable del puerto 0x99: el
; segundo OUT pasa a interpretarse como el primero de otro par. Por eso el di no
; esta aqui sino en quien llama, que es el que sabe donde acaba la transferencia.
;
; HL = direccion de VRAM (14 bits; aqui nunca se pasa de 0x3FFF)
SetVramWrite:
                ld a,l
                out (VDP_ADDR),a
                ld a,h
                and 0x3F
                or 0x40         ; bit de escritura
                out (VDP_ADDR),a
                ret

; HL = destino en VRAM, BC = longitud, A = valor
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

; HL = origen en RAM/ROM, DE = destino en VRAM, BC = longitud
CopyToVram:
                di
                ex de,hl        ; HL = destino
                call SetVramWrite
                ex de,hl        ; HL = origen otra vez
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
; Pantalla entera a color 0
;-----------------------------------------------------------------------------
; Con los tres a cero cada pixel usa el color 0, que es transparente, asi que
; se ve el color de fondo de R#7 y se puede cambiar a gusto.
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
; Patrones de sprite
;-----------------------------------------------------------------------------
LoadPatterns:
                ld hl,PatternsData
                ld de,SPRITE_GEN
                ld bc,PatternsEnd-PatternsData
                call CopyToVram
                ret

;-----------------------------------------------------------------------------
; Atributos y colores a partir de los grupos
;-----------------------------------------------------------------------------
; Formato del fichero de grupos (MSX2), tal cual lo exporta el editor:
;   por grupo:  1 byte con cuantos sprites tiene
;   por sprite: 16 bytes de color + offset Y + offset X + patron (ya x4)
;
; IX recorre el fichero, IY es el plano que toca, C el desplazamiento vertical
; que lleva acumulado el grupo actual.
BuildSprites:
                ld a,SPRITE_END_Y
                ld hl,SPRITE_ATTR
                ld bc,128
                call FillVram   ; todos los planos escondidos de entrada

                ld ix,GroupsData
                ld iy,0
                ld c,0

NextGroup:
                push ix
                pop hl
                ld de,GroupsEnd
                or a
                sbc hl,de
                jr nc,SpritesDone   ; se acabo el fichero

                ld b,(ix+0)         ; sprites de este grupo
                inc ix
                ld a,b
                or a
                jr z,GroupDone      ; un grupo vacio no gasta planos

NextMember:
                push iy
                pop hl
                ld a,l
                cp MAX_PLANES
                jr nc,SpritesDone   ; sin planos libres no se sigue

                call WriteMember
                inc iy
                djnz NextMember

GroupDone:
                ld a,c
                add a,GROUP_Y_STEP
                ld c,a
                jr NextGroup

; El primer plano libre lleva Y=216, que en modo 2 corta el resto.
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

; IX = miembro, IY = plano, C = desplazamiento del grupo, B = miembros que faltan
WriteMember:
                push bc         ; el contador de miembros y el desplazamiento

                ; Las coordenadas primero, que necesitan C y los registros
                ; libres; despues los volcados se comen HL, BC y A.
                ; Quedan en D (Y) y E (X).
                call MemberCoords

                ; --- 16 bytes de color en SPRITE_COLOR + plano*16
                push iy
                pop hl
                add hl,hl
                add hl,hl
                add hl,hl
                add hl,hl
                ld bc,SPRITE_COLOR
                add hl,bc

                di              ; las dos transferencias, de una pieza
                call SetVramWrite

                push ix
                pop hl
                ld b,16
ColorNext:
                ld a,(hl)
                out (VDP_DATA),a
                inc hl
                djnz ColorNext

                ; --- 4 bytes de atributo en SPRITE_ATTR + plano*4
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

                ld a,(ix+18)    ; patron, ya multiplicado por 4
                out (VDP_DATA),a

                xor a           ; en modo 2 el cuarto byte no lleva color
                out (VDP_DATA),a
                ei

                pop bc          ; recupera el contador y el desplazamiento

                ld de,19
                add ix,de
                ret

; Coordenadas del sprite en la escala que toque.
;
; Lo que se escala son las distancias al centro, no la posicion: el grupo se
; queda donde estaba y crece desde ahi. Multiplicar tambien el 128 lo mandaria
; todo a 256, fuera de la pantalla.
;
; Se calcula con 16 bits con signo a proposito. Un desplazamiento de grupo alto
; (GROUP_Y_STEP por 32 planos) duplicado se sale de 8 bits mucho antes de salirse
; de la pantalla, y con 8 bits el desbordamiento daria la vuelta y colocaria el
; sprite arriba en lugar de quitarlo de en medio.
;
; IX = miembro, C = desplazamiento del grupo -> D = Y, E = X
MemberCoords:
                ; ---- Y = CENTRE_Y + (offsetY + desplazamiento) * escala
                ld a,(ix+16)
                call SignExtend ; HL = offset Y con signo
                ld b,0          ; B ya esta guardado, se puede usar
                add hl,bc       ; + lo que baja el grupo
                call ScaleIfMag
                ld bc,CENTRE_Y
                add hl,bc

                ld a,h
                or a
                jr nz,HideMember        ; negativo, o pasado de 255
                ld a,l
                cp SCREEN_LINES
                jr nc,HideMember
                ld d,a

                ; ---- X = CENTRE_X + offsetX * escala
                ld a,(ix+17)
                call SignExtend
                call ScaleIfMag
                ld bc,CENTRE_X
                add hl,bc

                ld a,h
                or a
                jr nz,HideMember        ; no cabe en el byte de X
                ld e,l
                ret

; Al ampliar, un sprite que antes entraba puede acabar fuera. Se aparca debajo
; de la pantalla en vez de recortarlo: 200 no es 216, asi que no corta el
; procesado de los planos que vengan detras.
HideMember:
                ld d,HIDDEN_Y
                ld e,0
                ret

; A con signo -> HL
SignExtend:
                ld l,a
                rla             ; el bit de signo al acarreo
                sbc a,a         ; 0x00 o 0xFF
                ld h,a
                ret

; HL x2 si el bit MAG de R#1 esta puesto
ScaleIfMag:
                ld a,(REG1_VALUE)
                rrca            ; MAG es el bit 0
                ret nc
                add hl,hl
                ret

;-----------------------------------------------------------------------------
; Teclado
;-----------------------------------------------------------------------------
; Filas de la matriz: 0 = "0".."7", 1 = "8","9" en los bits 0 y 1,
; 2 = "A" y "B" en los bits 6 y 7, 3 = "C".."F" en los bits 0..3.
ScanColorKeys:
                xor a
                call KeyRow
                ld c,0
                call ScanRow

                ld a,1
                call KeyRow
                or 0xFC         ; el resto de la fila no interesa
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

; A = estado de la fila (bit a 0 = pulsada), C = color del bit 0
ScanRow:
                ld b,8
ScanBit:
                rrca
                push af
                jr c,ScanNext   ; a 1 no esta pulsada
                push bc
                ld a,c
                call SetBackdrop
                pop bc
ScanNext:
                pop af
                inc c
                djnz ScanBit
                ret

; A = color 0..15
SetBackdrop:
                and 0x0F
                ld b,7
                jp WriteVdp

; F1 esta en la fila 6, bit 5. Se espera a soltarla para no alternar sin parar.
ScanF1:
                ld a,6
                call KeyRow
                bit 5,a
                ret nz

                ld a,(REG1_VALUE)
                xor 1           ; MAG, bit 0 de R#1
                ld (REG1_VALUE),a
                ld b,1
                call WriteVdp

                ; Los sprites miden el doble, asi que las distancias entre ellos
                ; tambien: se rehace la tabla de atributos con la nueva escala.
                call BuildSprites

WaitRelease:
                ld a,6
                call KeyRow
                bit 5,a
                jr z,WaitRelease
                ret

; A = fila -> A = estado. La BIOS se guarda entera por si acaso.
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
; La paleta, solo si la maquina la tiene
;-----------------------------------------------------------------------------
; El byte 0x002D de la BIOS dice la version: 0 es MSX1, 1 es MSX2, 2 es MSX2+ y
; 3 es TurboR. En un MSX1 los 16 colores son fijos y no hay nada que cargar.
;
; Se comprueba en ejecucion en vez de con ensamblado condicional a proposito: asi
; hay una sola ROM que funciona en las dos maquinas, en lugar de dos que generar y
; distribuir por separado. Los 32 bytes de la paleta viajan siempre y no se notan.
LoadPalette:
                ld a,(MSX_VERSION)
                or a
                ret z                   ; MSX1: sin paleta

                di

                ; R#16 = 0: el indice del color que se va a escribir. Avanza solo
                ; con cada pareja de bytes, asi que basta con ponerlo una vez.
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
; Datos exportados por el editor
;-----------------------------------------------------------------------------
; Van entre etiquetas propias porque el fichero de grupos no lleva cuantos grupos
; hay: se recorre hasta llegar al final. Poniendolas aqui la ROM no depende del
; nombre del banco, que es de donde salen las etiquetas del exportador.
;
; Para probar la salida en ensamblador en vez de la binaria, comenta el .incbin
; y descomenta el .include de al lado. El resultado es el mismo byte a byte.
PaletteData:
                .incbin "msx_palette.bin"
              ; .include "msx_palette.asm"
PaletteEnd:

PatternsData:
                .incbin "bank_patterns.bin"
              ; .include "bank_patterns.asm"
PatternsEnd:

GroupsData:
                .incbin "bank_groups.bin"
              ; .include "bank_groups.asm"
GroupsEnd:

; Relleno hasta 16K, que es el tamano que espera un cartucho en la pagina 1.
; Con una etiqueta y no con $, porque en sass el $ dentro de una expresion no
; da el PC: se queda a cero y sale una ROM de 32K de mas.
RomEnd:
                .ds 0x8000 - RomEnd, 0xFF
