# ROM de prueba de los exportadores

Comprueban en una máquina real (o en un emulador) que lo que exporta el editor es
lo que espera el VDP. No son parte de la herramienta: son el banco de pruebas de
los exportadores.

Hay dos: `sprites_test.asm` para los grupos de sprites y `tileset_test.asm` para
los juegos de tiles.

## Ensamblar

```bash
sasSX.exe sprites_test.asm --output sprites_test.rom
```

```bash
sasSX.exe tileset_test.asm --output tileset_test.rom
```

Salen 16384 bytes exactos, que es lo que espera un cartucho en la página 1.

---

# La ROM de los sprites

## Con tus propios datos

Los ficheros de ejemplo que hay aquí (cuatro patrones y tres grupos, en las dos
salidas) son para que la ROM ensamble nada más clonar. Para probar un banco tuyo:

1. En el editor, **Exportar banco**, en binario o en ensamblador, con el banco en
   modo **MSX2** (el MSX1 no saca los 16 bytes de color por sprite que lee esta
   ROM).
2. Copia los dos ficheros aquí como `bank_patterns` y `bank_groups`, con la
   extensión que toque.
3. Vuelve a ensamblar.

El límite es 64 patrones (2048 bytes) y 32 planos de sprite entre todos los
grupos; a partir de ahí la ROM deja de colocar sprites, sin más.

## Binario o ensamblador

La ROM trae el `.incbin` activo y el `.include` comentado al lado. Cambiar de uno
a otro da la misma ROM byte a byte; está comprobado con el banco de ejemplo.

```asm
PatternsData:
                .incbin "bank_patterns.bin"
              ; .include "bank_patterns.asm"
PatternsEnd:
```

Las etiquetas `PatternsData` / `PatternsEnd` son de la ROM, no del exportador. Se
ponen aquí para que el fuente no dependa del nombre del banco: el exportador saca
las suyas de ahí, así que un banco llamado `test bank 1` genera
`test_bank_1_patterns` y `test_bank_1_patterns_end`, y cambiar el nombre del banco
obligaría a tocar la ROM.

En código tuyo que sí conozca el banco no hace falta envolver nada, porque el
exportador cierra cada bloque con su etiqueta de fin:

```asm
                .include "test_bank_1_patterns.asm"
                ...
                ld hl,test_bank_1_patterns
                ld bc,test_bank_1_patterns_end - test_bank_1_patterns
```

Esa etiqueta de fin hace falta porque ninguno de los dos ficheros dice cuántos
elementos trae. En el de grupos es imprescindible: cada grupo empieza por un byte
con cuántos sprites lo forman, pero no hay ningún sitio donde ponga cuántos grupos
hay, así que la única forma de recorrerlo es ir hasta el final.

## En marcha

- **0-9** y **A-F** cambian el color de fondo (R#7). La pantalla está entera a
  color 0, o sea transparente, así que se ve el fondo por todas partes.
- **F1** alterna el bit MAG de R#1: sprites a tamaño doble.

Cada grupo se centra en x=`CENTRE_X` y baja `GROUP_Y_STEP` píxeles respecto al
anterior.

Al ampliar con F1 se rehace la tabla de atributos entera. Lo que se multiplica
por dos es la **distancia al centro**, no la posición: si se multiplicara la
coordenada entera, el 128 se iría a 256 y no quedaría nada en pantalla. Así el
grupo se queda donde está y crece desde ahí, que es lo que se quiere ver para
comprobar que los offsets del editor son los correctos.

Un sprite que al duplicar se sale de la pantalla se aparca en Y=200, debajo del
área visible. No se recorta ni se deja a medias, y no vale cualquier valor: 216
es el terminador de la lista de planos en modo 2 y dejaría sin dibujar todos los
sprites de detrás. Con `CENTRE_Y`=30 y `GROUP_Y_STEP`=20 esto empieza a pasar a
partir del sexto grupo cuando está ampliado.

La cuenta va con 16 bits con signo a propósito: con 8 bits, un desplazamiento
grande duplicado daría la vuelta y colocaría el sprite arriba del todo en vez de
quitarlo de en medio.

## Mapa de VRAM

GRAPHIC 3, página 0. El manual del V9938 propone el generador de sprites en
`1C00H`, pero ahí sólo caben 32 patrones de 16x16, así que se sube a `1800H`.

| Tabla                    | Dirección | Tamaño |
| ------------------------ | --------- | ------ |
| Patrones de pantalla     | `0000H`   | 6144   |
| Patrones de sprite       | `1800H`   | 2048   |
| Colores de pantalla      | `2000H`   | 6144   |
| Nombres                  | `3800H`   |  768   |
| Colores de sprite        | `3C00H`   |  512   |
| Atributos de sprite      | `3E00H`   |  128   |

La tabla de colores de sprite tiene que estar 512 bytes por debajo de la de
atributos: no se direcciona aparte.

---

# La ROM del tileset

Pone GRAPHIC 2 (SCREEN 2), carga las dos tablas y enseña el juego entero.

## Lo que de verdad comprueba

En GRAPHIC 2 y 3 la pantalla se parte en **tres tercios**, y cada uno tiene su
propia tabla de patrones y su propia tabla de colores. El editor define un solo
juego de 256 tiles y hay que **copiarlo tres veces** en VRAM; eso no está en los
bytes exportados, así que es lo primero que se hace mal.

La ROM lo aprovecha para delatarlo: llena la tabla de nombres con `0..255`
repetido, y como un tercio son exactamente 32x8 = 256 celdas, **cada tercio
enseña el juego entero con la misma disposición que la rejilla del editor**. Si la
replicación fallara, el segundo y el tercer tercio saldrían con basura y se vería
de un vistazo.

Los tiles de ejemplo llevan un marco de un píxel y, en el centro, **su número en
binario en dos filas de cuatro bits**: arriba los bits 7 a 4 y abajo los 3 a 0, que
es como se lee un byte en hexadecimal. El tile 0 tiene el centro vacío, el 255 lo
tiene macizo y el 170 (`10101010`) sale con las dos filas iguales.

El color de fondo es `1 + (n mod 15)`, así que dos tiles seguidos nunca comparten
color: si la tabla de colores no hubiera viajado, se veria de inmediato.

## Con tus propios datos

1. En el editor, **Export tileset**, en binario o en ensamblador.
2. Copia los dos ficheros aquí como `tiles_patterns` y `tiles_colors`, con la
   extensión que toque.
3. Vuelve a ensamblar.

Igual que en la de sprites, el `.incbin` está activo y el `.include` comentado al
lado; las dos rutas dan la misma ROM byte a byte.

## En marcha

**0-9** y **A-F** cambian el color del borde (R#7).

## Mapa de VRAM

El de siempre en SCREEN 2.

| Tabla                | Dirección | Tamaño |
| -------------------- | --------- | ------ |
| Patrones             | `0000H`   | 6144   |
| Nombres              | `1800H`   |  768   |
| Atributos de sprite  | `1B00H`   |  128   |
| Colores              | `2000H`   | 6144   |
| Patrones de sprite   | `3800H`   | 2048   |

Los 6144 de patrones y de colores son los 2048 de una tabla por los tres tercios.
