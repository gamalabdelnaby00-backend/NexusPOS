using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UniversalPOS.Desktop;

/// <summary>Application-wide UI preferences shared by every WPF window and persisted per Windows user.</summary>
public sealed class AppearanceManager : INotifyPropertyChanged
{
    private static readonly string PreferencesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS");
    private static readonly string PreferencesPath = Path.Combine(PreferencesDirectory, "appearance.json");

    public static AppearanceManager Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? AppearanceChanged;

    private string _language = "ar";
    private string _theme = "Light";

    public string Language { get => _language; private set { if (_language == value) return; _language = value; OnPropertyChanged(); } }
    public string Theme { get => _theme; private set { if (_theme == value) return; _theme = value; OnPropertyChanged(); } }
    public bool IsDark => Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

    private AppearanceManager() { }

    public void Load()
    {
        try
        {
            if (File.Exists(PreferencesPath))
            {
                var state = JsonSerializer.Deserialize<PreferenceState>(File.ReadAllText(PreferencesPath));
                _language = state?.Language is "en" or "ar" ? state.Language : "ar";
                _theme = state?.Theme is "Dark" or "Light" ? state.Theme : "Light";
            }
        }
        catch
        {
            // Corrupt or inaccessible preference files must never prevent the POS from starting.
            _language = "ar";
            _theme = "Light";
        }
        LocalizationManager.Current.SetLanguage(Language);
        ApplyToOpenWindows();
    }

    public void SetLanguage(string language)
    {
        Language = language.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";
        LocalizationManager.Current.SetLanguage(Language);
        Save();
        ApplyToOpenWindows();
    }

    public void SetTheme(string theme)
    {
        Theme = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
        Save();
        ApplyToOpenWindows();
    }

    public void ApplyToOpenWindows()
    {
        var app = System.Windows.Application.Current;
        if (app is null) return;
        app.Dispatcher.BeginInvoke(() =>
        {
            foreach (Window window in app.Windows)
            {
                window.FlowDirection = LocalizationManager.Current.FlowDirection;
                if (window is Views.LoginWindow) continue;   // the sign-in screen keeps its own fixed design
                if (window is MainWindow main)
                {
                    main.ApplyGlobalPreferences(Language, Theme);
                    continue;
                }
                ApplyWindowTheme(window);
            }
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void ApplyWindowTheme(Window window)
    {
        bool dark = IsDark;
        var page = Brush(dark ? "#111827" : "#F3F6FB");
        var panel = Brush(dark ? "#1F2937" : "#FFFFFF");
        var text = Brush(dark ? "#F9FAFB" : "#17283F");
        var secondary = Brush(dark ? "#C0CAD8" : "#52647A");
        var border = Brush(dark ? "#4B5563" : "#DCE5F0");
        // Shared typography is applied centrally so windows do not drift visually.
        window.FontFamily = new FontFamily("Segoe UI");
        window.Background = page;
        window.Foreground = text;
        if (window.Content is DependencyObject root) ApplyControlTree(root, page, panel, text, secondary, border, dark);
    }

    private static void ApplyControlTree(DependencyObject node, Brush page, Brush panel, Brush text,
        Brush secondary, Brush border, bool dark)
    {
        if (node is Control control)
        {
            control.FontFamily = new FontFamily("Segoe UI");
            switch (control)
            {
                case TextBox:
                case PasswordBox:
                case ComboBox:
                case DatePicker:
                    control.Background = panel; control.Foreground = text; control.BorderBrush = border;
                    break;
                case DataGrid grid:
                    grid.Background = panel; grid.Foreground = text; grid.BorderBrush = border;
                    grid.RowBackground = panel; grid.AlternatingRowBackground = page;
                    grid.HorizontalGridLinesBrush = border; grid.VerticalGridLinesBrush = border;
                    break;
                case Button button:
                    if (button.IsDefault || (button.Content?.ToString()?.Contains("حفظ") == true) ||
                        (button.Content?.ToString()?.Contains("Save") == true))
                    { button.Background = Brush("#1769E8"); button.Foreground = Brushes.White; }
                    else { button.Background = panel; button.Foreground = text; button.BorderBrush = border; }
                    break;
                default:
                    if (control.Background is SolidColorBrush) control.Background = panel;
                    if (control.Foreground is SolidColorBrush) control.Foreground = text;
                    if (control.BorderBrush is SolidColorBrush) control.BorderBrush = border;
                    break;
            }
        }
        else if (node is Panel panelNode)
        {
            if (panelNode.Background is SolidColorBrush) panelNode.Background = page;
        }
        else if (node is Border borderNode)
        {
            borderNode.Background = panel; borderNode.BorderBrush = border;
        }
        else if (node is TextBlock block)
        {
            block.FontFamily = new FontFamily("Segoe UI");
            block.Foreground = block.FontWeight == FontWeights.Bold || block.FontSize >= 18 ? text : secondary;
        }

        if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
        {
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
                ApplyControlTree(VisualTreeHelper.GetChild(node, i), page, panel, text, secondary, border, dark);
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(PreferencesDirectory);
            File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(new PreferenceState(Language, Theme), new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Keep the current session usable if Windows profile storage is read-only.
        }
    }

    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private sealed record PreferenceState(string Language, string Theme);
}
