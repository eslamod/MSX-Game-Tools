;-----------------------------------------------------------------------------
; animation_player.asm - reproductor del formato de animaciones de MSX Game Tools
;-----------------------------------------------------------------------------
; Se incluye desde sprites_test.asm, que es quien trae los grupos: una animacion
; apunta a los grupos por el sitio que ocupan en la tabla, asi que solo se puede
; comprobar donde esa tabla existe y se sabe buena.
;
; El bloque de datos puede estar vacio. Entonces AnimInit no encuentra nada, la
; ROM se comporta como siempre -los grupos puestos en la rejilla- y aqui no se
; gasta ni un plano.
;
; --- El formato ---------------------------------------------------------------
; Por animacion: un byte con que hacer al acabar, los pasos, y un 0x00.
;
;   0x00                                          se acabo
;   0x01, a que apunta, cuanto espera             ensenalo y espera
;   0x02, apunta, espera, Y, X, aviso             lo mismo, desplazado
;   0x03, vueltas                                 empieza un bucle
;   0x04                                          y aqui acaba
;   0x05, color                                   el color de aqui en adelante
;
; Las esperas van en interrupciones y nunca son cero. Los desplazamientos son en
; complemento a dos y absolutos: cada uno sustituye al anterior, no se suman.
;
; El color solo lo traen las animaciones de patrones: un grupo ya trae el color
; de cada uno de sus sprites. Va aparte y no dentro del fotograma para pagarlo
; solo cuando cambia. Es un indice de color y nada mas: donde se escribe depende
; del modo de sprites, y eso lo decide ANIM_COLOR_TABLE aqui abajo.
;
; --- Por que se resuelve en una tabla ------------------------------------------
; Se recorre la tira una vez al arrancar y se deja una lista plana de fotogramas
; en RAM, seis bytes cada uno. El color va dentro de cada fotograma y no como un
; estado aparte, que es lo que hace que el ping-pong salga bien: yendo hacia
; atras cada fotograma lleva puesto el color que le tocaba. Un juego de verdad se ahorraria esa RAM
; interpretando la tira sobre la marcha, y para los bucles vale igual; pero el
; ping-pong pide recorrerla hacia atras, y una tira de pasos de tamano variable
; no se recorre hacia atras sin guardar por donde se ha pasado. Que es esta
; tabla. Ademas es lo mismo que hace el editor, asi que si los dos coinciden es
; que el formato se lee igual desde los dos lados.
;-----------------------------------------------------------------------------

; --- Los pasos ---------------------------------------------------------------
ANIM_END        .equ 0x00
ANIM_FRAME      .equ 0x01
ANIM_MOVED      .equ 0x02
ANIM_LOOP       .equ 0x03
ANIM_LOOP_END   .equ 0x04
ANIM_PAINT      .equ 0x05

; --- De que estan hechos los destinos ----------------------------------------
; El 38 de una animacion de patrones y el 38 de una de grupos son dos cosas
; distintas, y en la tira se ven igual: por eso lo dice la cabecera.
ANIM_OF_PATTERNS .equ 0x00
ANIM_OF_GROUPS   .equ 0x01

; Con el que sale un patron si la tira no trae ningun 0x05. El exportador pone
; uno siempre, asi que esto es para una tira escrita a mano.
ANIM_PATTERN_COLOR .equ 1       ; negro

; Donde va el color de un sprite, que no es el mismo sitio en las dos maquinas:
;
;   1 = modo 2, los 16 bytes de la tabla de color del plano (lo que pone esta ROM)
;   0 = modo 1, el cuarto byte del atributo
;
; Se elige al ensamblar y no se mira en marcha. Se podria: el byte de version de
; la Main ROM esta en 0x002D y desde un cartucho se lee directo. Pero la pregunta
; no seria esa, porque lo que manda es el modo de sprites que el juego haya
; puesto -un MSX2 corre en modo 1 perfectamente- y eso el juego ya lo sabe, que
; lo ha puesto el.
;
; Solo manda en las animaciones de patrones. Los grupos de esta ROM son de modo 2
; y ya: un grupo de MSX1 trae cuatro bytes por miembro en vez de diecinueve, que
; es otro formato y no un if.
ANIM_COLOR_TABLE .equ 1

