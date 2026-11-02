using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;
using UniversalPOS.Desktop.UI;
using UniversalPOS.Desktop.Views;

namespace UniversalPOS.Desktop.Pages;

/// <summary>Employees list (mockup 3): filters, search, avatar rows and view / edit / deactivate actions.</summary>
public sealed class EmployeesPage : UserControl
{
    private readonly EmployeeService _service;
    private readonly PermissionChecker _permissions;
    private IReadOnlyList<EmployeeListRow> _all = Array.Empty<EmployeeListRow>();
    private IReadOnlyList<DepartmentOption> _departments = Array.Empty<DepartmentOption>();
    private readonly TextBox _search;
    private readonly ComboBox _statusFilter;
    private readonly ComboBox _departmentFilter;
    private readonly PagedTable<EmployeeListRow> _table;
    private readonly TextBlock _message = new() { Foreground = Brushes.Firebrick, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
    private bool _loading;

    public EmployeesPage(EmployeeService service, PermissionChecker permissions)
    {
        _service = service; _permissions = permissions;
        bool ar = UiKit.IsArabic;

        var add = UiKit.PrimaryButton("PersonAdd", UiKit.T("إضافة موظف", "Add employee"));
        add.IsEnabled = permissions.HasPermission(PermissionCodes.Employees.Create);
        add.Click += (_, _) => OpenEditor(null);

        _statusFilter = UiKit.Combo(new[] { UiKit.T("جميع الموظفين", "All employees"), UiKit.T("نشط", "Active"), UiKit.T("غير نشط", "Inactive") });
        _departmentFilter = UiKit.Combo(new[] { UiKit.T("جميع الأقسام", "All departments") });
        var (searchRoot, searchBox) = UiKit.SearchBox(UiKit.T("البحث عن موظف...", "Search for an employee..."));
        _search = searchBox;
        _search.TextChanged += (_, _) => ApplyFilter();
        _statusFilter.SelectionChanged += (_, _) => ApplyFilter();
        _departmentFilter.SelectionChanged += (_, _) => ApplyFilter();

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        for (int i = 0; i < 3; i++) toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _statusFilter.Margin = new Thickness(12, 0, 0, 0);
        _departmentFilter.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.Margin = new Thickness(12, 0, 0, 0);
        searchRoot.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(add, 0); Grid.SetColumn(_statusFilter, 1); Grid.SetColumn(_departmentFilter, 2); Grid.SetColumn(searchRoot, 3);
        toolbar.Children.Add(add); toolbar.Children.Add(_statusFilter); toolbar.Children.Add(_departmentFilter); toolbar.Children.Add(searchRoot);

        var columns = new List<TableColumn<EmployeeListRow>>
        {
            new("#", 0.6, (_, index) => UiKit.Text(index.ToString(), 13)),
            new(UiKit.T("اسم الموظف", "Employee"), 2.4, (e, _) => NameCell(e), HorizontalAlignment.Left),
            new(UiKit.T("الرقم الوظيفي", "Employee ID"), 1.5, (e, _) => UiKit.Text(e.Code)),
            new(UiKit.T("القسم", "Department"), 1.4, (e, _) => UiKit.Text(e.DepartmentName ?? "—")),
            new(UiKit.T("المنصب", "Position"), 1.6, (e, _) => UiKit.Text(JobTitles.Display(e.JobTitle, ar))),
            new(UiKit.T("تاريخ الانضمام", "Join date"), 1.5, (e, _) => UiKit.Text(e.HireDate.ToString("yyyy/MM/dd"))),
            new(UiKit.T("الإجراءات", "Actions"), 1.7, (e, _) => ActionsCell(e)),
        };
        _table = new PagedTable<EmployeeListRow>(columns, UiKit.T("موظفين", "employees"), UiKit.T("لا يوجد موظفون مطابقون.", "No matching employees."));

        var stack = new StackPanel();
        stack.Children.Add(UiKit.PageTitle("People", UiKit.T("الموظفين", "Employees")));
        stack.Children.Add(_message);
        var card = UiKit.Card(new StackPanel { Children = { toolbar, _table } }, 18);
        stack.Children.Add(card);
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack, Padding = new Thickness(0, 0, 4, 0) };
        Loaded += async (_, _) => await LoadAsync();
    }

