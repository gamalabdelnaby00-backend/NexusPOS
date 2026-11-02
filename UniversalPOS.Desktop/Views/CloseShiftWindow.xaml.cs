
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop.Views;

public partial class CloseShiftWindow : Window
{
    private readonly CloseCashShiftService _service;
    private readonly int _shiftId;
    private readonly bool _canViewExpectedCash;

    public CloseShiftWindow(
        CloseCashShiftService service,
        PermissionChecker permissions,
        int shiftId)
    {
        InitializeComponent();

        _service = service ?? throw new ArgumentNullException(nameof(service));
        ArgumentNullException.ThrowIfNull(permissions);

        _shiftId = shiftId;

        _canViewExpectedCash =
            permissions.HasPermission(PermissionCodes.Shifts.ViewExpectedCash);

        var text = LocalizationManager.Current;

        Title = text["CloseShiftTitle"];
        TitleText.Text = text["CloseShiftTitle"];
        CountedCashLabel.Text = text["CountedCash"];
        NotesLabel.Text = text["Notes"];
        CloseShiftButton.Content = text["CloseShift"];
        CancelButton.Content = text["Cancel"];

        FlowDirection = text.FlowDirection;

        ExpectedCashPanel.Visibility = _canViewExpectedCash
            ? Visibility.Visible
            : Visibility.Collapsed;

        ExpectedCashLabel.Text = text["ExpectedCash"];
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_canViewExpectedCash)
            return;

        CloseShiftButton.IsEnabled = false;

        try
        {
            decimal expected =
                await _service.GetExpectedCashAsync(_shiftId);

            ExpectedCashText.Text =
                expected.ToString("N2", CultureInfo.CurrentCulture);
        }
        catch (Exception ex)
        {
            ShowError(ErrorMessageMapper.ToUserMessage(ex));
        }
        finally
        {
            CloseShiftButton.IsEnabled = true;
        }
    }

    private async void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        HideError();

        var text = LocalizationManager.Current;

        if (!decimal.TryParse(
                CountedCashInput.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal counted)
            || counted < 0m
            || decimal.Round(counted, 2) != counted)
        {
            ShowError(text["OpeningFloatInvalid"]);
            CountedCashInput.Focus();
            return;
        }

        CloseShiftButton.IsEnabled = false;
        CancelButton.IsEnabled = false;

        try
        {
            var result = await _service.ExecuteAsync(
                _shiftId,
                counted,
                NotesInput.Text);

            string message =
                result.Status == CashShiftStatus.PendingApproval
                    ? $"{text["ShiftClosedDiff"]}\n" +
                      $"{text["CashDifference"]}: " +
                      result.Difference.ToString(
                          "N2",
                          CultureInfo.CurrentCulture)
                    : text["ShiftClosedOk"];

            MessageBox.Show(
                this,
                message,
                text["CloseShiftTitle"],
                MessageBoxButton.OK,
                result.Status == CashShiftStatus.PendingApproval
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ErrorMessageMapper.ToUserMessage(ex));
        }
        finally
        {
            CloseShiftButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorText.Text = string.Empty;
        ErrorPanel.Visibility = Visibility.Collapsed;
    }
}
