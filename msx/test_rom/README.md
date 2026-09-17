# ROM de prueba de los exportadores

Comprueban en una máquina real (o en un emulador) que lo que exporta el editor es
lo que espera el VDP. No son parte de la herramienta: son el banco de pruebas de
los exportadores.

Quedan dos escritas a mano: `map_test.asm` para los mapas y
`supertile_test.asm` para los mapas hechos con supertiles.

**La de los juegos de tiles ya no está aquí: la genera el editor.** Al exportar un
juego se puede pedir la ROM de ejemplo, y sale de `Templates/TileSetRom.asm`
rellenada para el ensamblador que se elija —sasSX, sjasmplus, pasmo o asMSX—,
para el modo del juego y con los nombres de los ficheros que se acaban de
escribir.

**Y la del banco de sprites tampoco está ya**: sale de
`Templates/SpriteBankRom.asm` y `Templates/AnimationPlayer.asm`, que viajan
juntas porque la ROM se trae el reproductor con un `include`. Sólo para bancos de
MSX2: la ROM pone sprites de modo 2, y uno de MSX1 sería otro programa.

Cuando las dos de mapas tengan su plantilla, aquí no debería quedar ninguna.

## La paleta

Todas cargan también la paleta exportada (`msx_palette.bin`) y todas hacen lo
mismo con ella: **miran en ejecución si la máquina la tiene**. La generada de
tiles igual, sólo que la lleva dentro del propio fichero en vez de al lado, para
que el lote sea lo exportado y nada más. El byte `002DH` de la
BIOS dice la versión —0 es MSX1, 1 es MSX2— y en un MSX1 los 16 colores son fijos,
así que no hay nada que cargar y se salta.

Se comprueba al arrancar en vez de con ensamblado condicional a propósito: así hay
una sola ROM que funciona en las dos máquinas, en lugar de dos que generar y
distribuir por separado. Los 32 bytes de la paleta viajan siempre y no se notan.

Para exportarla: **Palette → Export palette**, en binario o en ensamblador. Son 16
colores de dos bytes, con el rojo en el nibble alto del primero, el azul en el bajo
y el verde en el segundo.

## Ensamblar

