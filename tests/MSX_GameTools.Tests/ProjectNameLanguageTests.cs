using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MSX_GameTools.Localization;
using MSX_GameTools.ViewModels;
using MSX_GameTools.Views;
using Xunit;

namespace MSX_GameTools.Tests;

/// <summary>
/// What the program calls a project that has no file yet, in the language of the interface.
/// </summary>
/// <remarks>
/// It was written in Spanish in the code, and here it did not show: this machine is in
/// Spanish. The screenshot of the macOS build, taken on a Mac in English, had the whole window
/// in English but the title, which said «Sin proyecto».
/// </remarks>
public class ProjectNameLanguageTests : IDisposable
{
    private readonly string _before = Localizer.Instance.Language;

    public void Dispose() => Localizer.Instance.Language = _before;

    [AvaloniaTheory]
    [InlineData("es", "MSX Game Tools — Sin proyecto")]
    [InlineData("en", "MSX Game Tools — No project")]
    [InlineData("ca", "MSX Game Tools — Sense projecte")]
    public void El_titulo_sin_proyecto_va_en_el_idioma_elegido(string language, string title)
    {
        Localizer.Instance.Language = language;

        var window = new MainWindow { DataContext = new MainWindowViewModel(new TestDialogService()) };

        window.Show();
        Pump();

        Assert.Equal(title, window.Title);

        window.Close();
        Pump();
    }

    /// <summary>
    /// Changing the language in Preferences changes the title there and then, like the menus.
    /// </summary>
    [AvaloniaFact]
    public async Task Cambiar_de_idioma_cambia_el_titulo_en_el_acto()
    {
        Localizer.Instance.Language = "es";

        var main = new MainWindowViewModel(new TestDialogService());
        var window = new MainWindow { DataContext = main };

        window.Show();
        Pump();

        string before = window.Title!;

        main.ShowPreferencesCommand.Execute(null);

        var form = (EditPreferencesViewModel)main.RightPanViewModel!;

        form.Language = Localizer.Languages.Single(choice => choice.Code == "en");

        await form.AcceptPreferencesCommand.ExecuteAsync(null);
        Pump();

        Assert.NotEqual(before, window.Title);
        Assert.Equal($"MSX Game Tools — {Localizer.Instance["NoProject"]}", window.Title);

        window.Close();
        Pump();
    }

    /// <summary>
    /// Saving a project with no file offers a name for one, and not the «no project» of the
    /// title.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("es", "Proyecto.msxproj")]
    [InlineData("en", "Project.msxproj")]
    [InlineData("ca", "Projecte.msxproj")]
    public async Task Guardar_un_proyecto_nuevo_propone_proyecto(string language, string offered)
    {
        Localizer.Instance.Language = language;

        // With no path to answer, the picker is cancelled and nothing gets written.
        var dialogs = new TestDialogService();
        var main = new MainWindowViewModel(dialogs);

        await main.SaveProjectCommand.ExecuteAsync(null);

        Assert.Equal(offered, dialogs.LastSuggestedFileName);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();
}
