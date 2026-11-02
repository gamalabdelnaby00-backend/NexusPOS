using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using UniversalPOS.Application.Employees;
using UniversalPOS.Application.Security;
using UniversalPOS.Desktop.UI;

namespace UniversalPOS.Desktop.Views;

/// <summary>
/// Employee master-data entry (and edit when an employee code is passed). Permissions are granted from the
/// Roles screen: an employee without a role (or without a login account) has no access to the program.
/// </summary>
public sealed partial class AddEmployeeWindow : Window
{
    private sealed record Option(string Key, string Text);

    private readonly Dictionary<string, (Control Control, TextBlock Error)> _fields = new();

    private readonly EmployeeService _service;
    private readonly bool _arabic;
    private readonly string? _editCode;
    private string? _oldPhotoKey;
    private string? _pickedPhotoPath;

    public AddEmployeeWindow(EmployeeService service, PermissionChecker permissions, string? editCode = null)
    {
        _service = service;
        _editCode = editCode;
        _arabic = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
        InitializeComponent();
        FlowDirection = LocalizationManager.Current.FlowDirection;
        ApplyTexts();

        RegisterField("FullName", FullNameTextBox, FullNameError);
        RegisterField("Phone", PhoneTextBox, PhoneError);
        RegisterField("Email", EmailTextBox, EmailError);
        RegisterField("JobTitle", JobComboBox, JobError);
        RegisterField("BranchId", BranchComboBox, BranchError);
        RegisterField("HireDate", HireDatePicker, HireDateError);
        RegisterField("DepartmentId", DepartmentComboBox, DepartmentError);

        BindHint(FullNameTextBox, FullNameHint);
        BindHint(PhoneTextBox, PhoneHint);
        BindHint(EmailTextBox, EmailHint);
        JobComboBox.SelectionChanged += (_, _) => JobHint.Visibility = JobComboBox.SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;
        BranchComboBox.SelectionChanged += (_, _) => BranchHint.Visibility = BranchComboBox.SelectedItem is null ? Visibility.Visible : Visibility.Collapsed;

        HireDatePicker.DisplayDateEnd = DateTime.Today;
        HireDatePicker.SelectedDate = DateTime.Today;

        PhotoButton.Click += PhotoButton_Click;
        SaveButton.Click += Save_Click;
        CancelButton.Click += (_, _) => DialogResult = false;
        Loaded += async (_, _) => await LoadListsAsync();
    }

    private string T(string arabic, string english) => _arabic ? arabic : english;

    private void ApplyTexts()
    {
        bool edit = _editCode is not null;
        Title = edit ? T("تعديل موظف | NexusPOS", "Edit employee | NexusPOS") : T("إضافة موظف | NexusPOS", "Add employee | NexusPOS");
        HeaderTitle.Text = edit ? T("تعديل بيانات الموظف", "Edit employee") : T("إضافة موظف جديد", "Add new employee");
        BasicTitle.Text = T("البيانات الأساسية", "Basic data");
        ExtraTitle.Text = T("بيانات إضافية", "Additional data");
        FullNameLabel.Text = T("الاسم الكامل *", "Full name *");
        PhoneLabel.Text = T("رقم الهاتف *", "Phone number *");
        EmailLabel.Text = T("البريد الإلكتروني *", "Email *");
        JobLabel.Text = T("الوظيفة *", "Job title *");
        BranchLabel.Text = T("الفرع *", "Branch *");
        HireDateLabel.Text = T("تاريخ التعيين *", "Hire date *");
        DepartmentLabel.Text = T("القسم", "Department");
        PhotoLabel.Text = T("الصورة الشخصية", "Profile photo");
        FullNameHint.Text = T("ادخل الاسم الكامل", "Enter the full name");
        PhoneHint.Text = T("ادخل رقم الهاتف", "Enter the phone number");
        EmailHint.Text = T("ادخل البريد الإلكتروني", "Enter the email address");
        JobHint.Text = T("اختر الوظيفة", "Choose the job title");
        BranchHint.Text = T("اختر الفرع", "Choose the branch");
        PhotoButton.Content = UiKit.IconText("Image", T("اختر صورة", "Choose photo"), Brushes.White, 20);
        SaveButton.Content = UiKit.IconText("Save", T("حفظ", "Save"), Brushes.White, 20);
        CancelButton.Content = UiKit.IconText("Cancel", T("إلغاء", "Cancel"), UiKit.Ink, 20);
        PermissionsNote.Text = T(
            "الصلاحيات تُمنح من شاشة الأدوار. الموظف الذي لا يُمنح دورًا أو حساب دخول لا يمكنه الدخول إلى البرنامج.",
            "Permissions are granted from the Roles screen. An employee without a role or login account has no access to the program.");
        BannerText.Text = T("يرجى استكمال الحقول المطلوبة وتصحيح الحقول المحددة.", "Please complete the required fields and fix the highlighted ones.");
    }