; --- Que hacer al acabar -----------------------------------------------------
ANIM_ONCE       .equ 0x00
ANIM_REPEAT     .equ 0x01
ANIM_PINGPONG   .equ 0x02

; --- Cuanto cabe -------------------------------------------------------------
; Ocho planos: es lo que esta ROM reserva para la animacion, no el tope de un
; grupo -el editor deja treinta y dos-. De uno mas grande se pintan los ocho
; primeros. Se reservan por delante y la rejilla de grupos empieza detras, asi
; que con animacion se ven ocho grupos menos: es el precio de tener las dos
; cosas en la misma pantalla.
ANIM_PLANES     .equ 8

; Si la animacion trae mas fotogramas se corta y se ve lo que quepa. 64 son de
; sobra para lo que se prueba aqui y son 320 bytes de RAM.
ANIM_MAX_FRAMES .equ 64
ANIM_MAX_DEPTH  .equ 4

; Abajo a la izquierda, y no arriba: la rejilla llena las lineas de arriba, y el
; VDP solo saca ocho sprites por linea de barrido. Compartiendo lineas con ella
; se comeria del cupo que esta ROM esta midiendo justamente.
ANIM_X          .equ 8
ANIM_Y          .equ 150

; --- RAM ---------------------------------------------------------------------
; La pagina 3 es RAM en cualquier MSX y aqui no la usa nadie mas. Los tres
; primeros bytes son de sprites_test.asm.
ANIM_FIRST      .equ 0xC003     ; primer plano que le queda a la rejilla
ANIM_ENDING     .equ 0xC004     ; que hacer al acabar
ANIM_COUNT      .equ 0xC005     ; fotogramas resueltos
ANIM_INDEX      .equ 0xC006     ; cual se esta viendo
ANIM_WAIT       .equ 0xC007     ; interrupciones que le quedan
ANIM_DEPTH      .equ 0xC008     ; bucles abiertos mientras se resuelve
ANIM_TARGET     .equ 0xC009     ; el grupo del fotograma de ahora
ANIM_OFF_Y      .equ 0xC00A     ; y su desplazamiento
ANIM_OFF_X      .equ 0xC00B
; el 0xC00C es de la rejilla
ANIM_TOTAL      .equ 0xC00D     ; cuantas animaciones trae el bloque
ANIM_CURRENT    .equ 0xC00E     ; cual se esta ensenando
ANIM_MADE       .equ 0xC00F     ; de que estan hechos sus destinos

ANIM_STACK      .equ 0xC010     ; 3 bytes por bucle: a donde volver y vueltas
ANIM_INK        .equ 0xC030     ; el color que va fijando el 0x05 al resolver
ANIM_COLOR      .equ 0xC031     ; y el del fotograma que se esta pintando
ANIM_TIMELINE   .equ 0xC040     ; 6 por fotograma: apunta, espera, Y, X, aviso, color

;-----------------------------------------------------------------------------
; Deja lista la primera animacion del bloque.
;
; A la vuelta, A = fotogramas resueltos. Cero si el bloque esta vacio, y
; entonces todo lo demas de aqui no hace nada.
;-----------------------------------------------------------------------------
AnimInit:
                xor a
                ld (ANIM_COUNT),a
                ld (ANIM_INDEX),a
                ld (ANIM_DEPTH),a
                ld (ANIM_TOTAL),a
                ld (ANIM_FIRST),a       ; sin animacion, la rejilla desde el 0

                ld hl,AnimationsEnd
                ld de,AnimationsData
                or a
                sbc hl,de
                ret z                   ; bloque vacio: no hay nada que reproducir

                ld a,ANIM_PLANES
                ld (ANIM_FIRST),a       ; la rejilla empieza detras de los suyos

                call AnimCountAll

                xor a
                ; y cae en AnimSelect, que deja lista la primera