El ensamblador `sasSX` es el submódulo `tools/sass-MSX` (el fork MSX de
*SirCmpwn's Assembler*). Tras clonar el repo:

```bash
git submodule update --init tools/sass-MSX
```

o clonar de entrada con `git clone --recurse-submodules`. Es un proyecto
SDK-style: se compila con el mismo `.NET` SDK que el editor, sin Mono ni nada
más:

```bash
dotnet build tools/sass-MSX -c Release
```

Sale `tools/sass-MSX/sass/bin/Release/sasSX` (`sasSX.exe` en Windows). En los
ejemplos de abajo `sasSX.exe` es ese fichero; lo cómodo es ponerlo en el `PATH` o
hacerse un alias. También vale
`dotnet run --project tools/sass-MSX/sass -- map_test.asm ...`.

La suite de pruebas lo busca ahí sola —ese `bin`, `Release` antes que `Debug`, y
luego el `PATH`—, así que compilándolo una vez ya lo encuentra. Si lo tienes en
otro sitio, díselo con la variable de entorno `SASSX`.

```bash
sasSX.exe map_test.asm --output map_test.rom
```

```bash
sasSX.exe supertile_test.asm --output supertile_test.rom
```

Las generadas salen de 16384 bytes exactos, que es lo que espera un cartucho en
la página 1 —salvo con asMSX, que redondea al cartucho más pequeño donde quepa—.
Las dos de mapas salen de 32768 y ocupan las páginas 1 y 2,
porque el mapa viaja dentro de la ROM y con 16K se quedaban cortas enseguida.

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

**No está en esta carpeta: la escribe el editor.** En el panel de exportar un
banco, marca la casilla de la ROM de ejemplo y elige el ensamblador; salen un
`..._rom.asm` y un `..._player.asm` al lado de los ficheros de datos, con la
orden para ensamblarlo en la cabecera del primero.

Sólo se ofrece con el banco en modo **MSX2**: el de MSX1 no saca los 16 bytes de
color por sprite que lee esta ROM, y la casilla ni sale.

El límite es 64 patrones (2048 bytes) y 32 planos de sprite entre todos los
grupos; a partir de ahí la ROM deja de colocar sprites, sin más.

## Binario o ensamblador

La ROM trae activa la salida que se haya exportado y la otra comentada al lado.
Cambiar de una a otra da la misma ROM byte a byte.

```asm
PatternsData:
                .incbin "bicho_patterns.bin"
              ; .include "bicho_patterns.asm"
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
- **Cursores izquierda y derecha** cambian de animación, si el banco trae más de
  una. Dan la vuelta por los dos lados.

Los grupos se colocan por filas, llenando cada fila mientras quepan. El VDP saca
`SPRITES_PER_LINE` sprites por línea de barrido —ocho en modo 2, que es el único
que corre esta ROM—, así que mientras lo que la fila lleva gastado más lo que
pide el grupo no pase de ese cupo, el grupo va **al lado** del anterior y no
debajo. Cuando no cabe, o cuando se acaban las tres columnas que entran de ancho,
se empieza otra fila más abajo.

Lo que baja cada fila **se mide de los grupos que traiga el banco**, no es una
constante: se recorren los offsets Y de todos los miembros y la fila baja lo que
mida el grupo más alto, más cuatro de margen. Con grupos de un solo sprite salen
20 píxeles, que es lo que estaba escrito a mano antes de medirlo; con figuras de
dos sprites de alto, 36. Con el 20 fijo, esas figuras se pisaban doce píxeles
entre filas y no había forma de ver si los offsets del grupo estaban bien.

Así se ve de una pasada lo que de verdad importa al montar una pantalla: cuántos
de estos personajes caben juntos a la misma altura. Con grupos de tres planos
salen dos por fila, y el tercero ya se baja; con grupos de dos, cuatro.

Un grupo sólo se empieza si caben **todos** sus planos en los 32 que hay. Se mira
por grupo y no por miembro: mirándolo por miembro, once grupos de tres planos
—que son 33— dejaban el último dibujado con dos, y un personaje al que le falta
un plano sale roto y parece un fallo del editor.

El cupo por línea es del hardware y no del editor: un personaje de cuatro planos
se come la mitad de los ocho, y dos personajes así juntos los agotan. En modo 1
serían cuatro por línea, pero esta ROM pide GRAPHIC 3 y no arranca en un MSX1.

Al ampliar con F1 se rehace la tabla de atributos entera. Lo que se multiplica
por dos es la **distancia al centro**, no la posición: si se multiplicara la
coordenada entera, el 128 se iría a 256 y no quedaría nada en pantalla. Así el
grupo se queda donde está y crece desde ahí, que es lo que se quiere ver para
comprobar que los offsets del editor son los correctos.

Un sprite que al duplicar se sale de la pantalla se aparca en Y=200, debajo del
área visible. No se recorta ni se deja a medias, y no vale cualquier valor: 216
es el terminador de la lista de planos en modo 2 y dejaría sin dibujar todos los
sprites de detrás. Con `CENTRE_Y`=10 y figuras de un sprite esto empieza a pasar a
partir de la quinta fila cuando está ampliado, porque allí lo que se dobla es
también lo que baja cada fila.

La primera fila va en `CENTRE_Y`=10 y no a media pantalla: con 30 se perdían dos
filas por nada. Así caben nueve filas en las 192 líneas —la última empieza en 170
y el sprite acaba en 186—, más de las que dan los 32 planos por muchos grupos de
un solo plano que traiga el banco.

La cuenta va con 16 bits con signo a propósito: con 8 bits, un desplazamiento
grande duplicado daría la vuelta y colocaría el sprite arriba del todo en vez de
quitarlo de en medio.

## Las animaciones

El bloque `AnimationsData` **puede quedarse vacío**, y así sale cuando el banco no
trae animaciones. Sin datos, la ROM hace exactamente lo de siempre:
la rejilla de grupos desde el plano 0 y ni un plano gastado en otra cosa. No hay
que tocar ninguna directiva ni definir nada al ensamblar; se mira en ejecución
comparando `AnimationsEnd` con `AnimationsData`.

Con datos —**Sprites → Exportar** deja un tercer fichero `..._animations`— se
reservan los **ocho primeros planos** para la animación y la rejilla empieza en
el noveno, así que se ven ocho grupos menos. La figura va abajo a la izquierda y
no arriba a propósito: el VDP saca ocho sprites por línea de barrido, y
compartiendo líneas con la rejilla se comería del cupo que esta ROM está
midiendo.

Cada animación empieza por dos bytes de cabecera: **de qué está hecha** —patrones
o grupos— y qué hace al acabar. El primero hace falta porque el 38 de una
animación de patrones y el 38 de una de grupos son dos cosas distintas y en la
tira se ven igual. Lo destapó esta misma ROM: buscaba el grupo 38 de un banco que
tiene dieciséis y no enseñaba nada.

Una animación de patrones se pinta de un color fijo, `ANIM_PATTERN_COLOR`. Los
colores del banco viajan dentro de los grupos, así que un patrón suelto no trae
ninguno: en un juego de verdad lo pone quien la reproduce.

El reproductor viaja aparte, en su propio `..._player.asm`, porque es la rutina
que se acaba copiando a un juego de verdad. Al arrancar recorre la tira una vez y
deja los fotogramas en una lista plana en RAM, cinco bytes cada uno. Un juego se
ahorraría esa RAM interpretando la tira sobre la marcha —para los bucles vale
igual—, pero el **ping-pong** pide recorrerla hacia atrás, y una tira de pasos de
tamaño variable no se recorre hacia atrás sin haber apuntado por dónde se pasó.

El reloj es el `halt` del bucle principal: las esperas del formato se cuentan en
interrupciones, así que una espera de 6 son seis interrupciones, 50 o 60 por
segundo según la máquina. Eso es justamente lo que no se puede comprobar
comparando bytes.

Lo que hay que mirar cuando corre:

- Que cada fotograma enseñe **la figura que toca**. Las animaciones apuntan a los
  grupos por el sitio que ocupan en la tabla, no por el número que enseña el
  editor, y ésa es la traducción que puede estar mal.
- Que la **cadencia** sea la que se ve en la vista previa del editor a los mismos
  Hz. Si va al doble o a la mitad, la espera se está contando mal.
- Que un **ping-pong** no repita los fotogramas de los extremos al dar la vuelta.
- Que los **desplazamientos** coloquen la figura donde toca y no se vayan
  acumulando: cada uno sustituye al anterior, no se suma.

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

Pone el modo del juego —GRAPHIC 2 o GRAPHIC 1—, carga las dos tablas y enseña el
juego entero. **No está en esta carpeta: la escribe el editor.** En el panel de
exportar un juego de tiles, marca la casilla de la ROM de ejemplo y elige el
ensamblador; sale un `..._rom.asm` al lado de los ficheros de datos, con la
orden para ensamblarlo en su cabecera.

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

No hay nada que copiar ni que renombrar: la ROM sale nombrando los ficheros que
se acaban de exportar. Igual que en la de sprites, el `.incbin` está activo y el
`.include` comentado al lado, así que para probar la otra salida basta con
cambiar el comentario de sitio.

Los tiles de ejemplo de esta carpeta —`tiles_patterns` y `tiles_colors`— se
quedan: los nombran las ROMs de mapas.

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

---

# La ROM de los supertiles

Lo mismo que la del mapa, pero cuando una celda del mapa no es un tile sino un
supertile entero: un rectángulo de tiles que se coloca de una vez.

## Lo que de verdad comprueba

**La tabla de supertiles**, que son tres bytes de cabecera —ancho, alto y
cuántos— y después los números de tile de cada uno, de izquierda a derecha y de
arriba abajo. La ROM la lee como la leería un juego: saca de la cabecera cuánto
mide un supertile, calcula dónde empieza cada uno y resuelve cada celda de
pantalla a través de ella.

Y la comprueba **con supertiles rectangulares a propósito**. Con uno cuadrado,
leer los tiles por columnas en vez de por filas da un dibujo transpuesto que a
simple vista puede pasar por bueno; con 2x3, un orden equivocado descuadra la
pantalla entera y no hay forma de no verlo.

Pero conviene probar **también con uno cuadrado y dos supertiles bien distintos**
—uno todo de un tile y otro todo de otro—, porque eso caza un fallo que el
rectangular disimula: si la cuenta de dónde empieza cada supertile dentro de la
tabla se desvía, cada uno se lee mezclado con el vecino y salen supertiles que no
son ninguno de los definidos. Así se encontró que la multiplicación de la ROM
devolvía 6 donde tenía que devolver 4. Dos manchas grandes de colores planos lo
delatan al instante; un dibujo con detalle, no.

Una cuenta de 0 en la cabecera significa 256, que es el tope que un mapa puede
nombrar porque cada celda es un byte.

## Los datos de ejemplo

`tiles_supertiles.bin` trae **ocho supertiles de 2x3**, y cada uno son seis tiles
consecutivos: el 0 lleva los tiles 0 a 5, el 1 los tiles 6 a 11, y así. Como los
tiles de ejemplo llevan su número escrito en binario, cada supertile se lee de
un vistazo y se ve si el orden es el que dice el exportador.

`super_map.bin` es de **20x12 supertiles** —o sea 40x36 tiles, más que la
pantalla por los dos lados— y cada celda lleva el supertile `(x + y) mod 8`, que
sale en bandas diagonales. Una banda torcida o cortada delata que la cámara o la
tabla no cuadran.

Los dos están generados con los exportadores de verdad, no escritos a mano.

## Con tus propios datos

1. En el editor, **Tiles → Exportar**: salen tres ficheros, y el tercero es
   `..._supertiles`. Y **Mapas → Exportar** el mapa de supertiles.
2. Copia aquí los cuatro como `tiles_patterns`, `tiles_colors`,
   `tiles_supertiles` y `super_map`, con la extensión que toque.
3. Vuelve a ensamblar:

```bash
sasSX.exe supertile_test.asm --output supertile_test.rom
```

Sale de 32768 bytes, como la del mapa y por lo mismo: ocupa las páginas 1 y 2 y
se engancha ella misma a la 2 con `ENASLT`. Si no son 32768 exactos, no la
cargues.

## En marcha

Los **cursores** mueven la cámara **de supertile en supertile**, que es la unidad
en la que está hecho el mapa. Se para sola en los bordes.

## Cómo lo dibuja

La tabla de nombres se arma entera en RAM y se vuelca de una vez. Armarla
directamente en VRAM obligaría a escribir cada supertile en filas salteadas, con
un cambio de dirección por fila; así es una sola escritura seguida de 768 bytes.

Y al arrancar se calcula dónde empieza cada supertile dentro de la tabla, una vez
para los 256. Sin eso, pintar la pantalla serían 768 multiplicaciones, una por
celda.
