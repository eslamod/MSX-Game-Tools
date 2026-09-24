;-----------------------------------------------------------------------------
; supertile_test.asm - ROM de prueba para los mapas de supertiles
;-----------------------------------------------------------------------------
; Ensamblar con SirCmpwn's Assembler (sasSX):
;
;     sasSX.exe supertile_test.asm --output supertile_test.rom
;
; Necesita al lado cuatro ficheros exportados por el editor, renombrados:
;
;     tiles_patterns.bin  y  tiles_colors.bin   del juego de tiles
;     tiles_supertiles.bin                      la tabla de supertiles
;     map.bin                                   del mapa
;
; Valen tambien los .asm: ver el bloque de datos del final y el README.
;
; Que hace:
;   - Lo mismo que la ROM del mapa hasta cargar el juego de tiles en los tres
;     tercios.
;   - Lee la cabecera de la tabla de supertiles: ancho, alto y cuantos, un byte
;     cada cosa, con el convenio de que una cuenta de 0 son 256.
;   - Dibuja el mapa resolviendo cada celda de pantalla a traves de la tabla:
;     una celda del mapa ya no es un tile, es un supertile entero, y el numero
;     que guarda es el del supertile.
;   - Cursores para moverse, de supertile en supertile.
;
; Lo que de verdad se prueba aqui es la tabla: que la cabecera diga la verdad y
; que los tiles de cada supertile esten en el orden que dice el exportador, de
; izquierda a derecha y de arriba abajo. Si el orden estuviera cambiado, cada
; supertile saldria transpuesto -y con supertiles cuadrados eso es justo lo que
; no canta a simple vista, asi que conviene probar con uno rectangular, 2x3 o
; 3x2, donde un orden equivocado descuadra el dibujo entero.
;
; El cartucho es de 32K y ocupa las paginas 1 y 2, con el mismo ENASLT que la
; ROM del mapa: la tabla de supertiles puede ser grande y con 16K no cabria.
;-----------------------------------------------------------------------------

; --- Puertos del VDP ---------------------------------------------------------
VDP_DATA        .equ 0x98       ; datos de VRAM
VDP_ADDR        .equ 0x99       ; direccion de VRAM y registros

; --- BIOS --------------------------------------------------------------------
SNSMAT          .equ 0x0141     ; A = fila de la matriz -> A, bit a 0 = pulsada
MSX_VERSION     .equ 0x002D     ; 0 = MSX1, 1 = MSX2, 2 = MSX2+, 3 = TurboR
JIFFY           .equ 0xFC9E     ; contador de interrupciones del VDP, en RAM
ENASLT          .equ 0x0024     ; A = slot, H = pagina -> la conmuta
RSLREG          .equ 0x0138     ; -> A = registro de slots primarios
EXPTBL          .equ 0xFCC1     ; un byte por slot: bit 7 si esta expandido
SLTTBL          .equ 0xFCC5     ; un byte por slot: que subslot hay puesto

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
SCREEN_COLS     .equ 32
SCREEN_ROWS     .equ 24
NAME_BYTES      .equ 768        ; 32x24
KEY_ROW_CURSOR  .equ 8          ; fila con los cuatro cursores
FRAME_MASK      .equ 0x03       ; mueve un supertile cada cuatro fotogramas
PAD_TILE        .equ 0          ; lo que se pinta fuera del mapa
SUPER_HEADER    .equ 3          ; ancho, alto y cuantos

; --- Variables ---------------------------------------------------------------
; En la pagina 3, que es RAM en cualquier MSX, y por debajo de 0xF380, donde
; empieza el area de trabajo de la BIOS.
CameraX         .equ 0xE000     ; columna del mapa, en supertiles
CameraY         .equ 0xE002
MapWidth        .equ 0xE004     ; en supertiles
MapHeight       .equ 0xE006
MaxCameraX      .equ 0xE008
MaxCameraY      .equ 0xE00A
MapCells        .equ 0xE00C     ; primera celda, pasada la cabecera del mapa
SuperWidth      .equ 0xE00E     ; 1 byte: tiles que mide un supertile
SuperHeight     .equ 0xE00F     ; 1 byte
SuperArea       .equ 0xE010     ; 1 byte: ancho por alto
VisibleSupersX  .equ 0xE011     ; 1 byte: supertiles que entran a lo ancho
VisibleSupersY  .equ 0xE012     ; 1 byte
FrameCount      .equ 0xE013     ; 1 byte
Moved           .equ 0xE014     ; 1 byte

