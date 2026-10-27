using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Desktop.Views;

public partial class OpenShiftWindow : Window
{
    private readonly OpenCashShiftService _openCashShiftService;
    private readonly ActiveShiftService _activeShiftService;
    private readonly CurrentUserSession _session;
    private readonly PermissionChecker _permissions;
    private readonly IPosTerminalRepository _terminalRepository;

    public OpenShiftWindow(
        OpenCashShiftService openCashShiftService,
        ActiveShiftService activeShiftService,
        CurrentUserSession session,
        PermissionChecker permissions,
        IPosTerminalRepository terminalRepository)
    {
        InitializeComponent();
        _openCashShiftService = openCashShiftService;
        _activeShiftService = activeShiftService;
        _session = session;
        _permissions = permissions;
        _terminalRepository = terminalRepository;
        Loaded += OpenShiftWindow_Loaded;
    }

    private async void OpenShiftWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_session.IsAuthenticated)
        {
            ReturnToLogin();
            return;
        }

        try
        {
            // The user may already have an open shift (e.g. after signing out without closing it).
            // Resume it instead of asking for a new opening float, so shifts are never duplicated.
            if (await _activeShiftService.RestoreAsync() is not null)
            {
                GoToMain();
                return;
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ErrorMessageMapper.ToUserMessage(ex);
            return;
        }

        // Accounts that cannot open shifts do not belong on this screen.
        if (!_permissions.HasPermission(PermissionCodes.Shifts.Open))
            GoToMain();
    }

    private async void OpenShiftButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (!_session.IsAuthenticated || !_session.UserId.HasValue)
        {
            ErrorText.Text = LocalizationManager.Current["ShiftRequired"];
            return;
        }

        if (!_permissions.HasPermission(PermissionCodes.Shifts.Open))
        {
            ErrorText.Text = LocalizationManager.Current["PermissionDenied"];
            return;
        }

        if (!decimal.TryParse(OpeningFloatTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal openingFloat)
            || openingFloat < 0m || decimal.Round(openingFloat, 2) != openingFloat)
        {
            ErrorText.Text = LocalizationManager.Current["OpeningFloatInvalid"];
            return;
        }

        OpenShiftButton.IsEnabled = false;
        try
        {
            var terminal = await _terminalRepository.GetByCodeAsync(ShiftFlow.DefaultTerminalCode);
            if (terminal is null || !terminal.IsActive)
            {
                ErrorText.Text = LocalizationManager.Current["TerminalMissing"];
                return;
            }

            int shiftId = await _openCashShiftService.ExecuteAsync(_session.UserId.Value, terminal.Id, openingFloat);
            _session.SetActiveShift(shiftId, terminal.Id);
            GoToMain();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ErrorMessageMapper.ToUserMessage(ex);
        }
        finally
        {
            OpenShiftButton.IsEnabled = true;
        }
    }

    private void GoToMain()
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        System.Windows.Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }

    private void ReturnToLogin()
    {
        var loginWindow = App.Services.GetRequiredService<LoginWindow>();
        System.Windows.Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }
}
