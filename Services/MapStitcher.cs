namespace MSX_GameTools.Services;

/// <summary>
/// Cose el mapa de un juego a partir de las pantallas que va enseñando.
/// </summary>
/// <remarks>
/// <para>
/// La idea: mientras un juego corre en un emulador, su tabla de nombres es la pantalla que se
/// ve. Al hacer scroll, la pantalla siguiente es casi la misma corrida unas celdas. Encajando
/// una con otra y quedándose con lo que asoma por el borde, sale el mapa entero de lo que se
/// haya recorrido.
/// </para>
/// <para>
/// <b>Por correlación y no por dirección.</b> Se podría mirar si el juego scrollea a un lado o
/// a otro y meter la fila o la columna nueva, pero entonces hay que acertar la dirección, el
/// número de celdas y los casos en diagonal. Aquí se prueba un puñado de desplazamientos y se
/// coge el que más celdas hace coincidir: da igual hacia dónde vaya, cuánto corra de golpe y si
/// va en diagonal, y no hay que saber nada del juego.
/// </para>
/// <para>
/// <b>Empatando, gana quedarse quieto.</b> Una pantalla de puro cielo encaja igual de bien con
/// cualquier desplazamiento. Los candidatos se prueban de menos a más movimiento y se coge el
/// primero que pasa el listón, que es lo que hace un juego de verdad: corre una celda por vez,
/// no quince.
/// </para>
/// <para>
/// <b>Lo que no encaja es otra pantalla.</b> Cuando ningún desplazamiento llega al listón, no
/// es que se haya movido mucho: es que se ha cambiado de sitio —otro nivel, otra sala, la
/// pantalla de morir—. Entonces esto lo dice y quien lo use empieza un mapa nuevo, que
/// pegarlas sería mezclar dos zonas que no se tocan.
/// </para>
/// </remarks>
public sealed class MapStitcher
{
    /// <summary>Celdas que se prueba a mover en cada dirección.</summary>
    /// <remarks>
    /// Cuatro llegan de sobra: un juego que corriera más de cuatro celdas en un fotograma
    /// dejaría un mapa a tirones que no habría por dónde coser. Probar más sólo añade
    /// candidatos con poco solape, que son los que se equivocan.
    /// </remarks>
    public const int Reach = 4;

    /// <summary>Celdas que tienen que coincidir para dar por bueno un encaje.</summary>
    public const double LeastMatch = 0.90;

    /// <summary>Y cuánto se tienen que solapar las dos pantallas.</summary>
    /// <remarks>
    /// Sin esto, un desplazamiento enorme deja tres celdas de solape, las tres coinciden por
    /// casualidad y sale un encaje perfecto que manda la pantalla a la quinta puñeta.
    /// </remarks>
    public const double LeastOverlap = 0.50;

    private readonly Dictionary<(int Column, int Row), int> _cells = [];

    private int[,]? _last;
    private int _atColumn;
    private int _atRow;

    /// <summary>Cuánto se ha movido la cámara entre dos pantallas, en celdas.</summary>
    public sealed record Shift(int Columns, int Rows, double Match);

    /// <summary>Pantallas que se han pegado, contando la primera.</summary>
    public int Screens { get; private set; }

    public int Left { get; private set; }

    public int Top { get; private set; }

    public int Width => _cells.Count == 0 ? 0 : _cells.Keys.Max(at => at.Column) - Left + 1;

    public int Height => _cells.Count == 0 ? 0 : _cells.Keys.Max(at => at.Row) - Top + 1;

    /// <summary>
    /// Cuánto se ha movido de la primera pantalla a la segunda, o <c>null</c> si no encajan.
    /// </summary>
    /// <remarks>
    /// El desplazamiento es el de la cámara: si el contenido se va hacia la izquierda de la
    /// pantalla, la cámara ha ido hacia la derecha y sale positivo.
    /// </remarks>
    public static Shift? Between(int[,] before, int[,] after, int reach = Reach)
    {
        int columns = before.GetLength(0);
        int rows = before.GetLength(1);

        if (columns != after.GetLength(0) || rows != after.GetLength(1))
            return null;

        // De menos movimiento a mas, para que empatando gane quedarse quieto.
        foreach ((int dc, int dr) in Candidates(reach))
        {
            int overlap = (columns - Math.Abs(dc)) * (rows - Math.Abs(dr));

            if (overlap < LeastOverlap * columns * rows)
                continue;

            int same = 0;

            for (int column = Math.Max(0, -dc); column < Math.Min(columns, columns - dc); column++)
            {
                for (int row = Math.Max(0, -dr); row < Math.Min(rows, rows - dr); row++)
                {
                    if (after[column, row] == before[column + dc, row + dr])
                        same++;
                }
            }

            double match = (double)same / overlap;

            if (match >= LeastMatch)
                return new Shift(dc, dr, match);
        }

        return null;
    }

    /// <summary>
    /// Pega una pantalla. Devuelve <c>false</c> si no encaja con la anterior.
    /// </summary>
    /// <remarks>
    /// Devolver y no lanzar: no encajar es lo normal cada vez que el juego cambia de sala, y
    /// quien lo use decide si empieza otro mapa o se lo salta.
    /// </remarks>
    public bool Feed(int[,] screen)
    {
        if (_last is not null)
        {
            if (Between(_last, screen) is not { } shift)
                return false;

            _atColumn += shift.Columns;
            _atRow += shift.Rows;
        }

        Stamp(screen);

        _last = screen;
        Screens++;

        return true;
    }

    /// <summary>
    /// El mapa cosido, con <c>null</c> en lo que no se ha llegado a ver.
    /// </summary>
    /// <remarks>
    /// Nulo y no cero: el tile 0 es un tile de verdad, y un mapa de una zona en L tiene
    /// esquinas por las que no se ha pasado. Es lo mismo que entiende el editor por celda
    /// vacía, así que el csv sale con su -1 y entra derecho.
    /// </remarks>
    public int?[,] ToGrid()
    {
        var grid = new int?[Math.Max(0, Width), Math.Max(0, Height)];

        foreach (((int column, int row), int tile) in _cells)
            grid[column - Left, row - Top] = tile;

        return grid;
    }

    /// <summary>Los desplazamientos a probar, de menos movimiento a más.</summary>
    private static IEnumerable<(int Columns, int Rows)> Candidates(int reach)
    {
        for (int distance = 0; distance <= reach * 2; distance++)
        {
            for (int dc = -reach; dc <= reach; dc++)
            {
                for (int dr = -reach; dr <= reach; dr++)
                {
                    if (Math.Abs(dc) + Math.Abs(dr) == distance)
                        yield return (dc, dr);
                }
            }
        }
    }

    private void Stamp(int[,] screen)
    {
        for (int column = 0; column < screen.GetLength(0); column++)
        {
            for (int row = 0; row < screen.GetLength(1); row++)
                _cells[(_atColumn + column, _atRow + row)] = screen[column, row];
        }

        Left = Math.Min(Left, _atColumn);
        Top = Math.Min(Top, _atRow);
    }
}
