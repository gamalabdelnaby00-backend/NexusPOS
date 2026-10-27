using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Security;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop.Views;

/// <summary>Cash count and close dialog. The expected amount is shown only to users allowed to see it (blind count otherwise).</summary>
public sealed class CloseShiftWindow : Window
{
    private readonly CloseCashShiftService _service;
    private readonly int _shiftId;
    private readonly TextBox _counted = new() { MinWidth = 220 };
    private readonly TextBox _notes = new() { MinWidth = 220, Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly Button _closeButton = new() { Padding = new Thickness(18, 6, 18, 6), Margin = new Thickness(0, 0, 8, 0) };

    public CloseShiftWindow(CloseCashShiftService service, PermissionChecker permissions, int shiftId)
    {
        _service = service;
        _shiftId = shiftId;
        var text = LocalizationManager.Current;

        Title = text["CloseShiftTitle"];
        FlowDirection = text.FlowDirection;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var panel = new StackPanel { Margin = new Thickness(24), MinWidth = 320 };

        if (permissions.HasPermission(PermissionCodes.Shifts.ViewExpectedCash))
        {
            panel.Children.Add(new TextBlock { Text = text["ExpectedCash"], FontWeight = FontWeights.SemiBold });
            _expectedText = new TextBlock { Margin = new Thickness(0, 2, 0, 12), FontSize = 18 };
            panel.Children.Add(_expectedText);
        }

        panel.Children.Add(new TextBlock { Text = text["CountedCash"], FontWeight = FontWeights.SemiBold });
        _counted.Margin = new Thickness(0, 2, 0, 12);
        panel.Children.Add(_counted);

        panel.Children.Add(new TextBlock { Text = text["Notes"], FontWeight = FontWeights.SemiBold });
        _notes.Margin = new Thickness(0, 2, 0, 12);
        panel.Children.Add(_notes);

        _error.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(_error);

        _closeButton.Content = text["CloseShift"];
        _closeButton.Click += CloseButton_Click;
        var cancel = new Button { Content = text["Cancel"], Padding = new Thickness(18, 6, 18, 6), IsCancel = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(_closeButton);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        Content = panel;
        Loaded += async (_, _) =>
        {
            if (_expectedText is null) return;
            try
            {
                _expectedText.Text = (await _service.GetExpectedCashAsync(_shiftId)).ToString("N2", CultureInfo.CurrentCulture);
            }
            catch (Exception ex)
            {
                _error.Text = ErrorMessageMapper.ToUserMessage(ex);
            }
        };
    }

    private readonly TextBlock? _expectedText;

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var text = LocalizationManager.Current;
        _error.Text = string.Empty;

        if (!decimal.TryParse(_counted.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal counted)
            || counted < 0m || decimal.Round(counted, 2) != counted)
        {
            _error.Text = text["OpeningFloatInvalid"];
            return;
        }

        _closeButton.IsEnabled = false;
        try
        {
            var result = await _service.ExecuteAsync(_shiftId, counted, _notes.Text);
            string message = result.Status == CashShiftStatus.PendingApproval
                ? $"{text["ShiftClosedDiff"]}\n{text["CashDifference"]}: {result.Difference.ToString("N2", CultureInfo.CurrentCulture)}"
                : text["ShiftClosedOk"];

            MessageBox.Show(message, text["CloseShiftTitle"], MessageBoxButton.OK,
                result.Status == CashShiftStatus.PendingApproval ? MessageBoxImage.Warning : MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            _error.Text = ErrorMessageMapper.ToUserMessage(ex);
            _closeButton.IsEnabled = true;
        }
    }
}
