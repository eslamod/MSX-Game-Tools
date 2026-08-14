# ROM de prueba de los exportadores

Comprueban en una máquina real (o en un emulador) que lo que exporta el editor es
lo que espera el VDP. No son parte de la herramienta: son el banco de pruebas de
los exportadores.

Hay tres: `sprites_test.asm` para los grupos de sprites, `tileset_test.asm` para
los juegos de tiles y `map_test.asm` para los mapas.

## La paleta

Las dos cargan también la paleta exportada (`msx_palette.bin`), y las dos hacen lo
mismo con ella: **miran en ejecución si la máquina la tiene**. El byte `002DH` de la
BIOS dice la versión —0 es MSX1, 1 es MSX2— y en un MSX1 los 16 colores son fijos,
así que no hay nada que cargar y se salta.

Se comprueba al arrancar en vez de con ensamblado condicional a propósito: así hay
una sola ROM que funciona en las dos máquinas, en lugar de dos que generar y
distribuir por separado. Los 32 bytes de la paleta viajan siempre y no se notan.

Para exportarla: **Palette → Export palette**, en binario o en ensamblador. Son 16
colores de dos bytes, con el rojo en el nibble alto del primero, el azul en el bajo
y el verde en el segundo.

## Ensamblar

```bash
sasSX.exe sprites_test.asm --output sprites_test.rom
```

```bash
sasSX.exe tileset_test.asm --output tileset_test.rom
```

```bash
sasSX.exe map_test.asm --output map_test.rom
```

Las dos primeras salen de 16384 bytes exactos, que es lo que espera un cartucho
en la página 1. La del mapa sale de 32768 y ocupa las páginas 1 y 2, porque el
mapa viaja dentro de la ROM y con 16K se quedaba corta enseguida.

Un cartucho de 32K no es sólo cuestión de tamaño: **la BIOS busca la `AB` en la
página 1 y conmuta esa, pero deja la 2 como estaba, que es RAM**. Sin hacer nada
más, la ROM no vería su propia mitad de arriba. Por eso lo primero que hace
`map_test.asm` al arrancar es averiguar en qué slot está —mirando cuál hay
puesto en la página 1, que es donde se está ejecutando— y ponerse ahí también
con `ENASLT`. Es la rutina del Technical Handbook, y el rodeo por `EXPTBL` y
`SLTTBL` es porque el slot puede estar expandido en subslots y entonces el
número primario no basta para nombrarlo.

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

---

# La ROM del mapa

Carga un juego de tiles y pinta un mapa encima, con los cursores para moverse si
el mapa es más grande que la pantalla.

## Lo que de verdad comprueba

**La cabecera.** El exportador de mapas escribe cuatro bytes delante de las
celdas: dos de ancho y dos de alto, byte bajo primero. Hasta ahora eso no lo
había leído ninguna máquina, sólo el propio editor al reimportar, que es un
lector poniéndose de acuerdo consigo mismo. Esta ROM lo lee como lo leería un
juego —`ld hl,(mapa)` y a correr— y lo usa para todo: cuánto se ve, hasta dónde
llega la cámara y cuántos bytes hay que saltar para bajar una fila.

Si el ancho estuviera mal, cada fila empezaría desplazada respecto a la anterior
y el mapa saldría **inclinado**. Por eso el mapa de ejemplo lleva un marco de
tile 255 alrededor: un borde que se tuerce se ve al instante, y un borde recto
sólo puede salir si el ancho es el que dice la cabecera.

## El mapa de ejemplo

`map.bin` y `map.asm` son 96x160: tres pantallas de ancho y casi siete de alto.
Están generados con el exportador de verdad, no escritos a mano.

Ese tamaño **no es por enseñar más mapa**, es para que la prueba pruebe algo.
Ensamblada, la tabla del mapa va de `527BH` a `8E7FH`, o sea que **cruza
`8000H` por la fila 121**: las 39 últimas filas están en la página 2 y no se
pueden leer si el `ENASLT` del arranque no ha funcionado. Con un mapa que
cupiera por debajo de `8000H` —el de ejemplo anterior, de 64x48, o uno de
96x96— la conmutación de página no se ejercita nunca y podría estar rota sin
que se notara.

O sea que **la prueba del cambio de página es bajar del todo**. Si fallara, de
la fila 121 en adelante se vería basura en vez del patrón regular, y el marco
de abajo no aparecería: ahí habría RAM sin inicializar en lugar de la ROM.

Las direcciones de arriba se mueven si cambia el tamaño del código, así que la
fila 121 es de referencia, no un número al que agarrarse.

Dentro del marco, cada celda lleva el tile `(x mod 16) + 16 * (y mod 16)`: un
bloque de 16x16 celdas que recorre los 256 tiles y se repite. Como los tiles de
ejemplo llevan su número y cada uno tiene su color, se sabe en todo momento en
qué parte del bloque estás.

## Con tus propios datos

1. En el editor, **Tiles → Exportar**, y **Mapas → Exportar**, los dos en el
   mismo formato.
2. Copia los tres ficheros aquí como `tiles_patterns`, `tiles_colors` y `map`,
   con la extensión que toque.
3. Vuelve a ensamblar.

El mapa tiene que caber en lo que sobra del cartucho: 32K menos el código, menos
los 4096 de las dos tablas y los 32 de la paleta dejan sitio para unas 28000
celdas, o sea 224x125, 168x168 o cualquier otra combinación que no pase de ahí.

Si te pasas, sasSX lo dice pero no se planta:

```
.fill or .ds too big:18446744073709543301 at line:595, param:0xC000 - RomEnd, 0xFF
```

Ese número enorme es el relleno hasta 32K puesto en negativo. **Y escribe el
`.rom` igualmente**, con el tamaño que salga en vez de 32768, así que la
comprobación de verdad es el tamaño del fichero: si no son 32768 bytes exactos,
no lo cargues.

Igual que las otras dos, el `.incbin` está activo y el `.include` comentado al
lado; las dos rutas dan la misma ROM byte a byte.

## En marcha

Los **cursores** mueven la cámara, una celda cada cuatro fotogramas. Se para
sola en los bordes del mapa.

Si el mapa cabe entero en la pantalla no se mueve nada, y lo que sobra se
rellena con el tile 0. Ese relleno es de la ROM y no del mapa: el exportador no
deja celdas vacías, escribe el tile de relleno que tenga puesto el mapa.

## Mapa de VRAM

El mismo que la ROM del tileset, que es el de siempre en SCREEN 2. La única
diferencia entre las dos es lo que se escribe en la tabla de nombres: allí
`0..255` repetido, y aquí la ventana del mapa que toque.
