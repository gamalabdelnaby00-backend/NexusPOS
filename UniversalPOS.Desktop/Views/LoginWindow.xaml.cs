using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Infrastructure.Data;

namespace UniversalPOS.Desktop.Views;

public partial class LoginWindow : Window
{
    private sealed record LoginPreferences(string UserName);

    private static readonly string PreferencesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusPOS", "login-preferences.json");

    private readonly LoginService _loginService;
    private readonly CurrentUserSession _session;
    private readonly System.ComponentModel.PropertyChangedEventHandler _languageChanged;
    private bool _isLoggingIn;
    private bool _syncingPassword;

    public LoginWindow(
        LoginService loginService,
        CurrentUserSession session)
    {
        InitializeComponent();

        _loginService = loginService;
        _session = session;

        // Make the culture match the saved language before anything is drawn, so the first paint is already correct.
        LocalizationManager.Current.SetLanguage(AppearanceManager.Current.Language);

        ApplyTexts();
        _languageChanged = (_, _) => ApplyTexts();
        LocalizationManager.Current.PropertyChanged += _languageChanged;
        Closed += (_, _) => LocalizationManager.Current.PropertyChanged -= _languageChanged;

        bool remembered = LoadRememberedUser();

        Loaded += (_, _) =>
        {
            AppearanceManager.Current.ApplyToOpenWindows();
            if (remembered) PasswordInput.Focus();
            else UserNameTextBox.Focus();
        };
    }

    // The saved preference is the source of truth for the language (the thread culture follows it).
    private static bool IsArabic =>
        AppearanceManager.Current.Language.Equals("ar", StringComparison.OrdinalIgnoreCase);

    private static string T(string arabic, string english) => IsArabic ? arabic : english;

    private void ApplyTexts()
    {
        Title = T("NexusPOS - تسجيل الدخول", "NexusPOS - Sign in");
        WelcomeText.Text = T("مرحبًا بك مجددًا في NexusPOS!", "Welcome back to NexusPOS!");
        UserNameHint.Text = T("اسم المستخدم أو البريد الإلكتروني", "Username or email");
        PasswordHint.Text = T("كلمة المرور", "Password");
        RememberCheckBox.Content = T("تذكرني", "Remember me");
        ForgotPasswordText.Text = T("نسيت كلمة المرور؟", "Forgot your password?");
        TaglineText.Text = T("إدارة ذكية.. لمبيعات أكثر", "Smart management.. for more sales");
        TogglePasswordButton.ToolTip = T("إظهار / إخفاء كلمة المرور", "Show / hide password");
        if (!_isLoggingIn) LoginButton.Content = T("تسجيل الدخول", "Sign in");

        // Only the card and the language menu follow the reading direction; the frame around them stays fixed.
        var direction = IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LoginCard.FlowDirection = direction;
        LanguageMenu.FlowDirection = direction;

        LanguageNameText.Text = IsArabic ? "العربية" : "English";
        ArabicCheck.Visibility = IsArabic ? Visibility.Visible : Visibility.Hidden;
        EnglishCheck.Visibility = IsArabic ? Visibility.Hidden : Visibility.Visible;
    }

    // ------------------------------------------------------------------ language

    private void LanguageButton_Click(object sender, RoutedEventArgs e) =>
        LanguagePopup.IsOpen = !LanguagePopup.IsOpen;

    private void LanguageOption_Click(object sender, RoutedEventArgs e)
    {
        LanguagePopup.IsOpen = false;

        if (sender is Button button && button.Tag is string languageCode)
            AppearanceManager.Current.SetLanguage(languageCode);
    }

    // ------------------------------------------------------------------ fields

    private string GetPassword() =>
        PasswordVisibleBox.Visibility == Visibility.Visible ? PasswordVisibleBox.Text : PasswordInput.Password;

    private void ClearPassword()
    {
        _syncingPassword = true;
        PasswordInput.Clear();
        PasswordVisibleBox.Clear();
        _syncingPassword = false;
        PasswordHint.Visibility = Visibility.Visible;
    }

    private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UserNameHint.Visibility = string.IsNullOrEmpty(UserNameTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        PasswordHint.Visibility = string.IsNullOrEmpty(GetPassword()) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PasswordVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingPassword) return;
        PasswordHint.Visibility = string.IsNullOrEmpty(GetPassword()) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
    {
        _syncingPassword = true;
        try
        {
            if (PasswordVisibleBox.Visibility != Visibility.Visible)
            {
                PasswordVisibleBox.Text = PasswordInput.Password;
                PasswordVisibleBox.Visibility = Visibility.Visible;
                PasswordInput.Visibility = Visibility.Collapsed;
                EyeIcon.Data = (Geometry)FindResource("LIcon.Eye");
                PasswordVisibleBox.Focus();
                PasswordVisibleBox.CaretIndex = PasswordVisibleBox.Text.Length;
            }
            else
            {
                PasswordInput.Password = PasswordVisibleBox.Text;
                PasswordVisibleBox.Visibility = Visibility.Collapsed;
                PasswordInput.Visibility = Visibility.Visible;
                EyeIcon.Data = (Geometry)FindResource("LIcon.EyeOff");
                PasswordInput.Focus();
            }
        }
        finally
        {
            _syncingPassword = false;
        }
    }