;-----------------------------------------------------------------------------
; A = cual se ensena. La deja resuelta y lista para reproducir.
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

                ; Cada animacion empieza sin color heredado: a esta se puede
                ; haber saltado desde cualquier otra.
                ld a,ANIM_PATTERN_COLOR
                ld (ANIM_INK),a

                ld a,(hl)               ; de que esta hecha
                ld (ANIM_MADE),a
                inc hl

                ld a,(hl)               ; y que hace al acabar
                ld (ANIM_ENDING),a
                inc hl

                ld de,ANIM_TIMELINE     ; donde cae el siguiente fotograma

; HL = por donde va la tira, DE = por donde va la tabla
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
                jr AnimResolved         ; el 0x00, o un byte que no se entiende

; Dos bytes: el color de aqui en adelante. No es un fotograma, asi que no cuenta
; ni ocupa sitio en la tabla; se queda apuntado y lo llevan puesto los que vengan
; detras. En la segunda vuelta de un bucle se pasa otra vez por aqui y se vuelve
; a fijar el mismo, que es justo lo que hace falta.
AnimStepPaint:
                ld a,(hl)
                inc hl
                ld (ANIM_INK),a
                jr AnimStep

; Tres bytes: a que apunta y cuanto espera. Lo demas, a cero.
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
                ld (de),a               ; sin desplazamiento Y
                inc de
                ld (de),a               ; ni X
                inc de
                ld (de),a               ; ni aviso
                inc de

                call AnimInk
                call AnimCounted
                jr AnimStep

; Seis: los mismos mas el desplazamiento y el aviso, que van en el mismo orden.
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

; Sella el color de ahora en el fotograma recien escrito. DE queda detras.
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

; Mas anidado de lo que se lleva la cuenta: se ignora el bucle y sus pasos se
; hacen una vez. Se ve raro, pero se ve, que es mejor que colgarse.
AnimTooDeep:
                inc hl                  ; las vueltas
                jr AnimStep

AnimStepLoopEnd:
                ld a,(ANIM_DEPTH)
                or a
                jr z,AnimStep           ; un cierre suelto: no hay nada que cerrar

                push hl                 ; por donde seguir si el bucle se acaba
                push de                 ; y por donde va la tabla

                dec a
                ld (ANIM_DEPTH),a
                call AnimSlot           ; DE = la cima de la pila

                ex de,hl
                ld e,(hl)
                inc hl
                ld d,(hl)               ; DE = a donde volver
                inc hl                  ; HL = las vueltas que quedan

                dec (hl)
                ld a,(hl)

                pop hl                  ; la tabla
                ex de,hl                ; DE = tabla, HL = a donde volver

                or a
                jr z,AnimLoopOver

                ld a,(ANIM_DEPTH)       ; sigue abierto: se vuelve a apilar
                inc a
                ld (ANIM_DEPTH),a
                pop bc                  ; y se tira el "por donde seguir"
                jp AnimStep             ; jp: desde aqui ya no alcanza un jr

AnimLoopOver:
                pop hl                  ; por donde seguir
                jp AnimStep

;-----------------------------------------------------------------------------
; Se acabo la tira. Si es ping-pong, se pega la vuelta.
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

; La vuelta del ping-pong: del penultimo al segundo. Los extremos no se repiten,
; que si no el fotograma en el que da la vuelta se veria el doble de tiempo.
AnimMirror:
                ld a,(ANIM_COUNT)
                cp 3
                ret c                   ; con dos o menos no hay vuelta que dar

                sub 2
                ld b,a                  ; cuantos se copian
                ld c,a                  ; y por cual se empieza: el penultimo

AnimMirrorNext:
                ld a,c
                push bc

                call AnimAt             ; HL = ese fotograma
                ld bc,6
                ldir                    ; a la cola, y DE queda detras solo

                call AnimCounted
                pop bc

                dec c                   ; el anterior
                djnz AnimMirrorNext
                ret

;-----------------------------------------------------------------------------
; Cuantas animaciones trae el bloque, en ANIM_TOTAL.
;
; Recorriendo los pasos y no contando bytes a cero: el 0x00 que cierra una
; animacion es el mismo valor que puede llevar dentro un fotograma -el grupo 0,
; un desplazamiento de cero, un aviso de cero-, asi que hay que andarlas.
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

