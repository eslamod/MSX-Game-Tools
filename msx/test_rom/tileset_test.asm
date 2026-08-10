;-----------------------------------------------------------------------------
; tileset_test.asm - ROM de prueba para los juegos de tiles de MSX Game Tools
;-----------------------------------------------------------------------------
; Ensamblar con SirCmpwn's Assembler (sasSX):
;
;     sasSX.exe tileset_test.asm --output tileset_test.rom
;
; Necesita al lado los dos ficheros que exporta el editor, renombrados a
; tiles_patterns.bin y tiles_colors.bin. Valen tambien los .asm: ver el bloque
; de datos del final y el README.
;
; Que hace:
;   - Pone GRAPHIC 2 (SCREEN 2 en MSX Basic).
;   - Copia la tabla de patrones y la de colores en los TRES tercios de pantalla,
;     que es lo que hay que hacer y no se ve en los bytes exportados.
;   - Llena la tabla de nombres con 0..255 repetido. Como un tercio son
;     exactamente 32x8 = 256 celdas, cada tercio enseña el juego entero con la
;     misma disposicion que la rejilla del editor. Si la replicacion estuviera
;     mal, el segundo y el tercer tercio saldrian con basura.
;   - Teclas 0-9 y A-F cambian el color del borde.
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

; --- Mapa de VRAM en GRAPHIC 2, el de siempre en SCREEN 2 --------------------
PATTERN_TABLE   .equ 0x0000     ; 6144: tres tercios de 2048
NAME_TABLE      .equ 0x1800     ;  768: 32x24 celdas
COLOR_TABLE     .equ 0x2000     ; 6144: tres tercios de 2048

; --- Constantes de la prueba -------------------------------------------------
TABLE_BYTES     .equ 2048       ; 256 tiles de 8 lineas, una tabla de un tercio
SCREEN_THIRDS   .equ 3
NAME_BYTES      .equ 768

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
                call SetupVdp
                call LoadPalette
                call LoadTables
                call FillNames

MainLoop:
                call ScanColorKeys
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

; Los valores clasicos de SCREEN 2. El 0xFF de R#3 y el 0x03 de R#4 no son
; direcciones sueltas: en GRAPHIC 2 esos registros llevan ademas la mascara que
; parte las tablas en los tres tercios.
VdpSetup:
                .db  0, 0x02    ; M3=1 -> GRAPHIC 2
                .db  1, 0xE0    ; 16K, pantalla activa, interrupcion del VDP
                .db  2, 0x06    ; tabla de nombres        0x1800
                .db  3, 0xFF    ; tabla de colores        0x2000
                .db  4, 0x03    ; tabla de patrones       0x0000
                .db  5, 0x36    ; atributos de sprite     0x1B00
                .db  6, 0x07    ; patrones de sprite      0x3800
                .db  7, 0x01    ; color del borde
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
; HL = direccion de VRAM (14 bits)
SetVramWrite:
                ld a,l
                out (VDP_ADDR),a
                ld a,h
                and 0x3F
                or 0x40         ; bit de escritura
                out (VDP_ADDR),a
                ret

; HL = origen en ROM, DE = destino en VRAM, BC = longitud. DE se conserva.
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
; Las dos tablas, en los tres tercios
;-----------------------------------------------------------------------------
; Aqui esta lo que la exportacion no puede meter en los bytes: en GRAPHIC 2 cada
; tercio de pantalla tiene su propia tabla de patrones y su propia tabla de
; colores. El editor define un solo juego de 256 tiles y se copia tres veces,
; que es lo que hace que un tile se vea igual este en el tercio que este.
LoadTables:
                ld hl,PatternsData
                ld de,PATTERN_TABLE
                ld bc,TABLE_BYTES
                call CopyThreeTimes

                ld hl,ColorsData
                ld de,COLOR_TABLE
                ld bc,TABLE_BYTES
                call CopyThreeTimes
                ret

; HL = origen, DE = primer destino, BC = longitud de una tabla
CopyThreeTimes:
                ld a,SCREEN_THIRDS
ThirdNext:
                push af
                push hl
                push bc

                call CopyToVram

                pop bc
                pop hl

                ; DE sigue siendo el destino: se avanza al tercio siguiente.
                push hl
                ld hl,TABLE_BYTES
                add hl,de
                ex de,hl
                pop hl

                pop af
                dec a
                jr nz,ThirdNext
                ret

;-----------------------------------------------------------------------------
; La tabla de nombres: 0..255 repetido
;-----------------------------------------------------------------------------
; Un tercio son 32x8 = 256 celdas justas, asi que cada tercio enseña el juego
; entero con la misma disposicion que la rejilla del editor. El inc a da la vuelta
; solo al pasar de 255.
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
                call SetBorder
                pop af
NoKeyA:
                bit 7,a
                jr nz,NoKeyB
                ld a,11
                call SetBorder
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
                call SetBorder
                pop bc
ScanNext:
                pop af
                inc c
                djnz ScanBit
                ret

; A = color 0..15
SetBorder:
                and 0x0F
                ld b,7
                jp WriteVdp

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
; Van entre etiquetas propias para que la ROM no dependa del nombre del juego,
; que es de donde salen las etiquetas del exportador. Para probar la salida en
; ensamblador, comenta el .incbin y descomenta el .include de al lado.
PaletteData:
                .incbin "msx_palette.bin"
              ; .include "msx_palette.asm"
PaletteEnd:

PatternsData:
                .incbin "tiles_patterns.bin"
              ; .include "tiles_patterns.asm"
PatternsEnd:

ColorsData:
                .incbin "tiles_colors.bin"
              ; .include "tiles_colors.asm"
ColorsEnd:

; Relleno hasta 16K, que es el tamano que espera un cartucho en la pagina 1.
; Con una etiqueta y no con $, porque en sass el $ dentro de una expresion no
; da el PC: se queda a cero y sale una ROM de 32K de mas.
RomEnd:
                .ds 0x8000 - RomEnd, 0xFF
