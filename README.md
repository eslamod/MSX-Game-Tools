# MSX Game Tools

Herramientas de creación de gráficos y mapas para juegos de MSX: sprites, juegos de
tiles, paletas, bloques y mapas, con exportación a los formatos que espera el VDP.

Escrito en C# sobre Avalonia 11.3 y .NET 10. Funciona en Windows, Linux y macOS.

## Qué hace

### Bancos de sprites

- Sprites de 16x16 en dos variantes: MSX, con un color para todo el sprite, y MSX2, con
  un color por línea y el bit CC del V9938 para mezclar planos.
- **Grupos**: varios patrones colocados con desplazamiento para formar una figura mayor
  de lo que da un plano de sprite.
- Imágenes de referencia detrás del lienzo, con opacidad regulable, para calcar.
- Exporta la tabla de patrones y los atributos, en binario y en `.asm`.

### Juegos de tiles

- 256 tiles de 8x8 para GRAPHIC 2 y 3, con sus dos colores por línea.
- **Un solo juego, no tres.** El VDP parte la pantalla en tres tercios con su propia
  tabla cada uno; aquí se define uno y la exportación se replica, para que un tile se vea
  igual esté donde esté.
- Importa y exporta **png** para poder trabajar en GIMP o Photoshop. Al importar
  comprueba lo que la máquina no admite —más de dos colores en una línea de ocho
  píxeles— y dice en qué tile y en qué línea está el problema.
- **Bloques**: grupos de tiles de hasta 16x16 colocados como van a quedar en el mapa, un
  árbol de 3x3 o un supertile de 2x2. Sirven de brocha en el editor de mapas.

### Mapas

- Tamaño libre hasta 1024 por lado, con **capas** que se aplastan al exportar.
- Estampar tiles sueltos, rectángulos de tiles o bloques enteros, con vista previa
  translúcida bajo el ratón.
- Seleccionar un rectángulo y rellenarlo, copiarlo o vaciarlo.
- Redimensionar con ancla, y sustituir rangos de tiles por otros.
- **Deshacer y rehacer**, veinte pasos.
- Entra y sale en json, csv (compatible con Tiled) y binario; y sale también en `.asm`.

### Paletas

- Las quince del MSX1 más el índice transparente, y paletas propias para MSX2 con los
  512 colores del V9938.
- Se exportan en el formato de dos bytes por color que espera el registro 16.

## Cómo se ejecuta

```bash
dotnet run
```

Los tests:

```bash
dotnet test
```

## Decisiones que conviene conocer

Estas son las que más condicionan el código, y están explicadas con detalle en los
comentarios y en los mensajes de commit.

**El color 0 es transparente, en sprites y en tiles.** Deja ver el color del borde
(registro 7), no es un color más de la paleta. El editor lo pinta así para enseñar lo
que se va a ver en la máquina.

**La celda vacía no es el tile 0.** El tile 0 es un tile de verdad, así que bloques y
mapas distinguen «aquí no hay nada» de «aquí va el primero del juego». Al estampar, lo
vacío deja ver lo que hubiera debajo. En csv se escribe `-1`; en binario no cabe, porque
la tabla de nombres siempre dibuja algo, así que cada mapa lleva un tile de relleno.

**Las capas no existen en la máquina.** Son ayuda de edición: al exportar se funden en
una sola tabla de nombres, ganando la celda no vacía más alta.

**Un bloque y una capa son la misma estructura.** Un bloque es una tabla de nombres
pequeña y un mapa la misma tabla más grande, así que estampar un bloque en el mapa no
necesita ninguna traducción.

**Los tiles se enseñan siempre en 32 columnas.** Es la disposición del editor y la del
png, y la única en la que coger un rectángulo significa algo: un árbol dibujado en tres
filas sólo es un rectángulo si las filas miden lo que medían al dibujarlo.

## Cómo se comprueba

Unas seiscientas pruebas automáticas, que se ejecutan con la interfaz montada de verdad
(Avalonia headless con Skia) cuando lo que se prueba es la interfaz.

Dos costumbres que han salvado bastantes fallos:

- **Una prueba que no se ha visto fallar no vale.** Antes de dar por bueno un arreglo se
  vuelve a poner el fallo y se comprueba que la prueba cae. Y tiene que caer *por el
  camino que usa el usuario*: probar el ViewModel suelto ha dejado pasar más de un fallo
  que estaba en el punto de entrada.
- **Los exportadores se validan ensamblando de verdad.** El `.asm` que sale se pasa por
  el ensamblador cruzado sasSX y se compara byte a byte con el binario. Si los dos
  coinciden, el fichero sirve.

## ROMs de prueba

En `msx/test_rom` hay dos ROMs en ensamblador Z80 que cargan lo exportado y lo enseñan en
un MSX de verdad o en openMSX:

- `sprites_test.asm` — GRAPHIC 3 y sprites de modo 2, con los grupos colocados. Las
  teclas 0-F cambian el color del borde y F1 conmuta la magnificación.
- `tileset_test.asm` — GRAPHIC 2 con el juego de tiles replicado en los tres tercios.

Las dos detectan en ejecución si están en un MSX1 o en un MSX2 para cargar la paleta sólo
donde se puede. Su README explica los mapas de VRAM y cómo cambiar entre datos incrustados
y ficheros propios.

## Estado

En desarrollo. Funcionan los bancos de sprites, los juegos de tiles con sus bloques, las
paletas y el editor de mapas. Están por hacer las animaciones, los comportamientos, los
sonidos y la música, que ya tienen su sitio en el árbol del proyecto.

Falta también un aviso de cambios sin guardar: al cerrar una pestaña o eliminar un
elemento se pierde lo que no se haya guardado en un fichero.
