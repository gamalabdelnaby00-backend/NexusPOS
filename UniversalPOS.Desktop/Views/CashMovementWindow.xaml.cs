
using System;
using System.Globalization;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Desktop.Views;

/// <summary>
/// Records a manual cash movement against the selected shift.
/// </summary>
public sealed partial class CashMovementWindow : Window
{
    private readonly int _shiftId;
    private readonly bool _arabic;
    private bool _saving;

    public CashMovementWindow(int shiftId, bool arabic)
    {
        InitializeComponent();

        _shiftId = shiftId;
        _arabic = arabic;

        Title = T("تسجيل حركة نقدية", "Register cash movement");
        FlowDirection = arabic
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        HeaderTitle.Text = T("تسجيل حركة نقدية", "Register cash movement");
        HeaderSubtitle.Text = T(
            "إضافة إيداع أو سحب أو مصروف أو توريد للخزنة",
            "Record a deposit, withdrawal, expense, or safe drop");

        MovementTypeLabel.Text = T("نوع الحركة", "Movement type");
        AmountLabel.Text = T("المبلغ (جنيه مصري)", "Amount (EGP)");
        ReasonLabel.Text = T(
            "السبب / الملاحظات (إجباري)",
            "Reason / notes (required)");

        SaveButton.Content = T("حفظ الحركة", "Save movement");
        CancelButton.Content = T("إلغاء", "Cancel");

        MovementTypeCombo.Items.Add(new MovementOption(
            T("إيداع نقدية", "Cash in"),
            CashMovementType.CashIn,
            true));

        MovementTypeCombo.Items.Add(new MovementOption(
            T("سحب نقدية", "Cash out"),
            CashMovementType.CashOut,
            false));

        MovementTypeCombo.Items.Add(new MovementOption(
            T("مصروف", "Expense"),
            CashMovementType.Expense,
            false));

        MovementTypeCombo.Items.Add(new MovementOption(
            T("توريد للخزنة", "Safe drop"),
            CashMovementType.SafeDrop,
            false));

        MovementTypeCombo.SelectedIndex = 0;

        Loaded += (_, _) => AmountTextBox.Focus();
    }

    private string T(string arabic, string english) =>
        _arabic ? arabic : english;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_saving)
            return;

        HideError();

        if (MovementTypeCombo.SelectedItem is not MovementOption option)
        {
            ShowError(T("اختر نوع الحركة.", "Select a movement type."));
            return;
        }

        if (!TryParseAmount(AmountTextBox.Text, out decimal amount) ||
            amount <= 0m ||
            decimal.Round(amount, 2) != amount)
        {
            ShowError(T(
                "أدخل مبلغًا موجبًا حتى منزلتين عشريتين.",
                "Enter a positive amount with at most two decimal places."));

            AmountTextBox.Focus();
            AmountTextBox.SelectAll();
            return;
        }

        string reason = ReasonTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(reason))
        {
            ShowError(T(
                "اكتب سبب الحركة.",
                "Enter a reason for this movement."));

            ReasonTextBox.Focus();
            return;
        }

        _saving = true;
        SaveButton.IsEnabled = false;
        SaveButton.Content = T("جارٍ الحفظ...", "Saving...");

        try
        {
            var session =
                App.Services.GetRequiredService<CurrentUserSession>();

            if (session.UserId is null)
            {
                ShowError(T(
                    "يجب تسجيل الدخول أولًا.",
                    "Sign in first."));

                return;
            }

            var service =
                App.Services.GetRequiredService<RegisterCashMovementService>();

            await service.ExecuteAsync(
                _shiftId,
                option.Type,
                amount,
                reason,
                option.IncreasesDrawer);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            ShowError(ErrorMessageMapper.ToUserMessage(ex));
        }
        finally
        {
            _saving = false;
            SaveButton.IsEnabled = true;
            SaveButton.Content = T("حفظ الحركة", "Save movement");
        }
    }

    private static bool TryParseAmount(
        string? text,
        out decimal amount)
    {
        if (decimal.TryParse(
                text?.Trim(),
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out amount))
        {
            return true;
        }

        return decimal.TryParse(
            text?.Trim(),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorTextBlock.Text = string.Empty;
        ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record MovementOption(
        string Label,
        CashMovementType Type,
        bool IncreasesDrawer);
}
