
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.Security;
using UniversalPOS.Application;

namespace UniversalPOS.Desktop.Views;

public partial class UserAccountsWindow : Window
{
    private readonly UserAccountManagementService _service;
    private readonly PermissionChecker _permissions;

    public UserAccountsWindow(
        UserAccountManagementService service,
        PermissionChecker permissions)
    {
        InitializeComponent();

        _service = service
            ?? throw new ArgumentNullException(nameof(service));

        _permissions = permissions
            ?? throw new ArgumentNullException(nameof(permissions));

        Loaded += UserAccountsWindow_Loaded;
    }

    private async void UserAccountsWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= UserAccountsWindow_Loaded;

        try
        {
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذر تحميل الحسابات: {ex.Message}", true);
        }
    }

    private async System.Threading.Tasks.Task LoadAccountsAsync()
    {
        // اربط هنا دالة التحميل الموجودة بالفعل في مشروعك:
        // _service.LoadOptionsAsync(...)
        // _service.LoadAsync(...)

        // بعد التحميل:
        // AccountsGrid.ItemsSource = accounts;
        // AccountCountText.Text = accounts.Count.ToString();

        await System.Threading.Tasks.Task.CompletedTask;
    }

    private async void CreateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameInput.Text))
        {
            ShowStatus("اكتب اسم المستخدم أولًا.", true);
            UsernameInput.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(PasswordInput.Password))
        {
            ShowStatus("اكتب كلمة المرور أولًا.", true);
            PasswordInput.Focus();
            return;
        }

        if (EmployeeCombo.SelectedValue is null)
        {
            ShowStatus("اختر الموظف المرتبط بالحساب.", true);
            EmployeeCombo.Focus();
            return;
        }

        if (RoleCombo.SelectedValue is null)
        {
            ShowStatus("اختر دور المستخدم.", true);
            RoleCombo.Focus();
            return;
        }

        try
        {
            // استدعِ CreateAsync الموجودة في خدمتك
            // باستخدام أنواع القيم التي تتطلبها الدالة الفعلية.

            ShowStatus("أكمل ربط عملية إنشاء الحساب بالخدمة الموجودة.", true);
            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذر إنشاء الحساب: {ex.Message}", true);
        }
    }

    private async void SearchInput_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        try
        {
            // اربط البحث بخدمة LoadAsync الموجودة عندك،
            // أو رشّح البيانات المحمّلة إذا كانت الخدمة لا تدعم البحث.
            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذر تنفيذ البحث: {ex.Message}", true);
        }
    }

    private async void ToggleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is null)
        {
            ShowStatus("اختر حسابًا أولًا.", true);
            return;
        }

        try
        {
            // استدعِ ToggleAsync للحساب المحدد.
            await System.Threading.Tasks.Task.CompletedTask;
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذر تغيير حالة الحساب: {ex.Message}", true);
        }
    }

    private async void ChangeRoleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is null)
        {
            ShowStatus("اختر حسابًا أولًا.", true);
            return;
        }

        if (RoleCombo.SelectedValue is null)
        {
            ShowStatus("اختر الدور الجديد.", true);
            return;
        }

        try
        {
            // استدعِ ChangeRoleAsync باستخدام الحساب والدور الجديد.
            await System.Threading.Tasks.Task.CompletedTask;
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذر تغيير الدور: {ex.Message}", true);
        }
    }

    private async void ResetPasswordButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is null)
        {
            ShowStatus("اختر حسابًا أولًا.", true);
            return;
        }

        try
        {
            // استدعِ ResetAsync للحساب المحدد،
            // مع الالتزام بطريقة إعادة تعيين كلمة المرور في خدمتك.
            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (Exception ex)
        {
            ShowStatus($"تعذرت إعادة تعيين كلمة المرور: {ex.Message}", true);
        }
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }
}
