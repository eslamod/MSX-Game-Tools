using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.Entities;

/// <summary>
/// Una línea de un tile: ocho pixeles y sus dos colores.
/// </summary>
/// <remarks>
/// En GRAPHIC 2 y 3 cada línea de un patrón ocupa un byte en la tabla de patrones, con
/// la máscara de bits, y otro en la de colores, con el color de frente en el nibble alto
/// y el de fondo en el bajo. Un bit a 1 se pinta con el de frente y uno a 0 con el de
/// fondo, así que en una línea de un tile sólo caben dos colores.
/// </remarks>
public partial class TileRow : ObservableObject
{
    public const int Columns = 8;

    /// <summary>Color con el que se pintan los bits a 1.</summary>
    [ObservableProperty]
    private int _foreColor = 15;

    /// <summary>Color con el que se pintan los bits a 0.</summary>
    [ObservableProperty]
    private int _backColor;

    public bool[] ArrayPattern { get; } = new bool[Columns];

    /// <summary>
    /// La máscara de bits tal como va a la tabla de patrones, con la columna 0 en el bit
    /// más significativo, que es como la lee el VDP.
    /// </summary>
    public byte PatternByte
    {
        get
        {
            int bits = 0;

            for (int column = 0; column < Columns; column++)
            {
                if (ArrayPattern[column])
                    bits |= 1 << (Columns - 1 - column);
            }

            return (byte)bits;
        }
    }

    /// <summary>El byte de la tabla de colores: el de frente arriba y el de fondo abajo.</summary>
    public byte ColorByte => (byte)(((ForeColor & 0x0F) << 4) | (BackColor & 0x0F));

    /// <summary>
    /// Cambia la línea a la otra forma de escribir exactamente lo mismo: intercambia los
    /// dos colores e invierte los bits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Con dos colores por línea, cada dibujo se puede escribir de dos maneras: frente
    /// negro sobre fondo verde con unos bits, o frente verde sobre fondo negro con esos
    /// mismos bits al revés. En pantalla no se distinguen.
    /// </para>
    /// <para>
    /// Importar un png elige una de las dos por su cuenta en cada línea, así que un mismo
    /// tile acaba con unas líneas de una forma y otras de la otra. Eso no se ve, pero se
    /// nota al retocarlo: el mismo color está en un desplegable o en el otro según la
    /// línea, y pintar con un color deja de ser predecible.
    /// </para>
    /// </remarks>
    public void SwapColors()
    {
        (ForeColor, BackColor) = (BackColor, ForeColor);

        for (int column = 0; column < Columns; column++)
            ArrayPattern[column] = !ArrayPattern[column];
    }
}
