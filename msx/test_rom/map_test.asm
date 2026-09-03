;-----------------------------------------------------------------------------
; map_test.asm - ROM de prueba para los mapas de MSX Game Tools
;-----------------------------------------------------------------------------
; Ensamblar con SirCmpwn's Assembler (sasSX):
;
;     sasSX.exe map_test.asm --output map_test.rom
;
; Necesita al lado tres ficheros exportados por el editor, renombrados:
;
;     tiles_patterns.bin  y  tiles_colors.bin   del juego de tiles
;     map.bin                                   del mapa
;
; Valen tambien los .asm: ver el bloque de datos del final y el README.
;
; Que hace:
;   - Pone GRAPHIC 2 y carga el juego de tiles en los tres tercios, igual que
;     tileset_test. Sin el juego de tiles el mapa no es nada: son indices.
;   - Lee el tamano del mapa de su cabecera, que son los cuatro primeros bytes
;     del fichero: dos de ancho y dos de alto, byte bajo primero. Esa cabecera
;     es lo unico del exportador que ninguna maquina habia leido nunca, y aqui
;     se lee de verdad: si estuviera mal, el mapa saldria descuadrado o movido.
;   - Dibuja las 32x24 celdas que caben, desde donde diga la camara.
;   - Cursores para moverse, si el mapa es mas grande que la pantalla. Si cabe
;     entero, no se mueve y lo que sobra queda con el tile 0.
;
; El cartucho es de 32K y ocupa las paginas 1 y 2, a diferencia de sus dos
; hermanas, que son de 16K. La BIOS conmuta la pagina 1, donde encuentra la
; "AB", pero deja la 2 en RAM: la primera cosa que hace esta ROM al arrancar es
; engancharse ella misma ahi con ENASLT, y de eso va EnablePage2.
;
; Descontando el codigo, las dos tablas del juego de tiles y la paleta quedan
; unos 28000 bytes para el mapa, que son 28000 celdas: 224x125, 168x168 o lo
; que salga sin pasar de ahi.
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
KEY_ROW_CURSOR  .equ 8          ; fila con los cuatro cursores
FRAME_MASK      .equ 0x03       ; mueve una celda cada cuatro fotogramas
PAD_TILE        .equ 0          ; lo que se pinta fuera del mapa

; --- Variables ---------------------------------------------------------------
; En la pagina 3, que es RAM en cualquier MSX, y por debajo de 0xF380, donde
; empieza el area de trabajo de la BIOS. El cartucho no puede escribir en si
; mismo, asi que la camara tiene que vivir aqui.
CameraX         .equ 0xE000     ; columna del mapa pegada al borde izquierdo
CameraY         .equ 0xE002     ; fila del mapa pegada al borde de arriba
MapWidth        .equ 0xE004
MapHeight       .equ 0xE006
MaxCameraX      .equ 0xE008     ; hasta donde puede llegar sin salirse
MaxCameraY      .equ 0xE00A
MapCells        .equ 0xE00C     ; primera celda, pasada la cabecera
VisibleCols     .equ 0xE00E     ; 1 byte: columnas de mapa que se ven
VisibleRows     .equ 0xE00F     ; 1 byte
PadCols         .equ 0xE010     ; 1 byte: lo que sobra hasta 32
PadRows         .equ 0xE011     ; 1 byte: lo que sobra hasta 24
FrameCount      .equ 0xE012     ; 1 byte
Moved           .equ 0xE013     ; 1 byte: si la camara ha cambiado

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
                call ReadMapHeader
                call DrawMap

