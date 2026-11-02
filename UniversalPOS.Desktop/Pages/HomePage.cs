using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UniversalPOS.Desktop.UI;

namespace UniversalPOS.Desktop.Pages;

public sealed record HomeTile(string Icon, string Title, string Description, Action Open);

/// <summary>Landing page: a greeting and one tile for every section the signed-in user may open.</summary>
public sealed class HomePage : UserControl
{
    public HomePage(string displayName, IEnumerable<HomeTile> tiles)
    {
        var stack = new StackPanel();
        stack.Children.Add(UiKit.PageTitle("Home", UiKit.T("الرئيسية", "Home")));
        stack.Children.Add(new TextBlock
        {
            Text = UiKit.T($"أهلاً بك، {displayName}", $"Welcome, {displayName}"), FontSize = 20, FontWeight = FontWeights.SemiBold,
            Foreground = UiKit.Ink, Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(new TextBlock
        {
            Text = UiKit.T("اختر القسم الذي تريد العمل عليه.", "Choose the section you want to work on."),
            FontSize = 13, Foreground = UiKit.Muted, Margin = new Thickness(0, 0, 0, 18)
        });

        var wrap = new WrapPanel();
        foreach (var tile in tiles)
        {
            var open = tile.Open;
            var icon = new Border
            {
                Width = 46, Height = 46, CornerRadius = new CornerRadius(12), Background = UiKit.B("#DCEAFB"), VerticalAlignment = VerticalAlignment.Top,
                Child = new Viewbox { Margin = new Thickness(11), Child = UiKit.Icon(tile.Icon, 24, UiKit.B("#0B5FCC")) }
            };
            var texts = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(new TextBlock { Text = tile.Title, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = UiKit.Ink, TextWrapping = TextWrapping.Wrap });
            texts.Children.Add(new TextBlock { Text = tile.Description, FontSize = 12, Foreground = UiKit.Muted, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(icon);
            row.Children.Add(texts);
            var card = UiKit.Card(row, 16);
            card.Width = 290; card.MinHeight = 92;
            card.Margin = new Thickness(0, 0, 16, 16);
            card.Cursor = Cursors.Hand;
            card.MouseLeftButtonUp += (_, _) => open();
            card.MouseEnter += (_, _) => card.BorderBrush = UiKit.Blue;
            card.MouseLeave += (_, _) => card.BorderBrush = UiKit.CardBorder;
            wrap.Children.Add(card);
        }
        stack.Children.Add(wrap);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack, Padding = new Thickness(0, 0, 4, 0) };
    }
}
