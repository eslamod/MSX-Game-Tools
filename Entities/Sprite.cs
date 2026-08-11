namespace MSX_GameTools.Entities;

public class Sprite
{
    public const int Rows = 16;

    public Sprite()
    {
        ArraySpriteRows = new SpriteRow[Rows];
        for (int i = 0; i < Rows; i++)
            ArraySpriteRows[i] = new SpriteRow { Color = 15 };
    }

    public SpriteRow[] ArraySpriteRows { get; }

    public ImageMini? ImageMini { get; set; }

    /// <summary>Imagen de referencia que se ve detras del lienzo al editar este patron.</summary>
    public BackgroundRef Background { get; set; } = BackgroundRef.None;
}
