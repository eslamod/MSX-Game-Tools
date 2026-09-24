namespace MSX_GameTools.Entities;

/// <summary>
/// Cómo se escribe el ensamblador que se exporta.
/// </summary>
/// <remarks>
/// <para>
/// De todo lo que sale por los exportadores, sólo una cosa cambia de un ensamblador a otro: el
/// nombre de la directiva de datos. Los comentarios con <c>;</c>, las etiquetas terminadas en
/// <c>:</c> y los números en <c>0x</c> los entienden los cuatro que se han probado, así que no
/// hay más que elegir.
/// </para>
/// <para>
/// Y no hay una grafía que valga para todos: sasSX exige el punto de <c>.db</c> y pasmo lo
/// rechaza. Por eso se elige una vez en las preferencias y se guarda, en vez de preguntarlo en
/// cada exportación.
/// </para>
/// </remarks>
public sealed record AsmStyle(string Data)
{
    /// <summary>Con punto: sasSX, asMSX y sjasmplus. No lo acepta pasmo.</summary>
    public const string Dotted = ".db";

    /// <summary>Sin punto: pasmo, asMSX, sjasmplus, y sasSX con su opción <c>-as</c>.</summary>
    public const string Plain = "db";

    /// <summary>Lo que sale si nadie ha elegido nada, que es como se exportaba antes.</summary>
    public static AsmStyle Default { get; } = new(Dotted);

    /// <summary>
    /// El estilo que pida ese texto, o el de siempre si no dice nada.
    /// </summary>
    /// <remarks>
    /// En blanco vuelve al de siempre en vez de escribir líneas sin directiva: un ajuste a
    /// medias no debería producir un fichero que no ensambla en ninguna parte.
    /// </remarks>
    public static AsmStyle Of(string? data) =>
        string.IsNullOrWhiteSpace(data) ? Default : new AsmStyle(data.Trim());

    /// <summary>
    /// Any other directive, spelled the way the data one is: with the leading dot or without.
    /// </summary>
    /// <remarks>
    /// For <c>dw</c>, <c>equ</c>, <c>include</c> and <c>incbin</c> the four assemblers measured
    /// differ in the dot and in nothing else, so the data directive already says how to write
    /// the rest. One typed by hand that is not <c>db</c> still says whether it carries a dot,
    /// which is all that is asked of it here.
    /// </remarks>
    public string Directive(string name) => Data.StartsWith('.') ? "." + name : name;
}