    private static void BindHint(TextBox box, TextBlock hint) =>
        box.TextChanged += (_, _) => hint.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;

    private void RegisterField(string key, Control control, TextBlock error) => _fields[key] = (control, error);

    private async System.Threading.Tasks.Task LoadListsAsync()
    {
        try
        {
            JobComboBox.ItemsSource = JobTitles.All.Select(j => new Option(j.Key, _arabic ? j.Arabic : j.English)).ToList();
            BranchComboBox.ItemsSource = await _service.GetBranchesAsync();
            if (BranchComboBox.Items.Count == 1) BranchComboBox.SelectedIndex = 0;   // single branch: preselect it

            var departments = new List<DepartmentOption> { new(0, T("— بدون قسم —", "— No department —")) };
            departments.AddRange(await _service.GetDepartmentsAsync());
            DepartmentComboBox.ItemsSource = departments;
            DepartmentComboBox.SelectedIndex = 0;

            if (_editCode is not null) await LoadEmployeeAsync();
        }
        catch (Exception ex)
        {
            ShowBanner(ErrorMessageMapper.ToUserMessage(ex));
        }
    }

    private async System.Threading.Tasks.Task LoadEmployeeAsync()
    {
        var employee = await _service.GetByCodeAsync(_editCode!);
        if (employee is null) { ShowBanner(T("الموظف غير موجود.", "The employee was not found.")); SaveButton.IsEnabled = false; return; }

        FullNameTextBox.Text = employee.FullName;
        PhoneTextBox.Text = employee.Phone;
        EmailTextBox.Text = employee.Email;
        JobComboBox.SelectedValue = employee.JobTitle;
        BranchComboBox.SelectedValue = employee.BranchId;
        DepartmentComboBox.SelectedValue = employee.DepartmentId ?? 0;
        HireDatePicker.SelectedDate = employee.HireDate.ToDateTime(TimeOnly.MinValue);
        _oldPhotoKey = employee.PhotoKey;
        if (!string.IsNullOrWhiteSpace(employee.PhotoKey))
            ShowPhoto(EmployeePhotoStore.PathFor(employee.PhotoKey));
    }

