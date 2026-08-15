using MSX_GameTools.ViewModels;

namespace MSX_GameTools.Tests;

/// <summary>
/// Crear una paleta desde los tests por donde la crea el usuario.
/// </summary>
/// <remarks>
/// Crear ya no es un paso: el botón abre un formulario que pregunta el nombre y de qué
/// paleta se copia, y hasta que no se acepta no hay paleta. Los tests que sólo necesitan
/// una para trabajar pasan por aquí en vez de llamar a la biblioteca por detrás, que
/// dejaría de enterarse si el camino del usuario se rompe.
/// </remarks>
internal static class TestPalette
{
    /// <summary>Pulsa el botón de crear y acepta el formulario. Devuelve el editor abierto.</summary>
    public static EditPaletteViewModel Create(MainWindowViewModel main, string? name = null)
    {
        main.AddPaletteCommand.Execute(null);

        var form = (NewPaletteViewModel)main.RightPanViewModel!;

        if (name is not null)
            form.Name = name;

        form.AcceptPaletteCommand.Execute(null);

        return (EditPaletteViewModel)main.RightPanViewModel!;
    }
}
