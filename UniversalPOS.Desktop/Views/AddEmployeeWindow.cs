using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Desktop.Views;

/// <summary>Separate screen for adding an employee, with an optional login account. Keeps typed data when saving fails.</summary>
public sealed class AddEmployeeWindow : Window
{
    private sealed record Option(string Key, string Text);

    private static readonly Brush ErrorBrush = Brushes.Firebrick;

    private readonly EmployeeService _service;
    private readonly bool _arabic;
    private readonly Dictionary<string, (Control Control, TextBlock Error)> _fields = new();

    private readonly Border _banner = new()
    {
        Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA)),
        BorderBrush = ErrorBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed
    };
    private readonly TextBlock _bannerText = new() { Foreground = ErrorBrush, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold };

    private readonly TextBox _code = new(), _arabicName = new(), _englishName = new(), _phone = new(),
        _qualification = new(), _address = new(), _age = new() { IsReadOnly = true }, _userName = new();
    private readonly DatePicker _birthDate = new(), _hireDate = new();
    private readonly ComboBox _job = new() { DisplayMemberPath = "Text", SelectedValuePath = "Key" };
    private readonly ComboBox _branch = new() { DisplayMemberPath = "Name", SelectedValuePath = "Id" };
    private readonly ComboBox _role = new();
    private readonly PasswordBox _password = new();
    private readonly CheckBox _createAccount = new();
    private readonly StackPanel _accountPanel = new() { Visibility = Visibility.Collapsed };
    private readonly Button _save = new() { Padding = new Thickness(22, 8, 22, 8), Margin = new Thickness(0, 0, 8, 0) };

    public AddEmployeeWindow(EmployeeService service, PermissionChecker permissions)
    {
        _service = service;
        _arabic = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

        Title = T("إضافة موظف", "Add employee");
        FlowDirection = LocalizationManager.Current.FlowDirection;
        Width = 560; Height = 760; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var form = new StackPanel { Margin = new Thickness(24) };
        _bannerText.Text = T("يرجى استكمال الحقول المطلوبة وتصحيح الحقول المحددة.", "Please complete the required fields and fix the highlighted ones.");
        _banner.Child = _bannerText;
        form.Children.Add(_banner);

        AddField(form, "Code", T("كود الموظف *", "Employee code *"), _code);
        AddField(form, "ArabicName", T("الاسم الثلاثي بالعربية *", "Full name (Arabic, three parts) *"), _arabicName);
        AddField(form, "EnglishName", T("الاسم الثلاثي بالإنجليزية *", "Full name (English, three parts) *"), _englishName);
        AddField(form, "BirthDate", T("تاريخ الميلاد *", "Date of birth *"), _birthDate);
        AddField(form, "Age", T("السن (يُحسب تلقائيًا)", "Age (calculated)"), _age);
        AddField(form, "Phone", T("رقم الهاتف *", "Phone *"), _phone);
        AddField(form, "JobTitle", T("الوظيفة *", "Job title *"), _job);
        AddField(form, "BranchId", T("الفرع *", "Branch *"), _branch);
        AddField(form, "Qualification", T("المؤهل الدراسي *", "Qualification *"), _qualification);
        AddField(form, "Address", T("العنوان *", "Address *"), _address);
        AddField(form, "HireDate", T("تاريخ التعيين *", "Hire date *"), _hireDate);

        _birthDate.DisplayDateEnd = DateTime.Today;
        _hireDate.DisplayDateEnd = DateTime.Today;
        _birthDate.SelectedDateChanged += (_, _) => UpdateAge();

        if (permissions.HasPermission(PermissionCodes.Users.Create))
        {
            _createAccount.Content = T("إنشاء حساب دخول لهذا الموظف", "Create a login account for this employee");
            _createAccount.Margin = new Thickness(0, 8, 0, 8);
            _createAccount.Checked += (_, _) => _accountPanel.Visibility = Visibility.Visible;
            _createAccount.Unchecked += (_, _) => _accountPanel.Visibility = Visibility.Collapsed;
            form.Children.Add(_createAccount);

            AddField(_accountPanel, "UserName", T("اسم المستخدم *", "Username *"), _userName);
            AddField(_accountPanel, "Password", T("كلمة المرور * (8 أحرف على الأقل)", "Password * (min. 8 characters)"), _password);
            AddField(_accountPanel, "RoleName", T("الدور *", "Role *"), _role);
            form.Children.Add(_accountPanel);
        }

        _save.Content = T("حفظ", "Save");
        _save.IsDefault = true;
        _save.Click += Save_Click;
        var cancel = new Button { Content = T("إلغاء", "Cancel"), Padding = new Thickness(22, 8, 22, 8), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(_save);
        buttons.Children.Add(cancel);
        form.Children.Add(buttons);

        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = form };
        Loaded += async (_, _) => await LoadListsAsync();
    }

    private string T(string arabic, string english) => _arabic ? arabic : english;

    private void AddField(Panel parent, string key, string label, Control control)
    {
        parent.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 2) });
        parent.Children.Add(control);
        var error = new TextBlock { Foreground = ErrorBrush, FontSize = 12, Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        parent.Children.Add(error);
        _fields[key] = (control, error);
    }

    private async System.Threading.Tasks.Task LoadListsAsync()
    {
        try
        {
            _job.ItemsSource = JobTitles.All.Select(j => new Option(j.Key, _arabic ? j.Arabic : j.English)).ToList();
            _branch.ItemsSource = await _service.GetBranchesAsync();
            if (_branch.Items.Count == 1) _branch.SelectedIndex = 0;   // single branch: preselect it
            _role.ItemsSource = await _service.GetAssignableRolesAsync();
        }
        catch (Exception ex)
        {
            ShowBanner(ErrorMessageMapper.ToUserMessage(ex));
        }
    }

    private void UpdateAge()
    {
        if (_birthDate.SelectedDate is not DateTime birth) { _age.Text = string.Empty; return; }
        var today = DateTime.Today;
        int age = today.Year - birth.Year;
        if (today < birth.AddYears(age)) age--;
        _age.Text = age >= 0 ? age.ToString() : string.Empty;
    }

    private static DateOnly? ToDateOnly(DateTime? d) => d is null ? null : DateOnly.FromDateTime(d.Value);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ClearErrors();
        _save.IsEnabled = false;
        try
        {
            AccountRequest? account = _createAccount.IsChecked == true
                ? new AccountRequest(_userName.Text, _password.Password, _role.SelectedItem as string ?? string.Empty)
                : null;

            var request = new CreateEmployeeRequest(
                _code.Text, _arabicName.Text, _englishName.Text, ToDateOnly(_birthDate.SelectedDate), _phone.Text,
                _job.SelectedValue as string ?? string.Empty,
                _branch.SelectedValue as int?, _qualification.Text, _address.Text, ToDateOnly(_hireDate.SelectedDate), account);

            var result = await _service.CreateAsync(request);

            if (!result.Success)
            {
                ShowErrors(result.Errors);
                return;
            }

            MessageBox.Show(T("تمت إضافة الموظف بنجاح", "The employee was added successfully"),
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowBanner(ErrorMessageMapper.ToUserMessage(ex));   // typed data stays on screen
        }
        finally
        {
            _save.IsEnabled = true;
        }
    }

    private void ClearErrors()
    {
        _banner.Visibility = Visibility.Collapsed;
        foreach (var (control, error) in _fields.Values)
        {
            error.Visibility = Visibility.Collapsed;
            control.ClearValue(Control.BorderBrushProperty);
        }
    }

    private void ShowBanner(string text)
    {
        _bannerText.Text = text;
        _banner.Visibility = Visibility.Visible;
    }

    private void ShowErrors(IReadOnlyDictionary<string, string> errors)
    {
        _bannerText.Text = T("يرجى تصحيح الحقول المحددة بالأحمر قبل الحفظ.", "Please correct the fields marked in red before saving.");
        _banner.Visibility = Visibility.Visible;

        foreach (var (field, key) in errors)
        {
            if (!_fields.TryGetValue(field, out var entry)) continue;
            entry.Control.BorderBrush = ErrorBrush;
            entry.Error.Text = Message(key);
            entry.Error.Visibility = Visibility.Visible;
        }
    }

    private string Message(string key) => key switch
    {
        "Required" => T("هذا الحقل مطلوب.", "This field is required."),
        "InvalidCode" => T("الكود يجب أن يكون من 2 إلى 20 حرفًا أو رقمًا (يسمح بـ - و _).", "Code must be 2-20 letters or digits (- and _ allowed)."),
        "DuplicateCode" => T("كود الموظف مستخدم بالفعل، يرجى إدخال كود آخر.", "This employee code is already in use. Enter another code."),
        "ArabicName3Words" => T("أدخل الاسم الثلاثي (ثلاث كلمات على الأقل).", "Enter the full name (at least three words)."),
        "EnglishName3Words" => T("أدخل الاسم الثلاثي (ثلاث كلمات على الأقل).", "Enter the full name (at least three words)."),
        "InvalidCharacters" => T("الاسم يحتوي على حروف غير مسموحة للغة المطلوبة.", "The name contains characters not allowed for this language."),
        "FutureDate" => T("التاريخ لا يمكن أن يكون في المستقبل.", "The date cannot be in the future."),
        "TooYoung" => T("يجب ألا يقل السن عن 16 سنة.", "The employee must be at least 16 years old."),
        "HireBeforeWorkingAge" => T("تاريخ التعيين يسبق بلوغ سن العمل.", "The hire date is before the employee reached working age."),
        "InvalidPhone" => T("رقم الهاتف غير صحيح (8 إلى 15 رقمًا).", "Invalid phone number (8-15 digits)."),
        "InvalidJob" => T("اختر وظيفة من القائمة.", "Choose a job title from the list."),
        "InvalidBranch" => T("الفرع غير صالح أو غير نشط.", "The branch is invalid or inactive."),
        "InvalidUserName" => T("اسم المستخدم من 3 إلى 50 حرفًا (حروف وأرقام و . _ -).", "Username must be 3-50 characters (letters, digits, . _ -)."),
        "DuplicateUserName" => T("اسم المستخدم مستخدم بالفعل.", "This username is already in use."),
        "WeakPassword" => T("كلمة المرور يجب ألا تقل عن 8 أحرف.", "The password must be at least 8 characters."),
        "RoleNotAllowed" => T("لا تملك صلاحية منح هذا الدور.", "You are not allowed to assign this role."),
        _ => T("القيمة غير صحيحة.", "Invalid value.")
    };
}