    private void ShowPhoto(string path)
    {
        var bitmap = UiKit.LoadBitmap(path, 260);
        if (bitmap is null) return;
        PhotoImage.Source = bitmap;
        PhotoPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void PhotoButton_Click(object sender, RoutedEventArgs e)
    {
        PhotoError.Visibility = Visibility.Collapsed;
        var dialog = new OpenFileDialog
        {
            Title = T("اختيار الصورة الشخصية", "Choose profile photo"),
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var info = new FileInfo(dialog.FileName);
            if (info.Length > EmployeePhotoStore.MaxBytes)
            {
                ShowPhotoError(T("حجم الصورة يتجاوز 5 ميجابايت.", "The image exceeds 5 MB."));
                return;
            }

            var bitmap = UiKit.LoadBitmap(dialog.FileName, 260);
            if (bitmap is null)
            {
                ShowPhotoError(T("تعذر قراءة الصورة المحددة.", "The selected image could not be read."));
                return;
            }
            PhotoImage.Source = bitmap;
            PhotoPlaceholder.Visibility = Visibility.Collapsed;
            _pickedPhotoPath = dialog.FileName;
        }
        catch (Exception)
        {
            ShowPhotoError(T("تعذر قراءة الصورة المحددة.", "The selected image could not be read."));
        }
    }

    private void ShowPhotoError(string text)
    {
        PhotoError.Text = text;
        PhotoError.Visibility = Visibility.Visible;
    }

    private static DateOnly? ToDateOnly(DateTime? d) => d is null ? null : DateOnly.FromDateTime(d.Value);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ClearErrors();
        SaveButton.IsEnabled = false;
        string? photoKey = null;
        try
        {
            if (_pickedPhotoPath is not null) photoKey = EmployeePhotoStore.Save(_pickedPhotoPath);

            int? departmentId = DepartmentComboBox.SelectedValue is int d && d > 0 ? d : null;
            // On create the code is generated by the service (EMP-0001, ...); on edit it stays unchanged.
            var request = new CreateEmployeeRequest(
                _editCode ?? string.Empty, FullNameTextBox.Text, PhoneTextBox.Text, EmailTextBox.Text,
                JobComboBox.SelectedValue as string ?? string.Empty,
                BranchComboBox.SelectedValue as int?, ToDateOnly(HireDatePicker.SelectedDate), photoKey, null, departmentId);

            var result = _editCode is null
                ? await _service.CreateAsync(request)
                : await _service.UpdateAsync(_editCode, request);

            if (!result.Success)
            {
                EmployeePhotoStore.Delete(photoKey);
                ShowErrors(result.Errors);
                return;
            }

            if (photoKey is not null && _oldPhotoKey is not null) EmployeePhotoStore.Delete(_oldPhotoKey);

            MessageBox.Show(
                _editCode is null ? T("تمت إضافة الموظف بنجاح", "The employee was added successfully")
                                  : T("تم حفظ تعديلات الموظف", "The employee changes were saved"),
                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            EmployeePhotoStore.Delete(photoKey);
            ShowBanner(ErrorMessageMapper.ToUserMessage(ex));   // typed data stays on screen
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ClearErrors()
    {
        Banner.Visibility = Visibility.Collapsed;
        PhotoError.Visibility = Visibility.Collapsed;
        foreach (var (control, error) in _fields.Values)
        {
            error.Visibility = Visibility.Collapsed;
            control.ClearValue(Control.BorderBrushProperty);
        }
    }

    private void ShowBanner(string text)
    {
        BannerText.Text = text;
        Banner.Visibility = Visibility.Visible;
    }

    private void ShowErrors(IReadOnlyDictionary<string, string> errors)
    {
        BannerText.Text = T("يرجى تصحيح الحقول المحددة بالأحمر قبل الحفظ.", "Please correct the fields marked in red before saving.");
        Banner.Visibility = Visibility.Visible;
        var errorBrush = (Brush)FindResource("ErrorBrush");

        foreach (var (field, key) in errors)
        {
            if (!_fields.TryGetValue(field, out var entry)) continue;
            entry.Error.Text = Message(key);
            entry.Error.Visibility = Visibility.Visible;
            entry.Control.BorderBrush = errorBrush;
        }
    }

    private string Message(string key) => key switch
    {
        "Required" => T("هذا الحقل مطلوب.", "This field is required."),
        "InvalidCode" => T("الكود يجب أن يكون من 2 إلى 20 حرفًا أو رقمًا (يسمح بـ - و _).", "Code must be 2-20 letters or digits (- and _ allowed)."),
        "DuplicateCode" => T("تعذر إنشاء كود الموظف، حاول الحفظ مرة أخرى.", "The employee code could not be generated. Try saving again."),
        "FullNameWords" => T("أدخل الاسم الكامل (كلمتان على الأقل).", "Enter the full name (at least two words)."),
        "InvalidCharacters" => T("الاسم يجب أن يحتوي على حروف فقط.", "The name must contain letters only."),
        "FutureDate" => T("التاريخ لا يمكن أن يكون في المستقبل.", "The date cannot be in the future."),
        "InvalidPhone" => T("رقم الهاتف غير صحيح (8 إلى 15 رقمًا).", "Invalid phone number (8-15 digits)."),
        "InvalidEmail" => T("البريد الإلكتروني غير صحيح.", "Invalid email address."),
        "InvalidJob" => T("اختر وظيفة من القائمة.", "Choose a job title from the list."),
        "InvalidBranch" => T("الفرع غير صالح أو غير نشط.", "The branch is invalid or inactive."),
        "InvalidDepartment" => T("القسم غير صالح أو غير نشط.", "The department is invalid or inactive."),
        "InvalidUserName" => T("اسم المستخدم من 3 إلى 50 حرفًا (حروف وأرقام و . _ -).", "Username must be 3-50 characters (letters, digits, . _ -)."),
        "DuplicateUserName" => T("اسم المستخدم مستخدم بالفعل.", "This username is already in use."),
        "WeakPassword" => T("كلمة المرور يجب ألا تقل عن 8 أحرف.", "The password must be at least 8 characters."),
        "RoleNotAllowed" => T("لا تملك صلاحية منح هذا الدور.", "You are not allowed to assign this role."),
        _ => T("القيمة غير صحيحة.", "Invalid value.")
    };
}
