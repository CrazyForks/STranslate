using CommunityToolkit.Mvvm.ComponentModel;

namespace STranslate.Plugin.Translate.BingDictBuiltIn.ViewModel;

public partial class SettingsViewModel(IPluginContext context, Settings settings) : ObservableObject
{
    [ObservableProperty] public partial bool EnableSentenceExample { get; set; } = settings.EnableSentenceExample;

    partial void OnEnableSentenceExampleChanged(bool value)
    {
        settings.EnableSentenceExample = value;
        context.SaveSettingStorage<Settings>();
    }
}