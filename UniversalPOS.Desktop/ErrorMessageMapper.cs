using System;
using UniversalPOS.Application.Common;

namespace UniversalPOS.Desktop;

/// <summary>Turns exceptions into short, localized messages. Technical detail goes to the trace log, never to the user.</summary>
public static class ErrorMessageMapper
{
    public static string ToUserMessage(Exception ex)
    {
        System.Diagnostics.Trace.WriteLine(ex.ToString());
        var text = LocalizationManager.Current;

        return ServiceErrors.GetCode(ex) switch
        {
            ServiceErrors.ShiftRequired or ServiceErrors.ShiftNotOpen => text["ShiftRequired"],
            ServiceErrors.ShiftNotOwned => text["ShiftNotOwned"],
            ServiceErrors.InsufficientStock => text["InsufficientStock"],
            ServiceErrors.PermissionDenied => text["PermissionDenied"],
            _ => ex switch
            {
                UnauthorizedAccessException => text["PermissionDenied"],
                // Domain/business-rule messages are written for people; database failures are not.
                InvalidOperationException or ArgumentException => ex.Message,
                _ => text["SaveFailed"]
            }
        };
    }
}
