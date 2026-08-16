namespace MSX_GameTools.Entities;

/// <summary>
/// Un trozo de tiles que se lleva de un juego a otro.
/// </summary>
/// <remarks>
/// <para>
/// Dentro de un mismo juego no hace falta: al estampar se copia lo marcado en ese momento,
/// para que retocar un tile de origen después de marcarlo salga en lo que cae. Esto es para
/// cuando el origen ya no está delante, que es justo cuando eso deja de poder hacerse.
/// </para>
/// <para>
/// Se congela al cambiar de pestaña y no al marcar, para no cambiar lo de dentro de un
/// juego: mientras el origen sigue delante manda lo que se está viendo, y en el momento en
/// que deja de verse se guarda tal y como estaba.
/// </para>
/// </remarks>
/// <param name="Patch">Los dibujos, ya copiados: sobreviven a que se cierre el juego de origen.</param>
/// <param name="Preview">
/// Las miniaturas del origen, para el fantasma bajo el ratón. Van aparte porque el trozo
/// copiado no las lleva: la miniatura es del hueco de su juego, no del dibujo.
/// </param>
/// <param name="From">De qué juego salió, para poder decirlo.</param>
public sealed record CopiedTiles(
    TileSetPatch Patch,
    IReadOnlyList<ImageMini> Preview,
    string From);
