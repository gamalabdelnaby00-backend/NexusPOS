using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace UniversalPOS.Desktop.UI;

public sealed class TableColumn<T>
{
    public TableColumn(string header, double width, Func<T, int, UIElement> cell, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        Header = header; Width = width; Cell = cell; Align = align;
    }
    public string Header { get; }
    public double Width { get; }
    public Func<T, int, UIElement> Cell { get; }
    public HorizontalAlignment Align { get; }
}

/// <summary>Bordered table with a grey header row, column separators and a pager footer, like the mockups.</summary>
public sealed class PagedTable<T> : Border
{
    private readonly IReadOnlyList<TableColumn<T>> _columns;
    private readonly string _noun;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _info = new() { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Foreground = UiKit.Ink };
    private readonly StackPanel _pager = new() { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox _sizeBox;
    private readonly TextBlock _empty = new() { FontSize = 14, Foreground = UiKit.Muted, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 36, 0, 36) };
    private IReadOnlyList<T> _items = Array.Empty<T>();
    private int _page = 1;
    private int _pageSize = 10;

    public PagedTable(IReadOnlyList<TableColumn<T>> columns, string noun, string emptyText)
    {
        _columns = columns; _noun = noun; _empty.Text = emptyText;
        Background = Brushes.White; BorderBrush = UiKit.CardBorder; BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(10);
        ClipToBounds = true;

        _sizeBox = UiKit.Combo(new[] { "10", "20", "50" }, 74);
        _sizeBox.Height = 34;
        _sizeBox.SelectionChanged += (_, _) =>
        {
            if (_sizeBox.SelectedItem is string s && int.TryParse(s, out int n)) { _pageSize = n; _page = 1; Render(); }
        };

        var root = new DockPanel();
        var footer = new Border { BorderBrush = UiKit.Line, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(18, 12, 18, 12), Background = Brushes.White };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.Children.Add(_info);
        var pagerHost = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.LeftToRight };
        pagerHost.Children.Add(_sizeBox);
        _pager.Margin = new Thickness(14, 0, 0, 0);
        pagerHost.Children.Add(_pager);
        Grid.SetColumn(pagerHost, 1);
        footerGrid.Children.Add(pagerHost);
        footer.Child = footerGrid;
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var header = BuildRow(true);
        var headerGrid = (Grid)header.Child;
        for (int c = 0; c < _columns.Count; c++)
        {
            var title = new TextBlock { Text = _columns[c].Header, FontWeight = FontWeights.SemiBold, FontSize = 13, Foreground = UiKit.Ink, VerticalAlignment = VerticalAlignment.Center };
            var headerCell = Cell(title, c, HorizontalAlignment.Center);
            Grid.SetColumn(headerCell, c);
            headerGrid.Children.Add(headerCell);
        }
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var body = new StackPanel();
        body.Children.Add(_rows);
        body.Children.Add(_empty);
        root.Children.Add(body);
        Child = root;
        Render();
    }

    public void SetItems(IReadOnlyList<T> items)
    {
        _items = items;
        _page = 1;
        Render();
    }

    private Border BuildRow(bool header)
    {
        var grid = new Grid();
        foreach (var col in _columns)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(col.Width, GridUnitType.Star) });
        return new Border
        {
            Background = header ? UiKit.TableHeader : Brushes.White,
            BorderBrush = UiKit.Line, BorderThickness = new Thickness(0, header ? 0 : 0, 0, 1),
            MinHeight = header ? 46 : 60, Child = grid
        };
    }

    private Border Cell(UIElement content, int columnIndex, HorizontalAlignment align)
    {
        bool last = columnIndex == _columns.Count - 1;
        // Separator sits on the physical left edge in RTL (the next column is to the left) and on the right edge in LTR.
        var thickness = last ? new Thickness(0)
            : UiKit.IsRtl ? new Thickness(1, 0, 0, 0) : new Thickness(0, 0, 1, 0);
        if (content is FrameworkElement fe) fe.HorizontalAlignment = align;
        return new Border { BorderBrush = UiKit.Line, BorderThickness = thickness, Padding = new Thickness(12, 6, 12, 6), Child = content };
    }

    private void Render()
    {
        _rows.Children.Clear();
        int total = _items.Count;
        int pages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
        _page = Math.Min(Math.Max(1, _page), pages);
        int skip = (_page - 1) * _pageSize;
        var pageItems = _items.Skip(skip).Take(_pageSize).ToList();

        for (int i = 0; i < pageItems.Count; i++)
        {
            var row = BuildRow(false);
            var grid = (Grid)row.Child;
            for (int c = 0; c < _columns.Count; c++)
            {
                var cell = Cell(_columns[c].Cell(pageItems[i], skip + i + 1), c, _columns[c].Align);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
            _rows.Children.Add(row);
        }

        _empty.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        int from = total == 0 ? 0 : skip + 1;
        int to = Math.Min(total, skip + _pageSize);
        _info.Text = UiKit.IsArabic
            ? $"عرض {from} - {to} من {total} {_noun}"
            : $"Showing {from} - {to} of {total} {_noun}";
        BuildPager(pages);
    }

    private void BuildPager(int pages)
    {
        _pager.Children.Clear();
        _pager.Children.Add(PagerButton("FirstPage", null, _page > 1, () => GoTo(1)));
        _pager.Children.Add(PagerButton("ChevronLeft", null, _page > 1, () => GoTo(_page - 1)));
        int start = Math.Max(1, Math.Min(_page - 2, pages - 4));
        int end = Math.Min(pages, start + 4);
        for (int p = start; p <= end; p++)
        {
            int target = p;
            _pager.Children.Add(PagerButton(null, p.ToString(), true, () => GoTo(target), p == _page));
        }
        _pager.Children.Add(PagerButton("ChevronRight", null, _page < pages, () => GoTo(_page + 1)));
        _pager.Children.Add(PagerButton("LastPage", null, _page < pages, () => GoTo(pages)));
    }

    private void GoTo(int page) { _page = page; Render(); }

    private static Button PagerButton(string? icon, string? text, bool enabled, Action click, bool current = false)
    {
        var fg = current ? Brushes.White : UiKit.Ink;
        var b = new Button
        {
            Style = (Style)System.Windows.Application.Current.FindResource("ShellButton"),
            Width = 34, Height = 34, Padding = new Thickness(0), Margin = new Thickness(3, 0, 3, 0),
            Background = current ? (Brush)UiKit.Blue : Brushes.White, BorderBrush = current ? UiKit.Blue : UiKit.B("#D2E2F5"),
            Foreground = fg, IsEnabled = enabled,
            Content = icon is not null ? UiKit.Icon(icon, 18, fg) : new TextBlock { Text = text, Foreground = fg, FontSize = 13, FontWeight = FontWeights.SemiBold }
        };
        b.Click += (_, _) => click();
        return b;
    }
}
