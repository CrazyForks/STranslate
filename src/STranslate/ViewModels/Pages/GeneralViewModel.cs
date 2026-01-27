using CommunityToolkit.Mvvm.Input;
using STranslate.Controls;
using STranslate.Core;
using STranslate.Helpers;
using STranslate.Plugin;
using System.Windows.Input;

namespace STranslate.ViewModels.Pages;

public partial class GeneralViewModel : SearchViewModelBase
{
    public GeneralViewModel(
        Settings settings,
        DataProvider dataProvider,
        Internationalization i18n) : base(i18n, "General_")
    {
        Settings = settings;
        DataProvider = dataProvider;
        Languages = i18n.LoadAvailableLanguages();
    }

    [RelayCommand]
    private void ResetFontFamily() => Settings.FontFamily = Win32Helper.GetSystemDefaultFont();

    [RelayCommand]
    private void ResetAutoTransDelay() => Settings.AutoTranslateDelayMs = 500;

    [RelayCommand]
    private void ResetFontSize() => Settings.FontSize = 14;

    [RelayCommand]
    private async Task IncreamentableTranalateKeyAsync()
    {
        var dialog = new HotkeyControlDialog(HotkeyType.Global, Settings.IncreamentalTranslateKey.ToString(), "LeftAlt");
        await dialog.ShowAsync();
        if (dialog.ReturnType == HotkeyControlDialog.HkReturnType.Save)
        {
            Settings.IncreamentalTranslateKey = Enum.Parse<Key>(dialog.ResultValue);
        }
    }

    public List<int> ScreenNumbers
    {
        get
        {
            var screens = MonitorInfo.GetDisplayMonitors();
            var screenNumbers = new List<int>();
            for (int i = 1; i <= screens.Count; i++)
            {
                screenNumbers.Add(i);
            }

            return screenNumbers;
        }
    }
    public Settings Settings { get; }
    public DataProvider DataProvider { get; }

    public List<I18nPair> Languages { get; }
}