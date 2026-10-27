using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Desktop.Views;

/// <summary>Employees and login accounts in one searchable list, with the "Add employee" button on top.</summary>
public sealed class UsersWindow : Window
{
    private sealed record Row(string Code, string Name, string UserName, string Job, string Branch, string HireDate, string Status, string Roles);

    private readonly EmployeeService _service;
    private readonly bool _arabic;
    private readonly TextBox _search = new() { MinWidth = 280, Margin = new Thickness(12, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 8, 0, 0) };

    public UsersWindow(EmployeeService service, PermissionChecker permissions)
    {
        _service = service;
        _arabic = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        Title = T("إدارة المستخدمين والموظفين", "Users & employees");
        FlowDirection = LocalizationManager.Current.FlowDirection;
        Width = 1100; Height = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var top = new DockPanel();
        if (permissions.HasPermission(PermissionCodes.Employees.Create))
        {
            var add = new Button { Content = T("إضافة موظف", "Add employee"), Padding = new Thickness(16, 7, 16, 7) };
            add.Click += (_, _) => OpenAdd();
            top.Children.Add(add);
        }
        top.Children.Add(new TextBlock { Text = T("بحث:", "Search:"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) });
        top.Children.Add(_search);
        _search.ToolTip = T("كود الموظف أو الاسم أو اسم المستخدم أو الفرع أو الوظيفة", "Employee code, name, username, branch or job");

        AddColumn(T("كود الموظف", "Code"), nameof(Row.Code));
        AddColumn(T("اسم الموظف", "Name"), nameof(Row.Name));
        AddColumn(T("اسم المستخدم", "Username"), nameof(Row.UserName));
        AddColumn(T("الوظيفة", "Job"), nameof(Row.Job));
        AddColumn(T("الفرع", "Branch"), nameof(Row.Branch));
        AddColumn(T("تاريخ التعيين", "Hire date"), nameof(Row.HireDate));
        AddColumn(T("حالة الحساب", "Account"), nameof(Row.Status));
        AddColumn(T("الأدوار", "Roles"), nameof(Row.Roles));

        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_grid);
        Content = root;

        _search.TextChanged += async (_, _) => await LoadAsync();
        Loaded += async (_, _) => await LoadAsync();
    }

    private string T(string arabic, string english) => _arabic ? arabic : english;

    private void AddColumn(string header, string path) =>
        _grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new System.Windows.Data.Binding(path), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });

    private void OpenAdd()
    {
        var window = new AddEmployeeWindow(
            App.Services.GetRequiredService<EmployeeService>(),
            App.Services.GetRequiredService<PermissionChecker>()) { Owner = this };

        if (window.ShowDialog() == true)
            _ = LoadAsync();   // the new employee appears in the results
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            var rows = await _service.SearchAsync(_search.Text);
            _grid.ItemsSource = rows.Select(r => new Row(
                r.EmployeeCode,
                _arabic ? r.ArabicName : r.EnglishName,
                r.UserName ?? "-",
                r.JobTitle is null ? "-" : JobTitles.Display(r.JobTitle, _arabic),
                r.BranchName ?? "-",
                r.HireDate?.ToString("yyyy-MM-dd") ?? "-",
                r.AccountActive is null ? T("بدون حساب", "No account") : r.AccountActive.Value ? T("نشط", "Active") : T("غير نشط", "Inactive"),
                string.IsNullOrEmpty(r.Roles) ? "-" : r.Roles)).ToList();
            _status.Text = T($"عدد السجلات: {rows.Count}", $"Records: {rows.Count}");
        }
        catch (Exception ex)
        {
            _grid.ItemsSource = null;
            _status.Text = ErrorMessageMapper.ToUserMessage(ex);
        }
    }
}
