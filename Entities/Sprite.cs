namespace MSX_SpritesEditor.Entities;

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
}
