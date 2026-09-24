# MSX Game Tools

Suite de herramientas de gráficos para MSX: sprites, tiles, paletas, bloques, mapas y
animaciones, con exportación a los formatos que espera el VDP. C# sobre Avalonia 12.1 y
.NET 10. No es un generador de juegos.

## Idioma del código

- **Los identificadores, en inglés.** Ya lo estaban.
- **Los comentarios nuevos, en inglés.** Los que ya hay en español se quedan como están:
  traducir las ~94.000 palabras que hay escritas está aparcado, no descartado. Un fichero
  puede quedar mezclado durante un tiempo y no pasa nada.
- **Lo que se le enseña al usuario, por el `Localizer`**, en los tres `.resx`. La excepción
  es `FileFormatException`, que va siempre en español a propósito y lo explica ella misma.
- **Los nombres por defecto de los datos** (`Group 1`, `Unnamed map`) van en inglés fijo y
  no traducidos: acaban grabados dentro del fichero al guardar.
- **Los comentarios que emiten los exportadores** al `.asm`, en inglés: ese fichero lo lee
  gente de fuera.
- **Los mensajes de commit siguen en español.** El historial son 239 commits y no se
  reescribe, así que cambiar de idioma ahora sólo lo dejaría a medias.

## Cómo se trabaja aquí

- **Las pruebas se falsifican.** Una prueba nueva no vale hasta haberla visto fallar por su
  motivo: se rompe a propósito lo que prueba y se comprueba que cae por eso y no por otra
  cosa. Han pasado por aquí varias que pasaban sin comprobar nada.
- **La suite entera antes de cada commit**, con `--blame-hang-timeout 120s`.
- **Un commit por asunto**, con el porqué en el cuerpo y no sólo el qué.
- **El usuario hace sus propios `git push`.** Aquí sólo se commitea en local, y el
  historial no se reescribe nunca.
- **Reescribir ficheros con cuidado**: PowerShell (`Get-Content | Set-Content`) destroza el
  UTF-8 y deja los acentos en mojibake, y `sed -i` sobre ficheros CRLF infla el diff al
  fichero entero. Con Python y `newline=''` tanto al leer como al escribir.
- **Al compilar puede fallar** con `MSB3027` si la aplicación está abierta: tiene bloqueado
  el DLL. Hay que pedirle al usuario que la cierre.
- **Las ROMs de ejemplo se generan**, no se escriben: salen de `Templates/*.asm` al
  marcar la casilla en el panel de exportar, rellenadas para el ensamblador que se elija.
  Lo que hace cada una y qué mirar cuando corre está en `msx/test_rom/README.md`, que es
  lo único que queda en esa carpeta.
- **Los cuatro ensambladores** con los que se prueban son sasSX, sjasmplus, pasmo y
  asMSX. sasSX es el submódulo `tools/sass-MSX` (proyecto SDK-style: `dotnet build
  tools/sass-MSX`, sin Mono): clonar con `--recurse-submodules` o `git submodule update
  --init`. Los otros tres se buscan donde estaban al medirlos, o donde diga su variable
  de entorno. El programa no los necesita; la suite los usa si están y se salta la prueba
  si no, así que en verde sin ninguno quiere decir que ahí no se comprobó.

## Pruebas

xUnit v3 con Avalonia headless sobre Skia (`[AvaloniaFact]`, `[AvaloniaTheory]`). Los
nombres de los métodos van en snake_case y describen la regla, no el método que llaman.

Lo que toque una colección enlazada a una selección se prueba **montando la ventana de
verdad** y pulsando con el ratón: un `ListBox` escribe `null` de vuelta cuando el elemento
que tenía seleccionado se mueve o desaparece, y con el modelo de vista suelto eso no sale.

```bash
dotnet test tests/MSX_GameTools.Tests/MSX_GameTools.Tests.csproj --nologo -v q --blame-hang-timeout 120s
```
