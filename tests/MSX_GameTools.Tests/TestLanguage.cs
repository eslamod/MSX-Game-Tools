using System.Globalization;
using System.Runtime.CompilerServices;
using MSX_GameTools.Localization;

namespace MSX_GameTools.Tests;

/// <summary>
/// The suite runs in Spanish, whatever the language of the machine.
/// </summary>
/// <remarks>
/// <para>
/// The program starts in the language of the system when it speaks it, and many tests read
/// what the user reads: a message, the text of a button. On a machine in Spanish they passed,
/// and on the one in English that GitHub builds the releases on they failed, saying in English
/// exactly what they were meant to say. The tests that are about the languages set theirs and
/// put it back; the rest take Spanish for granted, and this makes that true everywhere.
/// </para>
/// <para>
/// The format of numbers and dates too, which is the other thing the machine decides without
/// being asked. And before anything else in the assembly: the data of some theories is built
/// while the tests are being found, before any of them runs.
/// </para>
/// </remarks>
internal static class TestLanguage
{
    /// <summary>What the suite runs in.</summary>
    public const string Culture = "es-ES";

#pragma warning disable CA2255 // A module initializer in a library: this is a test assembly.
    [ModuleInitializer]
    internal static void Pin()
#pragma warning restore CA2255
    {
        var culture = new CultureInfo(Culture);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        Localizer.Instance.Language = culture.TwoLetterISOLanguageName;
    }
}
