using Microsoft.EntityFrameworkCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UniversalPOS.Infrastructure.Data;
using UniversalPOS.Domain.Entities;
using UniversalPOS.Domain.Enums;
using UniversalPOS.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.Text;
using NexusPOS;

namespace UniversalPOS.Desktop.Views;

/// <summary>Consistent operational page shell. Data is read from the existing AppDbContext.</summary>
public sealed class SectionWindow : Window
{
    private static readonly Color Navy = Color.FromRgb(20, 43, 72);
    private static readonly Color Blue = Color.FromRgb(22, 105, 232);
    private static readonly Color Page = Color.FromRgb(244, 247, 252);
    private static readonly Color Muted = Color.FromRgb(104, 119, 140);
    private readonly AppDbContext _db;
    private readonly string _section;
    private readonly Action<string, string>? _navigate;
    private readonly DataGrid _grid = new()
    {
        AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false,
        CanUserReorderColumns = false, CanUserResizeColumns = true, CanUserSortColumns = true,
        EnableRowVirtualization = true, EnableColumnVirtualization = true,
        HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        BorderThickness = new Thickness(0), RowHeaderWidth = 0, MinRowHeight = 45,
        ColumnHeaderHeight = 44, Margin = new Thickness(0), Background = Brushes.White,
        HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(232, 237, 245)),
        VerticalGridLinesBrush = Brushes.Transparent,
        SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow
    };
    private readonly TextBox _searchBox = new() { MinWidth = 230, Height = 40, Padding = new Thickness(12, 4, 12, 4), VerticalContentAlignment = VerticalAlignment.Center, FontSize = 13 };
    private readonly TextBlock _status = new() { Margin = new Thickness(2, 10, 2, 0), FontSize = 12, Foreground = new SolidColorBrush(Muted) };
    private readonly TextBlock _pageTitle = new() { FontSize = 25, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Navy) };
    private readonly TextBlock _pageDescription = new() { FontSize = 13, Margin = new Thickness(0, 5, 0, 0), Foreground = new SolidColorBrush(Muted) };
    private readonly Button _refreshButton;
    private readonly Button _exportReportButton;
    private readonly DatePicker _reportFromDate = new() { Width = 145, Height = 40, Margin = new Thickness(4, 0, 8, 0) };
    private readonly DatePicker _reportToDate = new() { Width = 145, Height = 40, Margin = new Thickness(4, 0, 8, 0) };
    private readonly Button _addButton;
    private readonly Button _editProductButton;
    private readonly Button _toggleProductButton;
    private readonly Button _manageCategoriesButton;
    private readonly Button _cashMovementButton;
    private readonly Button _approveDifferenceButton;
    private readonly ComboBox _categoryFilter = new() { MinWidth = 170, Height = 40, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(8, 4, 8, 4), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBlock _allCount = new() { FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Navy) };
    private readonly TextBlock _activeCount = new() { FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)) };
    private readonly TextBlock _lowCount = new() { FontSize = 23, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6)) };

    public SectionWindow(AppDbContext db, string section, string title, Action<string, string>? navigate = null)
    {
        _db = db; _section = section; _navigate = navigate;
        // Attached dependency properties must be set with SetValue, not object-initializer members.
        _grid.SetValue(VirtualizingPanel.IsVirtualizingProperty, true);
        _grid.SetValue(VirtualizingPanel.VirtualizationModeProperty, VirtualizationMode.Recycling);
        _grid.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        _grid.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        Title = title; Width = 1250; Height = 780; MinWidth = 1000; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Page);
        FontFamily = new FontFamily("Segoe UI");

        _refreshButton = PrimaryButton(IsArabic ? "تحديث البيانات" : "Refresh data");
        _exportReportButton = PrimaryButton(IsArabic ? "تصدير CSV" : "Export CSV");
        _exportReportButton.Margin = new Thickness(10, 0, 0, 0);
        _exportReportButton.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
        _exportReportButton.Click += ExportReportButton_Click;
        _refreshButton.Click += (_, _) => LoadRows();
        _addButton = PrimaryButton(IsArabic ? "+ إضافة منتج" : "+ Add product");
        _addButton.Margin = new Thickness(10, 0, 0, 0);
        _addButton.Click += AddProductButton_Click;
        _editProductButton = PrimaryButton(IsArabic ? "تعديل المنتج المحدد" : "Edit selected product");
        _editProductButton.Margin = new Thickness(10, 0, 0, 0);
        _editProductButton.Click += EditProductButton_Click;
        _toggleProductButton = PrimaryButton(IsArabic ? "تغيير حالة المنتج" : "Toggle product status");
        _toggleProductButton.Margin = new Thickness(10, 0, 0, 0);
        _toggleProductButton.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        _toggleProductButton.Click += ToggleProductButton_Click;
        _manageCategoriesButton = PrimaryButton(IsArabic ? "إدارة التصنيفات" : "Manage categories");
        _manageCategoriesButton.Margin = new Thickness(10, 0, 0, 0);
        _manageCategoriesButton.Background = new SolidColorBrush(Color.FromRgb(71, 85, 105));
        _manageCategoriesButton.Click += ManageCategoriesButton_Click;
        _cashMovementButton = PrimaryButton(IsArabic ? "+ حركة نقدية" : "+ Cash movement");
        _cashMovementButton.Margin = new Thickness(10, 0, 0, 0);
        _cashMovementButton.Click += CashMovementButton_Click;
        _approveDifferenceButton = PrimaryButton(IsArabic ? "اعتماد فرق العهدة" : "Approve difference");
        _approveDifferenceButton.Margin = new Thickness(10, 0, 0, 0);
        _approveDifferenceButton.Click += ApproveDifferenceButton_Click;
        _searchBox.TextChanged += (_, _) => ApplySearch();
        _categoryFilter.SelectionChanged += (_, _) => ApplySearch();

        ConfigureGrid();
        Content = BuildShell();
        _grid.AutoGeneratingColumn += (_, e) =>
        {
            if (e.PropertyName == "ImageKey" && _section == "Products")
            {
                var imageFactory = new FrameworkElementFactory(typeof(Image));
                imageFactory.SetValue(FrameworkElement.WidthProperty, 48d);
                imageFactory.SetValue(FrameworkElement.HeightProperty, 48d);
                imageFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4));
                imageFactory.SetValue(Image.StretchProperty, System.Windows.Media.Stretch.UniformToFill);
                imageFactory.SetValue(Image.ToolTipProperty, IsArabic ? "صورة المنتج" : "Product image");
                var imageBinding = new Binding("ImageKey") { Converter = new ProductImageKeyConverter() };
                imageFactory.SetBinding(Image.SourceProperty, imageBinding);
                e.Column = new DataGridTemplateColumn
                {
                    Header = IsArabic ? "الصورة" : "Image",
                    Width = new DataGridLength(76), MinWidth = 76, MaxWidth = 90,
                    IsReadOnly = true,
                    CellTemplate = new DataTemplate { VisualTree = imageFactory }
                };
                return;
            }
            if (IsArabic && ArabicHeaders.TryGetValue(e.PropertyName, out var headerText)) e.Column.Header = headerText;
            e.Column.IsReadOnly = true;
            e.Column.CanUserSort = false;
            e.Column.MinWidth = 95;
            e.Column.MaxWidth = 340;
            e.Column.Width = DataGridLength.SizeToCells;
            if (e.Column is DataGridTextColumn textColumn)
            {
                textColumn.ElementStyle = new Style(typeof(TextBlock));
                textColumn.ElementStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                textColumn.ElementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
                textColumn.ElementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                textColumn.ElementStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(e.PropertyName)));
            }
        };
        Loaded += (_, _) => { RefreshLocalizedChrome(); LoadRows(); };
        AppearanceManager.Current.AppearanceChanged += OnAppearanceChanged;
        Closed += (_, _) => AppearanceManager.Current.AppearanceChanged -= OnAppearanceChanged;
    }

    private bool IsArabic => LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
    private void OnAppearanceChanged(object? sender, EventArgs e) => RefreshLocalizedChrome();

    private UIElement BuildShell()
    {
        var shell = new Grid();
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(218) });
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = new Border { Background = new SolidColorBrush(Navy), Padding = new Thickness(15, 20, 13, 18) };
        var side = new DockPanel();
        var brand = new StackPanel { Margin = new Thickness(7, 0, 5, 28) };
        brand.Children.Add(new TextBlock { Text = "N  NexusPOS", Foreground = Brushes.White, FontSize = 21, FontWeight = FontWeights.Bold });
        brand.Children.Add(new TextBlock { Text = IsArabic ? "نظام إدارة الصيدلية" : "Pharmacy Management", Foreground = new SolidColorBrush(Color.FromRgb(174, 193, 216)), FontSize = 11, Margin = new Thickness(2, 5, 0, 0) });
        DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);

        var nav = new StackPanel();
        AddNav(nav, "⌂", "الرئيسية", "Home", "Home");
        AddNav(nav, "▣", "المبيعات", "Sales", "Sales");
        AddNav(nav, "▦", "المنتجات", "Products", "Products");
        AddNav(nav, "▤", "المخزون والجرد", "Inventory", "Inventory");
        AddNav(nav, "↕", "حركات المخزون", "Stock movements", "StockMovements");
        AddNav(nav, "⇩", "المشتريات", "Purchases", "Purchases");
        AddNav(nav, "♙", "الموردون", "Suppliers", "Suppliers");
        AddNav(nav, "◷", "الورديات والعهدة", "Shifts", "Shifts");
        AddNav(nav, "▧", "التقارير", "Reports", "Reports");
        AddNav(nav, "⚙", "المستخدمون والإعدادات", "Users", "Users");
        side.Children.Add(nav);
        var version = new TextBlock { Text = "NexusPOS  •  Desktop", Foreground = new SolidColorBrush(Color.FromRgb(153, 174, 200)), FontSize = 10, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
        DockPanel.SetDock(version, Dock.Bottom); side.Children.Add(version);
        sidebar.Child = side;
        Grid.SetColumn(sidebar, 0); shell.Children.Add(sidebar);

        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62) });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var topbar = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(229, 235, 244)), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(24, 0, 25, 0) };
        var top = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
        var pageLabel = new TextBlock { Text = IsArabic ? "لوحة الإدارة  /  " + Title : "Workspace  /  " + Title, Foreground = new SolidColorBrush(Muted), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(pageLabel, Dock.Left); top.Children.Add(pageLabel);
        var user = new TextBlock { Text = IsArabic ? "●  المدير" : "●  Administrator", Foreground = new SolidColorBrush(Navy), FontWeight = FontWeights.SemiBold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(user, Dock.Right); top.Children.Add(user); topbar.Child = top;
        Grid.SetRow(topbar, 0); main.Children.Add(topbar);

        var body = new DockPanel { Margin = new Thickness(28, 25, 28, 20) };
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 21) };
        header.Children.Add(_pageTitle); header.Children.Add(_pageDescription);
        DockPanel.SetDock(header, Dock.Top); body.Children.Add(header);

        if (_section == "Products")
        {
            var stats = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int i = 0; i < 3; i++) stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddMetric(stats, 0, IsArabic ? "إجمالي المنتجات" : "Total products", _allCount, "▦", Color.FromRgb(22, 105, 232));
            AddMetric(stats, 1, IsArabic ? "الأصناف النشطة" : "Active products", _activeCount, "✓", Color.FromRgb(22, 163, 74));
            AddMetric(stats, 2, IsArabic ? "تحتاج إعادة طلب" : "Need reorder", _lowCount, "!", Color.FromRgb(217, 119, 6));
            DockPanel.SetDock(stats, Dock.Top); body.Children.Add(stats);
        }

        var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 15) };
        _searchBox.Width = 310; _searchBox.ToolTip = IsArabic ? "ابحث بالاسم أو الكود أو الباركود" : "Search by name, code or barcode";
        _searchBox.Background = Brushes.White; _searchBox.BorderBrush = new SolidColorBrush(Color.FromRgb(215, 224, 237)); _searchBox.BorderThickness = new Thickness(1);
        var searchWrap = new Border { Child = _searchBox, Background = Brushes.White, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(215, 224, 237)), Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(searchWrap, Dock.Left); toolbar.Children.Add(searchWrap);
        if (_section == "Products")
        {
            _categoryFilter.Visibility = Visibility.Visible;
            DockPanel.SetDock(_categoryFilter, Dock.Left); toolbar.Children.Add(_categoryFilter);
        }
        if (_section is "Products" or "Suppliers" or "Purchases" or "Inventory")
        {
            _addButton.Content = _section == "Suppliers" ? (IsArabic ? "+ إضافة مورد" : "+ Add supplier") : _section == "Purchases" ? (IsArabic ? "+ فاتورة شراء" : "+ New purchase") : _section == "Inventory" ? (IsArabic ? "+ بدء الجرد" : "+ Start stock count") : (IsArabic ? "+ إضافة منتج" : "+ Add product");
            DockPanel.SetDock(_addButton, Dock.Right); toolbar.Children.Add(_addButton);
        }
        if (_section == "Products")
        {
            DockPanel.SetDock(_manageCategoriesButton, Dock.Right); toolbar.Children.Add(_manageCategoriesButton);
            DockPanel.SetDock(_toggleProductButton, Dock.Right); toolbar.Children.Add(_toggleProductButton);
            DockPanel.SetDock(_editProductButton, Dock.Right); toolbar.Children.Add(_editProductButton);
        }
        if (_section == "Shifts")
        {
            DockPanel.SetDock(_approveDifferenceButton, Dock.Right); toolbar.Children.Add(_approveDifferenceButton);
            DockPanel.SetDock(_cashMovementButton, Dock.Right); toolbar.Children.Add(_cashMovementButton);
        }
        if (_section == "Reports")
        {
            _reportFromDate.ToolTip = IsArabic ? "من تاريخ" : "From date";
            _reportToDate.ToolTip = IsArabic ? "إلى تاريخ" : "To date";
            _reportFromDate.SelectedDateChanged += (_, _) => LoadRows();
            _reportToDate.SelectedDateChanged += (_, _) => LoadRows();
            var dateFilters = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            dateFilters.Children.Add(new TextBlock { Text = IsArabic ? "من:" : "From:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0), Foreground = new SolidColorBrush(Navy) });
            dateFilters.Children.Add(_reportFromDate);
            dateFilters.Children.Add(new TextBlock { Text = IsArabic ? "إلى:" : "To:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0), Foreground = new SolidColorBrush(Navy) });
            dateFilters.Children.Add(_reportToDate);
            var clearDates = PrimaryButton(IsArabic ? "مسح التاريخ" : "Clear dates");
            clearDates.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            clearDates.Margin = new Thickness(0, 0, 10, 0);
            clearDates.Click += (_, _) => { _reportFromDate.SelectedDate = null; _reportToDate.SelectedDate = null; LoadRows(); };
            dateFilters.Children.Add(clearDates);
            DockPanel.SetDock(dateFilters, Dock.Left); toolbar.Children.Add(dateFilters);
            DockPanel.SetDock(_exportReportButton, Dock.Right); toolbar.Children.Add(_exportReportButton);
        }
        DockPanel.SetDock(_refreshButton, Dock.Right); toolbar.Children.Add(_refreshButton);
        DockPanel.SetDock(toolbar, Dock.Top); body.Children.Add(toolbar);

        var tableHeader = new DockPanel { Margin = new Thickness(2, 0, 2, 12) };
        var tableTitle = new TextBlock { Text = _section switch { "Products" => IsArabic ? "قائمة المنتجات" : "Product list", "Suppliers" => IsArabic ? "قائمة الموردين" : "Supplier list", "Purchases" => IsArabic ? "فواتير المشتريات" : "Purchase invoices", "Inventory" => IsArabic ? "أرصدة المخزون" : "Inventory balances", "StockMovements" => IsArabic ? "سجل حركات المخزون" : "Stock movement history", "LowStock" => IsArabic ? "الأصناف القريبة من النفاد" : "Low-stock items", "Shifts" => IsArabic ? "سجل الورديات والعهد" : "Shift and cash records", "Users" => IsArabic ? "حسابات المستخدمين" : "User accounts", "Reports" => IsArabic ? "نتائج التقرير" : "Report results", _ => IsArabic ? "قائمة البيانات" : "Records" }, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Navy), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(tableTitle, Dock.Left); tableHeader.Children.Add(tableTitle);
        var tableCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(14), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(230, 235, 243)), Child = _grid };
        var tableStack = new DockPanel(); DockPanel.SetDock(tableHeader, Dock.Top); tableStack.Children.Add(tableHeader); tableStack.Children.Add(tableCard);
        DockPanel.SetDock(_status, Dock.Bottom); body.Children.Add(_status);
        DockPanel.SetDock(tableStack, Dock.Top); body.Children.Add(tableStack);
        var bodyBorder = new Border { Child = body, Background = new SolidColorBrush(Page) };
        Grid.SetRow(bodyBorder, 1); main.Children.Add(bodyBorder);
        Grid.SetColumn(main, 1); shell.Children.Add(main);
        return shell;
    }

    // Reports are intentionally kept on the previous simple layout; this phase does not redesign reports.
    private UIElement BuildLegacyReportsView()
    {
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        header.Children.Add(_pageTitle); header.Children.Add(_pageDescription);
        var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 14) };
        _searchBox.Width = 340; _searchBox.ToolTip = IsArabic ? "ابحث في السجلات المعروضة" : "Search displayed records";
        _searchBox.Background = Brushes.White; _searchBox.BorderBrush = new SolidColorBrush(Color.FromRgb(215, 224, 237)); _searchBox.BorderThickness = new Thickness(1);
        if (_section == "Shifts")
        {
            DockPanel.SetDock(_approveDifferenceButton, Dock.Right); toolbar.Children.Add(_approveDifferenceButton);
            DockPanel.SetDock(_cashMovementButton, Dock.Right); toolbar.Children.Add(_cashMovementButton);
        }
        if (_section == "Reports")
        {
            _reportFromDate.ToolTip = IsArabic ? "من تاريخ" : "From date";
            _reportToDate.ToolTip = IsArabic ? "إلى تاريخ" : "To date";
            _reportFromDate.SelectedDateChanged += (_, _) => LoadRows();
            _reportToDate.SelectedDateChanged += (_, _) => LoadRows();

            var dateFilters = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            dateFilters.Children.Add(new TextBlock { Text = IsArabic ? "من:" : "From:", VerticalAlignment = VerticalAlignment.Center });
            dateFilters.Children.Add(_reportFromDate);
            dateFilters.Children.Add(new TextBlock { Text = IsArabic ? "إلى:" : "To:", VerticalAlignment = VerticalAlignment.Center });
            dateFilters.Children.Add(_reportToDate);
            var clearDates = PrimaryButton(IsArabic ? "مسح التاريخ" : "Clear dates");
            clearDates.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            clearDates.Margin = new Thickness(0, 0, 12, 0);
            clearDates.Click += (_, _) => { _reportFromDate.SelectedDate = null; _reportToDate.SelectedDate = null; LoadRows(); };
            dateFilters.Children.Add(clearDates);
            DockPanel.SetDock(dateFilters, Dock.Left); toolbar.Children.Add(dateFilters);
            DockPanel.SetDock(_exportReportButton, Dock.Right); toolbar.Children.Add(_exportReportButton);
        }
        DockPanel.SetDock(_refreshButton, Dock.Right); toolbar.Children.Add(_refreshButton);
        var searchBorder = new Border { Child = _searchBox, Background = Brushes.White, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(215, 224, 237)), Padding = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(searchBorder, Dock.Left); toolbar.Children.Add(searchBorder);
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(16), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(230, 235, 243)), Child = _grid };
        var body = new DockPanel(); DockPanel.SetDock(header, Dock.Top); body.Children.Add(header); DockPanel.SetDock(toolbar, Dock.Top); body.Children.Add(toolbar); DockPanel.SetDock(_status, Dock.Bottom); body.Children.Add(_status); body.Children.Add(card);
        return new Border { Padding = new Thickness(26), Background = new SolidColorBrush(Page), Child = body };
    }

    private void AddNav(Panel panel, string glyph, string arabic, string english, string? section)
    {
        bool selected = section == _section;
        var button = new Button
        {
            Content = $"{glyph}    {(IsArabic ? arabic : english)}", Height = 43, Margin = new Thickness(0, 2, 0, 3),
            Padding = new Thickness(13, 0, 9, 0), HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = selected ? new SolidColorBrush(Blue) : Brushes.Transparent,
            Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 12.5,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = section is null ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand
        };
        if (section is not null && _navigate is not null)
        {
            button.Click += (_, _) =>
            {
                if (section == _section) return;
                Close();
                _navigate(section, IsArabic ? arabic : english);
            };
        }
        else if (section is null) button.IsEnabled = false;
        panel.Children.Add(button);
    }

    private static Button PrimaryButton(string text) => new()
    {
        Content = text, Padding = new Thickness(17, 9, 17, 9), MinHeight = 40,
        Background = new SolidColorBrush(Blue), Foreground = Brushes.White, BorderThickness = new Thickness(0),
        FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand
    };

    private static void AddMetric(Grid grid, int column, string label, TextBlock value, string glyph, Color accent)
    {
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(9), Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(0, 0, column == 2 ? 0 : 14, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(230, 235, 243)), BorderThickness = new Thickness(1) };
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var textStack = new StackPanel(); textStack.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = new SolidColorBrush(Muted) }); value.Text = "—"; value.Margin = new Thickness(0, 8, 0, 0); textStack.Children.Add(value); layout.Children.Add(textStack);
        var icon = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.FromArgb(24, accent.R, accent.G, accent.B)), Child = new TextBlock { Text = glyph, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(accent), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        Grid.SetColumn(icon, 1); layout.Children.Add(icon); card.Child = layout; Grid.SetColumn(card, column); grid.Children.Add(card);
    }

    private sealed class ProductImageKeyConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not string key || string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "ProductImages");
                var relative = key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var fullPath = Path.GetFullPath(Path.Combine(root, relative));
                var rootFull = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) return null;
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 120;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
    }

    private sealed class AlternatingRowBrushConverter : IValueConverter
    {
        private static readonly Brush First = Brushes.White;
        private static readonly Brush Second = new SolidColorBrush(Color.FromRgb(248, 250, 253));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is int index && index % 2 == 1 ? Second : First;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => Binding.DoNothing;
    }

    private void ConfigureGrid()
    {
        _grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader));
        _grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(246, 249, 253))));
        _grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85))));
        _grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        _grid.ColumnHeaderStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 0, 12, 0)));
        _grid.RowStyle = new Style(typeof(DataGridRow));
        _grid.RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        _grid.AlternationCount = 2;
        _grid.RowStyle.Setters.Add(new Setter(Control.BackgroundProperty, new Binding("(ItemsControl.AlternationIndex)")
        {
            Converter = new AlternatingRowBrushConverter()
        }));
        _grid.RowStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(238, 242, 247))));
        _grid.RowStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        _grid.CellStyle = new Style(typeof(DataGridCell));
        _grid.CellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 6, 12, 6)));
        _grid.CellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        _grid.CellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
    }

    private void RefreshLocalizedChrome()
    {
        _refreshButton.Content = IsArabic ? "تحديث البيانات" : "Refresh data";
        if (_section is "Products" or "Suppliers" or "Purchases" or "Inventory") _addButton.Content = _section == "Suppliers" ? (IsArabic ? "+ إضافة مورد" : "+ Add supplier") : _section == "Purchases" ? (IsArabic ? "+ فاتورة شراء" : "+ New purchase") : _section == "Inventory" ? (IsArabic ? "+ بدء الجرد" : "+ Start stock count") : (IsArabic ? "+ إضافة منتج" : "+ Add product");
        _searchBox.ToolTip = IsArabic ? "ابحث بالاسم أو الكود أو الباركود" : "Search by name, code or barcode";
        _pageTitle.Text = _section switch
        {
            "Products" => IsArabic ? "المنتجات والتصنيفات" : "Products & Categories",
            "Inventory" => IsArabic ? "المخزون والجرد" : "Inventory & Stocktaking",
            "StockMovements" => IsArabic ? "حركات المخزون" : "Stock movements",
            "LowStock" => IsArabic ? "الأصناف منخفضة المخزون" : "Low-stock products",
            "Shifts" => IsArabic ? "الورديات والعهدة" : "Shifts & Cash",
            "Purchases" => IsArabic ? "المشتريات" : "Purchases",
            "Suppliers" => IsArabic ? "الموردون" : "Suppliers",
            "Users" => IsArabic ? "المستخدمون" : "Users",
            "Reports" => IsArabic ? "التقارير" : "Reports",
            _ => Title
        };
        _pageDescription.Text = _section switch
        {
            "Products" => IsArabic ? "إدارة الأصناف والباركود والتصنيفات والأسعار وحالة المخزون" : "Manage products, barcodes, categories, prices and stock status",
            "Inventory" => IsArabic ? "متابعة الكميات المتاحة وحدود إعادة الطلب" : "Monitor available quantities and reorder thresholds",
            "StockMovements" => IsArabic ? "مراجعة سجل حركات المخزون المسجلة مع مرجع كل حركة" : "Review recorded stock movements and their references",
            "LowStock" => IsArabic ? "الأصناف التي وصلت إلى حد إعادة الطلب المحدد لها" : "Items at or below their configured reorder threshold",
            "Shifts" => IsArabic ? "متابعة الورديات وأرصدة العهدة النقدية" : "Review shifts and cash balances",
            "Purchases" => IsArabic ? "مراجعة فواتير وحركات الشراء المسجلة" : "Review recorded purchase transactions",
            "Suppliers" => IsArabic ? "بيانات الموردين المسجلة في النظام" : "Supplier records registered in the system",
            "Users" => IsArabic ? "حسابات المستخدمين وحالاتها" : "User accounts and their status",
            "Reports" => IsArabic ? "عرض التقارير المسجلة" : "Review recorded reports",
            _ => string.Empty
        };
        _searchBox.FlowDirection = LocalizationManager.Current.FlowDirection;
        FlowDirection = LocalizationManager.Current.FlowDirection;
        Title = _pageTitle.Text;
        _status.Text = IsArabic ? $"عدد السجلات: {_grid.Items.Count}" : $"Records: {_grid.Items.Count}";
    }

    private static readonly Dictionary<string, string> ArabicHeaders = new()
    {
        ["InvoiceNo"] = "رقم الفاتورة", ["SaleDate"] = "التاريخ والوقت", ["SellerCode"] = "كود الموظف",
        ["SellerName"] = "اسم الموظف", ["Subtotal"] = "الإجمالي قبل الخصم", ["Discount"] = "الخصم",
        ["Tax"] = "الضريبة", ["Total"] = "الإجمالي", ["Paid"] = "المدفوع", ["Returned"] = "قيمة المرتجعات", ["NetSales"] = "صافي المبيعات", ["Status"] = "حالة الفاتورة",
        ["PaymentStatus"] = "حالة السداد", ["CustomerPhone"] = "هاتف العميل", ["Id"] = "المعرّف",
        ["Name"] = "الاسم", ["Code"] = "الكود", ["Barcode"] = "الباركود", ["Price"] = "سعر البيع", ["Quantity"] = "الكمية المتاحة",
        ["CostPrice"] = "تكلفة الشراء للوحدة", ["InventoryCostValue"] = "قيمة المخزون بالتكلفة", ["MinStock"] = "حد إعادة الطلب", ["Category"] = "التصنيف",
        ["IsActive"] = "الحالة", ["UnitOfMeasure"] = "وحدة القياس", ["Quantity"] = "الكمية",
        ["CurrentQuantity"] = "الكمية الحالية", ["ReorderThreshold"] = "حد إعادة الطلب", ["OpeningFloat"] = "رصيد الافتتاح",
        ["CountedCash"] = "النقدية المعدودة", ["ExpectedCash"] = "النقدية المتوقعة", ["Difference"] = "الفرق",
        ["CloseNotes"] = "ملاحظات الإغلاق", ["Status"] = "الحالة", ["UserId"] = "المستخدم", ["TerminalId"] = "جهاز الكاشير",
        ["SupplierId"] = "المورد", ["Supplier"] = "المورد", ["PurchaseDateUtc"] = "تاريخ الشراء", ["SupplierInvoiceNumber"] = "رقم فاتورة المورد", ["Total"] = "الإجمالي",
        ["CreatedAtUtc"] = "تاريخ الإنشاء", ["LastLoginAtUtc"] = "آخر تسجيل دخول", ["Phone"] = "الهاتف", ["TaxNumber"] = "الرقم الضريبي",
        ["ProductName"] = "اسم الصنف", ["MovementType"] = "نوع الحركة", ["QuantityChange"] = "تغير الكمية",
        ["ReferenceType"] = "نوع المرجع", ["ReferenceId"] = "رقم المرجع", ["Reason"] = "السبب", ["CreatedByUserId"] = "المستخدم المنفذ"
    };


    private void ExportReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_grid.Items.Count == 0)
        {
            MessageBox.Show(this, IsArabic ? "لا توجد بيانات لتصديرها." : "There is no data to export.", IsArabic ? "التقارير" : "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = IsArabic ? "تصدير التقرير الحالي" : "Export current report",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"NexusPOS-{_section}-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            AddExtension = true,
            DefaultExt = ".csv",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var items = _grid.Items.Cast<object>().Where(x => x is not null).ToList();
            if (items.Count == 0) return;
            var properties = TypeDescriptor.GetProperties(items[0]).Cast<PropertyDescriptor>().ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", properties.Select(p => CsvEscape(IsArabic && ArabicHeaders.TryGetValue(p.Name, out var header) ? header : p.Name))));
            foreach (var item in items)
            {
                sb.AppendLine(string.Join(",", properties.Select(p => CsvEscape(p.GetValue(item)))));
            }
            // UTF-8 BOM helps Excel recognize Arabic headers and values correctly.
            File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show(this, IsArabic ? $"تم تصدير {items.Count} سجلًا بنجاح." : $"Exported {items.Count} records successfully.", IsArabic ? "التقارير" : "Reports", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, (IsArabic ? "تعذر تصدير التقرير: " : "Could not export report: ") + ex.GetBaseException().Message, IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatCsvValue(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
        decimal number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static string CsvEscape(object? value)
    {
        var text = FormatCsvValue(value);

        // CSV opened in Excel can interpret untrusted text beginning with formula
        // markers as a formula. Prefix text values only; keep actual numeric values numeric.
        if (value is string && text.Length > 0)
        {
            var firstMeaningful = text.TrimStart();
            if (firstMeaningful.Length > 0 && "=+-@".Contains(firstMeaningful[0]))
                text = "'" + text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private List<object> LoadSalesRows()
    {
        if (_reportFromDate.SelectedDate is DateTime fromSelected && _reportToDate.SelectedDate is DateTime toSelected && fromSelected.Date > toSelected.Date)
        {
            _status.Text = IsArabic ? "تاريخ البداية يجب ألا يكون بعد تاريخ النهاية." : "The start date must not be after the end date.";
            return new List<object>();
        }
        // Financial sales reports must include completed sales only. Excluding Draft alone
        // accidentally includes Cancelled invoices in sales totals and CSV exports.
        var salesQuery = _db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Payments)
            .Where(s => s.Status == UniversalPOS.Domain.Enums.SaleReturnStatus.Completed);
        if (_reportFromDate.SelectedDate is DateTime fromDate)
        {
            var from = fromDate.Date;
            salesQuery = salesQuery.Where(s => s.SaleDate >= from);
        }
        if (_reportToDate.SelectedDate is DateTime toDate)
        {
            var untilExclusive = toDate.Date.AddDays(1);
            salesQuery = salesQuery.Where(s => s.SaleDate < untilExclusive);
        }
        var sales = salesQuery.OrderByDescending(s => s.SaleDate).Take(500).ToList();
        var saleIds = sales.Select(s => s.Id).ToList();
        // Only completed returns reduce net sales; drafts/cancelled returns must not affect totals.
        var completedReturnIds = _db.SaleReturns.AsNoTracking()
            .Where(r => saleIds.Contains(r.SaleId) && r.Status == UniversalPOS.Domain.Enums.SaleReturnStatus.Completed)
            .Select(r => new { r.Id, r.SaleId }).ToList();
        var returnSaleById = completedReturnIds.ToDictionary(r => r.Id, r => r.SaleId);
        var completedReturnIdList = completedReturnIds.Select(r => r.Id).ToList();
        var returnItemTotals = _db.SaleReturnItems.AsNoTracking()
            .Where(i => completedReturnIdList.Contains(EF.Property<int>(i, "SaleReturnId")))
            .Select(i => new { ReturnId = EF.Property<int>(i, "SaleReturnId"), Amount = i.Quantity * i.UnitRefund })
            .ToList();
        var returnedBySale = returnItemTotals
            .GroupBy(i => returnSaleById[i.ReturnId])
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Amount));
        var userIds = sales.Where(s => s.UserId.HasValue).Select(s => s.UserId!.Value).Distinct().ToList();
        var users = _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id);
        var employeeIds = users.Values.Where(u => u.EmployeeId.HasValue).Select(u => u.EmployeeId!.Value).Distinct().ToList();
        var employees = _db.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionary(e => e.Id);
        bool arabic = IsArabic;
        return sales.Select(s =>
        {
            string code = "-", name = "-";
            if (s.UserId is int uid && users.TryGetValue(uid, out var user))
            {
                name = user.DisplayName;
                if (user.EmployeeId is int eid && employees.TryGetValue(eid, out var emp)) { code = emp.Code; name = emp.FullName; }
            }
            decimal returned = returnedBySale.TryGetValue(s.Id, out var amount) ? amount : 0m;
            return (object)new { InvoiceNo = s.Id, s.SaleDate, SellerCode = code, SellerName = name, s.Subtotal, Discount = s.DiscountAmount, Tax = s.TaxAmount, Total = s.TotalAmount, Returned = returned, NetSales = s.TotalAmount - returned, Paid = s.PaidAmount, s.Status, s.PaymentStatus, s.CustomerPhone };
        }).ToList();
    }

    private int? SelectedProductId()
    {
        if (_grid.SelectedItem is null) return null;
        var property = TypeDescriptor.GetProperties(_grid.SelectedItem)["Id"];
        return property?.GetValue(_grid.SelectedItem) is int id && id > 0 ? id : null;
    }

    private void EditProductButton_Click(object sender, RoutedEventArgs e)
    {
        var id = SelectedProductId();
        if (id is null) { MessageBox.Show(this, IsArabic ? "حدد منتجًا من الجدول أولًا." : "Select a product first.", IsArabic ? "المنتجات" : "Products", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            var product = _db.Products.SingleOrDefault(x => x.Id == id.Value);
            if (product is null) { MessageBox.Show(this, IsArabic ? "المنتج لم يعد موجودًا." : "Product no longer exists.", IsArabic ? "المنتجات" : "Products", MessageBoxButton.OK, MessageBoxImage.Warning); LoadRows(); return; }
            var editor = new ProductEditorWindow(_db, product) { Owner = this };
            if (editor.ShowDialog() != true) return;
            _db.SaveChanges(); LoadRows();
            MessageBox.Show(this, IsArabic ? "تم حفظ تعديلات المنتج دون تغيير رصيد المخزون." : "Product changes saved without changing stock balance.", IsArabic ? "المنتجات" : "Products", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, (IsArabic ? "تعذر تعديل المنتج: " : "Could not edit product: ") + ex.GetBaseException().Message, IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ManageCategoriesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new CategoryManagementWindow(_db) { Owner = this };
            window.ShowDialog();
            LoadRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, (IsArabic ? "تعذر فتح إدارة التصنيفات: " : "Could not open category management: ") + ex.GetBaseException().Message,
                IsArabic ? "التصنيفات" : "Categories", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleProductButton_Click(object sender, RoutedEventArgs e)
    {
        var id = SelectedProductId();
        if (id is null) { MessageBox.Show(this, IsArabic ? "حدد منتجًا من الجدول أولًا." : "Select a product first.", IsArabic ? "المنتجات" : "Products", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            var product = _db.Products.SingleOrDefault(x => x.Id == id.Value);
            if (product is null) { LoadRows(); return; }
            var action = product.IsActive ? (IsArabic ? "تعطيل" : "deactivate") : (IsArabic ? "تفعيل" : "activate");
            var answer = MessageBox.Show(this, IsArabic ? $"هل تريد {action} المنتج «{product.Name}»؟" : $"Do you want to {action} product '{product.Name}'?", IsArabic ? "تأكيد تغيير الحالة" : "Confirm status change", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            if (product.IsActive) product.Deactivate(); else product.Activate();
            _db.SaveChanges(); LoadRows();
        }
        catch (Exception ex) { MessageBox.Show(this, (IsArabic ? "تعذر تغيير حالة المنتج: " : "Could not change product status: ") + ex.GetBaseException().Message, IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void AddProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (_section == "Inventory")
        {
            try
            {
                var window = new StocktakingWindow(_db, IsArabic) { Owner = this };
                if (window.ShowDialog() == true) LoadRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, (IsArabic ? "تعذر فتح شاشة الجرد: " : "Could not open stocktaking: ") + ex.GetBaseException().Message, IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }
        if (_section == "Purchases")
        {
            AddPurchase();
            return;
        }
        if (_section == "Suppliers")
        {
            AddSupplier();
            return;
        }
        if (_section != "Products") return;
        try
        {
            using var transaction = _db.Database.BeginTransaction();
            var editor = new ProductEditorWindow(_db) { Owner = this };
            if (editor.ShowDialog() != true) return;
            var product = editor.CreatedProduct;
            if (product is null) return;
            _db.Products.Add(product);
            _db.SaveChanges();
            if (!string.IsNullOrWhiteSpace(editor.SelectedImagePath))
            {
                var sourceImage = new FileInfo(editor.SelectedImagePath);
                var extension = sourceImage.Extension.ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                    throw new InvalidOperationException(IsArabic ? "صيغة الصورة غير مدعومة." : "Unsupported image format.");
                if (sourceImage.Length > 5 * 1024 * 1024)
                    throw new InvalidOperationException(IsArabic ? "حجم الصورة يتجاوز 5 ميجابايت." : "Image exceeds 5 MB.");
                var imageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "ProductImages");
                var relativeImageKey = $"products/{product.Id}/{Guid.NewGuid():N}{(extension == ".jpeg" ? ".jpg" : extension)}";
                var destination = Path.Combine(imageRoot, relativeImageKey.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(editor.SelectedImagePath, destination, overwrite: false);
                product.SetImageKey(relativeImageKey);
                _db.SaveChanges();
            }
            _db.StockBalances.Add(new StockBalance(product.Id, editor.OpeningQuantity));
            if (editor.OpeningQuantity > 0m)
            {
                var session = App.Services.GetRequiredService<CurrentUserSession>();
                if (!session.UserId.HasValue)
                    throw new InvalidOperationException(IsArabic ? "يجب تسجيل الدخول قبل تسجيل رصيد افتتاحي." : "Sign in before recording opening stock.");
                _db.StockMovements.Add(new StockMovement(product.Id, StockMovementType.AdjustmentIncrease,
                    editor.OpeningQuantity, "OpeningStock", product.Id.ToString(),
                    IsArabic ? "رصيد افتتاحي عند إنشاء المنتج" : "Opening stock when product was created", session.UserId.Value));
            }
            _db.SaveChanges();
            transaction.Commit();
            LoadRows();
            MessageBox.Show(IsArabic ? "تمت إضافة المنتج بنجاح." : "Product added successfully.",
                IsArabic ? "المنتجات" : "Products", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(IsArabic ? $"تعذرت إضافة المنتج: {ex.GetBaseException().Message}" : $"Could not add product: {ex.GetBaseException().Message}",
                IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddPurchase()
    {
        try
        {
            var editor = new PurchaseEditorWindow() { Owner = this };
            if (editor.ShowDialog() == true)
            {
                LoadRows();
                MessageBox.Show(this, IsArabic ? "تم حفظ فاتورة الشراء واستلام الأصناف وتحديث المخزون." : "Purchase invoice saved, received and stock updated.", IsArabic ? "المشتريات" : "Purchases", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, (IsArabic ? "تعذر فتح فاتورة الشراء: " : "Could not open purchase invoice: ") + ex.GetBaseException().Message, IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddSupplier()
    {
        try
        {
            var editor = new SupplierEditorWindow(_db) { Owner = this };
            if (editor.ShowDialog() != true || editor.CreatedSupplier is null) return;
            _db.Suppliers.Add(editor.CreatedSupplier);
            _db.SaveChanges();
            LoadRows();
            MessageBox.Show(this,
                IsArabic ? "تمت إضافة المورد بنجاح." : "Supplier added successfully.",
                IsArabic ? "الموردون" : "Suppliers", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                IsArabic ? $"تعذرت إضافة المورد: {ex.GetBaseException().Message}" : $"Could not add supplier: {ex.GetBaseException().Message}",
                IsArabic ? "خطأ" : "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private int? SelectedShiftId()
    {
        if (_grid.SelectedItem is null) return null;
        var property = TypeDescriptor.GetProperties(_grid.SelectedItem)["Id"];
        var value = property?.GetValue(_grid.SelectedItem);
        return value is int id && id > 0 ? id : null;
    }

    private void CashMovementButton_Click(object sender, RoutedEventArgs e)
    {
        if (_section != "Shifts") return;
        var shiftId = SelectedShiftId();
        if (shiftId is null)
        { MessageBox.Show(this, IsArabic ? "حدد وردية من الجدول أولًا." : "Select a shift in the table first.", IsArabic ? "الورديات" : "Shifts", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            var window = new CashMovementWindow(shiftId.Value, IsArabic) { Owner = this };
            if (window.ShowDialog() == true) LoadRows();
        }
        catch (Exception ex)
        { MessageBox.Show(this, ErrorMessageMapper.ToUserMessage(ex), IsArabic ? "تعذر تسجيل الحركة" : "Could not register movement", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void ApproveDifferenceButton_Click(object sender, RoutedEventArgs e)
    {
        var shiftId = SelectedShiftId();
        if (shiftId is null)
        { MessageBox.Show(this, IsArabic ? "حدد وردية من الجدول أولًا." : "Select a shift in the table first.", IsArabic ? "الورديات" : "Shifts", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var confirm = MessageBox.Show(this, IsArabic ? $"هل تريد اعتماد فرق العهدة للوردية رقم {shiftId.Value}؟" : $"Approve the cash difference for shift #{shiftId.Value}?", IsArabic ? "تأكيد الاعتماد" : "Confirm approval", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;
        _approveDifferenceButton.IsEnabled = false;
        try
        {
            var service = App.Services.GetRequiredService<UniversalPOS.Application.CashManagement.ApproveShiftDifferenceService>();
            await service.ExecuteAsync(shiftId.Value);
            LoadRows();
            MessageBox.Show(this, IsArabic ? "تم اعتماد فرق العهدة." : "Cash difference approved.", IsArabic ? "الورديات" : "Shifts", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        { MessageBox.Show(this, ErrorMessageMapper.ToUserMessage(ex), IsArabic ? "تعذر اعتماد الفرق" : "Could not approve difference", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _approveDifferenceButton.IsEnabled = true; }
    }

    private void ApplySearch()
    {
        if (_grid.ItemsSource is null) return;
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_grid.ItemsSource);
        string term = _searchBox.Text?.Trim() ?? string.Empty;
        string selectedCategory = _categoryFilter.SelectedItem?.ToString() ?? string.Empty;
        bool allCategories = selectedCategory == (IsArabic ? "كل التصنيفات" : "All categories") || string.IsNullOrWhiteSpace(selectedCategory);
        view.Filter = item =>
        {
            if (item is null) return false;
            if (_section == "Products" && !allCategories)
            {
                var categoryProperty = TypeDescriptor.GetProperties(item)["Category"];
                if (!string.Equals(categoryProperty?.GetValue(item)?.ToString(), selectedCategory, StringComparison.CurrentCultureIgnoreCase)) return false;
            }
            if (string.IsNullOrWhiteSpace(term)) return true;
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(item))
            {
                var value = property.GetValue(item)?.ToString();
                if (!string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.CurrentCultureIgnoreCase)) return true;
            }
            return false;
        };
        view.Refresh();
        _status.Text = IsArabic ? $"عدد السجلات المطابقة: {_grid.Items.Count}" : $"Matching records: {_grid.Items.Count}";
    }

    private void LoadRows()
    {
        var loadTimer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (_section == "Products")
            {
                var products = (from p in _db.Products.AsNoTracking()
                                join c in _db.Categories.AsNoTracking() on p.CategoryId equals c.Id
                                select new { p.Id, p.Code, p.Name, p.Barcode, p.Price, Category = c.Name, Quantity = _db.StockBalances.Where(b => b.ProductId == p.Id).Select(b => (decimal?)b.Quantity).FirstOrDefault() ?? 0m, p.CostPrice, p.MinStock, p.UnitOfMeasure, p.ImageKey, p.IsActive }).ToList();
                _grid.ItemsSource = products;
                _allCount.Text = products.Count.ToString("N0");
                _activeCount.Text = products.Count(x => x.IsActive).ToString("N0");
                var quantities = _db.StockBalances.AsNoTracking().ToList().ToDictionary(x => x.ProductId, x => x.Quantity);
                var lowIds = products.Where(p => quantities.TryGetValue(p.Id, out var q) && q <= p.MinStock).Select(p => p.Id).ToHashSet();
                _lowCount.Text = lowIds.Count.ToString("N0");
                var categories = _db.Categories.AsNoTracking().Select(c => c.Name).Distinct().OrderBy(x => x).ToList();
                var prior = _categoryFilter.SelectedItem?.ToString();
                _categoryFilter.ItemsSource = new[] { IsArabic ? "كل التصنيفات" : "All categories" }.Concat(categories).ToList();
                _categoryFilter.SelectedItem = prior is not null && _categoryFilter.Items.Contains(prior) ? prior : _categoryFilter.Items[0];
            }
            else
            {
                object rows = _section switch
                {
                    // Left joins are intentional: a product without a StockBalance row must still
                    // appear as zero stock, otherwise low-stock reports hide newly-created items.
                    "LowStock" => (from p in _db.Products.AsNoTracking()
                                    join balance in _db.StockBalances.AsNoTracking() on p.Id equals balance.ProductId into balances
                                    from b in balances.DefaultIfEmpty()
                                    let quantity = b == null ? 0m : b.Quantity
                                    where quantity <= p.MinStock
                                    select new { p.Id, p.Code, p.Name, p.Barcode,
                                        CurrentQuantity = quantity, ReorderThreshold = p.MinStock,
                                        p.CostPrice, InventoryCostValue = quantity * p.CostPrice,
                                        p.UnitOfMeasure }).ToList(),
                    "StockMovements" => (from m in _db.StockMovements.AsNoTracking()
                                          join p in _db.Products.AsNoTracking() on m.ProductId equals p.Id
                                          orderby m.CreatedAtUtc descending
                                          select new { m.Id, ProductName = p.Name, MovementType = m.Type, m.QuantityChange,
                                              m.ReferenceType, m.ReferenceId, m.Reason, m.CreatedAtUtc, m.CreatedByUserId }).ToList(),
                    "Inventory" => (from p in _db.Products.AsNoTracking()
                                    join balance in _db.StockBalances.AsNoTracking() on p.Id equals balance.ProductId into balances
                                    from b in balances.DefaultIfEmpty()
                                    let quantity = b == null ? 0m : b.Quantity
                                    select new { p.Id, p.Code, p.Name, p.Barcode,
                                        Quantity = quantity, p.MinStock, p.CostPrice,
                                        InventoryCostValue = quantity * p.CostPrice,
                                        p.UnitOfMeasure }).ToList(),
                    "Shifts" => _db.CashShifts.AsNoTracking().Select(x => new { x.Id, x.UserId, x.TerminalId, x.OpeningFloat, x.CountedCash, x.ExpectedCash, x.Difference, x.Status, x.CloseNotes }).ToList(),
                    "Purchases" => (from p in _db.Purchases.AsNoTracking()
                                    join s in _db.Suppliers.AsNoTracking() on p.SupplierId equals s.Id
                                    select new { p.Id, Supplier = s.Name, p.Status, p.PurchaseDateUtc, p.SupplierInvoiceNumber,
                                        Total = _db.PurchaseItems.Where(i => EF.Property<int>(i, "PurchaseId") == p.Id).Select(i => (decimal?)(i.UnitCost * i.Quantity)).Sum() ?? 0m }).OrderByDescending(x => x.PurchaseDateUtc).ToList(),
                    "Suppliers" => _db.Suppliers.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Phone, x.TaxNumber, x.IsActive }).ToList(),
                    "Reports" => LoadSalesRows(),
                    "Users" => _db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName, x.DisplayName, x.IsActive, x.CreatedAtUtc, x.LastLoginAtUtc }).ToList(),
                    _ => new List<object>()
                };
                _grid.ItemsSource = (IEnumerable)rows;
            }
            ApplySearch();
            System.Diagnostics.Trace.WriteLine($"Section '{_section}' data loaded in {loadTimer.ElapsedMilliseconds} ms ({_grid.Items.Count} visible rows).");
        }
        catch (Exception ex)
        {
            _grid.ItemsSource = null;
            _status.Text = IsArabic ? "تعذر تحميل البيانات. راجع اتصال قاعدة البيانات وتوافق الموديل." : "Could not load data. Check database connection and model compatibility.";
            System.Diagnostics.Trace.WriteLine($"Failed loading section '{_section}': {ex}");
            MessageBox.Show(
                IsArabic ? "تعذر تحميل بيانات الشاشة. راجع اتصال قاعدة البيانات وسجل الأخطاء." : "Could not load this screen. Check the database connection and diagnostic log.",
                IsArabic ? "خطأ في تحميل الشاشة" : "Screen loading error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