; Se redibuja solo cuando la camara se ha movido: son 768 bytes por VRAM y no
; hay ninguna razon para escribirlos sesenta veces por segundo si no cambia
; nada. Ademas asi se nota si el movimiento no llega, en vez de disimularlo.
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
; Lo primero de todo, porque de aqui en adelante hay datos por encima de 0x8000
; y sin esto no estarian ahi.
;
; La BIOS busca la "AB" en la pagina 1 y conmuta esa, pero deja la 2 como
; estaba, que es RAM: un cartucho de 32K no ve su propia mitad de arriba hasta
; que se engancha el mismo. Aqui se averigua en que slot esta uno mismo
; —mirando que slot hay puesto en la pagina 1, que es donde se esta
; ejecutando— y se pone ese mismo en la pagina 2.
;
; Es la rutina del Technical Handbook, tal cual. El rodeo por EXPTBL y SLTTBL
; es porque el slot puede estar expandido en subslots, y entonces el numero
; primario no basta para nombrarlo.
;
; Si alguna maquina o emulador ya hubiera conmutado la pagina 2, esto vuelve a
; poner el mismo slot y no pasa nada.
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
                or c                    ; con el bit de expandido si lo esta
                ld c,a

                inc hl                  ; SLTTBL esta cuatro bytes despues
                inc hl
                inc hl
                inc hl
                ld a,(hl)               ; que subslot hay puesto ahora mismo
                and 0x0C
                or c                    ; ya es el slot entero

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

; Los mismos valores que la ROM del tileset, para que las dos maquinas se
; configuren igual y la unica diferencia entre las dos pruebas sea lo que se
; escribe en la tabla de nombres.
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
; Las dos tablas del juego de tiles, en los tres tercios
;-----------------------------------------------------------------------------
; Igual que en tileset_test: en GRAPHIC 2 cada tercio de pantalla tiene su
; propia tabla de patrones y de colores, y el editor define un solo juego de
; 256 que hay que copiar tres veces. Aqui importa mas todavia, porque el mapa
; usa el mismo tile arriba y abajo y tiene que verse igual en los tres.
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
; La cabecera del mapa
;-----------------------------------------------------------------------------
; Los cuatro primeros bytes del fichero: ancho y alto, dos bytes cada uno y el
; bajo primero. Se leen seguidos y HL se queda apuntando a la primera celda,
; que es exactamente lo que dice el formato: la cabecera y luego las filas.
;
; De paso se calcula cuanto mapa se ve y cuanto sobra. Sale de aqui y no de
; cada redibujado porque no cambia nunca, y porque con esto el bucle de dibujo
; no tiene que comparar nada por celda.
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

                ; La camara empieza en la esquina de arriba a la izquierda.
                ld hl,0
                ld (CameraX),hl
                ld (CameraY),hl

                ; Ancho: o cabe entero, o hay sitio por donde moverse.
                ld hl,(MapWidth)
                ld de,SCREEN_COLS
                or a                    ; sin acarreo de antes
                sbc hl,de
                jr nc,WideMap

                ld hl,0                 ; mas estrecho que la pantalla
                ld (MaxCameraX),hl
                ld a,(MapWidth)         ; cabe en un byte: es menor que 32
                ld (VisibleCols),a
                ld b,a
                ld a,SCREEN_COLS
                sub b
                ld (PadCols),a
                jr HeaderRows

WideMap:
                ld (MaxCameraX),hl      ; ancho - 32
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

                ld hl,0                 ; mas bajo que la pantalla
                ld (MaxCameraY),hl
                ld a,(MapHeight)        ; cabe en un byte: es menor que 24
                ld (VisibleRows),a
                ld b,a
                ld a,SCREEN_ROWS
                sub b
                ld (PadRows),a
                ret

TallMap:
                ld (MaxCameraY),hl      ; alto - 24
                ld a,SCREEN_ROWS
                ld (VisibleRows),a
                xor a
                ld (PadRows),a
                ret

