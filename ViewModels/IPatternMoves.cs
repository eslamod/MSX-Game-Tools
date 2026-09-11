using CommunityToolkit.Mvvm.Input;

namespace MSX_GameTools.ViewModels;

/// <summary>
/// Moving the whole drawing around: what the pad offers over a tile or a sprite pattern.
/// </summary>
/// <remarks>
/// The two editors do the same eight things to different drawings, and the pad that offers them
/// is a single control used by both. This is what lets that control name the commands once and
/// be sure that both editors answer to those names.
/// </remarks>
public interface IPatternMoves
{
    IRelayCommand FlipHorizontalCommand { get; }

    IRelayCommand FlipVerticalCommand { get; }

    IRelayCommand RotateLeftCommand { get; }

    IRelayCommand RotateRightCommand { get; }

    IRelayCommand ShiftLeftCommand { get; }

    IRelayCommand ShiftRightCommand { get; }

    IRelayCommand ShiftUpCommand { get; }

    IRelayCommand ShiftDownCommand { get; }
}