    private FrameworkElement NameCell(EmployeeListRow e)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        string? photo = string.IsNullOrWhiteSpace(e.PhotoKey) ? null : EmployeePhotoStore.PathFor(e.PhotoKey);
        var avatar = UiKit.Avatar(photo, 38);
        avatar.Margin = new Thickness(0, 0, 0, 0);
        panel.Children.Add(avatar);
        var name = UiKit.Text(e.FullName, 14, e.IsActive ? UiKit.Ink : UiKit.Muted, FontWeights.Medium);
        name.Margin = new Thickness(12, 0, 0, 0);
        panel.Children.Add(name);
        if (!e.IsActive)
        {
            var chip = UiKit.Chip(UiKit.T("غير نشط", "Inactive"), "gray");
            chip.Margin = new Thickness(10, 0, 0, 0);
            panel.Children.Add(chip);
        }
        return panel;
    }

    private FrameworkElement ActionsCell(EmployeeListRow e)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var view = UiKit.IconButton("Eye", UiKit.B("#D9E9FB"), UiKit.B("#1262C4"), UiKit.T("عرض", "View"));
        view.Click += async (_, _) => await ShowDetailsAsync(e);
        var edit = UiKit.IconButton("Edit", UiKit.B("#FDE9C8"), UiKit.B("#C27A0C"), UiKit.T("تعديل", "Edit"));
        edit.IsEnabled = _permissions.HasPermission(PermissionCodes.Employees.Edit);
        edit.Click += (_, _) => OpenEditor(e.Code);
        // Employees are never deleted (invoices and audit entries reference them); the button deactivates / reactivates.
        var toggle = e.IsActive
            ? UiKit.IconButton("Delete", UiKit.B("#FBDADA"), UiKit.B("#C23B3B"), UiKit.T("تعطيل الموظف", "Deactivate employee"))
            : UiKit.IconButton("Check", UiKit.B("#D7F0DE"), UiKit.B("#2B7A43"), UiKit.T("تفعيل الموظف", "Reactivate employee"));
        toggle.IsEnabled = _permissions.HasPermission(PermissionCodes.Employees.Deactivate);
        toggle.Click += async (_, _) => await ToggleAsync(e);
        panel.Children.Add(view); panel.Children.Add(edit); panel.Children.Add(toggle);
        return panel;
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            _message.Visibility = Visibility.Collapsed;
            _all = await _service.ListAsync();
            _departments = await _service.GetDepartmentsAsync();

            int selected = _departmentFilter.SelectedIndex;
            _departmentFilter.Items.Clear();
            _departmentFilter.Items.Add(UiKit.T("جميع الأقسام", "All departments"));
            foreach (var d in _departments) _departmentFilter.Items.Add(d.Name);
            _departmentFilter.SelectedIndex = selected >= 0 && selected < _departmentFilter.Items.Count ? selected : 0;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _message.Text = ErrorMessageMapper.ToUserMessage(ex);
            _message.Visibility = Visibility.Visible;
            _table.SetItems(Array.Empty<EmployeeListRow>());
        }
        finally { _loading = false; }
    }

    private void ApplyFilter()
    {
        if (_all is null || _statusFilter is null || _departmentFilter is null || _table is null) return;
        string term = _search.Text.Trim();
        int status = _statusFilter.SelectedIndex;      // 0 all, 1 active, 2 inactive
        string? department = _departmentFilter.SelectedIndex > 0 ? _departmentFilter.SelectedItem?.ToString() : null;

        var rows = _all.Where(e =>
            (status == 0 || (status == 1 && e.IsActive) || (status == 2 && !e.IsActive)) &&
            (department is null || string.Equals(e.DepartmentName, department, StringComparison.CurrentCultureIgnoreCase)) &&
            (term.Length == 0 ||
             e.FullName.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             e.Code.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
             (e.DepartmentName?.Contains(term, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
             JobTitles.Display(e.JobTitle, UiKit.IsArabic).Contains(term, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _table.SetItems(rows);
    }

    private void OpenEditor(string? code)
    {
        try
        {
            var window = new AddEmployeeWindow(_service, _permissions, code) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) _ = LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("تعذر فتح الشاشة", "Could not open screen"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task ShowDetailsAsync(EmployeeListRow row)
    {
        try
        {
            var employee = await _service.GetByCodeAsync(row.Code);
            if (employee is null) { await LoadAsync(); return; }
            string text = string.Join(Environment.NewLine, new[]
            {
                $"{UiKit.T("الرقم الوظيفي", "Employee ID")}: {employee.Code}",
                $"{UiKit.T("الاسم الكامل", "Full name")}: {employee.FullName}",
                $"{UiKit.T("رقم الهاتف", "Phone")}: {employee.Phone}",
                $"{UiKit.T("البريد الإلكتروني", "Email")}: {employee.Email}",
                $"{UiKit.T("القسم", "Department")}: {row.DepartmentName ?? "—"}",
                $"{UiKit.T("المنصب", "Position")}: {JobTitles.Display(employee.JobTitle, UiKit.IsArabic)}",
                $"{UiKit.T("الفرع", "Branch")}: {row.BranchName}",
                $"{UiKit.T("تاريخ التعيين", "Hire date")}: {employee.HireDate:yyyy/MM/dd}",
                $"{UiKit.T("الحالة", "Status")}: {(employee.IsActive ? UiKit.T("نشط", "Active") : UiKit.T("غير نشط", "Inactive"))}"
            });
            MessageBox.Show(text, UiKit.T("بيانات الموظف", "Employee details"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("تعذر عرض البيانات", "Could not show details"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task ToggleAsync(EmployeeListRow row)
    {
        bool activate = !row.IsActive;
        var answer = MessageBox.Show(
            activate ? UiKit.T($"هل تريد تفعيل الموظف «{row.FullName}»؟", $"Reactivate employee '{row.FullName}'?")
                     : UiKit.T($"هل تريد تعطيل الموظف «{row.FullName}»؟ لن يتم حذف أي بيانات.", $"Deactivate employee '{row.FullName}'? No data will be deleted."),
            UiKit.T("تأكيد", "Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            await _service.SetActiveAsync(row.Code, activate);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), UiKit.T("تعذر تغيير الحالة", "Could not change status"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