;-----------------------------------------------------------------------------
; Dibujar la ventana que se ve
;-----------------------------------------------------------------------------
; La tabla de nombres entera, de un tiron: 768 bytes son unos tres milisegundos
; y se hace justo despues del retrazo, asi que no se parte la imagen. Redibujar
; todo en vez de mover lo que ya hay es de mas trabajo para el VDP, pero deja el
; codigo sin estado que pueda desincronizarse, que en una prueba vale mas.
DrawMap:
                ; primera celda visible = celdas + camaraY * ancho + camaraX
                ld de,(MapWidth)
                ld bc,(CameraY)
                call Mul16
                ld de,(CameraX)
                add hl,de
                ld de,(MapCells)
                add hl,de

                di
                ex de,hl                ; DE = origen en ROM
                ld hl,NAME_TABLE
                call SetVramWrite
                ex de,hl                ; HL = origen otra vez

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

                ; Lo que falte hasta las 32 columnas, si el mapa es estrecho.
                ; Con salto y no con djnz a secas: djnz con B a cero da 256
                ; vueltas y llenaria la pantalla de relleno.
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
                ld de,(MapWidth)        ; a la fila siguiente del mapa
                add hl,de
                pop bc
                djnz RowNext

                ; Y las filas de debajo del mapa, si sobra pantalla.
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

; DE * BC -> HL. Se pierde lo que pase de 16 bits, cosa que aqui no llega a
; pasar: el mapa entero tiene que caber en el cartucho, asi que el mayor
; desplazamiento posible son unos 28000.
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
; La camara
;-----------------------------------------------------------------------------
; Devuelve A distinto de cero si se ha movido, que es lo que decide si hay que
; redibujar.
;
; Una celda cada cuatro fotogramas: a una por fotograma se cruzan treinta y dos
; columnas en medio segundo y no hay quien pare donde quiere.
MoveCamera:
                ld a,(FrameCount)
                inc a
                ld (FrameCount),a
                and FRAME_MASK
                jr z,ReadCursors
                xor a                   ; este fotograma no toca
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
                ret z                   ; ya pegada al borde
                dec hl
                ld (CameraX),hl
                jr MarkMoved

CameraRight:
                ld hl,(CameraX)
                ld de,(MaxCameraX)
                or a
                sbc hl,de
                ret nc                  ; ya en el tope
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
; Por el contador de la BIOS y no leyendo el registro de estado del VDP: la
; rutina de interrupcion de la BIOS lo lee sesenta veces por segundo y borra el
; aviso al leerlo, asi que quien lo espere por su cuenta se lo pierde casi
; siempre. El byte bajo de JIFFY cambia en cada interrupcion y con eso basta.
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
; El byte 0x002D de la BIOS dice la version: 0 es MSX1, 1 es MSX2, 2 es MSX2+ y
; 3 es TurboR. En un MSX1 los 16 colores son fijos y no hay nada que cargar.
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
; Van entre etiquetas propias para que la ROM no dependa del nombre que le
; hayas puesto al mapa ni al juego de tiles, que es de donde salen las
; etiquetas del exportador. Para probar la salida en ensamblador, comenta el
; .incbin y descomenta el .include de al lado.
PaletteData:
                .incbin "msx_palette.bin"
              ; .include "msx_palette.asm"
PaletteEnd:

PatternsData:
                .incbin "tiles_cars_game_patterns.bin"
              ; .include "tiles_patterns.asm"
PatternsEnd:

ColorsData:
                .incbin "tiles_cars_game_colors.bin"
              ; .include "tiles_colors.asm"
ColorsEnd:

; La cabecera de cuatro bytes viene dentro del fichero, asi que MapData apunta
; al ancho y las celdas empiezan cuatro bytes despues. No se calcula con una
; constante: lo hace ReadMapHeader avanzando, que es como se lee un formato con
; cabecera y no deja que las dos cosas se separen.
MapData:
                .incbin "map_cars_game.bin"
              ; .include "map.asm"
MapEnd:

; Relleno hasta 32K, que es lo que ocupa un cartucho en las paginas 1 y 2.
; Con una etiqueta y no con $, porque en sass el $ dentro de una expresion no
; da el PC: se queda a cero y sale una ROM enorme.
RomEnd:
                .ds 0xC000 - RomEnd, 0xFF