    private void ForgotPassword_Click(object sender, MouseButtonEventArgs e)
    {
        // There is no self-service reset: passwords are changed by an administrator.
        MessageBox.Show(
            T("لإعادة تعيين كلمة المرور، تواصل مع مدير النظام.", "To reset your password, please contact the system administrator."),
            T("نسيت كلمة المرور؟", "Forgot your password?"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ------------------------------------------------------------------ remember me (user name only, never the password)

    private bool LoadRememberedUser()
    {
        try
        {
            if (!File.Exists(PreferencesPath)) return false;

            var preferences = JsonSerializer.Deserialize<LoginPreferences>(File.ReadAllText(PreferencesPath));
            if (preferences is null || string.IsNullOrWhiteSpace(preferences.UserName)) return false;

            UserNameTextBox.Text = preferences.UserName;
            RememberCheckBox.IsChecked = true;
            return true;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not read the saved login preferences: {0}", ex.Message);
            return false;
        }
    }

    private void SaveOrClearRememberedUser(string userName)
    {
        try
        {
            if (RememberCheckBox.IsChecked == true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
                File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(new LoginPreferences(userName)));
            }
            else if (File.Exists(PreferencesPath))
            {
                File.Delete(PreferencesPath);
            }
        }
        catch (Exception ex)
        {
            // Failing to remember the user must never block the sign-in.
            Trace.TraceWarning("Could not save the login preferences: {0}", ex.Message);
        }
    }

    // ------------------------------------------------------------------ sign in

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLoggingIn)
            return;

        ClearError();

        string typedUserName = UserNameTextBox.Text.Trim();
        string password = GetPassword();

        if (string.IsNullOrWhiteSpace(typedUserName))
        {
            ShowError("من فضلك أدخل اسم المستخدم أو البريد الإلكتروني.", "Please enter your username or email.");
            UserNameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("من فضلك أدخل كلمة المرور.", "Please enter your password.");
            if (PasswordVisibleBox.Visibility == Visibility.Visible) PasswordVisibleBox.Focus();
            else PasswordInput.Focus();
            return;
        }

        _isLoggingIn = true;
        LoginButton.IsEnabled = false;
        LoginButton.Content = T("جارٍ تسجيل الدخول...", "Signing in...");
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            // Wait for the application and database initialisation when needed.
            if (!App.StartupInitializationTask.IsCompletedSuccessfully)
            {
                ShowError("جارٍ تجهيز النظام لأول تشغيل...", "Preparing the system for first use...");

                await App.StartupInitializationTask;
                ClearError();
            }

            // An e-mail address is resolved to the user name of the account linked to that employee.
            string userName = typedUserName;
            if (typedUserName.Contains('@'))
                userName = await ResolveUserNameByEmailAsync(typedUserName) ?? typedUserName;

            bool success = await _loginService.ExecuteAsync(userName, password);

            if (!success)
            {
                ShowError(LocalizationManager.Current["InvalidCredentials"], "Invalid username or password.");

                ClearPassword();
                if (PasswordVisibleBox.Visibility == Visibility.Visible) PasswordVisibleBox.Focus();
                else PasswordInput.Focus();
                return;
            }

            SaveOrClearRememberedUser(typedUserName);

            // Pick the first screen from the user's role and permissions.
            Window nextWindow;

            switch (StartScreenResolver.Resolve(_session))
            {
                case StartScreen.OpenShift:
                    nextWindow = App.Services.GetRequiredService<OpenShiftWindow>();
                    break;

                case StartScreen.Main:
                    nextWindow = App.Services.GetRequiredService<MainWindow>();
                    break;

                default:
                    _session.SignOut();

                    ShowError(
                        "لا توجد شاشة بداية مناسبة لصلاحيات هذا المستخدم.",
                        "No supported start screen is available for this user.");

                    ClearPassword();
                    return;
            }

            // Show the next window only after a successful sign-in.
            System.Windows.Application.Current.MainWindow = nextWindow;
            nextWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            Trace.TraceError("Login failed: {0}", ex);

            ShowError(
                "تعذر تسجيل الدخول. تحقق من اتصال قاعدة البيانات وحاول مرة أخرى.",
                "Login failed. Check the database connection and try again.");
        }
        finally
        {
            Mouse.OverrideCursor = null;
            _isLoggingIn = false;

            if (IsVisible)
            {
                LoginButton.Content = T("تسجيل الدخول", "Sign in");
                LoginButton.IsEnabled = true;
            }
        }
    }

    private static async Task<string?> ResolveUserNameByEmailAsync(string email)
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await (from user in db.Users.AsNoTracking()
                      join employee in db.Employees.AsNoTracking() on user.EmployeeId equals (int?)employee.Id
                      where employee.Email == email
                      select user.UserName).FirstOrDefaultAsync();
    }

    private void ShowError(string arabicMessage, string englishMessage)
    {
        ErrorText.Text = IsArabic ? arabicMessage : englishMessage;
        ErrorBorder.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorText.Text = string.Empty;
        ErrorBorder.Visibility = Visibility.Collapsed;
    }
}
