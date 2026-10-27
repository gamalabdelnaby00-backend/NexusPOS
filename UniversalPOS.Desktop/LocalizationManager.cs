using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows;

namespace UniversalPOS.Desktop;

/// <summary>Runtime localization for WPF UI resources. Use resource keys in bindings, never hard-coded UI text.</summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resources = new("UniversalPOS.Desktop.Resources.Strings", typeof(LocalizationManager).Assembly);

    public static LocalizationManager Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _resources.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
    public System.Windows.FlowDirection FlowDirection => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
    public string LanguageCode => CultureInfo.CurrentUICulture.Name;

    public void SetLanguage(string languageCode)
    {
        var culture = languageCode switch
        {
            "ar" or "ar-EG" => CultureInfo.GetCultureInfo("ar-EG"),
            "en" or "en-US" => CultureInfo.GetCultureInfo("en-US"),
            _ => throw new ArgumentException("Supported UI languages are ar-EG and en-US.", nameof(languageCode))
        };

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        OnPropertyChanged(string.Empty); // Refresh all resource-key indexer bindings.
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(LanguageCode));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
