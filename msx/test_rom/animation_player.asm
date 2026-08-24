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
;
; Las esperas van en interrupciones y nunca son cero. Los desplazamientos son en
; complemento a dos y absolutos: cada uno sustituye al anterior, no se suman.
;
; --- Por que se resuelve en una tabla ------------------------------------------
; Se recorre la tira una vez al arrancar y se deja una lista plana de fotogramas
; en RAM, cinco bytes cada uno. Un juego de verdad se ahorraria esa RAM
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

; --- Que hacer al acabar -----------------------------------------------------
ANIM_ONCE       .equ 0x00
ANIM_REPEAT     .equ 0x01
ANIM_PINGPONG   .equ 0x02

; --- Cuanto cabe -------------------------------------------------------------
; Ocho planos, que es lo que puede traer un grupo como mucho. Se reservan por
; delante y la rejilla de grupos empieza detras, asi que con animacion se ven
; ocho grupos menos: es el precio de tener las dos cosas en la misma pantalla.
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

ANIM_STACK      .equ 0xC010     ; 3 bytes por bucle: a donde volver y vueltas
ANIM_TIMELINE   .equ 0xC040     ; 5 por fotograma: apunta, espera, Y, X, aviso

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
                ld (ANIM_FIRST),a       ; sin animacion, la rejilla desde el 0

                ld hl,AnimationsEnd
                ld de,AnimationsData
                or a
                sbc hl,de
                ret z                   ; bloque vacio: no hay nada que reproducir

                ld a,ANIM_PLANES
                ld (ANIM_FIRST),a       ; la rejilla empieza detras de los suyos

                ld hl,AnimationsData
                ld a,(hl)
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
                jr AnimResolved         ; el 0x00, o un byte que no se entiende

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

                call AnimCounted
                jr AnimStep

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
                jr AnimStep

AnimLoopOver:
                pop hl                  ; por donde seguir
                jr AnimStep

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
                ld bc,5
                ldir                    ; a la cola, y DE queda detras solo

                call AnimCounted
                pop bc

                dec c                   ; el anterior
                djnz AnimMirrorNext
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
                ld a,(ANIM_COUNT)
                or a
                ret z

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

                ld a,(ANIM_TARGET)
                call AnimGroupAt        ; IX = sus miembros, B = cuantos
                jr c,AnimShowNone       ; ese grupo no esta en el fichero

                ld a,b
                or a
                jr z,AnimShowNone       ; un grupo vacio no pinta nada

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
                add hl,hl
                add hl,hl               ; x4
                ld c,a
                ld b,0
                add hl,bc               ; x5
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