; HL = principio de una animacion -> HL = principio de la siguiente.
AnimSkip:
                inc hl                  ; de que esta hecha
                inc hl                  ; y que hace al acabar

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
                ret                     ; el 0x00, y HL ya esta detras

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
; Los cursores cambian de animacion, si el banco trae mas de una.
;-----------------------------------------------------------------------------
ANIM_KEY_ROW    .equ 8          ; la fila del teclado con los cursores
ANIM_KEY_LEFT   .equ 4
ANIM_KEY_RIGHT  .equ 7

ScanAnimationKeys:
                ld a,(ANIM_TOTAL)
                cp 2
                ret c                   ; con una sola no hay nada que elegir

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
                xor a                   ; de la ultima, a la primera
                jr AnimKeyGo

AnimKeyPrevious:
                ld a,(ANIM_CURRENT)
                or a
                jr nz,AnimKeyBack
                ld a,(ANIM_TOTAL)       ; de la primera, a la ultima
AnimKeyBack:
                dec a

AnimKeyGo:
                call AnimSelect
                call AnimShow

; A esperar a que se suelte: sin esto pasaria una animacion por interrupcion y
; no habria forma de pararse en ninguna.
AnimKeyRelease:
                ld a,ANIM_KEY_ROW
                call KeyRow
                and (1 << ANIM_KEY_LEFT) | (1 << ANIM_KEY_RIGHT)
                cp (1 << ANIM_KEY_LEFT) | (1 << ANIM_KEY_RIGHT)
                jr nz,AnimKeyRelease
                ret

;-----------------------------------------------------------------------------
; Una interrupcion menos. Cuando la espera se acaba, pasa al siguiente.
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
                jr c,AnimTickShow       ; queda animacion

                ld a,(ANIM_ENDING)
                or a
                jr nz,AnimTickWrap      ; bucle o ping-pong: vuelta a empezar

                ; De una vez: se queda quieta en el ultimo. Se vuelve a entrar
                ; cada 255 interrupciones y no hace nada, que sale mas barato que
                ; llevar un estado de "parada" solo para esto.
                ld a,255
                ld (ANIM_WAIT),a
                ret

AnimTickWrap:
                xor a

AnimTickShow:
                ld (ANIM_INDEX),a
                call AnimLoadWait
                ; y cae en AnimShow

;-----------------------------------------------------------------------------
; Pinta el fotograma que toca en los planos reservados.
;-----------------------------------------------------------------------------
AnimShow:
                ld a,(ANIM_TOTAL)
                or a
                ret z                   ; sin animaciones los planos son de la rejilla

                ; Una animacion sin pasos si tiene que borrar: se puede llegar a
                ; ella con los cursores, y sin esto se quedaria en pantalla la
                ; figura de la anterior como si siguiera puesta.
                ld a,(ANIM_COUNT)
                or a
                jp z,AnimShowNone       ; jp y no jr: desde aqui hasta alla no llega el salto corto

                ld a,(ANIM_INDEX)
                call AnimAt

                ld a,(hl)
                ld (ANIM_TARGET),a
                inc hl
                inc hl                  ; la espera ya se ha leido
                ld a,(hl)
                ld (ANIM_OFF_Y),a
                inc hl
                ld a,(hl)
                ld (ANIM_OFF_X),a
                inc hl
                inc hl                  ; el aviso, que esta ROM no lo usa
                ld a,(hl)
                ld (ANIM_COLOR),a

                ld a,(ANIM_MADE)
                cp ANIM_OF_GROUPS
                jr nz,AnimShowPattern

                ld a,(ANIM_TARGET)
                call AnimGroupAt        ; IX = sus miembros, B = cuantos
                jr c,AnimShowNone       ; ese grupo no esta en el fichero

                ld a,b
                or a
                jr z,AnimShowNone       ; un grupo vacio no pinta nada

                ; Y de los reservados no se pasa: detras empieza la rejilla de grupos,
                ; que se veria pisada por los planos de mas.
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

