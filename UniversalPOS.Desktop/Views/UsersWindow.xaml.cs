using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Desktop.Views;

/// <summary>Employee directory using the shared NexusPOS navy/blue visual language.</summary>
public sealed partial class UsersWindow : Window
{
    private sealed record Row(string Code, string Name, string Phone, string Job, string Branch, string HireDate, string Status, bool Active);
    private static readonly Color Navy = Color.FromRgb(20, 45, 75);
    private static readonly Color Blue = Color.FromRgb(17, 105, 232);
    private static readonly Color Canvas = Color.FromRgb(244, 248, 253);
    private readonly EmployeeService _service;
    private readonly bool _arabic;
    private readonly TextBox _codeSearch = new() { MinWidth = 170, Height = 38, Padding = new Thickness(12, 6, 12, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly TextBox _nameSearch = new() { MinWidth = 240, Height = 38, Padding = new Thickness(12, 6, 12, 6), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserReorderColumns = false, CanUserResizeColumns = false, CanUserSortColumns = false, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, RowHeight = 48, ColumnHeaderHeight = 42, Margin = new Thickness(0, 14, 0, 0), Background = Brushes.White, BorderThickness = new Thickness(0) };
    private readonly TextBlock _status = new() { Margin = new Thickness(2, 12, 2, 0), Foreground = Brush(Color.FromRgb(95, 112, 132)), FontSize = 12 };
    private readonly Button _add;
    private readonly PermissionChecker _permissions;
    private readonly Button _detailsButton;
    private readonly Button _toggleButton;

    public UsersWindow(EmployeeService service, PermissionChecker permissions)
    {
        _service = service;
        _permissions = permissions;
        _arabic = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        Title = T("الموظفون | NexusPOS", "Employees | NexusPOS");
        FlowDirection = LocalizationManager.Current.FlowDirection;
        Width = 1240; Height = 760; MinWidth = 920; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush(Canvas);
        FontFamily = new FontFamily("Segoe UI");

        _add = MakeButton(T("＋ إضافة موظف", "＋ Add employee"), true);
        _add.IsEnabled = permissions.HasPermission(PermissionCodes.Employees.Create);
        _add.Click += (_, _) => OpenAdd();

        var title = new TextBlock { Text = T("الموظفون", "Employees"), FontSize = 25, FontWeight = FontWeights.Bold, Foreground = Brush(Navy), VerticalAlignment = VerticalAlignment.Center };
        var subtitle = new TextBlock { Text = T("إدارة بيانات الموظفين وحالتهم الوظيفية", "Manage employee records and employment status"), FontSize = 13, Foreground = Brush(Color.FromRgb(105, 122, 143)), Margin = new Thickness(0, 5, 0, 0) };
        var titleStack = new StackPanel(); titleStack.Children.Add(title); titleStack.Children.Add(subtitle);
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titleStack); Grid.SetColumn(_add, 1); header.Children.Add(_add);

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition()); toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _codeSearch.ToolTip = T("البحث بكود الموظف فقط", "Search by employee code only");
        _nameSearch.ToolTip = T("البحث باسم الموظف فقط", "Search by employee name only");
        foreach (var box in new[] { _codeSearch, _nameSearch })
        {
            box.Background = Brushes.White; box.BorderBrush = Brush(Color.FromRgb(220, 230, 242));
            box.BorderThickness = new Thickness(1); box.FontSize = 13;
        }
        // Fixed, labelled search fields: each searches only its own employee field.
        // Keep column order and widths controlled by the application, not draggable by users.
        var searchWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var codeSearchGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        var codeLabel = new TextBlock { Text = T("كود الموظف", "Employee code"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Foreground = Brush(Navy), FontWeight = FontWeights.SemiBold, FontSize = 13 };
        _codeSearch.Width = 170;
        _codeSearch.ToolTip = T("ابحث بكود الموظف فقط", "Search by employee code only");
        codeSearchGroup.Children.Add(codeLabel); codeSearchGroup.Children.Add(_codeSearch);
        var nameSearchGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var nameLabel = new TextBlock { Text = T("اسم الموظف", "Employee name"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Foreground = Brush(Navy), FontWeight = FontWeights.SemiBold, FontSize = 13 };
        _nameSearch.Width = 220;
        _nameSearch.ToolTip = T("ابحث باسم الموظف فقط", "Search by employee name only");
        nameSearchGroup.Children.Add(nameLabel); nameSearchGroup.Children.Add(_nameSearch);
        searchWrap.Children.Add(codeSearchGroup); searchWrap.Children.Add(nameSearchGroup); toolbar.Children.Add(searchWrap);
        if (permissions.HasPermission(PermissionCodes.Users.View))
        {
            var accounts = MakeButton(T("حسابات الدخول", "Login accounts"), false); accounts.Margin = new Thickness(12, 0, 0, 0);
            accounts.Click += (_, _) => { var window = new UserAccountsWindow(App.Services.GetRequiredService<UniversalPOS.Application.Authentication.UserAccountManagementService>(), permissions) { Owner = this }; window.ShowDialog(); };
            Grid.SetColumn(accounts, 1); toolbar.Children.Add(accounts);
        }

        AddColumn(T("كود الموظف", "Employee code"), nameof(Row.Code), 110);
        AddColumn(T("اسم الموظف", "Employee name"), nameof(Row.Name), 1.5);
        AddColumn(T("رقم الهاتف", "Phone"), nameof(Row.Phone), 1.1);
        AddColumn(T("الوظيفة", "Job title"), nameof(Row.Job), 1.1);
        AddColumn(T("الفرع", "Branch"), nameof(Row.Branch), 1.1);
        AddColumn(T("تاريخ التعيين", "Hire date"), nameof(Row.HireDate), 1.0);
        AddColumn(T("الحالة", "Status"), nameof(Row.Status), 0.8);
        _detailsButton = MakeButton(T("عرض التفاصيل", "View details"), false);
        _toggleButton = MakeButton(T("تغيير الحالة", "Change status"), false);
        _detailsButton.Margin = new Thickness(0, 12, 8, 0);
        _toggleButton.Margin = new Thickness(0, 12, 0, 0);
        _detailsButton.Click += async (_, _) => await ShowSelectedDetailsAsync();
        _toggleButton.Click += async (_, _) => await ToggleSelectedAsync();
        _grid.SelectionChanged += (_, _) => UpdateActionButtons();
        _detailsButton.IsEnabled = false; _toggleButton.IsEnabled = false;
        _grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader)) { Setters = { new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(237, 243, 251))), new Setter(Control.ForegroundProperty, Brush(Navy)), new Setter(Control.FontWeightProperty, FontWeights.SemiBold), new Setter(Control.PaddingProperty, new Thickness(12, 6, 12, 6)), new Setter(Control.BorderBrushProperty, Brush(Color.FromRgb(225, 233, 243))) } };
        _grid.CellStyle = new Style(typeof(DataGridCell)) { Setters = { new Setter(Control.PaddingProperty, new Thickness(10, 4, 10, 4)), new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)), new Setter(Control.BorderBrushProperty, Brush(Color.FromRgb(239, 243, 248))), new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center) } };
        _grid.RowStyle = new Style(typeof(DataGridRow)) { Setters = { new Setter(Control.BackgroundProperty, Brushes.White), new Setter(Control.ForegroundProperty, Brush(Navy)), new Setter(Control.BorderThicknessProperty, new Thickness(0)) }, Triggers = { new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true, Setters = { new Setter(Control.BackgroundProperty, Brush(Color.FromRgb(245, 249, 255))) } } } };

        var contentPanel = new DockPanel();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        actions.Children.Add(_detailsButton); actions.Children.Add(_toggleButton);
        DockPanel.SetDock(actions, Dock.Bottom); contentPanel.Children.Add(actions);
        var cardContent = BuildCard(toolbar);
        contentPanel.Children.Add(cardContent);
        var card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), BorderBrush = Brush(Color.FromRgb(226, 234, 244)), BorderThickness = new Thickness(1), Child = contentPanel };
        var root = new DockPanel { Margin = new Thickness(24) };
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header); root.Children.Add(card); Content = root;
        _codeSearch.TextChanged += async (_, _) => await LoadAsync();
        _nameSearch.TextChanged += async (_, _) => await LoadAsync();
        Loaded += async (_, _) => await LoadAsync();
    }

    private UIElement BuildCard(UIElement toolbar)
    {
        var panel = new DockPanel(); DockPanel.SetDock(toolbar, Dock.Top); panel.Children.Add(toolbar);
        DockPanel.SetDock(_status, Dock.Bottom); panel.Children.Add(_status); panel.Children.Add(_grid); return panel;
    }
    private static SolidColorBrush Brush(Color color) => new(color);
    private string T(string arabic, string english) => _arabic ? arabic : english;
    private Button MakeButton(string text, bool primary) => new() { Content = text, Padding = new Thickness(18, 9, 18, 9), MinHeight = 38, FontSize = 13, FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand, Background = Brush(primary ? Blue : Colors.White), Foreground = Brush(primary ? Colors.White : Navy), BorderBrush = Brush(primary ? Blue : Color.FromRgb(218, 229, 242)), BorderThickness = new Thickness(1) };
    private void AddColumn(string header, string path, double weight) => _grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = new DataGridLength(weight, DataGridLengthUnitType.Star) });
    private void AddColumn(string header, string path, int width) => _grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = new DataGridLength(width) });
    private void OpenAdd()
    {
        var window = new AddEmployeeWindow(App.Services.GetRequiredService<EmployeeService>(), App.Services.GetRequiredService<PermissionChecker>()) { Owner = this };
        if (window.ShowDialog() == true) _ = LoadAsync();
    }
    private void UpdateActionButtons()
    {
        bool selected = _grid.SelectedItem is Row;
        _detailsButton.IsEnabled = selected && _permissions.HasPermission(PermissionCodes.Employees.View);
        _toggleButton.IsEnabled = selected && _permissions.HasPermission(PermissionCodes.Employees.Deactivate);
        if (_grid.SelectedItem is Row row)
            _toggleButton.Content = row.Active ? T("تعطيل الموظف", "Deactivate employee") : T("تفعيل الموظف", "Activate employee");
    }

    private async System.Threading.Tasks.Task ShowSelectedDetailsAsync()
    {
        if (_grid.SelectedItem is not Row row) return;
        try
        {
            var employee = await _service.GetByCodeAsync(row.Code);
            if (employee is null) return;
            var details = string.Join(Environment.NewLine, new[]
            {
                $"{T("كود الموظف", "Employee code")}: {employee.Code}",
                $"{T("الاسم الكامل", "Full name")}: {employee.FullName}",
                $"{T("رقم الهاتف", "Phone")}: {employee.Phone}",
                $"{T("البريد الإلكتروني", "Email")}: {employee.Email}",
                $"{T("الوظيفة", "Job title")}: {JobTitles.Display(employee.JobTitle, _arabic)}",
                $"{T("الفرع", "Branch")}: {row.Branch}",
                $"{T("تاريخ التعيين", "Hire date")}: {employee.HireDate:yyyy-MM-dd}",
                $"{T("الحالة", "Status")}: {(employee.IsActive ? T("نشط", "Active") : T("غير نشط", "Inactive"))}"
            });
            MessageBox.Show(details, T("تفاصيل الموظف", "Employee details"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async System.Threading.Tasks.Task ToggleSelectedAsync()
    {
        if (_grid.SelectedItem is not Row row) return;
        bool next = !row.Active;
        var confirm = MessageBox.Show(T(next ? "هل تريد تفعيل هذا الموظف؟" : "هل تريد تعطيل هذا الموظف؟", next ? "Activate this employee?" : "Deactivate this employee?"),
            Title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;
        try { await _service.SetActiveAsync(row.Code, next); await LoadAsync(); }
        catch (Exception ex) { MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var rows = await _service.SearchAsync(null);
            var code = _codeSearch.Text.Trim();
            var name = _nameSearch.Text.Trim();
            var employees = rows.Where(r => r.JobTitle is not null && r.EmployeeCode != "-")
                .Where(r => string.IsNullOrWhiteSpace(code) || r.EmployeeCode.Contains(code, StringComparison.OrdinalIgnoreCase))
                .Where(r => string.IsNullOrWhiteSpace(name) || r.FullName.Contains(name, StringComparison.OrdinalIgnoreCase))
                .ToList();
            _grid.ItemsSource = employees.Select(r => new Row(r.EmployeeCode, r.FullName, r.Phone, JobTitles.Display(r.JobTitle!, _arabic), r.BranchName ?? "-", r.HireDate?.ToString("yyyy-MM-dd") ?? "-", r.EmployeeActive ? T("نشط", "Active") : T("غير نشط", "Inactive"), r.EmployeeActive)).ToList();
            _status.Text = T($"إجمالي الموظفين: {employees.Count}", $"Total employees: {employees.Count}");
            UpdateActionButtons();
        }
        catch (Exception ex) { _grid.ItemsSource = null; _status.Text = ErrorMessageMapper.ToUserMessage(ex); }
    }
}