; La tabla de nombres se arma aqui y se vuelca de una vez. Armarla en VRAM
; obligaria a escribir cada supertile en filas salteadas, con un cambio de
; direccion por fila; asi es una escritura seguida de 768 bytes.
NameBuffer      .equ 0xE100     ; 768 bytes

; Donde empieza cada supertile dentro de la tabla, ya calculado. Se hace una vez
; al arrancar y ahorra una multiplicacion por cada una de las 768 celdas.
SuperAddr       .equ 0xE400     ; 256 entradas de dos bytes

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
; La segunda mitad del cartucho
;-----------------------------------------------------------------------------
; La BIOS busca la "AB" en la pagina 1 y conmuta esa, pero deja la 2 como
; estaba, que es RAM. Es la rutina del Technical Handbook, la misma que en la
; ROM del mapa.
EnablePage2:
                call RSLREG             ; slots primarios de las cuatro paginas
                rrca                    ; bits 2 y 3: el de la pagina 1
                rrca
                and 0x03
                ld c,a
                ld b,0

                ld hl,EXPTBL            ; ¿esta expandido ese slot?
                add hl,bc
                ld c,a                  ; el primario, guardado
                ld a,(hl)
                and 0x80
                or c
                ld c,a

                inc hl                  ; SLTTBL esta cuatro bytes despues
                inc hl
                inc hl
                inc hl
                ld a,(hl)
                and 0x0C
                or c

                ld h,0x80               ; una direccion de la pagina 2
                jp ENASLT

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
; El di no esta aqui sino en quien llama: poner la direccion son dos OUT que el
; VDP cuenta como un par, y una interrupcion en medio reinicia el biestable del
; puerto 0x99.
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

; HL = origen en ROM o RAM, DE = destino en VRAM, BC = longitud. DE se conserva.
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
; Las dos tablas del juego de tiles, en los tres tercios
;-----------------------------------------------------------------------------
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
; La cabecera de la tabla de supertiles
;-----------------------------------------------------------------------------
; Tres bytes: ancho, alto y cuantos. La cuenta no hace falta para dibujar -el
; mapa dice que supertile va en cada celda- pero el ancho y el alto si: de ahi
; sale cuantos tiles ocupa cada uno y cuantos entran en la pantalla.
ReadSuperHeader:
                ld hl,SuperData
                ld a,(hl)
                ld (SuperWidth),a
                inc hl
                ld a,(hl)
                ld (SuperHeight),a

                ; El area, que es lo que ocupa un supertile en la tabla.
                ld a,(SuperWidth)
                ld b,a
                ld a,(SuperHeight)
                call MulBytes           ; A = B * A
                ld (SuperArea),a

                ; Cuantos supertiles entran en la pantalla, a lo ancho y a lo
                ; alto. Division por restas: se hace una vez y el divisor es
                ; como mucho ocho.
                ld a,SCREEN_COLS
                ld hl,SuperWidth
                call DivideByte
                ld (VisibleSupersX),a

                ld a,SCREEN_ROWS
                ld hl,SuperHeight
                call DivideByte
                ld (VisibleSupersY),a
                ret

; A = B * A, con el resultado en A. Los dos caben en un byte y el producto
; tambien: ocho por ocho son 64.
MulBytes:
                ld c,a          ; C = lo que se va sumando
                xor a           ; y el acumulador empieza a cero

                ; Si B es cero el producto es cero, y djnz daria 256 vueltas. Se
                ; comprueba con inc/dec y no con «or b», que ademas de mirar el
                ; cero deja A valiendo B: asi el producto salia b + b*c, y con
                ; supertiles de 2x2 el area daba 6 en vez de 4.
                inc b
                dec b
                ret z
