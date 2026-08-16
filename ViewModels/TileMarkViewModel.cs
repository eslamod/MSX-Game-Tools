using CommunityToolkit.Mvvm.ComponentModel;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Si un hueco de la rejilla de tiles va teñido ahora mismo.
/// </summary>
/// <remarks>
/// Uno por tile, en una lista paralela a las miniaturas. Va aparte y no dentro de
/// <c>ImageMini</c> porque las miniaturas las comparten el editor de tiles y el de mapas
/// —son el mismo objeto—, y teñirlas ahí pintaría también los tiles del mapa, que no es lo
/// que se está mirando.
/// </remarks>
public sealed partial class TileMarkViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isMarked;
}
