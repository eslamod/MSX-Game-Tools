using MSX_GameTools.Services;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// El nombre con el que salen los ficheros de un documento y las etiquetas de dentro.
/// </summary>
/// <remarks>
/// Es el mismo para las dos cosas a propósito: un mapa «Nivel 1» sale como `nivel_1.asm` con
/// `nivel_1_map:` dentro.
/// </remarks>
public class AsmLabelTests
{
    [Theory]
    [InlineData("Sprite test 1", "sprite_test_1")]
    [InlineData("Nivel 1", "nivel_1")]
    [InlineData("Bicho: nivel 3/4", "bicho__nivel_3_4")]
    public void La_etiqueta_sale_de_un_nombre_valido(string name, string expected)
        => Assert.Equal(expected, AsmLabel.Of(name));

    /// <summary>
    /// Una etiqueta no puede empezar por dígito, así que se le pone una letra delante.
    /// </summary>
    /// <remarks>
    /// La letra es una cualquiera —viene de cuando esto sólo nombraba bancos de sprites—,
    /// pero cambiarla ahora le movería la etiqueta a quien tenga un documento que empiece por
    /// número.
    /// </remarks>
    [Theory]
    [InlineData("3 enemigos", "s3_enemigos")]
    [InlineData("1", "s1")]
    public void Una_etiqueta_no_empieza_por_digito(string name, string expected)
        => Assert.Equal(expected, AsmLabel.Of(name));

    /// <summary>
    /// Sin nada que salvar del nombre, «unnamed».
    /// </summary>
    /// <remarks>
    /// Era «sprites», de cuando por aquí sólo pasaba un banco: un mapa sin nombre proponía
    /// «sprites.bin». Ahora es la misma palabra que usa <c>CleanFileName</c> para el mismo
    /// caso, así que un documento sin nombre se llama igual se escriba donde se escriba.
    /// </remarks>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///")]
    public void Sin_nombre_que_salvar_sale_unnamed(string name)
        => Assert.Equal("unnamed", AsmLabel.Of(name));
}