MulNextByte:
                add a,c
                djnz MulNextByte
                ret

; A = A / (HL), por restas. Devuelve al menos uno: con un supertile mas ancho
; que la pantalla se ensena el que hay y no cero.
DivideByte:
                ld c,(hl)

                ; Un lado a cero no puede ser, pero un fichero estropeado lo
                ; traeria y esto se quedaria restando cero para siempre.
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
; Donde empieza cada supertile dentro de la tabla
;-----------------------------------------------------------------------------
; Se calcula una vez y se guarda: si no, dibujar la pantalla serian 768
; multiplicaciones, una por celda. Con esto cada celda es una lectura de tabla.
BuildSuperAddr:
                ld hl,SuperData
                ld bc,SUPER_HEADER
                add hl,bc               ; HL = primer supertile

                ld de,SuperAddr

                ld a,(SuperArea)
                ld c,a
                ld b,0                  ; BC = lo que ocupa un supertile

                ; 256 vueltas: el contador arranca en cero y se acaba cuando da
                ; la vuelta, que es justo el tope de supertiles que un mapa
                ; puede nombrar.
                xor a
BuildNext:
                push af

                ld a,l
                ld (de),a
                inc de
                ld a,h
                ld (de),a
                inc de

                add hl,bc               ; al siguiente supertile

                pop af
                inc a
                jr nz,BuildNext
                ret

;-----------------------------------------------------------------------------
; La cabecera del mapa
;-----------------------------------------------------------------------------
; Los mismos cuatro bytes que en un mapa normal: ancho y alto, dos bytes cada
; uno y el bajo primero. Lo que cambia es la unidad, que aqui son supertiles.
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

                ; Hasta donde puede ir la camara: lo que sobra de mapa despues
                ; de lo que se ve. Si cabe entero, no se mueve.
                ld hl,(MapWidth)
                ld a,(VisibleSupersX)
                call MaxCamera
                ld (MaxCameraX),hl

                ld hl,(MapHeight)
                ld a,(VisibleSupersY)
                call MaxCamera
                ld (MaxCameraY),hl
                ret

; HL = lado del mapa, A = lo que se ve -> HL = lo que sobra, o cero.
MaxCamera:
                ld e,a
                ld d,0
                or a
                sbc hl,de
                ret nc
                ld hl,0
                ret

;-----------------------------------------------------------------------------
; Dibujar
;-----------------------------------------------------------------------------
; Se arma la tabla de nombres entera en RAM y se vuelca de una vez. Cada celda
; de pantalla sale de dos lecturas: el numero de supertile que hay en el mapa y,
; dentro de ese supertile, el tile que toca por la fila y la columna en las que
; se esta. Los contadores van sumando en vez de dividir, que es lo mismo y no
; cuesta una division por celda.
DrawMap:
                ; HL = primera celda visible del mapa
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
                ld hl,(RowStart)        ; la fila del mapa en la que estamos

                ; Desplazamiento dentro del supertile por la fila: ancho * fila.
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
                push hl                 ; puntero al mapa

                ; La direccion del supertile que hay en esta celda.
                ld l,(hl)
                ld h,0
                add hl,hl               ; dos bytes por entrada
                ld bc,SuperAddr
                add hl,bc
                ld a,(hl)
                inc hl
                ld h,(hl)
                ld l,a                  ; HL = primer tile del supertile

                ; Mas la fila y la columna dentro de el.
                ld a,(RowOffset)
                ld c,a
                ld a,(ColInSuper)
                add a,c
                ld c,a
                ld b,0
                add hl,bc

                ld a,(hl)               ; el numero de tile
                ld (de),a               ; a la tabla de nombres en RAM
                inc de

                pop hl                  ; puntero al mapa otra vez

                ; La columna siguiente del supertile, y al siguiente cuando se
                ; acaba.
                ld a,(ColInSuper)
                inc a
                ld c,a
                ld a,(SuperWidth)
                cp c
                jr nz,ColSame
                xor a                   ; se acabo el supertile
                inc hl                  ; a la celda siguiente del mapa
                jr ColStore
