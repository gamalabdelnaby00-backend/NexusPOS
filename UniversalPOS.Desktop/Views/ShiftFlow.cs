using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using UniversalPOS.Application.Abstractions.Repositories;
using UniversalPOS.Application.Authentication;
using UniversalPOS.Application.CashManagement;
using UniversalPOS.Application.Security;

namespace UniversalPOS.Desktop.Views;

/// <summary>
/// Shared "make sure the signed-in user has a valid open shift" flow used by the sales screen.
/// It restores an existing shift; it only opens a new one if the user explicitly agrees.
/// </summary>
public static class ShiftFlow
{
    public const string DefaultTerminalCode = "POS-01";

    /// <summary>True when the session now has a verified open shift owned by the current user.</summary>
    public static async Task<bool> EnsureOpenShiftAsync()
    {
        var services = App.Services;
        var session = services.GetRequiredService<CurrentUserSession>();
        var text = LocalizationManager.Current;

        if (!session.IsAuthenticated || session.UserId is null)
        {
            MessageBox.Show(text["ShiftRequired"], text["OpenShiftTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var restored = await services.GetRequiredService<ActiveShiftService>().RestoreAsync();
        if (restored is not null)
            return true;

        var permissions = services.GetRequiredService<PermissionChecker>();

        // Admin may sell without a shift or opening float.
        if (permissions.HasPermission(PermissionCodes.Sales.SellWithoutShift))
            return true;

        if (!permissions.HasPermission(PermissionCodes.Shifts.Open))
        {
            MessageBox.Show(text["PermissionDenied"], text["OpenShiftTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var answer = MessageBox.Show(text["OpenShiftNow"], text["OpenShiftTitle"],
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
            return false;

        string input = Interaction.InputBox(text["OpeningFloatPrompt"], text["OpenShiftTitle"], "0");
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal openingFloat)
            || openingFloat < 0m || decimal.Round(openingFloat, 2) != openingFloat)
        {
            MessageBox.Show(text["OpeningFloatInvalid"], text["OpenShiftTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        try
        {
            var terminal = await services.GetRequiredService<IPosTerminalRepository>().GetByCodeAsync(DefaultTerminalCode);
            if (terminal is null || !terminal.IsActive)
            {
                MessageBox.Show(text["TerminalMissing"], text["OpenShiftTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            int shiftId = await services.GetRequiredService<OpenCashShiftService>()
                .ExecuteAsync(session.UserId.Value, terminal.Id, openingFloat);
            session.SetActiveShift(shiftId, terminal.Id);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ErrorMessageMapper.ToUserMessage(ex), text["OpenShiftTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
