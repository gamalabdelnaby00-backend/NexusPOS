using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;

namespace UniversalPOS.Desktop.UI;

/// <summary>Small factory helpers so every screen shares the same look as the design mockups.</summary>
public static class UiKit
{
    public static SolidColorBrush B(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    public static readonly SolidColorBrush Navy = B("#0B2A4F");
    public static readonly SolidColorBrush Ink = B("#12294A");
    public static readonly SolidColorBrush Blue = B("#0A74F0");
    public static readonly SolidColorBrush DeepBlue = B("#0B4FB3");
    public static readonly SolidColorBrush Muted = B("#5F7390");
    public static readonly SolidColorBrush Line = B("#E1EAF5");
    public static readonly SolidColorBrush PageBg = B("#EAF1FA");
    public static readonly SolidColorBrush CardBorder = B("#D2E2F5");
    public static readonly SolidColorBrush TableHeader = B("#EEF3F9");
    public static readonly SolidColorBrush Soft = B("#F2F7FD");

    public static bool IsArabic => LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    public static string T(string arabic, string english) => IsArabic ? arabic : english;
    public static bool IsRtl => LocalizationManager.Current.FlowDirection == FlowDirection.RightToLeft;

    public static Geometry Geo(string icon) => (Geometry)System.Windows.Application.Current.FindResource("Icon." + icon);

    public static Shapes.Path Icon(string icon, double size, Brush fill) => new()
    {
        Data = Geo(icon), Fill = fill, Width = size, Height = size,
        Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center
    };

    private static Style ShellStyle(string key) => (Style)System.Windows.Application.Current.FindResource(key);

    /// <summary>Icon followed by text. In a right-to-left layout the icon ends up on the right, as in the mockups.</summary>
    public static StackPanel IconText(string icon, string text, Brush fg, double iconSize = 18)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Icon(icon, iconSize, fg));
        panel.Children.Add(new TextBlock { Text = text, Foreground = fg, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    public static Button ActionButton(string icon, string text, Brush bg, Brush fg, Brush? border = null)
    {
        var b = new Button
        {
            Style = ShellStyle("ShellButton"), Background = bg, Foreground = fg,
            BorderBrush = border ?? bg, Padding = new Thickness(20, 0, 20, 0), Height = 42,
            Content = IconText(icon, text, fg)
        };
        return b;
    }

    public static Button PrimaryButton(string icon, string text) => ActionButton(icon, text, Blue, Brushes.White);

    public static Button IconButton(string icon, Brush bg, Brush fg, string tooltip)
    {
        var b = new Button
        {
            Style = ShellStyle("ShellButton"), Background = bg, Foreground = fg, BorderBrush = bg,
            Width = 36, Height = 34, Padding = new Thickness(0), Margin = new Thickness(3, 0, 3, 0),
            ToolTip = tooltip, Content = Icon(icon, 18, fg)
        };
        return b;
    }

    public static Border Card(UIElement? child = null, double padding = 18) => new()
    {
        Background = Brushes.White, BorderBrush = CardBorder, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = child
    };

    public static ComboBox Combo(IEnumerable<string> items, double minWidth = 170)
    {
        var combo = new ComboBox { Style = ShellStyle("ShellComboBox"), MinWidth = minWidth, Margin = new Thickness(0, 0, 0, 0) };
        foreach (var item in items) combo.Items.Add(item);
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        return combo;
    }

    /// <summary>Rounded search box with a magnifier icon and a placeholder hint.</summary>
    public static (Border Root, TextBox Box) SearchBox(string hint, double minWidth = 300)
    {
        var box = new TextBox
        {
            BorderThickness = new Thickness(0), Background = Brushes.Transparent, FontSize = 13, Foreground = Ink,
            VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(4, 0, 4, 0)
        };
        var hintText = new TextBlock
        {
            Text = hint, Foreground = B("#7C8DA6"), FontSize = 13, IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0)
        };
        box.TextChanged += (_, _) => hintText.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var icon = Icon("Search", 20, Muted);
        icon.Margin = new Thickness(12, 0, 6, 0);
        grid.Children.Add(icon);
        var inner = new Grid();
        inner.Children.Add(box);
        inner.Children.Add(hintText);
        Grid.SetColumn(inner, 1);
        grid.Children.Add(inner);

        var root = new Border
        {
            Height = 42, MinWidth = minWidth, Background = Brushes.White, BorderBrush = B("#C9D9EC"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = grid
        };
        return (root, box);
    }

    /// <summary>Status pill. kind: green, orange, red or gray.</summary>
    public static Border Chip(string text, string kind)
    {
        (string bg, string fg, string border) = kind switch
        {
            "green" => ("#DDF3E3", "#2B7A43", "#BFE5C9"),
            "orange" => ("#FDEBCB", "#B66A0A", "#F6D79C"),
            "red" => ("#FDE0E0", "#C23B3B", "#F5B9B9"),
            _ => ("#E9EEF5", "#5A6B82", "#D3DCE8")
        };
        return new Border
        {
            Background = B(bg), BorderBrush = B(border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13),
            Padding = new Thickness(16, 4, 16, 4), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = text, Foreground = B(fg), FontSize = 12, FontWeight = FontWeights.SemiBold }
        };
    }

    public static StackPanel PageTitle(string icon, string title)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        panel.Children.Add(Icon(icon, 30, DeepBlue));
        panel.Children.Add(new TextBlock
        {
            Text = title, FontSize = 26, FontWeight = FontWeights.Bold, Foreground = DeepBlue,
            Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    public static TextBlock Text(string text, double size = 13, Brush? fg = null, FontWeight? weight = null) => new()
    {
        Text = text, FontSize = size, Foreground = fg ?? Ink, FontWeight = weight ?? FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis
    };

    /// <summary>Round avatar: the picture when the file exists, otherwise a neutral person placeholder.</summary>
    public static FrameworkElement Avatar(string? filePath, double size)
    {
        var holder = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size / 2), Background = B("#DCE7F6"),
            ClipToBounds = true, VerticalAlignment = VerticalAlignment.Center
        };
        var bitmap = LoadBitmap(filePath, (int)(size * 2));
        if (bitmap is not null)
        {
            holder.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
            holder.Clip = new EllipseGeometry(new Point(size / 2, size / 2), size / 2, size / 2);
        }
        else
        {
            holder.Child = new Viewbox { Margin = new Thickness(size * 0.18), Child = Icon("Account", size, B("#7C93B3")) };
        }
        return holder;
    }

    public static BitmapImage? LoadBitmap(string? path, int decodeWidth)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    /// <summary>Resolves a product ImageKey inside %LocalAppData%/NexusPOS/ProductImages; null if it escapes the folder or is missing.</summary>
    public static string? ProductImagePath(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "ProductImages");
            var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
        }
        catch { return null; }
    }
}