ColSame:
                ld a,c
ColStore:
                ld (ColInSuper),a

                ld a,(ColsLeft)
                dec a
                ld (ColsLeft),a
                jr nz,ColLoop

                ; La fila siguiente del supertile, y a la fila siguiente del
                ; mapa cuando se acaba.
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

                ; Y de RAM a la tabla de nombres de una escritura.
                ld hl,NameBuffer
                ld de,NAME_TABLE
                ld bc,NAME_BYTES
                jp CopyToVram

; DE * BC -> HL. Se pierde lo que pase de 16 bits, que aqui no llega a pasar.
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
; La camara, en supertiles
;-----------------------------------------------------------------------------
MoveCamera:
                ld a,(FrameCount)
                inc a
                ld (FrameCount),a
                and FRAME_MASK
                jr z,ReadCursors
                xor a
                ret

; Fila 8 de la matriz: bit 4 izquierda, bit 5 arriba, bit 6 abajo y bit 7
; derecha. Un bit a cero es la tecla pulsada.
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
; Esperar al fotograma siguiente
;-----------------------------------------------------------------------------
; Por el contador de la BIOS: su rutina de interrupcion lee el registro de
; estado del VDP sesenta veces por segundo y borra el aviso al leerlo, asi que
; quien lo espere por su cuenta se lo pierde casi siempre.
WaitFrame:
                ld hl,JIFFY
                ld a,(hl)
FrameNext:
                cp (hl)
                jr z,FrameNext
                ret

;-----------------------------------------------------------------------------
; La paleta, solo si la maquina la tiene
;-----------------------------------------------------------------------------
LoadPalette:
                ld a,(MSX_VERSION)
                or a
                ret z                   ; MSX1: sin paleta

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
; Variables del dibujo
;-----------------------------------------------------------------------------
; En ROM no se puede escribir, asi que estas viven donde las demas. Van aqui
; abajo y no arriba para tenerlas al lado de quien las usa.
RowStart        .equ 0xE020     ; puntero a la fila del mapa que se esta pintando
RowsLeft        .equ 0xE022     ; 1 byte
ColsLeft        .equ 0xE023     ; 1 byte
RowInSuper      .equ 0xE024     ; 1 byte: por que fila del supertile vamos
ColInSuper      .equ 0xE025     ; 1 byte
RowOffset       .equ 0xE026     ; 1 byte: ancho * RowInSuper, ya calculado

;-----------------------------------------------------------------------------
; Datos exportados por el editor
;-----------------------------------------------------------------------------
; Entre etiquetas propias para que la ROM no dependa del nombre que le hayas
; puesto al juego ni al mapa. Para probar la salida en ensamblador, comenta el
; .incbin y descomenta el .include de al lado.
PaletteData:
                .incbin "msx_palette.bin"
              ; .include "msx_palette.asm"
PaletteEnd:

PatternsData:
              ;  .incbin "tiles_patterns.bin"
                 .include "tileset_super_patterns.asm"
PatternsEnd:

ColorsData:
              ;  .incbin "tiles_colors.bin"
                .include "tileset_super_colors.asm"
ColorsEnd:

; La tabla: tres bytes de cabecera y los tiles de cada supertile.
SuperData:
              ; .incbin "tiles_supertiles.bin"
                .include "tileset_super_supertiles.asm"
SuperEnd:

; El mapa, con sus celdas en numeros de supertile. Con otro nombre que el de un
; mapa normal a proposito: son mapas distintos y no se pueden intercambiar,
; porque alli una celda es un tile y aqui un supertile.
MapData:
              ;  .incbin "super_map.bin"
                .include "Mapa_superTiles.asm"
MapEnd:

; Relleno hasta 32K, que es lo que ocupa un cartucho en las paginas 1 y 2.
; Con una etiqueta y no con $, porque en sass el $ dentro de una expresion no
; da el PC: se queda a cero y sale una ROM enorme.
RomEnd:
                .ds 0xC000 - RomEnd, 0xFF
