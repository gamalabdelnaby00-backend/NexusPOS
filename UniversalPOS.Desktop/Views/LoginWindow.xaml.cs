
using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Authentication;

namespace UniversalPOS.Desktop.Views;

public partial class LoginWindow : Window
{
    private readonly LoginService _loginService;
    private readonly CurrentUserSession _session;

    public LoginWindow(
        LoginService loginService,
        CurrentUserSession session)
    {
        InitializeComponent();
        LanguageSelector.SelectedIndex = LocalizationManager.Current.LanguageCode.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _loginService = loginService;
        _session = session;
    }

    private void LanguageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string languageCode)
            LocalizationManager.Current.SetLanguage(languageCode);
    }

    private async void LoginButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        LoginButton.IsEnabled = false;

        try
        {
            bool success = await _loginService.ExecuteAsync(
                UserNameTextBox.Text,
                PasswordInput.Password);

            if (!success)
            {
                ErrorText.Text = LocalizationManager.Current["InvalidCredentials"];

                PasswordInput.Clear();
                PasswordInput.Focus();
                return;
            }

            Window nextWindow;

            switch (StartScreenResolver.Resolve(_session))
            {
                case StartScreen.OpenShift:
                    // Cashiers restore or open their shift first.
                    nextWindow = App.Services.GetRequiredService<OpenShiftWindow>();
                    break;

                case StartScreen.Main:
                    nextWindow = App.Services.GetRequiredService<MainWindow>();
                    break;

                default:
                    ErrorText.Text = LocalizationManager.Current["UnsupportedRole"];
                    _session.SignOut();
                    PasswordInput.Clear();
                    return;
            }

            System.Windows.Application.Current.MainWindow = nextWindow;
            nextWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Login failed: {ex.Message}",
                "UniversalPOS",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
