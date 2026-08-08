using Avalonia.Media;

namespace MSX_SpritesEditor.Entities;

public class ColorPalette
{
    private readonly Color[] _colors;
    private readonly IBrush[] _brushes;

    public ColorPalette()
    {
        // Paleta por defecto: MSX1
        //  0 0,0,0->0,0,0 = Transparent      8 7,1,1->255,36,36
        //  1 0,0,0->0,0,0                    9 7,3,3->255,109,109
        //  2 1,6,1->36,219,36               10 6,6,1->219,219,36
        //  3 3,7,3->109,255,109             11 6,6,4->219,219,146
        //  4 1,1,7->36,36,255               12 1,4,1->36,146,36
        //  5 2,3,7->72,109,255              13 6,2,5->219,72,182
        //  6 5,1,1->182,36,36               14 5,5,5->182,182,182
        //  7 2,6,7->72,219,255              15 7,7,7->255,255,255
        _colors =
        [
            Colors.Transparent,
            Color.FromRgb(0, 0, 0),
            Color.FromRgb(36, 219, 36),
            Color.FromRgb(109, 255, 109),
            Color.FromRgb(36, 36, 255),
            Color.FromRgb(72, 109, 255),
            Color.FromRgb(182, 36, 36),
            Color.FromRgb(72, 219, 255),
            Color.FromRgb(255, 36, 36),
            Color.FromRgb(255, 109, 109),
            Color.FromRgb(219, 219, 36),
            Color.FromRgb(219, 219, 146),
            Color.FromRgb(36, 146, 36),
            Color.FromRgb(219, 72, 182),
            Color.FromRgb(182, 182, 182),
            Color.FromRgb(255, 255, 255),
        ];

        // Los brushes se cachean: repintar el lienzo son 256 celdas y crear
        // un SolidColorBrush por celda en cada redibujado era gratuito en WPF pero innecesario.
        _brushes = new IBrush[_colors.Length];
        for (int i = 0; i < _colors.Length; i++)
            _brushes[i] = new SolidColorBrush(_colors[i]);
    }

    public int Count => _colors.Length;

    public Color GetColor(int index) => _colors[index];

    public IBrush GetBrush(int index) => _brushes[index];
}