; Un patron suelto: un sprite y ya, sin miembros ni desplazamientos de grupo.
AnimShowPattern:
                di

    .if ANIM_COLOR_TABLE
                ; En modo 2 el color de un sprite son 16 bytes, uno por linea. La
                ; animacion trae uno solo, asi que van las 16 del mismo: el color
                ; por linea vive en los grupos, no aqui.
                ld hl,SPRITE_COLOR      ; los 16 bytes de color del plano 0
                call SetVramWrite
                ld b,16
                ld a,(ANIM_COLOR)
AnimPatternColor:
                out (VDP_DATA),a
                djnz AnimPatternColor
    .endif

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

                ; El numero de patron se multiplica por cuatro para la tabla de
                ; atributos, que en 16x16 cada patron ocupa cuatro de los de 8x8.
                ; El fichero lo trae sin multiplicar porque un banco puede tener
                ; mas de 64 y entonces no cabria en un byte.
                ld a,(ANIM_TARGET)
                add a,a
                add a,a
                out (VDP_DATA),a

    .if ANIM_COLOR_TABLE
                xor a                   ; en modo 2 el color ya esta en la tabla
    .else
                ld a,(ANIM_COLOR)       ; y en modo 1 va aqui
    .endif
                out (VDP_DATA),a
                ei

                ld a,1                  ; el plano 0 gastado; el resto, fuera
                jr AnimHideFrom

AnimShowNone:
                xor a

; A = primer plano reservado que este fotograma no gasta. Se aparcan debajo de
; la pantalla: si no, los sprites del fotograma anterior se quedarian puestos.
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
; IX = miembro, IY = plano. Sus 16 bytes de color y sus 4 de atributo.
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

                ; Y = ancla + (lo del miembro + lo del fotograma) a la escala
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

                ld a,(ix+18)            ; patron, ya multiplicado por 4
                out (VDP_DATA),a

                xor a                   ; en modo 2 el cuarto byte no lleva color
                out (VDP_DATA),a
                ei

                ld de,19
                add ix,de
                ret

; Lo que se escala son las distancias dentro de la figura, no el ancla: al
; ampliar, el sprite crece desde donde esta y no se va al doble de lejos.
AnimScale:
                ld c,a
                ld a,(REG1_VALUE)
                rrca                    ; MAG es el bit 0
                ld a,c
                ret nc
                add a,a
                ret

;-----------------------------------------------------------------------------
; A = numero de grupo -> IX = sus miembros, B = cuantos. Carry si no existe.
;
; Por el sitio que ocupa en el fichero y no por ningun numero guardado dentro:
; es como los direcciona el exportador, que los escribe en orden.
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
                jr nc,AnimGroupNone     ; se acabo el fichero

                ld b,(ix+0)
                inc ix

                ld a,c
                or a
                ret z                   ; este es, y sin carry

                dec c

                ld a,b
                or a
                jr z,AnimGroupNext      ; grupo vacio, no ocupa miembros

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
; Ayudas de la tabla de fotogramas
;-----------------------------------------------------------------------------
; A = numero de fotograma -> HL = donde empieza
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

; La espera del fotograma de ahora. Una espera de cero pararia el reloj, asi que
; se cuenta como una: el editor no las deja poner, pero el fichero podria traerla.
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

; Carry si todavia caben fotogramas en la tabla: cp deja el acarreo puesto
; cuando lo que hay es menor que el tope, que es exactamente eso.
AnimRoom:
                ld a,(ANIM_COUNT)
                cp ANIM_MAX_FRAMES
                ret

AnimCounted:
                ld a,(ANIM_COUNT)
                inc a
                ld (ANIM_COUNT),a
                ret

; DE = ANIM_STACK + profundidad*3
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

; Apila el bucle que empieza. HL apunta a las vueltas y sale detras.
AnimPush:
                ld c,(hl)               ; vueltas
                inc hl                  ; y aqui empieza el cuerpo

                push hl
                call AnimSlot           ; DE = su hueco
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
